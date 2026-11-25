# GTact1

Source: `.unity files/.../APPPLESARCHIVE-GTact1.unity` (3245 KB, 1480 GameObjects). Output: `GTact1.rrsk` (48 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 130 (1 Unity built-in primitives; 180 LOD1+ and 145 disabled/inactive skipped)
- Matched mesh: 37.2% of non-primitive renderers (44 as R + 4 inside P prefabs); matched material slots: 61.7%
- Records: R 44, P 1, X 68, #U 14, C 449, L 30, A 0, F 17, G 36 (prefab instances: 0)
- Bounds: (15.5485, -4.6539, 19.515) .. (55.6094, 5.6899, 68.4155)
- Environment: fog linear, ambient trilight, sky Default-Skybox
- G kinds: enemy_spawner 14, spawn 11, door 6, trigger_zone 5
- Top unmatched meshes: Scaffolding_6M_LOD0 (13), Crenellations_LOD0 (10), WallTorch_LOD0 (8), Clock_Hand (8), Scaffolding_2M_LOD0 (3), OpenCrate_LOD0 (3), Banner_Vertical (3), Banner_Hrizontal (3), FeastTable_LOD0 (2), Hallway1StaticBatch_Wallpaper_RockWall_Mat (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 11. F records (ambient effects placed from the install): 17.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x6; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x6; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x2; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x2; Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5)
- The room's clip list (AudioClips.asset): none or empty.
