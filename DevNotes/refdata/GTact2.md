# GTact2

Source: `.unity files/.../APPPLESARCHIVE-GTact2.unity` (5134 KB, 2266 GameObjects). Output: `GTact2.rrsk` (62 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 146 (1 Unity built-in primitives; 173 LOD1+ and 410 disabled/inactive skipped)
- Matched mesh: 44.1% of non-primitive renderers (34 as R + 30 inside P prefabs); matched material slots: 46.8%
- Records: R 34, P 27, X 66, #U 16, C 508, L 50, A 0, F 19, G 94 (prefab instances: 0)
- Bounds: (41.3911, -9.7709, -69.8578) .. (120.455, 6.9254, -33.9433)
- Environment: fog linear, ambient trilight, sky ?16bf8a3697c6eb5498e48d55fc08f5ea
- G kinds: teleport 40, enemy_spawner 27, door 11, spawn 11, trigger_zone 5
- Top unmatched meshes: Scaffolding_6M_LOD0 (19), Clock_Hand (12), WallTorch_LOD0 (9), Crenellations_LOD0 (4), Banner_Vertical (4), Scaffolding_2M_LOD0 (3), Clock_Base_LOD0 (3), Clock_Housing_LOD0 (3), Banner_Hrizontal (2), OpenCrate_LOD0 (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 12. F records (ambient effects placed from the install): 19.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x8; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x8; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop); [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind); Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5)
- The room's clip list (AudioClips.asset): none or empty.
