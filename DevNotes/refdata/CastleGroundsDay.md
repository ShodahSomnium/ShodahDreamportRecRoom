# CastleGroundsDay

Source: `.unity files/.../APPPLESARCHIVE-CastleGroundsDay.unity` (12113 KB, 4633 GameObjects). Output: `CastleGroundsDay.rrsk` (227 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 235 (4 Unity built-in primitives; 95 LOD1+ and 1095 disabled/inactive skipped)
- Matched mesh: 26.8% of non-primitive renderers (31 as R + 31 inside P prefabs); matched material slots: 33%
- Records: R 31, P 18, X 121, #U 52, C 2431, L 11, A 1, F 3, G 201 (prefab instances: 0)
- Bounds: (286.1157, -253.6983, -1152.4414) .. (404.8801, -190.2753, -974.1501)
- Environment: fog off, ambient skybox, sky ?143589b3e3b92a946a86ed8cd42d9799
- G kinds: pickup 97, trigger_zone 49, spawn 36, teleport 11, door 8
- Top unmatched meshes: Trunk_Lid (24), Trunk_Latch (24), Trunk_Base (24), Trunk_glow (24), CastleCourtyardStaticBatch_Brick_Red_Mat (4), CastleCourtyardStaticBatch_Concrete_LightGrey_2x_Mat (4), CastleCourtyardStaticBatch_Tile_Pebble_Mat (4), StageLight_Support_Base (3), Tripod (3), StageLight_Support (3)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 51. F records (ambient effects placed from the install): 3.
- Placed: [VFX_CampfireFlame] < VFX -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (part, sound campfire_02_30_loop) x3
- Come with their P prefab: [Quest_Dracula1_PumpkinCollectableSmash_VFX] < [Quest_Dracula1_CollectablePumpkin_Small] x8
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_WaterRing] < [Park_Fountain] (inactive) x8
- Ambient loops (A, loop 1): ?431e3ee85aa482447aab69f4c3537572 (not in the install) vol 0.14 1-30 m
- The room's clip list (AudioClips.asset): none or empty.
