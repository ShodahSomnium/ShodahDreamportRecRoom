# iter8 / console - Rec Room's game-start hologram (who is in the room) + START

Owner: console agent. Owns `Games/Shared/RrGameConsole*.cs`, the console integration in `Games/Dodgeball/*`,
`Games/Paddleball/*`, `Games/Charades/*`, scenarios `console_*.json`.

## Status
- [x] 1 research: the install's (ScoreboardProjector) + [GameScoreboardView], the levels' G scoreboard markers
- [x] 2 RrGameConsole written (API below is PUBLISHED and stable; compiles)
  - static helpers for your own labels: RrGameConsole.PhaseStartLabel(room) / PhaseTimer(room) / PhaseLine(room)
- [x] 3 Dodgeball / Paddleball / Charades integration written (old pedestals / podium lobby buttons removed):
  Dodgeball (4.4, 0, -9.95) yaw -90 (arrival row turned to face it, yaw 90), START alone = PRACTICE / STOP PRACTICE;
  Paddleball (2.7, 0, -3.3) yaw 40 (aisle, facing the gallery), START alone = PRACTICE;
  Charades Ground(-0.6, 0.65, 0.2) yaw 270 (front corner of the stage), modes Co-op Classic / Team Lightning.
  Scenario: SP/harness/scenarios/console_test.json (waiting for the Jumbotron files to compile before run 1)
- [x] 4 verify (harness runs used: 3/3 + one that died at a Golden Trophy compile error in 8 s): compile 0 errors x3, 118 members proven

## API (namespace `SomniumSpace.Worlds.SomniumRecRoom.Games`, file `Games/Shared/RrGameConsole.cs`) - STABLE

```csharp
// What the console shows and does. Every Func is read on each refresh (every 0.5 s and on Refresh()).
public sealed class RrGameConsoleSpec
{
    public string Title;                        // the yellow title cell, upper-cased; null = room.Info.Title (ForGame) or "REC ROOM"
    public int MinPlayers;                      // below it START is greyed + "N MORE PLAYERS NEEDED"; 0 = room.Info.MinPlayers (ForGame) or 1
    public int MaxPlayers;                      // 0 = no limit (ForGame: room.Info.MaxPlayers); the count cell shows "3 / 8"
    public int TeamCount = 2;                   // 0 = one list (co-op quests, FFA); 1..4 = team-coloured rows (RrTeams colours)
    public Func<IReadOnlyList<int>> Members;    // null: RrNet.MembersOfMyInstance() filtered by RrIsolation.IsWithMe
    public Func<int, int> TeamOf;               // null: RrTeams.TeamOf; -1 = not on a team yet (neutral row)
    public Func<int, string> NameOf;            // null: RrNet.Presence(id).Nick, else "Player <id>"
    public Action<int> OnStart;                 // START pressed by the LOCAL player (arg = RrNet.LocalId). Ask your authority here.
    public Func<bool> CanStart;                 // null: true. False greys START (e.g. not in the lobby phase)
    public bool StartBelowMin;                  // START stays pressable below MinPlayers (Dodgeball's solo practice)
    public Func<string> StartLabel;             // null: "START" (e.g. "PRACTICE", "IN PROGRESS", "START QUEST")
    public Func<string> Status;                 // footer line; null: automatic ("1 MORE PLAYER NEEDED" / "2-6 PLAYERS")
    public Func<string> Timer;                  // the timer cell ("0:05"); null: empty (ForGame: countdown / round time)
    public Func<string> Subtitle;               // the line under the title when there are no Modes; null: "WAITING FOR PLAYERS" (ForGame: by phase)
    public string[] Modes;                      // optional: the subtitle cell shows Modes[ModeIndex()] and is a toggle:
    public Func<int> ModeIndex;                 //   pressing it calls OnMode((index + 1) % Modes.Length) - you ask your
    public Action<int> OnMode;                  //   authority and ModeIndex() reflects the result on the next refresh
    public string[] Maps;                       // optional: footer "MAP: <name>" with a CHANGE button, same pattern
    public Func<int> MapIndex;
    public Action<int> OnMap;
    public bool Projector = true;               // Rec Room's own (ScoreboardProjector) from the install carries the panel
    public float Reach = 8f;                    // our aim-ray reach in metres (desktop crosshair / VR right-hand ray)
}

public sealed class RrGameConsole : MonoBehaviour
{
    // localPose = the level's G "scoreboard" marker (the projector's floor point) in parent space; its +Z faces the players.
    public static RrGameConsole Create(Transform parent, Pose localPose, RrGameConsoleSpec spec);
    // For RrGameRoom games: fills what the spec leaves null - Title/Min/Max from room.Info, OnStart = room.RequestStart(),
    // CanStart = phase Lobby, StartLabel ("START" / "STARTING" / "IN PROGRESS"), Timer (countdown / round time).
    public static RrGameConsole ForGame(RrGameRoom room, Transform parent, Pose localPose, RrGameConsoleSpec spec = null);
    public static IReadOnlyList<RrGameConsole> All { get; }
    public RrGameConsoleSpec Spec { get; }
    public bool IsOpen { get; }          // hologram open (projector animated open, panel shown)
    public int ShownPlayers { get; }     // rows on the panel now
    public string StartText { get; }     // START's label now
    public bool StartEnabled { get; }
    public void Refresh();               // re-read the spec now
    public void Close();                 // animates closed (PopDown sound) and destroys itself
    public bool Press(string how = "test");  // presses START as a player would (harness hook); false when greyed
    public void PressMode();  public void PressMap();
}
```

Networking: the console never writes state. START / mode / map presses call your callbacks on the pressing client
only; route them through your game's authority (RrGameRoom.RequestStart / RrNet.Send(..., toAuthority: true)).
Input: VR fingertip poke, VR trigger ray (RrInput.AimRay + UseDown), desktop crosshair aim + click, and the client's
own XR ray / free cursor through the canvas' GraphicRaycaster + TrackedDeviceGraphicRaycaster (as RrRoomListPanel).
Destroyed with its parent (the room). Only the current room exists, so no cleanup call is needed on leave.

Example (a quest foyer, co-op, no teams):
```csharp
RrGameConsole.ForGame(this, transform, new Pose(markerPos, markerRot),
    new RrGameConsoleSpec { Title = "THE GOLDEN TROPHY", TeamCount = 0, StartLabel = () => "START QUEST" });
```

## Findings (install + levels, reference only)
- `(ScoreboardProjector)` (bundle 43ea1176..., root #2378492940477835731): Animator (ScoreboardProjectorAnimController:
  bool "Open", float "Rotate" blend tree RotateNegative/Zero/Positive; states CloseIdle -> Open -> OpenIdle -> Close;
  clips 30 fps), UIProjector: viewPrefab = `[GameScoreboardView]` canvas parented at `viewParent` = ScoreboardViewRoot
  (under Plane.Jnt, which rests at scale 0 = closed); innerProximityTriggerRadius 1.2, outerProximityTriggerRadius 20,
  maxOpeningAngle 90, maxRotationAngle 60; sounds SFX_ScoreboardPopUp_Mono (0.2), SFX_ScoreboardPopDown_Mono (0.2),
  SFX_ScoreboardLoop_Mono hum (0.01). Meshes: ScoreboardProjector_Geo/Legs/BeamBase/BeamTop/Quad (skinned).
- `[GameScoreboardView]`: world canvas 1333.33 x 1000 at scale 0.0083538, anchored (0, 50). Grid: Header 265
  (TitleRow: title cell 1333 pref + timer cell 300; SubtitleRow 65: mode toggle + score cell; TeamHeaderRow 65:
  PLAYER / SCORE), Body 900 pref (team rows (GameScoreboardTeamRow) + player rows (GameScoreboardPlayerRow)),
  PageControls 50, Footer 200 (ModeRow "TEAM BATTLE: FIRST TO 50", ButtonRow START (Futura 75), SWITCH TEAM,
  NEXT MAP: X, VOTE). Cells: yellow rgba(1, 0.953, 0.412) at alpha 0.5 (GameScoreboardView.defaultBackgroundAlpha;
  1 = highlight), SquareBorder sprite outlines #1A1A1A, text #1A1A1A in legacy_Futura-T-OT-Bold-Oblique (titles,
  buttons) / legacy_FuturaT_Bold (player names). Buttons: BorderFill sprite, white.
- Levels (DN/refdata/apk): G scoreboard in Dodgeball (2.79, 0.356, -11.81), Paddleball (-4.17, 0, -0.09), Charades
  (the classic hall: two, at x = +-13.4 facing the middle), GoldenTrophy_Foyer (108.35, -6.51, 42.72) and every quest
  room, Jumbotron (Quest_Scifi_*) rooms, Isle_*. Paintball_Lobby has a LeaderboardProjector variant at
  (-46.41, -5.67, 60.10) and two StartButton trigger zones. The marker's +Z faces the players (checked against the
  pregame spawns of Dodgeball / Paddleball / Charades).

## Requests
(none yet)

## Run 1 (harness 2026-09-25 00:05, 26 s, 0 step failures) - MEASURED, read before placing a console
- Rec Room's projector animates open and carries the panel: the canvas under ScoreboardViewRoot (view root lossy scale
  0.3914 / 0.3918 / 0.3918) is **4.36 x 3.27 m**, upright, reading toward the marker's +Z (so the G marker's +Z faces
  the players - no flip needed), centre **2.52 m up and 0.54 m ahead** of the marker: it spans 0.89-4.15 m above the
  floor. Opened in 1.5 s. Same in every room. Constants: `RrGameConsole.ProjectedCentre`, `RrGameConsole.ProjectedSize`.
- That is big: in our small procedural gyms it cut into walls (Dodgeball south wall, Paddleball tube glass, a Charades
  pillar). In Rec Room's own levels (APK markers) there should be room; to check a spot:
  `RrGameConsole.IsClear(parent, localPose, out string blocker)` (hologram box + projector base vs layer-23 colliders), and
  `RrGameConsole.FindSpot(parent, viewerLocal, candidates, accept, out Pose pose, out string note)` (first clear candidate
  facing the viewer that the viewer's eye can see). Every console also logs a WARN after opening if its hologram meets a
  wall: "Console: <title> at (x, y, z): the hologram meets <collider> (the hologram cuts into the room)".
- START / mode / map presses, the practice path (Dodgeball) and the aim ray ("ray from 2.5 m meets StartGameButton")
  work; fonts legacy_Futura-T-OT-Bold-Oblique_Static (labels) + legacy_FuturaT_Bold (names) from the install.
- Moved after run 1: Dodgeball -> (6.3, 0, -9.2) yaw -60 (arrival yaw 80); Paddleball -> (3.8, 0, -6.5) yaw 0 (arrival
  yaw -135); Charades -> searched with FindSpot (audience side, facing the arrival spots, not across the view of the
  performer).

## Run 2 (00:13, 0 step failures)
- Showcase (4 players, teams after START, modes, maps) renders as Rec Room's yellow scoreboard hologram: team header rows
  in team colours, the local row highlighted, CHANGE MAP / START buttons, mode toggle; all presses reach the callbacks.
- Dodgeball SE corner fits (WARN: its margin touched the south pad "Pad S2" -> moved 0.25 m north to (6.3, 0, -8.95));
  Paddleball south aisle end fits the view (WARN: margin touched "Glass E" -> x 3.8 -> 4.0); Charades FindSpot picked
  (-1.00, 0.64, 0.50) yaw 307 but a hull post hid its left third from the arrival camera -> FindSpot now requires the
  whole hologram (both sides, both heights) visible from the viewer's eye and 2.5 m clear in front of START; Charades
  also keeps the spot within 50 degrees of the arrival facing.
- Charades START with the toggle on TEAM LIGHTNING -> "Charades: player 0 starts Team Lightning" (the authority path).

## Run 3 (00:24, 0 step failures of 32) - final; shots kept in DN/iter8/console_shots/
- No "hologram cuts into the room" warning in Dodgeball (6.3, 0, -8.95) or Paddleball (4.0, 0, -6.5).
- Dodgeball: START alone -> PRACTICE (phase Countdown -> Playing), the hologram then shows 0:01, GET READY, BLUE TEAM 1
  (Player, YOU) / RED TEAM 0, START greyed "STARTING". Paddleball: PRACTICE. Charades: TEAM LIGHTNING -> START ->
  "Charades: player 0 starts Team Lightning".
- Charades: FindSpot still chose (-1.00, 0.64, 0.50) yaw 307 and a hull post still hides part of it from the arrival
  eye: the Ink Space posts have NO colliders. After run 3 (not harness-verified): FindSpot also tests post-like renderer
  bounds (>= 2.5 m tall, 0.15-1.2 m across), the arrival cone is 60 degrees; the pick is logged as
  "Charades: game hologram at ... (candidate i of 315, N posts; ...)". If none passes it falls back to (-0.6, 0.65, 0.2).
- Default status line is empty while CanStart is false (no "1 MORE PLAYER NEEDED" under a countdown).

## Untested
- VR poke / VR trigger ray / the client's XR ray and free cursor on the canvas (the harness has no XR; the aim-ray hit
  test is verified: "ray from 2.5 m meets StartGameButton").
- Networked START from a non-authority client (the console calls RrGameRoom.RequestStart / Charades.Request, both the
  games' existing authority paths).
- The Charades post-aware spot search (after run 3).
