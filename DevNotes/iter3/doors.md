# iter3 / doors - Rec Center category doors, room list panel, Magic Door, Dorm final changes

Status: DONE (compiles: 0 errors x3, all 113 members proven; 3 harness runs: doors_test, doors_all x2)

## Published API (for "reccenter" - placed through this)

```csharp
namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
public enum RrDoorCategory { SportsPvp, Quests, CreativeCampus, FeaturedRooms }
public static class RrCategoryDoor {
  // A category door on a wall: door leaf + frame + sign; using it opens the room list in front of it.
  // localPos = floor centre of the doorway, 0.06 m in front of the wall; the door's +Z faces into the room.
  public static GameObject Create(Transform parent, Vector3 localPos, float yaw, RrDoorCategory category);
  // The Magic Door (Sandbox_MagicDoor, key 2a9): picks a random built activity and travels there.
  public static GameObject CreateMagicDoor(Transform parent, Vector3 localPos, float yaw);
  public static IReadOnlyList<Rooms.RrActivity> Activities(RrDoorCategory category);
  public static string Sign(RrDoorCategory category);  // "SPORTS & PVP", "QUESTS", "CREATIVE CAMPUS", "FEATURED ROOMS"
}
```

Placement notes (measured with rrtool, B43 (Sandbox_RoomDoor) 2ac / (Sandbox_MagicDoor) 2a9):
- The door is Rec Room's SINGLE room door (same leaf/frame meshes in RoomDoor, RoomDoorV2 and MagicDoor):
  frame `ActivityDoorSingle_Frame` 1.15 m wide x 2.14 m tall, depth z -0.046..+0.142 about its pivot; one leaf
  0.91 x 1.99 m. Rec Room's pivot is the hinge-side edge; ours is re-centred, so `localPos` IS the doorway centre.
- Footprint for a wall: 1.25 m wide (frame + sign), sign box top at ~2.62 m, nothing behind the root deeper than
  0.046 m (so 0.06 m in front of the wall = 14 mm clear of it).
- The room list panel opens 0.25 m in front of the door, 1.6 m wide, y 0.86..1.84 m: keep door centres >= 1.9 m
  apart on one wall and >= 1.0 m clear in front of each door (the panel and the player standing at it).
- Solid box (layer 23, Environment) over the door so it cannot be walked through; click box on layer 16.
- Create/CreateMagicDoor can be called any time after the room's Build started (the look loads async, falls back
  to a procedural door); the returned GameObject is the door root (destroy it with the room).
- Frame colour: teal (reference photo, 2024); Magic Door purple (GUESS). The sign text is Futura Bold Condensed.

## Progress log
- (start) notes file created, API published.
- (milestone 1) Code written and compiling: Places/RecCenter/RrCategoryDoor.cs (API + internal RrCategoryDoorView),
  Places/RecCenter/RrRoomListPanel.cs (door list + board mode), Places/RecCenter/RrDoorsTest.cs (hooks),
  Places/Dorm/RrDormBoard.cs (Play board = RrRoomListPanel board: Originals + 4 category tabs),
  Places/Dorm/RrDormRoom.cs (NUX posters on quads w/ Sandbox fallback, help panel, 1 armchair, no dresser ->
  dracaena, dresser props -> desk, SandboxMarkerSet on the loft at (-4.30, 2.75, -0.40)).
  Extra public API for reccenter (optional): RrCategoryDoor.Title/IsBuilt/MagicCandidates/TitleOf,
  RrRoomListPanel.Current/OpenForDoor/CreateBoard/CategoryTabs.
- (milestone 2, harness run 1 doors_test) all 19 steps OK. Fixed after looking at the PNGs: list title invisible
  (Jost line 1.45 x size overflowed a 90-unit label -> 60 in 100), Magic Door showed Rec Room's baked "Delight Me!"
  text mesh under ours (now every renderer under the sign but the sign box is off), white tabs invisible on cream
  (resting fill #EFE4DB), test hover cleared by the free-cursor rule (test flag).
- (milestone 3, runs 2-3 doors_all = doors_test + dorm_room) doors: 5 doors from Rec Room's prefabs (not procedural),
  list 9 cards / 3 built / 9 art, our ray and poke hit-tests land on the right card, COMING SOON pick does not travel,
  Featured = Stunt Runner, Rec Rally, Paintball, Golden Trophy, Dodgeball, Paddleball; Magic Door rolls among the 5
  built games with MagicDoor_Refresh (resident after the prefab loads). Dorm: posters 2 notebook / 0 fallback,
  marker set, no dresser, 1 armchair, board 6 on Originals (5 a row at 1.2x), help note shown. Help panel moved to
  x -1.71 (0.32 m wide) and the CoC to x -2.28 after run 2 showed the CoC (0.71 m wide) covering it.
- Known, not mine: DormTest basket "missed" although the ball crosses the rim 0.019 m from its centre (also in the
  12:09 dorm_room run before these changes): RrHoop/RrBall score trigger does not fire in the harness. The
  AG/Unlit-Additive-Soft door glow has no _TintColor in the harness editor (same for RrPortal): glow off there, the
  hover light and the ajar leaf still show.


## API update (orchestrator, ~14:50)
RrDoorCategory += MakerWorld, CampusJams (empty lists, "coming soon"). FeaturedRooms = Charades, Midnight, Little Monsters, Punch Up, Rec Rally, Stunt Runner. Tabs: Sports & PvP, Quests, Featured Rooms, MakerWorld, Campus Jams. See DECISIONS.md.
