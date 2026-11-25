# iter4 "dorm" - Dorm in its real (DormRoom-2018) layout

Status: DONE (2026-09-24 ~17:35). 3 harness runs used (max). Final compile (after the spawn fix): 0 errors x3, all 113 members proven. Owner files: CODE/Places/Dorm/*, scenario dorm_room.json.

## Plan (room frame, metres; from REF1 DormRoom-2018 = rr_room_exports 3.1)
- Door (RrPortal) left wall under loft: centre (-4.88, 0, -0.86) yaw 90. NOTE: task text said z -0.43, but the
  export's ROOM_DOOR/ROOM_DOOR_V2 pivots (z -0.39 / -0.425) are Rec Room's hinge-side pivots (mesh x -0.12..1.02,
  iter3/doors.md), so the doorway centre is 0.45 m further -z = -0.84..-0.875; the 2018 INTERACTION_VOLUME
  "Go to Room-i-verse" (z -0.876) and the welcome mat (z -0.921) agree. Sign (2.45..2.80) lowered under loft (2.64).
- Spawn = welcome mat (-3.97, 0.05, -0.92), yaw 90 (+x). Mat prop (SandboxWelcomeMat) at the export pose.
- Bunk bed back wall (1.86, 0, -2.17) yaw 0: desk top z -2.88..-1.84 holds radio/cups/bottle/darts at export xz.
- Mirror front wall (0.88, 1.34, 2.92) Y180; mirror panel right of it (2.35); help (-0.44) + CoC (-1.03) left;
  campus map (3.55, 1.6) front wall. Play board -> back wall (-0.97, 1.70, -2.90) between loft wall end and bed.
- Under loft: armchair (-3.733, 1.962) ry 132.6; table short (-4.39, 0.585) ry 90 s 0.815 + paddle/ball;
  fig (-4.422, 1.429) 129; snake (-4.511, -2.427) 90; dracaena (-2.663, 2.10) 147 (z nudged from 2.227: crown measured into the loft wall).
- Ladder moved to z 1.27 (dracaena crown fills the gap's +z half). Ladder Top/Bottom follow LadderPosition.z.
- Loft: MoveableWhiteboard (-4.347, 2.75, -1.239) ry 91.35 replaces ArtCanvas; marker set at the export's
  MARKER_ERASER spot (-4.12, 0.50); eraser (-3.355, -2.136); clipboard (-3.757, 1.31). Paintbrush dropped.
- Floor: ball (3.409, -1.142) 270, bin under rim (kept), bucket (-2.163, -2.636) 306, clipboard (2.887, 1.913).
- Posters stay under the loft back wall, shifted to x -3.15 / -3.95 (snake plant at x -4.51).

## Milestones
- [x] read export tables + current RrDormRoom layout
- [x] re-place props (code): RrDormRoom, RrDormBoard (rotation param), RrDormLadder (landings follow z), RrDormTest (StandAtMirror front wall, Layout check)
- [x] compile: 0 errors x3, 113 members proven
- [x] harness runs 1-3 (dorm_room)

## Run 1 (17:14) - 27/28 steps ok (basket SCORED = known iter3 issue, RrHoop/RrBall trigger)
- Layout reads right in all 14 PNGs; door sign lowered 19 cm (top 2.61 < underside 2.64); board ray/poke OK on the
  back wall; mirror reflecting; posters 2 notebook; 14 props on the desk, 0 floating.
- Found: wall stripe (rrtool: Dorm_Wall_Stripe boxes 6 mm thick, front/back wall face 8 mm proud (2.952), pillar face
  22 mm proud (-1.421)) drew over the CoC (back 6 mm off) and the hoop backboard frame (flush at -1.44, also in iter3).
  Fix: CoC standoff 2 cm, map 1.2 cm, panels z 2.93, hoop pivot z -1.415 (rim 25 mm off the data point).
- Layout check false positives from rotated-mesh AABBs (ladder 1.8 m deep, plant crowns at yaw): ladder box by
  construction, plants as round crowns (localBounds), door = its Solid box, stripe added as 1 mm solid.

## Run 2 (17:20) - CoC and backboard now in front of the stripe (PNGs dorm_help, dorm_hoop_close). Check left:
  hoop right edge 4 mm into the right-wall stripe (x 4.756) -> hoop pivot x 3.893; dracaena round crown (centre
  5.5 cm +z of pivot) z ..2.77 > loft wall 2.72 -> z 2.10.
## Run 3 (17:24) - FINAL: 29/30 steps ok (only the known basket SCORED). Layout: 49 objects, walls 0, ladder gap 0,
  floating 0, overlaps 0, 14 props on the desk; plant crowns over chair/table only (leaves). Door sign top 2.61
  (underside 2.64); hoop rim (3.893, 1.095, -1.066) = 27 mm from the data point; board ray/poke OK; mirror reflecting.
## After run 3 (compile only, not harness-verified): spawn index 0 was 0.6 m beside the mat ((index%3-1)); now
  ((index+1)%3-1) -> index 0 exactly on the mat (-3.97, 0.05, -0.92) / straight in front of the door.
