# Iter 12 - Rec Center mirror spots (locate, read-only)

User report 2026-09-26: in the Rec Center, red-brick room with red curtains, white armchairs, big red rug,
quest posters -> empty dark-brown frame (~2.2 x 1.4 m, landscape) on the wall between curtains. "There is meant to be a mirror here."

## Progress log
- Scene 1AF#1: no GameObject/mesh/material/script named Mirror/Frame/Picture/Reflect (only 2 inactive reflection probes and the
  CAMPUS JAMS `LLM Door/Door Frame`). No mirror script in the cache's script index.
- Room data 182.data printable strings: NO "mirror" (bracket names: ActivityDoor/Menu/LoungeDoor_RRS, Grid_Menu, Backboard x6,
  LaserFan, Bell x4, DrinkingFountain x3, CafeStorefront_RRS). So the July 2025 room data holds no mirror record.
- User's room = the THEATRE (X 11.8..21.6, floor Y -0.79): Banner_Mat rug (red/gold, logo) X 13.61..19.94 Z -6.44..-2.47;
  RecCenter_Chair uses Couch_Minimalist_Brown_Instanced_Mat (renders WHITE uncoloured -> "white armchairs"); quest poster cases
  on X 21.5; curtains (2)/(3) on the Z ~1.2 wall with a gap X 15.84..18.24 and valance above (Y 3.08..). Only building meshes
  (walls/floor/ceiling combined mesh 20) cover the gap -> the frame must be geometry inside the `walls` combined mesh.
  Next: dump triangles of the walls combined mesh (new scratch tool NSP/rrtool_mirror, command `tris`).
- Whole-cache byte grep for "mirror": only shader property names (184/19A/1A2), an anim "PlayMirror" (206, another room),
  and 2FC json text ("...on your Dorm room mirror"). No mirror record for the Rec Center anywhere in the cache.
- FOUND THE FRAME (walls combined mesh = `Combined Mesh (root: scene) 20` = Mesh#18 in 1AF#1, 4640 verts, stream in the
  CAB-ab51...resS; walls renderer subs 5..9, sub 9 = Wood_Horizontal_DarkBrown_3x_Mat has ONLY this frame, 16 tris):
  room frame, theatre wall face Z 1.38 (normal -Z, into the theatre), frame outer X 15.06..19.02 Y -0.32..1.91,
  opening X 15.12..18.96 (3.84 m) Y -0.26..1.85 (2.11 m), reveal 0.23 m deep, back = RecCenter_Brick_Red at Z 1.61.
  Opening bottom = 0.53 m above the theatre floor (-0.79), top 2.64 m. Curtains (2)/(3) cover X <15.84 and >18.24.
- Exact frame vertices (room frame): face Z 1.383; outer X 15.059..19.022 Y -0.322..1.912 (3.96 x 2.23);
  opening X 15.122..18.958 Y -0.259..1.849 (3.84 x 2.11); back (brick) Z 1.611, X 15.126..18.955.
- Colliders there: TheaterWallCollision (0..3) = the wall around the hole, face Z 1.374; (4) fills the recess, face
  Z 1.497 (X 14.53..19.58, Y -0.57..2.48). A _fit.Wall ray at the frame hits Z 1.497 (not the brick 1.611).
- CORROBORATION (rr_room_exports.md 4.1/4.3): the fan reconstructions share the official scene frame EXACTLY (their
  "Quest" text (-2.866, 2.085, 19.436) = our QUESTS sign (21.52, 1.98, -4.36); "PVP" = SPORTS & PVP sign; "Magic Door"
  = the MakerWorld sign spot upstairs). Their SANDBOX_MIRROR records:
    2019_Rec_Center: scene (-8.596, 1.03, 15.069) rot (0,90,270) scale 3.415 -> room (17.12, 0.94, 1.37), facing -Z;
    RecCenter-2020:  scene (-8.577, 0.905, 14.824) rot (0,90,270) scale 2.055 -> room (16.87, 0.82, 1.35), facing -Z.
  Roll 270 = the prop turned on its side (landscape). A 0.62 x 1.12 glass x 3.415 = 2.12 x 3.83 = the opening exactly
  (the install's current (Sandbox_Mirror) glass is 2.04 x 2.21, a later, bigger version). 2020 x 2.055 = 2.30 x 1.27
  (~ the user's "2.2 x 1.4"). => the theatre alcove IS Rec Room's mirror spot (room data, not in 182.data of 2025).
- Other frames scanned (reveal faces of walls mesh subs 5-8): only windows (office, clerestory, curved sill), the
  entrance arch, a balcony doorway X -12.22..-10.61 Y 3.19..6.20 on the entrance wall. No other mirror frame; no
  bathrooms/locker room in the building. Hallway side walls X -2.96 / +1.67 are plain (no frame).
- OUR LAYOUT MIRROR today: (Dorm_FreeMirror) layout (-2.05, 1.35, -20.6) yaw 90, fit moves it (-0.85, 0, 0)
  (harness logs light9_perf/env_tour: "mirror (-0.85, 0.00, 0.00)") -> root/glass (-2.90, 1.35, -20.6), glass
  2.04 x 2.21 portrait facing +X, 6 cm off the hallway's left wall (X -2.96), our white 8 cm frame. Not a duplicate of
  the real spot (different room); it has no support in the archives (both reconstructions put the mirror in the theatre).

## RESULT - theatre mirror (for the Place agent)
Frame = RrRecCenterRoom local (room frame; cache container at (2.05,-0.09,-7.23) yaw 90 under the room).
- Recommended (glass inside the recess, clear of the curtains which reach Z 1.49):
  centre (17.040, 0.795, 1.585), facing (0,0,-1) (localRot Euler(0,180,0) for RrMirror.Create), up (0,1,0),
  glass 3.77 x 2.05 (3 cm inset in the 3.83 x 2.11 opening). Scene frame: (-8.815, 0.885, 14.990) facing +x.
- Alternative flush (like the 2019 record): centre (17.040, 0.795, 1.370), same size (1.3 cm in front of the frame face;
  the curtain meshes X<15.84 / >18.24 reach Z 1.44-1.49 and would cut through the glass edges).
- Place it directly (no _fit.Wall: the recess collider face is 1.497). Range: 6.5 m only reaches Z -4.9 (rug centre);
  the couches are at Z -8.1 (9.7 m) -> range ~10 suggested. Theatre floor Y -0.79: glass bottom 0.56 m, top 2.61 m.
- Hallway mirror: keep or drop is the Place agent's / user's call (not a Rec Room spot in the 2025 building).

## PLACE (iter 12, place agent)
- Decision: with the cached building, the theatre frame gets an RrMirror (RrMirror.Create under the room), and our
  hallway (Dorm_FreeMirror) is dropped there (Rec Room's 2025 hallway wall is plain; both archives put the mirror in the
  theatre). The rebuild (no cache) keeps the hallway mirror unchanged.
- Glass: centre (17.040, 0.795, 1.606) = 5 mm in front of the brick back (1.611), 3.81 x 2.09 (1 cm inside the
  3.83 x 2.11 recess at the back), localRot Euler(0,180,0) (faces -z into the theatre), range 10 m (couches at z -8.1).
- Code (done, compile 0 errors): RrRecCenterLayout.TheatreMirrorPos/Yaw/Width/Height/Range; KeepWithCachedBuilding no
  longer keeps role "mirror"; RrRecCenterRoom.BuildTheatreMirror() (after FitLoose, cached building only) ->
  RrRecCenterRoom.TheatreMirror (RrMirror "Theatre Mirror" under the room); the dropped hallway mirror is not counted as
  a furniture copy; Fit's wall case is hoops only; build/test logs report "mirror ready (theatre)" / "theatre (planar|stylised)".
- Harness: SP/harness/scenarios/rc_mirror.json (front 2 m, 4 m at +-35 deg, from the rug, the old hallway spot).
- Harness rc_mirror (the one run, exit 0): log "theatre mirror 3.81 x 2.09 m at (17.040, 0.795, 1.606) (stylised glass
  only: no mirror shader)" (harness scene has no carrier, as expected); report "mirror theatre (stylised)".
  Shots in DevNotes/iter12/screens/rc_mirror_{front,angle_left,angle_right,room,hallway}.png: the glass fills the dark-wood
  frame exactly, set back in the recess, clear of both curtains; the room shot matches the user's screenshot view
  (rug, white chairs, curtains, posters) with the frame now glazed. Hallway left wall: plain, our mirror gone.
  Only flaw: a hairline of brick showed round the 3.81 x 2.09 glass (1 cm margin) in the front shot.
- Fix after the run (not re-shot, geometry-safe): glass 3.835 x 2.115 m = 3 mm into the reveals all round (anything past
  the reveal is inside the wall, hidden from every viewpoint in front). Log text reworded. Compile 0 errors.
- Open: in the client the live reflection needs RrMain's "Default Mirror" carrier (present in the real world scene per
  RrMirror); range 10 m -> the planar mirror renders from anywhere on the rug/couches while the glass is in view.
