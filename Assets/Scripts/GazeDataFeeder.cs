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
    private float _forceFlushTimer = 0f;
    private const float FORCE_FLUSH_INTERVAL = 3.0f;

    [Header("Meta SDK References")]
    public OVREyeGaze leftEyeGaze;
    public OVREyeGaze rightEyeGaze;
    public OVRFaceExpressions faceExpressions;

    [Header("Vuplex References")]
    public CanvasWebViewPrefab canvasWebView;
    public RectTransform canvasRect;
    public int browserWidth  = 1920;
    public int browserHeight = 1080;

    [Header("FastAPI Config")]
    public string serverBase = "http://43.203.180.90:8000";
    public string endpoint   = "http://43.203.180.90:8000/ingest";

    [Header("User Info")]
    public string userId    = "user_001";
    public string sessionId = "";

    private const float SACCADE_THRESHOLD = 100f;
    private const int   MIN_SAMPLES       = 15;

    private ConcurrentQueue<GazeChunk> _sendQueue = new();
    private List<GazeDataPoint>        _buffer    = new();
    private Vector3 _lastDir;
    private double  _lastTime;
    private bool    _wasSaccade  = false;
    private string  _currentUrl  = "";
    private float   _yButtonHoldTime = 0f;
    private float   _xButtonHoldTime = 0f;
    public bool     isInitialized { get; private set; } = false;

    public bool isCalibrating = false;
    public bool isCalibrated = false;
    private List<GazeDataPoint> _calibrationBuffer = new();

    private readonly int[] targetIndices = {
        0, 1, 2, 3, 4, 5, 12, 13, 14, 15, 18, 19, 20, 21, 24, 25, 38, 39, 51, 54, 55
    };

    public TutorManager tutorManager;

    private void Awake()
    {
        // 안드로이드 네이티브 비디오 플레이어로 인한 텍스처 렌더링 멈춤 방지를 위해 데스크탑 User-Agent 적용
#if UNITY_ANDROID
        Vuplex.WebView.Web.SetUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
#endif
    }

    private void Start()
    {
        sessionId = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (canvasWebView == null)
        {
            Debug.LogError("[GazeFeeder] canvasWebView 미연결!");
            return;
        }

        // FaceExpressions 자동 탐색
        if (faceExpressions == null)
            faceExpressions = GetComponentInParent<OVRFaceExpressions>();
        if (faceExpressions == null)
            faceExpressions = FindFirstObjectByType<OVRFaceExpressions>();

        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        // 비동기 서버 세션 초기화
        StartCoroutine(ResetServerSession());

        // VR 트래킹 안정화 대기
        yield return new WaitForSeconds(1.0f);

        // 웹뷰 초기화 완료 대기
        var task = canvasWebView.WaitUntilInitialized();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogError($"[GazeFeeder] WebView 초기화 실패: {task.Exception}");
            yield break;
        }

        canvasWebView.WebView.SetFocused(true);

        // 웹뷰 내 전체화면 모드 지원을 위해 브라우저의 전체화면 API를 가상(Mock)으로 구현
        canvasWebView.WebView.PageLoadScripts.Add(@"
            if (!window.fakeFsInit) {
                window.fakeFsInit = true;

                // 1. 가상 전체화면 상태 변수
                window.__fakeFsElement = null;

                // 2. fullscreenElement 속성 오버라이드
                Object.defineProperty(document, 'fullscreenElement', {
                    get: function() { return window.__fakeFsElement; }
                });
                Object.defineProperty(document, 'fullscreenEnabled', {
                    get: function() { return true; }
                });

                // 3. requestFullscreen 메서드 오버라이드
                Element.prototype.requestFullscreen = function() {
                    window.__fakeFsElement = this;
                    this.classList.add('fake-fullscreen');
                    document.dispatchEvent(new Event('fullscreenchange'));
                    window.dispatchEvent(new Event('resize'));
                    return Promise.resolve();
                };

                // 4. exitFullscreen 메서드 오버라이드
                document.exitFullscreen = function() {
                    if (window.__fakeFsElement) {
                        window.__fakeFsElement.classList.remove('fake-fullscreen');
                    }
                    window.__fakeFsElement = null;
                    document.dispatchEvent(new Event('fullscreenchange'));
                    window.dispatchEvent(new Event('resize'));
                    return Promise.resolve();
                };

                // 5. 전체화면 CSS 주입
                var style = document.createElement('style');
                style.innerHTML = `
                    .fake-fullscreen {
                        position: fixed !important;
                        top: 0 !important;
                        left: 0 !important;
                        width: 100vw !important;
                        height: 100vh !important;
                        z-index: 2147483647 !important; /* 최대 z-index */
                        background-color: black !important;
                        margin: 0 !important;
                        padding: 0 !important;
                        transform: none !important;
                        border-radius: 0 !important;
                    }
                    .fake-fullscreen video {
                        width: 100% !important;
                        height: 100% !important;
                        object-fit: contain !important;
                    }
                `;
                document.head.appendChild(style);

                // 6. 유튜브 UI 버튼 클릭 이벤트 연동
                var eventsToBlock = ['click', 'touchend'];
                eventsToBlock.forEach(function(evt) {
                    document.addEventListener(evt, function(e) {
                        var btn = e.target.closest('.ytp-fullscreen-button');
                        if (btn) {
                            e.preventDefault();
                            e.stopPropagation();
                            e.stopImmediatePropagation();
                            
                            var player = document.querySelector('.html5-video-player');
                            if (player) {
                                if (document.fullscreenElement) {
                                    document.exitFullscreen();
                                } else {
                                    player.requestFullscreen();
                                }
                            }
                        }
                    }, true);
                });
            }
        ");

        canvasWebView.WebView.UrlChanged += (_, e) =>
        {
            _currentUrl = e.Url;
            Debug.Log($"[GazeFeeder] URL: {_currentUrl}");
        };

        isInitialized = true;
        Debug.Log("[GazeFeeder] 초기화 완료 sessionId=" + sessionId);
    }

    private IEnumerator ResetServerSession()
    {
        // 네트워크 라이브러리 충돌 방지를 위해 빈 JSON 데이터 전송
        byte[] emptyJson = Encoding.UTF8.GetBytes("{}");
        using var req = new UnityWebRequest(serverBase + "/session/reset", "POST")
        {
            uploadHandler   = new UploadHandlerRaw(emptyJson),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        Debug.Log(req.result == UnityWebRequest.Result.Success
            ? "[GazeFeeder] 서버 세션 리셋 완료"
            : $"[GazeFeeder] 서버 리셋 실패: {req.error}");
    }

    public async void GoBack()
    {
        try
        {
            if (canvasWebView?.WebView == null) return;
            if (await canvasWebView.WebView.CanGoBack())
                canvasWebView.WebView.GoBack();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GazeFeeder] GoBack 실패: {e.Message}");
        }
    }

    private void Update()
    {
        if (!isInitialized) return;

        // Y버튼 3초 유지 시 초기 화면으로 복귀
        if (OVRInput.Get(OVRInput.Button.Four))
        {
            _yButtonHoldTime += Time.deltaTime;
            if (_yButtonHoldTime >= 3.0f)
            {
                Debug.Log("[GazeFeeder] Y버튼 3초 이상 홀드됨 -> 초기 사이트로 복귀");
                if (canvasWebView != null && canvasWebView.WebView != null)
                {
                    canvasWebView.WebView.LoadUrl("https://www.youtube.com/");
                }
                _yButtonHoldTime = -9999f; // 중복 실행 방지
            }
        }
        else
        {
            if (_yButtonHoldTime < 0f) _yButtonHoldTime = 0f; // 초기화
            else _yButtonHoldTime = 0f;
        }

        // X버튼 3초 유지 시 캘리브레이션 재시작
        if (OVRInput.Get(OVRInput.Button.Three))
        {
            _xButtonHoldTime += Time.deltaTime;
            if (_xButtonHoldTime >= 3.0f)
            {
                Debug.Log("[GazeFeeder] X버튼 3초 이상 홀드됨 -> 캘리브레이션 재시작");
                var calibManager = FindFirstObjectByType<CalibrationManager>();
                if (calibManager != null)
                {
                    calibManager.RestartCalibration();
                }
                _xButtonHoldTime = -9999f;
            }
        }
        else
        {
            if (_xButtonHoldTime < 0f) _xButtonHoldTime = 0f;
            else _xButtonHoldTime = 0f;
        }

        while (_sendQueue.TryDequeue(out var chunk))
            StartCoroutine(PostChunk(chunk));

        if (!OVRPlugin.eyeTrackingEnabled) return;
        if (leftEyeGaze == null || rightEyeGaze == null) return;

        Vector3 leftDir  = leftEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 rightDir = rightEyeGaze.transform.TransformDirection(Vector3.forward);
        Vector3 avgDir   = ((leftDir + rightDir) * 0.5f).normalized;

        double  now        = Time.realtimeSinceStartupAsDouble;
        Vector3 gazeOrigin = (leftEyeGaze.transform.position + rightEyeGaze.transform.position) * 0.5f;

        float px = -1f, py = -1f;
        bool  hitCanvas = false;

        // Raycast를 이용해 웹뷰 캔버스와의 교차점 계산
        if (canvasRect != null && Physics.Raycast(gazeOrigin, avgDir, out RaycastHit hit, 15f))
        {
            Vector3 localHit = canvasRect.InverseTransformPoint(hit.point);
            float nx = (localHit.x / canvasRect.rect.width)  + canvasRect.pivot.x;
            float ny = (localHit.y / canvasRect.rect.height) + canvasRect.pivot.y;

            if (nx >= 0f && nx <= 1f && ny >= 0f && ny <= 1f)
            {
                px        = nx * browserWidth;
                py        = (1f - ny) * browserHeight;
                hitCanvas = true;
            }
        }

        var sample = new GazeDataPoint
        {
            timestamp            = now,
            left_gaze_direction  = new float[] { leftDir.x,  leftDir.y,  leftDir.z  },
            right_gaze_direction = new float[] { rightDir.x, rightDir.y, rightDir.z },
            left_openness        = faceExpressions != null
                ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedL) : 1f,
            right_openness       = faceExpressions != null
                ? 1f - GetSingleFaceWeight(OVRFaceExpressions.FaceExpression.EyesClosedR) : 1f,
            face_blend_shapes    = GetFaceWeights(),
            browser_pixel_x      = px,
            browser_pixel_y      = py,
            hit_canvas           = hitCanvas
        };

        if (isCalibrating)
        {
            _calibrationBuffer.Add(sample);
            return;
        }

        if (!isCalibrated) return;

        _buffer.Add(sample);

        bool flushed = false;
        _forceFlushTimer += Time.deltaTime;
        if (_forceFlushTimer >= FORCE_FLUSH_INTERVAL && _buffer.Count >= MIN_SAMPLES)
        {
            FlushBuffer("force_flush");
            flushed = true;
        }

        if (!flushed)
        {
            float dt = (float)(now - _lastTime);
            if (dt > 0 && _lastDir != Vector3.zero)
            {
                float velocity  = Vector3.Angle(_lastDir, avgDir) / dt;
                bool  isSaccade = velocity > SACCADE_THRESHOLD;
                if (_wasSaccade && !isSaccade && _buffer.Count >= MIN_SAMPLES)
                    FlushBuffer("fixation_start");
                _wasSaccade = isSaccade;
            }
        }

        _lastDir  = avgDir;
        _lastTime = now;
    }

    private void FlushBuffer(string trigger)
    {
        if (_buffer.Count == 0) return;
        _forceFlushTimer = 0f;
        _sendQueue.Enqueue(new GazeChunk
        {
            chunkId     = Guid.NewGuid().ToString("N"),
            userId      = this.userId,
            sessionId   = this.sessionId,
            startTime   = _buffer[0].timestamp,
            endTime     = _buffer[^1].timestamp,
            triggerType = trigger,
            url         = _currentUrl,
            samples     = _buffer.ToArray()
        });
        _buffer.Clear();
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
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[GazeFeeder] 전송 실패: {request.error}");
            yield break;
        }

        try
        {
            var resp = JsonConvert.DeserializeObject<IngestResponse>(request.downloadHandler.text);
            if (resp?.tutor != null && !string.IsNullOrEmpty(resp.tutor.message) && tutorManager != null)
            {
                // 응답에 포함된 트리거 상태값을 기반으로 튜터 애니메이션 실행
                int trigState = resp.tutor.trigger_state;
                tutorManager.ShowTutorFromServer(resp.tutor.message, resp.tutor.audio_url, trigState);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GazeFeeder] 파싱 오류: {e.Message}");
        }
    }

    private float GetSingleFaceWeight(OVRFaceExpressions.FaceExpression expr)
    {
        if (faceExpressions == null) return 0f;
        faceExpressions.TryGetFaceExpressionWeight(expr, out float w);
        return w;
    }

    private float[] GetFaceWeights()
    {
        var w = new float[21];
        if (faceExpressions == null) return w;
        for (int i = 0; i < targetIndices.Length; i++)
            faceExpressions.TryGetFaceExpressionWeight(
                (OVRFaceExpressions.FaceExpression)targetIndices[i], out w[i]);
        return w;
    }

    public void StartCalibration()
    {
        isCalibrating = true;
        isCalibrated = false;
        _calibrationBuffer.Clear();
    }

    public IEnumerator FinishCalibrationRoutine()
    {
        isCalibrating = false;
        
        if (_calibrationBuffer.Count == 0)
        {
            Debug.LogWarning("[GazeFeeder] 캘리브레이션 데이터가 없습니다.");
            isCalibrated = true;
            yield break;
        }

        var chunk = new GazeChunk
        {
            chunkId     = Guid.NewGuid().ToString("N"),
            userId      = this.userId,
            sessionId   = this.sessionId,
            startTime   = _calibrationBuffer[0].timestamp,
            endTime     = _calibrationBuffer[^1].timestamp,
            triggerType = "calibration",
            url         = _currentUrl,
            samples     = _calibrationBuffer.ToArray()
        };

        string json = JsonConvert.SerializeObject(chunk);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(serverBase + "/calibrate", "POST")
        {
            uploadHandler   = new UploadHandlerRaw(body),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[GazeFeeder] 캘리브레이션 전송 실패: {request.error}");
        }
        else
        {
            Debug.Log($"[GazeFeeder] 캘리브레이션 성공: {request.downloadHandler.text}");
        }

        _calibrationBuffer.Clear();
        isCalibrated = true;
    }
}

[System.Serializable]
public struct GazeDataPoint
{
    public double  timestamp;
    public float[] left_gaze_direction;
    public float[] right_gaze_direction;
    public float   left_openness;
    public float   right_openness;
    public float[] face_blend_shapes;
    public float   browser_pixel_x;
    public float   browser_pixel_y;
    public bool    hit_canvas;
}

[System.Serializable]
public class GazeChunk
{
    public string          chunkId;
    public string          userId;
    public string          sessionId;
    public double          startTime;
    public double          endTime;
    public string          triggerType;
    public string          url;
    public GazeDataPoint[] samples;
}

[System.Serializable]
public class IngestResponse
{
    public string        status;
    public string        chunkId;
    public PerclosResult perclos;
    public TutorResult   tutor;
}

[System.Serializable]
public class PerclosResult
{
    public int   state;
    public float perclos;
    public float mean_openness;
    public float gaze_out_ratio;
    public bool  trigger_tutor;
    public int   trigger_state;
}

[System.Serializable]
public class TutorResult
{
    public string message;
    public string audio_url;
    public int    trigger_state; // 백엔드에서 전송한 트리거 상태값
}