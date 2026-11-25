export const meta = {
  name: 'rr-iteration-4',
  description: 'Rec Room Dreamport iteration 4: install the scene kit, Golden Trophy from its real acts with final quest rules, Dorm real layout, Dodgeball final rules',
  phases: [
    { title: 'Build', detail: 'scene kit + Golden Trophy levels, quest rules, Dorm layout, Dodgeball rules' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'
const REF1 = 'D:/Dreamport/EXTERNAL FILES FOR REFERNCE/Rec/Rooms'
const REF2 = 'D:/Dreamport/EXTERNAL FILES FOR REFERNCE/Downloads/RecRoomAssets'

const COMMON = `
# Dreamport: Rec Room - iteration 4 (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars
untouched). The world ships NO Rec Room files: at runtime it loads everything from the player's own install through
RrAssets and rebuilds rooms in code. Iterations 1-3 are in the user's hands and work. The user works in short iterations
and tests in the world: build it well, compile, take a few harness screenshots to be sure it looks right, stop.

## Paths
- CODE = ${CODE} (asmdef SomniumRecRoom; README.md = handover). DN = ${DN}: design/ARCHITECTURE.md section 1 = hard rules;
  research/*.md (grep headings, read only what you need).
- SP = ${SP} (bash ${SPB}): compilecheck/, harness/, rrtool/ (index SP/rrtool/idx/objects.tsv).
- Reference data (user-approved, REFERENCE ONLY - never copy/import any REF file into the project, never modify them):
  - REF1 = ${REF1}: Rec Room room exports, decoded; see DN/research/rr_room_exports.md. Section 0 of
    rr_reference_coverage.md corrects it: the archive subrooms sit on Rec Room Studio copies of the official scenes.
  - REF2 = ${REF2}: the Studio scenes of the official levels. They are parsed into DN/refdata/<scene>.rrsk
    (placement data; format in DN/tools/rrscene/README.md), with DN/refdata/<scene>.md and INDEX.md summaries.
  - Coverage and plan: DN/research/rr_reference_coverage.md. Final-version facts: DN/research/rr_latest_rgames_story.md
    (quests, 3D Charades...) and rr_latest_rgames_action.md (Paintball, Dodgeball...).
- The scene kit: DN/iter4/kit_files/ (9 files for Core/, staged and compile-clean) and DN/iter4/kit.md (API, data
  embedding, sizes, limits). The generator is DN/tools/rrscene/rrsk2cs.sh (writes RrScene_<Name>.cs; RRSZ1 compressed
  above 64k characters). The whole assembly can hold at most ~8.3M characters of string constants, so embed only the
  scenes you need, compressed.

## Rules
- The Unity project is open in the user's editor: edit files only, never run Unity on it (the harness uses a copy).
- ARCHITECTURE.md section 1 applies. Never modify the Somnium avatar. Every room is at the world origin; only the current
  room exists.
- Only meshes/materials/prefabs that exist in the INSTALL can be loaded at runtime; REF2-only meshes become the kit's
  stand-ins (X records) or are rebuilt by hand. That's expected - the layout and gameplay are what we take from REF2.
- Edit ONLY the files your task owns. Compile: "bash ${SPB}/compilecheck/build.sh project --editor" -> 0 errors in all
  three builds and "all N Fusion/Somnium members are proven". Errors in files you don't own = another agent mid-edit:
  wait 2 minutes, retry. New files: "bash ${SPB}/tools/metas.sh".
- Harness: "bash ${SPB}/harness/run.sh <scenario>" (Bash timeout 600000; serialised by a lock). Shot steps take
  "frame": "room". LOOK at the PNGs. At most the number of runs your task says. Unity's compiler rejects a lambda local
  that shadows an outer local (CS0136).
- House style: XML doc comments that explain why, with measured facts; RrLog.Info("Area", ...) once per key fact.
- Durability: create DN/iter4/<key>.md in your first minutes (for "kit"/"scenes" files from the previous step already
  exist there - use a new name), update it after each milestone; if it exists at start, you were restarted: continue.

## User decisions
Games follow Rec Room's FINAL rules and content, with player minimums low enough for small Somnium groups.
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

const GTLEVELS = `
# Your task: "gtlevels" - install the scene kit, rebuild Quest for the Golden Trophy's lobby and acts 1-6 from the reference
Key: gtlevels (notes DN/iter4/gtlevels.md).
You own: CODE/Core/RrSceneKit*.cs and the other kit files (install them from DN/iter4/kit_files/), ONE added line in
CODE/Core/RrMain.cs OnDestroy (RrSceneKit.Shutdown()), CODE/Games/Quests/GoldenTrophy/* (including a new Scenes/
subfolder for the generated RrScene_*.cs data), harness scenarios goldentrophy_*.json and scenekit_test.json.
Another agent ("gtrules") changes CODE/Games/Quests/Shared/* at the same time (final quest rules); it only ADDS to public
APIs. If you need something there, write it in DN/iter4/gtlevels.md under "requests to gtrules" and work around it.

1. Install the kit (copy the kit files into CODE/Core/, run metas.sh, add the RrMain line, compile) and run
   scenekit_test.json once. Fix what fails, but keep the kit's public API as documented in kit.md.
2. The Golden Trophy reference: DN/refdata/GoldenLobby.rrsk and GTact1..GTact6.rrsk (with .md summaries; install
   match 37-64%, lobby 60%). rr_reference_coverage.md explains that these are the official levels (the 2020 merch
   store, act 6 = the library). Read DN/research/rr_latest_rgames_story.md for Golden Trophy's final acts, rules and
   points, and the README's quest section for how our quest framework works (RrQuestRoom, RrQuestAct, RrQuestDef, waves,
   spawn groups, doors, lava, pots, the boulder, the Goblin King).
3. Generate compressed RrScene_GoldenLobby and RrScene_GTact1..6 data files (rrsk2cs.sh). Build each area with
   RrSceneKit at an offset inside the quest room. All rooms share the world origin; keep every area within about 1 km
   of it. Prefer building only the lobby plus the current act, and the next act behind the act-door transition, to
   keep memory and frame time down. If the framework makes that hard, build them all spread over frames behind the
   loading card, and say which you chose.
4. Wire the gameplay markers (G records: enemy spawn points, checkpoints/player spawns, doors, the boulder, chests,
   pots, levers, lava/kill zones, triggers) into the acts: act entry/checkpoint, exit door, spawn groups for the waves,
   pots/chests as breakables, the boulder lane. The markers have names and poses but no values: take counts, types
   and timings from the existing act definitions (RrGoldenTrophyLevel.cs) and the research. Acts 7-9 are not in the
   reference: keep the existing research-built acts 7-9 (the finale and the Goblin King) after the six real ones.
   Remove guessed geometry that the real levels replace.
5. Look: the kit's stand-ins for REF2-only meshes are plain coloured shapes. Make the big ones read well: pick Rec Room
   materials from the install where a stand-in is a wall/floor/stone (RrQuestKit's tiling pass already textures boxes
   with Rec Room stone and wood).
6. Verify: compile; update and run goldentrophy_run.json / goldentrophy_acts.json / goldentrophy_final.json (room-frame
   shots of the lobby and every act, from the player's height). At most 5 harness runs. LOOK at the shots and fix the
   worst problems (floating pieces, missing floors, pitch-black areas, stand-ins in the player's face).
Return per the schema (pngs = the best 6-8 shots).
`

const GTRULES = `
# Your task: "gtrules" - the quests' FINAL rules in the shared quest framework
Key: gtrules (notes DN/iter4/gtrules.md).
You own: CODE/Games/Quests/Shared/*, and small quest-related additions in CODE/Games/Shared/* (health, announcer,
combat) that don't change behaviour for the non-quest games. Another agent ("gtlevels") rebuilds Golden Trophy's levels
in CODE/Games/Quests/GoldenTrophy/* at the same time: only ADD public members; never rename or remove one it may use.
Check DN/iter4/gtlevels.md for its requests.

From DN/research/rr_latest_rgames_story.md (sections on Golden Trophy and the quest framework; section 5.3 lists 11
changes) and rr_quests.md:
1. One hit downs a player in the quests (Rec Room's final rule, all quests): a down, then revive or bleed-out as today.
   Enemies' damage to players no longer uses HP.
2. Kill points per enemy type from the wiki values (450/500/850/850/1500/2000/5500 as the doc maps them), not the
   enemy's max HP; pots and chests keep their values; the death penalty stays.
3. Rating from fixed gold totals (Golden Trophy S 150k, A 125k, B 100k; per-quest thresholds in the def, so other quests
   can set theirs: Crimson Cauldron S 300k, A 275k, B 250k, ...).
4. Coach says "Game On!" at the run's start (clip Gameon, bundle 8b1b2e31, key e - check it loads).
5. The boss's death kills every remaining enemy (make it a framework feature the boss code calls).
6. Player counts: Rec Room's final caps per quest (1-4; Isle and Crescendo 1-3), with minimum 1.
7. Anything else in 5.3 that belongs in the framework (the dressing room with 4 swords, 2 shields and 2 bows is a
   level thing - leave the layout to gtlevels, but provide the rack API if it is missing).
Verify: compile; run goldentrophy_run.json once near the end if gtlevels is not holding the harness (check its notes);
at most 2 runs. Return per the schema (pngs may be empty).
`

const DORM = `
# Your task: "dorm" - the Dorm in its real layout
Key: dorm (notes DN/iter4/dorm.md). You own CODE/Places/Dorm/* and the scenario dorm_room.json.

The real 2018 "Spring Cleaning" dorm's object layout is in REF1: the DormRoom-2018 subroom of the archive room. Its
props sit exactly in our Dorm kit's frame. See DN/research/rr_room_exports.md (the Dorm tables and the comparison with
our guessed layout) and its correction in rr_reference_coverage.md section 0. It shows our guess was wrong in four
places:
- the exit door is on the left wall under the loft, at about (-4.82, 0, -0.43), facing +x;
- the mirror is on the front wall under the arched window, at (0.88, 1.34, 3.01), facing -z;
- the bunk bed and desk stand against the back wall, with the radio at (1.381, 0.883, -2.529) and cups and a bottle
  nearby;
- the spawn (welcome mat) is under the loft at (-3.97, 0.04, -0.92).
Our one data-backed fact agrees with it: the hoop rim at (3.905, 1.097, -1.090), with the basketball at
(3.409, 0.136, -1.142).
1. Re-place the Dorm's furniture and props at the DormRoom-2018 positions and rotations (armchair, coffee table, plants,
   loft whiteboard and markers corner, radio, cups, bottle, basketball, board, posters, bin...). Keep the iteration-3
   final touches (notebook posters, the HELP panel, one armchair, no dresser, the marker set, the Play board) and put
   them where the export's layout has the matching objects or free wall space. Move the RrPortal exit door to the export's
   door position. The spawn goes to the welcome mat, facing into the room. Update the mirror, board, ladder and radio
   positions in their components if they are hard-coded.
2. Check nothing overlaps (walls, the ladder gap, the loft rail) and nothing floats.
3. Verify: compile; update dorm_room.json's shots for the new layout (room frame) and run it; at most 3 runs. LOOK.
Return per the schema (pngs = 4-5 shots).
`

const DODGE = `
# Your task: "dodgeball" - Dodgeball's final rules
Key: dodgeball (notes DN/iter4/dodgeball.md). You own CODE/Games/Dodgeball/* and dodgeball_gym.json.
From DN/research/rr_latest_rgames_action.md (Dodgeball section) and the README's Dodgeball section:
1. Since 2025-01-14 a catch puts nobody out ("no more catch-outs"): CatchOutsThrower = false and no "teammate comes
   back". A catch still makes the ball dead, with Rec Room's catch feedback.
2. Since 2021-04 a screen-mode throw goes at 14 m/s however long you charge (no charge scaling).
3. The 2026 low barriers behind each side of the court: build them if the research gives enough; otherwise note it.
4. Player minimum 2 (1 v 1 allowed), or practice alone as today.
5. The physics values from REF2's PhysicMats (rr_reference_coverage.md mentions 29, including Dodgeball's): use its
   friction/bounce if it differs from ours.
Verify: compile; run dodgeball_gym.json (update expectations for the new rules); at most 2 runs.
Return per the schema.
`

phase('Build')
const results = await Promise.all([
  agent(COMMON + GTLEVELS, { label: 'gtlevels', phase: 'Build', schema: RESULT }),
  agent(COMMON + GTRULES, { label: 'gtrules', phase: 'Build', schema: RESULT }),
  agent(COMMON + DORM, { label: 'dorm', phase: 'Build', schema: RESULT }),
  agent(COMMON + DODGE, { label: 'dodgeball', phase: 'Build', schema: RESULT }),
])
return { gtlevels: results[0], gtrules: results[1], dorm: results[2], dodgeball: results[3] }
