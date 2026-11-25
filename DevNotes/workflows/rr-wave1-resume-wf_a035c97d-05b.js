export const meta = {
  name: 'rr-wave1-resume',
  description: 'Resume the interrupted foundation modules (Net, Player, Interact, Rooms, Watch, Games shared) from their partial files',
  phases: [{ title: 'Resume' }],
}
const SP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\\scratchpad'
const SPU = SP.replace(/\\/g, '/')
const CODE = 'D:\\Dreamport\\Rec Room\\Rec Room\\Assets\\#User\\SomniumRecRoom'

const COMMON = `
# Dreamport: Rec Room - RESUME of an interrupted module build
Rec Room played inside a Somnium Space world (Unity 6000.3 URP, desktop and VR). Code: ${CODE} (asmdef SomniumRecRoom). The
Unity project is open in the user's editor: add/edit files only; never run Unity on it.

A previous run of THIS task was stopped by a usage limit after writing part of the module. Your module folder already holds
that work. START by listing and reading your own module's files (they are the truth), compare with the task below, and FINISH
what is missing. Do not rewrite finished files; fix them only where they are wrong or incomplete. Remove the "SKELETON"
markers from files you complete.

Contract and context: ${SP}\\design\\ARCHITECTURE.md (read §1-§7, §11). Research docs are large: do NOT read them whole -
grep their headings ("grep -n '^#' file") and read only the sections you need (Read with offset/limit).

TOKEN ECONOMY (important - a usage limit stopped the last run): keep command output small (head, cut, grep -c), do not print
whole large files, do not re-read files you already read, at most 2 harness runs, concise final result.

Rules (several module agents run in parallel, each owning its folder):
- Edit ONLY your module's files. Keep existing public members (add freely). If another module lacks something you need, add
  a small adapter in your folder or a TODO, and list it under requests. A clear, blocking bug elsewhere may be fixed
  minimally - report exactly what.
- New files: run "bash ${SPU}/tools/metas.sh".
- Compile: "bash ${SPU}/compilecheck/build.sh project --editor" must end with 0 errors and the guard "all N ... proven".
  Errors in OTHER modules' files are theirs (they are being finished right now): if only those remain, wait ~2 minutes and
  re-run; your own files must be clean.
- Runtime check (optional, max 2 runs): "bash ${SPU}/harness/run.sh <scenario.json>" (see ${SP}\\harness\\README.md); it fails
  with exit 3 if any module does not compile - then skip it and say so.
- Offline/editor mode must work (no network, Bridge null). Log with RrLog once per fact. Never touch the Somnium avatar.
Return a structured result: files, public API, what was verified and how, open issues, requests to other modules.
`
const SCHEMA = { type: 'object', properties: {
  files: { type: 'array', items: { type: 'string' } }, api: { type: 'string' }, verified: { type: 'string' },
  issues: { type: 'array', items: { type: 'string' } }, requests: { type: 'array', items: { type: 'string' } },
}, required: ['files', 'api', 'verified', 'issues', 'requests'] }

const TASKS = [
 { key: 'net', prompt: `
## Module: Net/ (all Net/*.cs; SceneNetworking.cs only for bug fixes)
Design: ${SP}\\research\\somnium_networking.md §5 (read §0, §3.1-3.4, §4.1-4.9, §5 only). Already written by the previous run: the
pure wire layer (RrWire, RrSlotClaim, RrPresenceWire, RrInstanceWire, RrEventWire, RrPropWire, RrActorWire, RrLease, RrTimeline).
Still missing - the runtime that connects them to the scene's Fusion objects ("Scene Networking", "Rr Net Events", "Rr Net
Player 0..49" NBD64, "Rr Net Instance 0..23" NBD64, "Rr Net Actors 0..11" NBD64, "Rr Net Prop 0..47" NBD12; hold as NetworkBridgeData):
1. RrNet facade + a driver MonoBehaviour (created by RrNet.Init under RrMain.WorldRoot): find objects by name (1 Hz until found),
   SceneNetworking events, Online/LocalId/Present/OnPlayersChanged, presence slot claim + writes (SetLocation/SetTeam/Presence,
   health/kills/deaths, custom per-player ints), SlotIndex.
2. Instances: RrNet.JoinInstance(activity, number, Action<int> done) / LeaveInstance() (number -1 = fullest joinable, capped by
   RrRooms.Get(a).MaxPlayers), InstanceObjectIndex, MembersOfMyInstance, election + liveness/takeover, IsInstanceAuthority,
   OnAuthorityChanged, RrInstanceState backed by the bound instance object (authority writes only; timers as remaining ms).
   Offline: bind locally with k = (int)activity - 1.
3. Events: batched Send/On/Off scoped to the instance, loop-back of own events, toAuthority via RPC_SendMessageToController on
   the instance object.
4. RrNetBody (props, §5.5) and RrNetActors (AI blocks, §5.8) runtime on top of RrPropWire/RrActorWire/RrLease/RrTimeline;
   remote held props at the holder's hand via Player/RrRemotePlayers (Remote.LeftHand/RightHand).
5. Offline test: a dotnet console project ${SP}\\nettest compiling the pure wire/claim/election/lease files plus a fake room
   (last authority request wins, writes without authority dropped, delays): 3 clients join, simultaneous claims, authority leaves
   mid-round, late joiner, instance binding race, prop grab race. Report results.` },
 { key: 'player', prompt: `
## Module: Player/ (all Player/*.cs)
Design: ${SP}\\research\\somnium_player.md (read its TL;DR and the sections you need). Already written: RrHand, RrInput, RrXr,
RrScreenCamera, RrCrosshair, RrClientTablet. Still missing / to check:
1. RrPlayer (still the skeleton): Init creates the driver ([DefaultExecutionOrder(10000)] LateUpdate) under RrMain.WorldRoot that
   updates hands/input/camera modules you wrote; Camera (EtsVr.Head logic), head/feet/yaw, InVr, InPlayMode (RrInput uses it),
   LocalId (Fusion PlayerId via SceneNetworking.NetworkRunnerRef.LocalPlayer when running; 0 offline), Nick, Teleport (Bridge
   DoTeleportToPoint; offline: move the fallback rig), SetMovement (proven Bridge members only), Shutdown cleanup.
2. RrRemotePlayers (skeleton): Bridge PlayersContainer AllPlayers at 1 Hz + on join/leave, id/nick/body transforms (proven members).
3. Editor/offline fallback rig when no Somnium client (Bridge PlayersContainer null) and no camera renders: first-person
   capsule (layer 0, ours) with WASD + right-mouse look, so the world can be walked in the editor/harness; never in the client.
4. Make sure everything the other files reference exists and compiles; one harness run proving boot + the fallback rig + the
   screen-mode hand.` },
 { key: 'interact', prompt: `
## Module: Interact/ (all Interact/*.cs)
Research: ${SP}\\research\\rr_dorm_core.md §4 (core interactions: Tool, RigidbodyPickup, CollisionAudio, BreakableObject,
DrinkableBottle/Potion/ShakeAudio, food DisposableItem, basketball/hoop, AnimateInOut, ToolCleanup - exact values).
Already written: RrGrab, RrGrabbable, RrHighlight, RrMouth, RrScreenThrow. Still missing / to check:
1. RrTools.MakeTool(RrInstance): reads the prefab's configs (Tool grips/throw/audio, highlight renderers) and adds the
   behaviours below when their Rec Room component is on the prefab. (If RrTools no longer exists after the rewrite, recreate it.)
2. Behaviours, each driven by the prefab's own config values and clip/effect references (RrSound.Play(RrClipInfo),
   RrFx.Spawn(RrTarget)): RrCollisionSound (CollisionAudio mappings, velocity->volume curve, InstanceLimitDuration), RrBreakable
   (shatterSpeed, whole/fractured swap, smash VFX/sounds, broken lifetime), RrDrinkable (DrinkableBottle + Potion + ShakeAudio:
   uncork on use, pour while tilted, drink near the mouth via RrMouth, swallow sounds at drinkInterval, empty after lifeSpan),
   RrEdible (food bites, stages, chew sounds), RrShakeSound, RrAnimateInOut (spawn/despawn pop), RrRespawnable (ToolCleanup),
   RrBall (bounce sounds by speed, rim/backboard events for scoring).
3. RrPressable: world button (desktop aim+use or client click via IPointerClickHandler on layer 16; VR poke or trigger-point),
   Rec Room button sounds; events OnPressed.
4. One harness run: spawn bottle/basketball/food, RrGrab.Give, exercise use/drink/throw/break via test hooks; PNGs.` },
 { key: 'rooms', prompt: `
## Module: Rooms/ (all Rooms/*.cs)
Research: ${SP}\\research\\rr_reccenter.md §4 (transition/loading look) and the ActivityDoor sections (grep "ActivityDoor").
Already written: RrTravel, RrTravelDriver, RrLoadingScreen, RrPlacement, RrCoverArt, RrArt, RrRoom, RrActivity, RrRegistry.
Still missing / to check:
1. RrTravel complete per ARCHITECTURE §6 (fade + loading card, leave/destroy old room, bind instance via RrNet.JoinInstance when
   the Net module provides it - use a guarded adapter otherwise, build with timeout, teleport, presence, OnLocalEnter, music,
   fade in; Begin(): arrival room until network ready or 15 s, then GoHome; dispatch RrNet.OnPlayersChanged/OnAuthorityChanged to
   CurrentRoom; fall-safety respawn).
2. RrPortal: Rec Room's activity door (the official [ActivityDoor] prefab per the research: clone it, set its sign text/colour,
   hover animation/glow, sounds), entered by walking in or pressing (Interact's RrPressable if present) -> RrTravel.GoTo(activity).
3. RegisterInfrastructure (the partial hook in RrRegistry.cs - implement it in a new Rooms file): register a placeholder Dorm
   room ONLY when RrRooms.Get(RrActivity.Dorm) is null (so travel works before the real dorm exists) and likewise a placeholder
   Rec Center.
4. One harness run: boot -> dorm (placeholder or real) -> goto RecCenter; shots of the loading card and a portal.` },
 { key: 'watch', prompt: `
## Module: Watch/ (all Watch/*.cs)
Research: ${SP}\\research\\rr_watch_ui.md (read §0 and the re-creation spec; grep for what you need).
Already written: RrUiKit, RrUiButton, RrUiGradient, RrWatchArt, RrWatchCard, RrWatchPage, RrWatchHomePage, RrWatchPlayPage,
RrWatchPeoplePage, RrWatchRoomPage, RrWatchThisRoomPage, RrWatchSounds, RrWatchWorld, RrWatchTest. Still missing:
RrWatchUi (referenced by RrWatchPage but absent - the menu controller/canvas/page stack) and RrWatch (still a 10-line stub:
Init/Shutdown, the VR wrist watch model + glance-to-open per the spec, desktop H key via RrInput.MenuDown, RrInput.SetBlocked
while open, open/close animations and sounds). Wire every page into the controller, make it compile, and do one harness run
opening the menu on desktop (and the VR layout via a test switch) with screenshots; compare to the spec.` },
 { key: 'gameskit', prompt: `
## Module: Games/Shared/ (all Games/Shared/*.cs; RrGameRoom/RrTeams/RrAnnouncer/RrScoreboard/RrGameEvents keep their API)
Research (grep, read only needed parts): ${SP}\\research\\rr_shooters.md (weapons, projectiles, damage, VFX), rr_quests.md §2-3
(enemies, melee, revive, framework), somnium_networking.md §5.7 (Fire/Hit/Death events).
Already written: RrCombat, RrCombatDriver, RrHitVolume(s), RrImpacts, RrPrefabRefs, RrPresenceStats, RrProjectile, RrShotSpec,
RrWeaponSpec. Still missing (RrHitVolume.cs and RrProjectile.cs reference RrEnemy, which does not exist yet):
1. RrEnemy (quest AI base): spawn a Rec Room enemy prefab (skinned rig where the research found one, else the posed spawner
   mesh), health from the spawner config, idle/chase/attack/hurt/die with Rec Room sounds/VFX/coins, steering + obstacle rays,
   authority-simulated (RrNetActors when Net provides it; else local with a TODO).
2. RrHealth (local player HP, hit flash desktop / vignette VR without touching the camera, out + respawn at team spawn via
   RrPlayer.Teleport, stats to presence), RrWeapon (held ranged weapon: Rec Room gun prefab + RrWeaponSpec, RrGrabbable
   use -> RrProjectile, Fire events, reload, sounds, muzzle VFX; remote Fire -> visual projectiles), RrMelee (swing by hand
   velocity, MeleeWeapon damage, hits RrEnemy/players), RrLoadout (give a weapon to the local hand at match start).
3. Compile clean, then one harness run: paintball rifle in the screen-mode hand firing at a wall (test hook) + one goblin enemy;
   screenshots.` },
]
phase('Resume')
const results = await Promise.all(TASKS.map(t => agent(COMMON + t.prompt, { label: t.key, phase: 'Resume', schema: SCHEMA })))
return results.map((r, i) => ({ key: TASKS[i].key, result: r }))
