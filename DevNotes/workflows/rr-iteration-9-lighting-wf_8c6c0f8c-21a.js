export const meta = {
  name: 'rr-iteration-9-lighting',
  description: 'Rec Room Dreamport iteration 9: make Rec Room\'s realtime lighting work in the client - per-object light limit, ambient occlusion, hemisphere lights, glowing flames',
  phases: [{ title: 'Build', detail: 'one lighting agent, env owner' }],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'

const PROMPT = `
# Dreamport: Rec Room - iteration 9: Rec Room's realtime lighting, working in the Somnium client (2026-09-25)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop + VR). The world ships NO Rec Room files; it
builds rooms at runtime from the player's PC install (RrAssets, scene kit CODE/Core/RrSceneKit*.cs, environment
CODE/Core/RrEnvironment*.cs, post CODE/Core/RrPostFx*.cs). Read DN/iter8/env.md first (what iteration 8 did) and the
README "Iteration 8" section.

The user tested iteration 8 and sent a screenshot of the Golden Trophy Foyer: dark, and the lamp-post torches (small
cream flame squares) give off no light. They said: "I refuse to believe rec room used all real-time lights. Look at the
yellow squares here, they are torches and should give off light."

What the orchestrator found (verified):
- The torch glows are realtime point lights named "WarmGlow (N)": e.g. install 43ea1176 Light #7652678688170120839,
  type point, colour ~(0.98, ..), intensity 1.62, range 1.64 m, shadows none, m_Lightmapping 4 (realtime). The Foyer
  level (DN/refdata/apk/GoldenTrophy_Foyer.rrsk) has 40 L records (37 point, 3 spot), many of the small orange ones with
  range ~1.41 m at the lamp posts.
- Every Rec Room light carries UnityEngine.Rendering.Universal.URPLight - Rec Room's own fork of URP - with fields
  specularContribution (float; 0 on 44 of 60 sampled), isHemisphereLight (13 of 60), hemisphereLightInnerAngle (162),
  hemisphereLightOuterAngle (180). The install also has hand-placed "Bounce Light", "Room Fill", "Key Fill", "Orange Fill",
  "Cyan Fill" lights: Rec Room fakes bounced light with realtime fills.
- Rec Room's shaders: AG/Standard-Opaque keywords are _ADDITIONAL_LIGHTS, _ADDITIONAL_LIGHTS_VERTEX, _ADDITIONAL_LIGHT_SHADOWS,
  _FAST_MODE_SHADING, _RENDER_FEATURESET_FULL / _MOBILE, _SCREEN_SPACE_OCCLUSION, fog variants - and NO lightmap keywords
  (so no baked lightmaps); "Rec Room Studio Target/Lit" adds _MAIN_LIGHT_SHADOWS(_CASCADE/_SCREEN), _SHADOWS_SOFT,
  _MIXED_LIGHTING_SUBTRACTIVE. So Rec Room's look = realtime key light + soft shadows + many small point/spot/fill lights +
  SCREEN-SPACE AMBIENT OCCLUSION.
- The client (DN/iter8/env.md): URP Forward, at most 4 additional lights PER OBJECT, soft main-light shadows, 4 cascades,
  100 m shadow distance, and the client adds its own HBAO component into our global Volume profile (RrPostFx notes) at
  intensity 0 for worlds.

Your task (key "light9"; notes DN/iter9/light9.md, created early, updated per milestone, continue if it exists). You own
Core/RrSceneKit*.cs, RrSceneData.cs, Core/RrEnvironment*.cs, Core/RrPostFx*.cs, and small lighting-only edits in
Games/Quests/*, Games/Paintball/*, Places/* where a room adds or strips lights itself.
1. Per-object light limit. Measure which lights reach which renderers now (with 4 per object in Forward), in the GT
   Foyer, a GT act, the Jumbotron Foyer, the Paintball lobby, the Rec Center (cached building) and the Dorm. Change the
   kit's merging so every surface gets its nearby lights: merge only within small cells (or not at all) in rooms with
   many small lights, keep each merged piece compact, sort/assign so the lights that matter for a surface are within its
   4 (URP picks by distance/intensity per object - make objects small enough that the 4 nearest are the right ones).
   Keep draw calls sane for VR (report counts before/after).
2. Ambient occlusion like Rec Room's SSAO: find the client's HBAO VolumeComponent in our profile (it's added by the
   client; read CLIENT source E:/Somnium Space/Main Build/Assets for its type, parameters and defaults) and drive its
   parameters through the public VolumeComponent.parameters list / VolumeParameter<T>.value + overrideState - no
   reflection, nothing unproven (the compile guard must pass). Pick values that match Rec Room's soft contact shadows;
   if the component isn't there (editor/harness), log and skip. If there's no safe route, say so.
3. URPLight emulation: read each light's URPLight values from the levels (extend DN/tools/apkscene to emit them on the L
   record as keys, e.g. hemi=1 inner=162 outer=180 spec=0), and emulate: hemisphere lights as wide spotlights (outer angle
   capped at 179) facing the light's forward, or as point lights where that reads better; specularContribution 0 -> no
   visible highlight (URP has no per-light specular switch: approximate, e.g. keep them but note it).
4. Torches and lamps: make the flame cards glow (Rec Room's emissive flame material with bloom: check the flame/lantern
   materials' emission in the install and use it) so the lanterns read as light sources even from across the room; make
   sure every WarmGlow light next to a torch is kept (never culled by the kit's light cap) and lights the lamp post and
   the floor around it like in Rec Room. If Rec Room's URP fork attenuates differently than stock URP (compare the look
   against the footage/pictures you have: rr_paintball_footage.md, the Rec Center video notes in DN/research, and the
   fact that the user expects visible torch pools), compensate with a documented range/intensity factor.
5. Verify: compile ("bash ${SPB}/compilecheck/build.sh project --editor": 0 errors in all three builds, guard proven); a
   harness scenario light9_tour.json with "post": true shots before/after of: the GT Foyer from the user's viewpoint (the
   lobby spawn looking at the start hologram with lamp posts on both sides), a GT act with wall torches, the Armory, the
   Jumbotron Foyer, the Paintball lobby, the Rec Center; at most 6 harness runs. LOOK at every pair: torches must light
   their surroundings, corners get soft AO, nothing blown out.
Rules: ARCHITECTURE.md section 1; never modify the Somnium avatar or the client's camera; install-only at runtime; reference
data (DN/refdata, the APK at the orchestrator's scratchpad) is reference only. Unity's compiler rejects a lambda local that
shadows an outer local. New files need "bash ${SPB}/tools/metas.sh".
Return: files, summary (what was wrong, what changed, numbers), verification, pngs (before/after pairs), untested,
readme_section.
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
phase('Build')
const light9 = await agent(PROMPT, { label: 'light9', phase: 'Build', schema: RESULT })
return { light9 }
