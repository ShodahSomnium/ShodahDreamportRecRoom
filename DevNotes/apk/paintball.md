# Paintball rebuild from the Android layouts (key: paintball)

Started 2026-09-24. Owner files: CODE/Games/Paintball/*, CODE/Games/Paintball/Scenes/*, additive kit changes, paintball_*.json.

## Status
- [ ] read inputs (kit, rrsk format, current Paintball code, research)
- [ ] generate scene data (rrsk2cs)
- [ ] lobby
- [ ] 7 maps + map selection
- [ ] rules
- [ ] look
- [ ] verify (compile, harness)

## Log
- 21:45 inputs read. Decision: maps are BUILT PER MATCH (one at a time, preloaded when the map is selected in the
  lobby, torn down when another map is chosen), at room-local slot (0,0,-560) with the map's flag midpoint there;
  the lobby at the origin (its 8 pregame spawns' centre). Why: 8 levels at once = ~8k renderers + 33k colliders +
  terrain in memory and a long entry build; Rec Room had each map as its own subroom; rr_latest_rgames_action 1.5.9.
- 21:55 DN/tools/pbscene: pbprep.sh (apkscene APKSCENE_DEBUG=1 -> pbprep.awk: X -> "G dress <kind>" markers ->
  refdata/apk/dressed/*.rrsk -> rrsk2cs RRSZ1 --quant 3 -> Games/Paintball/Scenes/RrScene_Paintball_*.cs, 1.08M chars
  for all 8). topdown.awk = SVG plan (render: headless Edge, scratchpad pb/plan.sh).
- 22:05 Core/RrSceneKitTerrain.cs (+ RrSceneKitGrid.cs, RrSceneKitTerrainChunk.cs): H parse + build (chunks, far cut,
  steep cut for mesh grids, clip radius, worker-baked colliders). Not compiled yet.
- Lobby anchors (scene frame): exit ActivityDoor (-52.9,-5.4,57.3); START buttons (-44.4,-4.1,64.3) arena gate and
  (-67.6,-4.3,65.9) shooting gallery (targets at x -70..-78, z 63-67); test wall guns x -66.1 z 70.7-76.9; skins wall
  (-59..-65, 58.7); sprays store (-55..-58, 79.3); lockers; pregame spawns centre (-62,-5.25,76) facing yaw 162.
- 22:50 code written, compile 0/0 (256 files, 113 members proven): Core/RrSceneKitTerrain(+Grid,+TerrainChunk);
  Games/Paintball: RrPaintballLevel (worker parse -> kit -> terrain -> dress -> map data), RrPaintballDress (trees,
  surfaces by kind, merged per 48 m cell), RrPaintballMaps (7 maps table, anchors = flag midpoints, lobby anchor =
  pregame spawn centre), RrPaintballMap (FromMarkers: spawns by nearest flag, flags + flag homes, weapons, kill floor,
  Scoreboard_V2 boards), RrPaintballRoom (rewrite: per-match map build, START only min 2, AcceptHit splash rule,
  FriendlyFire on + victim veto, kill floor out, pickups from the level), RrPaintballRack (level-pose pickups + wall
  press volumes), RrPaintballBoom + RrPaintballThrowable (grenade 3 s fuse r6, mine stick/arm/trip r5, EvBoom),
  RrPaintballGallery (30 s round on 9 targets), RrPaintballLobby (controls at Rec Room's spots). Old River/DriveIn
  builders moved to scratchpad pb/old_paintball (not deleted).
- 22:55 harness run 1 started (paintball_all: 8 lobby shots, 14 map shots, CTF on River).
- 23:05 RUN 1 (all 47 steps ok, 35 s; lobby 3.9 s, each map 0.1-1.6 s): problems seen and fixed for run 2:
  Goblin_Ground_Green_Mat is a patterned quest tile -> terrain now maker-pen grass; renderers named "LOD0"/"LOD1"
  are the trees' crown parts (the kit loaded an unrelated 0.4 m install mesh -> "vines") -> pbprep turns them into
  "G tint <crown material>" (tree colour); "Cube" renderers are Unity's built-in cube (walls, boxes) -> "G dress cube"
  with their material; unknown materials on matched meshes -> the install mesh's own (picnic tables were grey);
  cliffs -> faceted boulders; structures (voxel shells) -> drawn from the level's own box colliders inside them;
  stand-in surfaces by the level material's name (apkscene APKSCENE_DEBUG=1 now also writes "@material", debug
  only, 2 lines in Emit.cs); base boards clustered (voxel boards); kill floor tests the body (Spillway spawns stand
  0.3 m inside its top). Looks OK already: install meshes (Drive-In cars, tyres, rocks), our door in the lobby's
  exit frame, the gallery (standees, bullseyes, START, NOW/BEST sign), pines.
- 23:25 RUN 2 (50 steps ok): lobby reads like the footage (orange picnic tables, orange signs on the arena gate arch,
  chain-link arena, containers, test wall with the bucket logo, our door in the frame); River forts now real walls
  (colliders) with windows. Fixed for run 3: faceted crowns/rocks collapsed to slivers ("vines": seed*0.37 in float
  lost all precision -> seed%1000); pbprep carried a voxel set's material onto later unnamed X (River water drew as
  wood) -> reset + "water" kind; only voxel/bulky struct pieces are replaced by colliders (thin plain pieces stay);
  trees use their own stand-in material colour; console moved beside the arena gate (13.2, 0, -13.8), not in it.
- 23:40 RUN 3 (50 ok): River, Homestead, Clearcut and the lobby now read like Rec Room (autumn aspens, pines, forts,
  barns, silo, bridge, river, boulders). Weak: Spillway (the dam = big pale concrete blocks), Merry Midway (dense
  stall blocks, huge voxel slabs at the edges), Drive-In (25 voxel cubes of the moon). Fixes for run 4: moon/sky
  pieces skipped, unreplaced voxel boxes > 12 m left out beyond 70 m / high above, unknown concrete mid grey.
- 23:42 RUN 4 started (final).
- 23:55 RUN 4 + RUN 5 (final, 50/50 ok each, 52 s): moon cubes gone (Drive-In), far building grids dropped
  (Merry Midway), dark metal lighter. Final PNGs copied to scratchpad pb/final/. Compile --editor 0/0, 113 proven.
  5 harness runs used (limit reached).

## Status (done)
- [x] scene data: 8 Scenes/RrScene_Paintball_*.cs, RRSZ1, 1.09M chars in all
- [x] lobby: level113 + our START (Rec Room's pedestal at the arena gate), console, scoreboard, Rec Center door
      (level's exit frame), test-wall pickups, shooting gallery round, radio = map station
- [x] 7 maps, built per match at (0,0,-560), map vote offers all 7
- [x] rules: START only, min 2, head+torso, splash hurts teammates (grenade/mine/launcher), Drive-In thrower +
      6 mine piles, Splat Hammer in each Merry Midway base, kill floor
- [x] look: install meshes + unknown materials -> install mesh's own; stand-ins by kind/material; structures from
      their colliders; trees; boulders; solid grass terrain
- [x] verify: 5 runs
## Not done / open
- Practice Arena 1 v 1 duel (the arena is there; our START is on its gate pedestal); skins shop is scenery only.
- Merry Midway is the weakest look (41.5 % install match): dense stall blocks, two tall slabs at its sides.
- Spillway's dam is grey concrete boxes (its meshes are all editor-merged).
- Remote players do not see a thrown grenade/mine in flight (only its burst, EvBoom); mines are thrower-side only.
- Not tested with two clients or in VR.
