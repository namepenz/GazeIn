using UnityEngine;
using Vuplex.WebView;

public class WebPanelController : MonoBehaviour
{
    [SerializeField] private CanvasWebViewPrefab _canvasWebView;
    [SerializeField] private string _initialUrl = "https://example.com";

    public string CurrentUrl { get; private set; }

    async void Start()
    {
        if (_canvasWebView == null)
            _canvasWebView = GetComponent<CanvasWebViewPrefab>();

        await _canvasWebView.WaitUntilInitialized();

        _canvasWebView.WebView.UrlChanged += (_, e) =>
        {
            CurrentUrl = e.Url;
            Debug.Log($"[WebPanel] URL changed: {CurrentUrl}");
        };

        _canvasWebView.WebView.LoadUrl(_initialUrl);
        CurrentUrl = _initialUrl;
        Debug.Log($"[WebPanel] 초기 URL 로드: {_initialUrl}");
    }

    public void NavigateTo(string url)
    {
        _canvasWebView.WebView?.LoadUrl(url);
    }

    public async System.Threading.Tasks.Task<string> GetVisibleElementsAsync()
    {
        if (_canvasWebView.WebView == null) return null;

        return await _canvasWebView.WebView.ExecuteJavaScript(@"
            (function() {
                var els = document.querySelectorAll(
                    'p,h1,h2,h3,h4,h5,h6,li,span,a,img,video,code,pre,td,th'
                );
                var r = [];
                els.forEach(function(el) {
                    var rect = el.getBoundingClientRect();
                    if (rect.width > 0 && rect.height > 0) {
                        r.push({
                            tag: el.tagName,
                            text: (el.innerText||'').substring(0,100),
                            x: rect.x,
                            y: rect.y,
                            w: rect.width,
                            h: rect.height
                        });
                    }
                });
                return JSON.stringify(r);
            })();
        ");
    }
}
