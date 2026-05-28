using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum EditorMode
{
    Terrain,
    Units
}

public class RuntimeTerrainEditor : MonoBehaviour
{
    private const bool ENABLE_DEBUG = false;

    [System.Serializable]
    public class TerrainBrush
    {
        [Header("Brush")]
        public string brushName;

        [Header("Terrain")]
        public TerrainType terrainType =
            TerrainType.Plain;

        [Range(1, 6)]
        public int heightLevel = 1;

        [Header("Terrain Sprite")]
        public string spriteId;

        [Header("Placeable Unit")]
        public GameObject placeableObject;

        [Header("Save ID")]
        public string unitPrefabId;

        [Header("Team")]
        public int placedTeamId = 0;
    }

    [System.Serializable]
    public class TerrainSaveData
    {
        public List<TestScenarioSpawner.TerrainPaintData>
            terrain =
                new List<TestScenarioSpawner.TerrainPaintData>();

        public List<TestScenarioSpawner.PlacedUnitData>
            units =
                new List<TestScenarioSpawner.PlacedUnitData>();
    }

    [Header("References")]
    [SerializeField]
    private GridManager grid;

    [SerializeField]
    private TestScenarioSpawner scenarioSpawner;

    [SerializeField]
    private Camera cam;

    [Header("Editor")]
    [SerializeField]
    private bool editModeEnabled = true;

    [SerializeField]
    private EditorMode editorMode =
        EditorMode.Terrain;

    [Header("Brushes")]
    [SerializeField]
    private TerrainBrush[] brushes;

    [SerializeField]
    private int currentBrushIndex = 0;

    private string SavePath =>
        Application.persistentDataPath +
        "/terrain.json";

    private string BackupPath =>
        Application.persistentDataPath +
        "/terrain_backup.json";

    // =========================
    // VALIDATION
    // =========================

    private void OnValidate()
    {
        if (currentBrushIndex < 0)
            currentBrushIndex = 0;

        if (brushes != null &&
            brushes.Length > 0 &&
            currentBrushIndex >= brushes.Length)
        {
            currentBrushIndex =
                brushes.Length - 1;
        }
    }

    // =========================
    // DEBUG
    // =========================

    private void DebugLog(string message)
    {
        if (!ENABLE_DEBUG)
            return;

        Debug.Log(message);
    }

    // =========================
    // UNITY
    // =========================

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

    // LOAD HANDLED
    // BY TESTSCENARIOSPAWNER
    private void Start()
    {
    }

    private void Update()
    {
        if (!editModeEnabled)
            return;

        HandleBrushSelection();

        // TERRAIN MODE
        if (editorMode ==
            EditorMode.Terrain)
        {
            if (Input.GetMouseButton(0))
            {
                PaintTerrain();
            }

            if (Input.GetMouseButton(1))
            {
                RemoveTerrain();
            }
        }

        // UNIT MODE
        if (editorMode ==
            EditorMode.Units)
        {
            if (Input.GetMouseButton(0))
            {
                PlaceUnit();
            }

            if (Input.GetMouseButton(1))
            {
                RemoveUnit();
            }
        }
    }

    // =========================
    // BRUSHES
    // =========================

    private void HandleBrushSelection()
    {
        if (brushes == null)
            return;

        for (int i = 0;
             i < brushes.Length &&
             i < 9;
             i++)
        {
            KeyCode key =
                KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(key))
            {
                currentBrushIndex = i;
            }
        }
    }

    // =========================
    // GRID
    // =========================

    private Vector2Int GetMouseGridPosition()
    {
        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        return grid.WorldToGrid(
            mouseWorld
        );
    }

    // =========================
    // TERRAIN
    // =========================

    private void PaintTerrain()
    {
        if (brushes == null ||
            brushes.Length == 0)
            return;

        if (currentBrushIndex < 0 ||
            currentBrushIndex >= brushes.Length)
        {
            currentBrushIndex = 0;
            return;
        }

        TerrainBrush brush =
            brushes[currentBrushIndex];

        Vector2Int gridPos =
            GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        Sprite sprite = null;

        if (TerrainSpriteDatabase.Instance != null)
        {
            sprite =
                TerrainSpriteDatabase.Instance.Get(
                    brush.spriteId
                );
        }

        grid.SetTileTerrain(
            gridPos,
            brush.terrainType,
            brush.heightLevel,
            sprite
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

                data.spriteId =
                    brush.spriteId;

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

            data.spriteId =
                brush.spriteId;

            scenarioSpawner
                .terrainPaints
                .Add(data);
        }

        SaveTerrain();
    }

    private void RemoveTerrain()
    {
        Vector2Int gridPos =
            GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        Sprite defaultSprite = null;

        if (TerrainSpriteDatabase.Instance != null)
        {
            defaultSprite =
                TerrainSpriteDatabase.Instance.Get(
                    "grass"
                );
        }

        grid.SetTileTerrain(
            gridPos,
            TerrainType.Plain,
            1,
            defaultSprite
        );

        for (int i =
                 scenarioSpawner
                 .terrainPaints.Count - 1;
             i >= 0;
             i--)
        {
            if (scenarioSpawner
                .terrainPaints[i]
                .gridPosition ==
                gridPos)
            {
                scenarioSpawner
                    .terrainPaints
                    .RemoveAt(i);
            }
        }

        SaveTerrain();
    }

    // =========================
    // UNITS
    // =========================

    private void PlaceUnit()
    {
        if (brushes == null)
            return;

        if (brushes.Length == 0)
            return;

        if (currentBrushIndex < 0 ||
            currentBrushIndex >= brushes.Length)
        {
            currentBrushIndex = 0;
            return;
        }

        TerrainBrush brush =
            brushes[currentBrushIndex];

        if (brush.placeableObject == null)
            return;

        Vector2Int gridPos =
            GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        // ONLY BLOCK SAME TEAM
        GameUnit existing =
            grid.GetUnitAt(gridPos);

        if (existing != null)
        {
            if (existing.TeamId ==
                brush.placedTeamId)
            {
                return;
            }
        }

        Vector3 spawnPos =
            new Vector3(
                gridPos.x,
                gridPos.y,
                -0.2f
            );

        GameObject spawned =
            Instantiate(
                brush.placeableObject,
                spawnPos,
                Quaternion.identity
            );

        GameUnit unit =
            spawned.GetComponent<GameUnit>();

        if (unit != null)
        {
            unit.SetTeam(
                brush.placedTeamId
            );

            unit.SnapToGrid(
                gridPos
            );

            grid.RegisterUnit(
                unit,
                gridPos
            );
        }

        TestScenarioSpawner
            .PlacedUnitData data =
                new TestScenarioSpawner
                    .PlacedUnitData();

        data.prefabId =
            brush.unitPrefabId;

        data.gridPosition =
            gridPos;

        data.teamId =
            brush.placedTeamId;

        scenarioSpawner
            .placedUnits
            .Add(data);

        SaveTerrain();
    }

    private void RemoveUnit()
    {
        Vector2Int gridPos =
            GetMouseGridPosition();

        GameUnit unit =
            grid.GetUnitAt(gridPos);

        if (unit == null)
            return;

        grid.UnregisterUnit(
            unit,
            gridPos
        );

        Destroy(unit.gameObject);

        for (int i =
                 scenarioSpawner
                 .placedUnits.Count - 1;
             i >= 0;
             i--)
        {
            if (scenarioSpawner
                .placedUnits[i]
                .gridPosition ==
                gridPos)
            {
                scenarioSpawner
                    .placedUnits
                    .RemoveAt(i);
            }
        }

        SaveTerrain();
    }

    // =========================
    // SAVE
    // =========================

    private void SaveTerrain()
    {
        if (scenarioSpawner == null)
            return;

        if (File.Exists(SavePath))
        {
            File.Copy(
                SavePath,
                BackupPath,
                true
            );
        }

        TerrainSaveData save =
            new TerrainSaveData();

        save.terrain =
            scenarioSpawner.terrainPaints;

        save.units =
            scenarioSpawner.placedUnits;

        string json =
            JsonUtility.ToJson(
                save,
                true
            );

        File.WriteAllText(
            SavePath,
            json
        );
    }

    // =========================
    // GIZMOS
    // =========================

    private void OnDrawGizmos()
    {
        if (!editModeEnabled)
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

        Gizmos.color =
            editorMode ==
            EditorMode.Terrain
                ? Color.green
                : Color.red;

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