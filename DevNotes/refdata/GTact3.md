# GTact3

Source: `.unity files/.../APPPLESARCHIVE-GTact3.unity` (5466 KB, 2314 GameObjects). Output: `GTact3.rrsk` (65 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 179 (1 Unity built-in primitives; 243 LOD1+ and 305 disabled/inactive skipped)
- Matched mesh: 53.9% of non-primitive renderers (64 as R + 32 inside P prefabs); matched material slots: 67.3%
- Records: R 64, P 29, X 68, #U 15, C 598, L 41, A 0, F 29, G 30 (prefab instances: 0)
- Bounds: (-162.5474, -9.6804, -10470.7742) .. (7871.8858, 5207.6402, 664.9313)
- Environment: fog linear, ambient trilight, sky Default-Skybox
- G kinds: spawn 10, trigger_zone 7, enemy_spawner 7, door 6
- Top unmatched meshes: WallTorch_LOD0 (14), Scaffolding_6M_LOD0 (13), Clock_Hand (8), Crenellations_LOD0 (7), Scaffolding_2M_LOD0 (6), OpenCrate_LOD0 (4), FeastTable_LOD0 (3), Clock_Housing_LOD0 (2), Clock_Base_LOD0 (2), Book_stack_LOD0 (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 17. F records (ambient effects placed from the install): 29.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x13; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x13; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop); [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind); Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5)
- The room's clip list (AudioClips.asset): none or empty.
