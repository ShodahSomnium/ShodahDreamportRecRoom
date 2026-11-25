# ForestRoad

Source: `.unity files/.../APPPLESARCHIVE-ForestRoad.unity` (5879 KB, 3187 GameObjects). Output: `ForestRoad.rrsk` (59 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 207 (4 Unity built-in primitives; 324 LOD1+ and 364 disabled/inactive skipped)
- Matched mesh: 42.9% of non-primitive renderers (83 as R + 4 inside P prefabs); matched material slots: 55.7%
- Records: R 83, P 6, X 81, #U 39, C 525, L 25, A 4, F 3, G 34 (prefab instances: 0)
- Bounds: (161.4033, -19.7549, -94.7494) .. (371.1086, 11.1855, 177.5591)
- Environment: fog linear, ambient trilight, sky ?1a31117a0bf47c54ca9d9d638feb2ad2
- G kinds: kill_zone 13, spawn 6, teleport 5, door 4, pickup 4, trigger_zone 2
- Top unmatched meshes: Scaffolding_6M_LOD0 (42), StageLight_Support_Base (4), Tripod (4), StageLight_Support (4), bramble_02 (4), Spinner.001 (3), CarriageRideStaticBatch_SpookyTree_Cutout_Mat (2), CarriageRideStaticBatch_WroughtIron_mat (2), [Quest_Dracula1_Horse_Butt_Front] (2), [Quest_Dracula1_BoxRectangle] (2)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 8. F records (ambient effects placed from the install): 3.
- Placed: [VFX_FanWindParticle] < Wind VFX -> [Sandbox_Wind_Wavy_VFX] (kind, size x1) x3
- Come with their P prefab: [Sandbox_Wind_Wavy_VFX] < VFX x2
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_CampfireFlame] < VFX (inactive) x3
- Ambient loops (A, loop 1): ?CBM_CarriageWheel (not in the install) vol 0.75 5.0762-30 m; ?Quest_Dracula1_Ambiance_CarriageRideGeneral01 (not in the install) vol 1 5.3445-18 m; ?Quest_Dracula1_HorsesLocal01 (not in the install) vol 1 6.0117-30 m
- One-shot sources (A, loop 0; the kit turns them into "audio" markers): ?CBM_CarriageExplore on [Quest_Dracula1_Carriage]
- The room's clip list (AudioClips.asset): none or empty.
