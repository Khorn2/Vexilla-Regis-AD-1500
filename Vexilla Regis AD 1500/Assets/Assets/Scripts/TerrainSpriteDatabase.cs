using System.Collections.Generic;
using UnityEngine;

public class TerrainSpriteDatabase : MonoBehaviour
{
    public static TerrainSpriteDatabase Instance;

    [System.Serializable]
    public class Entry
    {
        public string id;
        public Sprite sprite;
    }

    [SerializeField]
    private Entry[] entries;

    private Dictionary<string, Sprite> lookup;

    private void Awake()
    {
        Instance = this;

        lookup = new Dictionary<string, Sprite>();

        foreach (Entry e in entries)
        {
            if (!lookup.ContainsKey(e.id))
                lookup.Add(e.id, e.sprite);
        }
    }

    public Sprite Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        lookup.TryGetValue(id, out Sprite sprite);

        return sprite;
    }
}