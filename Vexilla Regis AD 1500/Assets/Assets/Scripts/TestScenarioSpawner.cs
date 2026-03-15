using System.Collections;
using UnityEngine;

public class TestScenarioSpawner : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private ScenarioSpawnData[] spawns;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();
    }

    private IEnumerator Start()
    {
        // czekamy aż GridManager zdąży wygenerować grid
        yield return null;

        SpawnScenario();
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