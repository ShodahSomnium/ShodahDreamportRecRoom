# Wave 1 results (foundation modules), 2026-09-24

## net
### API
The previous run's wire layer (RrWire, RrSlotClaim, RrPresenceWire, RrInstanceWire, RrEventWire, RrPropWire, RrActorWire, RrLease, RrTimeline) is unchanged. SceneNetworking.cs is untouched. No SKELETON markers are left in Net/.

The new pure files have no Unity or Fusion calls, so nettest runs exactly the same code:
- RrElection.Elect: the lowest id among members that are not Loading, else the lowest Loading one.
- RrElection.Acting: the member named in the header keeps the instance while its header is live. A named authority that left goes to the elected member. After 3 s of silence the others take over in id order, 3 s + 1.5 s per candidate ahead.
- RrGrabClaim: optimistic hold. Pending, then Holding; outputs Ask, WriteHeld and Drop. The timeout is 2 s.
- RrNetCore: one client's whole brain. It covers the slot claim and own-record pushes, housekeeping that writes free the slots of players who left, members, binding (FindBound plus an RrSlotClaim over the 24 instance objects), election, instance-state push and read with the timer sent as remaining ms (heartbeat 1 s), and prop and actor-block leases. The adapter fills the room view, calls Step(now) and carries out Asks and Writes. Queries: SlotIndex, Bound, BoundGeneration, IsAuthority, Acting, Members, PropOf[], BlockOf[], RecordOf(id).

RrNet (static):
- Status: Online (IsNetworkReady and the scene objects found), LocalId (runner.LocalPlayer.PlayerId, 0 offline), Present, Now, Core.
- Events: OnPlayersChanged and OnAuthorityChanged(bool).
- Presence setters: SetLocation(a, n, status), SetStatus(RrStatus), SetTeam, SetCombat(h, k, d), SetHealth/SetAmmo/SetKills/SetDeaths, SetPlayerInt(i, v), SetSpawn, SetNoGame, SetMenuOpen, SetHand.
- Presence reads: PlayerInt(id, i), Presence(id) (a RrPresenceView, now also with InstanceObject and Ammo), PresenceRecord(id), MembersOfMyInstance(), SlotIndex.
- Instance: IsInstanceAuthority, InstanceAuthorityId, InstanceObjectIndex (offline: activity - 1), InstanceGeneration, Instance.
- Joining: `IEnumerator JoinInstance(RrActivity a, int number, Action<int> done = null)`. The work starts at once and the returned routine only waits for the end. Number -1 picks the fullest joinable instance below RrRooms.Get(a).MaxPlayers. done(number) runs when the instance is bound, or after 8 s (then play solo, InstanceObjectIndex -1). Also LeaveInstance() and ChooseInstanceNumber(a).
- Events: Send(type, body, toAuthority) is batched into one frame per 50 ms and scoped to the instance by k+1 and generation. The sender gets its own events back locally. toAuthority goes through RPC_SendMessageToController on the bound instance object. Also SendGlobal, SendToPlayer, On, Off and EventStats.
- Lifecycle: Init creates the "Rr Net Driver" under RrMain.WorldRoot; Shutdown.

RrInstanceState:
- Phase, Round, Map, Mode and Seed are now properties that read and write the ints (source compatible). New: Option/SetOption, AuthorityId, Generation, SetTimer, Objective/SetObjective, Raw.
- Roster: PlayerScore/SetPlayerScore now work, plus RosterTeam, RosterReady, RosterSpectator, SetRoster, RemoveFromRoster, RosterPlayers and ResetGame. Existing members are kept.
- Only the authority can write. Other writes are ignored with one warning. `new RrInstanceState()` gives a detached local copy.

RrNetBody:
- Existing: LocalPropId, LocallyControlled, HolderId, HolderHand, ThrowId, OnRemoteGrab/OnRemoteRelease, LocalGrab, LocalRelease, LocalTouch, ResetTo.
- Added: PropObject, Networked, KindInt, SetKindInt and static Find(localPropId).
- Whoever holds the prop object's authority simulates and writes: up to 30 Hz, plus a forced sample on every collision, until the prop rests.
- On other clients the prop is a puppet: at the holder's Remote.LeftHand/RightHand with the grip offset from the holder's slot, otherwise on the timeline.

RrNetActors:
- Get(localBlock), Release, Put(actor, state), Actor(i), Sample(i, out state), GetBit/SetBit, Raw/SetRaw, Clear, IsWriter, Networked, OnAdopted.
- Writes at 10 Hz with a 1 s heartbeat. Positions are world positions in the API and are sent relative to the instance origin.

RrNetSelfTest.Run() (an IEnumerator) is an offline self-test for the harness.
### Verified
1. Compile. `build.sh project --editor` ends with 0 errors and 0 warnings on all 126 files (server-like, editor-like and editor tools) and "Server API guard: all 113 Fusion/Somnium members are proven". I re-ran it after the last edit.

2. nettest (dotnet net8.0, `scratchpad\nettest`, `dotnet bin/Release/net8.0/nettest.dll`): **all 30 checks passed**. It compiles the 12 pure Net files, including RrNetCore, and references UnityEngine.CoreModule for Vector3 and Quaternion. The fake room: the last authority request wins, writes without authority are dropped, every message and replication is delayed 60 ms, a client that loses authority is resynced to the server, and the master requests every object nobody holds once a second (SceneNetworking.SlowLoop).
   1. Three joins: slots 1, 2 and 3 are owned in about 1.25 s, and every client lists 3.
   2. Simultaneous claims (ids 105, 155 and 205 all start at slot 5): the collisions resolve to slots 5, 6 and 7. There are no duplicate listings, and the leavers' slots are written free by housekeeping.
   3. Election: all three clients bind the same instance object (1), only player 1 (the lowest id) is authority, and all three agree on the members. Round and score reach the others, and their timer end is 0.2 s off. At idle the instance costs one write per second (the heartbeat).
   4. Authority leaves mid-round: player 2 took over in 400 ms on the same object. Round and scores survived, and the timer is 0.4 s off.
   5. Late joiner: finds the same object, reads round, score and timer, and authority stays with player 2.
   6. Hung authority (present but silent): player 3 took over after 2.65 s of header silence. When player 2 comes back it stands down.
   7. Binding race: Dodgeball #0 and Dodgeball #5 both start their search at object 20 and end on 21 and 20. Each runs its own, and the Paintball binding is untouched.
   8. Prop lease and grab race: prop 0 is leased to object 2 and all clients agree. Players 3 and 4 grab at the same moment; exactly one holder remains (player 4), player 3 drops with "taken by player 4", every client's copy says Held by 4, and the holder keeps the prop's authority.
   9. Holder leaves: the instance authority adopts the orphaned prop.
   10. Last member alone: a two-player instance whose authority leaves keeps its object and round. This scenario caught a bug in FindBound, which is now fixed.

   Totals: 138 writes accepted, 1 dropped, 299 authority requests.

3. Play-mode harness (2 runs of `harness/run.sh .../nettest/net_offline.json`, exit 0 both times, the second after the last functional change): "Net self-test: all 14 checks passed" with 0 errors or exceptions. It covered editor offline mode:
   - binding to activity - 1, authority, members and the own presence view;
   - instance-state writes and the timer countdown;
   - events coming back on the next frame, and Off;
   - JoinInstance offline, and a clean state for a new instance;
   - an actor block's put/get and bitset;
   - a prop body falling back to local physics.

   Log lines: "Net: started (waiting for the room; offline until then)", "joined Dodgeball #0: offline or unnetworked".
### Issues
- Not tested against a real Somnium/Fusion room. The Fusion adapter parts (RrNetRoom, RrNetEvents, RrNetBody, RrNetActors) are only compile-checked (guard proven) and tested offline. nettest covers the pure core, not the Fusion calls, the RPC wiring or the prop physics.
- Timers: each authority hand-over adds about one one-way latency to the countdown (0.2 s on first sync, 0.4 s after a hand-over in the simulation). This follows the Â§5.6 design; there is no latency compensation.
- Prop puppets freeze constraints and turn gravity off instead of setting isKinematic. RrGrab records WasKinematic before it calls net.LocalGrab, so a forced isKinematic would stick after a throw. Remote colliders are not switched to triggers, the 150 ms throw blend is not done, and Impact sound events are left to the games.
- The contract's `RrNet.LeaseProp(int) -> RrNetProp` does not exist. Props are RrNetBody components that the instance authority leases automatically; RrNetBody.Find(localPropId) looks one up.
- When RrRemotePlayers has no hand transform for a holder (desktop players, or the player module not up), a held remote prop falls back to its last written pose on the timeline.
- Instance binding: a non-elected member of an unbound instance becomes its authority after 10 s (SoloAfter). A lone member whose authority request is never granted keeps waiting on the same object; it does not rebind.
- Hello/Census (bulk state for late joiners) is not implemented. SendToPlayer sends one unbatched RPC per call.
- SceneNetworking._autoTransferObjectsAuthority defaults to true in code. The design wants 0 in the scene; this only affects race speed, not correctness.
- RrNetSelfTest ships in the runtime assembly. It is small and the world never calls it.
### Requests
- Rooms (RrTravel): your reflection lookup now finds `RrNet.JoinInstance(RrActivity, int, Action<int> done = null)`, which returns an IEnumerator that ends when the instance is bound (at most 8 s). You can call it directly and drop the 5 s SetLocation adapter. After it ends, InstanceObjectIndex >= 0 is the bound object and -1 means play solo at the offline place.
- Games (RrPresenceStats): RrNet.SetCombat(int,int,int), SetStatus(RrStatus), SetHealth, SetKills, SetDeaths and SetAmmo now exist. The reflection adapter finds them, or you can call them directly.
- Editor (RrNetSceneSetup): keep the exact names and sizes: 'Rr Net Events' (NetworkBridgeEvents), 'Rr Net Player 0..49' / 'Rr Net Instance 0..23' / 'Rr Net Actors 0..11' (NetworkBridgeData64) and 'Rr Net Prop 0..47' (NetworkBridgeData12). Also set SceneNetworking._autoTransferObjectsAuthority to 0. RrNet logs one warning with found counts if anything is missing, and plays offline without the slots and instance objects.
- Games using props or AI: add RrNetBody with LocalPropId set before OnEnable (0..255, unique within the activity) to each shared prop root. For AI or bitsets use RrNetActors.Get(block): the writer uses Put and SetBit, watchers use Sample. Dorm and unnetworked rooms stay local automatically.
- Games: RrInstanceState writes only take effect on RrNet.IsInstanceAuthority. Other clients should ask the authority with RrNet.Send(type, body, toAuthority: true) and repeat the request until the ints show the effect, because requests can be lost during a hand-over.

## player
### API
RrPlayer (static class, namespace .Player):
- Mode: InVr; InPlayMode (= RrScreenCamera.Active); UsingFallbackRig; HasClient; ThirdPerson {get;set} (V toggles it); PlayModeWhileHolding {get;set}=true; FovKick {get;set} (clamped 0.5..2); RequestPlayMode(string owner, bool on).
- Pose: Camera (EtsVr.Head logic, cached once per frame, scene search at most every 0.5 s); PlayerObject (the "Rr Player" object under RrMain.WorldRoot); Root (Bridge Body.Root, else the rig); HeadAnchor (Bridge Body.Head, else the rig's head); BridgeHand(RrHandSide) (null on desktop and in the editor); HeadPosition (the camera in VR, play mode and on the rig, otherwise the head anchor); HeadRotation (the view); FeetPosition (Body.Root; in VR the head projected onto the root's floor; the rig; otherwise 1.6 m below the head); Yaw.
- Identity: LocalId (SceneNetworking.NetworkRunnerRef.LocalPlayer.PlayerId while the runner is running, otherwise 0); Nick (Bridge NickName, otherwise "Player"); Local (ISomniumPlayer); Motion (ISomniumPlayerMotion).
- Actions: Teleport(feet, yaw, done). With the client it calls Bridge DoTeleportToPoint(feet, (0,yaw,0), done). Offline it moves the rig, or stores the pose until the rig exists. done always runs, even when the move is impossible. Afterwards the hand velocity history is cleared and play mode takes the new yaw. SetMovement(speedScale, jump): clamped 0..10. It calls SetMovementSpeed(s,s,s) and SetJump; at 0 it also turns off VR teleport and fly mode. It is re-applied when the client's body appears or is replaced, and restored to 1 on Shutdown and when the driver is destroyed.
- Init() and Shutdown(). Shutdown resets RrScreenCamera, RrInput, RrXr, RrClientTablet and RrRemotePlayers and clears every static. Describe() gives a one-line state. Probe() is an internal harness coroutine.

RrRemotePlayers: All (IReadOnlyList<Remote>), Count, Get(id), event OnChanged. Remote fields: Id (PhotonId = Fusion PlayerId, -1 until known), Nick, UserId, Root, Head, LeftHand, RightHand (null for desktop players), Player (ISomniumPlayer), Vr. Remote objects are reused across refreshes. The list refreshes at 1 Hz and on Bridge OnPlayerAdded; leavers are found by the 1 Hz pass.

RrFallbackRig (public MonoBehaviour): EyeHeight 1.55, Height 1.8, Radius 0.3, SpeedScale, JumpAllowed, Head, Camera, OwnCamera.

RrInputPump ([DefaultExecutionOrder(-10000)] Update): RrXr.Poll, then the VR hands' PollVr, then RrInput.Poll, then on desktop the right hand's MirrorScreen, the left hand's ClearButtons and the V toggle.

RrPlayerDriver ([DefaultExecutionOrder(10000)] LateUpdate): calls RrPlayer.LateTick, which does the client tablet (only with a client), the rig decision, movement re-apply, RrScreenCamera.Tick, both hands' LatePose and RrRemotePlayers.Tick. Its OnDestroy gives back the camera, cursor and movement.
### Verified
1) Compile: "build.sh project --editor" at the end gives server-like 0 errors and 0 warnings (125 files), "all 113 Fusion/Somnium members are proven", editor-like runtime 0 errors and editor tools 0 errors. Earlier, while other modules were half-written, Player/ was also compiled on its own with SceneNetworking and minimal Core stubs: 0 errors, 0 warnings, all 90 members proven. The Bridge members used are LocalPlayer, AllPlayers, OnPlayerAdded, References.Body.*, Properties.NickName/PhotonId/Id/IsLocal, Features.Motion, DoTeleportToPoint, SetMovementSpeed, SetJump, SetTeleportationDisableState and SetFlyModeDisableState. Fusion: NetworkRunner.IsRunning, LocalPlayer and PlayerRef.PlayerId.
2) One harness run (scenarios/player.json): Unity exit 0, booted, 8 of 9 steps OK, 0 errors or exceptions.
- "Player: up" at +0.68 s. The view camera 'Harness Camera' is found as Camera.main.
- The screen-mode right hand appears at +0.72 s: ScreenFirstPerson, view-local offset (0.22, -0.20, 0.45); the left hand is unused.
- With no client after 0.5 s, the fallback rig is made at +1.20 s at (0, 0, -3), placed from the harness camera and driving it.
- The Probe coroutine teleported the rig to (2,0,2) yaw 90 (done=True, feet and yaw read back correctly) and back. SetMovement x0/no jump and x1 are logged. The hand stays at view-local (0.22, -0.2, 0.45) after each move.
- The dump shows Rr Player with RrInputPump, RrPlayerDriver, Rr Hand L/R with Rr Grip L/R, and the rig.
- The failing step is "play mode on": with the rig, play mode needs Application.isFocused, which a batch-mode run most likely does not have. So play-mode activation is unverified.
### Issues
- Play mode was not verified at runtime. The harness run showed no play mode on the fallback rig, most likely because batch mode is not focused. On the client, the Cinemachine-brain pause, body-yaw write and cursor lock in RrScreenCamera are untested; they need an in-client check.
- Remote-player leaves are found only by the 1 Hz pass: OnPlayerRemoved is not on the proven list, so it is not used. Between passes a leaver's transforms become null, and consumers already skip a null Root.
- LocalId returns 0 until the runner is running and PlayerId >= 0. Whether Fusion PlayerIds can be 0 in shared mode is not checked; if they can, 0 is ambiguous with offline.
- SetMovement(0) freezes speed and jump and turns off VR teleport and fly mode. Gravity and collisions are left alone; the full ETS freeze sequence is not exposed.
- In the editor the fallback rig drives an existing rendering camera instead of adding a second one. In the harness that is 'Harness Camera', which it moves to the rig's eye height; harness shots use their own camera and are unaffected. The rig can never exist once a client PlayersContainer has been seen.
- During the harness run, the Probe's SetMovement(1) lifted RrTravel's start-up freeze, because both ran at the same time. This is a probe-only side effect; the world never calls Probe.
- metas.sh wrote 44 .meta files across all modules, not only Player; that is how the shared tool works.
### Requests
- Net: RrNet.LocalId still returns a constant 0. It should use the same source as RrPlayer.LocalId (SceneNetworking.NetworkRunnerRef.LocalPlayer.PlayerId), or call RrPlayer.LocalId, so both agree.
- Games/Rooms: turn desktop first-person play mode on and off with RrPlayer.RequestPlayMode("<game>", true/false). Set RrPlayer.FovKick for sprint or slide and put it back to 1 afterwards. Freeze with RrPlayer.SetMovement(0,false) and always restore with SetMovement(1).
- Watch/menus: block gameplay input with RrInput.SetBlocked("watch", on). Play mode then hands the camera and cursor back by itself.
- Core (README owner): the module README still lists Player as 'In progress (wave 1)'; it can be marked done.

## interact
### API
namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
- RrTools (static): RrGrabbable MakeTool(RrInstance) / MakeTool(RrInstance, bool popIn). It reads Tool (CanBePickedUp, LockOnPickup, ThrowSpeedMultiplier, HasMax/MaxThrowSpeed, the FullBody grip poses with the Right/LeftHandPickupTransform fallback, Pickup/Throw/Drop/Reset clips, highlightRenderers minus RenderersThatDoNotHoverHighlight, and the screenSettings.ScreenBehaviorConfig read from the prefab's own bundle into RrScreenThrow) and RigidbodyPickup.interpolateOnPickup. Props with a KinematicRigidbodyPickup only are not grabbable. It then adds a behaviour for each Rec Room component on the prefab: CollisionAudio, BreakableObject, DrinkableBottle+Potion, ShakeAudio, DisposableItem, Basketball, a hoop (it has a Rim.ProxyInner node), AnimateInOut (unless suppressed) and ToolCleanup (unless excluded). Helpers: CurveOf(List<RrKey>) (null when empty), Evaluate(curve, x, fallback), V3/Q(raw), NodeOf/NodesOf(inst, cfg, field), Play(RrClipInfo, pos, volumeScale, maxDistance, pitchAdd, follow), PlayAny, Loop(RrClipInfo, follow, maxDistance), SetLoop, SpawnFx(RrTarget, fallbackName, pos, rot), Once(area, key, text), PlayerNear(point, dist), Made. internal Shutdown().
- RrRefs (static): collider -> Rec Room surface tag. Consts Table, Paddle, PoolBall, BowlingLane, BowlingGutter, BowlingBackboard. SetTag(Collider|GameObject, tag), TagOf(Collider).
- RrCollisionSound: Mappings (Name, Mask, Tag, Volume curve, Clips, InstanceLimit), MaxDistance, Played, event OnImpact(name, speed, collider), Impact(collider, speed, point) is the test hook, Choose(collider), Find(name), static OurMask(rrMask).
- RrBreakable: ShatterSpeed, ShatterOnAnyImpact, BrokenLifetime, Whole, Fractured, SmashSounds, DestroyWhenBroken, IsBroken, events OnBroken(point) and OnMended, Break(point, velocity), Mend().
- RrDrinkable: Cork, Mouth, Stream, the config values, RefillOnRespawn (default true), PourAngle 95, ScreenTilt 115, the Potion fields (DrinksToActivate, DrinkResetTime, PowerupType...). State: IsCorked, Remaining, IsEmpty, IsPouring, IsDrinking, Swallows. Events: OnUncorked, OnSwallow(n), OnEmptied, OnPotionActivated(powerup). Methods: Uncork(), Refill(), SetScreenDrink(bool), StopPouring().
- RrShakeSound: Definitions, OnlyWhileHeld, Played, event OnShake(linear), Evaluate(linear, angular) is the test hook, ResetHistory().
- RrEdible: Visuals, MinBiteInterval, BiteDistance, LastPieceInedible, DestroyFinalPieceOnCollision, RespawnWhenEaten. State: Bites, Stage, BitesLeft, IsFinished. Events: OnBite(left), OnEaten. Methods: Bite(), ScreenBite(), ResetBites().
- RrAnimateInOut: AnimateIn(Action done=null), AnimateOut(Action done=null), Restore(), IsAnimating, Visual, InDelay/OutDelay/Duration.
- RrRespawnable: ToolCleanup thresholds (MinDisplacement, MinRotation, MinPlayerDistance, Delay, OnlyWhenSleeping, KillHeight -50), SetHome(), Respawn(bool animate), Displaced(), Respawns, event OnRespawned. A shared prop respawns only on the client where RrNetBody.LocallyControlled is true.
- RrBall: static events OnScored(ball, hoop), OnRimHit / OnBackboardHit(ball, hoop, speed), OnBounce(ball, speed). Counters: Scores, Streak, Bounces, RimHits, BoardHits, LastThrowerId. Score(hoop) is the hook. The FireTrail lights after 3 baskets in a row.
- RrHoop: Setup(RrInstance), Rim colliders (tagged CollisionSfx_Table), ScoreTrigger, RimCenter, Scores, event OnScored(ball), static All, Of(collider), IsRim(collider).
- RrPressable (MonoBehaviour, IPointerClickHandler): const ScreenReach 3, PokeRadius 0.05, DefaultSound "Charades_ButtonPress". Fields: PressSound, Volume, Cooldown 0.3, Interactable, Cap, Travel. Events: OnPressed, OnPressedBy(how), static OnAnyPressed(button, how). Press(how), Presses. static Make(GameObject, Action) puts the colliders on layer 16 with excludeLayers ~0. static Create(parent, name, localPos, localRot, Color cap, diameter 0.3, Action) builds a procedural button. static PressAlong(Ray, reach, how) (occluders respected). internal Tick(vr) (VR poke with an empty hand) and Shutdown().
- RrInteractTest (static, harness hooks, all IEnumerator except Report): Setup, Drink, Break, Eat, Ball, Button, Report.
### Verified
1) Compile check: build.sh project --editor reports server-like 0 error(s), 0 warning(s) (125 files), "Server API guard: all 113 Fusion/Somnium members are proven", editor-like runtime build 0 errors and editor tools build 0 errors. The first attempts failed only on other modules (Net, Games), so I waited in a retry loop until they were finished. I ran it again after my last edits, with the same result.
2) Harness: 2 runs of scratchpad\harness\scenarios\interact.json, both exit 0. Run 1 passed 4 of 6: two bugs in the test itself, none in the behaviours. The eat step pressed "use" inside the 1.5 s bite interval. The ball throw started from the screen hand, which the Rooms module had teleported to the dorm at x -1125. I fixed both in RrInteractTest. Run 2 passed 6 of 6 and the expect_log step is ok:
- setup: 4 prefabs spawned and made into tools. The log line per tool shows grip, throw x1 unclamped, the hover renderers, the screen configs DrinkRootBeer, BasketballThrow (12 m/s) and EatPizza (5 m/s), and the added behaviours.
- drink: held, uncorked, pouring and drinking, 3 swallows in 2.6 s, 5.7 s of liquid left. The PNG shows the tilted bottle and the stream.
- break: thrown at the floor at 8.7 m/s, it shattered (shatterSpeed 2), with the fractured visual and 5 smash clips. The PNG shows the chunks.
- eat: 2 of 3 bites taken in screen mode. When the last piece was dropped it hit the floor, finished (destroyFinalPieceOnCollision) and respawned.
- ball: a lob went through the hoop's score trigger, 1 score, and the "Bounce" impact sound played.
- button: a ray press registered, a second press inside the cooldown was rejected, a code press after the cooldown registered, 2 presses in total.
I looked at interact_drink.png and interact_break.png; they render correctly. Everything runs offline in the editor harness (Bridge null) and each fact is logged once through RrTools.Once.
### Issues
- RrSeat (in ARCHITECTURE Â§5) is not implemented; it was not in this task's list.
- Not exercised by the harness, which has no VR and no Somnium client: VR poke and the trigger ray on RrPressable, the client's IPointerClickHandler click, RrShakeSound (only attached; its Evaluate() hook exists), VR drinking and eating by distance (screen mode was tested), RrRespawnable's 15 s cleanup timer (the fall-out-of-world and finished-food respawn paths did run), and the hoop's rim and backboard events (the test ball went straight through).
- The harness listener is at volume 0, so sounds were checked only through counters and log lines, not by ear.
- GUESSES, each documented in the class notes: pour angle 95 degrees; ShakeAudio angular thresholds taken as rad/s2, linear or angular triggers, volume 60-100 %; a CollisionAudio mapping whose tag matches wins over an untagged one; how Rec Room layers 0/10/11/17 map to ours; chunks keep 45 % of the impact velocity; FireTrail after 3 baskets in a row; screen-mode drink and eat go through RrGrabbable.ToMouth.
- Dreamport changes from Rec Room: a broken bottle or a finished food piece that has an RrRespawnable comes back whole at home instead of being destroyed (set DestroyWhenBroken / RespawnWhenEaten=false for Rec Room behaviour), and a respawned drinkable is refilled (RefillOnRespawn).
- Screen behaviour configs are read only from the prefab's own bundle. That covers the ball, drink and food configs; configs in other bundles fall back to the generic throw.
- Effect references from configs point at PooledParticle components, which the catalog does not index, so RrTools.SpawnFx falls back to the known prefab names ([VFX_Basketball_PassedRim], (Quest_Pirate1_Weapon_Sparks_VFX), (WaterBottleSplashFx), [VFX_ObjectSpawn]). Popcorn's own smash VFX is therefore replaced by the sparks.
- RrInteractTest creates a right hand through the internal RrHand.Create only if the Player module has none. In run 2 the Player module provided one.
- RrRespawnable takes its home pose when it is added. Room code that moves a prop after MakeTool must call RrRespawnable.SetHome().
### Requests
- Core (RrFx/RrAssets): let RrFx.Spawn(RrTarget) accept a component reference (a PooledParticle pathId) by mapping it to its prefab root, or expose a component->root lookup. After that, RrTools.SpawnFx no longer needs name fallbacks.
- Core (RrSound): add an overload Play(RrClipInfo, pos, volumeScale, maxDistance, pitchAdd, follow) and a Loop(RrClipInfo, follow) for clip structs. RrTools.Play/Loop duplicate these for now because RrSound fixes the range at 40 m and can loop only by clip name.
- Rooms/Places/Games: call RrTools.MakeTool(inst) (or MakeTool(inst, true) for props that should pop in) on every spawned Rec Room prop. Call RrRespawnable.SetHome() after moving a prop, and tag tables/paddles/lanes with RrRefs.SetTag(collider, RrRefs.Table / Paddle / ...) so CollisionAudio tag mappings play. Hoops set themselves up. Use RrPressable.Create/Make for world buttons and RrBall.OnScored / RrHoop.OnScored for basket scoring.
- Harness: the Rooms module teleports the fallback rig to the dorm about 4.5 s after boot. Scenarios that rely on the screen hand's position should place objects at fixed spots, as RrInteractTest does now.

## rooms
### API
Additions to RrTravel: `public static float ArrivedAt` is the realtime of the last arrival. Bind now uses `RrNet.JoinInstance(RrActivity, int[, optional params])` through reflection when it exists; it can return an IEnumerator (run with a 10 s limit), an int (the instance number) or void. Afterwards the instance number is read from `RrNet.Presence(LocalId).Instance`, then the code waits for `InstanceObjectIndex` as before. RrTravel was otherwise already complete per Â§6: fade and card, leaving the old room, a 90 s build timeout with fallback to dorm, then placeholder dorm, then the arrival room, teleport, presence, OnLocalEnter, music and arrival sting, Begin waiting 15 s (1 s in the editor), dispatch of OnPlayersChanged/OnAuthorityChanged to CurrentRoom, and CheckFooting as the fall safety.

RrRoom: `[NonSerialized] public float BuildProgress = -1` (0..1 moves the loading bar) and `public virtual RrStatus ArrivalStatus` (Lobby by default).

RrLoadingScreen: `public Func<float> ProgressSource`.

RrPortal (MonoBehaviour, the activity door):
- Creating and finding: `static RrPortal Create(Transform parent, RrActivity target, Vector3 localPos, float yaw, string label = null, Color? frame = null)`, where +Z after the yaw points into the room. `static IEnumerator Prepare()` loads the door template; call it in Build. `static RrPortal For(RrActivity)`, `static IReadOnlyList<RrPortal> All`, `static bool TemplateReady`, `static Color? FrameColorFor(RrActivity)`, `static void Shutdown()`.
- Fields: `Target`, `Instance = -1`, `Label`, `Locked`, `LockedMessage`, `Travels`.
- Events and members: `event Action<RrPortal> OnUsed`, `Built`, `Procedural`, `IsOpen`, `DisplayLabel`, `Pose FrontPose(float distance = 1.8f, float lateral = 0f)`, `void PlayArrive()`, `void Use(string how)`.
- Behaviour: it clones `[ActivityDoor]` from `(Charades_InkSpace_MainHull_Sandbox)Variant` once with RrClone, disables the Animator and fixes the materials. Each door gets its own TMP sign label. Frames are recoloured from `ActivityDoor_Recolorable_Mat` (loaded via `(Sandbox_RoomDoor)`) as colour / 0.76, with the LOD group off when recoloured. Hover swings the leaves +21.44 / -22.27 degrees in 0.25 s with smoothstep, fades in the `_TintColor` glow to alpha 0.834, and turns on a point light. The hover sound is Kiosk_StateChange01 at 0.18. The door is used by walking into the doorway (|x|<0.9, z<0.6, counted when entering), a click or trigger on its click box (layer 16, excludeLayers ~0, reach 15 m, walls block), or a VR hand touching the leaves. There is a 1.5 s delay after arrival and a 1 s repeat guard. Unregistered or locked targets show "COMING SOON". If the template cannot be loaded, a procedural door of the same size is built.

RrPlaceholderRoom : RrRoom:
- `static RrRoomInfo InfoFor(RrActivity)` returns a cached info for Dorm and RecCenter.
- The dorm is 7x7 m (unnetworked, Social) with a REC CENTER door.
- The Rec Center hall is 40x36 m with all 21 doors at the rr_reccenter Â§3.3 positions; the Dorm door is on the south wall. Spawn is in front of the door of the room you came from, and that door plays PlayArrive.

RegisterInfrastructure registers each placeholder only when `RrRooms.Get(Dorm)` or `RrRooms.Get(RecCenter)` is null.
### Verified
- **Real project compile:** not clean yet. `build.sh project --editor` reports 3 errors, all in other modules' files (RrHitVolume.cs and RrProjectile.cs cannot find RrEnemy). Those errors stop the compiler before it checks method bodies, so this build could not show whether Rooms is clean.
- **Rooms files:** clean. I copied the project to the scratchpad, added a stub RrEnemy and ran `build.sh dir <copy> --editor`. Method bodies were then checked, and the 30 remaining errors are all in other modules (RrGrab needs RrPressable; RrTools needs RrBall, RrHoop and RrAnimateInOut; RrLoadout needs RrMelee; RrBreakable needs RrRespawnable; RrCombat and RrProjectile need RrEnemy members). None are in Rooms/*. The only later edits are small (the Prepare deadline field and logic), and a later real-project run showed no new errors.
- **Waiting for a clean build:** I started a background loop that recompiles every 90 s until the project builds cleanly, but it had not finished when I had to return. The guard line "all N ... proven" has not been seen yet.
- **Harness:** not run. run.sh exits with 3 while any module fails to compile. The scenario is ready at `scratchpad/harness/scenarios/rooms_travel.json`: goto Dorm, a shot of the dorm door, goto RecCenter, checks for "placeholder RecCenter built" and "door template from", three shots of the hall and doors, `RrLoadingScreen.DebugPreview` with two loading-card shots, then back to the Dorm.
- **Metas:** metas.sh wrote 14 .meta files, including the new ones.
- **Skeleton markers:** none were present in Rooms; I removed the leftover skeleton hook `RaiseSkeletonEvents` from RrTravel.
### Issues
- The project does not compile yet because of other modules (RrEnemy, RrPressable, RrBall, RrHoop, RrAnimateInOut, RrMelee, RrRespawnable are missing). So the proven guard and the harness run (task item 4) are still to do. Once the project compiles, run: bash SP/harness/run.sh SP/harness/scenarios/rooms_travel.json
- The loading-card shot positions are guesses: the fallback rig head at the Rec Center spawn (-3000,1.65,1983.86), and the harness camera (0,1.6,-3) as a second option. DebugPreview logs the real head and panel position; adjust the shot if both images miss.
- Guesses not yet checked at runtime: the catalog finds the hull by its root name '(Charades_InkSpace_MainHull_Sandbox)Variant' through LoadPrefab; RrClone copies the door's LODGroup and leaf transforms; the TMP label under '[Door_Sign]/.../Text' (size 44x11, auto-size 50..100) reads correctly; DoorGlow_Mat keeps _TintColor. The fallbacks are procedural doors, glow off, and a sign at door-local (0,2.625,0.04).
- Interact's RrPressable does not exist yet, so RrPortal does its own click, trigger and touch detection. It is not wired to RrPressable.
- A walk-in trigger depth of 0.6 m (research said 0.35) is a guess based on the avatar capsule radius against the solid door collider; tune it in the client.
- If the template load never answers, the first room build waits up to 60 s, after which doors are built procedurally.
### Requests
- Net: add `public static IEnumerator JoinInstance(RrActivity activity, int instanceNumber)` or an int-returning version. It should set presence, pick the fullest joinable instance for -1, and bind InstanceObjectIndex. RrTravel finds and calls it by reflection with no code change. Also implement SlotIndex and InstanceObjectIndex, which are still skeletons that return -1.
- Interact: finish RrPressable (RrGrab already calls Shutdown, Tick, PressAlong and ScreenReach). Tell Rooms its event API if door presses should go through it; RrPortal handles input on its own until then.
- Places/RecCenter and Places/Dorm: use `RrPortal.Create(...)` for doors and `yield return RrPortal.Prepare()` in Build. `RrPortal.For(RrTravel.Previous)?.FrontPose(1.8f, lateral)` gives the arrival spawn, and `PlayArrive()` animates the door. Registering the real rooms replaces the placeholders automatically.
- Games: their exit door is `RrPortal.Create(root, RrActivity.RecCenter, pos, yaw, "REC CENTER")`. Set `room.BuildProgress` in Build to drive the loading bar, and override ArrivalStatus (Lobby, Playing or Spectating) where needed.
- Games/Shared: RrEnemy is missing, which breaks the whole project compile (RrHitVolume.cs, RrProjectile.cs, RrCombat.cs).

## watch
### API
RrWatch (static partial): Init(), Shutdown(), Open(), Close(), Toggle(), Show(RrWatchPageId), IsOpen, Ready, VrLayout, Ui, Model, event OnOpenChanged(bool), const InputOwner="watch"; settings fields TapToOpen, RightWrist, ShowSeconds (true); test switch ForceVrLayout.
RrWatchUi : MonoBehaviour: static Instance, static Pose? TestPose; Canvas, Built, IsOpen, Detached, VrLayout, Current; ShowPage(RrWatchPageId), ShowRoom(RrActivity), Back(), Travel(RrActivity, int instance), Open(), Close(), ToggleDetach(). Layout constants: canvas 2032x1230, 1920 page, nav bar 150 (VR) / 130 (desktop), desktop panel 0.9 m wide at 0.8 m, wrist 0.6 m, detached 0.9 m at 0.75 m, 80% dim in VR.
RrWatchModel : MonoBehaviour: static Pose? TestPose, TestDwell; OnWrist, FacePosition, Rotation, Dwell, OpenedByWrist; gaze constants 60/45/30 deg to open, 0.4 s dwell, 60/50/50 deg to close; LeftOffset/RightOffset, LeftRotation/RightRotation.
RrWatchSettingsPage : RrWatchPage.
RrWatchTest additions (IEnumerator, for harness "call" steps): OpenDesktop, OpenVr, ShowPlay, ShowPeople, ShowThisRoom, ShowSettings, ShowRoomPaintball, Back, CloseMenu, ShowWatch; HideWatch() (void).
### Verified
Compile: build.sh project --editor ends with 0 errors, 0 warnings (126 files), guard "all 113 Fusion/Somnium members are proven", editor runtime and editor tools builds 0 errors. One run in between printed "all 0 ... proven" while another module was mid-edit; the final run shows 113.
Harness run 1 (watch_menu.json, exit 0, all 25 steps ok):
- The menu builds in 641 ms after boot: 6 pages, 141 graphics.
- Fonts are Roboto (body) and Jost (headings). The wrist watch loads Rec Room's Wrist_Watch mesh (1163 verts) and Watch_Wrist_mat (AG/Avatar).
- In the editor it creates a StandaloneInputModule EventSystem.
- The desktop and VR layouts both open. Input is blocked by 'watch' while open and unblocked after closing.
- Back() returns to the previous page.
Screenshots compared with rr_watch_ui.md Â§0 and Â§3.2:
- Cream page with r20 corners, the 48 px shadow and the orange gradient nav bar.
- Nav bar: Profile on the left; Home, Play, People and This Room centred; Settings on the right; the active tab has the maroon plate.
- VR layout: taller bar, detach icon, world dimmed.
- Home tiles and quick access, the room details page (Paintball art and GO button) and the People, This Room and Settings pages all look right.
Run 1 showed three bugs, all fixed:
- Settings slider knobs were stretched into ellipses.
- The clock hands turned counter-clockwise (Unity is left-handed).
- The dwell ring used the mesh size without the prefab's 0.685 scale; it is now 40 mm.
Harness run 2 (watch_fix.json, exit 0, 9/9 ok) confirms the knobs are round and the hands match local time (07:16:45).
The only errors in either run were editor shader-import errors, not ours.
Not verified (needs a headset or the Somnium client):
- Glance detection and tap-to-open.
- Where the watch sits on the wrist and how the menu is placed while attached.
- Detach and re-attach.
- Clicks through the client's XRUIInputModule or XR ray.
- Freeing the cursor in the real client.
- The open, close and transition animations were not photographed mid-motion (the shots were taken after they settled).
### Issues
- Where the watch sits on the grip frame is a GUESS (LeftOffset (-0.015, 0, -0.1), Rec Room's anchor rotation). Also a GUESS: the wrist menu is 0.22 m above the face, 0.6 m wide. Both need tuning in a headset.
- Only headsets show the wrist watch. The screen-mode left hand does not exist, so desktop players see no watch model. Remote players' watches and the hologram shown while their menu is open are not built (they need a presence bit).
- Settings last for the session only; a TODO notes persisting them through Bridge LocalStorage.
- Desktop: the panel is world-space 0.8 m in front of the camera. It can clip into nearby walls (no ZTest-Always overlay yet).
- Play and Home cards list only RrRooms.All. In the harness only the placeholder Dorm and Rec Center are registered, so Play shows one card and 'Popular right now' is empty until the game modules register. The pages rebuild on Refresh, so new registrations appear automatically.
- Rec Room's look-only tabs (Customize, Create, Store, Chat, Notifications) are left out; Â§3.1 allows this. The Profile button opens People.
- Some sound pools are Rec Room GUESSes, and their clip names were not verified in this run (silent if missing): transition (287abc08), cloth.
- Detached VR menu: it can be closed only by re-attaching (a full wrist glance or the Collapse button), then lowering the wrist. No controller button is free.
### Requests
- Player (RrPlayer, RrInput): while RrInput.IsBlockedBy("watch"), play mode must not re-lock the cursor. On open the watch sets Cursor.lockState None and visible true, and restores both on close.
- Player: RrPlayer.InPlayMode and RrPlayer.UsingFallbackRig are used by RrInput and RrHand but were missing from RrPlayer.cs when I looked. The build now passes, so they may have been added since.
- Player: to show the watch on a desktop avatar, expose a read-only left forearm or wrist transform (for example the Bridge IK hand or bone). RrWatchModel would follow it without modifying the avatar.
- Core: add a small persistence helper (Bridge LocalStorage) so the Settings page can save volumes and watch options.
- Net: add a presence bit 'watch menu open' so remote players can show Rec Room's amber hologram rectangle (RemoteWatchMenu).

## gameskit
### API
Namespace SomniumSpace.Worlds.SomniumRecRoom.Games.

RrEnemy : MonoBehaviour (quest AI)
- Enums: RrEnemyKind {Melee, Ranged, Flyer, Charger, Exploder, Heavy, Thrower, Lunger} and RrEnemyState {Spawning, Idle, Move, Windup, Attack, Recover, Stunned, Dying, Dead}.
- Statics: RrEnemy Spawn(string spawnerPrefab, Transform parent, Vector3 worldPos, float yaw, Action<RrEnemy> ready = null, int id = 0); Find(int id); ResetIds(); ApplyRemoteHit(RrCombat.ActorHitInfo); All; bool ExternalSync; bool UseRigs; events OnSpawned, OnKilled(enemy, killer), OnDamaged(enemy, dmg, attacker).
- Instance: TakeLocalHit(int dmg, RrBodyPart part, Vector3 point, byte tool); Stun(float s); Die(int killer, bool points); RrActorState CaptureState(); ApplyState(RrActorState).
- Fields and properties: Id, Type, Kind, Elite, MaxHealth, Health, MoveSpeed, ProjectileSpeed, Dash*, Charge*, AttackRange, KeepMin/Max, Windup, ActiveTime, Cooldown, Damage, Splash, Burst, Volley, Hover, AggroRange, Points, Passive, Height, Radius, State, Target, Alive, Simulating, Center, ModelReady, HasRig.
- Numbers come from the spawner's QuestAIController.type and the QuestAiConfig_EnemyCommon, _Dash, _Projectile and _Charge configs. The archetype table follows rr_quests Â§3.4.
- Body: the skinned rig for types 0/5/21/9/19, driven by a PlayableGraph of the idle and walk clips, fitted to the spawner's height and given the spawner's material. Other types use the posed spawner Preview mesh. Hit volumes: body, head, and a weak spot for chargers and the heavy bot.
- AI: steering with obstacle and ledge probes, separation, ground snap. It uses the smoke-poof effect, coin effects plus Quest_Coins_Spawn01, and the hurt flash on _EmissionColor.

RrHealth : MonoBehaviour (one per world, for the local player)
- Statics: Local; Ensure(); CanAct.
- Methods: Begin(maxHealth, respawnDelay = 5, invincibility = 5); End(); int ApplyDamage(dmg, attacker, part, point, tool); Heal(n); GoOut(killer, tool, point); Respawn(bool teleport = true); Revive(health = 50, invincible = 3); Pose RespawnPose().
- Fields: InCombat, MaxHealth, Health, AutoRespawn, RespawnDelay, RespawnInvincibility, OutMovementScale, Func<Pose> RespawnPoint (the default is the room's WorldSpawn for the local team), Status, IsOut, Targetable, Invincible.
- Events: OnDamaged, OnOut, OnRespawned.
- Hits from other players arrive through RrCombat.OnHit, and going out sends Death. Stats go to RrPresenceStats.
- Hit feedback never touches the Somnium camera: desktop uses a red vignette on a screen-space overlay canvas (grey veil while out); VR uses our own quad placed 0.3 m in front of the head in onBeforeRender.

RrWeapon : MonoBehaviour (held gun)
- Statics: Create(prefab, parent, localPos, localRot, Action<RrWeapon>); Attach(RrInstance, prefab, Action<RrWeapon> ready); ShowRemoteShot(RrCombat.FireInfo), which draws another player's shot; Directions(spec, dir, seed, List<Vector3>), which builds the same pellets and spread from the seed on every client; All.
- Instance: TryFire(); bool FireToward(Vector3 point); Reload().
- Fields: Spec, Instance, Grabbable, Barrel, MuzzleOrigin, Ammo, Reserve, Reloading, ScreenHeld, Color? Paint, Hand, IsHeld, Ready, Tool. Event OnFired.
- Behaviour: grip, throw and pickup values from the spec; fire rate and auto rate; burst climb; magazine and reload (desktop RrInput.ReloadDown, VR right A, automatic when empty); one Fire event per trigger pull; muzzle effect, fire sound, haptics; team colour on the TeamColorRenderer targets. In screen mode the shot converges on what the aim ray hits.

RrMelee : MonoBehaviour (held melee tool)
- Statics: Attach(RrInstance, prefab); bool TryBlock(Vector3 center, float radius, out RrMelee by); All.
- Fields: EnemyMin/Max, PlayerDamage, MinSpeed/MaxSpeed, Crits, CanBlock, ThrowDamage/ThrowMultiplier, BladeBase/BladeTip/BladeRadius, ScreenHeld, TipSpeed. Values come from the MeleeWeapon config.
- Hits: the blade is swept each frame (capsule overlap plus a cast along the tip's path) against layer-27 volumes and the world. Damage is lerped by tip speed; head or weak spot counts double. Enemies go through TakeLocalHit, players through SendHit. Thrown damage applies for 3 s after a throw. In screen mode a click strikes along the aim ray within 2.2 m.

RrLoadout (static)
- Give(prefab, RrHandSide, Action<Component>): spawns the tool, attaches RrWeapon or RrMelee from its data, and puts it in the hand with RrGrab.Give. If there is no RrHand, our own camera-following "Rr Screen Hand" holds it instead.
- Also: GiveAll(prefabs, done); OnPhase(RrPhase, IList<string>); Clear(); Given; ScreenHand; ScreenHandOffset; Busy.

RrGameRoom: new protected virtuals Loadout, PlayerMaxHealth (0 = no health), RespawnSeconds, RespawnInvincibility. Playing starts health and gives the loadout; Lobby and MatchOver end health and clear the loadout.

RrCombat.SendActorHit(..., bool alsoSend, bool toAuthority = true).

RrCombatTest.Setup and RrCombatTest.Unleash: harness coroutines.
### Verified
Compile: `build.sh project --editor` ends with 0 errors and 0 warnings on the server-like build (126 files), "all 113 Fusion/Somnium members are proven", and 0 errors on both the editor-like runtime build and the editor tools build. No SKELETON markers remain in Games/Shared.

Harness: I used 2 executed runs of scenarios/combat.json. A third attempt was stopped by Unity before it started because Net/RrNet.cs had compile errors at that moment. The last run exited 0 with no exceptions from our code.

Proven by the run:
- The rifle "(PaintballAssaultRifle)" spec parsed as BurstFireGun, tool 1, 5/s, auto 12/s, 45 m/s arc, head 20 / body 12, AI 15, range 50 m, bullet [Paintball], decal and muzzle flash resolved.
- RrLoadout gave it to the Player module's RrHand Right through RrGrab.Give in 0.14 s.
- The goblin spawner loaded with 100 HP and 3 m/s from its config, height 1.28 m.
- The Goblin_Ground_PlayerTransformation rig loaded with its Goblin_Idle and Goblin_WalkCycle clips, was fitted x1.07 and grounded, and renders with Rec Room's green material and its own sword (combat_goblin.png). The spawner mesh is hidden, and the head and body hit volumes are on layer 27 (dump_enemy.txt).
- 9 projectiles were launched.

NOT proven: no shot hit the wall or the goblin (goblin health stayed 100 and combat_overview.png shows no splats), and the goblin found no target (it moved 0 m), even though the player was confirmed at (0, 0, -3) after my teleport. The gun is also not visible near the camera in the overview screenshot. The cause is not identified.
- One suspect is removed: FireToward no longer switches to Barrel.forward when the target point is behind the barrel.
- Diagnostics are added so the next run explains it: camera, gun, barrel and grip positions in the CombatTest log; shot origin and direction on the first shot; and a one-time "sees no local target" log with the reason.
- Also not yet exercised at runtime: melee, remote shots, and RrHealth respawn and vignette.
### Issues
- Open (runtime): in the harness the rifle's shots hit nothing and the goblin found no target, so hits, the kill, and the damage path to the local player are still unverified. Diagnostics are in place. Rerun with `bash SP/harness/run.sh combat` (scenario already written) and read the 'CombatTest:', 'Weapon: first local shot' and 'Enemy: ... sees no local target' lines.
- There is no enemy actor sync yet: every client simulates its own copy of each enemy (same ids in spawn order), and ActorHit is broadcast to all while RrEnemy.ExternalSync is false. Enemy attacks are decided per client, so a remote player never takes damage from another client's copy.
- Not implemented, GUESS items left out: projectile lead, arrow stun on basic goblins, heavy-bot beam as hitscan (a 100 m/s bolt is used instead), knock-back, boss types, and weapons in the rig's hand other than the goblin's own sword.
- Melee in screen mode strikes along the aim ray; the swing animation shows only when our screen hand holds the tool, not when RrHand holds it. Thrown melee damage is simple: a 3 s window after release.
- The VR vignette quad and our trails use Shader.Find('Universal Render Pipeline/Particles/Unlit'). This needs the shader to be included in the world build.
- Enemy sound names are best guesses from the research (Throw01, Quest_Coins_Spawn01, LaserPistol_01, Quest_Sword_Impactshield); missing clips fail silently.
### Requests
- Net: when RrNetActors lands, set RrEnemy.ExternalSync = true. Then have the instance authority publish enemy.CaptureState() at 10 Hz into the actor blocks, and call enemy.ApplyState(state) on the other clients, matching them by RrEnemy.Id. Also add the EnemyAttack event (rr_quests Â§3.6, proposed type 14) so the other clients see enemy attacks and can decide damage to their own player.
- Net: RrPresenceStats already looks up the presence setters by reflection; RrHealth publishes health, kills, deaths and status (Playing, or Down while out) through it.
- Editor module: include 'Universal Render Pipeline/Particles/Unlit' and 'Universal Render Pipeline/Unlit' in the build's shader references (used by RrHealth's VR vignette and RrProjectile's trails).
- Game modules (Paintball, Laser Tag, quests): override RrGameRoom.Loadout (e.g. { "(PaintballGun)" }) and PlayerMaxHealth (Paintball 1, Laser Tag and quests 100). Optionally set RrHealth.Local.RespawnPoint. Call RrEnemy.ResetIds() at the start of each act, then spawn enemies in the same order on every client.
- Interact: the contract's RrTools.MakeTool does not exist yet, so RrWeapon and RrMelee set their own grips (RightHandPickupTransform, mirrored for the left hand), throw values and pickup clip on RrGrabbable. Nothing is blocked.

