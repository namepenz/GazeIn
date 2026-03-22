using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class GazeDataFeeder : MonoBehaviour
{
    [Header("Meta SDK References")]
    public OVREyeGaze leftEyeGaze;
    public OVREyeGaze rightEyeGaze;
    public OVRFaceExpressions faceExpressions;

    [Header("FastAPI Config")]
    public string endpoint = "http://3.34.96.238:8000/ingest";

    private const float SACCADE_THRESHOLD = 100f;
    private const int   MIN_SAMPLES       = 15;

    private ConcurrentQueue<GazeChunk> _sendQueue = new();
    private List<GazeDataPoint>        _buffer    = new();
    private Vector3 _lastDir;
    private double  _lastTime;
    private bool    _wasSaccade = false;

    private void Start()
    {
        Debug.Log("[GazeFeeder] Start() 호출됨");

        if (faceExpressions == null)
            faceExpressions = GetComponentInParent<OVRFaceExpressions>();
        if (faceExpressions == null)
            faceExpressions = FindObjectOfType<OVRFaceExpressions>();

        Debug.Log($"[GazeFeeder] leftEye={leftEyeGaze}, rightEye={rightEyeGaze}, face={faceExpressions}");
    }

    private void Update()
    {
        // 큐에서 꺼내서 전송
        if (_sendQueue.TryDequeue(out var chunk))
        {
            StartCoroutine(PostChunk(chunk));
        }

        if (!OVRPlugin.eyeTrackingEnabled) return;
        if (leftEyeGaze == null || rightEyeGaze == null) return;

        Vector3 leftDir  = leftEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 rightDir = rightEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 avgDir   = ((leftDir + rightDir) * 0.5f).normalized;

        double now = Time.realtimeSinceStartupAsDouble;

        var sample = new GazeDataPoint
        {
            timestamp            = now,
            left_gaze_direction  = new float[] { leftDir.x,  leftDir.y,  leftDir.z  },
            right_gaze_direction = new float[] { rightDir.x, rightDir.y, rightDir.z },
            left_openness        = faceExpressions != null
                ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedL)
                : 1f,
            right_openness       = faceExpressions != null
                ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedR)
                : 1f,
            face_blend_shapes    = GetFaceWeights()
        };
        _buffer.Add(sample);

        float dt = (float)(now - _lastTime);
        if (dt > 0 && _lastDir != Vector3.zero)
        {
            float velocity            = Vector3.Angle(_lastDir, avgDir) / dt;
            bool  isSaccade           = velocity > SACCADE_THRESHOLD;
            bool  fixationJustStarted = _wasSaccade && !isSaccade;

            if (fixationJustStarted && _buffer.Count >= MIN_SAMPLES)
            {
                Debug.Log($"[GazeFeeder] 청크 생성 samples={_buffer.Count}");
                _sendQueue.Enqueue(new GazeChunk
                {
                    chunkId     = Guid.NewGuid().ToString("N"),
                    startTime   = _buffer[0].timestamp,
                    endTime     = _buffer[^1].timestamp,
                    triggerType = "fixation_start",
                    samples     = _buffer.ToArray()
                });
                _buffer.Clear();
            }
            _wasSaccade = isSaccade;
        }

        _lastDir  = avgDir;
        _lastTime = now;
    }

    private IEnumerator PostChunk(GazeChunk chunk)
    {
        string json = JsonConvert.SerializeObject(chunk);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log($"[GazeFeeder] 전송 시작: {chunk.chunkId}");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[GazeFeeder] 전송 실패: {request.error}");
        else
            Debug.Log($"[GazeFeeder] 전송 성공! 응답: {request.downloadHandler.text}");
    }

    private float GetSingleFaceWeight(OVRFaceExpressions.FaceExpression expr)
    {
        if (faceExpressions == null) return 0f;
        faceExpressions.TryGetFaceExpressionWeight(expr, out float w);
        return w;
    }

    private float[] GetFaceWeights()
    {
        var w = new float[63];
        if (faceExpressions == null) return w;
        for (int i = 0; i < 63; i++)
            faceExpressions.TryGetFaceExpressionWeight(
                (OVRFaceExpressions.FaceExpression)i, out w[i]);
        return w;
    }
}

[Serializable]
public struct GazeDataPoint
{
    public double  timestamp;
    public float[] left_gaze_direction;
    public float[] right_gaze_direction;
    public float   left_openness;
    public float   right_openness;
    public float[] face_blend_shapes;
}

[Serializable]
public class GazeChunk
{
    public string          chunkId;
    public double          startTime;
    public double          endTime;
    public string          triggerType;
    public GazeDataPoint[] samples;
}