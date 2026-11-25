# Final (pre-shutdown) versions: 3D Charades, Make It To Midnight, My Little Monsters, Punch Up, the five Quests, Rec Rally, Stunt Runner

Status: COMPLETE (2026-09-24, agent rgames_story). Research only; no CODE edited. Sections: 0 summary, 1 3D
Charades, 2 Make It To Midnight, 3 My Little Monsters, 4 Punch Up, 5 Quest for the Golden Trophy (built: the change
list is 5.3), 6 Crimson Cauldron, 7 Crescendo of the Blood Moon, 8 Isle of Lost Skulls, 9 Rise of Jumbotron,
10 Rec Rally, 11 Stunt Runner, 12 cross-cutting notes, open questions and sources. Each activity ends with a
"Changes vs existing research / build" list.
Rec Room shut down on 2026-06-01 at 12:00 PT (Steam news "School's Out", 2026-03-30). "Final version" below = the
state of each activity at that moment.

Conventions
- `NEWS yyyy-mm-dd "title"` = Steam news item in `SP\research\dorm\wiki\steamnews.json` (564 items, 2016-06-30 ..
  2026-04-06). Search script: `SP1\rgs\sn.pl <regex>` (SP1 = scratchpad of session 1c0947a6...), full item:
  `SP1\rgs\full.pl <gid>`.
- `WIKI Page` = rec-room.fandom.com wikitext fetched 2026-09-24 through the MediaWiki API
  (`api.php?action=parse&prop=wikitext`), saved in `SP1\rgs\wiki\*.txt`; older saves of the party pages are in
  `SP\research\party_wiki\`.
- Bundle ids as in the other research docs: `B43` = `43ea1176ca25c0333547813ec3298808` (= "BIG" in rr_party.md),
  keys = Addressables container keys.
- GUESS marks anything not read from a source or the data.

Is the install the final version? Yes. The Steam install (`E:\SteamLibrary\steamapps\appmanifest_471710.acf`: buildid
**23191908**; all 2,787 bundles dated 2026-09-08) was downloaded after the shutdown, so it is the last Steam PC build
(the April 2026 data-export build or later). Every bundle fact in this document and in the older research docs is
therefore the final shipping data. Where the wiki and the bundles disagree, the bundles win.

## 0. Summary (read this first)

| Activity | Final version (what changed late) | Build status | Biggest gap vs final |
|---|---|---|---|
| 3D Charades | The Ink Space venue (2020-09-09) with the 2020 rules: 60 s co-op turns, 45 s Lightning turns, all platforms, screen "Draw Mode". Only bug fixes after that. The old theatre lived on as a separate room, ^Legacy3DCharades. | Built (Ink Space) | The pen has no shapes, materials, redo or scale; drawings cannot be grabbed; games can start with 1 player |
| Make It To Midnight | The 2023-08 game, unchanged apart from 2023 patches. It spawned **Bonky's Inferno** (2024-11, a free-for-all hammer room) and the **Merry Midway Paintball map** (2025-10-28, shooters' scope). | Not built | - (the rr_party.md facts are the final ones; add the perk table, 2.1) |
| My Little Monsters | The 2024-06-13 Rooms 2.0 game; 2024-08 crafting tweak; it earned about 10 % of its cost (2025 CEO post). | Not built | - |
| Punch Up | Never released. It is only an Easter egg (a dev quote from 2017 is in 4.1). | Not built | - |
| Quest for the Golden Trophy | The 2017 quest with 2018 holsters, a 2020 merch booth, **Roomie** as an RR+ AI sword companion (2025-07) and **RRO mods** (2025-12-01: Turbo, Coconut Chaos, Laser Weapons; badges 2025-12-08). | Built | Players die in **one hit** (build: 100 HP); wiki enemy points and S/A/B/C gold thresholds; act 6 is a **library** with a spinning elite barrel knight; the dressing room holds 4 swords, 2 shields and 2 bows; the lobby is a theatre stage with Coach's "Game On!"; the mods |
| Curse of the Crimson Cauldron | The 2017 quest, polished in 2020 (bell-tower ladders, pike-goblin shields). | Not built | rr_quests 4.2's act list is a GUESS and wrong: the real 9 acts are Castle Courtyard, Forest Path, Campsite, Deep Forest, Bog, Mines, Deep Mines, Bell Tower Stairs, Finale |
| Crescendo of the Blood Moon | The 2018 quest; to Mobile/Quest 2024-11-23 (mirror fix, Dracula bat animation back, bow "Double Shot"); S-rank "Vampire Hunter" badge 2025-10. | Not built | 13 acts with **four** Grand Hall visits; level names (Forest Road, Lower Sanctum, Concert Hall...); **5 mini-bosses**; Dracula is hit with **3 fixed light mirrors**; silver and combat-score tables |
| Isle of Lost Skulls | The 2018 quest after its 2018-12-05 "Rebooty!" update (checkpoints, back holsters). | Not built | Real act list (Galley jail, Pirate Banquet, High Seas, Port Plunder, Gibbet Jungle, Caves, Sunken Ship, Ghost Beard's Gauntlet), per-act points, 1-3 players |
| The Rise of Jumbotron | The 2017 quest; 2025-01 auto-fire fix. | Not built | rr_quests 4.4 is close; one-hit downs, 1200 credits per death, the per-gun magazines, the elite bots |
| Rec Rally | The 2021 race. **2025-10-15: can be played with 4 players instead of 6.** 2025-09 texture/collision fixes. | Not built | Minimum player count |
| Stunt Runner | The 2019 game (6 courses) with 2019 fixes; the community door shows #StuntRunner rooms (2021-06). | Not built | - |

Cross-cutting correction for **all five quests**: the wiki's Quests page says "one hit from an enemy (or an ally's
ranged weapon) will down you". Rec Room quests have no player health bar. The quest framework's `PlayerHealth = 100`
with per-hit damage (rr_quests 1.1 "100 HP ... GUESS 10-35" and 3.7) is therefore not the final behaviour. See 5.3 item 1 and 12.1.

No Rec Room quest got a remaster or a new act. A 2025 CEO post (WIKI News_Archive_2025) says every quest "saw less
play than the one before it" and that no new quests were coming. The only late quest changes are ports (CBM to
Mobile and Quest 2024-11), Roomie (2025-07), the "Vampire Hunter" CBM badge (2025-10), loot chests fixed
(2025-09-23 "Loot Chests work in Quests again"), a Quest-lobby start-button fix (2025-12-08) and the Golden Trophy
mods (2025-12).

## 1. 3D Charades

### 1.1 Final version (sources: NEWS, WIKI 3D_Charades / The_Ink_Space)
- **Venue: The Ink Space**, since 2020-09-09 (NEWS "Ink Space" edition): a "hip, underground cafe" that is also a
  clonable base room. The old theatre venue (2016 Theater, "Piano Lounge" music) stayed playable as the separate
  room **^Legacy3DCharades** (WIKI 3D_Charades: "An older version of this room is Legacy3DCharades"; NEWS 2024-04-16
  fixes "games ended early in ^Legacy3DCharades"). The final main room is the Ink Space one.
- **Players** 2-8, all platforms (VR and screens since 2020-09-09). Junior accounts are barred. It is a pickup room
  (teleport, walk and screen players together).
- **Modes** (unchanged since 2017):
  - Co-op Classic: everyone guesses, rounds are counted.
  - Team Lightning: Red vs Blue, **at least two players on each team**, each team gets two rounds per game, most
    points wins (NEWS 2017-04-07). The 2017 note also made Lightning the default mode, with a switch to Classic.
  - Private rooms pick the mode and settings "via the Scoreboard before you press Start" (NEWS 2017-06-08).
- **Timings** (final, NEWS 2020-08-10 + bundle wrappers):
  - Co-op: 1 min drawing time per performer, 480 s game.
  - Lightning: 45 s per performer, 4 team rounds of 90 s ("game length has been adjusted accordingly").
- **Rules added over time, still in force at the end:**
  - Everyone gets a turn with the pen (2016-10-18).
  - The performer spawns holding the pen, so nobody can steal it (2016-08-11, 2017-02, 2017-04).
  - Spectators cannot steal cards or the pen (2017-04-20).
  - **Guessers cannot grab the drawing when it is not their turn** (2020-08-10).
  - Spectators can move (2021-07-09).
  - The stage is visible to seated players (2021-08-19).
- **The pen** (a Maker Pen variant):
  - Colour buttons on the pen; line thickness follows trigger squeeze (2017-05-05).
  - Primitive shapes spawn without pitch or roll, like the Maker Pen (2020-06-09).
  - Screen players get a finger-paint "Draw Mode", a shortcut menu of shapes and colours, trigger-sensitive tube
    size (controller), draw-speed tube size (mouse/touch) and Apple Pencil pressure (2020-05-27, 2020-09-09).
  - The final pen menu (bundle, rr_party.md 1.3): shapes **Tube, Cube, Sphere, Cylinder, Pyramid**; **Undo, Redo,
    Scale**; materials **Cardboard, Neon, Shiny Metal**; 12 colour swatches.
  - Drawings were physics objects you could use to act the charade out (NEWS 2016-07-06). Maker Pen "Basic Mode"
    creations switch to PLAY when grabbed (2017-11-10). Since 2020 only the performer may grab them.
- **Scoreboard**: the giant scoreboard behind the stage is hidden while the artist draws (2016-07-13; that note is
  about the theatre, and it is a GUESS that the Ink Space kept it).
- **Words**: the card list moved to Rec Room's server in 2017-04. "New charade cards" came with the Ink Space (WIKI).
  The list is not in the bundles.
- **Unlock**: the Artist Outfit came from playing Charades (2017-10-02). Charades also counted toward daily
  challenges (2024-04-16).
- **Late fixes only** (no rule changes after 2020-09):
  - 2022-08-26: pinched or twisted tubes.
  - 2023-01-31: the ink colour differed with Maker Pen skins.
  - 2023-05-23: switching between the Maker Pen and the Charades Pen.
  - 2024-07-23: the pen got stuck in VR hands.
- **Music**: Ink Space track `MUS_BGM_3DCharades2b_Music`. The Legacy room used "Charades (Piano Lounge)"
  `MUS_BGM_3DCharades2_Music` (both in rr_party.md 1.3).

### 1.2 Bundle content for the final version
Everything is already mapped in rr_party.md 1.3, and it is the final data (buildid above). Measured additions:
- `[CharadesManager]` MB B43 #-4657640698747555159 (final fields): `scoreName` "Round"; `customInGameRole.overrides`
  0 (no movement override); `successClip` Charades_TeamScoreUp; `blueTeamJoinVO` / `redTeamJoinVO`;
  `timerExpiredClip` SFX_Failure_Stereo@70f410f6; `timerTickLoopClip` Charades_IndividualTimerLoop;
  **`guessButton` null**. The room itself supplies the "got it" button, so the manager says nothing about who judges.
- There is no word list, scoreboard layout or Legacy theatre geometry in the plain bundles.

### 1.3 Changes vs existing research / build
Build (CODE/README "3D Charades in The Ink Space"):
1. The venue, timings, clips, pen tip, Draw Mode, ink sync and "guessers cannot grab" **already match the final
   version**. Keep them.
2. **Add the pen's shape tools**: Cube, Sphere, Cylinder and Pyramid primitives, placed upright (no pitch or roll,
   2020 note) and sized by a drag or the Scale tool.
   - Add **Redo** and **Scale** next to Undo.
   - Add three ink materials, Cardboard, Neon and Shiny Metal. GUESS: map them to our paint, an emissive paint and a
     metallic paint.
   - Sounds: `sfx_ui_prop_spawn_01..04` for shape creation, clone clips (rr_party 1.3).
   - The build has tubes, 12 colours, Undo and Clear only.
3. **Let the performer grab and move his own drawing** (the 2016 "physics object you can use to act out your
   charade"). Guessers may not. GUESS: move it as one rigid group, no physics.
4. **Start rules**:
   - `MinPlayersToStart` is 1 in the build; the final game is 2-8.
   - Team Lightning needs at least 2 players per team (2017 note). GUESS whether the Ink Space enforced it.
   - Keep 1 as a harness-only override.
5. Optional: hide the big scoreboard while someone draws (GUESS, see 1.1).
6. Optional: give private instances a mode/settings choice on the scoreboard before Start (2017-06-08).
7. **Not needed**: the Legacy theatre (^Legacy3DCharades) is not the final main version.

Research (rr_party.md 1.1): its facts are the final ones. Add Legacy3DCharades as the name of the old room, and note
that Team Lightning needs 2 players per team.

## 2. Make It To Midnight (MITM)

### 2.1 Final version (sources: NEWS, WIKI Make_It_To_Midnight, Bonky's_Inferno)
- **The game did not change after its 2023 launch patches.** It was released 2023-08-17 (WIKI) and shipped in NEWS
  2023-08-29 as a Rec Room Studio room. Later changes:
  - 2023-09: patches (players stuck on join; the progression screen; Bonky's HUD showing in the share and streamer
    cams).
  - 2023-10-25: the new "Add Velocity"-style impulse chip that "makes Bonky's hammer delightfully thwacky", so a
    hammer hit knocks the survivor back.
  - 2023-11-08: the "Collision Detection Volume" is what detects hammer hits.
  - 2025-03-25: the Bonky costume now shows on classic bean players.
  - There is no map, rule or perk rework in NEWS or the wiki up to the shutdown. The rr_party.md 4.1 rules are the
    final rules.
- **Spin-offs (not MITM itself):**
  - **Bonky's Inferno** (NEWS 2024-11-23 "now available to play and to copy"; WIKI): a copyable developer-made
    community room. It is a **free-for-all melee PvP game based on MITM where everyone hits each other with Bonky's
    hammer**. Optional extra mode: FFA, hammer only, last hits win (GUESS on scoring).
  - **Merry Midway Paintball map** (NEWS 2025-10-27/28):
    - "The nighttime fairground from Make It To Midnight, reimagined for team battles and Capture the Flag".
    - **The Splat Hammer (Bonky's squeaky hammer) became Paintball's first melee weapon**.
    - A map-specific mod, the "Clash of Clowns" battle pass, and sprays.
    - 2025-12-02: louder Merry Midway music; less movement speed from a charged Splat Hammer attack in screen mode.
    - This belongs to the shooters research (rr_shooters.md). Anyone building MITM should share the fairground kit
      with that map.
- **Rule numbers to add to rr_party.md 4.1** (WIKI, final):
  - Generator skill check: green +25 XP; light green = critical, +50 XP and extra progress. A fail costs 10 %
    progress (5 % with Tech Whiz) and alerts Bonky. A **sabotaged** generator (red bar, electrical buzz) scrambles
    the next test.
  - Heal: 10 s hold (7 s with Expert Medic). Heal and uncage sounds are heard map-wide.
  - Cage: 1:30. A freed survivor cannot be caged again: the next hit eliminates them.
  - Bathroom stall doors honk and stun whoever they hit, pushing them in front of the door. Slopes and the slide
    allow continuous sliding without stamina.
  - After the 3rd generator the intercom says "That's it! Everybody! Head for the exits", an alarm sounds, and the
    north and south gates open by lever (progress is kept). Rewards: +150 XP per opened gate, +550 XP for escaping.
    XP cap 7,500 per game. Levels go to 100.
  - Bonky's abilities are on the jump button and unlock with level:
    - Clown Radar (level 2): reveals survivors in the fear aura for 25 s; 75 s cooldown.
    - Silly Sneak (level 12): no aura sound or light and silent steps for 10 s; 45 s cooldown.
    - Bonky Bounce (level 36): a long forward leap; 75 s cooldown.
  - Bonky's hammer re-inflates for about 3 s after every swing, and he is slow meanwhile (25 % shorter with "Swing
    and a Miss!" after a miss).
  - Midnight: from 10 min Bonky keeps speeding up (not with Brainiac); at 15 min every survivor is revealed.
  - If Bonky leaves: "Bonky left the game, keep getting those generators!". If Bonky leaves before the start,
    another player is picked. When one player is left: back to the intermission, "Not enough players to start".
  - VR players are forced into Seated Mode. Votekicks are disabled during a match.
- **Perks** (WIKI table, unlock level, role, effect; 24 rows plus Brainiac):

| Perk | Lv | Role | Effect |
|---|---|---|---|
| Tech Whiz | 2 | S | failed test loses 5 % instead of 10 % |
| Swing and a Miss! | 4 | B | after a miss the slow lasts 25 % less |
| Expert Medic | 5 | S | heal others 30 % faster |
| Danger in Numbers | 7 | B | a generator worked by 2+ players is revealed for 10 s (75 s cd) |
| Get Hyped! | 8 | S | after a heal, both get +50 % speed for 5 s |
| Terrifying Presence | 10 | B | survivors in the aura regenerate stamina 50 % slower |
| Signal Boost | 13 | S | after a generator completes, Bonky is revealed to you for 15 s |
| Danger Zone | 15 | S | when a teammate is caged, +50 % speed for 5 s |
| Butterfly Effect | 16 | B | caging scrambles the next test on all generators |
| Brave Scout | 17 | S | within 10 m of a visible Bonky, reveal him to the team for 5 s (60 s cd) |
| Best Friends Forever | 19 | S | after uncaging, +60 % heal speed until the next heal |
| Gathering Storm | 20 | B | after an ability, +25 % speed for 10 s on the next hit |
| Powering Up! | 23 | S | +15 % max stamina per completed generator |
| Ante Up | 24 | B | ability cooldown +20 %, -20 % per eliminated survivor |
| Adrenaline Rush | 27 | S | stamina recharges 50 % faster while injured |
| Eye See You! | 28 | B | a bonk reveals the others within aura distance for 5 s |
| Midway Magic | 30 | B | Bounce farther, Sneak and Radar last 5 s longer |
| Escape Enchantment | 32 | S | with 1+ generator done, escape a cage by honking its horn |
| Sabotage | 34 | B | bonk a generator: its next test is scrambled |
| Squad Up! | 38 | S | repair 5 % faster per teammate at the generator |
| Silent but Deadly | 40 | B | aura 50 % smaller, Bonky 10 % slower |
| Leaving So Soon? | 42 | B | a generator first reaching 70 % is revealed; stealth + 25 % speed for 5 s |
| Stop Hitting Yourself | 44 | S | when Bonky hits you he is stunned 5 s (then off until you are caged) |
| Power Drain | 45 | B | caging makes unoccupied generators explode and decay twice as fast |
| Brainiac | ? | B | after an ability, reveal the closest survivor for 5 s; no midnight speed-up |

Up to 3 perks can be equipped (rr_party.md 4.1). All perks unlock between levels 2 and 46.

### 2.2 Bundle content for the final version
- rr_party.md 4.2 is complete: the hammer, clown dummy, fair food, standees, heartbeat and ambience. Nothing of the MITM
  map, generators, cages, photo booths or the Merry Midway Paintball map is in the plain bundles: both were Rec
  Room Studio / level content. Searched objects.tsv for midway, bonky, mitm, ferris, carnival, circus, clown and
  midnight: only UI bits and skins came up.
- **The hammer's final config is the Paintball version.** `(Paintball_SplatHammer)` (B43 #6323783143975840158, key `51d`)
  in the final build:
  - `MeleeWeapon`: player damage 100 on every body part, AI damage 67-100, damageType 1000, `canBlockWeaponAttacks` 0.
  - A charged-attack explosion (paint burst): radius 7 m, force 30, damage 100, `cooldownSecondsOptional` 6 s,
    VFX `(Paintball_SplatHammer_Paint_Explosion_VFX)`, decal `[PaintballImpactDecal_Large]`.
  - While held: `walkSpeedModifier` 1.4, `teleportCooldownMultiplier` 0.6.
  - Audio: `hammerInflate` sfx_pm_splathammer_inflate_01 vol 0.5, `hammerReady` _ready_01,
    `hammerDeflate` _deflate_01, `hammerSplatBegin` splat_01/02 vol 0.48.
  - Grip: LockOnPickup 1, right-hand pickup pos (-0.011,0.146,0.146) rot (-0.271,0.271,0.653,0.653).
  - These are **Paintball** tunings. For MITM use the wiki numbers instead: one hit injures, a second hit cages,
    3 s re-inflate.
- MITM-themed UI in the final bundles:
  - `Merry_Midway_Border` sprite (f124b399 #6987057044661087351, a 100 x 100 9-slice border).
  - `(Border_Panel_MerryMidway) Header/Body` (77ea7f3e...).
  - `ClashOfClowns_Standard/Accelerated/Ultimate_Icon` (f124b399 keys `d17` / `f38` / `3c3`; battle-pass icons).
  - `BattlePassConfigContent_MerryMidway2025` (0b8339ad... #3839587973801128266).
  - None of these is a MITM cover. rr_watch_ui.md row 10 (the Bonky-face hammer skin) stays the best stand-in.

### 2.3 Changes vs existing research
- rr_party.md 4.1 is the final rule set (no rework happened). Add the numbers and the perk table from 2.1.
- rr_party.md 4.3 GUESSes to replace with the wiki numbers:
  - Bonky "x0.5 during re-inflate" is still a GUESS for the slow-down factor; the 3 s re-inflate is confirmed.
  - The skill check needs the fail rules above (-10 %, alert Bonky, sabotage scramble).
- Add Bonky's Inferno (FFA hammer brawl) as an optional cheap mode on the same fairground.
- Add the Merry Midway note: Rec Room's own last content reused the MITM fairground for Paintball. Coordinate the
  fairground kit with the shooters builders, one kit for both.
- rr_party.md 4.3 step 4 says "strip MeleeWeapon" from key `51d`. That still applies. Note that `51d` is now the Paintball
  Splat Hammer, whose charged explosion must not be copied into MITM.

## 3. My Little Monsters (MLM)

### 3.1 Final version (sources: NEWS, WIKI My_Little_Monsters, News_Archive_2025)
- Released 2024-06-13 (WIKI), 1-8 players, built entirely in Rooms 2.0. NEWS 2024-06-18: "Added Cornelius Cryptid. He's
  in need of a new monster caretaker, head over to ^MyLittleMonsters".
- Later changes:
  - 2024-07-30: Rec Room Studio's default assets and a kit-based Template Room are built from MLM assets ("My
    Little Template").
  - 2024-08-06: the **Stone Axe crafting cost** drops to **1 Wood, 1 Stone, 1 Crystal**, and objective markers show
    on wood, stone and crystal right outside the first portal.
  - 2025-02-19 and 2025-06-24: MLM Holotars (the NPCs are recorded Holotars) fixed, and shown on Quest.
  - Nothing else up to the shutdown.
- A 2025 CEO post (WIKI News_Archive_2025) says MLM "earned about 10% of its dev cost". It was the last big Rec Room
  Original. There is no sequel or rework.
- The rr_party.md 5.1 rules (NPCs, sanctuary, 6 growth stages, XP numbers, the Evergrown light timer) are the final
  version.

### 3.2 Bundle content for the final version
Unchanged from rr_party.md 5.2: only the dango food, clothing and spray skins exist. Searched objects.tsv for cryptid,
sasquatch, mothman, chupacabra, evergrown, sunstone, sanctuary and MLM: 7 GameObjects, 4 prefabs, 6 meshes, all
dango. The MLM template assets live in Rec Room Studio and on the server, not in the install.

### 3.3 Changes vs existing research
- None to the rules. Add the Stone Axe cost (1 Wood / 1 Stone / 1 Crystal) and the first-portal objective markers to
  the Evergrown economy if it is ever built. The reduced-scope plan of rr_party.md 5.3 stands.

## 4. Punch Up

### 4.1 Final version
- **Never released**, so there is nothing to update. A developer, Gribbly, on the Steam forum on 2017-07-11 (thread
  "Punch Up."): "We want to do it! We think it would be fun. We've built one simple prototype maaaaany months ago and
  it was a bit meh". It was deprioritised for Go Karts and Quests.
- Easter eggs:
  - A boxing glove in a trash bin in the Laser Tag map CyberJunk City (WIKI).
  - A boxing dummy in the 2016 Changing Room (NEWS 2016-07-06).
- Boxing gloves existed as a full-body **avatar item** by 2024 (NEWS 2024-04-16 "completely hide hands (ex: boxing
  gloves)"). They are server-delivered, not in the bundles: searched objects.tsv for "glove" and "boxing", and the only
  hits are UI content lists.

### 4.2 Changes vs existing research
None. rr_party.md 6 ("our own design") stands. The punching bag `88`, `DingDing`, `DummyPunched1/2` and the crowd clips
are final data.

## 5. Quest for the Golden Trophy (Goblin1)

### 5.1 Final version (sources: WIKI Quest_for_the_Golden_Trophy / Quests / Quest_Rating, NEWS)
- **History:**

| Date | Change |
|---|---|
| 2017-02-10 | Released. |
| 2018-12 | Back holsters: a weapon in each hand plus one on the back. |
| 2019-06 | Oculus Quest port. |
| 2019-10-15 | "Golden Trophy **Library**" fix: act 6's name. |
| 2020-02-03 | Bow nock and release on the main trigger; **merch booth added to the ^GoldenTrophy lobby**. |
| 2020-05 | Its goblins became the first Maker AI (the `(QuestAIController)` spawners the build uses). |
| 2024-04-02 | Bow and crossbow holstering fixed. |
| 2025-07-24 | **Roomie** (RR+ AI robot companion) helps in Golden Trophy. |
| 2025-09-23 | Loot chests work again. |
| **2025-12-01** | **RRO mods** (below). |
| 2025-12-08 | Mod badges; Quest-lobby scoreboard start-button fix. |
| 2025-12 | "Goblin King Christmas 2025" Santa variant (WIKI image; seasonal, skip). |

- **Players** 1-4. It is a pickup room.
- **Lobby** (WIKI walkthrough):
  - The team gathers "in front of the **theater stage**". Someone points a laser at the "Start" button and pulls
    the trigger.
  - After the start, **Coach says "Game On!"** (WIKI Tips: "After Coach says 'Game On!'").
  - The lobby has the **merch booth** (2020).
- **Dressing room** (the first room after the start):
  - A mirror on the left wall, decor and gold pots.
  - **2 shields, 4 swords and 2 bows** on the back wall.
  - Everyone gathers on the platform and teleports through the door at the back wall.
- **Deaths:**
  - **One hit downs you** (WIKI: "Like all enemies, they would kill you in one hit, though you could block their
    swords with a shield"; Quests page: "one hit from an enemy (or an ally's ranged weapon) will down you").
  - A teammate revives you with a **high five**.
  - All players down = the run is lost and everyone goes back to the theatre stage. There are no checkpoints.
  - A death **or friendly fire costs 2,000** (bundle 2000 / 2000 agrees).
- **Act list** (final; WIKI walkthrough, marked where it corrects rr_quests 1.2 / 4.1):

| Act | Content |
|---|---|
| 1 | Sword goblins in small groups. Several waves in the final room. Clearing plays a "distinct tune" (the clear fanfare). |
| 2 | Imps introduced (arrows stun them). A side room on the right has 4 large pots and a **crossbow**. Waves spawn behind you; goblins and imps spawn at the top of a ramp. |
| 3 | The big stone **boulder** rolled down ramps onto groups; imps spawn in the rooms behind. |
| 4 | Large lava room with bridges; **barrel knights** introduced. Imps over the lava (one hit, they fall in); goblins from every side except the exit. **Two side tunnels (left and right) each spawn 2 extra goblins when entered** (optional, 1,800 points). |
| 5 | Split path; **elite goblins** (not stunned by arrows) and **elite imps** (5-fireball spread) introduced. At some point 2 melee goblins spawn behind you. High platforms, walls and pillars give cover. |
| **6** | **A library** (NOT "courtyard towers"): waves in different rooms, from nearly every side including behind. **Elite barrel knight introduced (it spins instead of charging)**. Players can **stand on bookshelves**. The room to the right is safest. **A left tunnel spawns 2 elite goblins when you enter it** (1,700 points). A raised platform with a ramp gives high ground. |
| 7 | An easier act with a **small maze** (a barrel knight inside; spawns ahead and behind at random). It ends in a larger room with elite imps and goblins. |
| 8 | Half lava, half wood; a trigger at the top of the spawn ramp. Waves: 1 elite + 4 goblins; then 3 elite + 2 goblins; 2 elite flyers + 2 flyers (over the lava or in the back corner); 5 elite goblins; **2 elite barrel knights**; 2 barrel knights + 1 elite flyer; a final wave of everything. |
| 9 | **Goblin King**: a giant barrel knight with an axe and a big imp cannon firing **3 large orbs** at a player he sees. He turns to whoever hits him. Only the **target on his back** takes damage; bows and crossbows are on the raised platform. **Shields stun him** (dual shields keep him stunned). **3 waves of adds, triggered by his health**. **His death kills every other enemy**, and each player gets a treasure chest. |

- **Enemy HP** (WIKI table = the bundle spawners, rr_quests 2.2.1):
  - Goblin 100, Imp 100, Barrel Knight 250.
  - Elite Goblin 200, Elite Imp 200, Elite Barrel Knight 333.
- **Scoring** (WIKI "Scoring"; final):
  - Pots: small 50, large 100.
  - **Kill points: Goblin 450, Imp 500, Elite Goblin 850, Elite Imp 850, Barrel Knight 1,500, Elite Barrel Knight
    2,000, Goblin King 5,500.** Points are split by damage share.
  - Death or friendly fire: -2,000.
  - **Rating from total team gold: C 0-100k, B 100-125k, A 125-150k, S 150k+.** The maximum is "slightly over 180k".
    Optional trigger spawns are only paid if killed.
  - Rewards: the chest item comes from the Golden Trophy sets by rank (Archer/Knight black at S; WIKI Quest_Rating).
- **RRO mods** (NEWS 2025-12-02, WIKI Timeline):
  - They are activated on a new **mod board** with "RRO Mod Tickets", the backpack consumable
    `(RROModification_Paintball_Ticket)`, renamed "RRO Mod Ticket".
  - **Turbo**: "everything is faster".
  - **Coconut Chaos**: "Coconut Bombs drop from the sky".
  - **Laser Weapons**: "Swaps all weapons in the quest for Laser Tag variants".
  - Badges: **Turbo Goblin** (beat it with Turbo + Coconut Chaos) and **Turbo Goblin Master** (20 times).
  - The mods' numbers are not published (GUESS below).
- **Roomie** (NEWS 2025-06-17, 07-08, 07-24; WIKI Roomie, News_Archive_2025):
  - An RR+ AI robot companion that follows its owner.
  - "While in a Quest, Roomie will prefer to be holding the sword. At the beginning of 'Taunt' states, Roomie will
    target the nearest enemy with an attack sequence capable of applying a stun effect."
  - The note says Golden Trophy first, "other quests soon". Only Golden Trophy is confirmed.
- **Bow "Double Shot"** (NEWS 2024-11-23): "Double Shot has returned for the bow!!! We worked on a fix for the longbow
  to allow Double Shot while still preventing infinite shot". This is a longbow trick that fires two arrows
  (mechanics GUESS). The note is in the Crescendo port update, but it is about the longbow Golden Trophy uses.

### 5.2 Bundle content for the final version (add to rr_quests 2.x)
- Everything in rr_quests 2.1-2.9 is final data. The build already uses the manager, spawners, weapons, pots and music.
- **Coach "Game On!"**: `Gameon` AudioClip, bundle `8b1b2e31c260b5671d9d4112d99a8b97` #3496784870384460132, key `e`
  (the same clip rr_shooters.md 7.1 lists for Paintball).
- **Library dressing for act 6**:
  - `(Sandbox_Book_Yellow)` B43 key `2e` #-7367355296382360499 and `(Sandbox_Book_Green)` B43 key `e82`
    #6641017581094378083. These are the only keyed book props.
  - There is no bookshelf prefab: build the shelves procedurally from the wood material and fill them with books.
- **Coconut Chaos**: `(Quest_Pirate1_CoconutBomb)` B43 key `91` #7371352274782639299 (rr_quests 2.3: Grenade 3000, 80
  damage, blast radius 2 m, force 10, fuse-spark VFX; the wiki says about a 4 s fuse; you can grab it and throw it back).
- **Laser Weapons**: Laser Tag guns (rr_shooters.md 3.2). All reload by pulling the handle.
  - `Arena_Pistol` key `61b` (5 shots, flip reload, AI damage 18).
  - `Arena_AutomaticGun` key `d2f` (22-round magazine, AI 8).
  - `Arena_Shotgun` key `7f` (3 shots, AI 16 per pellet).
  - `Arena_RailGun` key `a3` (3 shots, AI 70).
- **Roomie** (optional): bundle `4056f2fc0e2635c36096ef5d5294708e` (7.9 MB, 1,832 objects).
  - `(RoomieTool)` key `d` #-2499637652182519608. Components: Tool, RigidbodyPickup, RoomieTool, RoomieLocomotion,
    RoomieAttention, RoomieStimuliReactions, RoomieSoundboardPlayer, voice filters. The Roomie scripts are missing at
    runtime, so this is visuals only.
  - Rig `Roomie_Mark1_Rig` (Animator + LODGroup), AnimatorController `Roomie` #-5675842480881514332,
    `[RoomieMesh]`, `Roomie_Mk1_LOD1`, material `Roomie_Engine_Mat`, loop `sfx_roomie_movement_synth_loop_01`.
  - **71 AnimationClips** `Roomie_Skeleton|R_Mk1_*`, including **Sword_FightIdle_Enter01/Exit01, Sword_Idle01,
    Sword_FightAttack01, Sword_Retrieve01, Sword_PutAway01, Sword_FlyForward01, Sword_TurnLeft01/Right01,
    KnockedOut01**, FlyForward01, Idle01, HighFive_Left01/Right01, Panic_*, Scared_*.
  - It is a skinned, animated flying robot: the only animated sword fighter in the bundles.
- **Mod board**: not in the bundles; it is level content.

### 5.3 What the first build has that is outdated (CODE/README "The quest framework and Quest for the Golden Trophy", Games/Quests)
1. **Player health.**
   - Build: `RrQuestDef.PlayerHealth = 100`, enemies deal 15-50 per hit (RrEnemy defaults; king charge 40, orbs 20), and the HUD shows
     "HP n".
   - Final: **one hit downs** (a shield block cancels a melee hit).
   - Change: set player health to 1 hit. Either PlayerHealth 1, or treat any EnemyAttack hit as down. Drop the HP
     readout from the HUD and keep only the down state.
   - This also makes the 3 s post-revive invincibility the only damage protection.
2. **Kill points.**
   - Build: `PointsFor` = enemy max HP (100/100/200/200/250/333; the king 3000 x party scaling).
   - Final: **450/500/850/850/1500/2000/5500**. Override `PointsFor` in RrGoldenTrophyRoom with these. The build's
     death penalty 2000 and pots 50/100 already match.
3. **Rating.**
   - Build: `RatingS/A/B = 0.85/0.65/0.45` of max possible (GUESS).
   - Final: absolute gold thresholds S >= 150,000, A >= 125,000, B >= 100,000, else C.
   - With the real points the build's MaxPossible should land near Rec Room's ~180k. If our acts hold fewer enemies,
     scale the thresholds by MaxPossible / 180,000, which gives S 0.83, A 0.69, B 0.56.
4. **Act 6 "The Courtyard Towers" is wrong.**
   - It is a **library**: tall bookshelves players can stand on, several rooms (a safe room on the right, a left
     tunnel whose entry spawns 2 elite goblins), and a raised platform with a ramp.
   - **Elite barrel knights (spinners) first appear here.** Build type 5 KnightRed/`GoblinKnightSpin`: move it here
     if it appears earlier.
   - Rename the act and rebuild its geometry. Bookshelves: procedural boxes with `(Sandbox_Book_*)` props, flat
     tops walkable.
5. **Dressing room and lobby.**
   - The build's lobby is a castle courtyard with 4 swords, 4 shields and 4 bows on racks.
   - Final: a **lobby at a theatre stage** (start button, merch booth, scoreboard), then a **dressing room** with a
     mirror, pots, and **4 swords, 2 shields, 2 bows** on the back wall. The crossbow first appears in act 2
     (build: correct).
   - Change the rack counts. GUESS: keep our refill racks, but with 4/2/2.
   - Optional: split the lobby (theatre stage + our portal + start button) from a dressing-room act 0 that everyone
     teleports out of together.
   - Play `Gameon` (key `e`, 8b1b2e31) when the run starts.
6. **Optional act-4 tunnel spawns** (+2 goblins per tunnel on entry, paid only if killed): add them if act 4 has
   side tunnels.
7. **Act 8 waves** should follow the wiki order in 5.1 (1E+4G, 3E+2G, 2EI+2I, 5E, 2 elite knights, 2 knights + 1 EI,
   everything). The build's waves are GUESSes.
8. **The Goblin King.**
   - Final: shield hits stun him, 3 add waves by health (the build's every 25 % = 3 waves, OK), **his death kills all
     remaining enemies** (the build calls `Enemies.KillAll()` only in the debug hook `DebugClearAct`, so add it), one chest per player.
   - The build's charge (10 m at 10 m/s, 40 damage) becomes a one-hit down.
   - 3000 HP stays a GUESS (the wiki gives no HP; only the 5,500 points).
9. **Mods (new, final content).** Add a lobby "mod board" with three toggles, free (no tickets). They are
   authority-side flags in the quest ints, so every client applies them.
   - Turbo, GUESS: enemy move, attack and projectile speed x1.5, wave delays x0.67, player walk x1.25 through our own
     speed hook.
   - Coconut Chaos, GUESS: every 6-10 s, over each living player, drop a `(Quest_Pirate1_CoconutBomb)` from 12 m with a
     4 s fuse; it damages enemies and players; players can catch and throw it.
   - Laser Weapons: racks give Laser Tag guns instead. GUESS mapping: sword -> `Arena_Shotgun`, shield ->
     `Arena_AutomaticGun`, bow -> `Arena_RailGun`, crossbow -> `Arena_Pistol`. Rec Room did not publish its mapping.
     Laser hits count as ranged: they stun basic goblins and imps like arrows (GUESS).
   - Optional: two badges as log lines or a toast ("Turbo Goblin" for a Turbo + Coconut Chaos win; a 20-win counter
     needs persistence, skip).
10. **Roomie** (optional, RR+ only in Rec Room).
    - A local companion robot for the owner: spawn `(RoomieTool)` visuals (key `d`, 4056f2fc), drive its Animator with
      the Sword_* clips, give it a quest sword, and every ~8 s "taunt" = attack the nearest enemy for a stun.
    - Never attach anything to the Somnium avatar: Roomie follows the player's head position as a separate object.
    - GUESS whether it is worth it. It was a paid, AI-voiced feature.
11. **Revive.** The build's bleed-out (30 s, back to the checkpoint) has no source; Rec Room has no checkpoints in
    Golden Trophy. Keep the rr_quests GUESS "revived at the start of the next act, the team fails when all are down",
    and mark the 30 s bleed-out as ours.

Corrections to rr_quests.md:
- 1.1: "100 HP ... GUESS 10-35": wrong, one hit downs.
- 1.2: act 6 is the library (not "castle courtyard/towers"); the spinning elite barrel knight comes in act 6; the
  dressing room holds 4 swords, 2 shields, 2 bows; add the per-enemy points and absolute rank thresholds.
- 2.7: "No narrator VO for quests": Coach's `Gameon` clip exists and is used at the quest start.
- 4.1: the act 6 layout and the lobby/dressing-room split.

## 6. The Curse of the Crimson Cauldron (Goblin2)

### 6.1 Final version (sources: WIKI The_Curse_of_the_Crimson_Cauldron / Quest_Rating, NEWS)
- **History:**

| Date | Change |
|---|---|
| 2017-08-18 | Released; 1-4 players. |
| 2017-08-22 | **Hunter's Crossbow fire rate reduced**; the "ham sword" can reflect witch orbs; **mini-witches no longer react to hits from regular weapons** (only reflected orbs, potions and explosions hurt them, GUESS from the wording). |
| 2017-11-03 | Boss fight no longer stalls when enemies are knocked out of the windows. |
| 2018-04-27 | Knocking witch orbs back with the sword fixed. |
| 2018-12 | Back holsters. |
| 2020-02 | Merch booth in the lobby; Oculus Quest port 2020-02-20. |
| 2020-05-13 | "Improved visibility and flow in the **ladder section of the bell-tower**"; **Pike Goblins' shields react physically to projectile hits**; ramps added where players got stuck. |
| 2020-07-09 | The witch no longer flies outside the boss room. |

  Nothing after that. It is the "longest and most challenging" quest at launch, with no checkpoints.
- **Weapons** (WIKI):
  - Sword and shield (one-handed or dual).
  - Bow.
  - **Hunter's (manual) Crossbow**: one hand holds it, the other charges it, or flip it to reload.
  - **(Automatic) Crossbow**: one-handed, dual-wieldable.
  - **Wand**: one-handed or dual.
  - **Attack Potion**: doubles damage for a while. Thrown or smashed on an enemy it deals one full arrow of damage,
    and it damages Shield Goblins' shields and Barrel Knights' fronts.
- **Enemies** (WIKI names -> bundle spawners, rr_quests 2.2.1). The walkthrough says "green" for basic and "red" for
  elite.

| Wiki name | Behaviour | Spawner (key) |
|---|---|---|
| Goblin (a "runner") | charges mindlessly | GoblinBasicGroundMelee2020 (`c13`) |
| **Shield Goblin** | charges; a shield protects its front (the potion breaks through) | **= GoblinBasicMeleePike (`2`)**; the 2020 note calls them "Pike Goblins' shields" |
| Cannon Goblin (the walkthrough's "**Bomber**") | fires slow arcing cannonballs with splash; stands on platforms | GoblinBasicGroundRanged (`82`) + `Goblin2_Mortar_*` SFX |
| Elite Goblin | red; not knocked down by arrows | GoblinEliteGroundMelee (`76c`) |
| Imp (green flyer) | single fireballs | GoblinBasicFlyingRanged (`42`) |
| Elite Imp (red flyer) | 5-fireball spread | GoblinEliteFlyingRanged (`082`) (and/or GoblinVolley `c29`, GUESS) |
| **Witch / Imp Witch** (mini-witch) | large energy balls you can **reflect** with a sword, shield, potion or pot | no spawner (boss-only type, GUESS; build a flyer from the imp mesh + a witch hat) |
| Barrel Knight (green) / Elite Barrel Knight (red, spins) | back target only (and "the back of the head") | GoblinKnightCharge (`e0`) / GoblinKnightSpin (`a18`) |
| Bog monster | "a lowly bog monster will rise up from its home" at the end of the Bog act | not in bundles (mini-boss, GUESS) |

- **Act list** (final; this **replaces the GUESS list in rr_quests 4.2**). Every act ends with "a distinct triumphant
  sound".

| Act | Name | Content (WIKI walkthrough, summarised) |
|---|---|---|
| 1 | **Castle Courtyard** | Right: 4 goblins + 1 bomber on a platform. Left: 1 bomber + 2 shield goblins, then a 50/50 chance of 2 flyers (one red, one green). Two bombers on adjacent platforms. Aggroing the second one wakes **6 shield goblins guarding the exit** (use a potion). |
| 2 | **Forest Path** | Through the forest to the campsite. 3 goblins; kills trigger 2 flyers + 2 runners; 5 flyers on a tree branch. The **Hunter's Cabin** holds double-damage potions ("ten second delay"). A small room to the right. Goblins next to **2 explosive barrels**. Then red goblins behind, waves in several rooms, 3 bombers back to back, flyers blocking the path. |
| 3 | **Campsite** | Large open room with a campfire and **explosive barrels**. 2 bombers + 3 goblins; 5 more goblins around the barrels; 3 elite goblins behind + 2 bombers; flyers; a bomber on a pillar; 4 elites + a bomber; a final mixed wave. |
| 4 | **Deep Forest** | Large enclosed forest. **Imp Witches** at the spawn (reflectable orbs), then green goblins and 2 more witches; bombers at a **stairwell tree platform**; spawns behind you on the stairs; shield goblins; 5 green flyers; elites by position; bombers on platforms. |
| 5 | **Bog** | A horde of imps. **Use floats/buoys to cross the swampy water** (walkers can climb a large rock on the right). 2 bombers; flyers; an Imp Witch, then 2 more; everything including elites and bombers; then the **bog monster** rises. |
| 6 | **Mines** | "**The steam kills you**" (steam-vent hazard). Mostly green enemies: 4 cannon goblins; 5 flyers next to an explosive barrel; **goblins playing ping pong**; many shield goblins; 1 bomber + 2 green barrel knights; 4 elite goblins behind you at the exit platform. |
| 7 | **Deep Mines** | 6 elite goblins + 1 elite barrel knight at the top of a ramp next to an explosive barrel. 2 green barrel knights: **killing one spawns runners, the other spawns flyers**. More flyers + 2 elites near the barrels. |
| 8 | **Bell Tower Stairs** | Climb slowly: one player guards the stairs, another clears imps at the top. Explosive barrels. The 2020 update made the **ladder section** clearer. |
| 9 | **Finale** | The **Crimson Witch** in the bell tower. Bat her purple orb back 3 times (first phase, no adds), then 4 times per later phase with more and more enemies. Players often stand in a **window**. Knocking enemies out of the windows is a thing (2017 fix). |

- **Scoring**:
  - Gold for kills (split by damage) and pots: small 50, large 100.
  - Death -2,000 (bundle); friendly fire with a ranged weapon costs "a moderate" amount (bundle: also 2000).
  - **Rating (WIKI Quest_Rating): S 300k+, A 275-300k, B 250-275k, C 0-250k.** That is twice Golden Trophy's scale,
    so per-enemy points are larger or the quest is much longer. Per-enemy points are not published (GUESS: the
    Golden Trophy values x1.5-2).
- **Music**:
  - "Exploring the Mines" `CrimsonCauldronExploringtheMines`.
  - "Goblin Work Song" `CrimsonCauldronGoblinWorkSong` (it became a Sandbox track in 2018-04).
  - "The Ballad of Vota Vader" `CrimsonCauldronTheBalladofVotaVader` (all in rr_quests 2.7).
  - Lose stinger "Crimson Cauldron - Our Heroes Have Failed".

### 6.2 Bundle content for the final version
- rr_quests 2.1-2.9 is final. Rule circuit `935`; weapons `Crossbow_Hunter`, `Quest_Goblin_Wand`,
  `(Goblin2PotionConsumableTool)` 85515de1 key `8b2` / `(HandPotionConsumable_Goblin2)` key `b`; potion SFX
  `Goblin2_Potion_*` (1df8e0ef keys `09d`, `2d`, `364`, `37e`, `3a`, `5db`, `ac1`, `caf`, texture
  `Quest_Goblin2_Potion_Fire_Tex` `79f`).
- **Bog water**: `Water_Swamp_Mat` (18c123d6ac91200e89ad964373be8ed2 key `b` #-2294586719653009647, shader AG/TerrainWater).
  - DEAD END warning from the build (CODE/README): AG/TerrainWater materials draw nothing on a box. Use our own swamp
    surface, or test the material on a real terrain-water mesh.
- Explosive barrels: `(MetalBarrel_Sandbox)Variant` `79` + `[Quest_Goblin2_VFX_BarrelExplosion]` (rr_quests 4.2).
- Nothing in the bundles for the witch boss, the Imp Witch, the bog monster, the steam vents, the ping-pong goblins
  or the bell. A bell prop is `(Sandbox_Bell)` (rr_quests 4.2).

### 6.3 Changes vs existing research (unbuilt)
1. **Replace rr_quests 4.2's GUESS act list** (mine entrance, rail tunnel, cavern lake, forge, lever puzzle, goblin
   village, chasm bridges, tower stairs, bell tower) with the real 9 acts in 6.1.
   - The quest starts **at a castle and goes through forests and a bog before the mines**. It is not mines only.
   - The "puzzle solving" of the 2017 announcement is light: explosive barrels, the potion-vs-shield rule, buoys across
     the bog. There is **no documented lever puzzle**. Drop that GUESS.
   - The "hazards" are **steam vents that kill** (Mines) and the bog water.
2. **Rename "pike goblin" to Shield Goblin** in gameplay terms: a frontal shield that blocks weapon hits; only the
   attack potion, or hits from the side or back, get through (GUESS for the side/back). "Bomber" = cannon goblin.
3. Add the **Imp Witch** as a regular enemy (acts 4 and 5), not only the boss:
   - Its orbs are reflectable.
   - After the 2017-08-22 patch it **does not react to regular weapon hits**, so reflecting its orb is the way to
     kill it (GUESS on exactly what hurts it).
4. Add the **Hunter's Cabin** potion stash (act 2), **explosive barrels** in acts 2, 3, 6, 7 and 8, the **bog monster**
   mini-boss (act 5, GUESS: a large tentacled or rock creature built from primitives; HP ~ an elite barrel knight x3),
   and the **killing a barrel knight spawns a wave** triggers (act 7).
5. Boss: rr_quests 4.2's Crimson Witch (3 then 4 returns per phase) matches the wiki. Add the **window ledges** as
   the standard fighting spot.
6. Ranks: use absolute S/A/B/C = 300k/275k/250k (scale by MaxPossible if our acts are shorter).
7. **One-hit downs** (as in 5.3 item 1).
8. rr_quests 4.2 lobby racks (4 sword, 4 shield, 4 bow, 2 hunter crossbows, 2 wands): no source for the counts.
   GUESS: use Golden Trophy's 4 swords / 2 shields / 2 bows plus potions, and place the crossbows and wand in acts.

## 7. Crescendo of the Blood Moon (Dracula1, "CBM", ^Crescendo)

### 7.1 Final version (sources: WIKI Crescendo_of_the_Blood_Moon / Quests, NEWS)
- **History:**

| Date | Change |
|---|---|
| 2018-11-15 | Released; **1-3 players** (WIKI infobox and the Quests page). |
| 2018-11 | Patches: pumpkins and candlesticks give silver; killing decoy bats and their waves gives no silver; the Grand Hall no longer leads straight into the boss fight. |
| 2019-10 | "Burning Burg" collision fix. |
| 2019-11-12 | Planks on the scaffolding "in the CBM Dungeon"; the Dungeon gate message; a floating chain in the Grand Hall; **the pitchfork can crit**; rum jugs shatter when blocked. |
| 2020-05, 2021-04 | Fixes. |
| 2022-02-16 | Dracula no longer flies outside the map. |
| 2023-02-22 | "Dracula finally learned to aim" (his fireballs hit his own cape). |
| 2024-04-02 | Crescendo lobby inventory slots behave like in the game. |
| **2024-11-23** | **Ported to Mobile and Quest** (WIKI Update History). Occlusion culling and mobile lighting; "Fixed a bug where the **mirror kept shooting every second once fired**"; "**Re-added the Dracula bat animation**. Now you know who's who"; a Garden of Sorrows whip fix; "**Double Shot has returned for the bow**" (a longbow trick; not in Rec Royale). |
| **2025-10-15** | New **"Vampire Hunter" profile badge** for an **S rank** in ^Crescendo. |

  No level or rule rework. The 2024 port is the final state.
- **Structure** (WIKI "Act Order"; 13 acts, the Grand Hall hub between levels; each return **banks silver** and is
  the checkpoint):

| # | Act | Content | Mini-boss (WIKI name; bundle coin drop, measured value) |
|---|---|---|---|
| 1 | **Burning Village** ("Burning Burg" in the 2019 note; its third part is the "Country Farm") | Spawn in a house; shovels and pitchforks from hay piles. 3 waves of Pumpkin Spicers, then a gate. Part 2 adds Fire Bats + Skeleton Maids, then a gate to **chests, whips and blunderbusses**. Part 3: Dive Bats after a Maid exits the Shop, then a **carriage** to the next act. | - |
| 2 | **Forest Road** (carriage ride) | Waves from both sides of the moving carriage: **Barrel Dogs** introduced, red swooping bats. "Easily the hardest level in solo mode". You can shoot from the carriage seats. | - |
| 3 | **Castle Grounds** | After the carriage crash: Marksmen, Barrel Dogs, Maids; a door you tear down with the whip into the Grand Hall. | - |
| 4 | **Grand Hall** (1st visit) | Hub: 4 random chests, chandeliers (whip-grapple), candles (small 2, large 3 silver). The next door is on the 2nd floor, right. | - |
| 5 | **Gallery** | Main hall, locked gate, mini-boss room. | **Maid Warlord** (fiery bones that explode after landing; adds from the tunnels); `(Quest_Dracula1_MiniBossCoins_BoneThrower)` key `12a` **100** |
| 6 | **Chamber of Knowledge** | Several locked gates with waves; **push-in books** open secret shelves; the right book opens the door; whip-breakable walls. | **Mayor Marksman** (5 quick shots, pause; the platform spawns 2 pumpkin skeletons + 2 barrel dogs + 2 red bats); `_Musket` key `efb` **400** |
| 7 | Grand Hall (2nd) | Garden door on the 1st floor, right. | - |
| 8 | **Garden of Sorrows** | Pond with waves on both banks (every enemy except barrel dogs), a gazebo, a graveyard (minions rise from gravestones). | **President Pumpkin Head** (slow, block his swings like ILS); `_BoneMelee` key `47e` **200** |
| 9 | **Inverted Ballroom** | Two parts: waves on entry; then timed maids and riflemen on tables, then bats, pumpkin heads and a maiden. | **Baron Fire Bat** (purple, a horizontal line of 5 fireballs; jump over it); `_Bat` key `dbc` **800** |
| 10 | Grand Hall (3rd) | The Lower Sanctum door is at the far rear, 1st floor. A small back room unlocks after the Ballroom. | - |
| 11 | **Lower Sanctum** (the "Dungeon" / "Cellar") | **One torch in the dark** (like the ILS caves). A trap room: **arrow traps (they kill; the silver penalty is x0.1), laser walls and swinging axes (these don't kill)**. Then pumpkin heads, red bats, 2 maids on the steps. | **King Barrel Dog** (charges; no adds; a light turns on when he dies); `_Wolf` key `4c` **1600** |
| 12 | Grand Hall (4th) | The Concert Hall door is on the 2nd floor, back room. | - |
| 13 | **Concert Hall** | **Dracula** (below). Waves of everything except Green Fire Bats. | - |

  **Corrections to rr_quests 1.6 / 4.5:**
  - "Carriage Ride" = **Forest Road**; "Cellar" = **Lower Sanctum**; "Dracula" = **Concert Hall**.
  - There are **four** Grand Hall visits, not 9 level doors.
  - **Five** mini-bosses, not four: the bundle's five `MiniBossCoins_*` prefabs match the wiki's five exactly.
  - The mini-boss for the "wolf" is the **King Barrel Dog**, so the wiki's "Barrel Dog" enemy = the bundle's
    `DraculaWolfLunging2020`.
- **Enemies** (WIKI name -> bundle spawner; silver per kill from the WIKI):

| Wiki | Behaviour | Spawner (key) | Silver |
|---|---|---|---|
| Pumpkin Spicer | runs in, swings a bone; 2-3 side hits; two shovels on the head = instant kill; block with a horizontal shovel or pitchfork | DraculaMelee (`b`) | 10 |
| Fire Bat | stops a whip length away, charges, shoots a fireball | DraculaBatRanged (`e6`) | 5 |
| Dive Bat | swoops from a whip length away; touching it **knocks you out**; 1 shovel hit or 2 whip snaps | DraculaBatLunging (`9c`) | 15 |
| Bone Maid | lobs **3 bones** in quick succession; a bone hitting your held weapon **disarms** you; doesn't lead shots | DraculaBoneThrower2020 (`2b`) | 10 |
| Barrel Dog | charging leap attack; extra damage from shovel or pitchfork | **DraculaWolfLunging2020 (`e4f`)** | 25 |
| Marksman | charges and fires one round at the **torso**; doesn't lead; likes platforms; growls on spawn | DraculaBellhop (`549`) | 20 |

- **Dracula** (WIKI; final behaviour):
  - He starts on stage playing the **organ**, speaks, then teleports around the room firing a **line of fireballs**
    (up to 3 waves before he teleports again).
  - Standing in the "dog bite zone" next to him = instantly downed ("wolf bite").
  - Players aim **one of three mirrors scattered around the room** at him. The mirrors fire a light: "after the
    first time of hitting him with a light". The 2024 fix stopped a fired mirror from shooting every second.
  - A hit turns him into a **cloud of purple bats**, and enemies spawn on the floor (one maid on the top-right
    balcony). One bat is the real Dracula: it **spins before swooping** (animation re-added 2024-11) and has **no
    string**. Knocking it out brings Dracula back and **stops all enemy spawns**.
  - After the 1st hit he fires 2 bursts, after the 2nd hit 3. **Three mirror hits kill him.**
  - Balconies are the safe spot. The top-left balcony has a mirror.
- **Weapons** (WIKI; limited uses except the whip):
  - Pitchfork: 5 hits. Shovel: 6-9 hits. Their broken stumps still block and knock out bats.
  - Blunderbuss: **3** shots, no reload, one-handed.
  - **Whip**: infinite. Grapples chandeliers and roped beams, tears flimsy walls, holsters at the hip.
  - Candle stand: one hit, faint light. Pumpkins: one-use, ~2 per skeleton.
  - **Dracula's Flintlock: 9 shots** (a headshot one-shots some enemies). **Crossbow: 19** (WIKI) / **20** (bundle).
    **Hunter's Crossbow: 20**. Rum jug, bottles (once, in the Lower Sanctum), pirate plates.
  - Holsters: the whip on the hip, one other weapon on the back (the hip holds only the whip since a later update:
    at most 4 items).
- **Silver and rank** (WIKI + bundle):
  - Silver per enemy as in the table; mini-bosses 100/400/200/800/1600 (**bundle `customValue` confirms all five**);
    candles 2/3; chests.
  - Banked at every Grand Hall visit. "30k per banking, 150k per run" (a WIKI test).
  - Hidden **combat score**: base **4,400** for a deathless run. S+ >= **4,160**, S >= **3,160**; a death costs
    **-200**, an arrow-trap death **-20** (bundle: penalty 200, trap multiplier 0.1).
  - **Rank bonuses: S+ 15,000, S 10,000, A 6,000, B 3,000, C 2,000 silver** (the bundle LootManager `rankBonuses`
    4:15000, 0:10000, 1:6000, 2:3000, 3:2000 **match**).
  - S+ exists only in CBM. Death estimates: A 6-8, B 9-12, C 12+.
  - Rewards: 4 Vampire Hunter outfits and a Dracula outfit by rank; the **Caravan of Curiosities** gift shop sells 4
    Aristocrat outfits, film, potions and an apple basket for silver.

### 7.2 Bundle content for the final version
- rr_quests 2.x is final. New measurements:
  - Five mini-boss coin piles in B43 (`customValue`):
    - `(Quest_Dracula1_MiniBossCoins_BoneThrower)` `12a` #5611471273139385106 = 100
    - `_BoneMelee` `47e` #8904818310497324779 = 200
    - `_Musket` `efb` #-868188015900875905 = 400
    - `_Bat` `dbc` #-5233950321471998115 = 800
    - `_Wolf` `4c` #-1436563065578243640 = 1600
  - Weapon ammo (`maximumTotalAmmunition`, B43; magazine 1, no infinite reserve, `disablePostTotalAmmunitionExhaustion`
    1):
    - `(Quest_Dracula1_Flintlock)` `cc` = **9**
    - `(Quest_Dracula1_1H-Crossbow)` `08` = **20**
    - `(Quest_Dracula1_Crossbow_Hunter)` `2f4` = **20**
    - `(Quest_Dracula1_BlunderBuss)` `7d` = **3**
- Not in the bundles: Dracula, the mirrors, the organ, the carriage, the traps (arrow shooters, laser walls, swinging
  axes), any level geometry. Library props: `(Sandbox_Book_*)` (5.2).

### 7.3 Changes vs existing research (unbuilt)
1. Replace rr_quests 4.5's level list with the 13-act order in 7.1.
   - Hub visits happen after Castle Grounds, the Chamber of Knowledge, the Inverted Ballroom and the Lower Sanctum.
   - Gallery -> Chamber of Knowledge is one trip (no hub between them).
   - The hub doors unlock in that order.
2. The **carriage act** is "Forest Road": waves from both sides while the carriage moves, and it **crashes** at the
   Castle Grounds. The rr_quests 4.5 moving-platform GUESS still applies.
3. **Dracula**: rr_quests 4.5's GUESS (blood bolts reflected off 4 mirrors, 2400 HP, giant-bat phase every 25 %) is
   **wrong**. The final mechanic, from the wiki:
   - 3 fixed mirrors that players aim; a mirror fires a light beam.
   - 3 light hits kill him.
   - After each hit: the bat swarm, one real bat (spins, no string) to knock out, and the adds stop.
   - Fireball lines: 1, then 2, then 3 bursts.
   - An instant down in the bite zone.
   - The organ intro.
   - GUESS: mirror = a grabbable swivel mirror on a stand, fired by a trigger, with a 1 s beam raycast.
4. Five mini-bosses (Maid Warlord, Mayor Marksman, President Pumpkin Head, Baron Fire Bat, King Barrel Dog). Their
   placement is in 7.1. rr_quests' "miniboss wolf in Castle Grounds" and "bone thrower in the Chamber of Knowledge"
   are wrong.
5. **Lower Sanctum**: a torch-lit dark act with arrow traps (lethal, x0.1 penalty), laser walls and swinging axes
   (not lethal).
6. The Chamber of Knowledge has **push-in book buttons** and whip-breakable walls.
7. Weapon counts from 7.2 (flintlock 9, crossbows 20, blunderbuss 3); pitchfork 5 hits, shovel 6-9 (WIKI).
   - rr_quests 2.3's flintlock "single shot then manual reload (flip)" is right per shot. Add the 9-shot total.
8. The rank system in 7.1: combat score and silver are separate; S+ = at most 1 death solo; the bonuses are confirmed.
9. **Players 1-3** (rr_quests allows 4: keep 4 if wanted, but the final Rec Room cap is 3).
10. **One-hit downs** (see 5.3 item 1): CBM enemies "knock out" players in one hit.

## 8. Isle of Lost Skulls (Pirate1, ^IsleOfLostSkulls)

### 8.1 Final version (sources: WIKI Isle_of_Lost_Skulls / Quests, NEWS)
- **History:**

| Date | Change |
|---|---|
| 2018-03-01 | Released. The launch note: "**Up to three players** can play together"; the WIKI and the Quests page say 1-3. The rr_quests 1.1 "Road to VR says 4" is superseded. |
| 2018-05 | The "Swash and Buckle" store. |
| 2018-09-14 | Its props became Sandbox items. |
| **2018-12-05** | **"Isle of Lost Skulls - Reboot!" / "Rebooty!"**: **checkpoints** ("Continue" / "Start Over" when a checkpoint was reached), **back holster**, QoL and bug fixes; "levels becoming harder and more timely" (WIKI). This is the last gameplay update. |
| 2019-02-07 | Melee weapons auto-lock to your hands. |
| 2020-02 | Screen pickup of throwable boards. |
| 2020-04 | Boat and Town lighting. |
| 2020-10-11 | Quest 2 port. |
| 2022-02-02 | The boss-arena side-platform walkway fixed. |

- **Act list** (WIKI walkthrough, with per-act points; this **replaces rr_quests 1.4 / 4.3's GUESS list**):

| Act | Name | Content | Points |
|---|---|---|---|
| 1 | **Galley** | **No dressing room: the players start locked in a jail cell** in a ship's galley. Break out. Swashbucklers (block their swing with a weapon held in an X to stun them, then hit; they can only be hit when stunned or facing away), in waves. A gate. | 14,000 |
| 2 | **Pirate Banquet** | Swashbucklers, then the first Jug Skeleton and 2 Bottle Throwers. Killing the right-hand thrower spawns 2 more, then 3 on the ceiling beams. A Jug Skeleton on the stairs. Killing the last skeleton silences the music, and the gate starts the **Bar Fight** scene: everything at once, including on high platforms. | 22,000 |
| 3 | **High Seas** | A ship's deck: swashbucklers and a bottle thrower on the mainsail; a jug and a bottle thrower at the wheel behind you; 2 **cannon operators** (man the **left cannon** to kill them); 3 Pirate Captains on the other ship. **Hit a target with a cannon** to lower a bridge; waves of swashbucklers, captains and a jug skeleton. A platform in front of the ship reveals the way on. | 15,000 |
| 4 | **Port Plunder** | The harbour: flintlock pistols and cover; waves of Pirate Captains (one always on the first ship), swashbucklers, jug skeletons on high platforms; a gate that triggers 5 captains, then 4 more. | 27,500 |
| 5 | **Gibbet Jungle** (two parts) | The first **Gibbeted Skeleton**: throw its bombs back, **it only takes explosive damage**. Bottle throwers on side platforms, captains behind, swashbucklers and a jug skeleton in the corners, throwers on a bridge, 2 more gibbets; a paddle in a rope coil. Props bob in the water (2019 fix). | 23,500 |
| 6 | **Caves** | Dark: **one player carries the torch**, the others cannot see. A Jumbotron **mouse bot Easter egg** on a cliff ledge. A "**Not Heavy**" box puzzle: **stack 3 boxes** and use a 4th as a step to climb. A Gibbet (use the jugs), a bottle skeleton, a **bridge with holes** (falling = stuck until you die out of bounds). **Blunderbusses** only here. | 7,500 |
| 7 | **Sunken Ship** | **Underwater** through a tunnel (swashbucklers; swimming fish as weapons), **mines** that explode when you come close (throw things to set them off). Out of the water: a jugger on a platform. **Find 2 missing puzzle pieces** (plus 1 on a platform) to open the gate: one on the sunken ship past mines, one held by a skeleton near a ladder. | 11,500 |
| 8 | **Ghost Beard's Gauntlet** | Clear the ship (bleachers on the right give cover from cannoneers and captains), take pistols, board it: **Ghost Beard** appears. **Only cannons damage him**: hit him **3 times**, first with the **rear cannon**, then the **front**, then either. Waves of swashbucklers come over the bridges to the ship. He fires cannon shots. The rear hole of the ship is a hiding spot. The reward chests appear on the pile of gold, and he drops a **hook** you can pick up. | 34,500 |

  Deathless total about 155,500 (some players get 160,000).
- **Enemies** (WIKI point values): Swashbuckler 500, Bottle Thrower 500, Jug Thrower 500, **Pirate Captain**
  (flintlock, very accurate, a few seconds to aim, unlimited ammo, growls on spawn) 500, **Gibbet** 1,000 (explosions
  only), **Cannoneer** 1,000 (half HP, never leaves its cannon, explosive cannonballs). Ghost Beard 10,000.
- **Deaths and scores:**
  - The WIKI says a death is **-1,000** combat points; the bundle manager says **2,000 / 2,000** (the bundle is final
    data, but it may not be the hidden combat penalty: GUESS which one applies to the hidden score).
  - If all players die the combat score resets to zero, so a team wipe in act 3 or later makes S rank impossible.
  - About 9 deaths still gets S (WIKI).
- **Gold** (the shop currency, separate from the hidden rank score):
  - From chests and enemy drops; it disappears after a few seconds.
  - When everyone dies, **each player drops all their gold** (a lost-loot pile) and returns to the lobby. The pile
    stays until you log out.
  - **At most 1,600 gold per run** counts. Gold is kept when you finish.
- **Shop** ("Swash and Buckle" merch booth):
  - Scallywag Brown and Red (bracers 800, belt 800, vest 1,600, hat 3,200).
  - Pirate Captain Black and Red (the same prices).
  - Scallywag Black (5,000 / 5,000 / 10,000 / 20,000).
  - Ghost Beard's Treasures: film 250, explosive high-five potion 500.
  - Rank rewards: C belts/wrist pieces, B belts/shirts, A shirts/hats, S the S-rank set (always a new item).
- **Weapons** (WIKI hits-to-kill; **bundle `durability`** measured, see 8.2):
  - Cutlass: kills in 2 hits, breaks after 5.
  - Stool: kills in 1, breaks after 1; the best blocker.
  - Broom: 2 hits, breaks after 4; long reach.
  - Paddle/Oar: one-shots for 2 hits, then two-shots for 2 more.
  - Squeaky fish: 3 hits (WIKI says it breaks after 8, the bundle says 2).
  - Frying pan: one-shots, breaks after 5.
  - Torch: almost no damage; it lights the cannons.
  - Bottles: 2 hits. Jugs: explode, friendly damage. Plates: 2 hits, can friendly-fire. Mugs: 2 hits.
    Coconuts: 1 damage. Coconut bombs: about 4 s fuse, throw them back.
  - **Flintlock: 2 shots** (cock it by flipping or with the free hand's trigger over the hammer; a headshot kills).
  - Blunderbuss: 3 shots (act 6 only).
  - Cannons: small turning radius, lit with a torch, the only boss damage.
- **Music:** `PirateQuestShanty2` / `_Piano`, `Quest_Pirate1_PirateBrawl01` (the Bar Fight), `PirateShoppingChannel`
  (shop), ambience beds (rr_quests 2.7).

### 8.2 Bundle content for the final version
- rr_quests 2.x is final. New measurements (B43 `durability` / `maximumTotalAmmunition`):
  - `(Quest_Pirate1_Cutlass)` `fbd` 5, `(Quest_Pirate1_BarStool)` `a9e` 1, `(Quest_Pirate1_FryingPan)` `3b5` 5,
    `(Quest_Pirate1_Oar)` `4` 2 + 4 (two stages), `(Quest_Pirate1_PushBroom)` `76a` 4,
    `(Quest_Pirate1_BreakableFish)` `7a` **2**.
  - `(Quest_Pirate1_Flintlock)` `328` total ammunition **2**.
  - These match the wiki except the fish.
- Blunderbuss: the same prefab as CBM's `(Quest_Dracula1_BlunderBuss)` `7d` (3 shots). Torch sounds
  `Quest_Pirate1_TorchPickup01` (dfbb9fe0 key `3`), `Quest_Pirate1_TorchSwing01` (bb1ca5f7 key `1`). Cannon VFX
  materials only (1df8e0ef `0ed`, `86`, `ef`). **There is no cannon prefab, no jail cell, no mines, no puzzle pieces
  and no Ghost Beard** in the plain bundles.

### 8.3 Changes vs existing research (unbuilt)
1. rr_quests 4.3's act plan (tavern brawl, beach, Port Plunder, Gibbet Jungle x2, skull caves, Sunken Ship, a
   two-ship boss) must follow 8.1:
   - Start in a **jail cell in a ship's galley** (no dressing room, no racks: weapons are found).
   - Act 2 is the **banquet with the Bar Fight**.
   - Act 3 is a **ship at sea with cannons and a cannon target that lowers a bridge**.
   - Act 4 Port Plunder.
   - Act 5 Gibbet Jungle, one act in two parts.
   - Act 6 the torch-lit caves with the **box-stacking** climb and the holed bridge.
   - Act 7 the **underwater** Sunken Ship with mines and **3 puzzle pieces**.
   - Act 8 the boss.
   - The walkthrough has no separate "beach" act. The wiki says flintlocks are common "after the beach", i.e. from
     Port Plunder, where the team lands (GUESS: a short beach landing between acts 3 and 4).
2. Ghost Beard takes **3 cannon hits** (rear cannon, then front, then either). rr_quests 4.3's "HP 6 shells, phase
   every 2 hits" is wrong. Swashbuckler waves come over the bridges during the fight.
3. Checkpoints at every act (2018-12). Offer "Continue / Start Over" in the lobby after a team wipe.
4. **1-3 players** is the final cap (rr_quests allows 4; the user decides).
5. Durability numbers from 8.2 replace the rr_quests GUESSes. Blocking an X-parry stuns a swashbuckler, and it can
   only be hit while stunned or facing away.
6. Scoring: a hidden combat score (per-enemy 500/1,000, the boss 10,000, per-act totals above) for the rank, and
   visible gold (max 1,600 per run) for the shop. **One-hit downs** (5.3 item 1).

## 9. The Rise of Jumbotron (SciFi1, ^TheRiseofJumbotron)

### 9.1 Final version (sources: WIKI The_Rise_of_Jumbotron / Quest_Rating, NEWS)
- **History:**

| Date | Change |
|---|---|
| 2017-04-20 | Released; 1-4 players. |
| 2017 | Many fixes. |
| 2018-12 | Back holsters. |
| 2019-01 | Grenades have no friendly fire (but can hit yourself). |
| 2019-06-27 | Oculus Quest port. |
| 2019-10-10 | Grenades cannot friendly-fire in Jumbotron. |
| 2020-05-13 | Its bots became Maker AI; a first-level wall collision fix; JT grenade radius visual. |
| 2022-02-16 | The old grenade was renamed "Jumbotron Grenade". |
| 2023-05-04 | The audio-breaking bug with Assassin and Mouse Bots fixed. |
| **2025-01-15** | "Fixed an issue with **Auto Fire** in The Rise of Jumbotron" (a screen/accessibility auto-fire option, GUESS). |

  No rework. No checkpoints: progress and credits are lost on a team failure.
- **Lobby and dressing room:**
  - The team starts at a "Start" button (laser pointer).
  - The dressing room has a mirror on the left wall, decor, credit-filled monitors, and **4 laser pistols + 4 laser
    SMGs** on the back wall.
  - Everyone gathers on the platform and teleports through the back-wall door.
  - Friendly fire is on. The revive is a high five.
- **Acts** (the same 9 as rr_quests 1.5 / 4.4; wiki details that are new):

| Act | New final facts |
|---|---|
| 1 Armory | Entering the open port spawns 3 Bat Bots, one between each pillar. Then 3 at random spots, 3 more, 2 high on the left, 4 at the exit. The clear tune means go. |
| 2 Cargo Bay | 5 tread bots at the ramp; 5 more behind; 2 tread + 2 bat; **5 mouse bombs**; the right-hand side room with the **laser shotgun** (entering it spawns 3 bat bots, then 3 more + mice at the top of the first ramp); 3 consecutive waves at the end. |
| 3 Engineering | **Lasers kill on contact.** Tread bots in waves down and up the ramps, bat bots behind; the last room has a bit of everything. |
| 4 Bridge | Assassin bots (they aim a pink laser, it goes off, then they fire where it was; the battery pack on the back is the weak point); tread, bat and mice waves (the mice only if you enter their rooms). |
| 5 Flight Deck | Almost no floor, a stationary space ship in the centre. **The big green button on the console plays a projector scene** (the ship flying to the Jumbodome) and starts the attack. At the end a voice: "**Now arriving, all units please depart the escape vessel**". |
| 6 Space | **Every sound, including voice chat, is muffled** (underwater-like). A horde of tread bots next to an **explosive barrel**; lasers; spawns behind you; an **elite tread bot** (5 fast orbs x3) near the end; many explosive barrels. |
| 7 Hangar | 2 large rooms: elite bat bots (bursts of 5) + bat bots; then elites of both kinds, their normal versions and assassin bots; spawns behind you. |
| 8 Supply | Extra guns at the start; a side room with a **goblin ("Kevin") and a pot from Golden Trophy** (the Easter egg); explosive barrels; the final room is "by far the hardest" (elites, regulars, assassins). |
| 9 Jumbotron | 4 panels; an invincible turret below targets one player (preferring one out of cover) with streams of fast lasers aimed ahead of you. **Jumbotron is the only enemy that leads shots.** After each panel he yells "**Reinforcements!**" (tread and bat bots, normal and elite). The 4th panel destroys the turret and kills all enemies. A metal-and-glass cover wall right of the entrance, with no spawns there. Enemies spawn near his left. About 14 rail-gun shots per screen (a tip). |

- **Weapons** (WIKI = bundle, measured below):
  - Laser Pistol: **5** shots; reload by pulling the top black grip or flipping it; **stuns** normal and elite bots.
  - Laser Burst (SMG): **22**; bottom-grip reload; two-handed grip.
  - Laser Shotgun: **3**; pushes back and briefly stuns at close range.
  - Rail Gun: **3**; red side grips; pushes back and **stuns at any range**.
  - Only pistols and rail guns stun.
- **Enemies:**
  - Tread Bot: slow projectiles.
  - Bat Bot: bursts of 3 fast lasers at your head.
  - Mouse Bomb: crawls up in packs; the splash is about 3x its size.
  - Elite Tread (purple): sprays of 5 fast projectiles x3.
  - Elite Bat (purple): bursts of 5.
  - Assassin Bot (purple): the charge-beam pause-fire; the battery back is the weak spot.
  - Bots target the nearest player but switch to the last attacker; only Jumbotron leads.
  - Kill cost: a basic red bot = 1 shotgun hit, an elite flyer 2, an elite ground bot 3 (rr_quests 1.5).
- **Credits:**
  - Must be picked up. The destroyer gets them (split when several players did damage).
  - Sources: bots, monitors (50 each), explosive barrels, computer systems (small and large).
  - **Every hit from a bot and every friendly-fire hit costs 1,200** (= a down). The score cannot go below 0.
  - **Rating: S 150k+, A 125-150k, B 100-125k, C 0-100k** (the same scale as Golden Trophy; WIKI Quest_Rating).

### 9.2 Bundle content for the final version
- rr_quests 2.x is final. Measured magazines (B43, `ammunitionMagazineSize`, all `hasInfiniteReserveAmmunition` 1):
  - `(Quest_SciFi_Pistol)` `447` **5**
  - `(Quest_SciFi_AutomaticGun)` `0bd` **22**
  - `(Quest_SciFi_Shotgun)` `40` **3**
  - `(Quest_SciFi_RailGun)` `2a6` **3**
- Credits: `[Quest_SciFi_CreditParticle]` value 50; `(SciFatPot)` `564` 50.
- High-five stinger `Consumable_HighFive_JumboTron01` (85515de1 #8840540124564871427).
- Not in the bundles:
  - Jumbotron, the panels and the turret.
  - The projector scene and its VO ("Now arriving...").
  - The "Reinforcements!" VO: the audio index has no match for reinforce, arriv or jumbotron VO.
  - Level geometry.

### 9.3 Changes vs existing research (unbuilt)
1. rr_quests 4.4 is close to the final quest. Add:
   - The Flight Deck **console button + projector cutscene** that starts the attack, and the arrival line when it is
     cleared (text/TTS-free: show it as a toast; no VO exists).
   - The Space act's **muffled audio**: GUESS: low-pass our own SFX and music. Client voice cannot be muffled from
     world code, so show flavour text only.
   - The Supply **Kevin easter-egg room** (a goblin + a pot from Golden Trophy's kit).
   - Explosive barrels in Space, Hangar and Supply.
2. Lasers in Engineering **kill on contact** (= a one-hit down). rr_quests 4.4's "25 damage per 0.5 s" is wrong.
3. **One-hit downs**: "every time a player was shot by a bot ... lose 1200 credits". Drop per-hit damage.
4. Weapons: magazines 5/22/3/3 with infinite reserve (measured). Pistol and rail gun hits **stun** bots; the rail gun
   stuns at any range, the shotgun only close.
5. The dressing room has 4 pistols + 4 SMGs (rr_quests 4.4 already has this).
6. Absolute ranks S/A/B = 150k/125k/100k credits.

## 10. Rec Rally (^RecRally)

### 10.1 Final version (sources: NEWS, WIKI Rec_Rally)
- **History:**

| Date | Change |
|---|---|
| 2021-09-29 | Released: a six-player off-road race on one course in Pioneer Peak; the Rally Buggy went into the Maker Pen palette. |
| 2021-10-06 | Ghost engine sounds when a player leaves during the countdown fixed. **You can't grab other players' vehicles during races**, can't grab vehicles while seated, **players are locked in their seats during the starting countdown**. |
| 2021-10-13 | Wheels don't go inside other vehicles or objects; buggies behave at the race start; a melee swing inside a buggy no longer sends it crazy. |
| 2021-10-26 | Potions and rum jugs inside a buggy fixed; invisible boost pickups fixed. |
| 2021-11-19 | Deleting a buggy you sit in fixed. |
| 2021-12-08 / 12-16 | Trusty Pickup Truck, Classic Coupe and Wolf Ride added; seats and vehicles out of beta. |
| 2022-02-02 | **Spectators who joined an in-progress race can't get in or reset other players' vehicles** ("No more hitchhiking"). |
| 2022-02-09 | Buggy mass is configurable (UGC). |
| 2023-03-21 | The CV2 radio note says the radio distance thresholds are "consistent to those in ^RecRoyale & ^RecRally". Rec Rally has **team radio** beyond proximity range (GUESS: racers hear each other anywhere on the course). |
| 2025-06-04 | Vehicles in Rooms 2.0 (UGC only). |
| 2025-09-23 | "Fixed some buggy texture & collision issues so you can look cool in ^RecRally again". |
| **2025-10-15** | "**You can now play Rec Rally with 4 players instead of 6**" (thanks to @LyricalRacer2376). |

- **Final rules** (WIKI, unchanged except the player count):
  - One course, **3 laps** of about 2-3 min; a lap counts at the start/finish line; the race ends automatically after
    a time limit.
  - Boost: 3 nitro cans = 1 boost; the dashboard boost button flashes when it is ready.
  - After the race, 1st-3rd stand on a **podium**; the others spawn in the spawn area.
  - Rewards: a random **Settler Set** piece after a race; red and blue racing sets at the **merch booth**.
- **Player count**: the 2025-10-15 wording most likely means a race can now **start with 4** (before, a full lobby of
  6 was needed). The grid stays 6 (GUESS; no source says the maximum changed).

### 10.2 Bundle content for the final version
- rr_party.md 3.2 is final data. The buggy `20d`, the nitro pickup `edd`, `(LocalPlayerRaceHUD)`, music, ambience and
  the southwestern kit are unchanged. The later vehicles `4ec` / `8f` / `57d` are there.
- No Rec Rally game config (lap count, time limit, player minimum) is in the plain bundles. Searched objects.tsv for
  rally: only the buggy, the pickup, its mesh and `RecRallyIconAtlas(1024)`.

### 10.3 Changes vs existing research (unbuilt)
1. The race **starts with 4 or more players** (up to 6). rr_party.md 3.1 / 3.3 assume 6.
2. Add the anti-grief rules that shipped:
   - Nobody grabs or resets another racer's vehicle during a race.
   - Late joiners are spectators who cannot get into or reset vehicles.
   - Seats lock during the countdown.
   - Nobody grabs a vehicle while seated.
3. Race-wide voice ("team radio") is a Rec Rally feature (GUESS). For us, voice is the client's, so skip it.
4. Nothing else changed. The course walk-through, the buggy numbers and the controls in rr_party.md 3 are the final
   version.

## 11. Stunt Runner (^StuntRunner)

### 11.1 Final version (sources: NEWS, WIKI Stunt_Runner)
- **History:**

| Date | Change |
|---|---|
| 2019-09-05 | Released (6 courses, the new locomotion). |
| 2019-09-06 / 10 / 24 | Patches: runners can progress when a player leaves; a course 2 floor hole; a course 5 typo; lobby corners; **no bumping in the lobby**; **the course 4 checkpoint can't be triggered from outside the tower**; Stunt Runner sounds on the volume mixers; a **screen "button slapping" animation** at the end button. |
| 2019-10-10 | "Stunt Builder": creator tools and **^StuntTemplate** reachable from the lobby; community maps planned for the lobby. |
| 2019-10-29 | **A course 1 hole fixed and the leaderboards wiped**. |
| 2019-11-07 | "Fixed a shortcut in Stunt Runner so no one gets an early start". |
| 2019-11-21 | A **"new personal best" notification**. |
| 2019-12-19 | Air control back to normal. |
| 2021-06-02 | **The community door in ^StuntRunner shows all rooms tagged #StuntRunner**. |
| 2021-06-25 | A seat speed exploit removed. |
| 2022-01-20 | The WalkVR slowdown fixed. |
| 2025-02-19 | Stunt Runner boards no longer lock controls with "Respawn on Game Start" (UGC). |

  No new or changed official courses after 2019.
- **Final rules** (WIKI; as rr_party.md 2.1):
  - **Up to 4 walking players.** It is walk-only: VR teleport players cannot play (WIKI: "for up to 4 walking
    players").
  - 6 courses (Anticipation, Daring, Patience, Caution, Trust, Perseverance); 3 min per course.
  - Ranks from the total time: S+ < 4:45.
  - Rewards: the Feathered hair style (S+), Stunt Shades (S/A), Runner Helmets (B/C), the Stunt torso.
  - The lobby has leaderboards, the ^StuntTemplate door and the community door.
- Speedrun reference (speedrun.com Category Extensions): full runs of 4:13-4:34. A 4:06 S++ run on YouTube. There is
  **no S++ rank** in the manager data (S+ .. C only; rr_party.md 2.4 notifications), so "S++" is community slang.

### 11.2 Bundle content for the final version
rr_party.md 2.2-2.4 is final data (manager `528`, config, boost manager, kit, audio). Nothing to add.

### 11.3 Changes vs existing research (unbuilt)
1. Stunt Runner is **walk-only**. Our VR players need smooth locomotion (the client's walk mode); teleport must be
   off or ignored in the course (GUESS how the client exposes this; see somnium_player.md).
2. **No bumping in the lobby** (players pass through each other there).
3. The checkpoint trigger volumes must not fire from outside the structure (the 2019 course 4 fix): use enclosed
   triggers.
4. Show "NEW PERSONAL BEST!" (the `NewBestOoo01` clip exists, rr_party.md 2.4) when a local best is beaten.
5. Optional: a lobby door to community-style courses. Not needed.
6. Nothing else changed. The GUESS rank thresholds below S+ (rr_party.md 2.1) remain GUESSes: no source found.

## 12. Cross-cutting notes, open questions, sources

### 12.1 Things that apply to several activities
- **Quest framework (Games/Quests/Shared)**, final-version rules to add. All sourced above; the numbers marked GUESS
  are ours.
  1. One-hit downs (5.3 item 1). A melee block with a shield or held weapon cancels a hit. Swashbucklers are stunned
     by a block.
  2. Absolute rank thresholds per quest:

| Quest | S+ | S | A | B | C |
|---|---|---|---|---|---|
| Golden Trophy (gold) | - | 150k | 125k | 100k | below 100k |
| Jumbotron (credits) | - | 150k | 125k | 100k | below 100k |
| Crimson Cauldron (gold) | - | 300k | 275k | 250k | below 250k |
| CBM (hidden combat score, base 4,400) | 4,160 | 3,160 | GUESS | GUESS | - |
| Isle (hidden; per-enemy points in 8.1; deathless ~155,500) | - | about 9 deaths still S | ? | ? | ? |

  3. Per-enemy points: Golden Trophy (5.1), Isle (8.1) and CBM silver (7.1) are published; Crimson Cauldron and
     Jumbotron per-bot points are not.
  4. Checkpoints only in Isle (every act) and CBM (the Grand Hall). Golden Trophy, Crimson Cauldron and Jumbotron have
     none.
  5. Back holsters in all five (2018-12).
  6. Player caps: 1-4 for Golden Trophy, Crimson Cauldron and Jumbotron; **1-3** for Isle and CBM.
  7. The shared "Double Shot" bow trick came back in 2024-11 (fixed so it cannot become infinite shots). GUESS: a quick
     second nock-and-release fires two arrows. It is the longbow's, so it touches Golden Trophy's bow too.
- **Act-name lists** in rr_quests.md 1.3-1.6 / 4.2-4.5 are superseded by 6.1, 7.1 and 8.1. Golden Trophy and
  Jumbotron keep their act counts; Golden Trophy's act 6 changes.
- **The "no VO" claim** (rr_quests 2.7) needs one exception: Coach's `Gameon` (key `e`, 8b1b2e31) at the quest start.
  The Jumbotron lines ("Reinforcements!", "Now arriving...") and Dracula's speech are **not** in the bundles.

### 12.2 Open questions (not answered by any source found)
1. The Golden Trophy mods' actual numbers: Turbo's speed factor, the Coconut Chaos drop rate, the Laser Weapons
   mapping.
2. Whether a downed but un-revived player in Golden Trophy, Crimson Cauldron or Jumbotron comes back at the next act,
   or stays out until a team wipe.
3. Isle of Lost Skulls: the death penalty in the hidden score is 1,000 in the wiki and 2,000 in the bundle manager.
   Isle's rank thresholds.
4. Crimson Cauldron and Jumbotron per-enemy points (only the rank scales are known).
5. The Crimson Cauldron bog monster: its size, attacks and HP. What exactly damages the mini Imp Witches after the
   2017 change.
6. How Dracula's mirrors are aimed and fired: a player-rotated stand or a grabbable mirror; beam or bolt; how long it
   stays up.
7. Rec Rally: whether 4 is the new minimum with 6 as the maximum (our reading) or 4 as the new maximum.
8. Stunt Runner rank thresholds below S+.
9. Whether the Ink Space Charades kept the "hide the big scoreboard while drawing" rule and enforced 2 players per
   team in Lightning.
10. Layouts: every act's geometry is still a reconstruction from walkthrough text. The levels are encrypted, and no
    map images were downloaded (binary).

### 12.3 Sources
- Steam news: `SP\research\dorm\wiki\steamnews.json`, 564 items 2016-06-30 .. 2026-04-06, searched per activity with
  `SP1\rgs\sn.pl` (results saved as `SP1\rgs\{charades,mitm,mlm,punch,gt,cc,cbm,isle,jumbo,rally,sr,quests}.txt`).
  Key items:
  - "Team Charades" 2017-04-07, "Beta" 2020-08-10, "Ink Space" 2020-09-09.
  - "Make it to Midnight" 2023-08-29, "The Big One" 2024-11-23, "Merry Midway is Here!" 2025-10-28.
  - "Isle of Lost Skulls - Reboot!" 2018-12-05.
  - "Welcome, Roomie!" 2025-06-17, "It's Dangerous to Go Alone, Take This!" 2025-07-24.
  - "New Shipnotes Format" 2025-09-23, "Paintball Lobby" 2025-10-15.
  - "Winter Wonderland 25" 2025-12-02, "So Long, 2025" 2025-12-08, "School's Out" 2026-03-30.
- Fandom wiki wikitext (MediaWiki API, 2026-09-24), `SP1\rgs\wiki\`:
  - Quest_for_the_Golden_Trophy, The_Curse_of_the_Crimson_Cauldron, Isle_of_Lost_Skulls, The_Rise_of_Jumbotron,
    Crescendo_of_the_Blood_Moon, Quests, Quest_Rating.
  - Make_It_To_Midnight, My_Little_Monsters, Rec_Rally, Stunt_Runner, Punch_up, 3D_Charades, Bonky_s_Inferno,
    Roomie, Timeline, News_Archive_2025.
  - The older party pages are in `SP\research\party_wiki\`.
- Web: the Steam forum thread "Punch Up." (2017, a developer reply); search results for the Merry Midway,
  Golden Trophy mods and Rec Rally 4-player ship notes (recroom.com ship-notes pages, which are down since
  2026-09-18 per the wiki Timeline).
- Bundles (rrtool index + `rrtool tree/dump --fields`, final build 23191908):
  - B43 CharadesManager; the Splat Hammer.
  - Five CBM miniboss coin piles; CBM weapon ammo.
  - Pirate weapon durability; Jumbotron magazines.
  - The Roomie bundle 4056f2fc; `Gameon` 8b1b2e31; `Water_Swamp_Mat` 18c123d6; the Merry Midway UI; the RRO Mod
    Ticket.
