# iter15 - cleanup pass (2026-10-17)

Scope: the five items the integrator asked for (doors, Unlisted, settings persistence, Isle checkpoints, small audit fixes).
Compile: DevNotes/tools/compilecheck/build.sh project --editor. No harness.

Status log (append-only)
- baseline compile: 0 errors, 1 warning (ThirdParty Mirror.cs CS0618), guard 130/130, editor 0/0.

## 1+2. Doors and Unlisted (DONE, compiled 0 errors)
- Rooms/RrRoom.cs: `RrRoomInfo.Unlisted` (not offered as an activity card; travel still works) + `RrRooms.Listed(activity)`
  (registered, has Create, not Unlisted).
- Places/MakerRooms/RrRegistryMakerRooms.cs: MakerRoom `Unlisted = true`. No other placeholder needed it (every
  registration has a Create; RrPlaceholderRoom Dorm/RecCenter are real fallbacks).
- Honoured in: Watch/RrWatchPlayPage.cs (Shows(): no dorm, no Create-less, no Unlisted), Watch/RrWatchHomePage.cs
  (popular row), Places/Dorm/RrDormBoard.cs (PlayOrder), RrCategoryDoor.MagicCandidates, and the door/board lists
  (RrRoomListPanel.SelectTab drops registered-Unlisted items).
- Unregistered games (Rec Royale, Showdown, Midnight, Little Monsters) only appear as veiled COMING SOON cards
  (button not interactable, as before) and the Magic Door / Play / Home / Dorm Originals only take registered rooms.
  Punch Up removed from FeaturedList AND the legacy CreativeList (never released). The placeholder Rec Center
  (RrPlaceholderRoom, only used when the real Rec Center is missing) still has locked doors for them: locked portals do
  not open (RrPortal checks registration), left as is.
- Lists: SPORTS & PVP reordered so built games come first (Paintball, Laser Tag, Dodgeball, Paddleball, Soccer, Disc
  Golf, Bowling, then Rec Royale / Showdown COMING SOON). FEATURED ROOMS = Charades, Rec Rally, Stunt Runner, then
  Midnight / Little Monsters COMING SOON. Disc Golf / Stunt Runner / Rec Rally are registered, so they are pickable.
- MAKERWORLD / CAMPUS JAMS (decision): both list player-made rooms on the door list itself (and on the Dorm board's
  tabs, which reuse CategoryTabs). MAKERWORLD = other players' public rooms (RrMakerRooms.PublicRooms, busy first);
  CAMPUS JAMS = the player's own and co-owned rooms (RrMakerRooms.MyRooms, newest first) - mirrors the watch Rooms
  page's two tabs, so the two doors never show the same room. Both start with a "Maker Rooms" card that opens the
  watch on its Rooms page (Create, downloads). A room card = base map picture (RrWatchCreatePage.ArtOf), name,
  "by owner · base"; pick = RrMakerRooms.Open(id) (download first if needed; the list subtitle shows the download
  line and the list closes when travel starts). Rooms that cannot open now are veiled with the reason (PRIVATE /
  NOBODY ONLINE HAS IT). The cards rebuild when RrMakerRooms.Serial changes (at most every 1.5 s).
  Code: RrCategoryDoor.ListsMakerRooms / MakerRoomsOf; RrRoomListPanel Tab.MakerRooms, AddLeadCard, AddMakerCard,
  Veil, PickMaker, OpenRoomsPage, MakerStep; Pick(MakerRoom) opens the Rooms page instead of travelling.
  Watch/RrWatchCard.cs: Make() overload with art/title/subtitle (the old Make calls it; no behaviour change).

## 3. Watch settings persistence (DONE, compiled 0 errors)
- Watch/RrWatchSettingsPage.cs: new `RrWatchSettingsFile` ([Serializable], JsonUtility) + static `RrWatchSettings`
  (Load / Changed / Flush / Shutdown). File: Application.persistentDataPath/SomniumRecRoom/settings.json (same folder
  and .tmp-then-replace pattern as Player/Avatar/RrAvatarOutfit.Save; System.IO + JsonUtility already pass the upload
  check there). Saved: interface / effects / music volumes, Look brightness (RrPostFx.UserExposure) and warmth
  (UserWarmth), tap-to-open, right wrist, second hand. NaN/inf guarded, volumes clamped 0..1 (PostFx setters clamp).
- Every slider move / chip press captures the values (Changed) and writes 1 s later (page Tick), at once when the watch
  closes (RrWatch.RaiseOpenChanged(false)) and on world shutdown (RrWatch.Shutdown -> RrWatchSettings.Shutdown; the
  captured copy survives RrTravel.Shutdown resetting RrPostFx first).
- Restore: RrWatch.Init calls RrWatchSettings.Load() before the model/UI components are added; the page's sliders
  read the live values on Refresh, so they show the restored values. The old TODO (Bridge LocalStorage) is gone.

## 4. Isle of Lost Skulls same-act checkpoints (DONE as opt-in, compiled 0 errors; never run)
Judged safe: the restart reuses the framework's own act entry (EnterActAuthority: enemies cleared, waves/side waves/
hazards/boss ints reset, director re-seeded, intro timer) and the clients' own act arrival (EnterActLocal: up/healed +
teleport to the act's entry). The only missing piece was the client signal (the act int does not change); the stage
going back to Intro inside the same act is used, which no other path does (stage only moves Intro -> Combat -> Clear;
Crescendo's own Grand Hall checkpoint always changes act).
- Games/Quests/Shared/RrQuestRoom.cs:
  - `protected virtual bool RestartActOnWipe` (default false = old behaviour: wipe -> Finish(false)) and
    `protected virtual void OnActRestart(int act)` (every client, before the move back).
  - DownsAndFail: when RestartActOnWipe, after the team is all down for 1 s -> RestartActAuthority() instead of Finish.
    Skipped while the act is Clear (nothing to restart; the bleed-out gets people up) and during a grace period
    (intro + 2 s after a restart; 3 s after becoming authority) so members still getting up are not judged again.
  - RestartActAuthority: gold = min(current, gold at the act's start), MaxPossible = value at the act's start (the
    respawned waves would otherwise count twice in the rating) - GUESS of Rec Room's rule; only the authority that
    started the act knows the snapshot (OnAuthorityChanged clears it -> gold kept as is). Then EnterActAuthority(same act).
  - ActWatch (every client): stage back to Intro in the same act -> OnActRestart, EnterActLocal, toast "Back to the
    checkpoint!". Broken pots stay broken; weapons the players hold stay with them; weapon spots are not refilled.
- Games/Quests/LostSkulls/RrLostSkullsRoom.cs: RestartActOnWipe = true; OnActRestart resets the bridge fallback timer,
  cannon cooldowns, the Bar Fight toast (Banquet) and Ghost Beard (boss act: ResetBoss, the director spawns a new body
  and OnEnemyReady -> Setup). Class summary updated.
- Not opted in: Golden Trophy, Crimson Cauldron, Jumbotron (no checkpoints in Rec Room); Crescendo keeps its own.
- Consequence: an Isle run cannot fail any more (as in the final Isle); players leave through the watch.
- Untested (no harness this pass): two-client restart, a restart in the boss act.

## 5. Other small fixes from the top 25
- Games/Shared/RrEnemy.cs: stale "TODO(Net): when RrNetActors exists" summary rewritten (quests set ExternalSync;
  authority publishes CaptureState via RrQuestEnemies actor blocks, watchers ApplyState + EnemyAttack) - audit s3 #39.
- Checked, nothing to do: top-25 #3 (bankscene) - all 47 Data/*.bytes are referenced by Assets/Rec Room Scene.unity
  (guid check), so StuntRunner/DiscGolf/RecRally data is banked.
- Skipped: ThirdParty Mirror.cs CS0618 (RenderSingleCamera -> SubmitRenderRequest is a rendering change, not
  verifiable without the client); RrVersion bump to 15 + README "Where it stands" (integrator's release step);
  whitelist (release decision); everything else in the top 25 is features or needs the client.

Final compile: 0 errors, 1 warning (Mirror.cs CS0618, unchanged), guard 130/130, editor runtime 0, editor tools 0.
Status: DONE.
