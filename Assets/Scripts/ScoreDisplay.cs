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

/// <summary>
/// UI 생성 도우미 - 에디터에서 자동으로 Canvas와 Text 생성
/// </summary>
public class ScoreUIHelper
{
    [UnityEditor.MenuItem("Catch/Create Score UI")]
    public static void CreateScoreUI()
    {
        // 기존 UI가 있으면 제거
        GameObject existing = GameObject.Find("ScoreCanvas");
        if (existing != null)
            Object.DestroyImmediate(existing);

        // Canvas 생성
        GameObject canvasObj = new GameObject("ScoreCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();

        // ScoreDisplay 스크립트 추가
        var scoreDisplay = canvasObj.AddComponent<ScoreDisplay>();

        // 상단 영역에 배치 (anchor 상단 중앙 기준) - 겹치지 않게 위로
        // Runner Score Text
        var runnerTextObj = CreateText(canvas.transform, "RunnerScoreText", "Runner Score: 0", new Vector2(0, -8), TextAnchor.UpperCenter);
        scoreDisplay.runnerScoreText = runnerTextObj.GetComponent<Text>();

        // Chaser Score Text
        var chaserTextObj = CreateText(canvas.transform, "ChaserScoreText", "Chaser Score: 0", new Vector2(0, -42), TextAnchor.UpperCenter);
        scoreDisplay.chaserScoreText = chaserTextObj.GetComponent<Text>();

        // Status Text
        var statusTextObj = CreateText(canvas.transform, "StatusText", "Game Running...", new Vector2(0, -76), TextAnchor.UpperCenter);
        scoreDisplay.statusText = statusTextObj.GetComponent<Text>();

        // ScoreManager 찾기 (이름으로 검색하여 중복 생성 방지)
        ScoreManager manager = ScoreManager.Instance;
        if (manager == null)
        {
            GameObject existingManager = GameObject.Find("ScoreManager");
            if (existingManager != null)
            {
                manager = existingManager.GetComponent<ScoreManager>();
            }
            else
            {
                GameObject scoreManagerObj = new GameObject("ScoreManager");
                manager = scoreManagerObj.AddComponent<ScoreManager>();
            }
        }

        Debug.Log($"✅ Score UI Created! (no background, top aligned, ScoreManager: {manager != null})");
    }

    static GameObject CreateText(Transform parent, string name, string text, Vector2 position, TextAnchor alignment)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        // 상단 중앙에 고정
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = new Vector2(600, 35);

        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.fontSize = 26;
        textComponent.color = Color.white;
        textComponent.alignment = alignment;
        textComponent.raycastTarget = false;  // 클릭 간섭 방지 (플레이 화면 가리지 않음)
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;

        // 기본 폰트 사용
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            textComponent.font = font;
        else
        {
            Debug.LogWarning("ScoreUIHelper: LegacyRuntime.ttf not found, using default font");
        }

        // 배경 패널 없음 (요청사항) - 그림자만 살짝 적용해 가독성 확보
        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        return textObj;
    }
}
