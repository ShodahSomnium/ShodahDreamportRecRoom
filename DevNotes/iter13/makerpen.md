# iter13 makerpen - Maker Pen (basic form, networked)

## Plan
1. Read RrCharadesPen / RrCharadesInk / RrTools / Net layer / Watch pages.
2. Find the install maker pen prefab (SP/rrtool/idx) + maker pen material + colours.
3. Interact/MakerPen/: RrMakerPen (holdable tool, trigger draws), RrMakerPenInk (tube strokes),
   RrMakerPenNet (stroke sync: owned by author, late join, cap, clear on leave), palette, undo/erase.
4. Watch: a button to give/take the pen.
5. Compile check.

## Log
- step1 done: read Charades pen/ink, RrTools, RrGrab.Give, RrLoadout, RrNet events (Send = my instance only, loop-back;
  Dorm unnetworked -> local only), RrTravel.CurrentRoom, watch Home page (4 tiles + quick access).
- step2 done: install pen = "(MakerPen)" container key 5, bundle 4056f2fc (go#3543397827117124696): MakerPen_fbx
  (SkinnedMeshRenderer MakerPen: MakerPen_mat + WatchUI_Holographic_Dwell_Mat overlay), Colliders/Body+Handle,
  ExtrusionPoint (0,0.036,0.152), MenuAnchor/ToolMenu, LaserPointer, child tool MakerPenPaletteMoveableToolMenu
  (own Rigidbody+Tool -> destroy). TapToOpen on the pen mesh (Rec Room: tap pen with other hand opens its menu).
  MakerPen.defaultColor = 1. Swatch colours are runtime/server (no data in bundles) -> palette GUESS (16 colours).
  Sounds: sfx_ui_maker_pen_draw_tube_ribbon_loop_01, sfx_ui_maker_pen_created_object_delete_01..05, sfx_ui_prop_spawn_01..04.
## Design (step3)
- Event ids 20-23 (shared catalogue range 14-31 is free; games reuse 64+): 20 Points, 21 Delete, 22 Resend, 23 PenPose.
  Request for integrator: list them in Games/Shared/RrGameEvents.cs header comment.
- Files: Interact/MakerPen/ RrMakerPen (static facade: Toggle/Give/PutAway), RrMakerPenService (on the watch root;
  follows RrTravel.CurrentRoom, net handlers, member cleanup, resend, remote pen ghosts), RrMakerPenInk (canvas under
  the room: strokes per author, tubes, hit test, caps), RrMakerPenTool (on the spawned pen: draw/erase/undo/palette),
  RrMakerPenPalette (swatch panel), RrMakerPenWire (ids+codec+colours), RrMakerPenTest (harness hooks).
- Watch: RrWatch.Init adds RrMakerPenService to the watch root (1 line); Home page gets a 5th tile "Maker Pen".
- (resume 2) re-read APIs. Refined design:
  wire: 20 Points [stroke u16][colour u8][start u16][n u8][base x,y,z s16 cm] + n x (dx,dy,dz s16 mm from base, r u8 0.25mm)
        21 Delete [stroke u16] (0 = all mine); 22 Resend [] (newcomer asks, authors reset Sent, throttled 2 s);
        23 Pen [held u8][pos s16 x3 cm][rot s8 x4] 5 Hz while held (remote ghost pen, visual copy).
  coords room-local (rooms stand at world origin). Own loop-back ignored (sender == LocalId).
  caps: 240 pts/stroke (continue as new), 200 strokes + 8000 points per author (receivers enforce too).
  controls VR: trigger draw (pull = thickness), A/X undo, palette on the OFF hand (own swatch spheres, no
    RrPressable - off hand would self-poke), pen tip touches swatch = pick colour / eraser / undo / clear;
    eraser mode: trigger + tip touching one of MY strokes deletes it.
  desktop: LMB draw at crosshair 0.7 m, Z undo, RMB (play mode) delete my stroke under the crosshair,
    1/2 prev/next colour, 3 eraser toggle (LMB then deletes under crosshair), 4 clear mine; palette = HUD strip.
  pen release: stays hovering where dropped (kinematic); watch tile toggles give / put away.
  erase: own strokes only (ownership). author leaves my instance -> their strokes removed. generation change -> clear.
- step3a: wrote Interact/MakerPen/RrMakerPenWire.cs (ids, codec, 16-colour palette) + RrMakerPenTube.cs (tube mesh, hit math)
- step3b: wrote RrMakerPenInk.cs (per-author strokes, caps, flush, hit tests, remote apply/delete)
- step3c: wrote RrMakerPenService.cs (room canvas, handlers, members, resend, ghost pens) + RrMakerPen.cs (facade: Toggle/Give/PutAway/StripExtras/Shutdown)
- step3d: wrote RrMakerPenPalette.cs (VR off-hand board touched by tip / screen HUD) + RrMakerPenTool.cs (draw/erase/undo/keys/sounds/pose). Held pen is parented to the grip -> palette+cursor live under the Room; tool puts itself away when Room != CurrentRoom
- step4: watch: RrWatch.Init adds RrMakerPenService to the watch root; Shutdown calls RrMakerPen.Shutdown; Home page 5th tile 'Maker Pen' (Illustration_Menu_Create) toggles pen, label 'Put Away' while out. Test hooks RrMakerPenTest.cs written.
- step5: compile 0 errors (first run, 314 files). Second run had 4 errors in another job's RrBowlingBall.cs (in progress) - not mine; retrying.
- step5 done: palette tweaks (key labels in front, no shadows); compile of a copy without the in-progress
  Games/Bowling (another job's RrBowlingBall.cs missing RrBowlingRoom): 0 errors, 313 files. Full project build had
  0 errors before Bowling appeared (314 files).

## Files (all new unless noted)
Interact/MakerPen/: RrMakerPen (facade, Toggle/Give/PutAway, StripExtras), RrMakerPenService (on the watch root),
RrMakerPenInk (canvas under the room), RrMakerPenTool (on the pen), RrMakerPenPalette, RrMakerPenWire, RrMakerPenTube,
RrMakerPenTest (+ .meta each, folder meta).
Watch/RrWatch.cs (edited: Init adds RrMakerPenService; Shutdown calls RrMakerPen.Shutdown),
Watch/RrWatchHomePage.cs (edited: 5th tile "Maker Pen" / "Put Away", Illustration_Menu_Create; row width for 5 tiles).

## Controls
VR: trigger draw (pull = 3-20 mm), A/X undo, palette above the other hand - touch with the pen tip: 16 colours,
  Erase (then trigger + tip touching one of your strokes deletes it), Undo, Clear.
Screen: LMB draw at the crosshair 0.7 m out, Z undo, RMB delete your stroke under the crosshair, 1/2 colour,
  3 eraser (LMB deletes), 4 clear. HUD palette lower-left shows colour/mode.
Released: hovers; 60 s alone or leaving the room -> put away.

## Requests for the integrator
1. Games/Shared/RrGameEvents.cs header: list "20-23 Maker Pen (Interact/MakerPen/RrMakerPenWire: Points, Delete,
   Resend, Pen)" in the 1-31 shared catalogue so nobody reuses them.
2. README.md: a short Maker Pen paragraph (watch Home tile; controls above; networking = instance events, per-author
   ownership, late-join resend, caps 150 strokes / 6000 points per player, cleared on leave/reset).

## Harness steps (integrator)
{"goto": "Dorm", "timeout": 120}
{"expect_log": "ink ready in", "timeout": 10}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Give"}
{"expect_log": "MakerPenTest: pen given", "timeout": 5}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Draw"}
{"expect_log": "(wire exact)", "timeout": 5}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Erase"}
{"expect_log": "erased by touch True, undo True", "timeout": 5}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.PutAway"}
{"goto": "RecCenter", "timeout": 150}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Give"}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Draw"}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Report"}

## Open
- Palette colours are a GUESS (Rec Room fills swatches at runtime; not in bundles).
- Erase deletes only your own strokes (ownership); nobody can erase another player's ink.
- Remote players' drawing has no loop sound (only the author hears the tube loop).
- A/X undo shares the button with the client's jump/inspector (same as the Charades Pen).
- DONE (basic form). Not play-tested (builders do not run the harness).
