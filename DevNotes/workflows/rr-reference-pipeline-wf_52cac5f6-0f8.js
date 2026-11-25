export const meta = {
  name: 'rr-reference-pipeline',
  description: 'Rec Room Dreamport: turn the user-approved reference scenes into placement data, a runtime scene kit (staged), and a coverage plan for every place and game',
  phases: [
    { title: 'Reference', detail: 'scene parser + placement tables, runtime kit (staged, no project edits), coverage map' },
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
# Dreamport: Rec Room - reference pipeline (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop + VR). The world ships NO Rec Room files: at
runtime it loads everything from each player's own Rec Room install (plain Addressables bundles) through our RrAssets,
and rebuilds rooms in code. Iterations 1-3 built the Dorm, the final Rec Center (measured from the user's cache), 5 games.

The user has now provided REFERENCE folders and approved them for building the Dorm, the Rec Center and every game:
- REF1 = ${REF1}: output of Rec Room's own room exporter for 14 player rooms. They are already decoded:
  DN/research/rr_room_exports.md, decoder in DN/tools/roomexport, extracted files in ${NSP}/ex. Findings:
  - archive subrooms are hand reconstructions on a blank Maker scene;
  - DormRoom-2018 fits our Dorm kit frame exactly;
  - paintballtroll1 holds each official Paintball map's gameplay layer (flags, weapons, game configs) on the real map
    scenes, whose geometry is not included.
- REF2 = ${REF2}: 36k files, 8.3 GB, AssetRipper-style.
  - ".unity files/" holds 43 Unity YAML scenes: APPPLESARCHIVE-* (quest acts: GTact1-6 + GoldenLobby = Golden Trophy;
    Crescendo's GrandHall1-4/Foyer/ForestRoad/LowerSanctum/ConcertHall/Gallery/GardenOfSarrows/InvertedBallroom/
    ChamberOfKnowledge; Isle's IsleLobby/PiratesBanquet/PortPlunder/HighSeas/GibbetJungle/Caves/SunkenShip/
    GhostBeardGauntlet; CastleGrounds(Day)/BurningBurg; MakeItToMidnight; RecRally; OrientationStage*; ...), plus
    RecRoyale__PVP, RRLazerTagBeta and RunTheBlock. Some are ~16 KB, i.e. empty.
  - "Mesh/" holds 11.9k .glb files named by GUID.
  - ".assets/" holds 6.4k .asset files with .meta.
  - Also "2D Textures/" (png, .mat, .meta), "audio/", "Fbx,Prefabs,Objs/", "Fonts/", "PhysicMats/", "Sprites/"
    (json), "Rooms/" (the same exports as REF1) and "Clothes/".
  A first check: in GTact3, 107 of 249 distinct GameObject names equal a name in the install bundles
  (SP/rrtool/idx/objects.tsv), e.g. ActivityDoor_*, Barrel_Wine_fbx, Bench_LOD0, Curtain_LOD0, FatPot_Chunk1.

RULES (hard):
- Reference only. NEVER copy, import or move any file from REF1/REF2 into the Unity project (D:/Dreamport/Rec Room/Rec
  Room/Assets) or anywhere that ships. Read them; write derived numbers and names (placement data) only. Never modify
  REF1/REF2.
- This step does NOT edit the Unity project at all (the user may be uploading). Work in ${NSP} and in DN/tools,
  DN/refdata and DN/research only.
- Never touch Rec Room's encrypted install files. Install bundles are read via rrtool (README DN/tools/rrtool/README.md;
  binary SP/rrtool/bin/out/rrtool.exe; index SP/rrtool/idx/*.tsv: objects.tsv columns bundle, pathId, class, name,
  components).
- C# tools build with Unity's bundled Roslyn: see DN/tools/roomexport/build.sh (dotnet = C:/Program Files/Unity/Hub/Editor/
  6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe). Keep command output small. Mind Windows' 260-char path limit.
- Durability: create DN/iter4/<key>.md in your first minutes, update it after each milestone; if it exists at start,
  you were restarted - read it and continue.

THE CONTRACT between the tasks - the "RRSK 1" placement text format (one file per scene, UTF-8, one record per line,
space-separated, floats with up to 4 decimals, names with spaces encoded as %20):
  # RRSK 1 <scene name>
  B minx miny minz maxx maxy maxz                 # bounds of everything
  M <i> <install mesh name>                       # mesh table (install Mesh asset name, exact)
  T <i> <install material name>                   # material table (install Material asset name, exact)
  R <m> <t[,t...]> px py pz qx qy qz qw sx sy sz   # a renderer: mesh index, material indices per submesh, WORLD TRS
  P <install prefab root name> px py pz qx qy qz qw sx sy sz   # a whole install prefab (e.g. "(Sandbox_Barrel)") when a scene object IS one
  X <shape> cx cy cz ex ey ez qx qy qz qw r g b a  # stand-in for an unmatched renderer: shape box|cyl, world centre,
                                                  # half extents, rotation, average colour (from its material/texture)
  C box cx cy cz qx qy qz qw sx sy sz             # box collider, world OBB (centre, rotation, full size)
  C mesh <m> px py pz qx qy qz qw sx sy sz        # mesh collider using mesh table entry m
  C sphere cx cy cz r | C capsule cx cy cz qx qy qz qw r h
  L <point|spot|dir> px py pz qx qy qz qw r g b intensity range spotAngle
  A <install AudioClip name> px py pz volume minDist maxDist loop(0/1)   # ambient/looping audio sources
  G <kind> <name> px py pz qx qy qz qw [key=value...]  # gameplay markers: spawn, checkpoint, enemy_spawner, door,
                                                        # lever, trigger zone, weapon rack, flag base, etc.
  E fog <mode> r g b density start end | E ambient r g b | E sky <material name>   # environment
Frame: Unity world space of the scene as stored (Y up). Do not re-centre; also write the bounds.
`

const RESULT = {
  type: 'object',
  properties: {
    files: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
    numbers: { type: 'string' },
    problems: { type: 'array', items: { type: 'string' } },
  },
  required: ['files', 'summary', 'numbers', 'problems'],
}

const SCENES = `
# Your task: "scenes" - a scene parser and RRSK placement files for every reference scene
Key: scenes. Notes: DN/iter4/scenes.md. Tool source: DN/tools/rrscene/. Output: DN/refdata/<scene>.rrsk and
DN/refdata/<scene>.md (a short summary each) plus DN/refdata/INDEX.md (one row per scene).

1. GUID map: scan every .meta under REF2 ("guid: ..." line) to map GUID -> file. For each referenced mesh, work out its
   NAME: AssetRipper .asset Mesh YAML has m_Name; a .glb in Mesh/ has the mesh/node names in its JSON chunk (a .glb may
   hold several sub-meshes: GUID_0.glb, GUID_1.glb... - work out how fileID maps to them). For materials (.mat YAML):
   m_Name, shader GUID, _BaseColor/_Color, main texture GUID -> texture name. For prefabs (the few .prefab files) and
   PrefabInstance entries in scenes: note them.
2. Parse each Unity YAML scene: GameObject (m_Name, m_IsActive, m_Layer), Transform (m_LocalPosition, m_LocalRotation,
   m_LocalScale, m_Father) -> world TRS; MeshFilter (m_Mesh {fileID, guid}); MeshRenderer (m_Materials, enabled);
   SkinnedMeshRenderer; BoxCollider/MeshCollider/SphereCollider/CapsuleCollider (m_IsTrigger - triggers become G
   records); Light; AudioSource (clip GUID -> AudioClip name); ReflectionProbe (skip); RenderSettings (fog, ambient,
   skybox material) -> E records; MonoBehaviours: dump each distinct m_Script GUID with its field names and count, and
   map Rec Room gameplay ones (spawn points, checkpoints, enemy spawners, doors, levers, win zones, racks, flag bases)
   to G records - look at the field names to decide. Mind AssetRipper's truncated or empty m_Name values: fall back to
   the mesh name.
3. Match to the install: mesh names against install Mesh entries (objects.tsv class "Mesh"), material names against
   Material entries. When a GameObject subtree equals an install prefab (same root name as an install "GameObject*" root,
   e.g. "(Sandbox_Barrel)" or kit pieces), emit ONE P record for it instead of its renderers. Unmatched renderers -> X
   stand-ins with their average colour.
4. Write the .rrsk files. In INDEX.md give, per scene:
   - renderers;
   - matched mesh % and matched material %;
   - P count, X count, colliders, lights, audio, G markers;
   - bounds and file size;
   - the top 10 unmatched mesh names;
   - a one-line verdict: rebuildable from the install / partly / mostly stand-ins / empty.
5. Also convert the paintball gameplay layers from REF1 (DN/research/rr_room_exports.md, ${NSP}/ex) into
   DN/refdata/Paintball_<Map>_gameplay.rrsk: G records for flag bases, weapon pickups, standees and game configs, plus P
   records for the placed install props.
Keep the tool generic and documented (DN/tools/rrscene/README.md: build, run, format). Time-box about 60-75 minutes;
write outputs as you go (one scene at a time), so partial results survive.
Return: files, summary, numbers (the INDEX table in short form), problems.
`

const KIT = `
# Your task: "kit" - the runtime scene kit that rebuilds an RRSK placement from the player's install (staged)
Key: kit. Notes: DN/iter4/kit.md. The Unity project must NOT be edited now. Work in a staging copy:
copy ${CODE} to ${NSP}/stage/SomniumRecRoom (overwrite an older stage copy if one exists), add your files under
${NSP}/stage/SomniumRecRoom/Core/, and compile the whole staged tree with
"bash ${SPB}/compilecheck/build.sh dir ${NSP}/stage/SomniumRecRoom" (it must print 0 errors and "all N Fusion/Somnium
members are proven"; read DN/tools/compilecheck/build.sh for how dir mode works). Afterwards also copy your final files to
DN/iter4/kit_files/ so they can be installed into the project later.

Read first: DN/design/ARCHITECTURE.md section 1 (hard rules) and section 3 (Core API); CODE/Core/RrAssets.cs (TryFind,
Load, LoadRoutine, Resident: class ids 43 Mesh, 21 Material, 83 AudioClip, 1 prefab root), RrSpawn.cs (spawning install
prefabs, RrSpawnOptions.Scenery), RrMaterials.cs (every material through Fix), RrLook.cs (colour materials for stand-ins),
RrBuild.cs, RrLayers.cs, RrSound.cs, and how CODE/Places/RecCenter/RrRecCenterShell.cs merges many boxes (RrRcBatch in
RrRcGeo.cs).

Write:
1. Core/RrSceneKit.cs: parses RRSK 1 text (the contract above) and builds it under a parent Transform as a coroutine
   (IEnumerator Build(string rrsk, Transform parent, RrSceneKitOptions options, Action<RrSceneKitResult> done)).
   - Meshes and materials: load through RrAssets by exact name, cache them, fix materials via RrMaterials.
   - Renderers: static geometry combined per material (and per ~64k vertices) where practical, to keep draw calls
     low for VR.
   - P records: spawn through RrSpawn with Scenery options.
   - X stand-ins: RrLook.Solid colour boxes or cylinders.
   - Colliders: on RrLayers.Environment; triggers kept as markers.
   - Lights: capped count, shadows off, as the other rooms do.
   - Audio: RrSound loops.
   - Environment: fog/ambient; note that the world scene was saved with fog off (DN/tools/harness/README.md "Fog
     choice"), so fog can't be enabled at runtime and is reported only.
   - Gameplay markers: returned in the result list (kind, name, pose, key/values) for the game code to use.
   - Options: a transform offset (to put a scene at our room origin), a bounds filter, what to skip.
   - Spread the work over frames: at most ~4 ms per frame, via yields.
   - Log one summary line (counts, missing names, build ms). Clear statics on shutdown.
2. Core/RrSceneKitData.cs, or a documented pattern for data: how an RRSK text is embedded in the code. It must be a
   plain C# string constant split into chunks of <= 16k characters, because very long literals can be a problem, in a
   generated file per scene, e.g. Places/.../RrScene_GTact1.cs. Write DN/tools/rrscene/rrsk2cs.sh (or C#) that
   generates such a file from a .rrsk. Measure: how big the generated .cs would be for a large scene (use any .rrsk
   already in DN/refdata, or a synthetic one of 5,000 renderers), and propose quantisation if it is too big (e.g. > 1 MB
   per scene).
3. A harness-ready test hook (Core/RrSceneKitTest.cs) that builds an embedded small RRSK sample. It can be exercised in
   the next step, when the kit is installed. Put a tiny sample inline, using install names you verify exist, e.g. the
   Rec Center's own materials and a couple of prefabs.
Compile the staged tree. Return: files, summary, numbers (sizes, compile result), problems.
`

const COVERAGE = `
# Your task: "coverage" - what reference exists for the Dorm, the Rec Center and each of the 21 activities
Key: coverage. Notes: DN/iter4/coverage.md. Deliverable: DN/research/rr_reference_coverage.md (create early, fill as
you go).

Activities: 3D Charades, Bowling, Crescendo of the Blood Moon, Disc Golf, Dodgeball, Isle of Lost Skulls, Laser Tag, Make
It To Midnight, My Little Monsters, Paddleball, Paintball, Punch Up, Quest for the Golden Trophy, Rec Rally, Rec Royale,
Rec Room Showdown, Soccer, Stunt Runner, The Curse of the Crimson Cauldron, The Rise of Jumbotron, plus the Dorm and the
Rec Center (the Rec Center is done from the user's cache: DN/research/rr_reccenter_cache.md).

1. Inventory REF2 beyond the scenes (another agent parses the .unity scenes into DN/refdata - check that folder near the
   end for its INDEX.md and use its match rates):
   - Mesh/ and .assets/: sample the names to find which games' or maps' geometry is there (e.g. paintball maps
     River/DriveIn/Homestead/Quarry/Spillway/Clearcut/MerryMidway, Laser Tag, Dodgeball gym, Paddleball, Soccer, Disc
     Golf courses, Bowling alley, Showdown town, Stunt Runner, Midnight fairground, Little Monsters, Jumbotron ship,
     Crimson Cauldron castle).
   - "2D Textures/" names: which rooms' textures exist.
   - "audio/": music, VO and ambience per game.
   - "Fbx,Prefabs,Objs/": what they are.
   Compare with the install: SP/rrtool/idx/objects.tsv and DN/research/audioclips.tsv. Write a short list of what REF2
   has that the install does NOT have, per game.
2. Per activity (and Dorm/Rec Center) write a row + a short section:
   - the reference sources: REF2 scene(s) and whether they are empty or full; REF1 exports; install bundles; the
     research docs (DN/research/rr_latest_rgames_action.md, rr_latest_rgames_story.md, rr_shooters.md, rr_sports.md,
     rr_party.md, rr_quests.md);
   - whether the reference is the FINAL (pre-shutdown) version, an older one, or a player reconstruction, with the
     evidence: object names matching official kit names, gameplay scripts, dates, and agreement with the research
     descriptions of the final version;
   - geometry completeness: install-matched / REF2-only / missing;
   - a verdict: "rebuild from reference" / "reference + research" / "research only".
   - Name any user-provided reference that would still be needed.
3. Propose the next iterations (each one shippable and testable in the world, 1-3 places or games per iteration), in an
   order that front-loads the most complete references and the most-loved games. Note which built games need
   final-rule updates (Dodgeball no catch-outs, quests one-hit downs, Paintball maps and modes, etc. - see the
   rr_latest_* docs).
Return: files, summary, numbers (the coverage table in short form), problems.
`

phase('Reference')
const [scenes, kit, coverage] = await Promise.all([
  agent(COMMON + SCENES, { label: 'scenes', phase: 'Reference', schema: RESULT }),
  agent(COMMON + KIT, { label: 'kit', phase: 'Reference', schema: RESULT }),
  agent(COMMON + COVERAGE, { label: 'coverage', phase: 'Reference', schema: RESULT }),
])
return { scenes, kit, coverage }
