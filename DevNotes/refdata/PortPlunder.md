# PortPlunder

Source: `.unity files/.../APPPLESARCHIVE-PortPlunder.unity` (14821 KB, 8053 GameObjects). Output: `PortPlunder.rrsk` (302 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 932 (18 Unity built-in primitives; 911 LOD1+ and 700 disabled/inactive skipped)
- Matched mesh: 31.9% of non-primitive renderers (221 as R + 71 inside P prefabs); matched material slots: 42.5%
- Records: R 221, P 55, X 431, #U 207, C 2897, L 13, A 5, F 0, G 128 (prefab instances: 0)
- Bounds: (-145.1858, -34.3539, -309.4028) .. (7893.1309, 119.1612, 652.1299)
- Environment: fog linear, ambient trilight, sky ?8191b3ef00208d0449b08ec90b03b4b8
- G kinds: pickup 80, teleport 17, spawn 17, trigger_zone 9, door 4, checkpoint 1
- Top unmatched meshes: Quest_Pirate_BoxSquare (63), [Quest_Pirate1_Beam] (59), Quest_Pirate1_Fern_Mobile (42), Quest_Pirate_Platform (42), Quest_Pirate1_Fern_Desktop (42), PalmTree_Bent_LOD0 (34), PalmTree_Straight_LOD0 (28), Coconuts (27), Ship_Cover (26), OpenCrate_LOD0 (25)

## Effects and sounds (iteration 5)

- Ambient loops (A, loop 1): ?Quest_Pirate1_Ambiance_LocalGulls01 (not in the install) vol 0.7 1-20 m x2; ?PirateQuestBeach02 (not in the install) vol 0.5 1-500 m; ?Quest_Pirate1_Ambiance_PortBellsLocal01 (not in the install) vol 0.9 1-40 m; ?Quest_Pirate1_Ambiance_PortWaves01 (not in the install) vol 0.4 1-50 m
- The room's clip list (AudioClips.asset): none or empty.
