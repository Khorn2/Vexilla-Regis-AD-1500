using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameUnit : MonoBehaviour
{
    public static readonly List<GameUnit> AllUnits = new List<GameUnit>();

    [Header("Visuals")]
    [SerializeField] private GameObject selectionRing;
    [SerializeField] private GameObject brokenOverlay;

    [Header("Data")]
    [SerializeField] private UnitStats stats;
    [SerializeField] private OrderType currentOrder = OrderType.March;
    
    [Header("Order Modifiers")]
    [SerializeField, Min(1f)] private float chargeMovementRangeMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float chargeMoveSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float retreatMovementRangeMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float retreatMoveSpeedMultiplier = 1.75f;

    [Header("Team")]
    [SerializeField] private int teamId = 0;

    [Header("Ranged Through Ally")]
    [SerializeField] private bool allowShotThroughOneAlly = false;
    [SerializeField, Range(0f, 1f)] private float targetDamageMultiplierThroughAlly = 0.85f;
    [SerializeField, Range(0f, 1f)] private float allyDamageMultiplier = 0.25f;

    [Header("Terrain Combat Modifiers")]
    [SerializeField, Range(0f, 1f)] private float forestRangedDamageMultiplier = 0.65f;

    public bool IsSelected { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool IsMoving => _moveRoutine != null;
    public OrderType CurrentOrder => currentOrder;
    public int TeamId => teamId;
    public int CurrentSize => currentSize;
    public int CurrentMorale => currentMorale;
    public int CurrentAmmo => currentAmmo;
    public UnitStats Stats => stats;
    public IReadOnlyList<PlannedCommand> PlannedCommands => plannedCommands;
    public bool IsDead => currentSize <= 0;
    public bool IsBroken { get; private set; }
    public bool IsRetreating => currentOrder == OrderType.Retreat;
    public bool CanReceiveOrders => !IsBroken && !IsDead;
    public bool ShowCommandPreview => teamId == 0 && !IsBroken;

    private readonly List<PlannedCommand> plannedCommands = new List<PlannedCommand>();

    private Coroutine _moveRoutine;
    private int currentSize;
    private int currentMorale;
    private int currentAmmo;
    private bool tookDamageThisTurn;
    private bool actedThisTurn;
    private bool isBeingRemoved;
    private GridManager grid;

    private void OnEnable()
    {
        if (!AllUnits.Contains(this))
            AllUnits.Add(this);
    }

    private void OnDisable()
    {
        AllUnits.Remove(this);
    }

    private void Awake()
    {
        grid = FindObjectOfType<GridManager>();

        if (stats == null)
        {
            Debug.LogError($"{name}: brak przypiętego UnitStats. Sprawdź ROOT jednostki, nie selectionRing.", this);
            enabled = false;
            return;
        }

        currentSize = stats.unitSize;
        currentMorale = stats.maxMorale;
        currentAmmo = stats.maxAmmo;

        if (selectionRing != null)
            selectionRing.SetActive(false);

        if (brokenOverlay != null)
        brokenOverlay.SetActive(false);
    }

    private void Start()
    {
        if (!enabled) return;

        Vector2Int startPos = new Vector2Int(
            Mathf.RoundToInt(transform.position.x),
            Mathf.RoundToInt(transform.position.y)
        );

        SnapToGrid(startPos);
    }

        public void BeginTurnExecution()
        {
            if (IsDead || isBeingRemoved)
                return;

            actedThisTurn = false;
            tookDamageThisTurn = false;

            if (IsBroken && grid != null && grid.IsAtMapEdge(GridPosition))
            {
                Debug.Log($"{name} routed off the battlefield.");
                Die(UnitRemovalReason.Routed);
            }
        }

    public void ResolveTurnEnd()
    {
        if (IsDead || isBeingRemoved || stats == null)
            return;

        int recovery = tookDamageThisTurn ? 0 : stats.passiveMoraleRecovery;

        if (!actedThisTurn && !tookDamageThisTurn)
            recovery += stats.idleMoraleRecoveryBonus;

        if (recovery > 0)
        {
            currentMorale = Mathf.Clamp(currentMorale + recovery, 0, stats.maxMorale);
        }

        if (IsBroken && currentMorale >= stats.brokenMoraleThreshold)
        {
            IsBroken = false;

            if (brokenOverlay != null)
                brokenOverlay.SetActive(false);

            Debug.Log($"{name} recovered from broken state.");
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected && !IsBroken && !isBeingRemoved;

        if (selectionRing != null)
            selectionRing.SetActive(IsSelected);
    }

    public void SetOrder(OrderType order)
    {
        if (IsBroken || isBeingRemoved)
            return;

        if (order == OrderType.Shoot && !stats.canShoot)
            return;

        if (order == OrderType.Charge && !stats.canCharge)
            return;

        if (currentOrder == order)
            return;

        currentOrder = order;

        if (currentOrder == OrderType.Retreat)
            RemoveAttackCommandsFromQueue();

        Debug.Log($"{name} -> Order changed to: {currentOrder}");
    }

    public int GetMovementRange()
    {
        int range = stats.movementRange;

        switch (currentOrder)
        {
            case OrderType.Charge:
                range = Mathf.RoundToInt(range * chargeMovementRangeMultiplier);
                break;

            case OrderType.Retreat:
                range = Mathf.RoundToInt(range * retreatMovementRangeMultiplier);
                break;
        }

        return Mathf.Max(0, range);
    }

    public float GetCurrentMoveSpeed()
    {
        float speed = Mathf.Max(0.1f, stats.moveSpeedTilesPerSec);

        switch (currentOrder)
        {
            case OrderType.Charge:
                speed *= chargeMoveSpeedMultiplier;
                break;

            case OrderType.Retreat:
                speed *= retreatMoveSpeedMultiplier;
                break;
        }

        return speed;
    }

    public int GetCurrentShootRange()
    {
        int range = stats != null ? stats.shootRange : 0;

        if (grid != null)
            range += grid.GetShootRangeBonusAt(GridPosition);

        return Mathf.Max(0, range);
    }

    public float GetMeleeModifier()
    {
        float modifier = 1f;

        if (currentOrder == OrderType.Charge)
            modifier *= 1.5f;

        if (currentMorale < stats.lowMoraleThreshold)
            modifier *= stats.lowMoraleDamageMultiplier;

        if (grid != null)
            modifier *= grid.GetMeleeDamageMultiplierAt(GridPosition);

        return modifier;
    }

    public float GetRangedDamageModifier()
    {
        float modifier = 1f;

        if (currentMorale < stats.lowMoraleThreshold)
            modifier *= stats.lowMoraleDamageMultiplier;

        return modifier;
    }

    public void SnapToGrid(Vector2Int gridPos)
    {
        if (grid == null || isBeingRemoved) return;

        if (GridPosition != gridPos)
            grid.UnregisterUnit(this, GridPosition);

        if (!grid.RegisterUnit(this, gridPos))
            return;

        GridPosition = gridPos;
        transform.position = new Vector3(gridPos.x, gridPos.y, 0f);
    }

    public void MoveToGrid(Vector2Int desiredTargetGrid, Action onComplete = null)
    {
        if (currentOrder == OrderType.Shoot || isBeingRemoved)
        {
            onComplete?.Invoke();
            return;
        }

        if (grid == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector2Int startGrid = GridPosition;
        Vector2Int resolvedTarget = grid.ResolveMoveDestination(this, GridPosition, desiredTargetGrid);

        if (resolvedTarget == GridPosition)
        {
            onComplete?.Invoke();
            return;
        }

        if (!grid.MoveUnit(this, GridPosition, resolvedTarget))
        {
            onComplete?.Invoke();
            return;
        }

        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        actedThisTurn = true;
        _moveRoutine = StartCoroutine(MoveRoutine(startGrid, resolvedTarget, onComplete));
    }

    private IEnumerator MoveRoutine(Vector2Int startGrid, Vector2Int targetGrid, Action onComplete)
    {
        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetGrid.x, targetGrid.y, 0f);

        if (startGrid == targetGrid)
        {
            GridPosition = targetGrid;
            _moveRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        float movementCost = grid != null
            ? Mathf.Max(1f, grid.GetTravelCostAlongLine(this, startGrid, targetGrid))
            : Vector3.Distance(start, end);

        float duration = movementCost / GetCurrentMoveSpeed();
        duration = Mathf.Max(0.01f, duration);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = end;
        GridPosition = targetGrid;
        _moveRoutine = null;

        onComplete?.Invoke();
    }

    public bool IsAdjacentTo(GameUnit target)
    {
        if (target == null) return false;

        float dist = Vector2Int.Distance(GridPosition, target.GridPosition);
        return dist <= 1.5f;
    }

    public bool HasAdjacentEnemy()
    {
        if (grid == null) return false;

        Vector2Int[] neighbours = grid.GetNeighbours4(GridPosition);

        for (int i = 0; i < neighbours.Length; i++)
        {
            GameUnit other = grid.GetUnitAt(neighbours[i]);
            if (other != null && other.TeamId != TeamId)
                return true;
        }

        return false;
    }

    public void AttackMelee(GameUnit target)
    {
        if (currentOrder == OrderType.Retreat || IsBroken || isBeingRemoved)
        {
            Debug.Log($"{name} cannot attack in melee while retreating or broken.");
            return;
        }

        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!IsAdjacentTo(target)) return;

        int rawDamage = Mathf.RoundToInt(stats.meleeDamage * GetMeleeModifier());
        actedThisTurn = true;
        target.TakeMeleeDamage(this, rawDamage);
    }

    public void Shoot(GameUnit target)
    {
        if (currentOrder == OrderType.Retreat || IsBroken || isBeingRemoved)
        {
            Debug.Log($"{name} cannot shoot while retreating or broken.");
            return;
        }

        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!stats.canShoot) return;
        if (currentOrder != OrderType.Shoot) return;

        if (currentAmmo < stats.ammoPerShot)
        {
            Debug.Log($"{name} cannot shoot: insufficient ammo ({currentAmmo}/{stats.ammoPerShot})");
            return;
        }

        if (HasAdjacentEnemy())
        {
            Debug.Log($"{name} cannot shoot: enemy is adjacent.");
            return;
        }

        float dist = Vector2Int.Distance(GridPosition, target.GridPosition);
        if (dist > GetCurrentShootRange()) return;

        List<Vector2Int> line = GetLinePoints(GridPosition, target.GridPosition);

        int alliesOnLine = 0;
        GameUnit allyHit = null;

        for (int i = 1; i < line.Count - 1; i++)
        {
            GameUnit unitOnLine = grid != null ? grid.GetUnitAt(line[i]) : null;
            if (unitOnLine == null) continue;

            if (unitOnLine.TeamId == TeamId)
            {
                alliesOnLine++;
                if (allyHit == null)
                    allyHit = unitOnLine;
            }
        }

        if (alliesOnLine >= 2)
        {
            Debug.Log($"{name} cannot shoot: two allies block the shot.");
            return;
        }

        int ammoBefore = currentAmmo;
        currentAmmo = Mathf.Max(0, currentAmmo - stats.ammoPerShot);
        int ammoSpent = ammoBefore - currentAmmo;
        Debug.Log($"{name} fired: ammo -{ammoSpent}, remaining: {currentAmmo}");

        actedThisTurn = true;

        int baseRangedDamage = Mathf.RoundToInt(stats.rangedDamage * GetRangedDamageModifier());

        if (alliesOnLine == 1)
        {
            if (!allowShotThroughOneAlly)
            {
                Debug.Log($"{name} cannot shoot: ally blocks the shot.");
                return;
            }

            int rawTargetDmg = Mathf.RoundToInt(baseRangedDamage * targetDamageMultiplierThroughAlly);
            int rawAllyDmg = Mathf.RoundToInt(baseRangedDamage * allyDamageMultiplier);

            target.TakeRangedDamage(this, rawTargetDmg);

            if (allyHit != null)
                allyHit.TakeRangedDamage(this, rawAllyDmg);

            return;
        }

        target.TakeRangedDamage(this, baseRangedDamage);
    }

    public void TakeDamage(int dmg)
    {
        currentSize -= dmg;
        Debug.Log($"{name} took {dmg} damage. Current size: {currentSize}");

        if (currentSize <= 0)
            Die();
    }

    public void TakeMeleeDamage(GameUnit attacker, int rawDamage)
    {
        if (isBeingRemoved)
            return;

        int finalDamage = ApplyArmorReduction(rawDamage, false);

        currentSize -= finalDamage;
        ApplyMoraleLoss(finalDamage);

        Debug.Log($"{name} took {finalDamage} melee damage. Current size: {currentSize}, morale: {currentMorale}");

        if (currentSize <= 0)
            Die();
    }

    public void TakeRangedDamage(GameUnit attacker, int rawDamage)
    {
        if (isBeingRemoved)
            return;

        int damageAfterTerrain = ApplyTerrainDefenseToRangedDamage(rawDamage);
        int finalDamage = ApplyArmorReduction(damageAfterTerrain, true);

        currentSize -= finalDamage;
        ApplyMoraleLoss(finalDamage);

        Debug.Log($"{name} took {finalDamage} ranged damage. Current size: {currentSize}, morale: {currentMorale}");

        if (currentSize <= 0)
            Die();
    }

    private int ApplyArmorReduction(int rawDamage, bool againstRanged)
    {
        float armor = 0f;

        if (!IsRetreating)
        {
            armor = stats != null ? stats.armorPercent : 0f;

            if (grid != null)
                armor += grid.GetArmorBonusPercentAt(GridPosition);

            if (againstRanged)
                armor *= 0.5f;
        }

        armor = Mathf.Clamp01(armor);
        int reduced = Mathf.RoundToInt(rawDamage * (1f - armor));
        return Mathf.Max(0, reduced);
    }

    private int ApplyTerrainDefenseToRangedDamage(int baseDamage)
    {
        if (grid != null && grid.IsForestAt(GridPosition))
        {
            return Mathf.Max(0, Mathf.RoundToInt(baseDamage * forestRangedDamageMultiplier));
        }

        return Mathf.Max(0, baseDamage);
    }

    private void ApplyMoraleLoss(int finalDamage)
    {
        tookDamageThisTurn = true;

        float cohesionModifier = 1f;

        if (grid != null && stats != null)
            cohesionModifier = grid.GetMoraleCohesionModifier(this);

        int moraleLoss = Mathf.RoundToInt(
            finalDamage * stats.moraleDamagePerLostUnit * cohesionModifier
        );

        currentMorale = Mathf.Max(0, currentMorale - moraleLoss);

        Debug.Log($"{name} morale loss: -{moraleLoss} | cohesion x{cohesionModifier:F2} | morale: {currentMorale}");

        int lowUnitThreshold = Mathf.CeilToInt(stats.unitSize * 0.2f);
        if (currentSize > 0 && currentSize <= lowUnitThreshold)
            currentMorale = 0;

        if (!IsBroken && currentMorale < stats.brokenMoraleThreshold)
            BreakUnit();
    }

        private void BreakUnit()
    {
        IsBroken = true;
        currentOrder = OrderType.Retreat;
        RemoveAttackCommandsFromQueue();
        plannedCommands.Clear();
        SetSelected(false);

        if (brokenOverlay != null)
            brokenOverlay.SetActive(true);

        Debug.Log($"{name} is broken and starts routing.");
    }

    private void Die(UnitRemovalReason reason = UnitRemovalReason.Killed)
    {
        if (isBeingRemoved)
            return;

        isBeingRemoved = true;

        BattleStatsTracker tracker = FindFirstObjectByType<BattleStatsTracker>();
        if (tracker != null)
        {
            if (reason == UnitRemovalReason.Routed)
                tracker.MarkUnitRouted(this);
            else
                tracker.MarkUnitKilled(this);
        }

        currentSize = 0;
        IsSelected = false;

        if (selectionRing != null)
            selectionRing.SetActive(false);

        if (brokenOverlay != null)
            brokenOverlay.SetActive(false);

        if (grid != null)
            grid.UnregisterUnit(this, GridPosition);

        AllUnits.Remove(this);

        BattleResultChecker battleResultChecker = FindFirstObjectByType<BattleResultChecker>();
        if (battleResultChecker != null)
            battleResultChecker.CheckBattleResult();

        Destroy(gameObject);
    }

    public void ClearPlannedAction()
    {
        plannedCommands.Clear();
    }

    public void QueueMove(Vector2Int target, bool append)
    {
        if (IsBroken || isBeingRemoved)
            return;

        if (!append)
            plannedCommands.Clear();

        plannedCommands.Add(new PlannedCommand(PlannedCommandType.Move, target));
        Debug.Log($"{name} queued MOVE to {target}");
    }

    public void QueueAttack(GameUnit target, bool append)
    {
        if (currentOrder == OrderType.Retreat || IsBroken || isBeingRemoved)
        {
            Debug.Log($"{name} is retreating/broken and cannot queue attack commands.");
            return;
        }

        if (target == null) return;

        if (!append)
            plannedCommands.Clear();

        PlannedCommandType type =
            currentOrder == OrderType.Shoot
            ? PlannedCommandType.AttackShoot
            : PlannedCommandType.AttackMelee;

        plannedCommands.Add(new PlannedCommand(type, target));
        Debug.Log($"{name} queued {type} on {target.name}");
    }

    public IEnumerator ExecutePlannedAction()
    {
        if (IsDead || isBeingRemoved)
            yield break;

        if (IsBroken)
        {
            yield return ExecuteBrokenRetreat();
            yield break;
        }

        if (plannedCommands.Count == 0)
            yield break;

        int remainingMovement = grid != null ? grid.GetMovementBudgetForUnit(this) : GetMovementRange();
        int safety = 0;

        while (plannedCommands.Count > 0 && safety < 32)
        {
            safety++;

            PlannedCommand cmd = plannedCommands[0];
            if (cmd == null)
            {
                plannedCommands.RemoveAt(0);
                continue;
            }

            switch (cmd.commandType)
            {
                case PlannedCommandType.Move:
                {
                    if (GridPosition == cmd.targetGridPosition)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    if (remainingMovement <= 0)
                        yield break;

                    Vector2Int startPos = GridPosition;

                    Vector2Int stepTarget = grid != null
                        ? grid.GetReachablePointAlongLine(this, startPos, cmd.targetGridPosition, remainingMovement, out _)
                        : cmd.targetGridPosition;

                    int movementSpent = 0;
                    if (grid != null)
                        movementSpent = grid.GetTravelCostAlongLine(this, startPos, stepTarget);

                    bool finished = false;
                    MoveToGrid(stepTarget, () => finished = true);

                    while (!finished)
                        yield return null;

                    remainingMovement -= movementSpent;
                    remainingMovement = Mathf.Max(0, remainingMovement);

                    if (GridPosition == cmd.targetGridPosition)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    yield break;
                }

                case PlannedCommandType.AttackShoot:
                {
                    if (currentOrder == OrderType.Retreat || IsBroken)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    if (cmd.targetUnit == null)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    Shoot(cmd.targetUnit);
                    yield break;
                }

                case PlannedCommandType.AttackMelee:
                {
                    if (currentOrder == OrderType.Retreat || IsBroken)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    if (cmd.targetUnit == null)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    if (IsAdjacentTo(cmd.targetUnit))
                    {
                        AttackMelee(cmd.targetUnit);
                        yield break;
                    }

                    if (remainingMovement <= 0)
                        yield break;

                    if (grid != null && grid.TryGetFreeAdjacentTile(cmd.targetUnit.GridPosition, GridPosition, out Vector2Int attackTile))
                    {
                        Vector2Int startPos = GridPosition;
                        Vector2Int stepTarget = grid.GetReachablePointAlongLine(this, startPos, attackTile, remainingMovement, out int spentCost);

                        bool finished = false;
                        MoveToGrid(stepTarget, () => finished = true);

                        while (!finished)
                            yield return null;

                        remainingMovement -= spentCost;
                        remainingMovement = Mathf.Max(0, remainingMovement);

                        if (IsAdjacentTo(cmd.targetUnit))
                            AttackMelee(cmd.targetUnit);

                        yield break;
                    }

                    plannedCommands.RemoveAt(0);
                    continue;
                }
            }
        }
    }

    private IEnumerator ExecuteBrokenRetreat()
    {
        if (grid == null || isBeingRemoved)
            yield break;

        if (grid.IsAtMapEdge(GridPosition))
            yield break;

        Vector2Int retreatTarget = grid.GetNearestEdgePosition(GridPosition);
        int remainingMovement = grid.GetMovementBudgetForUnit(this);

        Vector2Int stepTarget = grid.GetReachablePointAlongLine(this, GridPosition, retreatTarget, remainingMovement, out _);

        if (stepTarget == GridPosition)
            yield break;

        bool finished = false;
        MoveToGrid(stepTarget, () => finished = true);

        while (!finished)
            yield return null;
    }

    public void SetTeam(int newTeamId)
    {
        teamId = newTeamId;
    }

    private void RemoveAttackCommandsFromQueue()
    {
        for (int i = plannedCommands.Count - 1; i >= 0; i--)
        {
            PlannedCommandType type = plannedCommands[i].commandType;

            if (type == PlannedCommandType.AttackMelee || type == PlannedCommandType.AttackShoot)
                plannedCommands.RemoveAt(i);
        }
    }

    private List<Vector2Int> GetLinePoints(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            result.Add(new Vector2Int(x0, y0));

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return result;
    }
}