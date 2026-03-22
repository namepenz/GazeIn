using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class TextCoordEntry
{
    public int    page;
    public string text;
    public float  u;
    public float  v;
}

public class PdfCoordMapper1 : MonoBehaviour
{
    [Header("매핑 대상 Plane")]
    public Transform planeTf;

    [Header("JSON 파일명 (StreamingAssets 기준)")]
    public string jsonFileName = "text_coords.json";

    private List<TextCoordEntry> _entries = new();

    private void Start()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[PdfMapper] JSON 없음: {path}");
            return;
        }

        _entries = JsonConvert.DeserializeObject<List<TextCoordEntry>>(
            File.ReadAllText(path)
        );
        Debug.Log($"[PdfMapper] 텍스트 좌표 {_entries.Count}개 로드 완료");
    }

    /// <summary>
    /// PDF 정규화 좌표 (u, v) → Unity 월드 좌표
    /// u: 0=왼쪽, 1=오른쪽
    /// v: 0=위쪽,  1=아래쪽 (PDF 좌상단 원점)
    /// </summary>
    public Vector3 UVToWorld(float u, float v)
    {
        // Plane 로컬 좌표 (-5 ~ 5 범위)
        float localX = (u - 0.5f) * 10f;
        float localZ = (v - 0.5f) * 10f;

        // TransformPoint: Scale · Rotation · Position 자동 적용
        return planeTf.TransformPoint(new Vector3(localX, 0f, localZ));
    }

    /// <summary>
    /// 현재 시선 Ray에서 가장 가까운 텍스트 항목 반환
    /// </summary>
    public TextCoordEntry FindNearestToGaze(Ray gazeRay, float maxDist = 0.15f)
    {
        TextCoordEntry nearest = null;
        float minDist = float.MaxValue;

        foreach (var entry in _entries)
        {
            Vector3 wp   = UVToWorld(entry.u, entry.v);
            float   dist = Vector3.Cross(gazeRay.direction,
                                         wp - gazeRay.origin).magnitude;
            if (dist < maxDist && dist < minDist)
            {
                minDist = dist;
                nearest = entry;
            }
        }
        return nearest;
    }

    // Scene 뷰에서 좌표 시각화 (Gizmos 켜져있을 때)
    private void OnDrawGizmosSelected()
    {
        if (_entries == null || planeTf == null) return;
        Gizmos.color = Color.cyan;
        foreach (var e in _entries)
            Gizmos.DrawSphere(UVToWorld(e.u, e.v), 0.01f);
    }
}