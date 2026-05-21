using System.Collections.Generic;
using UnityEngine;

public class EnemyAIManager : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private TurnManager turnManager;

    [Header("Flanking AI")]
    [SerializeField, Range(0f, 1f)] private float flankAttemptChance = 0.65f;
    [SerializeField, Range(0f, 1f)] private float repositionFromFrontChance = 0.35f;
    [SerializeField, Min(0)] private int maxExtraTravelCostForFlank = 3;
    [SerializeField] private bool logFlankingAI = false;

    private enum ShotQuality
    {
        Blocked,
        RiskyThroughOneAlly,
        Clean
    }

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
    }

    public void PlanEnemyTurn()
    {
        if (turnManager != null && turnManager.IsBattleEnded)
            return;

        List<GameUnit> enemies = GetEnemyUnits();

        foreach (GameUnit unit in enemies)
        {
            if (turnManager != null && turnManager.IsBattleEnded)
                return;

            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

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
            if (unit != null && !unit.IsDead && unit.TeamId == 1)
                result.Add(unit);
        }

        return result;
    }

    private List<GameUnit> GetPlayerUnits()
    {
        List<GameUnit> result = new List<GameUnit>();

        foreach (GameUnit unit in GameUnit.AllUnits)
        {
            if (unit != null && !unit.IsDead && !unit.IsBroken && unit.TeamId == 0)
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

        if (enemy.Stats != null && enemy.Stats.canShoot)
        {
            DecideRangedAction(enemy, target);
            return;
        }

        DecideMeleeAction(enemy, target);
    }

    private void DecideRangedAction(GameUnit enemy, GameUnit target)
    {
        GameUnit adjacentEnemy = FindAdjacentEnemy(enemy);

        if (adjacentEnemy != null)
        {
            enemy.SetOrder(OrderType.Charge);
            enemy.QueueAttack(adjacentEnemy, false);
            return;
        }

        ShotQuality currentShotQuality = EvaluateShotQuality(enemy, enemy.GridPosition, target);

        if (currentShotQuality == ShotQuality.Clean && CanShootTargetFromPosition(enemy, enemy.GridPosition, target))
        {
            enemy.SetOrder(OrderType.Shoot);
            enemy.QueueAttack(target, false);
            return;
        }

        if (TryFindBestCleanShootingPosition(enemy, target, out Vector2Int cleanShootingPos))
        {
            if (cleanShootingPos == enemy.GridPosition)
            {
                enemy.SetOrder(OrderType.Shoot);
                enemy.QueueAttack(target, false);
                return;
            }

            enemy.SetOrder(OrderType.March);
            enemy.QueueMove(cleanShootingPos, false);
            return;
        }

        if (currentShotQuality == ShotQuality.RiskyThroughOneAlly && CanShootTargetFromPosition(enemy, enemy.GridPosition, target))
        {
            enemy.SetOrder(OrderType.Shoot);
            enemy.QueueAttack(target, false);
            return;
        }

        if (TryFindBestRiskyShootingPosition(enemy, target, out Vector2Int riskyShootingPos))
        {
            if (riskyShootingPos == enemy.GridPosition)
            {
                enemy.SetOrder(OrderType.Shoot);
                enemy.QueueAttack(target, false);
                return;
            }

            enemy.SetOrder(OrderType.March);
            enemy.QueueMove(riskyShootingPos, false);
            return;
        }

        Vector2Int moveTarget = MoveTowards(enemy, target.GridPosition);
        enemy.SetOrder(OrderType.March);
        enemy.QueueMove(moveTarget, false);
    }

    private void DecideMeleeAction(GameUnit enemy, GameUnit target)
    {
        if (enemy == null || target == null || grid == null)
            return;

        if (enemy.IsAdjacentTo(target))
        {
            AttackDirection currentAttackDirection = FlankingUtility.GetAttackDirection(enemy, target);

            if (currentAttackDirection == AttackDirection.Flank || currentAttackDirection == AttackDirection.Rear)
            {
                enemy.SetOrder(OrderType.Charge);
                enemy.QueueAttack(target, false);

                if (logFlankingAI)
                    Debug.Log($"{enemy.name} attacks from {currentAttackDirection}.");

                return;
            }

            if (Random.value > repositionFromFrontChance)
            {
                enemy.SetOrder(OrderType.Charge);
                enemy.QueueAttack(target, false);
                return;
            }

            if (TryFindBestFlankingTile(enemy, target, out Vector2Int repositionTile))
            {
                if (repositionTile != enemy.GridPosition)
                {
                    enemy.SetOrder(OrderType.March);
                    enemy.QueueMove(repositionTile, false);

                    if (logFlankingAI)
                        Debug.Log($"{enemy.name} repositions for flank to {repositionTile}.");

                    return;
                }
            }

            enemy.SetOrder(OrderType.Charge);
            enemy.QueueAttack(target, false);
            return;
        }

        if (Random.value <= flankAttemptChance)
        {
            if (TryFindBestFlankingTile(enemy, target, out Vector2Int flankTile))
            {
                int flankCost = GetPathTravelCost(enemy, flankTile);

                if (flankCost <= grid.GetMovementBudgetForUnit(enemy))
                {
                    enemy.SetOrder(OrderType.March);
                    enemy.QueueMove(flankTile, false);

                    if (logFlankingAI)
                        Debug.Log($"{enemy.name} moves to flanking tile {flankTile}.");

                    return;
                }

                if (IsFlankRouteReasonable(enemy, target, flankTile))
                {
                    enemy.SetOrder(OrderType.March);
                    enemy.QueueMove(flankTile, false);

                    if (logFlankingAI)
                        Debug.Log($"{enemy.name} starts longer flank route to {flankTile}.");

                    return;
                }
            }
        }

        if (grid.TryGetFreeAdjacentTile(target.GridPosition, enemy.GridPosition, out Vector2Int attackTile))
        {
            Vector2Int resolved = grid.ResolveMoveDestination(enemy, enemy.GridPosition, attackTile);

            if (resolved == attackTile)
            {
                enemy.SetOrder(OrderType.Charge);
                enemy.QueueAttack(target, false);
                return;
            }

            enemy.SetOrder(OrderType.March);
            enemy.QueueMove(attackTile, false);
            return;
        }

        Vector2Int moveTarget = MoveTowards(enemy, target.GridPosition);
        enemy.SetOrder(OrderType.March);
        enemy.QueueMove(moveTarget, false);
    }

    private bool TryFindBestFlankingTile(GameUnit enemy, GameUnit target, out Vector2Int bestTile)
    {
        bestTile = enemy != null ? enemy.GridPosition : Vector2Int.zero;

        if (enemy == null || target == null || grid == null)
            return false;

        Vector2Int[] neighbours = grid.GetNeighbours4(target.GridPosition);

        float bestScore = float.MaxValue;
        bool found = false;

        for (int i = 0; i < neighbours.Length; i++)
        {
            Vector2Int candidate = neighbours[i];

            if (!grid.IsInside(candidate))
                continue;

            if (!grid.IsWalkable(candidate))
                continue;

            GameUnit unitAtCandidate = grid.GetUnitAt(candidate);
            if (unitAtCandidate != null && unitAtCandidate != enemy)
                continue;

            AttackDirection attackDirection = GetAttackDirectionFromPosition(candidate, target);

            if (attackDirection == AttackDirection.Front)
                continue;

            int travelCost = GetPathTravelCost(enemy, candidate);
            if (travelCost == int.MaxValue)
                continue;

            float distanceFromEnemy = Vector2Int.Distance(enemy.GridPosition, candidate);
            float terrainScore = GetMovementTerrainScore(candidate);
            float directionScore = attackDirection == AttackDirection.Rear ? -2f : -1f;
            float score = travelCost + distanceFromEnemy * 0.25f - terrainScore + directionScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestTile = candidate;
                found = true;
            }
        }

        return found;
    }

    private AttackDirection GetAttackDirectionFromPosition(Vector2Int attackerPosition, GameUnit defender)
    {
        if (defender == null)
            return AttackDirection.Front;

        Vector2Int delta = attackerPosition - defender.GridPosition;

        if (delta == Vector2Int.zero)
            return AttackDirection.Front;

        Vector2 attackVector = new Vector2(delta.x, delta.y).normalized;
        Vector2 defenderForward = new Vector2(defender.transform.up.x, defender.transform.up.y);

        if (defenderForward.sqrMagnitude < 0.01f)
            defenderForward = Vector2.up;

        defenderForward.Normalize();

        float angle = Vector2.Angle(defenderForward, attackVector);

        if (angle < 60f)
            return AttackDirection.Front;

        if (angle < 135f)
            return AttackDirection.Flank;

        return AttackDirection.Rear;
    }

    private bool IsFlankRouteReasonable(GameUnit enemy, GameUnit target, Vector2Int flankTile)
    {
        if (enemy == null || target == null || grid == null)
            return false;

        int flankCost = GetPathTravelCost(enemy, flankTile);
        if (flankCost == int.MaxValue)
            return false;

        if (!grid.TryGetFreeAdjacentTile(target.GridPosition, enemy.GridPosition, out Vector2Int normalAttackTile))
            return true;

        int normalCost = GetPathTravelCost(enemy, normalAttackTile);
        if (normalCost == int.MaxValue)
            return true;

        return flankCost <= normalCost + maxExtraTravelCostForFlank;
    }

    private bool CanShootTargetFromPosition(GameUnit shooter, Vector2Int shooterPosition, GameUnit target)
    {
        if (shooter == null || target == null) return false;
        if (shooter.Stats == null) return false;
        if (!shooter.Stats.canShoot) return false;
        if (shooter.CurrentAmmo < shooter.Stats.ammoPerShot) return false;

        if (grid.HasAdjacentEnemyForTeam(shooterPosition, shooter.TeamId))
            return false;

        int range = shooter.Stats.shootRange + grid.GetShootRangeBonusAt(shooterPosition);
        float dist = Vector2Int.Distance(shooterPosition, target.GridPosition);

        return dist <= range;
    }

    private ShotQuality EvaluateShotQuality(GameUnit shooter, Vector2Int shooterPosition, GameUnit target)
    {
        if (shooter == null || target == null || grid == null)
            return ShotQuality.Blocked;

        if (!CanShootTargetFromPosition(shooter, shooterPosition, target))
            return ShotQuality.Blocked;

        List<Vector2Int> line = GetLinePoints(shooterPosition, target.GridPosition);

        int alliesOnLine = 0;

        for (int i = 1; i < line.Count - 1; i++)
        {
            GameUnit unitOnLine = grid.GetUnitAt(line[i]);
            if (unitOnLine == null)
                continue;

            if (unitOnLine.TeamId == shooter.TeamId)
            {
                alliesOnLine++;

                if (alliesOnLine >= 2)
                    return ShotQuality.Blocked;
            }
            else
            {
                return ShotQuality.Blocked;
            }
        }

        if (alliesOnLine == 1)
            return ShotQuality.RiskyThroughOneAlly;

        return ShotQuality.Clean;
    }

    private bool TryFindBestCleanShootingPosition(GameUnit enemy, GameUnit target, out Vector2Int bestPos)
    {
        return TryFindBestShootingPositionByQuality(enemy, target, ShotQuality.Clean, out bestPos);
    }

    private bool TryFindBestRiskyShootingPosition(GameUnit enemy, GameUnit target, out Vector2Int bestPos)
    {
        return TryFindBestShootingPositionByQuality(enemy, target, ShotQuality.RiskyThroughOneAlly, out bestPos);
    }

    private bool TryFindBestShootingPositionByQuality(GameUnit enemy, GameUnit target, ShotQuality requiredQuality, out Vector2Int bestPos)
    {
        bestPos = enemy.GridPosition;

        if (enemy == null || target == null || enemy.Stats == null || grid == null)
            return false;

        int moveRange = grid.GetMovementBudgetForUnit(enemy);
        float bestScore = float.MaxValue;
        bool found = false;

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                Vector2Int candidate = new Vector2Int(enemy.GridPosition.x + x, enemy.GridPosition.y + y);

                if (!grid.IsInside(candidate))
                    continue;

                if (!grid.IsWalkable(candidate))
                    continue;

                GameUnit unitAtCandidate = grid.GetUnitAt(candidate);
                if (unitAtCandidate != null && unitAtCandidate != enemy)
                    continue;

                int travelCost = GetPathTravelCost(enemy, candidate);
                if (travelCost == int.MaxValue)
                    continue;

                if (travelCost > moveRange)
                    continue;

                ShotQuality quality = EvaluateShotQuality(enemy, candidate, target);

                if (quality != requiredQuality)
                    continue;

                float targetDist = Vector2Int.Distance(candidate, target.GridPosition);
                float terrainScore = GetShootingTerrainScore(candidate);
                float score = travelCost + targetDist * 0.25f - terrainScore;

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

    private int GetPathTravelCost(GameUnit mover, Vector2Int target)
    {
        if (mover == null || grid == null)
            return int.MaxValue;

        if (target == mover.GridPosition)
            return 0;

        GridPathfinder pathfinder = new GridPathfinder(grid);
        List<Vector2Int> path = pathfinder.FindPath(mover.GridPosition, target, mover);

        if (path == null || path.Count == 0)
            return int.MaxValue;

        int totalCost = 0;
        Vector2Int previous = mover.GridPosition;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int current = path[i];
            int stepCost = grid.GetMovementCost(previous, current, mover);

            if (stepCost == int.MaxValue)
                return int.MaxValue;

            totalCost += stepCost;
            previous = current;
        }

        return totalCost;
    }

    private float GetShootingTerrainScore(Vector2Int position)
    {
        Tile tile = grid.GetTileAt(position);
        if (tile == null)
            return 0f;

        float score = 0f;

        if (tile.HeightLevel >= 2)
            score += tile.HeightLevel * 0.5f;

        if (tile.TerrainType == TerrainType.Forest)
            score += 0.75f;

        if (tile.TerrainType == TerrainType.Road)
            score += 0.25f;

        if (tile.TerrainType == TerrainType.ShallowWater)
            score -= 1.0f;

        if (tile.TerrainType == TerrainType.RoughTerrain)
            score -= 0.25f;

        return score;
    }

    private GameUnit FindAdjacentEnemy(GameUnit unit)
    {
        if (unit == null || grid == null)
            return null;

        Vector2Int[] neighbours = grid.GetNeighbours4(unit.GridPosition);

        for (int i = 0; i < neighbours.Length; i++)
        {
            GameUnit other = grid.GetUnitAt(neighbours[i]);

            if (other != null && !other.IsDead && !other.IsBroken && other.TeamId != unit.TeamId)
                return other;
        }

        return null;
    }

    private Vector2Int MoveTowards(GameUnit mover, Vector2Int target)
    {
        if (mover == null || grid == null)
            return target;

        int moveRange = grid.GetMovementBudgetForUnit(mover);
        Vector2Int best = mover.GridPosition;
        float bestDist = Vector2Int.Distance(mover.GridPosition, target);

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                Vector2Int candidate = new Vector2Int(mover.GridPosition.x + x, mover.GridPosition.y + y);

                if (!grid.IsInside(candidate))
                    continue;

                if (!grid.IsWalkable(candidate))
                    continue;

                GameUnit unitAtCandidate = grid.GetUnitAt(candidate);
                if (unitAtCandidate != null && unitAtCandidate != mover)
                    continue;

                int travelCost = GetPathTravelCost(mover, candidate);
                if (travelCost == int.MaxValue)
                    continue;

                if (travelCost > moveRange)
                    continue;

                float distToTarget = Vector2Int.Distance(candidate, target);
                float terrainScore = GetMovementTerrainScore(candidate);
                float score = distToTarget - terrainScore;

                float bestScore = bestDist - GetMovementTerrainScore(best);

                if (score < bestScore)
                {
                    bestDist = distToTarget;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private float GetMovementTerrainScore(Vector2Int position)
    {
        Tile tile = grid.GetTileAt(position);
        if (tile == null)
            return 0f;

        float score = 0f;

        if (tile.TerrainType == TerrainType.Road)
            score += 0.35f;

        if (tile.TerrainType == TerrainType.Forest)
            score += 0.15f;

        if (tile.TerrainType == TerrainType.ShallowWater)
            score -= 0.75f;

        if (tile.TerrainType == TerrainType.RoughTerrain)
            score -= 0.25f;

        if (tile.HeightLevel >= 2)
            score += tile.HeightLevel * 0.1f;

        return score;
    }

    private List<Vector2Int> GetLinePoints(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            result.Add(new Vector2Int(x0, y0));

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return result;
    }
}