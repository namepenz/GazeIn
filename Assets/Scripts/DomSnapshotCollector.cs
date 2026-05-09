// ✅ DomSnapshotCollector.cs 수정본
using UnityEngine;
using System.IO;
using System.Collections;

public class DomSnapshotCollector : MonoBehaviour
{
    [SerializeField] private WebPanelController _webPanel;
    [SerializeField] private float _snapshotInterval = 2f;

    private bool _isRunning = false;

    void Start()
    {
        // null 체크 추가
        if (_webPanel == null)
        {
            Debug.LogWarning("[DomCollector] _webPanel이 연결되지 않았습니다.");
            return;
        }

        string savePath = Path.Combine(Application.persistentDataPath, "dom_snapshots");
        Directory.CreateDirectory(savePath);
        Debug.Log($"[DomCollector] 저장 경로: {savePath}");

        // async void Update 대신 코루틴으로 교체
        StartCoroutine(SnapshotLoop(savePath));
    }

    private IEnumerator SnapshotLoop(string savePath)
    {
        while (true)
        {
            yield return new WaitForSeconds(_snapshotInterval);

            if (_isRunning) continue; // 중첩 방지
            _isRunning = true;

            var task = _webPanel.GetVisibleElementsAsync();
            yield return new WaitUntil(() => task.IsCompleted);

            string json = task.Result;
            if (!string.IsNullOrEmpty(json))
            {
                // 파일 쓰기를 별도 스레드에서 실행
                string filename = Path.Combine(savePath,
                    $"dom_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json");
                System.Threading.Tasks.Task.Run(() => File.WriteAllText(filename, json));
            }

            _isRunning = false;
        }
    }
}
