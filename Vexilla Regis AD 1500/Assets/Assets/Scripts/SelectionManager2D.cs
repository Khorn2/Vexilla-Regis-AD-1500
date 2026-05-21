using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitMask;
    [SerializeField] private GridManager grid;
    [SerializeField] private DeploymentManager deploymentManager;
    [SerializeField] private CameraController2D cameraController;
    [SerializeField] private TurnManager turnManager;

    [Header("Tooltip")]
    [SerializeField] private UnitNameTooltipUI unitNameTooltip;

    [Header("Box Selection")]
    [SerializeField] private float dragThreshold = 12f;

    public GameUnit Selected => PrimarySelected;
    public bool HasSelection => PrimarySelected != null;
    public int SelectedCount => selectedUnits.Count;

    private readonly List<GameUnit> selectedUnits = new List<GameUnit>();

    private bool leftMouseDown;
    private bool isBoxSelecting;
    private Vector2 dragStartScreen;
    private Vector2 dragCurrentScreen;
    private bool selectionLockedByBattleEnd;
    private bool blockedFailedRetreatInput;

    private GameUnit PrimarySelected
    {
        get
        {
            if (selectedUnits.Count == 0) return null;
            return selectedUnits[selectedUnits.Count - 1];
        }
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindObjectOfType<GridManager>();
        if (deploymentManager == null) deploymentManager = FindObjectOfType<DeploymentManager>();
        if (cameraController == null) cameraController = FindObjectOfType<CameraController2D>();
        if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
        if (unitNameTooltip == null) unitNameTooltip = FindObjectOfType<UnitNameTooltipUI>();
    }

    private void Update()
    {
        if (turnManager != null && turnManager.IsBattleEnded)
        {
            if (!selectionLockedByBattleEnd)
            {
                CancelCurrentInputState();
                ClearSelection();
                selectionLockedByBattleEnd = true;
            }

            return;
        }

        selectionLockedByBattleEnd = false;

        RefreshBlockedRetreatState();
        HandleLeftMouseSelection();
        HandleRightClickAction();
        HandleOrderInput();
        HandleClearPlannedInput();
        UpdateRangeHighlight();
    }

    private void RefreshBlockedRetreatState()
    {
        if (!blockedFailedRetreatInput)
            return;

        GameUnit selected = PrimarySelected;

        if (selected == null || selected.CanUseManualRetreat())
            blockedFailedRetreatInput = false;
    }

    private KeyCode GetConfiguredKey(string actionId, KeyCode fallback)
    {
        if (GameSettingsManager.Instance != null)
            return GameSettingsManager.Instance.GetKey(actionId);

        return fallback;
    }

    private bool IsKeyDown(string actionId, KeyCode fallback)
    {
        KeyCode key = GetConfiguredKey(actionId, fallback);
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private bool IsKeyHeld(string actionId, KeyCode fallback)
    {
        KeyCode key = GetConfiguredKey(actionId, fallback);
        return key != KeyCode.None && Input.GetKey(key);
    }

    public void ApplyOrderFromUI(OrderType order)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (turnManager != null && !turnManager.IsPlanningPhase) return;

        if (selectedUnits.Count == 0)
        {
            Debug.Log($"OrderBar: cannot apply {order}, no selected units.");
            return;
        }

        if (order == OrderType.Retreat)
        {
            TrySetManualRetreat();
            return;
        }

        blockedFailedRetreatInput = false;
        SetOrderForSelection(order);
    }

    public bool CanApplyOrderFromUI(OrderType order)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return false;
        if (turnManager != null && !turnManager.IsPlanningPhase) return false;
        if (selectedUnits.Count == 0) return false;

        if (order == OrderType.Retreat)
        {
            if (selectedUnits.Count != 1) return false;

            GameUnit unit = selectedUnits[0];
            return unit != null && unit.CanApplyOrder(OrderType.Retreat);
        }

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];

            if (unit != null && unit.CanApplyOrder(order))
                return true;
        }

        return false;
    }

    public int GetManualRetreatCooldownRemaining()
    {
        GameUnit unit = PrimarySelected;

        if (unit == null)
            return 0;

        return unit.GetManualRetreatCooldownRemaining();
    }

    private void HandleLeftMouseSelection()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;

        if (IsPointerOverUI())
        {
            if (Input.GetMouseButtonDown(0))
                CancelCurrentInputState();

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            leftMouseDown = true;
            isBoxSelecting = false;
            dragStartScreen = Input.mousePosition;
            dragCurrentScreen = dragStartScreen;
        }

        if (leftMouseDown && Input.GetMouseButton(0))
        {
            dragCurrentScreen = Input.mousePosition;

            if (Vector2.Distance(dragStartScreen, dragCurrentScreen) > dragThreshold)
                isBoxSelecting = true;
        }

        if (leftMouseDown && Input.GetMouseButtonUp(0))
        {
            bool additive = IsSelectionAdditiveHeld();

            if (isBoxSelecting)
                FinishBoxSelection(additive);
            else
                HandleSingleClickSelection(additive);

            leftMouseDown = false;
            isBoxSelecting = false;
        }
    }

    private bool IsSelectionAdditiveHeld()
    {
        KeyCode configured = GetConfiguredKey("SelectionAdditive", KeyCode.None);

        if (configured != KeyCode.None)
            return Input.GetKey(configured);

        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private void HandleSingleClickSelection(bool additive)
    {
        GameUnit clickedUnit = GetPlayerUnitUnderMouse();

        if (clickedUnit == null)
        {
            if (!additive)
                ClearSelection();

            return;
        }

        if (!additive)
            ClearSelection();

        AddSelection(clickedUnit);
    }

    private void FinishBoxSelection(bool additive)
    {
        Rect rect = GetScreenRect(dragStartScreen, dragCurrentScreen);

        if (!additive)
            ClearSelection();

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.TeamId != 0) continue;
            if (unit.IsBroken) continue;

            if (deploymentManager != null && deploymentManager.DeploymentActive)
            {
                if (!deploymentManager.IsInsideDeploymentZone(unit.GridPosition))
                    continue;
            }

            Vector3 sp = cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z < 0f) continue;

            if (rect.Contains(new Vector2(sp.x, sp.y), true))
                AddSelection(unit);
        }
    }

    private void AddSelection(GameUnit unit)
    {
        if (unit == null) return;
        if (unit.IsDead) return;
        if (unit.IsBroken) return;
        if (selectedUnits.Contains(unit)) return;

        if (deploymentManager != null && deploymentManager.DeploymentActive)
        {
            if (!deploymentManager.IsInsideDeploymentZone(unit.GridPosition))
                return;
        }

        blockedFailedRetreatInput = false;
        selectedUnits.Add(unit);
        unit.SetSelected(true);

        if (cameraController != null)
            cameraController.DragEnabled = false;
    }

    private void HandleRightClickAction()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (!Input.GetMouseButtonDown(1)) return;
        if (turnManager != null && !turnManager.IsPlanningPhase) return;
        if (IsPointerOverUI()) return;

        if (selectedUnits.Count == 0)
        {
            GameUnit clickedAnyUnit = GetAnyUnitUnderMouse();

            Debug.Log(clickedAnyUnit != null
                ? $"Right click unit: {clickedAnyUnit.name}"
                : "Right click: no unit hit");

            if (clickedAnyUnit != null && unitNameTooltip != null)
                unitNameTooltip.Show(clickedAnyUnit);

            return;
        }

        GameUnit selected = PrimarySelected;
        if (selected == null || selected.IsDead || selected.IsBroken || grid == null) return;

        if (blockedFailedRetreatInput)
        {
            Debug.Log("Retreat is on cooldown. Select another order before assigning movement.");
            return;
        }

        bool append = IsAppendQueueHeld();

        GameUnit clickedEnemy = GetEnemyUnderMouse();

        if (clickedEnemy != null)
        {
            QueueGroupAttack(clickedEnemy, append);
            return;
        }

        if (selected.CurrentOrder == OrderType.Shoot)
            return;

        Vector3 mp = Input.mousePosition;
        mp.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mp);

        Vector2Int target = grid.WorldToGrid(world);
        if (!grid.IsInside(target)) return;

        if (deploymentManager != null && deploymentManager.DeploymentActive)
        {
            MoveSelectedUnitsDuringDeployment(target);
            return;
        }

        if (selected.CurrentOrder == OrderType.Retreat)
        {
            if (selectedUnits.Count != 1)
            {
                Debug.Log("Manual retreat can be assigned only to one selected unit.");
                return;
            }

            if (!selected.CanUseManualRetreat())
            {
                Debug.Log($"Manual retreat cooldown remaining: {selected.GetManualRetreatCooldownRemaining()} turn(s).");
                blockedFailedRetreatInput = true;
                return;
            }

            if (!grid.IsValidManualRetreatDestination(selected, target))
            {
                Debug.Log("Invalid retreat destination.");
                return;
            }
        }

        QueueGroupMove(target, append);
    }

    private GameUnit GetAnyUnitUnderMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, unitMask);

        for (int i = 0; i < hits.Length; i++)
        {
            GameUnit unit = hits[i].collider.GetComponentInParent<GameUnit>();

            if (unit != null && !unit.IsDead)
                return unit;
        }

        return null;
    }

    private void MoveSelectedUnitsDuringDeployment(Vector2Int target)
    {
        GameUnit primary = PrimarySelected;
        if (primary == null) return;

        Vector2Int primaryPos = primary.GridPosition;

        if (!CanPlaceWholeFormation(target, primaryPos, true))
        {
            Debug.Log("Cannot move formation: at least one target tile is invalid.");
            return;
        }

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit == null) continue;

            Vector2Int offset = unit.GridPosition - primaryPos;
            Vector2Int unitTarget = target + offset;

            unit.SnapToGrid(unitTarget);
        }
    }

    private void QueueGroupMove(Vector2Int target, bool append)
    {
        GameUnit primary = PrimarySelected;
        if (primary == null) return;

        if (selectedUnits.Count == 1)
        {
            primary.QueueMove(target, append);
            return;
        }

        Vector2Int primaryPos = primary.GridPosition;

        if (!CanPlaceWholeFormation(target, primaryPos, false))
        {
            Debug.Log("Cannot queue formation move: at least one target tile is invalid.");
            return;
        }

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

            Vector2Int offset = unit.GridPosition - primaryPos;
            Vector2Int unitTarget = target + offset;

            unit.QueueMove(unitTarget, append);
        }
    }

    private bool CanPlaceWholeFormation(Vector2Int target, Vector2Int primaryPos, bool requireDeploymentZone)
    {
        HashSet<Vector2Int> plannedTargets = new HashSet<Vector2Int>();

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

            Vector2Int offset = unit.GridPosition - primaryPos;
            Vector2Int unitTarget = target + offset;

            if (!grid.IsInside(unitTarget))
                return false;

            if (!grid.IsWalkable(unitTarget))
                return false;

            if (requireDeploymentZone)
            {
                if (deploymentManager == null || !deploymentManager.IsInsideDeploymentZone(unitTarget))
                    return false;
            }

            if (plannedTargets.Contains(unitTarget))
                return false;

            GameUnit occupyingUnit = grid.GetUnitAt(unitTarget);

            if (occupyingUnit != null && !selectedUnits.Contains(occupyingUnit))
                return false;

            plannedTargets.Add(unitTarget);
        }

        return true;
    }

    private void QueueGroupAttack(GameUnit clickedEnemy, bool append)
    {
        if (clickedEnemy == null)
            return;

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

            unit.QueueAttack(clickedEnemy, append);
        }

        Debug.Log($"Group attack queued: {selectedUnits.Count} unit(s) -> {clickedEnemy.name}");
    }

    private bool IsAppendQueueHeld()
    {
        return IsKeyHeld("AppendQueue", KeyCode.LeftControl);
    }

    private GameUnit GetPlayerUnitUnderMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, unitMask);

        for (int i = 0; i < hits.Length; i++)
        {
            GameUnit unit = hits[i].collider.GetComponentInParent<GameUnit>();
            if (unit != null && !unit.IsDead && !unit.IsBroken && unit.TeamId == 0)
                return unit;
        }

        return null;
    }

    private GameUnit GetEnemyUnderMouse()
    {
        GameUnit actor = PrimarySelected;
        if (actor == null || actor.IsDead) return null;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, unitMask);

        for (int i = 0; i < hits.Length; i++)
        {
            GameUnit unit = hits[i].collider.GetComponentInParent<GameUnit>();

            if (unit != null && !unit.IsDead && unit.TeamId != actor.TeamId)
                return unit;
        }

        return null;
    }

    private void HandleOrderInput()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (selectedUnits.Count == 0) return;

        if (IsKeyDown("OrderMarch", KeyCode.Alpha1))
            SetOrderForSelection(OrderType.March);

        if (IsKeyDown("OrderCharge", KeyCode.Alpha2))
            SetOrderForSelection(OrderType.Charge);

        if (IsKeyDown("OrderShoot", KeyCode.Alpha3))
            SetOrderForSelection(OrderType.Shoot);

        if (IsKeyDown("OrderRetreat", KeyCode.Alpha4))
            TrySetManualRetreat();
    }

    private void TrySetManualRetreat()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (turnManager != null && !turnManager.IsPlanningPhase) return;

        if (selectedUnits.Count != 1)
        {
            Debug.Log("Manual retreat can be assigned only to one selected unit.");
            blockedFailedRetreatInput = true;
            return;
        }

        GameUnit unit = selectedUnits[0];
        if (unit == null || unit.IsDead || unit.IsBroken)
        {
            blockedFailedRetreatInput = true;
            return;
        }

        if (!unit.CanUseManualRetreat())
        {
            Debug.Log($"Manual retreat cooldown remaining: {unit.GetManualRetreatCooldownRemaining()} turn(s).");
            blockedFailedRetreatInput = true;
            return;
        }

        bool applied = unit.TrySetOrder(OrderType.Retreat);
        blockedFailedRetreatInput = !applied;

        if (applied)
            Debug.Log("Selection -> applied order: Retreat to 1 unit.");
    }

    private void SetOrderForSelection(OrderType order)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (turnManager != null && !turnManager.IsPlanningPhase) return;

        blockedFailedRetreatInput = false;

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit != null && !unit.IsDead && !unit.IsBroken)
                unit.TrySetOrder(order);
        }

        Debug.Log($"Selection -> applied order: {order} to {selectedUnits.Count} unit(s)");
    }

    private void HandleClearPlannedInput()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (selectedUnits.Count == 0) return;

        if (IsKeyDown("ClearOrders", KeyCode.Delete))
        {
            blockedFailedRetreatInput = false;

            for (int i = 0; i < selectedUnits.Count; i++)
            {
                GameUnit unit = selectedUnits[i];
                if (unit != null && !unit.IsDead && !unit.IsBroken)
                    unit.ClearPlannedAction();
            }

            Debug.Log($"Cleared planned commands for {selectedUnits.Count} unit(s)");
        }
    }

    private void UpdateRangeHighlight()
    {
        if (grid == null)
            return;

        if (turnManager != null && turnManager.IsBattleEnded)
        {
            grid.ClearHighlights();
            return;
        }

        GameUnit unit = PrimarySelected;

        if (unit == null || unit.IsDead || unit.IsBroken)
        {
            grid.ClearHighlights();
            return;
        }

        if (unit.CurrentOrder == OrderType.Shoot && unit.Stats != null && unit.Stats.canShoot)
            grid.HighlightShootRange(unit.GridPosition, unit.GetCurrentShootRange());
        else
            grid.ClearHighlights();
    }

    private void ClearSelection()
    {
        blockedFailedRetreatInput = false;

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            if (selectedUnits[i] != null)
                selectedUnits[i].SetSelected(false);
        }

        selectedUnits.Clear();

        if (cameraController != null)
            cameraController.DragEnabled = true;

        if (grid != null)
            grid.ClearHighlights();
    }

    private void CancelCurrentInputState()
    {
        leftMouseDown = false;
        isBoxSelecting = false;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private Rect GetScreenRect(Vector2 p1, Vector2 p2)
    {
        Vector2 min = Vector2.Min(p1, p2);
        Vector2 max = Vector2.Max(p1, p2);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void OnGUI()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (!isBoxSelecting) return;

        Rect rect = GetScreenRect(dragStartScreen, dragCurrentScreen);

        Rect guiRect = new Rect(
            rect.xMin,
            Screen.height - rect.yMax,
            rect.width,
            rect.height
        );

        GUI.color = new Color(0.2f, 0.8f, 1f, 0.15f);
        GUI.DrawTexture(guiRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        DrawGuiRectBorder(guiRect, 2f);

        GUI.color = Color.white;
    }

    private void DrawGuiRectBorder(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
    }
}