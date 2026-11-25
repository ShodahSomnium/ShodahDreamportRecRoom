# Dreamport: Rec Room - Shooters (Paintball, Laser Tag, Rec Royale, Showdown)

Status: COMPLETE (2026-09-24). Research only; nothing in the project was changed.

Conventions used below
- `MAIN` = bundle `43ea1176ca25c0333547813ec3298808` (the big "tools" bundle in
  `E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64\`, 793 prefab roots,
  640 Addressables containers). Almost everything shooter-related is in it. Other bundles are named by their 8-char
  prefix; get the full file name from `scratchpad\rrtool\idx\cabs.tsv` or `objects.tsv`.
- `key` = Addressables container key: `bundle.LoadAsset<GameObject>(key)` (rr_runtime_facts.md). Prefabs written
  `[Name]` (square brackets) are NOT containers; they become resident when a container that references them is loaded
  (find them with `Resources.FindObjectsOfTypeAll`).
- `pathId` = root GameObject pathId unless noted. Component pathIds are given as `Class#pathId`.
- `RR` = `scratchpad\rrtool\bin\out\rrtool.exe`. A patched copy that also prints byte arrays (GUIDs) as hex is
  `research\shoot\rr2b\bin\out\rrtool.exe` (same commands).
- Raw material behind this document (reuse, do not redo): `research\shoot\dumps\*.txt` (component JSON per prefab),
  `research\shoot\trees\*.txt` and `research\shoot\props\*.txt` (hierarchies), `research\shoot\out\*` (condensed tables:
  `weapons_compact.txt`, `taxonomy.tsv`, `typeids.tsv`, `cfg_ctf.txt`, `cfg_tb.txt`, `prop_sizes.tsv`,
  `proplist_keys.tsv`), `research\shoot\key_audio.tsv` (227 clips: bundle, pathId, name), `research\shoot\wiki\*`
  (fandom wikitext), `research\shoot\tex\Map_Frontier_Tex.png` / `Map_Frontier_Solo_Tex.png` (the island map).
  Scripts: `shoot\top.pl` (top-level fields of a component), `shoot\flat.pl` (flatten JSON + decode ACTk obscured
  floats), `shoot\wsum.pl` + `shoot\wcomp.pl` (weapon summary), `shoot\tax.pl` (taxonomy blob parser).
- GUESS marks anything inferred rather than read.

## 0. Summary / key facts

1. **Everything needed for the guns, rules, VO, VFX and a lot of set dressing is in plain bundles**, mostly MAIN.
   The maps themselves (River, Homestead, Hangar, Frontier Island, ...) are in the encrypted level files and are NOT
   available: every map must be rebuilt from props + procedural geometry (section 10).
2. **Weapon numbers are exact** (read from the RangedWeapon MonoBehaviours): paint pistol 30 head / 18 body, 5 shots/s,
   33 m/s with gravity; burst rifle 20/12, 12/s held; shotgun 9 pellets x 10, 15 m; scope rifle 120/60, 250 m/s,
   0.75/s; launcher 55 splash r 2 m; paint thrower 24/s stream; grenade/mine 100 splash r 6/5 m; splat hammer 100 + r 7
   m burst every 6 s. Laser guns: magazines 5/22/3/3/4/60, pull-to-reload. Tables in section 3.
3. **Paintball is one hit = out.** The paintball game configs set `MaxHealth = 1` (weapon damage 18 > 1), auto
   respawn after 5 s with 5 s spawn invincibility. CTF: 3 captures or 600 s; Team Battle: 50 hits or 600 s; 4 v 4.
4. **Laser Tag** = the "Arena" system: 400 points or 480 s (config; the wiki says 10 min), player tag 10 pts, bot 2,
   elite bot 10, friendly fire -10/-2. Bots are the sci-fi quest AI. Two maps (Hangar, Cyber Junk City), each with a
   power weapon (orb launcher / beam cannon).
5. **Rec Royale**: 16 solo / 6 squads of 3, gondola -> hang glider drop, loot coolers, bee swarm circle, 100 health +
   100 shield, revive in squads. The complete loot/rarity/damage table is a protobuf TextAsset in MAIN
   (`RecRoyaleRangedWeaponTaxonomy`) and is decoded in 3.3. The island's own map textures (with POI names) are in MAIN
   and are the best layout reference we have (10.5).
6. **"Rec Room Showdown" = the Rec Room Original "Showdown"** (Aug 2022): a Wild-West 3 v 3 cork-gun team battle
   (first to 25 hits in 4 minutes) in the town of Sarsaparilla Springs, built by Rec Room with Circuits V2 / Rec Room
   Studio. None of its content is in the bundles (only a generic cork projectile and Sarsaparilla drink props), so it
   must be built entirely by us (10.6).
7. Audio: all paintball VO exists (flag taken/dropped/returned per team, team scores, team wins, tie, mode titles,
   countdown), plus lobby radios (one per map) and map music. Section 7.
8. Team colours: paintball Blue rgba(0.22,0.56,0.84) vs Red/orange rgba(1,0.45,0.25); laser tag cyan
   rgba(0.087,0.747,0.893) vs pink rgba(1,0.118,0.42).
9. Context: the fandom Paintball page states Rec Room shut down on 2026-06-01. The local install still has the bundles.

## 1. Web research: rules, modes, maps

Sources: Rec Room fandom wiki (Paintball, Laser Tag, Rec Royale, Showdown, Weapons, Weapon Damage, Game Rules Chip,
News Archives 2016-2020/2025; wikitext saved in `research\shoot\wiki\`), Road to VR hands-on of Rec Royale, Rec Room
ship notes quoted in the news archives. Summarised in my own words.

### 1.1 Paintball (released 2016-07-20, 2-8 players, pickup room ^Paintball, VR-teleport room ^PaintballVR)
- **Modes.** *Capture the Flag*: grab the enemy flag with a free hand and bring it to your own stand; a dropped flag
  stays where it fell until someone touches it: a defender returns it home instantly, an attacker picks it up; you can
  only score while your own flag is at home; first to 3 captures or leader when time runs out. *Team Battle*
  (deathmatch, Coach announces "Team Battle!"): first team to 50 hits or leader at time-out.
- **Flow.** Lobby with scoreboard (Start button, team join, map vote), maps auto-cycle; practice mode while waiting
  (shooting gallery with targets in the 2025 lobby); pistol given at spawn; other guns lie on racks in the field.
  2020 changes: spawn invincibility 3 -> 5 s, respawn delay 3 -> 5 s, flag-carrier speed penalty removed (the carrier
  still has a longer teleport cooldown), respawn points chosen out of opponents' view, sprint cooldown after shooting.
- **Maps** (release order): **River** (original, 2016), **Homestead** (farm, Aug 2016), **Quarry** (Dec 2016; the
  scope rifle's map; later a sniper rifle and a spawn point on the top level of each base), **Clearcut** (Mar 2017;
  logging site in thick early-morning fog, limited sightlines, burst rifle introduced), **Spillway** (Jun 2017; night
  shift at a hydroelectric dam, lots of interior cover, stealthy; grenade launcher introduced), **Sunset Cinema /
  Drive-In** (Mar 2020; 50s roadside drive-in at sunset, mid and long range, paint thrower and paint mines
  introduced), **Merry Midway** (2025; carnival, splat hammer). Internal names seen in audio: River, Homestead,
  Quarry, Clearcut, Spillway, Sunset (Drive-In), "Dam" (ambience `Paintball_Dam_Crickets_...`; GUESS = Spillway).
- **Weapons per map (wiki)**: pistol everywhere; shotgun everywhere; scope rifle on Quarry/Clearcut/Spillway; burst
  rifle on Clearcut; launcher on Spillway; paint thrower + mines on Drive-In; hammer on Merry Midway; shields and
  grenades from the 2016 "Arsenal" update.
- **Mods** (mod tickets): Wormz (fast while crouched), Zero Gravity, Sniper Only (Hammers Only on Midway), 2025:
  Shotguns Only, Hammers Only, Fiesta.
- Damage per the wiki "Weapon Damage" table equals the bundle values (section 3), which confirms BodyPart 0 = head.
- Full-body avatars: Rec Room turned off hit collisions on arms, legs and feet (2025 notes), i.e. only head and torso
  count.

### 1.2 Laser Tag (released 2017-11-18, 2-8 players, 4 v 4)
- Team deathmatch with laser guns (the Jumbotron quest guns) and **bots** fighting on both sides. First team to
  400 points, or most points at 10 min (bundle default 480 s). Tag enemy player or elite bot = 10, enemy bot = 2.
- Guns must be reloaded by pulling a part of the gun toward you with the other hand (pistol: or flip it). All but
  shotgun and power weapon deal extra head damage.
- Bots come in waves of 4 basic robots, or 1 basic + 2-3 elites, walk toward the enemy base, fight nearby enemies and
  anyone who shot them; they kill an unaware player in about 2 hits. Types: tread bot, bat (flying) bot, elite of each.
- **Maps**: **Hangar** (like a real laser-tag arena inside a hangar, near-symmetrical, glowing red and blue halves,
  bleachers, power weapon = Laser Orb Launcher / "Laser Launcher") and **Cyber Junk City** (2018; cyberpunk junk town,
  asymmetric, three levels, verticality and tight corridors, "Pulse Cannon"/beam minigun power weapon, orange-side
  tower with enterable windows). Cut modes: "Mainframe Assault" (the `Arena_EMP` bomb device in MAIN is from it,
  GUESS) and "Fuel Run".
- Tickets: 25 per game (+100 bonus for the first five per day), spent in the Laser Tag merch booth.

### 1.3 Rec Royale (solos 2018-06-07, squads 2018-07-27)
- 14-16 solo players or 6 squads of 3 on **Frontier Island**, a national park (summer camps, mountains, forests,
  outposts, ravines, lakes). Rooms ^RecRoyaleSolos, ^RecRoyaleSquads, ^RecRoyaleVR.
- **Flow**: lobby ("Visitor Center"), START on the scoreboard once enough players; everyone spawns in a **gondola**
  (cable car) crossing the island; after ~7 s the doors open, you step out and a **hang glider** handle appears: grab
  it, steer left/right, push to dive, pull to slow; teleport/drop when close to the ground.
- **Loot**: coolers/boxes all over the island, opened by holding a free hand on the handle for ~2 s; contain weapons,
  ammo packs, health (green) and shield (blue) potions (drink by tipping over your head). Two hip holsters, a backpack
  and a map pulled from over the shoulder (shows you, teammates, current zone in yellow, next zone in white).
- **Damage**: shield absorbs first, then health; at 0 health you are down (squads: a teammate revives you by holding
  your hand ~5 s) or out.
- **The Swarm** (bees): outside the safe zone you lose health fast regardless of shield. Alternating phases "The Swarm
  Is Preparing..." (next circle shown) and "The Swarm Is Approaching!" (zone shrinks).
- **Rarities** grey/green/blue/purple/orange with named legendaries (exact table in 3.3). Winning squad is teleported
  to a winner door area with a sign; points/XP/acorns feed the Frontier Pass.
- Talking: proximity voice; squad radio across the map.

### 1.4 Rec Room Showdown ("Showdown", Rec Room Original, 2022-08-11 RR+ early access, public 2022-08-18)
- Wild-West team battle, 3 v 3 (2-6 players): Deputies (Sheriff team) vs Bandits in **Sarsaparilla Springs**. Win =
  first to 25 hits within 4 minutes (else most hits). Announcer "Archie" instead of Coach. Built by Rec Room with
  Circuits V2 / RR Studio (so nothing of it is in our bundles). Locked behind Rec Room Plus at first.
- **Weapons** (auto-reload): *Cork Spinner* (revolver, 5 corks, near-hitscan unlimited range, everyone spawns with
  it), *Cork Blaster* (shotgun, 2 shots of 6 corks, short range; 2022 patch: spread +42 %, range -40 %), *Cork Repeater*
  (carbine, 3-round bursts, high recoil). Blasters/Repeaters lie at fixed spots.
- **Map areas**: The Bank (Sheriff spawn; open vault into The Mines, upper floor + balcony with a Repeater, bridge to
  the General Store), The Jailhouse (Bandit spawn; two cells, mine entrance), The Theater (lobby: team switch, start,
  4 Cork Spinners, small shooting range), Archibald Mining Co. (mine cart, hay bale with a Blaster), The Station (train
  stop, Blaster on a table, climbable roof that bridges to other roofs), The Bar (2 floors, Repeater), Sheriff's
  Office and General Store (closed, roofs reachable), The Mines (underground tunnels linking Bank and Jailhouse,
  Blaster inside, echoing ambience). **The Train** ("Rec Room Limited") arrives periodically and stands ~30 s at the
  station as temporary cover. Spawn invincibility ~2.8 s after the Aug-16 patch then -1 s.

## 2. Bundle map: where things live

### 2.1 Shooter prefabs in MAIN (container key, root pathId, SpawnableTool typeId GUID)

The typeId (hex of `SpawnableTool.typeId`, read with rr2b) is also the id used by the Rec Royale taxonomy (3.3) and
by game configs (`MainHandToolId`/`OffHandToolId`).

| prefab root | key | root pathId | typeId | what |
| --- | --- | --- | --- | --- |
| (PaintballGun) | 142 | -5634856804522456178 | A9933C5B8B9F1C4FA70396ABF274D72A | paint pistol |
| (PaintballAssaultRifle) | 09 | -6095007029195800782 | 529A8615E80E254D9707FDEC54A4F553 | paint burst rifle |
| (PaintballShotgun) | 582 | -3288968452276605465 | 118737BD6548684289A1B1CDEF04B730 | paint shotgun |
| (PaintballRifleScoped) | 294 | -2974785353309088618 | 28DA022171E34843976CBBB315C08182 | paint scope (sniper) rifle |
| (PaintballGrenadeLauncher) | 1eb | 6352070590928766185 | B9DF25B20AB27D498654A4BCB8AF9ACD | paint launcher |
| (Paintball_PaintThrower) | 87 | -8003002396401450981 | 233358777995C744B0193B0CAE3F3CB9 | paint thrower ("Rain Maker") |
| (Paintball_SplatHammer) | 51d | 6323783143975840158 | 3D0B933E7C4A214ABDC21976BC3FEC68 | splat hammer (melee) |
| (PaintballShield) | c5c | 1972340938374654085 | 86A3E9791AC6E9479D7B4A6A0979C765 | paint shield |
| (Grenade) | ce | -7959383737723459754 | 16C1C703EDF87840A46E3E5F1CFFC1F8 | paint grenade |
| (ProximityMine) | 980 | -2232168636428236173 | ACBCC337F72FE44CBF0E68FAC8C01A48 | paint mine |
| (PaintballGun)Confetti | 4fe | -4860720375845936648 | 3D527F5F65FD8E4388AC75EC8B5194A9 | party variant of the pistol |
| (FlagTool) | 89c | -3300879768199749876 | F3AD22A288E25C49AB78F92F5775EAA6 | CTF flag (carryable) |
| (FlagGoal) | 09f | -2070574637519426978 | FE9AFF4F898B6249876D6B7B1EDD4E21 | CTF flag stand / capture goal |
| (FlagAndGoal) | 89 | -7426305404840789377 | 1580F2D0E0491242B25873B217E9507E | Maker-pen set: flag + stand |
| (PaintballCaptureTheFlag_GameRuleWrapperCircuit)Variant | 7b | -1251208605462629746 | 68271290AE3CD347AB0A7B84B87772A6 | CTF rules chip -> [PaintballManager] + CTF config |
| (PaintballTeamBattle_GameRuleWrapperCircuit)Variant | b2 | -5638456619779314289 | B3280673703A124DA12634516314D2B0 | Team Battle rules chip |
| (LaserTag_GameRuleWrapperCircuit)Variant | cdc | 4993441790827724319 | E219EFAFCCAF854CA1401AD26350CC71 | Laser Tag rules chip -> [ArenaManager] |
| (RecRoyaleSolos_GameRuleWrapperCircuit)Variant | f | -7305107189367856736 | 983DB81A68927F419EDB0DAF1243E93E | Rec Royale solos -> [BattleRoyaleManager] |
| (RecRoyaleSquads_GameRuleWrapperCircuit)Variant | f6b | -132759031112414416 | 857F750016CA77408AE9F72F9877748A | Rec Royale squads |
| (Arena_Pistol) | 61b | 7820467338851362683 | E1146BDEB10C194A913DB4CA61B30798 | laser pistol |
| (Arena_AutomaticGun) | d2f | 6157498524630832894 | 4831FFA643A0704BB2D605A085546CEF | laser burst/auto gun |
| (Arena_Shotgun) | 7f | 7866521719765982762 | A20771D23522D34AA44E0E7A1BC567EB | laser shotgun |
| (Arena_RailGun) | a3 | 8630163416721235512 | 97D000E7923057429E7CC3A14605CCC4 | laser railgun |
| (Arena_RocketLauncher) | 44 | 8262130568968328032 | 4F5FC1602B1A8D40938C233CF686CC5C | laser orb launcher (Hangar power weapon; RR legendary "Laser Cannon") |
| (Arena_PowerWeapon_BeamGun) | 555 | 703114869300882648 | 4D174EC0CC6B6244922275D709D4273B | laser minigun / "Beam Cannon" (Cyber Junk City power weapon) |
| (Arena_Grenade) | 7ed9 | 8282434837044359620 | DFDD7C3E1E6A0D4E95F576BA3A5CE36A | laser tag grenade |
| (Arena_EMP) | 0b2 | 4433222969481056413 | 901E835FF26A5245BB3F22ED2C6E4845 | EMP device (arm 6 s, disarm 6 s, detonate after 40 s) of the cut mode |
| (Quest_SciFi_Pistol) | 447 | 7169440132558149863 | 5ED06149E075734CB439E74E525A9CC7 | laser pistol variant used by Rec Royale |
| (Quest_SciFi_AutomaticGun) | 0bd | 7207755909999432204 | 179E2299D31F1241AB6EDA50D6CAE6EE | laser burst gun (Rec Royale) |
| (Quest_SciFi_Shotgun) | 40 | -4911881099049790927 | F34EF461B0581B4582A48B8701A2520E | laser shotgun (Rec Royale) |
| (Quest_SciFi_RailGun) | 2a6 | -4887995713686368716 | A25B61D5F540C14DB624461897586F2F | laser railgun (Rec Royale) |
| (SciFi_Grenade) | a3a | -3006756886563974688 | 15FC486BC870554ABAD35818858FAC0B | sci-fi quest grenade |
| (RecRoyale_Grenade) | 4bd | -4553787212381468420 | 0F07CDC222F7E44C94A8D595207B54A8 | Rec Royale grenade |
| (RecRoyale_HealthPotion) | a9 | -9033987142647402370 | 7C2DBCFF0BFC3845ADCDB21CC047AFA5 | health potion |
| (RecRoyale_ShieldPotion) | 77 | -7617272970989539265 | 2069E548AF95F84195BE60CD5F64B0EA | shield potion |
| (RecRoyale_HealthAndShieldPotion) | 0f8 | 1020623890299595056 | 23100A630025B243AE4BB8F6E2D6F449 | health+shield potion |
| (RecRoyale_Pistol_Ammo_LootTool) | 19 | 391177611682863577 | A7BC64C09B112D4D9A0353C5D5F1A82A | ammo pack |
| (RecRoyale_Automatic_Ammo_LootTool) | fc6 | -5371492159439804755 | A79932185F9982438DD9602CF75672E8 | ammo pack |
| (RecRoyale_Shotgun_Ammo_LootTool) | 71 | -6245989375367703608 | 41E51E5756475748A03E4BB4E1B38596 | ammo pack |
| (RecRoyale_Rifle_Ammo_LootTool) | af3 | -922719951405352386 | E89CC04694E67D458E8EED28B7781231 | ammo pack |
| (RecRoyale_Explosive_Ammo_LootTool) | 22a | 4984029777033618397 | 1135C8C2C8754B4183B591FEA6ED80A4 | launcher ammo pack |
| (RecRoyale_Backpack) | 54 | -7031550216477737179 | 1D36A053B17DB44FB3F75A9BD69479D2 | gear bag (inventory) |
| (RecRoyale_PlayerMap) | 8ac | 5974861889833090474 | E06106748A1A2B4C9362B8544BA65B11 | fold-out island map |
| (Crossbow) / (Crossbow_Hunter) / (Longbow) | 417 / dd8 / 2a5 | -794468755873281445 / -3416171104567349203 / -3472331185247861377 | 8F753F91... / 84C358B1... / 13B859DE... | quest bows, also Rec Royale loot |
| (Quest_Pirate1_Flintlock) / (Quest_Dracula1_BlunderBuss) | 328 / 7d | 3869957709208423819 / 1527500553090847035 | CD005FBA... / A26077FC... | pirate/dracula guns, also Rec Royale loot |
| (Quest_Goblin_Wand) | c7 | 6501105180001912045 | DBD71B96E6F6494AA1668FB2762A17F3 | "Witch Slayer" in the taxonomy |
| (Pushgun) | 0 | 6639917086697858087 | 04DA2775AA9C7A41B13C4F49B4A4B58F | knock-back gun (no damage) |
| (Sandbox_Projectile_Launcher) | 2c | -9216747924238023974 | A1BB86EE9E9A03408029962FE834F1D3 | Maker-pen launcher (cork/paint/laser projectiles) |
| (ScoreboardProjector) | c3e | 2378492940477835731 | - | scoreboard projector (shows [GameScoreboardView]) |
| (ScoreboardSetupNode) | 03 | -428085946074952633 | - | circuits scoreboard node |
| (SpawnPointV2) | af | 7184232670940489959 | - | player spawn point gadget |
| (PlayerRespawnPointVisual) | 2b7 | 9074901807244140417 | - | respawn point visual |
| (RespawnNodeVisualization) | 4239 | -4884881208705464733 | - | respawn node visual |
| (ObjectRespawnPoint) | 13a | 1765471340472337819 | - | item respawner gadget |
| (Radio) / (AmbienceRadio) | f3 / 886 | -7125182807003965041 / 2043062669133516778 | - | radio props (lobby radios, 7.4) |
| (Paintball_Target_Sandbox)Variant | 197 | 186608942821500941 | D8D9F330D98CF448A5BBCFE61D5053A9 | practice target (mesh `Paintball_Target_fbx` in 30d07e20) |
| (Terrain_Generator) | d6f | -7989818527497434194 | - | Maker-pen terrain gadget (logic is a missing script; not useful) |

Non-container managers/VFX referenced by the above (resident after loading the container): `[PaintballManager]`
(-3410785895361013), `[ArenaManager]` (490217366522465100), `[BattleRoyaleManager]` (1375039659566223917),
`[PlayerCombatUI]` (2399671029511905144), `[Arena_PlayerCombatUI_3D]` (-7878617144436674605),
`[Arena_EnemyCombatUI_3D]` (-488507276004034338), `[RecRoyale_PlayerCombatUI_3D]` (-4043049519238611755),
`[GameScoreboardView]` (7808896619519225561), `[RecRoyale_WinnerDoor]` (-8096921586749517299),
`[RecRoyale_Frontier_WinnerSign]` (451221190100605886), `[Arena_Points_WorldSpaceNotification_Team1]`
(7500639247250683869) / `_Team2] Variant` (983594565515664114), bullets/VFX (section 6).

### 2.2 Other bundles
| bundle | content |
| --- | --- |
| `1df8e0ef` | weapon meshes/materials (PaintBall_Pistol_LOD0..3, PaintBall_Sniper_Render_LOD0, PaintBall_Launcher_LOD0, Gun_PaintThrower_LOD0, Grenade_LOD0, Shield_LOD0, PaintCanister_LOD0 = hammer, `Weapon_Paintball_SplatHammer_Skinned_fbx`, `Paintball_Launcher_fbx`), `(PaintballImpactDecal)` 3842073249555665047, `(Arena_Grenade_Explosion_VFX)`, `(VFX_GrenadeArmed)`, scoreboard rows `(GameScoreboardTeamRow)` -7116564267200832181, `(GameScoreboardPlayerRow)` 8098159630668516334, `(GameScoreboardPlayerStatColumn)` 8329486366838795837, `RecRoyale_Frontier_Bear_Standee_fbx`, `RecRoyale_Backpack_fbx`, `RecRoyale_Frontier_Healthbar_fbx`, sprite `Downed_Player_KO_Skin`, many paint clips (paintballPistol_pickUp_03, paintballShield_Pickup, Paintball_Grenade*) |
| `16079ee0` | Rec Royale UI: `[RecRoyaleUIPrefab]` -7587348999278496523, `(RecRoyaleInfoScreen)` -1733604550905538083, `[RecRoyale_Frontier_XP_Boost]` -6257192381604160439, 10 AnimationClips, 4 AudioClips |
| `5c91ea11` | weapon skin textures/materials (Paintball_Pistol_Skin_Orange_Mat, Destiny2_Paintball_Sniper_Skin_Adored_Mat, ...) |
| `4056f2fc` | `(Paintballspraycan)` -1640326311322228453 (2025 sprays) |
| `85515de1` | 465 containers incl. game-flow VO (Eenie/Meenie/Minie/Moe team pick, Join, Leave, Ready, Start, Play, Over, Safe, Got You, Result, Result Thump, The Winner Is, Pre Game Loop v2), `(RROModification_Paintball_Ticket)` 5024075317182616750, Sarsaparilla bottle/crate meshes (`Consumable_Sarsaparilla_*`, Showdown dressing), `[Bubbly Cork Projectile]` 3491613538365229916 |
| `ef9b849f` | `(StoreCategorized)_PaintballStore` 5433951995046038661 (lobby merch store) |
| `30d07e20` | `Paintball_Target_fbx` 8795686036833311317 |
| one-clip bundles | team results, radios and music (section 7) - each has its own container key |

## 3. Weapons (prefabs + configs)

How to read the tables (all fields belong to Rec Room's RangedWeapon base class; values exact):
- `playerDamageSettings[{BodyPart,Damage}]`: damage to players per body part. **BodyPart 0 = head** (confirmed by the
  wiki damage table). Others 1,2,3,5,6,7,-1 carry the body value (GUESS: 1 torso, 2-3 arms, 5-6 legs, 7 hands, -1
  fallback). Only head and torso need hit volumes (Rec Room itself disabled limb hits).
- `enemyDamageSettings{Min,Max}` = damage to AI bots.
- **Health**: Paintball configs `MaxHealth = 1`, so any hit is an out. Laser Tag and Rec Royale use 100 (wiki,
  combat-UI health bars; Rec Royale also 100 shield). GUESS for laser tag: 100.
- `maximumFiringRate` = shots per second (semi-auto cap); `maximumAutomaticFiringRate` = rate while the trigger is held.
- `bulletFireSpeed` in m/s. Paint bullets have gravity, laser bullets do not (6.1).
- `supportsAmmunition=0` = infinite ammo with cooldown only (all paint guns). Laser guns have magazines refilled by
  pulling the reload handle with the other hand (`WeaponReloader`, 8 "ticks", tick sound `Quest_Longbow_Creak`) or by
  flipping the pistol over (`RangedWeaponFlipReload` minFlipAngle 270).
- `reloadDelay` = s after a reload/pump before firing again. `sightAccuracyDistance` = aim convergence distance (m).
- Component pathIds: PaintballGun `Pistol#-4627435085723858034`, AssaultRifle `BurstFireGun#-8334433749841313998`,
  Shotgun `Shotgun#-1800719549910405657`, RifleScoped `Pistol#-5593046600072686442` + `SniperScope#7532192725397214358`,
  GrenadeLauncher `GrenadeLauncher#-1215716846674000663`, PaintThrower `PaintThrower#-7382890192947363813`,
  Grenade `Grenade#4577259056459671382`, ProximityMine `ProximityMine#6972563291962362483`,
  Shield `MeleeWeapon#-3707463686117054331`, SplatHammer `MeleeWeapon#1986807416898152862` +
  `ExplosiveWeaponAddon#2605919559945972126` + `ModifyWalkSpeedOnHeld#6948588176006343070`. Dump any of them with
  `RR dump 43ea1176ca25c0333547813ec3298808 <pathId> 7`.

### 3.1 Paintball weapons (damageType 2000-2004 / 3000, infinite ammo, `defaultProjectileColor` green = no team)

| weapon | class | head / body | AI dmg | speed m/s | fire rate /s | other | bullet | muzzle / impact |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PaintballGun (pistol) | Pistol | 30 / 18 | 18 | 33 | 5 (0.2 s) | reloadDelay 0.5, impact force 3.5 | [Paintball] | [VFX_PaintBall_Shoot]; decal (PaintballImpactDecal) scale 0.8-1.2 |
| PaintballAssaultRifle (burst) | BurstFireGun | 20 / 12 | 15 | 45 | 5 single, 12 while held | burstMode 0, totalBurstAngle 5 deg (each shot of a burst kicks up; wiki: bursts of up to 5), simpleBurstModeBurstSize 16, maxDistance 50 | [Paintball] | same |
| PaintballShotgun | Shotgun | 10 / 10 per pellet (fallback part 8) | 30 | 45 | 1 | 9 pellets (pattern below), maxDistance 15, decal 0.8-1.5 with random offset 0.2, pump effect | [ShotgunPellet] | [Paintball_ShotgunBurst01] |
| PaintballRifleScoped (sniper) | Pistol + SniperScope | 120 / 60 | 70 | 250 | 0.75 | reloadDelay 0.6 (bolt), scope = render-texture camera fov 7 deg, 256 px | [SniperRound] | [VFX_RifleImpact] |
| PaintballGrenadeLauncher | GrenadeLauncher | 55 splash (all parts) | 0-100 | 18 | 0.75 | explodes on impact (delay 0), radius 2, force 30, damageFalloff 1.0 to 0.666 r then down to 0.2 at r | [ProjectileGrenade] | [Paintball_GrenadeLauncher_Explosion_VFX] |
| Paintball_PaintThrower | PaintThrower + WeaponStabilizationGrip (two-handed) | 6 / 5 | 8 | 32 | 24 auto | spreadAngleRange 2.5 deg, maxProjectileLifetime 4 s, stream particle system + start/loop/stop sounds | [PaintThrowerBullet] (LiquidBullet, radius 0.09, airDrag 0.585) | [VFX_PaintThrower_MuzzleFlash] / [Paintball_PaintThrower_Splash], decal [PaintThrowerImpactDecal] |
| Grenade (paint) | Grenade | 100 splash | 300 | thrown | - | radius 6, force 30, fuse `explosionTimeout` 3 s once armed (trigger held), falloff 1.0 to 0.71 r then 0.2; configs allow grenades to hurt teammates | - | (Arena_Grenade_Explosion_VFX)@1df8e0ef, armed (VFX_GrenadeArmed)@1df8e0ef, colour rgba(0.29,0.90,0.17) |
| ProximityMine (paint mine) | ProximityMine + SimpleSticky | 100 splash | 300 | thrown, sticks to surfaces | - | radius 5, trigger volume 6 x 3 x 6 m (centre y +1.5), expires after 90 s, notices "{player} tripped your Mine!" / "You tripped a Mine!" | - | [ProximityMine_Explosion_VFX] |
| PaintballShield | MeleeWeapon (damageType 1001, no player damage) | blocks shots (`canBlockWeaponAttacks=1`); you cannot fire a gun that intersects a shield | 1 | - | - | small hand shield | - | hit sound paintballShield_hit_02 |
| Paintball_SplatHammer | MeleeWeapon + ExplosiveWeaponAddon + ModifyWalkSpeedOnHeld | 100 (any part) | 67-100 | swing | - | burst on hit: radius 7, force 30, damage 100, only closest, re-arms after 6 s ("inflate"), walk speed x1.4, teleport cooldown x0.6 while held | - | (Paintball_SplatHammer_Paint_Explosion_VFX) 6750918707587189322, decal [PaintballImpactDecal_Large] |

Shotgun pellet pattern (exact): `Barrel` at local (0,-0.01,0.556); child `SprayOffset` at (0,0,3); 9 targets at
SprayOffset-local (0,0,0), (+-1,0,0), (0,+-1,0), (+-0.53,+-0.53,0): centre + ring of 8 at 1 m (cardinals) / 0.75 m
(diagonals) at 3 m distance = outer half-angle 18.4 deg, diagonal 14 deg.

Meshes: pistol `PaintBall_Pistol_LOD0..3@1df8e0ef`, shotgun `PaintBall_Shotgun_LOD0..3` (MAIN -5253766911797613981...),
sniper `PaintBall_Sniper_Render_LOD0@1df8e0ef`, launcher `PaintBall_Launcher_LOD0@1df8e0ef`, thrower
`Gun_PaintThrower_LOD0@1df8e0ef`, grenade `Grenade_LOD0@1df8e0ef`, mine `Mine_Body_LOD0` (MAIN 8788451765726923384,
0.27 x 0.11 x 0.27 m), shield `Shield_LOD0@1df8e0ef`, hammer `PaintCanister_LOD0@1df8e0ef`. Pistol hierarchy: root
(layer 10) -> `Pivot/Colliders/{Body,Handle,MagTip}` BoxColliders, `Barrel` at (0,-0.01,0.258) (muzzle),
`PaintBallPistol_fbx` (Animator + LODGroup), `WorldOverlapCollider`, `[PulsingBeam]` help glow (inactive), hand poses
`Left/RightHandVisualPickupTransform` at (+-0.016,-0.122,-0.078). `Tool.interactionSettings.RightHandPickupTransform`
pos (0,0.0723,0.012), rot x 20 deg.

Team colour: every weapon has a `TeamColorRenderer` (shader property `_BaseColor`, per renderer + material index);
gun accents and paint take the holder's team colour. Material colours in MAIN: `Flag_Blue_Mat` 8711886033758793829
rgba(0.247,0.792,1), `Paint_Blue_Mat` -4120628079863786121 rgba(0.218,0.558,0.840), `Paint_RedOrange_Mat`
2688498985786965320 rgba(1,0.447,0.247), `Paint_Orange_Mat` 9080505886983148850 rgba(0.953,0.477,0.140),
`Paint_GoldenOrange_Mat` 7138409788936337575 rgba(1,0.591,0.156); flag stand material `Flag_Goal_Team_Colored`
-6463833506314514310. Use Blue rgba(0.22,0.56,0.84) / Red rgba(1,0.45,0.25) for paintball.

### 3.2 Laser weapons (Laser Tag = "Arena"; same guns as the Jumbotron quest)

All: `supportsAmmunition=1`, reload by pulling the handle, infinite reserve unless noted, no bullet gravity, impact
decal `[Quest_SciFi_ImpactDecal]` -3727660404194517299, projectile colour rgba(0,0.667,1) (team colour overrides).

| weapon (key) | class | head / body | AI | mag | total ammo | speed | rate /s | reloadDelay | other | bullet / VFX |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Arena_Pistol (61b) | Pistol + RangedWeaponFlipReload | 30 / 18 | 18 | 5 | inf | 80 | 6 | 0.5 | flip to reload | [Quest_SciFi_PistolLaser]; [VFX_Laser_MuzzelFlash], [VFX_Laser_WeaponImpact] |
| Arena_AutomaticGun (d2f) | AutomaticGun (+ grip) | 6 / 5 | 8 | 22 | 110 | 80 | 15 auto | 0.5 | spread 0 | [Quest_SciFi_AutomaticLaser]; [VFX_Laser_WeaponImpact_Small] |
| Arena_Shotgun (7f) | Shotgun (+ grip) | 5 / 5 (fallback 4) per pellet | 16 | 3 | 9 | 60 | 1.65 | 0.5 | maxDistance 40; spreadTargets have Start+End pairs (8 pellets, GUESS) | [Quest_SciFi_ShotgunLaser]; [VFX_Laser_MuzzelFlash_ShotGun] |
| Arena_RailGun (a3) | Pistol (+ grip) | 60 / 35 | 70 | 3 | 9 | 160 | 1.65 | 0.6 | | [Quest_SciFi_RailGunProjectile]; [VFX_Laser_MuzzelFlash_RailGun], [VFX_Laser_WeaponImpact_RailGun] |
| Arena_RocketLauncher (44) orb launcher | GrenadeLauncher + FlipOpenWeaponSights | 100 splash | 0-250 | 4 | 4 (no reserve) | 28, no gravity | 0.75 | 0.33 | radius 5, force 90, explosionDelay 0.725 | [Arena_RocketLauncher_Projectile] (radius 0.16); [Arena_RocketLauncherExplosion]; mesh `LaserTag_Launcher` -3315125188324178763 |
| Arena_PowerWeapon_BeamGun (555) beam cannon | BeamGun (+ grip) | 15 / 15 | 50 | 60 | 60 | 1000 | 10 auto | 0.6 | spin-up (Arena_BeamGun_SpinUp_02, FiringLoop_01, FireFinished_01) | [Quest_SciFi_StretchedBeamProjectile] (range 50 m); [Arena_Cyber_Beam_*_VFX] |
| Arena_Grenade (7ed9) | Grenade | 100 splash | 0-100 | 1 | 1 | thrown | - | - | radius 6, fuse 3 s | (Arena_Grenade_Explosion_VFX)@1df8e0ef |

Rec Royale uses the `Quest_SciFi_*` copies (pistol 30/20 rate 3; shotgun head 8 torso 5; auto 6/5 rate 15; railgun
80/40 rate 1) with per-rarity damage from 3.3. Meshes: `Quest_SciFi_Pistol_Main`, `Quest_SciFi_MachineGun_Main`,
`Quest_SciFi_Shotgun_Main`, `Railgun_LOD1`, `Arena_Cyber_BeamGun_Main` (+ FrontGrip/BackGrip).

### 3.3 Rec Royale weapons, rarities and loot table

Source: `TextAsset:RecRoyaleRangedWeaponTaxonomy` (MAIN #-7834917802259654357; protobuf) referenced by
`BattleRoyaleGearManager.arsenalBlob` on `[BattleRoyaleManager]`. Parsed by `research\shoot\tax.pl` into
`research\shoot\out\taxonomy.tsv` (82 rows). Numbers exact; field meanings inferred (GUESS where not obvious).
Per entry: name, rarity (0 common grey, 1 uncommon green, 2 rare blue, 3 epic purple, 4 legendary orange), loot weight,
weapon GUID (= typeId, 2.1), stats: `mag` (field 1), `load` (field 2, rounds per reload action, GUESS), body damage
(field 4), shots/s (field 5), auto rate (field 6), projectile speed (field 7; equals bulletFireSpeed where known, e.g.
launcher 18), headshot multiplier (field 8: 30/18 = 1.667), launchers also field 9 = explosion radius, 10 = explosion
delay. Ammo class (field 3): 0 pistol, 1 automatic, 2 rifle, 3 shotgun, 4 flintlock, 5 bow, 6 explosive,
7 consumable, 8 ammo pack.

| weapon (prefab) | ammo | C / U / R / E / L body dmg | weights C/U/R/E/L | mag | rate | speed (L) | head x | legendary name |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Paint pistol (PaintballGun) | pistol | 21 / 23.9 / 27.0 / 28.4 / 31.4 | 30/20/8/4/1 | 6 | 5 | 200 (250) | 1.667 | PSI Buster |
| Laser pistol (Quest_SciFi_Pistol) | pistol | 18 / 20.5 / 23.1 / 24.4 / 26.9 | 30/20/8/4/2 | 5 | 6 | 250 (300) | 1.667 | Neon Heat |
| Flintlock (Quest_Pirate1_Flintlock) | flintlock | 40 / 45.5 / 51.4 / 54.2 / 62.8 | 10 each | 1 (load 5) | 5 | 100 (150) | 1.667 (L 2) | GhostBeard's Sidearm |
| Paint shotgun (PaintballShotgun) | shotgun | 8 / 9.1 / 10.3 / 10.8 / 12.0 per pellet | 30/20/8/4/2 | 3 | 1 | 100 (120) | 1 | Splat Hammer |
| Laser shotgun (Quest_SciFi_Shotgun) | shotgun | 5 / 5.7 / 6.4 / 6.8 / 7.5 per pellet | 30/20/8/4/2 | 3 | 1 | 80 (110) | 1 | Bot Sweeper |
| Blunderbuss (Quest_Dracula1_BlunderBuss) | shotgun | L only: 9 per pellet | L 2 | 1 (load 5) | 1 | 80 | 1 | Deafening Boomstick |
| Paint burst rifle (PaintballAssaultRifle) | automatic | 12 / 13.5 / 15.2 / 16.8 / 18.4 | 30/20/8/4/1 | 24 | 5, auto 12 | 132 (166) | 1.67 | The Paintsaw |
| Laser burst gun (Quest_SciFi_AutomaticGun) | automatic | 5 / 5.6 / 6.3 / 7.0 / 7.7 | 30/20/8/4/2 | 22 | 5, auto 15 | 180 (220) | 1.2 | Neon Shredder |
| Paint thrower | automatic | L only: 5 | L 1 | 200 | 15 | 65 | 1 | Paint Thrower |
| Beam cannon (Arena_PowerWeapon_BeamGun) | automatic | L only: 10 | L 1 | 100 | 25 | 100 | 1 | Beam Cannon |
| Paint scope rifle (PaintballRifleScoped) | rifle | 40 / 42.2 / 50.4 / 55.2 / 60 | 30/20/8/4/2 | 3 (load 1) | 0.75 | 300 (350) | 2 | Perfectionist |
| Laser railgun (Quest_SciFi_RailGun) | rifle | 35 / 36.9 / 44.1 / 48.3 / 52.5 | 30/20/8/4/2 | 3 | 1.65 | 250 (300) | 1.714 | JumboTron Accelerator |
| Goblin wand (Quest_Goblin_Wand) | rifle | 25 (epic, no weight) | - | 25 | 0.75 | 75 | 2 | "Witch Slayer" |
| Laser orb launcher (Arena_RocketLauncher) | explosive | 100 (L 110) | 30/20/8/5/1 | 1 | 0.75 | 75 (90) | 1 | Laser Cannon (radius 5, delay 0.725) |
| Paint launcher (PaintballGrenadeLauncher) | explosive | 100 (L 110) | 30/20/8/3/1 | 3 | 0.75 | 18 (25) | 1 | House Painter (radius 2) |
| Longbow | bow | C 40 / U 42.2 / L 60 | 20/15/-/-/6 | 1 (load 10) | 5 | 99 (165) | 1.5 | Rigid Thwapper |
| Crossbow | bow | rare 20 | R 1 | 1 | 10 | 500 | 1.5 | Goblin Annoyer |
| Crossbow_Hunter | bow | L 30 | L 1 | 1 | 5 | 150 | 2 | Hunter's Dream |

Consumables (field 8: 1 = health, 2 = GUESS apply time s, 3 = shield, 4 = shield apply time):

| item (prefab) | variants (rarity, weight, amount) |
| --- | --- |
| RecRoyale_HealthPotion | SMALL HEALTH (0, 20, +20), LARGE HEALTH (3, 7, +50), Full Health Potion (4, 1, +100) |
| RecRoyale_ShieldPotion | SMALL SHIELD (0, 25, +20), LARGE SHIELD (3, 7, +50), Full Shield Potion (4, 1, +100) |
| RecRoyale_HealthAndShieldPotion | Shield And Health Potion (3, 3, +50/+50 over 5 s), Full Restore Potion (4, 1, +100/+100) |
| RecRoyale_Grenade | Grenade (1, 15) |
| ProximityMine | Paint Mine (3, 5) |
| (none) | EMPTY ACCESSORY (weight 20): the slot yields nothing |

Ammo packs (field 9 = {ammo class, amount}): Pistol Ammunition weight 8 -> 10 rounds; Automatic Ammunition 8 -> 44;
Shotgun Ammunition 16 -> 6; Rifle Ammunition 16 -> 6; Rocket Launcher Ammunition 4 -> 2; EMPTY AMMUNITION weight 25.
Potion prefab details: `HealthPotion.defaultHealthRestoreAmount 10`, duration 0.5; `Potion.drinksToActivate 2`,
`drinkResetTime 2`; `DrinkableBottle.lifeSpan 8`, pourInterval 0.25, meshes `RecRoyale_Frontier_Potion_Closed` /
`_Liquid`. Ammo pack: `BattleRoyaleAmmo` with HoldToInteractHandler (move speed x0.33 while collecting), collect sound
RecRoyale_Ammo_Pickup02 (0.85).

### 3.4 Other reusable combat bits
- Quest bows/flintlock/blunderbuss details: research\q (rr_quests.md).
- `[Sandbox_Projectile_Cork]` 7421077002004536387 (mesh `Cork_Projectile` MAIN 5703355152252123503),
  `[Sandbox_Projectile_Paintball]` 90050059627982726, `[Sandbox_Projectile_Laser]` -319069078546069995: projectiles
  of the Maker Pen launcher (key 2c). The cork is the right look for Showdown's cork guns.

## 4. Game-rule / manager objects

### 4.1 Paintball (`[PaintballManager]` MAIN -3410785895361013, component PaintballManager#-5292409918778354165)
Rules chips: CTF `PaintballCTFGameRuleWrapper#3205260043674710670` (defaultFlagCapturesToWin 3, defaultGameDuration
600 s, music on, config `PaintballCaptureTheFlag_Config` MAIN #-6907008430187319785); Team Battle
`PaintballTeamBattleGameRuleWrapper#4093265186504617359` (defaultScoreToWin 50, 600 s, config
`PaintballTeamBattle_Config` #2804163223909351648).

PaintballManager fields: flagCarrierGameRoleId 8388608; flagHolderMovementCooldown 1.5 s (carrier teleport
cooldown); flagHolderCustomWalkSpeed 3.55 m/s (2020 notes: speed penalty removed, so treat as normal speed);
audioFXMinInterval 1.5 s; in-game role teleportDelay 1.5 s (ACTk-obscured value; `flat.pl` decodes them:
ObscuredFloat hidden bytes have byte 2 and 3 swapped, then XOR key); playerCombatUIPrefab [PlayerCombatUI]; VO/SFX in
7.1; loadingGameMusic PtblRiver_10Sec, gameplayMusic RiverPredator.

Game configuration (both modes identical except name and stats; `research\shoot\out\cfg_ctf.txt`): MaxHealth 1,
MaxShield 0; 2 teams x MaxTeamSize 4; manual start needs 2 players on 2 teams (MinTeamSize 1); GameStartDelay 10 s;
EndGameResultsDuration 15 s; JoinInProgress; team change during game allowed (with respawn); TeamSelectionMethod 2
(GUESS auto-balance); SpawnPointSelectionMethod 1 (GUESS "Team": away from opponents); AutoRespawn after 5 s,
RespawnInvincibilityDuration 5 s, RestoreHealthOnRespawn; ResetToolsOnGameStart; friendly fire off except
`GrenadesCanDamageTeammates = 1`; OutOfBounds: grace 3 s, max 4 s, then downed; DeadMonochromeEffect and
DamageRedFlashEffect on; opponent combat UI on, teammate UI on (not always visible), no HUD; team outfits on, team
colour in feedback, team name instead of player name in notifications; stats "Hits", "Outs" (+ "Flags" in CTF);
MainHandToolId/OffHandToolId = starting weapons (16-byte GUIDs; read with rr2b if needed; the wiki says the pistol).

### 4.2 Laser Tag (`[ArenaManager]` MAIN 490217366522465100, component ArenaManager#7648634921983576908)
Wrapper `LaserTagGameRuleWrapper#2349319963380448799`: defaultScoreToWin 400, defaultGameDuration 480 s, no config asset.
ArenaManager: scoreName "Lasertag"; playerKillPoints 10, playerFriendlyFireKillPoints -10, normalEnemyKillPoints 2,
eliteEnemyKillPoints 10, enemyFriendlyFirePoints -2, lootChestPoints 1, minLeadFeedbackPoints 100,
gameEndingSoonPointsRemaining 100, gameEndingSoonTimeRemaining 60 s; combat music min 6 s / fade 5 s; collectible
[Quest_SciFi_CreditParticle] value 50; 3 dual-wieldable weapon type ids; UI [Arena_PlayerCombatUI_3D] (healthColor
rgba(0.914,0,0.247), damageColor rgba(0.914,0.624,0)) and [Arena_EnemyCombatUI_3D]; floating "+points" notices
[Arena_Points_WorldSpaceNotification_Team1/Team2] (font colours cyan rgba(0.087,0.747,0.893) / pink
rgba(1,0.118,0.420)); hit/crit particles [VFX_Arena_Laser_WeaponImpact_Player] 975748325100708119,
[VFX_Laser_Crit_WeaponImpact] 2002472830690599179; SFX Arena_PlayerOut01@c695b765, Arena_Headshot02@eacdd25c,
Arena_CashRegister01@56515818.
Bots: `(QuestAIController)SciFiBasicGroundRanged` (tread bot), `SciFiBasicFlyingRanged2020` (bat bot),
`SciFiEliteGroundRanged`, `SciFiEliteFlyingRanged`, `SciFiGroundExplosive`, `SciFiKnightAssassin` (dumps
`research\shoot\dumps\_QuestAIController_*.txt`; AI details in rr_quests.md). Music: ArenaIntense@9fbf7292,
Arena_CyberJunk_Explore03b@4de47222, Arena_Cyberjunk_combat04b@853e5df3.

### 4.3 Rec Royale (`[BattleRoyaleManager]` MAIN 1375039659566223917)
Wrappers `BattleRoyaleGameRuleWrapper` modeType 0 (solos, #5345796209044464032) / 1 (squads, #2457702587187055408).
BattleRoyaleManager#-7770154480817941971: spawnInCableCarStateDuration 10 s, cableCarFanfareDuration 10 s,
waitForPlayersToExitCableCarStateDuration 20 s, warnKickPlayersOutOfCableCarStateDuration 10 s,
kickPlayerOutOfCableCarStateDuration 5 s, victoryStateDuration 12 s; winnerDoor [RecRoyale_WinnerDoor]
(TeleportationPortal + 2 GamePlayerSpawnPoints + winner sign); SFX RecRoyale_Revive_Revived01; music
ScoutTheme_YouLose01 (out/lose, vol 0.66), RecRoyale_WinStinger02 (win), ScoutTheme_DelayedGliderLaunch02 (leaving the
gondola). BattleRoyaleGearManager#8005239986681532973: bag (RecRoyale_Backpack), map (RecRoyale_PlayerMap), arsenal
(3.3). BattleRoyaleImageManager: 7 ranks Recruit, Camper, Tracker, Hunter, Survivalist, Ranger, BeeKeeper
(`Scout_Badge_*_Color` / `_Mono` sprites in MAIN). Winner sign texts (random): "#1 Squad", "GG", "Easy Beezy",
"Victory", "Winner Winner", "Squad Goals", "Get Rec'd", "I won and all I got was this lousy sign", "I'm just here for
the acorns", "Winners!", "Mission Comp(lete)..." (component `BattleRoyaleWinnerSign#-7120805332537812034`).
Backpack: `BattleRoyaleGearBag#7463650732073942821` with put on/take off/hip attach/detach/interact sounds.

## 5. Flags, goals, respawn/spawn points
- (FlagTool) key 89c: `FlagTool#-927423950202200308` (team set at runtime), flag mesh `PaintballFlag_LOD0..2`, cloth
  flag `PaintballFlag_Flag_LOD0_Desktop` (SkinnedMeshRenderer + Cloth) / `_Mobile` (static mesh); pole about 2.2 m
  (pivot offset -0.946, cloth at +1.269); `GlowDisc` (scale 1.5,1.5,250: a tall team-coloured light pillar visible
  across the map) at y -1.478; `FlagReturnPortal` (GenericPortal + BoxCollider): used to return a dropped flag;
  `Goal Attach Point` y -0.983; colliders Handle + Staff; `GoalPoint`s along the staff; `PaintballVisibilityBehaviour`.
- (FlagGoal) key 09f: `FlagGoal#1886520097773562462` radius 0.5 m, `Flag Attach Point` y 0.3, `Flag Stand` mesh +
  BoxCollider at y 0.07, GlowDisc beam; material `Flag_Goal_Team_Colored`.
- (FlagAndGoal) key 89: a SandboxSet combining both (Maker Pen rooms).
- Spawn points: (SpawnPointV2) key af, [SandboxSceneSpawnPoint] 5047701108921656675, (PlayerRespawnPointVisual) key 2b7,
  (RespawnNodeVisualization) key 4239 (GamePlayerSpawnPoint components; dumps `_SpawnPointV2_.txt`). Our build uses
  plain Transforms; these prefabs are only useful as visible markers.

## 6. VFX, decals, projectiles

### 6.1 Projectiles (all `RecRoom.Core.Combat.Bullet`, pooled)
| prefab (MAIN pathId) | radius m | gravity | drag | reflections | life s | trail |
| --- | --- | --- | --- | --- | --- | --- |
| [Paintball] -7696766629268348260 | 0.02 | yes (x1) | 0 | 1 | 4 | BulletTrail 0.175 s, width 0.03, mat PaintBall_BulletTrail_Mat -1890250985906898361 |
| [ShotgunPellet] -8861887174896390049 | 0.02 | yes | 0 | 1 | 10 | trail 0.1 s |
| [SniperRound] -5475820469763026427 | 0.02 | yes | 0 | 1 | 4 | PersistentBulletTrail 1.5 s, width 0.02, finalWidthScale 3 |
| [ProjectileGrenade] 7607989148689966690 | 0.02 | yes | 0 | 1 | 4 | trail + Tumble |
| [PaintThrowerBullet] 334607149978527786 | 0.09 | yes | 0.585 | 1 | 10 | LiquidBullet (meshes PaintThrower_Projectile_1..3) |
| [Quest_SciFi_PistolLaser] -1708459135175520386, [Quest_SciFi_ShotgunLaser] -8850175759155996552, [Quest_SciFi_AutomaticLaser] -7851743954730369415 | 0.02 | no | 0 | 1 | 4 | visual stretching |
| [Quest_SciFi_RailGunProjectile] 768626568277057665 | 0.02 | no | 0 | 0 | 4 | stretching |
| [Quest_SciFi_StretchedBeamProjectile] 9148716205526620051 | 0.02 | no | 0 | 0 | 0.1 (max 50 m) | stretching |
| [Arena_RocketLauncher_Projectile] 6136161886108141632 | 0.16 | no | 0 | 0 | 4 | stretching |
Other fields: `minWallSlopeDegrees 30`, `restitution 1`, `destroyOnMask 150` (Rec Room layers; GUESS: bullets bounce once
off surfaces that are not players and flatter than 30 deg). Recommendation: splat on first hit except for the
explicit bounce case; visuals do not need to match exactly.

### 6.2 Paint splats / decals / particles (MAIN unless noted)
- (PaintballImpactDecal) 1df8e0ef 3842073249555665047: the splat for all paint guns (ImpactDecal; per-weapon scale
  0.8-1.2 or 1.2-1.5; takes team colour). [PaintballImpactDecal_Large] -183538601365443099 (hammer),
  [PaintThrowerImpactDecal] -7074337879239392423 (MeshFilter + MeshRenderer + ImpactDecal),
  [Quest_SciFi_ImpactDecal] -3727660404194517299 (laser scorch).
- Paint muzzle/impact: [VFX_PaintBall_Shoot] -4278402775329998714, [Paintball_ShotgunBurst01] -965337472542410761,
  [VFX_RifleImpact] -5772923591649601887, [Paintball_GrenadeLauncher_Explosion_VFX] 894152347303586998,
  [Paintball_PaintThrower_Splash] -7405811901820659496, [VFX_PaintThrower_MuzzleFlash] -4573838848275381700,
  (Paintball_SplatHammer_Paint_Explosion_VFX) 6750918707587189322, [ProximityMine_Explosion_VFX] 7854808299274300834,
  [Sandbox_Explosion_Grenade_VFX] 8611346365751145328, [Sandbox_Impact_Weapon_VFX] -1119345713705230156.
- Laser: [VFX_Laser_MuzzelFlash] 938935335124548314 (+ _RailGun -2824804043139756459, _RocketLauncher
  7543014403417106077, _ShotGun 5856511666102955214), [VFX_Laser_WeaponImpact] -5828286138502666526 (+ _Small
  -237685941335325812, _RailGun 368249850319573268, _RocketLauncher 8832408295093785813),
  [VFX_Laser_Crit_WeaponImpact] 2002472830690599179, [VFX_Arena_Laser_WeaponImpact_Player] 975748325100708119,
  [Arena_RocketLauncherExplosion] -377946016943667411 (+ _Implosion -3026147826397424049),
  [Arena_Grenade_Explosion_VFX] JumboTron Variant -4669758548333532865, beam [Arena_Cyber_Beam_Charge_VFX]
  3905870235381114005 / _Embers -7237409265087850308 / _ImpactSparks 8534286289196920814 / _MuzzleFlash
  -6969999572264808127, [VFX_ObjectSpawn] -1545908865104360033 (item respawn puff).
- Particle materials often use `Sprites/Default`: swap to URP Particles/Unlit for VR (rr_runtime_facts.md).

## 7. Audio

Clips in MAIN become resident with the container that references them (e.g. load key 7b for the paintball VO).
Clips in their own one-clip bundles are containers themselves (key given).

### 7.1 Paintball game VO/SFX (fields of PaintballManager unless noted)
| event | clip (bundle, pathId, key) |
| --- | --- |
| mode title | "Capture The Flag" (MAIN 5635933796967096366), "Team Battle" (MAIN 720655657548790325) |
| player hit / out | HitSound01 (MAIN 6451015365694038197, vol 0.5), OutSFX (MAIN 7834946154998007773, vol 0.5); VO "Out" (MAIN -4456959885725097216), "Hit"@0933d7bb, "Got You"@85515de1 |
| flag pickup | FlagTaken (MAIN 6268712261896013616) + "Blue flag taken" (814258215737215546) / "Red flag taken" (6187702441520179997) |
| flag dropped | "Blue flag dropped" (-4689924444538791882) / "Red flag dropped" (-8487452149001212544) |
| flag returned | FlagReturn01 (-6337917848659104918) + "Blue flag returned" (1224955003219747138) / "Red flag returned" (-966133786779632289) |
| flag captured | ScoreSFX@a8a48c1d 7228764702924650658 + Blueteamscores@3ac9d648 (key a) / Redteamscores@2467f3cb (key 4) |
| team names | "Blue Team" (MAIN -1273163103982826951), "Red Team" (MAIN -8532761083206627114) |
| results | Blueteamwins2@917f6f76 (key 5), Redteamwins2@07668113 (key a), Tiegame@4a14373c (key 0), "The Winner Is"@85515de1, Gameon@8b1b2e31 (key e), Gameover@b286ccb0 (key d), MatchOver01@34d38287 |
| countdown | "One minute remaining"@063b6b66 7283794961762533954, "10 seconds remaining"@063b6b66 8067635986747856111, 1up..10up (one bundle each: 1up@a180abb8, 2up@939385ee, 3up@c63df1ef, 4up@de2d1c4d, 5up@2544a19c, 6up@3dbff611, 7up@b984aed0, 8up@b71a98df, 9up@f876249c, 10up@f7d163db), 5_short@85515de1 |
| lobby/team pick | Eenie/Meenie/Minie/Moe/Join/Leave/Ready/Start/Play/Over/Safe/Result/Result Thump/Pre Game Loop v2 @85515de1; CohortNUX_WelcomeToPaintball@063b6b66 |
| music | PtblRiver_10Sec (MAIN 6160471612412744603, loading sting), RiverPredator@e84539de (key f, gameplay), PtblHomestead_Varmint@764a3f17, PtblSunset@dd09bf00, Spillway@954c197d, RangeFinder@95dc9c09 |
| ambience | Paintball_Ambiance_ClearCut01@18ef1d8f, Paintball_Dam_Crickets_AmbianceFill01@4d9cb9fb, Paintball_Drive_In_Classic_Movies_AUDIO_ONLY (MAIN 446310607076494074, drive-in screen audio) |
| misc | Horn01@b78dc502 / @d9e43916, Whistle01@ec475596, CrowdCheer01, GolfClap, AmyVO_Generic_GoodGame@7db0a98c, AmyVO_Generic_YouWin@3f598595 |

### 7.2 Weapon sounds (WeaponAudio; MAIN unless noted; volume in brackets)
| weapon | fire | reload | hit player / env | pickup |
| --- | --- | --- | --- | --- |
| pistol | PaintballShoot01 (0.906) | - | PaintballPlayerHit01 (0.848) / PaintballEnvironmentHit01 (0.75); misfire PaintballMisfire01 | paintballPistol_pickUp_03@1df8e0ef |
| burst rifle | Paintball_AssaultRifle_Fire11 | - | same | same |
| shotgun | Paintball_Shotgun_Fire05 (0.8) | PaintballShotgunReload01 (0.35) | same | paintballShotgun_Pickup_02 |
| scope rifle | Rifle_fire01 | Rifle_reload02 (0.35) | same | pistol pickup |
| launcher | Paintball_GrenadeLaunched01 (0.95) | Paintball_GrenadeLauncher_Reload (0.2) | explosion Paintball_GrenadeExplode01 (0.8) | shotgun pickup |
| paint thrower | sfx_paintball_paintthrower_fire06 + stream initialize01 / loopingfire01 / finishfire01 | Scifi_reload_generic_01 | ..._impact_player01 / ..._impact_environment01 | ..._pickup01 |
| grenade | explode PaintballGrenade02, fuse Paintball_GrenadeFuseLoop01, click Paintball_GrenadeClick01, beep Paintball_GrenadeBeep01 | | env Paintball_GrenadeHitEnvironment02@1df8e0ef | throw Paintball_GrenadeRelease01@1df8e0ef |
| mine | sfx_paintball_paintmine_detonate01, _arm01, _attach01, _impactandattach01, _trip01, _throw01 | | | |
| shield | | | paintballShield_hit / _hit_02 | paintballShield_Pickup@1df8e0ef |
| splat hammer | sfx_pm_splathammer_swing / _splat / _blop_drop / _inflate / _deflate / _ready / _playerhit / _physical_hit_* | | | |
| laser guns | LaserPistol_01, burst_02, Sniper_01, Charging, Quest2_Rifle_* | Scifi_reload_generic_01, AmmoOut_01@40da529a | EnemyHit_01/02, Arena_PlayerDamaged01@eb431ca9 | Arena_BeamGun_Pickup_01 ... |
Full list: `research\shoot\key_audio.tsv` (bundle, pathId, name; 227 rows).

### 7.3 Rec Royale audio
RecRoyale_Ammo_Pickup02, RecRoyale_Backpack_Attach03/Detach03/Interact01-04, RecRoyale_Damage_ShieldHit01,
RecRoyale_Glider_MetalCreek01/02, RecRoyale_Glider_WindGust01, RecRoyale_Heartbeat_Down01/Up01,
RecRoyale_Holster_Attach01/Detach01, RecRoyale_LookoutTower_GustLocal01, RecRoyale_Revive_Revived01,
RecRoyale_Score_* (XP/acorn tally), RecRoyale_Shields_Recharge01, RecRoyale_Walkie_Notification03,
RecRoyale_WinStinger02, ScoutTheme_Gondola01, ScoutTheme_DelayedGliderLaunch02, ScoutTheme_YouLose01 (bundles and
pathIds in key_audio.tsv).

### 7.4 Lobby radios
One track per paintball map, each its own bundle with a container: PaintballLobby_RiverRadio@2bb92954 (key 7),
_HomesteadRadio@e674005a (9), _QuarryRadio@8fa6e28d (9), _ClearcutRadio@0d0500fd (7), _SpillwayRadio@922e4496 (d),
_SunsetRadio@0716e245 (8). Play the selected map's track from a `(Radio)` prop (key f3) in the lobby. The Radio's
station list (wiki) includes River Predator, Hangar, Clearcut, Spillway, Range Finder, JumboTron Explore.

## 8. UI (scoreboard, combat HUD)
- **Scoreboard**: `(ScoreboardProjector)` key c3e (`UIProjector#6928108154898000339`): pops up when a player is within
  20 m (inner radius 1.2 m), max opening angle 90, rotation 60; sounds SFX_ScoreboardPopUp_Mono (0.2),
  SFX_ScoreboardPopDown_Mono (0.2), SFX_ScoreboardLoop_Mono hum (0.01); `viewPrefab` = the world-space canvas of
  `[GameScoreboardView]` (scale 0.008; VerticalLayoutGroup Grid -> Header (TitleText, TimerText, subtitle) -> Body
  (ScoreboardPagedGrid, 12 rows per page, prev/next) -> Footer; rows `(GameScoreboardTeamRow)` /
  `(GameScoreboardPlayerRow)` / `(GameScoreboardPlayerStatColumn)` in 1df8e0ef). Colours: primaryColor
  rgba(1,0.953,0.412,0.484) (yellow), text rgba(0.102,0.102,0.102), button highlight rgba(0.929,0.396,0.165) (orange),
  default background alpha 0.5, highlight 1. Recommendation (as for the watch UI): build our own TMP scoreboard with
  these colours; the Rec Room view scripts are missing.
- **Out marker**: `[PlayerCombatUI]` = a SpriteRenderer with `KO_Skull` (MAIN 4767738381296020622) shown over a player
  who is out (the "KO marker"; KO skins replace it).
- **Laser tag**: `[Arena_PlayerCombatUI_3D]` (health bar over players, colours above), `[Arena_EnemyCombatUI_3D]` (bots).
- **Rec Royale**: `[RecRoyale_PlayerCombatUI_3D]` `BattleRoyalePlayerCombatUI#-7142851532900598571` with ShieldBar,
  HealthBar, DBNOBar (downed timer), ReviveText (TMP), downed sprite `Downed_Player_KO_Skin@1df8e0ef`, dead sprite
  KO_Skull; `[RecRoyaleUIPrefab]` and `(RecRoyaleInfoScreen)` in 16079ee0; map sprites
  `RecRoyale_Fontier_Map_PlayerArrow_Tex` 7679726741018310557, `_PlayerDown_Tex` 8152060755141355467, `_PlayerOut_Tex`
  -1511637149885643783.
- **Player map**: `BattleRoyalePlayerMap#-4303724925715071574`: expanded size 0.4 x 0.4 m, collapsed/expanded roots,
  lowerLeft/upperRight map transforms (world bounds come from the encrypted scene; we define our own), pips for
  teammates, ring overlay renderer (material index 2) for the circles. Textures: `Map_Frontier_Tex` MAIN
  -1948765635445070417 (squads, with grid A-F x 1-6 and legend), `Map_Frontier_Solo_Tex` MAIN -6018074973680880826
  (with POI names), material `Map_Frontier_Mat` -6720064608398114017.

## 9. Map props (kit pieces with sizes)

All in MAIN, spawnable "Sandbox" variants (they carry Tool/Rigidbody/missing scripts: instantiate, strip or ignore the
missing scripts, set Rigidbody kinematic, move to our layers). Sizes = LOD0 mesh local AABB (x by y by z, metres) from
`research\shoot\out\prop_sizes.tsv`, times the child scale where noted; FBX meshes may be Z-up (then the tallest
dimension is the length). Measure `Renderer.bounds` at runtime for exact placement.

| prop (key, root pathId) | size (m) | use |
| --- | --- | --- |
| (Rock_Cliff)Variant_Sandbox)Variant (1ed, 501202330164767977) | 12.3 x 6.6 x 5.4 | Rec Royale island cliff (mesh RecRoyale_Rock_Cliff) |
| (Rock_Wall)Variant... (dc7, 5461149383523125665) | 4.9 x 8.2 x 7.8 | island rock wall |
| (Rock_Plateau)Variant... (b25, -8645099751828022059) | 8.0 x 4.7 x 6.7 | flat-top rock |
| (Rock_Stump)Variant... (b8fd, -1821153920922046143) | 7.6 x 5.0 x 6.1 | rock stump |
| (Rock_Tall)Variant... (b8, -7889829353332152086) | 5.0 x 6.4 x 3.6 | tall rock |
| (Rock_Cone)Variant... (423, 2145809273400279154) | 5.0 x 5.4 x 6.8 | cone rock |
| (Rock_Wedge)Variant... (22e, 2605741448996901903) | 6.7 x 2.9 x 3.4 | low wedge rock (cover) |
| (Rock_01)Variant... (761, -3687124906640291192) | 2.9 x 2.0 x 2.8 mesh, root scale 2.461 -> 7.0 x 5.0 x 6.8 | boulder |
| (Rock_04)Variant... (73, 5641500120161923649) | mesh Quest_Rock04@1df8e0ef | boulder |
| (Sandbox_FlatTreeLarge / Medium / Small) (63e / 4ae / e9d) | 2.8 x 3.0 / 2.8 x 2.8 / 2.4 x 1.6 (collision) | stylised Rec Room trees |
| (PineTree_Dead_Sandbox)Variant (f7, -4490394049085640736) | branches 10 x 11.7 x 6.8 centred at y 11.6 (tree ~20 m) | Clearcut/island dead pines |
| (Tree_Stump_Sandbox)Variant (275, 5578660280649625915) | 2.3 x 1.3 x 2.1 | low cover |
| (LogPile_Sandbox)Variant (523, 2821365170050377018) | 11.0 x 2.8 x 3.8 | big cover (Clearcut, River) |
| (WoodStack_Sandbox)Variant (038, 8773538442931707105) | 4.2 x 0.8 x 1.5 | lumber stack |
| (Wood_Pallet_Sandbox)Variant (ce4a, 1097731344342154998) | 1.6 x 1.4 x 0.2 | pallet |
| (Wooden_Spool_Sandbox)Variant (c1f, -370453489667178836) | 0.93 dia x 0.75, root scale 2.275 -> 2.1 m | cable spool cover |
| (Hay_Bale_Sandbox)Variant (7c, 7433812899791837730) | 1.3 x 0.8 x 0.8 | low cover (Homestead, Showdown) |
| (Tractor_Tire_Sandbox)Variant (704, 5443411400854158037) | 2.25 dia x 0.53 | cover |
| (Tire_Car_Sandbox)Variant (14a, 8430805198039020927) | 0.98 dia | dressing |
| (Truck_Sandbox)Variant (91b, 3950827878299083305) | (axle mesh 1.7 x 2.8; body larger) | truck (Clearcut/Quarry) |
| (Car_50s_Sandbox)Variant (ba, 6196579483568669122) | wheel base ~1.6 x 2.7 (tyres at x +-0.8, z -0.98..1.68), body ~2 x 1.6 x 5 GUESS | Drive-In cars |
| (Car_80sSportsCar_Sandbox)Variant (c19, 7043090853603724136) | 2.2 x 1.3 x 4.2 | Drive-In cars |
| (GasPump_Sandbox)Variant (4d, 5554453677834403891) | 1.4 x 2.2 x 0.75 | Frankie's Fuel |
| (Sign_FrankiesFuel_Sandbox)Variant (31, -891674695020453633) | 5.0 x 4.0 x 0.5 | gas station sign |
| (Sign_Diner_Sandbox)Variant (61, -8379063119583683322) | 15.7 x 5.3 x 0.6 | diner roof sign |
| (Sign_DriveIn_Sandbox)Variant (d8cd, -2109238989783053389) | 0.85 x 5.5 x 6.2 | drive-in sign |
| (Sign_Drivein_Redbase_Sandbox)Variant (1c99, -8015287384671747203) | 0.76 x 15.3 x 0.72 | 15 m pole sign marking the RED base on Drive-In |
| (Speaker_DriveIn_Sandbox)Variant (2a7, -7166362215221963111) | 0.5 x 1.5 x 0.5 | speaker post between cars |
| (Drivein_Projector_Sandbox)Variant (e6b, -2620064464598138134) | 1.5 x 2.3 x 2.8 | projector (booth roof) |
| (Drivein_AC_Sandbox)Variant (d5, 3141269373162928573) | 3.4 x 2.0 x 1.8 | roof AC unit / cover |
| (Drivein_SteamVent_Sandbox)Variant (3a, 3330124999517088517) | 2.1 x 2.7 x 2.1 | vent |
| (Booth_Diner_Sandbox)Variant (d57), (Table_Diner_Sandbox)Variant (e8), (Stool_Wood_Round_Sandbox)Variant (58) | booth 1.0 x 1.6 x 2.1 | diner interior |
| (Umbrella_Table_Sandbox)Variant (e5d7), (Table_Picnic_Sandbox)Variant (33), (LawnChair_Sandbox)Variant (c2f) | umbrella 3.1 x 3.4; picnic 1.9 x 0.85 x 1.85 | dressing |
| (Dumpster_Sandbox)Variant (999, -8503554245372140003) | 1.5 x 1.7 x 3.1 | cover |
| (TrashBin_Sandbox)Variant (0c, 7458994975604054613) | SciFi_TrashBin 2.6 x 1.3 x 4.1 | laser tag cover |
| (Arena_Cyber_TrashBag_Sandbox)Variant (f2c, -7069671901031807680) | 1.1 x 1.1 x 0.75 | Cyber Junk dressing |
| (TrashCan_Metal_Sandbox)Variant (d4ae), (TrashCan_Office)SandboxVariant (cd8) | ~0.5 | dressing |
| (Dam_ElectricalUnit_Sandbox)Variant (f9, 3155635585674695522) | 6.5 x 2.7 x 2.7 | Spillway machinery / cover |
| (ControlPanel_Tall_Sandbox)Variant (49, 723011165247542628) | 4.0 x 5.0 x 0.55 | Spillway wall panel |
| (ControlPanel_Short_Sandbox)Variant (967, 4523619598279891692) | 2.6 x 1.75 x 1.1, root scale 0.8 | console cover |
| (BreakerBox_Sandbox)Variant (a5, -5382165326010292219) | 0.55 x 2.4 x 0.35 | wall dressing |
| (Pipes_Straight / _Straight_Long / _Curved / _Bracket_Sandbox)Variant (9aa / 458 / 83 / cb) | measure at runtime | Spillway pipes |
| (IBeam_10M / 5M / Bracket_Sandbox)Variant (369 / 66 / 1d) | 10 x 0.66 x 0.41 / 5 x 0.65 x 0.41 / 1.7 x 1.2 x 0.6 | girders, catwalk frames |
| (Railing_Guardrail_Auto_Sandbox)Variant (81e, 4324932278584936935) | 1.0 x 0.67 x 0.2 (tile) | road/dam guard rail |
| (Concrete_Barrier_Sandbox)Variant (418, 745941225956459415) | 2.35 long x 0.85 x 0.94 high (Z-up) | jersey barrier cover |
| (Concrete_Tube_Sandbox)Variant (25, -6381181207946204830) | ~2.2 dia x 2.55 | culvert pipe (cover/tunnel) |
| (Sandbox_ConcreteBlock) (5a), (Sandbox_StonePillar) (93e) | 1.0 x 1.1 x 4.2 / 2.3 x 2.6 x 2.3 | blocks |
| (ElectricalPole_Sandbox)Variant (abd, -9062792070534546877) | 3.6 x 12.8 x 1.2 | power line poles |
| (Fence_Wood_Board_Wide / Fence_Wood_Frame_5m / Fence_Sandbox)Variant (93d / e1d / cd) | board 0.48 x 3.0; frame 5 m; I-beam fence 3.7 | fences, perimeter |
| (MetalBarrel_Sandbox)Variant (79), (Sandbox_Barrel) (96b) | 0.6 x 0.9 / 0.77 x 0.93 | barrels |
| (Scaffolding_Shiny_Sandbox) (aae), (Scaffolding_Shiny_Block_Sandbox) (af6) | 1 x 1 x 2 / 1.08 cube (x2 child scale) | scaffolding, platforms |
| (CrashPad_Scaffolding_Sandbox) (199) | 1.2 x 3.0 x 1.15 | |
| (Cardboard_box_Sandbox)Variant (11e) | 1.1 x 0.8 x 0.8 | crates |
| (Sandbox_Lockers) (d5c) | 2.1 x 2.0 x 0.7 | lobby lockers |
| (Sandbox_LogBench) (f5a), (Sandbox_SittingLog) (abf), (Sandbox_Campfire) (4b8) | 2.1 long / 0.5 / - | camp dressing (island, lobby) |
| (Standee_Bear01) (81, 848219397070967836) | mesh Bear_Standee@1df8e0ef | Frontier bear standee (also RecRoyale_Frontier_Bear_Standee_fbx in 1df8e0ef) |
| (Light_Chevron_Sandbox) (c83), (Light_Marquee_Long_Sandbox) (c2), (Light_Strip_Short_Sandbox) (53e), (StageLight_Can_Sandbox) (58e), (CircuitStageLight) (718d) | chevron 5.1 x 3.5 (x0.5), strip 0.5 long | neon/emissive dressing for laser tag and drive-in |
| (Sandbox_Glowstick_Blue / Red / Orange / Purple) (f5 / cab / e3 / a5f) | small | team-coloured light accents |
| (Sandbox_Wall) (67f), (Sandbox_Wall_Window) (bc), (Sandbox_Floor) (3ea) | measure at runtime | generic building blocks |
| (Bounce_Pad_Sandbox) (81d) | | jump pads (Showdown roofs, optional) |

Missing (build procedurally): terrain, water, bridges, forts/bases, hangar shell, catwalks and ramps, loot coolers, the
gondola and hang glider, cinema screen, western buildings and train.

## 10. Map rebuild proposals

General recipe (all maps)
- Build at the instance origin (networking 5.3: instances are 2 km apart). y = 0 is the playfield floor; keep all
  floors above the world's respawn height.
- Geometry: procedural meshes (boxes, ramps, heightfields) with flat, saturated URP Lit colours in Rec Room's palette,
  plus the props of section 9 (original materials). Colliders: BoxColliders on procedural pieces, MeshColliders on
  props (they already have colliders). World on layer 0 or 23.
- Out of bounds: a box volume around the play area; outside it for 3 s grace then 4 s max -> out (config values).
- Pickups: weapon racks where a weapon reappears 15 s after being taken (Game Rules chip behaviour). Pistol is given
  at spawn.
- Each team has 5 spawn points in its base; pick the one farthest from visible opponents (Rec Room 2020 rule).
- Team base colouring: floor decals/stripes and glow beams in team colour.
- Coordinates below: +z toward the Red/team-2 end, +x to the right when looking from Blue toward Red.
  All layouts are reconstructions in the spirit of the originals (GUESS); dimensions are chosen for the measured
  weapon ranges (pistol convergence 30 m, burst max 50 m, shotgun 15 m, sniper useful to 60+ m).

### 10.1 Paintball "River" (the classic; 4 v 4; 84 x 48 m)
Theme: forest clearing split by a shallow river, wooden forts. Music RiverPredator, radio PaintballLobby_RiverRadio.
- Ground: heightfield 84 (z) x 48 (x) m, grass green, dirt paths; tree line outside (FlatTreeLarge/Medium every 4-6 m
  along the border plus PineTree_Dead behind) acts as boundary.
- River: channel along x at z in [-4, +4], bed at y -0.8, water plane at y -0.3 (walkable shallow; slows nothing),
  banks sloped 1:3. Crossings: central wooden bridge 3 m wide x 12 m long (deck y 0.4, 1 m railings) at x = 0;
  stepping rocks (Rock_Wedge, Rock_01 scaled 0.5) at x = -16 and x = +16; a fallen log (LogPile rotated) at x = +8.
- Bases (mirror by 180 deg rotation about the origin; Blue at z = -36, Red at z = +36):
  fort 10 x 7 m, ground floor walls 1.2 m with two 1.5 m gaps, upper deck at y 2.6 m (with 1.1 m parapet and two
  window slots), ramp 1.5 m wide from the back; flag stand (FlagGoal) 5 m behind the fort centre at (0, 0, -41);
  5 spawn points around the fort's back: (-4,0,-40), (4,0,-40), (-6,0,-37), (6,0,-37), (0,2.6,-36) on the deck.
- Mid cover (per half, mirrored): LogPile at (-12, 0, -14) rotated 20 deg; Rock_Tall at (14, 0, -18); Tree_Stump at
  (-4, 0, -20) and (6, 0, -10); Rock_Plateau at (-20, 0, -6) (climbable sniper perch 4.7 m); Hay/crate pile (WoodStack +
  Cardboard boxes) at (18, 0, -28); FlatTrees scattered at (-8, 0, -28), (10, 0, -30), (-18, 0, -24).
- Weapon racks: shotgun at each fort ground floor; grenade crate (2 grenades) at the bridge ends (0, 0.4, +-7).
- CTF flag path length ~82 m; running at ~4 m/s = 20 s one way.

### 10.2 Paintball "Sunset Drive-In" (Sunset Cinema, 2020; mid/long range; 100 x 70 m)
Theme: 50s roadside drive-in at sunset, warm sky, neon. Music PtblSunset, radio PaintballLobby_SunsetRadio, screen audio
Paintball_Drive_In_Classic_Movies_AUDIO_ONLY looping at the screen. New weapons here: paint thrower + mines.
- Ground: asphalt lot 100 (z) x 70 (x) with parking-bay stripes, gravel border, low hills behind.
- Big screen at x = +33 facing -x: procedural frame 30 m wide x 14 m high on 4 m legs (screen quad 26 x 11 m, dim
  emissive), Sign_DriveIn at its base; the area under the screen legs is cover.
- Car rows: 4 arcs of cars facing the screen at x = -18, -8, 2, 12, z from -30 to +30 every 5.5 m, alternating
  Car_50s and Car_80sSportsCar, Speaker_DriveIn posts between cars (every second gap). Cars are waist-high cover;
  leave 3 m lanes at z = -12, 0, +12.
- Centre building (projection booth + snack bar) at (-26, 0, 0): 18 (z) x 10 (x) x 4.5 m box with doors on all sides,
  inside Booth_Diner x4, Table_Diner, Stool_Wood_Round x6, counter; flat roof reachable by an outside ladder/ramp at
  the back, Drivein_Projector on the roof at (-26, 4.5, 0) facing the screen, Drivein_AC x2 and Drivein_SteamVent on
  the roof (cover), Sign_Diner along the roof edge.
- Blue base = "Frankie's Fuel" gas station at z = -44: canopy 14 x 8 m on 4 posts (roof 4.5 m), 2 GasPump islands,
  Sign_FrankiesFuel on a 6 m pole, small shop 8 x 6 x 3.5 m (spawns inside), flag stand behind the shop at (0,0,-49).
- Red base = the entrance/ticket booth at z = +44: ticket booth 4 x 3 m with barrier arms, fence line
  (Fence_Wood_Frame_5m), `Sign_Drivein_Redbase` 15 m pole sign at (6, 0, 44) (its name marks the red base), flag stand
  at (0, 0, 49); spawns around the booth.
- Side flanks: Dumpster + TrashCan clusters, Umbrella_Table picnic area at (-30, 0, +-30), ElectricalPole line along
  x = -34.
- Pickups: paint thrower on the diner roof (power position), 2 mines at each base, scope rifle on the screen
  platform (x = +33, y 4 m, reachable by a ladder), shotguns at bases.

### 10.3 Paintball "Spillway" (night hydroelectric dam; interior cover; 80 x 44 m)
Theme: night shift at Rec Room's dam: concrete, steel girders, pipes, sodium lights, water rushing below. Music
Spillway@954c197d, ambience Paintball_Dam_Crickets_AmbianceFill01, radio PaintballLobby_SpillwayRadio. Weapons:
pistol, shotgun, launcher, scope rifle.
- Structure: a dam crest deck 80 m long (z) x 14 m wide at y 0, flanked on +x by the reservoir (water plane y -1.5,
  out of bounds) and on -x by a 6 m drop to a lower spillway apron (y -6) with the spillway channel (8 m wide water
  channel at x in [-20,-12], flowing, out of bounds if you fall in: respawn).
- Two turbine halls (one per team) at z = +-30, each 16 x 14 x 7 m with a mezzanine at y 3.5 (IBeam frames, grated
  floor), inside Dam_ElectricalUnit x2 as cover, ControlPanel_Tall along walls, ControlPanel_Short consoles,
  BreakerBox rows, pipes (Pipes_Straight_Long / Curved) along the ceiling. Flag stands inside at the back
  (0, 0, +-36). Spawns on both floors.
- Centre: the crest with a gantry crane made of IBeam_10M (a bridge 7 m high spanning the deck, reachable by ladders at
  both ends: sniper perch), Concrete_Barrier lines staggered along the crest, MetalBarrel clusters,
  Railing_Guardrail_Auto along both edges; a lower route along the apron (y -6) with Concrete_Tube culverts as cover
  and stairs up at z = +-18.
- Lighting: dark ambient, spot/point lights at the halls and crane (warm 2700 K), team-coloured light over each hall
  door.

Alternative 3rd map with good prop coverage: **Clearcut** (foggy logging site 90 x 60 m): LogPile, WoodStack,
Wood_Pallet, Wooden_Spool, Tree_Stump, PineTree_Dead, Truck, Tractor_Tire, ElectricalPole, Concrete_Tube; URP fog
density so that visibility is ~35 m; ambience Paintball_Ambiance_ClearCut01; burst rifles.

### 10.4 Laser Tag arena "Hangar" (4 v 4 + bots; 56 x 32 x 12 m interior)
Theme: a real-world laser tag arena inside an aircraft hangar: dark, black-light, neon edges, team halves (Rec Room used
red/blue glow; our team colours: cyan team 1 / pink team 2). Music ArenaIntense.
- Shell: hangar box 64 x 40 m, 14 m high, arched roof (procedural), dark floor with emissive grid lines. Arena floor
  56 (z) x 32 (x). Bleachers along +x (4 rows, spectators and lobby: scoreboard projector, merch booth) behind a 1.2 m
  glowing barrier.
- Symmetric halves (mirror z -> -z). Team bases at z = +-26: 12 x 8 m alcove with 3 m walls, bot spawn door (4 m wide)
  at the back wall (z = +-30), player spawns (5) inside the alcove, weapon wall with pistol + auto + shotgun + railgun.
- Mid: a raised centre platform 8 x 8 m at y 1.5 with two ramps (along x) and the **power weapon** pedestal (orb
  launcher, respawn 60 s GUESS) at (0, 1.5, 0).
- Upper catwalks at y 3.5 m along both long walls (x = +-14, 2 m wide) from z = -20 to +20, reached by ramps at
  z = +-20; railings 1.1 m; they overlook the mid.
- Cover (per half, mirrored): low walls 1.2 m (4 x 0.4 m) at (+-6, 0, -8), (0, 0, -14), (+-10, 0, -18); tall pillars
  1.5 x 1.5 x 4 m at (+-8, 0, -4) and (+-4, 0, -20); zig-zag wall 3 m high at (-10..-4, 0, -12); SciFi TrashBin and
  Arena_Cyber_TrashBag props; neon strips (Light_Strip_Short, Light_Chevron) along wall tops in team colour.
- Bots: every 30 s (GUESS) each base door emits a wave: 4 basic (tread/bat) or 1 basic + 2-3 elites; bots walk a
  navpath through mid to the enemy base, attack players within ~15 m and anyone who shot them. Use the sci-fi AI
  prefabs visually; AI logic is ours (rr_quests.md).
- Scoring (ArenaManager): 10 per enemy player, 2 per bot, 10 per elite, -10/-2 friendly fire; 400 points or 480 s.
- Cyber Junk City (optional later): 3-level asymmetric junk town, orange-side tower with windows, beam cannon.

### 10.5 Rec Royale "Frontier Island" (16 solo / 6 x 3 squads)
Reference: `Map_Frontier_Solo_Tex` (MAIN -6018074973680880826, 1024 x 1024; saved as
`research\shoot\tex\Map_Frontier_Solo_Tex.png`): the full island with a 6 x 6 grid (lines every ~171 px) and POI
names; `Map_Frontier_Tex` (-1948765635445070417; `tex\Map_Frontier_Tex.png`) is the same island inside a lettered grid
A-F (rows, north at top) x 1-6 (columns) with legend Trail (cream), Cliff (grey), Water (blue), Bee Swarm.
- **Scale (GUESS)**: 1 grid cell = 100 m, so the texture covers 600 x 600 m and the island is ~540 m across (fits the
  2 km instance spacing). Walking 4 m/s: 2.2 min coast to coast.
- **Terrain from the texture (runtime)**: sample the solo map 1024 px -> heightfield 257 x 257 (2.34 m cells) by colour
  class: water (blue) -> -3 m (sea/lake bed, water plane at 0); sand/beach (tan near water) 0.5-1 m; trail (cream) =
  local height smoothed + flattened (paths); light green/olive meadow 2-6 m (gentle noise); dark green forest 4-12 m;
  grey cliff clusters -> raise 10-25 m (these form the ridge from Vulture Valley to Carnivore Cave and around Firelook
  Perch). Smooth with 3 passes. Lakes/rivers: the river from the west coast (row C) into Bucket Lake (centre) and the
  east river (C5-C6) are carved 2 m below surroundings. Apply the map colours as a vertex-colour/terrain splat (grass,
  dark forest, sand, trail) for a look that matches the paper map.
- **POIs** (solo map pixel -> metres from island centre (512,512), x east, z north, 0.586 m/px):
  K.REQ Tower (460,140) -> (-30, +218): radio tower on the north hill; Frontier Power (615,225) -> (+60, +168):
  power station (Dam_ElectricalUnit, ElectricalPole lines); Lonely Woods (395,250) -> (-69, +154): dense forest;
  Carnivore Cave (715,350) -> (+119, +95): big grey rock mass with a cave (Rock_Cliff/Rock_Wall ring); Creekside Camp
  (850,420) -> (+198, +54): cabins + tents by the east river; Firelook Perch (535,395) -> (+13, +69): fire lookout
  tower (procedural 15 m tower); Vulture Valley (315,415) -> (-115, +57): rocky ravine; Bucket Lake (515,520) ->
  (0, -5): central lake ~60 m; Foulball Field (160,575) -> (-206, -37): baseball field; Thimble Theater (400,615) ->
  (-66, -60): open-air amphitheatre; Glamp Camp (760,590) -> (+145, -46): glamping tents in a grid; Messy Mess Hall
  (265,735) -> (-145, -131): large dining hall (red-roof building on the map); Hillside Hike (620,715) -> (+63,-119):
  hill trail; Paintball Paradise (400,850) -> (-66, -198): a paintball field (use River-like forts + hay bales);
  Paulas Pitstop (655,850) -> (+84, -198): gas station (GasPump, Sign_FrankiesFuel re-skin GUESS). Red-brown squares
  on the map (e.g. near (730,735) and (820,690)) are extra cabins.
- **Set dressing**: RecRoyale rock prefabs (Rock_Cliff/Wall/Plateau/Stump/Tall/Cone/Wedge, section 9) placed on grey
  pixel clusters (one prefab per ~8 x 8 m of cliff, random yaw, scale 0.8-1.5); FlatTree* in dark-green areas every
  ~6 m (Poisson disc), PineTree_Dead sprinkled; Standee_Bear01 at the Visitor Center and camps; picnic tables,
  LogBench, Campfire at camps; RecRoyale_Frontier_TrashCan mesh (MAIN -2857668224676749873).
- **Loot**: ~150 coolers (procedural 0.8 x 0.5 x 0.5 m box, lid + handle, "open" by holding a hand on the handle 2 s,
  move speed x0.33 while opening) placed at POIs (8-15 each) and along trails. Each cooler yields 1 weapon (weighted by
  3.3 weights, all rarities), 1 ammo pack of that weapon's class, 1 consumable/accessory roll (EMPTY weight included).
  Loot state = bitset in an actor block (networking 5.8: 1,920 bits per block).
- **Gondola + glider**: gondola (procedural cable car 6 x 3 x 3 m) spawns all players at 150 m altitude on a random
  straight line across the island at 15 m/s; 10 s spawn state, 10 s fanfare, then doors open; 20 s to leave, 10 s
  warning, 5 s kick (manager values). Glider: forward 12 m/s, sink 3 m/s (pull back: 2 m/s, push: 6 m/s), yaw
  30 deg/s from the handle (GUESS); detach under 3 m.
- **Swarm schedule (GUESS)**: phase 0: whole island safe for 120 s; then 5 rounds of "Preparing" (next circle shown,
  45 s) + "Approaching" (shrink, 45 s): radius 270 -> 160 -> 95 -> 55 -> 25 -> 0 m; damage outside 2, 3, 5, 8, 10 HP/s
  (ignores shield). Circle centre drifts randomly but stays over land. State (centre x/z, radius, phase) goes in
  instance ints 27-58 (networking 5.6).
- **Players**: 100 health + 100 shield; downed (squads) with a DBNO bar ~30 s (GUESS) and 5 s hand-hold revive;
  winner squad teleported to the winner door area ([RecRoyale_WinnerDoor] + winner sign) for 12 s.
- Lobby ("Visitor Center"): small lodge with scoreboard, map board, lockers, "Play Again" door.

### 10.6 Showdown (optional; 3 v 3; 70 x 70 m western town)
Nothing is in the bundles; build a compact town: one main street (z) 14 m wide with boardwalks, buildings as
procedural boxes with false fronts: Bank (Sheriff spawn, vault opening into a mine tunnel, balcony), Jailhouse (Bandit
spawn, two cells, mine entrance), Station + rail track across the street (train of 3 procedural cars arrives every
90 s GUESS and stands 30 s as cover), Bar (2 floors), Sheriff's Office and General Store (closed, climbable roofs,
plank bridges between roofs), Archibald Mining Co. (mine cart), Theater (lobby: team switch, start, shooting range
with Paintball_Target_Sandbox). Mines: a U-shaped tunnel 3 m wide below the street joining Bank and Jailhouse. Props:
Hay_Bale, Wood_Pallet, WoodStack, Sandbox_Barrel, Wooden_Spool, Table_Picnic, Sarsaparilla crates/bottles (85515de1).
Guns: reuse the paint pistol / shotgun / burst rifle meshes and logic re-tuned as cork guns (projectile mesh
`Cork_Projectile`): Spinner 5 shots then 1.5 s auto reload, fast near-straight shot (speed 120 m/s, no gravity);
Blaster 2 shots x 6 corks then auto reload, range ~12 m; Repeater 3-round bursts, 12 per mag. 1 hit = out, 25 hits
or 240 s wins, respawn invincibility ~2 s.

## 11. Implementation notes (Somnium: loading, weapons, networking, input, layers)

Loading
- Open MAIN once (header only, cheap) plus its dependency reach (rr_runtime_facts: `RrReach.Bundles`), then
  `LoadAsset<GameObject>(key)` for each needed container: paintball set = 142, 09, 582, 294, 1eb, 87, 51d, c5c, ce,
  980, 89c, 09f, 7b, b2; laser tag = 61b, d2f, 7f, a3, 44, 555, 7ed9, cdc (+ sci-fi AI prefabs); Rec Royale = 142,
  447, 0bd, 40, 2a6, 582, 09, 294, 1eb, 87, 555, 44, 328, 7d, 2a5, 417, dd8, 4bd, 980, a9, 77, 0f8, 19, fc6, 71, af3,
  22a, 54, 8ac, f, f6b. Load the one-clip VO/radio bundles by their keys (7.1, 7.4).
- Instantiate under an inactive parent, strip client-affecting components (rr_runtime_facts), keep MeshRenderers,
  colliders, particle systems, TrailRenderers, Animators, AudioSources. Remove Rigidbodies from world props (or set
  kinematic). Apply the DepthNormals fix (`renderQueue = 2501` for opaque materials whose shader lacks a
  "DepthNormals" pass) and the particle material swap for VR.
- Weapon behaviour is ours: the Rec Room scripts are missing. Read the numbers from the MonoBehaviour data with our
  parser (fields listed in 3.1-3.3; the pathIds above) or hard-code the tables in this document.

Weapon runtime (our code)
- Firing: projectile simulation per shot (not physics objects): position/velocity integration with gravity
  (9.81 x gravityRatio) and drag, SphereCast of radius `colliderRadius` along each step against world layers and our
  hit volumes. Shotgun = 9 pellets through the exact pattern above; burst = shots at 12/s while held, climbing up to
  5 deg total; paint thrower = 24 blobs/s with 2.5 deg random spread + stream particles.
- Hit volumes: our own GameObjects following each remote player's Bridge `Body.Head` (sphere r 0.14 m) and a torso
  capsule from neck to hips (r 0.22 m) (somnium_player.md for the transforms); never components on the avatar.
  BodyPart 0 = head, 1 = torso.
- Paint splats: pooled decal quads (PaintballImpactDecal instances) coloured with the shooter's team colour, max ~200
  per instance, oldest recycled. Do not paint the Somnium avatar; show the KO marker and a splat puff at the hit point.
- Team identity: we must not recolour the avatar (no jerseys). Show a team-coloured ring/beacon above each player and
  colour their held gun via TeamColorRenderer targets (`_BaseColor`).
- Reload (laser): VR: grab the reload handle with the free hand and pull back 8 ticks; pistol also by rotating > 270 deg
  around its forward axis. Desktop: a key (see Input).
- Flag: carry attaches the FlagTool to the carrier's hand anchor (needs a free hand); drop on out at the carrier's
  position; touch by own team -> return; touch by enemy -> pick up; capture when the carrier enters the own FlagGoal
  radius 0.5 m (+ player radius) while the own flag is home.

Networking (maps onto somnium_networking.md sections 5.4-5.8)
- Fire (type 1) broadcast by the shooter; Hit (type 2) decided by the shooter ("favour the shooter"); the victim
  applies damage (paintball: any hit = out) and emits Death/Out (type 3); the instance authority counts scores.
- Per-player slot ints: team, status (Playing/Down), health/shield (int 18), held items (ints 8-9: item id = typeId
  index, ammo bucket), Rec Royale inventory (ints 32-47: 2 hip slots + backpack slots x weapon index + rarity + ammo
  counts per class, health/shield potions).
- Instance ints: phase + timer, team scores (int 7), CTF flag state (ints 9-10: holder PlayerId per flag, at-base /
  dropped bits; dropped position in activity-specific ints as mm x/z + cm y), Rec Royale circle (centre, radius,
  phase), gondola path seed, laser tag bot waves in actor blocks, loot bitsets in actor blocks.
- Mines/grenades: thrower owns them until they explode; explosion is an Effect event; each client checks its own
  player against the blast (radius, falloff curve) and reports its own out/damage (victim-side for splash is simplest).

Input (somnium_conventions.md 11: the client owns WASD, Q, E, Space, Shift, F, I, M, R, Tab, X, Esc, mouse buttons for
its UI)
- Desktop: fire = left mouse (Quake precedent `Q3Combat.cs:33` uses `KeyCode.Mouse0`); weapon/hand swap = digits 1-5;
  laser reload = T (R belongs to the client); throw grenade/mine = G; drop held item = H; scope zoom = C (hold);
  scoreboard = P. Never lock the cursor.
- VR: trigger = fire / arm grenade; grip = grab and hold (guns, flag, glider handle, cooler handle, teammate hand for
  revive); reload by physical pull; holster by releasing near the hip (Rec Royale); A/X free for world use only if not
  claimed by the client (B/Y and menu buttons are the client's).

Layers (somnium_conventions.md 12)
- World (maps) on 0 or 23; our hit volumes on 27 (ShootingPlayer: collides only with 3, 6, 7, 26, 31, never the avatar
  capsule); if physics bullets are ever used, put them on 26 (Bullet). Pickable guns/flags/potions on 10 or 16.
  Rec Room prefabs arrive on layers 10/0/2/5/17: always re-layer.

## 12. Open questions / guesses
1. BodyPart enum values other than 0 (head) are unknown (no enum in plain data); only head vs torso matters for us.
2. Laser Tag player max health (100 assumed) and respawn time (5 s assumed, as paintball) are not in plain data (no
   config asset on the Laser Tag wrapper).
3. Rec Royale world scale, swarm timings/damage, glider speeds, DBNO duration, loot box count are GUESSES; the island
   layout comes from the in-game map textures only.
4. `simpleBurstModeBurstSize 16` vs the wiki's "bursts of up to 5": the burst rifle uses burstMode 0 (curve-based,
   GUESS); implement "12 shots/s while held, 5 deg climb over a burst".
5. Taxonomy field meanings (load, speed, 9/10) are inferred; damage, weights, magazine sizes and head multipliers are
   solid (they match the prefabs).
6. Bullet bounce rules (`maxReflections 1`, `minWallSlopeDegrees 30`, `destroyOnMask 150`) are only partially
   understood; splat-on-hit is the safe default.
7. Map layouts (River, Drive-In, Spillway, Hangar, Showdown town) are reconstructions: no geometry of the originals is
   available. Fandom pages have no per-map layout text and fandom blocks WebFetch (use curl to the MediaWiki API as
   done here); YouTube map tours would refine them.
8. Hangar power weapon respawn time and bot wave interval (30 s) are GUESSES.
