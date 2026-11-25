# GrandHall1-4

Source: `.unity files/.../APPPLESARCHIVE-GrandHall1-4.unity` (8013 KB, 3357 GameObjects). Output: `GrandHall1-4.rrsk` (89 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 456 (7 Unity built-in primitives; 78 LOD1+ and 1011 disabled/inactive skipped)
- Matched mesh: 67.9% of non-primitive renderers (61 as R + 244 inside P prefabs); matched material slots: 39.2%
- Records: R 61, P 47, X 117, #U 34, C 701, L 65, A 0, F 0, G 104 (prefab instances: 0)
- Bounds: (-622.9126, -185.9995, -133.3129) .. (43.286, 186.0856, 106.2258)
- Environment: fog linear, ambient skybox, sky ?678809225b404634d8f8ad6e3f6c4585
- G kinds: pickup 69, door 13, spawn 6, boss 6, teleport 6, checkpoint 4
- Top unmatched meshes: chest_base (17), chest_glow (17), chest_lid (17), chest_handle (17), StageTop (4), FeastTable_LOD0 (4), Quest_Dracula1_Chandelier_fbx (4), chandelier_chain (4), Tripod (3), StageLight_Support (3)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 10. F records (ambient effects placed from the install): 0.
- Placed: none
- Come with their P prefab: [Quest_Dracula1_PumpkinCollectableSmash_VFX] < [Quest_Dracula1_CollectablePumpkin_Small] x2
- The room's clip list (AudioClips.asset): none or empty.
