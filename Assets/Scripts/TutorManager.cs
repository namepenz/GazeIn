using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class TutorManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string serverUrl = "http://52.79.74.85:8000/tutor/check-state";

    [Header("UI & Audio References")]
    public TextMeshProUGUI tutorSpeechText;
    public AudioSource audioSource;

    [Header("UI Panel")]
    public GameObject tutorPanel;  // ← 튜터 UI 패널

    void Update()
    {
        // G키: 테스트용 수동 호출
        if (Input.GetKeyDown(KeyCode.G))
            StartCoroutine(GetTutorResponse());
    }

    // ★ GazeDataFeeder에서 자동 호출하는 함수
    public void ShowTutorFromServer(string message, string audioUrl)
    {
        StartCoroutine(ShowTutor(message, audioUrl));
    }

    private IEnumerator ShowTutor(string message, string audioUrl)
    {
        // 텍스트 표시
        if (tutorPanel != null) tutorPanel.SetActive(true);
        if (tutorSpeechText != null) tutorSpeechText.text = message;

        // 음성 재생
        if (!string.IsNullOrEmpty(audioUrl))
            yield return StartCoroutine(DownloadAndPlayAudio(audioUrl));

        // 5초 후 숨기기
        yield return new WaitForSeconds(5f);
        if (tutorPanel != null) tutorPanel.SetActive(false);
    }

    // G키 수동 테스트용
    IEnumerator GetTutorResponse()
    {
        if (tutorSpeechText != null)
            tutorSpeechText.text = "AI 튜터가 생각 중입니다...";

        using (UnityWebRequest webRequest = UnityWebRequest.PostWwwForm(serverUrl, ""))
        {
            webRequest.timeout = 60;
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string json = webRequest.downloadHandler.text;
                TutorResponse response = JsonUtility.FromJson<TutorResponse>(json);
                yield return StartCoroutine(ShowTutor(response.message, response.audio_url));
            }
            else
            {
                if (tutorSpeechText != null)
                    tutorSpeechText.text = "서버 연결에 실패했습니다.";
                Debug.LogError("Error: " + webRequest.error);
            }
        }
    }

    IEnumerator DownloadAndPlayAudio(string url)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("음성 재생 시작: " + url);
            }
            else
            {
                Debug.LogError("오디오 다운로드 실패: " + www.error);
            }
        }
    }
}

[System.Serializable]
public class TutorResponse
{
    public int state;
    public string message;
    public string audio_url;
}
