using Render.Common;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace EggsOfIron.Map
{
    public sealed class MapInteractionController : MonoBehaviour
    {
        public bool ShowNames = true;
        public Color HoverColor = new Color(1, 1, 1, .15f);
        public Color SelectionColor = new Color(1, .85f, .25f, .35f);
        public int HoveredProvince { get; private set; }
        public int SelectedProvince { get; private set; }
        public int HoveredKingdom => hoveredCell != null && world != null ? world.Politics.Owner(hoveredCell) : 0;
        public event Action<int> ProvinceSelected;
        WorldMapController world;
        MapCameraController cameraControl;
        IMapGeography geography;
        MapMeshLayer hover, selected;
        MapCell hoveredCell;
        public void Initialize(WorldMapController map, MapCameraController cameraController, Material material)
        {
            Clear(); world = map; cameraControl = cameraController; geography = map.Data;
            hover = new MapMeshLayer(map.transform, "Hover", material, 30); selected = new MapMeshLayer(map.transform, "Selection", material, 31);
        }
        void Update()
        {
            if (world == null || world.Data == null || Mouse.current == null || geography == null) return;
            bool blocked = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            hoveredCell = blocked ? null : geography.Pick(cameraControl.WorldPoint(Mouse.current.position.ReadValue()));
            int province = hoveredCell != null && hoveredCell.Land ? hoveredCell.Province : 0;
            if (province != HoveredProvince) { HoveredProvince = province; Highlight(hover, province, HoverColor); }
            if (!blocked && Mouse.current.leftButton.wasPressedThisFrame) SelectProvince(province);
        }
        public void SelectProvince(int province)
        {
            if (world == null || (province != 0 && !world.Data.Provinces.ContainsKey(province))) return;
            if (SelectedProvince == province) return;
            SelectedProvince = province; Highlight(selected, province, SelectionColor); ProvinceSelected?.Invoke(province);
        }
        void Highlight(MapMeshLayer layer, int province, Color color)
        {
            layer.Clear(); if (province > 0) foreach (var c in world.Data.Cells) if (c.Land && c.Province == province) layer.Polygon(c, color, Vector2.zero); layer.Flush();
        }
        string Description(int province)
        {
            if (province == 0 || !world.Data.Provinces.TryGetValue(province, out MapRegion p)) return "";
            int owner = p.InitialState;
            foreach (var c in world.Data.Cells) if (c.Province == province && c.Land) { owner = world.Politics.Owner(c); break; }
            string description = p.Name + " — " + (owner != 0 && world.Data.States.TryGetValue(owner, out MapRegion s) ? s.Name : "Unclaimed");
            if (p.Culture.HasValue) description += " · " + p.Culture.Value;
            return description;
        }
        void OnGUI()
        {
            if (!ShowNames || world == null || world.Data == null) return;
            GUI.Box(new Rect(12, 12, 500, 74), "Hover: " + Description(HoveredProvince) + "\nSelected: " + Description(SelectedProvince) + "\nWASD · Middle mouse pan · Wheel zoom");
        }
        public void Clear() { hover?.Clear(); selected?.Clear(); hover = selected = null; geography = null; hoveredCell = null; HoveredProvince = SelectedProvince = 0; world = null; }
        void OnDestroy() => Clear();
    }
}
