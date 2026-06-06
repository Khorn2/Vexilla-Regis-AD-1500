using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public TerrainType terrainType = TerrainType.Plain;

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
        public List<TestScenarioSpawner.TerrainPaintData> terrain =
            new List<TestScenarioSpawner.TerrainPaintData>();

        public List<TestScenarioSpawner.PlacedUnitData> units =
            new List<TestScenarioSpawner.PlacedUnitData>();
    }

    [Header("References")]
    [SerializeField] private GridManager grid;
    [SerializeField] private TestScenarioSpawner scenarioSpawner;
    [SerializeField] private Camera cam;

    [Header("Editor")]
    [SerializeField] private bool editModeEnabled = true;
    [SerializeField] private EditorMode editorMode = EditorMode.Terrain;

    [Header("Brushes")]
    [SerializeField] private TerrainBrush[] brushes;
    [SerializeField] private int currentBrushIndex = 0;

    private bool hasUnsavedChanges;
    private bool hasLastEditedPosition;
    private Vector2Int lastEditedPosition;

    private string SaveDirectory =>
        Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "Maps"
        );

    private string SavePath =>
        Path.Combine(
            SaveDirectory,
            "terrain.json"
        );

    private string BackupPath =>
        Path.Combine(
            SaveDirectory,
            "terrain_backup.json"
        );

    private string TempPath =>
        Path.Combine(
            SaveDirectory,
            "terrain_tmp.json"
        );

    private void OnValidate()
    {
        if (currentBrushIndex < 0)
            currentBrushIndex = 0;

        if (brushes != null &&
            brushes.Length > 0 &&
            currentBrushIndex >= brushes.Length)
        {
            currentBrushIndex = brushes.Length - 1;
        }
    }

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (scenarioSpawner == null)
            scenarioSpawner = FindObjectOfType<TestScenarioSpawner>();

        if (cam == null)
            cam = Camera.main;
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        bool changed = SanitizeScenarioData();

        if (changed)
        {
            if (scenarioSpawner != null)
                scenarioSpawner.ApplyTerrain();

            MarkDirty();
            SaveIfDirty();
        }
    }

    private void Update()
    {
        if (!editModeEnabled)
            return;

        if (grid == null || scenarioSpawner == null || cam == null)
            return;

        HandleBrushSelection();
        HandleEditing();

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            hasLastEditedPosition = false;
            SaveIfDirty();
        }
    }

    private void OnApplicationQuit()
    {
        SaveIfDirty();
    }

    private void OnDisable()
    {
        SaveIfDirty();
    }

    private void HandleEditing()
    {
        if (editorMode == EditorMode.Terrain)
        {
            if (Input.GetMouseButton(0))
                PaintTerrain();

            if (Input.GetMouseButton(1))
                RemoveTerrain();

            return;
        }

        if (editorMode == EditorMode.Units)
        {
            if (Input.GetMouseButtonDown(0))
                PlaceUnit();

            if (Input.GetMouseButtonDown(1))
                RemoveUnit();
        }
    }

    private void HandleBrushSelection()
    {
        if (brushes == null)
            return;

        for (int i = 0; i < brushes.Length && i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(key))
                currentBrushIndex = i;
        }
    }

    private Vector2Int GetMouseGridPosition()
    {
        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(Input.mousePosition);

        return grid.WorldToGrid(mouseWorld);
    }

    private bool ShouldSkipRepeatedEdit(Vector2Int gridPos)
    {
        if (hasLastEditedPosition && lastEditedPosition == gridPos)
            return true;

        hasLastEditedPosition = true;
        lastEditedPosition = gridPos;

        return false;
    }

    private TerrainBrush GetCurrentBrush()
    {
        if (brushes == null || brushes.Length == 0)
            return null;

        if (currentBrushIndex < 0 ||
            currentBrushIndex >= brushes.Length)
        {
            currentBrushIndex = 0;
        }

        return brushes[currentBrushIndex];
    }

    private void PaintTerrain()
    {
        TerrainBrush brush = GetCurrentBrush();

        if (brush == null)
            return;

        Vector2Int gridPos = GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        if (ShouldSkipRepeatedEdit(gridPos))
            return;

        RemoveTerrainEntriesAt(gridPos);

        TestScenarioSpawner.TerrainPaintData data =
            new TestScenarioSpawner.TerrainPaintData();

        data.gridPosition = gridPos;
        data.terrainType = brush.terrainType;
        data.heightLevel = Mathf.Clamp(brush.heightLevel, 1, 6);
        data.spriteId = brush.spriteId;

        scenarioSpawner.terrainPaints.Add(data);

        Sprite sprite = null;

        if (TerrainSpriteDatabase.Instance != null)
            sprite = TerrainSpriteDatabase.Instance.Get(brush.spriteId);

        grid.SetTileTerrain(
            gridPos,
            data.terrainType,
            data.heightLevel,
            sprite
        );

        MarkDirty();
    }

    private void RemoveTerrain()
    {
        Vector2Int gridPos = GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        if (ShouldSkipRepeatedEdit(gridPos))
            return;

        bool removed = RemoveTerrainEntriesAt(gridPos);

        Sprite defaultSprite = null;

        if (TerrainSpriteDatabase.Instance != null)
            defaultSprite = TerrainSpriteDatabase.Instance.Get("grass");

        grid.SetTileTerrain(
            gridPos,
            TerrainType.Plain,
            1,
            defaultSprite
        );

        if (removed)
            MarkDirty();
    }

    private void PlaceUnit()
    {
        TerrainBrush brush = GetCurrentBrush();

        if (brush == null)
            return;

        if (brush.placeableObject == null)
            return;

        if (string.IsNullOrWhiteSpace(brush.unitPrefabId))
            return;

        Vector2Int gridPos = GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        if (!grid.IsWalkable(gridPos))
            return;

        if (grid.GetUnitAt(gridPos) != null)
            return;

        GameObject spawned =
            Instantiate(
                brush.placeableObject,
                new Vector3(gridPos.x, gridPos.y, -0.2f),
                Quaternion.identity
            );

        GameUnit unit = spawned.GetComponent<GameUnit>();

        if (unit == null)
        {
            Destroy(spawned);
            return;
        }

        unit.SetTeam(brush.placedTeamId);
        unit.SnapToGrid(gridPos);

        if (grid.GetUnitAt(gridPos) != unit)
        {
            Destroy(spawned);
            return;
        }

        RemoveSavedUnitsAt(gridPos);

        TestScenarioSpawner.PlacedUnitData data =
            new TestScenarioSpawner.PlacedUnitData();

        data.prefabId = brush.unitPrefabId;
        data.gridPosition = gridPos;
        data.teamId = brush.placedTeamId;

        scenarioSpawner.placedUnits.Add(data);

        MarkDirty();
        SaveIfDirty();
    }

    private void RemoveUnit()
    {
        Vector2Int gridPos = GetMouseGridPosition();

        if (!grid.IsInside(gridPos))
            return;

        GameUnit unit = grid.GetUnitAt(gridPos);

        bool removedData = RemoveSavedUnitsAt(gridPos);

        if (unit != null)
        {
            grid.UnregisterUnit(unit, gridPos);
            Destroy(unit.gameObject);
        }

        if (unit == null && !removedData)
            return;

        MarkDirty();
        SaveIfDirty();
    }

    private bool RemoveTerrainEntriesAt(Vector2Int gridPos)
    {
        if (scenarioSpawner == null || scenarioSpawner.terrainPaints == null)
            return false;

        bool removed = false;

        for (int i = scenarioSpawner.terrainPaints.Count - 1; i >= 0; i--)
        {
            if (scenarioSpawner.terrainPaints[i].gridPosition == gridPos)
            {
                scenarioSpawner.terrainPaints.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    private bool RemoveSavedUnitsAt(Vector2Int gridPos)
    {
        if (scenarioSpawner == null || scenarioSpawner.placedUnits == null)
            return false;

        bool removed = false;

        for (int i = scenarioSpawner.placedUnits.Count - 1; i >= 0; i--)
        {
            if (scenarioSpawner.placedUnits[i].gridPosition == gridPos)
            {
                scenarioSpawner.placedUnits.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    private bool SanitizeScenarioData()
    {
        if (scenarioSpawner == null)
            return false;

        bool changed = false;

        if (scenarioSpawner.terrainPaints == null)
            scenarioSpawner.terrainPaints = new List<TestScenarioSpawner.TerrainPaintData>();

        Dictionary<Vector2Int, TestScenarioSpawner.TerrainPaintData> terrainLookup =
            new Dictionary<Vector2Int, TestScenarioSpawner.TerrainPaintData>();

        for (int i = 0; i < scenarioSpawner.terrainPaints.Count; i++)
        {
            TestScenarioSpawner.TerrainPaintData data =
                scenarioSpawner.terrainPaints[i];

            if (data.heightLevel < 1 || data.heightLevel > 6)
            {
                changed = true;
                continue;
            }

            if (!grid.IsInside(data.gridPosition))
            {
                changed = true;
                continue;
            }

            if (terrainLookup.ContainsKey(data.gridPosition))
                changed = true;

            terrainLookup[data.gridPosition] = data;
        }

        List<TestScenarioSpawner.TerrainPaintData> cleanTerrain =
            new List<TestScenarioSpawner.TerrainPaintData>();

        foreach (KeyValuePair<Vector2Int, TestScenarioSpawner.TerrainPaintData> pair in terrainLookup)
        {
            cleanTerrain.Add(pair.Value);
        }

        if (cleanTerrain.Count != scenarioSpawner.terrainPaints.Count)
            changed = true;

        scenarioSpawner.terrainPaints = cleanTerrain;

        if (scenarioSpawner.placedUnits == null)
            scenarioSpawner.placedUnits = new List<TestScenarioSpawner.PlacedUnitData>();

        Dictionary<Vector2Int, TestScenarioSpawner.PlacedUnitData> unitLookup =
            new Dictionary<Vector2Int, TestScenarioSpawner.PlacedUnitData>();

        for (int i = 0; i < scenarioSpawner.placedUnits.Count; i++)
        {
            TestScenarioSpawner.PlacedUnitData data =
                scenarioSpawner.placedUnits[i];

            if (!grid.IsInside(data.gridPosition))
            {
                changed = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(data.prefabId))
            {
                changed = true;
                continue;
            }

            if (unitLookup.ContainsKey(data.gridPosition))
                changed = true;

            unitLookup[data.gridPosition] = data;
        }

        List<TestScenarioSpawner.PlacedUnitData> cleanUnits =
            new List<TestScenarioSpawner.PlacedUnitData>();

        foreach (KeyValuePair<Vector2Int, TestScenarioSpawner.PlacedUnitData> pair in unitLookup)
        {
            cleanUnits.Add(pair.Value);
        }

        if (cleanUnits.Count != scenarioSpawner.placedUnits.Count)
            changed = true;

        scenarioSpawner.placedUnits = cleanUnits;

        return changed;
    }

    private void MarkDirty()
    {
        hasUnsavedChanges = true;
    }

    private void SaveIfDirty()
    {
        if (!hasUnsavedChanges)
            return;

        SaveTerrain();
        hasUnsavedChanges = false;
    }

    private void SaveTerrain()
    {
        if (scenarioSpawner == null)
            return;

        SanitizeScenarioData();

        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);

        TerrainSaveData save = new TerrainSaveData();

        save.terrain =
            new List<TestScenarioSpawner.TerrainPaintData>(
                scenarioSpawner.terrainPaints
            );

        save.units =
            new List<TestScenarioSpawner.PlacedUnitData>(
                scenarioSpawner.placedUnits
            );

        string json = JsonUtility.ToJson(save, true);

        if (File.Exists(SavePath))
            File.Copy(SavePath, BackupPath, true);

        File.WriteAllText(TempPath, json);
        File.Copy(TempPath, SavePath, true);
        File.Delete(TempPath);

        DebugLog("Map saved to: " + SavePath);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    private void DebugLog(string message)
    {
        if (!ENABLE_DEBUG)
            return;

        Debug.Log(message);
    }

    private void OnDrawGizmos()
    {
        if (!editModeEnabled)
            return;

        if (cam == null)
            return;

        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(Input.mousePosition);

        Vector2Int gridPos =
            new Vector2Int(
                Mathf.RoundToInt(mouseWorld.x),
                Mathf.RoundToInt(mouseWorld.y)
            );

        Gizmos.color =
            editorMode == EditorMode.Terrain
                ? Color.green
                : Color.red;

        Gizmos.DrawWireCube(
            new Vector3(gridPos.x, gridPos.y, 0f),
            Vector3.one
        );
    }
}