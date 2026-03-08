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

        public int Width => width;
    public int Height => height;

    public bool IsInside(Vector2Int p)
        => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

    public Vector2Int WorldToGrid(Vector3 world)
        => new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));

    public Vector3 GridToWorld(Vector2Int grid)
        => new Vector3(grid.x, grid.y, 0f);

    private void Start()
    {
        GenerateGrid();
        CenterCamera();
        ApplyCameraBounds();
    }

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

        // środek pomiędzy kaflami: 0..(width-1) => środek = (width-1)/2
        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        cameraController.transform.position = new Vector3(cx, cy, -10f);
    }

    private void ApplyCameraBounds()
    {
        if (cameraController == null) return;

        // clamp do KRAWĘDZI mapy (lepsze niż do środków)
        Vector2 worldMin = new Vector2(-0.5f, -0.5f);
        Vector2 worldMax = new Vector2(width - 0.5f, height - 0.5f);

        cameraController.SetBounds(worldMin, worldMax);
    }

    public Tile GetTileAtPosition(Vector2Int position)
    {
        return _tiles.TryGetValue(position, out var tile) ? tile : null;
    }
}