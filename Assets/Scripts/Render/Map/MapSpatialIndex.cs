using System.Collections.Generic;
using UnityEngine;

namespace EggsOfIron.Map
{
    public sealed class MapSpatialIndex
    {
        readonly Dictionary<Vector2Int, List<int>> buckets = new Dictionary<Vector2Int, List<int>>();
        readonly WorldMapData map;
        readonly float size;
        public MapSpatialIndex(WorldMapData map)
        {
            this.map = map; size = Mathf.Max(1, Mathf.Sqrt(map.Width * map.Height / map.Cells.Count) * 2);
            for (int i = 0; i < map.Cells.Count; i++)
            {
                Rect b = map.Cells[i].Bounds;
                for (int y = Mathf.FloorToInt(b.yMin / size); y <= Mathf.FloorToInt(b.yMax / size); y++)
                for (int x = Mathf.FloorToInt(b.xMin / size); x <= Mathf.FloorToInt(b.xMax / size); x++)
                {
                    var key = new Vector2Int(x, y); if (!buckets.TryGetValue(key, out List<int> list)) buckets[key] = list = new List<int>(); list.Add(i);
                }
            }
        }
        public MapCell Pick(Vector2 p)
        {
            if (p.x < 0 || p.y < 0 || p.x > map.Width || p.y > map.Height) return null;
            if (!buckets.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.y / size)), out List<int> list)) return null;
            foreach (int i in list) { var c = map.Cells[i]; if (Contains(c.Polygon, p)) return c; }
            return null;
        }
        public static bool Contains(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                Vector2 ab = b - a; float length = ab.sqrMagnitude;
                if (length > 0 && (a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / length) - p).sqrMagnitude < 1e-10f) return true;
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
