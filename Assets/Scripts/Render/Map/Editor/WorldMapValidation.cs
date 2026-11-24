using System;
using System.Linq;
using Render.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EggsOfIron.Map.Editor
{
    // No NUnit/package dependency. Runs inside the real Unity editor, or with -executeMethod.
    public static class WorldMapValidation
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception("Map validation: " + message); }
        [MenuItem("Tools/Eggs of Iron/Validate World Map")]
        public static void Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Tria.map.json");
            Check(source != null, "Tria map missing");
            var map = WorldMapLoader.Load(source.text);
            Check(map.Cells.Count == 4066 && map.Rivers.Count == 156, "Sample cell/river counts");
            Check(map.Routes.Count == 528 && map.Routes.Count(r => r.Kind == "road") == 13
                && map.Routes.Count(r => r.Kind == "trail") == 404
                && map.Routes.Count(r => r.Kind == "sea") == 111, "Sample route groups");
            Check(map.Routes[0].Name == "Celestial way"
                && Vector2.Distance(map.Routes[0].Points[0], new Vector2(583.55f, 480 - 252.99f)) < .001f,
                "Route metadata and position");
            Check(map.Settlements.Count == 623 && map.Settlements.Count(s => s.Capital) == 10, "Settlement and capital counts");
            Check(map.Settlements[0].Name == "Rench" && map.Settlements[0].CellId == 1221, "Settlement metadata");
            Check(map.Settlements[0].Inhabitants == 18706, "Settlement inhabitants");
            Check(map.Provinces.Values.All(p => p.Culture.HasValue)
                && map.States.Values.All(s => !s.Culture.HasValue)
                && map.Provinces.Values.GroupBy(p => p.Culture).Select(group => group.Count())
                    .OrderBy(count => count).SequenceEqual(new[] { 22, 23, 23, 23 }),
                "One culture per province with balanced province counts");
            int secondId = map.Settlements[1].Id;
            ISettlementTravel travel = map;
            double distance = travel.SettlementDistance(1, secondId);
            Check(map.MetersPerMapUnit == 100
                && Math.Abs(distance - 48565.349) < .1
                && Math.Abs(distance - travel.SettlementDistance(secondId, 1)) < .1
                && travel.SettlementDistance(1, 1) == 0, "Shortest settlement distance over routes and rivers");
            Check(map.ResourceSites.Count == 79
                && map.ResourceSites.Select(site => site.Kind).Distinct().Count() == 7, "Resource sites");
            Check(Vector2.Distance(map.Settlements[0].Position, new Vector2(191.94f, 480 - 180.64f)) < .001f, "Settlement position");
            Check(map.Width == 720 && map.Height == 480, "Map dimensions");
            Check(map.States[1].Name == "Haiters Empire", "Kingdom names");
            Check(map.Provinces[1].InitialState == 1, "Province ownership");
            IMapGeography geography = map;
            foreach (var cell in map.Cells)
            {
                Check(MapSpatialIndex.Contains(cell.Polygon, cell.Center), "Center outside cell " + cell.Id);
                Check(geography.Pick(cell.Center) == cell, "Spatial picking " + cell.Id);
                Check(geography.TryGetCell(cell.Id, out var found) && found == cell, "Cell lookup " + cell.Id);
                foreach (var p in cell.Polygon) Check(!float.IsNaN(p.x) && !float.IsNaN(p.y), "Invalid vertex");
            }
            Check(geography.Pick(new Vector2(-10, -10)) == null
                && !geography.TryGetCell(-1, out _), "Outside picking and unknown cell ID");
            var state = new MapPoliticalState(map); int events = 0; state.Changed += () => events++;
            state.SetProvinceOwner(1, 2);
            foreach (var c in map.Cells.Where(c => c.Land && c.Province == 1)) Check(state.Owner(c) == 2, "Ownership propagation");
            Check(map.Provinces[1].InitialState == 1, "Imported data must remain unchanged");
            state.SetKingdomColor(2, Color.magenta); Check(state.ColorOf(2) == Color.magenta && events == 2, "Political events");
            state.SetProvinceOwner(1, 0);
            foreach (var c in map.Cells.Where(c => c.Land && c.Province == 1)) Check(state.Owner(c) == 0, "Unclaimed province");
            foreach (var c in map.Cells.Where(c => !c.Land)) Check(state.Owner(c) == 0, "Ocean ownership");
            ExpectFailure("{}"); ExpectFailure("{broken"); ExpectFailure(source.text.Substring(0, source.text.Length / 2));
            ExpectFailure("{\"schemaVersion\":1,\"width\":10,\"height\":10}");
            ExpectFailure("{\"schemaVersion\":2,\"width\":10,\"height\":10}");
            ValidateScene();
            Debug.Log("WORLD MAP VALIDATION PASSED: 4,066 cells picked, 156 rivers, geometry, ownership, malformed JSON, schema version, rendering resources, and regeneration.");
        }
        static void ExpectFailure(string json)
        {
            bool failed = false; try { WorldMapLoader.Load(json); } catch (FormatException) { failed = true; }
            Check(failed, "Invalid input accepted");
        }
        static void ValidateScene()
        {
            var go = new GameObject("Validation map"); var cameraObject = new GameObject("Validation camera", typeof(Camera));
            try
            {
                var map = go.AddComponent<WorldMapController>(); map.GenerateOnStart = false;
                map.TerrainShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Render/Map/Shaders/MapTerrain.shader");
                map.OverlayShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Render/Map/Shaders/MapOverlay.shader");
                WorldMapEditor.AssignSprites(map);
                Check(map.TerrainShader && map.OverlayShader, "Shader assets missing");
                Check(!ShaderUtil.ShaderHasError(map.TerrainShader) && !ShaderUtil.ShaderHasError(map.OverlayShader), "Shader compilation");
                map.MapCamera = cameraObject.GetComponent<Camera>(); map.Generate();
                Check(map.Data?.Name == "Tria" && map.LastError == null, "Generate with the default map: " + map.LastError);
                Check(go.GetComponentsInChildren<MeshFilter>().Any(mesh => mesh.name == "Routes"
                    && mesh.sharedMesh.vertexCount > 0), "Route geometry");
                Check(go.GetComponentsInChildren<MeshFilter>().Any(mesh => mesh.name == "Resource sites"
                    && mesh.sharedMesh.vertexCount > 0), "Resource site geometry");
                var controls = cameraObject.GetComponent<MapCameraController>();
                float originalAspect = map.MapCamera.aspect;
                map.MapCamera.aspect = 1.5f; controls.SetZoom(10000);
                Check(Mathf.Approximately(map.MapCamera.orthographicSize, 240), "Map-fit maximum zoom-out");
                controls.MaximumZoom = 120; controls.SetZoom(10000);
                Check(Mathf.Approximately(map.MapCamera.orthographicSize, 120), "Configured maximum zoom-out");
                controls.MinimumZoom = 300; controls.SetZoom(1);
                Check(Mathf.Approximately(map.MapCamera.orthographicSize, 120), "Map cap wins over incompatible minimum");
                controls.MinimumZoom = 8; controls.MaximumZoom = 500;
                map.MapCamera.aspect = .75f; controls.SetZoom(10000);
                Check(Mathf.Approximately(map.MapCamera.orthographicSize, 480), "Portrait viewport zoom-out");
                map.MapCamera.aspect = 3; controls.SetZoom(10000);
                Check(Mathf.Approximately(map.MapCamera.orthographicSize, 240), "Wide viewport zoom-out");
                controls.SetZoom(-100); Check(Mathf.Approximately(map.MapCamera.orthographicSize, 8), "Minimum zoom");
                map.MapCamera.aspect = originalAspect;
                controls.Initialize(map.Data);
                var meshes = go.GetComponentsInChildren<MeshFilter>(); Check(meshes.Length > 3 && meshes.Length < 30, "Combined mesh count");
                foreach (var filter in meshes) Check(filter.sharedMesh.vertexCount <= 60000 && filter.sharedMesh.triangles.Length > 0, "Mesh chunk limits");
                foreach (string spriteLayer in new[] { "Villages", "Towns", "Forest sprites" })
                {
                    var spriteMesh = meshes.SingleOrDefault(m => m.gameObject.name == spriteLayer);
                    Check(spriteMesh && spriteMesh.sharedMesh.uv.Contains(Vector2.one), "Missing textured mesh for " + spriteLayer);
                }
                Check(meshes.Any(m => m.gameObject.name == "Kingdom borders") && meshes.Any(m => m.gameObject.name == "Kingdom border casing"), "Thick kingdom outlines");
                Check(go.GetComponentsInChildren<MeshRenderer>().First(r => r.gameObject.name == "Villages").sortingOrder > 31, "Settlements above province highlights");
                var labels = go.GetComponent<MapLabelController>();
                Check(labels && labels.KingdomLabelCount == 10 && labels.ProvinceLabelCount == 91, "Kingdom/province labels");
                Check(labels.TryGetKingdomAnchor(1, out Vector2 anchor) && map.Politics.Owner(map.Data.Pick(anchor)) == 1, "Kingdom label inside owned land");
                var terrain = meshes.First(m => m.gameObject.name == "Terrain").sharedMesh;
                map.SetProvinceOwner(1, 2); map.SetKingdomColor(2, Color.red);
                Check(terrain == go.GetComponentsInChildren<MeshFilter>().First(m => m.gameObject.name == "Terrain").sharedMesh, "Ownership rebuilt terrain");
                var interaction = go.GetComponent<MapInteractionController>(); interaction.SelectProvince(1); Check(interaction.SelectedProvince == 1, "Selection");
                map.MapCamera.aspect = 1.5f;
                map.MapCamera.transform.position = new Vector3(360, 240, -10);
                controls.SetZoom(240); labels.RefreshLabels();
                Check(!labels.ShowingProvinces && labels.IsProvinceLabelVisible(1), "Selected province name at full-map zoom");
                controls.SetZoom(60); labels.RefreshLabels(); Check(labels.ShowingProvinces, "Province names when zoomed in");
                Check(labels.TryGetKingdomAnchor(2, out anchor) && map.Politics.Owner(map.Data.Pick(anchor)) == 2, "Kingdom labels follow ownership changes");
                map.Generate(); Check(go.GetComponentsInChildren<MeshFilter>().Length == meshes.Length, "Regeneration leaked meshes");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cameraObject); }
        }
        // Creates a separate scene; it never edits SampleScene or build settings.
        [MenuItem("Tools/Eggs of Iron/Create World Map Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Map Camera", typeof(Camera), typeof(MapCameraController)); camera.tag = "MainCamera";
            camera.GetComponent<Camera>().orthographic = true;
            WorldMapEditor.Create();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Scenes/WorldMapDemo.unity");
        }
        // Optional GPU smoke check: run without -nographics. Output is a temporary PNG.
        public static void RunVisualBatch()
        {
            try
            {
                Run();
                CreateDemoScene();
                var map = UnityEngine.Object.FindFirstObjectByType<WorldMapController>();
                map.Generate(); Check(map.Data != null, "Visual generation");
                var target = new RenderTexture(1440, 960, 24);
                target.Create();
                map.MapCamera.aspect = 1.5f; map.MapCamera.orthographicSize = 240;
                map.MapCamera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); map.GetComponent<MapLabelController>().RefreshLabels(); Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(map.MapCamera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                var previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply(); RenderTexture.active = previous;
                var pixels = image.GetPixels32();
                int magenta = pixels.Count(c => c.r > 240 && c.g < 20 && c.b > 240);
                Check(magenta < pixels.Length / 100, "Pink error shader pixels");
                Check(pixels.Any(c => c.g > c.b && c.g > c.r), "No green land rendered");
                System.IO.File.WriteAllBytes("/tmp/world-map-preview.png", image.EncodeToPNG());
                var labels = map.GetComponent<MapLabelController>();
                Check(labels.TryGetProvinceAnchor(1, out Vector2 area), "Selected province anchor");
                map.MapCamera.transform.position = new Vector3(area.x, area.y, -10);
                map.MapCamera.GetComponent<MapCameraController>().SetZoom(60);
                map.GetComponent<MapInteractionController>().SelectProvince(1);
                labels.RefreshLabels(); Canvas.ForceUpdateCanvases();
                Check(labels.ShowingProvinces && labels.IsProvinceLabelVisible(1), "Visible selected-area text when zoomed in");
                RenderPipeline.SubmitRenderRequest(map.MapCamera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                previous = RenderTexture.active; RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply(); RenderTexture.active = previous;
                System.IO.File.WriteAllBytes("/tmp/world-map-detail-preview.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image); map.MapCamera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                Check(!ShaderUtil.ShaderHasError(map.TerrainShader) && !ShaderUtil.ShaderHasError(map.OverlayShader), "GPU shader compilation");
                Debug.Log("WORLD MAP GPU VALIDATION PASSED: /tmp/world-map-preview.png");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void RunBatch()
        {
            try { Run(); CreateDemoScene(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
