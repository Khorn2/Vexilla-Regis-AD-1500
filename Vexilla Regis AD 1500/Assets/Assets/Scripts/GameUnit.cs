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

    [Header("Team")]
    [SerializeField] private int teamId = 0; // 0 = player, 1 = enemy

    public bool IsSelected { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool IsMoving => _moveRoutine != null;
    public OrderType CurrentOrder => currentOrder;
    public int TeamId => teamId;
    public int CurrentSize => currentSize;
    public UnitStats Stats => stats;
    public IReadOnlyList<PlannedCommand> PlannedCommands => plannedCommands;

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
        Debug.Log($"{name} -> Order changed to: {currentOrder}");
    }

    public int GetMovementRange()
    {
        int range = stats.movementRange;

        if (currentOrder == OrderType.Charge)
            range = Mathf.RoundToInt(range * 1.5f);

        return range;
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
            return;

        if (grid == null)
            return;

        Vector2Int resolvedTarget = grid.ResolveMoveDestination(this, GridPosition, desiredTargetGrid);

        if (resolvedTarget == GridPosition)
            return;

        if (!grid.MoveUnit(this, GridPosition, resolvedTarget))
            return;

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

    public float GetCurrentMoveSpeed()
    {
        float speed = Mathf.Max(0.1f, stats.moveSpeedTilesPerSec);

        if (currentOrder == OrderType.Charge)
            speed *= 1.5f;

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
        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!IsAdjacentTo(target)) return;

        int dmg = Mathf.RoundToInt(stats.meleeDamage * GetMeleeModifier());
        target.TakeDamage(dmg);
    }

    public void Shoot(GameUnit target)
    {
        if (target == null) return;
        if (target == this) return;
        if (target.TeamId == TeamId) return;
        if (!stats.canShoot) return;
        if (currentOrder != OrderType.Shoot) return;

        // brak strzału gdy wróg stoi obok
        if (HasAdjacentEnemy())
        {
            Debug.Log($"{name} cannot shoot: enemy is adjacent.");
            return;
        }

        float dist = Vector2Int.Distance(GridPosition, target.GridPosition);
        if (dist > stats.shootRange) return;

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

        Destroy(gameObject);
    }

    // =========================
    // KOLEJKA ROZKAZÓW
    // =========================

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

        for (int i = 0; i < plannedCommands.Count; i++)
        {
            PlannedCommand cmd = plannedCommands[i];
            if (cmd == null) continue;

            switch (cmd.commandType)
            {
                case PlannedCommandType.Move:
                {
                    bool finished = false;

                    MoveToGrid(cmd.targetGridPosition, () => finished = true);

                    while (!finished)
                        yield return null;

                    break;
                }

                case PlannedCommandType.AttackShoot:
                {
                    if (cmd.targetUnit != null)
                        Shoot(cmd.targetUnit);

                    yield return new WaitForSeconds(0.15f);
                    break;
                }

                case PlannedCommandType.AttackMelee:
                {
                    if (cmd.targetUnit != null)
                    {
                        if (IsAdjacentTo(cmd.targetUnit))
                        {
                            AttackMelee(cmd.targetUnit);
                        }
                        else if (grid != null && grid.TryGetFreeAdjacentTile(cmd.targetUnit.GridPosition, GridPosition, out Vector2Int attackTile))
                        {
                            bool finished = false;

                            MoveToGrid(attackTile, () =>
                            {
                                if (this != null && cmd.targetUnit != null)
                                    AttackMelee(cmd.targetUnit);

                                finished = true;
                            });

                            while (!finished)
                                yield return null;
                        }
                    }

                    yield return new WaitForSeconds(0.15f);
                    break;
                }
            }
        }
    }
}