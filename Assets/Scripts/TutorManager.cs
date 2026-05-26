using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class TutorManager : MonoBehaviour
{
    [Header("UI & Audio References")]
    public TextMeshProUGUI tutorSpeechText;
    public AudioSource audioSource;

    [Header("UI Panel")]
    public GameObject tutorPanel;  // ← 튜터 UI 패널

    [Header("Animation Reference")]
    public Animator avatarAnimator;

    private bool isSpeaking = false;

    // ★ GazeDataFeeder(실시간 ingest 응답)에서 자동 호출하는 핵심 진입점
    public void ShowTutorFromServer(string message, string audioUrl, int triggerState)
    {
        // 이미 펭귄 튜터가 발화 중이면 중복 실행 방지
        if (isSpeaking) return;

        StartCoroutine(ShowTutor(message, audioUrl, triggerState));
    }

    private IEnumerator ShowTutor(string message, string audioUrl, int triggerState)
    {
        isSpeaking = true;

        // 1. 텍스트 표시 및 패널 활성화
        if (tutorPanel != null) tutorPanel.SetActive(true);
        if (tutorSpeechText != null) tutorSpeechText.text = message;

        // 2. 룰 기반 애니메이션 트리거 연동 (졸음/주의분산 분기)
        if (avatarAnimator != null)
        {
            if (triggerState == 1 || triggerState == 2)
            {
                // 졸음 전 단계(1) 또는 졸음(2) 일 때 Jump 발동
                avatarAnimator.SetTrigger("Jump");
                Debug.Log($"[TutorAnim] 실시간 State {triggerState} 감지 -> Jump 트리거 가동");
            }
            else if (triggerState == 3)
            {
                // 주의분산(3) 일 때 Jump2 발동
                avatarAnimator.SetTrigger("Jump2");
                Debug.Log($"[TutorAnim] 실시간 State {triggerState} 감지 -> Jump2 트리거 가동");
            }
        }

        // 3. 동적 생성된 TTS 음성 다운로드 및 재생 (오디오 재생 완료 시까지 대기)
        if (!string.IsNullOrEmpty(audioUrl))
        {
            yield return StartCoroutine(DownloadAndPlayAudio(audioUrl));
        }

        // 4. 피드백 창 유지 후 닫기 (5초)
        yield return new WaitForSeconds(5f);
        if (tutorPanel != null) tutorPanel.SetActive(false);

        isSpeaking = false;
    }

    // Edge-TTS 음성 스트리밍을 위한 오디오 다운로드 코루틴
    private IEnumerator DownloadAndPlayAudio(string url)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log("[TutorAudio] 실시간 피드백 음성 재생 시작: " + url);
                }
            }
            else
            {
                Debug.LogError("[TutorAudio] 오디오 다운로드 실패: " + www.error);
            }
        }
    }
}