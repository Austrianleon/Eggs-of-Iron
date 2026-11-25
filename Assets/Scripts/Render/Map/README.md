# World maps in Unity

## Display the supplied map

1. Allow Unity to import the scripts and shaders.
2. Open `Assets/Scenes/WorldMapDemo.unity` and press Play. To recreate it, choose **Tools > Eggs of Iron > Create World Map Demo Scene**. This creates `Assets/Scenes/WorldMapDemo.unity`, separate from SampleScene.
3. Alternatively, choose **GameObject > Eggs of Iron > World Map** in an existing scene. The menu assigns the bundled Tria map and both shaders. Assign another world map TextAsset in the Inspector to replace it.

The map root must have identity position, rotation, and scale. Coordinates use one Unity world unit per map unit. The camera faces the XY plane from negative Z. Use the project's URP **2D Renderer**: these shaders explicitly use `Universal2D` passes. A Forward Renderer needs a matching shader pass; it is not the project's configured renderer.

Terrain, opacity (default 0.3), borders, rivers, settlements, and camera controls are Inspector settings. Kingdom borders have a thick gold stroke (1.25 map units) with dark casing and rounded joins; region/province borders are thin (0.16 units). Settlement markers use ivory dots for towns and larger gold diamonds for capitals. Kingdom names appear in the full-map view; province names replace them below `Labels.ProvinceZoomThreshold` (default orthographic size 90). The selected province name stays visible at every zoom while its anchor is in view. Fonts, colors, and the threshold are Inspector settings. Names are outlined and overlapping labels are suppressed, with selection taking priority. Labels anchor to land in the largest connected part of each territory and update after ownership changes. Toggle them with `Settlements.Show` and adjust their radii, population scaling, and colors in the Inspector; regenerate to apply changes. Camera settings live on `MapCameraController`; the setup menu adds it to the main camera. Regenerate in Play mode after changing geometry/color/border settings. The default dark fantasy palette uses deep teal seas, muted earthy biomes, gold kingdom borders, and subdued political hues. `LandBrightness` and `LandSaturation` tune terrain in the Inspector. Vertex colors are converted correctly for the project color space. Overlay opacity and procedural detail settings update live. Assign both shader assets on the controller to retain them in player builds. Add the demo scene to build settings yourself if needed.

Controls: WASD movement, middle mouse drag, wheel zoom toward the cursor, left click selection. Zoom is exponential; both limits and movement speed are configurable. `LimitZoomOutToMap` caps zoom-out at the size needed to fit the entire map, also respecting `MaximumZoom`. The limit adapts when the window aspect ratio changes; the demo has an additional maximum orthographic size of 240. The view is constrained to the map; if the viewport is larger than a map dimension, it is centered along that dimension. A procedural deep-ocean rectangle fills areas omitted from the map mesh; water shading fades from shallow blue near land to deep blue offshore.

## World map JSON

Assign a `.map.json` TextAsset to **Map Json** on the world map controller, or leave it empty to load the bundled [Tria.map.json](../../../Resources/Tria.map.json). Version 2 uses these fields.

| Field | Contents |
| --- | --- |
| `schemaVersion` | Required integer, currently `2` |
| `name`, `width`, `height` | Map name and dimensions in Unity world units |
| `metersPerMapUnit` | Required positive distance in meters represented by one Unity world unit |
| `vertices` | Records with `id` and `position` |
| `cells` | Records with `id`, `center`, `height`, `biomeId`, `stateId`, `provinceId`, `vertexIds`, and `neighborIds`. Heights of 20 or more are land. Vertices form a convex polygon. |
| `biomes`, `states` | Records with `id`, `name`, and an HTML `color` |
| `provinces` | Region records that also include `stateId` and one `culture` from `Cultures.cs` |
| `routes`, `rivers` | Named paths with ordered `waypoints`. Routes have a `kind` of `road`, `trail`, or `sea`. Rivers have a `width`. |
| `settlements` | Records with `id`, `name`, `cellId`, `stateId`, `position`, `population` in people, and a Boolean `capital` |
| `resources` | Records with `id`, `cellId`, and `kind` |

All points are `[x,y]` in Unity's XY plane with Y increasing upward. A waypoint accepts a `position`, a `cellId`, or both. A cell-only waypoint uses the cell center. A position-only waypoint uses spatial picking to identify its cell for travel connections. Each path needs at least two waypoints.

Tria has 23 Human, 23 Elf, 23 Gnome, and 22 Ogre provinces. Culture is stored directly on each province. Hover over or select a province to see it.

## Runtime integration

Gameplay systems can accept the query interfaces in `Render.Common`. Use `IMapGeography` for cell lookup and position picking, `ISettlementTravel` for travel distances, and `IMapPolitics` for ownership, colors, and change notifications. Pass the controller's `Data` for geography and travel, and its `Politics` for political queries. These objects are replaced when the controller regenerates the map.

`MapSettlement.Inhabitants` exposes the population in people. `map.SettlementDistance(firstId, secondId)` returns the shortest distance in meters over roads, trails, sea routes, and rivers. Travel works in both directions, with transfers and settlement access through waypoints in the same cell. A disconnected pair returns `float.PositiveInfinity`. Unknown settlement IDs throw `KeyNotFoundException`.

**Show Routes** and **Route Width Scale** control the route layer. **Show Resources** controls the coal, iron, gold, nite, farmland, forest, and fish markers. Regenerate the map to apply these settings.

```csharp
using EggsOfIron.Map;
using UnityEngine;

// Obtain this from an Inspector field or your gameplay initialization.
WorldMapController map = GetComponent<WorldMapController>();
map.SetProvinceOwner(provinceId: 1, kingdomId: 2);
map.SetProvinceOwner(provinceId: 1, kingdomId: 0); // unclaimed
map.SetKingdomColor(kingdomId: 2, color: Color.magenta);
var interaction = map.GetComponent<MapInteractionController>();
interaction.ProvinceSelected += id => Debug.Log($"Selected province {id}");
int hoveredKingdom = interaction.HoveredKingdom;
```

Call these after generation, e.g. from a selection handler or after the map's Start has run. Use `Data` for imported geography and region definitions, `Politics.Owner(cell)` for current ownership, and `Politics.ColorOf(id)` for current kingdom colors. Imported definitions retain their original ownership. `Politics.Changed` signals changes. Selection ID 0 means none. Names and owners appear in a minimal built-in GUI panel; disable `ShowNames` to supply your own UI. Pointer interaction respects EventSystem UI blocking.

## Rendering and performance

- `WorldMapData`: imported geography and metadata; `MapPoliticalState`: mutable ownership/colors.
- `TerrainRenderer`: cached combined meshes, shared land/water vertex color averaging, biome palette, elevation shading. The deterministic shader draws mountain ridges/snow, clustered forest canopies, desert dunes, and terrain grain. These are stylized procedural surface details, not sprite assets or actual 3D relief. A deep-ocean backing mesh fills the map rectangle beyond the mesh boundary. Ocean shading samples a coastline distance texture shared by the water cells and the backing mesh. Coast geometry follows the exact shared Voronoi edges; no independent smoothing moves adjacent polygons apart.
- `PoliticalOverlayRenderer`: land-only geometry and point-filtered per-cell palette texture. Runtime changes upload palette colors; terrain and overlay meshes stay intact. No tint is applied to water or neutral cells.
- `BorderRenderer`: shared-edge topology supports disconnected territories automatically. Runtime political changes rebuild combined political border meshes in O(edges); static coastlines stay cached. Province borders are suppressed wherever a kingdom border occupies the same edge.
- `RiverRenderer`: combined rounded line geometry. Width scale is independent of ownership.
- `MapLabelController`: one camera-space UI canvas with outlined labels per named kingdom/province, using Unity’s built-in font. No external font assets or per-cell label objects.
- `SettlementRenderer`: combined marker geometry, no per-settlement GameObjects. Town size scales mildly with population; capital diamonds stay distinct at full-map zoom.
- Render order: terrain 0, politics 10, rivers 20, coasts 22, province borders 23, kingdom casing 24, kingdom strokes 25, hover 30, selection 31, settlements 40. Transparent layers use standard alpha blending, no depth writes, and two-sided XY meshes.
- `MapSpatialIndex`: uniform buckets covering cell bounds, then exact point-in-polygon testing; no per-cell GameObjects or colliders. Highlight geometry is rebuilt only when hover/selection changes, covering all cells of a province.
- Mesh chunks stay below 60,000 vertices with 16-bit indices. Regeneration and destruction dispose generated meshes, textures, and materials. Geometry is retained in memory while active, not serialized to a disk cache.

Parsing and mesh construction run synchronously on the main thread at generation time; very large maps may require a loading screen. Ownership changes currently scan cells and edges rather than maintaining dirty-edge ranges; this keeps the implementation simple at the supplied map's scale. The JSON reader parses the full document before extracting map data, so peak memory includes the JSON tree.

## Validation

Use **Tools > Eggs of Iron > Validate World Map** to check map data, shortest-path distances, province cultures, rendering resources, selection, and regeneration.

Headless equivalent:

```sh
Unity -batchmode -nographics -quit -projectPath <project> -executeMethod EggsOfIron.Map.Editor.WorldMapValidation.Run -logFile <log>
```
