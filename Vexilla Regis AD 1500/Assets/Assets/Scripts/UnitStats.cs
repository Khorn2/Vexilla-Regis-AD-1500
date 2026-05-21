using UnityEngine;

[CreateAssetMenu(menuName = "Vexilla/Unit Stats")]
public class UnitStats : ScriptableObject
{
    [Header("Identity")]
    public string unitName = "Unnamed Unit";

    [Header("Movement")]
    [Min(1)] public int movementRange = 4;
    [Min(0.1f)] public float moveSpeedTilesPerSec = 4f;
    public bool stopMovementOnEnemyContact = true;

    [Header("Unit Size")]
    [Min(1)] public int unitSize = 100;

    [Header("Combat")]
    [Min(0)] public int meleeDamage = 10;
    [Min(0)] public int rangedDamage = 8;
    [Min(0)] public int shootRange = 4;

    [Header("Flanking")]
    [Range(1f, 2f)] public float flankMeleeDamageMultiplier = 1.15f;
    [Range(1f, 2f)] public float rearMeleeDamageMultiplier = 1.30f;
    [Min(0)] public int flankMoralePenalty = 4;
    [Min(0)] public int rearMoralePenalty = 8;

    [Header("Charge Impact")]
    [Min(0f)] public float chargeImpactPerTile = 2f;
    [Min(0)] public int maxChargeBonus = 20;
    [Min(0)] public int minTilesForChargeBonus = 2;

    [Header("Auto Melee")]
    public bool canAutoMelee = true;
    [Range(0f, 2f)] public float autoMeleeDamageMultiplier = 1f;

    [Header("Morale")]
    [Min(1)] public int maxMorale = 100;
    [Min(0)] public int lowMoraleThreshold = 50;
    [Min(0)] public int brokenMoraleThreshold = 15;
    [Range(0f, 1f)] public float lowMoraleDamageMultiplier = 0.8f;
    [Min(0f)] public float moraleDamagePerLostUnit = 2f;
    [Min(0)] public int passiveMoraleRecovery = 4;
    [Min(0)] public int idleMoraleRecoveryBonus = 6;

    [Header("Routing")]
    [Min(1)] public int maxManualRetreatTurns = 2;
    [Min(0)] public int brokenMoraleRecoveryPerTurn = 8;
    [Range(0f, 1f)] public float brokenRallyChance = 0.25f;
    [Min(0)] public int rallyMoraleGain = 20;
    [Min(0)] public int rallyMinimumMorale = 25;

    [Header("Morale Cohesion")]
    [Min(1)] public int moraleSupportRadius = 3;
    [Range(0f, 2f)] public float adjacentAllyMoraleLossMultiplier = 0.75f;
    [Range(0f, 2f)] public float nearbyAllyMoraleLossMultiplier = 0.90f;
    [Range(0f, 2f)] public float normalMoraleLossMultiplier = 1.00f;
    [Range(0f, 3f)] public float isolatedMoraleLossMultiplier = 1.35f;

    [Header("Armor")]
    [Range(0f, 1f)] public float armorPercent = 0.20f;

    [Header("Ammunition")]
    [Min(0)] public int maxAmmo = 100;
    [Min(1)] public int ammoPerShot = 10;

    [Header("Capabilities")]
    public bool canCharge = true;
    public bool canShoot = false;
    public bool isInfantry = true;
    public bool isCavalry = false;
    public bool isCannon = false;
    public bool allowFriendlyFireThroughAlly = false;
}