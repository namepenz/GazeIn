using UnityEngine;
using System.IO;

public class DomSnapshotCollector : MonoBehaviour
{
    [SerializeField] private WebPanelController _webPanel;
    [SerializeField] private float _snapshotInterval = 2f;

    private float _timer;
    private string _savePath;

    void Start()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "dom_snapshots");
        Directory.CreateDirectory(_savePath);
        Debug.Log($"[DomCollector] 저장 경로: {_savePath}");
    }

    async void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _snapshotInterval) return;
        _timer = 0;

        string json = await _webPanel.GetVisibleElementsAsync();
        if (string.IsNullOrEmpty(json)) return;

        string filename = $"dom_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json";
        File.WriteAllText(Path.Combine(_savePath, filename), json);
    }
}
