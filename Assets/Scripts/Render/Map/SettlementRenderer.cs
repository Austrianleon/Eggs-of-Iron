using Render.Common;
using UnityEngine;

namespace EggsOfIron.Map
{
    [System.Serializable]
    public sealed class SettlementSettings
    {
        public bool Show = true;
        public Texture2D VillageSprite, TownSprite;
        [Min(0)] public float TownPopulation = 10000;
        [Min(.05f)] public float TownRadius = .65f;
        [Min(.05f)] public float CapitalRadius = 1.8f;
        [Range(0, 1)] public float PopulationScaling = .45f;
    }
    internal sealed class SettlementRenderer : System.IDisposable
    {
        readonly MapSpriteLayer villages, towns;
        public SettlementRenderer(Transform parent, Material material, WorldMapData map, SettlementSettings settings)
        {
            if (!settings.Show) return;
            if (settings.VillageSprite) villages = new MapSpriteLayer(parent, material, settings.VillageSprite, "Villages", 40);
            if (settings.TownSprite) towns = new MapSpriteLayer(parent, material, settings.TownSprite, "Towns", 41);
            foreach (var settlement in map.Settlements)
            {
                Vector2 p = settlement.Position;
                float population = Mathf.Clamp01(Mathf.Log10(1 + (float)settlement.Inhabitants / 1000) / 2);
                float radius = settings.TownRadius * (1 + population * settings.PopulationScaling);
                var sprite = settlement.Capital || settlement.Inhabitants >= settings.TownPopulation ? towns : villages;
                sprite = sprite ?? towns ?? villages;
                float size = settlement.Capital ? settings.CapitalRadius * 3.4f : radius * (sprite == towns ? 4.8f : 4f);
                sprite?.Add(p, size);
            }
            villages?.Flush(); towns?.Flush();
        }

        public void Dispose() { villages?.Dispose(); towns?.Dispose(); }
    }

    internal sealed class MapSpriteLayer
    {
        readonly MapMeshLayer layer;
        readonly Material material;
        readonly float aspect;

        public MapSpriteLayer(Transform parent, Material overlay, Texture2D texture, string name, int order)
        {
            material = new Material(overlay) { name = name + " sprites (runtime)" };
            material.SetTexture("_SpriteTex", texture);
            material.SetFloat("_UseSprite", 1);
            aspect = (float)texture.width / texture.height;
            layer = new MapMeshLayer(parent, name, material, order);
        }

        public void Add(Vector2 position, float height)
        {
            Vector2 halfSize = new Vector2(aspect, 1) * (height * .5f);
            layer.Quad(position - halfSize, position - halfSize + Vector2.right * (halfSize.x * 2),
                position + halfSize, position + halfSize - Vector2.right * (halfSize.x * 2), Color.white, textured: true);
        }

        public void Flush() => layer.Flush();
        public void Dispose() { layer.Clear(); UnityObjectLifetime.Destroy(material); }
    }
}
