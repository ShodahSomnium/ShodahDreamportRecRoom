# isolate - progress (phase 1, 2026-09-24)
Task: hide players who are not in the same place as you (locally), mute their voices.
Owns: CODE/Player/RrRemotePlayers.cs, CODE/Player/RrIsolation.cs (new), CODE/Player/RrIsolationTest.cs (new), SP/harness/scenarios/isolation_*.json

## Status
- [ ] started: reading ARCHITECTURE hard rules, RrRemotePlayers, RrNet presence, RrWatchWorld WithMe

## Client findings (CLIENT = E:/Somnium Space/Main Build/Assets/_SomniumSpace), 2026-09-24
Prefab trees dumped with SP/research/player_probe/ptree.pl into BASE-session scratchpad iso/tree_*.txt.
A REMOTE player is spread over FOUR places, only one of them under Body.Root:
1. Body prefab `Player.XR.Body.Simulator` / `Player.NonXR.Body.Simulator` = **Body.Root.parent** (PlayerGenerator.cs:126 instantiates parts under the PlayerManager).
   - Body.Root (`Root` / `NonXR.Body`, L2): CharacterController (enabled, r 0.15, h 1.35*scale: PlayerTransformNetworkDriver.cs:255-262) + kinematic RB. Remote pose set by transform.position (PlayerTransformNetworkDriver.cs:69,203), so the CC is only a collider.
   - `XR.Simulator.PlayerTrigger` / `PlayerTrigger` L15 SphereCollider trigger r 0.18 at the head (passive, PlayerTrigger.cs).
   - VR: `Left/RightHandVRIKPoint` L30 kinematic RB + SphereCollider r0.5 **disabled**; `Palm Collider` L30 BoxCollider on an inactive GO; `Cube` debug renderers inactive.
   - `AvatarFeedbackVFX` (4 VisualEffect+VFXRenderer, 1 AudioSource), desktop `EmoteParticleSystem`, `Navigation` arrow (TMP MeshRenderer + SpriteRenderer, inactive).
   - **Name tag** `SocialSystem.User.Tag` instantiated under Body.Root.parent (InfoFeature.cs:67-72, 86): `UICorner/Canvas` has Canvas + CanvasGroup (+ nested ToolTip Canvas). Client writes Canvas.enabled on every frustum enter/exit and settings (InfoFeature.cs:272-286 -> PlayerTagView.cs:243-255) and CanvasGroup.alpha only from QuickChat (QuickChatFeature.cs:120 -> InfoFeature.cs:142-146 -> PlayerTagView.cs:257-261). => hide the tag with CanvasGroup.alpha=0 + blocksRaycasts=false, leave Canvas.enabled to the client.
   - **Quick chat bubble** `QuickChat.Bubble` under Body.Root.parent (QuickChatFeature.cs:28-34); FadeView, "visibility is CanvasGroup alpha only" (QuickChatBubbleView.cs:11) => hide with Canvas.enabled=false.
   - VR profile display under Body.Root (InfoFeature.cs:70,92); DESKTOP: under the LOCAL player's ProfileDisplaySpawnPoint (InfoFeature.cs:73-74) -> never touched.
2. **Avatar model** `Avatar for Player [<NickName>] [<LongId>]` (AvatarManager.cs:105), `Instantiate(prefab)` with no parent for remote players (AvatarSpawner.cs:30-33) = a scene ROOT object, NOT under Body.Root. MAIN / DUMMY / CULLING holders under it (AvatarsCullingSystem switches holders with SetActive, BaseAvatarRoot.cs:199-202). Avatar AudioSources are destroyed (MainRoot.cs:134). Object wearables parent to bones (WearableBase.cs:139-142). Cloth wearables `Simulator Item: [..]` (InventoryItemController.cs:73) are scene roots whose armature is re-parented to avatar bones (ClothJoint.cs).
   Client renderer.enabled writers: AvatarRootsLODHandler.cs:87-101 (LOD setting), WearablesRoot.cs:36, ClothBone.cs:120/128 -> caught by the periodic re-scan.
   Quake's Q3Remote (walks Body.Root only) therefore does NOT hide the avatar in this client version.
3. **Voice** `Voice.Speaker` (RemoteVoiceSpeaker + AudioSource + Photon Speaker + LowPass) lives in the NETWORK part `Player.XR.Part.Network` (Body/Head/Voice.Speaker) / `Player.NonXR.Part.Network` (Body/Root/HeadOffset(Fake one)/Voice.Speaker), a Fusion NetworkObject whose StateAuthority = the player (NetworkAgent.cs:71-73). Found via FindObjectsByType<NetworkObject> + StateAuthority.PlayerId (both proven).
   AudioSource.mute writers: RemoteVoiceSpeaker.SetMuted (user mute button) / SetBlockValue (block) only (RemoteVoiceSpeaker.cs:48-58, 74-78); volume: SetVoiceLevel (profile slider / Bridge); enabled: VoiceCullingManager (range/speaking); maxDistance: amplify/boost. Nothing writes mute per frame => mute is stable.
4. Culling: AvatarsCullingSystem.cs counts the nearest 8 within 15 m (distance only) -> hidden players at the origin still take "near" slots (cannot fix without touching the client).
Collision: layer 2 hits 2 -> a remote CC at the origin blocks the local CC and the local VR hand boxes (L7 hits 2). Proven Bridge `Motion.SetCollisionDisableState(true)` excludes layer 2 from the local CC (MotionFeature.cs:180-191) - global, a request for the player task. Hidden remote: disable its colliders locally (approved).

## Design decisions (2026-09-24)
- Hook: RrRemotePlayers.Tick -> RrIsolation.Tick every frame (LateUpdate order 10000, after the client); RrRemotePlayers.Reset -> RrIsolation.Shutdown (restores). No RrMain/RrPlayer edit needed; lazy start.
- With me: my place = RrTravel.Current/CurrentInstance (+ Busy or my presence Loading = travelling -> nobody); theirs = RrNet.Presence(id). Apart if invalid presence, their status Loading, other activity, my activity unnetworked (Dorm) ; arrival room (None) together; else same instance number. Hysteresis: hide after 0.5 s apart, show after 0.35 s together; first decision immediate.
- Renderers: Renderer.forceRenderingOff (client never writes it: only Crest uses it) instead of enabled, so the client's own enabled toggles (LOD, wearables) survive untouched -> exact restore.
- Tag: CanvasGroup alpha 0 + blocksRaycasts false (client writes alpha only from QuickChat; restore value follows client writes seen while hidden). Bubble / other canvases directly under the body prefab (not under Body.Root): Canvas.enabled false.
- Colliders: Collider.enabled false (remote CC, PlayerTrigger, anything under body/avatar). Voice + other AudioSources: mute true, restore false only on those we flipped (user-muted players stay muted).
- Discovery sweep (<= 1/frame, throttled): scene roots "Avatar for Player [nick] [" (raw Bridge nick, never the local nick, never ambiguous), "Simulator Item:" roots skinned to a hidden avatar or owned (NetworkObject.StateAuthority) by a hidden player, FindObjectsByType<NetworkObject> (skip "Rr Net*"/"Scene Networking") for voices.
- Gaps in other modules (requests): RrQuestDirector.cs:124-135 zone check, RrEnemy.cs:1334-1356 targeting, RrTools.PlayerNear (RrTools.cs:550, used by RrRespawnable.cs:103) iterate all remotes without instance filter.

## Files
- [x] CODE/Player/RrIsolation.cs (new) - compiles, 0 errors, all 113 members proven
- [x] CODE/Player/RrRemotePlayers.cs (Tick -> TickList + RrIsolation.Tick; Reset -> RrIsolation.Shutdown; Remote.WithMe; RawNick)
- [x] CODE/Player/RrIsolationTest.cs (new) - compiles (meta 5c9763e2...)
- [x] SP/harness/scenarios/isolation_basic.json (written; first harness run pending)

## Harness run 1 (2026-09-24): 14/16 checks pass
- FAIL restore exact / dorm restore exact: test bug (the twin 902 correctly stays hidden in its Dorm; the comparison expected it restored). Fix: Together() brings the twin back too.
- Cost (editor play mode): first sweep 3.87 ms + first hide 3.96 ms (126 renderers) in ONE frame (cold); warm hides 0.50 / 1.21 ms.
  Fix: sweep and hide in separate frames, per-frame budget 1.5 ms for apply+maintain+sweep (at least one item), MaxApply 2, MaxMaintain 2; stress test with 48 fakes added.

## Requests for other owners (drafted)
1. games: filter by `r.WithMe` (RrRemotePlayers.Remote.WithMe / RrIsolation.IsWithMe): RrQuestDirector.cs:124-135 (zone occupancy counts every remote), RrEnemy.cs:1334-1356 (targets any remote not Down/Spectating), RrTools.PlayerNear RrTools.cs:550-567 (used by RrRespawnable.cs:103).
2. player/place (optional, Rec Room behaviour): `RrPlayer.Motion.SetCollisionDisableState(true)` (proven) so the local capsule ignores every player capsule; isolation already disables hidden players' CCs.
3. watch: RrWatchWorld.cs WithMe says nobody is with you in the arrival room; RrIsolation says arrivals are together (task rule). Use RrIsolation.IsWithMe for one answer.
4. place: no RrMain/RrPlayer line needed (RrRemotePlayers.Tick/Reset drive it). Optional explicit `Player.RrIsolation.Shutdown()` in RrMain.OnDestroy before RrNet.Shutdown is harmless (idempotent).
5. harness README (DN/tools/harness/README.md): list isolation_basic.

## Harness run 3 (final, 2026-09-24 11:18): isolation_basic 42/42 steps ok, "IsolationTest: PASS all (18 passed)"
- PNGs looked at: iso_visible (fake body/avatar/cloth/tag/bubble, orange twin, green local avatar), iso_hidden and iso_meddled (only the green local-avatar cube), iso_restored (all back; tag at the 0.5 alpha the fake client set while hidden).
- Timings: warm-up scan 0.82 ms at start; first sweep 3.10 ms (cold), first hide 2.76 ms (cold, 126 renderers, next frame); stress 48 players / 1440 renderers hidden 0.65 s after the change over 25 work frames, worst isolation frame 0.50 ms, warm sweeps 0.24-0.32 ms; restore 0.44 s, 0 differences in 1488 components.
- Changes after run 1: budget 1.5 ms/frame, sweep and hide in separate frames, MaxApply/MaxMaintain 2, places stored as structs (no strings per decision), warm-up scan.
- Compile: 0 errors in all three builds, all 113 Fusion/Somnium members proven.
## Status: DONE (untested: real two-client behaviour - see result)

## Review fixes (2026-09-24, started)
Review verdict needs_fixes: 1 major (voice mute collides with client mute/block), 4 minor (Tick starvation, RrMain shutdown order, MatchVoices never run in harness, culling slots).
- [x] major: voice silenced via AudioSource.volume (not mute); mute only for non-voice sources (code written 2026-09-24; Silence() in RrIsolation, Restore writes volume back only if still 0; test: Meddle blocks + sets 0.6 on the voice)
- [x] minor: RrRemotePlayers.Tick wraps TickList in try/catch so RrIsolation.Tick always runs
- [x] minor: requests to RrPlayer/RrMain owners (finally / explicit Shutdown first) - in the result (RrMain request now REQUIRED, not optional)
- [x] minor: two-client checklist (voice log line, Describe) + culling limitation: in RrIsolation class doc, result untested + readme_section (README not owned)
- Evidence re-checked in CLIENT (Network/Scripts/Voice): only VoiceSpeaker.SetVoiceLevel (VoiceSpeaker.cs:143-149) writes the voice volume (callers: RemoteVoiceFeature.SetVolume <- UserVolumeView slider, BridgePlayerVoice.SetVoiceLevel); VoiceCullingManager.cs:122 decides speaking from Photon Speaker.IsPlaying (not the output), so volume 0 does not change culling; RemoteVoiceSpeaker boost gain (OnAudioFilterRead) multiplies silence. Client mute writers: RemoteVoiceSpeaker.cs:78,100 and LoadedModel.cs:266 (admin item mute) only.
- Decision: exact 0 (not a 1e-7 marker): voices run at priority 16 (VoiceSpeaker.cs:26); a marker would keep real channels for inaudible people. Cost: a slider set to exactly 0 while hidden is lost on restore (documented).
- Non-voice sources keep mute (VFXAudioClipPlayer.cs:29 writes volume on every clip, so volume would leak until the next re-scan). Residual: an admin item mute on a hidden player's item is undone on restore (rare; documented).
- Compile after review fixes: 0 errors in all three builds, all 113 members proven.
- Harness run R1 (review fixes, 11:44): isolation_basic 44/44 steps ok, "PASS all (18 passed)"; re-scan shows "voice volume 0.00 mute True" (fake client blocked + set 0.6 while hidden); restore exact expects and got voice "mute True volume 0.60". PNGs looked at: same as run 3 (visible / only green cube / only green cube / all back, tag at 0.5 alpha). Cold first hide 5.15 ms this run (was 2.76: cold JIT variance on a shared machine); stress 0.83 s, worst frame 0.83 ms.
- minor (MatchVoices never ran): added TimedVoicePass + MeasureVoicePass() (test hook) + counts in Describe ("last voice pass N NetworkObject(s), V valid, O owned by hidden players, X ms"); logged once "first voice pass: ...". Two-client checklist goes into the result + readme_section.
- Harness run R2 (11:50): isolation_basic 46/46 steps ok, "PASS all (18 passed)". New: "first voice pass: 0 NetworkObject(s), 0 valid, 0 owned by a hidden player, 1.05 ms" (the harness scene has no NetworkObjects; 1.05 ms is the cold first FindObjectsByType call). First hide 3.34 ms cold. PNGs looked at: visible / hidden (green only) / meddled (green only; the sky changed because the place task's Dorm loaded around the camera mid-run - not isolation) / restored (all back, tag 0.5 alpha).
- Files touched in review fixes: RrIsolation.cs (Silence/Quiet*, ScanTree voice check, Restore volume, hide log text, TimedVoicePass/MeasureVoicePass, Describe voice pass, class doc voice + known limit), RrRemotePlayers.cs (Tick try around TickList, _tickErrors), RrIsolationTest.cs (voice volume 0.8, Meddle block + 0.6, snapshot mute+volume, expectations, voice pass log), isolation_basic.json (comment + 4 expect_log).
- Request line numbers re-checked: RrEnemy remote loop now RrEnemy.cs:1322-1345; RrQuestDirector.cs:124; RrTools.cs:550; RrHitVolumes.cs:162 already filters by match members (OK).
## Review fixes: DONE (untested: real two-client voice matching/audibility, block/mute via the real client UI)
