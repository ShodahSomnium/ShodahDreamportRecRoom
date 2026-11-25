# gtrules - quests' FINAL rules in the shared quest framework (iteration 4)
Owner: CODE/Games/Quests/Shared/*, small quest-only additions in CODE/Games/Shared/* (RrHealth.Accepts hook,
RrGameRoom.PhaseAnnouncement hook, RrAnnouncer.Say overload). Started 2026-09-24 ~16:50.

## Status
- [x] code for 1-7 + mods written (see below)
- [x] compile 0/0, all 113 members proven (after waiting out another agent's dorm errors)
- [x] harness runs 2/2: gtrules.json 46/46 ok; goldentrophy_run.json 34/34 ok (old level, one hit downs + guard, VICTORY rating C by fixed totals). DONE.

## What landed (framework; NO edits needed in GoldenTrophy/* for any of it)
- New `Shared/RrQuestFinalRules.cs`: per-RrActivity final rules (GT/CC/Jumbotron 1-4, Isle/CBM 1-3; S/A/B totals
  GT 150k/125k/100k, CC 300k/275k/250k, Jumbotron 150k/125k/100k; GT kill gold by spawner name 450/500/850/850/1500/
  2000 and boss 5500). RrQuestRoom.Build applies it right after CreateDef() to fields the def left unset.
- RrQuestDef: PlayerHealth default 1 (one hit downs), ReviveHealth 1, MinPlayers/MaxPlayers, KillPoints list +
  AddEnemy(spawner, points) + SetKillPoints + KillPointsOf, BossType (-1 = whatever PointsKind shows as PointsBoss),
  BossKillPoints, BossDeathKillsAll, RatingGoldS/A/B + FixedRating, RecRoomMaxGold, StartLine ("Gameon"), Scaled().
- RrQuestRoom: PointsFor default = boss gold / kill gold / max HP; IsBoss(e, type); AuthorityBossDefeated(boss)
  (kills the rest, no gold, drops remaining waves; called automatically when the boss dies); party clamp and wave
  scaling by MaxPlayers; MinPlayersToStart = Def.MinPlayers; announcer silent except Coach's Gameon at Playing (logged
  "Coach: "Game On!" (Gameon, x s ...)"); HUD shows only DOWN; ally melee hits never down (RrHealth.Accepts);
  logs "final rules: ..." and "level potential with 4 players: about N Gold ..." at build.
- Mods (`Shared/RrQuestMods.cs`): int 6 bits Turbo 1 / Coconut Chaos 2 / Laser Weapons 4, ReqMods request (lobby
  only), event 70 Coconut; board = `ModBoard(parent, local, yaw)` (public on RrQuestRoom).
- Racks: `RrQuestRack.CreateOnWall(parent, floorPointAtWall, yaw, label, spacing, weapons...)`,
  `RrQuestRack.DressingRoomWall(parent, pos, yaw, sword, shield, bow)` = 4 swords, 2 shields, 2 bows; racks swap to
  Laser Tag guns themselves while Laser Weapons is on; `Restock()`, `Count`.
- Harness: RrQuestTest.Start sets RrQuestRoom.TestGuard (hits logged, not counted). New hooks RrQuestTest.Rules,
  KillBoss, ModsAll, ModsOff. RrQuestTest.Down still downs (GoOut when guarded).

## For gtlevels (API you can use; nothing renamed or removed)
- Nothing is required. Optional: in CreateDef set `d.BossType = King;` (explicit boss; today the King is found via
  your PointsKind). Kill gold/ratings/caps already come from the catalog.
- Dressing room: `Racks.Add(RrQuestRack.DressingRoomWall(t, new Vector3(x, 0, zWall), yaw, Sword, Shield, Bow));`
  (the wall is at the rack's +z; players stand at -z). Or CreateOnWall with any list.
- Lobby: `ModBoard(t, localPos, yaw)` next to the start button places the RRO mod board (2.7 m wide table).
- Boss: AuthorityBossDefeated(e) runs by itself when the King dies; calling it again is harmless.
- Harness: one hit downs now; RrQuestTest.Start turns on a guard so scripted runs do not fail at the first hit.
- Optional tunnel spawns (act 4 two side tunnels +2 goblins each, act 6 left tunnel +2 elites): `act.SideWave(center, size).Add(EliteGoblin, 2, "tunnel")` - once per act when a player enters, any order, the act never waits for it; paid only if killed. Quest ints: 16..30 are yours (RrQuestInts.LastQuestSpecific = 30); 31 is now the framework's side-wave bits, 6 the mods.
- Racks: HangRotation(prefab) is public; bows now hang side-on (90 about y).
- FYI: my metas.sh run also wrote .meta files for your new GoldenTrophy/Scenes/RrScene_GTact*.cs (random GUIDs).

## Run 1 (gtrules.json, 17:15) - 0 failures of 46
- final rules line OK; kill gold [450,500,850,850,1500,2000,2000], boss 5500; rating edges 150000:S 149999:A 125000:A
  100000:B 99999:C; Coach "Game On!" loaded (Gameon, 2.00 s, 44100 Hz); guard stopped a 20-damage hit; coconut drop
  logged; boss death killed 4 adds; VICTORY 6400 Gold rating C by the fixed totals.
- LEVEL POTENTIAL (old level, 4 players): about 70,750 Gold from 105 wave enemies + 46 pots -> S (150k) is out of
  reach; Rec Room's best ~180k. gtlevels' waves need ~2.5x the gold (or it is accepted that ranks are hard).
- Look: mod board fine, lit lamps read white -> saturated green 1.1; title moved onto a taller back board. Wall rack:
  swords/shields fine, BOWS edge-on (limbs at the viewer) -> HangRotation: bows 90 about y (unverified); laser guns
  lay flat and overlapped -> barrel-up side view (unverified).
- Side waves (in the run, not exercised): RrQuestAct.SideWave(zoneCenter, zoneSize).Add(...) (optional tunnel spawns, int 31).

## Run 2 (goldentrophy_run.json) - 0 failures of 34
- Lobby, racks, acts 1-4, boulder, boss, victory unchanged; "final rules" and Coach "Game On!" lines present.
- Final compile after the look fixes: 0 errors / 0 warnings, 113 members proven, editor builds 0 errors.
- Still unverified: bow side-on hang, laser guns barrel-up, green lamp colour (all after run 1); networked mods,
  coconut model/blast in a shot, side waves.
