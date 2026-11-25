export const meta = {
  name: 'rr-iteration-8',
  description: 'Rec Room Dreamport iteration 8: Rec Room lighting/sky/shadows per room, the game-start hologram, Golden Trophy from the Android rooms (with the Armory), Paintball lobby polish, The Rise of Jumbotron',
  phases: [
    { title: 'Build', detail: 'env, console, goldentrophy, paintball, jumbotron in parallel' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'

const COMMON = `
# Dreamport: Rec Room - iteration 8 (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars untouched).
The world ships NO Rec Room files: at runtime it builds everything from the player's own PC install through RrAssets and
the scene kit (CODE/Core/RrSceneKit*.cs; RRSK format in DN/tools/rrscene/README.md incl. the H terrain records). The
Rec Center loads Rec Room's own building from the player's cache (Places/RecCenter/RrRecCenterCache.cs).

The user tested iteration 7 ("Ok much better!") and asked:
1. "The lighting still looks weird, there is no shadows or anything, can you take a look at things like that."
2. "There's also no skybox etc, please use what is there exactly like Rec Room."
3. "The pre-match of the adventure game is too bright and there is also missing ANOTHER pre-start area where you pick up
   your weapons etc, please check" (Golden Trophy: its lobby and the Armory).
4. "Please check the files, there was normally a hologram that shows who is in the room and a start button there for
   all the games."
5. "Also the paintball needs looking at, the lobby still feels strange."
6. "There is also some mesh overlapping."
7. "Let's get the rise of jumbotron in and see how that feels!"

## Reference data (all REFERENCE ONLY - never copy/import any of it into the project, never ship it, never modify it)
- The Android build (a 2026-03-23 XAPK; data only, never install or execute): extracted at ${NSP}/apk (UnityDataAssetPack.apk,
  and base/ with globalgamemanagers). Reader: DN/tools/apkscene (README; run.sh; it uses PC-bundle type trees; same Unity
  6000.0.27f1). Converted rooms: DN/refdata/apk/*.rrsk with INDEX.md (all 114 levels by scene path). NOTE: INDEX.md labels
  Quest_Scifi_* (levels 38-49) as "Crescendo of Champions" - they are THE RISE OF JUMBOTRON (the sci-fi quest); Crescendo
  of the Blood Moon is Quest_Dracula1_*. Golden Trophy = levels 19-29 (Quest_Foyer, Quest_Armory, Quest_Hallway1..5,
  Quest_DeadIsland, Quest_Library, Quest_Battlefield, Quest_Cafeteria).
- REF2 Studio scenes (DN/refdata/*.rrsk; GTact1-6, GoldenLobby...), the user's cache (DN/research/rr_reccenter_cache.md;
  DN/tools/rrtool_cache), research docs DN/research/*.md (rr_quests.md, rr_latest_rgames_story.md, rr_latest_rgames_action.md,
  rr_paintball_footage.md), the install index SP/rrtool/idx/objects.tsv.
- Install facts found by the orchestrator: the (ScoreboardProjector) prefab is in the install (Animator + UIProjector +
  beam/quad meshes: Rec Room's holographic projector), and the levels mark it as G "scoreboard" records
  (GameScoreboardProjectors, [ScoreboardProjector]) next to "StartButton" trigger zones. Rec Room's sky shader "AG/Skybox/
  Simple Gradient" and the material "ComfortModeSkybox" are in the install.

## Rules
- The Unity project is open in the user's editor: edit files only, never run Unity on it (the harness uses a copy).
- ARCHITECTURE.md section 1 (DN/design/ARCHITECTURE.md). Never modify the Somnium avatar; never touch the client's camera.
  Every room is at the world origin; only the current room exists; install-only at runtime (user decision).
- Edit ONLY the files your task owns; request anything else in your notes file (DN/iter8/<key>.md, section "Requests") -
  other agents read each other's notes. Compile: "bash ${SPB}/compilecheck/build.sh project --editor" -> 0 errors in all
  three builds and "all N Fusion/Somnium members are proven" (an UNPROVEN member may break the upload: find a proven
  route). Errors in files you don't own = another agent mid-edit: wait 2 minutes, retry. New files: "bash ${SPB}/tools/metas.sh".
- Harness: "bash ${SPB}/harness/run.sh <scenario>" (Bash timeout 600000; serialised by a lock - five agents share it, so
  keep to your run budget). Shots take "frame": "room" and "post": true. LOOK at the PNGs.
- Unity's compiler rejects a lambda local that shadows an outer local (CS0136). House style: XML doc comments explaining
  why with measured facts; RrLog.Info once per key fact.
- Durability: DN/iter8/<key>.md created in your first minutes, updated after each milestone; if it exists, continue.
`

const RESULT = {
  type: 'object',
  properties: {
    files_changed: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
    verification: { type: 'string' },
    pngs: { type: 'array', items: { type: 'string' } },
    untested: { type: 'array', items: { type: 'string' } },
    readme_section: { type: 'string' },
  },
  required: ['files_changed', 'summary', 'verification', 'pngs', 'untested', 'readme_section'],
}

const ENV = `
# Your task: "env" - Rec Room's lighting, shadows and sky in every room, and the mesh overlaps
Key: env. You own: new CODE/Core/RrEnvironment*.cs, CODE/Core/RrSceneKit*.cs + RrSceneData.cs (the ONLY agent that edits
the kit - others request kit changes in their notes), CODE/Core/RrPostFx*.cs (only if the new lighting needs the base
exposure adjusted), CODE/Rooms/RrTravel*.cs (apply/clear per room), CODE/Places/RecCenter/RrRecCenterCache.cs (apply the
cached building's own environment), DN/tools/apkscene (to emit environment records), scenarios env_*.json.
1. Find how the Somnium client lights a world (CLIENT source E:/Somnium Space/Main Build/Assets, read only): which
   directional light is the sun (RenderSettings.sun?), whether the world scene's own sun and RenderSettings apply after
   the client changes the active scene, shadow settings (URP asset shadow distance/cascades on the client), and the
   proven Bridge environment API (ETS used SomniumBridge.Environment SetSunSource / SetAmbientModeGradient - see
   D:/Dreamport/Euro Truck Simulator/Euro Truck Sim; use only members the guard proves).
2. Extract each room's environment exactly as Rec Room had it: RenderSettings (skybox material and ITS properties - the
   sky gradient colours for AG/Skybox/Simple Gradient or whatever shader it uses, ambient mode + trilight colours,
   ambient intensity, fog settings, reflection intensity), the directional light (rotation, colour, intensity, shadow
   type and strength), and baked lighting facts (lightmaps exist? light probes?). Sources: the APK levels (extend
   apkscene to write "E sky/ambient/sun" records), the REF2 scenes, the cache scene (1AF), the Dorm kit/built-in Dorm
   (APK Dorm_Builtin). Keyed by activity (and area for quests/maps).
3. Runtime CODE/Core/RrEnvironment.cs: per room, applied on enter and restored exactly on leave: the sky (a runtime material
   on the install's AG/Skybox/Simple Gradient shader with Rec Room's own values, or the level's own sky material when
   the install has it), ambient (trilight, Rec Room's colours), and a sun (our own world-owned directional light, or the
   proven Bridge route, matching Rec Room's direction/colour/intensity) WITH realtime shadows (soft; a VR-sensible shadow
   distance). Rooms have no baked lightmaps here, so compensate the way Rec Room's look needs: shadows from the sun,
   a few key lights with shadows where cheap, ambient occlusion-friendly ambient, and a one-time realtime reflection
   probe render per room if it helps. Fog: the world scene was saved with fog off (DN/tools/harness/README.md "Fog
   choice") - report whether it can be enabled from Rec Room's values without shader variants going missing.
4. Mesh overlapping: in the kit, drop exact duplicates (same mesh + pose), drop stand-in X boxes that sit inside an
   install-matched renderer or prefab (e.g. Paintball's tyre stacks drawn twice), and fix z-fighting between coplanar
   stand-ins and install meshes (tiny offsets). Log counts.
5. Verify: compile; env_tour.json with before/after shots (post on) of the Dorm, the Rec Center (cached building), the
   Paintball lobby and River, the Golden Trophy lobby: sky visible, sun shadows on the floor/walls, no overlaps. At most
   5 harness runs. LOOK.
Return per the schema.
`

const CONSOLE = `
# Your task: "console" - Rec Room's game-start hologram (who is in the room) and START, for all games
Key: console. You own: new CODE/Games/Shared/RrGameConsole*.cs, and its integration in CODE/Games/Dodgeball/*,
CODE/Games/Paddleball/*, CODE/Games/Charades/* (small edits), scenarios console_*.json. The Paintball, Golden Trophy and
Jumbotron agents integrate your console in their own folders through your API - publish the API in DN/iter8/console.md
within your first 20 minutes and keep it stable.
1. Find the real thing: the (ScoreboardProjector) prefab (install; its UIProjector and Animator configs via rrtool - what
   UI prefab it projects, the beam/quad, the open animation), and how the levels use it (the G "scoreboard" markers:
   GameScoreboardProjectors / [ScoreboardProjector] in DN/refdata/apk/*.rrsk - Paintball_Lobby, Dodgeball, Paddleball,
   GoldenTrophy_Foyer, Isle_Foyer, Charades...), plus the "StartButton" trigger zones and any start/ready/team UI prefabs
   in the install (search objects.tsv; e.g. RRUIButton StartButton inside game UI prefabs). Rec Room's game lobbies showed
   a hologram listing the players in the room (and teams / ready state) with a START button.
2. Build RrGameConsole: spawns Rec Room's (ScoreboardProjector) at a marker pose (install prefab, animated open), and
   projects our own Rec Room-styled panel (reuse Watch/RrUiKit look) with: the game title, the players in this instance
   (RrNet presence + RrIsolation.IsWithMe; team colour; local player marked), min/max players, the mode/map where the
   game has them, and START (press: VR poke/trigger ray, desktop aim+click - as RrRoomListPanel does). START calls the
   game's start callback; it shows "N more players needed" below the minimum. Networked through the game's existing
   authority path (RrGameRoom) - the console only asks.
   API sketch (adjust, then publish): RrGameConsole.Create(Transform parent, Pose pose, RrGameConsoleSpec spec) with spec =
   title, min/max players, Func<IReadOnlyList<int>> members, Func<int,int> teamOf, Action<int> onStart(playerId),
   optional mode/map choices; Close(), Refresh().
3. Integrate in Dodgeball, Paddleball and Charades at the positions their APK levels use (DN/refdata/apk/Dodgeball.rrsk,
   Paddleball.rrsk, Charades_InkSpace.rrsk G scoreboard / start markers, mapped into our rooms' frames), replacing their
   ad-hoc START buttons.
4. Verify: compile; console_test.json (a console on the Arrival floor + one in the Dodgeball gym; shots of the hologram;
   press START). At most 3 harness runs.
Return per the schema.
`

const GOLDEN = `
# Your task: "goldentrophy" - Golden Trophy from the Android build's rooms, with its Armory, and not too bright
Key: goldentrophy. You own: CODE/Games/Quests/GoldenTrophy/* (incl. Scenes/ data), CODE/Games/Quests/Shared/* (ADDITIVE
changes only - the "jumbotron" agent builds on the same framework at the same time; list new members in your notes),
scenarios goldentrophy_*.json.
The user: "The pre-match of the adventure game is too bright and there is also missing ANOTHER pre-start area where you
pick up your weapons etc, please check." Today Golden Trophy's lobby and acts 1-6 come from REF2 Studio scenes whose walls
were missing (README "Iteration 4"), acts 7-9 are research-built, and there is no Armory.
1. Compare the Android rooms (DN/refdata/apk/GoldenTrophy_*.rrsk: Foyer, Armory, Hallway1..5, DeadIsland, Library,
   Battlefield, Cafeteria; INDEX.md says "the older room-per-scene layout") with the REF2 GoldenLobby/GTact1-6 and the
   research (rr_latest_rgames_story.md Golden Trophy: the final flow - lobby/theatre with merch booth, then a dressing
   room with 4 swords, 2 shields, 2 bows, act 6 = library, etc.). Decide per room which source is the FINAL version and
   which is geometrically more complete (APK has the real colliders and walls). Write the decision table in your notes.
2. Rebuild the run as Rec Room had it: Foyer (the pre-match lobby) -> Armory (the pre-start room where you take your
   weapons from racks; the team gathers there and the run starts) -> the acts in order -> the boss -> victory. Use the
   Android rooms where they are final/complete; keep REF2/research pieces only where the Android build lacks them.
   Gameplay markers (enemy spawns, checkpoints, doors, pots, chests, boulder, lava, weapon pickups) from the levels.
3. Lighting: "too bright" - use each room's own lights and environment (the "env" agent adds RrEnvironment per room/area;
   call its area API when the act changes - read DN/iter8/env.md; until it exists, keep lights to the level's own values
   and don't add extra bright ambient). Remove any extra fill lights the iteration-4 build added.
4. Integrate the "console" agent's RrGameConsole (DN/iter8/console.md) where the Foyer/Armory has its ScoreboardProjector /
   start markers, as the party list + START.
5. Verify: compile; goldentrophy_run.json / _acts.json updated (shots of the Foyer, the Armory with its racks, each act,
   the boss); at most 5 harness runs. LOOK: not too bright, walls present, weapons in the Armory.
Return per the schema.
`

const PAINTBALL = `
# Your task: "paintball" - the Paintball lobby feels strange: make it feel like Rec Room's
Key: paintball. You own CODE/Games/Paintball/* (incl. Scenes/), DN/tools/pbscene, scenarios paintball_*.json. Kit changes
go to the "env" agent (request in DN/iter8/paintball.md "Requests"); lighting/sky come from env.
Read DN/apk/paintball.md and the README "Iteration 7" section first; the footage notes DN/research/rr_paintball_footage.md
describe the real 2025 lobby.
1. What makes it strange today (look at SP/.../pb/final and fresh shots): our low-poly sphere trees (use Rec Room's own
   install trees/bushes where the level's trees have install equivalents, e.g. (Sandbox_FlatTree*), pines, bushes - by
   kind and size), blank orange sign boards (the level's sign TEXTS are in DN/refdata/apk/Paintball_Lobby.md - draw them
   as Rec Room signs: PRACTICE ARENA, the lobby's other signs), the lobby building as grey/cream boxes (use install
   materials by kind: wood siding, brick, corrugated metal, roof), double-drawn tyres, missing interiors (the locker/shop
   room: lockers, CHOOSE YOUR SKINS, the lounge with purple couches and the red rug, the skins wall, the Try 'em out!
   wall), and scale/placement oddities.
2. Put Rec Room's game-start hologram (the "console" agent's RrGameConsole, API in DN/iter8/console.md) at the lobby's
   GameScoreboardProjectors marker(s) with the players in the lobby, teams, map/mode and START (replacing our board's START
   if it duplicates it; keep map/mode/team choice working).
3. The same tree/sign/material improvements on the 7 maps where cheap (trees especially).
4. Verify: compile; paintball_all.json (lobby shots from player height matching the footage notes; one shot per map);
   at most 4 harness runs. LOOK.
Return per the schema.
`

const JUMBO = `
# Your task: "jumbotron" - The Rise of Jumbotron (sci-fi quest) from the Android build's rooms
Key: jumbotron. You own new CODE/Games/Quests/Jumbotron/* (incl. Scenes/ data and its RrRegistry partial hook
RegisterJumbotron), DN/refdata/apk/Jumbotron_*.rrsk (convert them), scenarios jumbotron_*.json. The quest framework in
CODE/Games/Quests/Shared is owned by the "goldentrophy" agent this iteration (additive changes only, its notes list them);
if you need a framework change, subclass or add it in your folder, or request it in DN/iter8/jumbotron.md.
1. Convert the Android levels 38-49 (Quest_Scifi_Foyer, _Armory, _Battlefield1, _Hallway1, _Hallway2, _Reception,
   _StadiumConcession, _StadiumEntry, _Garage, _Cargo1, _Stadium, _Additive) with DN/tools/apkscene (run.sh; the same
   pipeline that made DN/refdata/apk/GoldenTrophy_*.rrsk), into DN/refdata/apk/Jumbotron_<Room>.rrsk, then generated
   compressed data files (DN/tools/rrscene/rrsk2cs.sh) under Games/Quests/Jumbotron/Scenes/.
2. Build the quest on the framework (see Games/Quests/GoldenTrophy for a full example: RrGoldenTrophyRoom, its areas, the
   kit usage, markers -> acts/waves/pots/doors): the Foyer lobby, the Armory (pre-start weapons), then the acts in the final
   order, the Stadium finale with Jumbotron the boss. Facts: DN/research/rr_quests.md (Jumbotron sections) and
   rr_latest_rgames_story.md (Jumbotron: the Flight Deck button and cutscene, muffled sound in Space, lasers that kill on
   contact, 1,200 credits lost per hit, magazines 5/22/3/3, rank scale 150k/125k/100k; the framework already has the
   final rules). Currency "credits". Enemies: the sci-fi robots/drones the install has (search objects.tsv for
   QuestAIController scifi/robot/drone prefabs and the weapons: laser pistol/rifle etc.). Music and VO from the install.
   Register it (1-4 players; category Quests; cover art per rr_watch_ui.md) so the QUESTS door lists it as built.
3. Use the "console" agent's RrGameConsole in the Foyer (party list + START) - API in DN/iter8/console.md.
4. Verify: compile; jumbotron_run.json (build, shots of the Foyer, Armory, each act, the boss arena; start a run, one act
   clear); at most 5 harness runs. LOOK. It's fine if some acts are rough - the user wants to "see how that feels".
Return per the schema.
`

phase('Build')
const tasks = [
  ['env', ENV], ['console', CONSOLE], ['goldentrophy', GOLDEN], ['paintball', PAINTBALL], ['jumbotron', JUMBO],
]
const results = await Promise.all(tasks.map(([k, p]) => agent(COMMON + p, { label: k, phase: 'Build', schema: RESULT })))
const out = {}
tasks.forEach(([k], i) => { out[k] = results[i] })
return out
