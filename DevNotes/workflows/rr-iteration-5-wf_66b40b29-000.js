export const meta = {
  name: 'rr-iteration-5',
  description: 'Rec Room Dreamport iteration 5: Rec Room post-processing per room, level particle effects and ambient/effect sounds from the references',
  phases: [
    { title: 'Build', detail: 'post-processing per room; effects + sounds through the scene kit and the rooms' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'
const REF2 = 'D:/Dreamport/EXTERNAL FILES FOR REFERNCE/Downloads/RecRoomAssets'
const CACHE = 'C:/Users/derii/AppData/LocalLow/Against Gravity/Rec Room/HTTPCache'

const COMMON = `
# Dreamport: Rec Room - iteration 5 (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars untouched).
The world ships NO Rec Room files: it loads everything from the player's own install through RrAssets at runtime.
Iterations 1-4 are built and working.

The user asked: "Can you add the same post processing as they added please? and effects sound". That means Rec Room's
own post-processing per room, plus the visual effects and sounds Rec Room put in its rooms. The user tests in the world:
build it well, compile, take a few harness screenshots (before/after where it matters), stop.

## Paths
- CODE = ${CODE} (README.md = handover). DN = ${DN}: design/ARCHITECTURE.md section 1 = hard rules;
  research/somnium_conventions.md (grep "Global Volume", "Post", "HBAO").
- SP = ${SP} (bash ${SPB}): compilecheck/, harness/ (scenarios, out/), rrtool/ (install index SP/rrtool/idx/objects.tsv;
  DN/research/audioclips.tsv is at SP/research/audioclips.tsv).
- References (user-approved, REFERENCE ONLY: never copy or import a file from them into the project, never modify them):
  - REF2 = ${REF2}.
    - ".unity files/<room>/" holds the Rec Room Studio copies of the official levels. Each scene has a
      <scene>-PostProcessingProfile.asset (16 of them): a URP VolumeProfile in YAML, whose components are MonoBehaviours
      with m_Script GUIDs (e.g. 0b2db861... is Bloom: threshold/intensity/scatter/tint; 66f335fb... is
      ColorAdjustments: postExposure/contrast/colorFilter/hueShift/saturation) and "m_OverrideState" plus "m_Value" per
      parameter.
    - Each scene also has <scene>-AudioClips.asset, the list of clips the room uses (GUIDs; resolve them through
      .meta files in REF2's audio/ folder to clip names).
    - The scenes' ParticleSystem objects are the level effects.
  - The parsed placement data is in DN/refdata/*.rrsk (format in DN/tools/rrscene/README.md); the parser is in
    DN/tools/rrscene.
  - The user's download cache ${CACHE}: 1AF.data is the final Rec Center scene (RecCenter_071825) and 35C.data is Make It
    To Midnight. The user approved both for MEASUREMENTS ONLY; read them with the tool in DN/tools/rrtool_cache (see its
    README and DN/research/rr_reccenter_cache.md).
- The scene kit (runtime): CODE/Core/RrSceneKit*.cs, RrSceneData.cs, RrSceneMarker.cs (notes DN/iter4/kit.md).

## Rules
- The Unity project is open in the user's editor: edit files only, never run Unity on it (the harness uses a copy).
- ARCHITECTURE.md section 1 applies. Never modify the Somnium avatar; never touch the client's camera. Every room is at
  the world origin; only the current room exists.
- Edit ONLY the files your task owns. Compile with "bash ${SPB}/compilecheck/build.sh project --editor". It must report
  0 errors in all three builds and "all N Fusion/Somnium members are proven". An UNPROVEN member means the upload may
  fail: find a proven route. If the errors are in files you don't own, another agent is mid-edit: wait 2 minutes and
  retry. New files: "bash ${SPB}/tools/metas.sh".
- Harness: "bash ${SPB}/harness/run.sh <scenario>" (Bash timeout 600000; runs are serialised by a lock). Shots take
  "frame": "room", plus "post": true to render with post-processing (see DN/tools/harness/README.md). LOOK at the PNGs.
  Unity's compiler rejects a lambda local that shadows an outer local (CS0136).
- House style: XML doc comments that explain why, with measured facts; RrLog.Info once per key fact.
- Durability: create DN/iter5/<key>.md in your first minutes and update it after each milestone. If it exists at start,
  you were restarted: continue from it.
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

const POSTFX = `
# Your task: "postfx" - Rec Room's post-processing, per room
Key: postfx (notes DN/iter5/postfx.md).
You own:
- new CODE/Core/RrPostFx.cs, and new data file(s) CODE/Core/RrPostFxProfiles.cs;
- CODE/Rooms/RrRoom.cs, RrTravel.cs and RrTravel.Origin.cs (only to apply and clear the profile on room enter and leave);
- ONE call in CODE/Games/Quests/GoldenTrophy/RrGoldenTrophyRoom.cs where the current act changes (RrPostFx.SetArea(...));
- CODE/Core/RrSpawn.cs (only the Volume-stripping part, if needed);
- the harness scenario postfx_tour.json.
Another agent ("fxsound") edits the scene kit and the Places/ and GoldenTrophy/Scenes files at the same time: don't
edit those.

1. The client (CLIENT = E:/Somnium Space/Main Build/Assets, read only): find how it sets up post-processing: its global
   Volume(s) and profiles, their priority, which overrides it sets (tonemapping, bloom, colour, HBAO), the camera's
   volume mask/layer, and whether a world's own global Volume is honoured. ETS did it: "ETS builds its own 'Ets Post'
   volume at runtime". Find that code in D:/Dreamport/Euro Truck Simulator/Euro Truck Sim (grep VolumeProfile, Volume)
   and copy its proven approach. Use only URP types the compile check proves.
2. Rec Room's profiles, per room:
   - the 16 REF2 <scene>-PostProcessingProfile.asset files (Golden Trophy acts and lobby, Crescendo, Isle, Rec Royale,
     Rec Rally, Make It To Midnight...). Map each m_Script GUID to its URP component by its field names;
   - the cache's Rec Center scene (1AF.data) and its dependency bundles. Find its Volume and profile and read their
     values with DN/tools/rrtool_cache;
   - the install bundles: Rec Room prefabs that carry a Volume, which RrSpawn strips today (e.g. the Charades Ink Space
     hull; search objects.tsv components for "Volume"). Their profiles live in the install, so we may even load and use
     them directly, if loading a VolumeProfile from the bundles works in 6000.3 (try it in the harness);
   - the Dorm and the other rooms: find Rec Room's default room profile. The blank Maker/Studio scene's profile is in
     REF2; otherwise use the most common values.
   Write the values into RrPostFxProfiles.cs as plain data (component -> overridden parameters), keyed by activity and
   optionally by area (quest acts).
3. RrPostFx: one global Volume that we own (a new GameObject; never on the client's objects). A runtime VolumeProfile is
   built from the data. It is applied when a room is entered and removed when it is left, with a short blend, and
   SetArea is available for quest acts. Priority above the client's volume only for the components Rec Room sets.
   VR-safe: skip depth of field, motion blur, lens distortion, chromatic aberration and film grain in VR (desktop may
   keep DoF/CA if Rec Room had them; state your choice). Keep bloom and colour grading. Tonemapping follows Rec Room's
   mode. Clear statics on shutdown and restore the client's state exactly.
4. Verify: compile; a harness scenario postfx_tour.json with "post": true shots of the Dorm, the Rec Center, the Golden
   Trophy lobby and one act, Dodgeball and Paintball, before and after (a hook to toggle RrPostFx). At most 4 harness
   runs. LOOK: nothing blown out, nothing too dark, and it matches Rec Room's look (warm, soft bloom).
Return per the schema (pngs = before/after pairs).
`

const FXSOUND = `
# Your task: "fxsound" - Rec Room's level effects and sounds in our rooms
Key: fxsound (notes DN/iter5/fxsound.md).
You own:
- CODE/Core/RrSceneKit*.cs, RrSceneData.cs and RrSceneMarker.cs (add effect and sound support);
- DN/tools/rrscene (the parser) and DN/refdata (regenerate);
- CODE/Games/Quests/GoldenTrophy/Scenes/* (regenerated data);
- CODE/Places/RecCenter/* and CODE/Places/Dorm/* (effects and ambience only; don't change layouts);
- the harness scenarios fxsound_*.json.
Another agent ("postfx") does post-processing at the same time. It owns Core/RrPostFx*, Rooms/RrRoom.cs, RrTravel*.cs
and one line in RrGoldenTrophyRoom.cs: don't edit those.

1. Effects in the reference levels. Extend the rrscene parser so that ParticleSystem objects (and their parent prefab
   instances) emit a new "F" record: install effect prefab name, pose and scale. Match by name against install prefab
   roots that contain a ParticleSystem (objects.tsv components): e.g. torches, fire, candles, dust, light shafts, lava
   bubbles, fog, sparkles. Unmatched effects: pick the closest install effect by name or kind (fire -> a Rec Room fire
   effect...), or skip them and list them. Document F in DN/tools/rrscene/README.md. The kit spawns F records through
   RrSpawn with particles kept (see RrFx.cs), capped per room for VR (e.g. 40, nearest the areas that matter).
2. Sounds in the reference levels:
   - resolve each scene's <scene>-AudioClips.asset (clip GUIDs -> REF2 audio/.meta -> clip names) and all its
     AudioSources to install AudioClip names (SP/research/audioclips.tsv; objects.tsv class AudioClip);
   - emit or extend "A" records for positional ambient loops (fire crackle, wind, water, lava, crowd, machinery) and
     list the one-shot clips per scene in DN/refdata/<scene>.md;
   - the kit plays the ambient loops through RrSound (3D, Rec Room's volume/distance), capped per room.
   Regenerate the Golden Trophy data files (rrsk2cs.sh) so the lobby and acts get their effects and sounds.
3. The Rec Center and the Dorm (no REF2 scene, but real sources):
   - Rec Center: the cache scene 1AF.data (with DN/tools/rrtool_cache; rr_reccenter_cache.md lists roots such as
     LightShafts) has its particle effects, light shafts and ambient audio sources at measured poses. Add them to the
     Rec Center with install prefabs and clips of the same names.
   - Dorm: its light shafts and ambience (AmbianceSummerOutdoor; rr_dorm_core.md). Check what is already there and add
     what Rec Room had (dust in the light shafts?).
4. Interaction sounds audit: check that the things players touch in the built rooms make Rec Room's sounds. List gaps
   and fix the cheap ones in your folders; list the others for later:
   - doors: hover and open;
   - the Rec Center list panels (the watch sounds);
   - pots breaking and chests in quests;
   - weapon racks refilling;
   - the boulder rolling;
   - lava sizzle;
   - footsteps on stairs? Only if Rec Room had them.
5. Verify: compile; a harness scenario fxsound_tour.json with shots that show effects (the Golden Trophy lobby and an act
   with torches, the Rec Center light shafts) and a log check of the ambient sources started (clip, position). At most 4
   harness runs. LOOK at the shots.
Return per the schema.
`

phase('Build')
const [postfx, fxsound] = await Promise.all([
  agent(COMMON + POSTFX, { label: 'postfx', phase: 'Build', schema: RESULT }),
  agent(COMMON + FXSOUND, { label: 'fxsound', phase: 'Build', schema: RESULT }),
])
return { postfx, fxsound }
