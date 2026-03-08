using UnityEngine;

[CreateAssetMenu(menuName = "Vexilla/Unit Stats")]
public class UnitStats : ScriptableObject
{
    [Min(0.1f)] public float moveSpeedTilesPerSec = 4f;
}
