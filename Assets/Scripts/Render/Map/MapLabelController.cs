using Render.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EggsOfIron.Map
{
    [System.Serializable]
    public sealed class MapLabelSettings
    {
        public bool Show = true;
        [Min(1)] public float ProvinceZoomThreshold = 90;
        [Range(10, 40)] public int KingdomFontSize = 20;
        [Range(10, 30)] public int ProvinceFontSize = 14;
        [Range(10, 36)] public int SelectedFontSize = 18;
        public Color KingdomColor = new Color(.9f, .83f, .65f, .95f);
        public Color ProvinceColor = new Color(.9f, .87f, .76f, .9f);
        public Color SelectedColor = new Color(1, .82f, .38f, 1);
    }

    // One batched UI canvas and one object per named territory, never one per cell.
    // Screen-space text keeps a readable size as the geographic camera zooms.
    public sealed class MapLabelController : MonoBehaviour
    {
        sealed class Label
        {
            public int Id;
            public Vector2 Anchor;
            public Text Text;
            public RectTransform Rect;
            public bool Visible;
        }
        readonly Dictionary<int, Label> kingdomLabels = new Dictionary<int, Label>();
        readonly Dictionary<int, Label> provinceLabels = new Dictionary<int, Label>();
        readonly List<Rect> occupied = new List<Rect>();
        WorldMapController world;
        IMapPolitics politics;
        MapInteractionController interaction;
        Camera view;
        Canvas canvas;
        Font font;
        public int KingdomLabelCount => kingdomLabels.Count;
        public int ProvinceLabelCount => provinceLabels.Count;
        public bool ShowingProvinces => view && world && view.orthographicSize <= world.Labels.ProvinceZoomThreshold;

        public void Initialize(WorldMapController map, MapInteractionController input, Camera camera)
        {
            Clear(); world = map; politics = map.Politics; interaction = input; view = camera;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Territory labels", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(map.transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view;
            canvas.planeDistance = Mathf.Max(view.nearClipPlane + .1f, 1);
            canvas.overrideSorting = true; canvas.sortingOrder = 50;
            CreateLabels(provinceLabels, cell => cell.Province, map.Data.Provinces);
            RebuildKingdoms(); politics.Changed += RebuildKingdoms;
            Canvas.ForceUpdateCanvases(); RefreshLabels();
        }
        void CreateLabels(Dictionary<int, Label> labels, System.Func<MapCell, int> territory, Dictionary<int, MapRegion> regions, bool uppercase = false)
        {
            foreach (var group in world.Data.Cells.Where(cell => cell.Land).GroupBy(territory))
                if (group.Key > 0 && regions.TryGetValue(group.Key, out MapRegion region))
                    labels[group.Key] = CreateLabel(group.Key, uppercase ? region.Name.ToUpperInvariant() : region.Name, Anchor(group.ToList()));
        }
        void RebuildKingdoms()
        {
            foreach (var label in kingdomLabels.Values) { label.Text.gameObject.SetActive(false); UnityObjectLifetime.Destroy(label.Text.gameObject); }
            kingdomLabels.Clear();
            CreateLabels(kingdomLabels, politics.Owner, world.Data.States, true);
        }
        // Pick a cell inside the largest contiguous piece of a territory, nearest its
        // area-weighted center. Centroids alone can lie in the sea or another kingdom.
        Vector2 Anchor(List<MapCell> cells)
        {
            var remaining = new HashSet<int>(cells.Select(cell => cell.Id));
            List<MapCell> largest = null; float largestArea = -1;
            var queue = new Queue<MapCell>();
            foreach (var seed in cells)
            {
                if (!remaining.Remove(seed.Id)) continue;
                var component = new List<MapCell>(); float area = 0; queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue(); component.Add(cell); area += Area(cell);
                    foreach (int id in cell.Neighbors) if (remaining.Remove(id) && world.Data.CellIndex.TryGetValue(id, out int index)) queue.Enqueue(world.Data.Cells[index]);
                }
                if (area > largestArea) { largest = component; largestArea = area; }
            }
            Vector2 center = largest.Aggregate(Vector2.zero, (sum, cell) => sum + cell.Center * Area(cell)) / Mathf.Max(largestArea, .0001f);
            return largest.OrderBy(cell => (cell.Center - center).sqrMagnitude).First().Center;
        }
        static float Area(MapCell cell)
        {
            double area = 0;
            for (int i = 0, j = cell.Polygon.Length - 1; i < cell.Polygon.Length; j = i++)
                area += (double)cell.Polygon[j].x * cell.Polygon[i].y - (double)cell.Polygon[i].x * cell.Polygon[j].y;
            return Mathf.Max(.0001f, (float)System.Math.Abs(area * .5));
        }
        Label CreateLabel(int id, string name, Vector2 anchor)
        {
            var go = new GameObject("Label " + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<Text>(); text.font = font; text.text = name;
            text.alignment = TextAnchor.MiddleCenter; text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false; text.supportRichText = false;
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.025f, .02f, .015f, .95f); outline.effectDistance = new Vector2(1, -1);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.one * .5f;
            return new Label { Id = id, Anchor = anchor, Text = text, Rect = rect };
        }
        void LateUpdate() => RefreshLabels();
        public void RefreshLabels()
        {
            if (!canvas || !world || !view) return;
            canvas.enabled = isActiveAndEnabled && world.Labels.Show && view.isActiveAndEnabled;
            if (!canvas.enabled) return;
            occupied.Clear();
            var labels = kingdomLabels.Values.Concat(provinceLabels.Values);
            foreach (var label in labels) label.Visible = false;
            int selected = interaction ? interaction.SelectedProvince : 0;
            if (selected > 0 && provinceLabels.TryGetValue(selected, out Label selectedLabel))
                Place(selectedLabel, world.Labels.SelectedFontSize, world.Labels.SelectedColor, true);
            if (ShowingProvinces)
            {
                foreach (var label in provinceLabels.Values) if (label.Id != selected) Place(label, world.Labels.ProvinceFontSize, world.Labels.ProvinceColor, false);
            }
            else foreach (var label in kingdomLabels.Values) Place(label, world.Labels.KingdomFontSize, world.Labels.KingdomColor, false);
            foreach (var label in labels) if (label.Text.gameObject.activeSelf != label.Visible) label.Text.gameObject.SetActive(label.Visible);
        }
        void Place(Label label, int size, Color color, bool priority)
        {
            Vector3 point = view.WorldToViewportPoint(label.Anchor);
            if (point.z <= 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) return;
            label.Text.fontSize = size; label.Text.color = color;
            Vector2 position = new Vector2(point.x * view.pixelWidth, point.y * view.pixelHeight);
            Vector2 dimensions = new Vector2(label.Text.preferredWidth + 12, size * 1.6f);
            var rect = new Rect(position - dimensions * .5f, dimensions);
            if (!priority) foreach (Rect used in occupied) if (rect.Overlaps(used)) return;
            occupied.Add(rect);
            // Normalized anchors stay aligned to geography even when a render target
            // or window resize changes canvas dimensions later in the render cycle.
            label.Rect.anchorMin = label.Rect.anchorMax = new Vector2(point.x, point.y);
            label.Rect.anchoredPosition = Vector2.zero; label.Rect.sizeDelta = dimensions;
            label.Visible = true;
        }
        public bool IsProvinceLabelVisible(int id) => canvas && canvas.enabled && provinceLabels.TryGetValue(id, out Label label) && label.Text.gameObject.activeSelf;
        public bool TryGetProvinceAnchor(int id, out Vector2 anchor) => TryGetAnchor(provinceLabels, id, out anchor);
        public bool TryGetKingdomAnchor(int id, out Vector2 anchor) => TryGetAnchor(kingdomLabels, id, out anchor);
        static bool TryGetAnchor(Dictionary<int, Label> labels, int id, out Vector2 anchor)
        {
            bool found = labels.TryGetValue(id, out Label label);
            anchor = found ? label.Anchor : default;
            return found;
        }
        void OnDisable() { if (canvas) canvas.enabled = false; }
        public void Clear()
        {
            if (politics != null) politics.Changed -= RebuildKingdoms;
            if (canvas) { canvas.enabled = false; UnityObjectLifetime.Destroy(canvas.gameObject); }
            kingdomLabels.Clear(); provinceLabels.Clear(); canvas = null;
            world = null; politics = null; interaction = null; view = null;
        }
        void OnDestroy() => Clear();
    }
}
