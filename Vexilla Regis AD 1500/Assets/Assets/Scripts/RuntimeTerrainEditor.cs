using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RuntimeTerrainEditor : MonoBehaviour
{
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

        [Header("Placeable Object")]
        public GameObject placeableObject;

        [Header("Saved Unit")]
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
    private bool editMode = true;

    [SerializeField]
    private bool allowObjectPlacement = true;

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

        if (Input.GetMouseButton(0))
        {
            PaintTile();
        }

        // PPM = usuwa tylko jednostki
        if (Input.GetMouseButton(1) &&
            !Input.GetKey(KeyCode.LeftShift))
        {
            RemoveObjectsOnly();
        }

        // SHIFT + PPM = usuwa terrain
        if (Input.GetMouseButton(1) &&
            Input.GetKey(KeyCode.LeftShift))
        {
            RemoveTerrain();
        }
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

    private Vector2Int GetMouseGridPosition()
    {
        Vector3 mouseWorld =
            cam.ScreenToWorldPoint(
                Input.mousePosition
            );

        return grid.WorldToGrid(mouseWorld);
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

        // TERRAIN
        grid.SetTileTerrain(
            gridPos,
            brush.terrainType,
            brush.heightLevel,
            sprite
        );

        bool terrainFound = false;

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

                terrainFound = true;

                break;
            }
        }

        if (!terrainFound)
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

        // PLACE UNIT / OBJECT
        if (allowObjectPlacement &&
            brush.placeableObject != null)
        {
            bool unitExists = false;

            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    new Vector2(
                        gridPos.x,
                        gridPos.y
                    ),
                    0.2f
                );

            foreach (Collider2D hit in hits)
            {
                if (hit == null)
                    continue;

                if (hit.GetComponent<GameUnit>() != null)
                {
                    unitExists = true;
                    break;
                }
            }

            if (!unitExists)
            {
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
                }

                if (!string.IsNullOrEmpty(
                    brush.unitPrefabId))
                {
                    bool alreadyExists = false;

                    foreach (var unitData in
                        scenarioSpawner
                        .placedUnits)
                    {
                        if (unitData.gridPosition ==
                            gridPos)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        TestScenarioSpawner
                            .PlacedUnitData
                            unitData =
                                new TestScenarioSpawner
                                    .PlacedUnitData();

                        unitData.prefabId =
                            brush.unitPrefabId;

                        unitData.gridPosition =
                            gridPos;

                        unitData.teamId =
                            brush.placedTeamId;

                        scenarioSpawner
                            .placedUnits
                            .Add(unitData);
                    }
                }
            }
        }

        SaveTerrain();
    }

    // PPM
    private void RemoveObjectsOnly()
    {
        Vector2Int gridPos =
            GetMouseGridPosition();

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                new Vector2(
                    gridPos.x,
                    gridPos.y
                ),
                0.3f
            );

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            GameUnit unit =
                hit.GetComponent<GameUnit>();

            if (unit != null)
            {
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
            }
        }

        SaveTerrain();
    }

    // SHIFT + PPM
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

    private void SaveTerrain()
    {
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

    private void LoadTerrain()
    {
        if (!File.Exists(SavePath))
            return;

        string json =
            File.ReadAllText(
                SavePath
            );

        TerrainSaveData save =
            JsonUtility.FromJson
            <TerrainSaveData>(json);

        if (save != null)
        {
            if (save.terrain != null)
            {
                scenarioSpawner
                    .terrainPaints =
                        save.terrain;
            }

            if (save.units != null)
            {
                scenarioSpawner
                    .placedUnits =
                        save.units;
            }
        }

        scenarioSpawner.ApplyTerrain();
        scenarioSpawner.SpawnRuntimeUnits();
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