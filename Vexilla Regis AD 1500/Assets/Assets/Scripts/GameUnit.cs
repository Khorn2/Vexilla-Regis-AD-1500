using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameUnit : MonoBehaviour
{
    private enum UnitRemovalReason
    {
        Killed,
        Routed
    }

    private const int ManualRetreatCooldownTurns = 2;

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

    [Header("Flanking Debug")]
    [SerializeField] private bool logFlankingResults = false;

    public bool IsSelected { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool IsMoving => _moveRoutine != null;
    public OrderType CurrentOrder => currentOrder;
    public int TeamId => teamId;
    public int CurrentSize => currentSize;
    public int CurrentMorale => currentMorale;
    public int CurrentAmmo => currentAmmo;
    public UnitStats Stats => stats;
    public string DisplayName => stats != null && !string.IsNullOrWhiteSpace(stats.unitName) ? stats.unitName : name;
    public IReadOnlyList<PlannedCommand> PlannedCommands => plannedCommands;
    public bool IsDead => currentSize <= 0;
    public bool IsBroken { get; private set; }
    public bool IsRetreating => currentOrder == OrderType.Retreat;
    public bool CanReceiveOrders => !IsBroken && !IsDead;
    public bool ShowCommandPreview => teamId == 0 && !IsBroken;

    private readonly List<PlannedCommand> plannedCommands = new List<PlannedCommand>(8);
    private readonly List<Vector2Int> directPathBuffer = new List<Vector2Int>(64);
    private readonly List<Vector2Int> lineBuffer = new List<Vector2Int>(64);
    private readonly List<GameUnit> adjacentEnemiesBuffer = new List<GameUnit>(4);

    private Coroutine _moveRoutine;
    private int currentSize;
    private int currentMorale;
    private int currentAmmo;
    private int tilesMovedThisTurn;
    private int manualRetreatAvailableTurn = 0;
    private bool tookDamageThisTurn;
    private bool actedThisTurn;
    private bool isBeingRemoved;
    private bool reachedMapEdgeWhileRouting;

    private GridManager grid;
    private TurnManager turnManager;
    private GridPathfinder pathfinder;
    private BattleStatsTracker battleStatsTracker;
    private BattleResultChecker battleResultChecker;

    private void OnEnable()
    {
        if (!AllUnits.Contains(this))
            AllUnits.Add(this);
    }

    private void OnDisable()
    {
        StopMovementSound();
        AllUnits.Remove(this);
    }

    private void Awake()
    {
        grid = FindObjectOfType<GridManager>();
        turnManager = FindObjectOfType<TurnManager>();
        battleStatsTracker = FindFirstObjectByType<BattleStatsTracker>();
        battleResultChecker = FindFirstObjectByType<BattleResultChecker>();

        if (grid != null)
            pathfinder = new GridPathfinder(grid);

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

    private int GetCurrentTurnNumber()
    {
        return turnManager != null ? turnManager.TurnNumber : 0;
    }

    public bool CanUseManualRetreat()
    {
        if (stats == null) return false;
        if (IsDead || IsBroken || isBeingRemoved) return false;

        return GetCurrentTurnNumber() >= manualRetreatAvailableTurn;
    }

    public int GetManualRetreatCooldownRemaining()
    {
        return Mathf.Max(0, manualRetreatAvailableTurn - GetCurrentTurnNumber());
    }

    public bool CanApplyOrder(OrderType order)
    {
        if (stats == null) return false;
        if (IsDead || IsBroken || isBeingRemoved) return false;
        if (order == OrderType.Shoot && !stats.canShoot) return false;
        if (order == OrderType.Charge && !stats.canCharge) return false;
        if (order == OrderType.Retreat && !CanUseManualRetreat()) return false;

        return true;
    }

    private void RegisterManualRetreatExecuted()
    {
        manualRetreatAvailableTurn = GetCurrentTurnNumber() + ManualRetreatCooldownTurns + 1;

        currentOrder = OrderType.March;
        plannedCommands.Clear();

        Debug.Log($"{name} used manual retreat. Available again on turn {manualRetreatAvailableTurn}.");
    }

    public void BeginTurnExecution()
    {
        if (IsDead || isBeingRemoved)
            return;

        tilesMovedThisTurn = 0;
        actedThisTurn = false;
        tookDamageThisTurn = false;

        if (IsBroken && reachedMapEdgeWhileRouting && grid != null && grid.IsAtMapEdge(GridPosition))
        {
            Debug.Log($"{name} routed off the battlefield.");
            Die(UnitRemovalReason.Routed);
        }
    }

    public void ResolveTurnEnd()
    {
        if (IsDead || isBeingRemoved || stats == null)
            return;

        if (IsBroken)
        {
            ResolveBrokenMoraleRecovery();
            return;
        }

        int recovery = tookDamageThisTurn ? 0 : stats.passiveMoraleRecovery;

        if (!actedThisTurn && !tookDamageThisTurn)
            recovery += stats.idleMoraleRecoveryBonus;

        if (recovery > 0)
            currentMorale = Mathf.Clamp(currentMorale + recovery, 0, stats.maxMorale);
    }

    private void ResolveBrokenMoraleRecovery()
    {
        if (stats == null)
            return;

        if (!tookDamageThisTurn && stats.brokenMoraleRecoveryPerTurn > 0)
            currentMorale = Mathf.Clamp(currentMorale + stats.brokenMoraleRecoveryPerTurn, 0, stats.maxMorale);

        if (tookDamageThisTurn)
            return;

        if (currentMorale < stats.brokenMoraleThreshold)
            return;

        if (UnityEngine.Random.value > stats.brokenRallyChance)
            return;

        IsBroken = false;
        reachedMapEdgeWhileRouting = false;
        currentOrder = OrderType.March;
        currentMorale = Mathf.Max(currentMorale + stats.rallyMoraleGain, stats.rallyMinimumMorale);
        currentMorale = Mathf.Clamp(currentMorale, 0, stats.maxMorale);

        if (brokenOverlay != null)
            brokenOverlay.SetActive(false);

        Debug.Log($"{name} rallied and returned to battle.");
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected && !IsBroken && !isBeingRemoved;

        if (selectionRing != null)
            selectionRing.SetActive(IsSelected);
    }

    public bool TrySetOrder(OrderType order)
    {
        if (!CanApplyOrder(order))
        {
            if (order == OrderType.Retreat)
                Debug.Log($"{name} cannot retreat. Cooldown remaining: {GetManualRetreatCooldownRemaining()} turn(s).");
            else
                Debug.Log($"{name} cannot apply order: {order}");

            return false;
        }

        if (currentOrder == order)
            return true;

        if (currentOrder == OrderType.Shoot && order != OrderType.Shoot && SoundManager.Instance != null)
            SoundManager.Instance.StopRangedLoopForUnit(this);

        currentOrder = order;

        if (currentOrder == OrderType.Retreat)
            RemoveAttackCommandsFromQueue();

        Debug.Log($"{name} -> Order changed to: {currentOrder}");
        return true;
    }

    public void SetOrder(OrderType order)
    {
        TrySetOrder(order);
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

        if (!grid.IsInside(desiredTargetGrid))
        {
            onComplete?.Invoke();
            return;
        }

        if (!grid.IsWalkable(desiredTargetGrid))
        {
            onComplete?.Invoke();
            return;
        }

        GameUnit unitAtTarget = grid.GetUnitAt(desiredTargetGrid);
        if (unitAtTarget != null && unitAtTarget != this)
        {
            onComplete?.Invoke();
            return;
        }

        List<Vector2Int> path = BuildDirectOrFallbackPath(desiredTargetGrid);

        if (path == null || path.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            StopMovementSound();
        }

        actedThisTurn = true;
        _moveRoutine = StartCoroutine(FollowPath(path, onComplete));
    }

    private List<Vector2Int> BuildDirectOrFallbackPath(Vector2Int desiredTargetGrid)
    {
        if (grid == null)
            return null;

        BuildDirectLinePath(GridPosition, desiredTargetGrid, directPathBuffer);

        if (IsPathUsable(directPathBuffer))
            return directPathBuffer;

        if (pathfinder == null)
            pathfinder = new GridPathfinder(grid);

        return pathfinder.FindPath(GridPosition, desiredTargetGrid, this);
    }

    private void BuildDirectLinePath(Vector2Int start, Vector2Int end, List<Vector2Int> result)
    {
        BuildLinePoints(start, end, result);

        if (result.Count > 0 && result[0] == start)
            result.RemoveAt(0);
    }

    private bool IsPathUsable(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0)
            return false;

        Vector2Int previous = GridPosition;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int p = path[i];

            if (!grid.IsInside(p))
                return false;

            if (!grid.IsWalkable(p))
                return false;

            if (grid.GetMovementCost(previous, p, this) == int.MaxValue)
                return false;

            GameUnit unitAt = grid.GetUnitAt(p);
            if (unitAt != null && unitAt != this)
                return false;

            previous = p;
        }

        return true;
    }

    public bool IsAdjacentTo(GameUnit target)
    {
        if (target == null) return false;

        int dx = Mathf.Abs(GridPosition.x - target.GridPosition.x);
        int dy = Mathf.Abs(GridPosition.y - target.GridPosition.y);

        return dx + dy == 1;
    }

    public bool HasAdjacentEnemy()
    {
        if (grid == null) return false;

        return grid.HasAdjacentEnemyForTeam(GridPosition, TeamId);
    }

    public void AttackMelee(GameUnit target)
    {
        if (!CanPerformMelee())
        {
            Debug.Log($"{name} cannot attack in melee while retreating, broken or removed.");
            return;
        }

        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!IsAdjacentTo(target)) return;

        int rawDamage = CalculateMeleeRawDamage();
        actedThisTurn = true;

        target.TakeMeleeDamage(this, rawDamage);

        if (SoundManager.Instance != null)
        {
            Vector3 soundPosition = (transform.position + target.transform.position) * 0.5f;
            SoundManager.Instance.PlayMelee(soundPosition, 1);
        }
    }

    public void ResolveAutoMeleeCombat()
    {
        if (!CanPerformMelee())
            return;

        if (stats == null || !stats.canAutoMelee)
            return;

        if (grid == null)
            return;

        grid.FillAdjacentEnemies(this, adjacentEnemiesBuffer);

        if (adjacentEnemiesBuffer.Count == 0)
            return;

        int rawTotalDamage = Mathf.RoundToInt(stats.meleeDamage * GetMeleeModifier() * stats.autoMeleeDamageMultiplier);

        if (rawTotalDamage <= 0)
            return;

        int damagePerTarget = Mathf.Max(1, Mathf.RoundToInt((float)rawTotalDamage / adjacentEnemiesBuffer.Count));

        actedThisTurn = true;

        Debug.Log($"{name} auto melee: targets={adjacentEnemiesBuffer.Count}, totalRaw={rawTotalDamage}, perTargetRaw={damagePerTarget}");

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMelee(transform.position, adjacentEnemiesBuffer.Count);

        for (int i = 0; i < adjacentEnemiesBuffer.Count; i++)
        {
            GameUnit enemy = adjacentEnemiesBuffer[i];

            if (enemy == null) continue;
            if (enemy.IsDead) continue;
            if (enemy.TeamId == TeamId) continue;
            if (!IsAdjacentTo(enemy)) continue;

            enemy.TakeMeleeDamage(this, damagePerTarget);
        }
    }

    private int CalculateMeleeRawDamage()
    {
        int baseDamage = Mathf.RoundToInt(stats.meleeDamage * GetMeleeModifier());
        int chargeBonus = 0;

        if (currentOrder == OrderType.Charge && tilesMovedThisTurn >= stats.minTilesForChargeBonus)
        {
            chargeBonus = Mathf.RoundToInt(tilesMovedThisTurn * stats.chargeImpactPerTile);
            chargeBonus = Mathf.Min(chargeBonus, stats.maxChargeBonus);
        }

        int rawDamage = baseDamage + chargeBonus;

        if (chargeBonus > 0)
            Debug.Log($"{name} charge impact: moved={tilesMovedThisTurn}, bonus={chargeBonus}, rawDamage={rawDamage}");

        return Mathf.Max(0, rawDamage);
    }

    private bool CanPerformMelee()
    {
        if (isBeingRemoved) return false;
        if (IsDead) return false;
        if (IsBroken) return false;
        if (currentOrder == OrderType.Retreat) return false;
        if (stats == null) return false;

        return true;
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

        BuildLinePoints(GridPosition, target.GridPosition, lineBuffer);

        int alliesOnLine = 0;
        GameUnit allyHit = null;

        for (int i = 1; i < lineBuffer.Count - 1; i++)
        {
            GameUnit unitOnLine = grid != null ? grid.GetUnitAt(lineBuffer[i]) : null;
            if (unitOnLine == null) continue;

            if (unitOnLine.TeamId == TeamId)
            {
                alliesOnLine++;

                if (allyHit == null)
                    allyHit = unitOnLine;
            }
            else
            {
                return;
            }
        }

        if (alliesOnLine >= 2)
        {
            Debug.Log($"{name} cannot shoot: two allies block the shot.");
            return;
        }

        if (alliesOnLine == 1 && !allowShotThroughOneAlly)
        {
            Debug.Log($"{name} cannot shoot: ally blocks the shot.");
            return;
        }

        int ammoBefore = currentAmmo;
        currentAmmo = Mathf.Max(0, currentAmmo - stats.ammoPerShot);
        int ammoSpent = ammoBefore - currentAmmo;
        Debug.Log($"{name} fired: ammo -{ammoSpent}, remaining: {currentAmmo}");

        if (SoundManager.Instance != null)
            SoundManager.Instance.RegisterExecutedRangedShot(this, target);

        actedThisTurn = true;

        int baseRangedDamage = Mathf.RoundToInt(stats.rangedDamage * GetRangedDamageModifier());

        if (alliesOnLine == 1)
        {
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
        ApplyCasualties(dmg, "damage");

        if (currentSize <= 0)
            Die();
    }

    public void TakeMeleeDamage(GameUnit attacker, int rawDamage)
    {
        if (isBeingRemoved)
            return;

        AttackDirection attackDirection = FlankingUtility.GetAttackDirection(attacker, this);
        int modifiedRawDamage = ApplyFlankingDamageModifier(attacker, rawDamage, attackDirection);
        int finalDamage = ApplyArmorReduction(modifiedRawDamage, false);

        ApplyCasualties(finalDamage, "melee damage");
        ApplyMoraleLoss(finalDamage);
        ApplyFlankingMoralePenalty(attacker, attackDirection);

        if (logFlankingResults)
            Debug.Log($"{name} took {finalDamage} melee damage from {attackDirection}. Current size: {currentSize}, morale: {currentMorale}");
        else
            Debug.Log($"{name} took {finalDamage} melee damage. Current size: {currentSize}, morale: {currentMorale}");

        if (currentSize <= 0)
            Die();
    }

    private int ApplyFlankingDamageModifier(GameUnit attacker, int rawDamage, AttackDirection attackDirection)
    {
        if (attacker == null || attacker.Stats == null)
            return Mathf.Max(0, rawDamage);

        float multiplier = 1f;

        switch (attackDirection)
        {
            case AttackDirection.Flank:
                multiplier = attacker.Stats.flankMeleeDamageMultiplier;
                break;

            case AttackDirection.Rear:
                multiplier = attacker.Stats.rearMeleeDamageMultiplier;
                break;
        }

        int result = Mathf.RoundToInt(rawDamage * multiplier);
        return Mathf.Max(0, result);
    }

    private void ApplyFlankingMoralePenalty(GameUnit attacker, AttackDirection attackDirection)
    {
        if (attacker == null || attacker.Stats == null || stats == null)
            return;

        int penalty = 0;

        switch (attackDirection)
        {
            case AttackDirection.Flank:
                penalty = attacker.Stats.flankMoralePenalty;
                break;

            case AttackDirection.Rear:
                penalty = attacker.Stats.rearMoralePenalty;
                break;
        }

        if (penalty <= 0)
            return;

        currentMorale = Mathf.Max(0, currentMorale - penalty);

        if (logFlankingResults)
            Debug.Log($"{name} flanking morale penalty: -{penalty} from {attackDirection}. Morale: {currentMorale}");

        if (!IsBroken && currentMorale < stats.brokenMoraleThreshold)
            BreakUnit();
    }

    public void TakeRangedDamage(GameUnit attacker, int rawDamage)
    {
        if (isBeingRemoved)
            return;

        int damageAfterTerrain = ApplyTerrainDefenseToRangedDamage(rawDamage);
        int finalDamage = ApplyArmorReduction(damageAfterTerrain, true);

        ApplyCasualties(finalDamage, "ranged damage");
        ApplyMoraleLoss(finalDamage);

        Debug.Log($"{name} took {finalDamage} ranged damage. Current size: {currentSize}, morale: {currentMorale}");

        if (currentSize <= 0)
            Die();
    }

    private void ApplyCasualties(int casualties, string source)
    {
        int safeCasualties = Mathf.Max(0, casualties);
        int previousSize = currentSize;

        currentSize = Mathf.Max(0, currentSize - safeCasualties);

        if (battleStatsTracker == null)
            battleStatsTracker = FindFirstObjectByType<BattleStatsTracker>();

        if (battleStatsTracker != null)
            battleStatsTracker.UpdateUnitCurrentSize(this);

        int actualLosses = previousSize - currentSize;

        if (actualLosses > 0)
            Debug.Log($"{name} lost {actualLosses} men from {source}. Current size: {currentSize}");
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
            return Mathf.Max(0, Mathf.RoundToInt(baseDamage * forestRangedDamageMultiplier));

        return Mathf.Max(0, baseDamage);
    }

    private void ApplyMoraleLoss(int finalDamage)
    {
        tookDamageThisTurn = true;

        if (stats == null)
            return;

        float cohesionModifier = grid != null ? grid.GetMoraleCohesionModifier(this) : 1f;
        int moraleLoss = Mathf.RoundToInt(finalDamage * stats.moraleDamagePerLostUnit * cohesionModifier);

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
        StopMovementSound();

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopRangedLoopForUnit(this);

        IsBroken = true;
        reachedMapEdgeWhileRouting = false;
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

        StopMovementSound();

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopRangedLoopForUnit(this);

        isBeingRemoved = true;

        if (battleStatsTracker == null)
            battleStatsTracker = FindFirstObjectByType<BattleStatsTracker>();

        if (battleStatsTracker != null)
        {
            if (reason == UnitRemovalReason.Routed)
                battleStatsTracker.MarkUnitRouted(this);
            else
                battleStatsTracker.MarkUnitKilled(this);
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

        if (battleResultChecker == null)
            battleResultChecker = FindFirstObjectByType<BattleResultChecker>();

        if (battleResultChecker != null)
            battleResultChecker.CheckBattleResult();

        Destroy(gameObject);
    }

    public void ClearPlannedAction()
    {
        plannedCommands.Clear();
        StopMovementSound();

        if (SoundManager.Instance != null)
            SoundManager.Instance.StopRangedLoopForUnit(this);
    }

    public void QueueMove(Vector2Int target, bool append)
    {
        if (IsBroken || isBeingRemoved)
            return;

        if (currentOrder == OrderType.Retreat && !CanUseManualRetreat())
        {
            Debug.Log($"{name} cannot queue retreat move. Cooldown remaining: {GetManualRetreatCooldownRemaining()} turn(s).");
            return;
        }

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
        {
            plannedCommands.Clear();

            if (currentOrder == OrderType.Shoot && SoundManager.Instance != null)
                SoundManager.Instance.StopRangedLoopForUnit(this);
        }

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
                    OrderType orderAtMoveStart = currentOrder;

                    Vector2Int stepTarget = grid != null
                        ? grid.GetReachablePointAlongLine(this, startPos, cmd.targetGridPosition, remainingMovement, out int spentCost)
                        : cmd.targetGridPosition;

                    if (stepTarget == startPos)
                        yield break;

                    int finalSpentCost = grid != null
                        ? grid.GetTravelCostAlongLine(this, startPos, stepTarget)
                        : 0;

                    if (finalSpentCost == int.MaxValue)
                        yield break;

                    bool finished = false;
                    MoveToGrid(stepTarget, () => finished = true);

                    while (!finished)
                        yield return null;

                    bool moved = GridPosition != startPos;

                    if (orderAtMoveStart == OrderType.Retreat && moved)
                    {
                        RegisterManualRetreatExecuted();
                        yield break;
                    }

                    remainingMovement -= finalSpentCost;
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

                        if (stepTarget == startPos || spentCost == int.MaxValue)
                            yield break;

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
        {
            reachedMapEdgeWhileRouting = true;
            yield break;
        }

        int remainingMovement = grid.GetMovementBudgetForUnit(this);
        int safety = 0;

        while (remainingMovement > 0 && safety < 16)
        {
            safety++;

            if (grid.IsAtMapEdge(GridPosition))
            {
                reachedMapEdgeWhileRouting = true;
                yield break;
            }

            Vector2Int startPos = GridPosition;
            Vector2Int nextStep = grid.GetBestRoutingStep(this);

            if (nextStep == startPos)
                yield break;

            int stepCost = grid.GetMovementCost(startPos, nextStep, this);

            if (stepCost == int.MaxValue)
                yield break;

            if (stepCost > remainingMovement)
                yield break;

            bool finished = false;
            MoveToGrid(nextStep, () => finished = true);

            while (!finished)
                yield return null;

            if (GridPosition == startPos)
                yield break;

            remainingMovement -= stepCost;
        }

        if (grid.IsAtMapEdge(GridPosition))
            reachedMapEdgeWhileRouting = true;
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

    private void BuildLinePoints(Vector2Int start, Vector2Int end, List<Vector2Int> result)
    {
        result.Clear();

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
    }

    private IEnumerator FollowPath(List<Vector2Int> path, Action onComplete)
    {
        int movementBudget = grid.GetMovementBudgetForUnit(this);
        int spent = 0;
        bool movementSoundStarted = false;

        try
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (currentOrder != OrderType.Retreat && !IsBroken && stats != null && stats.stopMovementOnEnemyContact && HasAdjacentEnemy())
                    break;

                Vector2Int target = path[i];
                int stepCost = grid.GetMovementCost(GridPosition, target, this);

                if (stepCost == int.MaxValue)
                    break;

                if (spent + stepCost > movementBudget)
                    break;

                if (!grid.MoveUnit(this, GridPosition, target))
                    break;

                if (!movementSoundStarted)
                {
                    StartMovementSound();
                    movementSoundStarted = true;
                }

                spent += stepCost;
                tilesMovedThisTurn++;

                Vector3 start = transform.position;
                Vector3 end = new Vector3(target.x, target.y, 0f);

                float duration = Mathf.Max(0.01f, stepCost / GetCurrentMoveSpeed());
                float t = 0f;

                while (t < 1f)
                {
                    t += Time.deltaTime / duration;
                    transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
                    yield return null;
                }

                transform.position = end;
                GridPosition = target;

                if (currentOrder != OrderType.Retreat && !IsBroken && stats != null && stats.stopMovementOnEnemyContact && HasAdjacentEnemy())
                    break;
            }
        }
        finally
        {
            StopMovementSound();
            _moveRoutine = null;
            onComplete?.Invoke();
        }
    }

    private void StartMovementSound()
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.StartMovementLoopForUnit(this);
    }

    private void StopMovementSound()
    {
        if (SoundManager.Instance == null)
            return;

        SoundManager.Instance.StopMovementLoopForUnit(this);
    }
}