using Render.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EggsOfIron.Map
{
    // A few combined meshes per layer; no cell GameObjects or colliders.
    internal sealed class MapMeshLayer : System.IDisposable
    {
        readonly Transform parent;
        readonly Material material;
        readonly string name;
        readonly int order;
        readonly List<GameObject> objects = new List<GameObject>();
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector2> detail = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        public MapMeshLayer(Transform parent, string name, Material material, int order) { this.parent = parent; this.name = name; this.material = material; this.order = order; }
        public int Vertex(Vector2 point, Color color, Vector2 tex = default, Vector2 data = default)
        {
            int i = positions.Count; positions.Add(new Vector3(point.x, point.y, 0)); colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color); uv.Add(tex); detail.Add(data); return i;
        }
        public void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        public void Polygon(MapCell c, Color color, Vector2 tex, System.Func<int, Color> vertexColor = null)
        {
            Reserve(c.Polygon.Length);
            int start = positions.Count;
            for (int j = 0; j < c.Polygon.Length; j++) Vertex(c.Polygon[j], vertexColor == null ? color : vertexColor(c.Vertices[j]), tex, new Vector2(c.Height / 100f, c.Biome));
            for (int j = 1; j < c.Polygon.Length - 1; j++) Triangle(start, start + j, start + j + 1);
        }
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, bool textured = false)
        {
            Reserve(4);
            int i = Vertex(a, color);
            Vertex(b, color, textured ? Vector2.right : Vector2.zero);
            Vertex(c, color, textured ? Vector2.one : Vector2.zero);
            Vertex(d, color, textured ? Vector2.up : Vector2.zero);
            Triangle(i, i + 1, i + 2); Triangle(i, i + 2, i + 3);
        }
        public void Segment(Vector2 a, Vector2 b, float width, Color color)
        {
            if ((b - a).sqrMagnitude < 0.0000001f) return;
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(a - n, a + n, b + n, b - n, color);
        }
        public void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            Segment(a, b, width, color); Disc(a, width * .5f, color); Disc(b, width * .5f, color);
        }
        public void Disc(Vector2 p, float radius, Color color, int sides = 8)
        {
            Reserve(sides + 1);
            int center = Vertex(p, color);
            for (int j = 0; j < sides; j++)
            {
                float angle = j * (2 * Mathf.PI / sides);
                Vertex(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
            }
            for (int j = 0; j < sides; j++)
                Triangle(center, center + 1 + j, center + 1 + (j + 1) % sides);
        }
        void Reserve(int n) { if (positions.Count + n > 60000) Flush(); }
        public void Flush()
        {
            if (positions.Count == 0) return;
            var mesh = new Mesh { name = name + " geometry", indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(positions); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetUVs(1, detail); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            objects.Add(go); positions.Clear(); colors.Clear(); uv.Clear(); detail.Clear(); triangles.Clear();
        }
        public void Dispose() => Clear();

        public void Clear()
        {
            foreach (var go in objects) if (go) { UnityObjectLifetime.Destroy(go.GetComponent<MeshFilter>().sharedMesh); UnityObjectLifetime.Destroy(go); }
            objects.Clear(); positions.Clear(); colors.Clear(); uv.Clear(); detail.Clear(); triangles.Clear();
        }
    }
}
