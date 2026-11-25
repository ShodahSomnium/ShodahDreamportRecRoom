# GoldenLobby

Source: `.unity files/.../APPPLESARCHIVE-GoldenLobby.unity` (8348 KB, 4019 GameObjects). Output: `GoldenLobby.rrsk` (167 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 353 (105 Unity built-in primitives; 426 LOD1+ and 266 disabled/inactive skipped)
- Matched mesh: 59.7% of non-primitive renderers (146 as R + 2 inside P prefabs); matched material slots: 54.3%
- Records: R 146, P 2, X 174, #U 31, C 1783, L 39, A 0, F 24, G 27 (prefab instances: 0)
- Bounds: (77.0815, -9.3476, 25.696) .. (118.9122, 7.6658, 62.8135)
- Environment: fog linear, ambient trilight, sky ?02343ba06fc5e95489bfd33110e841a8
- G kinds: teleport 12, spawn 9, door 4, trigger_zone 2
- Top unmatched meshes: Scaffolding_6M_LOD0 (22), WallTorch_LOD0 (11), Lantern_Post_LOD0 (7), FeastTable_LOD0 (6), Banner_Vertical (6), HangingLantern_LOD0 (5), Rug (5), [Scifi_Rug] (4), Book (3), Crenellations_LOD0 (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 17. F records (ambient effects placed from the install): 24.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x9; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x9; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x2; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x2; Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5) x2
- The room's clip list (AudioClips.asset): none or empty.
