using System.Collections.Generic;
using UnityEngine;

public class CommandPreviewRenderer : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;

    [Header("Colors")]
    [SerializeField] private Color marchColor = Color.blue;
    [SerializeField] private Color chargeColor = Color.red;
    [SerializeField] private Color shootColor = Color.yellow;
    [SerializeField] private Color retreatColor = Color.gray;

    [Header("Style")]
    [SerializeField] private float lineWidth = 0.12f;

    private readonly Dictionary<GameUnit, LineRenderer> lineMap = new Dictionary<GameUnit, LineRenderer>();

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
    }

    private void Update()
    {
        if (turnManager == null)
            return;

        if (!turnManager.IsPlanningPhase)
        {
            HideAllLines();
            return;
        }

        RenderAllUnitCommands();
    }

    private void RenderAllUnitCommands()
    {
        HashSet<GameUnit> usedThisFrame = new HashSet<GameUnit>();

        for (int u = 0; u < GameUnit.AllUnits.Count; u++)
        {
            GameUnit unit = GameUnit.AllUnits[u];
            if (unit == null) continue;

            if (!unit.ShowCommandPreview)
            {
                HideLine(unit);
                continue;
            }

            if (unit.PlannedCommands == null || unit.PlannedCommands.Count == 0)
            {
                HideLine(unit);
                continue;
            }

            usedThisFrame.Add(unit);
            RenderUnitCommands(unit);
        }

        List<GameUnit> keys = new List<GameUnit>(lineMap.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            GameUnit unit = keys[i];
            if (unit == null || !usedThisFrame.Contains(unit))
                HideLine(unit);
        }
    }

    private void RenderUnitCommands(GameUnit unit)
    {
        LineRenderer line = GetOrCreateLine(unit);

        List<Vector3> points = new List<Vector3>();
        Vector3 current = new Vector3(unit.GridPosition.x, unit.GridPosition.y, -0.2f);
        points.Add(current);

        Color color = GetOrderMoveColor(unit.CurrentOrder);

        for (int i = 0; i < unit.PlannedCommands.Count; i++)
        {
            PlannedCommand cmd = unit.PlannedCommands[i];
            if (cmd == null) continue;

            switch (cmd.commandType)
            {
                case PlannedCommandType.Move:
                {
                    current = new Vector3(
                        cmd.targetGridPosition.x,
                        cmd.targetGridPosition.y,
                        -0.2f
                    );

                    color = GetOrderMoveColor(unit.CurrentOrder);
                    points.Add(current);
                    break;
                }

                case PlannedCommandType.AttackMelee:
                {
                    if (cmd.targetUnit != null)
                    {
                        current = new Vector3(
                            cmd.targetUnit.GridPosition.x,
                            cmd.targetUnit.GridPosition.y,
                            -0.2f
                        );

                        color = chargeColor;
                        points.Add(current);
                    }
                    break;
                }

                case PlannedCommandType.AttackShoot:
                {
                    if (cmd.targetUnit != null)
                    {
                        current = new Vector3(
                            cmd.targetUnit.GridPosition.x,
                            cmd.targetUnit.GridPosition.y,
                            -0.2f
                        );

                        color = shootColor;
                        points.Add(current);
                    }
                    break;
                }
            }
        }

        line.enabled = true;
        line.positionCount = points.Count;

        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);

        line.startColor = color;
        line.endColor = color;
    }

    private Color GetOrderMoveColor(OrderType order)
    {
        switch (order)
        {
            case OrderType.Charge:
                return chargeColor;

            case OrderType.Retreat:
                return retreatColor;

            case OrderType.Shoot:
                return shootColor;

            default:
                return marchColor;
        }
    }

    private LineRenderer GetOrCreateLine(GameUnit unit)
    {
        if (lineMap.TryGetValue(unit, out LineRenderer existing) && existing != null)
            return existing;

        GameObject go = new GameObject($"PreviewLine_{unit.name}");
        go.transform.SetParent(transform);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.positionCount = 0;
        line.enabled = true;
        line.material = new Material(Shader.Find("Sprites/Default"));

        lineMap[unit] = line;
        return line;
    }

    private void HideLine(GameUnit unit)
    {
        if (unit == null) return;

        if (lineMap.TryGetValue(unit, out LineRenderer line) && line != null)
        {
            line.positionCount = 0;
            line.enabled = false;
        }
    }

    private void HideAllLines()
    {
        foreach (var kv in lineMap)
        {
            if (kv.Value != null)
            {
                kv.Value.positionCount = 0;
                kv.Value.enabled = false;
            }
        }
    }
}