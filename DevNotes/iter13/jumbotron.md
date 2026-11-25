# Iteration 13 - jumbotron: floating objects in The Rise of Jumbotron

User (after testing iter 11/12 in the headset): "There are still some random things floating in Rise of Jumbotron, please look."

## Plan
1. Read iter11 notes (geometry/layout/jt11_result), iter12 notes if any, the pipeline (jumbotron_convert.sh, gtscene jt=1,
   jumbotron_markers.pl) and the runtime (RrJumbotronHull/Look/Haze/Area) to learn what gets drawn: hull plates, dress boxes,
   kit renderers (install props), haze/shafts.
2. Write an offline support test: for every drawn piece (plate group / box / kit prop) in each of the 11 rrsk files, check
   whether something else is within a few cm below/beside/above it. List the unsupported ones with position/size/source.
3. Classify each floater (inactive/animated object, glow stick without string, lamp fixture offset, LOD/duplicate, Space
   debris, haze/shaft, voxel shell of a hanging prop) and fix in data (gtscene jt=1 / convert) or runtime.
4. Regenerate the 11 rooms, keep size <= 3.26 MB, Golden Trophy byte-identical, compile check 0 errors.
5. Log findings + what was done here.

## Log
- (1) read: rooms draw R (install meshes via kit), P (WaterBottle only), G plate (hull quads; skipped: NotASurface fog/portal/
  shaft/nav + Look.Plan skip: Wires_Mat, LockIcon, GarageMarkings, PingPongTable_Net, EventRoom_Moon, StageLight_Glow*),
  G dress (stand-in boxes), haze (fog blobs, portals, rebuilt shafts), door leaves. No H/X in jt rooms. apkscene already
  drops inactive-in-hierarchy renderers + LOD1+. NOTE Wires_Mat is SKIPPED at runtime -> hanging things lose their cables.
- (2) support test written: NSP/jt13/fl (Floaters.cs, run.sh, all.sh <rrsk dir> <out dir>): every drawn piece (R install-mesh
  bounds, P, drawable G plate quads, drawable G dress boxes) as an oriented box, SAT touch within 4 cm, union-find components;
  the biggest = the room, every other = a floater with its gap to the nearest piece and the drop below it. Baseline NSP/jt13/fl/r0.
  Candidates (big ones): Concession 6 floating StageTop platforms (15 m2 each) + ~45 glow-stick boxes in the air + projector
  curtains; Stadium 4 Enemy_Jumbotron chunks (y~7, under the boss drop box); Cargo1 cover-wall end + 13 glow sticks 1.7 m up,
  a Cube 0.66 m from anything, a 6 m2 decor panel; Hallway2 two 26 m2 cinderblock boxes 25 cm off; Space rockvox (58 boxes);
  Foyer 16 hanging cyan glow sticks + a stage-light can + marquee; Hallway2 9 purple glow sticks; lamp cans (Battlefield1,
  Space, Concession, Stadium tripod head 43 cm off); small: decor panels/terminal screens/speakers/banners 4-12 cm off walls,
  crane lattice bars 5 cm short, couches/lockers/chairs 5-10 cm above floors.
- (3, resumed after the usage limit) tracing tools NSP/jt13/tr: trace.pl (floater comp -> level objects inside it: path, act,
  scripts), near.pl (pieces -> nearest source object), col.pl (drawn pieces over a point, by height). Found so far:
  Concession [StagePieces] x6 = the Departure's jumping platforms over the portal pit (floor 5.5 m below, Portal_* haze at
  -2.3; own colliders): floating in Rec Room too -> KEEP. Concession glow sticks in the air = HorizGlowsticks (150, active;
  the 180 Glowsticks_Vert are inactive and already left out). Foyer's 16 cyan sticks hang 0.4-0.7 m under the black
  ceiling, no string in the level data (the batch is only the stick mesh).
- (3) more findings: (a) WRONG INSTALL MESH: apkscene matches R records by NAME; the runtime catalog takes the FIRST mesh of
  that name. The install has 2 "Cube" meshes (bundle 43ea11..: a 0.13 x 3.5 m pole first, the 1.14 x 0.78 m cardboard box
  second), so Cargo1's 44 [Cardboard_box] (+ Concession's box, EmitCube) are drawn as 3.5 m POLES lying in the air; the same
  for pCylinder1 (Battlefield1's 17 glow sticks -> the 2 cm variant). (b) Stadium: 4 Enemy_Jumbotron_Mat cable loops (y 5.5-
  8.4, ~190 plates each) hung under the original block, which the runtime drops/rebuilds smaller -> floating. (c) Concession:
  the Departure's 150 HorizGlowsticks hang mid-air (ceiling 10 m); Hallway2 2 ProBuilder pillars behind the curtain with no
  floor (own colliders); Garage LadderShort 18 cm up; couches/lockers/chairs 5-10 cm up; tripod head (Stadium
  [StageLight_Standing] (6), skinned Tripod_Height_Bone) 43 cm over its pole; light shafts all come from lamps on supports
  (17 Stadium ones are BossCinematic lamps, active at load) -> fine. Space boulder (vox shell 58 boxes, own collider) and the
  Departure [StagePieces] platforms float in Rec Room too -> keep.
  PLAN (fix): gtscene jt=1 prep: R records whose runtime install mesh (first of the name, inst_meshes order) does not match the
  level mesh's bounds -> re-measured stand-ins (Remeasure). New "gtscene float" pass (Gt.cs command, run by
  jumbotron_convert.sh after the cuts): support test; per floater: own collider -> keep; glow stick: support below <= 0.6 m ->
  drop onto it, ceiling above <= 1.5 m -> hang by a string (G dress Glowstick_String, Metal_Dull_Dark_Mat), else drop; other:
  gap <= 0.6 m -> snap onto the nearest piece (1 cm left), else drop; drop=<mat regex> (Stadium Enemy_Jumbotron_Mat).
- (4) DONE in DN/tools/gtscene/Gt.cs (backup NSP/jt13/Gt.cs.before): jt=1 prep re-measures R records whose runtime install
  mesh (first of the name) != the level mesh bounds (Cargo1: 44 Cube -> Cube_box cardboard boxes; header line "install-mesh
  records ... re-measured"); new command "float" (class GtFloat at the end; rules: keep = own collider and >= 4 m2 unless
  0-25 cm over a support; drop= regex; down = support under it <= 0.6 m and <= 2 x gap + 3 cm; glow sticks: snap <= 0.1,
  hang (string) <= 1.5 m, else out; others: snap <= 0.6 m along the SAT axis, else own collider -> keep, else out).
  jumbotron_convert.sh runs it after the Stadium cut (Foyer skip=ActivityDoor_*, Stadium drop=Enemy_Jumbotron_Mat
  support=89.9,11,-96.4,8.8,5.4,8.8 = RrJumbotronBoss body+cap+rim). Scratch tools rebuilt in NSP/int11/tools/gts.
  NEXT: trial run to NSP/jt13/rr (--no-cs), review the reports, then the real run + rrsk2cs + size + compile.
- (5) tuned GtFloat: keep only via keep= regex (Concession keep=StageTop_mat|Curtain_LOD0, StadiumEntry keep=Rock_Boulder_Mat;
  own-collider heuristic kept Cargo's floating barrels + cover wall, now report-only "collider/top m2"); below/above by rays
  on the OBBs (AABBs of tilted plates left gaps); down moves refined with SAT steps. Trial NSP/jt13/rr: re-testing the
  output finds only the 8 + 1 kept groups. Two lamps with shafts moved > 5 cm -> RrJumbotronHaze ShaftData apertures shifted
  (Concession exit-door lamp +0.235 y, Stadium BossCinematic [StageLight] (8) -0.168 y; backup NSP/jt13/RrJumbotronHaze.cs.before).
  NEXT: backup Scenes + rrsk to NSP/jt13/bak, real run, sizes, compile.
- (6) real run done: 11 rrsk + Scenes regenerated (3,241,031 B, was 3,257,919). NEXT: compile check.
- (7) compile check: 0 errors (server-like 320 files, 1 warning = ThirdParty Mirror.cs, not ours; API guard 118/118; editor
  runtime + tools 0). Golden Trophy: patched gtscene (jt off) == unpatched build (NSP/jt13/gts0 from Gt.cs.before),
  byte-identical rrsk for level19 Foyer, 20 Armory, 24 DeadIsland (REF2), 28 Battlefield. Re-running the support test on
  the new rrsk finds only the kept groups. Offline renders: NSP/jt13/png/fo_st_after.png (Foyer sticks on strings),
  co_before/co_after.png (Departure, mid-air sticks gone).

## Result (iteration 13) - what was floating and what was done

Tool: `gtscene float` (DN/tools/gtscene/Gt.cs, class GtFloat; run by DN/iter8/jumbotron_convert.sh after the cuts, before
the markers; report per room in NSP/apkwork/jtstats/<Room>_float.tsv). Every drawn piece (hull plates, stand-in boxes, install
props with the runtime's own mesh, the rebuilt boss block) is an oriented box; groups of touching pieces (4 cm) that do not
touch the room are floaters. 490 floating groups in 11 rooms before; after the fix only the 9 intentional ones (kept) remain.

| Found | Room(s) | Fix |
| --- | --- | --- |
| 44 cardboard boxes drawn as 3.5 m POLES lying in the air: the install has two meshes named "Cube", the runtime loads the first (a 0.13 x 3.5 m pole), the level means the second (the 1.14 x 0.78 m box); the barrels/pistols on them looked unsupported too | Cargo1 (44), Concession (3: a cardboard box, the hidden nav ground, a glowing 1 m EmitCube of Lighting/Baked), Battlefield1 (17 "pCylinder1" glow sticks, drawn as the install's 2 cm variant) | jt=1 prep: an R record whose runtime install mesh does not match the level mesh bounds is re-measured from the level mesh (64: Cube_box dress boxes, glow-stick boxes). The EmitCube (a light-bake helper in mid-air) left out by the support test |
| Jumbotron's own cable loops (Enemy_Jumbotron_Mat, ~190 plates each) hanging under the original block, which the runtime drops and rebuilds smaller | Stadium (3 loops, 575 plates) | drop=Enemy_Jumbotron_Mat; the rebuilt block (RrJumbotronBoss body+cap+rim) counts as support, so the 4th loop that touches it stays |
| Glow sticks hanging 0.2-0.7 m under the ceiling with no string (the level has none) | Foyer (16), Concession (13) | 8 mm dark string (G dress Glowstick_String, Metal_Dull_Dark_Mat) from the stick up to the ceiling surface (ray cast) |
| Glow sticks in mid-air, nothing within 1.5 m above (the Departure's 150 "HorizGlowsticks"; note: its level has a ZeroGravityOutro cinematic, the drift may be intended there) | Concession (127), Hallway2 (6), Cargo1 (13 with the barrels), Space (1) | left out |
| Explosive barrels + a cover wall 3 m over the floor: they stood on MetalShelf objects the level switches off | Cargo1 | left out (22 pieces) |
| Couches, lockers, chairs, a ladder, lamp heads 2-18 cm over the floor / stage (legs and feet were small curved pieces left out); a tripod lamp head 42 cm over its unskinned stand-in pole (Tripod_Height_Bone) | Armory, Reception, Battlefield1, Garage, Stadium, Cargo1 | lowered onto the surface under them (1 cm left, then SAT steps until touching) |
| Decor panels, terminal screens, wall signs, banners, light strips 4-25 cm off their walls; lamp cans 4-24 cm off trusses/ceilings; crane lattice bars 5 cm short; the Foyer marquee 16 cm behind the pillars; 2 cinderblock pillars behind the Hallway2 curtain 25 cm off | all rooms (~290 groups) | snapped along the separating axis onto the nearest piece (1 cm left) |
| Two moved lamps carry light shafts | Concession exit-door lamp (+0.235 m), Stadium BossCinematic [StageLight] (8) (-0.168 m) | RrJumbotronHaze.ShaftData: their four aperture corners shifted with the lamp (landing corners unchanged) |
| Intentional: the Departure's 7 stage platforms + 1 scaffold over the portal pit (own colliders, over the Portal haze 5.5 m above the floor), its hanging screen curtains (R bounds 27 cm off the truss), Space's boulder (asteroid, own collider) | Concession, Space | kept (keep= regex per room) |

Checked and fine: all 48 light shafts come from lamps on supports (17 Stadium ones are BossCinematic lamps, active at load);
inactive objects are drawn only where Rec Room's own merged batches hold them (as in Rec Room: the Cargo shelves); LOD1+ never drawn; the Armory AlarmCinematic light is on the ceiling.
Sizes: Jumbotron scene files 3,241,031 B (was 3,257,919). Backups: NSP/jt13/bak/{scenes,rrsk}, NSP/jt13/Gt.cs.before,
NSP/jt13/RrJumbotronHaze.cs.before.

Open: the Departure's zero-gravity look (drifting glow sticks) is now gone - if the user wants it back, drop "Glowstick" from
the float rules for StadiumConcession (keep=...|Glowstick). Cables still hang from the roof to where the old (bigger) boss
block was; after the boss dies the one cable loop touching the rebuilt block would hang free. Not tested in play mode.
