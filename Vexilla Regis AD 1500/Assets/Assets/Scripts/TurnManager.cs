using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    [SerializeField] private EnemyAIManager enemyAI;
    [SerializeField] private DeploymentManager deploymentManager;

    [Header("Turn Limit")]
    [SerializeField] private bool useTurnLimit = false;
    [SerializeField, Min(1)] private int maxTurns = 40;
    [SerializeField] private bool drawOnTurnLimit = true;

    public int TurnNumber { get; private set; } = 0;
    public bool IsPlanningPhase { get; private set; } = true;
    public bool IsBattleEnded { get; private set; } = false;
    public bool PlayerWon { get; private set; } = false;
    public bool BattleDraw { get; private set; } = false;

    public bool UseTurnLimit => useTurnLimit;
    public int MaxTurns => maxTurns;
    public bool DrawOnTurnLimit => drawOnTurnLimit;

    public event Action OnTurnStateChanged;
    public event Action<bool> OnBattleEnded;

    private bool executingTurn = false;

    private readonly List<GameUnit> turnStartUnits = new List<GameUnit>(128);
    private readonly List<GameUnit> executionUnits = new List<GameUnit>(128);
    private readonly List<GameUnit> autoMeleeUnits = new List<GameUnit>(128);
    private readonly List<GameUnit> endTurnUnits = new List<GameUnit>(128);

    private void Awake()
    {
        if (enemyAI == null)
            enemyAI = FindObjectOfType<EnemyAIManager>();

        if (deploymentManager == null)
            deploymentManager = FindObjectOfType<DeploymentManager>();
    }

    private void Start()
    {
        Debug.Log("Deployment / Planning Phase (Turn 0)");
        OnTurnStateChanged?.Invoke();
    }

    private void Update()
    {
        if (IsBattleEnded)
            return;

        if (!IsPlanningPhase)
            return;

        if (IsKeyDown("EndTurn", KeyCode.Return))
            RequestEndTurn();
    }

    private bool IsKeyDown(string actionId, KeyCode fallback)
    {
        KeyCode key = GetConfiguredKey(actionId, fallback);
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private KeyCode GetConfiguredKey(string actionId, KeyCode fallback)
    {
        if (GameSettingsManager.Instance != null)
            return GameSettingsManager.Instance.GetKey(actionId);

        return fallback;
    }

    public void RequestEndTurn()
    {
        if (IsBattleEnded)
            return;

        if (!IsPlanningPhase)
            return;

        if (executingTurn)
            return;

        if (deploymentManager != null && deploymentManager.DeploymentActive)
        {
            deploymentManager.FinishDeployment();
            OnTurnStateChanged?.Invoke();
            return;
        }

        StartCoroutine(ExecuteTurn());
    }

    public void ConfigureTurnLimit(bool useLimit, int limit, bool drawWhenLimitReached)
    {
        useTurnLimit = useLimit;
        maxTurns = Mathf.Max(1, limit);
        drawOnTurnLimit = drawWhenLimitReached;

        OnTurnStateChanged?.Invoke();
    }

    public void EndBattle(bool playerWon)
    {
        if (IsBattleEnded)
            return;

        IsBattleEnded = true;
        PlayerWon = playerWon;
        BattleDraw = false;
        IsPlanningPhase = false;
        executingTurn = false;

        OnBattleEnded?.Invoke(playerWon);
        OnTurnStateChanged?.Invoke();
    }

    public void EndBattleDraw()
    {
        if (IsBattleEnded)
            return;

        IsBattleEnded = true;
        PlayerWon = false;
        BattleDraw = true;
        IsPlanningPhase = false;
        executingTurn = false;

        Debug.Log("Remis");

        OnBattleEnded?.Invoke(false);
        OnTurnStateChanged?.Invoke();
    }

    private IEnumerator ExecuteTurn()
    {
        if (executingTurn || IsBattleEnded)
            yield break;

        executingTurn = true;
        IsPlanningPhase = false;
        OnTurnStateChanged?.Invoke();

        Debug.Log($"=== EXECUTING TURN {TurnNumber} ===");

        CopyAliveUnits(turnStartUnits);

        for (int i = 0; i < turnStartUnits.Count; i++)
        {
            GameUnit unit = turnStartUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            unit.BeginTurnExecution();
        }

        if (enemyAI != null && !IsBattleEnded)
            enemyAI.PlanEnemyTurn();

        CopyAliveUnits(executionUnits);

        for (int i = 0; i < executionUnits.Count; i++)
        {
            if (IsBattleEnded)
                yield break;

            GameUnit unit = executionUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            StartCoroutine(unit.ExecutePlannedAction());
        }

        while (AnyUnitMoving())
        {
            if (IsBattleEnded)
                yield break;

            yield return null;
        }

        if (IsBattleEnded)
            yield break;

        ResolveAutoMeleePhase();

        if (IsBattleEnded)
            yield break;

        CopyAliveUnits(endTurnUnits);

        for (int i = 0; i < endTurnUnits.Count; i++)
        {
            GameUnit unit = endTurnUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            unit.ResolveTurnEnd();
        }

        if (IsBattleEnded)
            yield break;

        TurnNumber++;

        if (useTurnLimit && TurnNumber >= maxTurns)
        {
            if (drawOnTurnLimit)
                EndBattleDraw();
            else
                EndBattle(false);

            yield break;
        }

        Debug.Log($"=== TURN {TurnNumber} START ===");

        IsPlanningPhase = true;
        executingTurn = false;

        OnTurnStateChanged?.Invoke();
    }

    private void CopyAliveUnits(List<GameUnit> target)
    {
        target.Clear();

        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            target.Add(unit);
        }
    }

    private bool AnyUnitMoving()
    {
        for (int i = 0; i < GameUnit.AllUnits.Count; i++)
        {
            GameUnit unit = GameUnit.AllUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            if (unit.IsMoving)
                return true;
        }

        return false;
    }

    private void ResolveAutoMeleePhase()
    {
        CopyAliveUnits(autoMeleeUnits);

        for (int i = 0; i < autoMeleeUnits.Count; i++)
        {
            if (IsBattleEnded)
                return;

            GameUnit unit = autoMeleeUnits[i];

            if (unit == null) continue;
            if (unit.IsDead) continue;

            unit.ResolveAutoMeleeCombat();
        }
    }

    public void SurrenderBattle()
    {
        if (IsBattleEnded)
            return;

        EndBattle(false);
    }

    private void OnDisable()
    {
        executingTurn = false;
    }
}