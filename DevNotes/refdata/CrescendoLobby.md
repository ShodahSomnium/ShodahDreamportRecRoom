# CrescendoLobby

Source: `.unity files/.../APPPLESARCHIVE-CrescendoLobby.unity` (8567 KB, 4416 GameObjects). Output: `CrescendoLobby.rrsk` (82 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 207 (3 Unity built-in primitives; 355 LOD1+ and 617 disabled/inactive skipped)
- Matched mesh: 40.7% of non-primitive renderers (37 as R + 46 inside P prefabs); matched material slots: 37.7%
- Records: R 37, P 11, X 82, #U 42, C 817, L 29, A 1, F 0, G 34 (prefab instances: 0)
- Bounds: (-201.2438, -13.823, 231.3567) .. (-159.191, 14.9254, 281.3872)
- Environment: fog linear, ambient trilight, sky ?1a31117a0bf47c54ca9d9d638feb2ad2
- G kinds: pickup 16, spawn 9, teleport 5, door 4
- Top unmatched meshes: Scaffolding_6M_LOD0 (23), Quest_Pirate_Plank (21), Quest_Dracula1_Gate_Iron_fbx (4), FeastTable_LOD0 (3), TrunkFancy_Latch (3), TrunkFancy_Base (3), TrunkFancy_Lid (3), Candle_Short_LOD0 (2), Quest_Pirate_BoxSquare (2), FoyerStaticBatch_Concrete_LightGrey_2x_Mat (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 9. F records (ambient effects placed from the install): 0.
- Placed: none
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_WaterRing] < [Park_Fountain] (inactive) x8; [VFX_Fireflies] < Decorative (inactive)
- Ambient loops (A, loop 1): CBM_OutdoorAmbience -> AmbianceSummerOutdoor (kind) vol 0.191 49.5222-500 m
- The room's clip list (AudioClips.asset): none or empty.
