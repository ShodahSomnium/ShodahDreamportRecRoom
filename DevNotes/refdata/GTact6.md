# GTact6

Source: `.unity files/.../APPPLESARCHIVE-GTact6.unity` (15659 KB, 6881 GameObjects). Output: `GTact6.rrsk` (219 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 949 (1 Unity built-in primitives; 1922 LOD1+ and 530 disabled/inactive skipped)
- Matched mesh: 63.7% of non-primitive renderers (562 as R + 42 inside P prefabs); matched material slots: 92.4%
- Records: R 562, P 39, X 329, #U 16, C 2169, L 56, A 0, F 23, G 26 (prefab instances: 0)
- Bounds: (-59.2645, -4.5649, -36.6847) .. (-8.7784, 11.0451, 27.0078)
- Environment: fog linear, ambient trilight, sky Default-Skybox
- G kinds: spawn 9, enemy_spawner 8, door 6, trigger_zone 2, boss 1
- Top unmatched meshes: Book_stack_LOD0 (247), Scaffolding_6M_LOD0 (44), WallTorch_LOD0 (11), OpenCrate_LOD0 (6), Crenellations_LOD0 (4), Clock_Hand (4), Scaffolding_2M_LOD0 (4), Clock_Housing_LOD0 (1), LibraryStaticBatch_Unlit_Black_mat (1), CachedProBuilderMesh 130x180 (819c8b04-88a6-412e-9b25-91942c3912ab) (1)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 14. F records (ambient effects placed from the install): 23.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x8; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x8; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x3; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x3; Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5)
- The room's clip list (AudioClips.asset): none or empty.
