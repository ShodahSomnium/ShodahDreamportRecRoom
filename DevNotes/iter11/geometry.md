# iter11 geometry (Jumbotron hull / dress re-measure)

Owner: geometry agent. Owns DN/tools/gtscene/*, DN/iter8/jumbotron_convert.sh + jumbotron_tools/*, DN/refdata/apk/Jumbotron_*.rrsk,
Games/Quests/Jumbotron/Scenes/RrScene_Jumbotron_*.cs. Golden Trophy output must stay byte-identical (default mode unchanged).

## Plan
1. Measure per room: hull tri area per material vs plate area kept; dropped small rects; staircase groups.
2. jt=1 mode in gtscene: keep small rects (all materials, from ~1 cm); planar faces -> try face edge directions as axes.
3. Replace G dress bounds boxes with re-measured plates (level-only meshes); curved < 70% coverage -> voxel shell if > 0.6 m, else drop.
4. Double-sided materials (APK shader cull off) -> emit plate twice (front + flipped).
5. Keep Space cut; size budget (Jumbotron scene files <= 3.5 MB). Regenerate, compile-check.

## Log
- (1) tools: DN/tools/gtscene/build.sh did not build any more (apkscene's Emit.cs/Env.cs now use UrpKeys from Tree.cs):
  build.sh now compiles every apkscene .cs but Main.cs (same code, so GT output unchanged). Scratch tools: NSP/jt11/tools.
- (1) baseline: re-running the unmodified prep on level38 reproduces Jumbotron_Foyer.rrsk except the 15 L lines, which
  now carry iter9's URPLight keys (spec= hemi= hin= hout= ispot=; apkscene Emit.cs changed after iter8's conversion).
  RrSceneData.LightRecord reads those keys (iter9 light9.md), so the regenerated rooms carry them too (intended).
- (4) DOUBLE-SIDED: new `gtscene cull <work> <levels>` reads each level material's shader pass state (culling val/name,
  and the material's _Cull). Output NSP/jt11/cull.txt (213 materials, levels 38-48): EVERY opaque surface material is
  back-face culled in Rec Room (135 AG/Standard-* "2,_Cull=2", 34 fixed 2). Cull Off only on: additive glows / light
  shafts / StageLight_Glow* / lasers / hologram (AG/Unlit-Additive*, Particles/Holographic), the PortalFog(Fresnel)
  portals, ToolBeacon, the pine tree leaves (Nature/Leaf _Cull=0) and TextMeshPro. None of them is a hull surface the
  hull draws (portals/fog/shafts = haze; StageLight_Glow = Plan skip; trees = organic, not hull).
  => Rec Room's walls are one-sided too: double-sided shading is NOT the cause of the see-through walls. Decision: no
  ds=1 key; in jt mode a plate of a cull-off material that is not haze would be emitted twice (front + flipped), which
  today touches no drawn material (count logged in the rrsk header).
- (2) Gt.cs jt=1 mode written (default path untouched): Plates.OfJt = plane groups -> 2D pieces -> per-triangle basis
  from the piece outline's edge directions (mod 90, 0.1 deg bins, 0.3 deg clusters) -> exact raster on the vertices' own
  (unrounded, 0.1 mm dedup) coordinates; a cover within 10 cm2 of exact counts as exact; else refined 2-5 cm (1/4 of the
  smaller side, no line closer than step/4 to a vertex line), centre test. Rects kept from 1 cm, all materials.
  Found on the way: the old grid rounded to 1 mm, so every thin trim/tube facet (< ~10 cm) failed the 1 % exact test and
  became a staircase (then dropped under 0.05 m2) -> that is where most "small detail rectangles" came from.
- (2) hull per 3D piece (Plates.Pieces, a prop of the merged batch): < 0.6 m and < 70 % exact -> left out (round
  grilles, knobs). Haze materials (AreaFog/Portal/...) keep the OLD Plates.Of path + old small-drop, so the haze input
  (and its raycast bake cost) is unchanged. JtSkipMat = SkipMat minus Glowstick (glow sticks are opaque level sticks).
- (3) stand-ins: Remeasure() per X-line renderer (matched by mesh + centre < 1 cm): props use OfJt(refine=false) (exact
  cells only; tube ends/mitres leave < 10 cm2 notches); >= 70 % exact -> plates in their own materials (+platemat);
  one-material box-shaped (rect area = bounds box area +-5 %) -> the dress box stays; else > 0.6 m -> voxel shell
  (Shape.Boxes 40 boxes, local frame) "G dress <mesh>_vox"; else left out. KeepDress: Quest_Jumbotron_Marquee.
- Foyer test: hull 2,664 -> ~6,300 plates; stand-ins: 46 -> plates (Scaffolding_6M 22 x 459 rects = 10.1 k plates!),
  1 vox (Clock_Housing), 4 out (DrinkingFountain/Fountain_Button: all LockerFountainStageLight_mat = skipped).
- Budget check, first full trial (NSP/jt11/r1, --no-cs): 375 k plates (old 63.5 k), ~13-16 B each embedded -> far over
  the 3.5 MB. Where: dress Scaffolding_6M 197 x 457 = 90 k (80 tube pieces each); hull staircases of big curved pieces
  (Curtain_Black 30 k, Enemy_Jumbotron 30 k = the boss block the runtime drops anyway, Crane_Red 16 k, barrels 24 k).
  Plan: (a) tube/plank props -> one oriented box per piece ("G dress", when every piece is box-like);
  (b) big curved hull pieces -> one rectangle per triangle pair (fitted quad) instead of a staircase;
  (c) mirror the runtime cuts offline: Stadium DropBox (89.9,11,-96.4 size 14,6,14.5; plates/dress with max e < 4) and
  StadiumEntry's Filter (-82,0,-88 size 160,90,180) - no visual change, less data;
  (d) staircase only where the uncovered part of a face is >= 0.02 m2 (GTJT_REM), else the exact cells.
- Implemented (a)-(d) + Plates.Approx (fitted rect per triangle pair) + Plates.Obb (oriented box, fill = area/box area).
  Stand-in outcomes now: box (unchanged dress line) / boxes (every box-like piece, fill 0.75-1.15, as one oriented
  "G dress <mesh>_box"; rest of it as plates) / plates / approx (curved, > 0.6 m, <= 1200 tris) / vox (> 1200 tris) / out.
  Scaffolding_6M -> 80 tube boxes each (was 457 plates). r2 trial: 159 k plates + 17 k dress, embedded 3.28 MB.
- Coordination: materials agent (DONE) reads per-piece "_box"/"_vox" dress as plain boxes (Frame() only for whole-truss
  bounds), LockerFountainStageLight_mat on plates = As(mat) -> jt keeps those props now (layout agent: the rebuilt light
  cones started at missing lamp fixtures). layout agent asked: LockIcon quads over exit pads -> JtSkipMat has LockIcon.
  layout agent: mist blobs were blocky staircases -> jt draws Scifi_AreaFog_* with Approx (Foyer 1,587 -> 142 plates,
  a closed faceted blob instead of stripes; offline NSP/jt11/geom/png/fog_old|new.png). Portals: old path (exact quads).
- Offline see-through survey (NSP/jt11/geom/holes.sh: 11 rooms, arrival + 5 spawners, 5 views, haze materials skipped):
  background px old/new: Cargo1 1420 -> 44, Hallway1 275 -> 200, Reception 89 -> 70, others ~equal (Space/arena sky).
- Budget round 2: LockerFountainStageLight fixtures cost 18.8 k plates -> hull rule: a small (< 0.6 m) many-sided
  (>= 8 rects) rectangular piece filling its oriented box (0.7-1.15) = one "G dress <batch>_box" (stage-light cans,
  glow sticks); props keep the largest rects making 97 % of their rect area (GTJT_TAIL; greeble tail goes).
  r5 trial: 153 k plates + 18 k dress, embedded 3.28 MB (Jumbotron budget 3.5 MB; world 1.83 + 3.28 = 5.1 MB).
- GOLDEN TROPHY CHECK: the modified gtscene without jt=1 writes byte-identical rrsk to the original Gt.cs (built from
  NSP/jt11/Gt.cs.orig against the same apkscene) for level19 Foyer, 24 DeadIsland, 26 Library, 28 Battlefield.
  (GT's checked-in rrsk differ from a fresh run only by iter9's light keys - not regenerated, not my files' output.)
- Backups of the iteration-10 rrsk + Scenes/*.cs: NSP/jt11/geom/backup/{rrsk,cs} (restore = copy back).
- Last rules: busy hull props (< 1.6 m, > 60 rects/m2: ribbed barrels) keep their largest rects making 97 % of their
  area (barrels 6.8 k -> 3.9 k in the Garage); the <= 10 cm2 sliver case covers every cell a triangle touches (no slit
  between mitred facets); stand-ins: "boxes" only for 2+ box-like pieces or a one-piece prop with fill >= 0.85,
  curved props "mixed" piece by piece (box-like piece -> box, rectangular -> rects, curved > 15 cm -> fitted rects).
- FINAL RUN: bash DN/iter8/jumbotron_convert.sh NSP/apkwork NSP/jt11/geom/tools -> the 11 rrsk + Scenes/*.cs.
  Jumbotron scene files 3,256,999 B (was 1.40 MB; budget 3.5 MB); all RrScene_*.cs 5,089,639 B (< 6 MB).
  Compile: server-like 0 errors 0 warnings (293 files), API guard 118/118, editor runtime 0, editor tools 0.

## Result (geometry, DONE)

What was wrong (measured):
1. The old grid rounded vertex coordinates to 1 mm, so every thin face (trims, tube facets, frame strips < ~10 cm)
   failed the 1 % "exact" test and became a 5 cm staircase, which the < 0.05 m2 rule then DROPPED for every material
   but the ~16 "main" ones: 4.6-48 k rectangles per room -> missing lids, bands, trims, edges = see-through props and
   slits (offline: Garage barrels had no lids/ribs; Cargo1's arrival views had 1,420 background px, now 44).
2. Slanted faces (chamfers, octagon strips, braces) were rasterised on the room's axes -> staircases (then dropped).
3. 1,233 level-only stand-ins were solid bounds boxes: 197 six-metre trusses (Scaffolding_6M) = solid grey slabs under
   every ceiling, 7 m solid poles (Theater_Sandbag lines), terminals/decor walls/vents/clocks/doors/spaceship as boxes.
4. Double-sided shading is NOT a cause: `gtscene cull` shows every opaque level material is Cull Back in Rec Room too.
   Mirrored hull transforms: none in levels 38-48 (a few mirrored stand-ins are turned).

What the data is now (DN/refdata/apk/Jumbotron_*.rrsk, Scenes/RrScene_Jumbotron_*.cs):
- Hull plates on each face's own axes from 1 cm in every material (~100 % of the hull triangle area), staircases only
  where >= 0.02 m2 of a face is not rectangles; big curved pieces (curtains, crane, tyres, boss block) = one fitted
  rectangle per triangle pair; small curved bits (< 0.6 m, knobs/grilles) left out; small many-sided pieces
  (stage-light cans, some glow sticks) = one oriented "G dress <batch>_box" in their material.
- Mist blobs Scifi_AreaFog_* = fitted rectangles (e.g. Foyer 1,587 staircase cells -> 142), portals unchanged.
- Stand-ins re-measured from their own meshes/materials: trusses = 80 oriented tube boxes each ("<mesh>_box"),
  cover walls = their 1-2 real boxes (dress look kept), terminals/decor walls/vents/clocks/doors = plates of their real
  faces (+ platemat records for their materials), ropes/sandbags/spaceship = mixed boxes + fitted rects, 10 curved
  props = "<mesh>_vox" shells, StageLight_Glow quads + LockIcon quads + all-skipped props left out;
  Quest_Jumbotron_Marquee stays the bounds box (the hull rebuilds it).
- Now included (were skipped): Glowstick_* plates, LockerFountainStageLight_mat props (lamp cans/fountains; the
  layout agent's light cones start at them). LockIcon_Mat now skipped (layout agent's request).
- Offline cuts that mirror the runtime exactly: StadiumEntry to its Filter box, Stadium's boss DropBox (small records).

| room | plates old -> new | dress old -> new | scene .cs KB old -> new |
| --- | --- | --- | --- |
| Foyer | 2684 -> 5721 | 52 -> 1842 | 74 -> 146 |
| Armory | 401 -> 3557 | 15 -> 342 | 62 -> 71 |
| Battlefield1 | 6092 -> 10596 | 78 -> 948 | 137 -> 226 |
| Hallway1 | 4392 -> 10175 | 57 -> 956 | 98 -> 206 |
| Hallway2 | 5019 -> 15846 | 172 -> 1206 | 121 -> 359 |
| Reception | 3674 -> 9289 | 95 -> 220 | 92 -> 206 |
| StadiumConcession | 14752 -> 15206 | 82 -> 3377 | 264 -> 347 |
| StadiumEntry | 5096 -> 19274 | 214 -> 2789 | 110 -> 388 |
| Garage | 1883 -> 11189 | 83 -> 344 | 52 -> 250 |
| Cargo1 | 6254 -> 21187 | 178 -> 2752 | 134 -> 411 |
| Stadium | 13506 -> 31063 | 207 -> 3262 | 218 -> 566 |

Per-room stand-in outcomes are in each rrsk's "# gtscene jt=1: stand-ins re-measured" line; per mesh numbers in
NSP/apkwork/jtstats/<Room>.tsv. Offline before/after renders: NSP/jt11/geom/png/*_old|_new.png (ship, a8, arm, act3,
act9, curt, barrel, fog). Regenerate: bash DN/iter8/jumbotron_tools/make_tools.sh <tools>; bash DN/iter8/
jumbotron_convert.sh NSP/apkwork <tools> [rrsk dir] [--no-cs]. Golden Trophy (gtprep.sh, no jt=1): byte-identical.

For the materials owner (RrJumbotronHull/Look): no ds=1 anywhere (not needed; a cull-off plate material would come
as two plates). New dress names: "<mesh>_box" (oriented piece boxes, arbitrary rotations), "<batch>_box" (hull pieces,
hull material names, m= with "?" for level-only), "<mesh>_vox". Former dress-only materials now also arrive as plates
(Terminal_SciFi_Prop, Wall_SciFi_Decor_Blue, WallVent, Goblet_Clock/Clock_Glass, Shipping_Container, SciFi_CoverSideWall_Glass,
Dropship_Prop, Sandbag, Metal_Dull, LockerFountainStageLight...) -> Plan(dress=false) applies to them.

## Open issues
- Hairline slits remain where tiny rib/band facets of ribbed barrels are curved small pieces or < 1 cm (offline zoom
  NSP/jt11/geom/png/rib.png); invisible at normal distance, visible against a bright wall up close.
- Plate count is 4x iteration 10 (153 k plates + 18 k dress boxes; biggest room Stadium 31 k plates, Cargo1 21 k):
  worth a look at the in-world build time / frame time of the Stadium and Cargo acts.
- Glow strips / pink dots on atlas materials (materials agent's open issue: an "a=" key per plate from the level UVs)
  not done; portal UV direction (layout agent) not done.
- Jumbotron_Additive.rrsk (level49, kill floors only) not part of the pipeline, unchanged.
