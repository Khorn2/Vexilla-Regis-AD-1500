using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private int width = 32;
    [SerializeField] private int height = 18;
    [SerializeField] private Tile tilePrefab;

    [Header("Camera")]
    [SerializeField] private CameraController2D cameraController;

    private Dictionary<Vector2Int, Tile> _tiles;
    private Dictionary<Vector2Int, GameUnit> _occupied;

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
                var spawnedTile = Instantiate(tilePrefab, new Vector3(x, y, 0f), Quaternion.identity);
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
        return _tiles.TryGetValue(position, out var tile) ? tile : null;
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return _occupied.ContainsKey(pos);
    }

    public GameUnit GetUnitAt(Vector2Int pos)
    {
        _occupied.TryGetValue(pos, out var unit);
        return unit;
    }

    public bool RegisterUnit(GameUnit unit, Vector2Int pos)
    {
        if (unit == null) return false;
        if (!IsInside(pos)) return false;

        if (_occupied.TryGetValue(pos, out var existing))
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

        if (from == to)
            return true;

        if (_occupied.TryGetValue(to, out var existing) && existing != unit)
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

    // QoL #4: sojuszników można "przejść", wrogowie blokują ruch
    public Vector2Int ResolveMoveDestination(GameUnit mover, Vector2Int from, Vector2Int desired)
    {
    if (mover == null) return from;
    if (!IsInside(desired)) return from;

    int maxRange = mover.GetMovementRange();

    List<Vector2Int> line = GetLine(from, desired);

    Vector2Int lastValid = from;
    int travelled = 0;

    for (int i = 1; i < line.Count; i++)
    {
        Vector2Int p = line[i];

        if (!IsInside(p))
            break;

        travelled++;

        if (travelled > maxRange)
            break;

        GameUnit unitAt = GetUnitAt(p);

        if (unitAt == null)
        {
            lastValid = p;
            continue;
        }

        if (unitAt == mover)
        {
            lastValid = p;
            continue;
        }

        if (unitAt.TeamId == mover.TeamId)
            continue;

        // enemy blocks
        break;
    }

    return lastValid;
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

    private List<Tile> highlightedTiles = new List<Tile>();

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