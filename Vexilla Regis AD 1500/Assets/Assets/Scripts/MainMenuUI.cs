using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameplaySceneName = "SampleScene";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button exitButton;

    [Header("Panel Buttons")]
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;

    [Header("References")]
    [SerializeField] private OptionsMenuUI optionsMenuUI;
    [SerializeField] private ScenarioMenuUI scenarioMenuUI;

    private void Awake()
    {
        if (optionsMenuUI == null)
            optionsMenuUI = GetComponent<OptionsMenuUI>();

        if (scenarioMenuUI == null)
            scenarioMenuUI = GetComponent<ScenarioMenuUI>();

        ShowMainPanel();
    }

    private void OnEnable()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlayGame);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(ShowOptionsPanel);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(ShowCreditsPanel);

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitGame);

        if (optionsBackButton != null)
            optionsBackButton.onClick.AddListener(ShowMainPanel);

        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(ShowMainPanel);
    }

    private void OnDisable()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(PlayGame);

        if (optionsButton != null)
            optionsButton.onClick.RemoveListener(ShowOptionsPanel);

        if (creditsButton != null)
            creditsButton.onClick.RemoveListener(ShowCreditsPanel);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitGame);

        if (optionsBackButton != null)
            optionsBackButton.onClick.RemoveListener(ShowMainPanel);

        if (creditsBackButton != null)
            creditsBackButton.onClick.RemoveListener(ShowMainPanel);
    }

    private void PlayGame()
    {
        if (scenarioMenuUI != null)
        {
            scenarioMenuUI.OpenScenarioMenu();
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void ShowMainPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (scenarioMenuUI != null)
            scenarioMenuUI.CloseScenarioMenu();
    }

    private void ShowOptionsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        if (scenarioMenuUI != null)
            scenarioMenuUI.CloseScenarioMenu();

        if (optionsPanel != null)
            optionsPanel.SetActive(true);

        if (optionsMenuUI != null)
            optionsMenuUI.OpenOptions();
    }

    private void ShowCreditsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (scenarioMenuUI != null)
            scenarioMenuUI.CloseScenarioMenu();

        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("ExitGame called.");
#else
        Application.Quit();
#endif
    }
}