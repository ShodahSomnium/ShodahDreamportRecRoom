# GTact4

Source: `.unity files/.../APPPLESARCHIVE-GTact4.unity` (3873 KB, 1421 GameObjects). Output: `GTact4.rrsk` (42 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 107 (1 Unity built-in primitives; 140 LOD1+ and 355 disabled/inactive skipped)
- Matched mesh: 40.6% of non-primitive renderers (12 as R + 31 inside P prefabs); matched material slots: 43.4%
- Records: R 12, P 28, X 47, #U 17, C 327, L 32, A 0, F 25, G 47 (prefab instances: 0)
- Bounds: (130.6638, -7.7256, 13) .. (191.9585, 10.3359, 81.732)
- Environment: fog linear, ambient trilight, sky Default-Skybox
- G kinds: enemy_spawner 27, spawn 11, door 5, trigger_zone 4
- Top unmatched meshes: Scaffolding_6M_LOD0 (23), WallTorch_LOD0 (12), Rug_Quest (3), Crenellations_LOD0 (3), Banner_Vertical (2), DeadIslandStaticBatch_Wood_Planks_Orange_Mat (1), Banner_Hrizontal (1), DeadIslandStaticBatch_Brick_Painted_DarkRed_Mat (1), DeadIslandStaticBatch_Wallpaper_Dark_Mat (1), DeadIslandStaticBatch_Brick_Painted_MedGrey_Mat (1)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 13. F records (ambient effects placed from the install): 25.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x11; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x11; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop); [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind); [VFX_Lava] < Lava -> VFX_Lava_Bubbles01 (kind, size x2, sound Lava Loop Low)
- The room's clip list (AudioClips.asset): none or empty.
