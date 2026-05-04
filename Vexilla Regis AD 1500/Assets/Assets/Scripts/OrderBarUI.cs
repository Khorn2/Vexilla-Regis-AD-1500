using UnityEngine;
using UnityEngine.UI;

public class OrderBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SelectionManager2D selectionManager;
    [SerializeField] private TurnManager turnManager;

    [Header("Order Buttons")]
    [SerializeField] private Button marchButton;
    [SerializeField] private Button chargeButton;
    [SerializeField] private Button shootButton;
    [SerializeField] private Button retreatButton;

    [Header("End Turn Button")]
    [SerializeField] private Button endTurnButton;

    [Header("Button Images")]
    [SerializeField] private Image marchButtonImage;
    [SerializeField] private Image chargeButtonImage;
    [SerializeField] private Image shootButtonImage;
    [SerializeField] private Image retreatButtonImage;
    [SerializeField] private Image endTurnButtonImage;

    [Header("Colors")]
    [SerializeField] private Color enabledColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.10f, 0.10f, 0.10f, 0.65f);
    [SerializeField] private Color selectedColor = new Color(0.20f, 0.45f, 0.90f, 1f);
    [SerializeField] private Color endTurnColor = new Color(0.35f, 0.25f, 0.10f, 1f);

    [Header("Future Sprites")]
    [SerializeField] private Sprite marchSprite;
    [SerializeField] private Sprite chargeSprite;
    [SerializeField] private Sprite shootSprite;
    [SerializeField] private Sprite retreatSprite;
    [SerializeField] private Sprite endTurnSprite;

    private void Awake()
    {
        if (selectionManager == null)
            selectionManager = FindObjectOfType<SelectionManager2D>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        AutoAssignImages();
        ApplySpritesIfAvailable();
        DisableButtonTransitions();
    }

    private void OnEnable()
    {
        if (marchButton != null)
            marchButton.onClick.AddListener(() => ApplyOrder(OrderType.March));

        if (chargeButton != null)
            chargeButton.onClick.AddListener(() => ApplyOrder(OrderType.Charge));

        if (shootButton != null)
            shootButton.onClick.AddListener(() => ApplyOrder(OrderType.Shoot));

        if (retreatButton != null)
            retreatButton.onClick.AddListener(() => ApplyOrder(OrderType.Retreat));

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (turnManager != null)
            turnManager.OnTurnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.OnTurnStateChanged -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void ApplyOrder(OrderType order)
    {
        if (selectionManager != null)
            selectionManager.ApplyOrderFromUI(order);

        Refresh();
    }

    private void OnEndTurnClicked()
    {
        if (turnManager != null)
            turnManager.RequestEndTurn();

        Refresh();
    }

    private void Refresh()
    {
        bool planning = turnManager == null || turnManager.IsPlanningPhase;
        bool battleEnded = turnManager != null && turnManager.IsBattleEnded;
        bool hasSelection = selectionManager != null && selectionManager.HasSelection;

        bool ordersEnabled = planning && !battleEnded && hasSelection;
        bool endTurnEnabled = planning && !battleEnded;

        RefreshOrder(marchButton, marchButtonImage, OrderType.March, ordersEnabled);
        RefreshOrder(chargeButton, chargeButtonImage, OrderType.Charge, ordersEnabled);
        RefreshOrder(shootButton, shootButtonImage, OrderType.Shoot, ordersEnabled);
        RefreshOrder(retreatButton, retreatButtonImage, OrderType.Retreat, ordersEnabled);

        RefreshEndTurn(endTurnEnabled);
    }

    private void RefreshOrder(Button button, Image image, OrderType order, bool baseEnabled)
    {
        bool canUse = baseEnabled;

        if (selectionManager != null)
            canUse &= selectionManager.CanApplyOrderFromUI(order);

        if (button != null)
            button.interactable = canUse;

        if (image == null)
            return;

        GameUnit selected = selectionManager != null ? selectionManager.Selected : null;

        if (!canUse)
        {
            image.color = disabledColor;
            return;
        }

        if (selected != null && selected.CurrentOrder == order)
        {
            image.color = selectedColor;
            return;
        }

        image.color = enabledColor;
    }

    private void RefreshEndTurn(bool enabled)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = enabled;

        if (endTurnButtonImage != null)
            endTurnButtonImage.color = enabled ? endTurnColor : disabledColor;
    }

    private void AutoAssignImages()
    {
        if (marchButtonImage == null && marchButton != null)
            marchButtonImage = marchButton.GetComponent<Image>();

        if (chargeButtonImage == null && chargeButton != null)
            chargeButtonImage = chargeButton.GetComponent<Image>();

        if (shootButtonImage == null && shootButton != null)
            shootButtonImage = shootButton.GetComponent<Image>();

        if (retreatButtonImage == null && retreatButton != null)
            retreatButtonImage = retreatButton.GetComponent<Image>();

        if (endTurnButtonImage == null && endTurnButton != null)
            endTurnButtonImage = endTurnButton.GetComponent<Image>();
    }

    private void ApplySpritesIfAvailable()
    {
        ApplySprite(marchButtonImage, marchSprite);
        ApplySprite(chargeButtonImage, chargeSprite);
        ApplySprite(shootButtonImage, shootSprite);
        ApplySprite(retreatButtonImage, retreatSprite);
        ApplySprite(endTurnButtonImage, endTurnSprite);
    }

    private void ApplySprite(Image img, Sprite sprite)
    {
        if (img != null && sprite != null)
            img.sprite = sprite;
    }

    private void DisableButtonTransitions()
    {
        DisableTransition(marchButton);
        DisableTransition(chargeButton);
        DisableTransition(shootButton);
        DisableTransition(retreatButton);
        DisableTransition(endTurnButton);
    }

    private void DisableTransition(Button btn)
    {
        if (btn != null)
            btn.transition = Selectable.Transition.None;
    }
}