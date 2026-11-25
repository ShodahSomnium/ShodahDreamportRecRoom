export const meta = {
  name: 'rr-wave1',
  description: 'Implement the foundation modules: Net, Player, Interaction, Rooms/Travel, Watch, Games shared kit',
  phases: [{ title: 'Build', detail: '6 module agents in parallel, each owning its folder' }],
}
const SP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\\scratchpad'
const SPU = SP.replace(/\\/g, '/')
const CODE = 'D:\\Dreamport\\Rec Room\\Rec Room\\Assets\\#User\\SomniumRecRoom'

const COMMON = `
# Dreamport: Rec Room - implementation wave 1
Rec Room played inside a Somnium Space world (Unity 6000.3 URP, desktop and VR). Code: ${CODE} (asmdef SomniumRecRoom,
namespace SomniumSpace.Worlds.SomniumRecRoom.*). The Unity project is open in the user's editor: add/edit files only; never run
Unity on it. READ FIRST: ${SP}\\design\\ARCHITECTURE.md (the contract: hard rules, module map, APIs), then the research docs it
points to, then the existing code (Core/, Rr/, the skeletons in Player/, Net/, Rooms/, Interact/, Games/Shared/) - match its style
(XML doc comments in prose explaining why, measured facts; Allman braces; C# 9; no LINQ in per-frame paths).

Rules for this wave (several agents work in parallel, each owning its folder):
- Edit ONLY the files of your module (listed in your task). Keep every public member of the existing skeleton files you own
  (you may add members and change bodies). If you need something from another module that does not exist, do not edit their
  files: write a small private adapter in your own folder or leave a TODO, and list the request in your result.
  Exception: a clear bug in Core/ or Rr/ may be fixed minimally - say exactly what in your result.
- New files need .meta files: run "bash ${SPU}/tools/metas.sh" after creating files.
- Compile check (must end clean): "bash ${SPU}/compilecheck/build.sh project --editor" -> 0 errors, guard "all N ... proven".
  Other agents edit other folders at the same time: if an error is in someone else's file, wait a minute and re-run; never edit it.
- Runtime check in Unity (play mode, offline, no Somnium client, Bridge getters null): "bash ${SPU}/harness/run.sh <scenario.json>"
  (read ${SP}\\harness\\README.md for the scenario steps: spawn, goto, shot, call, wait, expect_log, dump...). It takes a lock (one
  Unity at a time, ~20 s per run); write your scenarios as ${SP}\\harness\\scenarios\\<module>_*.json and look at the PNGs with the
  Read tool. Use it for what can be seen or logged; keep runs few.
- Offline/editor behaviour must work (no network, no Bridge): the local player is authority; there is a fallback camera.
- Log with RrLog ("[SomniumRecRoom] Area: ..."), once per fact, not per frame.
- Rec Room data: prefabs/sounds/effects come from the player's install via RrAssets/RrSpawn/RrSound/RrFx; configs via
  RrInstance.Config("ScriptName") (fields as the type trees name them). Research tool for looking things up: ${SP}\\rrtool\\README.md.
- Never modify the Somnium avatar/player objects (no components on them, no renderer changes).
Return a structured result (files, public API summary, what was verified how, open issues/requests).
`

const SCHEMA = { type: 'object', properties: {
  files: { type: 'array', items: { type: 'string' } },
  api: { type: 'string', description: 'public API added/changed, concise' },
  verified: { type: 'string' },
  issues: { type: 'array', items: { type: 'string' } },
  requests: { type: 'array', items: { type: 'string' }, description: 'things other modules must do' },
}, required: ['files', 'api', 'verified', 'issues', 'requests'] }

const TASKS = [
 { key: 'net', prompt: `
## Your module: Net/ (you own Net/*.cs except SceneNetworking.cs, which you may only touch to fix bugs)
Implement ${SP}\\research\\somnium_networking.md §5 (design) using §3-4 (proven API, ETS patterns - port EtsSlotClaim, EtsNetTimeline,
packing helpers from D:\\Dreamport\\Euro Truck Simulator\\Euro Truck Sim\\Assets\\#User\\SomniumEuroTruck\\EtsPlayerWire.cs / EtsNetWire.cs, adapted).
The scene already has the objects (Assets\\Rec Room Scene.unity): "Scene Networking", "Rr Net Events", "Rr Net Player 0..49" (NBD64),
"Rr Net Instance 0..23" (NBD64), "Rr Net Actors 0..11" (NBD64), "Rr Net Prop 0..47" (NBD12). Hold them as NetworkBridgeData (base).
Deliver behind the existing skeleton API (RrNet, RrInstanceState, RrNetBody, RrPresenceView; extend freely):
1. RrNet bootstrap (a MonoBehaviour created by RrNet.Init under RrMain.WorldRoot, or on the scene's "Rr Net" object): find objects by
   name, subscribe to SceneNetworking events, Online/LocalId/Present, OnPlayersChanged; static reset in Shutdown.
2. Presence slots: claim (EtsSlotClaim port), layout §5.4 (magic 'RRP1'), writes at 4 Hz/on change; SetLocation/SetTeam/Presence;
   also publish per-player health/kills/deaths setters (int 18), activity-specific ints (19, 32-47) with Get/Set helpers, and the
   local player's held items/grip offsets hooks used by RrNetBody. SlotIndex (fixes the dorm origin; -1 until claimed).
3. Instances: binding (claim a free "Rr Net Instance k" for (activity, number); join the fullest joinable instance when
   instanceNumber == -1, respecting RrRooms.Get(activity).MaxPlayers), InstanceObjectIndex, MembersOfMyInstance, election (lowest
   PlayerId member, liveness/takeover per §5.4), IsInstanceAuthority + OnAuthorityChanged, RrInstanceState backed by the instance
   object (phase/round/timer/map/mode/seed/team scores/roster/player scores/custom ints 27-58) with authority-only writes and
   remaining-time timers. Provide RrNet.JoinInstance(activity, number, Action<int k> done) / LeaveInstance() for RrTravel (Rooms
   module) to call; offline: bind to a local instance with k = (int)activity - 1 (0..21) so origins are stable.
4. Events: batched RPC frames (§5.7), Send(type, body, toAuthority), On/Off, scoped to the instance (drop others), loop-back of own
   events like the platform does (RPC to all comes back to the sender), offline loop-back immediately.
5. Props: RrNetBody per §5.5 (lease by the instance authority, grab/hold/throw, owner writes with force-write on collisions,
   remote kinematic puppets on the timeline, held props drawn at the holder's hand using RrRemotePlayers (Player module; its
   skeleton exists - Remote.LeftHand/RightHand) with the grip offset from the holder's slot, touch-steal with cooldown, reset by
   authority, orphan adoption). Offline or when the current room is not networked (RrRooms info Networked=false): inert.
6. Actor blocks for AI (quests): RrNetActors lease/read/write API (§5.8) - 15 actors per block (pos, yaw, type, anim state,
   health, flags) with timeline interpolation helpers for watchers.
7. Keep all wire code pure (no Unity native calls) and write an offline test (a dotnet console project in ${SP}\\nettest, compiling
   your pure wire/claim/election files plus a fake Fusion room that models "last authority request wins, writes without authority
   dropped, a delay on everything") covering: 3 clients joining, simultaneous slot claims, authority leaving mid-round, late
   joiner reading state, instance binding races, prop grab race. Report the results.
8. Guard: only server-proven Fusion/Somnium members (networking doc §10); the compile guard enforces it.` },
 { key: 'player', prompt: `
## Your module: Player/ (RrPlayer, RrHand, RrInput, RrRemotePlayers + any new Player/*.cs)
Implement per ${SP}\\research\\somnium_player.md (complete) and conventions §11 (input) - measured facts about the client's rig, Bridge
body transforms, OpenXR poses, desktop camera rig (third person, camera not under the body), keys the client owns.
1. RrPlayer: Init creates a driver MonoBehaviour ([DefaultExecutionOrder(10000)] LateUpdate as the doc recommends) under RrMain.WorldRoot;
   Camera (EtsVr.Head logic), head/feet/yaw, InVr, LocalId (Fusion PlayerId via SceneNetworking.NetworkRunnerRef.LocalPlayer when
   running; 0 offline), Nick, Teleport (Bridge DoTeleportToPoint; offline: move the fallback rig), SetMovement (Bridge
   SetMovementSpeed/SetJump, proven members only), plus anything else useful from the doc (gravity, fly/teleport disable for games).
2. RrHand x2: VR - a grip frame parented under the Bridge Body.LeftHand/RightHand anchors (re-found until non-null) with the
   per-runtime offset the doc recommends; fallback converted InputDevices pose. Desktop (screen mode) - the right hand is a virtual
   hand in front of the camera (Rec Room screen mode holds objects low-right in view), the left hand unused. Velocity/angular
   velocity from a pose ring buffer (cleared on teleport), grip/trigger with hysteresis, edges, haptics (InputDevices impulse).
3. RrInput: desktop scheme per the doc's section 5 on free keys/mouse (Rec Room screen mode: use/fire, grab/drop, throw, watch/menu);
   VR: triggers/grips are ours, menu on a free button or wrist glance (Watch module decides the glance; you expose MenuDown for the
   chosen desktop key and a VR button if one is free). Blocked flag while the client tablet or our watch is open (expose a setter).
4. RrRemotePlayers: Bridge PlayersContainer AllPlayers cached at 1 Hz and on join/leave (proven members only), id/nick/body transforms.
5. Editor/offline fallback: when there is no Somnium client (Bridge PlayersContainer null) and no enabled camera renders, create a
   simple first-person rig (capsule CharacterController on layer 2... NO: use layer 0 for the fallback body, it is ours) with WASD +
   mouse look (right mouse held) so the world can be walked in the editor and the harness; never create it in the client.
6. A harness scenario that boots and shoots a frame showing the screen-mode hand position marker (a small debug gizmo you add
   only in the editor fallback) to prove the rig works offline.` },
 { key: 'interact', prompt: `
## Your module: Interact/ (RrGrabbable, RrGrab, RrTools + new Interact/*.cs behaviours)
Research: ${SP}\\research\\rr_dorm_core.md (Tool/RigidbodyPickup/CollisionAudio/Breakable/Drinkable/food/basketball configs; it may
still be in progress - read what is there and look up the rest yourself with rrtool), somnium_player.md sections 2-5 (hands, the
client's own grab system and how not to fight it: our own grip reading + overlap test, excludeLayers |= 1<<7 while held, props on
layer 10), rr_runtime_facts.md.
Implement:
1. RrGrab: per-frame hover (nearest grabbable within ~0.12 m of the VR grip frame / the screen-mode aim ray up to ~2.5 m),
   highlight (Rec Room shows a soft outline/brighten on hover: a cheap emissive/rim tint on the hovered renderers via
   MaterialPropertyBlock, restored after), grab (parent to RrHand.Grip at the item's grip pose, kinematic, excludeLayers 7 while held),
   use (trigger / desktop use -> NotifyUseDown/Up), release/throw (velocity from RrHand, ThrowMultiplier, MaxThrowSpeed; desktop throw
   key throws forward with a Rec Room-like arc), drop. Networked props: call RrNetBody.LocalGrab/LocalRelease when present.
   Sounds on pickup/throw from Rec Room's Tool.audioSettings when present.
2. RrTools.MakeTool(RrInstance): read the prefab's Rec Room configs and add behaviours: Tool (grips: pick the Vive/Oculus pickup
   transforms or FullBody grip transforms as the doc explains; throw settings), CollisionAudio -> RrCollisionSound (mappings by
   layer/tag, velocity->volume curve, InstanceLimitDuration), BreakableObject -> RrBreakable (shatterSpeed, whole/fractured swap,
   smash particles and sounds, broken lifetime), DrinkableBottle (+Potion/ShakeAudio) -> RrDrinkable (uncork on use, pour particles
   while tilted, drink when near the mouth = near RrPlayer head: swallow sounds at drinkInterval, empty after lifeSpan...), food
   consumables -> RrEdible (bite near mouth: shrink/replace stages, chew sounds), ShakeAudio -> RrShakeSound, AnimateInOut ->
   spawn/despawn pop, ToolCleanup -> optional respawn of dropped tools after a delay (RrRespawnable), basketball -> RrBall (bounce
   sound by speed, rim/backboard hooks exposed as events). All values from the configs; clip/effect refs via RrSound.Play(RrClipInfo)
   and RrFx.Spawn(RrTarget).
3. RrPressable: a button/clickable in the world (desktop: aim + use or the client's click (IPointerClickHandler on layer 16 per
   conventions §11.3), VR: poke with the hand or trigger while pointing) with Rec Room button sounds; used by games and rooms.
4. A harness scenario: spawn the root beer bottle, basketball and a food item; programmatically grab them via RrGrab.Give and
   exercise behaviours (use, drink near a fake head, throw, a fall to break) - log the Rec Room values used; PNGs of before/after.` },
 { key: 'rooms', prompt: `
## Your module: Rooms/ (RrTravel, RrRegistry is shared - you may only add infrastructure there, not games; new Rooms/*.cs)
Research: ${SP}\\research\\rr_reccenter.md (section 4: how moving between rooms looks/sounds in Rec Room; may still be in progress),
somnium_networking.md §5.3-5.4 (placement, instance binding), ARCHITECTURE §6.
Implement:
1. RrTravel.GoTo/GoHome/Begin/Busy/Current/CurrentRoom/OnArrived: the trip - Rec Room-style transition (fade to the loading card:
   Rec Room's loading look with its logo/colours, activity title and cover art if available, GenericTeleport01 or the doc's sounds),
   leave the old room (OnLocalExit, RrNet.SetLocation, destroy), bind the instance (RrNet.JoinInstance - Net module adds it; until it
   exists use offline placement through a small adapter), create the room root at its origin (dorm: RrNet.SlotIndex, offline slot 0;
   instances: InstanceObjectIndex k; formulas in ARCHITECTURE §6), run Build() as a coroutine under the fade with a timeout and
   error handling (fall back to the dorm on failure), teleport (RrPlayer.Teleport) to Spawn(index), set presence Lobby/Playing,
   OnLocalEnter, room music (Info.Music), fade in. Screen fade for desktop (overlay canvas) and VR (a sphere/quad around the head -
   never write the camera). Begin(): stay in the arrival room with the status until the network is ready or 15 s pass (offline),
   then GoHome.
2. Dispatch RrNet.OnPlayersChanged / OnAuthorityChanged to CurrentRoom.OnMembersChanged / OnAuthorityChanged.
3. Safety: if the local player falls below the room's floor (y < origin.y - 20), put them back at the spawn; keep the arrival
   room as the fallback when nothing is built.
4. RrPortal (Rooms/RrPortal.cs): a Rec Room activity door: a framed doorway with the activity title/cover sign and glow, entered by
   walking into it or clicking/pressing (RrPressable from Interact, if not ready use your own trigger) -> RrTravel.GoTo(activity).
   The Rec Center and dorm use it.
5. A placeholder room registered ONLY IF no Dorm is registered yet: "RrPlaceholderRoom" (a simple Rec Room-style room with a
   title) so travel is testable now; register it from RrRegistry only when RrRooms.Get(Dorm) is null at the end of RegisterAll.
6. Harness scenario: boot -> Begin -> arrives in the (placeholder) dorm; goto RecCenter placeholder; shots of the loading card and
   the rooms.` },
 { key: 'watch', prompt: `
## Your module: Watch/ (RrWatch + new Watch/*.cs)
Research: ${SP}\\research\\rr_watch_ui.md (complete: Rec Room watch model, screens, colours, fonts, icons, sounds, re-creation spec),
somnium_player.md (wrist pose, free buttons), conventions §10 (runtime UI, VR panels: TrackedDeviceGraphicRaycaster, layer 5 on the
whole tree, worldCamera refresh; AoM's wrist panel precedent), §11 (keys).
Implement Rec Room's watch menu as faithfully as the research allows, built at runtime with UnityEngine.UI + TMP (fonts: RrText),
sprites/textures loaded from the bundles by name (RrAssets.Load<Sprite>/<Texture2D>), sounds via RrSound.PlayUi:
1. VR: the watch model on the left wrist (from the bundles if the doc found it; otherwise a Rec Room-like watch built from
   primitives), placed on RrHand.Left each frame (our object, never the avatar); raising/looking at the wrist opens the menu panel
   floating above the wrist (Rec Room behaviour per the doc), pointer = the client's own ray (world-space canvas with
   TrackedDeviceGraphicRaycaster), close by lowering/tapping.
2. Desktop: the key chosen in the doc/Player module (RrInput.MenuDown) toggles a screen-space version of the same menu; cursor use.
3. Screens: Home (Rec Center, Dorm, Play, People, Settings buttons as Rec Room lays them out), Play (cards for every RrRooms.All
   entry grouped by Category, with cover art from RrRoomInfo.CoverSprite or the doc's per-activity artwork names, title, player
   count from RrNet presence; clicking -> RrTravel.GoTo), People (players in the world: nick, where they are via RrNet.Presence,
   "Join" -> GoTo their activity/instance), Settings (RrSound.Volume, RrMusic.Volume, desktop sensitivity if Player exposes it),
   and a "Go to Dorm" / "Leave" action. Rec Room's open/close/click/hover sounds.
4. RrInput.Blocked while open; RrWatch.Shutdown cleans everything.
5. Harness scenario: open the menu (call a static RrWatch.OpenForTest or similar) in desktop mode and shoot it; also render the VR
   variant by forcing the world-space layout with a fake wrist pose (test-only switch). Compare to the Rec Room look described.` },
 { key: 'gameskit', prompt: `
## Your module: Games/Shared/ (you own the new files; RrGameRoom/RrTeams/RrAnnouncer/RrScoreboard/RrGameEvents exist - extend, keep API)
Research: ${SP}\\research\\rr_shooters.md (weapons, projectiles, configs, VFX, team colours), rr_quests.md (enemies, melee, revive),
rr_sports.md (balls), somnium_networking.md §5.7 (events: Fire/Hit/Death, "favour the shooter"), somnium_player.md (hands, aim).
Implement shared gameplay pieces every game will use:
1. RrHealth: the local player's hit points per game rules (config: max HP, regen, respawn delay), hit reaction (screen flash for
   desktop, a vignette sphere in VR - never touching the camera), "out" state and respawn at the room's team spawn via RrPlayer.Teleport,
   publishes health/kills/deaths to presence (RrNet setters if present, else local), Death event.
2. RrHitVolumes: our own layer-27 capsules/spheres (head, body) following each same-instance remote player's Bridge body transforms
   (RrRemotePlayers), plus one for the local player (for AI melee/enemy projectiles); lookup hit -> playerId, body part.
3. RrWeapon (ranged): wraps a spawned Rec Room gun prefab + its RangedWeapon/Projectile configs from the prefab data (fire rate,
   spread, magazine/ammo/reload, projectile speed/gravity, damage per body part - field names from the research doc), held via
   RrGrabbable (OnUseDown/Up), muzzle from the prefab's muzzle/spawn transform, fires RrProjectile locally, sends Fire events, plays
   Rec Room fire/reload/empty sounds and muzzle VFX; remote Fire events spawn visual projectiles. Hits decided by the shooter's
   projectile against RrHitVolumes -> Hit event -> the victim's RrHealth applies damage. Paint splat / laser impact VFX on surfaces.
4. RrProjectile: simulated ballistic projectile (paintball arc, laser bolt straight), layer 26, raycast-stepped, team-aware
   (friendly fire off by default), impact effect + sound per weapon.
5. RrMelee: swing detection by hand velocity for held melee tools (quest swords, pirate cutlass...) using Rec Room MeleeWeapon
   configs (damage, enemy damage), hits enemies (RrEnemy) and players (PvP where enabled).
6. RrEnemy (base for quest AI): spawn a Rec Room enemy prefab (Animator + controller from the bundle; drive its parameters as the
   quest research doc says), health, simple state machine (idle, chase nearest player, attack in range, hurt, die with Rec Room death
   VFX/sounds/coins), navigation by steering on flat arenas with obstacle avoidance raycasts (no NavMesh baking at runtime needed;
   optional NavMesh if available), authority-simulated with positions in RrNet actor blocks when Net exposes them (RrNetActors); if
   that API is missing, keep a local simulation with a TODO.
7. RrLoadout helpers: spawn a weapon into the local player's hand at match start (RrGrab.Give), holster/respawn.
8. Harness scenario: spawn the paintball assault rifle, give it to the screen-mode hand, fire 3 shots at a wall (call a test hook),
   shoot the paint splats; spawn one quest goblin enemy and shoot it idle/walking.` },
]

phase('Build')
const results = await Promise.all(TASKS.map(t => agent(COMMON + t.prompt, { label: t.key, phase: 'Build', schema: SCHEMA })))
return results.map((r, i) => ({ key: TASKS[i].key, result: r }))
