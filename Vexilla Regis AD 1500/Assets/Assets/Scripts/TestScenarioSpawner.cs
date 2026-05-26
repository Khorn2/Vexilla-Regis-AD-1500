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

        public Sprite spriteOverride;
    }

    [SerializeField] private GridManager grid;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private BattleStatsTracker battleStatsTracker;

    [Header("Scenario Rules")]
    [SerializeField] private bool useTurnLimit = false;

    [SerializeField, Min(1)]
    private int maxTurns = 40;

    [SerializeField]
    private bool drawOnTurnLimit = true;

    [Header("Units")]
    [SerializeField]
    private ScenarioSpawnData[] spawns;

    [Header("Terrain")]
    [SerializeField]
    public List<TerrainPaintData> terrainPaints =
        new List<TerrainPaintData>();

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
    }

    private void ApplyScenarioRules()
    {
        if (turnManager == null)
        {
            Debug.LogWarning(
                "TestScenarioSpawner: brak TurnManager."
            );

            return;
        }

        turnManager.ConfigureTurnLimit(
            useTurnLimit,
            maxTurns,
            drawOnTurnLimit
        );

        Debug.Log(
            $"Scenario rules: " +
            $"useTurnLimit={useTurnLimit}, " +
            $"maxTurns={maxTurns}"
        );
    }

    private void ApplyTerrain()
    {
        if (grid == null)
        {
            Debug.LogError(
                "TestScenarioSpawner: brak GridManager."
            );

            return;
        }

        if (terrainPaints == null ||
            terrainPaints.Count == 0)
        {
            Debug.Log(
                "TestScenarioSpawner: brak terenu."
            );

            return;
        }

        for (int i = 0;
             i < terrainPaints.Count;
             i++)
        {
            TerrainPaintData data =
                terrainPaints[i];

            if (!grid.IsInside(
                data.gridPosition))
            {
                Debug.LogWarning(
                    $"Terrain {i}: poza mapą."
                );

                continue;
            }

            grid.SetTileTerrain(
                data.gridPosition,
                data.terrainType,
                data.heightLevel,
                data.spriteOverride
            );
        }

        Debug.Log(
            "Applied terrain entries = " +
            terrainPaints.Count
        );
    }

    private void SpawnScenario()
    {
        if (grid == null)
        {
            Debug.LogError(
                "TestScenarioSpawner: brak GridManager."
            );

            return;
        }

        for (int i = 0;
             i < spawns.Length;
             i++)
        {
            ScenarioSpawnData data =
                spawns[i];

            if (data.prefab == null)
                continue;

            if (!grid.IsInside(
                data.gridPosition))
                continue;

            if (!grid.IsWalkable(
                data.gridPosition))
                continue;

            if (grid.IsOccupied(
                data.gridPosition))
                continue;

            GameObject go =
                Instantiate(
                    data.prefab,
                    new Vector3(
                        data.gridPosition.x,
                        data.gridPosition.y,
                        0f
                    ),
                    Quaternion.identity
                );

            if (go == null)
                continue;

            GameUnit unit =
                go.GetComponent<GameUnit>();

            if (unit == null)
            {
                Destroy(go);
                continue;
            }

            unit.SetTeam(data.teamId);

            unit.SnapToGrid(
                data.gridPosition
            );

            if (battleStatsTracker != null)
            {
                battleStatsTracker
                    .RegisterScenarioUnit(unit);
            }
        }

        Debug.Log(
            "Test scenario spawned."
        );
    }
}