using UnityEngine;
using UnityEngine.UI;

public class UnitWorldUI : MonoBehaviour
{
    [SerializeField] private GameUnit unit;

    [Header("Canvas Root")]
    [SerializeField] private Canvas canvas;

    [Header("Health Bar")]
    [SerializeField] private Image healthFill;

    [Header("Ammo Bar")]
    [SerializeField] private GameObject ammoBarRoot;
    [SerializeField] private Image ammoFill;

    private bool visible;

    private void Awake()
    {
        if (unit == null)
            unit = GetComponentInParent<GameUnit>();

        if (canvas == null)
            canvas = GetComponent<Canvas>();

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnUnitBarsVisibilityChanged += HandleVisibilityChanged;
    }

    private void OnDisable()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnUnitBarsVisibilityChanged -= HandleVisibilityChanged;
    }

    private void Start()
    {
        RefreshVisibility();
        RefreshBars();
    }

    private void Update()
    {
        if (!visible)
            return;

        RefreshBars();
    }

    private void HandleVisibilityChanged(bool show)
    {
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool shouldBeVisible =
            UIManager.Instance != null &&
            UIManager.Instance.ShowUnitBars &&
            unit != null &&
            !unit.IsDead;

        SetVisible(shouldBeVisible);
    }

    private void SetVisible(bool value)
    {
        visible = value;

        if (canvas != null)
            canvas.enabled = value;
    }

    private void RefreshBars()
    {
        if (unit == null || unit.Stats == null)
            return;

        if (healthFill != null)
        {
            float health01 = unit.Stats.unitSize > 0
                ? (float)unit.CurrentSize / unit.Stats.unitSize
                : 0f;

            healthFill.fillAmount = Mathf.Clamp01(health01);
        }

        bool hasAmmo = unit.Stats.canShoot && unit.Stats.maxAmmo > 0;

        if (ammoBarRoot != null)
            ammoBarRoot.SetActive(hasAmmo);

        if (hasAmmo && ammoFill != null)
        {
            float ammo01 = (float)unit.CurrentAmmo / unit.Stats.maxAmmo;
            ammoFill.fillAmount = Mathf.Clamp01(ammo01);
        }
    }
}