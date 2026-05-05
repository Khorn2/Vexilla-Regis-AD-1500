using System.Collections.Generic;
using UnityEngine;

public class GridPathfinder
{
    private GridManager grid;

    public GridPathfinder(GridManager grid)
    {
        this.grid = grid;
    }

    private class Node
    {
        public Vector2Int pos;
        public int gCost = int.MaxValue;
        public int hCost;
        public int fCost => gCost + hCost;
        public Node parent;

        public Node(Vector2Int pos)
        {
            this.pos = pos;
        }
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int target, GameUnit unit)
    {
        Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();
        List<Node> open = new List<Node>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

        Node startNode = new Node(start);
        startNode.gCost = 0;
        startNode.hCost = GetDistance(start, target);

        open.Add(startNode);
        allNodes[start] = startNode;

        while (open.Count > 0)
        {
            Node current = open[0];

            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].fCost < current.fCost ||
                   (open[i].fCost == current.fCost && open[i].hCost < current.hCost))
                {
                    current = open[i];
                }
            }

            open.Remove(current);
            closed.Add(current.pos);

            if (current.pos == target)
                return RetracePath(current);

            foreach (Vector2Int neighbourPos in grid.GetNeighbours4(current.pos))
            {
                if (!grid.IsInside(neighbourPos))
                    continue;

                if (closed.Contains(neighbourPos))
                    continue;

                GameUnit other = grid.GetUnitAt(neighbourPos);
                if (other != null && other != unit)
                    continue;

                int moveCost = current.gCost + grid.GetMovementCost(current.pos, neighbourPos, unit);

                if (!allNodes.TryGetValue(neighbourPos, out Node neighbour))
                {
                    neighbour = new Node(neighbourPos);
                    allNodes[neighbourPos] = neighbour;
                }

                if (moveCost < neighbour.gCost)
                {
                    neighbour.gCost = moveCost;
                    neighbour.hCost = GetDistance(neighbourPos, target);
                    neighbour.parent = current;

                    if (!open.Contains(neighbour))
                        open.Add(neighbour);
                }
            }
        }

        return null;
    }

    private List<Vector2Int> RetracePath(Node end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Node current = end;

        while (current.parent != null)
        {
            path.Add(current.pos);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private int GetDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}