using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleStatsTracker statsTracker;

    [Header("UI")]
    [SerializeField] private GameObject scorePanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private Button mainMenuButton;

    [Header("Scroll")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private RectTransform detailsTextRect;

    [Header("Layout")]
    [SerializeField] private float topPadding = 30f;
    [SerializeField] private float bottomPadding = 120f;
    [SerializeField] private float sidePadding = 25f;
    [SerializeField] private float minimumContentHeight = 900f;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (statsTracker == null)
            statsTracker = FindObjectOfType<BattleStatsTracker>();

        if (detailsTextRect == null && detailsText != null)
            detailsTextRect = detailsText.GetComponent<RectTransform>();

        if (scorePanel != null)
            scorePanel.SetActive(false);

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.OnBattleEnded += HandleBattleEnded;

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnBattleEnded -= HandleBattleEnded;

        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
    }

    private void HandleBattleEnded(bool playerWon)
    {
        ShowScoreMenu();
    }

    public void ShowScoreMenu()
    {
        if (scorePanel != null)
            scorePanel.SetActive(true);

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(true);

        RefreshResultText();
        RefreshDetailsText();

        StartCoroutine(RebuildScrollNextFrame());
    }

    private IEnumerator RebuildScrollNextFrame()
    {
        yield return null;
        RefreshScrollLayout();
    }

    private void RefreshResultText()
    {
        if (resultText == null || turnManager == null)
            return;

        if (turnManager.BattleDraw)
            resultText.text = "REMIS";
        else
            resultText.text = turnManager.PlayerWon ? "ZWYCIĘSTWO" : "PORAŻKA";
    }

    private void RefreshDetailsText()
    {
        if (detailsText == null || statsTracker == null)
            return;

        StringBuilder sb = new StringBuilder();

        AppendTeamSection(sb, "STRATY GRACZA", 0);
        sb.AppendLine();
        sb.AppendLine();
        AppendTeamSection(sb, "STRATY PRZECIWNIKA", 1);

        detailsText.text = sb.ToString();
    }

    private void AppendTeamSection(StringBuilder sb, string title, int teamId)
    {
        int totalMen = statsTracker.GetTotalInitialMen(teamId);
        int menLost = statsTracker.GetTotalMenLost(teamId);
        int totalUnits = statsTracker.GetTotalUnits(teamId);
        int unitsLost = statsTracker.GetUnitsLost(teamId);

        sb.AppendLine(title);
        sb.AppendLine();

        sb.AppendLine($"Ludzie:   {menLost}/{totalMen}");
        sb.AppendLine($"Oddziały: {unitsLost}/{totalUnits}");
        sb.AppendLine();

        foreach (BattleStatsTracker.UnitBattleRecord record in statsTracker.Records)
        {
            if (record.teamId != teamId)
                continue;

            sb.AppendLine(record.unitName);
            sb.AppendLine($"  Straty: {record.MenLost}/{record.initialSize}");
            sb.AppendLine();
        }
    }

    private void RefreshScrollLayout()
    {
        if (detailsText == null || contentRect == null || detailsTextRect == null)
            return;

        RectTransform viewportRect = scrollRect != null && scrollRect.viewport != null
            ? scrollRect.viewport
            : contentRect.parent as RectTransform;

        float viewportWidth = viewportRect != null ? viewportRect.rect.width : 700f;
        float textWidth = Mathf.Max(100f, viewportWidth - sidePadding * 2f);

        detailsTextRect.anchorMin = new Vector2(0f, 1f);
        detailsTextRect.anchorMax = new Vector2(0f, 1f);
        detailsTextRect.pivot = new Vector2(0f, 1f);
        detailsTextRect.anchoredPosition = new Vector2(sidePadding, -topPadding);
        detailsTextRect.sizeDelta = new Vector2(textWidth, 0f);

        detailsText.enableWordWrapping = true;
        detailsText.overflowMode = TextOverflowModes.Overflow;

        Canvas.ForceUpdateCanvases();
        detailsText.ForceMeshUpdate();

        float preferredHeight = detailsText.GetPreferredValues(detailsText.text, textWidth, Mathf.Infinity).y;
        float finalContentHeight = Mathf.Max(minimumContentHeight, preferredHeight + topPadding + bottomPadding);

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, finalContentHeight);

        detailsTextRect.sizeDelta = new Vector2(textWidth, preferredHeight + bottomPadding);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        if (scrollRect != null)
        {
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

        private void OnMainMenuClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
}