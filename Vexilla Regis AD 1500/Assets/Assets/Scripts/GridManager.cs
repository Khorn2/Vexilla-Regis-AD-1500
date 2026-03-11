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
}