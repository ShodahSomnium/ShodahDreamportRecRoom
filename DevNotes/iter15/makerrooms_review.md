# iter15 - Maker rooms correctness review (reviewer agent)

Scope: Places/MakerRooms/*, Interact/MakerPen/* (build tools), Watch Create/Rooms/Maker pages + keyboard.
Method: read + reason through the real flows (never run; no harness). Compile after each fix
(DevNotes/tools/compilecheck/build.sh project --editor).
Resume rule: read the log below and continue from the last step.

## Log
- 2026-10-17 started. Reading contract + core.

## Findings (severity: CRIT = breaks the feature / crash, HIGH = wrong for users, MED, LOW)

## Fixes made

## Remaining risks

### Findings so far (core read: contract, wire, store, catalog, transfer, rooms, room(+Net,+Objects), templates)
- F1 HIGH build budget: RrMakerRoom.Build waits up to 25 s (live state) + the base (Park: 61k plates, RrSceneKit
  LoadTimeout 50 s alone) + 60 s (spawns) inside travel's 90 s BuildTimeout (RrTravel.BuildTimeout, Guard kills the
  whole room -> falls back to the Dorm). Also a base routine's nested IEnumerators were yielded up to the travel Guard,
  so an exception inside RrJumbotronArea/RrSceneKit failed the whole room instead of "base failed, objects still built".
  -> FIX: own deadline (started + 80 s) for every wait; the base is stepped by the room itself (nested stack, catches
  exceptions, abandons the base past its share of the budget and goes on with the objects).
- F2 MED world close: Unity destroys the room's GameObject in any order relative to RrMain; RrMakerRoom.OnDestroy
  clears Current first, so RrMakerRooms.Shutdown's "save the dirty room" usually finds nothing -> unsaved edits lost.
  -> FIX: OnDestroy saves when Built && dirty && owner/co (OnLocalExit already saved and cleared dirty on travel).
- (checked OK) prefab shell layout vs install (rrtool rebuilt into scratch from DevNotes/tools/rrtool_cache):
  (Sandbox_Floor) mesh 15 x 0.4 x 15 centred; (Sandbox_Wall) mesh x +-7.5, y 0..8.2, z +-0.2, fbx child x -0.19
  (template adds +0.19 along the wall: centred); (Sandbox_Wall_Window) collider 15 x 8.21 x 0.4, mesh z -0.31..+1.03
  (frames stick out on +z = outside with yaw -90 at x -7.5), prefab root yaw -90 and (Sandbox_Ceiling) root y 8.19 are
  overridden by RrSpawn (localPos/localRot set) - the template's poses already account for that; whole frame shifted
  -0.2 so the floor top is y 0. Bounce lights sit at the floor. Window side still a GUESS (Basement level not converted).
- (checked OK) Dorm kit snapping: container = kit pose K, root.local = K^-1 * snap -> world = the Dorm's snap pose.
- (checked OK) Park spawn (-13.5, -3.8, 75): door markers at (-13.47, -3.88, 71.05); terrain under it h=6 above oy
  -11.0 -> y -5.0; the ray from y -1.8 down 15 m finds the terrace/terrain. Inside the terrain x -215..187, z 45..397.
- F3 MED late joiner vs a building authority: the live-state request failed after the 6 s chunk stall although the
  authority only answers once its own room is built (Park/Performance Hall take far longer) -> joiner without the file
  ended in the Dorm. FIX: state requests wait for the first chunk up to 60 s in RrMakerTransfer (the room's StateWait
  25 s decides); the room's wait loop stops when this client became the authority, and asks once more when the
  authority changed before any chunk came.
- F4 MED join without a pick (doors / Play page / People) into an EMPTY instance whose instance ints still name an
  earlier room this PC does not hold -> Build threw -> Dorm. FIX: no pick + nobody here + not stored -> the player's
  newest own room / a new Maker Room (as for an unnamed instance).
- F5 LOW RrMakerWireState.FromPacked: RrMakerStore.Check removed broken objects from Objects only, shifting the
  parallel Revs/Authors lists. FIX: filtered together.
- F6 MED pen (desktop): a new prop clicked onto the floor and then dragged elsewhere got its "settle onto the floor"
  fit after release -> jumped to the first floor point's height (up to 5 m). FIX: a moved drag drops the settle fit.
- 2026-10-17 fixes F1-F6 applied, compile 0 errors (364 files).
- F7 HIGH split rooms on stale instance ints: the authority wrote the room id into the instance ints only at the END
  of its build. Two players arriving together in an instance number that earlier hosted another room: the one who
  finished first (non-authority) saw the old id for > 3 s -> CheckConflict re-opened the room elsewhere (the two
  split); a joiner without a pick read the old id and then rejected the authority's live state ("names another
  room"). FIX: WriteInstanceState right after the room is resolved (step 4, before the base); CheckConflict ignores
  ints that disagree while the authority's presence int 19 says it plays this room; without a pick the authority's
  live state is accepted whatever id the ints named.
- F8 LOW MyHash fallback (Bridge Properties.Id) hashed an empty id when the client had not filled it yet and cached
  it (FNV of "" = the same "owner" for everyone). FIX: empty id = not yet. (RrNetRoom's presence int 6 has the same
  pattern - not mine; the access gate waits for the client's player first, so likely never hit.)
- compile 0 errors (367 files).
- F9 MED a scene base cut short / failed (F1) named no spawn -> players stood at the template's raw DefaultSpawn
  height with maybe no floor. FIX: base without spawns -> DefaultSpawn grounded by the ray on whatever was built.
- F10 LOW perf RrMakerStore.Write parsed the whole stored file (PeekInfo) on every save/auto-save just to compare
  versions. FIX: the catalogue's held version/time (kept in step with every write) unless the room is unknown.
- compile 0 errors (369 files).

## Checked and found correct (no change)
- Contract vs tools: positions room-local (pen's data frame = the Objects group = room origin), shapes = unit boxes
  centred on the container (core FitUnit re-centres ExtrusionShape_* min-corner meshes; pen AssumedUnit centred +
  measure-and-correct net), ink Width = diameter / Points container-local (pen subtracts the stroke centre, identity
  rotation), lights Scale = (range, intensity, angle) never on the transform (pen Scaled()/ScaleDrag keep z and skip
  localScale; core ApplyTransform keeps scale 1), Frozen = spec 2.3 (no grab/move/clone/recolour/delete; Physics mode
  unfreezes). Template pieces Frozen=false. All 156 prefab names used by templates + pen catalogue exist as install
  prefab roots (objects.tsv GameObject*).
- Wire sizes: chunk 460 (+9 frame/event header = 469 <= 480), catalogue messages packed to <= 460, settings <= ~390,
  edits split into <= 460-byte OpPart pieces (255 pieces max; ink capped at 2000 points by Sanitize, pen makes <= 200).
  CRC-32 + total length checked before use; joiner per (sender, msg id), 30 s expiry.
- Revisions: (rev, author) total order, transform ops and deletes checked against entries and tombstones; edits that
  arrive during the build are buffered and applied after the state (handlers registered at AddComponent, after Bind,
  before the state request) -> late joiners converge.
- Routing: SendDirect = SendToPlayer when unbound or same instance object, else SendGlobal + target id (receivers filter
  on target). Own broadcasts loop back and are ignored by sender checks.
- First run: Root creates MakerRooms/ and history/, missing catalog.json -> empty, Write on a missing file works,
  Store null-safe when there is no folder (room lives until left). JsonUtility: uint/long/byte/enum/Color32/List
  fields all serializable; null Points/Text come back as []/"" and Check() turns them into null.
- Lifecycle: travel calls OnLocalExit (save, host cleared, presence int cleared) before destroying the room and builds
  the next one after; the pen is put away when its room is left; statics cleared in RrMakerRooms.Shutdown,
  RrMakerEdits.Reset, RrMakerPen.Shutdown (ShapeUnits), RrWatch.Shutdown (keyboard); objects, owned meshes, grid host,
  pen previews/palette/gizmos all live under the room and go with it.
- Templates: Maker Room shell (see above), Dorm kit snap, Ink Space hull (0, 3.24, 0) + stage (2, 0.62, 4) exactly as
  3D Charades places them (+ the same door hiding and light-grid handling), Park spawn, Performance Hall spawn (0, 0, -9)
  inside its bounds (x -16..16, z -20.7..10.3), Bowling / Hangar spawns from their games.

## Remaining risks (not fixed; for the headset test)
- Big rooms and bandwidth: transfers run at ~2 chunks / 50 ms (~18 KB/s) shared by everything a client sends. JSON
  floats bloat ink (a room with hundreds of strokes can pack to hundreds of KB -> a late joiner may need longer than
  the 25 s state wait and then builds from its stored copy; every member re-downloads the file after each save).
- Park base time is still unmeasured: if RrSceneKit needs > 72 s (minus a joiner's state wait) the base is left
  partial (now instead of the whole trip failing). Check the log line "base took longer".
- Ink selection uses the stroke's bounding box (Grabbable-layer BoxCollider): a big curved stroke catches rays aimed
  at objects inside it.
- RrMakerPenBuild refreshes every object's bounds once a second (GetComponentsInChildren per object): a hitch in rooms
  with thousands of objects.
- Edits made under "Everyone can build" while no owner/co-owner is present are never saved (as designed).
- Desktop: digits 1-8, wheel, C/T/G/Y/F5/-/=/[/]/,/. while holding the pen in a room you build in; VR: A/X undo and
  thumbstick resize also reach the client (jump / locomotion) - as noted in iter14.
- Window-wall side of the Maker Room is a guess (frames point outward, which is consistent either way).
- Two rooms that pick the same free instance number at the same moment still settle by the non-authority re-opening
  (now only once the authority has resolved its room).

## Summary of fixes (files)
- Places/MakerRooms/RrMakerRoom.cs: build budget (BuildBudget 84 s / BaseBudget 72 s, own nested stepping of the
  base with exception isolation), state wait re-ask / stop when authority, stale-instance-id fallback, early
  WriteInstanceState, accept the authority's state without a pick, grounded spawn fallback, save in OnDestroy,
  FromPacked keeps revs/authors aligned. (F1, F2, F3, F4, F5, F7, F9)
- Places/MakerRooms/RrMakerRoom.Net.cs: CheckConflict trusts the authority's presence int 19. (F7)
- Places/MakerRooms/RrMakerTransfer.cs: state requests wait up to 60 s for the first chunk. (F3)
- Places/MakerRooms/RrMakerRooms.cs: MyHash ignores an empty Bridge id. (F8)
- Places/MakerRooms/RrMakerStore.cs: Write compares with the catalogue instead of parsing the file. (F10)
- Interact/MakerPen/RrMakerPenBuild.cs: a moved new prop drops its floor-settle fit. (F6)
- 2026-10-17 DONE. Final compile (build.sh project --editor): 0 errors, 369 files, API guard proven.
