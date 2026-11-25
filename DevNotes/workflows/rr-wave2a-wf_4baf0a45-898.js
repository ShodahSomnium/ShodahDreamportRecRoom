export const meta = {
  name: 'rr-wave2a',
  description: 'Places and first games: Dorm, Rec Center, Paintball, 3D Charades, Dodgeball+Paddleball, Quest framework+Golden Trophy',
  phases: [{ title: 'Build' }],
}
const SP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\\scratchpad'
const SPU = SP.replace(/\\/g, '/')
const COMMON = `
# Dreamport: Rec Room - wave 2 (places and games)
Rec Room played inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars untouched).
Code: D:\\Dreamport\\Rec Room\\Rec Room\\Assets\\#User\\SomniumRecRoom (asmdef SomniumRecRoom). The Unity project is open in the
user's editor: add/edit files only; never run Unity on it.

READ FIRST (economically - grep headings, read only the sections you need; do not print whole big files):
1. ${SP}\\design\\ARCHITECTURE.md (contract; §11 = how to register your room through its RrRegistry partial hook).
2. ${SP}\\design\\wave1_results.md (what the foundation modules provide: APIs and requests - read the parts for Net, Player,
   Interact, Rooms, Games/Shared). Then the public APIs of the foundation code you use: Core/ (RrAssets, RrSpawn +
   RrSpawnOptions.Scenery()/Visual(), RrInstance.Config, RrSound, RrMusic, RrFx, RrLook, RrBuild, RrText), Player/, Interact/
   (RrTools.MakeTool, RrPressable, RrRefs tags, behaviours), Net/ (RrNet presence/instance state/events/JoinInstance, RrNetBody,
   RrNetActors), Rooms/ (RrRoom, RrRoomInfo, RrTravel, RrPortal - use RrPortal.Create for doors), Watch/, Games/Shared/
   (RrGameRoom with Loadout/PlayerMaxHealth, RrTeams, RrAnnouncer, RrScoreboard, RrGameEvents, RrHealth, RrHitVolumes,
   RrWeapon, RrProjectile, RrMelee, RrEnemy, RrLoadout).
3. The research doc(s) for your area (below): the measured Rec Room data - prefab names, container keys, config values,
   clip names - and the rebuild plans.
4. ${SP}\\research\\somnium_conventions.md §11-13 and §24 (client-owned keys, layers, rendering, gotchas) - skim.

"Done" for your place/game:
- Registered via its RrRegistry partial hook (title, subtitle, description, category, min/max players, cover/icon sprite
  names from ${SP}\\research\\rr_watch_ui.md's per-activity artwork table, music).
- Built at its origin by Build() (coroutine, yield between chunks, set BuildProgress) from Rec Room's own prefabs where the
  bundles have them (RrSpawnOptions.Scenery() for furniture/architecture; RrTools.MakeTool for props people pick up) plus
  RrBuild/RrLook geometry following the research rebuild plan and Rec Room's real layout; lit to look like Rec Room.
- Plays like Rec Room: rules, timings and numbers from the research doc, Rec Room's sounds, music, announcer, effects; match
  flow via RrGameRoom (social rooms: RrRoom). An exit door back to the Rec Center (RrPortal) in every game.
- Desktop screen mode and VR (RrHand/RrInput); offline in the editor (local player = authority) and networked (instance
  authority = "fake master": state in RrNet.Instance custom ints, events type >= 64 via RrNet.Send/On with repeat-until-seen
  for requests, props with RrNetBody, AI with RrNetActors). Only draw things for other players in the same instance.
- Verified in the play-mode harness: scenarios ${SP}\\harness\\scenarios\\<activity>_*.json that boot, {"goto": "<RrActivity
  name>"}, and shoot several viewpoints; add static test hooks ({"call": "Namespace.Type.Method"}) to start a match / fire /
  score. Run: bash ${SPU}/harness/run.sh <scenario.json> (one Unity at a time - it queues on a lock; exit 3 = some module does
  not compile, wait and retry). LOOK at the PNGs (Read tool) and fix what does not look like Rec Room. At most 4 harness runs.
- Compiles: bash ${SPU}/compilecheck/build.sh project --editor -> 0 errors, guard proven. Errors in other agents' folders are
  theirs (they are working now): wait ~2 min and re-run; never edit them.
- New files get .meta: bash ${SPU}/tools/metas.sh
- Ownership: edit ONLY your folder (below). A clear blocking bug in a foundation folder may be fixed minimally (report it).
- TOKEN ECONOMY: a usage limit has already stopped runs twice. Write your code files early and keep them compiling, so an
  interruption loses little; keep command output small; don't re-read files; concise result.
Return: files, the player's experience step by step, verification (scenarios, PNGs and what they show), what is not done,
requests to other modules, and a README section in the house style (dated heading "(2026-09-24)", what was built, measured
Rec Room values used, log lines to look for, untested items).
`
const SCHEMA = { type: 'object', properties: {
  files: { type: 'array', items: { type: 'string' } }, experience: { type: 'string' }, verified: { type: 'string' },
  notDone: { type: 'array', items: { type: 'string' } }, requests: { type: 'array', items: { type: 'string' } },
  readme: { type: 'string' },
}, required: ['files', 'experience', 'verified', 'notDone', 'requests', 'readme'] }

const TASKS = [
 { key: 'dorm', prompt: `
## Your place: the Dorm Room (folder Places/Dorm/, namespace ...Places.Dorm; hook RegisterDorm; RrActivity.Dorm, Networked = false, Category "Social")
Research: ${SP}\\research\\rr_dorm_core.md - §3 (the 47 (Dorm_*) Maker Pen pieces in bundle 43ea1176... reassemble the real
dorm: root = inverse of the child's offset; pose table in §3.2; furniture layout §3.3; colours; hoop position from the rim VFX
at (3.905, 1.097, -1.090)), §4 (tools), §5 (extra props).
Build the player's dorm exactly: architecture from the kit (with sensible Rec Room dorm colours via MaterialPropertyBlock/
material copies on the maker-pen material), loft + ladder, windows/skylight (a sky/outdoor view outside: campus lawn and sky),
bunk bed, dresser, desk + lamp, mirror (a real reflective mirror is optional; Rec Room's mirror opens avatar customisation - here
show the Somnium player a Rec Room-style mirror frame with a simple reflection if cheap: a camera-rendered texture at low res only
while near, else a stylised mirror), community/bulletin board and campus map, basketball + hoop (RrBall + RrHoop scoring with the
Rec Room swish/rim sounds), drink bottles and a few food items on the desk/dresser (RrTools.MakeTool, respawn when dropped far),
other fun props from §5, lights, the door to the Rec Center (RrPortal to RrActivity.RecCenter, placed where the real door is).
Spawn: where Rec Room puts you (facing the room). Ambient: the dorm's quiet sound/music if found.` },
 { key: 'reccenter', prompt: `
## Your place: the Rec Center (folder Places/RecCenter/, namespace ...Places.RecCenter; hook RegisterRecCenter; RrActivity.RecCenter, Category "Social", MaxPlayers 40)
Research: ${SP}\\research\\rr_reccenter.md - §3 room plan (40 x 36 x 8 m hall + 12 x 8 m lobby; exact door coordinates for all
20 activity doors + the Dorm door, grouped PvP/Sports north, Quests east, Party/Racing west; furniture zones), §2 props (couches,
cafe, reception bell, pool table + cues, ping-pong, poker table, basketball hoop, vending machine, Friend-O-Tron, lockers, jukebox,
radios with 41 tracks), the official [ActivityDoor] (container key 84 hull) - RrPortal already clones it; §4 transitions.
Build the hub: architecture with Rec Room's own wall/floor/ceiling materials where the research found them (loading key 84 brings
wood floor, paints, carpet, fluorescent panels, posters), otherwise RrLook; every activity door via RrPortal.Create (only for
activities registered in RrRooms at build time, but reserve all spots), signs, the lobby with the Dorm door and the arrival
spawn, furniture and working props (pool balls/cues, ping-pong paddles + ball, basketball + hoop, cafe mugs/coffee, poker chips),
a playable jukebox/radio (RrPressable cycles Rec Room's tracks), ambient crowd/music. Networked: props people play with carry
RrNetBody (unique LocalPropId).` },
 { key: 'paintball', prompt: `
## Your game: Paintball (folder Games/Paintball/, namespace ...Games.Paintball; hook RegisterPaintball; RrActivity.Paintball, Category "PvP")
Research: ${SP}\\research\\rr_shooters.md - paintball sections (weapons and exact stats, one-hit health 1, 5 s respawn + 5 s
protection, 4v4, Capture the Flag 3 captures in 600 s, Team Battle 50 hits, flags prefab/stand/return logic, VO lines per team,
lobby radios, map music, paint splats/decals, team colours, scoreboard), plus the map rebuild proposals.
Build: a lobby (team-select area with Rec Room's radios and weapon racks) and 2 maps rebuilt from the proposals (e.g. River and
Homestead) with cover, spawn rooms per team, and flags; modes Team Battle and Capture the Flag (vote or alternate); loadout from
Rec Room's paintball guns (pistol/rifle/shotgun per the research, via RrGameRoom.Loadout/RrWeapon), grenades if cheap; one-hit out
with the Rec Room hit sound/VFX, respawn in the team spawn; announcer VO; scoreboard; flag carry/capture/return with VO.` },
 { key: 'charades', prompt: `
## Your game: 3D Charades (folder Games/Charades/, namespace ...Games.Charades; hook RegisterCharades; RrActivity.Charades, Category "Party")
Research: ${SP}\\research\\rr_party.md - 3D Charades (the whole Ink Space venue is one prefab, key 84: 20.8 x 7.1 x 23 m with 40
collision boxes, lights, posters; stage key 23f; the Charades Pen key cb9; card and push-button meshes; 2 music tracks; game
sounds; mode timings: co-op 480 s with 60 s per drawer; Team Lightning 90 s rounds, 4 rounds, 45 s per drawer).
Build: spawn the venue and stage as Scenery; the game: the drawer gets a secret word card (a list of Rec Room-style charades
words you write), draws in 3D with the pen (trigger/use draws a ribbon along the pen tip: LineRenderer/mesh ribbon in Rec Room's
ink colours, networked as strokes via events so everyone sees them; erase on round end), guessers press their buzzer/button and
the drawer confirms "correct" (or type is impossible in VR - so use Rec Room's flow: drawer presses correct/skip buttons on the
stage), scores, timers, music and sounds; co-op mode at minimum, Team Lightning if time allows.` },
 { key: 'dodgepaddle', prompt: `
## Your games: Dodgeball and Paddleball (folders Games/Dodgeball/ and Games/Paddleball/; hooks RegisterDodgeball, RegisterPaddleball; Category "Sports")
Research: ${SP}\\research\\rr_sports.md - dodgeball (ball key 1bf, settings, first to 3 rounds, rules, screen-mode throw 14 m/s,
arena plan, catch = thrower out, sounds) and paddleball (paddle key 2d, disc key 9d, first to 5 goals, table/court plan, serve).
Dodgeball: 2 teams on a court split by a centre line (crossing it = out), balls on the centre line at round start, hit by a live
thrown ball = out (to the bench), catch = thrower out and a teammate returns, last team standing wins the round, first to 3 rounds;
RrNetBody balls with throw ids and the catch/hit claim arbitration from the networking doc; Rec Room sounds/announcer.
Paddleball: 1v1 (or 2v2) table/court game with Rec Room's paddle and disc/ball physics (bounciness etc. from the research), goals,
serve, first to 5; paddle held solid (RrGrabbable.SolidWhileHeld) with touch-steal authority on the ball.` },
 { key: 'quests', prompt: `
## Your games: the quest framework + Quest for the Golden Trophy (folders Games/Quests/Shared/ and Games/Quests/GoldenTrophy/;
hook RegisterGoldenTrophy; RrActivity.GoldenTrophy, Category "Quests", 1-4 players)
Research: ${SP}\\research\\rr_quests.md - §0 load recipe, §2 (rules object via rule-circuit key fe6 for Golden Trophy: score
currency, death penalty 2000, win/lose music; 26 enemy spawner prefabs with health/speed/dash/projectile numbers; the goblin rig
with idle/walk; ~40 weapons with configs; loot and breakables; revive 2.5 m range / 1 m stay; music; VFX; building kit: stone
walls, pillars, cardboard trees, rocks, sci-fi door), §3 framework proposal, §4 level plans (Golden Trophy acts).
Build a reusable quest framework (Games/Quests/Shared): acts/rooms that open in sequence, enemy waves via RrEnemy (authority
director; actor blocks via RrNetActors), loot/coins and score, weapon choice at the start (Rec Room's quest weapon racks), player
death -> revive by a teammate within range (else respawn at the checkpoint with the death penalty), quest-complete/failed flow
with Rec Room's music and stingers. Then Golden Trophy on it: the goblin quest acts from §4, built from the fantasy kit and
procedural geometry, with its weapons (sword, shield, bow/crossbow, wand per research), enemies and final area with the trophy.
Other quests (Crimson Cauldron, Lost Skulls, Jumbotron, Crescendo) come later on your framework: keep its API clean and
document it in your result.` },
]
phase('Build')
const results = await Promise.all(TASKS.map(t => agent(COMMON + t.prompt, { label: t.key, phase: 'Build', schema: SCHEMA })))
return results.map((r, i) => ({ key: TASKS[i].key, result: r }))
