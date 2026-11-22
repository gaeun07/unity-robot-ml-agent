using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 점수 표시 UI - 화면에 Runner와 Chaser 점수를 표시
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    public Text runnerScoreText;
    public Text chaserScoreText;
    public Text statusText;

    void Update()
    {
        if (ScoreManager.Instance != null)
        {
            if (runnerScoreText != null)
                runnerScoreText.text = $"Runner Score: {ScoreManager.Instance.runnerScore}";
            if (chaserScoreText != null)
                chaserScoreText.text = $"Chaser Score: {ScoreManager.Instance.chaserScore}";
        }
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}

