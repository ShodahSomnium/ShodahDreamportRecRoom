# RecRally

Source: `.unity files/.../APPPLESARCHIVE-RecRally.unity` (36737 KB, 18286 GameObjects). Output: `RecRally.rrsk` (468 KB).

**Verdict: partly.**

- Renderers (active, enabled, LOD0): 1505 (0 Unity built-in primitives; 3693 LOD1+ and 2886 disabled/inactive skipped)
- Matched mesh: 31.3% of non-primitive renderers (438 as R + 33 inside P prefabs); matched material slots: 30.7%
- Records: R 438, P 30, X 806, #U 228, C 4324, L 63, A 0, F 0, G 401 (prefab instances: 0)
- Bounds: (-3629.0027, -718.8149, -3827.1813) .. (5383.2193, 1902.8751, 5692.0407)
- Environment: fog linear, ambient trilight, sky ?d39861a284c3b754ebec194a0cbdf100
- G kinds: pickup 284, teleport 55, trigger_zone 23, spawn 19, checkpoint 10, door 9, kill_zone 1
- Top unmatched meshes: TrainTrack_LOD0 (166), Cliff_01_LOD0 (163), ArrowSign_LOD0 (107), RecRoyale_Rock_Plateau_fbx (78), Plant_Yarrow_Leaves_LOD0 (64), Plant_Yarrow_Flower_LOD0 (53), RecRoyale_Rock_Cliff_fbx (48), Quest_Pirate_BoxSquare (44), RecRoyale_Rock_Cone_fbx (42), Whole_Closed_Inst_LOD0 (25)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 44. F records (ambient effects placed from the install): 0.
- Placed: none
- Triggered by gameplay or inactive (not looping on awake; not placed): [StuntRunner_Firework_VFX] < [RecRally_StartFinishVisuals] x6; [StuntRunner_Firework_VFX] < [RecRally_WinnersPodiumVisuals] x4
- No install effect of that kind (skipped): BaseFoam < [Waterfall] [water]; TopSpray < [Waterfall] [water]
- The room's clip list (AudioClips.asset): none or empty.
