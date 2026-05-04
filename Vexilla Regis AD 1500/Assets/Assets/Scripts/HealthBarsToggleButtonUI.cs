using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarsToggleButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private TMP_Text buttonText;

    [Header("Colors")]
    [SerializeField] private Color offColor = Color.blue;
    [SerializeField] private Color onColor = Color.red;

    private bool subscribed;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        if (buttonText == null)
            buttonText = GetComponentInChildren<TMP_Text>();

        if (button != null)
            button.transition = Selectable.Transition.None;
    }

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(ToggleHealthBars);

        TrySubscribeToUIManager();
    }

    private void Start()
    {
        TrySubscribeToUIManager();

        if (buttonText != null)
            buttonText.text = "H";

        bool visible = UIManager.Instance != null && UIManager.Instance.ShowUnitBars;
        RefreshVisual(visible);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(ToggleHealthBars);

        if (subscribed && UIManager.Instance != null)
            UIManager.Instance.OnUnitBarsVisibilityChanged -= RefreshVisual;

        subscribed = false;
    }

    private void TrySubscribeToUIManager()
    {
        if (subscribed)
            return;

        if (UIManager.Instance == null)
            return;

        UIManager.Instance.OnUnitBarsVisibilityChanged += RefreshVisual;
        subscribed = true;
    }

    private void ToggleHealthBars()
    {
        if (UIManager.Instance == null)
        {
            Debug.LogError("HealthBarsToggleButtonUI: UIManager.Instance is null.");
            return;
        }

        UIManager.Instance.ToggleUnitBars();

        // Wymuszamy natychmiastową zmianę koloru przycisku.
        RefreshVisual(UIManager.Instance.ShowUnitBars);
    }

    private void RefreshVisual(bool healthBarsVisible)
    {
        if (buttonImage != null)
            buttonImage.color = healthBarsVisible ? onColor : offColor;

        if (buttonText != null)
            buttonText.text = "H";
    }
}