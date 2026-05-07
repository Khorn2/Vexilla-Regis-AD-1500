using UnityEngine;
using UnityEngine.UI;

public class GameplayOptionsMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;

    [Header("Panels")]
    [SerializeField] private GameObject optionsPanel;

    [Header("Buttons")]
    [SerializeField] private Button openOptionsButton;
    [SerializeField] private Button closeOptionsButton;
    [SerializeField] private Button surrenderButton;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (openOptionsButton != null)
            openOptionsButton.onClick.AddListener(OpenOptions);

        if (closeOptionsButton != null)
            closeOptionsButton.onClick.AddListener(CloseOptions);

        if (surrenderButton != null)
            surrenderButton.onClick.AddListener(Surrender);
    }

    private void OnDisable()
    {
        if (openOptionsButton != null)
            openOptionsButton.onClick.RemoveListener(OpenOptions);

        if (closeOptionsButton != null)
            closeOptionsButton.onClick.RemoveListener(CloseOptions);

        if (surrenderButton != null)
            surrenderButton.onClick.RemoveListener(Surrender);
    }

    private void OpenOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
    }

    private void CloseOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void Surrender()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (turnManager != null)
            turnManager.SurrenderBattle();
    }
}