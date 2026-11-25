# ConcertHall

Source: `.unity files/.../APPPLESARCHIVE-ConcertHall.unity` (5168 KB, 2247 GameObjects). Output: `ConcertHall.rrsk` (84 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 238 (19 Unity built-in primitives; 67 LOD1+ and 597 disabled/inactive skipped)
- Matched mesh: 24.7% of non-primitive renderers (50 as R + 4 inside P prefabs); matched material slots: 45.7%
- Records: R 50, P 2, X 154, #U 30, C 575, L 43, A 0, F 20, G 148 (prefab instances: 0)
- Bounds: (-514.216, -22.2827, -138.5338) .. (-100.1848, 72.7474, 301.5075)
- Environment: fog linear, ambient trilight, sky ?1a31117a0bf47c54ca9d9d638feb2ad2
- G kinds: pickup 76, boss 50, spawn 12, door 5, teleport 3, kill_zone 1, trigger_zone 1
- Top unmatched meshes: chest_glow (19), chest_base (19), chest_lid (19), chest_handle (19), WallTorch_LOD0 (10), Quest_Dracula1_Coffin_Simple_01_fbx (9), Coffin_Simple_Lid (9), StageLight_Support (6), Tripod (6), StageLight_Support_Base (6)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 10. F records (ambient effects placed from the install): 20.
- Placed: [VFX_Flame] < [Torch_Wall] -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (kind, size x0.78, sound campfire_02_30_loop) x10; [VFX_Flame] < [Torch_Wall] flame card -> (Sandbox_Candle) / Candle_Tall/FirePlane_fbx (kind) x10
- The room's clip list (AudioClips.asset): none or empty.
