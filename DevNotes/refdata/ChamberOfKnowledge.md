# ChamberOfKnowledge

Source: `.unity files/.../APPPLESARCHIVE-ChamberOfKnowledge.unity` (12851 KB, 5719 GameObjects). Output: `ChamberOfKnowledge.rrsk` (184 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 565 (19 Unity built-in primitives; 171 LOD1+ and 1947 disabled/inactive skipped)
- Matched mesh: 45.1% of non-primitive renderers (64 as R + 182 inside P prefabs); matched material slots: 31.8%
- Records: R 64, P 63, X 261, #U 58, C 1639, L 138, A 0, F 1, G 281 (prefab instances: 0)
- Bounds: (-270.2042, -21.278, 117.1658) .. (-107.2953, 10.7073, 297.8857)
- Environment: fog linear, ambient trilight, sky ?4441ef96784cc2a4a898283c8fb8d244
- G kinds: pickup 217, teleport 16, spawn 16, button 13, trigger_zone 8, door 8, checkpoint 2, boss 1
- Top unmatched meshes: chest_glow (54), chest_lid (54), chest_handle (54), chest_base (54), [Quest_CoiledWires] (12), RecCenter_Chair_LOD0 (8), Quest_Dracula1_Bookcasee_Walkable_fbx (6), [Quest_Dracula1_BoxRectangle] (6), Quest_Dracula1_Fence_Iron_fbx (4), LibraryStaticBatch_legacy_Quest_Dracula1_Wall_Painted_Vertcolor_Mat (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 1. F records (ambient effects placed from the install): 1.
- Placed: [VFX_CampfireFlame] < VFX -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (part, sound campfire_02_30_loop)
- The room's clip list (AudioClips.asset): none or empty.
