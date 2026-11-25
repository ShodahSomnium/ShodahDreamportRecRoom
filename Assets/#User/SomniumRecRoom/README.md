# Somnium Rec Room

Rec Room inside a Somnium Space world, the way Somnium Quake, Somnium Mythology and Somnium Euro Truck do it: the
game's own data is read from the player's installation at runtime and rebuilt in Unity. Nothing from the game is
shipped with the world. Desktop (screen mode) and VR from the start, with Somnium avatars and voice.

## What can and cannot be read (2026-09-23)

Rec Room (Steam app 471710, Unity 6000.0.27f1, IL2CPP) keeps two kinds of data in `RecRoom_Data`:

- `level*`, `sharedassets*.assets`, `resources.assets`, `globalgamemanagers*` carry a custom `..RR..RR` encryption
  header. **This world does not decrypt them** - that would be circumventing a protection measure, a line the earlier
  Dreamports never needed to cross. The official rooms' level geometry lives there, so rooms are rebuilt.
- `StreamingAssets/aa/StandaloneWindows64/*.bundle`: 2,787 plain UnityFS bundles (LZ4, serialized format 22, type
  trees present), 1.6 GB. Props, tools, weapons, enemies, the watch UI, sprites, fonts, 1,178 sounds, 1,471 particle
  systems, 185 shaders, Maker Pen kits - including the **Dorm kit, whose 47 pieces still hold the real dorm's
  geometry in the room's own coordinates** - and the Charades "Ink Space" venue as one prefab. Every Rec Room
  MonoBehaviour's data is readable through the type trees (a bottle's drink interval, a gun's fire rate, a quest's
  death penalty).

(Web research during the build noted that Rec Room's servers closed on 1 June 2026; the installed game files are all
this world needs.)

## Where it stands

| Layer | State |
| --- | --- |
| Bundle reader (`Rr/`) | **Done.** UnityFS + LZ4 (only the blocks of the serialized file are decompressed), serialized files with type trees, typed configs (`RrConfig`), prefab hierarchies (`RrPrefabData`). |
| Catalog (`Rr/RrCatalog`) | **Done.** 2,787 bundles, 24,564 named entries (prefab roots, clips, textures, sprites, meshes, materials, fonts, named MonoBehaviour assets), every one with a loader. 17-19 s cold under .NET, 4.2 s in the Unity harness, ~70 ms from the cache afterwards. |
| Loading (`Core/RrAssets`) | **Done, harness-tested.** Unity's own AssetBundle loader. Only Addressables container entries load by API; anything else is made resident by loading the smallest container asset whose preload table lists it, then found by name. Dependency sets trimmed by `RrReach` to what Unity components need (root beer bottle: 4 bundles instead of 128). |
| Spawning (`Core/RrSpawn`, `RrClone`) | **Done, harness-tested.** Clean copies built from Unity's own components (meshes, skins, colliders, rigidbody, LODs, lights, audio, animator), particle/trail subtrees instantiated: missing-script warnings down from 573 to 210 for three props (the rest come from loading each prefab once). Bound Unity scripts that could act on the client (Volume, EventSystem, cameras) are stripped. |
| Materials (`Core/RrMaterials`) | **Done, harness-tested.** Rec Room's own URP shaders are kept (they render its look exactly; URP Lit loses the recolour masks). Shaders without a DepthNormals pass go to queue 2501 (the client forces depth priming); `Sprites/Default` particles become URP Particles/Unlit (stereo). |
| Sound, music, effects, fonts, procedural build helpers (`Core/`) | **Done.** Rec Room's clip struct (volume, pitch shift/variation), 40-source pool; crossfaded music; effect prefabs auto-destroyed; Futura/Jost fonts made into TMP assets at runtime; maker-pen material for our own walls. |
| World scene | **Done.** `Assets/Rec Room Scene.unity`: Uploader (scripting on, SomniumRecRoom, world slot 0 - pick it), arrival platform, status label, RrMain, Scene Networking and 135 network objects. |
| Player, input, hands (`Player/`) | **Built.** Bridge hand anchors (VR) and a screen-mode hand; Rec Room screen-mode keys on keys the client leaves free (left click use, E grab, Q drop/toss, Z reload, V third person, H watch); client tablet detection; editor fallback rig. **Iteration 2:** `RrAvatarHand` reads the avatar's drawn hands and wrists (read only). |
| Isolation (`Player/RrIsolation`) | **Iteration 2, harness-tested with a fake remote player; not yet tried with two real clients.** Players in other rooms are hidden on your client only (renderers, name tag, chat bubble, colliders) and their voices are turned to 0. Everything is restored exactly when they are with you again. Known limit: the client's avatar culling (the nearest 8 within 15 m) still counts hidden players standing at the origin. |
| Networking (`Net/`) | **Built, offline-tested.** Presence slots, per-instance elected authority ("fake master") with take-over, instance state, batched events, owner-simulated props, AI actor blocks; 30 of 30 checks pass in a simulated 3-client room (claims, elections, authority leaving mid-round, late join, grab races). Not yet run with two real clients. |
| Grab and tools (`Interact/`) | **Built.** Grab, hover highlight, throw with Rec Room's throw limits; drink, eat (88 foods), break, collision sounds, shake, basketball and hoop, pop-in, respawn, world buttons - all from each prop's own Rec Room config. **Iteration 2:** hold poses fixed (the ~90 deg tilt), plus Rec Room's screen-mode hand per tool. Not yet confirmed in a headset. |
| Travel (`Rooms/`) | **Built.** Rec Room's loading card ("GOING TO", 0.15 s fades), instance join, build under the fade, teleport, the official activity door as portals. **Iteration 2:** every room is built at the world origin (no more wobble), only the current room exists, its memory is freed on leaving, the arrival slab is hidden in rooms, and a fall or client respawn puts you back at the room spawn. |
| Watch menu (`Watch/`) | **Built.** Rec Room's current watch UI from its own icons, palette and sounds: Home, Play, People, This Room, Settings; VR wrist glance, desktop H. **Iteration 2:** in VR it sits on the avatar's drawn wrist, sized to the avatar and lifted over sleeves. The build stamp is at the foot of Settings. Not yet confirmed in a headset. |
| Game kit (`Games/Shared/`) | **Built.** Match flow, teams, announcer, scoreboard, health/respawn, hit volumes, weapons from Rec Room's weapon data, projectiles, melee, quest enemies, loadouts. |
| Places and games | **Built so far:** Dorm and Rec Center (final versions since iteration 3), Paintball, 3D Charades, Dodgeball, Paddleball, Quest for the Golden Trophy, The Rise of Jumbotron, and since iteration 13 (basic form): Laser Tag, Soccer, Bowling, The Curse of the Crimson Cauldron, Isle of Lost Skulls, Crescendo of the Blood Moon, plus the Maker Pen on the watch. **Next:** polish of the iteration 13 games, then Rec Royale, Showdown, Disc Golf, Stunt Runner, Rec Rally, Midnight, Little Monsters, Punch Up (research in `rr_latest_rgames_action.md` and `rr_latest_rgames_story.md`). |

## Iterations

The world is now built in short iterations: each one is uploaded and tested in the client, then the next one starts
from that feedback. The build stamp (`Core/RrVersion.cs`) appears on the status label at start, at the foot of the
watch's Settings page, and as the first `[SomniumRecRoom] Main:` line in Player.log.

| # | Date | What changed | Try in the world |
| --- | --- | --- | --- |
| 1 | 2026-09-24 (overnight) | First build: Dorm, Rec Center, Paintball, 3D Charades, Dodgeball, Paddleball, Golden Trophy, watch, travel, networking. | Tested by the user in VR: it works. Four issues: old versions of the rooms, held items ~90 deg off, the watch floating off the wrist, and wobble far from the origin. |
| 2 | 2026-09-24 | One origin for every room, isolation of other rooms' players, held items in the hand's orientation, the watch on the drawn wrist, and a **private whitelist** (below). Research for the final versions of the rooms and all 21 activities. | See the checklist below. |
| 3 | 2026-09-24 | The final Rec Center, rebuilt from Rec Room's own July 2025 room (measured from the user's download cache and Rec Room's archived room picture): timber beams, skylights, balcony and stairs, the kiosk under the REC ROOM logo, stage and theatre, gym, cafe wing, lockers. The real category doors (SPORTS & PVP, QUESTS, FEATURED ROOMS, MAKERWORLD, CAMPUS JAMS) with watch-style room lists, and the Magic Door. The Dorm's final touches. | See the iteration 3 checklist below. |
| 4 | 2026-09-24 | Quest for the Golden Trophy rebuilt from Rec Room's own levels: the lobby and acts 1-6 with Rec Room's props, markers and spawn points, and shell architecture rebuilt by us. Rec Room's final quest rules for every quest (one hit downs you, fixed kill gold, fixed rank totals, Coach's "Game On!", the boss's death clears the rest). The Dorm in its real layout. Dodgeball's final rules (no catch-outs, 14 m/s screen throw, end barriers, 1 v 1). The scene kit that rebuilds reference levels from the player's install. | See the iteration 4 checklist below. |
| 5 | 2026-09-24 | Rec Room's own post-processing, per room: its house look (soft bloom, Neutral tonemapping, warm grade) and the Rec Center's brighter, warmer profile from the cached room. Level effects: Golden Trophy torches where walls are drawn, act 4's lava bubbles, the Rec Center's six light shafts and window wind. Room ambience: the Rec Center crowd and cafe; torch crackle and lava loops in the quest. Quest effect sounds: lava, rack refills, the rolling boulder, the victory chests. | See the iteration 5 checklist below. |
| 6 | 2026-09-24 | **Rec Room's own Rec Center**, loaded at run time from the player's own Rec Room download cache (the July 2025 building, walls, stairs, cafe, gym, theatre, garden), with our doors and interactive things fitted into it; players without it in their cache get the rebuild. The look made darker and warmer, with live Brightness and Warmth sliders on the watch. | See the iteration 6 checklist below. |
| 7 | 2026-09-24 | **Paintball from Rec Room's own levels** (read from the Android build as reference only): the 2025 lobby and all 7 final maps (River, Homestead, Quarry, Clearcut, Spillway, Sunset Drive-In, Merry Midway), with every flag, weapon and spawn point at Rec Room's position. One map is built per match. Final rules: START only, head and torso hits, splash weapons can hurt teammates, Drive-In's paint throwers and mines. | See the iteration 7 checklist below. |
| 8 | 2026-09-25 | Rec Room's own lighting in every room (sun direction, colour and soft shadows; trilight ambient; linear fog; each room's sky colours from its baked sky), with no base darkening needed. Duplicate and overlapping meshes removed. **Rec Room's start hologram** (the install's Scoreboard Projector: players in the room, teams, mode/map, START) in every game. **Golden Trophy** from its 11 Android rooms: Foyer, then the **Armory** with its weapons, then 9 acts with real walls. **The Rise of Jumbotron**, new: Foyer, Armory, 9 acts, Jumbotron. The Paintball lobby reworked: Rec Room's rocks and tyres, real sign texts, building materials, interiors. | See the iteration 8 checklist below. |
| 9 | 2026-09-25 | **Rec Room's own light grid.** Rec Room's fork of URP gives its shaders their lights through a world-space grid (24x12x24 cells, 8 lights each, its own falloff), which Somnium's URP never fills. So from iteration 1, torches, lamps and fill lights never reached Rec Room's surfaces. `Core/RrLightGrid` now fills it per room: torches light their walls, the Rec Center and Dorm glow as in Rec Room. Rec Room's hemisphere and no-specular lights are honoured. Ambient occlusion comes through the client's HBAO when the player has AO on. | See the iteration 9 checklist below. |
| 10 | 2026-09-25 | **The Rise of Jumbotron in Rec Room's own look.** Its level-only materials now map to the closest install Rec Room materials (as the Golden Trophy's do), so the light grid lights them. Vertex colours are fed raw as Rec Room's shaders expect (they were far too dark and violet), the 0.354 colour alpha is no longer glass (the see-through pink boxes), and glow strips have Rec Room's strength. Every act now gets its own environment (four used the Foyer's violet) and the levels' own post profiles. The coloured mist, portal glows, light shafts and lasers are drawn. The marquee is our own neon lettering. | See the iteration 10 checklist below. |
| 11 | 2026-09-25 | **Jumbotron walls and props, reload in either hand.** The level converter (DN/tools/gtscene, new jt=1 mode; Golden Trophy output unchanged) no longer drops small wall pieces or cuts slanted faces into gapped staircases, so the see-through gaps are closed. Level-only props are re-measured from their own meshes instead of solid bounding boxes (trusses as open frames). Rec Room's atlas textures (door faces, stripes, glow strips on one image) are drawn as plain measured colours instead of tiled nonsense, and the bent light shafts are rebuilt as soft beams. Only the current act is shown. In VR a gun in either hand reloads with the free hand's trigger on the gun, or the right A. | See the iteration 11 checklist below. |
| 12 | 2026-09-26 | **Real mirrors (Thad's package).** `Core/RrMirror` wraps Thad's per-eye planar mirror (`ThirdParty/ThadMirror/Mirror.cs`, shader and materials in `Assets/Thad/Mirrors`, set up as his High Quality Mirror). It replaces our old single-image reflection in the Dorm and the Rec Center hallway, and the Jumbotron Armory's dressing mirror (drawn as grey metal until now) is a real mirror. Mirrors reflect only within 6.5 m in front of them, else stylised glass. The scene carries Thad's "Default Mirror" material into the upload (RrMain shader carriers). | See the iteration 12 checklist below. |
| 13 | 2026-09-26 | **Wave 1: six new games in basic form, the Maker Pen, Jumbotron floaters.** Laser Tag (Hangar, Cyber Junk City), Soccer and Bowling under SPORTS & PVP; The Curse of the Crimson Cauldron, Isle of Lost Skulls and Crescendo of the Blood Moon under QUESTS. Each is rebuilt from its Android level (gtscene jt=1) and loads it from `Data/*.bytes` (RrSceneBank, carried by the scene). Each has a lobby with the start hologram and a way back to the Rec Center. A Maker Pen tile on the watch Home page gives networked 3D drawing in every place. The Rise of Jumbotron has no more floating props (490 groups fixed). | See the iteration 13 checklist below. |
| 14 | 2026-10-14 | **Player-made rooms.** Home > Create: Maker Room, Dorm Room, The Ink Space, Park, Performance Hall, Bowling Alley, Hangar. Build with the Maker Pen's new tools (shapes, 111 props, select/move/rotate/scale, clone, recolor, delete, physics, undo). Saved on each player's PC (10 versions, auto-save); every player announces the rooms they hold and visitors pull a copy, so a public room opens even while its owner is offline. Owner + co-owners build; "Everyone can build" and Private options. | See the iteration 14 checklist below. |
| 15 | 2026-10-17 | **Finishing pass.** Opt-in Rec Room avatars (Watch > Avatar: classic bean head/torso/hands from the install, 137 outfit items, 26 eyes/mouths, dyes; saved locally, restored next visit, seen by others; the Somnium avatar is only hidden while worn). Disc Golf (Lake, Propulsion), Stunt Runner (lobby + 6 courses), Rec Rally (Chaparral, buggies, 3 laps). Maker rooms reviewed and fixed (10 issues). MAKERWORLD / CAMPUS JAMS doors list maker rooms; Punch Up dropped; unfinished games shown as COMING SOON only; watch settings saved; Isle of Lost Skulls restarts the act on a team wipe. | See the iteration 15 checklist below. |
| 16 | 2026-10-20 | **Networking on Photon Fusion 2.1.3 + Community Modules 4.0** (Somnium's client moved to 2.1; 2.0.12 worlds stop syncing). See the section "Networking migrated to Photon Fusion 2.1.3" below. | Every networked game syncs again. |
| 17 | 2026-11-10 | **Same rooms for everyone; Rec Room found anywhere.** Players without Rec Room's download cache get the final (July 2025) Rec Center built from measured data (`Data/RecCenter_Final.bytes`, tool DevNotes/tools/rcscene) in exactly the cached building's frame, instead of the old hand-built one. Dodgeball and Paddleball now build from Rec Room's own levels (Android level11/13). The install search (Rr/RrInstall.cs) reads Rec Room's own log, Steam's folders and every library, Meta libraries, any `*_Data` folder name (older builds: Recroom_Release_Data), then walks the drives if needed, and remembers the path (`SomniumRecRoom/install.txt`). Whitelist: Shodah, denevraut, Wildy, Ninquiet. | See the iteration 17 checklist below. |

**Iteration 2 checklist (headset and desktop, ideally two players):**
1. **Wobble:** in the Dorm, Rec Center and Paintball, nothing should shimmer or jitter.
2. **Grab (VR):** a bottle stands upright in an upright fist, the paintball pistol points where your hand points, and
   the basketball sits in the palm. Drink from a bottle. Throw things.
3. **Grab (desktop):** held tools appear lower right the way Rec Room shows them, and the paddleball paddle is visible.
4. **Watch (VR):** it sits on your avatar's wrist and stays there when you move and turn your arm. Glance at it to open
   the menu. Try an avatar with long sleeves.
5. **Two players:** A in the Dorm, B in the Rec Center: neither sees nor hears the other. B walks into A's instance (a
   game): they see and hear each other. B leaves: B disappears for A. Blocking and muting still work afterwards.
6. **Rescue:** fall off Paintball's River map or press the client's Respawn: you come back at the room's spawn, not
   inside a wall.
7. Send Player.log. The useful lines start with `Hands:`, `Grab:`, `Watch:`, `Isolation:` and `Travel: census`.
8. **Whitelist:** you (Shodah) load in as usual, and the log says `Access: Shodah (id ...) is on the whitelist`. An
   account that is not on the list sees only the plain platform with "This world is private for now.", and neither of
   you can see or hear the other.

**Iteration 3 checklist:**
1. **Arrival:** walk out of the Dorm door. You land in the hallway looking at the kiosk and the orange REC ROOM sign,
   like Rec Room's picture.
2. **Walk it:** climb both staircases to the balcony, go down the steps into the theatre, visit the gym, the cafe wing
   and its upstairs. Nothing should trip you or let you fall through.
3. **Doors:** each category door opens its list in front of it. Pick a built game and you travel there; come back and
   you arrive in front of that door. MAKERWORLD and CAMPUS JAMS say "coming soon". The Magic Door rolls a random game.
4. **Things to use:** ring the bell, change the 3 radios, shoot both hoops, play ping-pong, buy a bottle, look at the
   Friend-O-Tron and the mirror in the hallway.
5. **Dorm:** notebook posters, the HELP panel, one armchair, no dresser, the marker set by the loft canvas, and the Play
   board on the wall, with a list you can pick from.
6. **Two players:** the radios, bell and shared toys stay in step.

**Iteration 4 checklist:**
1. **Dorm:** you spawn on the welcome mat under the loft; the door is on the left wall under the loft, the mirror under
   the arched window, the bunk bed and desk on the back wall, and the Play board beside them. Climb the ladder.
2. **Golden Trophy:** start a run from the lobby (the theatre). Coach says "Game On!". Play through: act 3's boulder
   rolls, act 4 has the lava bridges, act 6 is the library, then the finale and the Goblin King.
3. **One hit downs you;** a teammate revives you. The gold and the rank at the end use Rec Room's fixed values.
4. **Look for:** props floating where Rec Room's walls or ledges were (worst in act 5's west hall), gaps you can fall
   through, and dark spots. The walls and floors are our rebuild.
5. **Dodgeball:** a catch doesn't put the thrower out; there's a low barrier behind each end; 2 players can start a
   1 v 1.
**Iteration 5 checklist:**
1. **Look:** every room is a touch warmer and softer, with gentle bloom on bright lights. Nothing should look washed
   out or too dark. Compare the Rec Center with Rec Room's picture: it should be honey-toned.
2. **Rec Center:** light shafts from the skylights and the curved windows. A light crowd murmur everywhere, and a
   coffee pour at the cafe counter.
3. **Golden Trophy:** torch flames and embers in acts 3 and 6 (the other torches are missing where our rebuilt walls
   aren't). Lava bubbles and a low rumble in act 4. The boulder rumbles as it rolls. Weapon racks pop back in with a
   puff. Stepping in lava hisses. The chests creak open at the victory.
4. **VR:** check the frame rate with the effects on, and that the flame cards look right in both eyes.
**Iteration 6 checklist:**
1. **Rec Center (you have it in your cache):** walk out of the Dorm into Rec Room's real building. Player.log should
   say `RecCenter: using Rec Room's own cached Rec Center (scene RecCenter_071825, 45 bundles, ...)`. Walk the stairs
   and the balcony, the cafe, the gym, the theatre and the garden outside.
2. **Doors and toys** sit in the real building: the category doors, the bell, the radios, the hoops, ping-pong, the
   Friend-O-Tron and the vending machine. Tell me anything that floats, clips into a wall or sits in the wrong spot.
3. **The fallback:** a player without the cached Rec Center (or anyone, if the cache is gone) gets the rebuild. The log
   says `no cached Rec Center on this PC`.
4. **Look:** darker and warmer everywhere. Open the watch, go to Settings > Look, move Brightness and Warmth until it
   looks right, then send me the numbers (or Player.log).
5. **Known:** the cache holds the July 2025 building. The later changes in your video (the curved upper ring, the mural
   lounge with FEATURED ROOMS double doors, the orange corridor) are not in it.
**Iteration 7 checklist:**
1. **Lobby:** you arrive at Rec Room's pregame spots in the outdoor compound: the Practice Arena gate with START on its
   pedestal, the map/mode/team board beside it, the leaderboard, the "Try 'em out!" wall (every weapon can be taken),
   the shooting gallery (its START runs a 30 s round) and the radio.
2. **Pick each map** on the board and play a round of Capture the Flag and of Team Battle. Flags, weapons and spawns
   are where Rec Room had them. Tell me the maps that feel off, and where.
3. **Rules:** START needs 2 players. Only head and torso hits count. Grenades and mines can hurt teammates; bullets
   can't. Drive-In has 2 paint throwers and mine piles.
4. **Look:** Rec Room's own props, cars and kit pieces load from your install. Trees, big buildings and signs that
   exist only in the Android build are our shapes in the right size and place: expect Spillway's dam and Merry
   Midway to look the least like Rec Room.
5. **Walk:** ramps, stairs, forts, the river banks, and falling into a map's kill floor (you go out).
**Iteration 8 checklist:**
1. **Light and sky:** every room has a sky and a sun with soft shadows (tables, trees and players cast them). Quests
   are dark and torch-lit as in Rec Room. If a room is too dark or bright, use Settings > Look and send the numbers.
2. **Start hologram:** in the Paintball lobby, the Golden Trophy and Jumbotron foyers, Dodgeball, Paddleball and
   Charades, Rec Room's projector shows who is here (and teams), the mode/map, and START. Press START (VR poke,
   trigger ray, or desktop click). Below the minimum it says how many more players are needed.
3. **Golden Trophy:** Foyer -> START -> the Armory (take a sword, shield or bow from the wall) -> through its door ->
   the 9 acts (boulder in act 3, lava in act 4, the library in act 6, the maze in act 7, half-lava in act 8) -> the
   Goblin King in the Cafeteria.
4. **The Rise of Jumbotron (new):** Foyer -> Armory (laser pistols and burst rifles) -> the 9 acts (the Flight Deck's
   green launch button in act 5, Space in act 6) -> Jumbotron in the Jumbodome. It's a first pass: tell me how it
   feels.
5. **Paintball lobby:** does it feel like Rec Room's now? Rocks, tyres, signs, the locker room and lounge, the skins
   wall, the Try 'em out! wall.
6. **Overlaps:** tell me about any mesh that still flickers or doubles up.
7. **Upload size:** the world's code is now about 15 MB (the level layouts are embedded, compressed). If the upload
   server rejects it, tell me and I'll move the data out of the code.
**Iteration 9 checklist:**
1. **Torches and lamps:** in the Golden Trophy Foyer, the torches and lamp posts light the brick, the floor and the
   carpet around them; in the acts, wall torches light the stone. The Armory is no longer black.
2. **Rec Center, Dorm and 3D Charades:** warm light pools on the ceilings, walls and floors, as in Rec Room (in
   Charades, The Ink Space's own 26 lights now reach its walls and stage).
3. **Ambient occlusion:** turn on ambient occlusion in the Somnium client's graphics settings (every preset has it off)
   and look for soft darkening in corners and under furniture.
4. **Frame rate:** lit Rec Room surfaces leave Unity's batcher, so each view costs about 2-3 ms more on the CPU (editor
   measurement). Check VR frame times in the Golden Trophy and the Rec Center.
5. Send Player.log: the useful lines start with `[SomniumRecRoom] Lights:` (one per room: its lights, cells and renderers).

**Iteration 10 checklist (The Rise of Jumbotron):**
1. **Foyer:** azure-blue cinderblock walls (no longer violet), opaque sci-fi cover crates (no see-through pink boxes),
   the stage lit lavender-pink with the curtains, pink haze over the stage, and our neon "THE RISE OF JUMBOTRON" marquee.
2. **Armory:** green walls, the glowing blacklight carpet, cyan mirror lights. It may be brighter than Rec Room's.
3. **Each act has its own colours:** act 1 hot magenta, act 2 crimson Cargo Bay, act 3 dark Engineering with red lasers,
   act 4 teal Bridge, act 5 deep-blue Flight Deck, act 9 the Stadium. Acts 1, 2, 5 and 8 used to show the Foyer's violet.
4. **Glow and haze:** the strips, signs and glowing crates bloom; soft coloured mist far off (invisible up close); light
   shafts are soft beams, not grey boxes.
5. **Wall colours:** Hallway2 (act 3) and the Flight Deck walls should read mid-dark grey or coloured, not black. If they
   are near black, tell me (the vertex colours assume Rec Room rendered in linear colour).
6. **Frame rate and hitches:** the quest builds its haze at start (the Flight Deck bakes about 81k rays over several
   frames); tell me if joining the Jumbotron hitches or the fps drops in the acts.
7. **Too dark or too bright?** Rec Room's own ambient is near black in most acts; Settings > Look > Brightness still works.
   Send Player.log: the useful lines start with `[SomniumRecRoom] Jumbotron:` and `PostFx: Jumbotron area`.

**Iteration 11 checklist:**
1. **Reload in either hand (VR):** hold a gun in the LEFT hand, put the right hand on it and pull the right trigger: it
   reloads (a buzz). Same the other way round. The right A button also reloads a gun in either hand (it still jumps too).
   Gripping the gun with the free hand still swaps hands.
2. **Jumbotron walls:** walk along the walls and door frames in the Foyer, act 1 (the octagonal port) and act 4's entrance:
   no gaps or see-through slits. Tiny sawtooth outlines may remain on slanted edges up close.
3. **No floating boxes:** ceiling trusses are open frames, lamps on stands have legs, the Foyer's magenta cone is a soft
   light beam onto the marquee, act 5's floating cyan square is gone, props keep their shapes.
4. **Textures:** pillars, door panels and crates are plain Rec Room colours instead of repeated hazard stripes and patches;
   the linoleum, corrugated walls and wood look right. The sunset disc in act 6.
5. **Only your room is shown:** the next act is switched on when you teleport; tell me if weapons appear late.
6. **Frame rate:** about 4x more wall pieces; check the fps in acts 8 and 9 (the Stadium) and the load time.

**Iteration 12 checklist (mirrors):**
1. **Dorm mirror:** walk up to it (within about 6.5 m, in front): a sharp reflection that is right in both eyes in VR, your
   Somnium avatar in it. Further away or from the side it turns back to the stylised glass. MIRROR ON / OFF still switches it.
2. **Rec Center hallway mirror** and the **Jumbotron Armory's dressing mirror** (3 x 2.2 m): the same.
3. **Frame rate** while standing at a mirror: it renders the room again for each eye. Tell me if it drops too much; the
   reflection resolution can be lowered (`RrMirror`, Thad's ResolutionScale).
4. If a mirror stays the stylised glass up close, send Player.log (look for `Mirror:`).

**Iteration 13 checklist (wave 1, basic form: ideally two players):**
1. **Reach every new game** from the watch's Play page and from the Rec Center's SPORTS & PVP (Laser Tag, Soccer,
   Bowling) and QUESTS (Crimson Cauldron, Isle of Lost Skulls, Crescendo) doors. Each opens in its own lobby.
2. **Start from the hologram console**, play, see the score or the victory/failure screen, then come back to the lobby.
   Laser Tag: TEAM BATTLE, pick HANGAR or CYBER JUNK CITY, START (400 points or 8 min). Soccer: START (3 min, most goals).
   Bowling: START, bowl on the middle lane (the side lanes are practice). Quests: START QUEST, the Armory/Foyer, then the acts.
3. **Load times:** tell me how long each game takes to open, especially Cyber Junk City and the quests' first acts.
4. **Maker Pen:** Home page > Maker Pen. VR: trigger draws (harder = thicker), A/X undo, palette above the other hand
   (touch with the pen tip). Desktop: left mouse draws, Z undo, right mouse erase, 1/2 colour, 3 eraser, 4 clear. A second
   player in the same place sees the ink and your pen.
5. **Jumbotron:** no props floating in the Foyer (glow sticks on strings, marquee on its pillars), Cargo (boxes, not poles),
   the Stadium (no cable loops under the Jumbotron).
6. Send Player.log after the session; the useful areas are `LaserTag`, `Soccer`, `Bowling`, `CrimsonCauldron`, `LostSkulls`,
   `Crescendo`, `MakerPen`, `Quest` and `Jumbotron` (the levels' build lines).
7. **Rec Center theatre mirror:** the dark-wood frame between the curtains now holds a mirror (reflects within 10 m); the
   hallway mirror is gone with Rec Room's own building (its hallway wall is plain).
8. **Dorm radio:** every visit starts with "In The Basement".
### Private testing: the whitelist (`Core/RrAccess.cs`)

While the Dreamport is only for the team, Rec Room loads only for the accounts listed in `RrAccess.Whitelist`, at the
top of `Core/RrAccess.cs`:

```csharp
public static readonly string[] Whitelist =
{
    "Shodah",
};
```

- **To let someone in,** add their Somnium username as it appears on their name tag (letter case does not matter), then
  upload again. A Somnium user id works too: the log prints everyone's at start as `Access: <name> (id <id>) ...`.
- **To open the world to everyone,** empty the list.
- **Everyone else** stays on the arrival platform with "This world is private for now.". Their client does not read the
  Rec Room install and starts no room, watch, network slot or game. Every other player is hidden and silent for them,
  and the testers' isolation hides and silences them in turn.
- **In the editor and the harness** the list is not applied.
- **The check waits for the client to report the username** before anything loads. A slow client only delays the start,
  and a warning is logged after 30 s.
## Workflow

1. Open `Assets/Rec Room Scene.unity` and press Play. With Rec Room installed, the catalog is built (first time) and
   the world starts in the arrival room, then travels to the dorm.
2. Upload through the Uploader with scripting on and `SomniumRecRoom` as the assembly. **Choose the world slot first**
   (`WorldIdForUpload` is 0 and `DoReplaceExistingWorld` off; Quake 12308, AoM 12410 and ETS 12510 are taken).
3. Offline checks used during development (session scratchpad): the compile check against the upload server's
   reference list (`compilecheck/build.sh project --editor`, which also guards Fusion/Somnium members) and the play-mode
   harness (`harness/run.sh <scenario>`), which boots the world in a copy of this project and saves screenshots.

## Dorm Room (2026-09-24)

`Places/Dorm/`, namespace `...Places.Dorm`, registered by `RrRegistryDorm.cs` (hook `RegisterDorm`):
RrActivity.Dorm, "Dorm Room", Social, 1 player, Networked = false. Cover `illustration_HomeButton_DormRoom`, icon `Illustration_Menu_Dorm`. No background music, like Rec Room's dorm.

**What was built.** `RrDormRoom` rebuilds the 2018 "Spring Cleaning" dorm from Rec Room's own Maker-Pen kit in bundle 43ea1176.
- **Architecture:** 22 pieces: floor, skirting, 4 walls, loft walls, pillar, stripe, flat and angled ceilings, skylight, large and small windows, loft floor, beam, railing and shelves, plus the gold light shafts. Each root is placed at the inverse of its mesh child's local transform, which snaps the pieces back into the original room frame. Measured deviation from the research table: at most 10 mm.
- **Colours:** the orange `AG-MakerPenShape` surfaces are recoloured with `RrLook.Solid`: walls #A9BFD0, ceiling #C9D6E0, frames and shelves #F2F2F2, skirting #EDEDED, beam #6E4A30. The main floor uses Rec Room's `Wood_Floor_Matte_mat`. Architecture casts shadows on both sides, so the sun only comes in through the windows. Maker-Pen selection meshes are hidden.
- **Furniture:** bunk bed and desk under the big window, dresser under the loft, ladder in the railing gap, mirror facing the spawn, campus map, Code of Conduct, Block/Mute and Shake Hands posters, community board, 2 armchairs, 3 plants, art canvas on the loft.
- **Hoop:** placed on the pillar face at scale 0.94, so its rim lands on Rec Room's saved rim VFX position (3.905, 1.097, -1.090). Measured: 2 mm off.
- **Props (17, all through `RrTools.MakeTool`):** basketball, office bin, 2 root beers, water bottle, pizza slice, apple, desk lamp with a point light, radio, cup, dice, frisbee, bell, 2 books, Oscar trophy, paintbrush. Each is dropped 1 cm above its surface, and its home is set once it has settled.
- **Our own parts:**
  - `RrDormMirror`: planar reflection on the glass, 448 px, within 6.5 m; otherwise a stylised glass.
  - `RrDormBoard`: the Play board, with Rec Room cover-art tiles that travel to each activity.
  - `RrDormRadio`: Rec Room's 41-track playlist.
  - `RrDormLadder`: press, or VR grip, to climb up to or down from the loft.
  - `RrDormOutdoors`: campus, sky dome and clouds outside the windows.
  - MIRROR panel: mirror on/off, put things back, Rec Center.
  - `RrPortal` door to the Rec Center.
  - Lights: 3 point lights plus a warm "sun" spot light through the big window.

**Rec Room values used:**
- Room frame x -4.94..4.77, z -2.96..2.96; loft floor 2.75.
- Spawn (0.30, 0, 0.60) facing the mirror.
- Mirror glass 2.04 x 2.21 m.
- Ladder 2.81 m high.
- Bunk-bed desk top 0.85 m; dresser top 1.81 m.
- Radio: random first track, 1-30 m linear rolloff.
- Ambience `AmbianceSummerOutdoor`.
- ToolCleanup: 0.1 m / 30 degrees / 3 m / 15 s.

**Log lines to look for:**
- `Dorm: floor: Rec Room's Wood_Floor_Matte_mat (AG/Standard-Opaque)`
- `Dorm: hoop rim at room (3.905, 1.095, -1.091) ... off by 2 mm`
- `Dorm: play board: N activities`
- `Dorm: built at (x, z): 60 Rec Room objects, 17 props, max kit snap error 10 mm, 3.0 s` (with `; missing: ...` when a prefab was not found)
- `Dorm: radio: <clip> (<s> s)`
- `Dorm: ladder (press|grip): up to the loft`
- `Dorm: put N prop(s) back`

**Harness:** `scenarios/dorm_room.json`, with hooks `Places.Dorm.RrDormTest.Report / StandAtMirror / ShootBasket / ClimbLadder / RadioNext`. It produces 9 PNGs in `out/dorm_room/`.

**Untested:**
- A basket counted in the dorm. The scripted lob goes through the rim cleanly, but RrBall does not score it; the Interact module is asked to look.
- VR: ladder grip, poke and ray presses, and the mono mirror in stereo.
- Client-side clicks.
- Audio levels by ear.
- Radio auto-advance.
- Sitting in the armchairs (there is no RrSeat yet).

### Places/RecCenter: the final Rec Center (2026-09-24, iteration 3)

**Built:** Rec Room's last Rec Center (the July 2025 Studio room) at the world origin. It was measured from the user's download cache (`DevNotes/research/rr_reccenter_cache.md`); nothing from the cache is loaded or shipped. Room frame: origin = the kiosk's floor centre, Dorm entrance at -Z looking +Z, +X to the right. It registers through `RegisterRecCenter` (Social, 1-40 players, no global music).

**Files:**
- `RrRecCenterRoom`: build, doors, spawn, props, interactions, networking.
- `RrRecCenterLayout`: every dimension, the door spots, the spawns and the prefab list.
- `RrRecCenterShell`: the architecture.
- `RrRecCenterKiosk`: the kiosk and the logo sign.
- `RrRecCenterOutdoors`: what the windows look out on.
- `RrRecCenterSurfaces`: materials.
- `RrRcGeo`: metre-UV boxes, convex prisms, ramps, collider-only solids, and `RrRcBatch` (many small boxes merged into one renderer).
- `RrRecCenterRadio` / `RrRecCenterTracks`: the radios.
- `RrRcPaddle` / `RrRcPingPongBall`: ping-pong.
- `RrRecCenterTest`: harness hooks.
- The doors themselves are the doors agent's: `RrCategoryDoor`, `RrRoomListPanel`.

**The room** (measured unless marked GUESS):
- **Hall.** 25.4 x 33.4 m (x -13.9..11.5, z -15.6..~18). The far end is a curve, a circle through (11.5, 7.3) and (-1.5, 17.8) (GUESS fit), with two big curved multi-pane windows: navy frames (0.070, 0.171, 0.382), dark-wood sill at 0.55, glass to 5.7. The wall is cream with a blue band on top.
- **Ceiling.** Cream plaster at 9.0, unlit. Dark timber beams (0.345, 0.197, 0.130), underside 8.4, on the measured lines (z -15.39, -12.17, -7.27, -2.18, 2.41; x -9.32, -13.83). The trimmers round the skylights and the posts under the balcony edge are GUESS. Two 6.8 x 6.1 m gridded skylights: x -3.30..3.48, z -9.57..-3.45 and 4.42..10.54. Over the balcony the roof slopes up to 11.9 at the left wall, under the three clerestory windows at 9.53-10.86 (slope GUESS).
- **Balcony** (left). Floor 3.10, x -13.9..-9.4, rail top 4.15 (timber posts and handrail, navy rails, guard colliders). Blue lower wall underneath (GUESS paint).
  - Front stairs: along the entrance wall, climbing -X from x -3.9.
  - Back stairs: along the gym's front wall (layout GUESS).
  - Both are 18 visible steps over an invisible ramp collider (29 deg), with solid fill underneath.
- **Right wall.** A red-brick stage section between blue pillars. The opening is z -8.7..-1.8, 4.4 m high, with red curtains and a valance. The marquee is x 10.87..11.29, y 4.60..7.96, a pink face with 60 bulbs; its text "NOW PLAYING / QUESTS" is a GUESS. The merch booth is at x 9..12, z -0.5..5.7: a blue counter, merch shelves and a "REC ROOM SHOP" header. It is decorative; nothing is sold.
- **Theatre.** Behind the stage: 4 steps and a ramp down to the floor at -0.79, plum walls (GUESS), a little stage with a red backdrop, brown couches, and the QUESTS door on the far wall between 4 lit quest poster cases showing cover art (COMING SOON for unbuilt quests).
- **Gym** (x -18..-1.8, z 13.3..25.9, ceiling 6.5 GUESS). The floor is kept at 0; the scene's is about 0.2 m higher. Terracotta walls (GUESS paint), the measured back and side windows, two hoops at z 19.5 (the scene has one painted key; the second hoop follows Rec Room's "two hoops"), orange keys and white lines, the punching bag, the vending machine, and the SPORTS & PVP door on the back wall.
- **Left wing.**
  - Cafe downstairs: the counter across the wing at z -5.86..-4.49, the back counter, shelves, two chalkboards, a curved bay window (quarter ellipse centre (-17.6, -3.6), radii 7.4 / 8.6, GUESS fit to Window_Cafe's box), 5 tables at the measured spots, pendant lamps.
  - Upper floor at 3.10 over the front part: a railing over the cafe, a lounge with the third radio, the Puzzles & Art corner (GUESS spot), a runner to the FEATURED ROOMS door.
- **Hallway** to the Dorm door (x -2.1..0.8, z -25.1..-15.6, ceiling 3.1): taupe runner, code of conduct poster, and the Dorm's planar mirror on the west wall.
- **Outdoors.** Sky dome, lawn (leaving out the theatre's footprint), the measured brick fence (3.2 m), bushes and trees at the measured spots.

**Kiosk.**
- A long low blue counter ring with a light wood top at 1.05 m (the scene's AABB x -3.19..2.72, z -2.97..2.86).
- 4 dark timber posts up to 8.4 and a top frame at 4.29-4.58.
- The tall blue back panel with a merch grid.
- The REC ROOM sign between the front posts, facing the entrance. It is Rec Room's own logo texture `RecRoom_Logo_SteamSplash_Tex` (crop 80,146,356,256), 3.95 x 2.84 m at y 4.58-7.42, in 9 transparent layers for its 0.26 m depth. If the texture does not load, TMP letters stand in.
- A round brown rug with a gold border (radius 4.3; colour GUESS).

**Doors** (placed only through `RrCategoryDoor`):

| Door | Where | Position (yaw) |
| --- | --- | --- |
| SPORTS & PVP | gym back wall | (-7.45, 0, 25.84) (180) |
| QUESTS | theatre far wall | (21.54, -0.79, -4.36) (270) |
| MAKERWORLD | balcony wall (the scene's MakerWorld sign) | (-13.84, 3.1, 5.6) (90) |
| FEATURED ROOMS | upstairs wing | (-24.94, 3.1, -11.93) (90) |
| CAMPUS JAMS | the coral-framed door under the balcony's end, green carpet | (-9.39, 0, 9.08) (90) |
| Magic Door | blue wall under the balcony, between the front lockers and the cafe openings. The July 2025 scene has no Magic Door, so this spot is a GUESS | (-13.84, 0, -6.5) (90) |
| DORM ROOM | hallway end, still an `RrPortal` | (-0.65, 0, -25.04) |

**Spawn.** From the Dorm or the world: the hallway at z -16.6 / -17.6, facing +Z (the rec.net picture's view). Back from an activity: 2.2 m in front of the category door that lists it. `RescuePose` is the hallway.

**Things that work:**
- the reception bell (event 64);
- 3 radios: kiosk (Rec Room Theme), cafe (Food Court) and upstairs lounge (Lone Shoe Blues). Each has the 41-track playlist; the track is in custom ints 0-2 (event 65);
- 2 hoops (RrHoop);
- 2 ping-pong tables in the hall's back-left, with 4 paddles and 2 balls (GUESS spot);
- the vending machine (a drinkable water bottle) and the Friend-O-Tron (who is here);
- the community board notes and the mirror;
- cafe food and drink to pick up (coffee pots, mugs, plates, donuts, red velvet and chocolate cake);
- Puzzles & Art whiteboards, canvases, markers and clipboards;
- loose toys: 4 basketballs, 3 dodgeballs, 3 frisbees.

**Shared props:** RrNetBody LocalPropId 1-16. The instance magic value is 0x52435232 ("RCR2"). **Lockers:** the scene's six `[Lockers]`, as `(Sandbox_Lockers)` at the measured spots: three by the back stairs, one in the gym and two under the balcony near the entrance. They were kept by user decision, because the real room had them. **Removed:** pool, poker, the jukebox, the 21 single-activity doors, the placards, the lobby.

**Materials.**
- Rec Room's own: Wood_Floor_Matte_mat tinted to the scene's (0.972, 0.633, 0.327), planks 3 x 0.75 m; Paint_White/Blue tinted for cream, blue, terracotta, teal and plum; Brick_Red_Mat; Wood_Horizontal_DarkBrown_5x_Mat for the timber (made resident by `(Sandbox_Table_Coffee_Large)`); Carpet_LowPile_Striped_Blue and Charades_Carpet_Taupe for rugs; Glass_Clear_mat (by loading the jukebox prefab, never spawned); posters and the exit sign.
- RrLook: navy frames, gold borders, curtains, emissive fluorescents.
- The couches' instanced material is tinted brown (GUESS shade).
- The wall paint colours are GUESSES from the picture: the scene keeps them in vertex colours that were not read.

**Log lines to look for:**
- `RecCenter: surfaces: 15 Rec Room materials, 0 fallback colour(s), timber, brick, posters`
- `RecCenter: shell built: 1044 pieces (...)`
- `RecCenter: doors: SPORTS & PVP (9), QUESTS (5), MAKERWORLD (0), FEATURED ROOMS (6), CAMPUS JAMS (0), DELIGHT ME!, DORM ROOM`
- `RecCenter: REC ROOM logo from RecRoom_Logo_SteamSplash_Tex (3.95 x 2.84 m, 9 layers)`
- `RecCenter: built at (0, 0) in N s: 6 doors (...), 101 prefabs (0 failed), 35 local tools, 16 shared props, 3 radios, 2 hoops, mirror ready`
- `RecCenter: entered from <activity> (...)` and `RecCenter: instance ints initialised`
- Harness hooks (`RrRecCenterTest.Report/RingBell/PressRadios/ShootHoop/Vend/FriendOTron`) log `RecCenter: test: ...`

Harness: `scenarios/reccenter_tour.json` passes all 37 steps. It has 20 room-frame shots (entrance, arrival, kiosk, overview, balcony and stairs, back-left, ping-pong, windows, stage, every door, cafe, gym, upstairs, Friend-O-Tron) and the hook checks.

**Not built:** the projector room over the hallway, the office windows above the entrance, the inner theatre curtains, the light shafts, and the scene's room-data objects at their real positions (`182.data` was not decoded).

**Untested:**
- The Somnium client (desktop and VR).
- Walking the stair and theatre ramps and railings with Somnium's controller.
- Two-client sync.
- Returning from an activity to its door.
- The logo's runtime transparent material in the uploaded world.
- Build frame times and the VR cost of about 35 lights.

### Places/RecCenter: category doors, room lists, Magic Door (iteration 3)

Files: `Places/RecCenter/RrCategoryDoor.cs` (API + the door component), `RrRoomListPanel.cs` (the list, also the Dorm board), `RrDoorsTest.cs` (harness hooks, scenario `doors_test.json`).

**API** (the Rec Center places doors through this):
- `RrCategoryDoor.Create(parent, localPos, yaw, RrDoorCategory)`: `localPos` is the floor centre of the doorway, 0.06 m in front of the wall, and the door's +Z faces into the room. Categories are SportsPvp, Quests, FeaturedRooms, MakerWorld and CampusJams, the July 2025 room's own signs. CreativeCampus is the older door; it still compiles but is unused.
- `RrCategoryDoor.CreateMagicDoor(parent, localPos, yaw)`.
- `RrCategoryDoor.Activities(category)`, `Sign(category)` ("SPORTS & PVP", ...), `Title`, `IsBuilt`, `MagicCandidates`.

Footprint: the frame is 1.25 m wide including the sign and the sign top is at about 2.62 m. Keep door centres at least 1.9 m apart and 1 m clear in front: the list floats 0.25 m out and is 1.6 m wide.

**Look.** Rec Room's single room door from B43, the same meshes in `(Sandbox_RoomDoor)` 2ac, `(Sandbox_RoomDoorV2)` 09fb and `(Sandbox_MagicDoor)` 2a9: a wooden leaf with a wired-glass window and pull bar, a 1.15 x 2.14 m frame, and a backlit sign. Only the "Door" and "[Door_Sign]" parts are copied, through RrClone, and re-centred on the frame (Rec Room's pivot is 0.45 m / 0.50 m off-centre). The frame is a teal tint of `ActivityDoor_Recolorable_Mat` (wanted / 0.76); the Magic Door's is purple (GUESS). The leaf is tinted warmer, to the 2024 photo's yellow-orange. The sign is our Futura Bold Condensed label. If the prefab does not load, a procedural door of the same size is built.

**Behaviour.**
- Rec Room's ActivityDoor values (as RrPortal): the leaf swings 21.44 degrees ajar and the glow fades in to 0.834 over 0.25 s within 3 m or when pointed at. Hover sound Kiosk_StateChange01 at a low volume.
- Use by click, VR trigger ray, a hand on the leaf, or walking into the doorway.
- A category door opens its list. While the list is open the leaf stays shut and the glow stays on.
- The Magic Door plays `MagicDoor_Refresh`, flicks through built activities on its sign for about 1.6 s, shows the pick for 0.9 s, then calls `RrTravel.GoTo`. It picks from registered activities, not the Dorm, the Rec Center or where you are.

**Mapping.**
- SPORTS & PVP: Paintball, Laser Tag, Rec Royale, Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling.
- QUESTS: Golden Trophy, Crimson Cauldron, Jumbotron, Isle of Lost Skulls, Crescendo of the Blood Moon.
- FEATURED ROOMS (user decision 2026-09-24): 3D Charades, Make It To Midnight, My Little Monsters, Punch Up, Rec Rally, Stunt Runner.
- MAKERWORLD and CAMPUS JAMS: Rec Room listed player-made rooms here. Dreamport has none, so both lists say "Nothing here yet - coming soon."

**The list** (`RrRoomListPanel.OpenForDoor`).
- Layout: the watch's style and parts, 1920 x 1170 canvas units at 1.6 m wide. Title and subtitle at the top, the five categories as tabs (the door's own selected), a card per activity, and the orange nav bar with the location and Close.
- Cards: cover art and title, plus an "N here" / "You are here" pill (RrWatchWorld, every 1.5 s). Activities not built yet are veiled with "COMING SOON" and cannot be picked.
- Picking a built card closes the list and calls `RrTravel.GoTo`.
- It opens and closes with the watch's PlayerMenu_Show/_Hide keys and sounds. It closes on Close, on walking more than 4 m away (after having been near), on travel, or when another door's list opens. Only one door list is open at a time (`RrRoomListPanel.Current`).
- Input, all ending in the card's RrUiButton, with actions closer than 0.35 s counted once:
  - the watch's GraphicRaycaster and TrackedDeviceGraphicRaycaster (XR ray, free cursor);
  - our own `RrInput.AimRay` hover and click (locked-cursor crosshair, VR trigger), blocked by walls, with mouse-wheel scrolling;
  - VR fingertip poke, from 3 cm in front of the canvas to 5 cm behind it.
- Board mode (`CreateBoard`): always open, no nav bar, 5 cards a row at 1.2x.

Logs: `Doors: <SIGN> door used (click|trigger|touched|walked in): list of N activities, M built`, `Doors: picked <activity> from the <tab> list (card)`, `Doors: Magic Door rolled <activity> out of N built activities`.

### Dorm Room: final default dorm (iteration 3)

From the wiki's "unmodified Dorm Room" list (2023-2024), rr_latest_places.md 2.2-2.3 and 5.1:
- **Social posters:** the notebook-paper NUX posters `Nux_Stop_Gesture2` and `Nux_Shake_hands2` (standalone 1024^2 textures) on our own 0.62 m lit quads under the loft. If a texture does not load, the 2018 Sandbox poster comes back.
- **HELP panel:** HELP / PRIVACY POLICY / TERMS OF SERVICE at (-1.71, 1.35, -2.935), 0.32 m wide, between the mirror and the Code of Conduct. The CoC moved to x -2.28 because it is 0.71 m wide. Each button shows a short note for 12 s, since Rec Room's pages are gone.
- **Furniture:** one armchair (the loft armchair is gone). No dresser: a dracaena stands in its place, and its root beers, bell and book are on the desk.
- **Marker set:** `(SandboxMarkerSet)` (B43 1e4) on the loft floor by the canvas.
- **Play board:** the final "Play" board, a board-mode RrRoomListPanel of 2.86 x 1.64 m on the community board. It is titled "Play", with tabs Originals (Rec Center first, then every built activity in the registry's order) and the Rec Center's five categories. It waits up to 20 s for the watch's fonts, because the dorm is built first.

Everything else is as before. Hooks: `RrDormTest.Report` (posters, marker set, dresser, armchairs, help), `BoardSports`, `PressHelp`. The scenario is `dorm_room.json`; `doors_all.json` runs both.

## Paintball: a lobby, River and Sunset Drive-In, Capture the Flag and Team Battle (2026-09-24)

**What was built.** `Games/Paintball/` (namespace `...Games.Paintball`) registers Rec Room's Paintball through `RrRegistry.RegisterPaintball`:
- Category PvP, 2-8 players.
- Cover and icon `GiftBox_Paintball_Tex|8,276,380,268`.
- Arrival music `PaintballLobby_RiverRadio`.

One `RrPaintballRoom : RrGameRoom` builds three areas at the instance origin (the world origin since iteration 2; it was (-5000, 0, 6000)):
- **The lobby** at the origin: a plank deck in a forest clearing, with Blue and Red team pads, a console (RIVER / DRIVE-IN, CAPTURE THE FLAG / TEAM BATTLE, START), a scoreboard, lockers, two `(Radio)` props, a practice range with `(Paintball_Target_Sandbox)Variant` targets and pistol, rifle and shotgun practice racks, and the Rec Center door (`RrPortal`).
- **River** at room-local (0, 0, 450), from rr_shooters Â§10.1: an 84 x 48 m clearing split by a shallow river (bed -0.8, water -0.3, 1:3 banks), a 3 x 12 m bridge, stepping rocks, a fallen log, two wooden forts (10 x 7 m, 1.2 m walls with 1.5 m gaps, deck at 2.6 m with parapet slots, back ramp), mirrored mid cover and a flat-tree border.
- **Sunset Drive-In** at (0, 0, -450), from Â§10.2: the screen at x +33 with a 4 m sniper platform, 4 rows of `Car_50s`/`Car_80sSportsCar` with speakers, the snack bar with booths, stools, projector, AC units and the Debbie's Drive-In sign on a reachable roof, Blue at Frankie's Fuel (canopy, pumps, shop as the spawn room, pole sign), Red at the ticket booth (barrier arms, fence line, `Sign_Drivein_Redbase`), and the movie audio looping at the screen.

Build: 316 Rec Room props with 0 missing, 11 racks, 3.1 s in the editor.

Files and GUIDs:

| File | GUID | Role |
| --- | --- | --- |
| `RrPaintballRoom.cs` | 3e089fd5ea184289a384484fce94e883 | Rules, flow, spawns, votes, VO queue |
| `RrPaintballLobby.cs` | 1d85da85668946faa877b6c75adba122 | Lobby builder |
| `RrPaintballRiver.cs` | 0f319d79760749c0b7c3f9a276cf3ff7 | River builder |
| `RrPaintballDriveIn.cs` | d8411bbc57c14441bfeb58d7c89e5e97 | Drive-In builder |
| `RrPaintballMap.cs` | c1a12ffd283f4de7824aa4c13927e3a3 | Map data |
| `RrPaintballBuilder.cs` | 2d47fb5b7298424c86a51dfd3db24e9c | Grounded prop spawns, palette, tint |
| `RrPaintballFlags.cs` | 74d82e89e5b44b33a258c17ec6869206 | Capture the Flag |
| `RrPaintballRack.cs` | dc73d8435a7c415dbf64f1f96266d418 | Weapon racks |
| `RrPaintballMarkers.cs` | 98e23360518848b39926d59e447104a9 | Team gem and KO marker over members |
| `RrPaintballTest.cs` | 2a66eacc03ab495482e4ebd9d6bd7e6e | Harness hooks |
| `RrRegistryPaintball.cs` | 8534f57d00ed4d38afb04ec87473a0ca | Registry hook |

**Measured Rec Room values used.** From the PaintballCTF and PaintballTeamBattle configs and PaintballManager (rr_shooters Â§3.1, Â§4.1, Â§7):
- MaxHealth 1 (one hit is out), respawn 5 s, spawn invincibility 5 s.
- 2 teams x 4, GameStartDelay 10 s (the countdown), EndGameResultsDuration 15 s (round-over 4 s plus match-over 11 s).
- CTF: 3 captures or 600 s. Team Battle: 50 hits or 600 s. Friendly fire off.
- Pistol `(PaintballGun)` at spawn. `(PaintballAssaultRifle)`, `(PaintballShotgun)` and `(PaintballRifleScoped)` on racks that refill after 15 s.
- Flags `(FlagTool)` and `(FlagGoal)`, tinted Blue rgba(0.22,0.56,0.84) and Red rgba(1,0.45,0.25). Flag foot read from `Goal Attach Point` (-0.983). Stand attach at +0.3.
- VO:
  - Countdown: "Capture The Flag" / "Team Battle", "Blue Team" / "Red Team", 5up..1up, the PtblRiver_10Sec sting.
  - Flag events: FlagTaken, "Blue/Red flag taken/dropped/returned", FlagReturn01, ScoreSFX, Blueteamscores / Redteamscores.
  - Results: Blueteamwins2 / Redteamwins2 / Tiegame, MatchOver01.
  - Player out: "Out".
  - Consecutive VO lines are spaced 1.5 s apart (audioFXMinInterval).
- Music: RiverPredator and PtblSunset while playing; lobby radios PaintballLobby_RiverRadio / _SunsetRadio.
- Rack refill puff [VFX_ObjectSpawn]; out marker KO_Skull.
- Respawn at the team spawn farthest from opponents (the 2020 rule).

**Networking.** Phase, map, mode and scores are in the instance ints (authority only). The CTF flags use custom ints 0..11: per flag, state, holder id + 1, and the dropped position in room-local mm. Flag take, return and capture requests are `EvFlag` 64 to the authority, repeated every 0.3 s until the ints show them. Map and mode votes use GameRequest 16/17 and are repeated until seen. Rack pickups are `EvRack` 65. Outs are the shared kit's Death events; the authority counts Team Battle hits and drops a carried flag at the victim's feet, or when the carrier leaves or shows Down.

**Log lines to look for.**
- `Paintball: built the lobby, River and Sunset Drive-In: N Rec Room props (M missing), 11 racks, flags ready`
- `Paintball: entered: River, Capture the Flag, phase ...`
- `Paintball: joined the Blue team in the lobby`
- `Paintball: match starting: ...`
- `Paintball: spawned in the Blue base on River`
- `Paintball: Red flag taken by player N`
- `Paintball: Blue captured the Red flag`
- `Paintball: Red flag dropped by player N at ...`
- `Paintball: took the Shotgun from rack 3 (press|grip)`
- `Paintball: round over: Blue wins (3 - 1)`
- Harness hooks: `PaintballTest: ...`

**Verified.** `build.sh project --editor`: 0 errors, 0 warnings, all 113 members proven. Harness `scenarios/paintball_all.json`: 3 runs, 0 failed steps. It builds the room, takes 15 shots, plays a River CTF (fire, take, capture, rack, out and respawn) and a Drive-In Team Battle (3 hits).

**Not verified / risks.**
- Nothing tested with real remote players or in VR: flag grip, rack grip, left-hand carry, remote carrier display, team gems, KO skull.
- Clip names are not confirmed by ear.
- No out-of-bounds timer (invisible walls instead), no grenades or mines, no scope camera.
- Lobby votes are last-press-wins.
- The Drive-In 'sunset' is only warm point lights and an emissive screen; the world's sun is not changed.

## 3D Charades in The Ink Space (2026-09-24)

**What was built.** Games/Charades (namespace `SomniumSpace.Worlds.SomniumRecRoom.Games.Charades`). It is registered through `RrRegistry.RegisterCharades`: "3D Charades", category Party, 2-8 players, cover `GiftBox_Charades_Tex|8,276,380,268`, music MUS_BGM_3DCharades2b_Music.
- `RrCharadesRoom : RrGameRoom` builds the room.
  - Venue: Rec Room's whole venue `(Charades_InkSpace_MainHull_Sandbox)Variant` (key 84), spawned as scenery and lifted 3.24 m so the main hall floor is y = 0. Its 26 lights are kept with shadows off, and its own activity door is hidden.
  - Added pieces: `(Charades_Stage)` (key 23f) snapped onto the middle strip, three `(Sandbox_Couch)` on the lounge rugs, and an RrPortal to the Rec Center at the hull door's spot (-3.66, 1.74, -11.02).
  - Performer's podium: CORRECT!, SKIP, UNDO, CLEAR, 12 colour swatches, CO-OP CLASSIC / TEAM LIGHTNING start buttons, and a 2.6x word card.
  - Also: an RrScoreboard with a status line, a performer spotlight, and the timer loop.
- `RrCharadesInk`: 8-sided tubes sampled every 8 mm, quantised to 1 mm with the radius in 0.25 mm steps, drawn with the RrLook.Solid maker-pen paint.
- `RrCharadesPen`: drawing with Rec Room's `(CharadesPen)` (key cb9) through RrTools.MakeTool plus RrNetBody (prop 0).
- `RrCharadesCard`: the rebuilt 0.197 x 0.147 x 0.008 m card ("No Peeking!").
- `RrCharadesWords`: our own 275-word list.
- `RrCharadesTest`: harness hooks.

**Measured Rec Room values used.**
- Co-op Classic: 480 s game, 60 s per performer.
- Team Lightning: 4 team rounds of 90 s, 45 s per performer.
- Clips:
  - Charades_IndividualTimerLoop: 60.00 s, started at 60 minus the turn length.
  - Charades_TeamScoreUp (2.55 s): correct guess.
  - SFX_Failure_Stereo: time's up.
  - Charades_ButtonPress: every button.
  - sfx_ui_maker_pen_draw_tube_ribbon_loop_01: pen draw loop, volume 0.005 to 0.22 by draw speed.
  - sfx_ui_maker_pen_created_object_delete_01..05: undo.
- Effect: [VFX_Charades_Goal].
- Pen: ExtrusionPoint (0, 0.036, 0.152), lock on pickup.
- Screen tube size: 1.0 at rest down to 0.15 at 5 viewport/s.
- Announcer: "Blue Team" / "Red Team" on team assignment.

**GUESSES.**
- The co-op intermission lasts 4 s.
- SKIP deals a new word to the same performer, in both modes.
- The performer confirms a correct guess.
- The performer faces -x toward the lounge.
- The stage piece's top sits at 0.62 on the strip.
- The 12-colour palette is our own.

**Network.** State lives in the instance's custom ints 0-5 (performer and word; turn and word counters; the game-remaining ms at which drawing starts and at which the turn ends; the last result; rotation counters).
- Event 67: requests to the authority, repeated every 0.5 s until the state shows them.
- Event 64: ink points.
- Event 65: undo.
- Event 66: clear.
- Event 68: resend request.

**Log lines to look for.**
- `Charades: Ink Space hull spawned: bounds (21.48, 7.07, 23.91) ... 26 lights (shadows off)`
- `Charades: The Ink Space built: stage mark (2.00, 0.64, 4.00), pen ready, 275 words`
- `Charades: player N starts Co-op Classic (480 s, 60 s per performer)` / `... Team Lightning (4 x 90 s, 45 s per performer)`
- `Charades: turn N: performer P (Blue), 45 s, word #W`
- `Charades: you are the performer: "Duck"`
- `Charades: solved "House" (score 1)`
- `Charades: skipped "..."`
- `Charades: ink cleared for drawing K (...)`
- `Charades: asked the performer for drawing K` / `re-sending drawing K ...`
- `Charades: the performer left: next turn`

**Harness.** `bash SP/harness/run.sh SP/harness/scenarios/charades_play.json` passes 23 of 23 steps. `charades_lightning.json` passes 17 of 17. The build takes 2.9 s.

**Integration.** The server-like build has 0 errors and 0 warnings, with all 113 Fusion/Somnium members proven. The editor-like runtime and editor tools builds each have 0 errors.

**Not verified.**
- Two real clients: ink seen remotely, resend, pen sync, and request retries to a remote authority.
- VR drawing, pressure and poking the podium.
- Desktop drawing with the real mouse.
- Sounds by ear.
- The turn timeout, Lightning team-round switch and match-over in a full game.
- The cover art (every GiftBox_* name misses in RrCoverArt).

## Dodgeball and Paddleball (Games/Dodgeball, Games/Paddleball) (2026-09-24)

**Dodgeball.** Built:
- A 16 x 22 x 9 m gym (ours; the level is encrypted): 9 x 16 m wooden court, centre line, circle, attack lines, team zones, padded walls, windows, trusses, light panels and backboards.
- Team benches on risers, two end scoreboards with info strips, the REC CENTER door and a START button.
- Rec Room props: 3 x (DodgeballBall) on the centre line as networked tools, (Sandbox_Cone) at the corners, and (Standee_Male01) / (Standee_Female03) as practice targets.
- RrGameRoom flow: countdown 3 s, untimed rounds, round over 4 s, first to 3 rounds.
- Rules: a live throw hits and puts the player out (to the bench). Centre line or out of court is out. A catch puts the thrower out and brings back the catcher's first teammate out (CatchOutsThrower; Rec Room's own rule, catch = dead ball, is false).
- Offline alone it is practice.
- Net: custom ints 0 out mask, 1 flags | round, 2 count, 4..15 players (id+1 | team<<20). Events 64 hit claim, 65 catch claim, 66 line claim (to the authority, resent every 0.3 s until seen), 67 verdict. The authority holds a hit 150 ms so a catch of the same throw id wins. Hits are found by the thrower's client, sweeping the flying ball against the layer-27 hit volumes. The ball's kind int holds live | team | thrower.

Measured values used: ball radius 0.11, mass 1, bounciness 0.6, MaxThrowSpeed 15. Screen throw 14 m/s, charge 0.1-0.5 s, 7 degrees gravity compensation, aim assist 9 m / 30 degrees. defaultScoreToWin 3. Sounds and effects: Whistle01, Horn01, dodgeball_PlayerHit_01, [VFX_Default_HitParticles], Out + OutSFX, Hit + HitSound01, Catch01, [DodgeballFireTrail] while live, Bounce03 via CollisionAudio. Music DodgeBaller.

**Paddleball.** Built:
- A dark hall with a 3 x 3 x 12 m glass tube (layer 23), glowing edge rings, team pads, (SandboxToolGoalLarge) frames stretched over the ends, the gallery, 3 scoreboards, the door and START.
- 4 x (Paddleball_Paddle) (key 2d) on stands: networked, SolidWhileHeld, LockOnPickup, handed out at the countdown.
- The (TennisBall) scaled to r 0.1 as the zero-gravity ball: RrNetBody, contacts off, perfect wall reflection by our own code, at least 25 % of the speed along the tube.
- Hits are computed from the paddle's face box on the holder's client: v_out = reflect(v_ball - v_face, n) + v_face, then 5 % faster, 5-14 m/s, always toward the other end. The hitter takes the ball (touch-steal).
- Goals are claimed by the ball's controller (event 71, resent until the rally moves on). Point show: Goal VO + CrowdCheer01 + [VFX_Default_Goal], 2 s, then the conceder serves. The serve auto-launches after 5 s, or after 1 s from an empty side. First to 5, AmyVO_Generic_YouWin for the winners.
- Screen mode: a click swings the paddle (0.25 s) and returns a ball within 2.4 m along the crosshair.
- Net: custom ints 0 ball state, 1 server, 2 rally, 3 hits, 4 winner+1, 5 flags, 8..11 seats. Events 70 hit (sound), 71 goal claim, 72 goal.

Measured values used: paddle face 0.24 x 0.28 x 0.02 under a 1.25-scaled node, mass 10, LockOnPickup; ballScoredStateDuration 2 s; goals to win 5; RacketHit01, [VFX_Impact_Particles], TennisBall_Impact01a. Music SportFunk (GUESS).

Log lines to look for:
- `Dodgeball: gym built: court 9 x 16 m, 3 balls`
- `Dodgeball: round N playing: K players`
- `Dodgeball: ball i: first live throw`
- `Dodgeball: ball i hit player P ... claiming`
- `Dodgeball: caught ball`
- `Dodgeball: verdict hit|catch|line|left: out X`
- `Dodgeball: local player out: to the Blue bench`
- `Dodgeball: round N won by Blue: blue 1 - red 0`
- `Paddleball: court built: tube 3 x 3 x 12 m, 4 paddles`
- `Paddleball: paddle 0 given to the local player`
- `Paddleball: match playing: blue 1 v red 0`
- `Paddleball: first paddle hit: paddle 0 at 9.0 m/s`
- `Paddleball: goal for Blue: blue 1 - red 0`
- `Paddleball: automatic serve by Red`
- `Paddleball: ball stalled: serving again`

Harness scenarios: dodgeball_gym (20/20 steps) and paddleball_court (18/18); hooks are RrDodgeballTest.Start/Throw/Opponent and RrPaddleballTest.Start/Serve/WaitFlight/Concede.

Untested:
- anything networked (a second client)
- VR paddle face hits, screen swings and screen charged throws
- real catches, the line / out-of-court check, the bench teleport and the teammate coming back
- match over back to the lobby
- 2v2 seats
- sounds by ear
- no screenshot of the scoreboard info strip in the gym

## The quest framework and Quest for the Golden Trophy (2026-09-24)

**What was built.**
- `Games/Quests/Shared` (namespace `...Games.Quests`) is a reusable quest framework on top of `RrGameRoom`:
  - The lobby is `RrPhase.Lobby`. The start button starts the run; it never starts automatically.
  - The whole run is one `Playing` round, and the end screen is `MatchOver`.
  - The instance authority runs the act machine in the custom ints (`RrQuestInts`): Act, Stage (Intro, Combat or Clear), Wave, team Score, MaxPossible, Outcome, Party, Downs, Kills, Revives, BossHealth, BossPhase, Hazards and HazardTime.
- `Games/Quests/GoldenTrophy` (namespace `...Games.Quests.GoldenTrophy`) is the first quest built on it: a castle-courtyard dressing room and nine acts. It is registered through `RegisterGoldenTrophy`: category Quests, 1-4 players, cover and icon `Consumable_Polaroid_Goblin1_Tex(2048)`, music GoldenTrophyExplore.

| File | Role |
| --- | --- |
| Shared/RrQuestRoom.cs | Abstract quest room. It holds the act machine, gold (`AddPoints`), requests (`Ask`), and the down-penalty and fail checks. Every client moves its own player into each act, handles doors, music and stingers, and checks lava. It also provides the start button and the test hooks. |
| Shared/RrQuestDef.cs, RrQuestAct.cs, RrQuestWave.cs | Data: currency, penalty, music, enemy roster, weapons, and player rules; acts (origin, entry/checkpoint, exit door, area, spawn groups, lava, waves); waves (ActStart, PrevCleared, PrevBelow n, Enter zone, After s), scaled by party size. |
| Shared/RrQuestDirector.cs | The authority's wave runner: triggers, an interleaved spawn queue, and a MaxAlive cap. |
| Shared/RrQuestEnemies.cs | 30 enemy slots in RrNetActors blocks 0-1. The authority publishes; watchers follow with id = 1 + slot x 8 + serial. EnemyAttack and EnemyVolley are replayed so that each victim's client decides. Arrows stun basic goblins and imps, and a watcher's parry becomes a stun request. |
| Shared/RrQuestBreakables.cs | Rec Room pots through RrTools.MakeTool, with a broken bitset in block 2. The authority pays each pot once. |
| Shared/RrQuestRevive.cs | Down, bleed-out (30 s) back to the checkpoint, revive (2.5 m, stay within 1 m, Quest_Revive), and our own downed markers. |
| Shared/RrQuestHud.cs | Gold, HP, title card, toasts, prompt with bar, boss bar and floating numbers. A screen overlay on desktop; a world panel re-placed in VR. |
| Shared/RrQuestRack.cs, RrQuestGear.cs | Weapon racks that refill in 3 s, and remote players' held weapons from presence hand items. |
| Shared/RrQuestDoor.cs, RrQuestKit.cs | The wooden act door with a pass-through zone; kit placement grounded by bounds; walls with merlons, towers, torches, banners, lava pools, chests; the tiling pass with Rec Room stone and wood. |
| Shared/RrQuestEvents.cs, RrQuestInts.cs, RrQuestTest.cs | Event ids 64-69 with the wire helpers, the int layout, and the harness hooks. |
| GoldenTrophy/RrGoldenTrophyRoom.cs, RrGoldenTrophyLevel.cs | The definition, the lobby, the nine acts, the victory set piece, and TestPushBoulder. |
| GoldenTrophy/RrGoblinKing.cs, RrGoldenTrophyBoulder.cs | The boss, and the act-3 rolling boulder, which runs on the synced instance timer. |

**How a new quest uses it.**
1. Subclass `RrQuestRoom`.
2. `CreateDef()`: set the currency, the penalty and the music; add enemies with `AddEnemy("(QuestAIController)...")`; add weapons.
3. `BuildLevel()`: build the lobby. For each act, make a root with `RrBuild.Group`, set `act.Root`, `act.ExitDoor = RrQuestDoor.Create(...)`, add spawn groups with `act.Group(...)`, waves with `act.Wave(When).Add(type, count4, group)`, lava with `act.Lava.Add(RrQuestKit.LavaPool(...))`, and pots with `Pots.Add(...)`. Register racks in `Racks` and put the lobby's `StartButton`. Then call `Def.AddAct(act)`.
4. Optional hooks: `AuthorityActStep` and `AuthorityActDone` for bosses and puzzles; `AuthorityHazard` for levers (sent with `Ask(ReqHazard, code, arg)`); `OnEnemyReady` for boss bodies; `PointsFor` and `PointsKind`; `OnActStage` and `QuestStep` for what every client shows; `OnRunReset`; `BossName`.
5. For a boss: set `QuestInt BossHealth` (permille) and call `Enemies.Volley(...)` for scripted shots.

**Measured Rec Room values used.**
- Rule circuit fe6 [Quest_Goblin1_Manager]: Gold; death penalty 2000; win clip "Crimson Cauldron - Our Heroes Have Triumphed"; lose clip "Golden Trophy - Our Heroes Have Failed"; point effects [VFX_GoldCoins] / [VFX_BossGoldCoins]; coin sounds Quest_Coins_Spawn01 / Quest_BossCoins.
- Music: GoldenTrophyExplore, GoldenTrophyCombat2, GoldenTrophyClearFanfare2 (4.5 s).
- Spawners: GoblinBasicGroundMelee2020 (100 HP, 3 m/s), GoblinBasicFlyingRanged, GoblinEliteGroundMelee (200 HP), GoblinEliteFlyingRanged, and GoblinKnightCharge and GoblinKnightSpin (250 and 333 HP, charging 8 m at 12 m/s).
- Pots: FatPot 50, FatPot_Large 100.
- Revive: range 2.5 m, reviver stays within 1 m.
- Weapons: (QuestSword) 67-100 AI damage over 3-15 m/s; (Longbow) 17-67; (Crossbow).
- Kit: (Sandbox_FlatTreeLarge/Medium/Small), (Sandbox_Campfire), (LogPile_Sandbox)Variant, (Sandbox_StonePillar), (Sandbox_Barrel), (Hay_Bale_Sandbox)Variant, (LavaBoulder), (Golden_Bucket).
- Materials: Stones_Modular_Theater_Mat and Wood_Floor_Matte_mat, tiled.
- GUESS (research 3.4, 4.1): layouts, wave contents, party scaling, and the king's 3000 HP, three orbs every 4 s, and adds every 25 %.

**DEAD END, do not retry.**
- Water_Lava_Mat (AG/TerrainWater) loads but draws nothing on a box. The lava is our own glow with crust plates.
- Wall_Stone_Section_Theater_mat is an atlas whose wooden inserts repeat as brown patches when tiled. The walls use the flagstone material instead.
- "(Rock_01)Variant" is not a prefab name.

**Log lines to look for.**
- `Quest: kit ready: stone ..., floor Stones_Modular_Theater_Mat, wood Wood_Floor_Matte_mat`
- `Quest: textured 1080 boxes with Rec Room stone/wood`
- `Quest: Quest for the Golden Trophy built at (...): 9 acts, 7 enemy types, 46 pots (2900 Gold), 7 racks, N kit props`
- `Quest: run started with N player(s)`
- `Quest: act a/9 "ACT a"`, `Quest: local player into act a at (...)`, `Quest: act a wave w: n enemies (party p)`, `Quest: act a clear (gold g)`
- `Quest: boulder released by player id (16.0 s roll)`
- `Quest: player id is down: -2000 Gold`, `Quest: revived player id`, `Quest: bled out: respawned at the checkpoint`
- `Quest: the Goblin King rises: H HP (party p), 6.0 m tall`, `Quest: Goblin King phase k: n adds`
- `Quest: the Golden Trophy drops on the throne`
- `Quest: Quest for the Golden Trophy: VICTORY|FAILED in act a, g Gold of m (rating X), kills, downs, revives`
- `Quest: back in the lobby`

**Verified.**
- The harness scenarios goldentrophy_run, goldentrophy_acts and goldentrophy_final boot with 0 step failures and cover the lobby, the start, acts 1-4, 7 and 9, pots, the boulder, the boss, victory, failure and the lobby reset.
- All three builds compile with 0 errors and 0 warnings. The server API check reports all 113 members proven.

**Still open / Not verified.**
- Everything networked: actor sync, attack replay, requests, revive, remote gear, hand-over and late joiners.
- The VR HUD and VR revive.
- The last visual tweaks: lava colour, act 3's goblins in the boulder lane, and the rock prefab name.
- Bow draw, pot breaking by projectiles, NavMesh, the king's retargeting and shield stun, potions, and the 4 m / 1 s VR teleport limit.

## Iteration 2: one origin, isolation, hands and watch (2026-09-24)

Code notes behind each change are in `DevNotes/phase1/*.md`.

### Drawn hands (Player/RrAvatarHand, 2026-09-24)
`RrHand.Grip` is the controller (the client's hand anchor, measured to be the OpenXR grip pose). The avatar's hand is drawn somewhere else: VRIK solves the arm to `LeftHandVRIKPoint` under the client's `Stabilized` node. The drawn wrist sits 8.4 cm behind the grip, and the hand is turned 26.7 deg from it.

`RrAvatarHand` gives what is actually drawn, and never touches the avatar:
- `TryGetWrist(side, out Pose wrist, out float forearmLength)`: +Z along the forearm, +Y out of the back of the hand.
- `TryGetPalm(side, out Pose palm)`: the fist centre, in the grip's axis convention.
- `TryGetRecRoomHand(side, out Pose hand)`: Rec Room's hand frame on the drawn hand (+Z fingers, +Y thumb side).
- `GripToPalmDefault` / `GripToRecRoomDefault`: constant grip-space poses for props parented under the grip.

Poses come from the local avatar's humanoid bones (orientation from bone positions, so any rig works). Fallbacks, in order: bones by name, the IK target plus the client's constants, the grip plus the same constants.

Read it at LateUpdate order 10000 or later, or in beginCameraRendering; the client's IK has finished by then.

Rec Room's `Tool.interactionSettings` hold poses are the tool's local pose in Rec Room's hand frame: tool = hand * (p, r). Prefer the legacy `{Left,Right}HandPickupTransform` over `FullBody*GripTransform`.

Full analysis and formulas: DevNotes/phase1/hands_frames.md. Harness: `run.sh hands_frames` (hooks in `Player/RrAvatarHandTest`; `BuildTestArm` / `BuildDefaultArms` build a synthetic arm for other tests). Not yet run in the Somnium client.

### One room at the world origin (phase 1 "place", 2026-09-24)

**Why.** Rooms used to be built kilometres apart: dorms 250 m apart around (-1125, 0, -2000), instances 2 km apart from (-5000, 0, 2000). A float32 position out there only steps by 0.24-0.98 mm, and in VR everything visibly wobbled. Every room is now built at (0, 0, 0). That gives steps of 7.6 um within 128 m, 31 um at Paintball's River and Drive-In (z ±450) and 61 um at the farthest quest act. Only the local player's current room exists, and players are kept apart by presence (activity + instance number), never by position.

**Where it lives.**
- `Rooms/RrPlacement.cs`: RoomOrigin, SceneSpawn. `DormOrigin` and `InstanceOrigin` now return the origin.
- `Rooms/RrTravel.cs` and `Rooms/RrTravel.Origin.cs`: the arrival slab, leaving a room, the census, memory, and rescue.
- `Core/RrAssets.cs`: room scopes and `Release`.
- `Rooms/RrTravelTest.cs`: harness hooks.

**Leaving a room**, behind the loading card:
1. The scene's "Arrival" slab comes back (renderers and colliders on).
2. OnLocalExit runs.
3. Held items go back under the room.
4. The player waits on the arrival spot.
5. Shared systems are cleared: RrFx effects, pooled 3D sounds, paint splats and impacts, shots in flight, given tools.
6. The room is destroyed, then one frame passes.
7. Memory is freed (`RrAssets.Release`, below).
8. A census line is logged.

RrSpawn now drops a spawn whose parent was destroyed before its prefab finished loading. Before, such copies landed at the scene root, inside the next room.

**Memory.** Each room gets an asset scope. On leaving, RrAssets forgets that room's cached assets, prefab data and material replacements, then runs Resources.UnloadUnusedAssets. It also calls Unload(false) on bundles only that room opened, but only when:
- no load is in flight,
- every asset handed out from them is dead,
- the bundle holds no audio, video, shader or font.

Log lines: `Travel: freed <room> in N ms: memory A -> B MB ...`.

**Arriving.** The room is built, its lowest floor is measured (Environment colliders), the player is teleported to the spawn, and then the arrival slab is hidden. The log line `Travel: census at <room> (visit n): 1 room, nothing outside it; room <room> at (0, 0, 0), feet ..., world root N objects (...), scene roots R, memory M MB` proves the old room is gone. After a leave the line reads `no room, nothing of it left`. Anything else prints `LEFT BEHIND` or `OUTSIDE THE ROOM` with the first stray object's path.

**Rescue** replaces the old "far from my origin" check:
- **Fall:** feet 10 m below the room's lowest floor (checked at 2 Hz).
- **Client respawn:** a jump of more than 3 m in one frame onto the scene spawn (0, 0.9, 0), with no RrPlayer.Teleport in the last 2 s (checked every frame).

Either one sends the player to the match's respawn point while a match runs (RrHealth), otherwise to `RrRoom.RescuePose` (default: the spawn). Sideways distance is never checked, so Paintball's maps and the quest acts are safe. All our own moves go through RrPlayer.Teleport, which records LastTeleportAt.

**Harness.**
- Shot steps accept `"frame": "room"` (pos and look relative to RrTravel.CurrentRoom.Origin; default world).
- Scenarios accept `"arrival": true`, which keeps the player in the arrival room. Use it for tests on the Arrival floor.
- Existing scenarios were converted by subtracting each room's old origin: dorm slot 0 (-1125, 0, -2000); RecCenter (-3000, 0, 2000); Charades (-1000, 0, 2000); Dodgeball (-5000, 0, 4000); Paddleball (5000, 0, 4000); Paintball (-5000, 0, 6000); GoldenTrophy (-1000, 0, 6000).
- `travel_origin.json` goes Dorm → Rec Center (simulated fall and simulated client respawn) → Paintball (paint fired on the River) → Dorm, and asserts every census line. Last run: 40/40.

**Not verified yet:** the Somnium client (desktop or VR), the real client respawn and Respawn button, and online instances.

### Players in other places: isolation (Player/RrIsolation) (2026-09-24)

Every room is built at the world origin, so a player in Paintball stands inside your Dorm as far as Unity knows. `RrIsolation` hides, on this client only, everyone who is not in your place and silences them. Everything is restored exactly when they rejoin you, and on world unload. `RrRemotePlayers.Tick` drives it every frame (LateUpdate, order 10000, after the client), and `RrRemotePlayers.Reset` shuts it down. Nothing to call.

**Who is with you** (`RrIsolation.IsWithMe(id)`, `Remote.WithMe`): the same activity and instance number in `RrNet.Presence`.
- Apart: unknown presence, either of you Loading, different activities, or you are in the Dorm or an unnetworked room.
- Together: the arrival room (activity None) counts as shared.
- Hysteresis: hidden after 0.5 s apart, shown after 0.35 s together, decisions every 0.2 s. The first decision about a new player applies at once.

**What a remote player is, and how each part is switched** (client evidence in DevNotes/phase1/isolate.md). Only one of the four parts is under Body.Root:
1. The body prefab (`Body.Root.parent`): the CharacterController and head trigger (`Collider.enabled` false), the feedback VFX, and next to Body.Root the name tag `SocialSystem.User.Tag` (CanvasGroup alpha 0 and blocksRaycasts false; the client toggles Canvas.enabled on every frustum change) and the `QuickChat.Bubble` (Canvas.enabled false).
2. The avatar, a scene root `Avatar for Player [nick] [longId]`.
3. Cloth and item roots `Simulator Item: [..]`.
4. The voice, in the player's own Fusion NetworkObject.

All renderers use `forceRenderingOff`, which the client never writes, so its own `enabled` toggles survive. The voice gets `AudioSource.volume` 0, never `mute`, because mute is the client's mute and block flag (RemoteVoiceSpeaker.SetMuted and SetBlockValue; VoiceSpeaker.IsMuted reads it). A block made while someone is hidden therefore survives, and a level set from the profile slider while hidden becomes the restored level. Other sounds are muted, and only the ones we flipped are unmuted later.

**API**: `IsWithMe`, `IsHidden`, `HiddenCount`, `OnVisibilityChanged(id, visible)`, `Enabled` (master switch), `Describe()` (counts, sweep and voice-pass cost). Test hooks are used by `RrIsolationTest` (scenario `isolation_basic`: a fake remote shaped like the client's, 18 checks).

**Cost** (editor harness): budget 1.5 ms per frame. Cold first sweep 3-4 ms and cold first hide 3-5 ms, in separate frames. 48 fake players (1440 renderers) hidden in about 0.8 s, worst frame under 1 ms.

**Known limits**
- The client's avatar culling keeps full avatars for the nearest 8 players within 15 m, by distance only. Hidden players near the origin still take those slots, so with more than 8 players a friend in your room can get the dummy avatar. This cannot be fixed without touching the client.
- A voice level set to exactly 0 while the player is hidden is lost on restore.
- An admin item mute on a hidden player's item is undone on restore.

**Two-client checklist (not tested yet)**: for each remote in another room, expect "player N (..): K sound(s) found in network object '...'", a "hidden: ..." line with "voice(s) silenced K" (K >= 1), and no "no sound found ... after 5 sweeps" warning. You should not hear them, and you should hear them again at their own level when they walk in. Block someone while they are elsewhere: they must stay blocked when they arrive. Record `RrIsolation.Describe()` for the real sweep and voice-pass cost. Test with more than 8 players for the culling limit.

Games: skip remotes that are not `Remote.WithMe` in anything you draw or test for other players (hit volumes, tags, zone checks, enemy targets).

### Held items follow the hand (Interact/, Player/RrHand, Net/RrNetBody) (2026-09-24)

The user's VR report: "Grabbing items are slightly off, it feels like they need to be rotated up 90 degrees, make them follow the same orientation as the hand."

**Why it happened.** Rec Room's hold poses (`Tool.interactionSettings.{Left,Right}HandPickupTransform`, `FullBody{Left,Right}HandGripTransform`) give the TOOL's local pose in Rec Room's hand frame (+Z fingers, +Y thumb side, +X out of the left palm / out of the back of the right hand). The first build read them backwards, as the hand's pose in the tool (`Inverse(gr)`, `-(Inverse(gr) * gp)`). It also applied them directly in `RrHand.Grip`, the OpenXR grip frame, whose +Z is the thumb axis. In an upright fist that put a bottle 96.5 deg off upright (along the forearm) and a pistol 110 deg off the aim (at the floor). It also preferred the FullBody poses, which are relative to `HandVisualPickupInfo.RootOverride` when one is set (the pistol's is turned 180 deg).

**What it does now.**
- **VR:** tool = grip * `RrGrab.HoldFrame` * (gp, gr). The hold frame is `RrHand.RecRoomFrame` = `RrAvatarHand.GripToRecRoomDefault`: the hand the client draws, 26.7 deg from `AimRotation` (the drawn fingers point 24 deg above the front of the fist and 12 deg outward). The drawn palm centre sits on the grip centre (0.1 mm at the default IK), so props stay parented to the grip and keep the late BeforeRender pose. Throws still use the controller's velocity. Items without a Rec Room pose are held where they were taken, or centred in the fist when given.
- **Screen mode:** each tool is held where Rec Room's screen mode holds it. `RrScreenHold` reads the tool's `screenSettings.IdleAnimation`: frame 0 of the right hand plus its offsets, relative to Rec Room's screen camera at (0, 1.55, 0.01). It reads the tool's own bundle at runtime, falls back to a measured table, and otherwise uses Rec Room's `Screens_Default_Idle_HoldingTool_02`. With these poses the paintball pistol's barrel is 4.1 deg from the view's forward and a cup stands 3.4 deg from upright. Items without a pose are centred at `ScreenOffset`, upright to the view.
- **Reading the poses** (`RrTools.ApplyHold`, also used by `RrWeapon` and `RrMelee`): legacy first; FullBody only when the legacy pose is zero, carried back from `RootOverride`; a missing side is mirrored (x -> -x; (x, y, z, w) -> (x, -y, -z, w)).
- **Other players see it too:** `RrNetBody.WireHeldPose` sends the pose relative to the client's hand anchor, in metres, without the rig's scale. Desktop holders, who have no Bridge hands, are drawn at their head in their body's yaw; before, their props stayed where they were picked up.
- **Fixed on the way:**
  - The paintball flag carry was upside down; it now uses Rec Room's `(FlagTool)` pose.
  - The VR button ray pointed up the thumb axis; it now uses the hand's aim.
  - `RrMelee` found the sword's blade tip from the wrong point; it now uses the hand's position in the sword frame.

**Verified** in the harness (`grab_pose.json`, 40/40 steps, 24 checks). An upright fist on a scaled, offset fake anchor holds:
- the bottle 17.9 deg back from upright (it follows the drawn fingers);
- the pistol barrel 11.4 deg from the aim;
- the basketball centre 12.6 cm on the palm side;
- the paddle head 17.7 deg from up.

Each object sits exactly at the formula's pose, and the wire pose rebuilds within 0.6 mm. Regressions: interact 6/6, paintball_all 29/29, paddleball_court 18/18, charades_play 23/23. dorm_room fails only its basket step, which also fails in the baseline.

**In the headset (not done yet):**
- Look for `Grab: first VR pickup: ...` and `Grab: <side> hand: the drawn palm (<source>) sits X cm and Y deg from the frame held objects use` in Player.log.
- If the pistol's 10.7 deg outward yaw feels wrong, make `RrHand.RecRoomFrame` pitch-only (`GripToAim * Euler(-23.9, 0, 0)`); it is a one-line change.

**Known gaps:**
- Rec Room's base screen FOV is unknown (ours is 60), so some screen-mode items sit low: the bottle is cut by the bottom edge and the paddleball paddle is mostly off-screen.
- The pool cue has no IdleAnimation and uses the default screen hand.
- There is no two-handed hold.

**Test hooks:** `RrHand.SetTestVr` / `RrHand.TestView` (internal, harness only) and `Interact/RrGrabTest.cs`.

### The watch on the drawn wrist (Watch/, 2026-09-24)

**What changed.** In VR the watch now sits on the wrist the avatar draws, not on the controller. The user's report was "floating off the avatar". The first build put it at `grip.TransformPoint(-0.015, 0, -0.1)` with Rec Room's anchor rotation. Measured on the client's default-IK arm, that is 10.3 cm from the right place: 9 cm off the forearm and 3 cm into the hand. The strap was also turned 66 deg, because Rec Room's rotation is written in its hand frame, which is the grip turned 90 deg about X.

`RrWatchModel` now places the watch from `RrAvatarHand.TryGetWrist`:
- **Position:** centre on the forearm axis, 1.8 cm behind the wrist joint (Rec Room's full-body value), times forearm/0.25 m.
- **Rotation:** face out of the back of the hand; 3 o'clock toward the fingers on the left wrist and toward the elbow on the right; 12 o'clock and the strap on the little-finger side. Left = wrist * (0, -0.7071, -0.7071, 0), right = wrist * (-0.7071, 0, 0, 0.7071).
- **Size:** Rec Room's `Wrist_Watch` at 0.94 times the avatar size, clamped to 0.7..1.4. This is the snuggest fit on RPM Women's forearm.
- **Timing:** execution order 10010, after the client's IK, which runs in LateUpdate at order 0. The menu (`RrWatchUi`) runs at 10020.
- **Fallbacks, logged when they change:** the client's VRIK target, then the grip with the client's IK offsets, then the grip with the first build's offset.

The gaze gesture, the menu above the wrist, tap-to-open (the other hand's drawn fingertip) and the right-wrist option all use this pose.

**Sleeves (`RrWatchFit`).** Measured in the harness, RPM Men wears a sweater that is 126 x 119 mm at the strap, and a watch placed for bare skin vanished inside it. Once per avatar, outfit or wrist change, the watch fits itself to the avatar:
- It reads the local avatar's readable skinned meshes around the drawn wrist. Only vertices driven by the lower-arm bone or bones below it count, and triangle midpoints are added for low-poly sleeves.
- It lifts or lowers the watch so the case sits 4 mm above the surface (range -12..+35 mm).
- Measured: RPM Men +26.4 mm (on the sweater), RPM Women +2.1 mm (bare wrist). The runtime sampler agrees with Unity's own BakeMesh skinning to within 0.4 mm. It costs 0.45-0.73 ms per avatar in the editor.
- Without readable meshes the default lift is 1 mm.

**In the next VR log, look for:**
- "wrist watch on the left wrist from the avatar's drawn wrist (HumanoidBones)"
- "the first build's grip-based watch would sit X cm from the drawn wrist's"
- "wrist watch fitted to the avatar's forearm: ..." or "no fit ... (N not readable)"
- "menu opened by looking at the left wrist (view, pitch, roll)"

**Test.** `SP/harness/scenarios/watch_wrist.json` runs the hooks in `Watch/RrWatchWristTest.cs`: 33 checks plus MEASURE lines, and photos of a synthetic arm, the RPM Men and RPM Women arms, and the right wrist. It also runs gaze, follow, close and tap end to end. Result: ALL PASS. Nothing has run in the client yet. The design notes and every measurement are in DN/phase1/watch.md.

## Iteration 4: Golden Trophy from its real levels, final quest rules, the real Dorm layout, Dodgeball's final rules (2026-09-24)

These supersede the older Golden Trophy, Dorm and Dodgeball notes above where they differ. Notes in `DevNotes/iter4/*.md`; the reference pipeline is in `DevNotes/tools/rrscene` and `DevNotes/refdata`; coverage and the plan are in `DevNotes/research/rr_reference_coverage.md`.

### Quest for the Golden Trophy on Rec Room's own levels + the scene kit (iteration 4, 2026-09-24)

**What changed.** The lobby and acts 1-6 are Rec Room's official levels. They come from DN/refdata GoldenLobby and GTact1..6, the Studio ports of Quest_Foyer, Quest_Hallway1-4, Quest_DeadIsland and Quest_Library. The research-built acts 1-6 are gone. Acts 7-9 (maze, half-lava hall, Goblin King) have no reference scene and stay research builds, now with the wiki's act-8 wave order. The scene kit (Core/RrSceneKit*.cs, API in DN/iter4/kit.md) is installed, and RrMain.OnDestroy calls `RrSceneKit.Shutdown()`.

**Key fact: the walls and floors are missing.**
- Every act kept its shell (walls, floors, ceilings) in one static batch per material, e.g. "Hallway1StaticBatch_Wood_Floor_Wide_mat". Those meshes are in neither REF2 nor the install, and the scenes have no NavMesh and no light probes.
- The kit builds what the scenes do have: Rec Room's props, colliders, lights and markers, at their exact poses.
- RrGoldenTrophyShell rebuilds the rooms. It uses the rooms' reflection-probe boxes and the heights of the props, spawners and pots.
- A spec lists floors (solid blocks), ramps and doorways in scene coordinates. The walls are generated around the floors on a 0.5 m grid, and a dark ceiling that casts no shadow goes on top.
- Floors use Rec Room's wood and walls its flagstone, through RrQuestKit's tiling pass.

| File (Games/Quests/GoldenTrophy) | Role |
| --- | --- |
| Scenes/RrScene_GoldenLobby, RrScene_GTact1..6 | Generated placement data (rrsk2cs.sh, RRSZ1, 167k characters in all). |
| RrGoldenTrophyShell.cs | Floors, ramps, generated walls, doorways and ceiling. `FloorAt`, `BuriedIn`, `NearWall`. |
| RrGoldenTrophyArea.cs | Parse, clean-up, kit build, shell, dressing of the stand-ins, markers (arrival, exit, spawners), pots, weapon racks, and the exit door with a small lit room behind it. |
| RrGoldenTrophyRefActs.cs | The lobby and acts 1-6: shell specs with the measured numbers, spawn groups and waves. |
| RrGoldenTrophyRoom.cs | BuildLevel, `ActSlot(i)`, `ShowAreas`, lobby spawns from Player1..8SpawnPoint, and the harness hooks `TestAct1..6`. |

**Placement.**
- The lobby is the room's origin: the centre of its eight spawns.
- Act i's root is `ActSlot(i)`, a 200 m grid: acts 1-3 at z 200 (x 0 / 200 / -200), acts 4-6 at z 400, acts 7-9 at z 600. Every area is within ~650 m of the world origin.
- Act-local = scene point - anchor, where the anchor is the centre of Player7SpawnPoint (1..4).

**Build and frame time.**
- Everything is built behind the loading card; the harness goto takes 4.7 s.
- Pots and racks get their network ids from build order, so building acts later would renumber them.
- Only the current act and the next one are active. The lobby is inactive during a run.

**Clean-up applied to every scene.**
- The WholeFloorBackup safety boxes (40 x 3.5 x 44 m and similar) are dropped.
- Stand-ins over 60 m and the ProBuilder floor pieces are dropped, and so are the LightArchShaft beams, which draw as solid orange slabs.
- Rec Room's activity-door meshes are skipped: our RrQuestDoor stands on the [Quest_Door_Active] marker instead.
- The larger stand-ins are rebuilt:

| Rec Room piece | Rebuilt as |
| --- | --- |
| Scaffolding | Wood planks |
| Banners and rugs | Red cloth |
| Feast tables | Dark wood |
| OpenCrate | Wood |
| Book stacks | Book colours |
| Hanging lanterns and lamp posts | Glowing lamps |
| Wall torches | Our torch on the nearest rebuilt wall (within 1.2 m), else left out |
| Tripod | Dropped |

- Pots and weapons become breakables and one-peg refill racks. A pot inside a rebuilt floor goes on top of it. A pot with nothing under it is set down on the floor.

**Gameplay from the markers.**
- The team arrives at Rec Room's player spawns, facing their way.
- Spawn groups are the EnemySpawnPoints, with ground points snapped onto the floors.
- Waves follow the final walkthrough. Act 4 has two side-tunnel side waves and act 6 one side wave. The spinning elite knight first appears in act 6.
- Level potential is about 141,200 Gold for 4 players.
- Act 3's boulder starts at Rec Room's [Quest_Boulder] pose, scene (62.05, 4.91, -1.84). It rolls down to the goblins' gallery when the lever is pulled.

**Log lines to look for.**
- `Quest: GTactN shell: f floors, r ramps, ... wall runs, 1 doorways`
- `Quest: GTactN: support - arrival dy 0.00, exit dy -0.01, spawners 14/14 within 1 m of a floor, pots ...`
- `Quest: GTactN: reference area at act-local offset ...: stand-ins kept k, dressed d (torches ...)`
- `Quest: GTactN: n pots at Rec Room's poses (x lifted..., y set down...)`
- `Quest: act a "Subtitle" root (...), entry ..., waves + side, groups, exit door`
- `SceneKit: GTactN: renderers ... merged ...`

**Verified.**
- Compile: 0 errors and 0 warnings, all 113 Fusion/Somnium members proven.
- goldentrophy_run: 0 failures of 43 steps. It shows the lobby, every act from the arrival and the boss room.
- goldentrophy_final: 0 failures of 33. The team goes through all six rebuilt exit doors (act 1 to act 7), the boulder kills 3 goblins, and the run ends in VICTORY.
- goldentrophy_acts: 0 failures of 38, including the fail path and "back in the lobby".
- scenekit_test: the kit test now logs PASS; the one earlier failure was the test's own line-count comparison.

**Not verified / open.**
- Walking in VR or desktop (ramps are 17-27 degrees), enemy pathing on the rebuilt floors, zone waves triggered by walking in, and networking.
- Rec Room's interior walls, balconies and ledges are not rebuilt. Props that stood on them float, worst in act 5's west hall. Act 2's big room is sparse.
- 72 of 80 wall torches found no rebuilt wall near them. Their positions mark where Rec Room's walls were, a lead for a later pass.

**Dead end, do not retry.** Wooden platforms under floating props (found by testing the colliders): one walled off act 6's arrival.

### The quests' final rules (iteration 4, 2026-09-24)

**What changed.** The quest framework (`Games/Quests/Shared`) now runs Rec Room's final (pre-shutdown) quest rules (research: rr_latest_rgames_story.md sections 5.1, 5.3 and 12.1) for every quest. `RrQuestFinalRules` is a catalog of final rules per activity. `RrQuestRoom.Build` applies it right after `CreateDef()` to every field the definition left unset (0, -1 or empty). Golden Trophy therefore gets the final rules without any change of its own, and a quest that sets a value itself keeps it.

| Rule | Final behaviour | Where |
| --- | --- | --- |
| Downs | One hit downs a player (`RrQuestDef.PlayerHealth` 1; a revive brings you back at 1). A shield or blade block still cancels a melee hit. An ally's sword or shield never downs you (registered melee tool ids 200-249 are vetoed through `RrHealth.Accepts`); an ally's arrow does. The HUD shows only DOWN. The 30 s bleed-out is ours (no source has one). | RrQuestDef, RrQuestRoom.AcceptHit, RrHealth.Accepts |
| Kill gold | Fixed per enemy type, looked up by spawner: goblin 450, imp 500, elite goblin 850, elite imp 850, barrel knight 1,500, elite barrel knight 2,000, Goblin King 5,500 (`BossKillPoints`). Falls back to max HP where nothing is published. Pots 50/100 and the 2,000 death penalty are unchanged. | RrQuestDef.KillPoints / AddEnemy(spawner, points) / SetKillPoints, RrQuestRoom.PointsFor / IsBoss |
| Rating | Fixed totals `RatingGoldS/A/B`: Golden Trophy and Jumbotron 150k/125k/100k, Crimson Cauldron 300k/275k/250k. Isle and Crescendo are unpublished and use the old share of the max possible. | RrQuestDef.Rating / FixedRating |
| Start | Coach's "Game On!" (`Gameon`, bundle 8b1b2e31 #3496784870384460132, 2.00 s) plays as the run starts. No "Ready" and no "Game Over" in quests. | RrQuestDef.StartLine, RrGameRoom.PhaseAnnouncement (virtual; unchanged for other games), RrAnnouncer.Say(clip, volume, loaded) |
| Boss | Its death kills every other enemy (no gold, GUESS) and drops the act's remaining waves. This happens automatically; `AuthorityBossDefeated(boss)` can also be called and is safe to repeat. | RrQuestRoom, RrQuestEnemies.KillAllExcept |
| Players | Golden Trophy, Crimson Cauldron and Jumbotron 1-4; Isle and Crescendo 1-3; the minimum is always 1. Wave sizes scale to party / MaxPlayers. | RrQuestDef.MinPlayers/MaxPlayers, RrQuestWave.Scaled(count, party, maxParty) |
| Tunnels | Optional side waves: `act.SideWave(zoneCenter, zoneSize).Add(type, count, group)`. Each spawns once when a player enters, in any order; the act never waits for it, and its gold is paid only if killed. | RrQuestAct, RrQuestDirector, quest int 31 |
| Racks | `RrQuestRack.CreateOnWall(parent, floorPointAtWall, yaw, label, spacing, weapons...)`; `DressingRoomWall(parent, pos, yaw, sword, shield, bow)` = the final dressing room's 4 swords, 2 shields and 2 bows; `Restock()`, `Count`, `HangRotation(prefab)`. | RrQuestRack |
| Mods | The RRO mods (2025-12): Turbo (enemies x1.5 speed, x0.67 cooldown and wave gaps), Coconut Chaos (a `(Quest_Pirate1_CoconutBomb)` from 12 m over each standing player every 6-10 s; blows up 4 s after landing, 2 m, 80 damage or a down) and Laser Weapons (racks hold `Arena_Shotgun`/`AutomaticGun`/`RailGun`/`Pistol` for sword/shield/bow/crossbow; laser hits stun like arrows). All numbers are GUESS. They are bits in quest int 6, toggled only in the lobby (ReqMods); coconut drops use event 70. `ModBoard(parent, local, yaw)` places the board. A Turbo + Coconut Chaos win shows the "Turbo Goblin" toast. | RrQuestMods |

Quest ints 16-30 are quest-specific (`RrQuestInts.LastQuestSpecific`). Int 31 holds the side-wave bits and int 6 the mods.

**Harness.** `RrQuestTest.Start` sets `RrQuestRoom.TestGuard`, so hits on the harness player are logged, not counted; one-hit downs would otherwise end every scripted run. `RrQuestTest.Down` still downs the player. New hooks: `Rules`, `KillBoss`, `LobbyKit` (a mod board and a test wall rack in the lobby), `ModsAll`, `ModsOff`. Scenario: `gtrules.json`.

**Log lines to look for.**
- `Quest: final rules: one hit downs, players 1-4, death -2000, kill gold ... boss 5500, rating S 150,000 / A 125,000 / B 100,000 Gold, start line Gameon`
- `Quest: level potential with 4 players: about N Gold from W wave enemies and P pots ...; S needs 150,000, Rec Room's best ~180,000`
- `Quest: Coach: "Game On!" (Gameon, 2.00 s, 44100 Hz)`
- `Quest: one hit downs: a D-damage hit from ... downs the local player`, `Quest: an ally's melee hit ... does not down`, `Quest: harness guard: ...`
- `Quest: the boss is down: n other enemies die with it (act a)`
- `Quest: act a side wave k triggered (optional: paid only if killed)`
- `Quest: mod board built ...`, `Quest: mods: TURBO + ...`, `Quest: rack restocked: (QuestSword) -> (Arena_Shotgun) (4 pegs)`, `Quest: Coconut Chaos: first drop ...`, `Quest: Coconut Chaos: first blast ...`
- `Quest: ... VICTORY in act 9, g Gold of m (rating X by the fixed totals S 150000 / A 125000 / B 100000) ...`

**Verified.** All three builds compile with 0 errors and 0 warnings, and all 113 server API members are proven. `gtrules.json` passed 46 of 46 steps: rules, rating edges, Game On, guard, mods, rack swap, coconut drop, the boss killing 4 adds, and victory rated C by the fixed totals. `goldentrophy_run.json` passed 34 of 34 steps.

**Open.** On the old level, 4 players can earn only about 70,750 Gold, so S is out of reach; the rebuilt levels need about 2.5 times more to match Rec Room's ~180k. Not verified: bows hanging side-on, Laser Tag guns barrel-up and the lamp colour (all changed after the look shot); networked mods and coconuts; Turbo and the laser guns in play; side waves in a real act; catching coconuts (not built). The mod board still has to be placed in the Golden Trophy lobby.

**Furniture (iteration 4: the real DormRoom-2018 layout).** The positions and yaws come from REF1's DormRoom-2018 export (rr_room_exports §3.1), whose props sit in the kit's room frame.
- **Door and spawn:** the exit door (RrPortal, to the Rec Center) is on the left wall under the loft at (-4.88, 0, -0.86), facing +x. Its sign is lowered 19 cm to fit under the loft (underside 2.64). The spawn is on the welcome mat at (-3.97, 0.05, -0.92), facing into the room.
- **Under the loft:** one armchair (-3.73, 1.96) at yaw 132.6; a coffee table (Sandbox_Table_Short, scale 0.815) with a ping-pong paddle and ball; fig and snake plant; the dracaena at the loft edge (-2.66, 2.10); the notebook posters on the loft's back wall.
- **Ladder:** at z 1.27 in the railing gap (the dracaena takes the gap's +z half).
- **Back wall:** the bunk bed and desk at (1.86, 0, -2.17), yaw 0. On the desk: the export's radio, 2 cups, water bottle and 2 darts, plus iteration 3's root beers, bell, book, lamp, pizza, apple and dice. The Play board is at (-0.97, 1.70, -2.90), between the loft wall and the bed. The bucket stands by the back wall.
- **Front wall:** the mirror under the arched window at (0.88, 1.34, 2.92). Beside it, from left to right: Code of Conduct, HELP panel, mirror, MIRROR panel, campus map.
- **Loft:** a MoveableWhiteboard at (-4.35, 2.75, -1.24), yaw 91 (it replaces the art canvas), with the marker set, an eraser and a clipboard, plus the trophy and book.
- **No dresser.**

**Hoop:** pivot (3.893, 1.41, -1.415) at scale 0.94, 2.5 cm proud of the pillar face so that the wall stripe (22 mm proud there) no longer covers the backboard. The rim is 27 mm from Rec Room's saved rim VFX position (3.905, 1.097, -1.090).

**Props (25):** basketball, office bin, bucket, 2 clipboards, frisbee, radio, 2 cups, water bottle, 2 darts, 2 root beers, bell, 2 books, desk lamp, pizza, apple, dice, paddle, ping-pong ball, eraser, trophy.

**Rec Room values used:**
- Wall stripe (rrtool): 6 mm thick boxes at y 1.43..1.85, standing 8 mm off the front and back walls and 22 mm off the pillar face. The Code of Conduct hangs 2 cm off the wall, the panels 1 cm.
- Spawn: DormRoom-2018's welcome mat (-3.974, 0.044, -0.921), facing +x.

**Test hook:** `RrDormTest.Layout` checks walls, loft slab, beam, railing, shelves, stripe, the ladder's climbing space, floating props and pairwise overlaps. Last run: 49 objects, 0 problems; the only contacts left are plant leaves over the chair and table.

**Dodgeball (iteration 4: Rec Room's final rules, June 2026).** Changes over iteration 1:
- Rules: only the player hit by a live throw is out, and a ball that has hit someone is dead. A catch makes the ball dead and puts NOBODY out ("no more catch-outs", 2025-01-14). `RrDodgeballRoom.CatchOutsThrower` is false; true brings back the 2021-2024 catch-out. Nobody comes back before the next round: the teammate return is gone (no source had it). Crossing the centre line or leaving the court is still out (our rule).
- Screen throw: 14 m/s however long the button is held (2021-04-15: holding only keeps the ball in hand). 7 degrees of lift and aim assist at 9 m / 30 degrees are unchanged.
- Players: 2-6. Two players auto-start a 1 v 1 after the 15 s lobby; alone, START is practice against the standees.
- 2026 map change: a low padded barrier across each end, 0.5 m x 10 m x 0.2 m, centred 0.9 m behind the back line (z +-8.9). The size and spot are a GUESS. A player fetching a ball there is never out. Balls resting between the back line and the barrier stay in play; other balls resting off court go back to the centre line after 3 s. The arrival row moved to z -9.7, behind the blue barrier; players walk round its ends.
- Physics: every gym collider gets Rec Room's DodgeballCourt_phys numbers (friction 0.6 / 0.6, bounce 0.6, combine Average; from REF2 PhysicMats). The ball keeps the install's DodgeballBall_phys (0.6 / 0.6 / 0.6, friction Maximum, bounce Minimum; ART #-6279935958218146749). Before, the floor had Unity's default bounce of 0, so the balls did not bounce. Measured now: a 1.39 m drop bounces 0.56 m (bounce 0.63). The REF2 copy of the ball material has bounce combine Multiply; the install's (Minimum) is used. Unity's combine enum is Average 0, Multiply 1, Minimum 2, Maximum 3.
- Net: the verdict (event 67) is 9 bytes: kind, ball, throw id, victim (0xFFFF = nobody out), other (the hit's thrower or the catcher), round.

New or changed log lines:
- `Dodgeball: court physics: DodgeballCourt_phys (0.6 / 0.6 / bounce 0.6, Average) on 25 gym colliders; barriers 0.5 m high at z +-8.9, 10 m wide`
- `Dodgeball: ball collider SphereCollider material DodgeballBall_phys: friction 0.6 / 0.6 (Maximum), bounce 0.6 (Minimum)`
- `Dodgeball: gym built: ... screen throw 14 m/s at any charge / 15 m/s max ... final rules: catch = dead ball, catch outs thrower False`
- `Dodgeball: ball i: first screen throw at 14.0 m/s after holding X s`
- `Dodgeball: caught ball i thrown by P (throw id N): dead ball, claiming the catch`
- `Dodgeball: catch by P beat a pending hit on the same throw`
- `Dodgeball: verdict catch: dead ball, nobody out, caught by P` / `Dodgeball: verdict hit: out X, by P`

Harness: dodgeball_gym (31/31 steps). Hooks are RrDodgeballTest.Start / Throw / Bounce / Catch / Opponent. Catch: the fake opponent 77 throws a live ball at the local player, a hit claim and a catch of the same throw arrive, the result is a catch verdict with nobody out.

Untested: two real clients (a real 1 v 1), a real VR catch and the screen catch assist, a real mouse throw, walking round the barriers with a Somnium avatar, match over back to the lobby. Not built: the 2026 mods (Mod Board with Low Gravity, Dodge-Bombs, Disco Dodgeball).

## Iteration 5: Rec Room's post-processing, level effects and sounds (2026-09-24)

Notes in `DevNotes/iter5/*.md`.

### Post-processing: Rec Room's own look per room (iteration 5, `Core/RrPostFx`, `Core/RrPostFxProfiles`)

**Why.** In a world, the Somnium client applies only its URP default profile, and that profile sets everything to neutral: no tonemapping, no bloom, no grading. Its graphics settings do not reach a world's volumes, and the world scene has no Volume. So until now Rec Room's materials showed untonemapped and without their glow.

**What.** Travel enters each room and the room's Rec Room profile fades in (0.35 s, under the loading card). Leaving fades it out (0.25 s). A quest act change crossfades the values in 1 s (`RrPostFx.SetArea`, called from Golden Trophy's QuestStep). The Volume is one GameObject, "Rr Post", under the Rec Room object:
- layer 0 (the client cameras' volume mask is Default only);
- a global Volume at priority 100, built the same way as ETS's proven "Ets Post";
- its own runtime VolumeProfile with stock URP Bloom, Tonemapping, WhiteBalance, ColorAdjustments and LiftGammaGain;
- only the parameters Rec Room overrode are overridden.

The profile is set as both the shared and the instance profile. The client's ambient-occlusion setting writes HBAO into every global Volume, and this way it lands in ours without cutting our link to it. On world exit only our own objects are destroyed, so the client returns exactly to its own state.

**The data** (`RrPostFxProfiles`, component -> overridden parameters, exactly as in Rec Room's files):

| Room | Profile | Source | Values |
| --- | --- | --- | --- |
| Dorm, Dodgeball, Paintball, Charades, Paddleball, other games | "Testing", the blank Studio scene (house look) | REF2 | bloom threshold 1.04, intensity 0.15, scatter 0.6; Neutral tonemapping; temperature +5; exposure +0.1 EV; contrast 10; lift (0.92, 0.94, 1), gamma (1, 0.98, 0.96), gain (1, 0.99, 0.96, +0.034) |
| Rec Center | RecCenter_071825 | cache 1AF | exposure +0.2, contrast 5, saturation +8, gamma (1, 0.925, 0.868), bloom 0.25 from 1.1 |
| Golden Trophy lobby and all acts | GoldenLobby (same as the house look) | REF2 | the act scenes' own profiles were never exported |
| Crescendo, Isle, Midnight (35C), Rec Royale, Jumbotron, Stunt Runner | mapped per act | REF2 / cache / install | for when those rooms are built |

8 of REF2's 16 profiles hold exactly the house values, and 14 share its family. The install's StuntRunner_Default_SSFX_Profile loads as is in Unity 6000.3 and matches the data (harness PASS).

**Comfort and cost.** No depth of field, motion blur, lens distortion, chromatic aberration, film grain or vignette: none of Rec Room's 19 profiles has them. Grading costs nothing, because URP bakes it into its lookup table. In a headset, bloom runs from quarter resolution with 5 iterations instead of half resolution with 6: the same widest glow at about half the cost.

**Log lines:**
- `PostFx: built the "Rr Post" Volume ...`
- `PostFx: <Room>: <profile> (<source>): Bloom ...; Tonemapping mode 1; ...; bloom chain ...`
- `PostFx: left <Room>`
- `PostFx: GoldenTrophy area N: ...`

**Harness:** `scenarios/postfx_tour.json`, with hooks `RrPostFx.TestOff / TestOn / TestReport / TestVrChain / TestDesktopChain / TestBloomProbe / TestBundleProfile`. It produces before/after pairs `out/postfx_tour/pfx_*_before|after.png`.

**Try in the world:**
- Walk Dorm -> Rec Center -> a quest -> a game: each room should look slightly warmer and softer, with no clipped whites.
- The Rec Center should be the warmest.
- Lamps and lava should glow faintly (Rec Room's bloom is subtle).
- Nothing should jump when the loading card lifts.

**Known limits:**
- Our own LDR glow materials soften under Rec Room's Neutral tonemapping (the quest lava turns yellower); stronger emission in those rooms would fix it.
- The client's Bloom Off setting does not reach this Volume.

### Iteration 5: Rec Room's level effects and sounds (fxsound, 2026-09-24)

Notes: `DevNotes/iter5/fxsound.md`. The converter's F format is in `DevNotes/tools/rrscene/README.md` ("F records", "Sounds").

**How it works.**
- The scene converter (`DevNotes/tools/rrscene`) now writes **F records**. Each is one looping, play-on-awake effect of a reference level, placed from an install effect prefab: the same one (`exact`), the same object inside an install prefab (`part`), or the closest by kind (`kind`), with the scene's own particle numbers (`ps.*`).
- A wall torch's flame is Rec Room's `[VFX_Flame]` embers plus a flame card: `(Sandbox_Candle)` `Candle_Tall/FirePlane_fbx`, with `LaternFlame_Mat`, the material the Golden Trophy torch meshes use for their flame.
- Ambient clips missing from the install get the closest install clip of the same kind; the original name is kept as `was=`.
- `rrscene instfx <work>` catalogues the install's 1,471 particle systems and 107 audio sources. Every scene `.md` now lists its effects (placed, triggered, unmatched), ambient loops, one-shots and its `AudioClips.asset` list.
- The scene kit spawns F records through `RrSceneKitFx`, which waits until the room is built (`RrTravel.Busy` false).
- A wall-mounted effect goes only onto a wall the room draws (a collider with its own renderer), never onto Rec Room's invisible collision boxes.
- VR limits: the nearest 40 effects and nearest 8 effect loops run (`RrSceneKitOptions.MaxEffects` / `MaxEffectSounds`), at most 150 particles per system, and loops roll off linearly.
- `RrSceneKitOptions.FlameCards = false` drops the torch flame cards.

**In the rooms.**
- **Golden Trophy:**
  - torches with flame card, embers and `campfire_02_30_loop` crackle: act 3 has 6, act 6 has 4;
  - no torch flames in the lobby or acts 1, 2, 4 and 5, where the rebuilt shell has no wall where Rec Room's torches hung;
  - act 4: `VFX_Lava_Bubbles01` over the 13.9 m lava disc, with `Lava Loop Low` (GUESS);
  - drinking fountains off (no wall).
- **Rec Center:**
  - six light shafts rebuilt from the cache scene's rig corners, in URP Particles/Unlit with `LightShaftLocker_Mat`'s values (`RrRecCenterFx`);
  - `[Sandbox_Wind_Wavy_VFX]` where `[Wind_Lines_VFX]` was;
  - Rec Room's only two ambient sources, with install substitutes: the light crowd as the onboarding people ambience, 2D at 0.135; the cafe counter as the coffee-pour loop, linear 9.55-13.08 m at (-19.74, 1.0, -1.82);
  - the iteration-3 birds and kiosk hum are removed.
- **Dorm:** unchanged. It already has Rec Room's light shafts and ambience; the dorm kit has no particles.

**Log lines to look for.**
- `SceneKit: <scene>: effects placed N of M (wall mounts: a on a rebuilt wall, b left out ...); ...; sounds k: <clip> at (x, y, z) ...`
- `SceneKit: <scene>: ambient loops started ...`
- `RecCenter: fx: 6 light shafts ...`
- `RecCenter: ambience: ...`

**Harness.** Scenario `fxsound_tour.json`; the test hook `RrSceneKitTest.Effects` prints effect positions and materials, plus shot poses for the wall effects.

**Interaction sounds: still missing (Games/Quests code).**
- chest opening;
- weapon rack refill (pop-in sound);
- the boulder's rolling (install `Bowling_BallRoll02`);
- a lava sizzle (install `Lava Touch` / `Lava Sink`).

Footsteps are left to the client.

### Quest effect sounds (iteration 5, added after the audit)
- **Lava:** a player who steps in lava hears Rec Room's floor-is-lava clips (`Lava Touch`, then `Lava Sink`). An enemy
  that dies in lava plays `Lava Sink` (`Games/Quests/Shared/RrQuestRoom.cs`).
- **Weapon racks:** a peg that refills in play pops in with Rec Room's `[VFX_ObjectSpawn]` puff and one of its four
  `sfx_ui_prop_spawn_0N` sounds, the same set RrAnimateInOut uses. The first fill at build time stays silent
  (`RrQuestRack.Peg.Pop`).
- **Boulder:** while it rolls, `Bowling_BallRoll02` loops on the rock, pitched down to 0.55 (GUESS: the install has no
  boulder clip). It stops when the boulder breaks or resets (`RrGoldenTrophyBoulder`).
- **Victory chests:** once, as the lids open, a slowed `Quest_Longbow_Creak` and the gold sound (GUESS: the install has no
  chest clip).

## Iteration 6: Rec Room's own Rec Center from the player's cache, and a warmer look (2026-09-24)

Notes in `DevNotes/iter6/cachecenter.md`.

### Rec Room's own Rec Center from the player's download cache (2026-09-24, iteration 6)

The user tested the hand-built Rec Center and called it "completely wrong". The building was never in the Steam install. It is, however, in Rec Room's download cache on the player's PC (`%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache`). That cache holds the July 2025 Studio room as plain Unity 2021.3 bundles: `1AF.data` is the scene "RecCenter-RecCenter_071825", and 44 dependency bundles (75.9 MB in all) hold its textures, meshes and materials. The Rec Center now loads that building at runtime. Players without the cache get the Dreamport rebuild as before. Nothing ships with the world.

**How it works.**
- `Rr/RrHttpCache.cs` (new, guid ef02b5ff4e09496a97a1d53a7758b455) finds the files by their content, on the worker. The files have hashed names and no URL, so it reads every UnityFS file's directory and serialized-file metadata. The scene bundle is the one whose AssetBundle container names ".../RecCenter-RecCenter_...unity". Its dependencies are the closure of its externals, mapped from CAB names to files. The per-file results are kept in `persistentDataPath/SomniumRecRoom/rr_httpcache_index.txt`, keyed by size and write time. The first scan of the user's cache takes 1.8 s (1,011 files, 344 UnityFS). Later scans take 90 ms.
- `Rr/RrUnityFs.cs` gained `ReadNodeRange`, which decompresses only the blocks a range spans.
- `Places/RecCenter/RrRecCenterCache.cs` (new, guid 07350e6d93124336829f6cd712bad470) loads the bundles with `AssetBundle.LoadFromFileAsync` and the scene with `SceneManager.LoadSceneAsync(..., Additive)`. In `sceneLoaded` it cleans the scene up and moves its roots under a container at the room-frame pose (2.05, -0.09, -7.23), yaw 90. The loaded building's colliders never stand in the scene's own frame.
- **Static batching.** 524 static-batched renderers have their vertices baked in the scene's world space, so moving their transforms does nothing. Each one gets the container as its static batch root (`Renderer.staticBatchRootTransform`, set by reflection).
- **Shaders.** 73 of the 75 renderer materials arrive on InternalErrorShader, because the Rec Room Studio shader bundle (cab-634aa40a...) is not in the cache. The shader references' ids equal stock URP ids, so each material gets a shader back:
  - Lit becomes the install's "Rec Room Studio Target/Lit". Rec Room itself swapped Studio materials to this shader, and it is the only one that shows the wall paint stored in vertex colours. With URP Lit, the walls and the REC ROOM logo come out white.
  - Particles, Unlit, Terrain and TMP go to their URP or TMP shaders. Trees are alpha-clipped.
- **Cleaning up.**
  - The scene's URP Volume is logged and destroyed.
  - The scene has no cameras, listeners, event systems or screen canvases; any would be switched off.
  - The key light is off, because the world has its sun. 39 point and spot lights stay on, with shadows off.
  - All 1,177 colliders move to layer 23. The 10 triggers are off.
  - The 2 ambience sources play Rec Room's real clips.
- **On leaving,** the scene is unloaded and the bundles are unloaded with their assets. Re-entry reloads the building in 0.5 s.
- `Places/RecCenter/RrRecCenterFit.cs` (new, guid 4c1cb3c67d384336adf7ea83eb6ae57d) and `RrRecCenterRoom.cs`:
  - With the cached building, the room drops its shell, outdoors and effects, and 54 furniture copies (`RrRecCenterLayout.KeepWithCachedBuilding`).
  - Doors, hoops, the mirror and props are fitted onto the real floors, counters and walls. Every move is logged.
  - A door is never placed behind the plane of the building's own sign; the MakerWorld collider stands 0.3 m behind the painted wall.
  - The Magic Door moves to the entrance wall at (2.6, 0, -15.44), because its rebuild spot is an opening into the cafe.
  - Our vending machine and Friend-O-Tron replace the building's two static vending machines, which are hidden.
  - The building's door signs replace ours.
  - Loose props are fitted last, onto the building or our own props.
- **Test hooks.** `RrRecCenterCacheTest` has `Probe`, `ForceFallback`, `AllowCache`, `Status` and the Lit comparisons. Scenarios: `reccenter_cache_probe`, `reccenter_cache`, and `reccenter_tour`, which now forces the rebuild.

**Log lines to look for.**
- `RecCenter: cache scan: 1011 files, 344 UnityFS, 3 scene bundles, 0 re-read, 90 ms; scene Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity in 1AF.data + 44 dependencies, 75.9 MB; missing CABs: cab-634aa40a8f2cc92082b491f41a54aec8`
- `RecCenter: using Rec Room's own cached Rec Center (scene RecCenter_071825, 45 bundles, 75.9 MB) from ...\HTTPCache in 2.6 s`
- `RecCenter: no cached Rec Center on this PC: using the Dreamport rebuild (<reason>)`
- `RecCenter: cached scene shaders: Lit from StudioTarget (Rec Room Studio Target/Lit); 75 swaps`
- `RecCenter: fitted the doors to Rec Room's building: ...` and `RecCenter: fitted the props to Rec Room's building: ...`
- `RecCenter: cached Rec Center released: scene unloaded, 45 bundles unloaded in 0.10 s`

**Rec Room's own post profile for this room.** It is logged, for the post work: Bloom threshold 1.1, intensity 0.25, scatter 0.617; Neutral tonemapping; white balance temperature +5; post exposure 0.2, contrast 5, saturation 8; LiftGammaGain lift (0.89, 0.95, 1, 0), gamma (1, 0.925, 0.868, 0), gain (1, 0.989, 0.94, 0.034).

**What differs from the user's final-day video.**
- The cache holds only the 18 Jul 2025 scene.
- It has no curved upper balcony ring on the right, no raised curved platform at the windows, no mural lounge with FEATURED ROOMS double doors, no orange "BACKPACK TOYS" corridor and no dartboard. These probably came with the August version, which is not cached.
- The doors, hoops, ping-pong tables, Friend-O-Tron, radios and mirror were Rec Room room data; ours stand in for them.

**Still open / Not verified.**
- Not run in the Somnium client yet: the 2021.3 bundle load, the reflection write, and Target/Lit under depth priming and in VR.
- Walking in VR on the adopted colliders.
- The scene-activation hitch (behind the loading card) and about 5,800 missing-script warnings per load.
- Mixed instances, where one player has the cache and another does not.
- LZMA caches on other PCs fall back to the rebuild.

**Integration.** All three builds compile with 0 errors and 0 warnings, and the server API check reports all 113 members proven. In the harness: `reccenter_cache` passes 64/64 steps (cached visit, forced fallback, re-entry, census clean on every visit) and `reccenter_tour` (the rebuild) passes 41/41.

### The look after iteration 5's test (Core/RrPostFx, Watch/RrWatchSettingsPage)
After testing iteration 5 in the world, the user asked for "too bright, it needs to be warmer". Every Rec Room profile
now gets a base correction: exposure -0.35 EV, white-balance temperature +12, bloom intensity x0.7
(`RrPostFx.BaseExposure / BaseWarmth / BloomScale`). The watch's Settings page has a new **Look** section with two live
sliders on top of that: **Brightness** (-1..+1 EV) and **Warmth** (-30..+30). The middle of each slider is the new
default. Every change is logged as `PostFx: look from the watch: brightness X EV total (...), warmth +Y total (...)`,
so a tester can send the values that look right and they become the defaults. The Settings rows are a little more
compact (84 high instead of 110) so the new section fits on the page.

## Iteration 7: Paintball from Rec Room's own levels (2026-09-24)

The Android build (a user-provided XAPK) is read as data only: nothing from it is installed, run, loaded or shipped. Notes in `DevNotes/apk/*.md`; the reader is `DevNotes/tools/apkscene`; the converted levels (Paintball plus 41 more rooms) are in `DevNotes/refdata/apk` with `INDEX.md`. This section supersedes the older Paintball section above.

### Android build as a layout reference (2026-09-24)

DevNotes/tools/apkscene reads the user's Android XAPK (2026-03-23) as data only: nothing is installed, executed, loaded or copied into the project. The level files have no type trees, so they are read with type trees harvested from the PC install's bundles, matched by exact type hash. The tool writes derived RRSK placement data to DevNotes/refdata/apk.

- **Level index:** all 114 levels are named from the base APK's scene list (INDEX.md).
- **Paintball:** the lobby and the 7 maps have their own files, Paintball_<Map>.rrsk.
  - The flag bases match the room exports within 0.001 m on all 7 maps that have one.
  - The loadable install art is 41.5% to 94.4% per map.
  - Colliders are written exactly as the level has them. Meshes that are not in the install are measured into boxes.
  - Terrain is a new H height grid: at most 2 m cells in the play area, a coarse far grid beyond. The format is in tools/rrscene/README.md; the kit does not build H yet.
  - Markers: spawns with teams, flag bases, weapon pickups with their mode set, and bounds, team, kill and teleport-block volumes.
- **Other rooms:** 41 more files: the older built-in Rec Center, the Dorm, Dodgeball, Paddleball, Charades, Laser Tag, Soccer, Disc Golf, Bowling, Golden Trophy rooms, Isle rooms, Rec Rally and Stunt Runner.

To rerun: `bash build.sh`, `bash run.sh bank <work>`, `bash run_paintball.sh <work>`, `bash run_rooms.sh <work>`, `bash run.sh scan <work> <refdata/apk>`, `bash index.sh`.

### Paintball: Rec Room's 2025 lobby and all seven final maps from its own levels (2026-09-24)

**What changed.** The first build's hand-made lobby, River and Drive-In are gone. The 2025 lobby and all seven maps at the shutdown are now Rec Room's own levels. They are read from the Android build (level113 lobby; level14 River, 15 Homestead, 16 Quarry, 17 Clearcut, 18 Spillway, 103 Sunset Drive-In, 104 Merry Midway) and rebuilt from the player's install. The build is data only: the scene files hold names and numbers.

**Placement: one map at a time.**
- The lobby is at the room origin, at the centre of its 8 pregame spawn points.
- The map chosen in the lobby is built in the background at room-local (0, 0, -560), with its flag-base midpoint there.
- It is torn down when another map is chosen. The area you are not in is hidden.
- Why:
  - All eight levels at once would be about 8,000 renderers, 33,000 colliders and 16 terrains in memory, and a long entry build.
  - Rec Room itself had each map as its own subroom.
  - A map builds in 0.1-1.9 s once assets are loaded.
- The far terrain is clipped at 330 m, so the room stays within ±900 m.

**How a level is built (`RrPaintballLevel`).**
1. The RRSZ1 text is inflated and parsed on the worker.
2. `RrSceneKit` places Rec Room's own meshes and materials, its colliders exactly as the level has them, lights, loops and markers.
3. `Core/RrSceneKitTerrain` builds the H height grids as meshes and colliders:
   - near grids carry colliders;
   - far grids leave out the cells their near grid covers, and sit 8 cm lower;
   - a building's merged-mesh far grid is dropped;
   - the grass is a plain maker-pen colour, because the install has no grass texture and Goblin_Ground_Green_Mat is a patterned quest tile.
4. `RrPaintballDress` rebuilds what the install lacks, merged per surface and 48 m cell:
   - The editor-merged forts, sheds, dam and lobby building are drawn from the level's own box colliders inside them, so they have real walls, doorways and windows.
   - Aspens, bushes and pines are low-poly, in the level's own crown colours. Cliffs are faceted boulders.
   - Other stand-ins take their surface by material name: the install material when the level's is in the install, else Rec Room wood or grey concrete, else a colour (containers, cranes, tarps, lit light cubes).
5. `RrPaintballMap.FromMarkers` reads the gameplay data:
   - spawns: each team gets the Team1/Team2 group nearest its own flag, because the levels' naming differs per map;
   - the flag bases and the flags' home poses;
   - the weapon spots, the KillFloor, and the Scoreboard_V2 boards (clustered).

**Scene data pipeline.** `bash DevNotes/tools/pbscene/pbprep.sh` does it all:
1. It reruns `tools/apkscene` with `APKSCENE_DEBUG=1`, so stand-ins carry their mesh and material names.
2. `pbprep.awk` rewrites the records:
   - Stand-ins become `G dress <kind> ... e= c= m= [vox=1]`.
   - Unity-cube renderers become `G dress cube`.
   - "LOD0"/"LOD1" tree-crown renderers become `G tint`. The kit would otherwise load an unrelated install mesh of that name.
   - A material the install lacks becomes the install's own material for that mesh.
3. `rrsk2cs.sh --mode z --quant 3` writes the generated files.

Results: `DevNotes/refdata/apk/dressed/*.rrsk`, and `Games/Paintball/Scenes/RrScene_Paintball_*.cs` (1.09M characters for all 8). `topdown.awk` draws an SVG plan of a level for layout work.

**The lobby: our controls where Rec Room had its equivalents** (`RrPaintballLobby`):
- **START:** Rec Room's own START button on its pedestal by the tyre stacks, at the Practice Arena gate. START only, and at least 2 players.
- **Match board beside the gate:** the 7 maps, Capture the Flag / Team Battle, Join Blue / Join Red. The chosen map and mode light yellow.
- **Scoreboard:** "LAST MATCH", on the lobby's leaderboard projector.
- **Rec Center door:** in the level's exit-door frame.
- **"Try 'em out!" wall:** every weapon hung on it can be taken to practise (pistol, burst rifle, shotgun, scope rifle, paint launcher, shield, paint thrower).
- **Shooting gallery (`RrPaintballGallery`):** the counter START gives a 30 s round on its 3 standees and 6 bullseyes, with NOW / BEST shown. The gallery pistol lies on the counter.
- **Radio:** the level's own radio stays on its picnic table. The lobby plays the chosen map's PaintballLobby_*Radio station (River's for Merry Midway, which has none).
- **Arrival:** at Rec Room's pregame spawns in the locker building.

**Maps**

| map | level | install-mesh match | build (warm) | spawns Blue/Red | pickups |
| --- | --- | --- | --- | --- | --- |
| River | level14 | 57.0 % | 0.1-0.3 s | 14/14 | 25 |
| Homestead | level15 | 72.4 % | 0.2 s | 8/8 | 23 |
| Quarry | level16 | 49.2 % | 0.5 s | 14/14 | 26 |
| Clearcut | level17 | 67.7 % | 0.5 s | 20/15 | 27 |
| Spillway | level18 | 44.8 % | 0.4 s | 17/20 | 18 |
| Sunset Drive-In | level103 | 94.4 % | 1.4 s | 15/15 | 30 |
| Merry Midway | level104 | 41.5 % | 0.8 s | 15/15 | 27 |
| Lobby | level113 | 56.7 % | 3.6 s (on entry) | 8 arrival spawns | 8 practice |

Music: RiverPredator, PtblHomestead_Varmint, Spillway and PtblSunset for their maps. The others play PaintballManager's RiverPredator (GUESS). The Clearcut and Spillway ambience loops play.

**Final rules** (rr_latest_rgames_action.md 1.2):
- 4 v 4. One hit and you are out. Respawn after 5 s at the team spawn farthest from the nearest opponent, with 5 s of protection.
- CTF: 3 captures or 600 s. Team Battle: 50 hits or 600 s. The countdown is 10 s. START only, with at least 2 players.
- Only head and torso count.
- Bullets never hurt teammates; grenade, mine and paint-launcher splash does (GrenadesCanDamageTeammates = 1). Friendly fire is on for the shooter, and each victim's `AcceptHit` rejects a teammate's non-splash hit.
- The weapons are the level's own "Weapons" set, where the level puts them. The pistol is handed out at spawn.
- Drive-In has 2 paint throwers and 6 paint-mine piles. Merry Midway has a Splat Hammer at the level's Hammers-Only spot nearest each base (GUESS).
- Grenades (`RrPaintballThrowable` / `RrPaintballBoom`): the trigger or the throw arms them, with a 3 s fuse, 100 damage within 6 m.
- Mines stick where they land, arm after 1.5 s, trip on opponents inside 6 x 3 x 6 m, do 100 damage within 5 m, and expire after 90 s.
- After a throw, the pistol comes back to your hand.
- The KillFloor under each map puts you out when your body is below its top.

**Networking.**
- The map and mode are the instance ints; votes are GameRequests 16 and 17.
- The flags use custom ints 0-11, as before.
- `EvRack` 65 is now (map, spot). Every client builds the same map, so the spot indices agree.
- `EvBoom` 66 carries a blast (tool, room-local position in dm) to everyone.

**Files**

| file | role |
| --- | --- |
| Core/RrSceneKitTerrain.cs, RrSceneKitGrid.cs, RrSceneKitTerrainChunk.cs | kit addition: H grids parse and build (the kit's API is unchanged) |
| RrPaintballRoom.cs | rules, flow, per-match map build, visibility, votes, VO |
| RrPaintballLevel.cs | one level: parse, kit, terrain, stand-ins, map data |
| RrPaintballMaps.cs | the 7 maps: names, anchors, music, radios, scene text |
| RrPaintballMap.cs | map data from the level's markers |
| RrPaintballDress.cs | stand-ins, structures from colliders, trees, rocks, surfaces |
| RrPaintballLobby.cs, RrPaintballGallery.cs | lobby controls, shooting gallery |
| RrPaintballRack.cs | level-pose pickups, and press volumes on the test wall |
| RrPaintballThrowable.cs, RrPaintballBoom.cs | grenades, paint mines, splash |
| RrPaintballFlags.cs | CTF (now on the level's flag bases and flag home poses) |
| Scenes/RrScene_Paintball_*.cs | generated placement data (8 files) |
| RrPaintballTest.cs | harness hooks: ShowRiver…ShowMerryMidway, ShowLobby, Gallery, PressStartAlone, Grenade, StartCtfRiver, Fire, TakeFlag, Capture, Rack, Out, Hits, Report |

RrPaintballRiver.cs and RrPaintballDriveIn.cs were removed (the maps are now Rec Room's own).

**Log lines to look for.**
- `Paintball: built the lobby from Rec Room's level in N s`
- `Paintball: <Map> level built in N s: kit … terrain … stand-ins (… structures drawn from … of their colliders) … spawns b/r, flags …`
- `Paintball: <Map> ready: N pickups, N boards, kill floor y …`
- `Paintball: START pressed with 1 player(s): 2 needed`
- `Paintball: spawned in the Blue base on River at …`
- `Paintball: grenade burst at …: n player(s) hit`
- `Paintball: shooting gallery: n hits (best m)`

**Verified.**
- Compile: `build.sh project --editor` gives 0 errors and 0 warnings, with all 113 members proven.
- Harness `paintball_all`: 5 runs, the last passing 50 of 50 steps. It covers the lobby shots, every map chosen and shown, and a River CTF round (spawn, fire, take, capture, pickup, grenade burst, out and respawn).

**Not verified / open.**
- Not tested with two clients or in VR.
- Mines only trip on remote players.
- Other players see a grenade's burst but not its flight.
- The Splat Hammer's 7 m burst is not built; the shield is a plain grabbable.
- Not built: the Practice Arena 1 v 1 duel, the skins shop, mods, beacons, the post-match page.
- Merry Midway (41.5 % install match) is still a dense block town, with two tall slabs at its sides. Spillway's dam is grey concrete boxes.
- Tyre stacks sometimes draw twice.
- `DevNotes/tools/apkscene/Emit.cs`: with `APKSCENE_DEBUG=1`, it now also writes the stand-in's material name (2 lines). Its normal output is unchanged.

**In-world checklist.**
1. Arrive in the lobby's locker building. Walk out to the plaza: picnic tables, radio, the arena gate with the board beside it.
2. Take a weapon from the test wall. Press the gallery START and shoot the targets.
3. With 2 players: pick a map (it loads in a second or two), pick a team, press START on the pedestal.
4. Play each map. Check the spawn rooms, the flags on their stands, the pickups, and falling off.
5. On Drive-In, throw a paint mine. On a map with grenades, check that a grenade hurts a teammate but a pistol does not.

## Iteration 8: Rec Room's lighting and skies, the start hologram, Golden Trophy with its Armory, The Rise of Jumbotron (2026-09-25)

Notes in `DevNotes/iter8/*.md`. These sections supersede older ones where they differ.

### Lighting, shadows and sky (iteration 8)

Every room uses Rec Room's own lighting. `Core/RrEnvironment` applies it when you arrive, and puts the world scene back exactly as it was when you leave or the world closes.

**Where the numbers come from.** None of Rec Room's 61 built-in Android levels has a lightmap, and every light in them is realtime. So the look can be rebuilt live. Each room gets:
- its key light as the sun, with soft shadows at Rec Room's strength (quests have no sun);
- its trilight ambient and its linear fog;
- its reflection strength;
- its sky, drawn with the install's own sky shader (`AG/Skybox/Simple Gradient`). The colours are sampled at the right elevations from Rec Room's final cubemaps (decoded offline) or from the level's procedural-sky settings.

The values live in `Core/RrEnvironmentTable.cs`, one line per scene (69 scenes). It is generated by `DevNotes/tools/apkscene`: first `env` (writes `refdata/apk/env/*.env`), then `envcs`. Only numbers are stored; nothing from Rec Room ships.

**Which lighting applies.** A room starts with the lighting for its activity (`RrEnvironment.KeyFor`). Every scene the kit builds registers itself, and while the player's head is inside a visible scene, that scene's lighting applies instead. That covers Paintball maps, Golden Trophy and Jumbotron areas, and the Armory. `RrEnvironment.SetArea(name)` pins an area by hand.

**Special cases.**
- The cached Rec Center: its own KeyLight becomes the sun, and its own cubemap sky is used when the client has `Skybox/Cubemap`.
- The Dorm: the level's own 8 lights are placed in the room.

**Scene kit.**
- Shadows are on by default.
- Each scene keeps as many point and spot lights as Rec Room ran (up to 48). The ones Rec Room shadowed get soft shadows.
- Quest areas lit only by their own lights keep renderers separate, because the client lights each object with at most 4 lights.
- Duplicate and near-duplicate records are built once. Stand-ins that repeat install art are left out, and stand-in faces are moved in 3-6 mm so coplanar faces cannot flicker.

**Fog.** Rec Room's install shaders carry fog variants and fog as Rec Room did. Shaders built into the world bundle stay unfogged until the world scene is saved with linear fog on.

**Test.** Run the `env_tour` scenario. In its before/after pairs, `RrEnvironmentTest.TestOff` gives iteration 7's look and `TestOn` gives Rec Room's.

### Game consoles (Rec Room's scoreboard hologram with START)
Every game lobby has Rec Room's own **(ScoreboardProjector)**, loaded from your install. When you come within 20 m it opens with Rec Room's own animation and pop-up sound, and shows a large hologram (about 4.4 x 3.3 m) in the style of Rec Room's scoreboard view. The hologram lists:
- the game's title and a timer;
- the mode (a toggle, for games that have modes);
- the player count;
- everyone in this room instance, with your own row highlighted and marked YOU, in team-coloured rows once teams are assigned;
- a status line such as "1 MORE PLAYER NEEDED";
- a map row with CHANGE MAP (for games that have maps);
- a big **START** button.

To press START: in VR, poke it with a fingertip or point your right hand at it and pull the trigger; on desktop, aim at it and click. The console only asks the game to start. The room's host starts the countdown, so everyone sees the same thing.

- **Dodgeball:** the hologram is in the south-east corner of the gym. Alone, START becomes PRACTICE (and STOP PRACTICE).
- **Paddleball:** the hologram is at the south end of the spectator aisle. Alone, START becomes PRACTICE.
- **3D Charades:** the mode toggle chooses Co-op Classic or Team Lightning before START.
- **Paintball, The Golden Trophy and The Rise of Jumbotron:** each shows the hologram in its lobby too.

### Iteration 8: Quest for the Golden Trophy from Rec Room's own Android rooms, with its dressing room (2026-09-25)

This section replaces the iteration-4 Golden Trophy section above. Notes are in `DevNotes/iter8/goldentrophy.md`.

**What changed.**
- The whole run is now Rec Room's own levels from the Android build (2026-03-23, levels 19-29, build order = play order):
  1. Quest_Foyer, the lobby theatre.
  2. Quest_Armory, the dressing room.
  3. Hallway1, the acts 1-6 rooms (Hallway2, Hallway3 with the boulder, DeadIsland with the lava, Hallway4, Library), then Hallway5 (7, the maze), Battlefield (8, half lava) and Cafeteria (9, the Goblin King).
- The Studio ports (GTact1-6, GoldenLobby) are the same levels in the same frame: their player spawns match exactly. They add only their enemy spawn points and level effects.
- The research-built acts 7-9 (RrGoldenTrophyLevel) and the hand shell (RrGoldenTrophyShell) are removed.

**Flow.**
1. The team gathers at the start hologram: Rec Room's [ScoreboardProjector] (1), made with RrGameConsole. It shows the party (1-4) and START QUEST.
2. START takes everyone into the dressing room. This is act index 0, marked `RrQuestAct.Prelude`: no title card, no music, no fanfare. It holds Rec Room's 4 swords, 2 shields and 2 bows on the back-wall hooks, the mirror table, 11 pots, and the door with its platform.
3. Each room's door opens when the room is clear. A lit pane fills Rec Room's own ActivityDoor_Frame and the [DoorTrigger] platform glows. Stepping onto the platform takes the team on.
4. The Goblin King spawns at the Cafeteria's "Boss Spawn Point (1)". His death drops the trophy and opens the chests.

**Walls and floors: DevNotes/tools/gtscene.**
- Each room's shell is editor-merged static batches ("<Room>StaticBatch_<mat>") plus ProBuilder mesh colliders, and the install has neither.
- gtscene measures them into exact planar rectangles:
  - "G plate": one-sided quads per level material, with " c=" where the vertex colour isn't white. The platemat records hold each material's colour, texture and scale.
  - The hull's collider faces become "C box" plates, 0.2 m thick behind each face.
  - The other missing meshes become "G dress" markers.
- The runtime builder is `RrGoldenTrophyHull`:
  - Surfaces are install materials of the same kind, with the level's texture when the install has it, the level's colour, and the level's own tiling (ProBuilder metres: one repeat is 1 / scale).
  - Quads are merged per material and 12 m cell.
  - Stand-ins are rebuilt by kind, and the kit's FixOverlaps drops duplicates.
- The tool also has `render` (an offline PNG view) and `probe`/`profile` (floor heights from the collider data).
- Pipeline: `bash DevNotes/tools/gtscene/build.sh`, then `bash DevNotes/tools/gtscene/gtprep.sh`. This writes `DevNotes/refdata/apk/goldentrophy/GoldenTrophy_<Room>.rrsk` and `Scenes/RrScene_GoldenTrophy_<Room>.cs`. The generated C# is 730 KB, RRSZ1, with 1 cm positions.

**Lighting.**
- Nothing of ours lights the rooms: the house, trophy, torch, vestibule and lava lights are gone.
- Each area is a kit scene named after its level (GoldenTrophy_*). RrEnvironment lights it with Rec Room's values while the player is inside.
- The kit keeps the level's own lights.

**Files (Games/Quests/GoldenTrophy).**
| File | Role |
| --- | --- |
| RrGoldenTrophyRoom.cs | The definition, BuildLevel, ActSlot (4 x 3 grid, 200 x 220 m), the lobby hologram, the act holograms, gather platforms, victory, harness hooks |
| RrGoldenTrophyRefActs.cs | Lobby, Armory, Act1..Act9: waves from the final walkthrough; acts 7-9 groups sampled on the floors |
| RrGoldenTrophyArea.cs | One room: parse, hull, kit, dress. Also markers, FloorAt / FloorPoints (raycasts), pots and weapons at level poses, the exit door and zone |
| RrGoldenTrophyHull.cs, RrGoldenTrophyHullRoot.cs | Plates, surfaces, tints, dressing; owner of the meshes and materials |
| Scenes/RrScene_GoldenTrophy_*.cs | Generated placement data (11 files) |

**Shared (additive).**
- `RrQuestAct.Prelude` and `ArrivalToast`.
- `RrQuestRack.AtPoses`: pegs at level poses.
- `RrQuestDoor.Zone`: an optional "went through" zone.

**Log lines to look for.**
- `Quest: GoldenTrophy_<Room>: built at area-local offset ...: N hull rectangles, N stand-ins (N torches), N markers, N lights, N box colliders`
- `Quest: GoldenTrophy hull: 34865 rectangles in 1441 merged meshes ...`
- `Quest: lobby hologram at Rec Room's [ScoreboardProjector] ...`
- `Quest: shown area GoldenTrophy_<Room>`
- `Quest: GoldenTrophy_Armory: 8 weapons at Rec Room's poses ...`

**Verified.**
- Compile: 0 errors and 0 warnings, all 118 members proven.
- goldentrophy_run passed 50 of 50 steps in each of three runs. Its shots cover the lobby, the hologram, the mod board, the dressing room, every act, the boss and the victory.
- goldentrophy_acts passed 52 of 52 steps: START, through every door to the King, VICTORY, the lobby, then the fail path.

**Open.**
- Every room looks dark blue-grey and floors look flat grey (probe reflections and the post lift, env's area).
- Rec Room required the whole team on the platform; ours takes the team on when any player steps on it.
- The store teleport pads in the Foyer do nothing, and Roomie is not built.
- Not tested with two clients, in VR, or walking with an avatar.

### Iteration 8: the Paintball lobby made to look like Rec Room's (2026-09-25)

**What changed** (`Games/Paintball`):
- **Game-start hologram.** Rec Room's own projector hologram (`RrGameConsole` on the install's `(ScoreboardProjector)`) stands on the lobby's second leaderboard projector, scene (-46.25, -5.12, 71.15). That projector is inactive in the level. It faces the locker building's plaza door. The hologram shows:
  - PAINTBALL, and the mode (tap to switch Capture the Flag / Team Battle);
  - the players with their teams, and "1 MORE PLAYER NEEDED" below 2 players;
  - MAP with CHANGE MAP, and START.

  The level has no GameScoreboardProjectors marker. Its active leaderboard projector keeps our LAST MATCH scoreboard. The match board by the arena gate still picks map, mode and team. The pedestal button at the gate is Rec Room's "Challenge" button (the level's own text); it also starts the match, because the 1 v 1 duel is not built.
- **Signs** (`RrPaintballSigns`):
  - The four lit door signs carry the level's own texts, read from its TextMeshPro components: "Paintball" over the exit door, "Customize your look!" (twice) and "Try 'em out!".
  - The orange boards read "PRACTICE ARENA" (the arena arch) and "PAINTBALL" (the entrance board), as in the 2025 footage.
  - "CHOOSE YOUR SKINS", "SPRAYS", "TARGET PRACTICE" and "LEADERBOARD" are guessed wording: the sign texture is not in the install.
  - The small sign pieces are coloured stickers on the lockers.
- **Lounge and gallery:** a red rug with an orange border and a cream roundel on the level's rug; the armchair purple (the level asks for Couch_Plush_Purple_Mat); the gallery's male target is the install's male practice cutout, since Bonky is not in the install.
- **Building** (`RrPaintballDress`): each face is chosen by two physics rays against the level's colliders:
  - outside walls: Wood_Horizontal_RedOrange_Mat;
  - inside walls: Paint_Warm_Grey_Mat;
  - ceilings: Paint_White_Mat;
  - covered floors: Paint_DarkGrey_Mat;
  - roof: Metal_Galvanized_Mat.

  The locker room has its roof back. Each level collider now belongs to the smallest structure holding it; before, the roof collider fell into a light-shaft volume and was drawn see-through. Light shafts and fog volumes are never drawn.
- **Props, on all 8 levels:**
  - Trees: the install has no 3D trees (only the cardboard `(Sandbox_FlatTree*)` standees and a dead pine), so they stay our own, reshaped. Crowns are 5-8 faceted clumps in two shades, aspens have white trunks, pines have 4 tiers.
  - Cliffs are Rec Room's own RecRoyale rocks (Rocks_Frontier_Mat), fitted to each cliff's size.
  - Tyre stacks are Rec Room's own tyre (Tire_LOD0 with Paintball_TractorTire_Mat), 3 per stack.
  - Containers have ribs and rails; lockers are red or teal, with vents and handles.
  - Every stand-in casts shadows.
- **Lighting budget:** the lobby keeps 16 level lights (it was 8).

**Log lines to look for:**
- `Paintball: install mesh Tire_LOD0 / RecRoyale_Rock_*: N vertices, readable True`
- `Paintball: Lobby level built in N s: ... N install tyres ... building faces N inside / N outside`
- `Paintball: game-start hologram at the level's second projector (15.7, 0.1, -4.8)`
- `Paintball: lobby signs: 20 labels on 6 boards and 4 door signs ... 1 couch material(s) purple`
- `Paintball: gallery: the install's male practice cutout stands at the Bonky target`

**Harness:** `paintball_all.json` has lobby shots from player height, one per footage spot: locker room, lounge, skins shop, test wall, exit door, hologram, entrance, plaza, gate, board, gallery and arena. It also has one shot per map and the new hook `RrPaintballTest.Hologram`. The last run passed 56 of 56 steps.

**In-world checklist:**
1. Arrive in the locker room: the teal and red lockers, the "Try 'em out!" wall and the skins shop under CHOOSE YOUR SKINS.
2. Walk out to the courtyard: the PAINTBALL board over the exit door and the red-orange building.
3. Find the hologram by the plaza door. Check that it lists you, then switch the mode and change the map on it.
4. With 2 players, press START on the hologram.
5. Look at the PRACTICE ARENA gate, the tyre stacks, the ribbed containers and the trees.

**Known:**
- The locker-room interior is dark under the level's realtime lights. Rec Room baked this light, and the lightmaps are not in the install; a request for the env agent is in `DevNotes/iter8/paintball.md`.
- Merry Midway's tall slabs and Spillway's dam boxes are unchanged.

### The Rise of Jumbotron (iteration 8)

The sci-fi quest, played on Rec Room's own Android levels 38-48 (Quest_Scifi_*). Every mesh and material comes from the player's install; only placement data (names and numbers) is in the world.

**Flow**
- **Foyer (lobby):** a theatre with Rec Room's start hologram, which shows the party list and **START QUEST**. There are practice laser weapons on the tables and doors back to the Rec Center.
- **Armory (pre-start):** the dressing room, with 4 laser pistols and 4 burst rifles on the wall. They refill 3 s after being taken. Step onto the pad by the door to go on.
- **Acts:**
  1. SS Coach
  2. Cargo Bay
  3. Engineering
  4. Bridge
  5. Flight Deck: the green button starts the attack; don't fall off.
  6. Space
  7. Hangar
  8. Supply: Kevin the goblin's side room.
  9. Jumbodome: Jumbotron.
- **Jumbotron:** a hanging 4-screen jumbotron with a turret underneath.
  - One screen at a time is lit and takes damage.
  - Each destroyed screen calls "Reinforcements!".
  - The turret fires streams of lasers that aim ahead of you.
  - The last screen ends the fight and every bot with it.

**Rules (Rec Room's final ones)**
- 1-4 players. One hit downs you. A high-five revives.
- Credits: 1,200 lost per down. Rating S / A / B at 150k / 125k / 100k.
- No checkpoints: a team wipe goes back to the Foyer.
- Kill credits are estimates (Rec Room never published them).

**Data pipeline (reference only; nothing shipped)**
- `bash DevNotes/iter8/jumbotron_tools/make_tools.sh <tools>` builds scratch copies of apkscene (a broader `find`) and gtscene.
- `bash DevNotes/iter8/jumbotron_convert.sh <apk-work> <tools>` runs three steps:
  1. gtscene prep turns the hull into exact plates with vertex tints, plus collider plates.
  2. `jumbotron_markers.pl` adds the EncountersV2 spawn points, regions and trigger volumes, the doors and door pads, and the button.
  3. rrsk2cs writes `Games/Quests/Jumbotron/Scenes`.
- The results are in `DevNotes/refdata/apk/Jumbotron_*.rrsk`. Notes: `DevNotes/iter8/jumbotron.md`. Harness: `scenarios/jumbotron_run.json`.

**Known gaps**
- Credits are paid on the kill instead of being picked up.
- No muffled sound or low gravity in Space.
- No projector film on the Flight Deck.
- Jumbotron's face is ours: none of his art is in the install.
- Most level textures are Android-only, so walls and floors are flat Rec Room colours with their tints.

## Iteration 9: Rec Room's own light grid (2026-09-25)

Notes in `DevNotes/iter9/light9.md`; the shader research tool is `DevNotes/tools/shdis` (reads the install's shaders for research only).

### Iteration 9: Rec Room's own lighting, working in the client (2026-09-25)

Notes in `DevNotes/iter9/light9.md`; before/after pictures in `DevNotes/iter9/screens`.

### Why the torches lit nothing
The torches have always had lights. Each Golden Trophy wall torch has:
- two realtime orange point lights ("Torch Hot Spot" 1 / 1.41 m and "Torch Light" 0.85 / 5.8 m);
- two paper-flame cards;
- a square-particle flame.

The hanging lanterns carry a light that shines downward. Nothing in Rec Room is baked: its shaders have no lightmap variant at all.

The problem was how Rec Room's own version of URP hands lights to its shaders. The installed shaders were disassembled offline with `DevNotes/tools/shdis`. They don't use URP's lights-per-object lists. They read their lights from a world-space grid:
- 24 × 12 × 24 cells, each holding up to 8 lights;
- Rec Room's own falloff, 1 − (d/r)², with no inverse-square term;
- a per-light specular switch.

Somnium's standard URP never fills that grid. So every Rec Room surface (install art, our maker-pen walls, the cached Rec Center) was lit by the sun and ambient only.

### What the world does now
- **`Core/RrLightGrid`: the grid, per scene.** It fills the grid and Rec Room's light buffer in Rec Room's layout and binds them to the renderers that use Rec Room's shaders. Each of those renderers gets one property block holding one bound buffer.
  - The scene kit feeds it every point and spot light of a level (up to Rec Room's 160).
  - The Dorm light rig and the cached Rec Center have grids too.
  - Avatars and URP-shaded objects keep Unity lights set up to match. Hemisphere lights become wide spots for them, with a falloff correction factor, and these copies cast no shadows.
- **Rec Room's URPLight values** now come from the levels: specular contribution (0 on most lights: no highlights) and hemisphere lights. apkscene writes them, and each scene's line in `Core/RrEnvironmentTable` carries them as `urp=`.
- **Merging:** with the grid, merged meshes keep their torches, so quest areas merge normally again. Golden Trophy kit renderers: about 600 → 182.
- **Ambient occlusion:** the post volume exists from world start, so Somnium's own ambient occlusion (HBAO) lands in it. In Rec Room rooms it is shaped like Rec Room's contact shadows (radius 0.6 m, intensity at least 1). It only draws when ambient occlusion is on in Somnium's graphics settings.
- **Golden Trophy lanterns and torch cups** glow orange.

### Iteration 9 checklist
1. **Golden Trophy lobby:** stand at the spawn. The torches on both sides light their pillars and the floor around them. The stage is lit by its spotlights, and the lanterns glow orange.
2. **Acts 1–9 and the Armory:** wall torches light the walls and the floor; the encounter spotlights make pools.
3. **Dorm, Rec Center, Paintball lobby:** brighter and warmer, Rec Room's own lights. Tell me if anything looks too bright; Settings > Look still has Brightness.
4. **Frame rate, especially in VR:** lit surfaces cost more to draw now (about +2 to +3 ms per view in the editor). Tell me the fps in the Golden Trophy lobby, the Rec Center and the Dorm.
5. **Ambient occlusion:** turn it on in Somnium's graphics settings, then look for soft shadows in corners and under furniture. The log line starts with `PostFx: AO:`.
6. **Send Player.log.** The useful lines start with `Lights:` (one per scene: lights, cells, renderers lit).

## Iteration 13: wave 1 games, the Maker Pen, Jumbotron floaters (2026-09-26)

Six games added in basic form: reachable from the watch and the Rec Center doors, Rec Room's real layout, the core loop
from the start hologram to the result and back to the lobby, networked through the existing frameworks, the install's
music and sounds. Builder notes: `DevNotes/iter13/*.md`; tour result: `DevNotes/iter13/wave1_result.md`.

**Level data out of the assembly.** From this iteration, new levels are not generated C# strings (the user-string heap
held 10.7 MB of its 16 MB). Each RRSK is a gzip file `Data/<Name>.bytes` (`DevNotes/tools/rrscene/rrsk2bytes.sh`),
loaded with `RrSceneBank.Text("<Name>")`. `DevNotes/tools/rrscene/bankscene.sh` lists every `Data/*.bytes` in RrMain's
scene data so the upload carries them: 35 files, 12.8 MB. Run it again after adding a data file. All six games build
their levels through Jumbotron's public `RrJumbotronArea`, so their build lines log under `Jumbotron`.
`Core/RrEnvironmentTable.cs` has the 22 new Crimson_* and Crescendo_* lighting rows (apkscene envcs).

### Laser Tag (Games/LaserTag)
Hangar (level76) and Cyber Junk City (level77) from `Data/LaserTag_*.bytes` (gtscene jt=1, rrsk2bytes --quant 2). Each
level's own lobby holds the console at its ScoreboardProjector: TEAM BATTLE, the map choice and START. Final rules are
first to 400 points or the most after 480 s. A tag is +10 and a self-destruct -10. There is no friendly fire, but
grenades hurt their thrower. Players get 3 s of spawn invincibility and start with the laser pistol. The level's weapon
pickups come back after 15 s, and each map has a power weapon (Laser Orb Launcher / Beam Cannon, its spot a guess). A
map switch builds the new map in its own slot (Hangar at 0, Cyber Junk City at (300, 0, 0)), then frees the old one.
It uses events 64 (pickup) and 65 (blast) and GameRequest 16 (map vote). No bots yet.

### Soccer (Games/Soccer)
Level72 from `Data/Soccer.bytes`. Players arrive in the pre-game hall. The console stands at the ScoreboardProjector and
the Rec Center portal at the exit door. START works alone as practice. After a 5 s countdown each team is on its own
spawns. The ball is Rec Room's SoccerBall, shared as net prop 0, and drops from (0, 5.18, 0). A match is 180 s and the
most goals wins. Goals use the level's trigger zones, claimed by the client simulating the ball. Kicks come from the body
(8-20 m/s), a teleport into the ball, VR hands or a desktop click. The speed-boost orbs are not built.

### Bowling (Games/Bowling)
Level91 from `Data/Bowling.bytes`, with its pin colliders cut (`DevNotes/iter13/sports_tools/bowl_clean.awk`). The
middle lane scores and the side lanes are local practice lanes. There are 10 pins per lane on the level's markers, and
the scoring lane's balls are net props 0-3. A roll starts at the foul line. The roller's client simulates the pins and
reports the standing ones. The authority scores standard ten-pin (`RrBowlingScore`) and runs the pinsetter. Up to 6
bowlers take turns and the highest total wins. The sheet shows on the overhead monitors.

### The Curse of the Crimson Cauldron (Games/Quests/CrimsonCauldron)
Levels 50-60 from `Data/Crimson_*.bytes` (`DevNotes/iter13/crimson_convert.sh`). The quest goes Foyer console, then the
Armory, then 9 acts: Castle Courtyard, Forest Path, Campsite, Deep Forest, Bog, Mines, Deep Mines, Bell Tower Stairs,
Finale. Then come the Crimson Witch, the reward chests and the victory screen, and the team goes back to the lobby. The
waves use each level's own encounter spawns and triggers, with the install's goblins. Pots, exit doors, ladders, steam
traps and the bog water work. The bog monster and the witch are simplified stand-ins, and the witch takes normal hits.

### Isle of Lost Skulls (Games/Quests/LostSkulls)
Levels 62-70 from `Data/Isle_*.bytes`, thinned by `DevNotes/iter13/isle_tools/isle_thin.pl` to fit. The Foyer lobby
leads to 8 acts: Galley (the cell door and padlock), Pirate Banquet, High Seas (drawbridge), Port Plunder, Gibbet Jungle,
Caves, Sunken Ship, and Ghost Beard's Gauntlet (three cannon hits). There are pirate enemies from the install, cannons
with fuse buttons and rope ladders. Mid-level gates are open. A team wipe ends the run, because the framework has no
same-act checkpoints yet.

### Crescendo of the Blood Moon (Games/Quests/Crescendo)
Levels 79-89 from `Data/Crescendo_*.bytes` (`DevNotes/iter13/crescendo_tools/cbm_convert.sh`). The 13 acts come in the
story's order, and the Grand Hall is built once for its 4 visits. Each visit banks silver and is a checkpoint. The
gates' iron bars rise once the waves before them are beaten. The rank is 4,400 minus 200 per down. Dracula is simplified:
fireball lines, 3 self-aiming light mirrors, then the bat phase. The carriage, the whip, secret shelves and most traps
are not built.

### The Maker Pen (Interact/MakerPen)
The watch Home page has a 5th tile, "Maker Pen" ("Put Away" while it is out), so the pen works in every place. It is
the install's own (MakerPen) tool. Its ink is tubes in Rec Room's maker-pen material, with the install's draw and delete
sounds.
- **VR:** the trigger draws, 3-20 mm by pressure, and A/X undoes. A palette floats above the other hand: touch it with
  the pen tip for 16 colours, Erase, Undo or Clear.
- **Desktop:** left mouse draws 0.7 m out, Z undoes, right mouse erases your stroke, 1/2 change colour, 3 toggles the
  eraser, 4 clears.
- **Networking:** events 20-23. Only the place's own players see the ink, and each stroke belongs to its author. Late
  joiners get the ink re-sent. The limits are 150 strokes and 6000 points per player. Ink is cleared when you leave or
  the place resets. The Dorm is not networked, so ink there stays local.

### The Rise of Jumbotron: no floating props
A new `gtscene float` step (`DevNotes/tools/gtscene/Gt.cs`, run by `DevNotes/iter8/jumbotron_convert.sh`) checks that
every drawn group touches the room: 490 groups are fixed. Cargo1's boxes and Battlefield1's glow sticks are rebuilt from
the level's own mesh (they were drawn as poles). The Foyer's and Concession's glow sticks hang on strings. Mid-air glow
sticks, the Stadium's cable loops and switched-off shelves are left out, and ~290 panels, signs and lamps are moved onto
their supports. The Departure's platforms over the portal pit float in Rec Room too, so they stay.

**Integration fixes (tour 2026-09-26, `DevNotes/iter13/wave1_result.md`).**
- `Core/RrSceneKit`: a mesh collider on a non-readable install mesh (Crimson's Quest_Rock01/04) is now a box from the mesh's bounds, instead of a Unity error and no collision.
- The Isle's cannons turn 55 degrees, so Ghost Beard's third position can be hit.
- Crescendo's racks only take the install's "(Name)" prefabs.

## Iteration 14: player-made rooms - create, save, share (Places/MakerRooms, core) (2026-10-14)

Rec Room's custom rooms (research: `DevNotes/research/rr_maker_rooms.md`; build notes: `DevNotes/iter14/makerrooms_core.md`).
The Maker Pen's build tools talk to the room only through `Places/MakerRooms/RrMakerContract.cs` (`RrMakerEdits.Host`).

- **Create** (watch Home > Create): pick a base room, CREATE ROOM. The room is called "&lt;Name&gt;'s &lt;Base&gt;", belongs to
  you (your Somnium user id), is saved at once and you are taken there with the Maker Pen. Base rooms: **Maker Room**
  (Rec Room's blank 15 x 15 m box from its six install pieces - walls, floor and ceiling are objects too), **Dorm Room**
  (the Maker Pen dorm kit, every piece editable), **The Ink Space** (the Charades venue spawnables), and Rec Room's own
  levels as fixed bases: **Park** (level75, with its terrain), **Performance Hall** (level73), **Bowling Alley** (level91),
  **Hangar** (level76).
- **Rooms** (watch Home > Rooms): MY ROOMS and PUBLIC ROOMS, with owner, base, version, last save and who is in it now;
  JOIN / GO, DELETE (yours, tap twice). The "Maker Rooms" card on the Play page opens it too.
- **In a room** (This Room > Room Settings): SAVE, rename (on-watch keyboard; on a screen you can type), Private / Public,
  who can build (owners or everyone), the Maker Pen, and co-owners (the owner picks them from the players in the room).
  Changes save automatically every 2 minutes and when you leave (owner and co-owners only).
- **Where rooms live**: on each player's PC, in the client's `Application.persistentDataPath\SomniumRecRoom\MakerRooms`
  (next to the world's catalogue cache; the full path is in Player.log, `MakerRooms: started`): `<id>.json` (latest),
  `history/<id>/v<n>.rrz` (last 10 versions), `catalog.json`
  (every room you know of). No server: players announce the rooms they hold, a room is copied from any player who holds
  it, and everyone who visits a room gets a copy - so a public room can be opened while its owner is offline, as long as
  someone who has it is in the world (or you have it yourself).
- Networking: events 24-29 (catalogue, request, chunk, edit, state, meta), each room in its own instance of activity
  MakerRoom (the instance's custom ints 0-3 hold the room id, presence int 19 the room's hash).

**Iteration 14 checklist (core; ideally two players):**
1. Home > Create > Maker Room > CREATE ROOM: you arrive in the grey box with the pen. Try each base room once (the Park
   takes longest to build).
2. Build something with the pen tools, then This Room > Room Settings > SAVE (the line under it shows the version).
3. Player B: Home > Rooms > PUBLIC ROOMS lists your room (within a minute of joining the world) with "1 here now"; JOIN.
   B sees what you built and your edits as you make them; B cannot build until you make B a co-owner or set "Everyone".
4. Leave, come back with Rooms > GO: everything is still there. Restart the world: still there.
5. With you offline (or in another place), B opens your public room from PUBLIC ROOMS (B got a copy when visiting).
6. Set the room Private: B no longer sees it listed; co-owners still do.
7. Send Player.log: the useful lines start with `[SomniumRecRoom] MakerRooms:`.
8. **Maker Pen build tools** (Interact/MakerPen, in a maker room you may build in): the palette above the other hand has
   8 modes - Draw, Shapes, Props, Select, Clone, Recolor, Delete, Physics - then Snap, Undo, Redo, -, + and Save. Shapes:
   tap = 25 cm shape at the tip, hold and pull = stretch it. Props: 111 install props in 8 tabs. Select: touch or point the
   laser, hold the trigger to carry it; -/+ resize. A/X = undo. Desktop: 1-8 modes, left mouse acts (hold = drag), Z/Y
   undo/redo, C colour, T prop tab, G snap, wheel = next item (resize while dragging), [ ] turn, , . tilt, F5 save.

**Iteration 15 checklist (ideally two players):**
1. **Avatar:** Watch > Avatar > WEAR REC ROOM AVATAR. Pick skin, eyes, mouth, hair, hat, top...; the preview stands to your
   right. Check the face (eyes/mouth placement), both hands (the left could render inside-out) and finger curl on grip.
   A second player sees your outfit and not your Somnium avatar. WEAR SOMNIUM AVATAR brings it back exactly. Leave and come
   back: your choice is restored.
2. **Disc Golf** (SPORTS & PVP): Lake and Propulsion via CHANGE on the console; throw (desktop: hold to charge, release),
   basket, scorecard, 9 holes, results.
3. **Stunt Runner** (FEATURED ROOMS): START, countdown, run course 1 to the finish button, checkpoints, KO Cola, times.
4. **Rec Rally** (FEATURED ROOMS): START (practice below 4 players), drive (W/S, A/D, Shift drift, Space boost, T reset;
   VR triggers + left stick), 3 laps, positions.
5. **Maker rooms** (iteration 14 checklist) again, with two players arriving at the same time, and the Park.
6. **Rec Center doors:** MAKERWORLD = public maker rooms, CAMPUS JAMS = your rooms; COMING SOON cards are greyed out.
7. **Watch settings:** change Brightness/Warmth/volumes, restart the world: they stay.
8. **Isle of Lost Skulls:** let the whole team go down: the act restarts instead of the run ending.
9. Send Player.log: areas `Avatar`, `DiscGolf`, `StuntRunner`, `RecRally`, `MakerRooms`, `Quest`.

## Networking migrated to Photon Fusion 2.1.3 and Community Modules 4.0 (2026-10-20, iteration 16)

Somnium now runs Photon Fusion 2.1.3; worlds built on 2.0.12 no longer sync. Migrated the same way as Annihilator,
following the Somnium migration tool's guide (`Fusion_2.1_Migration_Guide.md`), by hand with Unity closed:

- **Photon:** `Assets/Photon/Fusion` and `PhotonLibs` (2.0.12) replaced by the tool's "Somnium Photon Fusion 2.1.3 +
  Voice 2.64" package (Fusion, PhotonLibs, Photon Realtime 5, Photon Voice). Our `NetworkProjectConfig.fusion` and
  `PhotonAppSettings.asset` were kept (with their .meta files).
- **Scripting defines (Standalone):** `FUSION_2_0`/`FUSION_2_0_12` out; `FUSION_2_1`, `FUSION_2_1_3`, `FUSION_2_1_OR_NEWER`,
  `PHOTON_REALTIME_5_OR_NEWER` in (also in the harness copy).
- **Community Modules 4.0:** the NetworkBridge scripts are byte-identical to ours (same GUIDs, so the scene's
  references hold). `SomniumSpace.Network.asmdef`: `PhotonRealtime` -> `Photon.Realtime`, `Fusion.Addons.Physics` removed.
- **SceneNetworking (ours, `Net/`):** 4.0's changes merged in: flags `V2 | AllowStateAuthorityOverride`, the debug
  runner only in editor play mode without the client, `RequestStateAuthority()` checked, `OnReliableDataReceived` takes
  `ReadOnlySpan<byte>`. Kept our late-join catch-up fix. 4.0's NetworkGrabbable detach is not needed (no NetworkGrabbable).
- **RrNetSceneSetup:** adds `AllowStateAuthorityOverride` instead of assigning fixed flags (2.1 bakes V2 itself).
- **New tool:** "Somnium Rec Room/Fusion 2.1: re-bake network objects" (`Editor/RrFusionRebake.cs`). Run in batch mode:
  Unity compiled with **0 errors**, all **135 NetworkObjects now V2 with AllowStateAuthorityOverride**, prefab table rebuilt.
- Not used here, so nothing to convert: NetworkRigidbody3D, NetworkTransform, NetworkGrabbable.
- Offline compile check now points at this project's Fusion 2.1 libraries: 0 errors, 124 Fusion/Somnium members proven.
- Backup of everything changed: `MigrationBackups/fusion21_20261020_1615/` (delete it once the world syncs in the client).

**Test in the client with two players:** presence (watch People page), the same Rec Center instance, a prop thrown
between players, a game whose host leaves mid-match.

**Iteration 17 checklist (with the testers):**
1. **The tester whose Rec Room wasn't found:** the loading card may say "searching your drives for Rec Room..." for a few
   seconds the first time, then Rec Room loads; next visit it is instant. If it still fails, the card names the file
   (`...\LocalLow\...\SomniumRecRoom\install.txt`) where they can paste the folder that holds RecRoom.exe. Their
   Player.log line `Main: install search: ...` lists every place tried and every copy found.
2. **Rec Center:** every player sees the same final building (players without the cache: the measured-data version - same
   layout, doors, props and mirror; plain colours where Rec Room's own textures are missing). Log line: "RecCenter: Rec
   Room's July 2025 building from the measured data".
3. **Dodgeball and Paddleball:** Rec Room's real rooms; play a round of each (Paddleball's blue side is now at +z, as in
   Rec Room). Only the door nearest the lobby leads back to the Rec Center.
4. Note how long the Rec Center takes to load for a tester without the cache (it builds ~45k pieces).
