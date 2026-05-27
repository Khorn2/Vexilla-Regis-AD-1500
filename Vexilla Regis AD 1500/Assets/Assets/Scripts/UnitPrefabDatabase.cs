using System.Collections.Generic;
using UnityEngine;

public class UnitPrefabDatabase : MonoBehaviour
{
    public static UnitPrefabDatabase Instance;

    [System.Serializable]
    public class Entry
    {
        public string id;
        public GameObject prefab;
    }

    [SerializeField]
    private Entry[] entries;

    private Dictionary<string, GameObject>
        lookup;

    private void Awake()
    {
        Instance = this;

        lookup =
            new Dictionary
            <string, GameObject>();

        foreach (Entry e in entries)
        {
            if (!lookup.ContainsKey(e.id))
            {
                lookup.Add(
                    e.id,
                    e.prefab
                );
            }
        }
    }

    public GameObject Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        lookup.TryGetValue(
            id,
            out GameObject prefab
        );

        return prefab;
    }
}