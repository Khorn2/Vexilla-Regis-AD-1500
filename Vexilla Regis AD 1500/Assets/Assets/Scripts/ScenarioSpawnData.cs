using UnityEngine;

[System.Serializable]
public class ScenarioSpawnData
{
    public GameObject prefab;
    public Vector2Int gridPosition;
    public int teamId; // 0 = player, 1 = enemy
}