# OrientationStageEnd

Source: `.unity files/.../APPPLESARCHIVE-OrientationStageEnd.unity` (4686 KB, 1826 GameObjects). Output: `OrientationStageEnd.rrsk` (66 KB).

**Verdict: mostly stand-ins.**

- Renderers (active, enabled, LOD0): 254 (0 Unity built-in primitives; 603 LOD1+ and 144 disabled/inactive skipped)
- Matched mesh: 9.1% of non-primitive renderers (23 as R + 0 inside P prefabs); matched material slots: 20.3%
- Records: R 23, P 1, X 217, #U 14, C 738, L 16, A 1, F 0, G 12 (prefab instances: 0)
- Bounds: (-121.8673, -13.9289, 193.1779) .. (-61.1093, 13.7431, 220.8989)
- Environment: fog off, ambient skybox, sky Default-Skybox
- G kinds: spawn 5, pickup 3, trigger_zone 2, door 2
- Top unmatched meshes: Balloon_LOD0 (131), BalloonString_LOD0 (30), Ottoman_Rectangle_LOD0 (6), Rarity_Stamp (5), Ottoman_Chair_LOD0 (4), CachedProBuilderMesh 4x6 (5b1ac66c-82b6-499d-b936-dbc7242bff01) (4), Ottoman_Cube_LOD0 (4), Light_PendantLarge_LOD0 (4), RecCenter_Chair_LOD0 (3), Vase_Tall_LOD0 (3)

## Effects and sounds (iteration 5)

- Particle systems in the scene: 9. F records (ambient effects placed from the install): 0.
- Placed: none
- Come with their P prefab: [VFX_ObjectSpawn] < GiftMoment
- Triggered by gameplay or inactive (not looping on awake; not placed): [VFX_Confetti_Orientation] < GiftMoment
- Ambient loops (A, loop 1): ?AvatarCreate_Loop_Short_2 (not in the install) vol 0.169 1-40 m
- The room's clip list (AudioClips.asset): none or empty.
