using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AStarPathfinding : MonoBehaviour
{
    private static readonly Vector3Int[] directions = {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    public class Node
    {
        public Vector3Int Position;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public Node Parent;

        public Node(Vector3Int position)
        {
            Position = position;
            GCost = int.MaxValue;
            HCost = 0;
            Parent = null;
        }
    }

    public static List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, Tilemap floorTilemap, Tilemap wallTilemap)
    {
        List<Node> openList = new List<Node>();
        Dictionary<Vector3Int, Node> allNodes = new Dictionary<Vector3Int, Node>();
        HashSet<Vector3Int> closedList = new HashSet<Vector3Int>();

        Node startNode = new Node(start);
        startNode.GCost = 0;
        startNode.HCost = CalculateHeuristic(start, end);
        openList.Add(startNode);
        allNodes[start] = startNode;

        while (openList.Count > 0)
        {
            // Находим узел с наименьшим FCost (без сортировки)
            Node currentNode = openList[0];
            foreach (Node node in openList)
            {
                if (node.FCost < currentNode.FCost || (node.FCost == currentNode.FCost && node.HCost < currentNode.HCost))
                {
                    currentNode = node;
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.Position);

            // Если мы достигли цели — восстановить путь
            if (currentNode.Position == end)
            {
                return ReconstructPath(currentNode);
            }

            foreach (Vector3Int direction in directions)
            {
                Vector3Int neighborPosition = currentNode.Position + direction;

                // Пропускаем закрытые клетки
                if (closedList.Contains(neighborPosition) || !IsInsideMaze(neighborPosition, wallTilemap))
                    continue;

                int movementCost = GetMovementCost(neighborPosition, wallTilemap);
                if (movementCost == int.MaxValue) // Непроходимая стена
                    continue;

                int gCost = currentNode.GCost + movementCost;

                if (!allNodes.ContainsKey(neighborPosition))
                {
                    Node neighborNode = new Node(neighborPosition)
                    {
                        GCost = gCost,
                        HCost = CalculateHeuristic(neighborPosition, end),
                        Parent = currentNode
                    };

                    allNodes[neighborPosition] = neighborNode;
                    openList.Add(neighborNode);
                }
                else if (gCost < allNodes[neighborPosition].GCost)
                {
                    Node neighborNode = allNodes[neighborPosition];
                    neighborNode.GCost = gCost;
                    neighborNode.Parent = currentNode;
                }
            }
        }

        return null; // Путь не найден
    }

    private static List<Vector3Int> ReconstructPath(Node node)
    {
        List<Vector3Int> path = new List<Vector3Int>();
        while (node.Parent != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    private static int CalculateHeuristic(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static bool IsInsideMaze(Vector3Int position, Tilemap wallTilemap)
    {
        BoundsInt bounds = wallTilemap.cellBounds;
        return position.x >= bounds.xMin && position.x < bounds.xMax &&
               position.y >= bounds.yMin && position.y < bounds.yMax;
    }

    private static int GetMovementCost(Vector3Int position, Tilemap wallTilemap)
    {
        return wallTilemap.GetTile(position) != null ? 100 : 0; // Стены непроходимые
    }
}
