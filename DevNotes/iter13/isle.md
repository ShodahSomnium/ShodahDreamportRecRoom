# iter13 - isle (Isle of Lost Skulls, basic form)

## Plan
1. Read Jumbotron / Golden Trophy / Quests Shared to learn the pattern.
2. Research: DN/research/rr_latest_rgames_story.md (Lost Skulls facts), DN/refdata/apk/INDEX.md (levels 62-70).
3. Convert levels 62-70 with gtscene jt=1 + jumbotron_markers -> rrsk -> rrsk2bytes.sh -> CODE/Data/LostSkulls_*.bytes
4. Write Games/Quests/LostSkulls/*: Registry (RegisterLostSkulls), Room, Acts, Area, boss (simplified).
5. Compile check 0 errors.

## Log
- start (fresh, no prior notes)
- 14:21 tools: NSP/isle/tools (make_tools.sh), dumps NSP/isle/dump; wrote DN/iter13/isle_tools/isle_markers.pl + isle_convert.sh
- 17:20 RESUMED (2nd attempt). State found: NSP/isle/rrsk/Isle_*.rrsk exist (jt=1 + markers, NOT yet bytes) but far too big:
  3.5-60 MB raw each (176 MB total; Beach1 164k plates, SunkenShip 660k 0.2 m "C box" collider plates). gz ratio ~5.6x
  (Hallway1 3.47 MB -> 615 KB). Budget 3 MB gz => ~17 MB raw => ~16-20k plates + ~4k C per room.
  PLAN: new DN/iter13/isle_tools/isle_thin.pl: cluster-merge (material, cell S, normal bin, plane offset) of G plates of
  busy materials and of the 0.2 m C box plates, S grown until the room budget holds; then rrsk2bytes. Runtime: reuse the
  public RrJumbotronArea (+Hull/Haze) for building each level (no code copy), own LostSkulls room/acts.
- 17:45 isle_thin.pl written + tested (Hallway1 top render before/after identical layout: NSP/isle/thin/h1_top_*.png);
  isle_bytes.sh = thin (per-room caps 11-13k plates, 3k collider plates) + drop the gates' AreaBlockingGeo (9.2x41x58 C box)
  + rrsk2bytes. Thin run -> NSP/isle/thin (Foyer..Caves1 done: 7.6-11.8k plates each).
  Code plan (Games/Quests/LostSkulls): Registry, Room (RrQuestRoom), Acts (Foyer lobby + 8 acts on the levels' Encounters
  via the public RrJumbotronArea / RrJumbotronWeaponSpot), Area helpers (jail cell door + lock, gates dropped open),
  Cannon (pressable fuse -> ball), Bridge (ShipDeck drawbridge), GhostBeard (3 cannon hits). Roster:
  PirateGroundMelee2020 / PirateExplosiveThrowing / PirateBottleThrowing / PirateFlintock2020 / PirateGibbet (+boss body).
- 18:05 DATA WRITTEN: CODE/Data/Isle_{Foyer,Hallway1,Hallway2,ShipDeck,Beach1,Beach2,Caves1,SunkenShip,BossArena}.bytes
  (+.meta), 3.29 MB total; re-running Foyer/Hallways/BossArena/SunkenShip with tighter caps (isle_bytes.sh list) to get ~3.0 MB.
  Arrivals (Player7SpawnPoint centres): Hallway1 145.53,-3.44,49.81; Hallway2 27.57,-5.99,64.54; ShipDeck -161.71,5.57,-23.80;
  Beach1 -64.58,-4.56,-259.85; Beach2 -178.93,13.90,110.53; Caves1 -55.73,-2.38,-59.64; SunkenShip 181.64,-10.83,-267.77;
  BossArena -172.15,-4.06,161.17. Foyer: hologram -68.29,-6.47,148.62; RR door -56.81,-6.47,151.80; spawns z 158.9.
  NEXT: write the code (registry, room, acts, cell/gates, cannon, bridge, Ghost Beard), compile.
- 18:40 CODE WRITTEN: Games/Quests/LostSkulls/ RrLostSkullsRegistry, RrLostSkullsRoom, RrLostSkullsActs (Foyer + 8 acts),
  RrLostSkullsCell (brig door + lock), RrLostSkullsExit (quest door on the [DoorTrigger] pad), RrLostSkullsLadder (rope
  ladders), RrLostSkullsCannon (fuse button, 45 deg auto-aim), RrLostSkullsBridge (High Seas drawbridge + target),
  RrGhostBeard (3 cannon hits, fight positions 1/2/3 = rear/front/either by the cannon arcs) + .meta files.
  Data re-thinned: 3,084,815 B total. Compile: the live project has 2 errors in Games/Quests/Crescendo (other agent, in
  progress); a copy WITHOUT Crescendo compiles 0 errors (1 warning = ThirdParty Mirror.cs), API guard 118/118.

## Result (iteration 13, isle) - Isle of Lost Skulls in basic form

Data (CODE/Data, loaded with RrSceneBank.Text("Isle_<Room>"); the integrator's bankscene.sh must add them):
| file | bytes | plates (jt=1 -> thinned) | collider plates |
| --- | --- | --- | --- |
| Isle_Foyer.bytes | 251,350 | 28,018 -> 7,524 | 17,634 -> 2,446 |
| Isle_Hallway1.bytes | 263,182 | 34,885 -> 10,166 | 2,660 |
| Isle_Hallway2.bytes | 247,948 | 31,765 -> 8,969 | 3,447 -> 2,516 |
| Isle_ShipDeck.bytes | 361,773 | 149,381 -> 11,771 | 175,632 -> 2,785 |
| Isle_Beach1.bytes | 395,567 | 163,886 -> 10,913 | 164,598 -> 2,963 |
| Isle_Beach2.bytes | 347,394 | 156,927 -> 11,672 | 271 |
| Isle_Caves1.bytes | 415,066 | 58,782 -> 7,632 | 202,085 -> 2,713 |
| Isle_SunkenShip.bytes | 537,748 | 130,196 -> 9,684 | 659,820 -> 4,173 |
| Isle_BossArena.bytes | 264,787 | 98,987 -> 7,007 | 86,273 -> 2,187 |
| total | 3,084,815 | | |
Pipeline: DN/iter13/isle_tools/isle_convert.sh --no-bytes (gtscene prep jt=1 + isle_markers.pl; rrsk in NSP/isle/rrsk)
-> isle_bytes.sh (isle_thin.pl per-room caps + the 8 AreaBlockingGeo boxes out + rrsk2bytes --quant 3).
Offline checks: top/eye renders before vs after thinning (NSP/isle/thin/h1_top_*.png, b1_before/after.png): same layout,
curved art (palm crowns, boat hulls, rocks) blockier. Every encounter name the acts use has its trigger and points in the data.

Code (Games/Quests/LostSkulls, namespace ...Games.Quests.LostSkulls; reuses the public RrJumbotronArea / RrJumbotronHull /
RrJumbotronWeaponSpot, no copy):
- RrLostSkullsRegistry: RegisterLostSkulls (1-3 players, Quests, Ghost Beard polaroid, PirateShoppingChannel, networked).
- RrLostSkullsRoom (RrQuestRoom): roster PirateGroundMelee2020 / PirateExplosiveThrowing / PirateBottleThrowing /
  PirateFlintock2020 / PirateGibbet / cannoneer (flintlock, still, half HP) / Ghost Beard body; gold 500/1000/10000, penalty
  2000, stingers PirateQuestHeroesAreVictorious / PirateQuest_Fail, FailSeconds 10, music Shanty2 / PirateBrawl01; lobby +
  8 acts on a 400 m grid (ActSlot(i) = ((i%4-1.5)*400, 0, 400*(1+i/4)), act-local origin = arrival); only the current
  act active; lighting RrEnvironment.SetArea("Isle_<Room>") + RrPostFx area act+1; hazards: lock (bit 1), cannon fuse
  (authority resolves, quest int 16 counts shots for every client's visuals); High Seas needs the bridge down
  (fallback: drops by itself 20 s after the pre-boarding waves are dead); overboard check (25 m under the arrival ->
  back to the entry); harness hooks TestConsoleStart / TestBreakLock / TestFireCannons / TestFightClear / TestAct2..7 / TestBoss.
- RrLostSkullsActs: Foyer (console on [ScoreboardProjector] (1), REC CENTER portal on [ActivityDoor], practice weapons)
  and the acts Galley, Pirate Banquet (Bar Fight), High Seas, Port Plunder, Gibbet Jungle, Caves, Sunken Ship, Ghost
  Beard's Gauntlet with waves on the levels' Encounters (wiki order), found weapons at every weapon_pickup, rope ladders.
- RrLostSkullsCell (brig door + padlock button), RrLostSkullsExit (quest door logic on the [DoorTrigger] pad + gold glow),
  RrLostSkullsLadder, RrLostSkullsCannon (fuse button, 45 deg auto-aim, ball + blast), RrLostSkullsBridge (drawbridge
  + target), RrGhostBeard (primitive ghost, 3 cannon hits; fight positions 1/2/3 lie in the rear / front / both cannon
  arcs, so the wiki's order falls out of the geometry; cannon shots every 5 s; boarders after each hit).

Compile (19:05, live project after the crescendo agent fixed its files): 0 errors, 1 warning (ThirdParty Mirror.cs), API guard
118/118, editor runtime + tools 0 errors. Earlier (18:40) the live project had Crescendo errors only; a copy
of the project without Games/Quests/Crescendo = 0 errors, 1 warning (ThirdParty Mirror.cs), API guard 118/118.

Shared requests (integrator):
1. Run DN/tools/rrscene/bankscene.sh once (9 new Data/Isle_*.bytes).
2. Checkpoints (RrQuestFinalRules: Isle.Checkpoints = true) are not in RrQuestRoom: a team wipe ends the run. Suggested
   (RrQuestRoom, the all-down branch "else if (Time.time - _allDownSince > 1f)"): when FinalRules != null &&
   FinalRules.Checkpoints && ActIndex > 0 -> _allDownSince = -1f; SetQuestInt(RrQuestInts.Score, 0);
   EnterActAuthority(ActIndex); Broadcast("Back to the checkpoint!"); return; - plus, on clients, StageChanged(Intro) of
   the SAME act must run EnterActLocal (revive + teleport), since the act int does not change.
3. README.md: a short "Isle of Lost Skulls" section (the table in RrLostSkullsRoom's summary).

Open (basic form): gates stay open (no gate encounters); no mines / puzzle pieces / torch darkness / box stacking; gibbets take
any damage; thrown bottles/mugs are grabbable props only; cannons auto-aim (no handle); Ghost Beard is our own shape;
the Swash and Buckle shop is not built; not tested in play mode (the integrator's tour).
- 19:05 final: OpenGate AABB fixed for the 45-degree Hallway1 gate; boss act's unused exit door object destroyed; live
  compile 0 errors (editor 0). DONE (not play-tested; harness steps in the StructuredOutput / below).
Harness (integrator, room frame; act i root = ActSlot(i), local origin = arrival):
  {"goto": "LostSkulls", "timeout": 300} / {"expect_log": "Isle of Lost Skulls built", "timeout": 30} /
  TestConsoleStart + RrQuestTest.Start / act 1 brig shot pos [-600,1.6,400] look [-596,1.0,398.2] / TestBreakLock /
  TestFightClear then RrQuestTest.NextAct per act / High Seas bridge shot pos [200,1.6,400] look [218.6,3,386] /
  TestBoss + TestFightClear / expect_log "VICTORY".
