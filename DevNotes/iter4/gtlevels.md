# gtlevels - iteration 4 (Golden Trophy on Rec Room's own levels) - DONE 2026-09-24 ~18:20
Scratch: C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/map
(map.dll = top-down PNG of an .rrsk, grid.dll = support-height text grid, probes.dll = reflection-probe boxes from the
REF2 YAML, compiled against DN/tools/rrscene/Scene.cs by path; dbg/ = GT scenes re-written with RRSCENE_DEBUG names).

## Status
- [x] 1. Kit installed: 9 files in CODE/Core (+ metas), RrMain.OnDestroy `RrSceneKit.Shutdown();` after RrTravel.
      scenekit_test run: 9/10; the FAIL was the test's own plain-vs-RRSZ1 compare including the line count (69 inline
      lines with comments vs 64 generated) -> RrSceneKitTest compares records only; later runs log "SceneKitTest: PASS".
- [x] 2. Reference read. KEY FACT: every act's shell (walls/floors/ceilings) is one static batch per material
      ("Hallway1StaticBatch_Wood_Floor_Wide_mat", "...Wallpaper_RockWall_Mat"...) that is in neither REF2 nor the
      install; no NavMesh, no light probes. Rebuilt by hand from the reflection-probe boxes (one per room, probes.dll),
      the heights of props/spawners/pots and the collider walls. WholeFloorBackup boxes (40x3.5x44 etc.) = safety volumes.
- [x] 3. Scene data: GoldenTrophy/Scenes/RrScene_GoldenLobby + GTact1..6 (RRSZ1, 167k chars embedded).
- [x] 4. Code: RrGoldenTrophyShell (floors/ramps, auto walls on a 0.5 m grid, doorways, dark no-shadow ceiling),
      RrGoldenTrophyArea (parse, clean-up, kit build, stand-in dressing, markers, pots, racks, exit door + vestibule),
      RrGoldenTrophyRefActs (lobby + acts 1-6 specs, groups from EnemySpawnPoints, waves), RrGoldenTrophyRoom
      (BuildLevel: lobby + 6 reference acts + research acts 7-9; ActSlot 200 m grid; ShowAreas; TestAct1..6 hooks),
      RrGoldenTrophyLevel (acts 1-6 guesses removed; act 7 barrel knight = charge; act 8 waves in the WIKI order).
- [x] 5. Look: scaffolding -> tiled wood planks, banners/rugs -> red cloth, feast tables -> dark wood, OpenCrate -> wood,
      Book_stack -> book colours, hanging lanterns / lamp posts -> glowing lamps, wall torches -> our torches on the
      nearest rebuilt wall (else left out), Tripod + ProBuilder floor pieces + LightArchShaft beams dropped.
- [x] 6. Harness 5/5 runs: goldentrophy_run x3 (1: 0/42 fail; 2 and 5: 0/43), goldentrophy_final (0/33: all six exit
      doors act 1 -> 7, boulder kills 3, victory), goldentrophy_acts (0/38: fail -> back in the lobby).

## Design decisions
- Everything is built behind the loading card (4.7 s goto in the harness): the framework numbers pots and racks in
  build order (network ids), so lazy building would renumber them. Only the current act + next act roots are ACTIVE;
  the lobby is inactive during a run (RrGoldenTrophyRoom.ShowAreas from QuestStep).
- Act slots: RrGoldenTrophyRoom.ActSlot(i) = x {0, 200, -200}, z 200 * (1 + i / 3); every area within ~650 m.
- Act-local = scene - anchor, anchor = centre of Player7SpawnPoint (1..4) (lobby: Player1..8SpawnPoint centre).
- Tried and dropped: wooden platforms under floating props (collider test) - one walled off act 6's arrival.

## Known issues (next iteration)
- The shells are reconstructions: RR had interior walls, balconies and ledges we do not have. 72 of 80 wall torches
  found no rebuilt wall within 1.2 m (their positions are where RR's walls were: good evidence for a later pass).
  Props that stood on missing ledges float (act 5 west hall prop boxes 3.5 m up, act 2 banners/beams; ceilings make
  the high ones read as rafters). Act 2's big room is mostly empty floor.
- Level potential 141k Gold (4 players) vs RR's ~180k; S (150k) needs the side waves and boss adds.
- Ramps are 17-27 degrees; not walked in VR. Enemy pathing over the rebuilt floors only seen standing.

## Requests to gtrules
(none: SideWave, DressingRoomWall, ModBoard used as documented)
