# iter14 - Maker rooms CORE (rooms, templates, saves, sharing, watch pages)

Owner: the "core" agent. Parallel agent "tools" extends Interact/MakerPen/* and only calls RrMakerEdits.Host.
Spec: DevNotes/research/rr_maker_rooms.md (section 5). Contract: Places/MakerRooms/RrMakerContract.cs (public shape fixed).
Resume rule: if this file exists, read the log at the bottom and continue from the last step.

## Conventions the tools agent must know (binding)

- **Find(id)** returns the object's own container GameObject "Maker <id> <kind>", child of the room's "Objects" group,
  which stands at the room origin = world origin with identity rotation. So room-local == world coordinates, and the
  container's localPosition/localRotation ARE Position/Rotation. Move the container directly while dragging, then call
  Update(o) with o built from **Get(id)** (a copy) with Position/Rotation (and Scale where it is a size, see below)
  copied from the container. Never build o from scratch: Get keeps Asset, Color, Points, Text, Width...
- **Scale**: Prop, Shape, Ink, Text: container.localScale == Scale. **Light: Scale is NOT a transform scale**:
  Scale.x = range (m), Scale.y = intensity, Scale.z = spot angle in degrees (0 = 60). The light container keeps scale 1.
- **Shape**: Asset = "Cube", "Sphere", "Cylinder", "Wedge", "Pyramid", "Cone", "Hemisphere", "Donut", "Pipe",
  "HalfPipe", "Diamond", "Mound" -> install mesh ExtrusionShape_<Asset> (pivot at its min corner, unit box) which the
  core RE-CENTRES: the shape is a unit-size shape CENTRED on the container origin, so Scale = its size in metres per
  axis (a 1 x 0.1 x 2 m slab = Scale (1, 0.1, 2)). Unknown names / missing meshes fall back to Unity primitives or a
  generated wedge/pyramid. Material = RrLook.Solid(Color) (AG-MakerPenShape, lit by RrLightGrid). Collider: Box (Sphere
  for Sphere/Hemisphere-ish shapes), see physics below.
- **Ink**: Points are in the container's LOCAL space (use Position = first point or the centre and subtract it; or
  Position = 0 and world points: both work). Width = tube DIAMETER in metres (radius = Width / 2, min 2 mm). Built with
  RrMakerPenTube (read-only use). A thin box collider over the bounds for selection.
- **Light**: Asset "Point" or "Spot" (spot shines along the container's +Z). A small glowing bulb with a sphere
  collider is drawn only for players who can edit (so they can select it); everyone gets the light. Lights feed the
  room's Rec Room light grid (rebuilt 1 s after the last light change).
- **Text**: RrBuild.Sign, font size 1 at Scale 1 (about 0.25 m letters), width 4 m, Color; a thin box collider.
- **Prop**: install prefab by name (Asset), spawned under the container at identity (its own pivot). Maker-pen
  materials (AG/MakerPenShape) on it are painted with RrLook.Solid(Color); other materials are left alone.
- **Physics**: Environment / Sticky / Physical -> colliders on the Environment layer (players stand on them),
  kinematic; Decoration -> colliders on the Grabbable layer (rays and the pen hit them, the avatar walks through).
  Physical objects are NOT simulated yet (not networked): they stay where placed. Grabbable is stored, not acted on yet.
- **IdOf(c)**: any collider/renderer under a container (an RrMakerObjectMark on the container).
- **Ids**: object ids are (PlayerId & 0xFF) << 24 | counter; the core starts each prefix's counter above every id the
  room already has, so loaded rooms never collide. Template base pieces get ids 1..N.
- **CanEdit**: owner, co-owner, or the room's "Everyone can build". It can change at any time (settings) - poll it.
- **RrMakerEdits.Changed(id)** fires after every create/update/delete applied here (local AND remote edits, and once
  per object after the room is built or a state arrives).
- **Save()**: owner/co-owners only (false otherwise). Auto-save every 2 min when changed + on leave is the core's job.
- Template base pieces (Maker Room floor/walls/ceiling, the Dorm kit) are ordinary Prop objects with Frozen = false
  (Rec Room: deletable, "freeze the ones you keep"). Scene templates (Bowling Alley, Hangar) have a fixed scene base
  that is not objects.

## Plan

1. Notes (this file). Read the code: Rooms (RrRoom, RrRegistry, RrTravel), Net (RrNet, events, presence, instance
   state), Watch (Ui, pages, kit, cards, art), Places/Dorm, Games/Bowling + LaserTag + Charades builders, RrLook/RrBuild/
   RrSpawn/RrLightGrid/RrEnvironment, MakerPen tube. Install facts for the Maker Room shell (rrtool).
2. Files (Places/MakerRooms/, namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms):
   - RrMakerWire.cs: byte writer/reader, CRC32, deflate (DeflateStream, "RRZ1" + raw deflate), object/edit/meta/catalog
     encodings, id <-> 4 ints, room hash (FNV-1a 32 of the id).
   - RrMakerStore.cs: persistentDataPath/SomniumRecRoom/MakerRooms/<id>.json (latest, JsonUtility), history/<id>/
     v<version>.rrz (last 10), catalog.json; load/save/delete; the bytes that travel (RRZ of the JSON).
   - RrMakerCatalog.cs: known rooms (mine + others'), holders per room (user hash, name, version, last seen, live
     PlayerId), merge rules (highest version, SavedAt tie-break), deletion tombstones, event 24 encode/decode/broadcast.
   - RrMakerTransfer.cs: event 25 requests / event 26 chunks (kind 1 = room file, kind 2 = live room state), paced
     ~1 chunk per 50 ms per transfer (<= 2 per tick), 6 s stall -> next holder, CRC check. Routing: SendToPlayer when
     either side is unbound or both in the same instance, else SendGlobal with the target id in the payload (SendToPlayer
     is scoped to the sender's instance - see "Requests to other modules").
   - RrMakerRooms.cs: facade + driver: Init (from the registry hook) / Shutdown (RrMain), identity (Somnium user hash as
     presence int 6 computes it + RrPlayer.Nick), Create(template), Open(id) (download if needed, choose instance number,
     pending, GoTo), Delete(id), status line for the watch, catalog timer (join, save, every 60 s, new players).
   - RrMakerTemplates.cs: "MakerRoom" (the 6 shell prefabs, measured), "Dorm" (copy of RrDormRoom's placement lists +
     RrDormOutdoors + the dorm's lights), "InkSpace" (hull + stage prefabs), "Bowling" (Data/Bowling.bytes via
     RrJumbotronArea), "Hangar" (RrLaserTagLevel composition), "Park" + "PerformanceHall" = coming soon (slot marked).
   - RrMakerRoom.cs (+ .Objects.cs, .Net.cs): the RrRoom for RrActivity.MakerRoom, IRrMakerRoomHost. Build: resolve the
     room (pending / instance ints / presence int 19 / my latest / a new Maker Room), get the state (instance authority
     via event 28 if others are in, else the local file), build the template base, then the objects. Host set on enter,
     cleared on exit. Edits (event 27) with per-object revisions (rev, author) + tombstones; remote drags lerped.
     Meta (event 29): settings + "saved v". Instance state ints: custom 0-3 room id, 4 version, 5 flags; option Private.
     Auto-save 2 min when changed (designated saver = owner if present, else lowest-id co-owner) + on leave.
     Light grid over the objects. Conflict check (another room in my instance number -> re-open elsewhere).
   - RrMakerObjectMark.cs: the id on each container.
   - Rooms registry hook (RrRegistryMakerRooms.cs, partial RrRegistry): registers RrActivity.MakerRoom (Category
     "Create", networked, 12 players) and calls RrMakerRooms.Init.
3. Watch: RrWatchCreatePage (template cards + coming soon), RrWatchRoomsPage (My Rooms / Public Rooms, JOIN, DELETE,
   who is in it now, download status), RrWatchMakerPage (This Room > Room Settings: Save, Public/Private, Everyone can
   build, co-owners add/remove from players present, rename with an on-watch keyboard + desktop typing). Additive edits:
   RrWatchPageId (Create 6, Rooms 7, MakerRoom 8), RrWatchUi.Start AddPage x3 + ShowRoom(MakerRoom) -> Rooms page,
   Home tiles "Create" + "Rooms" (keep "Maker Pen"), This Room "Room Settings" button in maker rooms.
4. RrRegistry.cs: RegisterMakerRoom hook. RrMain.OnDestroy: RrMakerRooms.Shutdown().
5. Compile check (0 errors), notes, report.

## Requests to other modules (not mine to change)

- Net (RrNetEvents.SendToPlayer): it stamps the event with the SENDER's instance object, so a direct message to a
  player in another instance is dropped by the receiver. Worked around (SendGlobal + target id). Request: an
  `RrNet.SendToPlayerGlobal(playerId, type, body)` that sends one direct RPC with instance 0.
- Rooms (RrRoomInfo): an `Unlisted` flag so MakerRoom does not show in the Rec Center doors / Dorm board / Magic Door
  lists. Worked around: a GoTo(MakerRoom, -1) without a picked room joins the fullest public maker-room instance, else
  the player's latest room, else creates a new Maker Room for them.

## Status log

- 2026-10-14 step 1 done (reading). Install facts: (Sandbox_Floor) box 15 x 0.4 x 15 centred on its pivot (material
  Tile_Linoleum_Brown_Mat); (Sandbox_Wall) box 15 x 8.38 x 0.4, centre y 4.1, mesh child at x -0.19 (Paint_LightGrey/
  Dusk/Blue); (Sandbox_Wall_Window) the same 15 x 8.21 x 0.4, prefab root yaw -90; (Sandbox_Ceiling) prefab root at
  y 8.19 (floor mesh + 6 [Light_Flourescent] + Room Fill (hemisphere, r 21.4, 0.6) + 6 Ceiling Lights (r 6, 0.8) + 2
  Bounce Lights near the floor). None use the maker-pen material (no painting). Layout used: floor root y -0.2 (top at
  0), walls at +-7.5 (window wall at -x, yaw -90), ceiling root y 7.99. ExtrusionShape_* = unit box, pivot at min corner.
- 2026-10-14 step 2 (part): written RrMakerWire.cs, RrMakerStore.cs, RrMakerCatalog.cs, RrMakerTransfer.cs,
  RrMakerTemplates.cs, RrMakerRoom.cs (+ RrMakerWireState, RrMakerObjectMark), RrMakerRoom.Objects.cs. Stopped at the
  usage limit. RESUMED: remaining RrMakerRoom.Net.cs, RrMakerRooms.cs (facade + driver), KitPose lookup, registry hook,
  watch pages + additive edits, RrMain shutdown.
- NEW (coordinator): the user re-supplied the XAPK; Data/Maker_Park.bytes (level75, terrain H grid, 1.5 MB) and
  Data/Maker_PerformanceHall.bytes (level73) are converted and in the scene's data list -> Park and Performance Hall
  become real templates (RrJumbotronArea + RrSceneBank like Bowling). Sources: DN/refdata/apk/Maker_*.rrsk. Park
  terrain x -215..187, z 45..397, no spawn markers: spawn on the central field (raycast down). Durable data now in
  DevNotes: DN/apkdata, DN/rrtoolidx (install index), DN/apkwork.
- step 2 done: + RrMakerRoom.Net.cs, RrMakerRooms.cs, RrMakerDriver.cs, RrMakerObjectMark.cs, RrRegistryMakerRooms.cs;
  Park + Performance Hall are real templates (BuildScene over Data/Maker_*.bytes, spawn grounded by a ray). Park spawn:
  where its activity door stood (-13.5, 75) facing +z (door skipped); Performance Hall: audience (0, -9) facing the stage.
  RrRegistry.RegisterMakerRoom hook added; RrMain.OnDestroy calls RrMakerRooms.Shutdown (saves a dirty room first).
  Compile check: 0 errors (server-like, editor-like, editor tools), guard all proven.
- step 3 done: Watch/RrWatchCreatePage.cs (template cards incl. Park + Performance Hall, details + CREATE ROOM, MY ROOMS),
  Watch/RrWatchRoomsPage.cs (MY ROOMS / PUBLIC ROOMS, JOIN/GO with download, DELETE x2, Remove copy, SETTINGS for the
  current room; + RrWatchMakerUi chips), Watch/RrWatchMakerPage.cs (Room Settings: rename via keyboard, SAVE, Private/
  Public, Owners/Everyone build, Maker Pen, co-owners of the players here), Watch/RrWatchKeyboard.cs (on-watch keyboard,
  desktop typing). Additive edits: RrWatchPageId Create 6 / Rooms 7 / MakerRoom 8; RrWatchUi.Start AddPage x3,
  ShowRoom(MakerRoom) -> Rooms page, H/Backspace ignored while the keyboard types; RrWatch.Shutdown resets the keyboard;
  Home tiles Create + Rooms (7 tiles; Maker Pen kept); This Room: "Room Settings" button in maker rooms.
  Compile check: 0 errors.
- step 4: README section "Iteration 14: player-made rooms" + checklist appended. Light shafts lose their colliders;
  scene spawns are grounded by a ray from 2 m above the given height (not from 40 m: a roof would be taken as floor).
- step 5: tools agent's requests (makerrooms_tools.md): 3 done - IRrMakerRoomHost.Remaining (MaxObjects 3000 minus
  objects) added to the contract (additive) and implemented; 1 (physics simulation) open; 2/4 nothing to do. Contract:
  doc comments for Scale (Light), Width (diameter), Points (container-local) - the tools already use exactly these.
  Private rooms: Build refuses a private room to non-owners however they came (People > Join) -> travel falls back to
  the Dorm. Final compile: 0 errors (364 files), guard all proven.

## Final state (2026-10-14)

Files (Places/MakerRooms): RrMakerContract.cs (+Remaining, docs), RrMakerWire.cs, RrMakerStore.cs, RrMakerCatalog.cs,
RrMakerTransfer.cs, RrMakerTemplates.cs, RrMakerRoom.cs / .Objects.cs / .Net.cs, RrMakerObjectMark.cs, RrMakerDriver.cs,
RrMakerRooms.cs, RrRegistryMakerRooms.cs. Watch: RrWatchCreatePage.cs, RrWatchRoomsPage.cs (+RrWatchMakerUi),
RrWatchMakerPage.cs, RrWatchKeyboard.cs. Edited (additive): Rooms/RrRegistry.cs, Core/RrMain.cs, Watch/RrWatchPage.cs,
RrWatchUi.cs, RrWatch.cs, RrWatchHomePage.cs, RrWatchThisRoomPage.cs, README.md (iteration 14 section + checklist).
Not touched: RrTravel*.cs (not needed), Interact/MakerPen/*, Games/*, other Places/*. RrVersion not bumped (shared).

Templates: MakerRoom, Dorm, InkSpace (object bases) + Park, PerformanceHall, Bowling, Hangar (fixed scene bases).

## Open issues

- Not run anywhere (no harness by brief): the first headset test is the first run. Risky spots: the Park's build time
  (61k plates + terrain; travel's 90 s build timeout), the Maker Room wall layout (window wall side is a GUESS), the
  Dorm template's kit snapping relative to template poses, the spawn heights on the Park/Performance Hall (ray from
  2 m above the guessed floor height).
- Physics modes Physical / Sticky and Grabbable are stored and sent but not simulated (all objects kinematic).
- Object Material (Rec Room's 25 materials) is stored, not drawn: maker-pen surfaces use RrLook.Solid(colour) only.
- Restore of older versions: the last 10 are kept (history/<id>/v<n>.rrz, RrMakerStore.LoadVersion) but there is no
  watch UI for it yet.
- SendToPlayer is instance-scoped: transfers between players in different instances go out as global events with a
  target id (every client receives and drops them). Request to Net: RrNet.SendToPlayerGlobal.
- MakerRoom is a registered activity, so it also shows in the Rec Center doors / Dorm board / Magic Door lists (as
  "Maker Rooms"): a GoTo without a pick joins the fullest public maker-room instance, else the player's newest room,
  else a new Maker Room. Request to Rooms: an RrRoomInfo.Unlisted flag.
- A room is shared only while someone who holds it is in the world; a version saved while nobody else is around
  reaches others only when the saver (or a holder) is online again.
- Desktop typing on the rename keyboard reads Input.inputString; if the Somnium client also reacts to letter keys while
  the watch is open, use the on-watch keys instead.
- Identity is the Somnium user id hash (FNV-1a 32 of Properties.Id, as presence int 6); in the editor without a client a
  hash of the nickname stands in (rooms made in the editor belong to that).
