# HighSeas

Source: `.unity files/.../APPPLESARCHIVE-HighSeas.unity` (6357 KB, 3244 GameObjects). Output: `HighSeas.rrsk` (110 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 453 (11 Unity built-in primitives; 289 LOD1+ and 387 disabled/inactive skipped)
- Matched mesh: 10.2% of non-primitive renderers (38 as R + 7 inside P prefabs); matched material slots: 34.4%
- Records: R 38, P 4, X 261, #U 147, C 855, L 20, A 3, F 4, G 113 (prefab instances: 0)
- Bounds: (-240.2201, -25.7203, -97.7464) .. (-4.5012, 23.1754, 70.8435)
- Environment: fog linear, ambient skybox, sky ?49cdcabc1f0d76643ab5572a934f1cfa
- G kinds: pickup 80, teleport 15, spawn 11, door 5, trigger_zone 1, checkpoint 1
- Top unmatched meshes: Quest_Pirate_Plank (39), Quest_Pirate1_Fern_Mobile (29), Quest_Pirate1_Fern_Desktop (29), Quest_Pirate_Waves (23), Quest_Pirate_Waves_LOD1 (23), chest_glow (20), chest_lid (20), chest_handle (20), chest_base (20), Quest_Pirate_Platform (17)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 4. F records (ambient effects placed from the install): 4.
- Placed: [VFX_FanWindWave] < Effects -> [Sandbox_Wind_Wavy_VFX] (kind, size x1) x2; [VFX_CampfireFlame] < VFX -> (Sandbox_Campfire) / VFX/[VFX_CampfireFlame] (part); [VFX_FanWindParticle] < Effects -> [Sandbox_Wind_Wavy_VFX] (kind, size x1)
- Ambient loops (A, loop 1): ?Quest_Pirate1_Ambiance_LocalBigWaveCrashes01 (not in the install) vol 0.65 1-20 m; ?Quest_Pirate1_Ambiance_LocalGulls01 (not in the install) vol 0.85 1-20 m; campfire_02_30_loop vol 0.33 1-16.7301 m
- The room's clip list (AudioClips.asset): none or empty.
