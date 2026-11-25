# iter11 integrate (Jumbotron) - notes

## Plan
1. Compile check (bash SP/compilecheck/build.sh project --editor), 0 errors; scene size budget (all RrScene_*.cs < 6.0 MB).
2. Edit SP/harness/scenarios/jt10_audit.json: remove 11 "dump ... _floors" steps; add 2 close shots (wall 2-4 m, corridor edge)
   for Foyer, act 1, act 5, act 9. Run once: bash SP/harness/run.sh jt10_audit. Check summary.txt + log for exceptions.
3. Look at all arrival/wide + close shots; compare with DN/iter10/screens/jt10_*.png + DN/research/rr_jumbotron_look.md.
   Small targeted fixes -> at most one extra run.
4. Copy final shots to DN/iter11/screens/jt11_<area>.png.
5. Write DN/iter11/jt11_result.md.

## Log
- 1. compile check (after all 3 builders): server-like 0 errors 0 warnings (293 files), API guard 118/118, editor-like 0, editor tools 0.
  Scene size: all RrScene_*.cs 5,089,639 B (< 6.0 MB), Jumbotron 3,256,999 B (<= 3.5 MB). OK.
- 2. scenario SP/harness/scenarios/jt10_audit.json edited (orig copy NSP/int11/jt10_audit.json.orig): 11 "dump *_floors" removed;
  8 close shots added (room frame), positions picked with NSP/int11/ray.pl (perl raycast over plates/dress) + gtscene render previews
  (NSP/int11/pre_*.png, all 0-10 bg px except act9_wall 6.8k = skylight grid):
  foyer_wall (-12,1.6,14)->(-13,1.4,17) purple hall side wall 3 m; foyer_edge (1.5,1.6,-2.4)->(-30,2,-3) along vestibule wall into hall;
  act1_wall (-125.5,1.6,254)->(-122.19,1.4,255) tunnel wall 3.3 m; act1_edge (-122.8,1.6,251)->(-122.4,1.8,264) along tunnel wall;
  act5_wall (-126,1.6,499.5)->(-126.5,1.4,503) vestibule wall 3.5 m; act5_edge (-124.5,1.6,497.6)->(-139,1.8,497.2);
  act9_wall (-126.5,1.6,755.5)->(-123,1.5,756.5) concrete wall 3.5 m; act9_edge (-123.6,1.6,754.5)->(-123.1,1.8,740).
- 2. RUN 1 (21:40, 80.5 s): booted, 0 step failures of 136, exit 0. Errors: only the 2 editor-only "Host type is not matching" +
  the package-cache DirectoryNotFoundException. Output saved to NSP/int11/run1/. Build: Quest built at +10.38 s (Foyer hull +6.42).
  Hull plates: Foyer 5549, Armory 3535, B1 10158, H1 9932, H2 15806, Rec 9016, Conc 12825, Entry 19177, Garage 10818, Cargo 20938,
  Stadium 30991 (all "from the install", 0 fallbacks).
- 3. LOOK run 1: Foyer/Armory/acts good (marquee reads "THE RISE OF JUMBOTRON", soft shafts, no cyan square, no magenta cone,
  open trusses, walls closed in all 8 close shots). BREAKAGE found: act 4 arrival - vertical SAWTOOTH with pink see-through
  specks along the vestibule's wall edges (left x~90 px, right x~1185 px). Cause (measured, Reception rrsk near (201.07, y, 24.89)):
  a 0.3 x 3.8 m wedge triangle staircased in 5 cm strips by the centre test (Cover centre=true) -> a row of see-through
  triangles along its diagonal edge. The act 9 right-side "red-dotted" surface = kit Curtain_Flat (Curtain_Black -> Curtain_Red
  x0.245, iter10 choice), not a hull fault.
- FIX TRIAL (scratch copy NSP/int11/tools/gts/Gt.cs): staircase cells by CoverTouch (any of 5 test points inside) instead of the
  centre; knob GTJT_STAIRCENTRE=1 restores the old test. With the knob the Reception rrsk is byte-identical to the checked-in
  one (baseline proven). Reception: 9289 -> 9245 plates; corner close-up bg px 280 -> 68 (NSP/int11/m_rec.png: teeth gone).
- Scratch full conversion (NSP/int11/rr_all, --no-cs, 37 s): plates per room -0.1..-1.7 % (Hallway2 +1.7 %), dress unchanged.
  GOLDEN TROPHY check: patched vs unpatched gtscene (jt off) for level19 Foyer, 24 DeadIsland (ref), 28 Battlefield: byte-identical.
- Scratch survey (NSP/int11/holes.sh = geometry's holes.sh, 11 rooms x 6 points x 5 views, bg px old -> new): Reception 70->30,
  Garage 13->5, Cargo1 44->20, Hallway1 200->117, StadiumEntry 34753->34574 (sky), Stadium 9519->9509 (sky), others equal.
- APPLIED (21:52): DN/tools/gtscene/Gt.cs = the patched copy (+ header note, knob GTJT_STAIRCENTRE=1); rebuilt DN/tools/gtscene/out
  and NSP/int11/tools/gts; backups NSP/int11/backup/{Gt.cs.geometry,rrsk/,cs/}. Real run: bash DN/iter8/jumbotron_convert.sh
  NSP/apkwork NSP/int11/tools (47 s, 0 errors) -> 11 rrsk (= the scratch ones) + 11 Scenes/*.cs.
  Size: all RrScene_*.cs 5,090,559 B, Jumbotron 3,257,919 B.
- compile after regeneration: server-like 0 errors 0 warnings (293 files), guard 118/118, editor runtime 0, tools 0. GT files untouched.
- RUN 2 started (the one extra run, to verify the staircase fix in-engine).
- RUN 2 (21:54, 79.0 s): booted, 0 failures of 136, exit 0; errors only the editor-only 2 + package-cache DirectoryNotFound.
  act 4 edges (NSP/int11/crop_act4_left|right.png): the see-through slivers (grey/magenta/red) are gone; the edge now shows
  small lighter overshoot tabs (a 5 cm x 0.31 m sawtooth outline, same hue) - not see-through. All other shots identical lumas
  (act4 sd 17 -> 18). Build: "built Jumbotron in 8044 ms" (run1 8652; iter10 7739-8423). Longest frame 425 ms (editor).
  Active renderers per act 84-148 (iter10 167-317; only the current act is on now), Stadium 238 (iter10 204).
- 4. screens copied: DN/iter11/screens/jt11_{foyer,armory,act1..act9}.png (run 2 arrivals), jt11_foyer_edge.png,
  jt11_act1_edge.png (close shots), jt11_act4_edge_before_after.png (run1 | run2 crop of the act 4 right edge).
- 5. writing DN/iter11/jt11_result.md
- DONE: DN/iter11/jt11_result.md written. Open: reload with the other hand (user's report) not in this job; sawtooth outlines on
  diagonal edges (finer stair step or nothing); brightness gaps; glow strips on plain atlas surfaces; curtain material; VR frame
  rate in acts 8/9.
