# LowerSanctum

Source: `.unity files/.../APPPLESARCHIVE-LowerSanctum.unity` (11454 KB, 5308 GameObjects). Output: `LowerSanctum.rrsk` (184 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 468 (21 Unity built-in primitives; 352 LOD1+ and 885 disabled/inactive skipped)
- Matched mesh: 11.4% of non-primitive renderers (40 as R + 11 inside P prefabs); matched material slots: 36.3%
- Records: R 40, P 8, X 300, #U 117, C 1565, L 7, A 3, F 58, G 212 (prefab instances: 0)
- Bounds: (-194.1818, -27.6992, 92.0735) .. (22.7883, 35.551, 312.2236)
- Environment: fog linear, ambient skybox, sky ?6b3d093359979024fa50899091b71db3
- G kinds: trigger_zone 89, pickup 80, respawn 23, spawn 10, door 5, teleport 3, boss 2
- Top unmatched meshes: Quest_Pirate_BoxSquare (71), WallTorch_LOD0 (29), Chain.000 (27), Gibbet_Bottom (23), chest_handle (20), chest_base (20), chest_lid (20), chest_glow (20), Quest_Pirate_Plank (18), Quest_Dracula1_Trap_Base_fbx (15)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 29. F records (ambient effects placed from the install): 58.
- Placed: [VFX_Flame] < [WallTorch] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x27; [VFX_Flame] < [WallTorch] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x27; [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x2; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x2
- Ambient loops (A, loop 1): ?Quest_Dracula1_Dungeon_WolfEating02 (not in the install) vol 1 8.4396-23.1366 m; Quest_Pirate1_Caves_LocalDraft01 -> Quest_Pirate1_Caves_GhastlyAmbiance01 (kind) vol 1 1-10 m; Quest_Pirate1_Caves_LocalDraft01 -> Quest_Pirate1_Caves_GhastlyAmbiance01 (kind) vol 1 7.2791-16.124 m
- The room's clip list (AudioClips.asset): none or empty.
