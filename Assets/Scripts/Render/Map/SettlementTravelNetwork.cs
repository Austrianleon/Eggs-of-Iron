using System.Collections.Generic;
using UnityEngine;

namespace EggsOfIron.Map
{
    internal sealed class SettlementTravelNetwork
    {
        readonly List<Vector2> positions = new List<Vector2>();
        readonly List<List<(int node, float length)>> edges = new List<List<(int node, float length)>>();
        readonly Dictionary<int, int> settlementNodes = new Dictionary<int, int>();

        public SettlementTravelNetwork(WorldMapData map)
        {
            var waypointNodes = new Dictionary<(int cell, Vector2 position), int>();
            var waypointsByCell = new Dictionary<int, List<int>>();

            void AddPath(Vector2[] points, int[] pathCells)
            {
                int previous = -1;
                for (int i = 0; i < points.Length; i++)
                {
                    int cell = i < pathCells.Length ? pathCells[i] : -1;
                    if (!map.CellIndex.ContainsKey(cell))
                        cell = map.Pick(points[i])?.Id ?? -1;

                    var key = (cell, points[i]);
                    if (!waypointNodes.TryGetValue(key, out int node))
                    {
                        node = AddNode(points[i]);
                        waypointNodes.Add(key, node);
                        if (cell >= 0)
                        {
                            if (!waypointsByCell.TryGetValue(cell, out var local))
                                waypointsByCell.Add(cell, local = new List<int>());
                            // Join paths that pass through the same map cell.
                            foreach (int other in local) Connect(node, other);
                            local.Add(node);
                        }
                    }
                    if (previous >= 0 && previous != node) Connect(previous, node);
                    previous = node;
                }
            }

            foreach (var route in map.Routes) AddPath(route.Points, route.PathCells);
            foreach (var river in map.Rivers) AddPath(river.Points, river.PathCells);
            foreach (var settlement in map.Settlements)
            {
                int node = AddNode(settlement.Position);
                settlementNodes.Add(settlement.Id, node);
                if (waypointsByCell.TryGetValue(settlement.CellId, out var local))
                    foreach (int waypoint in local) Connect(node, waypoint);
            }
        }

        int AddNode(Vector2 position)
        {
            int node = positions.Count;
            positions.Add(position);
            edges.Add(new List<(int node, float length)>());
            return node;
        }

        void Connect(int first, int second)
        {
            float length = Vector2.Distance(positions[first], positions[second]);
            edges[first].Add((second, length));
            edges[second].Add((first, length));
        }

        public float Distance(int firstId, int secondId)
        {
            int start = settlementNodes[firstId], destination = settlementNodes[secondId];
            var distances = new float[edges.Count];
            for (int i = 0; i < distances.Length; i++) distances[i] = float.PositiveInfinity;
            distances[start] = 0;
            var queue = new SortedSet<(float distance, int node)> { (0, start) };
            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                if (current.distance > distances[current.node]) continue;
                if (current.node == destination) return current.distance;

                foreach (var edge in edges[current.node])
                {
                    float distance = current.distance + edge.length;
                    if (distance >= distances[edge.node]) continue;
                    distances[edge.node] = distance;
                    queue.Add((distance, edge.node));
                }
            }
            return float.PositiveInfinity;
        }
    }
}
