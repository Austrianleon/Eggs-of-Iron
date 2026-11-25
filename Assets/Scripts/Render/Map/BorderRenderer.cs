using Render.Common;
using System.Collections.Generic;
using UnityEngine;

namespace EggsOfIron.Map
{
    [System.Serializable]
    public sealed class BorderSettings
    {
        [Min(.01f)] public float KingdomWidth = 1.25f;
        [Min(.01f)] public float ProvinceWidth = .16f;
        [Min(.01f)] public float CoastWidth = .3f;
        public Color KingdomColor = new Color(.68f, .53f, .32f, 1);
        public Color ProvinceColor = new Color(.58f, .51f, .38f, .4f);
        public Color CoastColor = Color.black;
    }
    internal sealed class BorderRenderer : System.IDisposable
    {
        readonly MapMeshLayer coast, borders, kingdomCasing, kingdoms;
        readonly WorldMapData map;
        readonly IMapPolitics state;
        readonly BorderSettings settings;
        readonly HashSet<int> coastVertices = new HashSet<int>();
        public BorderRenderer(Transform parent, Material material, WorldMapData map, IMapPolitics state, BorderSettings settings)
        {
            this.map = map; this.state = state; this.settings = settings;
            coast = new MapMeshLayer(parent, "Coastlines", material, 22); borders = new MapMeshLayer(parent, "Political borders", material, 23);
            kingdomCasing = new MapMeshLayer(parent, "Kingdom border casing", material, 24);
            kingdoms = new MapMeshLayer(parent, "Kingdom borders", material, 25);
            foreach (var edge in map.Edges)
            {
                if (!map.IsCoast(edge)) continue;
                coast.Segment(map.Vertices[edge.A], map.Vertices[edge.B], settings.CoastWidth, settings.CoastColor);
                coastVertices.Add(edge.A);
                coastVertices.Add(edge.B);
            }
            // Fill the outside corner where two flat-ended coast segments turn.
            foreach (int vertex in coastVertices)
                coast.Disc(map.Vertices[vertex], settings.CoastWidth * .5f, settings.CoastColor, sides: 32);
            coast.Flush(); Refresh();
        }
        public void Refresh()
        {
            borders.Clear(); kingdomCasing.Clear(); kingdoms.Clear();
            var stateEdges = new List<MapEdge>();
            var neighbors = new Dictionary<int, List<int>>();
            foreach (var edge in map.Edges)
            {
                if (edge.Right < 0) continue;
                var a = map.Cells[edge.Left];
                var b = map.Cells[edge.Right];
                if (!a.Land || !b.Land) continue;
                if (state.Owner(a) != state.Owner(b))
                {
                    stateEdges.Add(edge);
                    AddNeighbor(neighbors, edge.A, edge.B);
                    AddNeighbor(neighbors, edge.B, edge.A);
                }
                else if (a.Province != b.Province)
                    borders.Segment(map.Vertices[edge.A], map.Vertices[edge.B], settings.ProvinceWidth, settings.ProvinceColor);
            }

            foreach (var edge in stateEdges)
                DrawStateSegment(TrimmedPoint(edge.A, edge.B, neighbors), TrimmedPoint(edge.B, edge.A, neighbors));

            foreach (var entry in neighbors)
            {
                int vertex = entry.Key;
                var adjacent = entry.Value;
                if (adjacent.Count != 2 || coastVertices.Contains(vertex)) continue;

                Vector2 corner = map.Vertices[vertex];
                Vector2 start = TrimmedPoint(vertex, adjacent[0], neighbors);
                Vector2 end = TrimmedPoint(vertex, adjacent[1], neighbors);
                Vector2 previous = start;
                const int steps = 8;
                for (int i = 1; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    Vector2 point = Vector2.Lerp(Vector2.Lerp(start, corner, t), Vector2.Lerp(corner, end, t), t);
                    DrawStateSegment(previous, point);
                    previous = point;
                }
            }
            borders.Flush(); kingdomCasing.Flush(); kingdoms.Flush();
        }
        static void AddNeighbor(Dictionary<int, List<int>> neighbors, int vertex, int adjacent)
        {
            if (!neighbors.TryGetValue(vertex, out var connected))
                neighbors[vertex] = connected = new List<int>(3);
            connected.Add(adjacent);
        }

        Vector2 TrimmedPoint(int vertex, int adjacent, Dictionary<int, List<int>> neighbors)
        {
            Vector2 point = map.Vertices[vertex];
            if (neighbors[vertex].Count != 2 || coastVertices.Contains(vertex)) return point;
            // Keep the curve close enough for the casing to cover the political fill boundary.
            Vector2 target = map.Vertices[adjacent];
            float trim = Mathf.Min(settings.KingdomWidth * .65f, Vector2.Distance(point, target) * .35f);
            return Vector2.MoveTowards(point, target, trim);
        }

        void DrawStateSegment(Vector2 a, Vector2 b)
        {
            float casingWidth = settings.KingdomWidth * 1.65f;
            var ink = new Color(.045f, .035f, .025f, 1);
            DrawRoundedSegment(kingdomCasing, a, b, casingWidth, ink);
            DrawRoundedSegment(kingdoms, a, b, settings.KingdomWidth, settings.KingdomColor);
        }

        static void DrawRoundedSegment(MapMeshLayer layer, Vector2 a, Vector2 b, float width, Color color)
        {
            layer.Segment(a, b, width, color);
            layer.Disc(a, width * .5f, color, sides: 24);
            layer.Disc(b, width * .5f, color, sides: 24);
        }

        public void Dispose() { coast.Clear(); borders.Clear(); kingdomCasing.Clear(); kingdoms.Clear(); }
    }
}
