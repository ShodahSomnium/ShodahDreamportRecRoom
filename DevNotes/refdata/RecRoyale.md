# RecRoyale

Source: `.unity files/.../RecRoyale__PVP-RecRoyale.unity` (72682 KB, 32027 GameObjects). Output: `RecRoyale.rrsk` (1810 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 5787 (157 Unity built-in primitives; 1557 LOD1+ and 6250 disabled/inactive skipped)
- Matched mesh: 31.8% of non-primitive renderers (1773 as R + 16 inside P prefabs); matched material slots: 56.7%
- Records: R 1773, P 18, X 3577, #U 421, C 16876, L 1, A 11, F 3, G 1346 (prefab instances: 1)
- Bounds: (-3464.6859, -959.4995, -5932.2186) .. (7057.9814, 1335.3989, 4651.8762)
- Environment: fog linear, ambient skybox, sky ?fbe511ed77e3af1429de86a3789a278f
- G kinds: pickup 607, teleport 606, spawn 80, weapon_rack 32, enemy_spawner 10, door 6, trigger_zone 4, prefab_instance 1
- Top unmatched meshes: [RecRoyale_Log_Post_Fence] (268), Cross beam 1 (254), Crossbeam 2 (254), IBeam_LOD1 (178), [Cardboard_box] (164), BoardWall_SupportBottom (84), BoardWall_SupportLeft (84), BoardWall_SupportTop (84), BoardWall_SupportRight (84), BoardWall_Core (84)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 31. F records (ambient effects placed from the install): 3.
- Placed: [RecRoyale_Frontier_FanWindLoop_VFX] < Effects -> [Sandbox_Wind_Wavy_VFX] (kind, size x1); [RecRoyale_Frontier_FanWindWave_VFX] < Effects -> [Sandbox_Wind_Wavy_VFX] (kind, size x1); [VFX_CampfireFlame] < VFX -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (part)
- Come with their P prefab: Particle System < Cap
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_CampfireFlame] < VFX (inactive) x18
- No install effect of that kind (skipped): BaseFoam < [Waterfall] [water] x2; TopSpray < [Waterfall] [water] x2
- Ambient loops (A, loop 1): ?RecRoyale_Sprinkler_Local01 (not in the install) vol 0.14 1-60 m x2; RecRoyale_MountainStream_Local01 -> Quest_Pirate1_Ambiance_BrookLocal01 (kind) vol 0.14 1-40 m x2; WATER_RIVER_FAST_ROCKS_LOOP_STEREO -> Quest_Pirate1_Ambiance_BrookLocal01 (kind) vol 0.22 1-50 m x2; campfire_02_30_loop vol 0.25 1.37-2.0329 m; RecRoyale_LookoutTower_GustLocal01 vol 0.211 1-15 m; RecRoyale_LookoutTower_GustLocal01 vol 0.211 17.24-30.5451 m; RecRoyale_MountainStream_Local01 -> Quest_Pirate1_Ambiance_BrookLocal01 (kind) vol 0.14 1-20 m; RecRoyale_TallPinesWind_Local01 -> RecRoyale_LookoutTower_GustLocal01 (kind) vol 0.11 1-120 m
- The room's clip list (AudioClips.asset): none or empty.
