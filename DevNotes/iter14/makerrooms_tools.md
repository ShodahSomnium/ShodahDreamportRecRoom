# iter14 makerrooms_tools - Maker Pen build tools for maker rooms

Owner: the "tools" agent. Owns Interact/MakerPen/* only. Talks to the room only through
Places/MakerRooms/RrMakerContract.cs (RrMakerEdits.Host / Changed). The "core" agent owns Places/MakerRooms/* and
the watch pages.

## Plan
1. Read: contract, all of Interact/MakerPen, spec rr_maker_rooms.md 2 + 5.3, helpers (RrInput, RrHand, RrGrab,
   RrSound, RrLook, RrBuild, RrSpawn/RrAssets, toast/log), the install index (ExtrusionShape_*, SpawnableToolTex_*,
   maker pen sprites + sounds).
2. Catalogs (new file RrMakerPenCatalog.cs): shapes (exact ExtrusionShape_* suffixes), ~80 curated props in Rec
   Room's categories (install prefab names that exist + SpawnableToolTex_* thumbnails), colours (16 + rows).
3. Build mode (new file RrMakerPenBuild.cs, a component on the pen next to RrMakerPenTool): modes Draw, Shapes,
   Props, Select, Clone, Recolor, Delete, Physics; snap; undo/redo 10 steps (own edits; create/update/delete
   inverses); save + toast; hover highlight (MaterialPropertyBlock tint); laser from the tip; sounds; haptics.
4. Palette (RrMakerPenPalette): when RrMakerEdits.CanEdit, extra rows (mode keys, shape/prop pages, +/- , snap,
   undo/redo, save) + a mode readout; screen mode: HUD strip + keys.
5. RrMakerPenTool: route the trigger to the build mode when CanEdit; ink strokes become Kind=Ink objects
   (Create with points/width/colour) instead of ephemeral ink in maker rooms.
6. Compile check (build.sh project --editor), log, report.

## Log
- step1 done (research):
  - Contract read. Pen code read (Tool/Palette/Ink/Wire/Tube/Service/Test). Helpers: RrInput (desktop: play mode
    while holding; E drops the held pen, Q-hold throws it -> Q/E can NOT rotate; free keys: digits, [ ], , . - =,
    C, T, G, Y, F5...; wheel), RrHand (Trigger, PrimaryDown; thumbstick via RrXr.Controller + CommonUsages.primary2DAxis),
    RrHighlight (property-block tint), RrLook (Solid/Unlit/Glass), RrBuild, RrAssets.Load<T>(name, classId),
    RrSound.Play(name, pos, vol). No global toast API -> the build palette shows its own toast line.
  - ExtrusionShape_* meshes (bundle 1df8e0ef): Cube, Sphere, Cylinder, Pyramid, Wedge, Cone, Hemisphere, Donut, Pipe,
    HalfPipe, Diamond, Mound (+ _CenterPivot for Sphere/Hemisphere). Mesh AABB = centre (0.5,0.5,0.5) extent 0.5:
    UNIT box with the pivot at the MIN CORNER (checked Cube + Sphere with rrtool dump). -> the tools measure the built
    object once per shape and correct (works whatever pivot the core uses).
  - Sounds: sfx_ui_prop_spawn_01..04, sfx_ui_maker_pen_created_object_{place_01, pickup_01, delete_01..05,
    apply_color_01..04}, sfx_ui_maker_pen_magnetic_snap, sfx_ui_maker_pen_draw_primitive_loop_medium_01,
    comped_sfx_rrui_watch_ui_select_negative_01 (denied).
  - Menu icons: Sprites MakerPen_Sprites_{Create, Select, Move, Rotate, Scale, Clone, ReColor, Configure, Freeze,
    Delete, Undo, Redo, Snap, Tube, Cube, Sphere, Cylinder, Wedge, Pyramid, Settings, Done...} in atlas
    "MakerPen_Sprites" (bundle f124b399). Prop thumbnails: SpawnableToolTex_<ENUM> (Texture2D + Sprite), 603 of them.
  - 60 Rec Room colours: only an image on the wiki (Colors33.png), no values -> keep the 16 + 16 more (32).
- step2 done: RrMakerPenCatalog.cs (modes, 12 shapes, 111 props in 8 categories Furniture/Decor/Nature/Lights/
  Kitchen/Sports/Toys/Building incl. Point/Spot light gadgets as Kind=Light; every prefab + thumbnail checked against
  spawn.tsv / stt.txt - scratchpad makertools/props.tsv). RrMakerPenWire.Palette 16 -> 32 colours (0-15 unchanged,
  +light row +dark row); RrMakerPenPalette (ephemeral) now 8 columns x 4 rows.
- step3a: RrMakerPenHistory.cs (10 steps, create/update/delete inverses, id remap when a re-create gets a new id,
  Amend for post-build fits) + RrMakerPenGizmos.cs (RrMakerPenMeasure: world/oriented bounds; gizmos: laser,
  outline box (LineRenderer path over 12 edges), glass shape preview box).
- Design decisions (step3, RrMakerPenBuild.cs):
  - Data frame = parent of any built maker object (else the room): o.Position/Rotation are local to it.
  - Drags track the object's visual CENTRE relative to the pen (VR) / the crosshair (screen) so scale/rotate keys
    turn and grow around the centre; pivot derived; snap = pivot to 5 cm, rotation Euler to 15 deg.
  - Scale while dragging: localScale *= newFactor/oldFactor (proportional, so it works whatever the core's
    Scale->localScale mapping is); Update() every 0.2 s + on release.
  - Shapes: preview box while the trigger is held (tap = 25 cm shape at the tip, pull = box from press point to tip);
    created on release; the built object is measured once per shape name (unit centre/size) and corrected.
  - Props: trigger creates at the tip (desktop: on the surface under the crosshair) and the prop follows until
    release; desktop floor placements settle the prop's bounds bottom onto the floor once loaded.
  - Ink in maker rooms: local preview tube while drawing (uniform width), Kind=Ink object on release (Position =
    stroke centre, Points object-local, Width = diameter); max 200 points per object, longer strokes continue.
  - Frozen (Rec Room semantics): Select/Clone/Recolor/Delete refuse with a toast; Physics mode can unfreeze.
  - Desktop keys: E drops / Q-hold throws the held pen in play mode, so rotation is on [ ] (turn) and , . (tilt).
- step3b: wrote RrMakerPenBuild.cs (modes, targeting by touch/laser/crosshair + bounds for collider-less objects,
  drags, shapes, props, ink objects, recolor/delete/physics, history, fits, visuals, sounds, toast, desktop keys)
  and RrMakerPenBuildPalette.cs (+ RrMakerPenArt: icon/thumbnail materials). RrMakerPenTool routes to the build when
  held && RrMakerEdits.CanEdit. RrMakerPen.Shutdown clears the new statics. .meta files made.
- step4: compile check next.
- step4a: compile of a project copy without the core's in-progress Places/MakerRooms files (contract kept):
  0 errors (348 files) - my files are clean. Full project then failed only in core files (RrMakerTemplates ->
  RrMakerRoom missing, mid-edit).
- step4b (review fixes): per-id bounds refresh on Changed (full refresh 1 Hz); Selected outline only in
  Select/Shapes/Props; desktop shape plane frozen at press; VR lights point along the pen; held objects' rigidbodies
  are kinematic while dragged (restored + woken on release/cancel/delete); unused vars removed.
- (resume after usage limit 2026-10-14) index now at DN/rrtoolidx; continuing with the compile + core alignment.
- step4c (core alignment, read-only look at Places/MakerRooms/RrMakerRoom*.cs): objects are containers under the
  room's Objects group, localPosition/Rotation/Scale = data (lights scale 1); shapes are re-centred UNIT boxes ->
  AssumedUnit now centred (0,0,0)/1 (the measure-and-correct fit stays as a safety net); ink radius = Width/2 (=
  ours); lights Scale = (range, intensity, spot angle) -> new lights (6, 1.5, 60), size steps change range +
  intensity only and never scale the light's container; Create honours a free Id (undo re-creates keep ids);
  IdOf = RrMakerObjectMark in parents. The core auto-gives the pen in a maker room.
- step4d: build cursor (small ball: tip / desktop draw point / crosshair surface spot in Draw, Shapes, Props);
  RrMakerPenTest.Build hook (cube create -> recolour+move -> undo/redo through RrMakerPenHistory).
  Isolated compile (project copy, core files except the contract removed): 0 errors.

## Files (Interact/MakerPen)
New: RrMakerPenBuild.cs (build modes brain), RrMakerPenBuildPalette.cs (+ RrMakerPenArt icon/thumbnail
materials), RrMakerPenCatalog.cs (modes, shapes, 111 props, brushes), RrMakerPenHistory.cs (undo/redo 10),
RrMakerPenGizmos.cs (RrMakerPenMeasure + laser/outline/preview/cursor) + .meta each.
Edited: RrMakerPenTool.cs (hands the frame to RrMakerPenBuild when held && RrMakerEdits.CanEdit),
RrMakerPenWire.cs (32 colours), RrMakerPenPalette.cs (8 columns), RrMakerPen.cs (Shutdown clears new statics),
RrMakerPenTest.cs (Build hook).

## Controls (maker room with build rights; elsewhere the pen is unchanged: ephemeral ink)
VR (pen hand trigger acts; palette above the other hand, touch keys with the pen tip):
- Palette: 8 modes (Draw, Shapes, Props, Select, Clone, Recolor, Delete, Physics) with Rec Room's pen icons; Snap,
  Undo, Redo, - / + (size of the held or last selected object), Save; mode page: 32 colours / 12 shapes + colours /
  8 prop tabs + 6x3 thumbnails + < > pages / 6 physics settings; readout, toast, hints.
- Draw: trigger draws a tube (pull harder = thicker, whole stroke), release = room object.
- Shapes: tap = 25 cm shape at the tip (upright, facing the pen); hold + pull = box from press point to tip.
- Props: trigger spawns the prop at the tip; it follows the pen until release.
- Select: touch or laser (from the tip, 6 m) + hold trigger: follows the pen (position + rotation); either
  thumbstick up/down resizes; release places. Clone: same with a copy (a tap puts the copy beside the original).
- Recolor / Delete / Physics: touch or laser + trigger. Physics applies the chosen setting (Decoration,
  Environment, Physical, Sticky) or toggles Freeze / Grabbable. Frozen objects refuse Select/Clone/Recolor/Delete.
- A/X: undo.
Desktop (crosshair, 8 m reach, play mode while the pen is held):
- 1-8 modes, LMB act (hold to drag / stretch), RMB cancel a drag, Z undo, Y redo, C / Shift+C colour,
  wheel = next shape / prop / colour / physics setting (while dragging: size; Shift+wheel: distance),
  T / Shift+T prop category, - / = size, [ / ] turn 15 deg, , / . tilt 15 deg, G snap, F5 save.
- Shapes: click = 25 cm shape on the surface under the crosshair; drag = footprint slab (25 cm thick) on that
  surface (in the air: a camera-facing panel). Props: click places on the surface (floor placements settle the
  bounds bottom onto the floor), hold = drag. HUD in the lower left (readout, modes, tools, toast, key hints).

## Requests for the core (Places/MakerRooms)
1. Physics modes: Physical / Sticky / Grabbable are only stored today (every prop spawns kinematic). When they
   get simulated: keep an object kinematic while the local pen drags it (the tools already make its rigidbodies
   kinematic during a drag and restore them), and do not let a remote simulation owner overwrite a pen drag.
2. Frozen: the tools use Rec Room's meaning (cannot be moved/cloned/recoloured/deleted with the pen; Physics mode
   unfreezes). Template pieces are created Frozen=false - fine.
3. Optional API: IRrMakerRoomHost.Remaining (objects left before MaxObjects) for a palette "ink" bar; today a full
   room just makes Create return 0 (the tools toast "The room did not take ...").
4. Text objects: the tools never create Kind=Text (no keyboard in VR); they select/move/delete them fine.

## Open issues
- Not play-tested (no harness runs by brief). Palette sizes/fonts and the desktop HUD placement are first guesses.
- VR thumbsticks also drive Somnium's locomotion/turn: resizing with a stick moves the player too (palette +/-
  is the clean way). A/X undo also triggers the client's jump/inspector (as the iter13 pen).
- Desktop rotation is on [ ] , . because E/Q belong to the grab system (drop/throw the pen).
- Desktop shapes are slabs (footprint + 25 cm); resize is uniform only (no per-axis scale yet).
- The 60 Rec Room colours are not in the bundles: 32 GUESS colours.
- Prop thumbnails come from SpawnableToolTex_*; a few enum<->prefab pairings are guesses (curtain, standee).
- step5 DONE: full project compile (build.sh project --editor, with the core's finished files): 0 errors, 1 old
  warning (ThirdParty Mirror), 360 files; editor-like + editor tools 0 errors; API guard all proven.

## Harness steps (integrator, in a maker room you own)
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Give"}
{"expect_log": "build tools on", "timeout": 5}
{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen.RrMakerPenTest.Build"}
{"expect_log": "MakerPenTest: build created", "timeout": 5}
