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

    [Header("Sprites")]
    [SerializeField] private Sprite marchSprite;
    [SerializeField] private Sprite chargeSprite;
    [SerializeField] private Sprite shootSprite;
    [SerializeField] private Sprite retreatSprite;
    [SerializeField] private Sprite endTurnSprite;

    [Header("Visual Settings")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color selectedOrderColor = Color.yellow;
    [SerializeField] private Color disabledIconColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField, Range(0f, 1f)] private float disabledSelectedAlpha = 0.35f;
    [SerializeField] private bool hideOrderIconsWhenNoSelection = true;

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
            marchButton.onClick.AddListener(OnMarchClicked);

        if (chargeButton != null)
            chargeButton.onClick.AddListener(OnChargeClicked);

        if (shootButton != null)
            shootButton.onClick.AddListener(OnShootClicked);

        if (retreatButton != null)
            retreatButton.onClick.AddListener(OnRetreatClicked);

        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (turnManager != null)
            turnManager.OnTurnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (marchButton != null)
            marchButton.onClick.RemoveListener(OnMarchClicked);

        if (chargeButton != null)
            chargeButton.onClick.RemoveListener(OnChargeClicked);

        if (shootButton != null)
            shootButton.onClick.RemoveListener(OnShootClicked);

        if (retreatButton != null)
            retreatButton.onClick.RemoveListener(OnRetreatClicked);

        if (endTurnButton != null)
            endTurnButton.onClick.RemoveListener(OnEndTurnClicked);

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

    private void OnMarchClicked()
    {
        ApplyOrder(OrderType.March);
    }

    private void OnChargeClicked()
    {
        ApplyOrder(OrderType.Charge);
    }

    private void OnShootClicked()
    {
        ApplyOrder(OrderType.Shoot);
    }

    private void OnRetreatClicked()
    {
        ApplyOrder(OrderType.Retreat);
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

        bool ordersBaseEnabled = planning && !battleEnded && hasSelection;
        bool endTurnEnabled = planning && !battleEnded;

        RefreshOrder(marchButton, marchButtonImage, OrderType.March, ordersBaseEnabled, hasSelection);
        RefreshOrder(chargeButton, chargeButtonImage, OrderType.Charge, ordersBaseEnabled, hasSelection);
        RefreshOrder(shootButton, shootButtonImage, OrderType.Shoot, ordersBaseEnabled, hasSelection);
        RefreshOrder(retreatButton, retreatButtonImage, OrderType.Retreat, ordersBaseEnabled, hasSelection);

        RefreshEndTurn(endTurnEnabled);
    }

    private void RefreshOrder(Button button, Image image, OrderType order, bool baseEnabled, bool hasSelection)
    {
        bool canUse = baseEnabled;

        if (selectionManager != null)
            canUse &= selectionManager.CanApplyOrderFromUI(order);

        if (button != null)
            button.interactable = canUse;

        if (image == null)
            return;

        image.preserveAspect = true;

        if (!hasSelection && hideOrderIconsWhenNoSelection)
        {
            image.enabled = false;
            return;
        }

        image.enabled = true;

        GameUnit selectedUnit = selectionManager != null ? selectionManager.Selected : null;
        bool isSelectedOrder = selectedUnit != null && selectedUnit.CurrentOrder == order;

        if (isSelectedOrder)
            image.color = canUse ? selectedOrderColor : WithAlpha(selectedOrderColor, disabledSelectedAlpha);
        else
            image.color = canUse ? normalIconColor : disabledIconColor;
    }

    private void RefreshEndTurn(bool enabled)
    {
        if (endTurnButton != null)
            endTurnButton.interactable = enabled;

        if (endTurnButtonImage == null)
            return;

        endTurnButtonImage.enabled = true;
        endTurnButtonImage.preserveAspect = true;
        endTurnButtonImage.color = enabled ? normalIconColor : disabledIconColor;
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
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

    private void ApplySprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.color = normalIconColor;
        image.preserveAspect = true;
    }

    private void DisableButtonTransitions()
    {
        DisableTransition(marchButton);
        DisableTransition(chargeButton);
        DisableTransition(shootButton);
        DisableTransition(retreatButton);
        DisableTransition(endTurnButton);
    }

    private void DisableTransition(Button button)
    {
        if (button != null)
            button.transition = Selectable.Transition.None;
    }
}