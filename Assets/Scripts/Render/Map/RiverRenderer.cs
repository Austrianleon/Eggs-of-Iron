using Render.Common;
using System.Collections.Generic;
using UnityEngine;

namespace EggsOfIron.Map
{
    internal sealed class RiverRenderer : System.IDisposable
    {
        readonly MapMeshLayer layer;

        public RiverRenderer(Transform parent, Material material, WorldMapData map, float widthScale, Color color)
        {
            layer = new MapMeshLayer(parent, "Rivers", material, 20);
            var junctions = new HashSet<Vector2>();
            foreach (var river in map.Rivers)
                junctions.Add(river.Points[river.Points.Length - 1]);

            var shoreline = new List<(Vector2 start, Vector2 end)>();
            foreach (var edge in map.Edges)
                if (map.IsCoast(edge))
                    shoreline.Add((map.Vertices[edge.A], map.Vertices[edge.B]));

            var cuts = new List<float>();
            foreach (var river in map.Rivers)
            {
                var path = new List<Vector2>(river.Points);
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    var smoothed = new List<Vector2> { path[0] };
                    for (int i = 1; i < path.Count; i++)
                    {
                        smoothed.Add(Vector2.Lerp(path[i - 1], path[i], .25f));
                        smoothed.Add(Vector2.Lerp(path[i - 1], path[i], .75f));
                        // Keep tributary mouths on the receiving river after rounding its bends.
                        if (i == path.Count - 1 || junctions.Contains(path[i]))
                            smoothed.Add(path[i]);
                    }
                    path = smoothed;
                }

                float width = Mathf.Clamp(river.Width, .04f, 1.5f) * widthScale;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector2 start = path[i - 1], end = path[i];
                    if ((end - start).sqrMagnitude < 1e-8f) continue;
                    float startRadius = width * .5f * Mathf.Lerp(.4f, 1, (i - 1f) / (path.Count - 1));
                    float endRadius = width * .5f * Mathf.Lerp(.4f, 1, (float)i / (path.Count - 1));
                    DrawLandSegment(start, end, startRadius, endRadius, shoreline, map, cuts, color);
                }
            }
            layer.Flush();
        }

        void DrawLandSegment(Vector2 start, Vector2 end, float startRadius, float endRadius,
            List<(Vector2 start, Vector2 end)> shoreline, IMapGeography geography, List<float> cuts, Color color)
        {
            Vector2 delta = end - start;
            cuts.Clear();
            cuts.Add(0);
            cuts.Add(1);
            foreach (var edge in shoreline)
            {
                Vector2 bank = edge.end - edge.start;
                float determinant = Cross(delta, bank);
                if (Mathf.Abs(determinant) < 1e-8f) continue;
                Vector2 offset = edge.start - start;
                float alongRiver = Cross(offset, bank) / determinant;
                float alongBank = Cross(offset, delta) / determinant;
                if (alongRiver > 0 && alongRiver < 1 && alongBank >= 0 && alongBank <= 1)
                    cuts.Add(alongRiver);
            }
            cuts.Sort();

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
            for (int i = 1; i < cuts.Count; i++)
            {
                float from = cuts[i - 1], to = cuts[i];
                if (to - from < 1e-6f || geography.Pick(start + delta * ((from + to) * .5f))?.Land != true)
                    continue;

                Vector2 a = start + delta * from, b = start + delta * to;
                float aRadius = from == 0 && geography.Pick(a)?.Land == true ? startRadius : 0;
                float bRadius = to == 1 && geography.Pick(b)?.Land == true ? endRadius : 0;
                layer.Quad(a - normal * aRadius, a + normal * aRadius,
                    b + normal * bRadius, b - normal * bRadius, color);
                if (aRadius > 0) layer.Disc(a, aRadius, color);
                if (bRadius > 0) layer.Disc(b, bRadius, color);
            }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        public void Dispose() => layer.Clear();
    }
}
