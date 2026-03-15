using System.Collections.Generic;
using UnityEngine;

public class EnemyAIManager : MonoBehaviour
{
    [SerializeField] private GridManager grid;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();
    }

    public void PlanEnemyTurn()
    {
        List<GameUnit> enemies = GetEnemyUnits();

        foreach (GameUnit unit in enemies)
        {
            if (unit == null) continue;

            unit.ClearPlannedAction();

            GameUnit target = FindBestPlayerTarget(unit);
            if (target == null) continue;

            DecideAction(unit, target);
        }

        Debug.Log("Enemy AI finished planning.");
    }

    private List<GameUnit> GetEnemyUnits()
    {
        List<GameUnit> result = new List<GameUnit>();

        foreach (GameUnit unit in GameUnit.AllUnits)
        {
            if (unit != null && unit.TeamId == 1)
                result.Add(unit);
        }

        return result;
    }

    private List<GameUnit> GetPlayerUnits()
    {
        List<GameUnit> result = new List<GameUnit>();

        foreach (GameUnit unit in GameUnit.AllUnits)
        {
            if (unit != null && unit.TeamId == 0)
                result.Add(unit);
        }

        return result;
    }

    private GameUnit FindBestPlayerTarget(GameUnit enemy)
    {
        List<GameUnit> players = GetPlayerUnits();

        GameUnit best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < players.Count; i++)
        {
            GameUnit player = players[i];
            if (player == null) continue;

            float dist = Vector2Int.Distance(enemy.GridPosition, player.GridPosition);

            // niższy score = lepszy cel
            // bliżej = lepiej, osłabiony = też lepiej
            float score = dist - (player.CurrentSize * 0.01f);

            if (score < bestScore)
            {
                bestScore = score;
                best = player;
            }
        }

        return best;
    }

    private void DecideAction(GameUnit enemy, GameUnit target)
    {
        if (enemy == null || target == null || grid == null)
            return;

        // jednostka strzelająca
        if (enemy.Stats != null && enemy.Stats.canShoot)
        {
            DecideRangedAction(enemy, target);
            return;
        }

        // jednostka melee
        DecideMeleeAction(enemy, target);
    }

    private void DecideRangedAction(GameUnit enemy, GameUnit target)
    {
        // jeśli wróg obok, ranged przechodzi do melee
        GameUnit adjacentEnemy = FindAdjacentEnemy(enemy);
        if (adjacentEnemy != null)
        {
            enemy.SetOrder(OrderType.Charge);
            enemy.QueueAttack(adjacentEnemy, false);
            return;
        }

        // jeśli da się strzelić -> strzał
        if (CanShootTarget(enemy, target))
        {
            enemy.SetOrder(OrderType.Shoot);
            enemy.QueueAttack(target, false);
            return;
        }

        // spróbuj ustawić się na tile, z którego będzie mógł strzelać
        if (TryFindBestShootingPosition(enemy, target, out Vector2Int shootingPos))
        {
            enemy.SetOrder(OrderType.March);
            enemy.QueueMove(shootingPos, false);
            return;
        }

        // fallback: podejdź bliżej
        Vector2Int moveTarget = MoveTowards(enemy, target.GridPosition);
        enemy.SetOrder(OrderType.March);
        enemy.QueueMove(moveTarget, false);
    }

    private void DecideMeleeAction(GameUnit enemy, GameUnit target)
    {
        // jeśli już stoi obok -> atak
        if (enemy.IsAdjacentTo(target))
        {
            enemy.SetOrder(OrderType.Charge);
            enemy.QueueAttack(target, false);
            return;
        }

        // jeśli da się dobiec do pozycji ataku w tej turze -> szarża
        if (grid.TryGetFreeAdjacentTile(target.GridPosition, enemy.GridPosition, out Vector2Int attackTile))
        {
            Vector2Int resolved = grid.ResolveMoveDestination(enemy, enemy.GridPosition, attackTile);

            if (resolved == attackTile)
            {
                enemy.SetOrder(OrderType.Charge);
                enemy.QueueAttack(target, false);
                return;
            }

            // nie dobiegnie w tej turze -> podejdź
            enemy.SetOrder(OrderType.March);
            enemy.QueueMove(attackTile, false);
            return;
        }

        // fallback: zwykłe podejście
        Vector2Int moveTarget = MoveTowards(enemy, target.GridPosition);
        enemy.SetOrder(OrderType.March);
        enemy.QueueMove(moveTarget, false);
    }

    private bool CanShootTarget(GameUnit shooter, GameUnit target)
    {
        if (shooter == null || target == null) return false;
        if (shooter.Stats == null) return false;
        if (!shooter.Stats.canShoot) return false;

        // jeśli wróg obok, strzał zablokowany
        if (shooter.HasAdjacentEnemy()) return false;

        float dist = Vector2Int.Distance(shooter.GridPosition, target.GridPosition);
        return dist <= shooter.Stats.shootRange;
    }

    private GameUnit FindAdjacentEnemy(GameUnit unit)
    {
        if (unit == null || grid == null) return null;

        Vector2Int[] neighbours = grid.GetNeighbours4(unit.GridPosition);

        for (int i = 0; i < neighbours.Length; i++)
        {
            GameUnit other = grid.GetUnitAt(neighbours[i]);
            if (other != null && other.TeamId != unit.TeamId)
                return other;
        }

        return null;
    }

    private bool TryFindBestShootingPosition(GameUnit enemy, GameUnit target, out Vector2Int bestPos)
    {
        bestPos = enemy.GridPosition;

        if (enemy == null || target == null || enemy.Stats == null)
            return false;

        int moveRange = enemy.GetMovementRange();
        int shootRange = enemy.Stats.shootRange;

        float bestScore = float.MaxValue;
        bool found = false;

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                Vector2Int candidate = new Vector2Int(enemy.GridPosition.x + x, enemy.GridPosition.y + y);

                if (!grid.IsInside(candidate))
                    continue;

                // ograniczenie zasięgu ruchu po Manhattan
                int moveDist = Mathf.Abs(x) + Mathf.Abs(y);
                if (moveDist > moveRange)
                    continue;

                if (candidate != enemy.GridPosition && grid.IsOccupied(candidate))
                    continue;

                float targetDist = Vector2Int.Distance(candidate, target.GridPosition);

                // musi wejść w zasięg strzału
                if (targetDist > shootRange)
                    continue;

                // nie chcemy stawać obok celu jeśli jesteśmy ranged
                if (targetDist <= 1.5f)
                    continue;

                // preferuj pozycję bliżej obecnej i lekko bliżej celu
                float score = moveDist + targetDist * 0.25f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestPos = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private Vector2Int MoveTowards(GameUnit mover, Vector2Int target)
    {
        if (mover == null || grid == null)
            return target;

        int moveRange = mover.GetMovementRange();
        Vector2Int best = mover.GridPosition;
        float bestDist = Vector2Int.Distance(mover.GridPosition, target);

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                Vector2Int candidate = new Vector2Int(mover.GridPosition.x + x, mover.GridPosition.y + y);

                if (!grid.IsInside(candidate))
                    continue;

                int moveDist = Mathf.Abs(x) + Mathf.Abs(y);
                if (moveDist > moveRange)
                    continue;

                Vector2Int resolved = grid.ResolveMoveDestination(mover, mover.GridPosition, candidate);

                if (resolved == mover.GridPosition)
                    continue;

                float distToTarget = Vector2Int.Distance(resolved, target);

                if (distToTarget < bestDist)
                {
                    bestDist = distToTarget;
                    best = resolved;
                }
            }
        }

        return best;
    }
}