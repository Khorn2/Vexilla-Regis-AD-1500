using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameUnit : MonoBehaviour
{
    public static readonly List<GameUnit> AllUnits = new List<GameUnit>();

    [Header("Visuals")]
    [SerializeField] private GameObject selectionRing;

    [Header("Data")]
    [SerializeField] private UnitStats stats;
    [SerializeField] private OrderType currentOrder = OrderType.March;

    [Header("Order Modifiers")]
    [SerializeField, Min(1f)] private float chargeMovementRangeMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float chargeMoveSpeedMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float retreatMovementRangeMultiplier = 1.5f;
    [SerializeField, Min(1f)] private float retreatMoveSpeedMultiplier = 1.75f;

    [Header("Team")]
    [SerializeField] private int teamId = 0; // 0 = player, 1 = enemy

    [Header("Ranged Through Ally")]
    [SerializeField] private bool allowShotThroughOneAlly = false;
    [SerializeField, Range(0f, 1f)] private float targetDamageMultiplierThroughAlly = 0.85f;
    [SerializeField, Range(0f, 1f)] private float allyDamageMultiplier = 0.25f;

    public bool IsSelected { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool IsMoving => _moveRoutine != null;
    public OrderType CurrentOrder => currentOrder;
    public int TeamId => teamId;
    public int CurrentSize => currentSize;
    public UnitStats Stats => stats;
    public IReadOnlyList<PlannedCommand> PlannedCommands => plannedCommands;
    public bool IsDead => currentSize <= 0;
    public bool IsRetreating => currentOrder == OrderType.Retreat;

    // Tylko jednostki gracza mają pokazywać preview rozkazów
    public bool ShowCommandPreview => teamId == 0;

    private readonly List<PlannedCommand> plannedCommands = new List<PlannedCommand>();

    private Coroutine _moveRoutine;
    private int currentSize;
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

        if (selectionRing != null)
            selectionRing.SetActive(false);
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

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (selectionRing != null)
            selectionRing.SetActive(selected);
    }

    public void SetOrder(OrderType order)
    {
        if (order == OrderType.Shoot && !stats.canShoot)
            return;

        if (order == OrderType.Charge && !stats.canCharge)
            return;

        if (currentOrder == order)
            return;

        currentOrder = order;

        // Retreat ma być rozkazem ruchowym, więc czyścimy stare ataki.
        if (currentOrder == OrderType.Retreat)
        {
            RemoveAttackCommandsFromQueue();
        }

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

    public float GetMeleeModifier()
    {
        switch (currentOrder)
        {
            case OrderType.Charge:
                return 1.5f;

            default:
                return 1f;
        }
    }

    public void SnapToGrid(Vector2Int gridPos)
    {
        if (grid == null) return;

        if (GridPosition != gridPos)
            grid.UnregisterUnit(this, GridPosition);

        if (!grid.RegisterUnit(this, gridPos))
            return;

        GridPosition = gridPos;
        transform.position = new Vector3(gridPos.x, gridPos.y, 0f);
    }

    public void MoveToGrid(Vector2Int desiredTargetGrid, Action onComplete = null)
    {
        if (currentOrder == OrderType.Shoot)
        {
            onComplete?.Invoke();
            return;
        }

        if (grid == null)
        {
            onComplete?.Invoke();
            return;
        }

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

        _moveRoutine = StartCoroutine(MoveRoutine(resolvedTarget, onComplete));
    }

    private IEnumerator MoveRoutine(Vector2Int targetGrid, Action onComplete)
    {
        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetGrid.x, targetGrid.y, 0f);

        float distanceTiles = Vector3.Distance(start, end);

        if (distanceTiles <= 0.001f)
        {
            GridPosition = targetGrid;
            _moveRoutine = null;
            onComplete?.Invoke();
            yield break;
        }

        float duration = distanceTiles / GetCurrentMoveSpeed();
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
        if (currentOrder == OrderType.Retreat)
        {
            Debug.Log($"{name} cannot attack in melee while retreating.");
            return;
        }

        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!IsAdjacentTo(target)) return;

        int dmg = Mathf.RoundToInt(stats.meleeDamage * GetMeleeModifier());
        target.TakeDamage(dmg);
    }

    public void Shoot(GameUnit target)
    {
        if (currentOrder == OrderType.Retreat)
        {
            Debug.Log($"{name} cannot shoot while retreating.");
            return;
        }

        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!stats.canShoot) return;
        if (currentOrder != OrderType.Shoot) return;

        if (HasAdjacentEnemy())
        {
            Debug.Log($"{name} cannot shoot: enemy is adjacent.");
            return;
        }

        float dist = Vector2Int.Distance(GridPosition, target.GridPosition);
        if (dist > stats.shootRange) return;

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

        if (alliesOnLine == 1)
        {
            if (!allowShotThroughOneAlly)
            {
                Debug.Log($"{name} cannot shoot: ally blocks the shot.");
                return;
            }

            int targetDmg = Mathf.RoundToInt(stats.rangedDamage * targetDamageMultiplierThroughAlly);
            int allyDmg = Mathf.RoundToInt(stats.rangedDamage * allyDamageMultiplier);

            if (allyHit != null)
                allyHit.TakeDamage(allyDmg);

            target.TakeDamage(targetDmg);
            return;
        }

        target.TakeDamage(stats.rangedDamage);
    }

    public void TakeDamage(int dmg)
    {
        currentSize -= dmg;
        Debug.Log($"{name} took {dmg} damage. Current size: {currentSize}");

        if (currentSize <= 0)
            Die();
    }

    private void Die()
    {
        if (grid != null)
            grid.UnregisterUnit(this, GridPosition);

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
        if (!append)
            plannedCommands.Clear();

        plannedCommands.Add(new PlannedCommand(PlannedCommandType.Move, target));
        Debug.Log($"{name} queued MOVE to {target}");
    }

    public void QueueAttack(GameUnit target, bool append)
    {
        if (currentOrder == OrderType.Retreat)
        {
            Debug.Log($"{name} is retreating and cannot queue attack commands.");
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
        if (plannedCommands.Count == 0)
            yield break;

        int remainingMovement = GetMovementRange();
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
                    Vector2Int stepTarget = GetPointAlongLine(startPos, cmd.targetGridPosition, remainingMovement);

                    bool finished = false;
                    MoveToGrid(stepTarget, () => finished = true);

                    while (!finished)
                        yield return null;

                    int moved = CountLineSteps(startPos, GridPosition);
                    remainingMovement -= moved;

                    if (GridPosition == cmd.targetGridPosition)
                    {
                        plannedCommands.RemoveAt(0);
                        continue;
                    }

                    yield break;
                }

                case PlannedCommandType.AttackShoot:
                {
                    if (currentOrder == OrderType.Retreat)
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
                    if (currentOrder == OrderType.Retreat)
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
                        Vector2Int stepTarget = GetPointAlongLine(startPos, attackTile, remainingMovement);

                        bool finished = false;
                        MoveToGrid(stepTarget, () => finished = true);

                        while (!finished)
                            yield return null;

                        int moved = CountLineSteps(startPos, GridPosition);
                        remainingMovement -= moved;

                        if (IsAdjacentTo(cmd.targetUnit))
                        {
                            AttackMelee(cmd.targetUnit);
                        }

                        yield break;
                    }

                    plannedCommands.RemoveAt(0);
                    continue;
                }
            }
        }
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

    private Vector2Int GetPointAlongLine(Vector2Int start, Vector2Int end, int maxSteps)
    {
        List<Vector2Int> line = GetLinePoints(start, end);

        if (line.Count == 0)
            return start;

        int index = Mathf.Clamp(maxSteps, 0, line.Count - 1);
        return line[index];
    }

    private int CountLineSteps(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> line = GetLinePoints(start, end);
        return Mathf.Max(0, line.Count - 1);
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