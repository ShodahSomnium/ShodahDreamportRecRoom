# BurningBurg

Source: `.unity files/.../APPPLESARCHIVE-BurningBurg.unity` (16513 KB, 7807 GameObjects). Output: `BurningBurg.rrsk` (365 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 354 (8 Unity built-in primitives; 407 LOD1+ and 1411 disabled/inactive skipped)
- Matched mesh: 35% of non-primitive renderers (119 as R + 2 inside P prefabs); matched material slots: 44.6%
- Records: R 119, P 3, X 165, #U 68, C 4319, L 86, A 5, F 0, G 76 (prefab instances: 0)
- Bounds: (6.9429, -28.4659, -133.3129) .. (264.5151, 107.3624, 25.9241)
- Environment: fog linear, ambient trilight, sky ?ab742626a723c93429d7821b5c23b242
- G kinds: teleport 24, pickup 20, spawn 14, trigger_zone 13, door 3, weapon_rack 2
- Top unmatched meshes: Quest_Pirate_BoxSquare (54), Quest_Pirate_Platform (25), Quest_Pirate_Plank (24), Theater_Sandbag_LOD0 (6), Lantern_Post_LOD0 (6), HangingLantern_LOD0 (5), chest_handle (4), chest_lid (4), Scaffolding_6M_LOD0 (4), chest_base (4)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 32. F records (ambient effects placed from the install): 0.
- Placed: none
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_CampfireFlame] < VFX (inactive) x21; Rain < [Quest_Dracula1_TownshipSprinker_VFX] (inactive) x7; [Spray] < VFX (inactive)
- No install effect of that kind (skipped): [Quest_Dracula1_BillowingSmoke_VFX] < VFX [smoke] x3
- Ambient loops (A, loop 1): ?CBM_Siege (not in the install) vol 0.4 1-500 m; Quest_Dracula1_Ambiance_LocalFireBright01 -> campfire_02_30_loop (kind) vol 1 15.334-20 m; Quest_Dracula1_Ambiance_LocalFireBright01 -> campfire_02_30_loop (kind) vol 1 7.7776-12.3823 m; Quest_Dracula1_Ambiance_LocalFireRaging02 -> campfire_02_30_loop (kind) vol 1 42.5576-65.1606 m; Quest_Dracula1_Ambiance_LocalFireRaging02 -> campfire_02_30_loop (kind) vol 1 7.255-11.707 m
- The room's clip list (AudioClips.asset): none or empty.
