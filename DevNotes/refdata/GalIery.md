# GalIery

Source: `.unity files/.../APPPLESARCHIVE-GalIery.unity` (15359 KB, 7107 GameObjects). Output: `GalIery.rrsk` (231 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 841 (15 Unity built-in primitives; 1286 LOD1+ and 787 disabled/inactive skipped)
- Matched mesh: 51.2% of non-primitive renderers (399 as R + 24 inside P prefabs); matched material slots: 75.3%
- Records: R 399, P 18, X 374, #U 44, C 2156, L 136, A 6, F 6, G 134 (prefab instances: 0)
- Bounds: (17.0718, -572.9158, -1858.8296) .. (1513.8457, 352.8831, 108.9276)
- Environment: fog linear, ambient trilight, sky ?b12cc9a8b5f82a948a17c3ccd4230244
- G kinds: pickup 80, spawn 16, door 15, teleport 10, trigger_zone 9, checkpoint 4
- Top unmatched meshes: Quest_Pirate_Plank (82), Candle_Short_LOD0 (40), Quest_Pirate_BoxSquare (25), chest_handle (20), chest_glow (20), chest_base (20), chest_lid (20), Quest_Pirate1_Hammock (18), Candle_Tall_LOD0 (15), Porthole (13)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 18. F records (ambient effects placed from the install): 6.
- Placed: [VFX_CampfireFlame] < VFX -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (part) x4; Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5) x2
- Come with their P prefab: Particle System < Cap x2
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_Fog_Vent] < Decorative x4
- Ambient loops (A, loop 1): campfire_02_30_loop vol 0.25 1-28 m x2; ?Quest_Pirate1_Ambiance_PanSizzle01 (not in the install) vol 0.25 2.726-11.9184 m; ?Quest_Pirate1_Ambiance_ShipCreak01 (not in the install) vol 0.5 1-20 m; ?Quest_Pirate1_Ambiance_ShipCreak01 (not in the install) vol 0.7 1-20 m; campfire_02_30_loop vol 0.12 1-28 m
- The room's clip list (AudioClips.asset): none or empty.
