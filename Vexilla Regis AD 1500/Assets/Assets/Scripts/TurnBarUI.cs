using UnityEngine;
using TMPro;

public class TurnBarUI : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private TMP_Text turnText;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.OnTurnStateChanged += RefreshTurnText;
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnTurnStateChanged -= RefreshTurnText;
    }

    private void Start()
    {
        RefreshTurnText();
    }

    private void RefreshTurnText()
    {
        if (turnText == null || turnManager == null)
            return;

        if (turnManager.UseTurnLimit)
            turnText.text = $"{turnManager.TurnNumber}/{turnManager.MaxTurns}";
        else
            turnText.text = $"{turnManager.TurnNumber}";
    }
}