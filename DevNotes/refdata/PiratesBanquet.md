# PiratesBanquet

Source: `.unity files/.../APPPLESARCHIVE-PiratesBanquet.unity` (19170 KB, 9543 GameObjects). Output: `PiratesBanquet.rrsk` (289 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 1055 (22 Unity built-in primitives; 1882 LOD1+ and 897 disabled/inactive skipped)
- Matched mesh: 60.8% of non-primitive renderers (623 as R + 5 inside P prefabs); matched material slots: 78.1%
- Records: R 623, P 2, X 366, #U 61, C 2789, L 97, A 3, F 2, G 170 (prefab instances: 0)
- Bounds: (-148.3614, -20.5, 0) .. (141.9216, 33.5969, 113.5955)
- Environment: fog linear, ambient trilight, sky ?b45c32b5288ceaf4da015ecc015051af
- G kinds: pickup 80, teleport 38, spawn 22, trigger_zone 10, door 8, weapon_rack 8, enemy_spawner 3, checkpoint 1
- Top unmatched meshes: Quest_Pirate_BoxSquare (93), Book_stack_LOD0 (20), chest_handle (20), chest_lid (20), chest_glow (20), chest_base (20), Lantern_Post_LOD0 (16), Window_Wooden (15), Quest_Pirate_Platform (15), Scaffolding_6M_LOD0 (14)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 6. F records (ambient effects placed from the install): 2.
- Placed: Particle System < [DrinkingFountain] -> (WaterBottle) / Cap/Particle System (kind, size x0.5) x2
- Ambient loops (A, loop 1): ?Quest_Pirate1_Ambiance_ShipCreak01 (not in the install) vol 0.65 1-20 m; Quest_Pirate1_CrowdAmbiance vol 0.25 1-42 m
- One-shot sources (A, loop 0; the kit turns them into "audio" markers): ReceptionBell on [ReceptionBell]
- The room's clip list (AudioClips.asset): none or empty.
