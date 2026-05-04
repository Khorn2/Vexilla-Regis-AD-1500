using System.Collections;
using UnityEngine;

public class TestScenarioSpawner : MonoBehaviour
{
    [System.Serializable]
    public class TerrainPaintData
    {
        public Vector2Int gridPosition;
        public TerrainType terrainType = TerrainType.Plain;
        [Range(1, 6)] public int heightLevel = 1;
        public Sprite spriteOverride;
    }

    [SerializeField] private GridManager grid;
    [SerializeField] private TurnManager turnManager;

    [Header("Scenario Rules")]
    [SerializeField] private bool useTurnLimit = false;
    [SerializeField, Min(1)] private int maxTurns = 40;
    [SerializeField] private bool drawOnTurnLimit = true;

    [Header("Units")]
    [SerializeField] private ScenarioSpawnData[] spawns;

    [Header("Terrain")]
    [SerializeField] private TerrainPaintData[] terrainPaints;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
    }

    private IEnumerator Start()
    {
        yield return null;

        ApplyScenarioRules();
        ApplyTerrain();
        SpawnScenario();
    }

    private void ApplyScenarioRules()
    {
        if (turnManager == null)
        {
            Debug.LogWarning("TestScenarioSpawner: brak TurnManager przy ApplyScenarioRules.");
            return;
        }

        turnManager.ConfigureTurnLimit(useTurnLimit, maxTurns, drawOnTurnLimit);

        Debug.Log($"Scenario rules: useTurnLimit={useTurnLimit}, maxTurns={maxTurns}, drawOnTurnLimit={drawOnTurnLimit}");
    }

    private void ApplyTerrain()
    {
        if (grid == null)
        {
            Debug.LogError("TestScenarioSpawner: brak GridManager przy ApplyTerrain.");
            return;
        }

        if (terrainPaints == null || terrainPaints.Length == 0)
        {
            Debug.Log("TestScenarioSpawner: brak wpisów terenu.");
            return;
        }

        for (int i = 0; i < terrainPaints.Length; i++)
        {
            TerrainPaintData data = terrainPaints[i];
            if (data == null)
                continue;

            if (!grid.IsInside(data.gridPosition))
            {
                Debug.LogWarning($"Terrain {i}: pozycja poza mapą {data.gridPosition}.");
                continue;
            }

            grid.SetTileTerrain(data.gridPosition, data.terrainType, data.heightLevel, data.spriteOverride);
        }

        Debug.Log($"TestScenarioSpawner: applied terrain entries = {terrainPaints.Length}");
    }

    private void SpawnScenario()
    {
        if (grid == null)
        {
            Debug.LogError("TestScenarioSpawner: brak GridManager.");
            return;
        }

        Debug.Log($"TestScenarioSpawner: start spawning. Count = {spawns.Length}");

        for (int i = 0; i < spawns.Length; i++)
        {
            ScenarioSpawnData data = spawns[i];

            if (data.prefab == null)
            {
                Debug.LogWarning($"Spawn {i}: brak prefabu.");
                continue;
            }

            Debug.Log($"Spawn {i}: prefab={data.prefab.name}, pos={data.gridPosition}, team={data.teamId}");

            if (!grid.IsInside(data.gridPosition))
            {
                Debug.LogWarning($"Spawn {i}: pozycja poza mapą {data.gridPosition}.");
                continue;
            }

            if (!grid.IsWalkable(data.gridPosition))
            {
                Debug.LogWarning($"Spawn {i}: teren nieprzechodni na {data.gridPosition}.");
                continue;
            }

            if (grid.IsOccupied(data.gridPosition))
            {
                Debug.LogWarning($"Spawn {i}: pole zajęte {data.gridPosition}.");
                continue;
            }

            GameObject go = Instantiate(
                data.prefab,
                new Vector3(data.gridPosition.x, data.gridPosition.y, 0f),
                Quaternion.identity
            );

            if (go == null)
            {
                Debug.LogError($"Spawn {i}: Instantiate zwrócił null.");
                continue;
            }

            GameUnit unit = go.GetComponent<GameUnit>();
            if (unit == null)
            {
                Debug.LogError($"Spawn {i}: prefab {data.prefab.name} nie ma GameUnit na ROOT.");
                Destroy(go);
                continue;
            }

            unit.SetTeam(data.teamId);
            unit.SnapToGrid(data.gridPosition);

            Debug.Log($"Spawn {i}: SUCCESS -> {go.name} at {data.gridPosition}");
        }

        Debug.Log("Test scenario spawned.");
    }
}