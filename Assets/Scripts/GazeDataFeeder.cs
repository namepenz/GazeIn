using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Vuplex.WebView;

public class GazeDataFeeder : MonoBehaviour
{
    [Header("Meta SDK References")]
    public OVREyeGaze leftEyeGaze;
    public OVREyeGaze rightEyeGaze;
    public OVRFaceExpressions faceExpressions;

    [Header("Vuplex References")]
    public CanvasWebViewPrefab canvasWebView;
    public RectTransform canvasRect;
    public int browserWidth = 1920;
    public int browserHeight = 1080;

    [Header("UI Positioning")]
    public float uiDistance = 2.0f; // 화면 띄울 거리
    public bool smoothFollow = false; // 시선 따라다니게 할지 여부
    public float followLerpSpeed = 3f;

    [Header("FastAPI Config")]
    public string endpoint = "http://3.35.207.124:8000/ingest";

    [Header("User Info")]
    public string userId = "user_123";
    private string _sessionId;

    private const float SACCADE_THRESHOLD = 100f;
    private const int MIN_SAMPLES = 15;
    private const int MAX_BUFFER_SIZE = 100; // 최대 버퍼 크기 상수화

    private ConcurrentQueue<GazeChunk> _sendQueue = new();
    private List<GazeDataPoint> _buffer = new();
    private Vector3 _lastDir;
    private double _lastTime;
    private bool _wasSaccade = false;

    public void RecenterUI()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 targetPos = cam.transform.position + cam.transform.forward * uiDistance;
            Quaternion targetRot = Quaternion.LookRotation(targetPos - cam.transform.position);
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
        else
        {
            transform.position = new Vector3(0, 1.5f, uiDistance);
            transform.rotation = Quaternion.identity;
        }
    }
    private string _currentUrl = "";

    public TutorManager tutorManager;

    private void Start()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        Debug.Log($"[GazeFeeder] Start() 호출됨 / SessionID: {_sessionId}");

        if (faceExpressions == null)
            faceExpressions = GetComponentInParent<OVRFaceExpressions>();
        if (faceExpressions == null)
            faceExpressions = FindFirstObjectByType<OVRFaceExpressions>();

        if (canvasWebView != null)
            StartCoroutine(InitRoutine());  // ← 이것만 호출

        Debug.Log($"[GazeFeeder] leftEye={leftEyeGaze}, rightEye={rightEyeGaze}, face={faceExpressions}");

        transform.SetParent(null);

        if (canvasWebView != null)
        {
            canvasWebView.Native2DModeEnabled = false;
        }

        transform.position = new Vector3(0, 1.5f, 50f);
        transform.rotation = Quaternion.identity;
    }

    private IEnumerator InitRoutine()
    {
        // WaitUntilInitialized()는 Task → yield로 대기
        var task = canvasWebView.WaitUntilInitialized();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogError($"[GazeFeeder] WebView 초기화 실패: {task.Exception}");
            yield break;
        }

        canvasWebView.WebView.SetFocused(true);

        if (faceExpressions == null)
            faceExpressions = GetComponentInParent<OVRFaceExpressions>();
        if (faceExpressions == null)
            faceExpressions = FindFirstObjectByType<OVRFaceExpressions>();

        // UrlChanged 먼저 등록 후 LoadUrl
        canvasWebView.WebView.UrlChanged += (_, e) =>
        {
            _currentUrl = e.Url;
            Debug.Log($"[GazeFeeder] URL 변경: {_currentUrl}");
        };
        canvasWebView.WebView.LoadUrl("https://www.youtube.com/");
    }

    // ── GoBack — async void 유지하되 예외 처리 추가 ──────────────
    public async void GoBack()
    {
        try
        {
            if (canvasWebView?.WebView == null) return;
            bool canGoBack = await canvasWebView.WebView.CanGoBack();
            if (canGoBack)
                canvasWebView.WebView.GoBack();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GazeFeeder] GoBack 실패: {e.Message}");
        }
    }

    private void Update()
    {
        // 화면 재정렬 로직 (A 버튼 또는 X 버튼 누르면 눈앞으로 화면 이동)
        if (OVRInput.GetDown(OVRInput.Button.One))
        {
            RecenterUI();
        }

        // 부드럽게 시선 따라가기 로직 (인스펙터에서 켤 수 있음)
        if (smoothFollow)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 targetPos = cam.transform.position + cam.transform.forward * uiDistance;
                Quaternion targetRot = Quaternion.LookRotation(targetPos - cam.transform.position);
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followLerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followLerpSpeed);
            }
        }

        // 큐에 쌓인 데이터 전송 처리
        if (_sendQueue.TryDequeue(out var chunk))
            StartCoroutine(PostChunk(chunk));

        if (!OVRPlugin.eyeTrackingEnabled) return;
        if (leftEyeGaze == null || rightEyeGaze == null) return;

        // 1. 시선 데이터 계산
        Vector3 leftDir = leftEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 rightDir = rightEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 avgDir = ((leftDir + rightDir) * 0.5f).normalized;
        double now = Time.realtimeSinceStartupAsDouble;

        // 2. 브라우저 픽셀 좌표 계산 (Canvas Plane Intersection)
        Vector3 gazeOrigin = (leftEyeGaze.transform.position + rightEyeGaze.transform.position) * 0.5f;
        float px = -1f, py = -1f;
        bool hitCanvas = false;

        if (canvasRect != null)
        {
            Plane canvasPlane = new Plane(canvasRect.forward, canvasRect.position);
            Ray gazeRay = new Ray(gazeOrigin, avgDir);

            if (canvasPlane.Raycast(gazeRay, out float enter))
            {
                Vector3 hitPoint = gazeRay.GetPoint(enter);
                Vector3 localHit = canvasRect.InverseTransformPoint(hitPoint);

                float nx = (localHit.x / canvasRect.rect.width) + canvasRect.pivot.x;
                float ny = (localHit.y / canvasRect.rect.height) + canvasRect.pivot.y;

                if (nx >= 0f && nx <= 1f && ny >= 0f && ny <= 1f)
                {
                    px = nx * browserWidth;
                    py = (1f - ny) * browserHeight;
                    hitCanvas = true;
                }
            }
        }

        // 3. 샘플 생성 및 버퍼 추가
        var sample = new GazeDataPoint
        {
            timestamp = now,
            left_gaze_direction = new float[] { leftDir.x, leftDir.y, leftDir.z },
            right_gaze_direction = new float[] { rightDir.x, rightDir.y, rightDir.z },
            left_openness = faceExpressions != null ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedL) : 1f,
            right_openness = faceExpressions != null ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedR) : 1f,
            face_blend_shapes = GetMappedBlendShapes(),
            browser_pixel_x = px,
            browser_pixel_y = py,
            hit_canvas = hitCanvas
        };
        _buffer.Add(sample);

        // 4. 전송 트리거 판단 (Fixation 시작 또는 버퍼 가득 참)
        float dt = (float)(now - _lastTime);
        if (dt > 0 && _lastDir != Vector3.zero)
        {
            float velocity = Vector3.Angle(_lastDir, avgDir) / dt;
            bool isSaccade = velocity > SACCADE_THRESHOLD;
            bool fixationJustStarted = _wasSaccade && !isSaccade;
            bool bufferFull = _buffer.Count >= MAX_BUFFER_SIZE;

            // 수정된 조건문: 고정이 시작되었거나 버퍼가 100개 이상일 때 (최소 샘플 수 만족 시)
            if ((fixationJustStarted || bufferFull) && _buffer.Count >= MIN_SAMPLES)
            {
                string triggerName = fixationJustStarted ? "fixation_start" : "buffer_full";
                Debug.Log($"[GazeFeeder] 청크 생성 trigger={triggerName}, samples={_buffer.Count}");

                _sendQueue.Enqueue(new GazeChunk
                {
                    chunkId = Guid.NewGuid().ToString("N"),
                    userId = this.userId,
                    sessionId = this._sessionId,
                    startTime = _buffer[0].timestamp,
                    endTime = _buffer[^1].timestamp,
                    triggerType = triggerName,
                    url = _currentUrl,
                    samples = _buffer.ToArray()
                });
                _buffer.Clear();
            }
            _wasSaccade = isSaccade;
        }

        _lastDir = avgDir;
        _lastTime = now;
    }

    private void FlushBuffer(string trigger)
    {
        if (_buffer.Count == 0) return;
        _sendQueue.Enqueue(new GazeChunk
        {
            chunkId = Guid.NewGuid().ToString("N"),
            userId = this.userId,
            sessionId = this._sessionId,
            startTime = _buffer[0].timestamp,
            endTime = _buffer[^1].timestamp,
            triggerType = trigger,
            url = _currentUrl,
            samples = _buffer.ToArray()
        });
        _buffer.Clear();
    }

    private IEnumerator PostChunk(GazeChunk chunk)
    {
        string json = JsonConvert.SerializeObject(chunk);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log($"[GazeFeeder] 전송 시작: {chunk.chunkId} ({chunk.triggerType})");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[GazeFeeder] 전송 실패: {request.error}");
            yield break;
        }

        Debug.Log($"[GazeFeeder] 전송 성공! 응답: {request.downloadHandler.text}");

        // ★ 튜터 응답 처리
        try
        {
            var response = JsonConvert.DeserializeObject<IngestResponse>(
                request.downloadHandler.text);

            if (response?.tutor != null &&
                !string.IsNullOrEmpty(response.tutor.message) &&
                tutorManager != null)
            {
                Debug.Log($"[GazeFeeder] 졸음 감지 → 튜터 호출: {response.tutor.message}");
                tutorManager.ShowTutorFromServer(response.tutor.message, response.tutor.audio_url);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GazeFeeder] 응답 파싱 실패: {e.Message}");
        }
    }

    private float GetSingleFaceWeight(OVRFaceExpressions.FaceExpression expr)
    {
        if (faceExpressions == null) return 0f;
        faceExpressions.TryGetFaceExpressionWeight(expr, out float w);
        return w;
    }

    // 매핑 테이블 인덱스만 골라서 21개 담음
    private static readonly int[] BS_INDICES = { 0, 1, 2, 3, 4, 5, 12, 13, 14, 15, 18, 19, 20, 21, 24, 25, 38, 39, 51, 54, 55 };

    private float[] GetMappedBlendShapes()
    {
        var result = new float[21];
        if (faceExpressions == null) return result;
        for (int i = 0; i < BS_INDICES.Length; i++)
            faceExpressions.TryGetFaceExpressionWeight(
                (OVRFaceExpressions.FaceExpression)BS_INDICES[i], out result[i]);
        return result;
    }
}

[Serializable]
public struct GazeDataPoint
{
    public double timestamp;
    public float[] left_gaze_direction;
    public float[] right_gaze_direction;
    public float left_openness;
    public float right_openness;
    public float[] face_blend_shapes;
    public float browser_pixel_x;
    public float browser_pixel_y;
    public bool hit_canvas;
}

[Serializable]
public class GazeChunk
{
    public string chunkId;
    public string userId;
    public string sessionId;
    public double startTime;
    public double endTime;
    public string triggerType;
    public string url;
    public GazeDataPoint[] samples;
}

// 응답 구조체 추가 (파일 하단)
[Serializable]
public class IngestResponse
{
    public string status;
    public string chunkId;
    public PerclosResult perclos;
    public TutorResult tutor;
}

[Serializable]
public class PerclosResult
{
    public int state;
    public float perclos;
    public bool trigger_tutor;
}

[Serializable]
public class TutorResult
{
    public string message;
    public string audio_url;
}