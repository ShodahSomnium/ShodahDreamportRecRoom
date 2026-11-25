# iter8 - jumbotron (The Rise of Jumbotron)

Owner: jumbotron agent. Owns CODE/Games/Quests/Jumbotron/*, DN/refdata/apk/Jumbotron_*.rrsk, scenarios jumbotron_*.json.

## Status
- [x] 1. Convert Android levels 38-49 (Quest_Scifi_*) -> DN/refdata/apk/Jumbotron_<Room>.rrsk
- [x] 2. Generate compressed scene data -> Games/Quests/Jumbotron/Scenes/
- [x] 3. Quest code (Foyer lobby, Armory, acts, Stadium boss) + registry hook RegisterJumbotron
- [x] 4. RrGameConsole in the Foyer (ForGame on the Foyer [ScoreboardProjector] (1) marker)
- [x] 5. Compile + harness (jumbotron_run.json): run 4 PASS; harness runs used 4/5 (run 3 aborted on another agent's compile error)

## Log
- 23:50 converted levels 38-48 (level49 Additive = quest systems, kill floor at y -22 / -45; not a room) with DN/tools/apkscene + encounter markers (jumbotron_markers.pl; pipeline jumbotron_convert.sh <work> <scratch apkscene with find edits>). Scenes/RrScene_Jumbotron_*.cs generated (11 files, 22-50 KB).
- Act order = BuildSettings order (exit signs): Foyer (lobby) -> Armory (dressing room, 4 pistols + 4 SMGs) -> Battlefield1 = 1 Armory ("SS Coach", exit CARGO BAY) -> Hallway1 = 2 Cargo Bay -> Hallway2 = 3 Engineering (SERVERS) -> Reception = 4 Bridge -> StadiumConcession = 5 Flight Deck ("Departure", Blast Off! button) -> StadiumEntry = 6 Space -> Garage = 7 Hangar -> Cargo1 = 8 Supply -> Stadium = 9 Jumbotron.

- 00:25 code written (compiles clean in my files): Games/Quests/Jumbotron/ RrJumbotronRoom (quest def, build, hooks, test hooks),
  RrJumbotronArea (one APK room: worker parse, clean-up, kit + terrain grids + door leaves, marker helpers), RrJumbotronActs
  (Foyer, Armory, acts 1-9 waves on the levels' EncountersV2), RrJumbotronBoss (hanging 4-screen Jumbotron + turret; the body
  is a hidden passive SciFiKnightAssassin carrying health 4 x 1300 x party scale; screens are WeakSpot hit volumes pointing at
  it; Reinforcements after each screen), RrJumbotronDoor (framework door on the trigger pad + Rec Room's sliding door_L/R),
  RrJumbotronWeaponSpot (laser weapons at their level poses, 3 s refill), RrJumbotronRegistry.
  Room layout: Foyer at the room origin; room i (0 Armory .. 9 Jumbotron) at ActSlot(i) = ((i%4-1.5)*250, 0, 250*(1+i/4)).
- Scenario: SP/harness/scenarios/jumbotron_run.json (18 shots, console START, Armory -> act 1 fight to clear -> acts 2-9, boss kill).

- 00:55 harness run 1 (1/5): builds (all 11 rooms, potential ~209k credits for 4), console START works, Armory -> act 1
  fight -> CLEAR (9 kills, 6,300 credits) -> act 2 OK. Looks bad: apkscene voxel-shell hull boxes stand in the rooms, pink/cyan
  area-fog glass boxes tint whole views, no walls/ceilings; the Reception arrival had no floor collider (the harness player
  fell below the kill floor -> run failed at act 4). Fix: data now from DN/tools/gtscene prep (the hull as exact plates +
  C box collider plates, stand-ins as G dress; Space cut from 1.16 M backdrop plates to 5.2 k), drawn by my
  RrJumbotronHull (per-material merged quads, install material or colour+install texture; fog/portal/lightshaft/nav left
  out). Kill-floor check now waits 2 s after entering an act.

- 01:40 vertex colours: jumbotron_tools/make_tools.sh builds scratch copies of apkscene (broad find) and gtscene + VtxCol
  (average vertex colour per plate of *VertColor* materials, "c=r,g,b"); jumbotron_convert.sh <work> <tools> uses both.
  The Jumbotron block is rebuilt where Rec Room hangs it (Enemy_Jumbotron_Mat plates y 8.4-13.6 in the gym roof; only
  small plates are dropped there, the roof beams through it stay). Harness run 2 started (2/5).

- 02:05 harness run 2 (2/5): PASS end to end - Foyer console START -> Armory -> act 1 fought to CLEAR (9 kills, 6,300
  credits) -> acts 2-8 -> Jumbotron rises (3,640 HP, party 1; the turret streams lasers) -> KillBoss -> VICTORY. No falls.
  Build 8 s for 11 rooms (hull 386-12,806 plates per room, 10-28 meshes). The rooms now read as Rec Room's (Foyer theatre
  with chairs + the hologram, the Armory's weapon wall and exit pad, the SS Coach port, lockers, the gym arena), lit by
  the env agent's per-scene RrEnvironment (dark blue/teal quest ambients; the Armory and Hangar are very dark).
  Most level textures are level-only (not in the install) -> flat colours; the linoleum floors now use the install's
  Tile_Linoleum_Flecked_Tex.

- 02:40 gtscene upstream now writes plate vertex colours itself (c= of the face under each rectangle) -> make_tools.sh
  skips my VtxCol patch when upstream has it; scenes regenerated. Kit options MaxLights 64, CellSize 12 (as Golden
  Trophy). Harness run 4 (4/5): PASS end to end again (console START, act 1 fought to clear, Jumbotron rises, VICTORY).
  DONE for this iteration. Not built: credits as pickups (kills pay directly), Space's muffled sound and low gravity,
  the Flight Deck projector film (toast + button only), the wiki's exact wave counts per encounter (authored from the
  walkthrough on the levels' own EncountersV2 points/zones), Jumbotron's own art (our face on the screens).

## Facts found
- APK quest levels keep enemy spawns in EncountersV2 (plain Transforms): EnemySpawnPoint*, "Spawn Region" (scale = size),
  Triggers/Overlap Volume (scale = trigger size, e.g. Battlefield1 Encounter1 26.4 x 19.7 x 18.7 m). jumbotron_markers.pl
  extracts them as G enemy_spawner / enemy_region / enc_trigger (+ door, door_trigger, barrel, terminal, portal, pregame,
  button, cinematic). Golden Trophy's APK levels have the same structure (note for the goldentrophy agent).
  goldentrophy agent: jumbotron_markers.pl + jumbotron_tools/make_tools.sh (the apks find) would give acts 7-9 their own
  EncountersV2 spawn points and zones instead of hand-placed groups.
- The Stadium's Jumbotron hangs over the arena centre (RoomGeo/Jumbotron (89.46, 0.76, -96.31), BossSpawnPoint (1) y 2.89,
  its Enemy_Jumbotron_Mat block measured 84.3-94.9 x 5.5-13.3 x -101.5..-91.1): a 4-sided arena screen = the 4 panels.
- The Flight Deck button: [QuestEncounterTriggerButton] (SimplePhysicalButton) at (157.0, 1.43, 54.6) in "Departure".
- No act-clear fanfare clip exists for this quest in the install (ClearFanfare null).

## Requests
- env agent: RESOLVED - RrEnvironment lights every kit scene by its RRSK name while the player is inside it (its table has
  Jumbotron_Foyer .. Jumbotron_Stadium; the activity default is Jumbotron_Foyer). Nothing to call from the quest.
- goldentrophy agent: your gtscene "G plate / platemat / dress" hull records look far better than apkscene's voxel boxes.
  If the runtime that draws them lands in Quests/Shared (or a public static in your folder), tell me here; I can run
  `gtscene prep` on levels 38-48 into DN/refdata/apk/Jumbotron_*.rrsk (my files) and draw them the same way.
- goldentrophy agent (gtscene): the *VertColor* hull materials (Wall_Vertcolor_mat, Cinderblock_VertColor_Mat,
  Concrete_VertColor_Mat - most of the Jumbotron walls) get their colour from the mesh's vertex colours, which the plates
  lose (col=1,1,1). A per-plate (or per plane-group) area-weighted average vertex colour as "c=r,g,b" on G plate would let
  both quests tint them like Rec Room. I multiply it into the material if present (RrJumbotronHull reads c= when there).
