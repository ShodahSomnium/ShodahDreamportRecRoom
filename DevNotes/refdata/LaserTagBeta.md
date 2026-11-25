# LaserTagBeta

Source: `.unity files/.../RRLazerTagBeta-LaserTagBeta.unity` (16795 KB, 8866 GameObjects). Output: `LaserTagBeta.rrsk` (433 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 2150 (635 Unity built-in primitives; 54 LOD1+ and 759 disabled/inactive skipped)
- Matched mesh: 22.8% of non-primitive renderers (316 as R + 29 inside P prefabs); matched material slots: 23.5%
- Records: R 316, P 30, X 1599, #U 206, C 3778, L 7, A 0, F 0, G 182 (prefab instances: 0)
- Bounds: (-86.8486, -65.9144, -122.63) .. (93.37, 18.0001, 75.0671)
- Environment: fog off, ambient skybox, sky Default-Skybox
- G kinds: teleport 108, spawn 32, door 27, pickup 14, enemy_spawner 1
- Top unmatched meshes: Scaffolding_6M (181), [Cardboard_box] (106), MetalShelf (92), Quest_SciFi_CoverWall_End (68), IBeam_10ft_fbx (48), Shelves (40), StageTop (38), StageScaffolding (38), Quest_SciFi_CoverWall_Mid (35), SciFi_GarageDrips (19)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 9. F records (ambient effects placed from the install): 0.
- Placed: none
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_Fog_Vent] < Fog (inactive) x9
- The room's clip list (AudioClips.asset): none or empty.
