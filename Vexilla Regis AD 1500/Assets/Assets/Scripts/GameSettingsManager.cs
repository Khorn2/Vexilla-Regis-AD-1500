using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";

    public float SfxVolume { get; private set; } = 1f;

    public event Action OnSettingsChanged;

    private readonly Dictionary<string, string> actionNames = new Dictionary<string, string>
    {
        { "EndTurn", "Koniec tury" },
        { "ToggleHealthBars", "Pokaż paski jednostek" },
        { "OrderMarch", "Rozkaz: Marsz" },
        { "OrderCharge", "Rozkaz: Szarża" },
        { "OrderShoot", "Rozkaz: Strzał" },
        { "OrderRetreat", "Rozkaz: Odwrót" },
        { "ClearOrders", "Usuń rozkaz" },
        { "AppendQueue", "Kolejkowanie rozkazów" },
        { "CameraUp", "Ruch kamery: góra" },
        { "CameraDown", "Ruch kamery: dół" },
        { "CameraLeft", "Ruch kamery: lewo" },
        { "CameraRight", "Ruch kamery: prawo" },
        { "CameraBoost", "Szybszy ruch kamery" },
        { "CameraDrag", "Przeciąganie kamery" },
        { "MultiSelect", "Zaznaczanie wielu jednostek" }
    };

    private readonly Dictionary<string, KeyCode> defaultKeybinds = new Dictionary<string, KeyCode>
    {
        { "EndTurn", KeyCode.Return },
        { "ToggleHealthBars", KeyCode.H },
        { "OrderMarch", KeyCode.Alpha1 },
        { "OrderCharge", KeyCode.Alpha2 },
        { "OrderShoot", KeyCode.Alpha3 },
        { "OrderRetreat", KeyCode.Alpha4 },
        { "ClearOrders", KeyCode.Delete },
        { "AppendQueue", KeyCode.LeftControl },
        { "CameraUp", KeyCode.W },
        { "CameraDown", KeyCode.S },
        { "CameraLeft", KeyCode.A },
        { "CameraRight", KeyCode.D },
        { "CameraBoost", KeyCode.LeftShift },
        { "CameraDrag", KeyCode.Mouse1 },
        { "MultiSelect", KeyCode.LeftShift }
    };

    private readonly Dictionary<string, KeyCode> keybinds = new Dictionary<string, KeyCode>();

    public IReadOnlyDictionary<string, string> ActionNames => actionNames;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    public string GetActionName(string actionId)
    {
        if (actionNames.TryGetValue(actionId, out string actionName))
            return actionName;

        return actionId;
    }

    public KeyCode GetKey(string actionId)
    {
        if (keybinds.TryGetValue(actionId, out KeyCode key))
            return key;

        if (defaultKeybinds.TryGetValue(actionId, out KeyCode defaultKey))
            return defaultKey;

        return KeyCode.None;
    }

    public void SetKey(string actionId, KeyCode key)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return;

        if (!defaultKeybinds.ContainsKey(actionId))
            return;

        keybinds[actionId] = key;
        PlayerPrefs.SetString($"keybind_{actionId}", key.ToString());
        PlayerPrefs.Save();

        OnSettingsChanged?.Invoke();
    }

    public Dictionary<string, KeyCode> GetAllKeybinds()
    {
        Dictionary<string, KeyCode> result = new Dictionary<string, KeyCode>();

        foreach (KeyValuePair<string, KeyCode> pair in defaultKeybinds)
            result[pair.Key] = GetKey(pair.Key);

        return result;
    }

    public void ResetKeybinds()
    {
        keybinds.Clear();

        foreach (KeyValuePair<string, KeyCode> pair in defaultKeybinds)
        {
            keybinds[pair.Key] = pair.Value;
            PlayerPrefs.DeleteKey($"keybind_{pair.Key}");
        }

        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("sfx_volume", SfxVolume);
        PlayerPrefs.Save();

        ApplySfxVolume();
        OnSettingsChanged?.Invoke();
    }

    private void LoadSettings()
    {
        SfxVolume = PlayerPrefs.GetFloat("sfx_volume", 1f);

        keybinds.Clear();

        foreach (KeyValuePair<string, KeyCode> pair in defaultKeybinds)
        {
            string saved = PlayerPrefs.GetString($"keybind_{pair.Key}", pair.Value.ToString());

            if (Enum.TryParse(saved, out KeyCode key))
                keybinds[pair.Key] = key;
            else
                keybinds[pair.Key] = pair.Value;
        }

        ApplySfxVolume();
    }

    private void ApplySfxVolume()
    {
        if (audioMixer == null)
            return;

        float db = SfxVolume <= 0.001f ? -80f : Mathf.Log10(SfxVolume) * 20f;
        audioMixer.SetFloat(sfxVolumeParameter, db);
    }
}