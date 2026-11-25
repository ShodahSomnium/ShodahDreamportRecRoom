# Dreamport: Rec Room - FINAL (pre-shutdown) versions of the action/sports activities

Status: COMPLETE (2026-09-24). Research only; nothing in CODE was changed.
Activities: Paintball, Laser Tag, Rec Royale, Rec Room Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling.

"Final" = the state on 1 June 2026, when Rec Room shut down (user decision 3). This document only ADDS to or CORRECTS
the existing research (rr_shooters.md, rr_sports.md, rr_party.md, rr_quests.md, rr_watch_ui.md); it does not repeat it.

Sources and working files (reuse, do not redo)
- Steam news, all 564 items 2016-06-30 .. 2026-04-06, split one file per item:
  `SP/research/rga/news/<yyyy-mm-dd>_<gid>.txt` (index `SP/research/rga/news_index.tsv`, splitter `SP/research/rga/split.pl`).
  NOTE: from mid 2025 most weekly Steam posts only say "read the ship notes on recroom.com/ship-notes"; the wiki's
  News Archive pages copy the full ship notes, so both were searched.
- Fandom wiki wikitext (fetched with curl from `https://rec-room.fandom.com/api.php?action=parse&page=<P>&prop=wikitext&format=json`):
  `SP/research/rga/wiki/<Page>.txt` (activity pages, News_Archive_2021..2025; there is no 2026 archive page).
- Rec Room developer blog "Showdown" parts 1-3 (2022-08) via the Wayback Machine (`web.archive.org/web/2023id_/https://blog.recroom.com/posts/showdownN`):
  `SP/research/rga/blog/showdown1..3.txt`. recroom.com itself is a parking page since the shutdown; the Wayback
  CDX list of recroom.com/ship-notes shows no ship note after 2025-09 that is missing from Steam.
- Bundle dumps made for this doc: `SP/research/rga/cfg/*.txt` (game-rule wrappers of all nine activities),
  `SP/research/rga/pgs.txt` (the Paintball match summary page).
- `SP` = `C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad`.
- Bundle facts: `SP/rrtool/idx` (objects.tsv, containers.tsv, mbscripts.tsv) and rrtool. `MAIN`/`TOOLS` = bundle
  `43ea1176ca25c0333547813ec3298808` as in the other docs.
- GUESS marks anything inferred rather than read.

## 0. Summary

Key point for every activity: **the plain bundles in the install ARE the final (April 2026) build**, so every number
the existing docs read from bundles (weapon stats, game configs, manager tuning, the Rec Royale loot table) is already
the final version. What the existing docs could miss is feature and rule changes that live in room content (the
encrypted levels / circuits): those come from the ship notes, and they are listed here.

| activity | final state (June 2026) vs the existing docs | build status | most important changes for builders |
| --- | --- | --- | --- |
| Paintball | the only activity with major late changes: new shared lobby (2025-10-14: shooting gallery, 1 v 1, store, lockers), **Merry Midway** 7th map + **Splat Hammer** (2025-10-28), **mods** (Wormz, Zero Gravity, Sniper Only, then Shotguns / Hammers Only, Fiesta; Mod Board + RRO Mod Tickets 2025-12), match summary page (bundle 77ea7f3e key 6), sprays, battle passes, beacons, head+torso-only hit zones (2025-04) | built: 2 of 7 maps | add 5 maps; Drive-In needs the paint thrower + mines; grenade splash hits teammates; mods; summary page; only the selected map should exist (at the origin) |
| Laser Tag | unchanged content since 2018; 2019 rules (self-destruct -10, grenades no friendly fire but self damage, 3 s spawn invincibility), 2023-01 small layout tweaks + lobby buckets; 480 s (bundle) not 10 min | not built | use the bundle 480 s; add the 2019 rules; team radio |
| Rec Royale | content final since 2020-12 + 2021-01 balance (bundle already has both); fixes to 2024; **needs 14 (solos) / 18 (squads) players to start** | not built | decide a lower start threshold for Somnium; chests clustered at Locales; ocean = instant down |
| Showdown | final = launch week 2022-08 (+08-16/17 balance) ; dev blog adds: no dual wield, HUD with team hits + timer, music speeds up near the end, Deputies vs Outlaws | not built | 25 hits / 4 min / 3 v 3, nerfed Blaster and Repeater, 3 Blasters + 2 Repeaters spots |
| Dodgeball | **2025-01: catch-outs removed** (a catch puts nobody out; only the hit player is out); 2021-04: screen charge no longer changes throw speed; **2026: barriers behind each side, Mod Board with Low Gravity, Dodge-Bombs, Disco Dodgeball** | built | set `CatchOutsThrower = false` and drop the teammate return; constant 14 m/s screen throw; add the barriers; mods |
| Paddleball | unchanged since 2016 except fixes (last: 2025-09 mid-court freeze); strictly 1 v 1, VR only | built | build's 2 v 2 and screen mode are Somnium extras (keep or restrict) |
| Soccer | unchanged since 2019: 3 min, speed-boost orbs, no shields | not built | leave `(SoccerShield)` out |
| Disc Golf | courses unchanged since 2017; 2019 water no longer KOs players (Lake); 2024 screen throw aims at the cursor | not built | aim the Non-XR throw at the crosshair |
| Bowling | unchanged since 2018; pool table playable since 2020-02; 2025 roll-sound fix | not built | add the working pool table |

Nothing in these nine activities was removed before the shutdown (no ship note retires a map, mode or room). Seasonal
content (Winter Clearcut, battle passes, Winterfest) is not part of the June 2026 state. March 2026: token rewards at
the end of RROs stopped.

Untested / unverified: everything here is desk research; no in-game check is possible any more. The mods' exact
effects (Fiesta, Dodge-Bombs, Disco Dodgeball, the Merry Midway mod), the 2025 lobby layout, the Merry Midway paintball
layout, the 2023 Laser Tag layout changes and the final map rotation are not documented anywhere I could reach
(recroom.com is a parking page now; Reddit blocks scripted access; YouTube captions could not be fetched).

## 1. Paintball (^Paintball; built: lobby + River + Sunset Drive-In, CTF + Team Battle)

### 1.1 What changed in the final years (additions to rr_shooters.md 1.1)
rr_shooters.md section 1.1 already covers 2016-2020 and names Merry Midway and the 2025 mods. New or corrected items,
all from the ship notes (Steam news files by date, wiki News_Archive_2021..2025):

| date | change |
| --- | --- |
| 2021-06-02 | invincibility bug in RROs "especially Paintball" fixed (another exploit fixed 2026-01-22) |
| 2021 | Sunset Drive-In added to the "5 hit streak" weekly challenge; Spillway spawn-without-a-gun and under-floor respawn fixed |
| 2021-12 / 2022-12 | **Winter Clearcut** (snow-covered Clearcut) for the Winter Wonderland season only: seasonal, not the June 2026 state |
| 2021 / 2023-10 | vote-kicking opponents during a match disabled in Paintball, Laser Tag, Dodgeball |
| 2024-11-14 | **Beacons** (team/party pings) for screen players: double tap (mobile), double RB (pad), middle click (PC); VR on 2025-01-14 (point + trigger) |
| 2025-01-14 | scope usable again while prone |
| 2025-04-08 | **full-body avatars: arm, leg and foot hit collisions turned off** in PvP RROs (only head + torso are hit) |
| 2025-09-22 | CTF flag-return bug fixed ("most common cause"); collision fix near the Red and Blue **Quarry** bases (players got stuck under water) |
| ~2025-09/10 | **Mods** in public games on "Mod Mondays", paid with Mod Tickets (250 tokens each per the wiki): Wormz (very fast while crouched), Zero Gravity (high, slow jumps), Sniper Only (every weapon a sniper) |
| 2025-10-14 | **new Paintball Lobby**: shooting gallery to practise, a **1 v 1** challenge spot, the Paintball gear store, **lockers** to change your look; some UGC paintball rooms fixed |
| 2025-10-27/28 | **Merry Midway** map (the Make It To Midnight night fairground reimagined for Team Battle and CTF, "the first new Paintball map in years"); **Splat Hammer** = first melee weapon; **Sprays** (spray can from the radial menu, sprays equipped in the backpack, Spray Boxes); **Clash of Clowns battle pass** (web: 20 levels, finishing it gave a Bonky skin + Spiral Hammer); **mods always available in private instances** (can be switched off there); Merry Midway has a map-specific mod (the wiki: Hammers Only replaces Sniper Only there) |
| 2025-11-10 | the **paintball match summary** can be skipped with Continue |
| 2025-12-01 | 3 more mods: **Shotguns Only, Hammers Only, Fiesta Mode**; "Paintball Mod Tickets" became **RRO Mod Tickets** (visible in the backpack) with a **new board to activate them** (the Mod Board, later also in Golden Trophy and Dodgeball); Merry Midway music turned up; screen-mode speed boost from a charged Splat Hammer attack nerfed |
| 2026-01-22 | an invincibility exploit fixed; "unlimited **paint thrower** ammo **in the paintball lobby**" fixed (so the 2025 lobby hands out a paint thrower) |
| 2026-02-10 | **Heartbreaker battle pass** (40 levels, avatar items, weapon skins, new sprays bought with "paint" or earned in the pass) |
| 2026-03-30 | shutdown announced; token rewards at the end of RROs stopped; store items -80 % |

Nothing was removed from Paintball in the final years: all **7 maps** (River, Homestead, Quarry, Clearcut, Spillway,
Sunset Drive-In, Merry Midway) and both modes were live at the shutdown (no ship note retires any). GUESS: all 7 were
in the map choice at the end; no source lists the final rotation. Whether `^PaintballVR` (teleport-only room) still
existed at the end is not documented (GUESS: yes, like ^DodgeballVR).

### 1.2 Final rules (the bundles are the final build, so these are the June 2026 values)
Unchanged from rr_shooters 3.1 / 4.1: one hit = out (MaxHealth 1), auto-respawn 5 s, 5 s spawn invincibility, 4 v 4
(`TeamConfigurations[*].MaxTeamSize = 4`), manual start with 2 players on 2 teams (`ManualGameStartRequirement`
MinPlayerCount 2 / MinTeamCount 2 / MinTeamSize 1), CTF 3 captures or 600 s, Team Battle 50 hits or 600 s,
`FriendlyFireSettings.GrenadesCanDamageTeammates = 1` (grenade, launcher and mine splash DOES hit teammates; bullets do
not), OutOfBounds grace 3 s / max 4 s then downed. Stats shown: Hits, Outs, Flags.
Add from the ship notes: only head and torso count as hit zones (2025-04).

Mods (final list, 2025-12): Wormz, Zero Gravity, Sniper Only, Shotguns Only, Hammers Only, Fiesta Mode, plus the Merry
Midway map mod. Effects as named. **Fiesta Mode's effect is not described anywhere I found** (GUESS: party weapons;
the bundle has `(PaintballGun)Confetti` MAIN key `4fe` #-4860720375845936648, a confetti variant of the pistol, and
`[VFX_Confetti]` MAIN #-2861756407430242321). Activation: the RRO Mod Ticket consumable
(`(RROModification_Paintball_Ticket)` bundle `85515de19d04f3c4dfe704e6f02046e3` #5024075317182616750, friendlyName
"RRO Mod Ticket", sprite `punchcard_Spritesheet_paintball`) on the Mod Board in the lobby; in public games only on Mod
Mondays, in private instances always. The board itself is room content (not in the bundles); the mods were built
with circuits (script `RROModificationConsumableItem` is only the ticket).

Weapons per map at the end (wiki + rr_shooters 1.1; the actual rack positions are in the encrypted levels): pistol at
spawn everywhere; shotgun everywhere; scope rifle on Quarry / Clearcut / Spillway; burst rifle on Clearcut; paint
launcher on Spillway; **paint thrower + paint mines on Drive-In**; Splat Hammer on Merry Midway; grenades and shields
(2016 Arsenal update) on the older maps. GUESS: exact rack positions and counts.

### 1.3 Lobby (final, 2025-10-14) and post-match
One shared lobby space (GUESS from "a new Lobby space"). Before 2025 each map seems to have had its own lobby with a
radio playing that map's lobby track (GUESS from the 6 `PaintballLobby_<Map>Radio` clips; there is none for Merry Midway). Contents per the ship notes: shooting
gallery (`(Paintball_Target_Sandbox)Variant` targets; weapons including a paint thrower), a 1 v 1 challenge spot
(GUESS: a small duel lane with two pads), gear store (`(StoreCategorized)_PaintballStore` ef9b849f
#5433951995046038661), lockers (`(Sandbox_Lockers)` key d5c), the battle-pass board, the Mod Board (2025-12), and the
scoreboard with START / team join / map and mode choice (rr_shooters 1.1). Layout and look: not documented (GUESS).

Post-match (2025-11): the **match summary page** `(Page_BattlePass_PostGameSummary)`, bundle
`77ea7f3e16c479c388e138a2de921376` key `6` #2385283516829016654 (animator `BattlePass_PostGameSummary_Animator`
#8243770686961252785). Its texts: "Match Complete!", "Match XP", "Team Bonus", "XP Boost", "Daily Punch Card",
"Daily Punch Card Bonus +500 XP", "Next Reward" / "Last Reward" / "Earned Reward!", "You would have earned:",
"Purchase battle pass to earn rewards!", "Buy Pass!", "Continue". World-space progress board:
`(BattlePass_Progression_WorldSpace)` same bundle key `9` #5457169647014518791; watch page `(ThisRoom_BattlePass)`
1a69c270 #7267153796729019672; `(BattlePassStore)` ef9b849f #-4228153379048671278.

### 1.4 Merry Midway (2025-10-28): what exists
- Level: room content; nothing of the map is in the plain bundles (checked: no Midway mesh, no Midway music; only a
  `CarnivalBits` mesh in 1df8e0ef and `(Border_Panel_MerryMidway)` UI frames in 77ea7f3e). Rebuild from the Make It
  To Midnight fairground (rr_party.md 4.1: Ferris wheel, slide, alleys, red circus tent, concession stands,
  bathrooms, photo booths, north and south gates), scaled for 4 v 4 CTF. The whole layout is a GUESS.
- Splat Hammer `(Paintball_SplatHammer)` MAIN key `51d` #6323783143975840158 (numbers in rr_shooters 3.1: 100 damage,
  burst radius 7 m re-arming after 6 s, walk speed x1.4 while held), VFX `(Paintball_SplatHammer_Paint_Explosion_VFX)`
  #6750918707587189322, clips `sfx_pm_splathammer_*` (MAIN). Final tweak: the screen-mode charged-attack speed boost
  was reduced (2025-12; the amount was not published).
- Music: none in the bundles (GUESS: pick a carnival-like track we already load, or leave the map on ambience).

### 1.5 Changes vs existing research / build
Research corrections:
- rr_shooters 1.1 lists the 2025 mods without dates: Wormz / Zero Gravity / Sniper Only came first (autumn 2025, Mod
  Mondays); Shotguns Only / Hammers Only / Fiesta and the RRO Mod Tickets + Mod Board are 2025-12-01.
- The shooting-gallery lobby is the 2025-10-14 shared lobby, and it hands out a paint thrower.
- Add: head+torso-only hit zones (2025-04), beacons (2024-11 / 2025-01), the match summary page (77ea7f3e key 6).
Build (CODE/README "Paintball"):
1. **5 of the 7 final maps are missing**: Homestead, Quarry, Clearcut, Spillway, Merry Midway (Spillway has a proposal
   in rr_shooters 10.3; Merry Midway needs the Splat Hammer and a fairground rebuild).
2. **Drive-In weapons are not the final set**: it should have the paint thrower (on the snack-bar roof in the
   rr_shooters 10.2 proposal) and paint mines at the bases; the build puts a burst rifle on the roof and has no mines.
   River's burst rifles are not in the sources either (River = pistol + shotguns + grenades in the 10.1 proposal).
3. Grenade / launcher / mine splash should damage teammates (`GrenadesCanDamageTeammates = 1`); the build says
   "Friendly fire off", which is right for bullets only.
4. Hit zones: only head and torso should register (check the build's hit volumes exclude limbs).
5. Lobby: add the 1 v 1 spot, the Mod Board and a paint thrower in the practice range; the rest of the build's lobby
   (range, lockers, radios, console, scoreboard) already matches the 2025 lobby.
6. Mods: none built. Final-version set: Wormz (crouch speed), Zero Gravity (jump / gravity), Sniper Only, Shotguns
   Only, Hammers Only (swap the spawn weapon and all racks), Fiesta (GUESS: confetti pistol). Tickets and Mod Mondays
   were monetisation; in Somnium let the lobby toggle mods for free (product decision for the user).
7. Post-match: the build shows results for 15 s; the final game shows the summary page with Continue (skip).
8. Optional, low value: sprays, beacons, battle pass.
8b. Start: the final CTF / Team Battle configs have `AutomaticGameStartSupported = 0` (START button only, 10 s
   `GameStartDelay` countdown, 15 s `EndGameResultsDuration`); the build also auto-starts 20 s after two players
   are in. That is a Somnium convenience, not Rec Room behaviour (keep or drop: user's call).
9. Foundation-pass rule (rooms at the origin, only the current room exists): in Rec Room each map was its own subroom with the lobby next to it (GUESS for the 2025 lobby); build
   only the selected map at the origin and tear it down after the match (the build spawns River and Drive-In 450 m
   from the lobby at the same time).

## 2. Laser Tag (^LaserTag; not built)

### 2.1 Final version (June 2026)
- **One room `^LaserTag`** (2018 "Costume Party" update, Oct 2018: ^LaserTagHangar and ^LaserTagCyberJunk were merged into it, "with the same
  room rotation behaviour as ^Paintball; you can choose a specific map"). Maps: **Hangar** and **Cyber Junk City**,
  both live at the shutdown. It was a VR-teleport-only activity in 2018; the final wiki page lists ^LaserTag as a
  #pickup room (teleport, walking and screen mode together).
- **Mode: team deathmatch with bots only** (Mainframe Assault and Fuel Run were cut before release; the bundle
  still has their pieces: `(Arena_EMP)` MAIN #4433222969481056413 with Arena_EMPArming/Armed/Planted/Hacking/Defused/
  Detonated clips = Mainframe Assault, and Arena_FuelCell_Pickup/Throw/HitEnvironment clips = Fuel Run. Not final:
  do not build).
- **Teams 4 v 4** (since the 2018 Cyber Junk City update), **walkie-talkie team radio** (2018: "now supports
  walkie-talkie style team chat"), bots on both sides.
- **Score**: wrapper defaults 400 points or **480 s** (bundle; the wiki's "10 minutes" is older text), 10 per enemy
  player / elite bot, 2 per bot, -10 / -2 friendly fire (rr_shooters 4.2). Add: **a self-destruct costs 10 points**
  (2019-01 grenade rework note).
- **Grenades** (`(Arena_Grenade)` MAIN #8282434837044359620): since 2019-01 **no friendly fire in Laser Tag, but
  they can hit yourself**; picking up a live grenade switches it to your team; shooting a held grenade does not
  explode it (2019-01).
- **Spawn invincibility 3 s** (2019: raised from 1 to 3 s in Paintball and Laser Tag; Paintball went to 5 s in 2020,
  no later note for Laser Tag. GUESS: still 3 s at the end; the Laser Tag wrapper has no config asset to confirm).
- Weapons: the PvP-tuned `Arena_*` guns (rr_shooters 3.2) - since 2019 these are "the PvP-tweaked Laser Tag variants";
  power weapons: Laser Orb Launcher on Hangar, Beam Cannon / "Pulse Cannon" on Cyber Junk City. 2024-04-30: two-handed grab
  on the laser guns uses the new weapon stabiliser.
- Map changes: **2023-01-30 "on both Laser Tag maps, fixed issues where you could get out of the map and made small
  layout adjustments based on feedback from the Laser Tag community"** (the change log went to Discord only; not
  found). 2023-03-06: **buckets added to the lobbies** (promised with the January changes) (fun props). 2019: a spot on top of the Hangar spawn areas
  blocked.
- Lobby: bleachers/lobby area with scoreboard, the Laser Tag merch booth (tickets: 25 per game + 100 bonus for the
  first 5 games a day, wiki) and, from 2021, a Laser Tag storefront selling consumables (gift boxes on purchase).
  2026-03: no more token rewards at the end of RROs.
- Hit zones: head and torso only for full-body avatars (2025-04, all PvP RROs).
- Nothing Laser-Tag-specific changed in 2024-2026 except the above; the 2025-12 "Laser Weapons" mod belongs to Quest
  for the Golden Trophy (it swaps the quest's weapons for the Laser Tag variants), not to Laser Tag.

### 2.2 Bundle content (all MAIN unless noted; complements rr_shooters 2.1 / 3.2 / 4.2 / 7)
- Guns `(Arena_Pistol)` #7820467338851362683, `(Arena_AutomaticGun)` #6157498524630832894, `(Arena_Shotgun)`
  #7866521719765982762, `(Arena_RailGun)` #8630163416721235512, `(Arena_RocketLauncher)` #8262130568968328032 (orb
  launcher), `(Arena_PowerWeapon_BeamGun)` #703114869300882648, `(Arena_Grenade)` #8282434837044359620.
- Game: `(LaserTag_GameRuleWrapperCircuit)Variant` #4993441790827724319 -> `[ArenaManager]` 490217366522465100.
- HUD: `[Arena_PlayerCombatUI_3D]` #-7878617144436674605, `[Arena_EnemyCombatUI_3D]` #-488507276004034338,
  `[Arena_Points_WorldSpaceNotification_Team1]` #7500639247250683869 / `_Team2] Variant` #983594565515664114.
- Only one map prop: `(Arena_Cyber_TrashBag_Sandbox)Variant` #-7069671901031807680. Neither map's geometry is in
  the bundles (Hangar and Cyber Junk City are levels).
- Music: ArenaIntense (bundle 9fbf7292), Arena_CyberJunk_Explore03b (4de47222), Arena_Cyberjunk_combat04b (853e5df3).

### 2.3 Changes vs existing research / build
- rr_shooters 1.2 says "first to 400 or most at 10 min": use the bundle's 480 s; add the -10 self-destruct rule, the
  no-friendly-fire-but-self-damage grenade rule, 3 s spawn invincibility (GUESS for the final value), team radio,
  one ^LaserTag room with a map rotation / map choice, and the 2023 layout tweaks (unknown detail; build from the
  wiki descriptions as rr_shooters 10.4 does).
- Somnium: the user's rule "players not in the same place are muted" does not conflict with team radio: both teams
  are in the same place; team radio = teammates hear each other at any distance inside the arena (GUESS on how the
  isolation task exposes voice routing - a request for that task if needed).
- Not built: everything. Minimum final-version build: Hangar (rr_shooters 10.4) + bots + the rules above;
  Cyber Junk City second.

## 3. Rec Royale (^RecRoyaleSolos, ^RecRoyaleSquads, ^RecRoyaleVR; not built)

### 3.1 Final version (June 2026)
Rec Royale had no content update after the "Rec Royale Update" of 2020-12-18 and its balance patch of 2021-01-28;
everything later is bug fixes and small map-geometry tweaks (the last in 2024; no note removes the mode, GUESS: live at
the shutdown). The final game = the 2020-12 content + the 2021 balance
+ the fixes below. The existing rr_shooters 1.3 / 3.3 / 4.3 / 10.5 describe this final game correctly (the loot table
in 3.3 is read from the final bundle and already contains the 2020-12 weapons and the 2021 nerfs), with these
additions:

| date | change (source: ship notes) |
| --- | --- |
| 2020-12-18 | 6 new weapons: Beam Cannon (L), Paint Thrower (L), Paint Mines (Epic), Deafening Boomstick (L, blunderbuss), Hunter's Dream crossbow (L), Goblin Annoyer crossbow (Rare); flintlocks rarer + more damage; bows rarer, more damage/speed, legendary only; **chests concentrated in and around the Locales** instead of random spots; **gondola start height lowered** ("make the decision of when to drop more impactful"); new footstep, downing-an-opponent and win sounds, more local ambience; 15 new levels and 6 new Frontier Pass tiers; Rec Royale on iOS and Quest 2 |
| 2021-01-28 | balance: Ghostbeard's Sidearm body 100 -> 75, head 200 -> 125, ammo 6 -> 5, drop rate down; Deafening Boomstick full shot 170 -> 153; Rigid Thwapper projectile slower. (Bundle agrees: Boomstick 17 pellets x 9 = 153; flintlock load 5.) |
| 2021-02-11 | **leaderboard projector** in the lobby tracking **wins and tags** |
| 2021 | gondola: players no longer fall through or spawn on top of it at game start; scorched terrain fixed; Beam Cannon / Paint Thrower refill fixed |
| 2022 | loot chests: "firmly grasp it for a few seconds to open" (hold-to-open, as rr_shooters says), items dropped into open chests pick-up-able, ammo no longer falls through the ground; endless XP beeping fixed |
| 2023-03-06 | **jumping into the ocean immediately downs you**; mines stuck to your body no longer block interactions |
| 2024-02-05 | landscape geometry adjusted where players got stuck in the falling animation |
| 2024 | VR hip holster positions fixed when you turn physically; potions easier to pick up; Double Shot never allowed on Rec Royale bows |

- **Minimum players to START: 14 in Solos, 18 in Squads/VR** (wiki: the START button on the lobby scoreboard works
  only then; otherwise players use the "Play Again" door to another lobby). This is the most important final-version
  fact for a Somnium build with few players (rr_shooters says 16 solo / 6 x 3 squads = the capacity, not the start
  threshold).
- Squads: 6 teams of 3, JOIN TEAM buttons on the scoreboard (`ExplicitTeamSelectionSupported` + automatic start
  per the 2019 game-rules notes), teammate beacons shown when a revive is available, revive health shrinks with each
  successive revive (`Revive Health Multiplier Normalized`), walkie-talkie team radio across the map.
- Frontier Pass: only ever **Season 1** (it ran from 2018 to the end, with the 2020-12 tiers added; wiki); Elite
  upgrade 1000 tokens. Rec Room CEO developer post, ~2025-05 (in the wiki 2025 news archive): "Rec Royale launched with a
  battle pass - players didn't bite". Ranks from BattleRoyaleImageManager (Recruit ... BeeKeeper, rr_shooters 4.3).
- 2026-03: token rewards at the end of RROs stopped.

### 3.2 Bundle content
Nothing new beyond rr_shooters 2-10 (manager, gear bag, map, potions, ammo, winner door/sign, taxonomy, island map
textures, audio). The island itself is a level (not in the bundles).

### 3.3 Changes vs existing research / build
- Add the **14 / 18 player start threshold**. For Somnium (GUESS, product decision): lower it (e.g. 2+ for private
  instances) or the mode never starts; Rec Room had no bots in Rec Royale.
- Add the Dec 2020 map-flow facts: chests clustered at Locales (not random), lower gondola, ocean = instant down.
- Add the leaderboard projector (wins, tags) in the lobby.
- Not built. The final-version rules to build are the ones in rr_shooters 1.3 / 4.3 plus this section.

## 4. Rec Room Showdown (^Showdown; not built)

### 4.1 Final version (June 2026)
Showdown never got a content update after launch. Final = the launch week (RR+ early access 2022-08-11, balance patch
2022-08-16, rewards + invincibility patch 2022-08-17, open to everyone 2022-08-18) plus fixes 2022-08-26 (joining
timeouts) and 2022-09-15 ("no more shrunken yet supersized cowboys": the team costume scaling). The Rec Room Plus lock
was only the one-week early access (the 2025 CEO post: "Showdown was locked behind Rec Room Plus - no impact"). No
later ship note touches it or removes it (GUESS: still playable at the shutdown).

New sources: the three-part developer blog (2022-08, fetched from the Wayback Machine into
`SP/research/rga/blog/showdown1..3.txt`) and the wiki changelog (`SP/research/rga/wiki/Showdown.txt`).

Rules (blog pt 1-2 + wiki):
- 6-player team deathmatch, **2 teams of 3**: the Lawful **Deputies** (the blog; the wiki also says "Sheriff team")
  vs the Nefarious **Outlaws / Bandits**. **4 minutes**, first team to **25 hits** or most hits at time-out. The design
  was "a reskin of paintball" iterated from there: one hit = out ("KO skull over your head"), then respawn at a new spot
  **with a fresh revolver in your hand**; the dropped weapon stays.
- **No dual wielding** (blog: "we made it so you couldn't dual wield the weapons in this game").
- **In-game HUD** (a then-new circuit): team hits and the countdown timer on screen. Scoreboard in the town (maker-pen
  built). Team uniforms are full-body costumes given at the game intro (not possible in Somnium: the avatar is never
  modified; use our team markers).
- Game states: Pre Game, Intro Game, Game Start, Game End (blog's Game Manager). The game ends if all players but one
  leave during the pre-game countdown (2022-08-16).
- Spawn invincibility: +1.8 s on 08-16, -1 s on 08-17 (base value unknown; GUESS final ~1.8-2 s). Spawning near an
  enemy made rarer.
- Weapons (auto-reload, made with CV2): **Cork Spinner** (revolver; wiki: 5 corks per load, the pre-release blog says 6;
  use 5 as the final GUESS), near-hitscan, unlimited range, press or hold to fire, everyone spawns with it;
  **Cork Blaster** (shotgun, 2 shots of 6 corks; 08-16: spread +42 %, range -40 %; **3 spawn spots** after 08-16: hay
  bale at Archibald Mining Co. (added), table in the Station, inside the Mines); **Cork Repeater** (lever carbine,
  3-round bursts, 08-16: burst fire rate -75 %, normal fire rate -44 %; spots: **2nd floor of the Bank** (moved there
  from the Train Station on 08-16) and the Bar). Balanced "rock-paper-scissors" for near / mid / far. No crosshair in VR
  (wiki criticism).
- Map: Sarsaparilla Springs, a "fairly cylindrical" small town with **3 vertical layers** (roofs, street, mine
  tunnels), circular movement, asymmetric (no sides needed without CTF), near/mid/far zones, balconies with limited
  sightlines, long sightlines in the tunnels for revolvers. Story: bandits escape the jail, dynamite into the bank, flee
  by train. Areas as in rr_shooters 1.4. The **train "Rec Room Limited"** interrupts mid-game: announcement "Your
  attention please, the Rec Room Limited is now arriving from a trip around the Rec Room Campus", stops ~30 s at the
  Station as moving cover. Population sign at the Station: 132 -> 51 -> 12, "Archie".
- Sound (blog pt 3): theatre lobby with a piano playing, desert wind and eagle screeches outside, cold echoing mine
  ambience (falling rocks, distant whisper), and **the match music speeds up towards the end of the round**. Archie
  is the announcer (not Coach). None of these clips is in the plain bundles.
- Lobby: the Theater (balcony view over the town is the first thing you see), team switch, start, 4 Cork Spinners, a
  small shooting range; players return there when the game ends.

### 4.2 Bundle content
Confirmed again: no Showdown map, weapon mesh, music or VO in the plain bundles (search: cork, showdown, sarsaparilla,
western, revolver, spinner, repeater, sheriff, bandit). Usable: `[Sandbox_Projectile_Cork]` MAIN #7421077002004536387
(mesh `Cork_Projectile` #5703355152252123503), Sarsaparilla bottle / crate meshes (85515de1). `Gun_Revolver_Norm` /
`_Spec` textures exist in MAIN (#-7225295520078542267 / #9042113019551367428) with no mesh: GUESS leftovers, unusable.

### 4.3 Changes vs existing research / build
- rr_shooters 1.4: teams are "Deputies vs Outlaws/Bandits"; add: no dual wield, HUD (team hits + timer), music speeds
  up near the end, respawn with a fresh revolver, weapon spots after the 08-16 patch (3 Blasters, Repeaters at the Bank
  2nd floor + Bar), the train announcement text, the "game ends if everyone else leaves in the countdown" rule.
- rr_shooters 10.6 proposal "25 hits or 240 s, 1 hit = out, ~2 s invincibility, Spinner 5 shots" matches the final
  game; keep the Repeater at 3-round bursts but with the nerfed (slow) burst rate.
- Not built.

## 5. Dodgeball (^Dodgeball pickup room, ^DodgeballVR teleport-only; built)

### 5.1 What changed after 2020 (all from the ship notes; rr_sports 2.2 has the 2016-2018 basis)

| date | change |
| --- | --- |
| 2017-06-08 | private activities configurable from the scoreboard before START (Paintball, Dodgeball, Paddleball, 3D Charades) |
| 2021-04-15 | **screen-mode throw rework**: "trajectory more predictable and powerful on screen"; **"Charging your throw will now have no impact on the dodgeball's launch speed. It merely allows you to hold it."** |
| 2021-07-09 | the feedback tool can no longer block balls: "Now you'll just have to rely on **catch-outs**" (so from at least 2021 to 2025 a catch put the thrower out) |
| 2022-03 | the pointing finger can no longer block dodgeballs (and paintballs) |
| 2021 / 2023-10 | vote-kicking opponents during a match disabled |
| **2025-01-14** | **"Changed Dodgeball to count outs only on the player that was hit (finally, no more catch-outs!)"** and **"Dodgeballs no longer count as out when they hit another player."** |
| 2025-04-08 | the Dodgeball prop became available in Rooms 2 (UGC; not the RRO) |
| 2025-09-22 | VR fix: a hand in pointing mode when picking up no longer spoils the release ("throwing off dodgeball players") |
| **2026-01-22** | **Mod Board in the gymnasium** with 2 RRO mods: **Low Gravity** and **Dodge-Bombs**; **map change: "small barriers behind each side to catch dodgeballs rolling by"** |
| **2026-02-10** | **Disco Dodgeball** mod: "decorates the gym for the Rec Room school dance" (new Music/SFX Constant audio shipped with it) |

Final rules (June 2026), combining the bundle (final) and the notes:
- 2-6 players, 2 auto-sorted teams (max 3 v 3), 3 balls on the centre line, a ball in each hand, move with a cooldown
  (teleportDelay 1.5 s, maxTeleportDistance 12 m), first to **3 round wins** (`defaultScoreToWin 3`).
- **Hit by a live thrown ball = out; only the player who was hit is out.**
- **Catching a thrown ball puts NOBODY out** (2025-01 removed catch-outs; before that, 2021-2024, a catch outed the
  thrower). Reading of "no longer count as out when they hit another player": a ball that has already hit one player
  does not put out a second player it bounces into (GUESS on the exact meaning).
- Nothing in any source says a catch brings a teammate back in (that is real-world dodgeball, not Rec Room).
- Screen mode: fixed launch speed (Rec Room's `ThrowDodgeballToolBehaviorConfig` throwSpeed 14 m/s), holding the button
  only holds the ball; aim assist 9 m / 30 degrees, 7 degrees gravity compensation (rr_sports 1.5).
- Court: the gym court with **low barriers behind each back line** (2026) that stop rolling balls (GUESS: ~0.5 m high,
  just behind each team's back line, full court width).
- Mods (Mod Board in the gym, RRO Mod Tickets, 2026): Low Gravity (lower gravity for balls and players, GUESS both),
  Dodge-Bombs (GUESS: balls explode on impact / after a fuse with a splash out - "pretty self explanatory"; no
  details published), Disco Dodgeball (cosmetic: school-dance decorations; GUESS disco ball, coloured lights, the
  `Dodgeball_Skin_Disco_Mat` ball skin in SKINS `5c91ea11` #4366878749224701714, and the "SportFunkO2c (slow dance)"
  music: ee61d066 key `8` 136.6 s, 10 s fanfare 63f5edf5 key `4`, radio 644d5b01 key `a` - listed in rr_sports 8.1;
  that they belong to Disco Dodgeball is a GUESS).

### 5.2 Bundle content
No new objects beyond rr_sports 2.1 (ball, manager, wrapper, skins, music DodgeBaller). No bomb/disco prefab exists in
the plain bundles; the mods were room circuits.

### 5.3 Changes vs existing research / build
Research corrections:
- rr_sports 2.2 "Catching a thrown ball nullifies the throw: neither player is out" is right for the FINAL version
  (and 2016), but catch-outs existed in between (2021-2024 notes).
- Add: the 2021 screen rule (charge does not change speed), the 2026 barriers and mods.
Build (CODE/README "Dodgeball and Paddleball"):
1. **`RrDodgeballRoom.CatchOutsThrower = true` is the pre-2025 rule. Final version: set it to false**, and drop the
   "catch brings the catcher's first teammate back in" behaviour (no source has it). A catch = dead ball, nobody out.
2. **Screen throw speed must not depend on charge**: `ScreenThrowVelocity` scales speed by `Lerp(0.45, 1, ease)`;
   the final game throws at the full 14 m/s however long the button is held (charge = hold only).
3. A ball that has already hit a player must not put out a second player: already true in the build
   (`RrDodgeball` calls `Kill()` after the first player hit), no change.
4. Add the **low barriers behind each back line** (2026 map change).
5. Mods not built: a Mod Board with Low Gravity, Dodge-Bombs and Disco Dodgeball (all GUESS in detail).
6. The centre-line / out-of-court "out" rule in the build is a GUESS from rr_sports (Rec Room's notes only mention a
   penalty for leaving the court in 2016 and a fix for getting onto the wrong side); keep but mark as ours.

## 6. Paddleball (^Paddleball; built)

### 6.1 Final version (June 2026)
Paddleball is one of the 2016 launch games and never got a rework. Every later note is a fix:

| date | change |
| --- | --- |
| 2016-07-19 | leaderboards (all-time + weekly, reset every Tuesday); Paddleball was the first game with them |
| 2016-08-11 | lighting updated (with 3D Charades, Dodgeball, locker room) |
| 2016-12 | double-scoring bug fixed; Ghost Mode allowed during a game |
| 2017-06-08 | private activities configurable from the scoreboard before START |
| 2017-08 / 2018-04 | ball spawning under the ground / flying out of the court fixed; "Paddleball music wasn't playing" fixed (so it has game music; which track is not named) |
| 2022 | the Paddleball court door interactable again |
| **2025-09-22** | **"The ball in ^Paddleball no longer freezes sporadically at mid-court"** (the last Paddleball change) |

Final facts (wiki infobox + bundle): **2 players (1 v 1)**, **first to 5** (`defaultGoalsToWin 5`), 2 s scored state,
**VR only** (wiki: screen mode No; not on Xbox / PlayStation; no mobile). Zero-gravity ball in a transparent tube,
serve, a point when the ball reaches the back of the opponent's area (rr_sports 3.2).

### 6.2 Bundle content
Nothing new beyond rr_sports 3.1 (`[PaddleballManager]`, `(Paddleball_Paddle)` key 2d, `PaddleToolBehaviorConfig`,
TennisBall / pingpong / RacketHit01 sounds). No Paddleball-specific music clip exists in the plain bundles (search:
paddle, pong, racket, tennis); the 2018 note proves the room had music, so it was in the level or is one of the
generic sports tracks (GUESS: SportFunk, as the build uses).

### 6.3 Changes vs existing research / build
- Research: rr_sports 3.2 already matches the final game. Add only the 2025-09 freeze fix (the build's "ball stalled:
  serving again" watchdog is the right idea).
- Build: **the build supports 2 v 2 (4 paddles, 4 seats); the final Rec Room game is strictly 1 v 1.** Keep 2 v 2 as a
  Somnium extra or restrict to 2 seats for fidelity (user's call; untested either way).
- Build: screen mode (click to swing) is a Somnium adaptation; Rec Room's final Paddleball had none. Fine to keep.
- Nothing else is outdated.

## 7. Soccer (^Soccer; not built)

### 7.1 Final version (June 2026)
No gameplay change after 2019; the June 2026 game is the one rr_sports section 4 describes. Timeline of the later
notes: 2018-03 walking allowed in Soccer; 2018-08 lighting fix; 2018-12 "couldn't start Soccer in screens mode" fixed
(screen mode supported); 2019-11 broken textures fixed; 2020-02 "giant sign in ^Soccer" fixed; 2020-05 sign collision
fix. Nothing in 2021-2026 (Steam news and wiki archives searched).

Final facts: 2-6 players, 2 teams (3 v 3), **3-minute match** (`defaultGameDuration 180`, bundle), most goals wins,
giant ball (1.43 m, cannot be picked up; kick by body, hands or teleporting into it), fast in-game role (walk 6 m/s,
teleport 8 m, delay 0.5 s), **yellow speed-boost orbs** on the field. The shield of the original "Shield Soccer"
(2016) is gone in the final game: the wiki says Soccer Shields are "since unobtainable" and the room was renamed from
^ShieldSoccer to ^Soccer; the orbs that once charged the shield now give a speed boost (the generic "speed-boosting
pickups" shipped for creators in 2019-09; GUESS that Soccer's orbs are the same system: clips
`Locomotion_Boost_Initiate02/Looping01/Spent01`). Mobile: iOS only (wiki infobox).

### 7.2 Changes vs existing research / build
- rr_sports 4 is correct for the final version. rr_sports 9.3 already makes `(SoccerShield)` (key 28) an optional
  "Shield Soccer classic mode": for the final version leave it out (it is the 2016 tool).
- Not built.

## 8. Disc Golf (^DiscGolfLake, ^DiscGolfPropulsion; not built)

### 8.1 Final version (June 2026)
The courses did not change after 2017 (Propulsion 2017-03, Co-op Catch 2017-05); later notes are fixes and one
screen-mode rework:

| date | change |
| --- | --- |
| 2018-03 | walking allowed; Lake hazard fix |
| 2019-06 / 2019-12 | Lake on Oculus Quest; Propulsion on Quest ("with the world famous **troll fan on Hole 4**") |
| 2019-10-15 | **standing on the water in Lake no longer KOs the player** ("behaviour now matches Disc Golf Propulsion") |
| 2019-11 | players no longer stuck in place after the mercy rule |
| 2020 | missing trees fixed; sign collision fixed; **players can no longer climb onto rocks** (07-28) or get inside rocks (09-29, Lake) |
| 2021-04 / 2021-06 | watch hazard button sometimes did nothing - fixed; a rock invisible to mobile players fixed |
| 2023-01-30 | Propulsion VR lighting fix |
| **2024-05-14** | **screen mode: "fixed some issues with the way frisbees are thrown - animations now look more natural and your throw will actually go towards the cursor now"** |

Final facts: 1-4 players, 9 holes per course, mercy rule 7, stroke threshold 2.5 m, Co-op Catch mode (bundle values in
rr_sports 5). Lake: water obstacles, minus-stroke bonus panels. Propulsion: fans (a notorious one on hole 4), hazard
panels, moving walls, climbable platforms. Rewards (Golfer Set / Mad Scientist Set) = cosmetics, skip.

### 8.2 Changes vs existing research / build
- Add to rr_sports 5.2: water in Lake is a disc hazard but does not KO the player (2019); players cannot climb the
  rocks (2020); Propulsion hole 4 has the "troll fan" (a fan placed to blow discs away; GUESS: next to the basket).
- Screen throw: the final game aims the throw at the cursor (2024). Our Non-XR mapping (rr_sports 1.5: throw speed
  from charge 5 -> 19.96 m/s over 3 s, curve from mouse motion) should send the disc toward the crosshair point.
- Not built.

## 9. Bowling (^Bowling = Lone Shoe Lanes; not built)

### 9.1 Final version (June 2026)
Released 2018-10-11 and unchanged in rules since its 2018 patches (others cannot grab your ball right after a throw;
10th-frame reminder; gift box after a game). Later: 2019-06 invisible tables near the **vending machine** fixed;
2019-12 on Quest; **2020-02-03 the pool table became fully playable** ("grab a pool cue, slap one of the buttons to
spawn the balls"; screen mode: grab a cue, use the "Play Shot" option at the centre of the table, aim with the look
controls, hold Shoot and pull the cue back for power, get low for backspin); 2025-04-09 the bowling ball no longer plays
the rolling sound all the time. Nothing else through 2026.

Final facts: one scoring lane (the middle one) for 1-6 bowlers, two practice lanes, automatic ten-pin scoring, the
pool table, jukebox, bar stools, vending machine (rr_sports 6).

### 9.2 Changes vs existing research / build
- Add to rr_sports 6.2: the working pool table (2020) and its controls, the vending machine near the tables, and the
  2025 roll-sound fix (roll sound only while the ball actually rolls: rr_sports already says "volume by speed^2").
- Not built.

