using UnityEngine;

public static class FlankingUtility
{
    public static AttackDirection GetAttackDirection(GameUnit attacker, GameUnit defender)
    {
        if (attacker == null || defender == null)
            return AttackDirection.Front;

        Vector2Int delta = attacker.GridPosition - defender.GridPosition;

        if (delta == Vector2Int.zero)
            return AttackDirection.Front;

        Vector2 attackVector = new Vector2(delta.x, delta.y).normalized;
        Vector2 defenderForward = GetForwardVector(defender.transform);

        float angle = Vector2.Angle(defenderForward, attackVector);

        if (angle < 60f)
            return AttackDirection.Front;

        if (angle < 135f)
            return AttackDirection.Flank;

        return AttackDirection.Rear;
    }

    private static Vector2 GetForwardVector(Transform unitTransform)
    {
        if (unitTransform == null)
            return Vector2.up;

        Vector2 forward = new Vector2(unitTransform.up.x, unitTransform.up.y);

        if (forward.sqrMagnitude < 0.01f)
            return Vector2.up;

        return forward.normalized;
    }
}