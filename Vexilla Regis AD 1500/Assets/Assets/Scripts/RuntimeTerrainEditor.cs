using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RuntimeTerrainEditor : MonoBehaviour
{
    [System.Serializable]
    public class TerrainBrush
    {
        public string brushName;

        public TerrainType terrainType =
            TerrainType.Plain;

        [Range(1, 6)]
        public int heightLevel = 1;

        public Sprite spriteOverride;
    }

    [System.Serializable]
    public class TerrainSaveData
    {
        public List<TestScenarioSpawner.TerrainPaintData>
            terrain =
                new List<TestScenarioSpawner.TerrainPaintData>();
    }

    [Header("References")]
    [SerializeField] private GridManager grid;

    [SerializeField]
    private TestScenarioSpawner scenarioSpawner;

    [SerializeField]
    private Camera cam;

    [Header("Editor")]
    [SerializeField]
    private bool editMode = true;

    [Header("Brushes")]
    [SerializeField]
    private TerrainBrush[] brushes;

    [SerializeField]
    private int currentBrushIndex = 0;

    private string SavePath =>
        Application.persistentDataPath +
        "/terrain.json";

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (scenarioSpawner == null)
            scenarioSpawner =
                FindObjectOfType<TestScenarioSpawner>();

        if (cam == null)
            cam = Camera.main;
    }

    private void Start()
    {
        LoadTerrain();
    }

    private void Update()
    {
        if (!editMode)
            return;

        HandleBrushSelection();

        HandlePainting();
    }

    private void HandleBrushSelection()
    {
        for (int i = 0;
             i < brushes.Length && i < 9;
             i++)
        {
            KeyCode key =
                KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(key))
            {
                currentBrushIndex = i;

                Debug.Log(
                    "Selected Brush: " +
                    brushes[i].brushName
                );
            }
        }
    }

    private void HandlePainting()
    {
        if (Input.GetMouseButton(0))
        {
            PaintTile();
        }

        if (Input.GetMouseButton(1))
        {
            EraseTile();
        }
    }

    private void PaintTile()
    {
        if (brushes == null ||
            brushes.Length == 0)
            return;

        if (currentBrushIndex < 0 ||
            currentBrushIndex >= brushes.Length)
            return;

        TerrainBrush brush =
            brushes[currentBrushIndex];

        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        Vector2Int gridPos =
            grid.WorldToGrid(mouseWorld);

        if (!grid.IsInside(gridPos))
            return;

        grid.SetTileTerrain(
            gridPos,
            brush.terrainType,
            brush.heightLevel,
            brush.spriteOverride
        );

        bool found = false;

        for (int i = 0;
             i < scenarioSpawner
                 .terrainPaints.Count;
             i++)
        {
            var data =
                scenarioSpawner
                    .terrainPaints[i];

            if (data.gridPosition ==
                gridPos)
            {
                data.terrainType =
                    brush.terrainType;

                data.heightLevel =
                    brush.heightLevel;

                data.spriteOverride =
                    brush.spriteOverride;

                scenarioSpawner
                    .terrainPaints[i] =
                        data;

                found = true;

                break;
            }
        }

        if (!found)
        {
            TestScenarioSpawner
                .TerrainPaintData data =
                    new TestScenarioSpawner
                        .TerrainPaintData();

            data.gridPosition =
                gridPos;

            data.terrainType =
                brush.terrainType;

            data.heightLevel =
                brush.heightLevel;

            data.spriteOverride =
                brush.spriteOverride;

            scenarioSpawner
                .terrainPaints
                .Add(data);
        }

        SaveTerrain();

        Debug.Log(
            $"Painted {gridPos.x} " +
            $"{gridPos.y}"
        );
    }

    private void EraseTile()
    {
        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        Vector2Int gridPos =
            grid.WorldToGrid(mouseWorld);

        if (!grid.IsInside(gridPos))
            return;

        grid.SetTileTerrain(
            gridPos,
            TerrainType.Plain,
            1,
            null
        );

        for (int i =
                 scenarioSpawner
                 .terrainPaints.Count - 1;
             i >= 0;
             i--)
        {
            if (scenarioSpawner
                .terrainPaints[i]
                .gridPosition == gridPos)
            {
                scenarioSpawner
                    .terrainPaints
                    .RemoveAt(i);
            }
        }

        SaveTerrain();

        Debug.Log(
            $"Erased {gridPos.x} " +
            $"{gridPos.y}"
        );
    }

    private void SaveTerrain()
    {
        TerrainSaveData save =
            new TerrainSaveData();

        save.terrain =
            scenarioSpawner.terrainPaints;

        string json =
            JsonUtility.ToJson(
                save,
                true
            );

        File.WriteAllText(
            SavePath,
            json
        );

        Debug.Log(
            "Terrain Saved"
        );
    }

    private void LoadTerrain()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log(
                "No terrain save found."
            );

            return;
        }

        string json =
            File.ReadAllText(
                SavePath
            );

        TerrainSaveData save =
            JsonUtility.FromJson
            <TerrainSaveData>(json);

        scenarioSpawner
            .terrainPaints =
                save.terrain;

        Debug.Log(
            "Terrain Loaded"
        );
    }

    private void OnDrawGizmos()
    {
        if (!editMode)
            return;

        if (cam == null)
            return;

        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        Vector2Int gridPos =
            new Vector2Int(
                Mathf.RoundToInt(
                    mouseWorld.x
                ),
                Mathf.RoundToInt(
                    mouseWorld.y
                )
            );

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireCube(
            new Vector3(
                gridPos.x,
                gridPos.y,
                0f
            ),
            Vector3.one
        );
    }
}