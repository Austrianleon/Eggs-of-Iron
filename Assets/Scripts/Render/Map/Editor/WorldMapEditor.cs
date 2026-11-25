using UnityEditor;
using UnityEngine;

namespace EggsOfIron.Map.Editor
{
    [CustomEditor(typeof(WorldMapController))]
    public sealed class WorldMapEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var map = (WorldMapController)target;
            if (Application.isPlaying && GUILayout.Button("Regenerate Map")) map.Generate();
            if (!string.IsNullOrEmpty(map.LastError)) EditorGUILayout.HelpBox(map.LastError, MessageType.Error);
            if (!Application.isPlaying) EditorGUILayout.HelpBox("Enter Play mode to load the map. Leave Map Json empty to use Tria.", MessageType.Info);
        }
        [MenuItem("GameObject/Eggs of Iron/World Map", false, 10)]
        public static void Create()
        {
            var go = new GameObject("World Map"); Undo.RegisterCreatedObjectUndo(go, "Create world map");
            var map = go.AddComponent<WorldMapController>();
            map.TerrainShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Render/Map/Shaders/MapTerrain.shader");
            map.OverlayShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Render/Map/Shaders/MapOverlay.shader");
            map.MapJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Tria.map.json");
            AssignSprites(map);
            map.MapCamera = Camera.main;
            if (map.MapCamera && !map.MapCamera.GetComponent<MapCameraController>()) Undo.AddComponent<MapCameraController>(map.MapCamera.gameObject);
            go.AddComponent<MapInteractionController>();
            Selection.activeGameObject = go;
        }

        internal static void AssignSprites(WorldMapController map)
        {
            map.Terrain.TreeSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/tree.png");
            map.Settlements.TownSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/town.png");
            map.Settlements.VillageSprite = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/village.png");
        }
    }
}
