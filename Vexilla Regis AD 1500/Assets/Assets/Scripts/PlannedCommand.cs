using UnityEngine;

public enum PlannedCommandType
{
    Move,
    AttackMelee,
    AttackShoot
}

[System.Serializable]
public class PlannedCommand
{
    public PlannedCommandType commandType;
    public Vector2Int targetGridPosition;
    public GameUnit targetUnit;

    public PlannedCommand(PlannedCommandType type, Vector2Int gridPos)
    {
        commandType = type;
        targetGridPosition = gridPos;
        targetUnit = null;
    }

    public PlannedCommand(PlannedCommandType type, GameUnit unit)
    {
        commandType = type;
        targetUnit = unit;
        targetGridPosition = unit != null ? unit.GridPosition : Vector2Int.zero;
    }
}
