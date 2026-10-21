# The Veil Arena — layout

Units are meters. X is east/west, Z is north/south, Y is vertical. The same coordinates are used in both realities.

- Shared foundation: 32 × 32 m, floor at Y=0; enclosing 2 m boundary stones with irregular coping.
- Spawn: (0, .15, -9), south landmark arch at Z=-14.
- Shared, unraised combat floor with staggered stone tiles and lighter crossing paths. Cosmetic tiles are combined into two meshes; the floor collider remains smooth. No decorative neon ring.
- Guardian: (0, .1, 5); north landmark arch at Z=14.
- Eight ruined columns at radius 13.8 m, outside the main fighting zone. Both arches provide recognizable north/south landmarks.
- Living cover gate: (-7, 1, 1), 1.4 × 2 × 5 m. Blocks the western route only in Living; shifting opens it.
- Broken cover: three separate chunks near X=7, Z=-.65/1/2.65, active only in Broken.
- Living low platform: (-10, .2, -5), 4 × .4 × 4 m.
- Broken low platform: three separated slabs near (10, .2, 6). Both are step-height, reachable without jumping.
- The common floor persists under all routes/platforms. This is a combat courtyard, not a jumping puzzle. No lethal holes or unrelated destination maps.

Procedural pieces: foundation/tiles, column assemblies, arches, gate slabs, low platforms, crystals. Materials are shared, with one energy material recolored on shifts. Collision solids are on layer 8, `Environment`; cosmetic shapes have no colliders. Actors use CharacterControllers on the default layer. The camera queries only environment obstruction, not the player's own geometry.

Construction lives in `PrototypeSceneArt`, called by `VeilbreakBootstrap` using `PrimitiveFactory`. No concept art files are loaded at runtime. Supplied reference boards informed silhouette, palette, courtyard arches, glowing cores, HUD placement, and the one-boss scope; they are not treated as usable 3D assets.
