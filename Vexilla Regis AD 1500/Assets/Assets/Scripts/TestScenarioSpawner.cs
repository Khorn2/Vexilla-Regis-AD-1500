using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScenarioSpawner : MonoBehaviour
{
    [System.Serializable]
    public class TerrainPaintData
    {
        public Vector2Int gridPosition;

        public TerrainType terrainType =
            TerrainType.Plain;

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

    [SerializeField]
    private GridManager grid;

    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private BattleStatsTracker battleStatsTracker;

    [Header("Scenario Rules")]
    [SerializeField]
    private bool useTurnLimit = false;

    [SerializeField, Min(1)]
    private int maxTurns = 40;

    [SerializeField]
    private bool drawOnTurnLimit = true;

    [Header("Scenario Units")]
    [SerializeField]
    private ScenarioSpawnData[] spawns;

    [Header("Terrain")]
    public List<TerrainPaintData>
        terrainPaints =
            new List<TerrainPaintData>();

    [Header("Runtime Units")]
    public List<PlacedUnitData>
        placedUnits =
            new List<PlacedUnitData>();

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (turnManager == null)
            turnManager =
                FindObjectOfType<TurnManager>();

        if (battleStatsTracker == null)
            battleStatsTracker =
                FindObjectOfType
                <BattleStatsTracker>();
    }

    private IEnumerator Start()
    {
        yield return null;

        ApplyScenarioRules();

        if (battleStatsTracker != null)
            battleStatsTracker.Clear();

        ApplyTerrain();

        SpawnScenario();

        SpawnRuntimeUnits();
    }

    private void ApplyScenarioRules()
    {
        if (turnManager == null)
        {
            Debug.LogWarning(
                "Missing TurnManager."
            );

            return;
        }

        turnManager.ConfigureTurnLimit(
            useTurnLimit,
            maxTurns,
            drawOnTurnLimit
        );

        Debug.Log(
            $"Scenario Rules: " +
            $"TurnLimit={useTurnLimit}, " +
            $"MaxTurns={maxTurns}"
        );
    }

    public void ApplyTerrain()
    {
        if (grid == null)
        {
            Debug.LogError(
                "Missing GridManager."
            );

            return;
        }

        if (terrainPaints == null)
            return;

        for (int i = 0;
             i < terrainPaints.Count;
             i++)
        {
            TerrainPaintData data =
                terrainPaints[i];

            if (!grid.IsInside(
                data.gridPosition))
                continue;

            Sprite sprite = null;

            if (TerrainSpriteDatabase
                .Instance != null)
            {
                sprite =
                    TerrainSpriteDatabase
                        .Instance
                        .Get(data.spriteId);
            }

            grid.SetTileTerrain(
                data.gridPosition,
                data.terrainType,
                data.heightLevel,
                sprite
            );
        }

        Debug.Log(
            "Applied terrain entries: " +
            terrainPaints.Count
        );
    }

    private void SpawnScenario()
    {
        if (spawns == null)
            return;

        for (int i = 0;
             i < spawns.Length;
             i++)
        {
            ScenarioSpawnData data =
                spawns[i];

            if (data.prefab == null)
                continue;

            SpawnUnit(
                data.prefab,
                data.gridPosition,
                data.teamId
            );
        }

        Debug.Log(
            "Scenario units spawned."
        );
    }

    public void SpawnRuntimeUnits()
    {
        if (placedUnits == null)
            return;

        foreach (PlacedUnitData data
            in placedUnits)
        {
            GameObject prefab =
                UnitPrefabDatabase
                    .Instance
                    .Get(data.prefabId);

            if (prefab == null)
                continue;

            SpawnUnit(
                prefab,
                data.gridPosition,
                data.teamId
            );
        }

        Debug.Log(
            "Runtime units spawned."
        );
    }

    private void SpawnUnit(
        GameObject prefab,
        Vector2Int gridPosition,
        int teamId)
    {
        if (prefab == null)
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

        GameUnit unit =
            spawned.GetComponent<GameUnit>();

        if (unit == null)
        {
            Destroy(spawned);
            return;
        }

        unit.SetTeam(teamId);

        unit.SnapToGrid(gridPosition);

        if (battleStatsTracker != null)
        {
            battleStatsTracker
                .RegisterScenarioUnit(unit);
        }
    }
}