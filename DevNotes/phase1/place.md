# place - every room at the world origin; only the current room exists

Status: DONE (2026-09-24 ~13:00). Compiles 0 errors x3, 113/113 proven. 10 real harness runs.

## Milestones
- [x] read ARCHITECTURE hard rules, RrPlacement, RrTravel, RrAssets, RrSpawn, RrSound, RrFx, harness
- [x] RrPlacement -> origin (docs with float32 precision numbers)
- [x] RrTravel: teardown (drop held, clear shared systems, destroy, a frame, census), memory (RrAssets scopes + UnloadUnusedAssets + conservative bundle Unload(false))
- [x] arrival hide/show (scene "Arrival" root in RrMain's scene)
- [x] rescue: client respawn (jump to scene spawn without our teleport) + fall 10 m below room's lowest floor
- [x] Net origin users (RrNetActors, RrNetSelfTest, docs)
- [x] harness frame=room + "arrival": true (RrTravel.StayInArrival) + scenario conversion + travel_origin.json
- [x] compile + harness runs (10 real runs; 2 more aborted in 8-9 s by other agents' mid-edit compile errors)

## Findings (audit)
- Old origins: dorm slot s (-1125+(s%10)*250, 0, -2000-(s/10)*250); instance k (-5000+(k%6)*2000, 0, 2000+(k/6)*2000).
  Offline k = activity-1: RecCenter k1 (-3000,0,2000), Charades k2 (-1000,0,2000), Dodgeball k6 (-5000,0,4000),
  Paddleball k11 (5000,0,4000), Paintball k12 (-5000,0,6000), GoldenTrophy k14 (-1000,0,6000), Dorm slot0 (-1125,0,-2000).
- Leaks outside the room root found:
  1. RrSpawn.Spawn async callback: `parent == null` is true for a DESTROYED parent -> instance lands at scene root (Core, fix).
  2. RrFx effects under WorldRoot, live <=12 s (Core: track + Clear on leave).
  3. RrSound pool one-shots under WorldRoot keep playing (Core: StopWorld on leave; teleport whoosh -> 2D UI source).
  4. RrImpacts decals/fx under "Rr Impacts" (WorldRoot) never cleared on leave (RrGameRoom.OnLocalExit clears projectiles+loadout only) -> RrTravel calls RrImpacts.Clear/RrProjectile.ClearAll/RrLoadout.Clear.
  5. Held props are parented under our hand grip (outside the room) -> survive the room: RrTravel releases both hands before destroying.
- Membership: events/leases/props carry instance object k + generation; presence carries activity+instance. Nothing gates by position.
  Origin-derived: RrNetActors.Origin (InstanceOrigin(k)), RrNetSelfTest, RrTravel.Bind, RrCombat.Origin (room.Origin) -> all 0 now.
- Client respawn (BodyFeature.cs): head y <= -100 -> TeleportToPoint(spawn (0,0.897,0)); after 3 ungrounded falls -> free-fly. Rescue must fire before.
- Harness: scenarios without goto (smoke, interact, combat, watch_*, world_scene, diag_passes) spawn on the Arrival floor; with rooms at origin the auto-home dorm would be built around them -> new scenario key "arrival": true sets RrTravel.StayInArrival before boot.

## Files touched

## Verification
- CODE/Rooms/RrPlacement.cs: every origin = RoomOrigin (0,0,0); SceneSpawn; Inside [Obsolete]; docs with float32 steps.
- CODE/Rooms/RrTravel.cs: scope per room build (RrAssets.Scope), leave = SetArrival(true) -> OnLocalExit -> ReleaseHeld -> teleport
  arrival spot -> ClearShared -> Destroy -> 1 frame -> FreeRoom (RrAssets.Release) -> census; after build MeasureLowestFloor, teleport,
  SetArrival(false), census; failed builds release their scope; teleport whoosh on the 2D source; CheckFooting = fall only (10 m under
  LowestFloor); StayInArrival for the harness.
- CODE/Rooms/RrTravel.Origin.cs (NEW partial): arrival toggle, ReleaseHeld, ClearShared (RrFx, RrSound, RrImpacts, RrProjectile,
  RrLoadout), FreeRoom (memory log), Census, MeasureLowestFloor, WatchRespawn (per frame: >3 m one-frame jump onto (0,0.9,0) +-2 m/0.35 m
  without our teleport in 2 s), Rescue (RrHealth.RespawnPose in a match, else RrRoom.RescuePose).
- CODE/Rooms/RrTravelTest.cs (NEW): SimulateFall, SimulateClientRespawn, Report (harness hooks).
- CODE/Rooms/RrTravelDriver.cs: WatchRespawn every frame.
- CODE/Rooms/RrRoom.cs: LowestFloor, AssetScope, virtual RescuePose; docs.
- CODE/Core/RrAssets.cs: scopes (Scope, NewScope, Tag), Release (forget keys/prefab data, clear loader requests, RrMaterials.Release,
  UnloadUnusedAssets, Unload(false) of bundles only the room opened when idle + all handed-out assets dead + no audio/video/shader/font).
- CODE/Core/RrMaterials.cs: ReplacedScope + Release(scope).
- CODE/Core/RrSpawn.cs: skip spawns whose parent was destroyed before the load finished (was landing at the scene root). SkippedOrphans.
- CODE/Core/RrFx.cs: "Rr Effects" holder + Clear().
- CODE/Core/RrSound.cs: StopWorld(), PlayUi(AudioClip).
- CODE/Player/RrPlayer.cs: LastTeleportAt/DoneAt/Target, SimulateExternalMove (harness).
- CODE/Net/RrNetActors.cs (Origin = RoomOrigin), RrNetSelfTest.cs, RrNet.cs + RrInstanceWire.cs (docs).
- SP harness RrScenarioRun.cs: shot "frame": "room"; scenario "arrival": true -> RrTravel.StayInArrival.
- Scenarios converted (room frame, old origin subtracted): rooms_travel (5 of 6; rooms_loading_card_b kept world), dorm_room 9, dorm 2,
  reccenter_tour 17, paintball_all 15, charades_play 11, charades_lightning 5, dodgeball_gym 7, paddleball_court 6, goldentrophy_run 10,
  goldentrophy_acts 9, goldentrophy_final 6. "arrival": true: smoke, interact, combat, diag_passes, world_scene, watch_fix, watch_menu,
  player, watch_diag. NEW travel_origin.json. Backup of the originals: 1c09.../scratchpad/work/scenarios_before/.

## Verification (harness)
- compile: 0 errors, 113/113 proven (several times).
- run travel_origin (#1 real run, 11:10): 39/39 steps ok. Census every stop: Dorm/RecCenter/Paintball at (0,0,0), "no room, nothing of it
  left" after each leave; world root outside-room count 312 -> 312 -> 352 -> 379 (pools grow); memory freed: Dorm 581->579 MB,
  RecCenter 649->600, Paintball 621->597 (editor numbers); bundles unloaded 5/4/2 per room; fall rescue + client-respawn rescue both
  worked in the Rec Center; 5 Paintball splats cleared on leaving; PNG to_dorm_2_river (Dorm, looking where the River splats were) = sky.
- run rooms_travel (#2): 17/18; FAIL "placeholder RecCenter built" = stale scenario (the real Rec Center replaced the placeholder) ->
  scenario fixed to "RecCenter: built"; rooms_loading_card moved to the real Rec Center's head (0,1.65,-24.2) (was placeholder spawn).
- two runs aborted in 8-9 s by other agents' mid-edit compile errors (RrIsolation.cs, RrAvatarHandTest.cs): not counted.
- run dorm_room (#3): 18/19; the one FAIL (basket SCORED) is the same as the baseline. PNGs vs baseline: mean abs diff 0.05-0.26/255.
- run reccenter_tour (#4): 39/39 (baseline 39/39). PNGs 0.02-0.10 except rc_lobby 0.36 / rc_lobby_board 0.69: the lobby arch trim
  z-fights with the wall ends/soffit (flush faces; the winner changed at the origin: speckled jamb, grey lintel). Fixed in
  Places/RecCenter/RrRecCenterShell.cs (trim 2 cm proud: -3.98 / 3.98 / y 3.98) - verified in travel_origin re-run (see below).
- run paintball_all (#5): 29/29 (baseline 29/29). PNGs 0.00-0.20.
- PNG diff tool: 1c09.../scratchpad/work/pngdiff.ps1 <scenario> (System.Drawing, every 6th pixel).
- run charades_play (#6): 23/23 (baseline 23/23). PNG diffs <=0.15 except ch_podium_* 1.5 (button layout; RrCharadesRoom.cs was edited
  at 07:57, after the baseline run at 07:56:44) and ch_board_correct 0.7 (random word SPOON vs HOUSE).
- run dodgeball_gym (#7): 20/20 (baseline 20/20). Diffs 0.2-1.4: the "Info Strip" under the scoreboards is new (RrDodgeballRoom.cs edited
  07:59, baseline ran 07:58:19). Same gym otherwise.
- run paddleball_court (#8): 18/18 (baseline 18/18). Diffs 0.2-0.8 (serve shot 7.2 mean: the camera is inside the green translucent
  tube; overall tint slightly brighter, same geometry).
- run goldentrophy_run (#9): 34/34 (baseline 34/34). Diffs vs the 07:55 baseline are large because the quest was re-textured after it
  (flagstone walls, goldentrophy_final 08:02); vs the far-origin goldentrophy_final PNGs of 08:03 (same shot positions): 0.05-0.73.
  Lowest floor y -2.5 (705 colliders).
- run travel_origin (#10, final): 40/40. Same census as run 1; rescue fall + client respawn ok; Rec Center arch trim now clean orange
  (to_reccenter, to_reccenter_board). Scene rootCount 7 -> 8 (Dorm) / 9 (Paintball) -> 7 after leaving, but GetRootGameObjects shows no
  root outside the start set: Unity-counted objects that are not listed (hidden), gone with the room.
- Copied RrScenarioRun.cs -> DN/tools/harness/RrHarnessTests/, 22 scenarios (mine) -> DN/tools/harness/scenarios/.
- Also touched: CODE/Places/RecCenter/RrRecCenterShell.cs (arch trim 2 cm proud; 3 lines + comment).
