using System.Collections.Generic;
using UnityEngine;

public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitMask;
    [SerializeField] private GridManager grid;
    [SerializeField] private DeploymentManager deploymentManager;
    [SerializeField] private CameraController2D cameraController;
    [SerializeField] private TurnManager turnManager;

    [Header("Box Selection")]
    [SerializeField] private float dragThreshold = 12f;

    [Header("Manual Retreat Rules")]
    [SerializeField, Min(1)] private int retreatCooldownTurns = 5;

    public GameUnit Selected => PrimarySelected;

    private readonly List<GameUnit> selectedUnits = new List<GameUnit>();

    private bool leftMouseDown;
    private bool isBoxSelecting;
    private Vector2 dragStartScreen;
    private Vector2 dragCurrentScreen;
    private bool selectionLockedByBattleEnd = false;

    private int lastManualRetreatTurn = -9999;

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

        HandleLeftMouseSelection();
        HandleRightClick_Action();
        HandleOrderInput();
        HandleClearPlannedInput();
        UpdateRangeHighlight();
    }

    private void HandleLeftMouseSelection()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;

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
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (isBoxSelecting)
                FinishBoxSelection(shift);
            else
                HandleSingleClickSelection(shift);

            leftMouseDown = false;
            isBoxSelecting = false;
        }
    }

    private void HandleSingleClickSelection(bool additive)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;

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
        if (turnManager != null && turnManager.IsBattleEnded) return;

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

            Vector3 sp = cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z < 0f) continue;

            if (rect.Contains(new Vector2(sp.x, sp.y), true))
                AddSelection(unit);
        }
    }

    private void AddSelection(GameUnit unit)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;

        if (unit == null) return;
        if (unit.IsDead) return;
        if (unit.IsBroken) return;
        if (selectedUnits.Contains(unit)) return;

        selectedUnits.Add(unit);
        unit.SetSelected(true);

        if (cameraController != null)
            cameraController.DragEnabled = false;
    }

    private void HandleRightClick_Action()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (!Input.GetMouseButtonDown(1)) return;
        if (turnManager != null && !turnManager.IsPlanningPhase) return;

        GameUnit selected = PrimarySelected;
        if (selected == null || selected.IsDead || selected.IsBroken || grid == null) return;

        bool append = IsAppendQueueHeld();

        GameUnit clickedEnemy = GetEnemyUnderMouse();

        if (clickedEnemy != null)
        {
            selected.QueueAttack(clickedEnemy, append);
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
            if (!deploymentManager.IsInsideDeploymentZone(target)) return;
            if (grid.IsOccupied(target)) return;

            selected.SnapToGrid(target);
            return;
        }

        if (selected.CurrentOrder == OrderType.Retreat)
        {
            if (selectedUnits.Count != 1)
            {
                Debug.Log("Manual retreat can be assigned only to one selected unit.");
                return;
            }

            if (!grid.IsValidManualRetreatDestination(selected, target))
            {
                Debug.Log("Invalid retreat destination: only back/left/right relative to nearest enemy and not next to enemies.");
                return;
            }
        }

        if (selectedUnits.Count == 1)
        {
            selected.QueueMove(target, append);
            return;
        }

        Vector2Int primaryPos = selected.GridPosition;

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

            Vector2Int offset = unit.GridPosition - primaryPos;
            Vector2Int unitTarget = target + offset;

            if (!grid.IsInside(unitTarget))
                continue;

            unit.QueueMove(unitTarget, append);
        }
    }

    private bool IsAppendQueueHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            SetOrderForSelection(OrderType.March);

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            SetOrderForSelection(OrderType.Charge);

        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            SetOrderForSelection(OrderType.Shoot);

        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            TrySetManualRetreat();
    }

    private void TrySetManualRetreat()
    {
        if (turnManager != null && turnManager.IsBattleEnded)
            return;

        if (selectedUnits.Count != 1)
        {
            Debug.Log("Manual retreat can be assigned only to one selected unit.");
            return;
        }

        GameUnit unit = selectedUnits[0];
        if (unit == null || unit.IsDead || unit.IsBroken)
            return;

        int currentTurn = turnManager != null ? turnManager.TurnNumber : 0;
        int turnsSinceLastRetreat = currentTurn - lastManualRetreatTurn;

        if (turnsSinceLastRetreat < retreatCooldownTurns)
        {
            int remaining = retreatCooldownTurns - turnsSinceLastRetreat;
            Debug.Log($"Manual retreat on cooldown. Available in {remaining} turn(s).");
            return;
        }

        unit.SetOrder(OrderType.Retreat);
        lastManualRetreatTurn = currentTurn;

        Debug.Log($"Selection -> applied order: Retreat to 1 unit. Cooldown: {retreatCooldownTurns} turns.");
    }

    private void SetOrderForSelection(OrderType order)
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;

        for (int i = 0; i < selectedUnits.Count; i++)
        {
            GameUnit unit = selectedUnits[i];
            if (unit != null && !unit.IsDead && !unit.IsBroken)
                unit.SetOrder(order);
        }

        Debug.Log($"Selection -> applied order: {order} to {selectedUnits.Count} unit(s)");
    }

    private void HandleClearPlannedInput()
    {
        if (turnManager != null && turnManager.IsBattleEnded) return;
        if (selectedUnits.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.Delete))
        {
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
        {
            grid.HighlightShootRange(unit.GridPosition, unit.GetCurrentShootRange());
        }
        else
        {
            grid.ClearHighlights();
        }
    }

    private void ClearSelection()
    {
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