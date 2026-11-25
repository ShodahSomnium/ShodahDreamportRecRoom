# Iteration 16 — every player sees the same, newest rooms

Problem (user, testing with others): "they see the older rooms, we remade them from the APK remember?"
Cause: rooms that depend on per-player state (Rec Room HTTP cache) or are still iteration-1 hand-built.

## Plan
A. Rec Center: measure the cached Studio scene RecCenter_071825 (1AF.data) into an RRSK (gtscene-style plates,
   materials -> install AG materials / measured colours, lights -> RrLightGrid, C boxes from scene colliders),
   ship as Data/RecCenter_Final.bytes (<= 2.5 MB), build it in the cached building's exact frame.
   Order: live cache (if player has it) -> data build -> old rebuild (only if data missing).
   Fallback if Studio scene can't be converted: Android level7 RecCenter_Builtin via gtscene jt=1.
B. Dodgeball (level11) + Paddleball (level13): gtscene jt=1 -> Data/Dodgeball.bytes, Data/Paddleball.bytes,
   RrJumbotronArea like Soccer/Bowling, gameplay anchors moved onto the level's markers.
C. Audit of anything else per-player (RrHttpCache, persistentDataPath, cache) or hand-built with APK data.

Lead is editing Rr/RrInstall.cs and Core/RrMain.cs install lookup — don't touch.

## Log
- (start) notes created.
- 16:03 B done by a sub-agent (DN/iter16/dodge_paddle.md): Data/Dodgeball.bytes 134 KB, Paddleball.bytes 121 KB,
  RrDodgeballRoom/RrPaddleballRoom on RrJumbotronArea, compile 0 errors. Needs bankscene.sh (done at the end).
- 16:05 A starts: new tool DN/tools/rcscene (rrtool_cache reader + Gt.cs Plates) to convert cache 1AF#1 -> RRSK.
- 16:45 A data WORKS: DN/tools/rcscene (build.sh: dotnet 8 + ../rrtool_cache reader + Gt.cs Plates extracted at build)
  `dotnet DN/tools/rcscene/out/bin/rcscene.dll convert DN/iter16/work/RecCenter_Final.rrsk stats=...` (~6 s).
  1AF#1: 814 renderers (95 off, 286 LOD1+), 112 install R records (couch bounds match, vertex counts differ), 308 ->
  44.8k plates (jt rules; foliage cards Approx, biggest 75 % / max 300 per tree), vertex colours c= (walls cream/blue/
  orange), 1132 colliders (box/sphere/capsule + 2825 mesh-collider C plates), 40 lights (L, key light L dir), terrain
  -> H grid 33x33 2 m, G spot (prefab roots at their drawn centre; [VendingMachine_Static], community BulletinBoard and
  [Door_Sign] geometry left out = the objects the cache path hides/replaces), G sign (6 door texts). B = building only.
  Header name RecCenter_071825 so RrEnvironmentTable's cached-building entry (sun, fog, sky, ambient) applies.
  platemat keys mean= (texture mean, DXT1/5 decoded, alpha-weighted for cut-outs), glow= (emission x map mean), clear=1.
  Runtime: RrJumbotronHull reads them -> new RrJumbotronLook.PlanMeasured (Mean albedo; walls concrete+vc; floor planks;
  brick relief; install tiling/pictures/glass as is; strong glow -> emissive template).
  gtscene render of the hall from the hallway matches the building (DN/iter16/work/rc_hall.png).
- 17:05 Data/RecCenter_Final.bytes 891,346 B (guid 246a4ef8...); RrRecCenterRoom: cache -> BuildFromData (RrJumbotronArea under a frame at RrRecCenterCache.FramePosition/FrameYaw, G spot proxies) -> rebuild; RealBuilding/BuildingName/BuildingFrame; HideOurSigns only with the cache; RrRecCenterFx (light shafts) for data + rebuild. Compile 0 errors.
- 17:20 static-batch winding checked against the vertex normals: 0 submeshes need turning. Floor probe on the data's
  C boxes (gtscene probe): hallway/hall/kiosk/cafe room Y 0, balcony 3.32, theatre -0.78, gym 0.25, projector room 3.13
  = the cached building's (RrRecCenterFit notes). Terrain stays under every floor (theatre: terrain -0.79 < floor -0.69 scene).
- 17:25 bankscene.sh: 50 data files in RrMain's list (RecCenter_Final, Dodgeball, Paddleball new). Compile 0 errors.

## Result
Every player now:
- Rec Center: with Rec Room's cache -> the live cached building (unchanged); without -> the SAME building from
  Data/RecCenter_Final.bytes (891,346 B) in the same frame (doors, props, fit, theatre mirror, spawns identical);
  only if the data file is missing -> the old Dreamport rebuild. Data-build differences from the cache build: plain
  measured colours instead of Rec Room's textures, our door signs instead of theirs (their text is TMP), trees as
  opaque card rectangles, no ambience clips (the install substitutes play), light shafts = RrRecCenterFx's.
- Dodgeball / Paddleball: Rec Room's level11 / level13 (DN/iter16/dodge_paddle.md).
Rebuild the data: `bash DN/tools/rcscene/build.sh; dotnet DN/tools/rcscene/out/bin/rcscene.dll convert
DN/iter16/work/RecCenter_Final.rrsk stats=DN/iter16/work/rc_stats.tsv; bash DN/tools/rrscene/rrsk2bytes.sh
DN/iter16/work/RecCenter_Final.rrsk --name RecCenter_Final; bash DN/tools/rrscene/bankscene.sh`.

## C. Audit: what else depends on per-player state / is still hand-built
- RrHttpCache: only RrRecCenterCache (now with the data fallback).
- The player's own Rec Room PC install: every room loads install meshes/materials/prefabs from it; a player without
  it (or with another Rec Room version) still sees gaps/stand-ins -> the lead's RrInstall/RrMain work.
- persistentDataPath: install.txt (lead), StuntRunner personal best, Maker rooms (local saves, shared p2p), avatar
  outfit (sent to everyone), watch settings (volume, brightness, warmth): per player by design, not room geometry.
- Hand-built while Android data exists: the Dorm (install Maker-Pen dorm kit + our colours; DN/refdata/apk/
  Dorm_Builtin.rrsk exists) - same for everyone, but not Rec Room's level; the Rec Center rebuild (now last resort);
  Soccer/Bowling/DiscGolf/LaserTag/Paintball/RecRally/StuntRunner/quests/Maker templates already use level data.
