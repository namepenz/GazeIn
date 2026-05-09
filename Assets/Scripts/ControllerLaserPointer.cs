using UnityEngine;
using UnityEngine.EventSystems;
using Vuplex.WebView;

public class ControllerLaserPointer : MonoBehaviour
{
    [Header("Ray 설정")]
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private Color startColor = new Color(0f, 1f, 1f, 1f);
    [SerializeField] private Color endColor = new Color(0f, 1f, 1f, 0f);
    [SerializeField] private float startWidth = 0.005f;
    [SerializeField] private float endWidth = 0.001f;

    [Header("컨트롤러 지정")]
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Vuplex")]
    [SerializeField] private CanvasWebViewPrefab canvasWebView;
    [SerializeField] private RectTransform canvasRect;

    private LineRenderer lr;
    private OVRInputModule inputModule;
    private GameObject _hitPoint;
    private bool _webViewReady = false;   // ★ 초기화 완료 플래그

    void Start()
    {
        lr = gameObject.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.startColor = startColor;
        lr.endColor = endColor;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = true;

        inputModule = Object.FindFirstObjectByType<OVRInputModule>();
        if (controller == OVRInput.Controller.RTouch && inputModule != null)
            inputModule.rayTransform = this.transform;

        _hitPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _hitPoint.name = "LaserHitPoint";
        _hitPoint.transform.localScale = Vector3.one * 0.015f;
        Destroy(_hitPoint.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0f, 1f, 1f, 0.8f);
        _hitPoint.GetComponent<Renderer>().material = mat;
        _hitPoint.SetActive(false);

        // ★ 초기화는 Start에서 한 번만
        if (canvasWebView != null)
            StartCoroutine(WaitForWebView());
    }

    private System.Collections.IEnumerator WaitForWebView()
    {
        var task = canvasWebView.WaitUntilInitialized();
        yield return new WaitUntil(() => task.IsCompleted);
        _webViewReady = true;
    }

    void Update()
    {
        bool isActive = OVRInput.IsControllerConnected(controller);
        lr.enabled = isActive;

        if (!isActive)
        {
            _hitPoint.SetActive(false);
            return;
        }

        Ray ray = new Ray(transform.position, transform.forward);
        bool isHitting = false;
        float dist = maxDistance;
        Vector3 hitPos = transform.position + transform.forward * maxDistance;

        if (canvasRect != null)
        {
            Plane canvasPlane = new Plane(-canvasRect.forward, canvasRect.position);
            if (canvasPlane.Raycast(ray, out float enter) && enter <= maxDistance)
            {
                isHitting = true;
                dist = enter;
                hitPos = ray.GetPoint(enter);
            }
        }

        if (!isHitting && Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            isHitting = true;
            dist = hit.distance;
            hitPos = hit.point;
        }

        lr.SetPosition(0, transform.position);
        lr.SetPosition(1, transform.position + transform.forward * dist);

        if (isHitting)
        {
            _hitPoint.SetActive(true);
            _hitPoint.transform.position = hitPos + (transform.position - hitPos).normalized * 0.005f;
        }
        else
        {
            _hitPoint.SetActive(false);
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller))
        {
            Debug.Log($"[{controller}] 트리거 클릭");
            Debug.Log(isHitting ? $"Hit at: {hitPos}" : "Ray가 아무것도 못 맞춤");
        }

        // ★ 초기화 완료된 경우에만 뒤로가기 실행
        if (OVRInput.GetDown(OVRInput.Button.Two) && _webViewReady)
            GoBack();
    }

    private async void GoBack()
    {
        try
        {
            if (canvasWebView?.WebView == null) return;
            if (await canvasWebView.WebView.CanGoBack())
            {
                canvasWebView.WebView.GoBack();
                Debug.Log("[Laser] 뒤로가기 실행");
            }
        }
        // using System; 제거하고 GoBack()에서 아래처럼 변경
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Laser] GoBack 실패: {e.Message}");
        }

    }
}
