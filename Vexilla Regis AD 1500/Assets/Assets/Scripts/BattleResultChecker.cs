using UnityEngine;

public class BattleResultChecker : MonoBehaviour
{
    private bool battleEnded = false;

    public bool HasBattleEnded => battleEnded;

    public void CheckBattleResult()
    {
        if (battleEnded) return;

        bool playerAlive = false;
        bool enemyAlive = false;

        foreach (GameUnit unit in GameUnit.AllUnits)
        {
            if (unit == null) continue;
            if (unit.IsDead) continue;

            if (unit.TeamId == 0)
                playerAlive = true;
            else
                enemyAlive = true;
        }

        if (!playerAlive)
        {
            battleEnded = true;
            Debug.Log("Porażka");

            TurnManager turnManager = FindFirstObjectByType<TurnManager>();
            if (turnManager != null)
                turnManager.EndBattle(false);
        }
        else if (!enemyAlive)
        {
            battleEnded = true;
            Debug.Log("Zwycięstwo");

            TurnManager turnManager = FindFirstObjectByType<TurnManager>();
            if (turnManager != null)
                turnManager.EndBattle(true);
        }
    }
}