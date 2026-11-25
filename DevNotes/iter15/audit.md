# Iter 15 - "finish this Dreamport off" audit (read-only, 2026-10-17)

Status log (append-only; resume from the last section written)
- started: registry hooks grepped, README Iterations read.

Note: other iter15 agents are active at the same time (DevNotes/iter15/discgolf.md, stuntrally.md, avatar.md,
makerrooms_review.md). Findings below describe the code as of this audit, before their work lands.

## 1. Registry hooks (Rooms/RrRegistry.cs partial methods)

Implemented (16): RegisterDorm (Places/Dorm/RrRegistryDorm.cs), RegisterRecCenter (Places/RecCenter/RrRegistryRecCenter.cs),
RegisterPaintball, RegisterLaserTag, RegisterGoldenTrophy, RegisterCrimsonCauldron, RegisterLostSkulls, RegisterJumbotron,
RegisterCrescendo, RegisterDodgeball, RegisterPaddleball, RegisterSoccer, RegisterBowling, RegisterCharades,
RegisterMakerRoom (Places/MakerRooms/RrRegistryMakerRooms.cs), RegisterInfrastructure (placeholder Dorm/RecCenter only if missing).

NOT implemented -> compile away, activity unreachable (8): RegisterRecRoyale, RegisterShowdown, RegisterPunchUp,
RegisterDiscGolf, RegisterStuntRunner, RegisterRecRally, RegisterMidnight, RegisterLittleMonsters.
(Enum Rooms/RrActivity.cs has 20 game ids 3..22 + Dorm, RecCenter, MakerRoom=23; 12 of 20 games are registered.)

## 1b. Per activity / place (README = Assets/#User/SomniumRecRoom/README.md)

Legend: BUILT = registered + playable loop; NOT BUILT = hook empty (unreachable; the Rec Center door shows COMING SOON,
the watch Play page does not list it at all because it lists RrRooms.All only).

| # | Activity | State | Hook / code | Listed missing, guessed or untested (source) |
|---|---|---|---|---|
| 1 | Dorm | BUILT (final 2018-kit dorm, iter 3/4) | RegisterDorm, Places/Dorm | Basket not scored in the dorm (RrBall); no RrSeat (armchairs not sittable); VR ladder/poke untested; not networked by design (ink local) (README "Dorm Room" Untested) |
| 2 | Rec Center | BUILT: Rec Room's own cached July-2025 building (iter 6) or our rebuild | RegisterRecCenter, Places/RecCenter | Not in the cache: curved upper ring, mural lounge, FEATURED ROOMS double doors, orange corridor, dartboard; Magic Door spot GUESS; rebuild fallback: projector room, office windows, inner curtains not built, wall paints GUESS; cache path untested in client (2021.3 bundle load, reflection write, ~5,800 missing-script warnings per load, mixed instances cached/not, LZMA caches fall back) (README iter 6 "Still open"); rebuild has no light grid (iter9/light9.md) |
| 3 | 3D Charades | BUILT (Ink Space) | RegisterCharades | GUESS: 4 s intermission, SKIP rule, performer confirms, 12-colour palette; MinPlayers 2; pen has no shapes/redo/grab (research); two-client ink/resend untested (README Charades "Not verified") |
| 4 | Bowling | BUILT basic (level91) | RegisterBowling | Practice lanes local only; screen roll has no charge; pin clatter clip GUESS; red panels over pin decks; playable pool table (2020) not built; full 10-frame game never played (iter13/sports.md, wave1_result.md) |
| 5 | Crescendo of the Blood Moon | BUILT basic (levels 79-89) | RegisterCrescendo | Carriage does not move; whip, secret shelves, most traps, chest loot, gift shop missing; Dracula = scaled Bellhop, mirrors self-aim; silver values and combat music GUESS; "(Whip)" may not spawn (iter13/crescendo.md OPEN) |
| 6 | Disc Golf | NOT BUILT (iter15 agent started, DevNotes/iter15/discgolf.md) | RegisterDiscGolf empty | Data ready: refdata/apk DiscGolf.rrsk (level9 Lake, 42% art), DiscGolf_Propulsion.rrsk (level10) |
| 7 | Dodgeball | BUILT (our own procedural gym) | RegisterDodgeball | 2026 mods (Low Gravity, Dodge-Bombs, Disco) not built; barrier size GUESS; gym hand-built although level11 Dodgeball.rrsk exists; no light grid; real catch / 1v1 / bench untested (README iter4 Dodgeball) |
| 8 | Isle of Lost Skulls | BUILT basic (levels 62-70) | RegisterLostSkulls | No same-act checkpoints (wipe ends run; final rules have them every act); gates open, no mines/puzzles/caves darkness/shop/gold cap; Ghost Beard our shape; 40,858 objects (perf risk); rank thresholds unpublished (iter13/isle.md, wave1_result.md) |
| 9 | Laser Tag | BUILT basic (Hangar, Cyber Junk City) | RegisterLaserTag | No bots (Rec Room filled matches with bots); no team radio, per-player tag counts, +10 notices, dual wield; power-weapon spots, 100 HP, 5 s respawn GUESS; CJC build time in client unmeasured vs 90 s travel timeout (iter13/lasertag.md) |
| 10 | Make It To Midnight | NOT BUILT | RegisterMidnight empty | No Android level; user approved measuring cache 35C.data (reference-folders memory); research rr_latest_rgames_story.md s2 + rr_party.md |
| 11 | My Little Monsters | NOT BUILT | RegisterLittleMonsters empty | No Android level; Splootybean archive has its Studio bundles (ask before download) |
| 12 | Paddleball | BUILT (our own tube hall) | RegisterPaddleball | Rec Room was 1v1 VR-only (2v2 + screen mode are extras); music GUESS; hall hand-built although level13 Paddleball.rrsk exists; no light grid; VR face hits untested |
| 13 | Paintball | BUILT (2025 lobby + all 7 maps, iter 7/8) | RegisterPaintball | Not built: Practice Arena 1v1 duel (Challenge button just starts the match), skins shop, mods, beacons, post-match page, Splat Hammer 7 m burst, scope camera, out-of-bounds timer; shield plain grabbable; mines only trip on remotes; others don't see grenade flight; votes last-press-wins; Merry Midway block town, Spillway dam boxes; tyre stacks draw twice; locker room dark (README iter7/8 open/known) |
| 14 | Punch Up | NOT BUILT; never released by Rec Room (Easter egg only) | RegisterPunchUp empty | Candidate to drop from the FEATURED ROOMS list instead of building (rr_latest_rgames_story.md s4) |
| 15 | Quest for the Golden Trophy | BUILT (Android levels 19-29, iter 8) | RegisterGoldenTrophy | Foyer store teleport pads do nothing; Roomie not built; door pad moves the team when ANY player steps on (RR needed all); rooms dark blue-grey / flat grey floors; mod numbers GUESS; coconut catching not built; networked mods untested (README iter4/iter8 Open) |
| 16 | Rec Rally | NOT BUILT (iter15 agent, DevNotes/iter15/stuntrally.md) | RegisterRecRally empty | Data: RecRally_Chaparral.rrsk (level107, 42.8% art, 1.4 MB, 1705 mesh colliders); min players 4 (2025) |
| 17 | Rec Royale | NOT BUILT | RegisterRecRoyale empty | No Android level (UGC/Addressables room); RR needed 14/18 players -> threshold decision; post profile already in RrPostFxProfiles |
| 18 | Showdown | NOT BUILT | RegisterShowdown empty | No Android level; Splootybean Studio bundles (ask before download); 3v3, 25 hits, 4 min (research) |
| 19 | Soccer | BUILT basic (level72) | RegisterSoccer | Speed-boost orbs missing; kick speeds and music GUESS; goal-box barrier look; a full 180 s match never run (iter13/sports.md) |
| 20 | Stunt Runner | NOT BUILT yet (iter15 agent writing Data/StuntRunner_*.bytes now; 7 files not yet in the scene's data list) | RegisterStuntRunner empty | Data: levels 92-98 (91-97% install art) |
| 21 | The Curse of the Crimson Cauldron | BUILT basic (levels 50-60) | RegisterCrimsonCauldron | Witch's bat-the-orb mechanic, explosive barrels, real bog monster / Imp Witch missing; kill gold and steam timing GUESS; no gtscene float pass (floating props possible); act 1 over-saturated (iter13/crimson.md) |
| 22 | The Rise of Jumbotron | BUILT (levels 38-48, iter 8-13) | RegisterJumbotron | Credits paid on kill (not pickup); no Space low gravity / muffle; no Flight Deck film; Jumbotron's face ours; flat colours (Android-only textures); sawtooth edges, brightness gaps; acts 8/9 frame rate unmeasured (README iter8 Known gaps, iter11/jt11_result.md) |
| - | Watch | BUILT: Home (7 tiles), Play, People + Join, This Room, Settings, Create, Rooms, Room Settings, keyboard | Watch/* | Settings not persisted between sessions (TODO, Watch/RrWatchSettingsPage.cs:14); no party UI; Play page hides unbuilt activities; VR wrist fit never confirmed in the notes |
| - | Maker Pen | BUILT (iter 13 drawing + iter 14 build tools) | Interact/MakerPen | A/X undo = client jump/inspector clash; keys 1-4 / 1-8 overlap weapon slots; thumbstick resize also moves the player; palette colours GUESS (16 / 32 of 60); no Text creation in VR; erase own strokes only; remote drawing silent (iter13/makerpen.md, iter14/makerrooms_tools.md) |
| - | Maker rooms | BUILT, never run (iter 14); iter15 review in progress (iter15/makerrooms_review.md) | RegisterMakerRoom, Places/MakerRooms | Physical/Sticky/Grabbable stored not simulated; 25 surface materials not drawn; no restore-version UI; no RrNet.SendToPlayerGlobal (global events + target id); no RrRoomInfo.Unlisted, so "Maker Rooms" shows on the Dorm board / Play page and the Magic Door can roll it (RrCategoryDoor.MagicCandidates has no MakerRoom exclusion); MAKERWORLD door still "coming soon" although public maker rooms exist; Park build vs 90 s timeout; Maker Room window wall side GUESS; desktop typing via Input.inputString may leak keys to the client (iter14/makerrooms_core.md Open) |
| - | Mirrors | BUILT (Thad's mirror, iter 12): Dorm, Rec Center theatre, Jumbotron Armory | Core/RrMirror, ThirdParty/ThadMirror | Per-eye frame cost unmeasured; 1 CS0618 warning (Mirror.cs); Thad's far-clip TODO (Mirror.cs:309); hallway mirror gone with the cached building |

Status: section 1 written.

## 2. Cross-cutting gaps vs the original goals and what Rec Room players expect

Original goals ("start in the Dorm, go to the Rec Center, have the watch menu, play all 21 official activities, fake the
master, isolate players in different places"):

| Goal | State | Gap (file) |
|---|---|---|
| Start in the Dorm | DONE (arrival -> Dorm, README Workflow 1) | Only whitelisted accounts get anything: `Core/RrAccess.cs` Whitelist = { "Shodah" }; everyone else sees "This world is private for now." Opening the world is an explicit release decision (empty the list). |
| Go to the Rec Center | DONE (cached real building or rebuild) | Cached path never confirmed in the client (README iter 6 "Still open"); mixed cached/uncached instances untested; rebuild unlit by the light grid. |
| Watch menu | DONE | Settings session-only (Watch/RrWatchSettingsPage.cs:14 TODO); VR wrist placement and sleeve fit never confirmed in a headset in the notes. |
| All 21 official activities | HALF: 12 of 20 enum games registered (Punch Up was never released, so 19 real) | Missing: Disc Golf, Stunt Runner, Rec Rally (iter15 agents working), Rec Royale, Showdown (no Android level), Make It To Midnight, My Little Monsters (no Android level), Punch Up (N/A). Rooms/RrRegistry.cs empty hooks. |
| Fake the master (per-instance authority) | BUILT, offline only | Net/RrElection.cs etc.: 30/30 checks in a simulated 3-client room; README "Where it stands" says never run with two real clients. No DevNotes record of a two-client session since. |
| Isolate players in different places | BUILT, harness only | Player/RrIsolation.cs: tested with a fake remote; two-client checklist (README "Players in other places") never reported back. Known limit: client avatar culling (nearest 8 within 15 m) counts hidden players at the origin -> with >8 players friends may get dummy avatars (cannot fix without the client). Voice level exactly 0 lost on restore; admin item mute undone on restore. |

What players expect from Rec Room:

| Feature | State | Gap (file) |
|---|---|---|
| Rec Center category doors listing everything | PARTIAL | Places/RecCenter/RrCategoryDoor.cs: SPORTS & PVP 6 of 9 built (Rec Royale, Showdown, Disc Golf COMING SOON); QUESTS 5/5; FEATURED ROOMS 1 of 6 (only Charades; Punch Up never existed); MAKERWORLD and CAMPUS JAMS return NoneList ("coming soon") although iter 14 maker rooms exist - MAKERWORLD should list public maker rooms. Magic Door's MagicCandidates() does not exclude RrActivity.MakerRoom. |
| Room lists (watch Play, Dorm Play board, Rooms page) | DONE | Play page lists only registered rooms (unbuilt ones invisible, unlike the doors); "Maker Rooms" appears as an activity card everywhere (no RrRoomInfo.Unlisted; iter14/makerrooms_core.md request). |
| People page / join a friend | DONE (Watch/RrWatchPeoplePage.cs: In this room / Elsewhere, Join; Dorms private) | No friends filter, no "follow"; private maker rooms refuse non-owners and fall back to the Dorm. |
| Party (travel together) | MISSING | Net/RrPresenceWire.cs reserves presence int 7 "party leader" (AtParty, PartyLeader()) but nothing outside the wire reads or writes it: no party invite, no group travel. Quests rely on players walking into the same instance. |
| Settings | PARTIAL | Audio (3 sliders), Look (Brightness, Warmth), Watch (open by look/tap, wrist, seconds). Not persisted; no comfort/hand-dominance for tools; client's Bloom Off does not reach our Volume (README iter 5 Known limits). |
| Desktop + VR controls | BUILT, conflicts open | Screen keys E/Q/Z/V/H + left click; Maker Pen 1-8, Z/Y, C, T, G, [ ] , . F5. Conflicts: A/X undo = client jump/inspector (iter13/makerpen.md, iter14 tools); keys 1-4 overlap weapon slots in games; thumbstick resize moves the player; screen FOV unknown so some held items sit low/off-screen (README "Held items" Known gaps); no two-handed hold; pool cue default pose. |
| Loading cards | DONE | Rooms/RrLoadingScreen.cs ("GOING TO"); 90 s build timeout (Rooms/RrTravel.cs:36) vs untested client build times (Cyber Junk City, Park, Isle). |
| Music | DONE | Rooms carry Rec Room tracks + radios; several picks GUESS (Paddleball SportFunk, Soccer, Crimson/Crescendo combat). |
| Sitting / seats | MISSING | No RrSeat anywhere (README Dorm Untested). |
| Rec Room avatar option | IN PROGRESS | DevNotes/iter15/avatar.md (opt-in, Somnium avatar stays default). |
| Lighting consistency | PARTIAL | Light grid missing in Dodgeball, Paddleball and the Rec Center rebuild (iter9/light9.md Open); Golden Trophy rooms "dark blue-grey" (README iter 8 Open). |
| Two-client / headset validation | MISSING | Every section's "Not verified" lists two-client sync and VR; iter 13 and 14 never run in the client per the notes. |
| Net capacity | RISK | Editor/RrNetSceneSetup.cs: 50 players, 24 instance objects, 12 actor blocks, 48 prop objects for the WHOLE world; Rec Center alone leases 16 props, each quest uses actor blocks 0-2. Many simultaneous games/maker rooms (each its own instance) could exhaust leases; never load-tested. |

Status: section 2 written.

## 3. Open issues, TODO, GUESS, "not built" (deduplicated; P1 breaks play, P2 visible gap, P3 polish)

Sources: README = Assets/#User/SomniumRecRoom/README.md; DN = DevNotes.

### P1 - breaks play (or very likely to, and never verified)
1. Nothing since iteration 2 has a recorded two-client session: authority election / take-over, instance state, prop
   leases, quest actor sync, Charades ink, Paintball flags, Maker Pen ink, maker-room transfer (README "Where it stands"
   Networking/Isolation rows; every game's "Not verified"; DN/iter13/wave1_result.md; DN/iter14/makerrooms_core.md).
2. Iteration 13 games and iteration 14 maker rooms never run in the Somnium client (DN/iter14/makerrooms_core.md "Not
   run anywhere"; iter13 builders "not play-tested"; harness only). Maker-room review in progress (DN/iter15/makerrooms_review.md).
3. Build-time vs the 90 s travel timeout in the client (Rooms/RrTravel.cs:36): Cyber Junk City 4.7 s and Isle 5.9 s in
   the editor, the Park (61k plates + terrain) never measured; a timeout drops the player out (DN/iter13/lasertag.md,
   wave1_result.md, DN/iter14/makerrooms_core.md).
4. Isle of Lost Skulls has no same-act checkpoint: a team wipe ends the whole run, against the final rules
   (Games/Quests/Shared/RrQuestRoom.cs all-down branch; DN/iter13/isle.md shared request 2, wave1_result.md Open 1).
5. Quest return to the lobby after a victory screen never observed (DN/iter13/wave1_result.md Open 2) - all five quests use it.
6. Whitelist = { "Shodah" } (Core/RrAccess.cs:32): nobody else can play until it is emptied - required for "finished".
7. Isle performance: 40,858 objects in the room, no headset measurement (DN/iter13/wave1_result.md).
8. 7 new Data/StuntRunner_*.bytes are not in the scene's RrMain data list yet (bankscene.sh not rerun); without it the
   level builds a stand-in (iter15 in progress; check before upload).

### P2 - visible gaps
9. 8 activities unbuilt (empty hooks in Rooms/RrRegistry.cs): Disc Golf, Stunt Runner, Rec Rally (iter15 agents), Rec
   Royale, Showdown, Make It To Midnight, My Little Monsters, Punch Up (never released - drop from FEATURED ROOMS?).
10. MAKERWORLD / CAMPUS JAMS doors say "coming soon" while maker rooms exist (Places/RecCenter/RrCategoryDoor.cs Activities()).
11. No RrRoomInfo.Unlisted: "Maker Rooms" listed as an activity on the Play page / Dorm board, Magic Door can roll it
    (RrCategoryDoor.MagicCandidates; DN/iter14/makerrooms_core.md request).
12. No party / group travel: presence int 7 reserved, unused (Net/RrPresenceWire.cs:20,55,159).
13. Laser Tag has no bots (Rec Room filled matches with tread/bat bots); no team radio, tag counts, +10 notices, dual wield (DN/iter13/lasertag.md).
14. Crimson Cauldron: witch's bat-the-orb mechanic, explosive barrels, real bog monster / Imp Witch (DN/iter13/crimson.md).
15. Crescendo: carriage motion, whip, secret shelves, traps, chest loot, gift shop; Dracula stand-in body (DN/iter13/crescendo.md).
16. Isle: gates open, mines, puzzles, cave darkness, shop, gold cap; Ghost Beard our shape (DN/iter13/isle.md).
17. Paintball: Practice Arena 1v1, skins shop, mods, beacons, post-match page, Splat Hammer burst, scope, out-of-bounds
    timer; mines trip only remotes; remote grenade flight invisible (README iter 7 "Not verified / open").
18. Dodgeball and Paddleball are hand-built rooms, not Rec Room's levels (level11/level13 in DN/refdata/apk exist); no
    light grid there or in the Rec Center rebuild (DN/iter9/light9.md Open); Dodgeball 2026 mods missing.
19. Golden Trophy: rooms dark blue-grey / flat grey floors; store pads dead; Roomie; any-player door pad (README iter 8 Open).
20. Jumbotron: credits on kill, no Space low-gravity, no Flight Deck film, own face, sawtooth edges / brightness gaps (README iter 8, DN/iter11/jt11_result.md).
21. Soccer speed-boost orbs; Bowling playable pool table; Bowling practice lanes local only (DN/iter13/sports.md).
22. Maker rooms: physics/sticky/grabbable not simulated, 25 materials not drawn, no version restore UI, no cross-instance
    direct send (DN/iter14/makerrooms_core.md Open).
23. Rec Center cached path: ~5,800 missing-script warnings per load, mixed cached/uncached instances, LZMA fallback (README iter 6 Still open).
24. Control clashes: A/X undo vs client jump/inspector; 1-4/1-8 vs weapon slots; thumbstick resize moves the player (DN/iter13/makerpen.md, DN/iter14/makerrooms_tools.md).
25. Client avatar culling (8 nearest within 15 m) counts hidden players -> friends may get dummy avatars with >8 players (README Isolation Known limits; client-side).
26. Settings not persisted (Watch/RrWatchSettingsPage.cs:14 TODO).
27. No seats (no RrSeat) - armchairs, couches, benches decorative (README Dorm Untested).
28. Net capacity: 24 instance objects, 12 actor blocks (3 per quest -> 4 concurrent quests), 48 prop objects for the
    whole world (Editor/RrNetSceneSetup.cs:44-47) - never load-tested.
29. Crimson levels have no gtscene float pass (floating props possible) (DN/iter13/crimson.md).

### P3 - polish / GUESS
30. Rules/values marked GUESS: Laser Tag 100 HP / 5 s respawn / power-weapon spots; Soccer kick speeds; Bowling clatter;
    Crimson kill gold + steam; Crescendo silver/Dracula/combat music; Charades intermission/SKIP/palette; Paddleball music;
    quest mod numbers; quest 30 s bleed-out; Dodgeball barrier size; Merry Midway hammer spots (README + DN/iter13/*).
31. Look items: Crimson act 1 saturation, Soccer goal-box barriers, Bowling red pin-deck panels, Hangar lobby scoreboard
    behind console, Merry Midway block town, Spillway dam, tyre stacks double-draw, Paintball locker room dark (wave1_result.md, README iter 7/8).
32. Screen-mode FOV unknown: bottle cut off, paddle off-screen; no two-handed hold; pool cue default pose (README Held items Known gaps).
33. Maker Pen / tools palettes: 16 / 32 GUESS colours of Rec Room's 60; prop pairings guessed; no VR text (DN/iter14/makerrooms_tools.md).
34. Additional-light shadows not reachable; AO only when the player enables HBAO (DN/iter9/light9.md).
35. Our LDR glow softens under Neutral tonemapping; client Bloom Off ignored (README iter 5 Known limits).
36. Mirrors' per-eye cost unmeasured; Thad far-clip TODO (ThirdParty/ThadMirror/Mirror.cs:309); CS0618 warning.
37. Jumbotron: Departure drifting glow sticks removed (may have been intended zero-g) (DN/iter13/jumbotron.md).
38. 25 *Test.cs harness-hook files (377 KB source) ship inside the world assembly.
39. Games/Shared/RrEnemy.cs:55 "TODO(Net): when RrNetActors exists ..." - stale comment (RrNetActors exists).
40. README "Where it stands" still says Isolation / Networking "not yet tried with two real clients" and "Next: ... Rec
    Royale, Showdown ..." - refresh when iteration 15 lands; RrVersion still 14.

Status: section 3 written.

## 4. Data / size health

- Data/*.bytes: 44 files, 14,768,929 bytes (14.1 MiB). 37 are banked in the scene (14.39 MB); the 7 StuntRunner_*
  (378,857 bytes, written 2026-10-17 20:18 by the iter15 agent) are NOT yet referenced by "Assets/Rec Room Scene.unity"
  (guid check) -> rerun DevNotes/tools/rrscene/bankscene.sh before upload. Largest: LaserTag_CyberjunkCity 2.30 MB,
  Maker_Park 1.52 MB, LaserTag_Hangar 0.79 MB.
- Generated C# strings: 30 RrScene_*.cs files hold 5,034,776 literal chars (Jumbotron 11 files ~3.06M, Paintball 8
  ~1.09M, Golden Trophy 11 ~0.73M); Core/RrEnvironmentTable.cs another 204,742. All code: 5,397,423 unique literal
  chars in 9,345 strings -> estimated #US heap 10.3 MB of 16 MiB (64.5%), i.e. ~5.4M of the ~8.3M char budget
  (DN/iter4/kit.md CS8103 limit). Headroom ~2.9M chars. (Estimate: scratchpad perl script; agrees with iter 13's measured 10.66 MB.)
- Risks:
  - Every new level must stay in Data/*.bytes; a new RrEnvironmentTable regeneration adds ~3 KB of strings per scene
    (iter15 Disc Golf / Stunt Runner / Rec Rally rows) - fine, but the 5.0M chars of RrScene_*.cs could be moved to
    Data/*.bytes (rrsk2bytes) to free the heap if more is needed.
  - World upload: ~14.8 MB of TextAssets in the scene + an assembly with ~10.3 MB of user strings and 364 runtime .cs
    files (10.6 MB source). Iteration 8 flagged "if the upload server rejects it"; no rejection is recorded in the notes
    (iteration 13 shipped 12.8 MB of data); +2 MB since (Park, Performance Hall, Stunt Runner) - the first upload of
    iteration 15 is the real size test.
  - Namespaces: every runtime file is under SomniumSpace.Worlds.SomniumRecRoom (grep clean); asmdef references only the
    server's allowed assemblies.
  - Scratch tools (compilecheck, harness) must be run from DevNotes/tools copies (scratch-wipe memory).

Status: section 4 written.

## 5. Top 25, prioritised

1. P1 Run a real two-client session: election, take-over, isolation and voice, prop leases, one game per framework (README Networking/Isolation rows).
2. P1 First in-client run of the iter 13 games and iter 14 maker rooms (create, save, PUBLIC ROOMS join, offline copy) - never run anywhere.
3. P1 Rerun bankscene.sh for Data/StuntRunner_*.bytes (and any iter15 Disc Golf / Rec Rally data) before upload, or those levels build stand-ins.
4. P1 Measure build times in the client vs the 90 s travel timeout: Cyber Junk City, Isle, Park (Rooms/RrTravel.cs:36).
5. P1 Isle same-act checkpoints (Games/Quests/Shared/RrQuestRoom.cs all-down branch + client re-entry; DN/iter13/isle.md).
6. P1 Verify the quest victory -> lobby return for all five quests (DN/iter13/wave1_result.md Open 2).
7. P1 Decide release: empty Core/RrAccess.cs Whitelist (only "Shodah" can play now).
8. P1 Isle performance (40,858 objects) in the headset; thin further if the fps drops.
9. P2 Finish Disc Golf, Stunt Runner, Rec Rally (iter15 agents in progress; data in DN/refdata/apk).
10. P2 Build Make It To Midnight (cache 35C.data approved for measuring) and My Little Monsters / Showdown (Splootybean bundles, ask before download).
11. P2 Rec Royale: no level anywhere; needs a design call (player threshold 14/18, map source) or an explicit "not in Dreamport".
12. P2 Punch Up: never released - remove from the FEATURED ROOMS list (RrCategoryDoor.FeaturedList) instead of building.
13. P2 MAKERWORLD door should list public maker rooms (RrCategoryDoor.Activities returns NoneList).
14. P2 Add RrRoomInfo.Unlisted; keep MakerRoom out of the Play page, Dorm board and Magic Door (RrCategoryDoor.MagicCandidates).
15. P2 Party / group travel: presence int 7 is reserved but unused (Net/RrPresenceWire.cs) - no way to take friends into a quest together.
16. P2 Laser Tag bots, tag counts and +10 notices (DN/iter13/lasertag.md).
17. P2 Quest boss mechanics: Crimson witch orb-batting and barrels, Crescendo carriage/whip/traps, Isle gates/mines/puzzles.
18. P2 Paintball Practice Arena 1v1, mods, post-match page, Splat Hammer burst (README iter 7 open).
19. P2 Rebuild Dodgeball and Paddleball from levels 11/13 (DN/refdata/apk) and give them, plus the Rec Center rebuild, a light grid.
20. P2 Golden Trophy's dark blue-grey rooms / flat floors, store pads, any-player door pad (README iter 8 Open).
21. P2 Control clashes: A/X undo vs client jump, number keys vs weapon slots, thumbstick resize moving the player.
22. P2 Maker rooms: simulate Physical/Grabbable, draw the 25 materials, version-restore UI, RrNet.SendToPlayerGlobal.
23. P2 Persist watch Settings (Watch/RrWatchSettingsPage.cs:14 TODO) and add seats (no RrSeat).
24. P2 Net capacity check: 24 instances / 12 actor blocks / 48 props world-wide (Editor/RrNetSceneSetup.cs) under a busy world.
25. P3 GUESS values and look polish (section 3 items 30-37); refresh README "Where it stands", bump RrVersion to 15, and consider moving RrScene_*.cs (5.0M chars) to Data/*.bytes if the string heap (64.5% used) is needed.

Status: DONE (audit complete, 2026-10-17).
