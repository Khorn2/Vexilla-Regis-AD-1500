using TMPro;
using UnityEngine;

public class UnitNameTooltipUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TMP_Text nameText;

    [Header("Settings")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.85f, 0f);
    [SerializeField, Min(0.1f)] private float visibleDuration = 2f;

    private GameUnit targetUnit;
    private float hideTime;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>();

        ConfigureCanvas();
        ConfigureText();
        SetVisible(false);
    }

    private void Update()
    {
        if (canvas == null || !canvas.enabled)
            return;

        if (targetUnit == null || targetUnit.IsDead)
        {
            Hide();
            return;
        }

        transform.position = targetUnit.transform.position + worldOffset;

        if (Time.time >= hideTime)
            Hide();
    }

    public void Show(GameUnit unit)
    {
        if (unit == null)
            return;

        targetUnit = unit;
        hideTime = Time.time + visibleDuration;

        ConfigureCanvas();
        ConfigureText();

        if (nameText != null)
            nameText.text = unit.DisplayName;

        transform.position = targetUnit.transform.position + worldOffset;

        SetVisible(true);

        Debug.Log($"Tooltip show: {unit.DisplayName}");
    }

    public void Hide()
    {
        targetUnit = null;
        SetVisible(false);
    }

    private void ConfigureCanvas()
    {
        if (canvas == null)
            return;

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = "Unit";
        canvas.sortingOrder = 50;

        if (canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;
    }

    private void ConfigureText()
    {
        if (nameText == null)
            return;

        nameText.alignment = TextAlignmentOptions.Center;
        nameText.enableAutoSizing = false;
        nameText.raycastTarget = false;

        RectTransform rt = nameText.rectTransform;
        rt.localPosition = Vector3.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.sizeDelta = new Vector2(300f, 80f);
    }

    private void SetVisible(bool value)
    {
        if (canvas != null)
            canvas.enabled = value;

        if (nameText != null)
            nameText.enabled = value;
    }
}