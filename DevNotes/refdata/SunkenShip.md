# SunkenShip

Source: `.unity files/.../APPPLESARCHIVE-SunkenShip.unity` (12599 KB, 6539 GameObjects). Output: `SunkenShip.rrsk` (243 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 866 (4 Unity built-in primitives; 790 LOD1+ and 611 disabled/inactive skipped)
- Matched mesh: 14.3% of non-primitive renderers (111 as R + 12 inside P prefabs); matched material slots: 53.9%
- Records: R 111, P 9, X 434, #U 309, C 1869, L 30, A 2, F 0, G 175 (prefab instances: 0)
- Bounds: (-383.7373, -581.8326, -1042.9079) .. (2408.0335, 343.9663, 876.1367)
- Environment: fog linear, ambient trilight, sky ?b02f2d13981881f4d86580f5dee8fa3a
- G kinds: pickup 116, teleport 28, spawn 17, trigger_zone 9, door 4, checkpoint 1
- Top unmatched meshes: Quest_RockWall_LOD0 (148), [Quest_Pirate1_Seaweed] (53), Chain.000 (37), [SciFi_Glowstick] (33), chest_glow (29), chest_handle (29), chest_lid (29), chest_base (29), Quest_Pirate1_CoralPlate1 (27), [Quest_Pirate1_CoralPlane1] (27)

## Effects and sounds (iteration 5)

- Ambient loops (A, loop 1): ?Quest_Pirate1_Ambiance_SunkenShip_WaterDrips01 (not in the install) vol 0.45 1-20 m; Quest_Pirate1_Ambiance_RunningWater01 -> Quest_Pirate1_Ambiance_BrookLocal01 (kind) vol 0.25 1-20 m
- The room's clip list (AudioClips.asset): none or empty.
