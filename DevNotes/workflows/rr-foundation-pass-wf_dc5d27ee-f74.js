export const meta = {
  name: 'rr-foundation-pass',
  description: 'Rec Room Dreamport: fix grab orientation, watch on wrist, one-origin rooms + isolation; research latest room/game versions',
  phases: [
    { title: 'Build', detail: 'hands model, placement, isolation, grab, watch + 3 research agents' },
    { title: 'Review', detail: 'adversarial review of each code change' },
    { title: 'Fix', detail: 'apply review findings' },
    { title: 'Integrate', detail: 'full harness suite, README' },
  ],
}

const PROJ = 'D:/Dreamport/Rec Room/Rec Room'
const CODE = PROJ + '/Assets/#User/SomniumRecRoom'
const DN = PROJ + '/DevNotes'
const SP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const SPB = '/c/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad'
const NSP = 'C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad'
const CLIENT = 'E:/Somnium Space/Main Build/Assets'
const HP = SP + '/unityharness/RRHarness/Assets/RrHarnessTests'

const COMMON = `
# Dreamport: Rec Room - foundation pass (2026-09-24)
Rec Room rebuilt inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars). The first
build works in the client. The user tested it in VR and reported four issues. This pass fixes three in code (held items
sit ~90 degrees off the hand; the VR watch floats off the avatar's wrist; everything jitters because rooms are built
kilometres from the world origin) and researches the fourth (use the LATEST versions of the rooms and games).

## Paths
- CODE = ${CODE} (asmdef SomniumRecRoom, namespace SomniumSpace.Worlds.SomniumRecRoom). CODE/README.md is the handover
  ("Where it stands" + one section per place/game).
- DN = ${DN}: design/ARCHITECTURE.md (contract; read section 1 "Hard rules" first), research/*.md (big files: grep the
  headings, read only the sections you need), tools/ (copies of the tools' sources and READMEs).
- SP = ${SP} (bash: ${SPB}): the working tools - compilecheck/, harness/ (scenarios/, out/), unityharness/RRHarness (the
  harness's own Unity project copy; its test code is in ${HP}), rrtool/ (bundle research tool, index in rrtool/idx/,
  README at DN/tools/rrtool/README.md), research/ (raw dumps, wiki JSON, dorm/wiki/steamnews.json = all Steam news).
- BASE = ${NSP}/baseline/SomniumRecRoom: untouched copy of CODE from before this pass (diff -ru BASE CODE).
- Baseline harness output from the first build: ${NSP}/harness_baseline_out/<scenario>/*.png (compare against it).
- CLIENT = ${CLIENT}: the Somnium client's source (read-only reference; the research docs cite it as CL/CLIENT).
- Sibling Dreamports (read-only): D:/Somnium Quake, D:/Dreamport/Age Of Mythology, D:/Dreamport/Euro Truck Simulator/Euro Truck Sim.
- Rec Room install: E:/SteamLibrary/steamapps/common/RecRoom. Its level/sharedassets/resources files are encrypted: never
  try to read or decrypt them. Use the Addressables bundles only (through rrtool or the runtime's RrAssets).

## Rules
- The Unity project is open in the user's editor: add/edit files only, never run Unity on it. The harness uses its own copy.
- ARCHITECTURE.md section 1 applies (C# 9, whitelisted references only, no DontDestroyOnLoad, statics cleared in
  OnDestroy, keys the client owns, VR frame budget, RrLog).
- The Somnium avatar/player is never modified: no components added, nothing parented under its transforms, no changes to
  its renderers/materials/animator/IK. READING its transforms (bones, anchors, IK targets) is fine. The only approved
  exception belongs to the isolation task (other players, local, reversible).
- Edit ONLY the files your task owns (listed in your task). Anything else goes into "requests" in your result.
- Compile: "bash ${SPB}/compilecheck/build.sh project --editor" must report 0 error(s) in all three builds and
  "all N Fusion/Somnium members are proven". An UNPROVEN member means the upload server may reject the world: do not
  use it. Errors in files you do not own come from another agent mid-edit: wait 2 minutes and retry.
- New .cs files need a .meta: "bash ${SPB}/tools/metas.sh".
- Harness: "bash ${SPB}/harness/run.sh <scenario>" (give the Bash call timeout 600000; a lock serialises runs, so you
  may wait; exit 3 = a compile error somewhere: wait and retry). Step reference: DN/tools/harness/README.md, section
  "Scenario steps". LOOK at the PNGs you produce. The harness compiles with Unity's real compiler, which rejects a
  lambda local that shadows an outer local (CS0136).
- House style: XML doc comments that explain why, with measured facts, like the existing files. Log each key fact
  once with RrLog.Info("Area", "...").
- Keep command output small (head, grep, cut -c1-400). Don't print whole big files.

## Durability (usage limits have killed agents mid-task before)
Create your progress file DN/phase1/<key>.md in your first few minutes. Update it after every milestone: findings,
decisions, files touched, verification results. If it already exists when you start, you were restarted: read it and
continue from where it stops.

## Decisions the user has made (do not revisit)
1. Every room is built at the world origin; no more kilometre spacing. Only the local player's current room exists.
2. Players who are not in the same place as you are hidden locally, and their voices are muted.
3. "Latest" means the final, pre-shutdown version (Rec Room closed on 1 June 2026) of every room and game.

Return the structured result. Be factual, and say plainly what is untested.
`

const CODE_SCHEMA = {
  type: 'object',
  properties: {
    files_changed: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
    verification: { type: 'string' },
    untested: { type: 'array', items: { type: 'string' } },
    requests: { type: 'array', items: { type: 'string' } },
    readme_section: { type: 'string' },
  },
  required: ['files_changed', 'summary', 'verification', 'untested', 'requests', 'readme_section'],
}
const RESEARCH_SCHEMA = {
  type: 'object',
  properties: {
    doc: { type: 'string' },
    summary: { type: 'string' },
    confidence: { type: 'string' },
    open_questions: { type: 'array', items: { type: 'string' } },
  },
  required: ['doc', 'summary', 'confidence', 'open_questions'],
}
const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['ok', 'needs_fixes'] },
    issues: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          severity: { type: 'string', enum: ['critical', 'major', 'minor'] },
          file: { type: 'string' },
          line: { type: 'integer' },
          problem: { type: 'string' },
          scenario: { type: 'string' },
          fix: { type: 'string' },
        },
        required: ['severity', 'file', 'problem', 'scenario', 'fix'],
      },
    },
  },
  required: ['verdict', 'issues'],
}

// ---------------------------------------------------------------------------------------------------------------
const HANDS = `
# Your task: "hands" - a read-only model of the local avatar's DRAWN hands, and the hand-frame analysis
Key: hands. Progress file: DN/phase1/hands.md. Frame analysis: DN/phase1/hands_frames.md.
You own: new CODE/Player/RrAvatarHand.cs, new CODE/Player/RrAvatarHandTest.cs (harness hooks), new scenarios
SP/harness/scenarios/hands_*.json, and the two DN files above. Two tasks start after you and depend on you: "grab"
(fixes held-item orientation) and "watch" (puts the VR watch on the drawn wrist).

Why: in VR, RrHand.Grip (CODE/Player/RrHand.cs, RrXr.cs) is the CONTROLLER's OpenXR grip pose, measured under the
client's hand anchor. The avatar's hand is drawn elsewhere: the client solves the avatar's IK toward "LeftHandVRIKPoint"
/"RightHandVRIKPoint" (children of the anchors) with PlayerBodyHolder._ikPoints offsets (_leftHandPos about
(-0.033, 0.068, -0.036) plus _leftHandRot) - see DN/research/somnium_player.md sections 1.1-1.2 and 2. The watch uses the
grip with a guessed 10 cm offset (CODE/Watch/RrWatchModel.cs LeftOffset), so it floats off the drawn wrist.

1. From CLIENT source (prefabs under CLIENT/_SomniumSpace/Player/Prefabs/NEW; AvatarFeature.cs,
   XR/XRLocalAvatarFeature.cs, PlayerBodyHolder, the IK code), establish and write down with file:line evidence:
   where the local avatar model lives relative to the Bridge's Body.Root/LeftHand/RightHand (SomniumBridge
   PlayersContainer.LocalPlayer ... References.Body - see how RrPlayer.cs reaches them); whether it has an Animator with
   a humanoid Avatar (Animator.GetBoneTransform) and what bone names the rigs use; which IK drives the hands (VRIK?);
   the exact IK target poses and offsets; when the IK solves relative to our LateUpdate and to
   RenderPipelineManager.beginCameraRendering, and when the anchors are re-posed (Update and BeforeRender); whether the
   local avatar's hands/arms are drawn in first person in VR; how avatar scale is applied; what happens on avatar/outfit
   change (bones destroyed and recreated?).
2. Frame analysis (hands_frames.md): the anchor (aim pose or grip pose?), RrHand.Grip, RrHand.AimRotation
   (= Grip * Euler(90,0,0), the constant Quake and AoM proved on this client), the IK target / drawn hand bone, and
   REC ROOM's hand frame. Rec Room's hold poses are Tool.interactionSettings FullBody{Left,Right}HandGripTransform and
   {Left,Right}HandPickupTransform (read CODE/Interact/RrTools.cs ApplyTool and RrGrab.cs around line 200:
   RestRotation = Inverse(gripRotation), applied directly under RrHand.Grip). Establish Rec Room's hand-frame convention
   from bundle data with rrtool: several tools' grip transforms against their mesh axes - a bottle (its grip rotation is
   nearly identity with the bottle upright, +Y up), a paintball pistol/rifle (barrel axis), a sword, a paddle, a cup, the
   basketball, the charades pen - and Rec Room's own player hand prefabs if the bundles hold them. HYPOTHESIS to confirm
   or refute with evidence: Rec Room's hand frame is +Y up through the fist (thumb side) and +Z forward out of the front
   of the fist, i.e. our AimRotation frame, so applying Rec Room's poses directly in the Grip frame tips every held
   tool 90 degrees - which is exactly what the user reported ("they need to be rotated up 90 degrees; make them follow
   the same orientation as the hand"). Give the exact conversion (quaternion and position formula) the grab task should
   use, and the transform from RrHand.Grip to the drawn palm (so held objects can sit in the avatar's drawn hand).
3. Write CODE/Player/RrAvatarHand.cs with this public API (write the API block at the top of hands_frames.md as soon as
   it is fixed, before implementing, so the dependent tasks can rely on it):
     public enum RrAvatarHandSource { None, HumanoidBones, NamedBones, IkTarget, ControllerGuess }
     public static class RrAvatarHand {
       // the drawn wrist joint; rotation: +Z along the forearm toward the fingers, +Y out of the BACK of the hand,
       // +X completes a left-handed Unity frame; forearmLength in metres (for avatar scale)
       public static bool TryGetWrist(RrHandSide side, out Pose wrist, out float forearmLength);
       // centre of the drawn closed fist, axes in RrHand.Grip's convention (document it)
       public static bool TryGetPalm(RrHandSide side, out Pose palm);
       public static RrAvatarHandSource Source(RrHandSide side);
       public static void Refresh();   // re-find the bones; cheap
     }
   Compute rig-independently from bone POSITIONS where possible (hand, lower arm, index/middle/little proximal: palm
   normal from the knuckle line, finger direction hand->middle proximal), because bone axis conventions differ per rig.
   Fallback chain: humanoid Animator bones -> name search for common rigs (mixamorig:LeftHand, LeftHand, hand_l,
   Left_Hand, ...) under the local avatar -> the IK target plus the client's offsets -> the controller grip with a
   documented offset. Read the poses at the right moment for rendering (after the IK; justify it). Never add components
   or children to the avatar; cache transforms and re-find when they are destroyed. Cost under 0.05 ms per frame. Log
   the active source once per change. Null-safe in the editor (Bridge getters are null). Clear statics on shutdown (find
   how the other Player/ statics are shut down from RrMain/RrPlayer and request the one-line hook if needed).
4. Test in the harness: in RrAvatarHandTest.cs build a synthetic arm (GameObjects named like humanoid bones, a fake hand
   anchor with a VRIK point) and, if the harness project contains a humanoid model with an Avatar (look in
   SP/unityharness/RRHarness/Assets: XRI_Examples, SomniumSpaceTemplates, Animation), a real humanoid Animator. Feed it
   to RrAvatarHand through a test override (keep the override internal/test-only), and check the computed wrist and palm
   frames numerically: log "HandsTest: PASS ..."/"HandsTest: FAIL ..." lines, expect_log them in hands_frames.json.
   Also add a public test hook that the watch task can reuse to build the synthetic arm at a given pose
   (e.g. RrAvatarHandTest.BuildTestArm(...) plus a zero-argument IEnumerator variant for scenarios).
Return: files, the frame analysis summary (the conversion formula in full), the API, verification, untested.
`

const PLACE = `
# Your task: "place" - every room at the world origin; only the current room exists
Key: place. Progress file: DN/phase1/place.md.
You own: CODE/Rooms/*, CODE/Net/* except Net/RrNetBody.cs, CODE/Core/*, CODE/Player/RrPlayer.cs, the harness's
${HP}/RrScenarioRun.cs, and every scenario in SP/harness/scenarios (convert them; add new ones). In CODE/Places/* and
CODE/Games/* you may make small, local, ORIGIN-RELATED edits only (the "grab" task may make hold-pose edits in the same
folders: before editing a file there, check the "files touched" list in DN/phase1/grab.md, keep edits minimal, never
rewrite a whole file). Another task ("isolate") handles hiding other players; don't do that part.

Why: CODE/Rooms/RrPlacement.cs builds each dorm at x = -1125 + (s%10)*250, z = -2000 - (s/10)*250 and each shared
instance at x = -5000 + (k%6)*2000, z = 2000 + (k/6)*2000 (up to about 9 km out). There a float32 can only step by
0.5-1 mm, which jitters in VR ("everything is wobbly"). The user wants rooms unloaded and people hidden instead.

1. RrPlacement: every room (every dorm slot, every instance object, offline or online) at the world origin (0,0,0).
   Rewrite the doc comments (the old spacing rationale is now wrong; state the precision numbers). Keep what callers need.
2. Only the current room exists. Confirm RrTravel destroys the old room completely before the new one is built,
   including anything a room or game spawned OUTSIDE its root (effects, pooled or looping audio, projectiles, net bodies,
   enemies, HUD/world panels, music, test objects) and room-tied statics. Audit Places/ and Games/ for objects parented
   to RrMain.WorldRoot or the scene root instead of the room, and fix them (origin/lifetime edits are yours). Free
   memory on leaving: at least Resources.UnloadUnusedAssets during the loading fade; if RrAssets can tell which bundles
   only the old room used (not the new room, not shared systems such as the watch, sounds, player, fonts), unload those
   (AssetBundle.Unload) - be conservative, a wrong unload is worse than none. Log memory before/after
   (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong).
3. The arrival room (the scene's "Arrival" slab at the origin, top at y = 0, plus whatever RrTravel/Rooms dress it with)
   now overlaps every room: hide its renderers and disable its colliders while a room is built; show them again when the
   player is back in the arrival room. These are our own scene objects.
4. Rescue: the old "away from my room" watchdog (RrTravel around line 921: !RrPlacement.Inside(origin, feet, 100f))
   detects nothing now. Replace it: when the client respawns the player (it puts them at the scene spawn (0, 0.9, 0)
   after a fall below RespawnHeightY -100, or when they press Respawn) or they fall out of a room (feet more than 10 m
   below the room's lowest floor), teleport them to the room's spawn. Do not fight legitimate moves: our own
   RrPlayer.Teleport calls, quest act moves, the dorm ladder, Paintball's River (room z +450) and Drive-In (z -450), the
   quests' acts - some room areas are hundreds of metres from room (0,0,0).
5. Anything that derived instance membership, keys or origins from positions or the old spacing: RrNetActors (origin),
   RrNetSelfTest, presence/receive gates, RrTravel's instance lookup, every RrPlacement.Inside user, room-local mm
   conversions in games, hit volumes, remote held items, tags. Membership must come from presence (activity + instance
   number), never from where someone stands.
6. Harness: add an optional "frame": "room" to shot steps in RrScenarioRun.cs (pos/look relative to
   RrTravel.CurrentRoom's Origin; the default stays world), then convert every existing scenario's absolute shot
   coordinates to room frame by subtracting that room's OLD origin (offline: dorm slot 0 = (-1125, 0, -2000); activity a
   used instance object k = a - 1 with x = -5000 + (k%6)*2000, z = 2000 + (k/6)*2000 - e.g. Rec Center k = 1 ->
   (-3000, 0, 2000), Paintball k = 12 -> (-5000, 0, 6000); check the RrActivity ids in Rooms/RrActivity.cs for each).
   Afterwards copy RrScenarioRun.cs to DN/tools/harness/RrHarnessTests/ and the scenarios to DN/tools/harness/scenarios/.
7. Verify: compile; harness runs of a new travel_origin.json (Dorm -> Rec Center -> Paintball -> back to Dorm; per stop
   log the room origin, the player's feet, the GameObject count under RrMain.WorldRoot and the scene root count to prove
   the old room is gone, and memory), then rooms_travel, dorm_room, reccenter_tour, paintball_all, charades_play,
   dodgeball_gym, paddleball_court, goldentrophy_run. Compare the PNGs with the baseline ones: they must show the same
   rooms. At most 10 harness runs.
Return: files, what changed, verification (scenarios, step results, what the PNGs show), untested, a README section.
`

const ISOLATE = `
# Your task: "isolate" - hide players who are not in the same place as you (locally), mute their voices
Key: isolate. Progress file: DN/phase1/isolate.md (include the list of client objects you affect, with CLIENT file:line
evidence).
You own: CODE/Player/RrRemotePlayers.cs, new CODE/Player/RrIsolation.cs, new CODE/Player/RrIsolationTest.cs, new
scenarios SP/harness/scenarios/isolation_*.json. Hooking your system into boot/shutdown needs a line in RrMain or
RrPlayer ("place" owns those): put it in requests, AND make RrIsolation start itself lazily if nobody calls it (for
example from RrRemotePlayers' existing update path) so it works either way.

Why: all rooms now share the world origin (the "place" task), so a player in Paintball stands, invisibly to the game, in
the same spot as you in your Dorm. The user asked to hide people who are not in the same place and mute them. This is the
ONE approved exception to "never touch the avatar": on OTHER players' objects only, locally, always restored exactly,
you may switch Renderer.enabled, Canvas/CanvasGroup visibility of their name tag, their voice AudioSource's mute, and
Collider.enabled. Never the local player's avatar, never add/destroy components, never change materials. The Quake
Dreamport did the renderer part and shipped: D:/Somnium Quake/Assets/#User/SomniumQuake/Q3Remote.cs lines 286-335
(walks remote.Player.References.Body.Root, disables renderers, remembers them, re-enables later).

1. "With me": the same activity and the same instance number in presence (RrNet.Presence(id) - see CODE/Net/RrNet.cs,
   RrPresenceWire.cs, and how CODE/Watch/RrWatchWorld.cs decides WithMe). The Dorm is private: nobody else is with you
   there. In the arrival room (activity None) arrivals are together. Unknown presence, or status Loading: hidden. Use a
   little hysteresis so people don't flicker during travel.
2. From CLIENT source, find the full set of objects that represent a REMOTE player (the remote/"Simulator" body
   prefabs, AvatarFeature*Simulator, PlayerTagView / IPlayerTagView, the voice speaker, held client items, VFX,
   friend indicators) and where they live (under Body.Root or elsewhere). Then, for every remote player not with you:
   (a) avatar: disable their enabled Renderers, remember exactly which, re-enable exactly those; every ~0.5 s and on
       avatar change catch renderers the client re-enabled or created (LOD switches, avatar reloads, outfit changes);
   (b) name tag: find how the tag is drawn and hide it (a proven Bridge route if one exists - e.g. a world-level
       SomniumPlayerTagRules component; ISomniumPlayerInfo.SetPlayerTagDisableState is UNPROVEN, do not call it
       directly; otherwise its Canvas/CanvasGroup/renderers, restored later);
   (c) voice: CLIENT/_SomniumSpace/Network/Scripts/Voice/RemoteVoiceSpeaker.cs and VoiceSpeaker.cs - find what drives
       the remote voice AudioSource each frame (volume, maxDistance, mute, spatial blend) and mute it in a way the client
       does not override (AudioSource.mute if nothing resets it; verify in the source); restore when together;
   (d) colliders: find whether a remote avatar carries colliders (hand boxes on layer 7, body capsule, the layer-15
       trigger) that could touch the local player or our props, projectiles and raycasts. Prefer fixing it on OUR side
       (our layer masks, excludeLayers); only if unavoidable disable their colliders locally and restore;
   (e) our own objects drawn for other players (hit volumes, held items, team markers, name/score tags, remote gear) must
       exist only while the player is with you: grep for SameInstance / MembersOfMyInstance / RrRemotePlayers users across
       CODE/Games and CODE/Places and list gaps in requests (or fix them if in your files).
3. API: RrIsolation.IsWithMe(int playerId), event RrIsolation.OnVisibilityChanged(int playerId, bool visible), a test
   override. Log once per change: "Isolation: player N (nick) hidden: in Paintball #2 (you: Dorm)" / "... shown".
   Cost: up to 50 remote players; spread scans over frames. Null-safe in the editor. Clear statics on shutdown and
   restore everything you hid when the world unloads.
4. Test offline (the harness has no remote players): RrIsolationTest.cs builds a fake remote (a hierarchy with
   renderers, a world-space canvas tag, an AudioSource, colliders), registers it through a test path, flips presence
   with an override, and checks hide and exact restore numerically ("IsolationTest: PASS/FAIL ..." lines, expect_log
   them in isolation_basic.json). Mention what only a two-client test in the real client can prove.
Return: files, the exact set of client objects affected (with evidence), verification, untested, requests, a README section.
`

const GRAB = (hands) => `
# Your task: "grab" - held items follow the hand's orientation
Key: grab. Progress file: DN/phase1/grab.md (keep a "files touched" list at the top - the "place" task checks it).
You own: CODE/Interact/*, CODE/Player/RrHand.cs, CODE/Player/RrInput.cs, CODE/Player/RrScreenCamera.cs,
CODE/Net/RrNetBody.cs (the held-pose parts), new scenarios SP/harness/scenarios/grab_*.json. In CODE/Games/* and
CODE/Places/* you may make small HOLD-POSE-RELATED edits only (weapons, paddles, pen, cue, flags, swords, bows...) -
before editing a file there, check DN/phase1/place.md's files list; keep edits minimal; never rewrite a whole file.

The user's report (VR): "Grabbing items are slightly off, it feels like they need to be rotated up 90 degrees, make them
follow the same orientation as the hand."

The "hands" task has analysed the frames and written CODE/Player/RrAvatarHand.cs. Read DN/phase1/hands_frames.md
first (and DN/phase1/hands.md). Its result summary:
${hands ? JSON.stringify(hands).slice(0, 6000) : '(the hands task did not return a result - read its DN files; if the analysis is missing, do it yourself: see RrGrab.cs ~line 200, RrTools.cs ApplyTool, RrHand.cs GripToAim, and Rec Room tool grip transforms via rrtool)'}

1. Fix the hold pose in RrGrab (and wherever else a hold pose is computed): Rec Room's poses are applied in Rec Room's
   hand frame, converted to RrHand.Grip with the formula from hands_frames.md. Objects without a Rec Room pose: in VR
   held where grabbed; in screen mode shown the way Rec Room's screen mode shows them (check Tool.screenSettings /
   ScreenBehaviorConfig data) - upright to the view, lower right. If the analysis shows the drawn palm sits noticeably
   away from the controller grip, place held objects in the drawn palm (RrAvatarHand.TryGetPalm relative to the Grip,
   sampled so it does not jitter) while throw velocity still comes from the controller.
2. Audit every path that depends on the hold frame and keep them consistent: pickup and slide-in, drinking and eating
   (PoseAtMouth), two-handed items, LockOnPickup, release and throw, the screen-mode hand (RrHand's screen grip = the
   view turned back by GripToAim), remote display of held props (RrNetBody.LocalGrab sends RestPosition/RestRotation
   relative to our measured grip; the receiver attaches to the REMOTE player's Bridge hand = their anchor, which is
   not our measured grip - make the sent pose reproducible on the receiver), weapons (Games/Shared/RrWeapon.cs,
   RrProjectile.cs: shots must leave the muzzle and go where the hand/gun points), the paddleball paddle's face box, the
   charades pen's ExtrusionPoint, the Rec Center pool cue and ping-pong paddle, quest swords/bows/crossbows, paintball
   guns and flags, dodgeballs.
3. Verify: add a test path that forces RrHand into VR mode with a given Grip pose (keep it test-only), and a scenario
   grab_pose.json that holds a bottle, a paintball pistol, the basketball and a paddle in a synthetic upright fist
   pointing forward, logging each held object's world axes against expectations ("GrabTest: PASS/FAIL bottle up
   3 deg from world up", gun barrel vs hand forward) plus screenshots. Screen mode too (the editor fallback rig): hold a
   bottle and a gun, screenshot from the player camera, and check it looks like Rec Room's screen mode. Then re-run
   interact, dorm_room, paintball_all, paddleball_court and charades_play for regressions. At most 8 harness runs.
Return: files, the root cause and the fix, verification, untested (say what only a headset can confirm), a README section.
`

const WATCH = (hands) => `
# Your task: "watch" - the VR watch sits on the avatar's drawn wrist and follows it
Key: watch. Progress file: DN/phase1/watch.md.
You own: CODE/Watch/*, new scenarios SP/harness/scenarios/watch_wrist*.json.

The user's report (VR): "The watch is slightly off, make that follow the hand better, it is floating off the avatar."
Today CODE/Watch/RrWatchModel.cs places the watch from RrHand.Grip (the CONTROLLER pose) with a guessed offset
(LeftOffset (-0.015, 0, -0.1)); the client draws the avatar's hand elsewhere (IK toward LeftHandVRIKPoint plus its own
offsets). The "hands" task wrote CODE/Player/RrAvatarHand.cs: TryGetWrist gives the drawn wrist joint (+Z along the
forearm toward the fingers, +Y out of the back of the hand) and the forearm length. Read DN/phase1/hands_frames.md and
DN/phase1/hands.md first. Its result summary:
${hands ? JSON.stringify(hands).slice(0, 5000) : '(the hands task did not return a result - read its DN files and use whatever API exists in CODE/Player/RrAvatarHand.cs; if it is missing, request it and fall back to the grip)'}

1. Place the watch on the drawn wrist every frame, just before rendering and after the IK (reuse RrAvatarHand's timing):
   on the forearm just behind the wrist joint, the face on the back of the wrist, the strap round the forearm, lifted
   enough not to sink into a sleeve, scaled with the forearm length (avatar scale; clamp it). Match Rec Room's own
   placement where it applies: DN/research/rr_watch_ui.md sections 2.1 and 3.5 (Rec Room's watch on its wrist:
   p (0.02, 0, -0.119), r (-0.5, 0.5, -0.5, 0.5) relative to its hand pivot; the face 4 cm above the wrist axis; the face
   looks out of the back of the hand; 3 o'clock toward the fingers). Keep the fallback chain (IK target, then the grip
   with the old offset) and log which source is used.
2. Everything built on the wrist pose must use the same frame: the look-at-your-watch gesture (dwell, pitch/roll
   limits), the menu panel that opens above the wrist and follows it, the other hand's tap/poke, right-wrist option.
3. Verify in the harness with the synthetic arm from the hands task's test hooks (see RrAvatarHandTest): a scenario
   watch_wrist.json that builds the arm in 2-3 poses, lets the watch place itself, logs PASS/FAIL checks (the watch
   centre within 1.5 cm of where it should be on the forearm, the face normal within 10 deg of the back-of-hand
   normal), and takes close screenshots from 3 angles. Re-run watch_menu.json for regressions. At most 6 harness runs.
Return: files, the fix, verification (what the PNGs show), untested (what only a headset can confirm), a README section.
`

const RPLACES = `
# Your task: "rplaces" - research the FINAL versions of the Dorm Room and the Rec Center
Key: rplaces. Progress file: DN/phase1/rplaces.md. Deliverable: DN/research/rr_latest_places.md (create it early, fill it
section by section). Research only: do not edit CODE.

The user: "You're using the oldest versions of everything, please use the latest, such as rec center, dorm room etc."
What was built: the Dorm is the 27 Apr 2018 "Spring Cleaning" dorm rebuilt from Rec Room's Maker Pen Dorm kit
(CODE/Places/Dorm, README "Dorm Room" section, DN/research/rr_dorm_core.md). The Rec Center is our OWN invented layout
(40 x 36 x 8 m hall + lobby, DN/research/rr_reccenter.md section 3), not any real version. The real rooms' geometry is in
Rec Room's encrypted level files, which we never read, so the final versions must be rebuilt from references plus
whatever the plain bundles contain.

1. What were the final (2025 - 1 June 2026) Dorm Room and Rec Center? Layout, size, colours, materials, windows,
   lighting, doors and signs, furniture, interactive objects, sounds, what changed and when (e.g. the Sep 2020 Rec Center
   redesign "new layout, fresh paint, nice big windows", the 2021 Friend-O-Tron, the 2023 Room-i-verse door and Play board,
   the Sep 2025 return of the Rec Center door, any later dorm redesign). Sources: SP/research/dorm/wiki/*.json (fandom
   pages already fetched: Dorm_Room, Rec_Center, Custom_Dorms, Dorm_Skins, Dorm_Mirror...), SP/research/dorm/wiki/
   steamnews.json (every Steam news item up to 2026 - search it for dorm / Rec Center / hub / lobby changes), the fandom
   wiki through its MediaWiki API with WebFetch (action=parse&prop=wikitext, page revisions, gallery captions, image
   descriptions), web search (Reddit, patch notes, video titles and descriptions of Rec Center tours), the Wayback
   Machine for recroom.com and rec.net pages. Read pages as text. Do not download binary files.
2. Rec Room's own imagery in the bundles may show the final rooms: the watch illustrations (illustration_HomeButton_
   RecCenter, illustration_HomeButton_DormRoom, Illustration_Menu_Dorm, Illustration_Menu_RecCenter), the campus map
   poster, loading-screen art, room-card art, any texture whose name mentions RecCenter or Dorm. Export them and LOOK at
   them: write a harness diag file ${HP}/RrExport.cs (a static method that loads named Texture2D/Sprite assets through
   RrAssets and writes PNGs through a RenderTexture blit + ReadPixels to SP/harness/out/export/), a scenario
   SP/harness/scenarios/export_art.json that calls it ("call" steps take zero-argument static methods; hard-code the
   list or read it from a text file), run it ("bash ${SPB}/harness/run.sh export_art", timeout 600000, at most 4 runs),
   then Read the PNGs. Use rrtool's index (SP/rrtool/idx/objects.tsv: class Texture2D/Sprite) to find names.
3. Bundle evidence: search SP/rrtool/idx (objects.tsv, containers.tsv, mbscripts.tsv) for RecCenter*, Dorm*, hub,
   lobby, window, and the newer furniture and materials; say which pieces of the final rooms exist as prefabs, meshes,
   materials, sounds (with bundle and key/pathId, as the other research docs do).
4. Write, for each of the Dorm and the Rec Center: (a) what the final version is, dated, with sources; (b) exactly what
   differs from what was built; (c) a complete rebuild plan in the form of rr_reccenter.md section 3 (frame, shell with
   dimensions and bundle materials, every door and prop with prefab name + key + position + yaw, lighting, audio,
   interactions), every GUESS marked; (d) the exported PNGs (paths) and what each shows. Be honest about confidence:
   where sources are thin, say so and give the best-supported reconstruction.
Return: doc path, summary, confidence, open questions for the user (for example screenshots they could provide).
`

const RGAMES = (group, list) => `
# Your task: "${group}" - research the FINAL versions of these Rec Room activities: ${list}
Key: ${group}. Progress file: DN/phase1/${group}.md. Deliverable: DN/research/rr_latest_${group}.md (create it early and
fill it activity by activity). Research only: do not edit CODE.

The user wants the final, pre-shutdown (Rec Room closed on 1 June 2026) version of every game, not the oldest. Existing
research (read the relevant sections only; do not repeat them - reference them and add or correct): DN/research/
rr_shooters.md, rr_sports.md, rr_party.md, rr_quests.md, rr_watch_ui.md (activity art). Built so far (see CODE/README.md
sections): Paintball (River + Sunset Drive-In, CTF + Team Battle), 3D Charades (Ink Space), Dodgeball, Paddleball, Quest for
the Golden Trophy. Everything else is still to build.

For each activity in your list:
1. The final version: maps and their rotation, modes, team sizes, rules, timings and scores, weapons/items, UI
   (scoreboards, HUD), announcer lines, music, notable remasters or reworks and their dates (e.g. quest remasters, new
   Paintball maps, Laser Tag/Showdown/Rec Royale season changes), and anything removed before the shutdown. Sources:
   SP/research/dorm/wiki/steamnews.json (every Steam news item up to 2026 - search it per activity), the fandom wiki via
   its MediaWiki API with WebFetch (action=parse&prop=wikitext), web search (patch notes, Reddit, video titles and
   descriptions). Read as text. Do not download binary files.
2. What the plain bundles hold for that final version (prefabs, maps' kit pieces, configs, sounds, music, VO, materials):
   search SP/rrtool/idx (objects.tsv, containers.tsv, mbscripts.tsv) and use rrtool (README at DN/tools/rrtool/README.md)
   for configs; give bundle + key/pathId like the other docs.
3. For built games: exactly what the first build has that is outdated compared with the final version, and what to change.
   For unbuilt games: the final-version facts the builders need, plus corrections to the existing research.
Write one section per activity with a "Changes vs existing research / build" list at its end, and mark every GUESS.
Return: doc path, summary, confidence, open questions.
`

// ---------------------------------------------------------------------------------------------------------------
const OWNED = {
  place: 'CODE/Rooms/*, CODE/Net/* except RrNetBody.cs, CODE/Core/*, CODE/Player/RrPlayer.cs, origin-related edits in Places/Games, the harness RrScenarioRun.cs and scenarios',
  isolate: 'CODE/Player/RrRemotePlayers.cs, CODE/Player/RrIsolation.cs, CODE/Player/RrIsolationTest.cs, isolation scenarios',
  grab: 'CODE/Interact/*, CODE/Player/RrHand.cs, RrInput.cs, RrScreenCamera.cs, CODE/Net/RrNetBody.cs, hold-pose edits in Games/Places, grab scenarios (plus CODE/Player/RrAvatarHand*.cs written by the hands task, which you may fix)',
  watch: 'CODE/Watch/*, watch_wrist scenarios (plus CODE/Player/RrAvatarHand*.cs from the hands task, which you may fix only if the watch needs it)',
}
const ISSUE = {
  place: 'everything jitters because rooms are built up to ~9 km from the origin; the user wants all rooms at the origin, only the current room loaded',
  isolate: 'with every room at the origin, players in other rooms must be hidden locally (avatar, name tag, voice, colliders) and restored exactly when together',
  grab: 'in VR held items sit ~90 degrees off; they must follow the orientation of the hand',
  watch: 'in VR the watch floats off the avatar; it must sit on and follow the drawn wrist',
}

function reviewPrompt(key, result) {
  return COMMON + `
# Your task: adversarial review of the "${key}" change
The change was meant to fix: ${ISSUE[key]}. The task's own spec and notes: DN/phase1/${key}.md. Files it owns: ${OWNED[key]}.
Its result: ${JSON.stringify(result).slice(0, 5000)}

Review it as a skeptic. Diff the files it touched against BASE (diff -ru on each file/folder; new files have no
baseline). Check, with concrete evidence:
1. Does it actually fix the user's issue, for desktop AND VR, offline AND with remote players? Trace the real code path.
2. Correctness bugs: null paths in the editor/client, lifetime (destroyed transforms, re-entry, statics), frame order
   (Update/LateUpdate/beforeCameraRendering), math (frames, handedness, left vs right hand), network consistency.
3. Hard-rule violations: touching the local avatar; touching other players beyond the approved local, reversible flags;
   DontDestroyOnLoad; unproven server members; client-owned keys; frame budget.
4. Regressions in callers of anything it changed (grep the whole CODE tree for users of changed members).
5. Run "bash ${SPB}/compilecheck/build.sh project --editor". Do not run the harness unless a claim needs it (at most 1 run).
Do not edit any file. Every issue needs file:line, a concrete failure scenario and a concrete fix. Report only real
problems; if it is sound, return verdict ok with an empty or minor-only list.
`
}

function fixPrompt(key, result, review) {
  return COMMON + `
# Your task: apply the review findings to the "${key}" change
Key: ${key} (continue its progress file DN/phase1/${key}.md; add a "Review fixes" section). You own the same files: ${OWNED[key]}.
The change was meant to fix: ${ISSUE[key]}.
Original result: ${JSON.stringify(result).slice(0, 4000)}
Review findings: ${JSON.stringify(review).slice(0, 8000)}

Fix every critical and major finding (and minor ones when cheap). If you are sure a finding is wrong, say why in the
progress file instead of changing code. Then compile, and re-run the harness scenarios that cover what you changed (at
most 4 runs). Return the full, updated structured result for the task (files, summary, verification, untested, requests,
readme_section) - not just the delta.
`
}

async function reviewAndFix(key, result) {
  if (!result) return { key, result: null, review: null }
  const review = await agent(reviewPrompt(key, result), { label: 'review:' + key, phase: 'Review', schema: REVIEW_SCHEMA })
  if (!review || review.verdict === 'ok' || !(review.issues || []).some(i => i.severity !== 'minor')) {
    return { key, result, review }
  }
  const fixed = await agent(fixPrompt(key, result, review), { label: 'fix:' + key, phase: 'Fix', schema: CODE_SCHEMA })
  return { key, result: fixed || result, review, fixed: !!fixed }
}

// ---------------------------------------------------------------------------------------------------------------
phase('Build')
log('Wave 1: hands model, placement, isolation, and three research agents in parallel; grab and watch follow the hands model.')

const researchP = Promise.all([
  agent(COMMON + RPLACES, { label: 'research:places', phase: 'Build', schema: RESEARCH_SCHEMA }),
  agent(COMMON + RGAMES('rgames_action', 'Paintball, Laser Tag, Rec Royale, Rec Room Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling'), { label: 'research:action', phase: 'Build', schema: RESEARCH_SCHEMA }),
  agent(COMMON + RGAMES('rgames_story', '3D Charades, Make It To Midnight, My Little Monsters, Punch Up, Quest for the Golden Trophy, The Curse of the Crimson Cauldron, Crescendo of the Blood Moon, Isle of Lost Skulls, The Rise of Jumbotron, Rec Rally, Stunt Runner'), { label: 'research:story', phase: 'Build', schema: RESEARCH_SCHEMA }),
])

const handsP = agent(COMMON + HANDS, { label: 'hands', phase: 'Build', schema: CODE_SCHEMA })
const placeP = agent(COMMON + PLACE, { label: 'place', phase: 'Build', schema: CODE_SCHEMA }).then(r => reviewAndFix('place', r))
const isolateP = agent(COMMON + ISOLATE, { label: 'isolate', phase: 'Build', schema: CODE_SCHEMA }).then(r => reviewAndFix('isolate', r))
const grabP = handsP.then(h => agent(COMMON + GRAB(h), { label: 'grab', phase: 'Build', schema: CODE_SCHEMA })).then(r => reviewAndFix('grab', r))
const watchP = handsP.then(h => agent(COMMON + WATCH(h), { label: 'watch', phase: 'Build', schema: CODE_SCHEMA })).then(r => reviewAndFix('watch', r))

const [hands, place, isolate, grab, watch] = await Promise.all([handsP, placeP, isolateP, grabP, watchP])
const code = { hands, place, isolate, grab, watch }
log('Code tasks done: ' + Object.entries(code).map(([k, v]) => k + (v && (v.result || v.files_changed) ? ' ok' : ' MISSING')).join(', '))

phase('Integrate')
const summaries = JSON.stringify({
  hands: hands,
  place: place && place.result, isolate: isolate && isolate.result, grab: grab && grab.result, watch: watch && watch.result,
  requests: [place, isolate, grab, watch].filter(Boolean).map(x => ({ key: x.key, requests: x.result ? x.result.requests : [] })),
}).slice(0, 30000)

const integration = await agent(COMMON + `
# Your task: "integrate" - make the four changes work together, run the whole harness suite, update the README
Key: integrate. Progress file: DN/phase1/integrate.md. You are now the ONLY writer: you may edit any file in CODE, the
harness project and the scenarios.
The four code tasks (hands model, one-origin placement, isolation, grab orientation, watch on the wrist) have finished;
their notes are in DN/phase1/*.md. Their results and open requests:
${summaries}

1. Apply the open requests that are still needed (e.g. boot/shutdown hooks for RrIsolation and RrAvatarHand in
   RrMain/RrPlayer), resolve any conflicts between the tasks' edits, and make sure nothing half-finished is left.
2. Compile ("bash ${SPB}/compilecheck/build.sh project --editor": 0 errors in all three builds, guard proven).
3. Run the full suite (timeout 600000 per run): smoke, world_scene, interact, player, dorm_room, reccenter_tour,
   rooms_travel, travel_origin, paintball_all, charades_play, charades_lightning, dodgeball_gym, paddleball_court,
   goldentrophy_run, goldentrophy_acts, goldentrophy_final, watch_menu, watch_wrist, grab_pose, hands_frames,
   isolation_basic (skip names that don't exist; list them). For each: step failures, and LOOK at a sample of PNGs
   against ${NSP}/harness_baseline_out/<scenario>/. Fix regressions you find (re-run what you fix). Known before this
   pass: dorm_room's "DormTest: basket: SCORED" expectation already failed in the baseline.
4. Update CODE/README.md in its house style: the "Where it stands" table (new rows or changed states for Player/hands,
   grab, watch, travel/placement, isolation) and ONE new dated section "Foundation pass: one origin, isolation, hands and
   watch (2026-09-24)" built from the tasks' readme_section texts (what changed, why, Rec Room/client values used, log
   lines to look for, harness scenarios and results, untested). Fix any README statement the pass made false (e.g. the
   placement numbers, "Untested" items now covered). Copy the harness project's test files (${HP}/*.cs) and all
   scenarios into DN/tools/harness/ (RrHarnessTests/ and scenarios/).
5. Write DN/phase1/INTEGRATION.md: per task what changed, the suite results table (scenario, steps ok/failed, notes), what
   still needs a headset or two real clients to confirm, and anything left open.
Return: files, summary, verification (the suite table), untested, requests (things for the user), readme_section (empty).
`, { label: 'integrate', phase: 'Integrate', schema: CODE_SCHEMA })

const research = await researchP
return { code, integration, research }
