export const meta = {
  name: 'rr-apk-paintball',
  description: 'Rec Room Dreamport: read the Android build\'s plain level files (reference only), convert Paintball lobby + 7 maps to placement data, rebuild Paintball from them',
  phases: [
    { title: 'Convert', detail: 'type-tree-less level reader (PC bundle type trees), identify all levels, RRSK for Paintball' },
    { title: 'Build', detail: 'Paintball lobby + 7 maps rebuilt from the real layouts with final rules' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'
const APK = NSP + '/apk'

const COMMON = `
# Dreamport: Rec Room - the Android build as a layout reference (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop + VR). The world ships NO Rec Room files; at
runtime it builds everything from the player's own PC install (plain Addressables bundles) through RrAssets and our
scene kit (CODE/Core/RrSceneKit*.cs; notes DN/iter4/kit.md; RRSK placement format in DN/tools/rrscene/README.md).

The user provided the Android build of Rec Room (a 2026-03-23 XAPK from APKPure). Treat it as DATA ONLY: never install,
execute or load it; nothing from it may be copied into the Unity project or shipped. Use it the way we use the user's
cache and the dump: read it, measure it, write derived placement numbers/names into our code.
- Extracted packs are in ${APK}: UnityDataAssetPack.apk (a zip; assets/bin/Data/* holds 113 level files, some split into
  levelN.split0..k (concatenate them in order), plus sharedassetsN.assets / hashed .resource / GUID-named files) and
  UnityStreamingAssetsPack.apk (assets/aa/Android/*.bundle, Addressables).
- The level files are PLAIN Unity serialized files (format 22, Unity 6000.0.27f1 = the same version as the PC install)
  WITHOUT type trees (a player build). The PC install's plain bundles DO carry type trees for the same Unity version:
  harvest the type trees of built-in classes (GameObject 1, Transform 4, RectTransform 224, MeshFilter 33, MeshRenderer
  23, SkinnedMeshRenderer 137, BoxCollider 65, MeshCollider 64, SphereCollider 135, CapsuleCollider 136, Light 108,
  Terrain 218, TerrainCollider 154, TerrainData 156, LODGroup 205, ReflectionProbe 215, AudioSource 82, Mesh 43, Material
  21, MonoBehaviour 114, MonoScript 115, ...) from them and use them to parse the level files; MonoBehaviour data by the
  script's type tree where a PC bundle has one for the same script.
  Our reader: CODE/Rr/ (RrSerializedFile etc.) and the rrtool research tool (DN/tools/rrtool; index SP/rrtool/idx).
- A first scan (keyword counts per level, ${APK}/levelscan.tsv) suggests:
  - level14 River, level103 Drive-In, level15 Homestead, level16 Quarry, level17 Clearcut, level18 Spillway,
    level104 Merry Midway, level113 probably the Paintball lobby;
  - level7 a built-in Rec Center (older than the final Studio one);
  - level11 and level34 Dodgeball/Paddleball, level12 Charades, level53/58 Golden Trophy acts, level62/65/66 Isle of
    Lost Skulls acts, level107 Rec Rally.
  Confirm each by its contents.
- Ground truth to validate the frame: the exact Paintball flag bases and weapon spots in DN/refdata/Paintball_<Map>_
  gameplay.rrsk. They come from Rec Room's room exports, which sat ON these official map scenes, so the scene's own
  objects should line up with them.
- Footage notes for the final lobby: DN/research/rr_paintball_footage.md. Final rules: DN/research/
  rr_latest_rgames_action.md (Paintball section). The current Paintball build: CODE/Games/Paintball/* (README section
  "Paintball").
Rules: ARCHITECTURE.md section 1 (DN/design/ARCHITECTURE.md). Never modify the Somnium avatar. Compile "bash ${SPB}/compilecheck/
build.sh project --editor" (0 errors, "all N Fusion/Somnium members are proven"; errors in files you don't own = another
agent mid-edit, wait 2 min). New files: "bash ${SPB}/tools/metas.sh". Harness: "bash ${SPB}/harness/run.sh <scenario>"
(timeout 600000, serialised by a lock), "frame": "room" shots; LOOK. Another agent is working on CODE/Places/RecCenter at
the same time: don't touch it. Durability: DN/apk/<key>.md, created early, updated per milestone; continue if it exists.
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

const CONVERT = `
# Your task: "convert" - read the Android level files and write RRSK placement data
Key: convert (notes DN/apk/convert.md). You own DN/tools/apkscene/ (new tool), DN/refdata/apk/ (output), DN/apk/*.
No project code edits.
1. Build the reader (C#, Unity's Roslyn like DN/tools/roomexport/build.sh): a type-tree bank harvested from PC bundles
   (by class id, plus MonoBehaviour trees by script), a parser for type-tree-less serialized files (with externals and
   PPtr resolution into sharedassetsN.assets and the GUID-named files for mesh, material and texture NAMES and bounds),
   and split-file joining.
2. Identify every one of the 113 levels (room and area, by object names/scripts/signs) -> DN/refdata/apk/INDEX.md.
3. For the Paintball lobby and the 7 maps, write DN/refdata/apk/Paintball_<Map>.rrsk in the RRSK format (see the README;
   R/P/X/C/L/A/G/E records, the '?' and '#U' conventions). Work one map at a time so partial results survive.
   - Resolve every mesh and material name, and match it to the PC install (SP/rrtool/idx/objects.tsv) so the kit can
     load it.
   - Handle static batching: a renderer that points into a combined mesh keeps its GameObject name; match by that and by
     the submesh range.
   - Terrains: write their heightmap as a coarse grid of heights (at most a 2 m cell) under a new "H" record (document it
     in the README). The kit builds our own terrain mesh from it, textured with install materials.
   - Colliders exactly as the level has them: they are the walkable shape.
   - Gameplay markers (G): spawn points, flag bases, weapon pickups, out-of-bounds volumes, team zones.
   Validate each map's frame against Paintball_<Map>_gameplay.rrsk: flag bases within 0.5 m; report the offset.
4. Then the other rooms in priority order, while time allows (1 file each): the older built-in Rec Center (level7, for
   the no-cache fallback), the Dodgeball/Paddleball gym, Charades, Laser Tag, Soccer, Disc Golf, Bowling, Showdown, the
   Dorm (if built in), Golden Trophy acts 7-9, Isle acts, Rec Rally, Rec Royale, Stunt Runner.
In INDEX.md, per converted level: renderer count, install-mesh match %, colliders, terrain yes/no, and a verdict.
Time-box about 90 minutes; the Paintball files come first.
Return per the schema (files = the tool + the rrsk files; summary with the match rates and the validation offsets).
`

const PAINTBALL = (c) => `
# Your task: "paintball" - rebuild Paintball from the real layouts: the 2025 lobby and all 7 final maps
Key: paintball (notes DN/apk/paintball.md). You own CODE/Games/Paintball/*, new data files under
CODE/Games/Paintball/Scenes/ (generated from the rrsk files with DN/tools/rrscene/rrsk2cs.sh, compressed), additive
changes to CODE/Core/RrSceneKit*.cs if the kit needs the new "H" terrain record or other additions (keep its API), and
scenarios paintball_*.json. The converter agent's result:
${c ? JSON.stringify(c).slice(0, 7000) : '(missing - read DN/apk/convert.md and DN/refdata/apk/INDEX.md)'}
The user said the current Paintball and its lobby are "completely wrong". It was two maps hand-built from text.
1. The lobby: build DN/refdata/apk/Paintball_Lobby.rrsk (or the converter's lobby file) with the scene kit, and check
   it against DN/research/rr_paintball_footage.md: the Practice Arena (1v1), the leaderboard, the skins shop and test
   wall, the shooting gallery, picnic tables, the radio. Keep our game controls working: team and map/mode selection,
   START, the Rec Center door. Put them where Rec Room had its equivalents.
2. The 7 maps: River, Drive-In, Homestead, Quarry, Clearcut, Spillway, Merry Midway, each from its rrsk (all maps in
   one room at offsets within ~1 km of the origin, or built per match - choose and justify). Map selection offers all 7.
   Flags, weapon pickups and spawns come from the real data; teams spawn at Rec Room's spawn points.
3. Final rules (rr_latest_rgames_action.md): START button only (no auto-start); head and torso hits count; grenades can
   hurt teammates (GrenadesCanDamageTeammates = 1); Drive-In's paint thrower and paint mines; the Splat Hammer where the
   research says. Player minimum 2 (user decision: final rules, but low enough minimums for small groups).
4. Look: install-matched meshes load as Rec Room's own; unmatched pieces get the kit's stand-ins in their real sizes,
   colours and positions. Terrain from the H records gets install grass/dirt/rock materials. Make the big stand-ins read
   well (install materials by kind), as the Golden Trophy pass did.
5. Verify: compile; paintball_all.json updated (room-frame shots of the lobby and every map from player height, plus a
   CTF round on one map); at most 5 harness runs. LOOK at the shots next to the footage notes and fix the worst problems.
Return per the schema (pngs = the lobby + one shot per map).
`

phase('Convert')
const convert = await agent(COMMON + CONVERT, { label: 'convert', phase: 'Convert', schema: RESULT })
phase('Build')
const paintball = await agent(COMMON + PAINTBALL(convert), { label: 'paintball', phase: 'Build', schema: RESULT })
return { convert, paintball }
