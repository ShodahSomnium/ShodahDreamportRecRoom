# IsleLobby

Source: `.unity files/.../APPPLESARCHIVE-IsleLobby.unity` (6617 KB, 3407 GameObjects). Output: `IsleLobby.rrsk` (120 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 295 (55 Unity built-in primitives; 301 LOD1+ and 327 disabled/inactive skipped)
- Matched mesh: 49.2% of non-primitive renderers (102 as R + 16 inside P prefabs); matched material slots: 51.8%
- Records: R 102, P 14, X 126, #U 51, C 1264, L 26, A 1, F 2, G 21 (prefab instances: 0)
- Bounds: (-283.5228, -8.8811, 134.733) .. (-54.1431, 42.9132, 250.9386)
- Environment: fog linear, ambient trilight, sky ?13a4f7f46bcdb844bae6aa56299d61ba
- G kinds: spawn 13, door 3, trigger_zone 2, pickup 2, teleport 1
- Top unmatched meshes: Scaffolding_6M_LOD0 (22), Quest_Pirate1_Fern_Desktop (8), Coconuts (5), PalmTree_Bent_LOD0 (4), Quest_Pirate_BoxSquare (4), FeastTable_LOD0 (4), Quest_Pirate1_MooringLine (3), Candle_Tall_LOD0 (3), Tripod (3), StageLight_Support (3)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 7. F records (ambient effects placed from the install): 2.
- Placed: Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5) x2
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_Fireflies] < Decorative
- Ambient loops (A, loop 1): PirateShoppingChannel vol 0.85 1-20 m
- The room's clip list (AudioClips.asset): none or empty.
