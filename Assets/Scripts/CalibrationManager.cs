using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class CalibrationManager : MonoBehaviour
{
    [Header("References")]
    public GazeDataFeeder feeder;
    public CanvasGroup uiGroup;
    public TextMeshProUGUI instructionText;

    private void Start()
    {
        if (feeder == null) feeder = FindFirstObjectByType<GazeDataFeeder>();
        RestartCalibration();
    }

    public void RestartCalibration()
    {
        StopAllCoroutines();
        
        // 재시작 시 오디오 중첩을 방지하기 위해 빈 페이지 로드
        if (feeder != null && feeder.canvasWebView != null && feeder.canvasWebView.WebView != null)
        {
            if (feeder.isInitialized) 
            {
                feeder.canvasWebView.WebView.LoadUrl("about:blank");
            }
        }

        uiGroup.alpha = 1f;
        StartCoroutine(CalibrationFlow());
    }

    private IEnumerator CalibrationFlow()
    {
        if (feeder == null)
        {
            Debug.LogError("[CalibrationManager] GazeDataFeeder가 연결되지 않았습니다.");
            yield break;
        }

        // 1. 센서 초기화 및 웹뷰 로딩 대기
        instructionText.text = "시스템 초기화 중...";
        uiGroup.alpha = 1f;
        
        yield return new WaitUntil(() => feeder.isInitialized);

        // 안내 문구 출력 및 대기
        instructionText.text = "센서 안정화가 완료되었습니다.\n지금부터 사용자 맞춤형 눈 깜빡임을 측정합니다.\n(10초 뒤 다음 안내가 나옵니다)";
        yield return new WaitForSeconds(10f);

        // 캘리브레이션 지시사항 안내
        instructionText.text = "화면 정면의 붉은 점을 편안하게 응시하며\n자연스럽게 눈을 두세 번 깜빡여주세요.\n(곧 측정이 시작됩니다...)";
        yield return new WaitForSeconds(10f);

        // 캘리브레이션 데이터 수집
        instructionText.text = "화면 정면의 붉은 점을 편안하게 응시하며\n자연스럽게 눈을 서너 번 깜빡여주세요.\n[데이터 수집 중... (10초)]";
        feeder.StartCalibration();
        yield return new WaitForSeconds(10f);

        // 수집 완료 및 서버로 데이터 전송
        instructionText.text = "데이터 수집 완료.\n맞춤형 기준값을 서버에 설정하는 중입니다...";
        yield return StartCoroutine(feeder.FinishCalibrationRoutine());

        // 캘리브레이션 완료 UI 페이드아웃 효과 적용
        instructionText.text = "캘리브레이션 완료!\n유튜브 화면으로 이동합니다.";
        yield return new WaitForSeconds(1.5f);
        
        float fadeDuration = 1.0f;
        float elapsed = 0f;
        while(elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            uiGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        
        // 캘리브레이션 완료 후 기본 화면(유튜브) 로드
        if (feeder.canvasWebView != null && feeder.canvasWebView.WebView != null)
        {
            feeder.canvasWebView.WebView.LoadUrl("https://www.youtube.com/");
        }
    }
}
