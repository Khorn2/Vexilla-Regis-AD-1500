using UnityEngine;

[CreateAssetMenu(menuName = "Vexilla/Unit Stats")]
public class UnitStats : ScriptableObject
{
    [Header("Movement")]
    [Min(1)] public int movementRange = 4;
    [Min(0.1f)] public float moveSpeedTilesPerSec = 4f;

    [Header("Unit Size")]
    public int unitSize = 100; // liczebność, pełni funkcję hp

    [Header("Combat")]
    public int meleeDamage = 10;
    public int rangedDamage = 8;
    public int shootRange = 4;

    [Header("Capabilities")]
    public bool canCharge = true;
    public bool canShoot = false;
}
