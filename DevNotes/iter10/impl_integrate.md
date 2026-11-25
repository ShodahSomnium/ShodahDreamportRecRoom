# iter10 integrate - notes (plan + log)

## Plan
1. Compile check (bash SP/compilecheck/build.sh project --editor), fix errors.
2. One harness run: bash SP/harness/run.sh jt10_audit; check summary.txt + Exceptions in unity.log.
3. Look at arrival shots Foyer, Armory, act1, act2, act5, act9 vs audit_*.png and rr_jumbotron_look.md; small targeted fixes only; one more run only if a fix was made.
4. Copy final arrival shots to DN/iter10/screens/jt10_<area>.png.
5. Write DN/iter10/jt10_result.md (per-builder changes, lumas before/after, differences, risks).

## Log
- (start) notes file created; no previous integrate notes existed.
- Step 1: compile check with all three builders' code: server-like 0 errors / 0 warnings (293 files), API guard 118/118, editor runtime 0, editor tools 0. No fixes needed.
- Baseline saved: old jt10_audit summary -> NSP/integ/old_summary.txt, lumas -> NSP/integ/old_lumas.txt (old run had 11 failures, all "dump *_floors: root not found" = scenario artefact, not a code fault).
- Step 2: starting harness run jt10_audit (run 1 of max 2).
- Run 1 done (113 s): 11 step failures of 139 = the same 11 "dump *_floors: root not found" as the old run (scenario artefact); no exceptions except the startup DirectoryNotFound package-cache noise. Log: per-act env (Jumbotron: lighting: <scene> (act n)) + PostFx Scifi_Default / Scifi_Armory crossfades confirmed; hull/haze summary lines per room present; StadiumEntry grid cut (161,88,181)->(35,15,113).
- Lumas run1 (old -> new): foyer_arrival 47->53, armory 32->74, act1 44->29, act2 40->35, act3 17->32, act4 29->36, act5 30->30, act6 25->33, act7 31->29, act8 40->24, act9 31->35. Now looking at the shots.
- Shots run 1 reviewed (Foyer arrival/wide/boxes/back, Armory arrival/wide, acts 1-6, 8, 9): no black/white-out rooms, no missing walls, no magenta error shaders, haze soft (Act 2 ceiling mist, Foyer pink cone), cover-wall boxes opaque (Foyer, act 9), bloom not blown, red lasers act 3/6, blacklight carpet + green walls Armory, per-act env colours right.
- ONE defect found: the Foyer marquee (quest_jumbotron_cauldron_marquee_tex -> StuntRunner_JumbotronMarquee_Mat, FaceUv) reads MIRRORED from the audience and UPSIDE-DOWN from the stage (crops NSP/integ/crop_marquee*.png). Cause: BoxMesh gives every face u along r with Cross(r,u)=n, which in Unity's left-handed frame puts u to the viewer's LEFT; plus the marquee box's local axes are not world-aligned. Fix (RrJumbotronHull.BoxMesh, FaceUv only): PictureUvs - V along the face axis nearest world up (pointing up), U along the viewer's right Cross(n, V). Tiled faces untouched.
- Fix applied: RrJumbotronHull.cs BoxMesh -> new PictureUvs (FaceUv faces only). Compile check 0 errors / 0 warnings (293 files), API guard 118/118, editor builds 0. Run-1 PNGs + summary saved to NSP/integ/run1/. Starting run 2 (the last allowed).
- Run 2 done (98 s): same 11 floors-dump failures only, no exceptions; every luma identical to run 1 (fix touches only the marquee faces). Marquee now reads "STUNTRUNNER..." the right way round from the seats AND from the stage (NSP/integ/crop_marquee_run2.png).
- Step 4: final shots copied to DN/iter10/screens/jt10_{foyer,armory,act1..act9}.png (+ jt10_foyer_wide/back/boxes, jt10_armory_wide, jt10_act9_wide).
- Step 5: writing DN/iter10/jt10_result.md.
- Step 5 done: DN/iter10/jt10_result.md written (checks, per-builder changes, lumas before/after, look, differences, in-world risks). DONE.
