using Render.Common;
using UnityEngine;

namespace EggsOfIron.Map
{
    internal sealed class PoliticalOverlayRenderer : System.IDisposable
    {
        readonly WorldMapData map;
        readonly IMapPolitics state;
        readonly MapMeshLayer layer;
        readonly Texture2D palette;
        readonly Color32[] pixels;
        readonly int width;
        public PoliticalOverlayRenderer(Transform parent, Material material, WorldMapData map, IMapPolitics state)
        {
            this.map = map; this.state = state;
            width = Mathf.Min(SystemInfo.maxTextureSize, 1024); int height = Mathf.CeilToInt((float)map.Cells.Count / width);
            if (height > SystemInfo.maxTextureSize) throw new System.InvalidOperationException("Map exceeds political palette capacity");
            palette = new Texture2D(width, Mathf.Max(1, height), TextureFormat.RGBA32, false) { name = "Runtime political palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[width * palette.height]; material.SetTexture("_Palette", palette);
            layer = new MapMeshLayer(parent, "Kingdom overlay", material, 10);
            for (int i = 0; i < map.Cells.Count; i++) if (map.Cells[i].Land) layer.Polygon(map.Cells[i], Color.white, new Vector2((i % width + .5f) / width, (i / width + .5f) / palette.height));
            layer.Flush(); Refresh();
        }
        public void Refresh()
        {
            for (int i = 0; i < map.Cells.Count; i++) { int owner = state.Owner(map.Cells[i]); Color c = owner == 0 ? Color.clear : state.ColorOf(owner); c.a = owner == 0 ? 0 : c.a; pixels[i] = c; }
            palette.SetPixels32(pixels); palette.Apply(false, false);
        }
        public void Dispose() { layer.Clear(); UnityObjectLifetime.Destroy(palette); }
    }
}
