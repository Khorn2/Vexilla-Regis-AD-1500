using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject optionsPanel;

    [Header("Buttons")]
    [SerializeField] private Button openOptionsButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button surrenderButton;
    [SerializeField] private Button resetKeybindsButton;

    [Header("Audio")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeText;

    [Header("Keybinds")]
    [SerializeField] private Transform keybindsContainer;
    [SerializeField] private GameObject keybindRowTemplate;

    [Header("References")]
    [SerializeField] private TurnManager turnManager;

    private GameSettingsManager settingsManager;
    private Coroutine listenForKeyRoutine;
    private string waitingActionId;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        settingsManager = GameSettingsManager.Instance;

        if (settingsManager == null)
            settingsManager = FindObjectOfType<GameSettingsManager>();

        if (settingsManager == null)
        {
            GameObject go = new GameObject("GameSettingsManager");
            settingsManager = go.AddComponent<GameSettingsManager>();
        }

        if (keybindRowTemplate != null)
            keybindRowTemplate.SetActive(false);

        if (optionsPanel == null)
            optionsPanel = gameObject;

        LoadSfxVolume();
        RefreshSurrenderVisibility();
        BuildKeybindList();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (openOptionsButton != null)
            openOptionsButton.onClick.AddListener(OpenOptions);

        if (backButton != null)
            backButton.onClick.AddListener(CloseOptions);

        if (surrenderButton != null)
            surrenderButton.onClick.AddListener(SurrenderBattle);

        if (resetKeybindsButton != null)
            resetKeybindsButton.onClick.AddListener(ResetKeybinds);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);

        if (settingsManager != null)
            settingsManager.OnSettingsChanged += HandleSettingsChanged;

        if (turnManager != null)
            turnManager.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDisable()
    {
        if (openOptionsButton != null)
            openOptionsButton.onClick.RemoveListener(OpenOptions);

        if (backButton != null)
            backButton.onClick.RemoveListener(CloseOptions);

        if (surrenderButton != null)
            surrenderButton.onClick.RemoveListener(SurrenderBattle);

        if (resetKeybindsButton != null)
            resetKeybindsButton.onClick.RemoveListener(ResetKeybinds);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);

        if (settingsManager != null)
            settingsManager.OnSettingsChanged -= HandleSettingsChanged;

        if (turnManager != null)
            turnManager.OnBattleEnded -= HandleBattleEnded;
    }

        private void Update()
    {
        if (turnManager == null)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (listenForKeyRoutine != null)
            {
                StopListeningForKey();
                BuildKeybindList();
                return;
            }

            if (optionsPanel != null && optionsPanel.activeSelf)
                CloseOptions();
            else
                OpenOptions();
        }
    }

    public void OpenOptions()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);

        RefreshSurrenderVisibility();
        LoadSfxVolume();
        BuildKeybindList();
    }

    public void CloseOptions()
    {
        StopListeningForKey();

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void HandleBattleEnded(bool playerWon)
    {
        CloseOptions();
        RefreshSurrenderVisibility();
    }

    private void RefreshSurrenderVisibility()
    {
        if (surrenderButton == null)
            return;

        bool isGameplayScene = turnManager != null;
        bool battleActive = turnManager != null && !turnManager.IsBattleEnded;

        surrenderButton.gameObject.SetActive(isGameplayScene && battleActive);
    }

    private void LoadSfxVolume()
    {
        float volume = settingsManager != null ? settingsManager.SfxVolume : PlayerPrefs.GetFloat("sfx_volume", 1f);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(volume);

        RefreshSfxVolumeText(volume);
    }

    private void SetSfxVolume(float value)
    {
        if (settingsManager != null)
            settingsManager.SetSfxVolume(value);
        else
        {
            PlayerPrefs.SetFloat("sfx_volume", value);
            PlayerPrefs.Save();
        }

        RefreshSfxVolumeText(value);
    }

    private void RefreshSfxVolumeText(float value)
    {
        if (sfxVolumeText != null)
            sfxVolumeText.text = $"SFX: {Mathf.RoundToInt(value * 100f)}%";
    }

    private void BuildKeybindList()
    {
        if (settingsManager == null || keybindsContainer == null || keybindRowTemplate == null)
            return;

        for (int i = keybindsContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = keybindsContainer.GetChild(i);

            if (child.gameObject != keybindRowTemplate)
                Destroy(child.gameObject);
        }

        keybindRowTemplate.SetActive(false);

        Dictionary<string, KeyCode> allKeybinds = settingsManager.GetAllKeybinds();

        foreach (KeyValuePair<string, KeyCode> pair in allKeybinds)
        {
            string actionId = pair.Key;
            KeyCode key = pair.Value;

            GameObject row = Instantiate(keybindRowTemplate, keybindsContainer);
            row.SetActive(true);

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            Button keyButton = row.GetComponentInChildren<Button>(true);

            if (texts.Length >= 1)
                texts[0].text = settingsManager.GetActionName(actionId);

            if (texts.Length >= 2)
                texts[1].text = KeyToDisplayName(key);

            if (keyButton != null)
            {
                string capturedActionId = actionId;
                keyButton.onClick.RemoveAllListeners();
                keyButton.onClick.AddListener(() => StartListeningForKey(capturedActionId));
            }
        }
    }

    private void StartListeningForKey(string actionId)
    {
        if (settingsManager == null)
            return;

        StopListeningForKey();

        waitingActionId = actionId;
        listenForKeyRoutine = StartCoroutine(ListenForKeyRoutine(actionId));

        BuildKeybindList();
        MarkWaitingRow(actionId);
    }

    private IEnumerator ListenForKeyRoutine(string actionId)
    {
        yield return null;

        while (true)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StopListeningForKey();
                BuildKeybindList();
                yield break;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None)
                    continue;

                if (Input.GetKeyDown(key))
                {
                    settingsManager.SetKey(actionId, key);
                    StopListeningForKey();
                    BuildKeybindList();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void StopListeningForKey()
    {
        if (listenForKeyRoutine != null)
        {
            StopCoroutine(listenForKeyRoutine);
            listenForKeyRoutine = null;
        }

        waitingActionId = null;
    }

    private void MarkWaitingRow(string actionId)
    {
        if (settingsManager == null || keybindsContainer == null)
            return;

        for (int i = 0; i < keybindsContainer.childCount; i++)
        {
            Transform child = keybindsContainer.GetChild(i);

            if (child.gameObject == keybindRowTemplate)
                continue;

            TMP_Text[] texts = child.GetComponentsInChildren<TMP_Text>(true);

            if (texts.Length < 2)
                continue;

            if (texts[0].text == settingsManager.GetActionName(actionId))
                texts[1].text = "Naciśnij klawisz...";
        }
    }

    private void ResetKeybinds()
    {
        if (settingsManager == null)
            return;

        settingsManager.ResetKeybinds();
        BuildKeybindList();
    }

    private void SurrenderBattle()
    {
        if (turnManager != null)
            turnManager.SurrenderBattle();

        CloseOptions();
    }

    private void HandleSettingsChanged()
    {
        LoadSfxVolume();

        if (string.IsNullOrEmpty(waitingActionId))
            BuildKeybindList();
    }

    private string KeyToDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Return:
                return "Enter";

            case KeyCode.Delete:
                return "Delete";

            case KeyCode.LeftShift:
                return "LShift";

            case KeyCode.RightShift:
                return "RShift";

            case KeyCode.LeftControl:
                return "LCtrl";

            case KeyCode.RightControl:
                return "RCtrl";

            case KeyCode.Mouse0:
                return "LPM";

            case KeyCode.Mouse1:
                return "PPM";

            case KeyCode.Mouse2:
                return "MMB";

            case KeyCode.Alpha0:
                return "0";

            case KeyCode.Alpha1:
                return "1";

            case KeyCode.Alpha2:
                return "2";

            case KeyCode.Alpha3:
                return "3";

            case KeyCode.Alpha4:
                return "4";

            case KeyCode.Alpha5:
                return "5";

            case KeyCode.Alpha6:
                return "6";

            case KeyCode.Alpha7:
                return "7";

            case KeyCode.Alpha8:
                return "8";

            case KeyCode.Alpha9:
                return "9";

            default:
                return key.ToString();
        }
    }
}