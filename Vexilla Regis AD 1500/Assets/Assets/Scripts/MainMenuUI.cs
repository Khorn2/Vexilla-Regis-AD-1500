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

    private void Awake()
    {
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
        if (string.IsNullOrWhiteSpace(gameplaySceneName))
        {
            Debug.LogError("MainMenuUI: gameplaySceneName is empty.");
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
    }

    private void ShowOptionsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    private void ShowCreditsPanel()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(true);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("ExitGame called. In build this will close the application.");
#else
        Application.Quit();
#endif
    }
}