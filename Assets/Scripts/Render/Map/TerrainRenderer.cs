using Render.Common;
using System.Collections.Generic;
using UnityEngine;

namespace EggsOfIron.Map
{
    [System.Serializable]
    public sealed class TerrainSettings
    {
        public Color DeepOcean = new Color(.16f, .32f, .48f);
        public Color ShallowOcean = new Color(.26f, .53f, .64f);
        public Texture2D TreeSprite;
        [Range(0, 1)] public float ElevationShading = .65f;
        [Range(0, 1)] public float ProceduralDetail = .8f;
        [Min(.05f)] public float DetailScale = .85f;
        [Range(.1f, 1.5f)] public float LandBrightness = .78f;
        [Range(0, 1)] public float LandSaturation = .65f;
    }
    internal sealed class TerrainRenderer : System.IDisposable
    {
        readonly MapMeshLayer layer;
        readonly MapMeshLayer ocean;
        readonly Material material;
        Texture2D coastalShading;
        MapSpriteLayer forest;
        Rect oceanBounds;
        public TerrainRenderer(Transform parent, Material material)
        {
            this.material = material;
            layer = new MapMeshLayer(parent, "Terrain", material, 0);
            ocean = new MapMeshLayer(parent, "Ocean", material, -1);
        }
        public void Build(WorldMapData map, TerrainSettings settings)
        {
            layer.Clear();
            SetOceanBounds(new Rect(0, 0, map.Width, map.Height));
            BuildCoastalShading(map, settings);
            // Average shared land vertex colors, keeping water and land palettes independent.
            var colors = new Dictionary<(int vertex, bool land), (Vector4 sum, int count)>();
            foreach (var cell in map.Cells)
            {
                Color color;
                if (!cell.Land) color = settings.DeepOcean;
                else
                {
                    color = map.BiomeColors.TryGetValue(cell.Biome, out Color biome) ? biome : new Color(.48f, .58f, .31f);
                    color = Color.Lerp(color, new Color(.72f, .70f, .65f), Mathf.InverseLerp(55, 90, cell.Height) * .6f);
                    float luminance = color.r * .2126f + color.g * .7152f + color.b * .0722f;
                    color = Color.Lerp(new Color(luminance, luminance, luminance), color, settings.LandSaturation);
                    color *= settings.LandBrightness;
                    color *= 1 - settings.ElevationShading * Mathf.InverseLerp(20, 100, cell.Height) * .5f; color.a = 1;
                }
                foreach (int vertex in cell.Vertices)
                {
                    var key = (vertex, cell.Land);
                    colors.TryGetValue(key, out var value);
                    colors[key] = (value.sum + (Vector4)color, value.count + 1);
                }
            }
            foreach (var cell in map.Cells)
                layer.Polygon(cell, Color.white, Vector2.zero, vertex =>
                {
                    var value = colors[(vertex, cell.Land)];
                    return (Color)(value.sum / value.count);
                });
            layer.Flush();
        }
        public void CoverViewport(Camera camera)
        {
            Vector3 center = camera.transform.position;
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            float left = center.x - halfWidth, right = center.x + halfWidth;
            float bottom = center.y - halfHeight, top = center.y + halfHeight;
            if (left >= oceanBounds.xMin && right <= oceanBounds.xMax &&
                bottom >= oceanBounds.yMin && top <= oceanBounds.yMax)
                return;

            // Leave room for further resizing without rebuilding the mesh on every frame.
            float padding = halfHeight * .1f;
            SetOceanBounds(Rect.MinMaxRect(
                Mathf.Min(oceanBounds.xMin, left - padding), Mathf.Min(oceanBounds.yMin, bottom - padding),
                Mathf.Max(oceanBounds.xMax, right + padding), Mathf.Max(oceanBounds.yMax, top + padding)));
        }
        public void BuildForest(Transform parent, Material overlay, WorldMapData map, Texture2D tree)
        {
            if (!tree) return;

            forest = new MapSpriteLayer(parent, overlay, tree, "Forest sprites", 12);
            var settlementCells = new HashSet<int>();
            foreach (var settlement in map.Settlements) settlementCells.Add(settlement.CellId);
            foreach (var cell in map.Cells)
            {
                if (!cell.Land || cell.Biome < 5 || cell.Biome > 9 || settlementCells.Contains(cell.Id)) continue;
                uint seed = unchecked((uint)cell.Id * 2654435761u);
                if ((seed >> 24) >= 170) continue;
                float scale = Mathf.Lerp(3.8f, 5.4f, ((seed >> 16) & 255) / 255f);
                forest.Add(cell.Center, scale);
            }
            forest.Flush();
        }
        void SetOceanBounds(Rect bounds)
        {
            oceanBounds = bounds;
            ocean.Clear();
            ocean.Quad(bounds.min, new Vector2(bounds.xMax, bounds.yMin), bounds.max,
                new Vector2(bounds.xMin, bounds.yMax), Color.white);
            ocean.Flush();
        }
        void BuildCoastalShading(WorldMapData map, TerrainSettings settings)
        {
            UnityObjectLifetime.Destroy(coastalShading);
            const int width = 512;
            int height = Mathf.Clamp(Mathf.RoundToInt(width * map.Height / map.Width), 32, 1024);
            float stepX = map.Width / width, stepY = map.Height / height;
            float diagonal = Mathf.Sqrt(stepX * stepX + stepY * stepY);
            float fadeDistance = Mathf.Min(map.Width, map.Height) * .035f;
            var distances = new float[width * height];

            // Only land seeds the distance field. Water outside the map mesh uses the same gradient.
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = map.Pick(new Vector2((x + .5f) * stepX, (y + .5f) * stepY));
                distances[y * width + x] = cell != null && cell.Land ? 0 : fadeDistance;
            }

            // Propagate distances in both directions, including diagonal neighbors.
            for (int pass = 0; pass < 2; pass++)
            {
                int direction = pass == 0 ? 1 : -1;
                for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    int y = pass == 0 ? row : height - 1 - row;
                    int x = pass == 0 ? column : width - 1 - column;
                    int index = y * width + x;
                    int previousX = x - direction, previousY = y - direction;
                    float distance = distances[index];
                    if (previousX >= 0 && previousX < width)
                        distance = Mathf.Min(distance, distances[y * width + previousX] + stepX);
                    if (previousY >= 0 && previousY < height)
                    {
                        int previousRow = previousY * width;
                        distance = Mathf.Min(distance, distances[previousRow + x] + stepY);
                        if (x > 0)
                            distance = Mathf.Min(distance, distances[previousRow + x - 1] + diagonal);
                        if (x < width - 1)
                            distance = Mathf.Min(distance, distances[previousRow + x + 1] + diagonal);
                    }
                    distances[index] = distance;
                }
            }

            var pixels = new Color[distances.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1 - Mathf.SmoothStep(0, 1, distances[i] / fadeDistance), 0, 0, 1);
            coastalShading = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Coastal water shading",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            coastalShading.SetPixels(pixels); coastalShading.Apply(false, true);
            material.SetTexture("_CoastalShading", coastalShading);
            material.SetVector("_MapSize", new Vector4(map.Width, map.Height, 0, 0));
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            material.SetVector("_DeepOcean", linear ? settings.DeepOcean.linear : settings.DeepOcean);
            material.SetVector("_ShallowOcean", linear ? settings.ShallowOcean.linear : settings.ShallowOcean);
        }
        public void Dispose() { forest?.Dispose(); ocean.Clear(); layer.Clear(); UnityObjectLifetime.Destroy(coastalShading); }
    }
}
