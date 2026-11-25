using System;
using System.Collections.Generic;
using Render.Common;
using UnityEngine;

namespace EggsOfIron.Map
{
    public sealed class MapCell
    {
        public int Id, Height, Biome, Province, InitialState;
        public int[] Vertices, Neighbors;
        public Vector2 Center;
        public Vector2[] Polygon;
        public Rect Bounds;
        public bool Land => Height >= 20;
    }
    public sealed class MapRegion
    {
        public int Id, InitialState;
        public string Name;
        public Color Color;
        public Cultures? Culture;
    }
    public sealed class MapResourceSite
    {
        public int Id, CellId;
        public string Kind;
        public Vector2 Position;
    }
    public sealed class MapRiver
    {
        public int Id;
        public string Name;
        public float Width;
        public Vector2[] Points;
        internal int[] PathCells;
    }
    public sealed class MapRoute
    {
        public int Id;
        public string Kind, Name;
        public Vector2[] Points;
        internal int[] PathCells;
    }
    public sealed class MapSettlement
    {
        public int Id, CellId, InitialState;
        public string Name;
        public Vector2 Position;
        public double Inhabitants;
        public bool Capital;
    }
    public sealed class MapEdge
    {
        public int A, B, Left, Right = -1;
    }
    public sealed class WorldMapData : IMapGeography, ISettlementTravel
    {
        public string Name;
        public double MetersPerMapUnit = 1;
        public float Width, Height;
        public readonly List<MapCell> Cells = new List<MapCell>();
        public readonly Dictionary<int, int> CellIndex = new Dictionary<int, int>();
        public readonly Dictionary<int, Vector2> Vertices = new Dictionary<int, Vector2>();
        public readonly Dictionary<int, MapRegion> States = new Dictionary<int, MapRegion>();
        public readonly Dictionary<int, MapRegion> Provinces = new Dictionary<int, MapRegion>();
        public readonly Dictionary<int, Color> BiomeColors = new Dictionary<int, Color>();
        public readonly Dictionary<int, string> BiomeNames = new Dictionary<int, string>();
        public readonly List<MapRiver> Rivers = new List<MapRiver>();
        public readonly List<MapRoute> Routes = new List<MapRoute>();
        public readonly List<MapSettlement> Settlements = new List<MapSettlement>();
        public readonly List<MapResourceSite> ResourceSites = new List<MapResourceSite>();
        internal SettlementTravelNetwork TravelNetwork;
        MapSpatialIndex spatialIndex;
        public readonly List<MapEdge> Edges = new List<MapEdge>();
        public readonly List<string> Warnings = new List<string>();

        public bool TryGetCell(int cellId, out MapCell cell)
        {
            cell = CellIndex.TryGetValue(cellId, out int index) ? Cells[index] : null;
            return cell != null;
        }

        public MapCell Pick(Vector2 position)
        {
            if (spatialIndex == null) spatialIndex = new MapSpatialIndex(this);
            return spatialIndex.Pick(position);
        }

        internal bool IsCoast(MapEdge edge)
        {
            bool rightLand = edge.Right >= 0 && Cells[edge.Right].Land;
            return Cells[edge.Left].Land != rightLand;
        }

        public float SettlementDistance(int firstId, int secondId)
        {
            return (float)(TravelNetwork.Distance(firstId, secondId) * MetersPerMapUnit);
        }
    }
    // Mutable gameplay data. The imported geography remains untouched.
    public sealed class MapPoliticalState : IMapPolitics
    {
        readonly WorldMapData map;
        readonly Dictionary<int, int> owners = new Dictionary<int, int>();
        readonly Dictionary<int, Color> colors = new Dictionary<int, Color>();
        public event Action Changed;
        public MapPoliticalState(WorldMapData data)
        {
            map = data;
            foreach (var p in map.Provinces.Values) owners[p.Id] = p.InitialState;
            foreach (var s in map.States.Values) colors[s.Id] = s.Color;
        }
        public int Owner(MapCell cell) => cell.Land ? (cell.Province > 0 && owners.TryGetValue(cell.Province, out int s) ? s : cell.InitialState) : 0;
        public Color ColorOf(int state) => colors.TryGetValue(state, out Color c) ? c : Color.clear;
        public void SetProvinceOwner(int province, int state)
        {
            if (!map.Provinces.ContainsKey(province) || province == 0) throw new ArgumentException("Unknown province: " + province);
            if (state != 0 && !map.States.ContainsKey(state)) throw new ArgumentException("Unknown state: " + state);
            if (owners[province] == state) return;
            owners[province] = state;
            Changed?.Invoke();
        }
        public void SetKingdomColor(int state, Color color)
        {
            if (state == 0 || !map.States.ContainsKey(state)) throw new ArgumentException("Unknown kingdom: " + state);
            colors[state] = color;
            Changed?.Invoke();
        }
    }
}
