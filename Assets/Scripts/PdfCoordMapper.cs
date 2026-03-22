using UnityEngine;

public class PdfCoordMapper : MonoBehaviour
{
    public Transform studyPlane; // 미리 만들어둔 Plane 할당

    /// <summary>
    /// PDF 좌표를 유니티 월드 좌표로 변환 (수식 적용)
    /// </summary>
    public Vector3 GetWorldPosFromPdf(float u, float v)
    {
        // 1. Plane은 기본 10x10 유닛 크기, 피벗은 중앙
        // 2. PDF (u,v)는 좌상단 원점(0,0) ~ 우하단(1,1)
        float localX = (u - 0.5f) * 10f;
        float localZ = (0.5f - v) * 10f; // 유니티 Z는 위가 +, PDF v는 아래가 +

        Vector3 localPos = new Vector3(localX, 0, localZ);
        
        // 3. TransformPoint를 통해 Plane의 Position, Rotation, Scale을 모두 반영
        return studyPlane.TransformPoint(localPos);
    }
}