using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [SerializeField] private EnemyAIManager enemyAI;

    public int TurnNumber { get; private set; } = 0;
    public bool IsPlanningPhase { get; private set; } = true;

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
        if (!IsPlanningPhase)
            return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            StartCoroutine(ExecuteTurn());
        }
    }

    private IEnumerator ExecuteTurn()
    {
        if (executingTurn)
            yield break;

        executingTurn = true;
        IsPlanningPhase = false;

        Debug.Log($"=== EXECUTING TURN {TurnNumber} ===");

        if (enemyAI != null)
            enemyAI.PlanEnemyTurn();

        List<GameUnit> units = new List<GameUnit>(GameUnit.AllUnits);
        List<Coroutine> running = new List<Coroutine>();

        for (int i = 0; i < units.Count; i++)
        {
            GameUnit unit = units[i];
            if (unit == null) continue;

            Coroutine c = StartCoroutine(unit.ExecutePlannedAction());
            running.Add(c);
        }

        // czekaj aż wszystkie skończą
        bool anyMoving = true;

        while (anyMoving)
        {
            anyMoving = false;

            for (int i = 0; i < GameUnit.AllUnits.Count; i++)
            {
                GameUnit unit = GameUnit.AllUnits[i];
                if (unit == null) continue;

                if (unit.IsMoving)
                {
                    anyMoving = true;
                    break;
                }
            }

            yield return null;
        }

        TurnNumber++;

        Debug.Log($"=== TURN {TurnNumber} START ===");

        IsPlanningPhase = true;
        executingTurn = false;
    }
}