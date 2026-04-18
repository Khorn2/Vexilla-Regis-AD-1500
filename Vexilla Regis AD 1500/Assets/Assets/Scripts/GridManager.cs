using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private int width = 32;
    [SerializeField] private int height = 18;
    [SerializeField] private Tile tilePrefab;

    [Header("Camera")]
    [SerializeField] private CameraController2D cameraController;

    [Header("Terrain Rules")]
    [SerializeField] private int steepHeightThreshold = 3;
    [SerializeField] private int steepHeightExtraCost = 1;

    [Header("Terrain Combat Modifiers")]
    [SerializeField, Range(0f, 1f)] private float shallowWaterMeleeMultiplier = 0.9f;
    [SerializeField, Range(0f, 1f)] private float heightMeleeBonusPerLevel = 0.1f;
    [SerializeField, Range(0f, 1f)] private float forestArmorBonusPercent = 0.2f;

    private Dictionary<Vector2Int, Tile> _tiles;
    private Dictionary<Vector2Int, GameUnit> _occupied;

    private readonly List<Tile> highlightedTiles = new List<Tile>();

    public int Width => width;
    public int Height => height;

    private void Awake()
    {
        _occupied = new Dictionary<Vector2Int, GameUnit>();
    }

    private void Start()
    {
        GenerateGrid();
        CenterCamera();
        ApplyCameraBounds();
    }

    public bool IsInside(Vector2Int p)
        => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

    public Vector2Int WorldToGrid(Vector3 world)
        => new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));

    public Vector3 GridToWorld(Vector2Int grid)
        => new Vector3(grid.x, grid.y, 0f);

    private void GenerateGrid()
    {
        _tiles = new Dictionary<Vector2Int, Tile>(width * height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile spawnedTile = Instantiate(tilePrefab, new Vector3(x, y, 0f), Quaternion.identity);
                spawnedTile.name = $"Tile {x} {y}";

                bool isOffset = (x % 2 == 0 && y % 2 != 0) || (x % 2 != 0 && y % 2 == 0);
                spawnedTile.Init(isOffset);

                _tiles[new Vector2Int(x, y)] = spawnedTile;
            }
        }
    }

    private void CenterCamera()
    {
        if (cameraController == null) return;

        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        cameraController.transform.position = new Vector3(cx, cy, -10f);
    }

    private void ApplyCameraBounds()
    {
        if (cameraController == null) return;

        Vector2 worldMin = new Vector2(-0.5f, -0.5f);
        Vector2 worldMax = new Vector2(width - 0.5f, height - 0.5f);

        cameraController.SetBounds(worldMin, worldMax);
    }

    public Tile GetTileAtPosition(Vector2Int position)
    {
        return _tiles.TryGetValue(position, out Tile tile) ? tile : null;
    }

    public Tile GetTileAt(Vector2Int position)
    {
        return GetTileAtPosition(position);
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return _occupied.ContainsKey(pos);
    }

    public GameUnit GetUnitAt(Vector2Int pos)
    {
        _occupied.TryGetValue(pos, out GameUnit unit);
        return unit;
    }

    public bool RegisterUnit(GameUnit unit, Vector2Int pos)
    {
        if (unit == null) return false;
        if (!IsInside(pos)) return false;
        if (!IsWalkable(pos)) return false;

        if (_occupied.TryGetValue(pos, out GameUnit existing))
        {
            if (existing == unit) return true;
            return false;
        }

        _occupied[pos] = unit;
        return true;
    }

    public bool MoveUnit(GameUnit unit, Vector2Int from, Vector2Int to)
    {
        if (unit == null) return false;
        if (!IsInside(to)) return false;
        if (!IsWalkable(to)) return false;

        if (from == to)
            return true;

        if (_occupied.TryGetValue(to, out GameUnit existing) && existing != unit)
            return false;

        if (_occupied.ContainsKey(from) && _occupied[from] == unit)
            _occupied.Remove(from);

        _occupied[to] = unit;
        return true;
    }

    public void UnregisterUnit(GameUnit unit, Vector2Int pos)
    {
        if (unit == null) return;

        if (_occupied.ContainsKey(pos) && _occupied[pos] == unit)
            _occupied.Remove(pos);
    }

    public Vector2Int[] GetNeighbours4(Vector2Int pos)
    {
        return new Vector2Int[]
        {
            new Vector2Int(pos.x + 1, pos.y),
            new Vector2Int(pos.x - 1, pos.y),
            new Vector2Int(pos.x, pos.y + 1),
            new Vector2Int(pos.x, pos.y - 1)
        };
    }

    public bool IsWalkable(Vector2Int pos)
    {
        if (!IsInside(pos))
            return false;

        Tile tile = GetTileAtPosition(pos);
        return tile != null && tile.IsWalkable();
    }

    public int GetMovementBudgetForUnit(GameUnit mover)
    {
        if (mover == null)
            return 0;

        int budget = mover.GetMovementRange();
        return Mathf.Max(0, budget);
    }

    public int GetShootRangeBonusAt(Vector2Int pos)
    {
        Tile tile = GetTileAtPosition(pos);
        if (tile == null)
            return 0;

        return tile.GetShootRangeBonus();
    }

    public bool IsForestAt(Vector2Int pos)
    {
        Tile tile = GetTileAtPosition(pos);
        return tile != null && tile.TerrainType == TerrainType.Forest;
    }

    public float GetMeleeDamageMultiplierAt(Vector2Int pos)
    {
        Tile tile = GetTileAtPosition(pos);
        if (tile == null)
            return 1f;

        float multiplier = 1f;

        if (tile.TerrainType == TerrainType.ShallowWater)
            multiplier *= shallowWaterMeleeMultiplier;

        if (tile.HeightLevel > 1)
            multiplier *= 1f + ((tile.HeightLevel - 1) * heightMeleeBonusPerLevel);

        return Mathf.Max(0f, multiplier);
    }

    public float GetArmorBonusPercentAt(Vector2Int pos)
    {
        Tile tile = GetTileAtPosition(pos);
        if (tile == null)
            return 0f;

        if (tile.TerrainType == TerrainType.Forest)
            return forestArmorBonusPercent;

        return 0f;
    }

    public bool IsAtMapEdge(Vector2Int pos)
    {
        if (!IsInside(pos))
            return false;

        return pos.x == 0 || pos.y == 0 || pos.x == width - 1 || pos.y == height - 1;
    }

    public Vector2Int GetNearestEdgePosition(Vector2Int from)
    {
        int leftDist = from.x;
        int rightDist = (width - 1) - from.x;
        int bottomDist = from.y;
        int topDist = (height - 1) - from.y;

        int minDist = leftDist;
        Vector2Int best = new Vector2Int(0, from.y);

        if (rightDist < minDist)
        {
            minDist = rightDist;
            best = new Vector2Int(width - 1, from.y);
        }

        if (bottomDist < minDist)
        {
            minDist = bottomDist;
            best = new Vector2Int(from.x, 0);
        }

        if (topDist < minDist)
        {
            best = new Vector2Int(from.x, height - 1);
        }

        return best;
    }

    public GameUnit GetNearestEnemy(GameUnit mover)
    {
        if (mover == null)
            return null;

        GameUnit best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.TeamId == mover.TeamId) continue;

            float dist = Vector2Int.Distance(mover.GridPosition, unit.GridPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = unit;
            }
        }

        return best;
    }

    public bool HasAdjacentEnemyForTeam(Vector2Int pos, int teamId)
    {
        Vector2Int[] neighbours = GetNeighbours4(pos);

        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector2Int n = neighbours[i];
            if (!IsInside(n)) continue;

            GameUnit unit = GetUnitAt(n);
            if (unit != null && !unit.IsDead && unit.TeamId != teamId)
                return true;
        }

        return false;
    }

    public bool IsValidManualRetreatDestination(GameUnit mover, Vector2Int target)
    {
        if (mover == null)
            return false;

        if (!IsInside(target))
            return false;

        if (!IsWalkable(target))
            return false;

        GameUnit unitAtTarget = GetUnitAt(target);
        if (unitAtTarget != null && unitAtTarget != mover)
            return false;

        if (HasAdjacentEnemyForTeam(target, mover.TeamId))
            return false;

        GameUnit nearestEnemy = GetNearestEnemy(mover);
        if (nearestEnemy == null)
            return true;

        Vector2Int targetDelta = target - mover.GridPosition;
        if (targetDelta == Vector2Int.zero)
            return false;

        Vector2Int threatDirection = GetPrimaryThreatDirection(mover.GridPosition, nearestEnemy.GridPosition);
        Vector2Int backward = -threatDirection;
        Vector2Int left = new Vector2Int(-threatDirection.y, threatDirection.x);
        Vector2Int right = new Vector2Int(threatDirection.y, -threatDirection.x);

        Vector2Int moveDirection = GetPrimaryDirectionFromDelta(targetDelta);

        return moveDirection == backward || moveDirection == left || moveDirection == right;
    }

    private Vector2Int GetPrimaryThreatDirection(Vector2Int moverPos, Vector2Int enemyPos)
    {
        Vector2Int delta = enemyPos - moverPos;
        return GetPrimaryDirectionFromDelta(delta);
    }

    private Vector2Int GetPrimaryDirectionFromDelta(Vector2Int delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return new Vector2Int(delta.x >= 0 ? 1 : -1, 0);

        return new Vector2Int(0, delta.y >= 0 ? 1 : -1);
    }

    public int GetMovementCost(Vector2Int from, Vector2Int to, GameUnit mover)
    {
        if (!IsInside(from) || !IsInside(to))
            return int.MaxValue;

        Tile destinationTile = GetTileAtPosition(to);
        Tile sourceTile = GetTileAtPosition(from);

        if (destinationTile == null || sourceTile == null)
            return int.MaxValue;

        if (!destinationTile.IsWalkable())
            return int.MaxValue;

        int cost = destinationTile.GetBaseMovementCost();

        int heightDelta = destinationTile.HeightLevel - sourceTile.HeightLevel;
        if (heightDelta >= steepHeightThreshold)
            cost += steepHeightExtraCost;

        return Mathf.Max(0, cost);
    }

    public int GetTravelCostAlongLine(GameUnit mover, Vector2Int start, Vector2Int end)
    {
        if (mover == null)
            return 0;

        List<Vector2Int> line = GetLine(start, end);
        if (line.Count <= 1)
            return 0;

        int totalCost = 0;
        Vector2Int previous = line[0];

        for (int i = 1; i < line.Count; i++)
        {
            Vector2Int current = line[i];
            int stepCost = GetMovementCost(previous, current, mover);

            if (stepCost == int.MaxValue)
                return int.MaxValue;

            totalCost += stepCost;
            previous = current;
        }

        return Mathf.Max(0, totalCost);
    }

    public Vector2Int GetReachablePointAlongLine(GameUnit mover, Vector2Int from, Vector2Int desired, int movementBudget, out int spentCost)
    {
        spentCost = 0;

        if (mover == null)
            return from;

        if (!IsInside(desired))
            return from;

        List<Vector2Int> line = GetLine(from, desired);

        Vector2Int lastValid = from;
        Vector2Int previous = from;
        int runningCost = 0;

        for (int i = 1; i < line.Count; i++)
        {
            Vector2Int p = line[i];

            if (!IsInside(p))
                break;

            int stepCost = GetMovementCost(previous, p, mover);
            if (stepCost == int.MaxValue)
                break;

            if (runningCost + stepCost > movementBudget)
                break;

            GameUnit unitAt = GetUnitAt(p);

            if (unitAt == null)
            {
                runningCost += stepCost;
                lastValid = p;
                previous = p;
                continue;
            }

            if (unitAt == mover)
            {
                runningCost += stepCost;
                lastValid = p;
                previous = p;
                continue;
            }

            if (unitAt.TeamId == mover.TeamId)
            {
                runningCost += stepCost;
                previous = p;
                continue;
            }

            break;
        }

        spentCost = runningCost;
        return lastValid;
    }

    public Vector2Int ResolveMoveDestination(GameUnit mover, Vector2Int from, Vector2Int desired)
    {
        if (mover == null) return from;
        if (!IsInside(desired)) return from;

        int maxRange = GetMovementBudgetForUnit(mover);
        return GetReachablePointAlongLine(mover, from, desired, maxRange, out _);
    }

    public bool TryGetFreeAdjacentTile(Vector2Int targetPos, Vector2Int attackerPos, out Vector2Int result)
    {
        result = targetPos;

        Vector2Int[] neighbours = GetNeighbours4(targetPos);

        float bestDist = float.MaxValue;
        bool found = false;

        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector2Int n = neighbours[i];

            if (!IsInside(n)) continue;
            if (!IsWalkable(n)) continue;
            if (IsOccupied(n)) continue;

            float dist = Vector2Int.Distance(attackerPos, n);
            if (dist < bestDist)
            {
                bestDist = dist;
                result = n;
                found = true;
            }
        }

        return found;
    }

    private List<Vector2Int> GetLine(Vector2Int start, Vector2Int end)
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

    public void SetTileTerrain(Vector2Int position, TerrainType terrainType, int heightLevel, Sprite spriteOverride = null)
    {
        Tile tile = GetTileAtPosition(position);
        if (tile == null)
            return;

        tile.SetTerrain(terrainType, heightLevel);
        tile.SetTerrainSprite(spriteOverride);
    }

    public void ClearHighlights()
    {
        for (int i = 0; i < highlightedTiles.Count; i++)
            highlightedTiles[i].SetRangeHighlight(false);

        highlightedTiles.Clear();
    }

    public void HighlightMovementRange(Vector2Int center, int range)
    {
        ClearHighlights();

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2Int p = new Vector2Int(center.x + x, center.y + y);

                if (!IsInside(p))
                    continue;

                float dist = Mathf.Abs(x) + Mathf.Abs(y);

                if (dist > range)
                    continue;

                Tile tile = GetTileAtPosition(p);

                if (tile != null)
                {
                    tile.SetRangeHighlight(true);
                    highlightedTiles.Add(tile);
                }
            }
        }
    }

    public void HighlightShootRange(Vector2Int center, int range)
    {
        ClearHighlights();

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2Int p = new Vector2Int(center.x + x, center.y + y);

                if (!IsInside(p))
                    continue;

                float dist = Vector2Int.Distance(center, p);

                if (dist > range)
                    continue;

                Tile tile = GetTileAtPosition(p);

                if (tile != null)
                {
                    tile.SetRangeHighlight(true);
                    highlightedTiles.Add(tile);
                }
            }
        }
    }
}