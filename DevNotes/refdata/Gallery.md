# Gallery

Source: `.unity files/.../APPPLESARCHIVE-Gallery.unity` (12579 KB, 5054 GameObjects). Output: `Gallery.rrsk` (125 KB).

**Verdict: rebuildable from the install.**

- Renderers (active, enabled, LOD0): 654 (15 Unity built-in primitives; 131 LOD1+ and 1663 disabled/inactive skipped)
- Matched mesh: 73.9% of non-primitive renderers (44 as R + 428 inside P prefabs); matched material slots: 44.4%
- Records: R 44, P 75, X 122, #U 60, C 1211, L 126, A 0, F 3, G 109 (prefab instances: 0)
- Bounds: (46.002, -18.2902, 32.1995) .. (116.0019, 15.4938, 242.363)
- Environment: fog linear, ambient trilight, sky ?4441ef96784cc2a4a898283c8fb8d244
- G kinds: pickup 71, teleport 12, spawn 9, trigger_zone 7, door 6, boss 4
- Top unmatched meshes: chest_base (17), chest_handle (17), chest_glow (17), chest_lid (17), Book_stack_LOD0 (13), [Light_Flourescent] (6), FirePlane_fbx (6), [Quest_Dracula1_BoxRectangle] (4), Fountain_Button (3), GalleryStaticBatch_Tile_Ceiling_mat (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 24. F records (ambient effects placed from the install): 3.
- Placed: Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5) x3
- Come with their P prefab: [Quest_Dracula1_PumpkinCollectableSmash_VFX] < [Quest_Dracula1_CollectablePumpkin_Small] x3
- The room's clip list (AudioClips.asset): none or empty.
