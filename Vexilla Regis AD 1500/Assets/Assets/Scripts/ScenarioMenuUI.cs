using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ScenarioMenuUI : MonoBehaviour
{
    [Header("Scenario")]
    [SerializeField] private ScenarioSelectionData defaultScenario;
    [SerializeField] private string fallbackGameplaySceneName = "SampleScene";

    [Header("Panels")]
    [SerializeField] private GameObject scenarioPanel;
    [SerializeField] private GameObject mainPanel;

    [Header("UI")]
    [SerializeField] private TMP_Text scenarioTitleText;
    [SerializeField] private Image scenarioImage;
    [SerializeField] private TMP_Text scenarioDescriptionText;

    [Header("Buttons")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button backButton;

    private ScenarioSelectionData activeScenario;

    private void Awake()
    {
        activeScenario = defaultScenario;

        if (scenarioPanel != null)
            scenarioPanel.SetActive(false);

        RefreshUI();
    }

    private void OnEnable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(StartBattle);

        if (backButton != null)
            backButton.onClick.AddListener(BackToMainMenu);
    }

    private void OnDisable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(StartBattle);

        if (backButton != null)
            backButton.onClick.RemoveListener(BackToMainMenu);
    }

    public void OpenScenarioMenu()
    {
        activeScenario = defaultScenario;

        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (scenarioPanel != null)
            scenarioPanel.SetActive(true);

        RefreshUI();
    }

    public void CloseScenarioMenu()
    {
        if (scenarioPanel != null)
            scenarioPanel.SetActive(false);
    }

    private void BackToMainMenu()
    {
        CloseScenarioMenu();

        if (mainPanel != null)
            mainPanel.SetActive(true);
    }

    private void RefreshUI()
    {
        if (activeScenario == null)
        {
            if (scenarioTitleText != null)
                scenarioTitleText.text = "Brak scenariusza";

            if (scenarioDescriptionText != null)
                scenarioDescriptionText.text = "Nie przypisano danych scenariusza.";

            if (scenarioImage != null)
            {
                scenarioImage.sprite = null;
                scenarioImage.enabled = false;
            }

            if (startBattleButton != null)
                startBattleButton.interactable = false;

            return;
        }

        if (scenarioTitleText != null)
            scenarioTitleText.text = activeScenario.scenarioTitle;

        if (scenarioDescriptionText != null)
            scenarioDescriptionText.text = activeScenario.historicalDescription;

        if (scenarioImage != null)
        {
            scenarioImage.sprite = activeScenario.scenarioImage;
            scenarioImage.enabled = activeScenario.scenarioImage != null;
        }

        if (startBattleButton != null)
            startBattleButton.interactable = true;
    }

    private void StartBattle()
    {
        if (activeScenario != null)
            SelectedScenario.SetScenario(activeScenario);

        string sceneName = SelectedScenario.GetGameplaySceneName(fallbackGameplaySceneName);
        SceneManager.LoadScene(sceneName);
    }
}