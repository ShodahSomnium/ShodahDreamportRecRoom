# GTact5

Source: `.unity files/.../APPPLESARCHIVE-GTact5.unity` (7925 KB, 3382 GameObjects). Output: `GTact5.rrsk` (93 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 196 (1 Unity built-in primitives; 395 LOD1+ and 539 disabled/inactive skipped)
- Matched mesh: 50.8% of non-primitive renderers (55 as R + 44 inside P prefabs); matched material slots: 59%
- Records: R 55, P 41, X 82, #U 15, C 891, L 67, A 0, F 37, G 34 (prefab instances: 0)
- Bounds: (86.4391, -8.235, -32.4529) .. (172.5934, 6.722, 27.4547)
- Environment: fog linear, ambient trilight, sky Default-Skybox
- G kinds: spawn 18, trigger_zone 6, door 6, enemy_spawner 4
- Top unmatched meshes: Scaffolding_6M_LOD0 (28), WallTorch_LOD0 (18), Scaffolding_2M_LOD0 (14), Clock_Hand (4), LanternPost_LOD0 (3), Banner_Hrizontal (2), HangingLantern_LOD0 (2), Hallway4StaticBatch_LockerFountainStageLight_mat (2), OpenCrate_LOD0 (2), Crenellations_LOD0 (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 21. F records (ambient effects placed from the install): 37.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x16; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x16; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x2; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x2; Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5)
- The room's clip list (AudioClips.asset): none or empty.
