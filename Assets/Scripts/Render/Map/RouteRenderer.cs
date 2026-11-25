using UnityEngine;

namespace EggsOfIron.Map
{
    internal sealed class RouteRenderer : System.IDisposable
    {
        readonly MapMeshLayer layer;

        public RouteRenderer(Transform parent, Material material, WorldMapData map, float widthScale)
        {
            layer = new MapMeshLayer(parent, "Routes", material, 21);
            foreach (var route in map.Routes)
            {
                var (width, color) = route.Kind switch
                {
                    "road" => (.8f, new Color(.69f, .52f, .32f)),
                    "sea" => (.35f, new Color(.32f, .52f, .56f)),
                    _ => (.4f, new Color(.56f, .43f, .30f))
                };
                width *= widthScale;
                for (int i = 1; i < route.Points.Length; i++)
                    layer.Segment(route.Points[i - 1], route.Points[i], width, color);
                foreach (var point in route.Points)
                    layer.Disc(point, width * .5f, color);
            }
            layer.Flush();
        }

        public void Dispose() => layer.Clear();
    }
}
