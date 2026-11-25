using Render.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace EggsOfIron.Map
{
    [DisallowMultipleComponent]
    public sealed class WorldMapController : MonoBehaviour
    {
        [Header("Map Source")]
        [FormerlySerializedAs("FullJson")]
        [Tooltip("Leave empty to use the bundled Tria map.")]
        public TextAsset MapJson;
        public bool GenerateOnStart = true;
        [Tooltip("Assign the provided shader assets so player builds retain them.")]
        public Shader TerrainShader;
        public Shader OverlayShader;
        [Header("Terrain")]
        public TerrainSettings Terrain = new TerrainSettings();
        [Header("Politics")]
        [Range(0, 1)] public float OverlayOpacity = .3f;
        public BorderSettings Borders = new BorderSettings();
        [Header("Rivers")]
        [Min(.01f)] public float RiverWidthScale = 2;
        public Color RiverColor = new Color(.20f, .37f, .39f, 1);
        [Header("Routes")]
        public bool ShowRoutes = true;
        [Min(.01f)] public float RouteWidthScale = 1;
        [Header("Resources")]
        public bool ShowResources = true;
        [Header("Settlements")]
        public SettlementSettings Settlements = new SettlementSettings();
        [Header("Labels")]
        public MapLabelSettings Labels = new MapLabelSettings();
        [Header("Navigation")]
        public Camera MapCamera;
        public WorldMapData Data { get; private set; }
        public MapPoliticalState Politics { get; private set; }
        public string LastError { get; private set; }
        TerrainRenderer terrain;
        PoliticalOverlayRenderer political;
        BorderRenderer borders;
        readonly List<IDisposable> renderLayers = new List<IDisposable>();
        MapInteractionController interaction;
        MapLabelController labels;
        Material terrainMaterial, overlayMaterial, lineMaterial;
        void Start() { if (GenerateOnStart) Generate(); }
        void LateUpdate()
        {
            if (MapCamera) terrain?.CoverViewport(MapCamera);
            if (overlayMaterial) overlayMaterial.SetFloat("_Opacity", OverlayOpacity);
            if (terrainMaterial) { terrainMaterial.SetFloat("_Detail", Terrain.ProceduralDetail); terrainMaterial.SetFloat("_DetailScale", Terrain.DetailScale); }
        }
        [ContextMenu("Generate Map")]
        public void Generate()
        {
            try
            {
                TextAsset source = MapJson ? MapJson : Resources.Load<TextAsset>("Tria.map");
                if (!source) throw new InvalidOperationException("Assign a world map JSON TextAsset or restore the bundled Tria map");
                if (transform.position != Vector3.zero || transform.rotation != Quaternion.identity || transform.lossyScale != Vector3.one) throw new InvalidOperationException("Map root must have identity position, rotation, and scale (coordinates are world units)");
                // Validate before disposing the currently displayed map.
                var imported = WorldMapLoader.Load(source.text);
                Shader groundShader = TerrainShader ? TerrainShader : Shader.Find("EggsOfIron/Map/Terrain");
                Shader colorShader = OverlayShader ? OverlayShader : Shader.Find("EggsOfIron/Map/Overlay");
                if (!groundShader || !colorShader) throw new InvalidOperationException("Map shaders are missing. Assign both supplied shader assets");
                Clear();
                Data = imported;
                Politics = new MapPoliticalState(imported);
                BuildLayers(groundShader, colorShader);
                InitializeInteraction();
                LastError = null;
                Debug.Log($"Loaded {Data.Name}: {Data.Cells.Count} cells, {Data.Provinces.Count} provinces, {Data.States.Count} states, {Data.Rivers.Count} rivers, {Data.Routes.Count} routes, {Data.Settlements.Count} settlements, {Data.Width} × {Data.Height}", this);
                foreach (string warning in Data.Warnings) Debug.LogWarning(warning, this);
            }
            catch (Exception e) { LastError = e.Message; Debug.LogError("Map generation failed: " + e, this); }
        }
        void BuildLayers(Shader groundShader, Shader colorShader)
        {
            terrainMaterial = new Material(groundShader) { name = "Map terrain (runtime)" };
            overlayMaterial = new Material(colorShader) { name = "Map political tint (runtime)" };
            overlayMaterial.SetFloat("_UsePalette", 1); overlayMaterial.SetFloat("_Opacity", OverlayOpacity);
            lineMaterial = new Material(colorShader) { name = "Map lines and highlights (runtime)" };
            lineMaterial.SetFloat("_UsePalette", 0); lineMaterial.SetFloat("_Opacity", 1);
            terrainMaterial.SetFloat("_Detail", Terrain.ProceduralDetail); terrainMaterial.SetFloat("_DetailScale", Terrain.DetailScale);
            terrain = Own(new TerrainRenderer(transform, terrainMaterial));
            terrain.Build(Data, Terrain);
            terrain.BuildForest(transform, lineMaterial, Data, Terrain.TreeSprite);
            political = Own(new PoliticalOverlayRenderer(transform, overlayMaterial, Data, Politics));
            borders = Own(new BorderRenderer(transform, lineMaterial, Data, Politics, Borders));
            Own(new RiverRenderer(transform, lineMaterial, Data, RiverWidthScale, RiverColor));
            if (ShowRoutes) Own(new RouteRenderer(transform, lineMaterial, Data, RouteWidthScale));
            if (ShowResources) BuildResources();
            Own(new SettlementRenderer(transform, lineMaterial, Data, Settlements));
            Politics.Changed += RefreshPolitics;
        }

        T Own<T>(T layer) where T : IDisposable
        {
            renderLayers.Add(layer);
            return layer;
        }

        void InitializeInteraction()
        {
            if (!MapCamera) MapCamera = Camera.main;
            if (!MapCamera)
            {
                var cameraObject = new GameObject("Map Camera", typeof(Camera));
                MapCamera = cameraObject.GetComponent<Camera>();
                MapCamera.tag = "MainCamera";
            }

            MapCamera.clearFlags = CameraClearFlags.SolidColor;
            MapCamera.backgroundColor = Terrain.DeepOcean;
            var controls = MapCamera.GetComponent<MapCameraController>();
            if (!controls) controls = MapCamera.gameObject.AddComponent<MapCameraController>();
            controls.Initialize(Data);
            terrain.CoverViewport(MapCamera);

            interaction = GetComponent<MapInteractionController>();
            if (!interaction) interaction = gameObject.AddComponent<MapInteractionController>();
            interaction.Initialize(this, controls, lineMaterial);
            labels = GetComponent<MapLabelController>();
            if (!labels) labels = gameObject.AddComponent<MapLabelController>();
            labels.Initialize(this, interaction, MapCamera);
        }

        void BuildResources()
        {
            var resources = Own(new MapMeshLayer(transform, "Resource sites", lineMaterial, 26));
            foreach (var site in Data.ResourceSites)
            {
                Color color = site.Kind switch
                {
                    "coal" => new Color(.22f, .21f, .24f),
                    "iron" => new Color(.62f, .35f, .29f),
                    "gold" => new Color(.98f, .76f, .24f),
                    "nite" => new Color(.72f, .58f, .86f),
                    "farmland" => new Color(.80f, .66f, .42f),
                    "forest" => new Color(.34f, .63f, .34f),
                    "fish" => new Color(.38f, .73f, .81f),
                    _ => Color.white
                };
                resources.Disc(site.Position, 1.75f, new Color(.10f, .11f, .13f), 4);
                resources.Disc(site.Position, 1.25f, color, 4);
            }
            resources.Flush();
        }
        public void SetProvinceOwner(int provinceId, int kingdomId) { if (Politics == null) throw new InvalidOperationException("Generate a map first"); Politics.SetProvinceOwner(provinceId, kingdomId); }
        public void SetKingdomColor(int kingdomId, Color color) { if (Politics == null) throw new InvalidOperationException("Generate a map first"); Politics.SetKingdomColor(kingdomId, color); }
        void RefreshPolitics() { political.Refresh(); borders.Refresh(); }
        void Clear()
        {
            if (Politics != null) Politics.Changed -= RefreshPolitics;
            if (labels) labels.Clear();
            if (interaction) interaction.Clear();
            for (int i = renderLayers.Count - 1; i >= 0; i--) renderLayers[i].Dispose();
            renderLayers.Clear();
            terrain = null;
            political = null;
            borders = null;
            UnityObjectLifetime.Destroy(terrainMaterial);
            UnityObjectLifetime.Destroy(overlayMaterial);
            UnityObjectLifetime.Destroy(lineMaterial);
            terrainMaterial = overlayMaterial = lineMaterial = null;
            Data = null; Politics = null;
        }
        void OnDestroy() => Clear();
    }
}
