# Rec Room Quests in Dreamport: research (rr_quests.md)

Status: COMPLETE (2026-09-24). Measured facts cite bundle/pathId/key; design and reconstructions are marked PROPOSAL or GUESS.

Conventions in this document
- `B43` = bundle `43ea1176ca25c0333547813ec3298808` (the big "creation/quest" bundle: quest managers, Maker enemy
  spawners, quest weapons, loot, rule circuits). `B1DF` = `1df8e0efac4f496ebc58d7e6f86480c3` (shared tools/meshes).
  `BTR` = `d10447fc7fb3dd6312c640a4b3dd438b` (player-transformation rigs, the only skinned enemy rigs).
  Bundle file = `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\<name>.bundle`.
- `key` = Addressables container key (what `bundle.LoadAsset<GameObject>(key)` takes; see rr_runtime_facts.md).
  Entries without a key are implicit: they become resident when a keyed prefab referencing them is loaded and are
  then found with `Resources.FindObjectsOfTypeAll<T>()` by name.
- Tool: `RR` = `SP\rrtool\bin\out\rrtool.exe` (SP = this session's scratchpad). Raw dumps behind this doc:
  `SP\research\q\` (ai\*.txt enemy spawners, wp\*.txt weapons, loot\*.txt, mgr_*.txt quest managers,
  wtable.txt weapon table, music.tsv, audio_quest.tsv, spawner_meshes.txt).
- GUESS marks anything not read from data or a source.

## 0. Summary (read this first)
1. **All five quests' rules, weapons, loot, music and enemy tuning are in the plain bundles; their levels, enemy
   rigs/animations/voices and bosses are not.** Almost everything sits in B43 (`43ea1176ca25c0333547813ec3298808`).
   Load a quest by its rule-circuit key (`fe6` Golden Trophy, `935` Crimson Cauldron, `e5d71` Isle of Lost Skulls,
   `f8` Rise of Jumbotron, `4e` Crescendo of the Blood Moon): that makes its `QuestManager` (score currency, death
   penalty, win/lose stingers, point VFX, enemy health-bar prefab, LootManager banking rules) resident (2.1).
2. **Enemies:** 26 Maker spawners `(QuestAIController)*` (keys in 2.2.1) give each enemy type's measured `maxHealth`,
   `movementSpeed`, dash, projectile speed and charge values, plus a **static posed mesh with the real material**
   (goblins, imps, barrel knights, cannon/pike goblins, laser bots, drones, RecRoomba, assassin bot, pirate
   skeletons and gibbet, CBM skeletons, bats, wolf). Three enemies also exist as **skinned rigs with idle + walk
   clips** (goblin, pirate swashbuckler, laser bot: player-transformation prefabs in bundle BTR, 2.2.2). Attacks,
   hurt and death are ours (procedural, 3.5).
3. **Weapons:** 40+ quest weapons are fully configured in B43 (damage ranges, fire rates, projectile speeds, reload,
   breakability, audio, VFX, meshes; table 2.3). Rec Room's weapon scripts are missing at runtime, so we implement
   the behaviours from these numbers (3.11).
4. **Loot/score:** breakables (FatPot 50, FatPot_Large 100, SciFatPot 50, CBM candles 2-3 silver), coin/credit/silver
   pickups, Pirate lost-loot piles, CBM banking with rank bonuses - all measured (2.4, 2.1).
5. **Revive:** measured Rec Room revive config (2.5 m range, reviver must stay within 1 m, high-five/handshake) +
   `Quest_Revive` audio; our own downed markers (the Somnium avatar is never modified) (2.5, 3.7).
6. **Music/VFX:** every quest's explore/combat/boss tracks and stingers with bundle and length (2.7), all quest VFX
   (2.8). No enemy or narrator voice lines exist.
7. **Bosses** (Goblin King, Crimson Witch, Ghost Beard, Jumbotron, Dracula) must be built from scaled enemy meshes,
   the `TricksterGhost`/`TricksterYeti` skinned rigs (bundle 6d3223da) and procedural parts, with mechanics from the
   wiki (2.6, 4.x).
8. **Levels** are rebuilt from Rec Room kit props (stone walls/pillars, quest cardboard trees, campfire, rocks, logs,
   fences, barrels, sci-fi door with its animator, lockers, panels, I-beams, pipes), Rec Room's own surface materials
   (stone, rock, wood, metal, keyed water/lava materials) on procedural meshes, laid out act by act from the wiki's
   descriptions (4.0-4.5).
9. **Framework** (3): one director on the elected instance authority; enemies in 2 actor blocks (30 actors), a
   bitset block for breakables/pickups; enemy attacks broadcast as events and **judged by each victim's client**;
   player hits on enemies reported to the authority; six new event types (14-19) extend the networking catalogue.

Load recipe (for implementers):
```
bundles: open B43 + RrReach.Bundles(B43, <prefab pathId>) for each keyed prefab used; open the music bundles (2.7)
rules  : LoadAsset<GameObject>("fe6")  -> Resources.FindObjectsOfTypeAll<GameObject>() "[Quest_Goblin1_Manager]"
         -> parse QuestManager/LootManager fields with our serialized-data reader (values also tabled in 2.1)
enemy  : LoadAsset<GameObject>("c13") -> child "Preview/Goblin_Ground_Spawner_fbx" MeshFilter+MeshRenderer = visual;
         QuestAiConfig_EnemyCommon etc. = config (values tabled in 2.2.1)
rig    : BTR.LoadAsset<GameObject>("Assets/Activities/Quest/Shared/Content/Enemies/Transformations/Goblin_Ground_PlayerTransformation.prefab")
weapon : LoadAsset<GameObject>("14") (QuestSword) -> strip missing scripts, keep meshes/colliders, add our behaviour
```

## 1. Web research: the five quests (rules and structure)
Sources (fandom pages could not be fetched: HTTP 402; content below comes from search-engine extracts of the Rec Room
fandom wiki and from other pages; summarised in our words): rec-room.fandom.com pages *Quests*, *Quest Rating*,
*Quest for the Golden Trophy*, *The Curse of the Crimson Cauldron*, *Isle of Lost Skulls*, *Isle of Lost Skulls
Level Skips*, *The Rise of Jumbotron*, *Crescendo of the Blood Moon*; Steam guides 2008860357 ("jumbotron guide")
and 2797825064 ("How to actually get better at The Rise of Jumbotron"); roadtovr.com Crimson Cauldron and Isle of
Lost Skulls launch articles; rec-room-walkthrough.blogspot.com CBM solo tutorial (chapter list); speedrun.com
(Crimson Cauldron 4-player run 08:18). Where the web is silent, bundle data or GUESS fills the gap.

### 1.1 Shared quest rules (all five)
- **Co-op PvE, 1-4 players** (Isle of Lost Skulls and Blood Moon: 1-3 per the wiki; the Road to VR article says up
  to 4 for Pirate). A quest is a chain of **acts** (rooms); clearing all waves in an act opens the exit door to the
  next act; the final act is a boss.
- **Players:** 100 HP (Rec Room standard; enemies deal damage per hit, GUESS 10-35). At 0 HP a player is **downed**
  and cannot use weapons; a teammate revives with a **held handshake / high five** within 2.5 m (config in 2.5).
  Brief invincibility for both after a revive. An un-revived player is out until the team reaches the next
  act/checkpoint (GUESS: the Rec Room rule is "revived at the start of the next act"). **All players down = team
  failure** ("Our Heroes Have Failed"), restart from the beginning (Golden Trophy, Crimson Cauldron, Jumbotron have
  no checkpoints) or from the last checkpoint (Pirate since Dec 2018; Blood Moon via the Grand Hall hub).
- **Scoring:** enemies killed and breakables smashed give points (Gold / Credits / Silver); a death costs
  `deathPointsPenalty` (2000 / 1200 / 200) and friendly fire the same. Team score -> **Quest Rating** letter
  (S, A, B, C) -> quality of each player's end chest. Beating the boss spawns **one treasure chest per player**.
- **Weapons** are found as racks in the start room ("dressing room"/armory) and lying around in acts; weapons can
  be holstered (hip / shoulder). Medieval melee blocks enemy melee; arrows stun basic goblins/imps.
- **Movement:** VR teleport limited to 4 m with 1 s delay during quests (manager role override); walk 3.5 m/s.
- **Duration:** full clear 25-55 min for a casual team (CBM solo tutorial is 53 min; GUESS 25-40 min for the
  others); speedruns: Golden Trophy about 4 min, Crimson Cauldron 8 min 18 s (4 players).

### 1.2 Quest for the Golden Trophy (Goblin1, campaign 0) - 9 acts
Story: the Goblin King stole Coach's Golden Trophy (in-game prop: `(Golden_Bucket)`, a golden bucket).
Start: dressing room with **swords, shields, bows** (4 each GUESS for 4 players).

| Act | Content (wiki extracts + GUESS where marked) |
|---|---|
| 1 | Outdoor forest/camp; **sword goblins** only (basic melee). |
| 2 | **Imps** introduced (small flyers with a cannon firing slow fire orbs; an arrow hit knocks them down stunned). Waves spawn behind the players; a ramp with goblins/imps spawning at the top. A side room holds 4 large pots and a **crossbow** (slower than the bow, stronger, one-handed). |
| 3 | A big **stone boulder** that players roll down ramps to crush groups of enemies. |
| 4 | Large open room, floor mostly **lava** with bridges; **barrel knights** introduced (big goblin with a halberd: moves slowly, pauses to aim, then charges fast to the aimed point; killed by hitting the **target on its back**). Imps spawn over the lava (die in one hit by falling in); goblins from every side except the exit. |
| 5 | Split path; **elite** (red) goblins and imps introduced (elite goblins are not stunned by arrows). |
| 6 | GUESS: mixed waves of all types in a castle courtyard/towers. |
| 7 | Easier level with a small **maze** containing a barrel knight and enemies spawning ahead and behind; ends in a larger room with elite imps and goblins (room to retreat). |
| 8 | Large room half **lava**, half wood floor. Wave 1: 1 elite goblin + 4 basic goblins; wave 2: 3 elite + 2 basic; further waves with 2 elite + 2 regular flyers placed over the lava or in the back corner. |
| 9 (finale) | **Goblin King**: a giant barrel knight with an **axe** and a huge **imp cannon** firing **3 large fire orbs** at a player he sees. He turns toward whoever hits him. Damage only via the **target on his back** (arrows/crossbow best; thrown swords do more than swung swords). A raised platform with bows/crossbows; enemies spawn all over the room. Shields can stun/distract him while teammates shoot the back target. |

### 1.3 The Curse of the Crimson Cauldron (Goblin2, campaign 2) - 9 acts
Third quest; "longest and most challenging" at launch: more unpredictable enemies, **puzzle solving**, **environmental
hazards**; new **magic wand** and upgraded **crossbow**; **double-damage potions**. Mines setting (music "Exploring
the Mines", mines ambience, "Goblin Work Song") ending in a **bell tower**.
Enemies: the Goblin1 set + (bundle) cannon goblins, pike goblins, volley flyers, mortars; wiki: elite goblins charge
mindlessly and ignore arrows; imps fire single fireballs; **elite imps fire a spread of 5 fireballs**; **witches**
fire large energy balls that can be **reflected** with a sword, shield, potion or pot.
Act list: not recoverable from the web (GUESS layout in section 4: mine entrance, cart tracks, cavern lake, forge,
lava crossing, puzzle doors with levers, goblin village, tower stairs, bell tower).
Boss: **Crimson Witch** at the top of the bell tower. She launches purple orbs; players bat them back with a sword
(or shield/pot/potion, like Paddleball). Phase 1: no adds, return the orb **3** times; later phases need **4**
returns each and spawn progressively more enemies (GUESS: 3 phases).

### 1.4 Isle of Lost Skulls (Pirate1, campaign 3) - 8 acts
Fourth quest (March 2018); pickup room (VR + screen players). **Checkpoints** added Dec 2018 (continue from the last
checkpoint unless everyone leaves). Gold: every player carries gold; on death the carried gold drops as a named
**lost-loot pile** that anyone can pick up; gold is "escaped with" at the end; a merch booth in the lobby sells
themed items for gold.
Weapons are **improvised and break** after a few hits (cutlass, oar, broom, frying pan, bar stool, squeaky fish),
throwables (bottles: 2 hits kill most enemies; rum jugs explode; mugs, plates, coconut bombs), **flintlock pistols**
(dropped by Skeleton Pistoleers or found after the beach; cock before firing; **two shots** then spent), **torches**
(light cannon fuses), **hook hand**.
Enemies: **Swashbuckler Skeleton** (most common: block its attack to stun it, then hit it), **Skeleton Pistoleer**
(flintlock), **Bottle Skeleton**, **Jug Skeleton** (exploding jugs), **Gibbet** (skeleton in a hanging cage that
spawns as you walk on).
Acts (names from the wiki where known): tavern start (GUESS: "Act 1" tavern brawl with `CrowdAmbiance` +
`PirateBrawl01`), **beach**, **Port Plunder** (town/docks), **Gibbet Jungle** (two parts), caves (ghastly
ambience), **Sunken Ship** (ringed by rocks), ship deck, finale.
Boss: **Ghost Beard**; **cannons are the only thing that damages him**: cannons on ships, small turning arc,
explosive shells; light the fuse with a torch.

### 1.5 The Rise of Jumbotron (SciFi1, campaign 1) - 9 acts
Second quest; futuristic space ship; laser weapons; **no checkpoints** (progress and credits lost on team failure).
Credits drop from bots/crates and **must be picked up**. Start: dressing room with a mirror, credit monitors and 4
laser pistols (plus 4 burst rifles in the Armory per the Steam guide).

| Act | Name | Content |
|---|---|---|
| 1 | Armory | Large open room; 3 **Bat Bots** (flyers firing bursts of 3 fast lasers). |
| 2 | Cargo Bay | **Tread Bots** and **Mouse Bots** (mouse bombs crawl up in packs and explode); shotgun in a side room where mice spawn. |
| 3 | Engineering | **Laser** hazards (beams to avoid); shotgun in front of the spawn behind the lasers; rail gun on a ramp. |
| 4 | Bridge | **Assassin Bots**: high HP, aim a pink laser, pause, then fire where the laser was; weak spot = power pack on the back (shotgun / rail gun). |
| 5 | Flight Deck | Lethal fall damage (open edges); rail guns on a spaceship. |
| 6 | Space | Outside the ship; players cannot hear each other. |
| 7 | Hangar | Computers, barrels, ramp. |
| 8 | Supply | Callbacks to earlier content; cardboard boxes. |
| 9 | Finale | **Jumbotron** on a giant screen: 4 **panels** to destroy, an invincible **turret** under him targets one player with streams of fast lasers (duck to dodge). Each destroyed panel -> "Reinforcements!" -> tread bots and bat bots (normal + elite) spawn; after the 4th panel his turret dies and remaining enemies die. A metal/glass wall prop to the right of the entrance gives cover; railings/platform with weapons. |

Weapons: laser pistol (reload by grabbing the top handle or flipping the gun), laser burst / SMG (bottom handle
reload), rail gun (charge), laser shotgun (best; 11 across acts 2-8). Enemies: Bat Bot, Tread Bot, Mouse Bot,
Assassin Bot, Purple (elite) Bat Bot, Purple Tread Bot. Basic red bot = 1 shotgun hit, elite flyer 2, elite ground 3.

### 1.6 Crescendo of the Blood Moon (Dracula1, campaign 4) - hub + 8 levels + boss
Released Nov 15 2018; Castlevania homage (castle, whips, skeletons). **Silver** from breaking candles (and
candelabras/pumpkins) and killing enemies; silver is **banked** each time a level is completed and players return
to the **Grand Hall** hub (the hub is the checkpoint); rank bonus at the end; silver buys cosmetics in the
"Caravan of Curiosities" gift shop. All weapons except the **whip** have limited uses. Whip also hooks
**hookshot points** to teleport across gaps.
Level order (CBM solo tutorial chapter list, 53 min total): **Burning Village** (4 min), **Carriage Ride**
(2 min), **Castle Grounds** (5 min), **Gallery** (4 min), **Chamber of Knowledge** (8 min), **Garden of Sorrow**
(6 min), **Inverted Ballroom** (10 min; music `CBM_Ballroom`), **Cellar** (4 min), **Dracula** (8 min).
Enemies (bundle): pumpkin-head melee skeleton, maid bone-thrower skeleton, bellhop musket skeleton, bats (lunging,
ranged), wolves; minibosses drop big silver piles (bat, wolf, bone thrower, bone melee, musket).
Boss: **Dracula**; hit him with a **mirror shot** (reflect his attack with a mirror, GUESS) to damage him; the
balconies are the safe spot to clear adds afterwards. Music `CBM_CombatBoss`.

## 2. Bundle content map (measured)

### 2.0 The big picture (read first)
- **What the plain bundles contain for quests:** each quest's *rules object* (`QuestManager` + `LootManager` +
  `QuestGearManager` data), every **quest weapon** (fully configured: damage, rates, audio, VFX, meshes), loot/coins/
  breakables, music and SFX, VFX, the enemy **health-bar UI**, and Maker "Quest AI" spawners
  `(QuestAIController)*` with each enemy type's **config** (max health, move speed, dash/projectile/charge numbers)
  and a **static posed mesh of the real enemy with its real material**.
- **What is NOT in the plain bundles:** the official quest *levels* (encrypted), the enemies' runtime prefabs,
  their Animator controllers / attack+death clips, enemy voice/grunt SFX, the five bosses (Goblin King, Crimson
  Witch, Ghost Beard, Jumbotron, Dracula), and the quest-select UI. Verified: `objects.tsv` has 69
  AnimatorControllers and none is an enemy controller; no AudioClip name matches goblin/skeleton/robot/bat/wolf/
  witch/Dracula voice.
- **Consequence:** enemies are rebuilt as *our* NPCs: a posed static mesh (or one of three skinned rigs with idle +
  walk) + our own procedural animation (bob, lean, squash, spin, bone swings) + our AI driven by the measured
  config numbers. Bosses are built from kit meshes + procedural parts (design in sections 3-4).

### 2.1 Quest rule objects (QuestManager per quest)
Loading the quest's rule-circuit prefab by key makes its `QuestManager` prefab (no key of its own) and all clips it
references resident. All in B43. Fields read from `q\mgr_*.txt`, `q\goblin1_manager.txt`.

| Quest | Rule circuit prefab (key, pathId) | Manager GO (pathId) | campaign | score name | death / friendly-fire penalty | collectible | win / lose SFX (clip, B43 pathId, length s) |
|---|---|---|---|---|---|---|---|
| Quest for the Golden Trophy | `(QuestForTheGoldenTrophy_GameRuleWrapperCircuit)Variant` key `fe6`, -9060994791089726935 | `[Quest_Goblin1_Manager]` 3389189270401179563 | 0 | Gold | 2000 / 2000 | none (points shown as coin VFX) | win `Crimson Cauldron - Our Heroes Have Triumphed` (-4950059348427035262, 18.2; reused); lose `Golden Trophy - Our Heroes Have Failed` (-3927911219958854199, 15.0) |
| Curse of the Crimson Cauldron | `(CurseOfTheCrimsonCauldron_GameRuleWrapperCircuit)Variant` key `935`, -4135721262361396085 | `[Quest_Goblin2_Manager]` -409550925792103234 | 2 | Gold | 2000 / 2000 | none | same win; lose `Crimson Cauldron - Our Heroes Have Failed` (-6619955854217260243, 14.9) |
| Isle of Lost Skulls | `(IsleofLostSkulls_GameRuleWrapperCircuit)Variant` key `e5d71`, -4868050408680623212 | `[Quest_Pirate1_Manager]` 6402587617209858979 (+LootManager, +QuestGearManager) | 3 | "" (gold via LootManager, currency 100) | 2000 / 2000 | loot piles (LootManager) | `PirateQuestHeroesAreVictorious` (-8264335817754304648, 9.5) / `PirateQuest_Fail` (-1642873442655550638, 9.5); game-over screens 10 s |
| The Rise of Jumbotron | `(RiseOfJumbotron_GameRuleWrapperCircuit)Variant` key `f8`, -6314749316754083315 | `[Quest_SciFi1_Manager]` 23278338029046748 | 1 | Credits | 1200 / 1200 | `usesCollectiblesForPoints=1`: `[Quest_SciFi_CreditParticle]`, value **50** each, must be picked up | `JumboTron - Our Heroes Have Triumphed` (-2838361179791384808, 15.0) / `JumboTron - Our Heroes Have Failed` (1005665985233980239, 12.2) |
| Crescendo of the Blood Moon | `(CrescendoOfTheBloodMoon_GameRuleWrapperCircuit)Variant` key `4e`, 5791420566573996470 | `[Quest_Dracula1_Manager]` 8923214061001421911 (+LootManager currency 101, +QuestGearManager) | 4 | Silver | 200 / 200, trap multiplier 0.1 | `[Quest_Dracula1_Silver_Store_VFX]` value 10; `everyoneEarnsUndividedLoot=1` | `CBM_Victory` (3728390911084181894, 9.35) / `CBM_Failure` (-6072281223067451835, 9.35) |

Common manager fields: game-over screens 6 s (Pirate 10 s); victory text "Our Heroes Are Victorious!", lose text
"Our Heroes Have Failed"; `localPlayerOutSFX` = `Out` (B43 -4456959885725097216, 2.0 s); `smallPointsSpawnSFX` =
`Quest_Coins_Spawn01` (B43 1185241486661356506, 0.86 s); `largePointsSpawnSFX` = `Quest_BossCoins`
(B43 1407898267928393354, 7.05 s); `playerDamagedVFXPrefab` = `[VFX_Default_HitParticles]` (Pirate:
`[Quest_Pirate1_PlayerDamaged_VFX]`); small/large point VFX = `[VFX_GoldCoins]` / `[VFX_BossGoldCoins]`
(Goblin1/2), `[VFX_CreditChips]` (SciFi), `[Quest_Dracula1_Silver_VFX]` / `[Quest_Dracula1_Silver_Boss_VFX]`
(Dracula), none (Pirate). Objective ids (stats; informational): Goblin1 1010-1013, Dracula1 1050-1053 (games,
wins, player revives, enemy kills). In-game role override on all five: `teleportDelay` 1 s,
`maxTeleportDistance` 4 m (VR teleport limit during a quest), walk speed 3.5 m/s, no sprint/jump overrides.
Dracula1 subtitle: "(Silver banked at checkpoints will be saved)".
`enemyCombatUIPrefab` (floating enemy health bar): SciFi `[Quest_SciFi_EnemyCombatUI_3D]` (B43
-1421199533325401314), Dracula `[Quest_Dracula1_EnemyCombatUI_3D]` (B43 -1308464109203661391, scale 0.65,
FaceCamera + MaintainWorldSpaceOffset); Goblin1/2 and Pirate have none (no bars).

**LootManager** (bank / lose-loot rules):

| field | Pirate1 (gold, currency 100) | Dracula1 (silver, currency 101) |
|---|---|---|
| lootBankedText | "YOU ESCAPED WITH {0} GOLD" | "YOU BANKED {0} SILVER" |
| lootLossType | 0 | 1 |
| rankBonuses (rank:bonus) | none | 0:10000, 1:6000, 2:3000, 3:2000, 4:15000 |
| dropLostLootDelay / bankLootDelay | 6.5 / 6.5 s | 3 / 3 s |
| spawnedLootPileLifetime | 8 s | 10 s |
| small / large pile value | 1 / 200 | 0 / 0 |
| maxLostLootPileValue | 250 | 250 |
| smallChestLootValue | 3 | 0 |
| enemyDeathLootDropRate / enemyLootValue | 0.15 / 3 | 0 / 0 |
| breakableContainerLootDropRate / value | 0.15 / 300 | 1 / 50 |
| enemy loot spawn max linear / angular speed, jitter | 5 m/s, 15, 0.1 | 3 m/s, 15, 0.1 |
| audio | pickup `Quest_Pirate1_Collectable_GoldPickup01..04`; drop `..._GoldBarfed01`; recovered `..._GoldRecovered01`; reminder/nearby `..._GoldNearbyNotify01`; "return to recover" `..._ReturntoRecoverNotify01`; enemy drop `..._EnemyDropGold01` | same pickups; bank `Arena_CashRegister01` (bundle 56515818...) |

Meaning (fields + wiki): in Pirate1 a player who goes down **drops the gold they carry as a lost-loot pile**
(`(Quest_Pirate1_LostLoot)`, with a name tag, max 250 per pile) that anyone can recover; gold counts when the team
escapes. In Dracula1 silver is **banked** each time the team returns to the Grand Hall hub (checkpoint) and a rank
bonus is added at the end.

### 2.2 Enemies
#### 2.2.1 The 26 Maker "Quest AI" spawners (all B43, all container entries)
Each `(QuestAIController)<Name>` prefab is Rec Room's Maker enemy spawner: `QuestAIController` (`type` enum = enemy
kind), `QuestAiConfig_EnemyCommon` (`maxHealth`, `movementSpeed`, `healthBarSetting` 0, `spawnState` 3,
`preventAutoAggro` 0), optional `QuestAiConfig_Dash` (`dashSpeed`, `minDashDistance`, `maxDashDistance`),
`QuestAiConfig_Projectile` (`projectileSpeed`), `QuestAiConfig_Charge` (`chargeDistance`, `chargeSpeed`). Child
`Preview/<X>_Spawner_fbx` holds a **static posed mesh of the enemy with its real material** plus a BoxCollider
(size below). `spawnOffset` is (0,0,0) except flyers (y 0.4 / 0.5). These are the Maker defaults, i.e. Rec Room's
own tuning per enemy (GUESS: official quests may scale health with party size; not visible here).
Key column: Addressables container key in B43.

| `(QuestAIController)...` | key | pathId | type | HP | speed m/s | dash speed, min-max m | projectile m/s | charge | spawner mesh (B43 pathId) | material | AABB center / extent (m) | box collider |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| GoblinBasicGroundMelee2020 (sword goblin) | c13 | 6419816383160993212 | 0 | 100 | 3 | - | - | - | Goblin_Ground_Spawner -5488257243879500597 | Goblin_Ground_Green_Mat (B1DF -7822277281660078164) | (0,0.64,0.18) / (0.37,0.64,0.48) | 0.57x1.06x0.66 |
| GoblinBasicFlyingRanged (imp) | 42 | 8532791956978764326 | 1 | 100 | 3 | 4, 2-4 | 6 | - | Goblin_Flying_Spawner 3932211932813876055 | Goblin_Flying_Green_Mat | (0,0.33,0.08) / (0.79,0.37,0.32) | 0.4x0.7x0.4 |
| GoblinKnightSpin (red barrel knight) | a18 | -6464216221385053130 | 2 | 333 | 2.5 | - | - | 8 m @ 12 m/s | Goblin_Knight_Spawner -1441323512065202166 | Heavy_Goblin_RED_Mat | (0,1.72,0.12) / (0.50,1.73,0.60) | 1x1x1 |
| GoblinBasicGroundRanged (cannon goblin) | 82 | 5184757040037063358 | 4 | 100 | 3 | 4, 2-4 | 9.5 | - | Goblin_GroundCannon_Spawner 1424814659385735801 | Quest_Goblin2_GroundCannon_Mat | (0.12,0.59,0.05) / (0.45,0.59,0.35) | 0.67x1.19x0.71 |
| GoblinEliteGroundMelee (red goblin) | 76c | 7090226680046923864 | 5 | 200 | 4 | - | - | - | Goblin_Ground_Spawner | Ground_Goblin_RED_Mat | as type 0 | 0.57x1.06x0.57 |
| GoblinEliteFlyingRanged (red imp) | 082 | -4703166778525884900 | 7 | 200 | 3 | 6, 2-4 | 6 | - | Goblin_Flying_Spawner | Flying_Goblin_RED_Mat 2 | as type 1 | 0.4x0.7x0.4 |
| GoblinKnightCharge (green barrel knight) | e0 | 8134796981067487268 | 8 | 250 | 2.5 | - | - | 8 m @ 12 m/s | Goblin_Knight_Spawner | Heavy_Goblin_Mat | as type 2 | 1x1x1 |
| SciFiBasicGroundRanged (red laser bot) | 8cc | 9027785122236765612 | 9 | 100 | 2.5 | 4, 2-4 | 9 | - | SciFi_Enemy_Ground_Spawner -6454959622133763691 | Enemy_SciFi_Ground_Red_mat (B1DF -4670451985027745027) | (0.01,0.52,0) / (0.40,0.52,0.28) | 0.64x1.03x0.57 |
| SciFiGroundExplosive (RecRoomba) | 5b8 | -1955902563095163582 | 10 | 10 | 4 | - | - | - | Scifi_Ground_Roomba_Spawner 6068598108526825387 | Scifi_Enemy_RecRoomba_Mat | (0,0.34,0.03) / (0.39,0.34,0.38) | 0.78x0.69x0.76 |
| SciFiBasicFlyingRanged2020 (pink drone) | dfd | 2710228449644666749 | 11 | 100 | 2.5 | 4, 2-4 | 21 | - | Scifi_Enemy_flying_Spawner 8643553812010953351 | Enemy_SciFi_Flying_Pink_mat | (0.02,-0.03,0.05) / (0.74,0.42,0.52) | 0.77x0.75x0.57 |
| GoblinBasicMeleePike (pike goblin) | 2 | 1761435800380639598 | 12 | 100 | 3 | - | - | - | Goblin_GroundPike_Spawner 743861734721509466 | Quest_Goblin2_GroundPike_Mat | (0.11,0.81,0.10) / (0.49,0.81,0.41) | 1x1x1 |
| GoblinVolley (red volley flyer) | c29 | -8189656542275648087 | 13 | 200 | 3 | 6, 2-4 | - | - | Goblin_Volley_Spawner -5469179575463571624 | Flying_Goblin_RED_Mat 2 | (0,0.45,-0.01) / (0.79,0.51,0.47) | 0.68x1.03x0.6 |
| SciFiKnightAssassin (heavy assassin bot) | f9b | 731329598396593185 | 14 | 1600 | 2 | - | 100 | - | SciFi_Heavy_Main_Spawner -797689930067798158 | Scifi_Enemy_Heavy_mat | (0,0.95,-0.04) / (0.47,0.97,0.48) | 0.94x1.94x0.96 |
| SciFiEliteFlyingRanged (purple drone) | 9ca | -3193146125236556109 | 18 | 400 | 4 | 6, 2-4 | 21 | - | Scifi_Enemy_flying_Spawner | Enemy_SciFi_Flying_Purple_mat | as type 11 | as type 11 |
| SciFiEliteGroundRanged (purple bot) | 3c | 820307718424098879 | 19 | 450 | 4 | 6, 4-8 | 15 | - | SciFi_Enemy_Ground_Spawner | Enemy_SciFi_Ground_Purple_mat | as type 9 | as type 9 |
| PirateGroundMelee2020 (swashbuckler skeleton) | 15 | -2804306731780228049 | 21 | 100 | 1.5 | - | - | - | Pirate_Skeleton_Spawner_Melee -1465685487550230886 | Skeleton_PirateEnemies_Sword_Mat (B1DF 2447065726456068879) | (0.01,0.90,0.22) / (0.39,0.88,0.58) | 0.67x1.49x0.56 |
| PirateExplosiveThrowing (rum-jug thrower) | 94 | 6557234845296924633 | 22 | 100 | 1 | 1, 0.65-1.5 | 11 | - | Pirate_Skeleton_Spawner_JugThrower 2628053029238250039 + `_Jug` 877599533792795191 | Skeleton_PirateEnemies_Mat + RumJug_Mat | (0.04,0.74,-0.08) / (0.42,0.73,0.43) | 0.64x1.48x0.46 |
| PirateBottleThrowing | 26a | -1973149871575316891 | 23 | 100 | 1 | 1, 0.65-1.5 | 13 | - | Pirate_Skeleton_Spawner_BottleThrower 6446559093119344902 + `_Bottle` -2030328137313656570 | Skeleton_PirateEnemies_Bottle_Mat + Consumable_Sarsaparilla_mat (B1DF) | extent (0.63,0.84,0.32) | 0.54x1.46x0.55 |
| PirateFlintock2020 (flintlock skeleton) | fcf | 5855762008578856982 | 26 | 100 | 1 | 1, 0.65-1.5 | 35 | - | Pirate_Skeleton_Spawner_Flintlock 2680252049924573810 | Skeleton_Dead_Mat | (0,0.80,0.23) / (0.36,0.79,0.53) | 0.67x1.58x0.5 |
| PirateGibbet (skeleton in hanging cage) | 89b | -8128668697506963021 | 27 | 100 | 1 | 1, 0.65-1.5 | 8 | - | Pirate_Skeleton_Spawner_Gibbet -3689306148706259065 + `_GibbetGuy` 365621989491339143 | BoxRectangle_JailBars_Porthole_Mat + Skeleton_PirateEnemies_Gibbet_Mat | cage extent (0.44,0.75,0.45) | 0.87x1.50x0.90 |
| DraculaBellhop (musket skeleton) | 549 | -4719707820311702549 | 28 | 75 | 1 | 1, 0.65-1.5 | 50 | - | Dracula1_Skeleton_Belhop_Spawner -2040291713188767935 | Quest_Dracula1_Skeleton_Musket_Mat | (-0.08,0.98,0.42) / (0.43,0.98,0.81) | 0.69x1.84x0.60 |
| DraculaBoneThrower2020 (maid skeleton) | 2b | 5619385082125101759 | 29 | 100 | 2 | 1, 2-4 | 10 | - | Dracula1_Skeleton_Maid_Spawner -9104988208319164705 | Quest_Dracula1_Skeleton_Mat | (-0.02,0.78,0.04) / (0.47,0.77,0.40) | 0.78x1.54x0.62 |
| DraculaBatRanged | e6 | 1381644359212475065 | 30 | 50 | 5 | 4, 6-8 | 9 | - | Dracula_Bat_Spawner 4544958468522964155 | Quest_Dracula1_Bat_Ranged_Mat | (0,0.20,0) / (0.75,0.41,0.14) | 1.51x0.82x0.28 |
| DraculaBatLunging | 9c | 1479409784065389657 | 31 | 50 | 2 | 2, 2-4 | - | - | Dracula_Bat_Spawner | Quest_Dracula1_Bat_Mat | as type 30 | as type 30 |
| DraculaMelee (pumpkin-head skeleton) | b | 1481565135588116667 | 34 | 100 | 2.25 | - | - | - | Dracula1_Skeleton_Pumpkin_Spawner -5661343718152686666 | Quest_Dracula1_Skeleton_Pumpkin_Mat | (0.18,0.76,-0.12) / (0.66,0.77,0.26) | 0.85x1.54x0.52 |
| DraculaWolfLunging2020 | e4f | 430285960656641479 | 35 | 200 | 3 | 2, 2-4 | - | - | Dracula_Wolf_Spawner -2870562877146769279 | Quest_Dracula1_Wolf_Mat | (0,0.56,0.10) / (0.40,0.56,0.83) | 0.79x1.12x1.67 |

Materials are in B43 unless marked B1DF. Type numbers without a spawner (3, 6, 15-17, 20, 24, 25, 32, 33) are enemy
kinds that exist only in the official quests (GUESS: bosses/minibosses).
Heights from the AABBs: goblin 1.28 m, pike goblin 1.63 m, barrel knight 3.45 m, laser bot 1.03 m, heavy bot
1.94 m, pirate skeletons 1.5-1.6 m, CBM skeletons 1.54-1.96 m, wolf 1.12 m tall x 1.67 m long, bat 1.5 m wingspan.

Enemy-to-quest mapping (names, materials, wiki):
- **Golden Trophy (Goblin1):** sword goblin (0), imp (1), elite red goblin (5), elite red imp (7), barrel knights
  (2 spin, 8 charge; weak spot on the back per the wiki). Boss Goblin King: not in bundles.
- **Crimson Cauldron (Goblin2):** the Goblin1 set plus cannon goblin (4, material `Quest_Goblin2_GroundCannon_Mat`),
  pike goblin (12, `Quest_Goblin2_GroundPike_Mat`), volley flyer (13); mortar SFX `Goblin2_Mortar_Charge01/Fire01/
  Impact01/Incoming01`. Boss Crimson Witch: not in bundles.
- **Isle of Lost Skulls (Pirate1):** pirate types 21-27. Boss Ghost Beard: not in bundles.
- **Rise of Jumbotron (SciFi1):** sci-fi types 9-19 (mice = RecRoomba GUESS). Boss Jumbotron: not in bundles.
- **Blood Moon (Dracula1):** Dracula types 28-35; miniboss coin drops exist for Bat, Wolf, BoneThrower, BoneMelee
  (section 2.4), so those four act as minibosses at larger scale/health (GUESS). Boss Dracula: not in bundles.

#### 2.2.2 Skinned enemy rigs (bundle BTR, container keys are full asset paths)
Only three enemy bodies exist as skinned, animated rigs: Rec Room's *player transformation* costumes, key
`Assets/Activities/Quest/Shared/Content/Enemies/Transformations/<Name>.prefab`. Root has
`PlayerTransformationAnimationManager {animator}` and `PlayerTransformationHeadOffsetShifter {offset}`; root local
position (0,-1,0) (authored to hang under a player's head; reset to 0 for NPC use).

| Prefab (pathId) | Animator controller | Clips (pathId, length s) | Renderers / materials | Bones to drive procedurally |
|---|---|---|---|---|
| `Goblin_Ground_PlayerTransformation` (5670464857732337561), offset -0.75 | `GoblinIdleWalk` override (-7440278327319042748) of `SimpleIdleWalk` (2977298251725366011); Avatar `Goblin_Ground_Skinned_fbxAvatar` | `Goblin_Idle` (8873101534263580173, 1.45, loop); `Goblin_WalkCycle` (8979700462539619258, 1.0, loop) | SMR `Goblin_Ground_Mesh` (-8274895958940605662, 2688 verts) + `_LOD1`; `Goblin_Ground_Green_Mat` (B1DF); LODGroup size 7.11 | Goblin_Ground > Arms > LeftArm, RightArm; Body > Flap1-4; Head > Helmet, Jaw; Rope |
| `Pirate_Ground_PlayerTransformation` (-3705871020688775911), offset -0.35 | `PirateIdleWalk` (-2617946664027744302); Avatar `Quest_Pirate1_Skeleton_MeleeAvatar` | `Pirate_MeleeEnemy_Idle` (4258814246261097107, 3.5, loop); `Pirate_MeleeEnemy_Walk` (-8048904514212593123, 1.0, loop) | SMR `Quest_Pirate_Skeleton_Melee` (1522681531779011212, 6328 verts, 2 submeshes: material slot 0 is null, slot 1 `Skeleton_PirateEnemies_Sword_Mat`) + `_LOD1` | Skeleton_CoG_Jnt > Pelvis > L/R LegUpper > LegLower > Foot; Ribs > L/R Scapula > ArmUpper > ArmLower; Skull_Jnt > SkullRope_Jnt |
| `SciFi_Enemy_Ground_PlayerTransformation` (9197020357579987806), offset -0.75 | `LaserBotIdleWalk` (1288551912102837495); Avatar `SciFi_Enemy_Ground_Skinned_fbxAvatar` | `SciFi_Enemy_Ground_Stand` (915026056891607832, 1 frame); `SciFi_Enemy_Ground_Walk` (-8297733343663411481, 1.0, loop) | SMR `SciFi_Enemy_Ground_msh` (5326335320884148207, 3431 verts) + `_LOD1`; `Enemy_SciFi_Ground_Red_mat` (B1DF) + `Scifi_Enemy_Face` (BTR -7810645200654956286) | SciFi_Ground_Body > Cannon, DiscDrive, Head > Face, LEar, REar; Keyboard; LTread, RTread; Torso; Rope |

Controller parameters are unknown (the base `SimpleIdleWalk` state machine is not dumped); simplest is to drive the
two clips ourselves with a `PlayableGraph` / `AnimationMixerPlayable` (idle<->walk weight = speed / movementSpeed)
and add attacks/hit/death procedurally on the bones in `LateUpdate`. Skinned-mesh AABBs are in bind space (y
centre 3.5 m) and not usable for sizing; use the spawner AABBs above. Pirate slot-0 material is null: assign
`Skeleton_PirateEnemies_Sword_Mat` to both slots (GUESS) or leave the submesh unrendered.
Elites: swap `sharedMaterials` to the red/purple materials of the same mesh (all resident once the matching spawner
prefab is loaded). Also in BTR: `VFX_Generic_SmokePoof01` (4281126155123845646) - spawn/despawn poof.

### 2.3 Weapons per quest (all B43, all keyed prefab roots; data from `q\wtable.txt`, `q\wsumm.txt`, `q\wp\*.txt`)
Rec Room weapon components are missing scripts at runtime: **we implement the behaviour** and read these numbers.
`enemyDamage` = `enemyDamageSettings.MinDamage-MaxDamage` (melee: scaled by swing speed between
`minSpeedImpulseSpeed` and `maxSpeedImpulseSpeed` m/s, GUESS linear); `playerDmg` = damage to players (friendly
fire, percent of 100 HP). `rate` = `maximumFiringRate` shots/s. Damage types: 1000 melee, 1001 shield, 1002
thrown breakable, 2000 pistol, 2001 bow/crossbow, 2002 shotgun, 2003 automatic, 2004 rail, 3000 explosive/whip,
3001 wand. `Handedness` 0 = one hand, 1 = off-hand (shield/hook), 2 = either. Equip slots: hip/shoulder flags as
listed (Rec Room lets you holster on hip or over the shoulder).

| Weapon (key, pathId) | Quest(s) | Class | enemyDamage | playerDmg | rate / speed / reload | Other | Key audio |
|---|---|---|---|---|---|---|---|
| `(QuestSword)` 14, -7308406628275338915 | GT, CC | MeleeWeapon | 67-100 (swing 3-15 m/s), crits, throwable x1 | 100 | - | blocks enemy melee; shoulder slot; mesh `Sword3` (B1DF) | swing `Throw01`, hit `Quest_MeleeImpact_01` / `Quest_Sword_ImpactHeavy01`, env `Quest_Sword_ImpactEnvironment01`, shield `Quest_Sword_Impactshield` |
| `(QuestShield)` 1f, -1545829764821527151 | GT, CC | MeleeWeapon 1001 | 1 (bash 0-3 m/s) | - | - | off-hand; blocks melee and projectiles; reflects witch orbs (wiki: sword, shield, pot or potion can return them); meshes `Quest_Shield`, `Quest_Shield_Spikes` | `Quest_Sword_Impactshield`, pickup `paintballShield_Pickup` |
| `(QuestMeatSword)` 35e, 2293466989750540216 | skin (ham) | MeleeWeapon | 67-100 | 100 | - | sword with `Quest_Campfire_Ham` mesh (B 30d07e20); treat as cosmetic variant | as sword |
| `(Longbow)` 2a5, -3472331185247861377 | GT, CC | Bow | 17-67 (draw strength), crits | 60 | arrow 50 m/s, reload 0.5 s | projectile `[Arrow]` (-6052086741775502907, meshes `Arrow`, `Arrow_LOD1`); two-handed draw; arrows stun basic goblins/imps (wiki) | fire `Longbow_ArrowFly01`, nock `Longbow_NockArrow02`, pull `Longbow_TensionPull02`, creak `Quest_Longbow_Creak`, hit `Quest_Arrow_Impact_02`, pickup `LongBow_Pickup01` |
| `(Crossbow)` 417, -794468755873281445 | GT (found in act 2), CC | Crossbow | 17-67 | 100 | bolt 10 m/s (sic), 2/s, reload 0.5 s | one hand; bolt `[CrossbowBolt]` (-2993712895970678811); muzzle `[VFX_PaintBall_Shoot]` | `Quest_Crossbow_fire01`, `Quest_Crossbow_Reload01` |
| `(Crossbow_Hunter)` dd8, -3416171104567349203 | CC (upgraded crossbow) | CrossbowSimple | 17-67 | 35 | bolt 40 m/s, 2/s, reload 0.5 s | mesh `Crossbow_Reload` | `Goblin2_UpgradedCrossbow_Fire01`, `_BoltLoaded01`, `_Pickup01`, `_Whizby01/02` |
| `(Quest_Goblin_Wand)` c7, 6501105180001912045 | CC | ForceWandWeapon 3001 | 20-60, no crits | 8 | bullet 18 m/s, 2/s, reload 0.67 s, max range 30 m | bullet `[Quest_Goblin_WandBullet]` (-8831872315314471116), impact `[VFX_Wand_Impact]`, muzzle `[VFX_Wand_MuzzelFlash]`; meshes `polySurface12` (B1DF), `Quest_Wand_Star` | fire `Goblin2_RomanCandle_Fire01`, charge `Goblin2_EnergyWand_Spawn01` |
| `(Quest_Goblin_FirePotion)` 7ed, 429743082938597441 | CC | MeleeWeapon 1002 + Breakable | 50 (thrown, shatters on any impact >= 2 m/s) | 100 | - | GUESS: area fire on shatter; meshes `Quest_Potion`, `_Cork`, `_Liquid` (B1DF) | `Goblin2_Potion_Pickup01`, `Goblin2_Potion_Smash01/02` |
| `(Whip)` e7e, -4191416349721915365 | CBM | Whip 3000 | 40-90, crits | - | popper 15 m/s, 3/s, reload 0.5 s | explosion r 0.5 m force 20 at tip, `blastRadiusOverDistance` 0.75-3 m, `powerOverDistance` 0.1..1 over charge 7 s, coiling 0.12 s; crack VFX `[Quest_Dracula1_WhipCrack_VFX]` / `_Crit_VFX`; also hooks `WhipHookshotPoint`s; `AutomaticHolsteringTool` | `Quest_Dracula1_Whip_Prime01`, `_ThrowLight01/Heavy01`, `_CrackLight01/02`, `_CrackHeavy01`, `_CrackUltra01`, `_Pickup01` |
| `(Quest_Dracula1_Hookshot_Sandbox)` bc8, -5024532721708749692 | CBM (traversal point) | WhipInteractable + WhipHookshotPoint | enemyDamage 1000 field (unused) | 1 | - | `landingPoint` transform: whip-crack the point to teleport there; meshes `Cable_V1`, `Beam_Crossbrace`, `Beam_Long`, `Quest_Pirate_FlourSack` | hooked `Quest_Dracula1_WhipTraversal_Hooked_01`, teleport `Quest_Dracula1_WhipTraversal_Teleport_01` |
| `(Quest_Dracula1_1H-Crossbow)` 08, -3961491805328472840 | CBM | Crossbow | 17-67 | 100 | bolt 10 m/s, 2/s | hip + shoulder | as Crossbow |
| `(Quest_Dracula1_Crossbow_Hunter)` 2f4, 8404945553503185985 | CBM | CrossbowSimple | 17-67 | 35 | bolt 40 m/s, 2/s | hip + shoulder | as Crossbow_Hunter |
| `(Quest_Dracula1_BlunderBuss)` 7d, 1527500553090847035 | CBM | Shotgun 2002 | 25 per pellet | 100 | 75 m/s, 0.75/s, max range 11 m | projectile `[Quest_Pirate1_FlintlockProjectile]`, impact `(Quest_Pirate1_FlintlockImpact_VFX)` (B1DF), muzzle `[Quest_Pirate1_Cannon_Fire_FX]`; animator `Pirate_Blunderbuss_Animator` | `Quest_Pirate1_FlintlockPistol_Fire03`, `_FlintlockReloaded01`, `AmmoOut_01` |
| `(Quest_Dracula1_Flintlock)` cc, 2583638830538025949 | CBM | Pistol 2000 | 90 | 100 | 75 m/s; single shot then manual reload (flip) | as Pirate flintlock | as Pirate flintlock |
| `(Quest_Dracula1_PitchFork)` 5b, 3400544043349533365 | CBM | MeleeWeapon, destructible | 40, crits | 100 | - | breaks after use (isDestructible); meshes head1-4, handle | `Quest_Dracula1_PitchFork_*` |
| `(Quest_Dracula1_Rake)` 37, 2205938377985692009 | CBM | MeleeWeapon, destructible | 60 | 100 | - | ProBuilder meshes | `Quest_Dracula1_Rake_*` |
| `(Quest_Dracula1_Shovel)` 364, -1326650752992200178 | CBM | MeleeWeapon, destructible | 60, crits | 100 | - | meshes head1-4, handle | `Quest_Dracula1_Shovel_*` |
| `(Quest_Dracula1_SkeletonBone)` 649, -7669376316608311721 | CBM (thrown by maid skeletons) | MeleeWeapon 1002 + Breakable | 80 | 0 | - | not pickable (CanBePickedUp 0); mesh `Bone_Whole` | `Quest_Pirate1_BottleThrow01`, `Quest_Dracula1_SkeletonBone_HitEnvironment02` |
| `(Quest_Dracula1_MiniBossSkeletonBone)` 413, 2852938947218050860 | CBM miniboss projectile | + Grenade 3000 | 80, blast r 2 m force 10 | 100 | - | explodes: `Quest_Dracula1_BoneGrenade_Explosion01` | |
| `(Quest_Dracula1_Bat_Plushy)` 76, 4436530697076807301 | CBM (collectible toy) | Tool | - | - | - | skinned `Quest_Dracula1_Bat_Skinned_fbxAvatar` | squeak pickup |
| `(Quest_Pirate1_Cutlass)` fbd, 2608633785829740115 | Pirate | MeleeWeapon, destructible | 60-80 | 100 | - | sparks `(Quest_Pirate1_Weapon_Sparks_VFX)` (B1DF); meshes Cutlass_01-04 (break stages) | `Quest_Pirate1_CutlassSwoosh01-04`, `_HitEnemyLight/Heavy`, `_Parry01-03`, `_Pickup01` |
| `(Quest_Pirate1_Flintlock)` 328, 3869957709208423819 | Pirate | Pistol | 90, crits | 100 | 75 m/s; one shot then flip-reload | projectile `[Quest_Pirate1_FlintlockProjectile]` (a cork, mesh `Quest_Potion_Cork`), impact/smoke VFX (B1DF); animator override `Pirate_Flintlock_Override_Controller` | `..._FlintlockPistol_Fire04`, `_PullTrigger01`, `_ReloadTick01`, `_Reloaded01`, `_FlintlockCorkImpact01` |
| `(Quest_Pirate1_BarStool)` a9e, 6799285001596601829 | Pirate | MeleeWeapon, destructible | 150 | 100 | - | heaviest hit; mesh `Quest_Pirate1_BarStool` | `Quest_Pirate1_Stool_*`, `StoolShatter01` |
| `(Quest_Pirate1_FryingPan)` 3b5, -4087794432169899452 | Pirate | MeleeWeapon, destructible | 100 | 75 | - | | `Quest_Pirate1_FryingPan_*` |
| `(Quest_Pirate1_Oar)` 4, 6649324011998393314 | Pirate | MeleeWeapon, destructible | 60 | 100 | - | | `Quest_Pirate1_Oar_*` |
| `(Quest_Pirate1_PushBroom)` 76a, 5078876987832169548 | Pirate | MeleeWeapon, destructible | 60 | 100 | - | mesh `Quest_Pirate_BreakableBroom` | `Quest_Pirate1_Broom_*` |
| `(Quest_Pirate1_BreakableFish)` 7a, 1849081970389669758 | Pirate | MeleeWeapon, destructible | 40 | 100 | - | squeaky fish (joke weapon) | `Quest_Pirate1_SqueakyFish_*` |
| `(Quest_Pirate1_BeerBottle)` fbb, 5156788414105738343 | Pirate | thrown 1002 + Breakable | 80 | 0 | - | shatters on any impact; mesh `Quest_Pirate1_Bottle` (+ `_Fractured`) | `Quest_Pirate1_BottleThrow01`, `_BottleShatter01/03/05` |
| `(Quest_Pirate1_Mug)` 20, -2712311130349214368 | Pirate | thrown + Breakable | 80 | 100 | - | shatters above 2 m/s | `Quest_Pirate1_WoodShatter01/02` |
| `(Quest_Pirate1_Plate)` e4, -978619375690687182 | Pirate | thrown + Breakable | 80 | 75 | - | mesh `Plate_Whole` | `Quest_Pirate1_Plate_Smash01/02` |
| `(Quest_Pirate1_RumJug)` de, 6913928489384898333 | Pirate | thrown + Breakable + ExplosiveWeaponAddon | 80 + explosion | 100 | - | explosion VFX `[Quest_Pirate1_RumExplosion_VFX]`; also the jug-thrower skeleton's projectile | `Quest_Pirate1_JugShatter01`, `_JugExplosion01` |
| `(Quest_Pirate1_CoconutBomb)` 91, 7371352274782639299 | Pirate | Grenade 3000 | 80, blast r 2 m, force 10 | 100 | - | fuse sparks `[Quest_Pirate1_CoconutBomb_Sparks_VFX]`; mesh `Coconut` | throw `Quest_Pirate1_BottleThrow01` |
| `(Quest_Pirate1_HookHand)` 62, 5024657719698697115 | Pirate | Tool (off-hand) | - | - | - | mesh `Quest_Pirate1_Hook`; GUESS: hook to climb/grab ropes | |
| `(Quest_SciFi_Pistol)` 447, 7169440132558149863 | Jumbotron | Pistol 2000 | 25, crits | 30 | laser 80 m/s, 3/s, reload 0.5 s (grab the top handle) | bolt `[Quest_SciFi_PistolLaser]`, impact `[VFX_Laser_WeaponImpact]`, muzzle `[VFX_Laser_MuzzelFlash]`; override controller `Scifi_Pistol_Override_Controller` | `LaserPistol_01`, `Scifi_reload_generic_01`, `AmmoOut_01` |
| `(Quest_SciFi_Shotgun)` 40, -4911881099049790927 | Jumbotron | Shotgun 2002 | 16 per pellet, crits | 8 | 60 m/s, 1/s, range 40 m | `[Quest_SciFi_ShotgunLaser]`, `[VFX_Laser_MuzzelFlash_ShotGun]` | `Quest2_Cannon_Fire10` |
| `(Quest_SciFi_AutomaticGun)` 0bd, 7207755909999432204 | Jumbotron ("Laser Burst") | AutomaticGun 2003 | 8 | 6 | 80 m/s, 15/s auto | `[Quest_SciFi_AutomaticLaser]`, `[VFX_Laser_WeaponImpact_Small]` | `burst_02` |
| `(Quest_SciFi_RailGun)` 2a6, -4887995713686368716 | Jumbotron | Pistol 2004 (charge) | 70, crits | 80 | 160 m/s, 1/s, reload 0.6 s | charge loop; `[Quest_SciFi_RailGunProjectile]`, `[VFX_Laser_WeaponImpact_RailGun]`, `[VFX_Laser_MuzzelFlash_RailGun]` | `Sniper_01`, charged `Quest2_Rifle_FireCharged01`, `Quest2_Rifle_Charging01`, `Quest2_Rifle_ChargedLoop03` |
| `(SciFi_Grenade)` a3a, -3006756886563974688 | Jumbotron (GUESS) | grenade | not dumped | | | explosion `[Arena_Grenade_Explosion_VFX] JumboTron Variant` (B43 -4669758548333532865) | |

Sword skins in bundle 30d07e20 (`QuestSword_Clarinet/Ham/JackOLantern/NeonSpikes/RubberChicken/Umbrella_Prefab`)
are cosmetic reskins. Laser projectiles all use mesh `CubeBulged_LOD0` (B1DF) stretched. Weapon animators:
`Crossbow_Controller`, `Bow_Controller`, `Default_Gun_Controller`, `Pirate_Blunderbuss_Animator`, override
controllers `Scifi_Pistol/Shotgun/MachineGun/Railgun_Override_Controller`, `Pirate_Flintlock_Override_Controller`
(all B43; clips `Scifi_*_Fire_Anim`, `*_ManualReloadOpen/Closed_Additive_Anim`, `Pirate_Flintlock_Recoil/Hammer/
FireFail/Idle_ReloaderOpen/Closed`). Animator + clips are Unity built-ins, so these reload/recoil animations
*play* if we set the triggers (parameter names not dumped: read them from the controller at runtime via
`animator.parameters`).

**Potions.** Quest-themed drinkable potions (bundle 85515de1..., `RecRoom.Activities.Quest.Goblin2.Potion` +
`DrinkableBottle` + `BreakableObject` + `MeleeWeapon` 50 dmg thrown): `(Goblin1PotionConsumableTool)` key 454
(-5443669074983145148, liquid `Consumable_PotionLiquid_Gold_Mat`), `(Goblin2PotionConsumableTool)` key 8b2
(-2302459760741190609), `(SciFiPotionConsumableTool)` key 4f9 (6752043634857175492), `(Pirate1PotionConsumableTool)`
(-1221214204825432734), `(Dracula1PotionConsumableTool)` (6758808412864313266). Potion fields: `drinksToActivate` 2,
`drinkResetTime` 2 s, `powerupType` -1 in these store versions; DrinkableBottle `lifeSpan` 8 s, `drinkInterval`
0.5 s, `pourInterval` 0.25 s, uncork `Goblin2_Potion_Uncork01`, pour `Goblin2_Potion_PourRepeat01`, gulp
`Goblin2_Potion_Gulp01`. Rec Royale health potion `(RecRoyale_HealthPotion)` (B43 -9033987142647402370):
`defaultHealthRestoreAmount` 10 over 0.5 s. Use the Goblin1 potion model as the in-quest **health potion** and the
Goblin2 one as the Crimson Cauldron **double-damage potion** (GUESS mapping; the wiki confirms both exist).

### 2.4 Collectibles, loot, coins, breakables, chests (B43 unless noted)

| Prefab (key, pathId) | Use | Data |
|---|---|---|
| `(FatPot)` fe, 2319845052722683669 | breakable clay pot, all medieval quests | `BreakablePointsContainer` + `PointsDropper` 50 points; mesh `FatPot` (2399465769510351343, extent 0.19x0.22x0.19 m = 0.38 x 0.44 m pot), chunks `FatPot_Chunk1-7` (B1DF), `Pot_TerraCotta_mat`; smash `Quest_PotterySmash01-03` |
| `(FatPot_Large)` 0a6, 8363078193670886477 | large pot | 100 points |
| `(SciFatPot)` 564, -1544727655544018294 | sci-fi breakable (Jumbotron) | 50 points, material `Computer_Retro_Mat`; SFX `Quest_Scifi_collectable_smashed01-05`, collected `Quest_Scifi_collectable_collected02` |
| `(Quest_Dracula1_Collectable_Large)` 49f, -994500571964164069 | breakable candelabra | 3 silver (`PointsDropper` min=max 3, `dontAddToCombatScore` 1), shatter >= 2 m/s; meshes `breakable_candleabra_stand` (407096397711457020), `CandleFlameMesh`, `FlamePlane` |
| `(Quest_Dracula1_Collectable_Small)` 25e, 6236189600499215590 | small candle | (not dumped; GUESS 1-2 silver) |
| `(Quest_Dracula1_Collectable_Wall)` 1c, 2760856849023593116 | wall sconce candle | 2 silver |
| `(Quest_Dracula1_CollectablePumpkin_Small)` 0f, 2896811292249745900 | jack-o-lantern | 3 silver; mesh `JackOLantern` (bundle 30d07e20); `Quest_Dracula1_PumpkinCollectableBreak01` |
| `(Quest_Dracula1_CoinStack_Small)` 56e, -3606132516128229634 | silver pile pickup | `LootTool` lootType 0; mesh `Coin_Stack_Small_LOD0` (1985698717357699797, 0.27x0.16x0.30 m), material `Coin_Dracula_Mat`; beacon mesh `ToolBeacon` (B1DF) |
| `(Quest_Dracula1_ChestCoins_Small/Medium/Large)` c4 / 93 / 717 | chest reward piles | lootType 2; Large `customValue` 200 |
| `(Quest_Dracula1_MiniBossCoins_Bat/_Wolf/_BoneThrower/_BoneMelee/_Musket)` dbc, 4c, 12a, 47e, efb | miniboss drops | lootType 2, mesh `Coin_Pile_Small_LOD0` (-5022897115491266917, 0.79x0.26x0.69 m); pickup `Quest_Dracula1_Silver_MiniBoss_Pickup01` |
| `(Quest_Pirate1_CoinStack_Small)` bb, -4329487791221066249 | gold pickup | as Dracula stack, `Coin_Pirate_Mat` |
| `(Quest_Pirate1_LostLoot)` 8af, -3621960599966184653 | dropped-on-death gold pile | lootType 0, `nameTagCanvas` (owner's name), blink 4 s, box 0.63x0.46x0.55 |
| `[Quest_SciFi_CreditParticle]` (implicit, 3394427581457770912) | credit chip to pick up | `SpawnableCollectible` value 50 (manager); mesh `Scifi_Credit_Chip` (2279281034897578813, flat chip 1.7x0.19x2.0 local units, scale down) |
| `[Quest_Dracula1_Silver_Store_VFX]` (implicit) | silver collectible | value 10 |
| `(Golden_Bucket)` 1c9, -1180097354083664138 | **the Golden Trophy** (a golden bucket) | mesh `GoldenBucket_Trophy_LOD0` (-9161849157953319434, 0.35 x 0.51 x 0.26 m), `GoldenBucket_Trophy_Mat` |
| `(Sandbox_Barrel)` 96b | wine barrel prop | meshes `WineBarrel_LOD0-2`, collision mesh |
| `(MetalBarrel_Sandbox)Variant` 79 | metal barrel | |
| end-of-quest chests | reward chest per player | spawners `(LootChest_AddressablePrefabSpawner)` (bundle 743bb99f, `giftPrefabName` "(LootChest)"), `(LootChest_SciFi_...)` / `(MiniLootChest_SciFi_...)` (e14fee75), `(Quest_Pirate1_LootChest_...)` / `_StoreLootChest_` (d8e69164), `(Quest_Dracula1_LootChest_...)` / `_StoreLootChest_` (e2f76543). The chest prefabs themselves are loaded by name at runtime from outside the plain index (not found): build our own chest from `SciFi_TreasureChest_Handle` (bundle 4056f2fc) + procedural box, or a Rec Royale loot box (GUESS) |

Point VFX: `[VFX_GoldCoins]` (6149828590530533466), `[VFX_BossGoldCoins]` (4837748863013848360),
`[VFX_CreditChips]` (6910145093392166811), `[Quest_Dracula1_Silver_VFX]` (-7833942051480654425),
`[Quest_Dracula1_Silver_Boss_VFX]` (-5100327133226368256) - all implicit in B43, resident after the quest rule
prefab loads.

### 2.5 Player health, down / revive
- **Nothing quest-specific ships except audio + config**: `Quest_Revive` (bundle 07e99906a41705d55ca3f33ca33f1672,
  key `9`, 7.03 s, mono) and `RecRoyale_Revive_Revived01` (B43 4907343187002660675); screen-mode animation asset
  `Screens_Quest_WaitingForRevive_01` (bundle 063b6b66..., `PlayerAnimationAsset`, for Rec Room's own avatar; we do
  not use it on the Somnium avatar).
- `CombatRevivePerHandScreenPlayerBehaviorConfig` (bundle 063b6b6653aa642616fd21d3d4701899 pathId 1818224689998502492, `RecRoom.Core.Controllers.
  ScreenBehaviors.CombatReviveScreenPlayerBehaviorConfig`): `MaxTeammateSearchDistance` 5 m,
  `MaxTeammateReviveDistance` **2.5 m**, `PlayerSearchFrequency` 3 Hz, `MaxReviveMoveDistance` 1 m (reviver must
  stay within 1 m while reviving), revive by **high five** (anim speed 1.45) or **handshake** (fade-in 0.4 s).
  `GameReviveHandshakeGesture` components sit on the player's hands (VR: hold a handshake with the downed player).
- Rules from the wiki (summarised): at 0 health a player is downed (cannot attack), a teammate revives by a held
  handshake / high five; if nobody revives within a short time the player is out until the next checkpoint/room;
  both players get brief invincibility after a revive; the team loses when every player is down/out. The manager
  penalises a death by `deathPointsPenalty` (2000 gold, 1200 credits, 200 silver). Downed-player UI sprite
  `Downed_Player_KO_Skin` (B1DF, Sprite -5077236008522997619).
- Health potions: see 2.3 (Rec Royale potion restores 10 HP per drink over 0.5 s; quest potions need 2 drinks).

### 2.6 Bosses (nothing in bundles) and usable stand-ins
No boss mesh, rig, config or sound exists in the plain bundles (searched objects/meshes/clips for King, Witch,
Beard, Jumbotron, Dracula, Boss). Stand-ins available:
- **Goblin King**: scale up `Goblin_Knight_Spawner` (barrel knight, 3.45 m) x1.6-2 with `Heavy_Goblin_RED_Mat` + a
  crown (procedural) + axe (`Quest_Campfire_Sword` mesh / sword mesh scaled) + a big imp cannon (scaled
  `Goblin_GroundCannon_Spawner` cannon part or procedural cylinder). Back target = procedural disc (weak spot).
- **Crimson Witch**: no witch model. Build a procedural cloaked figure (cone robe + sphere head + hat) using the
  flying-goblin material, OR use the `TricksterGhost` skinned rig (bundle 6d3223da, key `0e`, pathId
  6833133397433673540, `TricksterController` animator, `Ghost_Mat`, clips `Ghost_Idle01`, `Ghost_Locomotion01`,
  `Ghost_Interaction01`, `Ghost_CaptureSuccess01/02`, `Ghost_CaptureFail01`; mesh `Ghost_LOD0` 0.82 x 0.50 x 0.79 m)
  tinted crimson and scaled x2 (GUESS, visual stand-in). Orbs: `VFX_Wand_*` particles.
- **Ghost Beard**: `TricksterGhost` scaled x3 with a procedural pirate hat + beard, translucent; ghost SFX
  `Tricks_GhostSpawn01/02`, `Tricks_GhostDespawn01`, `Tricks_GhostCollect01(b)`, `GhostModeBoom`.
- **Jumbotron**: a big screen robot: procedural box body + a screen quad (render the Jumbotron face as a
  `RenderTexture`/emissive quad), arms from `IBeam_5M_Sandbox`/`Pipes_*` pieces, `SciFi_Heavy_Main_Spawner` mesh
  scaled for the lower body (GUESS). Laser = `LineRenderer` + `VFX_Laser_*`.
- **Dracula**: `Dracula1_Skeleton_Belhop_Spawner` or `Pirate` skeleton rig with a procedural cape (cloth-like
  skinned quad) + bat form = `Dracula_Bat_Spawner` scaled x2.5 with `Quest_Dracula1_Bat_Ranged_Mat`.
- Optional animated monster: `TricksterYeti` (6d3223da, -2246481277997280358, `TricksterController_Yeti`, clips
  `Yeti_Idle01`, `Yeti_Walk01`, `Yeti_Spawn01`, `Yeti_React01`, `Yeti_Freeze01`, `Yeti_Exit01`; spawn VFX
  `VFX_Yeti_Spawn` -5054841878521988972, despawn `[VFX_ObjectSpawn] Yeti Despawn`).
- Optional animated robot: Roomie (mesh `Roomie_Mk1_LOD0-2` bundle a4eb4f30, `AnimatorController:Roomie` bundle
  4056f2fc with 58 clips incl. `R_Mk1_Sword_FightAttack01`, `R_Mk1_ThrowObject_*`, `R_Mk1_KnockedOut01`,
  `R_Mk1_FlyForward01`, `R_Mk1_Panic_*`). It is Rec Room's friendly companion robot: only use it for friendly
  NPCs (e.g. a Jumbotron-quest guide), not as an enemy (GUESS/taste).

### 2.7 Music, stingers, ambience (all AudioClips; own bundles are keyed, B43 ones are implicit)

| Quest | Explore | Combat | Boss / other | Win / lose | Ambience |
|---|---|---|---|---|---|
| Golden Trophy | `GoldenTrophyExplore` (48e65ed9e94ca22dc4b1dfa942879b99, key f, 208.6 s) | `GoldenTrophyCombat2` (0bcf564af51fffb5a8190378cb31de94, 45.9 s) | act clear: `GoldenTrophyClearFanfare2` (0086ec4e81c779abdbf39064f081d2dd, 4.5 s) | see 2.1 | - |
| Crimson Cauldron | `CrimsonCauldronExploringtheMines` (d37aa2e80bd056ef71458502f51c8ac1, 135.6 s) | (reuse GoldenTrophyCombat2, GUESS) | `CrimsonCauldronTheBalladofVotaVader` (00b726373740e0ab6f7d3e794c714c60, 72.6 s; lobby/boss song, GUESS), `CrimsonCauldronGoblinWorkSong` (09ebc2f0d6ef1b16dc46bcc9d618a3ab, 23.3 s) | see 2.1 | `Quest_Goblin2_Ambiance_Mines01` (0e82ce187511fbc59f4b1031fe08ac0e, 31.6 s, 48 kHz) |
| Isle of Lost Skulls | `PirateQuestShanty2` (53ac5b3859f3415f75cea17226d294ab, key c, 45.1 s), `PirateQuestShanty_Piano` (f2b57b6bdfa02a62cb032b02258a6649, 45.1 s) | `Quest_Pirate1_PirateBrawl01` (8acf4c7587546e3c9d672ff3ab4b523e, 52.3 s, mono) | `PirateShoppingChannel` (228ee7f6e41ba293b995ffcfd7a7dc81, 37.7 s; shop/lobby) | see 2.1 | `Quest_Pirate1_CrowdAmbiance` (6f057ebd509611e43f6c7237a8788d7f, 56.6 s, tavern), `Quest_Pirate1_Caves_GhastlyAmbiance01` (f515f33419d149a2c1681426045c280b, 60.2 s), `Quest_Pirate1_Ambiance_BrookLocal01` (6a40b22f6127c3f8eb057a9edea2a1da, 25.0 s) |
| Rise of Jumbotron | `JumboTronExplore` (410620feb3f9ac3eb03e81ae1e96c7a9, 120.0 s) | `JumboTronCombat1` (d036b095ed1170a60adb65426d13ab18, key 2, 111.1 s) | - | see 2.1 | - |
| Blood Moon | `CBM_Gavotte` (3592c2829c49f983aa167a1f1370bb9d, 133.5 s), `CBM_Ballroom` (8536d664be16ff0c32a2390e85c9917d, 39.8 s; Inverted Ballroom) | `CBM_CombatMiniboss` (ce2d2a2f66ddcc35c86f4944fe6240c5, 69.4 s) | `CBM_CombatBoss` (1b69480d3296b3891f38ea4b04959017, key 0, 116.0 s) | see 2.1 | - |

Shared SFX: `EnemyHit_01` (4ba33c14de1a1e86895f9922ea55518d, 3.6 s), `EnemyHit_02` (dae4270eb1edf01a529850b61326589c);
coins `Quest_Coins_Spawn01`, `Quest_BossCoins`; `Out`; mortar `Goblin2_Mortar_*`; high-five stingers
`Consumable_HighFive_JumboTron01`, `Consumable_HighFive_LostSkulls01` (bundle 85515de1). **No enemy voices, no
narrator VO for quests** (only AmyVO onboarding lines). Enemy barks: use pitched generic hits, `Tricks_Ghost*`,
`Goblin2_Mortar_*`, and our own synthesized grunts (GUESS).
All music is `LoadType` 2 (Streaming) Vorbis; fine for looping via `AudioSource.loop`.

### 2.8 VFX (particle prefabs; built-in ParticleSystems work at runtime, swap Sprites/Default materials per rr_runtime_facts)

| Prefab (bundle, pathId, key) | Use |
|---|---|
| `[VFX_Default_HitParticles]` (B43 6721327806489923429) | player damaged |
| `[Quest_Pirate1_PlayerDamaged_VFX]` (B43 8603557909329089495) | pirate player damaged |
| `VFX_Generic_SmokePoof01` (BTR 4281126155123845646, key = its asset path) | enemy spawn / death poof |
| `[VFX_ObjectSpawn]` (B43 -1545908865104360033), `(VFX_ObjectSpawn)LimitedTimeBundleVariant` (6d3223da, key 79) | spawn-in sparkle |
| `[VFX_Wand_Shoot]` 2564134274005761227, `[VFX_Wand_MuzzelFlash]` 2253959320601601800, `[VFX_Wand_Impact]` 6615655587066232715, `[VFX_Wand_FireWorkCharge]` -7056031617123654704 (B43) | wand, imp orbs, witch orbs |
| `[VFX_Laser_MuzzelFlash]` 938935335124548314, `_ShotGun` 5856511666102955214, `_RailGun` -2824804043139756459, `_RocketLauncher` 7543014403417106077; `[VFX_Laser_WeaponImpact]` -5828286138502666526, `_Small` -237685941335325812, `_RailGun` 368249850319573268, `_RocketLauncher` 8832408295093785813, `[VFX_Laser_Crit_WeaponImpact]` 2002472830690599179 (B43) | sci-fi weapons, robot shots, crit hits |
| `[Arena_Grenade_Explosion_VFX] JumboTron Variant` (B43 -4669758548333532865); `(Arena_Grenade_Explosion_VFX)` (B1DF 8804271494373140557, key dd) | RecRoomba / grenade explosions |
| `[Quest_Goblin2_VFX_BarrelExplosion]` (B43 3525979577847440405) | exploding barrels, mortar |
| `[Quest_Pirate1_Cannon_Fire_FX]` 100697609050316686, `[Quest_Pirate1_RumExplosion_VFX]` 2236718730118751199, `[Quest_Pirate1_CoconutBomb_Sparks_VFX]` -7469138092476084967, `[Quest_Pirate1_FlintlockFire_VFX]` -7146241991039232643, `[Quest_Pirate1_WoodSplinter_VFX]` 8549682015566258736 (B43); `(Quest_Pirate1_FlintlockImpact_VFX)` key 37dc, `(Quest_Pirate1_FlintlockSmoke_VFX)` key 959, `(Quest_Pirate1_Weapon_Sparks_VFX)` key f8 (B1DF) | pirate weapons, cannons, crates |
| `[Quest_Dracula1_WhipCrack_VFX]` -1306632604413931883, `[Quest_Dracula1_WhipCrack_Crit_VFX]` 8066054580580777860 (B43) | whip |
| `[VFX_PaintBall_Shoot]` (B43 -4278402775329998714) | crossbow muzzle |
| `VFX_Lava_Bubbles_Death01` (85515de1 -6368704927241128061) | enemy falls in lava (Golden Trophy act 8) |
| `VFX_Ghost_CaptureSuccess01/02`, `VFX_Yeti_Spawn` (6d3223da) | ghost/boss effects |

### 2.9 Level-building kit usable for quest rooms (B43 keyed Maker props; sizes from mesh AABBs)

| Prefab (key, pathId) | Mesh (size m) | Material | Quest use |
|---|---|---|---|
| `(Sandbox_VerticalWall)` bcd, -6104563012412893582 | `WallPiece_Vertical_LOD0` (1.09 x 2.0 x 0.33) | `Wall_Stone_Section_Theater_mat` (stone) | castle walls (GT, CC, CBM) |
| `(Sandbox_HorizontalWall)` f5f, -5649714375427427788 | `WallPiece_Horizontal_LOD0` (2.95 x 1.47 x 0.33) | stone | castle walls, battlements |
| `(Sandbox_StonePillar)` 93e, 5584839958047387614 | `Quest_StonePillar` (2.31 x 2.57 x 2.31) | `Stones_Modular_Theater_Mat` | pillars, towers (stack), GT/CC/CBM |
| `(Sandbox_Campfire)` 4b8, 1989340370463002370 | `Quest_Campfire_Posts/Logs/Rocks/Sword/Ham` + flame | | goblin camps (GT act 1, CC) |
| Rocks: `(Rock_01)`, `(Rock_04)`, `(Rock_Cliff)`, `(Rock_Cone)`, `(Rock_Plateau)`, `(Rock_Stump)`, `(Rock_Tall)`, `(Rock_Wall)`, `(Rock_Wedge)` ...`Variant_Sandbox)Variant` (keys 761, 73, 1ed, 423, b25, b8fd, b8, dc7, 22e) | `RecRoyale_Rock_*` meshes with `_ColN` collision meshes | | caves/mines (CC), island & caves (Pirate), cliffs |
| Trees: `(Sandbox_FlatTreeLarge/Medium/Small)` 63e/4ae/e9d, `(PineTree_Dead_Sandbox)Variant` f7, `(Tree_Stump_Sandbox)Variant` 275, `(LogPile_Sandbox)Variant` 523, `(WoodStack_Sandbox)Variant` 038, `(Hay_Bale_Sandbox)Variant` 7c, `(Sandbox_SittingLog)` abf, `(Sandbox_LogBench)` f5a | | | forest (GT act 1), Burning Village (CBM), Castle Grounds |
| Fences: `(Fence_Sandbox)Variant` cd, `(Fence_Wood_Frame_5m_Sandbox)Variant` e1d, `(Fence_Wood_Board_Wide_Sandbox)Variant` 93d; `FenceRock_V1` mesh | | | village, graveyard |
| `(Sandbox_Barrel)` 96b, `(MetalBarrel_Sandbox)Variant` 79, `(Wood_Pallet_Sandbox)Variant` ce4a, `(Wooden_Spool_Sandbox)Variant` c1f, `(Cardboard_box_Sandbox)Variant` 11e, `(Rope_Net_Sandbox)` ce3 | | | pirate docks/ship/tavern, mines |
| `(Sandbox_Candle)` cc2, `(Curtain_Sandbox)Variant` 5f, `(Curtain_Flat_Sandbox)Variant` 2a, `(Curtain_NoTassel_Sandbox)Variant` 68, `(Sandbox_JackOLantern)` 95, `(Standee_Skeleton_Squeezebox)` 04, `(Sandbox_Mirror)` 544, `(Sandbox_Bell)` 45b | | | CBM castle interiors; bell for CC bell tower |
| Sci-fi: `(Pipes_Straight/Straight_Long/Curved/Bracket_Sandbox)Variant` 9aa/458/83/cb, `(IBeam_5M/10M/Bracket_Sandbox)Variant` 66/369/1d, `(Scaffolding_Shiny_Sandbox)` aae, `(Scaffolding_Shiny_Block_Sandbox)` af6, `(Concrete_Barrier/Tube_Sandbox)Variant` 418/25, `(Sandbox_Lockers)` d5c, `(ControlPanel_Tall/Short_Sandbox)Variant` 49/967, `(Light_Strip_Short_Sandbox)` 53e, `(Light_Chevron_Sandbox)` c83, `(StageLight_Can_Sandbox)` 58e, `(TrashBin_Sandbox)Variant` 0c (SciFi_TrashBin mesh) | | | Jumbotron facility |
| Sci-fi quest door: inside `(EndingGate_Sandbox_Invention)Variant` (key 53, -2742104978754823211) child `[Quest_SciFi_RoomExitDoor]` (4983092689322515413, `LockableDoor`) > `Quest_Door_Scifi_fbx` (mesh `door_scifi` 6.45 x 5.56 x 0.61 m + `door_L`/`door_R`, `Door_SciFi_Cardboard_mat`, animator `SciFi_Doors` with clips `Scifi_Doors_Closed/Open/Opening03_anim`) and `[Quest_Door_Active]` (animator `SciFi_DoorTrigger_Controller`: `SciFi_DoorTrigger_Locked/Unlock/Unlocked`) + `Exit Door` (`QuestRoomExitDoor` with 2 spotlights) | | | Jumbotron room exits (and any quest exit door) |
| Activity door frame: meshes `ActivityDoor_Frame` (2.05 x 2.14 m), `ActivityDoor_Emit`, `ActivityDoor_Glow`, `ActivtyDoor_Orange_mat`, controller `ActivityDoor_Controller` (B43) | | | quest lobby / room-to-room doors |
| `(Sandbox_Floor)` 3ea, `(Sandbox_Wall)` 67f, `(Sandbox_Wall_Window)` bc, `(Sandbox_Ceiling)` ce4, `(Sandbox_RoomDoorV2)` 09fb, `(Sandbox_MagicDoor)` 2a9 | | | generic shells |
| `(LavaBoulder)` (bundle 85515de1, 7122126256701559267) and `(FloorIsLavaGameVisuals)` (3143584720535506358) | `Quest_Rock04` (B1DF -5750357113360534615) + `_LOD1` | `Rocks_Frontier_Mat` (B1DF) | GT act 3 rolling boulder, act 8 lava floor |

## 3. Shared quest framework (PROPOSAL)
Everything here is design, built on the measured data above, the networking design (`somnium_networking.md` 0 and
5), the player/input design (`somnium_player.md` 0, 4.3, 5.4, 6.2, 8.3) and the conventions (`somnium_conventions.md`
0, 11, 12). Numbers not read from data are marked GUESS and collected in one tuning table (3.4) so they can be
changed in one place.

### 3.1 Modules (namespace `SomniumSpace.Worlds.SomniumRecRoom.Quest`, folder `Assets/#User/SomniumRecRoom/Quest/`)

| Class | Runs on | Job |
|---|---|---|
| `RrQuestCatalog` | all | Static definitions of the five quests: rule-circuit key, currency, checkpoint mode, music ids, enemy roster, weapon racks, act list (`RrActDef`), boss def. Tables of sections 2 and 4 in code form. |
| `RrQuestAssets` | all | Opens B43 + reach bundles (`RrReach.Bundles`), loads the rule-circuit prefab by key (makes the QuestManager, its clips and VFX resident), reads manager/loot fields with our serialized-data parser, loads the needed `(QuestAIController)*` spawners by key and extracts an **enemy visual template** (the `Preview/*_Spawner_fbx` MeshFilter+MeshRenderer, materials kept, all missing-script components and colliders dropped), loads weapons/loot/VFX by key, finds implicit clips by name (`Resources.FindObjectsOfTypeAll<AudioClip>()`). Applies the depth-priming material fix and Sprites/Default particle swap (rr_runtime_facts). Caches per session. |
| `RrQuestLevelBuilder` | all members of the instance | Builds each act from its `RrActLayout` (procedural meshes + kit placements) at the instance origin (+ act offset), sets layers (floors/walls 23, see 3.12), builds the act NavMesh with `UnityEngine.AI.NavMeshBuilder.BuildNavMeshData` + `NavMesh.AddNavMeshData` (AIModule is on the upload server's reference list: measured in `D:\Dreamport\Age Of Mythology\Scripting\logs.txt`; runtime use in the client is untested, GUESS). Registers spawn points, doors, breakables, pickups, hazards with stable indices. |
| `RrQuestInstance` | all members | Mirror of the instance object ints (phase, act, wave, score, doors, boss) and the actor blocks; drives local presentation (music, doors, HUD, puppets). |
| `RrQuestDirector` | instance authority only | The game: phase machine (3.2), wave spawning (3.3), enemy brains (3.4), applying ActorHit, act clear, pickups first-claim, score, rating, checkpoint, boss scripts. Writes ints; sends EnemyAttack/Toast/Effect events. Adopts state on migration (networking 5.4). |
| `RrEnemy` | all members | One enemy puppet: visual template instance, procedural animation (3.5), health bar (SciFi/Dracula), hit volumes (for local hit tests), SFX/VFX. On the authority it also hosts the brain (`RrEnemyBrain`) and a `NavMeshAgent` (or our steering, 3.5). |
| `RrQuestPlayer` | local player | Health, down/out, revive, invincibility, carried loot (Pirate gold / CBM unbanked silver), potions buff; writes the presence slot; applies enemy hits to itself (victim decides, 3.6). |
| `RrQuestWeapons` | all | Quest weapon behaviours on top of the shared tool/hold framework (same one the shooters/sports docs use): melee, shield, bow, crossbow, guns, throwables, grenades, wand, whip + hookshot, potions (3.7). |
| `RrQuestHud` | local | Health vignette + bar, team score (Gold/Credits/Silver), act title card, toasts ("Reinforcements!", "YOU BANKED {0} SILVER"), downed/revive prompts, rating and chest screen. World-space on VR, screen overlay on desktop. |
| `RrQuestMusic` | local | Explore/combat/boss music crossfades (1.5 s) and stingers (3.9). |

### 3.2 Quest flow (instance phase + quest ints)

```
Lobby  -> (all members ready, or "Start" plate held 3 s) -> Countdown 5 s
Countdown -> ActIntro(act 0)
ActIntro(a)  : entry door open; wait until every alive member is inside act a's entry volume, or 20 s after the first
               entered (stragglers are teleported in: Bridge teleport, conventions 9); close the entry door. 2 s title.
Combat(a)    : waves run (3.3). Breakables, pickups, hazards live. Music -> combat on first spawn.
ActClear(a)  : all waves of a done and no enemy alive -> fanfare, exit door opens, remaining pickups stay 20 s.
               Checkpoint quests: write checkpoint = a+1 (Pirate), or return to the hub and bank (Blood Moon).
               If a is the boss act -> Victory.
Victory      : win stinger, 6 s (Pirate 10 s), one chest per member shows rating + score; back to Lobby after 20 s.
Fail         : every member Down or Out at the same time -> lose stinger, 6 s (Pirate 10 s), then
               no-checkpoint quests restart at act 0 (score reset); checkpoint quests restart at the checkpoint act
               with the score/banked loot they had at that checkpoint.
```

Checkpoint modes (catalog field): Golden Trophy, Crimson Cauldron, Jumbotron = **none**; Isle of Lost Skulls = **every
act start** (GUESS granularity; the wiki only says "the last checkpoint"); Blood Moon = **hub**: levels are entered
from the Grand Hall in a fixed order (GUESS: the hub doors unlock one by one), silver banks on return.
A player who joins mid-quest enters as Out (spectator) and becomes alive at the next ActIntro (Rec Room lets friends
join between acts; GUESS).

### 3.3 Acts, waves and spawning

```
RrActDef   { name, layout, entryDoor, exitDoor, spawnGroups[], waves[], pickups[], breakables[], hazards[], music }
RrWaveDef  { entries: (archetype, count, spawnGroup)[], trigger, maxAlive, spawnInterval }
trigger    = ActStart | PrevCleared | PrevBelow(n alive) | EnterVolume(id) | After(seconds) | Scripted(id)
```
- Spawn = authority picks a free actor slot, a point of the group not visible to any player if possible
  (flyers: 2.5-4 m above the point), writes the actor (serial bumped = snap), broadcasts Effect `VFX_Generic_SmokePoof01`.
  Ground enemies rise 0.8 m out of the floor over 0.5 s; flyers drop in from 3 m (GUESS; Rec Room goblins "pop" in with
  a puff). Enemies are inactive (no attack) for the 0.6 s spawn animation.
- **Party-size scaling** (authored for 4): `count(n) = max(1, round(count4 * (0.4 + 0.15 * n)))` -> 1 player 55 %,
  2 players 70 %, 3 players 85 %, 4 players 100 % (GUESS). Health is not scaled (GUESS). Bosses: HP x (0.55 + 0.15 n).
- `maxAlive` default 8 per act (GUESS) and 30 per instance (two actor blocks).
- Target selection (every 1 s, and on being hit): nearest alive player in line of sight, sticky for 3 s; the last
  attacker gets +5 m of preference (the Goblin King and barrel knights turn to whoever hit them, per the wiki).
- Aggro: `preventAutoAggro` 0 in all configs = enemies aggro immediately on spawn.

### 3.4 Enemy archetypes: behaviour + tuning
Measured columns come from 2.2; the rest is GUESS, tuned to Rec Room feel from the wiki/video descriptions.
All damage is to a 100-HP player. Points awarded on kill = `maxHealth` (GUESS; breakables use their measured points).

| Archetype (types) | Move | Attack | Windup / active / cooldown s | Damage | Stun / special | Death |
|---|---|---|---|---|---|---|
| **Melee walker**: goblin 0, elite goblin 5, pike goblin 12, pirate swashbuckler 21, pumpkin skeleton 34 | NavMesh chase at `movementSpeed`; stop at 1.1 m (pike 1.8 m) | swing/stab in front (sphere r 0.6 at 0.9 m; pike: capsule 1.8 m) | 0.6 / 0.2 / 1.4 | 20 (elite 25, pike 25) | basic goblins: arrow/bolt hit = stunned 1.5 s (fall over), elites immune; pirate & pumpkin: attack **blocked** by a held melee weapon or shield (tool collider overlaps the attack volume in the active window) = stunned 2 s, otherwise melee damage only lands while stunned x0.5 (GUESS "block then hit" rule) | tip over + shrink 0.4 s, poof, drop |
| **Ranged walker**: cannon goblin 4, laser bot 9, elite laser bot 19, pirate flintlock 26, bellhop musket 28 | keep 6-12 m, dash sideways (`dashSpeed`, `min-maxDashDistance`) every 2-4 s | projectile at `projectileSpeed` toward target's chest with lead 50 % | 1.0 / - / 2.5 (bot: burst of 3 at 0.15 s) | cannon 20 (splash 1.5 m), laser 10 per bolt, flintlock/musket 30 | laser bots: none; flintlock skeletons: pistol spent after 2 shots then melee (GUESS) | as above; bots spark + `[Arena_Grenade_Explosion_VFX]` small |
| **Flyer**: imp 1, elite imp 7, volley 13, drone 11, elite drone 18, ranged bat 30 | hover 2.5-4 m, orbit target at 7-10 m, dash (`dashSpeed`) | imp: 1 slow fire orb (speed 6); elite imp/volley: spread of 5 orbs (wiki); drones: burst of 3 lasers (speed 21); bat: spit (speed 9) | 0.8 / - / 2.5 | orb 15, laser 10, spit 10 | imps: arrow hit = knocked down 1.5 s (fall; over lava = dies), elites immune | fall to the ground, poof |
| **Charger** (barrel knight): 2 (spin), 8 (charge) | slow chase 2.5 m/s | aim 1.2 s (red line), charge `chargeDistance` 8 m at `chargeSpeed` 12 m/s; spin variant spins 2 s with halberd (r 1.6 m) | 1.2 / charge / 3 | 40 on contact, knock-back 4 m/s | **only the back target takes damage** (procedural disc r 0.25 m on the barrel's back, 1.4 m up at 3.45 m height scale); front hits clang, no damage; a shield bash in front interrupts the aim (GUESS) | collapse, barrel rolls |
| **Exploder** (RecRoomba 10 "mouse bot") | rush at 4 m/s in packs | explode at 1.2 m | 0.4 fuse / - / - | 35 (r 2.5 m, falloff) | dies to anything (10 HP) | explosion VFX |
| **Heavy / sniper** (assassin bot 14) | slow 2 m/s, keeps 12-20 m | charge a pink laser on the target 2.0 s (LineRenderer), freeze the aim 0.4 s, fire a beam (`projectileSpeed` 100, hitscan) along the frozen line | 2.4 / 0.1 / 4 | 50 | **back battery x3 damage**, front x0.5 (GUESS; wiki: weak spot on the back) | big explosion |
| **Thrower** (pirate bottle 23, jug 22, CBM maid bone 29) | slow 1-2 m/s, keep 5-9 m | lob (ballistic, speed from config) a bottle / exploding jug (r 2 m) / bone | 0.9 / - / 2.2 | bottle 15, jug 25 splash, bone 15 | projectiles are real `(Quest_Pirate1_BeerBottle)`, `(Quest_Pirate1_RumJug)`, `(Quest_Dracula1_SkeletonBone)` visuals; the bottle/jug that misses stays on the floor as a **pickup** for players (Rec Room lets you throw them back) | |
| **Gibbet** (27) | hangs from a chain (procedural), sways; spawns as players walk past | spits/throws from the cage (speed 8) | 1.2 / - / 3 | 15 | cage absorbs 50 % damage (GUESS) | cage drops, poof |
| **Lunger** (bat 31, wolf 35) | circle at 4-6 m, then lunge (`dashSpeed` over `min-max` m) | contact during lunge | 0.5 / lunge / 2 | bat 10, wolf 25 | - | |

Health (all measured), speeds (measured), projectile speeds (measured), dash (measured), charge (measured).
Everything else in this table is one `RrQuestTuning` asset (GUESS values) so it can be tuned in play tests.

### 3.5 Enemy movement and animation
- **Pathing:** authority only. Preferred: runtime NavMesh per act (built with the act, agent radius 0.35 m, height
  1.6 m, step 0.4 m, slope 40 deg) + `NavMeshAgent` with `updatePosition` on and `speed = movementSpeed`
  (charge/dash: disable the agent and move along a checked straight line). Fallback if runtime NavMesh fails in the
  client: waypoint graph authored with each act layout (A* over <= 64 nodes) + local avoidance (separation 0.8 m).
  Flyers never use NavMesh: seek/orbit steering with sphere-cast obstacle avoidance at their hover height.
- **Replication:** actor block ints (networking 5.8) at 10 Hz: position mm, yaw, type, anim state (Idle, Move, Windup,
  Attack, Stunned, Hurt, Dying, Charging, Spawning), health %, flags. Watchers draw on `EtsNetTimeline`.
- **Procedural animation for static spawner meshes** (all except the three skinned rigs): root motion from the puppet
  position; `Move` = bob 3 cm at 2 steps/s x speed + 6 deg forward lean + side sway 2 deg; `Idle` = breathe (scale y
  1.0-1.02 at 0.5 Hz); `Windup` = lean back 12 deg + scale 1.05; `Attack` = lunge 0.25 m forward + lean 20 deg over
  0.15 s; `Hurt` = squash (1.1, 0.9, 1.1) 0.1 s + white flash (MaterialPropertyBlock pulse on
  `_EmissionColor` + `_EmissivePower`: enemy materials use `AG/Standard-Opaque-Emission`, measured properties
  `_BaseColor`, `_BaseMap`, `_EmissionColor`, `_EmissionMap`, `_EmissivePower`; that shader has no DepthNormals pass,
  so apply the renderQueue 2501 fix of rr_runtime_facts to every enemy material); `Stunned` = tilt 70 deg
  sideways, stars VFX (`VFX_Wand_Impact` tinted); `Dying` = tip over 0.4 s then poof. Flyers: bob 8 cm at 1.5 Hz + bank
  into turns. Barrel knight: waddle roll +-8 deg. Wolf: gallop pitch +-6 deg at 3 Hz. Gibbet: pendulum sway.
- **Skinned rigs** (goblin ground, pirate melee skeleton, laser bot): drive `Goblin_Idle`/`Goblin_WalkCycle` (etc.)
  with a `PlayableGraph` mixer by speed, and add attack/hurt/death as additive bone rotations in `LateUpdate`
  (e.g. goblin `Goblin_Ground_RightArm` swing -90 deg -> +30 deg over the active window; pirate
  `Skeleton_RArmUpper_Jnt`/`LowerArm` slash; bot `SciFi_Ground_Cannon` recoil). Use them for the most common enemy of
  each quest (goblin 0/5, swashbuckler 21, laser bot 9/19) and put the weapon mesh in the hand bone.
  Elite colours by material swap (2.2.2).
- Hand-held enemy weapons for static meshes are already part of the posed spawner meshes (sword, pike, cannon,
  flintlock, jug...). For the skinned goblin add `Sword3` (B1DF) to `Goblin_Ground_RightArm`, for the pirate rig
  `Cutlass_01` to `Skeleton_RArmLower_Jnt`.

### 3.6 Combat resolution and networking (extends networking 5.7)
- **Player -> enemy:** the attacker's client detects the hit (melee: tool collider vs our enemy hit volumes on layer
  27, swept between frames; projectiles: simulated locally from its own Fire), computes damage from the weapon's
  config (2.3): melee `lerp(Min, Max, clamp01((v - minSpeedImpulseSpeed) / (maxSpeedImpulseSpeed - minSpeedImpulseSpeed)))`
  with `v` = tool tip speed; bow `lerp(Min, Max, draw/0.6 m)`; guns `Min..Max` random with the shot seed; thrown
  `MeleeWeapon` x `throwDamageMultiplier` when `supportsThrowDamage`; **crit** x2 when `canApplyCriticalHits` and the
  hit is on the head/weak-spot volume (GUESS factor). Sends **ActorHit** (type 7) to the authority. The authority
  applies it (clamped, ignores dead/spawning actors), updates health %, handles stun/kill, adds points.
- **Enemy -> player (victim decides):** the authority broadcasts **EnemyAttack** (new type 14: block u8, actor u8,
  attack kind u8, target PlayerId u16, origin 3 x S16 cm, dir yaw/pitch 2 x Angle16, speed u16 cm/s, seed u8,
  windup ms u16 = 19 B). Every member plays the windup/attack/projectile identically; **each client tests only its own
  local player** (capsule from `Body.Root` to `Body.Head`, r 0.25) against the melee volume / projectile / beam /
  explosion, and applies the damage to itself (unless invincible or already down). This favours the victim, matches
  "damage/death by the victim" (networking 0.8) and needs no extra messages.
- **Blocking:** the defender's client checks, during the active window, whether its held melee tool
  (`canBlockEnemyMeleeAttacks` 1) or shield overlaps the attack volume; if so it takes no damage and sends
  **ActorBlocked** (new type 17: block, actor) to the authority, which stuns the enemy (pirate 2 s, others 0.5 s knock-back).
- **Reflection** (Crimson Witch orbs, CBM mirror shot, GUESS for Dracula): the orb is an authority-owned actor-like
  projectile in the actor block (type = orb); a player whose sword/shield/pot/potion touches it sends **Reflect**
  (new type 18: orb id, new dir) and the authority re-aims it at the boss (first claim wins).
- **Pickups** (credits, gold stacks, lost loot, silver, potions, weapons on the floor): **PickupClaim** (new type 15:
  kind u8, index u16) to the authority, first claim wins; state lives in the bitset actor block and the claimer's
  presence slot (carried loot).
- **Breakables:** the breaker's client shatters it locally and sends **BreakClaim** (new type 16: index u16); the
  authority sets the bit and adds the points; late/duplicate claims are ignored; others shatter it on the bit change.

### 3.7 Players: health, down, revive, potions
- Health 100, no regeneration (GUESS; heal with potions). Stored in presence slot int 18 bits 0-7; status Down/Out in
  int 4 (status 5 = Down; Out = 4 Spectating).
- **Down** at 0 HP: drop held weapons (they fall as pickups), play `Out` (2.0 s), movement slowed with the Bridge
  multiplier `SetMovementSpeed(0.3, 0.3, 0.3)` (restored on revive; somnium_player 6.2 rules), cannot grab or fire;
  show our own markers only (never modify the avatar): the `Downed_Player_KO_Skin` sprite billboard 0.4 m above
  `Body.Head` + a red ring decal at `Body.Root` visible to teammates. Bleed-out 30 s (GUESS) -> **Out** until the
  next ActIntro/checkpoint (walks freely, ignored by enemies, cannot interact).
- **Revive** (values from `CombatRevivePerHandScreenPlayerBehaviorConfig`, 2.5): an alive teammate within 2.5 m;
  VR: bring a hand within 0.25 m of the downed player's hand (remote `Body.LeftHand/RightHand`; desktop downed player:
  of a revive orb 1.1 m up at their `Body.Root`) and hold **grip** 1.2 s (GUESS); desktop: look at the downed player and
  hold **H** 1.5 s (GUESS; H is free per conventions 11.1); the reviver must stay within 1 m of where they started.
  Progress ring on both HUDs, `Quest_Revive` plays (7.0 s clip, fade out on completion/cancel). Completion: the
  reviver broadcasts **Revive** (new type 19: victim PlayerId); the victim's client sets health to 50 (GUESS) and
  status Playing; both get 3 s invincibility (GUESS; wiki: both are briefly invincible); play
  `RecRoyale_Revive_Revived01`. The authority counts revives (objective stat) and subtracts `deathPointsPenalty`
  from the team score for each Down (floor 0).
- **Potions** (Goblin1/2 potion models, 2.3): drink = tilt the bottle > 100 deg with its mouth within 0.2 m of the
  head (VR) or hold LMB 0.5 s (desktop); `drinksToActivate` 2 gulps 0.5 s apart. Health potion: +50 HP over 1 s (GUESS);
  double-damage potion (Crimson Cauldron): x2 damage for 30 s (GUESS), a red aura on the held weapon. A thrown potion
  shatters (`shatterSpeed` 2 m/s) and deals its 50 melee damage; it also reflects witch orbs.

### 3.8 Scoring, loot and rating
- Team score (instance int 28) in the quest's currency: kills (`maxHealth` points, GUESS), breakables (measured:
  FatPot 50, FatPot_Large 100, SciFatPot 50, candles 2-3 silver), pickups (credit chip 50, gold stack 1 per
  `smallPileLootValue`, large pile 200, silver 10), minus `deathPointsPenalty` per Down and `friendlyFirePointsPenalty`
  per friendly-fire kill (friendly fire itself: player damage values `playerDmg` from 2.3 apply to teammates only if
  the catalog enables it; GUESS off by default because Rec Room quests show "friendly fire" only as a penalty).
- Jumbotron: points only when **credits are picked up** (`usesCollectiblesForPoints` 1): kills/breakables drop
  `Scifi_Credit_Chip` pickups (value 50 each; count = points/50) that despawn after 20 s (GUESS).
- Pirate: each player carries gold (presence slot int 19); on Down it drops as `(Quest_Pirate1_LostLoot)` piles of at
  most 250 with the owner's name after `dropLostLootDelay` 6.5 s; anyone recovers it; enemies drop gold with rate
  0.15 x value 3; breakable containers 0.15 x 300; chests 3 (all measured). Final = sum carried by members alive at
  the end ("YOU ESCAPED WITH {0} GOLD").
- Blood Moon: per-player unbanked silver (int 19) -> banked into the team total when the team returns to the Grand Hall
  (`bankLootDelay` 3 s, `Arena_CashRegister01`, toast "YOU BANKED {0} SILVER"); death drops unbanked silver
  (`lootLossType` 1, GUESS meaning); at the end add the rank bonus `rankBonuses` (rank 0..3 = S..C: 10000 / 6000 / 3000 /
  2000; rank 4 = 15000, GUESS "perfect/no deaths").
- **Rating** (S/A/B/C): director tracks `maxPossible` (sum of spawned enemy points + breakables present). Rating = S >= 85 %,
  A >= 65 %, B >= 45 %, else C (GUESS thresholds). Chest screen per player at Victory; store the best rating per quest in
  Bridge LocalStorage (conventions 18) for a dorm trophy shelf (`Trophy_*` props exist in B43, GUESS use).

### 3.9 Music and stingers
Per quest: Lobby = lobby song (Crimson Cauldron: `CrimsonCauldronTheBalladofVotaVader`; Pirate: `PirateShoppingChannel`;
Blood Moon: `CBM_Gavotte`; others: explore track at -6 dB, GUESS); ActIntro/ActClear/transit = explore track;
first spawn of an act = crossfade 1.5 s to combat; ActClear = fanfare (Golden Trophy `GoldenTrophyClearFanfare2`; others
`Quest_Coins_Spawn01` + short silence, GUESS) then explore; boss act = boss track (Blood Moon `CBM_CombatBoss`, minibosses
`CBM_CombatMiniboss`; others: combat track); Victory/Fail = manager stingers (2.1). Ambience beds per act (2.7).
Music plays 2D at 0.6 volume (GUESS) on one AudioSource per local player; everything else 3D.

### 3.10 Networking layout for a quest instance (fills networking 5.6 ints 27-58 and 5.8 blocks)

| int | Content |
|---|---|
| 27 | quest id (0-3) \| act index (4-9) \| act phase (10-13: Intro, Combat, Clear, Transit, Boss, Victory, Fail) \| wave index (14-19) \| checkpoint act (20-25) \| difficulty/party size at start (26-28) |
| 28 | team score (int32) |
| 29 | max possible score so far (int32, for the rating) |
| 30 | boss HP (0-15, 0-65535) \| boss phase (16-19) \| boss flags (20-31: shield up, vulnerable, panel bits) |
| 31 | doors bitmask (entry/exit of up to 16 acts) |
| 32 | hazards/puzzle bits (lasers on, levers, boulder released, cannons loaded...) |
| 33 | scripted-event timer: remaining ms (e.g. laser cycle, cart ride), rewritten on change |
| 34 | act seed (spawn point choice, drops) |
| 35-40 | per-act counters (enemies killed in act, breakables broken, pickups taken) |
| 41-58 | quest-specific (Pirate: cannon states and angles; Blood Moon: hub doors unlocked, banked silver per roster entry 16-bit x 16; Crimson Cauldron: witch orb ids) |

Actor blocks leased per quest instance: **2 enemy blocks** (30 actors: position, yaw, type, anim state, health %,
flags) + **1 bitset block** (breakables broken / pickups taken / lost-loot piles by index; lost-loot pile positions in
ints 4-35 of that block as mm-relative pairs, GUESS layout). Presence slot per player: int 18 health/ammo/kills/deaths,
int 19 carried loot, ints 32-47 quest (held potion buff end time, revive target, weapon durability of held tools).
New event types: 14 EnemyAttack, 15 PickupClaim, 16 BreakClaim, 17 ActorBlocked, 18 Reflect, 19 Revive (the ids
continue the catalogue of networking 5.7). Budget: 30 actors at 10 Hz ~1.5-2.5 KB/s authority upload (ETS measured
704 ints/s for 60 cars = 2.8 KB/s), EnemyAttack ~20 B x ~5/s.

### 3.11 Weapons (on the shared hold/holster framework)
Load each weapon prefab by key, instantiate under an inactive parent, strip missing scripts/Rec Room behaviours but
keep MeshFilters/Renderers/LODGroup/Animator/colliders, re-layer to 10 (held props, somnium_player 0.4), then add our
behaviour from its config (2.3):

| Behaviour | Weapons | Rules |
|---|---|---|
| Melee | QuestSword, QuestMeatSword, Pirate cutlass/oar/broom/pan/stool/fish, CBM pitchfork/rake/shovel | damage by tip speed (3.6); `isDestructible` 1 -> durability (GUESS: cutlass 12 hits with 4 mesh stages `Cutlass_01..04`, others 6-8 hits); swing SFX from `onSwingAudioPool` when tip speed > 3 m/s |
| Shield | QuestShield | off-hand; blocks enemy melee/projectiles hitting its collider; bash 0-3 m/s = 1 dmg + 0.5 s stagger (GUESS) |
| Bow | Longbow + Arrow | two-hand draw (VR: second hand grips string, draw 0-0.6 m; desktop: hold LMB 0-1 s); arrow 50 m/s x draw, gravity; damage 17-67 |
| Crossbow | Crossbow, Crossbow_Hunter, CBM 1H-Crossbow, CBM Crossbow_Hunter | one hand; bolt speed 10 (Crossbow) / 40 (Hunter) m/s; reload 0.5 s automatic; 2 shots/s max |
| Gun | SciFi pistol/shotgun/automatic/railgun, flintlocks, blunderbuss | `maximumFiringRate`, `reloadDelay`; SciFi reload = grab the handle / flip (VR) or Z (desktop); shotgun 6 pellets in a 6 deg cone (GUESS); railgun charge 1 s for full damage (GUESS); flintlock 2 shots then spent (wiki) |
| Throwable | beer bottle, mug, plate, rum jug, fire potion, skeleton bone | shatter on impact >= `shatterSpeed` (2 m/s) if `shatterOnAnyImpact`; damage from `MeleeWeapon` x throw multiplier; jug explodes (r 2 m, 80 GUESS) |
| Grenade | coconut bomb, miniboss bone, SciFi_Grenade | fuse 3 s after throw (GUESS), `explosionRadius` 2, damage 80 |
| Wand | Quest_Goblin_Wand | bolt 18 m/s, `gravityRatio` 0.8, 2/s, reload 0.67 s, range 30 m, damage 20-60 |
| Whip | Whip | crack at the tip: charge time 0-7 s scales power 0.1-1 and blast radius 0.75-3 m, damage 40-90; hookshot: crack within 1 m of a hookshot point -> teleport the player to its `landingPoint` (Bridge teleport) |
| Potion | Goblin1/Goblin2 potion tools | 3.7 |

Weapon placement: start-room racks (per catalog, one set per player slot), fixed pickups inside acts (4), enemy drops
(Pirate cutlass from swashbucklers, flintlock from pistoleers; CBM limited-use weapons from breakables, GUESS).

### 3.12 Layers (conventions 12) and physics
Act floors/walls/props: 23 (Environment; walkable VR+desktop). Pickups/weapons at rest: 10; held: 10 with layer 7
excluded. Enemy hit volumes (body capsule/box from the spawner's BoxCollider size, head sphere, weak-spot disc) and
player hit capsules: 27, triggers. Projectiles: raycasts/sphere-casts per frame (no rigidbodies) masked to 23 + 27.
Enemy bodies for player blocking: a kinematic capsule on 23 so players cannot walk through them (GUESS; check that
the avatar controller (layer 2) collides with 23: yes per the matrix). Lava/deadly volumes: triggers on 15 detected
through the client's `PlayerTrigger` (somnium_player 8.3).

## 4. Per-quest level plans (PROPOSAL; layouts are GUESS reconstructions from the wiki facts in section 1)

### 4.0 Common construction rules
- **Placement:** the quest instance origin O (networking 5.3, 2 km grid). Lobby at O; act *a* is a separate room
  whose entry is at `O + (0, 0, 60 + 80 a)` (80 m pitch along +z; rooms up to 60 x 60 m fit, the Pirate island acts
  use up to 70 m). Rooms never overlap, so all acts can be built once at quest start (simple, costs ~5-20 MB) or
  lazily one act ahead (GUESS: build all; measure).
- **Transitions:** each exit door is a doorway into a 6 m corridor that ends at a **transit door**; when the team is
  in (or 20 s pass), stragglers are teleported, the act behind is hidden (renderers off) and the next act's entry
  door opens. The corridor hides the 20 m gap between rooms. Jumbotron uses the sci-fi quest door
  (`Quest_Door_Scifi_fbx` + `SciFi_Doors` animator, 2.9); medieval quests a procedural wooden double door in a
  `Sandbox_StonePillar`/`WallPiece` frame; Blood Moon hub doors = the activity door frame (`ActivityDoor_*` meshes).
- **Procedural geometry** (our `RrProcMesh`): floors, ramps, stairs, bridges, arches, crenellated walls, cave shells
  (noise-displaced inverted capsules/boxes), cliffs, lava/water planes, ship hulls (lofted splines). Materials are
  Rec Room's own, kept as loaded: stone `Wall_Stone_Section_Theater_mat` / `Stones_Modular_Theater_Mat` (B43), rock
  `Rocks_Frontier_Mat` (B1DF), wood `Wood_Floor_Matte_mat`, `Wood_Horizontal_DarkBrown_5x_Mat`, `Fence_Wood_5m_Mat`,
  `Paintball_WoodStack_Mat` (B43), metal `Metal_FloorTread_Shiny_Mat`, `Metal_Dull_Dark_Mat`, `Metal_Galvanized_Mat`,
  `Metal_LightGray_Mat` (B43), carpet `Carpet_Purple_Mat` (B43), brick `Brick_Red_Mat` (B43), liquids
  `Water_Lava_Mat`, `Water_Ocean_Mat`, `Water_Swamp_Mat`, `Water_River_Mat`, `Water_Lake_Mat` (bundle 18c123d6),
  `LavaFloor` (85515de1). The B43/B1DF materials are implicit (no keys): they become resident with any keyed prefab that uses them (e.g. the
  kit props of 2.9). The water set is **directly keyed** in bundle `18c123d6ac91200e89ad964373be8ed2`
  (`LoadAsset<Material>(key)`): Water_Lava_Mat `17`, Water_Ocean_Mat `3e`, Water_River_Mat `6`, Water_Lake_Mat `6a`,
  Water_Swamp_Mat `b`, Water_Blood_Mat `1b`, Water_Slime_Mat `0`, Water_Alien_Mat `1`, Water_Frozen_Mat `2`,
  Water_OilSpill_Mat `3`, Water_Radioactive_Mat `6f`, Water_Shoreline_Mat `8a`, BelowWaterPlaneMaterial `9`,
  DefaultWaterMaterial `dc`; shader `AG/TerrainWater` (made for Rec Room water volumes: GUESS whether it renders on
  a plain plane; fallback URP Unlit with the material's main texture).
  Planar UVs at 1 unit = 2 m (GUESS; check each texture's tiling). Apply the depth-priming fix to every material.
- **Kit pieces** from 2.9, with measured sizes: stone wall 1.09 x 2.0 x 0.33 and 2.95 x 1.47 x 0.33 m, stone pillar
  2.31 x 2.57 x 2.31 m, flat quest trees (`Quest_FlatTree2`, 2.75 x 2.98 m cardboard trees: the real Golden Trophy
  style), dead pine 27 m tall, rocks (`Rock_Cliff` 12.2 x 6.6 x 5.4, `Rock_Wall` 4.9 x 8.2 x 7.7, `Rock_Tall` 5.0 x 6.4 x
  3.6, `Rock_Plateau` 8.0 x 4.7 x 6.7, `Rock_Cone` 5.0 x 5.3 x 6.8, `Rock_Stump` 7.6 x 5.0 x 6.1, `Rock_Wedge` 6.7 x
  2.9 x 3.4, `Rock_01` 2.9 x 2.0 x 2.8), log pile 11 x 2.8 x 3.8, hay bale 1.3 x 0.8 x 0.8, 5 m wood fence (5.0 x 2.6),
  wine barrel 0.74 x 0.93, pallet 1.64 x 1.37, lockers 2.1 x 2.0 x 0.72, tall control panel 4.0 x 5.0 x 0.55, curtain
  4.9 x 6.0, skeleton standee 1.18 x 0.46 x 1.63, 5 m I-beam 5.0 x 0.65 x 0.42, Maker floor 15 x 0.4 x 15, Maker wall
  15 x 8.2 x 0.4 (all B43, keys in 2.9; sizes = 2 x mesh AABB extent, `q\kit_sizes.txt`).
- **Breakables** everywhere (FatPot 50 / FatPot_Large 100 in medieval, SciFatPot 50 in sci-fi, barrels/crates as
  Pirate containers, candles/candelabras/pumpkins in Blood Moon). ~10-20 per act.
- **Lighting:** one baked-looking setup per act: a directional light (outdoor) or 4-8 point lights with torches
  (`Torch_FlamePlane` mesh + flame particles), plus the client's ambient. No realtime shadows except the main light.

### 4.1 Quest for the Golden Trophy (9 acts; medieval, cardboard-forest look)
Lobby: castle courtyard 24 x 16 m, stone walls (`Sandbox_HorizontalWall` rows, 2 high), weapon racks: 4 `QuestSword`,
4 `QuestShield`, 4 `Longbow`; `(Golden_Bucket)` shown on a pedestal hologram as the goal (GUESS flavour); music
`GoldenTrophyExplore` at low volume. Start = all members stand on the drawbridge plate 3 s.

| Act | Room (size m) | Geometry + kit | Waves (authored for 4 players; types from 2.2) | Special |
|---|---|---|---|---|
| 1 Forest camp | 40 x 30 outdoor | grass floor (procedural, `Rocks_Frontier_Mat` tinted? GUESS: a green Maker colour), 12 `Quest_FlatTree2`, 2 `Sandbox_Campfire`, 3 `Rock_01`, `LogPile`, fences | W1: 3 goblin(0) on start; W2: 4 goblin when W1 < 2; W3: 5 goblin | 6 FatPots; teaches melee |
| 2 Ramp hall | 30 x 40, ramp 4 m high to an upper floor | stone walls 3 high, stone ramp 6 m wide, side room 6 x 6 with **4 FatPot_Large + 1 Crossbow** | W1: 2 imps(1) + 2 goblins; W2 (behind the players): 3 goblins; W3 (top of ramp): 2 imps + 3 goblins | imps introduced; arrows knock imps down |
| 3 Boulder ramps | 25 x 60, zig-zag ramps down 8 m | stone pillars, a `LavaBoulder`-style rock (`Quest_Rock04` scaled 2.2 m, Rigidbody 400 kg) on a lever at the top | W1: 4 goblins at the bottom; W2: 6 goblins + 2 imps coming up the ramp | pushing the lever (or the rock) rolls the boulder down the ramps: kills goblins it touches (authority-simulated rigidbody, pose in act ints) |
| 4 Lava bridges | 40 x 40, lava plane 1.5 m below, 3 stone bridges 3 m wide | `Water_Lava_Mat`/`LavaFloor` plane (deadly trigger: player -> Down, enemy -> dies with `VFX_Lava_Bubbles_Death01`), bridge edges with low walls | W1: 1 barrel knight(8) + 3 goblins; W2: 3 imps over the lava (die when knocked into it) + 3 goblins from 3 sides; W3: 1 barrel knight(2) + 2 goblins | barrel knights introduced (back target) |
| 5 Split path | 50 x 30, two parallel corridors 8 m wide joining at a hall 20 x 20 | stone walls, towers (`Sandbox_StonePillar` stacks 3 high) | per corridor: 1 elite goblin(5) + 2 goblins + 1 elite imp(7); hall: 2 elite goblins + 2 elite imps + 2 goblins | elites introduced (not stunned by arrows) |
| 6 Courtyard towers (GUESS act) | 45 x 45 outdoor, 4 towers 6 m high with stairs | walls with crenellations, 4 towers | W1: 4 imps + 2 goblins; W2: 2 elite imps (on towers) + 4 goblins; W3: barrel knight(8) + 2 elite goblins | archers' act: 2 `Longbow` + 1 `Crossbow` on tower tops |
| 7 Hedge maze | 30 x 30 maze (corridors 3 m, walls 2.5 m) + end room 20 x 20 | procedural hedge walls (flat tree material or stone) | maze: 1 barrel knight(2) + 4 goblins spawning ahead/behind as players pass volumes; end room: 3 elite imps + 3 elite goblins | 4 health potions in maze dead ends (GUESS) |
| 8 Half-lava hall | 40 x 30: 20 x 30 wood floor + 20 x 30 lava | `Wood_Floor_Matte_mat` floor, lava plane, back corner ledge | W1: 1 elite goblin + 4 goblins; W2: 3 elite + 2 goblins; W3: 2 elite imps + 2 imps over the lava/back corner (waves 1-3 as described by the wiki) | |
| 9 Throne room (boss) | 40 x 40, raised platform 3 m along the right wall (stairs), throne at the back | stone walls 4 high, pillars, banners (`Curtain_Flat` red), torches; platform holds 2 `Longbow` + 2 `Crossbow` | **Goblin King** + adds: every 25 % of his HP lost, spawn 2 goblins + 2 imps (+1 elite each later phase) all over the room | see boss below |

**Goblin King** (boss, GUESS stats): HP 3000 (x party scaling of 3.3), speed 2 m/s, height 6 m (barrel knight mesh x1.75,
`Heavy_Goblin_RED_Mat`) + procedural gold crown + axe (sword mesh x3). Attacks: (a) **triple fire orbs**: turns to the
player who hit him last (or nearest visible), fires 3 large orbs (r 0.4 m, speed 7 m/s, 20 dmg, splash 1.5 m) in a
10 deg fan every 4 s; (b) axe sweep when a player is within 3 m (35 dmg, windup 1 s); (c) charge 10 m at 10 m/s every
12 s (40 dmg). Only the **back target** (disc r 0.35 m at 3.5 m height on his back) takes damage; thrown swords x1.5.
A shield bash to his front stuns him 1 s (lets teammates get behind). On death: `Quest_BossCoins` + `[VFX_BossGoldCoins]`,
then the **Golden Trophy** `(Golden_Bucket)` drops on the throne as the victory prop.

### 4.2 The Curse of the Crimson Cauldron (9 acts; goblin mines -> bell tower)
Lobby: mine camp at the entrance with the quest's weapon racks (4 sword, 4 shield, 4 bow, 2 `Crossbow_Hunter`,
2 `Quest_Goblin_Wand`), 4 `Goblin2PotionConsumableTool` as double-damage potions (GUESS count), music
`CrimsonCauldronTheBalladofVotaVader`, then `CrimsonCauldronExploringtheMines` + `Quest_Goblin2_Ambiance_Mines01`
in the acts. Acts are GUESS (the web did not give an act list); they use every documented feature (puzzles, hazards,
cannon goblins, pikes, volley flyers, mortars, witches, bell tower).

| Act | Room | Geometry + kit | Waves | Special |
|---|---|---|---|---|
| 1 Mine entrance | 30 x 40 canyon | `Rock_Cliff`/`Rock_Wall` walls, wooden mine portal (procedural beams + `Wood_Horizontal_DarkBrown_5x_Mat`), `Wooden_Spool`, `Wood_Pallet`, crates | W1: 4 goblins; W2: 2 pike goblins(12) + 2 goblins | |
| 2 Rail tunnel | 12 x 70 tunnel (cave shell), rails | procedural cave (`Rocks_Frontier_Mat`), rails (I-beam pieces), hanging lanterns | 3 volume-triggered ambushes: 2 goblins + 1 cannon goblin(4) each | cannon goblins introduced (splash) |
| 3 Cavern lake | 50 x 50 cavern, `Water_Lake_Mat` lake, wooden walkways | walkways 3 m wide on posts | W1: 3 imps + 2 cannon goblins on ledges; W2: 2 elite imps (5-orb spread) + 3 goblins | falling in the lake = Down (cold water, GUESS) |
| 4 Forge | 35 x 35, lava channels, bellows | lava channels (deadly), forge (procedural), barrels (`MetalBarrel`) that explode (`[Quest_Goblin2_VFX_BarrelExplosion]`, r 3 m, 60 dmg) | W1: 4 pike goblins + 2 goblins; W2: 1 barrel knight(2) + 2 cannon goblins | **mortar** hazard: every 6 s a marked circle (r 2 m) gets a mortar shell (`Goblin2_Mortar_Incoming01` -> `Impact01`, 30 dmg) |
| 5 Lever puzzle | 30 x 30 vault with 3 gates | 3 levers (procedural, grabbable) must be pulled in the order shown by 3 torches; wrong order spawns 2 goblins | W between gates: 2 volley flyers(13) + 2 goblins | puzzle solving (wiki) |
| 6 Goblin village | 50 x 40 cavern with huts | huts (procedural + `Hay_Bale`, `LogPile`, `Sandbox_Campfire`), work song `CrimsonCauldronGoblinWorkSong` from a hut | W1: 6 goblins + 2 imps; W2: 2 elite goblins + 2 pike + 1 cannon; W3: 1 witch (reflectable orbs, see boss) as a mini-encounter | teaches orb reflection before the boss |
| 7 Chasm bridges | 40 x 60, bottomless chasm (fall = Down) | rope bridges (procedural planks + `Rope_Net`), wind | flyers only: 3 imps, 2 elite imps, 2 volley flyers | |
| 8 Tower stairs | spiral stairs, tower 16 m diameter, 30 m tall | stone walls, `Quest_StonePillar` core, windows | 4 waves on landings: 2 goblins + 1 pike + 1 elite imp each | |
| 9 Bell tower (boss) | 20 x 20 top floor + belfry, open arches | `(Sandbox_Bell)` scaled x4 in the belfry, stone arches, crimson cauldron (procedural pot + bubbling `Water_Blood_Mat`, 18c123d6) | Crimson Witch + adds | see boss |

**Crimson Witch** (GUESS model: `TricksterGhost` rig tinted crimson x2, or procedural robed figure; hovers over the
cauldron). She is invulnerable to direct hits; she throws a **purple orb** (speed 5 m/s, homing 30 deg/s, r 0.3 m,
30 dmg on a player) at a random player every 5 s. A player who hits the orb with a sword, shield, pot, potion or wand
bolt **reflects** it (3.6 Reflect) toward her; a reflected orb that reaches her = 1 hit. Phase 1: 3 hits, no adds.
Phases 2 and 3: 4 hits each, adds per phase: 2 goblins + 2 imps, then 3 goblins + 2 elite imps + 1 pike (wiki: "later
phases need 4 returns and spawn progressively more enemies"). Orbs can be volleyed back and forth: each return speeds
it up x1.15 (GUESS, Paddleball feel).

### 4.3 Isle of Lost Skulls (8 acts; tavern -> island -> ships; up to 3 players in Rec Room, we allow 4)
Lobby: pier and **merch booth** (`PirateShoppingChannel` music; the booth only shows gold totals, GUESS), then act 1.
Every act start is a checkpoint. Weapons are **found in the acts** (no racks): improvised melee (cutlass, oar, broom,
frying pan, bar stool, squeaky fish), throwables (bottles, mugs, plates, rum jugs, coconut bombs), flintlocks after the
beach, torches near cannons.

| Act | Room | Geometry + kit | Waves | Special |
|---|---|---|---|---|
| 1 Tavern brawl | 20 x 16 tavern, bar, 2 floors | wood (`Wood_Floor_Matte_mat`, `Wood_Table_Mat`), `(Sandbox_Barrel)`, stools, tables (`Sandbox_Table_*`), plates/mugs/bottles on tables as weapons; `Quest_Pirate1_CrowdAmbiance` + `Quest_Pirate1_PirateBrawl01` | W1: 3 swashbucklers(21); W2: 2 swashbucklers + 1 bottle thrower(23) | blocks then hits (3.4) |
| 2 Beach | 60 x 40 sand + ocean (`Water_Ocean_Mat`), palm trees (procedural trunks + flat fronds, GUESS) | `Rock_Cone`/`Rock_Wedge`, wrecked boat hull, crates, coconuts (`CoconutBomb` pickups) | W1: 3 swashbucklers; W2: 2 flintlock skeletons(26) + 2 swashbucklers; W3: 1 jug thrower(22) + 2 swashbucklers | flintlocks found after this act |
| 3 Port Plunder | 60 x 50 docks + shacks | pier planks, shacks (procedural + `Wood_Pallet`, `Rope_Net`, `Wooden_Spool`), lanterns | 4 volume ambushes: gibbets(27) swing in as players walk on (wiki), 2 swashbucklers + 1 pistoleer each | first gibbet appears as you walk away from Port Plunder's exit (wiki) |
| 4 Gibbet Jungle (part 1) | 50 x 60 jungle path | flat trees (tinted), `Rock_Tall`, `Tree_Stump`, river (`Water_River_Mat`, `Quest_Pirate1_Ambiance_BrookLocal01`), paddle (`Oar`) in a rope coil (wiki) | bottle + jug throwers on ledges, 3 gibbets, swashbucklers | |
| 5 Gibbet Jungle (part 2) | 50 x 50 ruins | stone ruins (`WallPiece`, pillars) | W1: 4 swashbucklers + 2 pistoleers; W2: 2 jug throwers + 3 gibbets | |
| 6 Skull caves | 40 x 70 cave (cave shell), skull rock entrance (procedural skull face) | `Quest_Pirate1_Caves_GhastlyAmbiance01`, treasure piles (`Coin_Pile_Small` x many as decor, not pickups) | ambushes from alcoves: 3 x (2 swashbucklers + 1 bottle thrower) | large gold container breakables (`breakableContainerLootValue` 300) |
| 7 Sunken Ship | 50 x 50 lagoon with a half-sunk ship (procedural hull, tilted deck 15 deg), rocks around (wiki) | crates at the start (wiki skip route onto the rocks) | W1: 3 swashbucklers + 2 pistoleers on deck; W2: 2 jug throwers + 2 gibbets from the masts | |
| 8 Ghost Beard (boss) | open sea: 2 ships side by side (ours 30 m long with 4 cannons per side, his ghost ship 40 m, 25 m apart) | procedural hulls/decks/masts/sails (`Curtain` cloth material GUESS), `Water_Ocean_Mat` sea; cannons (procedural barrel + carriage; `[Quest_Pirate1_Cannon_Fire_FX]`, trail `Quest_Pirate1_Cannon_Trail_Mat`) | skeletons board from his ship in waves (2-4 swashbucklers + pistoleers per phase) | see boss |

**Ghost Beard** (GUESS model: `TricksterGhost` x3, translucent green, procedural tricorn + beard; ghost SFX
`Tricks_Ghost*`, `GhostModeBoom`). He floats on/above his ship and is **damaged only by cannonballs** (wiki). Cannons: grab
a **torch** (brazier on deck, 3 torches), touch the fuse (0.2 m) -> 1.5 s fuse -> shell (speed 30 m/s, small arc,
explosion r 2.5 m). Cannons aim by grabbing the rear handle (yaw +-25 deg, pitch 0-15 deg, "small turning radius").
HP 6 shells (x party scaling, GUESS); every 2 hits a phase change: he summons boarders (skeletons walk a plank bridge)
and sweeps our deck with a ghostly blast (telegraphed 2 s, 25 dmg, duck/shelter behind the mast). Victory = his ship
sinks (tilt + sink 10 m over 6 s) and gold rains.

### 4.4 The Rise of Jumbotron (9 acts; space ship; names from the Steam guide)
Lobby = **dressing room**: mirror, credit monitors (show team credits), 4 `Quest_SciFi_Pistol` + 4
`Quest_SciFi_AutomaticGun` on racks. Music `JumboTronExplore` / `JumboTronCombat1`. No checkpoints. Walls/floors:
`Metal_FloorTread_Shiny_Mat`, `Metal_LightGray_Mat`, `Metal_Dull_Dark_Mat`, emissive strips (`Light_Strip_Short`,
`Light_Chevron`), `scifi_rug` decals, lockers, control panels, I-beams, pipes, scaffolding; exits = sci-fi quest door.
Breakables = `SciFatPot`; credit chips drop from kills/breakables and **must be picked up**.

| Act | Room | Geometry + kit | Waves | Special |
|---|---|---|---|---|
| 1 Armory | 30 x 30 open hall | lockers, weapon crates, 1 `AutomaticGun` on the right side (guide) | W1: 3 bat bots(11) (wiki); W2: 2 bat bots + 2 laser bots(9) | |
| 2 Cargo Bay | 40 x 30, container stacks (procedural boxes + `Cardboard_box`) | side room where mice spawn with a `Shotgun` (wiki), 1 pistol at the start, 1 automatic at the end | W1: 3 tread bots(9); W2: 6 mouse bots(10) from the side room; W3: 2 tread + 4 mice | tread and mouse bots introduced |
| 3 Engineering | 30 x 45, reactor core, catwalks | laser grids (LineRenderers + damage triggers 25 dmg/0.5 s) cycling on/off every 3 s (instance int 33 timer), `Shotgun` in front of the spawn behind the lasers, `RailGun` on a ramp | W1: 3 laser bots; W2: 2 elite drones(18) + 2 bots | lasers introduced |
| 4 Bridge | 40 x 30, captain's bridge, windows to space (skybox stars) | control panels, chairs, 1 `RailGun` + 1 `AutomaticGun` in a side room | W1: 1 **assassin bot**(14) + 2 laser bots; W2: 2 assassin bots | assassin bots: aim laser, back battery weak spot |
| 5 Flight Deck | 50 x 40, open edges over space (fall = Down: "lethal fall damage") | landing pads, 2 parked space ships (procedural) with 2 `RailGun` on one ship (guide), 1 `AutomaticGun` on a chair | W1: 4 bat bots + 2 elite drones; W2: 3 tread + 1 elite bot(19) | edges |
| 6 Space | 40 x 60 hull exterior, low gravity (Bridge `SetGravity(0.35)` GUESS), no voice (can't prevent client voice; show "no sound in space" flavour only) | hull plates, antennas; 1 `RailGun` at spawn | W1: 4 drones; W2: 2 elite drones + 2 assassin bots | low gravity |
| 7 Hangar | 50 x 40, computers, barrels (`MetalBarrel` explode), ramp | 1 pistol down the ramp, 1 `RailGun` by the barrels | W1: 4 tread + 4 mice; W2: 2 elite bots + 2 elite drones | |
| 8 Supply | 40 x 40 warehouse, shelves, cardboard boxes | 2 pistols (spawn, last room on a box), 1 automatic, 1 railgun | W1: mixed 6; W2: 1 assassin + 2 elite bots + 2 elite drones | callbacks to Golden Trophy (a goblin cardboard standee GUESS) |
| 9 Finale | 40 x 40 arena, giant screen wall 16 x 9 m with Jumbotron's face, 4 **panels** (2 x 2 m screens) around it, invincible turret below; cover wall of metal+glass right of the entrance; railings/platform with 2 pistols, 2 railguns, 2 automatics (guide) | | Jumbotron + reinforcements | see boss |

**Jumbotron** (GUESS model: screen quad with an animated face drawn by us (`RenderTexture`/emissive, face made from
simple shapes; no Jumbotron art in bundles), frame from I-beams/pipes/panels). HP = 4 panels x 400 (x party scaling).
Turret: invincible, targets one player (prefers players not in cover), fires streams of fast lasers (speed 30 m/s,
8/s, 6 dmg) aimed slightly ahead ("shoots in front of you"; ducking dodges it: aim at the head height at trigger time).
Each destroyed panel: toast + VO text "Reinforcements!" and spawn 2 tread bots + 2 bat bots (normal, then elites);
bots may drop from the ceiling near the front-left corner (wiki). 4th panel: the turret explodes, all remaining enemies
die, victory.

### 4.5 Crescendo of the Blood Moon (hub + 8 levels + Dracula; up to 3 players in Rec Room, we allow 4)
**Grand Hall** hub (checkpoint and bank): 30 x 40 castle hall, `Carpet_Purple_Mat`, stone walls, candles
(`Sandbox_Candle`), curtains, 9 level doors (activity-door frames) that unlock in order; the "Caravan of Curiosities"
shop stand (flavour). Returning here banks silver (`Arena_CashRegister01`, "YOU BANKED {0} SILVER"). Starting weapon:
**Whip** for everyone (infinite); other weapons are limited-use pickups in levels (pitchfork, rake, shovel, 1H-crossbow,
Crossbow_Hunter, flintlock, blunderbuss). Breakables: candelabras (3), wall candles (2), small candles, pumpkins (3).
Music: hub `CBM_Gavotte`, levels explore = `CBM_Gavotte` (GUESS), minibosses `CBM_CombatMiniboss`, Ballroom
`CBM_Ballroom`, Dracula `CBM_CombatBoss`.

| Level (solo-run minutes) | Room | Geometry + kit | Enemies | Special |
|---|---|---|---|---|
| Burning Village (4) | 50 x 40 night village | huts (procedural), fences, `Hay_Bale`, `Sandbox_JackOLantern`, fire particles on roofs, blood moon sky (red directional light) | pumpkin skeletons(34), wolves(35), bats(31) | pitchforks/rakes/shovels lying around |
| Carriage Ride (2) | a moving carriage on a 150 m forest road (players stand on a platform that moves at 6 m/s: move the platform and the players with it: Bridge seat-less "moving floor" is untested, GUESS: teleport-follow each frame like ETS seats) | dead pines (27 m), `Rock_*` | bats (ranged 30 + lunging 31) attacking the carriage, wolves running alongside | on-rails shooting: crossbows provided |
| Castle Grounds (5) | 60 x 50 gardens + gate | stone walls, gate, hedges, statues (skeleton standees) | maids(29) throwing bones, bellhops(28) with muskets, wolves; **miniboss wolf** (wolf x1.8, HP 800, GUESS) | hookshot points over the moat (`Quest_Dracula1_Hookshot_Sandbox`) |
| Gallery (4) | 20 x 60 long gallery | paintings (procedural frames with dark canvases), candelabras, curtains | bellhops, pumpkin skeletons, bats | many candles (silver) |
| Chamber of Knowledge (8) | library 30 x 30 x 15 m tall, bookshelves, balconies | procedural shelves + books (`Sandbox_Book_*`), ladders, hookshot points to balconies | maids, bats; **miniboss bone thrower** (maid x1.8 throwing `MiniBossSkeletonBone` grenades r 2 m) | whip traversal between balconies |
| Garden of Sorrow (6) | 50 x 50 graveyard garden | fences, gravestones (procedural), dead trees, fog | wolves, pumpkin skeletons; **miniboss bat** (bat x2.5 ranged) | |
| Inverted Ballroom (10) | 40 x 40 ballroom with an upside-down twin: players teleport between floor and "ceiling" through mirrors (hookshot points) | chandeliers (procedural), `Carpet_Purple_Mat`, curtains, mirrors (`Sandbox_Mirror`) | bellhops, maids, bats; **miniboss musket/bone melee** | `CBM_Ballroom` music |
| Cellar (4) | 30 x 40 wine cellar | `Sandbox_Barrel` racks, arches, torches | pumpkin skeletons, bats, wolves | |
| Dracula (8) | 40 x 40 throne hall with **balconies** on two sides (wiki), 4 large mirrors | stone, curtains, candelabras, throne | Dracula + adds | see boss |

**Dracula** (GUESS model: bellhop skeleton mesh x1.4 with a procedural cape and collar, `Quest_Dracula1_Skeleton_Musket_Mat`
recoloured dark; bat form = `Dracula_Bat_Spawner` x2.5). HP 2400 (x party scaling). He is invulnerable while casting;
he fires a **blood bolt** at a player (speed 8, 25 dmg); a bolt that hits one of the 4 mirrors reflects to the mirror's
set direction; players line up **mirror shots**: hit the bolt with the whip/weapon so it bounces off a mirror into him
(= vulnerable 5 s: normal weapons damage him). Every 25 % HP he turns into the giant bat (flies, lunges) and summons
2 bats + 2 skeletons; players clear adds from the balconies (wiki tip) until he lands again. Death: silver rain
(`[Quest_Dracula1_Silver_Boss_VFX]`), victory.

## 5. Open questions, GUESS list and checks to run

### 5.1 Verify in the Unity harness / client before building on it
| # | Check | How | Why |
|---|---|---|---|
| 1 | Enemy spawner visuals render with their materials in the Somnium client (depth priming) | load key `c13`, `f9b`, `e4f`; apply the renderQueue 2501 fix to `AG/Standard-Opaque-Emission` materials; screenshot | enemy materials lack a DepthNormals pass (3.5) |
| 2 | The three transformation rigs animate with their AnimatorOverrideControllers, or with our PlayableGraph | instantiate the Goblin rig, play `Goblin_WalkCycle` via `AnimationClipPlayable` | base controller parameters unknown |
| 3 | Runtime NavMesh (`NavMeshBuilder.BuildNavMeshData`) works in the uploaded client build | build a 40 x 40 test act; log bake ms and agent pathing | AIModule is on the server reference list (measured) but runtime use is unproven |
| 4 | `AG/TerrainWater` materials (keys in 4.0) render on a plain plane | load key `17` (lava) and `3e` (ocean) | lava/ocean surfaces |
| 5 | Loading a rule-circuit prefab makes the QuestManager's clips resident | load `fe6`, then find `Golden Trophy - Our Heroes Have Failed` by name | stingers are implicit B43 clips |
| 6 | `TricksterGhost` (6d3223da key `0e`) animates and can be tinted/scaled | instantiate, play `Ghost_Skeleton\|Ghost_Idle01` | boss stand-ins |
| 7 | Moving-floor carriage (CBM Carriage Ride) keeps players on it | parent-less follow: Bridge teleport each frame vs seat pattern (somnium_player 6.3) | on-rails level |
| 8 | Low gravity for the Jumbotron "Space" act | Bridge `SetGravity(0.35, 0.35)` | act 6 |

### 5.2 GUESS list (tune or confirm)
- Enemy damage, attack ranges, windups, cooldowns, stun durations, weak-spot multipliers (3.4); points per kill =
  `maxHealth`; party-size scaling formula (3.3); boss HP and attack numbers (4.1-4.5).
- Player: no health regen; revive hold 1.2 s VR / 1.5 s desktop (key H); revive to 50 HP; 3 s invincibility; 30 s
  bleed-out; downed movement x0.3 (3.7). Rating thresholds 85/65/45 % (3.8). Health potion +50, double damage x2 for
  30 s. Grenade fuse 3 s. Shotgun 6 pellets / 6 deg. Weapon durability counts.
- Act lists and layouts of Crimson Cauldron (all 9), Golden Trophy act 6, Pirate acts 1-2 and 5-7 details, the Blood
  Moon hub rules (door order, shop), Dracula's mirror mechanic details: reconstructed, not sourced.
- Checkpoint granularity in Isle of Lost Skulls (every act assumed). Pirate/CBM player caps (wiki says 1-3; we allow 4).
- Meaning of `lootLossType` (Pirate 0, Dracula 1) and `rankBonuses` rank 4 (15000).
- The chest prefabs `(LootChest)`, `(LootChest_SciFi)`, `(Quest_Pirate1_LootChest)`, `(Quest_Dracula1_LootChest)` are
  referenced by name only (`GiftBoxAddressablePrefabSpawner.giftPrefabName`) and were not found in the plain index:
  build our own chest.
- Mapping of the Goblin1/Goblin2 potion tools to "health" and "double damage" (their `powerupType` is -1 in the
  store versions).

### 5.3 Could still be mined from the bundles (not done)
- Exact `Tool.interactionSettings` pickup/holster transforms per quest weapon (in `q\wp\*.txt` and `q\wsumm.txt`;
  somnium_player 4.4 explains the fields) for our hold code.
- `Bullet` fields of every enemy-usable projectile (radius 0.02, lifetime 4 s, stretch 10 m for lasers: rr_shooters 6.1).
- AnimatorController parameters of `SciFi_Doors`, `SciFi_DoorTrigger_Controller`, `ActivityDoor_Controller`,
  `Pirate_Blunderbuss_Animator`, `Crossbow_Controller`, `Bow_Controller` (read `animator.parameters` at runtime).
- Mesh AABBs of the remaining kit meshes used in 4.x (campfire parts, curtains, candles, `Quest_Rock04`).

### 5.4 Sources
Web (summarised, no long quotes): rec-room.fandom.com wiki pages (Quests, Quest Rating, Quest for the Golden Trophy,
The Curse of the Crimson Cauldron, Isle of Lost Skulls, Isle of Lost Skulls Level Skips, The Rise of Jumbotron,
Crescendo of the Blood Moon) through search-engine extracts (direct fetch returns HTTP 402);
steamcommunity.com/sharedfiles/filedetails/?id=2008860357 and ?id=2797825064 (Jumbotron guides);
roadtovr.com/rec-room-update-adds-new-quest-curse-crimson-cauldron/ and
roadtovr.com/rec-room-get-new-quest-isle-skulls-starting-march-1st/;
rec-room-walkthrough.blogspot.com/2021/08/rec-room-crescendo-of-blood-moon-solo.html; speedrun.com rec_room runs.
Bundles: rrtool dumps under `SP\research\q\` and the index TSVs `SP\rrtool\idx\`.
