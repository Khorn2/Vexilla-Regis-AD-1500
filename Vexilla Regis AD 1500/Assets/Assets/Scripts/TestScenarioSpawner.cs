using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TestScenarioSpawner : MonoBehaviour
{
    [System.Serializable]
    public class TerrainPaintData
    {
        public Vector2Int gridPosition;
        public TerrainType terrainType = TerrainType.Plain;

        [Range(1, 6)]
        public int heightLevel = 1;

        public string spriteId;
    }

    [System.Serializable]
    public class PlacedUnitData
    {
        public string prefabId;
        public Vector2Int gridPosition;
        public int teamId;
    }

    [SerializeField] private GridManager grid;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleStatsTracker battleStatsTracker;

    [Header("Scenario Rules")]
    [SerializeField] private bool useTurnLimit = false;
    [SerializeField, Min(1)] private int maxTurns = 40;
    [SerializeField] private bool drawOnTurnLimit = true;

    [Header("Scenario Units")]
    [SerializeField] private ScenarioSpawnData[] spawns;

    [Header("Terrain")]
    public List<TerrainPaintData> terrainPaints = new List<TerrainPaintData>();

    [Header("Runtime Units")]
    public List<PlacedUnitData> placedUnits = new List<PlacedUnitData>();

    private string SavePath =>
        Path.Combine(
            Application.streamingAssetsPath,
            "Maps",
            "terrain.json"
        );

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (battleStatsTracker == null)
            battleStatsTracker = FindObjectOfType<BattleStatsTracker>();
    }

    private IEnumerator Start()
    {
        yield return null;

        ApplyScenarioRules();

        if (battleStatsTracker != null)
            battleStatsTracker.Clear();

        bool saveLoaded = LoadRuntimeMap();

        ApplyTerrain();

        if (saveLoaded)
            SpawnRuntimeUnits();
        else
            SpawnScenario();
    }

    private void ApplyScenarioRules()
    {
        if (turnManager == null)
            return;

        turnManager.ConfigureTurnLimit(
            useTurnLimit,
            maxTurns,
            drawOnTurnLimit
        );
    }

    private bool LoadRuntimeMap()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("Runtime map file not found: " + SavePath);
            return false;
        }

        string json = File.ReadAllText(SavePath);

        RuntimeTerrainEditor.TerrainSaveData save =
            JsonUtility.FromJson<RuntimeTerrainEditor.TerrainSaveData>(json);

        if (save == null)
        {
            Debug.LogError("Runtime map file is invalid: " + SavePath);
            return false;
        }

        terrainPaints = save.terrain != null
            ? save.terrain
            : new List<TerrainPaintData>();

        placedUnits = save.units != null
            ? save.units
            : new List<PlacedUnitData>();

        Debug.Log(
            "Runtime map loaded. Terrain: " +
            terrainPaints.Count +
            ", Units: " +
            placedUnits.Count
        );

        return true;
    }

    public void ApplyTerrain()
    {
        if (grid == null)
            return;

        if (terrainPaints == null)
            return;

        for (int i = 0; i < terrainPaints.Count; i++)
        {
            TerrainPaintData data = terrainPaints[i];

            if (!grid.IsInside(data.gridPosition))
                continue;

            Sprite sprite = null;

            if (TerrainSpriteDatabase.Instance != null)
                sprite = TerrainSpriteDatabase.Instance.Get(data.spriteId);

            grid.SetTileTerrain(
                data.gridPosition,
                data.terrainType,
                data.heightLevel,
                sprite
            );
        }

        Debug.Log("Applied terrain entries: " + terrainPaints.Count);
    }

    private void SpawnScenario()
    {
        if (spawns == null)
            return;

        for (int i = 0; i < spawns.Length; i++)
        {
            ScenarioSpawnData data = spawns[i];

            if (data == null)
                continue;

            if (data.prefab == null)
                continue;

            SpawnUnit(
                data.prefab,
                data.gridPosition,
                data.teamId
            );
        }

        Debug.Log("Scenario units spawned.");
    }

    public void SpawnRuntimeUnits()
    {
        if (placedUnits == null)
            return;

        for (int i = 0; i < placedUnits.Count; i++)
        {
            PlacedUnitData data = placedUnits[i];

            if (string.IsNullOrWhiteSpace(data.prefabId))
                continue;

            if (UnitPrefabDatabase.Instance == null)
            {
                Debug.LogError("Missing UnitPrefabDatabase in scene.");
                return;
            }

            GameObject prefab =
                UnitPrefabDatabase.Instance.Get(data.prefabId);

            if (prefab == null)
            {
                Debug.LogError("Missing prefab for ID: " + data.prefabId);
                continue;
            }

            SpawnUnit(
                prefab,
                data.gridPosition,
                data.teamId
            );
        }

        Debug.Log("Runtime units spawned.");
    }

    private void SpawnUnit(
        GameObject prefab,
        Vector2Int gridPosition,
        int teamId)
    {
        if (prefab == null)
            return;

        if (grid == null)
            return;

        if (!grid.IsInside(gridPosition))
            return;

        if (!grid.IsWalkable(gridPosition))
            return;

        if (grid.IsOccupied(gridPosition))
            return;

        GameObject spawned =
            Instantiate(
                prefab,
                new Vector3(
                    gridPosition.x,
                    gridPosition.y,
                    -0.2f
                ),
                Quaternion.identity
            );

        GameUnit unit = spawned.GetComponent<GameUnit>();

        if (unit == null)
        {
            Destroy(spawned);
            return;
        }

        unit.SetTeam(teamId);
        unit.SnapToGrid(gridPosition);

        if (battleStatsTracker != null)
            battleStatsTracker.RegisterScenarioUnit(unit);

        Debug.Log(
            "Spawned unit: " +
            prefab.name +
            " Team: " +
            teamId
        );
    }
}