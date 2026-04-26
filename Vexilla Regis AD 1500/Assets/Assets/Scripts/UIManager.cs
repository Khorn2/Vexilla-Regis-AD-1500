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
        if (Input.GetKeyDown(KeyCode.H))
            ToggleUnitBars();
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