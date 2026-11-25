export const meta = {
  name: 'games-wave1',
  description: 'Wave 1: Jumbotron floaters, networked maker pen, 3 quests, Laser Tag, Soccer + Bowling in basic form; one integration run',
  phases: [
    { title: 'Build', detail: '7 builders in their own folders, compile-only' },
    { title: 'Integrate', detail: 'scene data list, compile, ONE harness tour of the new games, fixes' },
  ],
}

const CODE = 'D:\\Dreamport\\Rec Room\\Rec Room\\Assets\\#User\\SomniumRecRoom'
const DN = 'D:\\Dreamport\\Rec Room\\Rec Room\\DevNotes'
const SP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\\scratchpad'
const NSP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\1c0947a6-bfa3-41e9-ae24-3394d8a31490\\scratchpad'

const CONTEXT = `
PROJECT: "Dreamport: Rec Room" - Rec Room played inside a Somnium Space world (Unity 6000.3 URP, C# 9, Photon Fusion shared
mode via our Net/* layer). Everything loads at runtime from each player's own PC Rec Room install (plain Addressables bundles:
props, tools, weapons, enemies, sounds, music, materials; RrAssets / RrSpawn). Nothing from Rec Room ships with the world.
PATHS: CODE = ${CODE} (README.md there = the project doc; ARCHITECTURE notes in DN); DN = ${DN} (notes, tools, research,
refdata); SP = ${SP} (compile check: bash SP/compilecheck/build.sh project --editor ; harness SP/harness/run.sh + scenarios);
NSP = ${NSP} (scratch; NSP/apk = the user's Android XAPK extracted, DATA ONLY).

HOW GAMES ARE BUILT HERE (read the existing ones before writing anything):
- Activity enum Rooms/RrActivity.cs already has every game; Rooms/RrRegistry.cs calls a static partial hook per game
  (RegisterCrimsonCauldron, RegisterLostSkulls, RegisterCrescendo, RegisterLaserTag, RegisterSoccer, RegisterBowling, ...).
  Implement YOUR hook in YOUR folder like Games/Quests/Jumbotron/RrJumbotronRegistry.cs (title, category, players, cover,
  music, Networked, Create). Rec Room's final rules; player minimums low enough for 1-2 testers.
- Game rooms: RrGameRoom (Games/Shared), lobby hologram console + START (RrGameConsole.ForGame), teams/scores/timers, the
  quest framework (Games/Quests/Shared: RrQuestRoom, acts, waves, RrEnemy, RrQuestFinalRules, racks), weapons (RrWeapon,
  RrWeaponSpec from the install's RangedWeapon data), Paintball (Games/Paintball: teams, respawn, maps from APK levels),
  Dodgeball / Paddleball (sports rooms), Charades (networked pen strokes: RrCharadesPen / RrCharadesInk), the watch.
- Levels: the Android build's plain level files are the layout reference (DN/refdata/apk/INDEX.md maps levelN -> room;
  apkscene RRSK already exist for Isle_*, LaserTag_*, Soccer, Bowling, DiscGolf*, StuntRunner_*, RecRally_Chaparral;
  Crimson Cauldron = levels 50-61 (Quest_Goblin2_*), Crescendo = levels 79-90 (Quest_Dracula1_*), not converted yet).
  Tools: DN/tools/apkscene (convert), DN/tools/gtscene (hull rectangles; USE ITS jt=1 MODE for new levels - no dropped
  pieces, props re-measured, not bounding boxes), DN/iter8/jumbotron_convert.sh + jumbotron_markers.pl (encounter markers:
  enemy spawns, zones, doors), the quests' look = RrJumbotronLook/RrGoldenTrophyHull ideas (level material -> closest
  install AG material + tint; atlas textures -> plain measured colours), RrLightGrid lights them automatically via the kit.
- NEW LEVEL DATA MUST NOT GO INTO C# STRINGS (the assembly's string heap is nearly full): write each RRSK as a gzip data file
  with bash DN/tools/rrscene/rrsk2bytes.sh <in.rrsk> [--name Name] -> CODE/Data/<Name>.bytes (+.meta), and load it at
  runtime with RrSceneBank.Text("<Name>") (Core/RrSceneBank.cs) wherever the existing games use RrScene_X.Text(). Do NOT
  run bankscene.sh (the integrator adds every Data/*.bytes to the scene once). Keep each game's data files <= ~3 MB total.

HARD RULES
- Never decrypt Rec Room's encrypted PC files. The APK is DATA ONLY: never install/execute it; only measured numbers and
  measured shapes (gtscene rectangles/boxes) go into the project, never its textures or raw meshes.
- Never modify the Somnium avatar or the client camera. No DontDestroyOnLoad; statics cleared on shutdown; everything a room
  creates is freed with it. Every script lives in the world assembly under namespace SomniumSpace.Worlds.SomniumRecRoom.*.
- Compile must stay at 0 errors (bash SP/compilecheck/build.sh project --editor). C# 9. Match the code style (XML doc
  comments, RrLog.Info(area, ...), small readable classes).
- The user wants RESULTS, not testing: builders DO NOT run the play-mode harness (offline renders/checks are fine). The
  integrator runs one tour.
- FILE OWNERSHIP: edit only the files/folders your job lists. Do NOT edit Core/*, Rooms/*, Net/*, Games/Shared/*,
  Games/Quests/Shared/*, Watch/* (unless your job says so), Places/RecCenter/* (another workflow is editing it now), or other
  games' folders. If you truly need a change in a shared file, write it as a request in your notes (exact code) and work
  around it; the integrator applies requests. If a compile error is in someone else's file, wait 2-3 min and retry.
- "BASIC FORM" (what the user asked for): reachable from the watch / Rec Center like the other games, the real layout,
  the core loop playable end to end (start from the hologram console, play, score/win/lose, back to the lobby), networked
  through the existing frameworks, the game's install music/sounds. Polish comes later - skip cosmetics.
- Notes FIRST: create ${DN}\\iter13\\<your-name>.md (plan, then a log line after each step) so a usage-limit crash loses
  nothing; if it exists, RESUME from it. Keep command output small; never cat Scenes/*.cs or big rrsk files.
`

const RESULT = {
  type: 'object',
  properties: {
    notes_file: { type: 'string' },
    summary: { type: 'string' },
    files_changed: { type: 'array', items: { type: 'string' } },
    compile_ok: { type: 'boolean' },
    data_files: { type: 'array', items: { type: 'string' }, description: 'CODE/Data/*.bytes written' },
    shared_requests: { type: 'array', items: { type: 'string' }, description: 'exact changes needed in shared files' },
    harness_steps: { type: 'array', items: { type: 'string' }, description: 'scenario steps (JSON lines) the integrator should run to reach/start/check your work' },
    open_issues: { type: 'array', items: { type: 'string' } },
  },
  required: ['notes_file', 'summary', 'files_changed', 'compile_ok'],
}

const JOBS = [
  { key: 'jumbotron', prompt: `YOUR JOB (name: jumbotron). The user, after testing iteration 11/12 in the headset: "There are still some
random things floating in Rise of Jumbotron, please look." You own Games/Quests/Jumbotron/* (not RrJumbotronHull's mirror /
marquee code paths other than to fix floaters), DN/tools/gtscene (jt=1 mode only; Golden Trophy output must stay byte-identical),
DN/refdata/apk/Jumbotron_*.rrsk and Games/Quests/Jumbotron/Scenes/*.cs (regenerate with the existing pipeline; stay within
the Jumbotron's current size, 3.26 MB). Find EVERY floating object: pieces with nothing under, beside or above them within a
few cm (support test on the built plates/boxes/kit renderers vs the room's colliders and hull), e.g. props of inactive or
animated/cinematic objects, glow sticks without strings, lamp fixtures offset from the ceiling, LOD/duplicate copies,
Space-room debris, haze or light-shaft pieces, voxel shells of hanging props. Use offline renders (gtscene render) and the
iteration 11 notes (DN/iter11/*.md, jt11_result.md). For each: fix the data or the runtime (leave out things that are
hidden/animated in Rec Room at load, attach hanging things to their real support, or drop them). List what you found and
what you did in the notes.` },
  { key: 'makerpen', prompt: `YOUR JOB (name: makerpen). The user: "can you add the maker pen? Network it too." You own a new folder
Interact/MakerPen/* and may ADD to Watch/* only what is needed to give the player a maker pen (a watch button/entry, in the
style of the existing watch pages). Build Rec Room's Maker Pen in basic form: the install's own maker pen tool model (search
the install index SP/rrtool/idx for MakerPen / Maker_Pen prefabs; the Charades pen RrCharadesPen shows how a Rec Room tool is
made holdable with RrTools.MakeTool + RrNetBody), drawing 3D ink lines while the trigger is held (tubes like RrCharadesInk,
Rec Room's maker-pen shading via RrLook.Solid which uses the install maker-pen material), a colour palette (Rec Room's
maker-pen colours), an eraser/undo (delete the last stroke, and delete a stroke by touching it), in VR and desktop (screen
mode: draw where the crosshair points at arm's length). NETWORKED: every stroke and delete is seen by everyone in the same
place (use the existing Net layer the way Charades syncs its strokes; strokes are owned by their author; late joiners get
the current strokes; strokes are cleared when the place is left/reset; a cap on points/strokes per player). Isolation: only
players in the same place see them. Available in the Dorm and the Rec Center at least (and wherever the watch is).` },
  { key: 'crimson', prompt: `YOUR JOB (name: crimson). Build "Curse of the Crimson Cauldron" (quest, RrActivity.CrimsonCauldron,
RegisterCrimsonCauldron) in basic form in a new folder Games/Quests/CrimsonCauldron/*, like Golden Trophy / Jumbotron:
convert levels 50-60 (Quest_Goblin2_*: Foyer, Armory, CastleCourtyard, Forest1, GoblinCamp, Forest2, Bog, Mines1, BoilerRoom,
BellTowerStairs, BellTowerArena) with apkscene + gtscene jt=1 + the encounter-marker tool into data files (rrsk2bytes.sh),
lobby with the hologram console, armory (weapon rack), the acts in order with their encounters from the levels' EncountersV2
markers using the install's goblin enemies, the final boss (simplified is fine), rewards/score screen, back to the lobby.
Final-version facts: DN/research/rr_latest_rgames_story.md (the real 9 acts list is there) + rr_quests notes. Music/sounds
from the install (e.g. CrimsonCauldron* clips).` },
  { key: 'isle', prompt: `YOUR JOB (name: isle). Build "Isle of Lost Skulls" (quest, RrActivity.LostSkulls, RegisterLostSkulls) in basic
form in a new folder Games/Quests/LostSkulls/*, like Golden Trophy / Jumbotron: levels 62-70 (Quest_Pirate1_*: Foyer,
Hallway1, Hallway2, ShipDeck, Beach1, Beach2, Caves1, SunkenShip, BossArena) - re-convert with gtscene jt=1 (the existing
apkscene RRSK are stand-in heavy) + the encounter-marker tool into data files (rrsk2bytes.sh); lobby with the hologram
console, weapon pickups, the acts with their encounters (the install's pirate/skeleton enemies), the boss (simplified is
fine), score screen, back to the lobby. Final-version facts in DN/research/rr_latest_rgames_story.md. Install music
(PirateQuest*, Quest_Pirate1_* clips).` },
  { key: 'crescendo', prompt: `YOUR JOB (name: crescendo). Build "Crescendo of the Blood Moon" (quest, RrActivity.Crescendo,
RegisterCrescendo) in basic form in a new folder Games/Quests/Crescendo/*, like Golden Trophy / Jumbotron: levels 79-89
(Quest_Dracula1_*: Foyer, Township, CarriageRide, CastleEntry, CastleMainFoyer, Gallery, Library, Park, Ballroom, Dungeon,
BossRoom) converted with apkscene + gtscene jt=1 + the encounter-marker tool into data files (rrsk2bytes.sh); lobby with the
hologram console, weapons, the acts in Rec Room's order (the story doc says 13 acts with four Grand Hall visits - use the
level order the data/doc supports), the install's vampire/bat enemies, Dracula (simplified: the 3 light mirrors can be
basic), score screen, back to the lobby. Facts: DN/research/rr_latest_rgames_story.md. Install music (CBM_* clips).` },
  { key: 'lasertag', prompt: `YOUR JOB (name: lasertag). Build "Laser Tag" (RrActivity.LaserTag, RegisterLaserTag) in basic form in a new
folder Games/LaserTag/*, reusing the Paintball framework (teams, respawn, scoring, weapon spots) without editing it (compose or
subclass from your folder; ask via shared_requests if something must open up): maps Hangar (level76) and Cyberjunk City
(level77) - the existing apkscene RRSK are stand-in heavy, re-convert their hulls with gtscene jt=1 into data files
(rrsk2bytes.sh); Rec Room's final Laser Tag rules/modes/weapons from DN/research/rr_latest_rgames_action.md (the install's
laser tag guns and their RangedWeapon data), lobby console with map/mode, team battle at least, round timer, scoreboard,
back to the lobby. Install music (Arena_* clips).` },
  { key: 'sports', prompt: `YOUR JOB (name: sports). Build "Soccer" (RrActivity.Soccer, RegisterSoccer) and "Bowling" (RrActivity.Bowling,
RegisterBowling) in basic form in new folders Games/Soccer/* and Games/Bowling/*, in the style of Dodgeball / Paddleball:
Soccer from level72 (DN/refdata/apk/Soccer.rrsk; its hull via gtscene jt=1 into a data file if the stand-ins look wrong),
Rec Room's final soccer rules (DN/research/rr_latest_rgames_action.md: teams, the ball, goals, timer, kickoff), networked
ball; Bowling from level91 (BowlingAlley.rrsk), lanes, pins, the install's bowling balls, ten-frame scoring per player,
pinsetter reset, networked balls/pins. Lobby consoles, install music/sounds (Bowling_* etc.).` },
]

phase('Build')
// Two batches (the account's usage window): the user's priorities first, so they finish whole if the limit hits.
const RESUME = '\nNOTE: an earlier attempt of this job stopped at a usage limit. Your notes file (and any tools/data it lists) may already exist: read it first and CONTINUE from it; do not redo finished steps.'
const runBatch = keys => parallel(JOBS.filter(j => keys.includes(j.key)).map(j => () =>
  agent(CONTEXT + '\n' + j.prompt + RESUME, { label: 'build:' + j.key, phase: 'Build', schema: RESULT }).then(r => ({ key: j.key, r }))))
const batch1 = await runBatch(['jumbotron', 'makerpen', 'lasertag', 'sports'])
log('batch 1 done: ' + batch1.filter(x => x && x.r).map(x => x.key).join(', '))
const batch2 = await runBatch(['crimson', 'isle', 'crescendo'])
const built = batch1.concat(batch2).filter(x => x && x.r)
const missing = JOBS.map(j => j.key).filter(k => !built.find(b => b.key === k))
if (missing.length) log('builders without a result: ' + missing.join(', ') + ' - see DN/iter13/*.md')

phase('Integrate')
const digest = built.map(b => `## ${b.key} (compile_ok=${b.r.compile_ok})\nnotes: ${b.r.notes_file}\n${b.r.summary}\nfiles: ${(b.r.files_changed || []).join(', ')}\ndata: ${(b.r.data_files || []).join(', ')}\nshared requests: ${(b.r.shared_requests || []).join(' || ')}\nharness steps: ${(b.r.harness_steps || []).join(' ')}\nopen: ${(b.r.open_issues || []).join(' | ')}`).join('\n\n')
const integ = await agent(CONTEXT + `
YOUR JOB (name: integrate). The builders are done (digest below; without a result: ${missing.join(', ') || 'none'} - read their
DN/iter13/*.md and finish what is safely finishable). You may edit any file now, including shared ones.
1. Apply the builders' shared_requests where sound (small, additive). Make every new game reachable: the Rec Center category
   doors / room lists (Places/RecCenter/RrCategoryDoor.cs, RrRoomListPanel.cs - check how they list activities; Crimson,
   Isle, Crescendo under QUESTS; Laser Tag, Soccer, Bowling under SPORTS & PVP) and the watch's room list.
2. Run bash DN/tools/rrscene/bankscene.sh once (adds every Data/*.bytes to the scene). Compile check: 0 errors.
3. ONE harness run: a scenario "wave1_tour" that visits each new game (goto the activity, wait for its build, a lobby shot,
   start it via its console/test hook, a shot in play) plus the Jumbotron floaters check (a few shots where floaters were)
   and the maker pen (draw a stroke via its test hook, a shot). Use the builders' harness_steps. Check summary.txt + the log
   for exceptions. Fix clear breakage with small targeted edits (at most one more run).
4. Copy the shots to ${DN}\\iter13\\screens\\*.png; write ${DN}\\iter13\\wave1_result.md (per game: state, what works, what
   is missing, risks).
Return notes path, summary (last line "SHOTS: p1; p2; ..."), files changed, compile_ok, open issues.
Builder digest:
${digest}`, { label: 'integrate', phase: 'Integrate', schema: RESULT })
return { built, missing, integ }
