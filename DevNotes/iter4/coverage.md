# iter4 / coverage - progress notes
Task: reference coverage for Dorm, Rec Center, 21 activities. Deliverable: DN/research/rr_reference_coverage.md
Started 2026-09-24.
## Milestones
- [x] 0 notes + deliverable skeleton created
- [x] 1 REF2 inventory (Mesh, .assets, 2D Textures, audio, Fbx/Prefabs/Objs, other)
- [x] 2 install comparison (objects.tsv, audioclips.tsv)
- [x] 3 per-activity rows + sections
- [x] 4 next iterations proposal
- [x] 5 refdata INDEX.md match rates merged
## Scratch
work dir: SP/cov (scratchpad/cov)
## Findings so far (2026-09-24, milestone 1 partial)
- index is in OLD scratchpad: C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad/rrtool/idx ; audioclips.tsv in .../00e4b7e5.../scratchpad/research/
- cov work files in SP/cov: ref2_files.tsv (all REF2 files), ag.tsv (.assets meta guid->mesh name), scene_meshrefs.tsv, scene_mf.tsv, scene_mf_summary.txt, roots.awk, mf.awk, glbb.pl (glb bounds)
- KEY: archive subrooms carry UnityAssetId = Rec Room Studio (RRS) scene bundle ("Cloned from ^RROArchive-rrs"); REF2 .unity = those RRS scenes. Roots are OFFICIAL scene roots (Quest_Hallway1_root, Quest_DeadIsland_root, Quest_Library_root, Quest_Foyer_root, Quest_Dracula1_*_root, Quest_Pirate1_*_root, Orientation_Scene*_root) + RRS registries (MaterialMap, PrefabRegistry, AudioClipRegistry...). => ports of official levels, gameplay markers by name (EnemySpawnPoint, [Quest_Boulder], LavaPathBlocker, Store/Storefront in GoldenLobby = 2020+ merch booth).
- .assets = 6426 Mesh assets only (GUID metas used by scenes). Mesh/ = glb of meshes incl. world-space static-batch chunks of scenes NOT in .unity (Arena_Hangar3, Arena_Cyberjunk_City, Quest_Goblin2_Mines1/Bog/Forest2, StuntRunner_Course3-5, Quest_Scifi_Cargo1, RecRally_Chaparral, DriveIn, paintball2_open, Paintball_Dam_Terrain).
- MakeItToMidnight scene: hand-built (Cubes, Townhouse, Tent, 1605 prefab instances, 0 resolvable meshes) = reconstruction. RRLazerTagBeta: builtin cubes 4236 + DodgeballCourt decal + Scifi cover walls; roots RiverMetrics/MainframeAssaultEncounters = player build.
- [x] milestone 1+2 done (inventory + install compare). Key extra facts: RRLazerTagBeta = pre-release Laser Tag beta (Mainframe Assault/Fuel Run, cut) = OLD;
  RecRoyale__PVP = official Frontier Island (Locales, 455 Chest, 66 SpawnPoint, GameplayZone, CableCar, Gaia); RecRally = official Chaparral (75 [RecRally_Pickup], 5 [Checkpoint]);
  MakeItToMidnight scene = official 2023 layout names but prefabs missing; user cache 35C.data = MakeItToMidnight-Home.unity (Aug 2025, indexed in SP/cacheidx, 1365 meshes).
  Isle scenes have HolsteringTransform (post 2018-12 Rebooty). GoldenLobby has Store/Storefront (2020 merch booth); GTact6 = Quest_Library_root.
  Per-scene % (install/REF2-only/builtin/missing) in SP/cov/scene_pct.tsv. PhysicMats: 29 physics materials (values in doc).
- next: write deliverable sections 0-4 (writing now)
- [x] milestone 3+4: deliverable written (sections 0-4, 6-8). Section 5 waits for DN/refdata/INDEX.md (scenes agent).
- remaining: check refdata INDEX.md; also verify the GT dressing room location and a couple of claims if time.
- [x] milestone 5: refdata INDEX.md merged into section 5 + coverage table geom column + section 4 geometry lines. DONE (2026-09-24).
