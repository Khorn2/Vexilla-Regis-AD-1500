using System;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public bool ShowUnitBars { get; private set; } = false;

    public event Action<bool> OnUnitBarsVisibilityChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        if (IsKeyDown("ToggleHealthBars", KeyCode.H))
            ToggleUnitBars();
    }

    private bool IsKeyDown(string actionId, KeyCode fallback)
    {
        KeyCode key = GetConfiguredKey(actionId, fallback);
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private KeyCode GetConfiguredKey(string actionId, KeyCode fallback)
    {
        if (GameSettingsManager.Instance != null)
            return GameSettingsManager.Instance.GetKey(actionId);

        return fallback;
    }

    public void ToggleUnitBars()
    {
        SetUnitBarsVisible(!ShowUnitBars);
    }

    public void SetUnitBarsVisible(bool visible)
    {
        if (ShowUnitBars == visible)
            return;

        ShowUnitBars = visible;
        OnUnitBarsVisibilityChanged?.Invoke(ShowUnitBars);

        Debug.Log($"Unit bars visibility: {ShowUnitBars}");
    }
}