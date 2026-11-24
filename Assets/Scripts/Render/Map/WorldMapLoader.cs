using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Render.Common;
using UnityEngine;

namespace EggsOfIron.Map
{
    public static class WorldMapLoader
    {
        static bool Numeric(JToken value) => value?.Type == JTokenType.Integer || value?.Type == JTokenType.Float;
        static double Num(JToken value, double fallback = 0) => Numeric(value) ? (double)value : fallback;
        static int Int(JToken row, string key, int fallback = 0) => (int)Num(row[key], fallback);
        static string Str(JToken row, string key, string fallback = "") => row?[key]?.Type == JTokenType.String ? (string)row[key] : fallback;
        static bool Flag(JToken value) => value?.Type == JTokenType.Boolean ? (bool)value : Num(value) > 0;
        static Color ParseColor(string text, Color fallback) => ColorUtility.TryParseHtmlString(text, out Color color) ? color : fallback;
        static int[] Indices(JToken value) => value is JArray array ? array.Select(v => (int)Num(v, -1)).ToArray() : Array.Empty<int>();
        static Vector2 Point(JToken value)
        {
            if (!(value is JArray point) || point.Count != 2 || !Numeric(point[0]) || !Numeric(point[1]))
                throw new FormatException("Expected a numeric [x,y] point");
            return new Vector2((float)point[0], (float)point[1]);
        }
        static IEnumerable<JObject> Records(JObject root, string field, bool required = false)
        {
            JToken table = root[field];
            if (table == null && !required) return Array.Empty<JObject>();
            if (!(table is JArray array) || (required && array.Count == 0) || array.Any(row => !(row is JObject)))
                throw new FormatException($"Expected records in {field}");
            return array.Cast<JObject>();
        }
        public static WorldMapData Load(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException error) { throw new FormatException("Invalid map JSON: " + error.Message, error); }
            if (root["schemaVersion"]?.Type != JTokenType.Integer || Int(root, "schemaVersion") != 2)
                throw new FormatException("Unsupported map schemaVersion. Expected 2.");
            var map = new WorldMapData
            {
                Name = Str(root, "name", "World"),
                Width = (float)Num(root["width"]),
                Height = (float)Num(root["height"])
            };
            if (map.Width <= 0 || map.Height <= 0)
                throw new FormatException("Map width and height must be positive");
            double metersPerMapUnit = Num(root["metersPerMapUnit"], double.NaN);
            if (double.IsNaN(metersPerMapUnit) || double.IsInfinity(metersPerMapUnit) || metersPerMapUnit <= 0)
                throw new FormatException("Map metersPerMapUnit must be a positive finite number");
            map.MetersPerMapUnit = metersPerMapUnit;
            foreach (var row in Records(root, "vertices", true))
            {
                int id = Int(row, "id", -1); if (id < 0 || map.Vertices.ContainsKey(id)) throw new FormatException("Duplicate or negative vertex ID: " + id);
                map.Vertices[id] = Point(row["position"]);
            }
            foreach (var row in Records(root, "cells", true))
            {
                int id = Int(row, "id", -1);
                if (id < 0 || map.CellIndex.ContainsKey(id)) throw new FormatException("Duplicate or negative cell ID: " + id);
                if (!Numeric(row["height"])) throw new FormatException("Cell " + id + " is missing height");
                var cell = new MapCell { Id = id, Height = Int(row, "height"), Biome = Int(row, "biomeId"), Province = Int(row, "provinceId"), InitialState = Int(row, "stateId"), Vertices = Indices(row["vertexIds"]), Neighbors = Indices(row["neighborIds"]), Center = Point(row["center"]) };
                if (cell.Vertices.Length < 3) throw new FormatException("Cell " + id + " has fewer than three vertices");
                cell.Polygon = new Vector2[cell.Vertices.Length];
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                for (int j = 0; j < cell.Vertices.Length; j++)
                {
                    if (!map.Vertices.TryGetValue(cell.Vertices[j], out Vector2 p)) throw new FormatException($"Cell {id} references missing vertex {cell.Vertices[j]}");
                    cell.Polygon[j] = p; min = Vector2.Min(min, p); max = Vector2.Max(max, p);
                }
                cell.Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                // Map cells must be convex; reject incompatible geometry instead of rendering broken fans.
                float winding = 0;
                for (int j = 0; j < cell.Polygon.Length; j++)
                {
                    Vector2 a = cell.Polygon[j], b = cell.Polygon[(j + 1) % cell.Polygon.Length], c = cell.Polygon[(j + 2) % cell.Polygon.Length];
                    float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                    if (Mathf.Abs(cross) < 0.00001f) continue;
                    if (winding != 0 && Mathf.Sign(cross) != winding) throw new FormatException("Non-convex polygon in cell " + id);
                    winding = Mathf.Sign(cross);
                }
                if (winding == 0) throw new FormatException("Degenerate polygon in cell " + id);
                map.CellIndex[id] = map.Cells.Count; map.Cells.Add(cell);
            }
            ReadRegions(root, "states", map.States); ReadRegions(root, "provinces", map.Provinces);
            foreach (var row in Records(root, "biomes"))
            {
                int id = Int(row, "id"); map.BiomeColors[id] = ParseColor(Str(row, "color"), new Color(.5f, .6f, .35f)); map.BiomeNames[id] = Str(row, "name");
            }
            var edgeLookup = new Dictionary<ulong, MapEdge>();
            for (int i = 0; i < map.Cells.Count; i++)
            {
                var c = map.Cells[i];
                if (c.Province > 0 && !map.Provinces.ContainsKey(c.Province))
                    throw new FormatException($"Cell {c.Id} references missing province {c.Province}");
                foreach (int n in c.Neighbors) if (!map.CellIndex.ContainsKey(n)) map.Warnings.Add($"Cell {c.Id} references missing neighbor {n}");
                for (int j = 0; j < c.Vertices.Length; j++)
                {
                    int a = c.Vertices[j], b = c.Vertices[(j + 1) % c.Vertices.Length];
                    ulong key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                    if (edgeLookup.TryGetValue(key, out MapEdge edge)) { if (edge.Right != -1) throw new FormatException("Non-manifold shared edge"); edge.Right = i; }
                    else { edge = new MapEdge { A = a, B = b, Left = i }; edgeLookup[key] = edge; map.Edges.Add(edge); }
                }
            }
            foreach (var row in Records(root, "rivers"))
            {
                var (points, cells) = ReadWaypoints(row, map);
                map.Rivers.Add(new MapRiver { Id = Int(row, "id"), Name = Str(row, "name"),
                    Width = (float)Num(row["width"], .1), Points = points, PathCells = cells });
            }
            foreach (var row in Records(root, "routes"))
            {
                var (points, cells) = ReadWaypoints(row, map);
                string kind = Str(row, "kind");
                if (kind != "road" && kind != "trail" && kind != "sea")
                    throw new FormatException("Unknown route kind: " + kind);
                map.Routes.Add(new MapRoute { Id = Int(row, "id"), Name = Str(row, "name"),
                    Kind = kind, Points = points, PathCells = cells });
            }
            if (map.BiomeColors.Count == 0) map.Warnings.Add("No biome palette found in biomes; using fallback terrain colors");
            var settlementIds = new HashSet<int>();
            foreach (var row in Records(root, "settlements"))
            {
                int id = Int(row, "id", -1);
                if (id <= 0 || !settlementIds.Add(id)) throw new FormatException("Duplicate or negative settlement ID: " + id);
                int cellId = Int(row, "cellId", -1);
                if (!map.TryGetCell(cellId, out var cell) || !cell.Land)
                {
                    map.Warnings.Add("Skipped settlement " + id + ": missing or water cell " + cellId); continue;
                }
                Vector2 position = row["position"] == null ? cell.Center : Point(row["position"]);
                if (position.x < 0 || position.y < 0 || position.x > map.Width || position.y > map.Height)
                {
                    map.Warnings.Add("Skipped settlement outside map: " + id); continue;
                }
                double population = Math.Max(0, Num(row["population"]));
                var settlement = new MapSettlement { Id = id, CellId = cellId, InitialState = Int(row, "stateId"),
                    Name = Str(row, "name", "Settlement " + id), Position = position,
                    Inhabitants = Math.Round(population),
                    Capital = Flag(row["capital"]) };
                map.Settlements.Add(settlement);
            }
            foreach (var row in Records(root, "resources"))
            {
                int cellId = Int(row, "cellId", -1);
                string kind = Str(row, "kind");
                if (kind.Length == 0 || !map.TryGetCell(cellId, out var cell)) continue;
                map.ResourceSites.Add(new MapResourceSite { Id = Int(row, "id"), Kind = kind,
                    CellId = cellId, Position = cell.Center });
            }
            map.TravelNetwork = new SettlementTravelNetwork(map);
            return map;
        }
        static (Vector2[] points, int[] cells) ReadWaypoints(JObject row, WorldMapData map)
        {
            var waypoints = Records(row, "waypoints", true).ToArray();
            if (waypoints.Length < 2) throw new FormatException("A path needs at least two waypoints");
            var points = new Vector2[waypoints.Length];
            var cells = new int[waypoints.Length];
            for (int i = 0; i < waypoints.Length; i++)
            {
                var waypoint = waypoints[i];
                cells[i] = Int(waypoint, "cellId", -1);
                bool hasCell = map.TryGetCell(cells[i], out var cell);
                if (cells[i] >= 0 && !hasCell)
                    throw new FormatException($"Waypoint references missing cell {cells[i]}");
                if (waypoint["position"] != null) points[i] = Point(waypoint["position"]);
                else if (hasCell) points[i] = cell.Center;
                else throw new FormatException("Waypoint requires a position or cellId");
            }
            return (points, cells);
        }

        static void ReadRegions(JObject root, string field, Dictionary<int, MapRegion> target)
        {
            foreach (var row in Records(root, field))
            {
                int id = Int(row, "id", -1);
                if (id < 0 || target.ContainsKey(id)) throw new FormatException("Invalid or duplicate " + field + " ID " + id);
                var region = new MapRegion
                {
                    Id = id,
                    InitialState = Int(row, "stateId"),
                    Name = Str(row, "name", field + " " + id),
                    Color = ParseColor(Str(row, "color"), Color.HSVToRGB(Mathf.Repeat(id * .618034f, 1), .45f, .8f))
                };
                if (field == "provinces" && id > 0)
                {
                    string name = Str(row, "culture");
                    if (!Enum.TryParse(name, out Cultures culture) || !Enum.IsDefined(typeof(Cultures), culture) || name != culture.ToString())
                        throw new FormatException($"Province {id} requires a culture from Cultures.cs");
                    region.Culture = culture;
                }
                target.Add(id, region);
            }
        }
    }
}
