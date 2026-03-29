using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [SerializeField] private EnemyAIManager enemyAI;

    public int TurnNumber { get; private set; } = 0;
    public bool IsPlanningPhase { get; private set; } = true;
    public bool IsBattleEnded { get; private set; } = false;
    public bool PlayerWon { get; private set; } = false;

    private bool executingTurn = false;

    private void Awake()
    {
        if (enemyAI == null)
            enemyAI = FindObjectOfType<EnemyAIManager>();
    }

    private void Start()
    {
        Debug.Log("Deployment / Planning Phase (Turn 0)");
    }

    private void Update()
    {
        if (IsBattleEnded)
            return;

        if (!IsPlanningPhase)
            return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            StartCoroutine(ExecuteTurn());
        }
    }

    public void EndBattle(bool playerWon)
    {
        if (IsBattleEnded)
            return;

        IsBattleEnded = true;
        PlayerWon = playerWon;
        IsPlanningPhase = false;
        executingTurn = false;
    }

    private IEnumerator ExecuteTurn()
    {
        if (executingTurn || IsBattleEnded)
            yield break;

        executingTurn = true;
        IsPlanningPhase = false;

        Debug.Log($"=== EXECUTING TURN {TurnNumber} ===");

        if (enemyAI != null && !IsBattleEnded)
            enemyAI.PlanEnemyTurn();

        List<GameUnit> units = new List<GameUnit>(GameUnit.AllUnits);
        List<Coroutine> running = new List<Coroutine>();

        for (int i = 0; i < units.Count; i++)
        {
            if (IsBattleEnded)
                yield break;

            GameUnit unit = units[i];
            if (unit == null) continue;
            if (unit.IsDead) continue;

            Coroutine c = StartCoroutine(unit.ExecutePlannedAction());
            running.Add(c);
        }

        bool anyMoving = true;

        while (anyMoving)
        {
            if (IsBattleEnded)
                yield break;

            anyMoving = false;

            for (int i = 0; i < GameUnit.AllUnits.Count; i++)
            {
                GameUnit unit = GameUnit.AllUnits[i];
                if (unit == null) continue;
                if (unit.IsDead) continue;

                if (unit.IsMoving)
                {
                    anyMoving = true;
                    break;
                }
            }

            yield return null;
        }

        if (IsBattleEnded)
            yield break;

        TurnNumber++;

        Debug.Log($"=== TURN {TurnNumber} START ===");

        IsPlanningPhase = true;
        executingTurn = false;
    }

    private void OnDisable()
    {
        executingTurn = false;
    }
}