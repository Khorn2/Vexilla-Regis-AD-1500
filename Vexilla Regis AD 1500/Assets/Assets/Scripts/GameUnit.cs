using System;
using System.Collections;
using UnityEngine;

public class GameUnit : MonoBehaviour
{
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

    private Coroutine _moveRoutine;
    private int currentSize;
    private GridManager grid;

    private void Awake()
    {
        grid = FindObjectOfType<GridManager>();

        if (stats == null)
        {
            Debug.LogError($"{name}: brak przypiętego UnitStats.", this);
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

        currentOrder = order;
    }

    public void SnapToGrid(Vector2Int gridPos)
    {
        if (grid == null) return;

        // jeśli jednostka już stoi gdzieś indziej, zwolnij stare pole
        if (GridPosition != gridPos)
            grid.UnregisterUnit(this, GridPosition);

        if (!grid.RegisterUnit(this, gridPos))
            return;

        GridPosition = gridPos;
        transform.position = new Vector3(gridPos.x, gridPos.y, 0f);
    }

    public void MoveToGrid(Vector2Int targetGrid, Action onComplete = null)
    {
        if (currentOrder == OrderType.Shoot)
            return;

        if (grid == null)
            return;

        if (!grid.MoveUnit(this, GridPosition, targetGrid))
            return;

        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        _moveRoutine = StartCoroutine(MoveRoutine(targetGrid, onComplete));
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
}