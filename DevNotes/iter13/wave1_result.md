# Iteration 13, wave 1: integration result (2026-09-26)

Integrator notes and step log: `DevNotes/iter13/integrate.md`. Builder notes: `DevNotes/iter13/{lasertag,sports,crimson,isle,crescendo,makerpen,jumbotron}.md`.
Shots: `DevNotes/iter13/screens/` (29 from the tour `w1_*`, 2 from the re-run `w1fix_*`).

## What the integrator did

- **Shared requests applied:**
  - `Games/Shared/RrGameEvents.cs` header: Maker Pen event ids 20-23 reserved.
  - `Games/Quests/Shared/RrQuestRoom.cs`: `virtual LavaToast`. Crimson overrides it, so the Bog says "Swallowed by the bog!".
  - `Core/RrEnvironmentTable.cs` regenerated with apkscene envcs: 69 -> 91 scenes, only additions (11 Crimson_*, 11 Crescendo_*).
  - `Core/RrEnvironment.KeyFor`: CrimsonCauldron -> Crimson_Foyer, Crescendo -> Crescendo_Foyer.
  - README: the Places row, the iteration 13 table row and checklist, and an "Iteration 13" section with one paragraph per game plus the Maker Pen.
- **Not applied:** Isle's same-act checkpoints (`RrQuestRoom` all-down branch). Clients would also need a re-entry into the SAME act, so it is not a small change. A team wipe on the Isle still ends the run.
- **Reachability:** nothing to change. `RrCategoryDoor` already lists Laser Tag, Soccer and Bowling under SPORTS & PVP and Crimson, Isle and Crescendo under QUESTS. The watch's Play page lists `RrRooms.All`, and all six registries have a Create. The Dorm's room board in `w1_pen_dorm_strokes.png` already shows Laser Tag and The Curse of the Crimson Cauldron.
- `bankscene.sh` was run once: 35 `Data/*.bytes` (12.8 MB) are in RrMain's `_sceneData`, and the rest of the scene is unchanged (`_shaderCarriers` intact). The run happened before the user opened the project in Unity.
- `.meta` files were added for `Games/Soccer`, `Games/Bowling` and their 10 scripts. `RrVersion` is now 13.
- **Harness:** a new shot frame `"player"` in `RrScenarioRun.cs` (SP harness), for the Maker Pen shots.
- Compile: 0 errors (server-like 342 files, API guard 118/118, editor builds 0). The one warning is ThirdParty ThadMirror's.

## Harness runs

1. `wave1_tour` (149 steps, 490 s, saved world scene): **0 step failures.**
   - One error kind (x172): `CollisionMeshData couldn't be created ... Quest_Rock01`, from mesh colliders on non-readable install meshes in the Crimson levels.
   - Isle: Ghost Beard's third hit never came.
   - Crescendo: 24 "could not spawn ... for a rack" warnings.
2. `wave1_fix` (32 steps, re-run after 3 fixes): **0 step failures, all three fixed.**
   - 0 CollisionMeshData errors. Quest_Rock01 and Quest_Rock04 are now box colliders.
   - Ghost Beard 3/3, then Isle VICTORY.
   - Crescendo builds 11 racks with no warnings.

Fixes (small, targeted):
- `Games/Quests/LostSkulls/RrLostSkullsCannon.cs`: Arc 45 -> 55 degrees. From the muzzle, Fight Position 3 is 45.6 degrees off both boss cannons. Positions 1 and 2 stay 70-75 degrees off the other cannon, so the rear, front, either order holds.
- `Core/RrSceneKit.cs` ColliderPhase: a non-readable mesh gets its bounds as a box collider (WarnOnce), instead of a Unity error and no collider.
- `Games/Quests/Crescendo/RrCrescendoActs.cs` Weapons(): only the install's `(Name)` prefabs. Scene objects such as `[Quest_Dracula1_Plate] (9)`, `[Quest_Dracula1_MirrorCannon]`, `[Quest_Pirate1_Torch] (1)` and `Crossbow Trap (1)` are skipped.

Build times in the harness editor (RTX 3080 Ti, from the travel log; the headset client will be slower):

| Room | built | objects in the room | data |
| --- | --- | --- | --- |
| Crimson Cauldron (all 11 levels) | 5.4 s | 13,246 | 3.04 MB |
| Isle of Lost Skulls (9 levels) | 5.9 s | **40,858** | 3.08 MB |
| Crescendo (11 levels) | 5.5 s | 15,589 | 2.89 MB |
| Laser Tag Hangar / Cyber Junk City | 1.4 s / 4.7 s (switch) | 1,047 / - | 3.09 MB |
| Soccer | 0.3 s | 237 | 0.45 MB |
| Bowling | 0.8 s | 1,444 | 0.24 MB |

## Per game

### Laser Tag: **basic form works**
- **Works:**
  - The Hangar lobby has its hologram (MAP: HANGAR, CHANGE MAP, START) and a scoreboard.
  - START leads to the countdown, then a spawn in the Blue base with the laser pistol. A base scoreboard shows 7:57 (`w1_lt_hangar_play`).
  - A pickup can be taken. The grenade blast hurts its thrower (self-destruct -10, clamped at 0).
  - The timer end gives "match over: tie", then the lobby.
  - The map switch builds Cyber Junk City in 4.7 s (166,722 plates), then frees the Hangar. Its lobby console is at the level's projector (`w1_lt_cyberjunk_lobby`).
- **Missing:** bots, walkie-talkie radio, per-player tag counts and "+10" notices, dual wield.
- **Risks:**
  - The power-weapon spots and respawn times are guesses.
  - Cyber Junk City's build time in the headset client is unmeasured (4.7 s here; the travel limit is 90 s).
  - The lobby scoreboard stands partly behind the Hangar console (cosmetic).

### Soccer: **basic form works**
- **Works:**
  - The level72 hall is built. The console reads PRACTICE with 1 player ("1 more player needed").
  - START gives the countdown, then a kick-off drop from 5.18 m. A test kick at 20 m/s scores "goal for Blue" (1-0).
- **Missing:** the speed-boost orbs. A full 180 s match end was not run (it uses the same RrGameRoom MatchOver path as Laser Tag).
- **Risks:**
  - Kick speeds, the music and which team the goal-box blocker applies to are guesses.
  - The red translucent goal-box barriers show up large when standing in them (`w1_soccer_field`); check them in VR.

### Bowling: **basic form works**
- **Works:**
  - All 3 lanes have their pins from the level's spots (10/10/10), and the pins measure 0.548 m.
  - The console reads "10 FRAMES - HIGHEST SCORE WINS".
  - START puts Player on frame 1. A test roll at 6.8 m/s takes 10 pins down: STRIKE. The pins settle in 5 s, the pinsetter resets them, and frame 2 follows (`w1_bowling_pins`).
- **Missing:** a whole 10-frame game was not played in the harness (the scoring offline test passed 300 / 150 / 167). Screen-mode rolls have no charge.
- **Risks:**
  - The practice lanes are local-only.
  - Red upright panels hang above each pin deck (level geometry, cosmetic).
  - The pin clatter clip is a guess.

### The Curse of the Crimson Cauldron: **basic form works end to end**
- **Works:**
  - Foyer lobby with the START QUEST hologram and the practice weapons (`w1_cc_foyer`, `w1_cc_console`).
  - Console START leads to the Armory (12 weapons at Rec Room's poses), then act 1. Act 1 is fought to CLEAR: 4 waves, 11 kills.
  - The Bog is reached, and its lighting switches.
  - The Finale: the Crimson Witch appears (3150 HP). KillBoss gives VICTORY (27,800 gold, rating C), and 4 reward chests open (`w1_cc_boss`).
- **Missing:**
  - The witch's bat-the-orb-back mechanic (she takes normal hits).
  - The explosive barrels.
  - The floater pass on its levels.
- **Risks:**
  - Act 1 (Castle Courtyard) is very saturated orange, with blocky foliage cards (`w1_cc_act1`). This is a look/polish item.
  - Rocks now collide as boxes.

### Isle of Lost Skulls: **basic form works end to end (after the arc fix)**
- **Works:**
  - Foyer lobby with the hologram.
  - Console START leads to the brig (act 1). The padlock breaks, and act 1 clears (15 kills).
  - Act 2 clears (39 kills). The High Seas deck is reached.
  - Ghost Beard's Gauntlet: after the fix he takes 3/3 cannon hits and is defeated, then VICTORY (15,000 gold, S).
- **Missing:**
  - Same-act checkpoints (a wipe ends the run).
  - Mid-level gates, mines, puzzles, the caves' darkness, the shop, the gold cap.
- **Risks:**
  - **40,858 objects in the room**, 3x Crescendo. Check the frame rate and memory in the headset.
  - Curved art is blocky.
  - The High Seas bridge drop was not shot after the fire.

### Crescendo of the Blood Moon: **basic form works end to end**
- **Works:**
  - Foyer lobby with the hologram.
  - Console START leads to the Burning Village, which is fought to CLEAR (the gate opens), then walked to its exit.
  - The Grand Hall (`w1_cbm_grand_hall`) and the Concert Hall: Dracula appears at his organ and takes 3 mirror hits, with the bat phases in between. Then VICTORY (18,545 silver, rank S+ with the 15,000 bonus).
  - After the fix: 11 racks and no spawn warnings.
- **Missing:** the carriage ride motion, the whip, secret shelves, most traps, random chest loot, the gift shop.
- **Risks:**
  - Dracula is a scaled Bellhop body.
  - The bats may walk (Lunger kind).
  - `[Quest_Dracula1_Silver_VFX]` logs Unity "referenced script is missing" warnings (inert Rec Room scripts).

### Maker Pen: **works**
- **Works:**
  - In the Dorm: the watch hook gives the pen, and the pen is held.
  - A spiral and a line are drawn, plus a remote copy decoded from the wire format ("wire exact"). The ink tubes render (`w1_pen_dorm_strokes`).
  - Touch-erase, undo and remote delete-all work. The pen can be put away.
  - In the Rec Center the same works again (`w1_pen_reccenter_strokes`).
- **Missing:** a real two-client test. The palette colours are guesses.
- **Risks:** A/X undo clashes with the client's jump/inspector, and keys 1-4 overlap weapon slots in game rooms.

### The Rise of Jumbotron: floater check
- **Seen:**
  - `w1_jt_foyer_back`: the marquee sits on its pillars.
  - `w1_jt_foyer_arrival`: the Foyer.
  - `w1_jt_act5_concession`, `w1_jt_act8_cargo`, `w1_jt_act9_stadium`: nothing visibly floating in these views.
- **Not covered:** the tour did not frame the Departure area or the Foyer sticks up close. The builder's offline renders cover those (`NSP/jt13/png`).
- **Risk:** the Departure's drifting glow sticks were removed. Rec Room may mean them to float there (ZeroGravityOutro).

## Open for the next iteration

1. **Isle same-act checkpoints.** A shared RrQuestRoom change plus client re-entry into the same act.
2. **Quest "back to the lobby" after the victory screen.** It is the framework's 20 s MatchOver path, the same one Golden Trophy and Jumbotron use. It was not observed in this tour, because the harness matched an earlier "back in the lobby" line.
3. **Performance in the headset:** the Isle (40k objects), the quests' first travel, and Cyber Junk City's switch.
4. **Cosmetic items:**
   - the Crimson Castle Courtyard's saturation;
   - the Soccer goal-box barrier look;
   - the Bowling pin-deck red panels;
   - the Hangar lobby's scoreboard behind the console.
