# Caves

Source: `.unity files/.../APPPLESARCHIVE-Caves.unity` (17595 KB, 9065 GameObjects). Output: `Caves.rrsk` (205 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 973 (3 Unity built-in primitives; 1178 LOD1+ and 849 disabled/inactive skipped)
- Matched mesh: 42.7% of non-primitive renderers (329 as R + 85 inside P prefabs); matched material slots: 64.1%
- Records: R 329, P 67, X 470, #U 89, C 1498, L 3, A 4, F 0, G 232 (prefab instances: 0)
- Bounds: (-268.398, -67.9357, -118.8618) .. (182.9685, 35.6763, 70.8435)
- Environment: fog off, ambient trilight, sky ?6b3d093359979024fa50899091b71db3
- G kinds: pickup 137, teleport 46, spawn 17, kill_zone 16, door 9, trigger_zone 6, checkpoint 1
- Top unmatched meshes: Quest_RockWall_LOD0 (65), Quest_Pirate_Platform (55), Quest_Pirate_BoxSquare (54), Quest_Pirate_Scaffold1 (40), Quest_Pirate_Scaffold2 (36), Quest_Rock02 (31), chest_handle (29), chest_glow (29), chest_base (29), chest_lid (29)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 12. F records (ambient effects placed from the install): 0.
- Placed: none
- Triggered by gameplay or inactive (not looping on awake; not placed): BaseFoam < [Waterfall] (inactive) x3; TopSpray < [Waterfall] (inactive) x3
- Ambient loops (A, loop 1): Paintball_Dam_Waterfall01 -> Quest_Pirate1_Ambiance_BrookLocal01 (kind) vol 0.45 1-20 m; Quest_Pirate1_Caves_LocalDraft01 -> Quest_Pirate1_Caves_GhastlyAmbiance01 (kind) vol 0.65 1-25 m; Quest_Pirate1_Caves_LocalDraft01 -> Quest_Pirate1_Caves_GhastlyAmbiance01 (kind) vol 1 1-10 m; Quest_Pirate1_Caves_LocalDraft01 -> Quest_Pirate1_Caves_GhastlyAmbiance01 (kind) vol 1 1-18 m
- The room's clip list (AudioClips.asset): none or empty.
