using System.Collections.Generic;
using UnityEngine;

public class EnemyAIManager : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private TurnManager turnManager;

    [Header("Tactical AI")]
    [SerializeField, Range(0f, 1f)] private float globalAggression = 0.45f;
    [SerializeField, Range(0f, 1f)] private float lineHoldingBias = 0.75f;
    [SerializeField, Range(0f, 1f)] private float rangedPreference = 0.9f;
    [SerializeField, Range(0f, 1f)] private float cannonDistancePreference = 0.9f;
    [SerializeField, Range(0f, 1f)] private float cavalryAggressionBonus = 0.25f;
    [SerializeField, Min(1)] private int desiredLineDistance = 4;
    [SerializeField, Min(1)] private int cannonDesiredDistance = 6;
    [SerializeField, Min(1)] private int threatScanRadius = 6;
    [SerializeField, Min(0f)] private float allOutAttackAdvantageThreshold = 1.15f;
    [SerializeField, Min(0f)] private float highThreatThreshold = 70f;
    [SerializeField] private bool logTacticalAI = false;

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

    private enum TacticalActionType
    {
        Hold,
        Shoot,
        MoveToShootingPosition,
        MoveToLinePosition,
        MoveTowards,
        Charge,
        Flank
    }

    private struct TacticalAction
    {
        public TacticalActionType type;
        public GameUnit target;
        public Vector2Int position;
        public float score;
        public ShotQuality shotQuality;
    }

    private readonly List<GameUnit> enemiesBuffer = new List<GameUnit>(64);
    private readonly List<GameUnit> playersBuffer = new List<GameUnit>(64);
    private readonly Dictionary<Vector2Int, int> pathCostCache = new Dictionary<Vector2Int, int>(128);
    private readonly List<Vector2Int> lineBuffer = new List<Vector2Int>(64);

    private GridPathfinder pathfinder;
    private GameUnit cachedMover;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();

        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();

        if (grid != null)
            pathfinder = new GridPathfinder(grid);
    }

    public void PlanEnemyTurn()
    {
        if (turnManager != null && turnManager.IsBattleEnded)
            return;

        if (grid == null)
            return;

        if (pathfinder == null)
            pathfinder = new GridPathfinder(grid);

        GetUnitsByTeam(1, enemiesBuffer);
        GetUnitsByTeam(0, playersBuffer);

        float armyAdvantage = EvaluateArmyAdvantage(enemiesBuffer, playersBuffer);
        bool allOutAttack = ShouldCommitToAttack(enemiesBuffer, playersBuffer, armyAdvantage);

        for (int i = 0; i < enemiesBuffer.Count; i++)
        {
            if (turnManager != null && turnManager.IsBattleEnded)
                return;

            GameUnit unit = enemiesBuffer[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;

            unit.ClearPlannedAction();

            BeginUnitPathCache(unit);

            TacticalAction action = ChooseBestAction(unit, playersBuffer, armyAdvantage, allOutAttack);
            ApplyAction(unit, action);
        }

        Debug.Log("Enemy AI finished tactical planning.");
    }

    private void BeginUnitPathCache(GameUnit mover)
    {
        cachedMover = mover;
        pathCostCache.Clear();
    }

    private void GetUnitsByTeam(int teamId, List<GameUnit> result)
    {
        result.Clear();

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;
            if (unit.TeamId != teamId) continue;

            result.Add(unit);
        }
    }

    private TacticalAction ChooseBestAction(GameUnit enemy, List<GameUnit> players, float armyAdvantage, bool allOutAttack)
    {
        TacticalAction best = new TacticalAction
        {
            type = TacticalActionType.Hold,
            target = null,
            position = enemy != null ? enemy.GridPosition : Vector2Int.zero,
            score = 0f,
            shotQuality = ShotQuality.Blocked
        };

        if (enemy == null || enemy.Stats == null || grid == null || players == null || players.Count == 0)
            return best;

        float localThreat = EvaluateLocalThreat(enemy, players);
        float aggression = GetUnitAggression(enemy, armyAdvantage, localThreat, allOutAttack);

        for (int i = 0; i < players.Count; i++)
        {
            GameUnit target = players[i];

            if (target == null) continue;
            if (target.IsDead) continue;
            if (target.IsBroken) continue;

            TrySetBest(ref best, BuildHoldAction(enemy, target, aggression, localThreat));

            if (enemy.Stats.canShoot)
            {
                TrySetBest(ref best, BuildShootAction(enemy, target, aggression, localThreat));
                TrySetBest(ref best, BuildMoveToShootingPositionAction(enemy, target, aggression, localThreat));
                TrySetBest(ref best, BuildMoveToLinePositionAction(enemy, target, aggression, localThreat));
                continue;
            }

            if (enemy.Stats.canCharge)
            {
                TrySetBest(ref best, BuildChargeAction(enemy, target, aggression, localThreat, allOutAttack));
                TrySetBest(ref best, BuildFlankAction(enemy, target, aggression, localThreat, allOutAttack));
            }

            TrySetBest(ref best, BuildMoveTowardsAction(enemy, target, aggression, localThreat, allOutAttack));
        }

        if (logTacticalAI)
            Debug.Log($"{enemy.name} AI action: {best.type}, score={best.score:F1}, target={(best.target != null ? best.target.name : "none")}, pos={best.position}");

        return best;
    }

    private void TrySetBest(ref TacticalAction best, TacticalAction candidate)
    {
        if (candidate.score > best.score)
            best = candidate;
    }

    private TacticalAction BuildHoldAction(GameUnit enemy, GameUnit target, float aggression, float localThreat)
    {
        float distance = Vector2Int.Distance(enemy.GridPosition, target.GridPosition);
        float score = 10f;

        if (enemy.Stats.canShoot)
        {
            int preferredDistance = enemy.Stats.isCannon ? cannonDesiredDistance : desiredLineDistance;
            float distanceFit = Mathf.Max(0f, 16f - Mathf.Abs(distance - preferredDistance) * 2f);

            score += distanceFit;
            score += lineHoldingBias * 35f;

            if (CanShootTargetFromPosition(enemy, enemy.GridPosition, target))
                score += rangedPreference * 35f;

            if (enemy.Stats.isCannon)
                score += cannonDistancePreference * 35f;
        }

        if (localThreat > highThreatThreshold)
            score -= 8f;

        score -= aggression * 8f;

        return new TacticalAction
        {
            type = TacticalActionType.Hold,
            target = target,
            position = enemy.GridPosition,
            score = score,
            shotQuality = ShotQuality.Blocked
        };
    }

    private TacticalAction BuildShootAction(GameUnit enemy, GameUnit target, float aggression, float localThreat)
    {
        ShotQuality quality = EvaluateShotQuality(enemy, enemy.GridPosition, target);

        if (quality == ShotQuality.Blocked)
            return InvalidAction();

        float score = 70f;
        score += EvaluateTargetPriority(enemy, target);
        score += quality == ShotQuality.Clean ? 25f : 5f;

        if (enemy.Stats.isCannon)
            score += 35f;

        return new TacticalAction
        {
            type = TacticalActionType.Shoot,
            target = target,
            position = enemy.GridPosition,
            score = score,
            shotQuality = quality
        };
    }

    private TacticalAction BuildMoveToShootingPositionAction(GameUnit enemy, GameUnit target, float aggression, float localThreat)
    {
        if (!TryFindBestShootingPosition(enemy, target, out Vector2Int bestPos, out ShotQuality quality, out float utility))
            return InvalidAction();

        if (bestPos == enemy.GridPosition)
            return InvalidAction();

        float score = 55f;
        score += utility;
        score += EvaluateTargetPriority(enemy, target) * 0.5f;
        score += quality == ShotQuality.Clean ? 20f : 4f;

        if (enemy.Stats.isCannon)
            score += 20f;

        if (localThreat > highThreatThreshold)
            score -= 8f;

        return new TacticalAction
        {
            type = TacticalActionType.MoveToShootingPosition,
            target = target,
            position = bestPos,
            score = score,
            shotQuality = quality
        };
    }

    private TacticalAction BuildMoveToLinePositionAction(GameUnit enemy, GameUnit target, float aggression, float localThreat)
    {
        if (!TryFindBestLineHoldingPosition(enemy, target, out Vector2Int linePos, out float utility))
            return InvalidAction();

        if (linePos == enemy.GridPosition)
            return InvalidAction();

        float score = 42f;
        score += utility;
        score += lineHoldingBias * 25f;

        if (enemy.Stats.isCannon)
            score += cannonDistancePreference * 20f;

        return new TacticalAction
        {
            type = TacticalActionType.MoveToLinePosition,
            target = target,
            position = linePos,
            score = score,
            shotQuality = ShotQuality.Blocked
        };
    }

    private TacticalAction BuildChargeAction(GameUnit enemy, GameUnit target, float aggression, float localThreat, bool allOutAttack)
    {
        if (enemy.Stats.canShoot)
            return InvalidAction();

        if (enemy.Stats.isCannon)
            return InvalidAction();

        bool adjacent = enemy.IsAdjacentTo(target);
        bool canReachAdjacent = CanReachAdjacentToTarget(enemy, target, out Vector2Int attackTile);

        if (!adjacent && !canReachAdjacent)
            return InvalidAction();

        float score = 20f;
        score += EvaluateTargetPriority(enemy, target);
        score += aggression * 35f;
        score += allOutAttack ? 25f : 0f;
        score += localThreat > highThreatThreshold ? 10f : 0f;

        if (enemy.Stats.isCavalry)
            score += cavalryAggressionBonus * 40f;

        if (adjacent)
        {
            AttackDirection direction = FlankingUtility.GetAttackDirection(enemy, target);

            if (direction == AttackDirection.Flank)
                score += 18f;
            else if (direction == AttackDirection.Rear)
                score += 28f;
            else
                score += Random.value <= repositionFromFrontChance ? -10f : 4f;
        }

        return new TacticalAction
        {
            type = TacticalActionType.Charge,
            target = target,
            position = adjacent ? enemy.GridPosition : attackTile,
            score = score,
            shotQuality = ShotQuality.Blocked
        };
    }

    private TacticalAction BuildFlankAction(GameUnit enemy, GameUnit target, float aggression, float localThreat, bool allOutAttack)
    {
        if (enemy.Stats.canShoot)
            return InvalidAction();

        if (enemy.Stats.isCannon)
            return InvalidAction();

        float chance = flankAttemptChance;

        if (enemy.Stats.isCavalry)
            chance += 0.2f;

        if (Random.value > Mathf.Clamp01(chance))
            return InvalidAction();

        if (!TryFindBestFlankingTile(enemy, target, out Vector2Int flankTile))
            return InvalidAction();

        int flankCost = GetPathTravelCost(enemy, flankTile);
        if (flankCost == int.MaxValue)
            return InvalidAction();

        bool canReachNow = flankCost <= grid.GetMovementBudgetForUnit(enemy);
        bool reasonableLongRoute = IsFlankRouteReasonable(enemy, target, flankTile);

        if (!canReachNow && !reasonableLongRoute)
            return InvalidAction();

        float score = 30f;
        score += EvaluateTargetPriority(enemy, target);
        score += aggression * 20f;
        score += enemy.Stats.isCavalry ? 25f : 0f;
        score -= flankCost * 1.5f;

        AttackDirection direction = GetAttackDirectionFromPosition(flankTile, target);

        if (direction == AttackDirection.Rear)
            score += 28f;
        else if (direction == AttackDirection.Flank)
            score += 18f;

        if (logFlankingAI)
            Debug.Log($"{enemy.name} considers flank on {target.name}: tile={flankTile}, score={score:F1}, direction={direction}");

        return new TacticalAction
        {
            type = TacticalActionType.Flank,
            target = target,
            position = flankTile,
            score = score,
            shotQuality = ShotQuality.Blocked
        };
    }

    private TacticalAction BuildMoveTowardsAction(GameUnit enemy, GameUnit target, float aggression, float localThreat, bool allOutAttack)
    {
        if (enemy.Stats.canShoot)
            return InvalidAction();

        Vector2Int moveTarget = MoveTowards(enemy, target.GridPosition);

        if (moveTarget == enemy.GridPosition)
            return InvalidAction();

        float score = 18f;
        score += aggression * 25f;
        score += allOutAttack ? 18f : 0f;
        score += EvaluateTargetPriority(enemy, target) * 0.35f;

        return new TacticalAction
        {
            type = TacticalActionType.MoveTowards,
            target = target,
            position = moveTarget,
            score = score,
            shotQuality = ShotQuality.Blocked
        };
    }

    private TacticalAction InvalidAction()
    {
        return new TacticalAction
        {
            type = TacticalActionType.Hold,
            target = null,
            position = Vector2Int.zero,
            score = float.MinValue,
            shotQuality = ShotQuality.Blocked
        };
    }

    private void ApplyAction(GameUnit enemy, TacticalAction action)
    {
        if (enemy == null || enemy.IsDead || enemy.IsBroken)
            return;

        switch (action.type)
        {
            case TacticalActionType.Hold:
                enemy.SetOrder(OrderType.March);
                break;

            case TacticalActionType.Shoot:
                enemy.SetOrder(OrderType.Shoot);
                enemy.QueueAttack(action.target, false);
                break;

            case TacticalActionType.MoveToShootingPosition:
            case TacticalActionType.MoveToLinePosition:
            case TacticalActionType.MoveTowards:
                enemy.SetOrder(OrderType.March);
                enemy.QueueMove(action.position, false);
                break;

            case TacticalActionType.Charge:
                enemy.SetOrder(OrderType.Charge);

                if (enemy.IsAdjacentTo(action.target))
                    enemy.QueueAttack(action.target, false);
                else if (action.position != enemy.GridPosition)
                    enemy.QueueMove(action.position, false);

                break;

            case TacticalActionType.Flank:
                enemy.SetOrder(OrderType.March);

                if (action.position != enemy.GridPosition)
                    enemy.QueueMove(action.position, false);

                break;
        }
    }

    private float EvaluateArmyAdvantage(List<GameUnit> enemies, List<GameUnit> players)
    {
        float enemyPower = EvaluateArmyPower(enemies);
        float playerPower = EvaluateArmyPower(players);

        if (playerPower <= 0.01f)
            return 99f;

        return enemyPower / playerPower;
    }

    private float EvaluateArmyPower(List<GameUnit> units)
    {
        float total = 0f;

        if (units == null)
            return total;

        for (int i = 0; i < units.Count; i++)
        {
            GameUnit unit = units[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;
            if (unit.IsBroken) continue;
            if (unit.Stats == null) continue;

            float power = unit.CurrentSize;
            power += unit.CurrentMorale * 0.5f;
            power += unit.Stats.meleeDamage * 3f;

            if (unit.Stats.canShoot)
            {
                power += unit.Stats.rangedDamage * 4f;
                power += unit.GetCurrentShootRange() * 5f;
                power += unit.CurrentAmmo * 0.2f;
            }

            if (unit.Stats.isCavalry)
                power *= 1.15f;

            if (unit.Stats.isCannon)
                power *= 1.25f;

            total += Mathf.Max(0f, power);
        }

        return total;
    }

    private bool ShouldCommitToAttack(List<GameUnit> enemies, List<GameUnit> players, float armyAdvantage)
    {
        if (players == null || players.Count == 0)
            return false;

        if (armyAdvantage >= allOutAttackAdvantageThreshold)
            return true;

        float totalThreat = 0f;

        for (int i = 0; i < enemies.Count; i++)
            totalThreat += EvaluateLocalThreat(enemies[i], players);

        float averageThreat = enemies.Count > 0 ? totalThreat / enemies.Count : 0f;
        return averageThreat >= highThreatThreshold;
    }

    private float EvaluateLocalThreat(GameUnit enemy, List<GameUnit> players)
    {
        if (enemy == null || players == null)
            return 0f;

        float threat = 0f;

        for (int i = 0; i < players.Count; i++)
        {
            GameUnit player = players[i];

            if (player == null) continue;
            if (player.IsDead) continue;
            if (player.IsBroken) continue;
            if (player.Stats == null) continue;

            float distance = Vector2Int.Distance(enemy.GridPosition, player.GridPosition);

            if (distance > threatScanRadius)
                continue;

            float distanceFactor = Mathf.Clamp01(1f - distance / Mathf.Max(1f, threatScanRadius));
            float unitThreat = player.CurrentSize * 0.25f;
            unitThreat += player.CurrentMorale * 0.15f;
            unitThreat += player.Stats.meleeDamage * 1.5f;

            if (player.Stats.canShoot)
            {
                unitThreat += player.Stats.rangedDamage * 2f;

                if (distance <= player.GetCurrentShootRange())
                    unitThreat += 25f;
            }

            if (player.Stats.isCavalry)
                unitThreat += 15f;

            if (player.Stats.isCannon)
                unitThreat += 25f;

            threat += unitThreat * distanceFactor;
        }

        return threat;
    }

    private float GetUnitAggression(GameUnit enemy, float armyAdvantage, float localThreat, bool allOutAttack)
    {
        float aggression = globalAggression;

        if (armyAdvantage >= allOutAttackAdvantageThreshold)
            aggression += 0.2f;

        if (localThreat >= highThreatThreshold)
            aggression += 0.2f;

        if (allOutAttack)
            aggression += 0.15f;

        if (enemy != null && enemy.Stats != null)
        {
            if (enemy.Stats.isCavalry)
                aggression += cavalryAggressionBonus;

            if (enemy.Stats.isCannon)
                aggression -= 0.4f;

            if (enemy.Stats.canShoot)
                aggression -= 0.45f;

            if (enemy.CurrentMorale < enemy.Stats.lowMoraleThreshold)
                aggression -= 0.2f;
        }

        return Mathf.Clamp01(aggression);
    }

    private float EvaluateTargetPriority(GameUnit attacker, GameUnit target)
    {
        if (attacker == null || target == null || target.Stats == null)
            return 0f;

        float score = 0f;
        float distance = Vector2Int.Distance(attacker.GridPosition, target.GridPosition);

        score += Mathf.Max(0f, 12f - distance) * 2f;

        float sizeLost = 1f - ((float)target.CurrentSize / Mathf.Max(1, target.Stats.unitSize));
        score += sizeLost * 25f;

        float moraleLost = 1f - ((float)target.CurrentMorale / Mathf.Max(1, target.Stats.maxMorale));
        score += moraleLost * 20f;

        if (target.Stats.canShoot)
            score += 12f;

        if (target.Stats.isCannon)
            score += 18f;

        if (target.Stats.isCavalry)
            score += 10f;

        if (target.CurrentMorale < target.Stats.lowMoraleThreshold)
            score += 12f;

        return score;
    }

    private bool TryFindBestShootingPosition(GameUnit enemy, GameUnit target, out Vector2Int bestPos, out ShotQuality bestQuality, out float bestUtility)
    {
        bestPos = enemy.GridPosition;
        bestQuality = ShotQuality.Blocked;
        bestUtility = float.MinValue;

        int moveRange = grid.GetMovementBudgetForUnit(enemy);
        bool found = false;

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > moveRange)
                    continue;

                Vector2Int candidate = new Vector2Int(enemy.GridPosition.x + x, enemy.GridPosition.y + y);

                if (!IsValidMoveCandidate(enemy, candidate))
                    continue;

                int travelCost = GetPathTravelCost(enemy, candidate);
                if (travelCost == int.MaxValue || travelCost > moveRange)
                    continue;

                ShotQuality quality = EvaluateShotQuality(enemy, candidate, target);
                if (quality == ShotQuality.Blocked)
                    continue;

                float distance = Vector2Int.Distance(candidate, target.GridPosition);
                float terrainScore = GetShootingTerrainScore(candidate);
                float qualityScore = quality == ShotQuality.Clean ? 25f : 6f;
                float rangeFit = Mathf.Max(0f, enemy.Stats.shootRange + grid.GetShootRangeBonusAt(candidate) - distance);

                float utility = qualityScore + terrainScore * 5f + rangeFit * 2f - travelCost * 1.5f;

                if (enemy.Stats.isCannon)
                {
                    float desiredPenalty = Mathf.Abs(distance - cannonDesiredDistance) * 2f;
                    utility += cannonDistancePreference * 15f - desiredPenalty;
                }

                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestPos = candidate;
                    bestQuality = quality;
                    found = true;
                }
            }
        }

        return found;
    }

    private bool TryFindBestLineHoldingPosition(GameUnit enemy, GameUnit target, out Vector2Int bestPos, out float bestUtility)
    {
        bestPos = enemy.GridPosition;
        bestUtility = float.MinValue;

        int moveRange = grid.GetMovementBudgetForUnit(enemy);
        int preferredDistance = enemy.Stats.isCannon ? cannonDesiredDistance : desiredLineDistance;
        bool found = false;

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > moveRange)
                    continue;

                Vector2Int candidate = new Vector2Int(enemy.GridPosition.x + x, enemy.GridPosition.y + y);

                if (!IsValidMoveCandidate(enemy, candidate))
                    continue;

                int travelCost = GetPathTravelCost(enemy, candidate);
                if (travelCost == int.MaxValue || travelCost > moveRange)
                    continue;

                if (grid.HasAdjacentEnemyForTeam(candidate, enemy.TeamId))
                    continue;

                float distance = Vector2Int.Distance(candidate, target.GridPosition);
                float distanceFit = Mathf.Max(0f, 18f - Mathf.Abs(distance - preferredDistance) * 3f);
                float terrainScore = GetShootingTerrainScore(candidate);
                float cohesionScore = CountNearbyAllies(enemy, candidate, 3) * 4f;

                float utility = distanceFit + terrainScore * 4f + cohesionScore - travelCost;

                if (enemy.Stats.isCannon)
                    utility += cannonDistancePreference * 12f;

                if (utility > bestUtility)
                {
                    bestUtility = utility;
                    bestPos = candidate;
                    found = true;
                }
            }
        }

        return found;
    }

    private int CountNearbyAllies(GameUnit unit, Vector2Int position, int radius)
    {
        int count = 0;

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit other = GameUnit.AllUnits[i];

            if (other == null) continue;
            if (other == unit) continue;
            if (other.IsDead) continue;
            if (other.IsBroken) continue;
            if (other.TeamId != unit.TeamId) continue;

            int distance = Mathf.Abs(other.GridPosition.x - position.x) + Mathf.Abs(other.GridPosition.y - position.y);

            if (distance <= radius)
                count++;
        }

        return count;
    }

    private bool CanReachAdjacentToTarget(GameUnit enemy, GameUnit target, out Vector2Int attackTile)
    {
        attackTile = enemy.GridPosition;

        if (!grid.TryGetFreeAdjacentTile(target.GridPosition, enemy.GridPosition, out attackTile))
            return false;

        int travelCost = GetPathTravelCost(enemy, attackTile);

        if (travelCost == int.MaxValue)
            return false;

        return travelCost <= grid.GetMovementBudgetForUnit(enemy);
    }

    private bool IsValidMoveCandidate(GameUnit enemy, Vector2Int candidate)
    {
        if (!grid.IsInside(candidate))
            return false;

        if (!grid.IsWalkable(candidate))
            return false;

        GameUnit unitAtCandidate = grid.GetUnitAt(candidate);
        if (unitAtCandidate != null && unitAtCandidate != enemy)
            return false;

        return true;
    }

    private bool TryFindBestFlankingTile(GameUnit enemy, GameUnit target, out Vector2Int bestTile)
    {
        bestTile = enemy.GridPosition;

        float bestScore = float.MaxValue;
        bool found = false;

        for (int i = 0; i < 4; i++)
        {
            Vector2Int candidate = GetNeighbourByIndex(target.GridPosition, i);

            if (!IsValidMoveCandidate(enemy, candidate))
                continue;

            AttackDirection attackDirection = GetAttackDirectionFromPosition(candidate, target);

            if (attackDirection == AttackDirection.Front)
                continue;

            int travelCost = GetPathTravelCost(enemy, candidate);
            if (travelCost == int.MaxValue)
                continue;

            float terrainScore = GetMovementTerrainScore(candidate);
            float directionScore = attackDirection == AttackDirection.Rear ? -2f : -1f;
            float score = travelCost - terrainScore + directionScore;

            if (score < bestScore)
            {
                bestScore = score;
                bestTile = candidate;
                found = true;
            }
        }

        return found;
    }

    private Vector2Int GetNeighbourByIndex(Vector2Int pos, int index)
    {
        switch (index)
        {
            case 0:
                return new Vector2Int(pos.x + 1, pos.y);
            case 1:
                return new Vector2Int(pos.x - 1, pos.y);
            case 2:
                return new Vector2Int(pos.x, pos.y + 1);
            default:
                return new Vector2Int(pos.x, pos.y - 1);
        }
    }

    private AttackDirection GetAttackDirectionFromPosition(Vector2Int attackerPosition, GameUnit defender)
    {
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
        if (!CanShootTargetFromPosition(shooter, shooterPosition, target))
            return ShotQuality.Blocked;

        BuildLinePoints(shooterPosition, target.GridPosition, lineBuffer);

        int alliesOnLine = 0;

        for (int i = 1; i < lineBuffer.Count - 1; i++)
        {
            GameUnit unitOnLine = grid.GetUnitAt(lineBuffer[i]);

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

    private int GetPathTravelCost(GameUnit mover, Vector2Int target)
    {
        if (mover == null || grid == null)
            return int.MaxValue;

        if (target == mover.GridPosition)
            return 0;

        if (cachedMover != mover)
            BeginUnitPathCache(mover);

        if (pathCostCache.TryGetValue(target, out int cachedCost))
            return cachedCost;

        if (pathfinder == null)
            pathfinder = new GridPathfinder(grid);

        int cost = pathfinder.FindPathCost(mover.GridPosition, target, mover);
        pathCostCache[target] = cost;
        return cost;
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
            score -= 1f;

        if (tile.TerrainType == TerrainType.RoughTerrain)
            score -= 0.25f;

        return score;
    }

    private Vector2Int MoveTowards(GameUnit mover, Vector2Int target)
    {
        int moveRange = grid.GetMovementBudgetForUnit(mover);
        Vector2Int best = mover.GridPosition;
        float bestScore = float.MaxValue;

        for (int x = -moveRange; x <= moveRange; x++)
        {
            for (int y = -moveRange; y <= moveRange; y++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > moveRange)
                    continue;

                Vector2Int candidate = new Vector2Int(mover.GridPosition.x + x, mover.GridPosition.y + y);

                if (!IsValidMoveCandidate(mover, candidate))
                    continue;

                int travelCost = GetPathTravelCost(mover, candidate);

                if (travelCost == int.MaxValue || travelCost > moveRange)
                    continue;

                float distToTarget = Vector2Int.Distance(candidate, target);
                float terrainScore = GetMovementTerrainScore(candidate);
                float score = distToTarget + travelCost * 0.25f - terrainScore;

                if (score < bestScore)
                {
                    bestScore = score;
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

    private void BuildLinePoints(Vector2Int start, Vector2Int end, List<Vector2Int> result)
    {
        result.Clear();

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
    }
}