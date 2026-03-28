using System.Collections.Generic;
using System;


/// <summary>
/// Simple min-heap priority queue. Lower priority value = higher priority (dequeued first).
/// </summary>
public class PriorityQueue<T>
{
    private List<(T item, int priority)> heap = new List<(T, int)>();

    public int Count => heap.Count;

    public void Enqueue(T item, int priority)
    {
        heap.Add((item, priority));
        HeapifyUp(heap.Count - 1);
    }

    public T Dequeue()
    {
        if (heap.Count == 0) throw new InvalidOperationException("PriorityQueue is empty.");
        T result = heap[0].item;
        if (heap.Count == 1)
        {
            heap.Clear();
            return result;
        }

        heap[0] = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);
        HeapifyDown(0);
        return result;
    }

    public T Peek()
    {
        if (heap.Count == 0) throw new InvalidOperationException("PriorityQueue is empty.");
        return heap[0].item;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (heap[index].priority >= heap[parent].priority) break;
            Swap(index, parent);
            index = parent;
        }
    }

    private void HeapifyDown(int index)
    {
        int last = heap.Count - 1;
        while (true)
        {
            int left = (index << 1) + 1;
            int right = left + 1;
            int smallest = index;

            if (left <= last && heap[left].priority < heap[smallest].priority)
                smallest = left;
            if (right <= last && heap[right].priority < heap[smallest].priority)
                smallest = right;

            if (smallest == index) break;
            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int i, int j)
    {
        var tmp = heap[i];
        heap[i] = heap[j];
        heap[j] = tmp;
    }
}

public class AStar
{
    private readonly MapGrid map;

    private static readonly (int x, int y)[] Neighbors =
    {
    (1,0), (-1,0), (0,1), (0,-1)
};

    public AStar(MapGrid map)
    {
        this.map = map;
    }

    public List<(int x, int y)> FindPath(int startX, int startY, int goalX, int goalY)
    {
        var open = new PriorityQueue<Node>();
        var nodes = new Dictionary<(int, int), Node>();

        Node startNode = new Node(startX, startY, cost: 0,
            priority: Heuristic(startX, startY, goalX, goalY));

        open.Enqueue(startNode, startNode.Priority);
        nodes[(startX, startY)] = startNode;

        while (open.Count > 0)
        {
            Node current = open.Dequeue();

            // Goal reached
            if (current.X == goalX && current.Y == goalY)
                return ReconstructPath(current);

            foreach (var (dx, dy) in Neighbors)
            {
                int nx = current.X + dx;
                int ny = current.Y + dy;

                if (!map.IsWalkable(nx, ny))
                    continue;

                int newCost = current.Cost + 1;

                if (!nodes.TryGetValue((nx, ny), out Node neighbor) || newCost < neighbor.Cost)
                {
                    neighbor = new Node(nx, ny, newCost,
                        newCost + Heuristic(nx, ny, goalX, goalY), current);

                    nodes[(nx, ny)] = neighbor;
                    open.Enqueue(neighbor, neighbor.Priority);
                }
            }
        }

        // no path
        return null;
    }

    private int Heuristic(int x1, int y1, int x2, int y2)
        => Math.Abs(x1 - x2) + Math.Abs(y1 - y2); // Manhattan

    private List<(int x, int y)> ReconstructPath(Node goal)
    {
        var path = new List<(int x, int y)>();
        Node current = goal;

        while (current != null)
        {
            path.Add((current.X, current.Y));
            current = current.Parent;
        }

        path.Reverse();
        return path;
    }

    private class Node
    {
        public int X, Y;
        public int Cost;
        public int Priority;
        public Node Parent;

        public Node(int x, int y, int cost, int priority, Node parent = null)
        {
            X = x; Y = y; Cost = cost; Priority = priority; Parent = parent;
        }
    }
}