using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public int TurnNumber { get; private set; } = 0;

    public bool IsPlanningPhase { get; private set; } = true;

    private bool executingTurn = false;

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

        List<GameUnit> units = new List<GameUnit>(GameUnit.AllUnits);

        for (int i = 0; i < units.Count; i++)
        {
            GameUnit unit = units[i];

            if (unit == null)
                continue;

            yield return unit.StartCoroutine(unit.ExecutePlannedAction());
        }

        // wyczyść plany po wykonaniu
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
                units[i].ClearPlannedAction();
        }

        TurnNumber++;

        Debug.Log($"=== TURN {TurnNumber} START ===");

        IsPlanningPhase = true;
        executingTurn = false;
    }
}