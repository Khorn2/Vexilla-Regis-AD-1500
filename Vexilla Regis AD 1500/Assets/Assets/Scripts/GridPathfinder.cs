using System.Collections.Generic;
using UnityEngine;

public class GridPathfinder
{
    private readonly GridManager grid;

    private readonly Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>(512);
    private readonly List<Node> open = new List<Node>(128);
    private readonly HashSet<Vector2Int> openPositions = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> resultPath = new List<Vector2Int>(128);

    public GridPathfinder(GridManager grid)
    {
        this.grid = grid;
    }

    private class Node
    {
        public Vector2Int pos;
        public int gCost;
        public int hCost;
        public int fCost;
        public Node parent;

        public Node(Vector2Int pos)
        {
            this.pos = pos;
        }

        public void Reset()
        {
            gCost = int.MaxValue;
            hCost = 0;
            fCost = int.MaxValue;
            parent = null;
        }
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, GameUnit unit)
    {
        if (!CanSearch(start, target, unit))
            return null;

        Node endNode = Search(start, target, unit);

        if (endNode == null)
            return null;

        return RetracePath(endNode);
    }

    public int FindPathCost(Vector2Int start, Vector2Int target, GameUnit unit)
    {
        if (start == target)
            return 0;

        if (!CanSearch(start, target, unit))
            return int.MaxValue;

        Node endNode = Search(start, target, unit);

        if (endNode == null)
            return int.MaxValue;

        return endNode.gCost;
    }

    private bool CanSearch(Vector2Int start, Vector2Int target, GameUnit unit)
    {
        if (grid == null || unit == null)
            return false;

        if (!grid.IsInside(start) || !grid.IsInside(target))
            return false;

        if (!grid.IsWalkable(target))
            return false;

        GameUnit unitAtTarget = grid.GetUnitAt(target);
        if (unitAtTarget != null && unitAtTarget != unit)
            return false;

        return true;
    }

    private Node Search(Vector2Int start, Vector2Int target, GameUnit unit)
    {
        ClearSearchState();

        Node startNode = GetNode(start);
        startNode.gCost = 0;
        startNode.hCost = GetDistance(start, target);
        startNode.fCost = startNode.hCost;

        open.Add(startNode);
        openPositions.Add(start);

        while (open.Count > 0)
        {
            Node current = PopBestOpenNode();

            openPositions.Remove(current.pos);
            closed.Add(current.pos);

            if (current.pos == target)
                return current;

            for (int i = 0; i < 4; i++)
            {
                Vector2Int neighbourPos = GetNeighbourByIndex(current.pos, i);

                if (!grid.IsInside(neighbourPos))
                    continue;

                if (closed.Contains(neighbourPos))
                    continue;

                if (!grid.IsWalkable(neighbourPos))
                    continue;

                GameUnit other = grid.GetUnitAt(neighbourPos);
                if (other != null && other != unit)
                    continue;

                int stepCost = grid.GetMovementCost(current.pos, neighbourPos, unit);
                if (stepCost == int.MaxValue)
                    continue;

                int moveCost = current.gCost + stepCost;

                Node neighbour = GetNode(neighbourPos);

                if (moveCost >= neighbour.gCost)
                    continue;

                neighbour.gCost = moveCost;
                neighbour.hCost = GetDistance(neighbourPos, target);
                neighbour.fCost = neighbour.gCost + neighbour.hCost;
                neighbour.parent = current;

                if (!openPositions.Contains(neighbourPos))
                {
                    open.Add(neighbour);
                    openPositions.Add(neighbourPos);
                }
            }
        }

        return null;
    }

    private void ClearSearchState()
    {
        foreach (KeyValuePair<Vector2Int, Node> pair in allNodes)
            pair.Value.Reset();

        open.Clear();
        openPositions.Clear();
        closed.Clear();
        resultPath.Clear();
    }

    private Node GetNode(Vector2Int pos)
    {
        if (allNodes.TryGetValue(pos, out Node node))
            return node;

        node = new Node(pos);
        node.Reset();
        allNodes[pos] = node;
        return node;
    }

    private Node PopBestOpenNode()
    {
        int bestIndex = 0;
        Node best = open[0];

        for (int i = 1; i < open.Count; i++)
        {
            Node candidate = open[i];

            if (candidate.fCost < best.fCost ||
                candidate.fCost == best.fCost && candidate.hCost < best.hCost)
            {
                best = candidate;
                bestIndex = i;
            }
        }

        int lastIndex = open.Count - 1;
        open[bestIndex] = open[lastIndex];
        open.RemoveAt(lastIndex);

        return best;
    }

    private List<Vector2Int> RetracePath(Node end)
    {
        resultPath.Clear();

        Node current = end;

        while (current.parent != null)
        {
            resultPath.Add(current.pos);
            current = current.parent;
        }

        resultPath.Reverse();

        return new List<Vector2Int>(resultPath);
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

    private int GetDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}