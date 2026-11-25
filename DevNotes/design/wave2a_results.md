## dorm
### Experience
1. The player arrives in the dorm. On first login they appear in the middle of the room, turned toward the mirror. When they come back from somewhere else, they appear 1.5 m in front of the REC CENTER door, and the door plays its arrival swing.
2. The room is Rec Room's own 2018 dorm, rebuilt from the 22 kit pieces. It is 9.7 x 5.9 m with pale blue-grey walls, a white stripe and white skirting. The floor uses Rec Room's own Wood_Floor_Matte_mat. There is a loft at the -x end, a skylight, a big slanted window at +x, an arched window above the board, and a pillar in the back-right corner. The kit's gold light shafts are kept, and the scene sun casts the window-frame pattern on the walls because the walls cast shadows on both sides.
3. Outside the windows there is a sunny campus: a gradient sky dome, puffy clouds, lollipop trees, and campus buildings with coloured roofs, including an orange-roofed Rec Center seen through the big window. A quiet bird ambience (AmbianceSummerOutdoor) plays from the window.
4. Back wall: a big mirror with a white frame. Within 6.5 m it shows a live planar reflection. Next to it is a MIRROR panel with 3 round buttons: mirror on/off, put things back, go to Rec Center. The Code of Conduct poster and the campus map hang on the same wall. Under the loft are the Block/Mute and Shake Hands posters, the dresser with 2 root beers, a bell and a book, an armchair and plants.
5. Front wall: the community board is now a Rec Room Play board. It has an orange header and cover-art tiles for every registered activity, with the Rec Center first. Pressing a tile (click, VR ray or poke) travels there. The Rec Center door with its lit sign is on the right.
6. Right side: the bunk bed under the big window with the desk underneath. On the desk are the boombox radio, a desk lamp with its own light, a water bottle, a pizza slice, an apple, a cup and dice. The radio plays a random Rec Room track when you enter. Pressing its top button, or pulling the trigger while holding it, skips to the next track, and it moves on by itself when a track ends. On the pillar face is the basketball hoop, placed at Rec Room's measured rim position, above the office bin; the basketball and a frisbee lie on the floor.
7. Every prop can be picked up and used with Rec Room's behaviours through RrTools.MakeTool: drinking, eating, breaking, bounce and rim sounds, DingDing plus VFX on a basket, and pop-in. A prop goes back home 15 s after it is left displaced with nobody within 3 m, or with the PUT THINGS BACK button.
8. The ladder: press it on desktop (VR trigger ray or poke also work), or squeeze a grip with a hand on the rails in VR. From the floor this teleports the player onto the loft; from the loft, down to the foot of the ladder. The loft has a second armchair, the art canvas with a paintbrush, a trophy and a book.
9. Leaving: walk into the door, or use the board or the mirror-panel button. The dorm is not networked: every player builds their own copy, and nothing in it is drawn for anyone else.
### Verified
Compile: `build.sh project --editor` gives 0 errors and 0 warnings (197 files), "all 113 Fusion/Somnium members are proven", and editor-like runtime and editor tools both 0 errors. This was re-run after the last edit. At first other modules (Paintball, Dodgeball, GoldenTrophy) were mid-edit, so I compiled Dorm plus the foundation modules in an isolated copy (0 errors) and waited for the project to become clean. Metas were written with metas.sh.

Harness: scenario `scratchpad/harness/scenarios/dorm_room.json`, 4 runs.
- Run 1 was wasted: I passed a relative scenario path and the boot failed.
- Runs 2-4 exit 0. All 18 steps pass except the final `expect_log basket: SCORED`.
- Logs from run 4: "Dorm: built at (-1125, -2000): 60 Rec Room objects, 17 props, max kit snap error 10 mm, 3.0 s" (build about 3 s, arrived in 3.9 s). "floor: Rec Room's Wood_Floor_Matte_mat". "hoop rim at room (3.905, 1.095, -1.091) (Rec Room's rim VFX: (3.905, 1.097, -1.090), off by 2 mm)". "play board: 6 activities". Report: 17 props, nothing missing, door built, mirror reflecting, radio playing, board 6 tiles with 6 cover arts, 1 hoop. "at mirror: reflecting". Ladder: feet at room y 2.79 after 1 climb (on the loft). Radio: next track played (Spillway).
- The only errors in the summaries are URP editor "Host type is not matching" messages. The same lines appear in every other scenario's summary, so they are not from the dorm.

PNGs, all looked at, in `harness/out/dorm_room/`:
- dorm_spawn: mirror wall, map, panel, Code of Conduct, social posters, dresser, loft beam.
- dorm_loft: the whole room toward the loft (ladder, dresser, armchairs, shelves, skylight with sky), board and door.
- dorm_window: bunk-bed desk with props, hoop and bin, big window showing sky, clouds and the Rec Center roof, the board with art.
- dorm_front: board, door, arched window, ladder.
- dorm_board: close-up of the Play board, 6 art tiles in a 3 x 2 grid.
- dorm_outside: looking out the big window.
- dorm_mirror: framed mirror with a correct mirrored reflection (the reversed REC CENTER sign).
- dorm_hoop: desk with lamp, radio, bottle and pizza; the hoop's full backboard on the pillar.
- dorm_loft_top: standing on the loft (canvas, trophy, book, brush, armchair).

Fixes made after looking at the PNGs:
- Hid the Maker-Pen holographic selection meshes, which were tinting the big window lavender.
- Added a white mirror frame; Rec Room's own frame did not show.
- Made the board grid adaptive: 6 activities now get big tiles, and 21 still fit (6 x 4).
- Made the trees smaller and moved them further out.
- The wood material is used on the main floor only; on the loft floor its UVs showed green edges.

Basket not verified: the scripted lob went through the hoop (no rim or board contact) and knocked the bin over, but RrBall did not count a basket in any run.
### Not done
- Basket scoring is not confirmed in the dorm. In run 4 the scripted lob crossed the rim plane 0.017 m from the rim centre, touched neither rim nor backboard, and knocked the bin over below. RrBall's trigger scoring (OnTriggerEnter/OnTriggerExit against RrHoop.ScoreTrigger) still reported 0 baskets. The hoop is spawned with RrSpawnOptions.Scenery() at Scale 0.94 and set up with AddComponent<RrHoop>().Setup(inst); triggers are cleaned before Setup. The interact test scored with an unscaled hoop and the ball handed over and released through RrGrab. The cause is not found: I used all 4 harness runs.
- VR is untested: the ladder grip-climb, VR poke/ray on the board, panel and radio button, and how the one-eye mirror reflection looks in stereo. Desktop clicks through the real client are also untested; the harness called the hooks directly.
- The mirror renders one mono reflection from the view camera's centre. It is slightly off per eye in VR, and the Somnium local avatar may be hidden in first person, so the player may not see themselves in it.
- Furniture positions follow the research's Â§3.3 guesses. Only the hoop, the bin under it and the architecture are data-backed. The wall colours are guesses (Rec Room's palettes are not in the bundles).
- Not built: Rec Room's mirror buttons (Customize, Outfits, Store: the avatar is never modified; replaced by our MIRROR panel), Dorm_Help_Buttons, RrSeat sitting in the armchairs (the Interact module has no RrSeat), drawers that open, painting on the art canvas, darts sticking in walls, and the tutorial VO AmyVO_NUX16_ThisIsYourDormRoom.
- Radio volume 0.35 and ambience 0.22 are set by guess; the harness listener is muted, so nothing was checked by ear. Rec Room's own radio AudioSource is at 0.1, behind their mixer.
- Harness runs cannot check the ambience loop or the auto-advance of a finished radio track (a track is about 78 s).
### Requests
- Interact: check why RrBall does not score through a Scenery-spawned (Basketballhoop) at Scale 0.94 when the ball is thrown by setting its velocity (no RrGrab Give/Release). Repro: RrDormTest.ShootBasket in scenarios/dorm_room.json. The ball crosses the rim plane 1.7 cm from the centre with no rim contact, yet Scores stays 0.
- Interact: add RrSeat so the dorm armchairs (Sandbox_CouchChair SeatPortal) can be sat in; the dorm would call it on its two armchairs.
- Rooms/Watch: the dorm registers IconSprite = Illustration_Menu_Dorm, CoverSprite = illustration_HomeButton_DormRoom, and Music = null on purpose. The dorm has no background music; the radio and outdoor ambience are 3D sources the room starts in OnLocalEnter and stops in OnLocalExit.
- Harness README: say that run.sh needs an absolute scenario path; a relative one boots and fails with 'scenario unreadable'.
- Core (RrLook): optionally expose textured Rec Room materials (Wood_Floor_Matte_mat, DormRoom_PlasterWalls_*). The dorm loads Wood_Floor_Matte_mat itself through RrAssets.LoadRoutine<Material>(name, 21) and it works (AG/Standard-Opaque).
## reccenter
### Experience
1. You arrive in the lobby (12 x 8 x 4 m) from the Dorm or the world at (0, -24.2), looking north. There are 6 arrival spots, so people who arrive together do not overlap. Behind you is the Dorm door with its welcome mat and an EXIT sign. To your left: the community board ("REC CENTER NEWS": where each kind of door is, what to do) and blue lockers. To your right: two more lockers, a REC CENTER sign and the purple "DELIGHT ME!" door, which takes you to a random registered activity. Rec Room's Code of Conduct poster hangs on the south wall. Ahead is the orange-trimmed archway with "WELCOME TO THE REC CENTER" above it. Coming back from an activity instead puts you in front of that activity's door, and the door plays its arrive swing.
2. The hall is 40 x 36 x 8 m and uses Rec Room's own surfaces. The floor is long wood planks. The walls have Rec Room's blue wainscot, an orange stripe and off-white paint. Clerestory windows run along the top of three walls, and two tall south windows look out on grass and flat trees. Overhead are an off-white ceiling with 30 fluorescent panels and orange-painted 10 m I-beams. A four-sided REC CENTER sign hangs over the ring kiosk in the middle. You hear a low crowd murmur at the archway, birds by the windows and the kiosk's hum.
3. Doors: orange header panels read PVP and SPORTS (north wall), QUESTS (east), PARTY and RACING (west). Every registered activity gets Rec Room's double activity door with its sign, framed in its group's colour. Walk into it, click it, pull the trigger on it or touch it in VR to travel. A spot whose activity is not registered when the hall is built shows a grey "<NAME> / COMING SOON" placard. All 20 spots plus the Dorm door are kept.
4. Kiosk: press the reception bell and everyone in the instance hears Rec Room's ReceptionBell (0.25 s cooldown). The boombox on the counter starts on "Rec Room Theme". Pressing it steps through Rec Room's 41 radio tracks, then off. A "NOW PLAYING / title" label floats above it for 4 s.
5. South-west basketball court: painted key, free-throw circle and 3-point arc, a wall-mounted hoop, 4 basketballs, 2 dodgeballs and a bucket. Baskets score through the Interact hoop; after 3 in a row the ball gets its fire trail.
6. West: two ping-pong tables, each with 2 paddles and a ball. A paddle you hold hits the ball back: the ball's own velocity is reflected and the paddle's swing is added. The ball keeps Rec Room's table and paddle clicks.
7. North: a poker table with 6 poker chairs, a dice set and three stacks of pickable poker chips (Rec Room's chip mesh in our colours). Next to it is the pool table with 15 balls racked for 8-ball and the cue ball on its spot, and two cues standing in the wall rack on a blue panel.
   - To shoot on desktop, hold a cue, look at a ball, hold "use" to charge (up to 1.2 s) and release: 0.6 to 6 m/s.
   - In VR, aim along the cue with the trigger, or just hit the ball with the cue.
   - Pocketed balls go to the ball-return tray. The cue ball comes back to its spot once the table is at rest.
   - The table's 8-ball and 9-ball buttons re-rack for everyone.
8. East lounge: a striped blue rug, two Rec Center couches facing each other, blue couch chairs, orange, teal and purple beanbags, plants, and the jukebox. It starts on "Lone Shoe Blues", and pressing it steps through its 15 tracks.
9. South-east cafe, under an orange-fascia soffit with 3 pendant lamps. The counter holds coffee pots, 8 mugs, a plate stack and the mocktail mixer; you can pick them all up and drink from what Rec Room made drinkable. There are bar stools, cafe tables and chairs, shelves, mini-fridges, a Stay Hydrated poster, and a second radio playing "Food Court". The vending machine hums, drops and hands you a drinkable water bottle; at most 5 of your bottles exist at a time. The Friend-O-Tron shows who is in this Rec Center right now.
10. Same instance, everyone sees the same things: all 14 toys you play with (balls, paddles, cues) are shared props, the bell rings for everyone, and the radios, jukebox and pool layout are shared. Cafe items, dice and chips are local, as in Rec Room.
### Verified
Compile: `build.sh project --editor` gives 0 errors and 0 warnings on the server-like build (197 files), "Server API guard: all 113 Fusion/Somnium members are proven", editor-like runtime build 0 errors, editor tools build 0 errors. That was the last run, after every edit. While other agents' Paddleball files were half-written, I also checked a copy without their folders: clean. Metas: all 14 .cs files and the folder have .meta files (metas.sh).

Harness: 4 runs of `scratchpad/harness/scenarios/reccenter_tour.json` (goto RecCenter, 16 shots, test calls). All 4 exited 0, and in the final run all 39 steps were ok. The final run showed:
- "RecCenter: built at (-3000, 2000) in 3.6 s: 7 doors, 15 reserved, 128 prefabs (0 failed), 33 local tools, 14 shared props, 3 radios, pool 16 balls (procedural), cloth y 0.728, hoop ready". The 7 doors are 5 registered activities, the Dorm door and DELIGHT ME!.
- "surfaces: 13 Rec Room materials, 0 fallback colour(s), beam paint, exit sign, poster", "shell built: 319 pieces", "poker chips: 24 (Rec Room PokerChip_LOD1 mesh)".
- The hooks: bell rung (event loop-back); radios pressed (Kiosk: Rec Room Theme -> Crescendo Miniboss after 2 presses, Jukebox: Lone Shoe Blues -> Clearcut, Cafe: Food Court -> Goblin Work Song); pool break (shots 1, 1 ball pocketed); ShootHoop "hoop scored (scores 1)"; vending "dispensed 1" ((WaterBottle) made Drinkable); Friend-O-Tron shown; rack 9 through the request path (racks 2); CueShot "struck the cue ball (shots 2)".

PNGs I looked at, in `scratchpad/harness/out/reccenter_tour/`:
- rc_lobby: the archway into the hall, the purple DELIGHT ME! door, the board.
- rc_lobby_board: lockers, the board with its notes, the Code of Conduct poster facing correctly, the Dorm door and welcome mat.
- rc_hall: the whole hall. Long wood planks, off-white ceiling with fluorescent panels and I-beams, clerestory windows, group headers, doors and placards, kiosk, lounge with coloured beanbags, jukebox, vending.
- rc_north_doors and rc_east: PVP/SPORTS headers, Dodgeball/Paddleball/Paintball doors, COMING SOON placards, poker table and chairs, the hanging REC CENTER sign.
- rc_kiosk: ring counter, bell and boombox on top, snake plant.
- rc_pool_rack / rc_pool_break / rc_pool_nine / rc_pool_cue: the 8-ball rack; the cue ball rolling into it; the 9-ball diamond; a cue laid on the table after the cue shot.
- rc_cue_rack: both cues standing upright in the rack. The fourth run fixed their orientation; before that they lay flat through the panel.
- rc_court: painted key, circle and arc, hoop on the wall, balls, trees outside the south windows.
- rc_pingpong: tables with paddles and balls on them.
- rc_lounge: couches facing each other, chairs, coffee table.
- rc_cafe and rc_vending: soffit, pendants, counter with props, stools, tables, vending machine and Friend-O-Tron facing the hall, the Friend-O-Tron text.

What the runs changed:
- Run 1 showed a muddy grey ceiling (made unlit off-white), a parquet-checkerboard floor (switched to Rec Room's 1:4 plank tiling, 3 m x 0.75 m), a dark linoleum slab (tint lifted), and placard text too small (enlarged).
- Run 2 showed the racked cues lying flat through the panel. They are now turned upright from their measured ends, kinematic until someone takes one, and re-racked on respawn.
- Beanbag tinting through a property block works.
### Not done
- Networked play is only compile-checked (proven guard). It needs a real two-client test: radio requests (event 65 plus the seen value, repeated until the int changes), pool shot sync (event 66), the rest layout in ints 4-19 with its version in int 3, rack requests (67 -> 68 with the rack counter in int 20), bell events (64), the 14 RrNetBody props, and late joiners reading the ints.
- VR paths are not exercised by the harness: the physical cue hit (a ball touching a cue held locally in VR), ping-pong paddle returns (need a held paddle), and poke or trigger presses on the bell, radios and vending machine. Desktop click and 'use' charge-and-release on a held cue are also untested with a real player; the test drives the same code through StrikeNow and AimAt.
- Pool balls are procedural coloured spheres: solids, and stripes shown as a lighter tint, with no numbers. Rec Room's [PoolBall] is not a child of (PoolTable) (it is the table's poolBallPrefab reference), so the PoolBalls atlas is not used. The 8-ball and 9-ball buttons fell back to procedural buttons, because the resetButton nodes were not found or used.
- No seating: Interact's RrSeat does not exist, so the couches, chairs and stools are scenery only.
- The community board uses our own TMP notes, not the (CommunityBoard)_RecCenter UI prefab. The ExitSign and StayHydrated quads are placed; their facing was not checked closely.
- These fronts are GUESSES from the research doc: poker chair front (+Z toward the table), locker front, Friend-O-Tron and vending front (the shots show the fronts facing the hall, so those two are right), jukebox front (looks right).
- No arcade machines or dartboard (the bundles have no meshes for them). No global music by design: the radios and jukebox are the music, so RrRoomInfo.Music is null.
- Sounds were checked only through log lines and counters: the harness listener is at volume 0, so volumes and 3D ranges of the radios (12 / 9 / 11 m), ambience and bell are untuned. The authority's auto-advance to the next track at the end of a song was not seen in a run.
- Frame budget was not measured in the client. The build took 3.2-3.8 s in the harness, with 4 prefab loads per frame and at most 12 in flight. The shell is 319 separate boxes (no static batching). There are 14 point lights: 9 hall, 2 lobby, 3 cafe.
- The 15 COMING SOON placards reflect which activities were registered when the harness ran. A spot becomes a real door on the next build once its activity registers.
### Requests
- Rooms (RrPortal): the log says 'the door glow's material has no _TintColor: glow off', so the hover glow of Rec Room's door is disabled everywhere. Please check which property DoorGlow_Mat uses after RrMaterials.Fix.
- Interact: RrSeat (ARCHITECTURE Â§5) would make the Rec Center's couches, beanbags, poker chairs and stools sittable. The hub would call it on the seat-highlight nodes.
- Interact/Core: the prefab index does not show (PoolTable)'s poolBallPrefab ([PoolBall], layer 24, PoolBall_Mat InstancedST atlas). If the catalog can load it by component reference, RrRecCenterPool.MakeBalls already takes a template mesh and material, and applies a 4x4 atlas ST per ball (GUESS layout).
- Net: Hello/Census for late joiners is not needed by the Rec Center (all its state is in the instance's custom ints 0-21 plus the 0x52435231 marker in int 31), but the RrNetBody props rely on the prop lease showing up for late joiners.
- Core/README owner: please add the Rec Center README section below and mark Places/RecCenter done. Its event types are 64-68 and its custom ints are 0-21 and 31, scoped to the Rec Center instance.
- Watch: the Rec Center registers CoverSprite 'illustration_HomeButton_RecCenter' and IconSprite 'Illustration_Menu_RecCenter'.
## paintball
### Experience
1. The player picks Paintball on the watch or at its Rec Center door. The loading card shows the paintball gift-box art. They arrive at the room origin, in front of the REC CENTER door (RrPortal) of the paintball lobby. The lobby is a plank-walled deck in a forest clearing (flat Rec Room trees and dead pines), with lockers, two (Radio) props and the lobby radio of the selected map playing (PaintballLobby_RiverRadio or _SunsetRadio).
2. There are two ways to pick a team: stand on the Blue or Red pad (you hear "Join" and "Blue Team"/"Red Team", and your team is written to presence), or let auto-balance choose. The console has RIVER / DRIVE-IN and CAPTURE THE FLAG / TEAM BATTLE vote buttons and a big START. These are RrPressable buttons, used by click, the VR trigger ray or a poke. The console board shows the next mode and map and a status line (STAND ON A TEAM PAD / PRESS START / STARTING IN n). The north wall has a scoreboard and the PAINTBALL title.
3. Practice: three racks on the shooting line (pistol, burst rifle, shotgun) hand you that gun. You can shoot Rec Room's paintball targets on the range to the south.
4. Start: anyone presses START (it works solo, for practice), or the match starts 20 s after 2+ players are in. Teams are balanced, keeping pad choices. The 10 s countdown (GameStartDelay) plays "Ready", then the mode title ("Capture The Flag"/"Team Battle"), the PtblRiver_10Sec sting and 5up..1up. Everyone is teleported into their team's spawn area: on River the fort, on Drive-In Blue gets Frankie's Fuel and Red gets the ticket booth. The team name is announced.
5. Playing (600 s): "Gameon", then the map music (RiverPredator or PtblSunset). You spawn with the paint pistol (RrGameRoom.Loadout). Field racks hold the burst rifle, the shotgun and, on the Drive-In screen platform, the scope rifle. Taking one swaps it into your hand. That rack is empty for everyone for 15 s, then refills with the [VFX_ObjectSpawn] puff.
6. One hit and you are out (MaxHealth 1): RrHealth plays HitSound01/OutSFX, the grey veil and the "Out" VO. Other players see a KO_Skull over your head in place of your team gem. After 5 s you respawn with 5 s of protection at the team spawn point farthest from opponents, carrying the pistol again.
7. CTF: walk into the enemy flag, or close a VR hand on it, to take it. You hear FlagTaken plus "Red flag taken". The carrier holds it in the left hand (VR) or at the lower left of the view (screen); others see it in your left hand, or on your back for desktop players. If the carrier is hit, the flag drops where they fell ("... flag dropped"). A defender touching it sends it home ("FlagReturn01" and "... flag returned"); an attacker picks it up. Bring it to your own stand while your flag is home to score (ScoreSFX and "Blueteamscores"). Three captures win.
8. Team Battle: each out of an opponent is +1 for the shooter's team. 50 wins.
9. The end: at 600 s the leader wins; a tie gives "Tiegame". Team-win VO plays, then "Gameover". The base scoreboards (one per spawn area on each map) show the score and "BLUE TEAM WINS". After 15 s (RoundOver 4 s plus MatchOver 11 s) everyone returns to the lobby. The rotation moves to the next map, and the mode flips after each full rotation; lobby votes override this.
10. Other players get a spinning team-coloured gem over their heads, only for members of your own instance.
### Verified
Compile: `build.sh project --editor` gave 0 error(s), 0 warning(s) with "Server API guard: all 113 Fusion/Somnium members are proven". The editor-like runtime build and the editor tools build also had 0 errors. That was after the last code edit (197 files). The final re-run failed only on Games/Dodgeball/RrDodgeballRoom.cs (`_infos`), which belongs to another agent who was editing it at the time. Before other modules compiled, I also verified Paintball in a filtered copy: 0 errors, guard proven. All 11 .cs files have .meta files.

Harness: 3 runs of `scenarios/paintball_all.json`, all exit 0, "0 step failure(s)" out of 28 and then 29 steps.

Run 3 log:
- "built the lobby, River and Sunset Drive-In: 316 Rec Room props (0 missing), 11 racks, flags ready in 3.1 s" (goto took 4.1 s; the worst loading frame was 158 ms).
- "flag 0/1 ready (10 team-coloured materials)".
- "spawned in the Blue base on River". Loadout gave (PaintballGun) to the right hand, with the pistol spec 5/s, 33 m/s, head 30 / body 18.
- "fired 8 of 8 (PaintballGun) shots".
- "Red flag taken by player 0", then "Blue captured the Red flag", score 0 -> 1, enemy flag back home.
- "took the Shotgun from rack 3": (PaintballShotgun), 9 pellets, rack not available for 15 s.
- Out: 18 damage -> out, status Down. After 5.8 s: respawned, out False, invincible True, at a Blue spawn (the deck spawn at room-local (-1.5, 2.64, 414.5)).
- Team Battle on Drive-In: phase Playing, spawned in the shop, hits Blue 0 - Red 3, music PtblSunset.
- No exceptions from our code.

PNGs I looked at:
- pb_lobby: the deck, blue and red pads, console, title and scoreboard, lockers, radios and door.
- pb_lobby_console: labelled buttons and the "STAND ON A TEAM PAD" status.
- pb_lobby_range: the burst rifle lying on its rack with its label, the targets downrange and the tree ring.
- pb_river (run 2): the river with bridge, rocks, log piles, both forts, a dense flat-tree border, paths.
- pb_river_fort (run 1): the Blue fort with banner, parapet windows and pennant.
- pb_drivein (run 1): the whole lot with the screen, sniper stairs, 4 rows of cars with speakers, the diner with projector and AC, Frankie's Fuel and the red ticket booth.
- pb_drivein_diner (run 1): the Debbie's Drive-In / DINER signs and cars. It looks like Rec Room.
- pb_ctf_splats (run 1): blue paint splats on the fort wall beside the blue flag on its stand.
- pb_ctf_carry: the red flag tilted off its stand in the carrier's view, next to the held pistol.
- pb_ctf_home: the blue flag home, the team-coloured spawn outline and the beam.
- pb_tb_base: Frankie's Fuel canopy, pumps and sign, with the base scoreboard at 0-3.

Fixes made after looking at the pictures:
- Pad rim z-fighting.
- The scoreboard and next-map text were hidden by the console.
- Flat trees scaled 2-3.4x, because they are about 3 m maker-pen props, and fewer dead pines.
- Filled spawn pads replaced by outlines.
- Hills flattened.
- Car rows moved 2 m so the diner's east door is clear.
- The maps moved 450 m north and south of the lobby so they are out of each other's view.
### Not done
- Untested networked with 2+ real clients: the authority hand-over mid-match, flag requests arriving from remote players, remote flag carry display (LeftHand vs Root fallback), EvRack hide on other clients, TeamAssign racing the phase change, and late joiners (smaller-team pick).
- VR is untested: flag grab by grip, rack grip, button poke, and the left-hand flag carry.
- Grenades, paint mines, the paint thrower and the splat hammer are not included: RrWeapon has no thrown-grenade class. The scope rifle fires without the scope camera.
- Only 2 maps: River and Sunset Drive-In. Homestead has no rebuild proposal in the research; Spillway (Â§10.3) could be a third map.
- No out-of-bounds timer (grace 3 s / max 4 s). Invisible walls keep players in instead.
- The flag carrier's movement cooldown (flagHolderMovementCooldown 1.5 s) is not implemented; the 2020 notes removed the speed penalty.
- Sounds are checked only through the log and clip names: the harness listener is muted. I did not confirm that the clips 'Join', 'Start', 'Out', '5up'..'1up', 'FlagTaken', 'FlagReturn01', 'ScoreSFX', the 'Blue flag taken'-style lines and 'PaintballLobby_*Radio' resolve by name.
- The KO_Skull sprite marker (unlit alpha-clip quad) was never seen: the harness has no remote players. The team gems are unverified for the same reason.
- Practice targets have no hit feedback beyond paint splats.
- Lobby votes use last-press-wins, not a tally.
### Requests
- Games/Shared (RrLoadout): add an API to swap the held gun in one hand (for example Replace(prefab, side)). Racks currently call RrLoadout.Clear() and then Give(), which also removes any left-hand item in VR.
- Games/Shared (RrWeapon): support thrown Grenade/ProximityMine (Rec Room's 'Grenade' class: 3 s fuse once armed, 100 splash within r 6 m, falloff 1.0 to 0.71 r and then 0.2). Paintball would put 2 grenades on crates at River's bridge ends (rr_shooters Â§10.1).
- Games/Shared (RrScoreboard): make Title settable after Create (keep the TMP reference) so games can show mode and map on it. Paintball draws its own text below the board for now.
- Net: nothing is blocking. Paintball uses custom ints 0..11 (flags), EvFlag 64 (to the authority) and EvRack 65, plus GameRequest codes 16/17 for the map and mode votes.
- Core README owner: add the Paintball section below.
## charades
### Experience
1. The player goes through the 3D CHARADES door (or picks it on the watch) and lands in Rec Room's own Ink Space. This is the whole venue prefab, key 84: brick walls, pipes and trusses, pendant lamps, stage cans, posters, the root-beer neon and the drink rail. Our code adds the (Charades_Stage) mat on the middle strip, three blue (Sandbox_Couch) couches on the lounge rugs, the podium, and a big board above the stage. The player spawns in the lounge facing the stage. The Ink Space music (MUS_BGM_3DCharades2b_Music) plays. The board says "3D CHARADES / WAITING FOR PLAYERS / PRESS START ON THE STAGE / DRAW IT! GUESS IT!". The entrance corridor has a REC CENTER door (RrPortal) where the hull's own door was.
2. Anyone presses CO-OP CLASSIC or TEAM LIGHTNING on the podium. Presses work by desktop click, by VR poke, or by VR trigger-ray. The announcer says "Ready", the countdown ticks, then "Gameon" and the music fades out. In Team Lightning, teams are balanced and each player hears "Blue Team" or "Red Team".
3. Performers take turns. In co-op everyone rotates; in Lightning the performer comes from the active team, and team rounds alternate Blue, Red, Blue, Red. The performer is teleported to the stage mark facing the lounge and handed the Charades Pen (right hand, or the screen hand). A spotlight follows them. A golden-orange word card appears: above the left hand in VR, lower left of the view on desktop. The big card on the podium shows the word only on the performer's client; everyone else sees "No Peeking!". The board shows "UP NEXT: NAME" during the 4 s co-op intermission, then "NAME IS DRAWING 0:59".
4. Drawing:
   - **VR:** hold the trigger to draw a tube from the pen's ExtrusionPoint. Trigger pressure sets the thickness (4-22 mm radius). A undoes the last stroke.
   - **Desktop:** hold left mouse to draw at the cursor, 1.6 m from the eye. The tube thins with cursor speed (Rec Room's screen curve). Z undoes. A click on a podium button presses it instead of drawing.
   - **Both:** the podium has 12 colour swatches, UNDO and CLEAR. A coloured cursor ball shows where the ink will come out. The Maker Pen draw loop follows the drawing speed. Charades_IndividualTimerLoop (60 s) ticks through the turn; in 45 s Lightning turns it starts 15 s in.
   - **Other players:** they see the same tubes live (events every 0.1 s). A late joiner asks the performer to re-send the drawing.
5. Guessers shout on voice. The performer presses CORRECT!:
   - **Everyone:** hears Charades_TeamScoreUp, sees the green [VFX_Charades_Goal] burst, and the board shows "CORRECT! HOUSE".
   - **Co-op:** WORDS SOLVED goes up by 1 and the next performer takes over.
   - **Lightning:** the active team scores and the same performer gets a new word at once.
   SKIP deals a new word. When time runs out, SFX_Failure_Stereo plays and the board shows "TIME'S UP! It was X". Each new word or turn clears the ink.
6. Match end:
   - **Co-op:** ends after 480 s. The board says "YOU SOLVED n WORDS!", the announcer says "Gameover", and the Ink Space music returns.
   - **Lightning:** ends after 4 rounds of 90 s. The announcer calls the winning team (or a tie) and the board shows "BLUE TEAM WINS 5 - 3".
   Then back to the lobby. At any time the player can leave through the REC CENTER door.
### Verified
Compile: `build.sh project --editor` passes. The server-like build has 0 errors and 0 warnings (197 files), "all 113 Fusion/Somnium members are proven", and the editor-like runtime and editor tools builds each have 0 errors. metas.sh wrote the .meta files for all 7 new files.

Harness (3 executed runs; a 4th attempt never started because Unity hit other agents' compile errors):
- **charades_play.json, run 1:** 22 of 23 steps passed. The failure was a test-hook bug: calling SetTimer every frame kept the countdown from ever reaching zero, so CORRECT arrived during the intermission and was rightly rejected. Fixed.
- **charades_play.json, run 2:** 23 of 23 steps passed.
- **charades_lightning.json, run 3:** 17 of 17 steps passed.

Timings: the room builds in 2.9 s (Travel: built Charades in 2929 ms). The hull spawned 21.48 x 7.07 x 23.91 m with 26 lights; the stage piece is 3.71 x 0.39 x 10.02 m. The pen is found with its ExtrusionPoint, and MakeTool wired lock on pickup, CollisionSound, AnimateInOut and Respawnable. The longest frame in the build was 87.7 ms, inside the loading fade.

What the logs prove:
- Co-op flow: "turn 1: performer 0, 60 s", "you are the performer", the pen picked up in the right hand. The test drew 3 strokes (460 points; 9-10 ink events went out). A 50-byte ink event was decoded back into a stroke. "solved \"House\" (score 1)" was followed by turn 2. SKIP dealt a new word, and "ink cleared for drawing 65" appeared when the word changed.
- Lightning: "starts Team Lightning (4 x 90 s, 45 s per performer)", "turn 1: performer 0 (Blue), 45 s", "solved \"Duck\" (Blue score 1)" with the same turn and a new word, then "skipped".

PNGs (the scenarios' out/ folders):
- ch_audience, ch_from_stage and ch_drawing_side show the real Ink Space, lit by its own lamps.
- ch_door shows the REC CENTER door in the entrance corridor.
- ch_drawing_audience and chl_performer_view show the 3D ink tubes: an orange house, a yellow ring, a blue 3D spiral and the green decoded copy. chl_performer_view also shows the "Duck" hand card at the lower left and the pen in the screen hand.
- ch_board_correct and chl_board_scored show the board: "CO-OP CLASSIC 7:57, WORDS SOLVED: 1, CORRECT! HOUSE" and "TEAM LIGHTNING 1:27, 1 - 0, CORRECT! DUCK", with the green goal effect.
- chl_podium shows readable CORRECT! / SKIP / UNDO / CLEAR, 12 swatches with the selected one enlarged, and the word card.
- ch_topdown and ch_overview are not useful: one camera sat outside the hull, the other against the ceiling.
### Not done
- Only verified offline, where the local player is the authority and the only member. Two clients have never been run together, so none of these are tested: ink seen by a second client, the late-joiner resend (event 68), request retries to a remote authority, pen sync through RrNetBody, and hand-over when the performer leaves.
- VR has not been run: drawing with the trigger, pressure sets thickness, A to undo, the left-hand card, and poking podium buttons. On desktop the pen was only driven by the test hooks; drawing with the real mouse and Z-undo are untested.
- No sound was actually heard: the harness listener is at volume 0. Clip names come from the research doc.
- These parts of the flow were never reached in the harness: turn timeout, next performer after a correct guess with 2+ players, the switch between Lightning team rounds, the match-over board and announcer winner, and the pen being put back after a drop.
- Stage orientation is a GUESS (research Â§1.4): the performer faces -x toward the lounge, rugs and couches. The venue's paint-splattered drink rail between the pillars partly hides the stage from seated guests. The stage piece's exact placement is also a guess: it is snapped with its top at 0.62 on the middle strip.
- Rec Room's card mesh and push-button meshes are not used: [CharadesCard] has no container key, so the card is rebuilt at its measured size. The buttons are RrPressable.Create procedural buttons, not the Charades_Button mesh from (Sandbox_Room_Boost_Button).
- The pen's shape/material menu is not rebuilt (Rec Room has Cube/Sphere/Cylinder/Pyramid shapes, Redo and Scale). The pen draws tubes only; undo and clear are on the podium.
- The cover art does not show: 'Watch: picture not found in Rec Room's bundles: GiftBox_Charades_Tex|8,276,380,268'. The same miss happens for every GiftBox_* name (Paintball, DodgeBall, Paddleball), so the fault is in the shared cover-art lookup, not the name.
- The word list is our own: 275 entries in a family-friendly Rec Room style. 'Headphones' appears twice (harmless).
### Requests
- Rooms/Watch (RrCoverArt): GiftBox_*_Tex names from rr_watch_ui.md Â§3.3 are not found by name, for Charades and all the others. They probably need loading through container key '0' in bundle 56e54a941a1085685fd924215dfa98b5, or through RrAssets.TryFind with class 28 in that exact bundle.
- Net: the Hello/Census late-joiner service is not implemented. Charades works around it with its own event 68 (a resend request answered by the performer). A shared Census would replace it.
- Games/Shared RrGameRoom: RequestStart() sends once, with no repeat-until-seen. Charades uses its own event 67 with retries every 0.5 s for up to 4 s; the shared kit could offer the same.
- Player: on desktop, the pen in the screen hand (RrHand.ScreenOffset) sits large in the lower right and covers part of the view while drawing. A smaller or lower hold pose for pens would help.
- Editor: the scene must reference the shaders RrLook.Solid/Emissive use, since the ink tubes and cursor use them (already on Core's list, presumably).
## dodgepaddle
### Experience
DODGEBALL (watch Play > Sports, 2-6 players, gift-box art, music DodgeBaller)
1. You arrive by the south door (REC CENTER portal) of a 16 x 22 x 9 m gym. It has a two-tone wood floor, a 9 x 16 m court with white lines, a yellow centre line, a centre circle, 3 m attack lines and pale blue and red back zones. There are padded walls (navy sides, blue and red ends), high windows, ceiling trusses with light panels, backboards and orange DODGEBALL titles. A scoreboard hangs on each end wall with an info strip under it, each team has a bench on a riser beside its half, and there are Rec Room cones at the corners. Three Rec Room (DodgeballBall) balls sit on the centre line at X = -3, 0, +3. In the lobby you can pick up and throw balls at two cardboard standees, (Standee_Male01) and (Standee_Female03), on the red half.
2. With 2 or more players the lobby starts by itself after 15 s; the green START button beside the door starts it at once. Teams are balanced (blue on -Z, red on +Z). The 3 s countdown puts you on your half, frozen, with the Ready VO and ticks, and the balls reset to the centre line and cannot be grabbed until play starts.
3. Play starts with Gameon VO + Whistle01. VR: grip grabs (one ball per hand) and you throw by releasing (hand velocity, max 15 m/s). Screen: E or a click picks up; hold left mouse to charge for 0.1-0.5 s (ease-out); release throws at up to 14 m/s with 7 degrees of lift and a soft aim at the nearest opponent within 9 m and 30 degrees. Q-hold throws with the same 14 m/s config.
4. A throw by a player who is in the round is live: the ball's own [DodgeballFireTrail] burns and its trail shows. It stays live until it touches anything (floor, wall, another ball, a held ball = a block) or for 4 s. A live ball that hits an opponent (layer-27 hit volumes) puts them out: dodgeball_PlayerHit_01 + [VFX_Default_HitParticles] at them, the victim hears Out + OutSFX and is teleported to their team's bench (frozen), and the thrower hears HitSound01 + Hit.
5. Catching a live opponent throw (VR grip, or a screen click/E with an empty hand while it comes within 2.5 m) plays Catch01. It puts the thrower out and brings back the catcher's teammate who went out first, who is teleported back onto the court. Rec Room itself only cancelled the throw; RrDodgeballRoom.CatchOutsThrower = false gives that rule.
6. Crossing the centre line (0.25 m tolerance) or leaving the court (0.5 m to the sides, 1 m at the ends) puts you out after a 1.5 s grace at the start of the round.
7. When a team has nobody left in, Horn01 plays and the winning team's VO sounds (Blue Team / Red Team, or Blueteamwins2 / Redteamwins2 on the deciding round). The board shows round wins, and the strip shows the round and how many are still in per team. After 4 s the next countdown starts. The first team to 3 round wins takes the match (Gameover), and 8 s later everyone is back in the lobby by the door.
8. Alone you get solo practice: the round never ends, the standees take hits (they fall and stand up again after 3 s), START stops it, and a second player arriving sends everyone back to the lobby.

PADDLEBALL (watch Play > Sports, 2-4 players, gift-box art, music SportFunk (GUESS))
1. You arrive in the spectator gallery of a dark sports hall with a grid floor and light strips. Beside you is a glass tube 3 x 3 x 12 m with glowing white edge rings (blue and red at the ends), Rec Room (SandboxToolGoalLarge) frames stretched over both 3 x 3 m back walls, blue and red standing pads with glowing lines, and 4 Rec Room (Paddleball_Paddle) on team-coloured stands. Three scoreboards with info strips, two benches, the REC CENTER door and a START button complete the room.
2. The start works as in Dodgeball (auto with 2+ players, or START). The countdown teleports you into your end of the tube (blue -Z, red +Z; 2v2 side by side) and puts your paddle in your right hand (LockOnPickup; it stays there until the match ends).
3. The zero-gravity ball (Rec Room's TennisBall scaled to 0.1 m, with a trail and a small glow) floats at the server's serve point, 1 m in front of the pad at chest height, and a whistle sounds. The server hits it. If the server does not, it serves itself slowly (4 m/s) after 5 s, or after 1 s when that side is empty.
4. VR: swing the paddle through the ball. The hit comes from the face box: v_out = reflect(v_ball - v_face, n) + v_face, at least 5 m/s, 5 % faster than the rally, capped at 14 m/s, always toward the other end. Screen: a click swings the paddle (0.25 s) and returns a ball within 2.4 m in front of you along the crosshair. Every hit plays RacketHit01 for everyone plus the paddle's own [VFX_Impact_Particles] and haptics.
5. The ball bounces perfectly off the glass (TennisBall_Impact01a). Reaching the back of an end scores for the other team: Goal VO, CrowdCheer01, [VFX_Default_Goal], 2 s of scored state, then the conceder serves. The boards show the score and the strip shows BLUE SERVES / RALLY n / GOAL!.
6. First to 5 wins: the winners hear AmyVO_Generic_YouWin, everyone else the team-wins VO, then Gameover. Paddles go back to their stands and players back to the gallery. Alone, the empty red end serves itself, so you can play against its serves.
### Verified
Compile: bash compilecheck/build.sh project --editor gives 0 error(s), 0 warnings, 197 files, 'all 113 Fusion/Somnium members are proven', editor-like runtime build 0 errors, editor tools build 0 errors. That was the final run, after every edit.

Harness (4 runs used): the first used a scenario path, which run.sh cannot resolve, and never booted. The second was blocked by another module's compile error (RrRecCenterRoom CS0136). The two real runs follow.

dodgeball_gym: booted, 0 step failures of 20. Every expect_log was found: 'Dodgeball: gym built: court 9 x 16 m, 3 balls', 'DodgeballTest: start: phase Playing, practice True, balls 3, local team 0', 'Dodgeball: practice target 0 hit by ball 1 (knocks 1)' (a live 14 m/s throw with 7 degrees of lift swept into the standee's hit volume), 'Dodgeball: verdict hit: out 77, by 0' (the claim to the authority, the 150 ms catch window, then the verdict event), and 'round 1 won by Blue: blue 1 - red 0'. Also seen: CollisionSound Bounce played, the Tools line for (DodgeballBall) with 'screen ThrowDodgeballToolBehaviorConfig 14 m/s', and the build took 2.9 s.
PNGs:
- dodgeball_overview: the whole gym. It looks like a Rec Room gym: wood court, lines, circle, team zones, benches with the BLUE BENCH sign, windows, trusses, the scoreboard reading 'WAITING FOR PLAYERS / FIRST TO 3 ROUNDS', and 3 red balls on the centre line.
- dodgeball_centre: the Rec Room balls on the yellow line and both standees rendered with their real art.
- dodgeball_north: the length of the gym toward the door, START pedestal and blue dado.
- dodgeball_door: the Rec Room activity door with the REC CENTER sign, the START button and sign, readable scoreboard text.
- dodgeball_knocked: standee 0 lying flat with the hit particles and the practice text on the board.
- dodgeball_roundover: board 'ROUND OVER'.
Fixed after this run and not shot again: the Custom text had hidden the team scores, so the round and in-count text moved to an info strip under the boards.

paddleball_court: booted, 0 step failures of 18. Seen: 'court built: tube 3 x 3 x 12 m, 4 paddles ((Paddleball_Paddle)), ball (TennisBall) r 0.1 m', 'Grab: first pickup: (Paddleball_Paddle) in the Right hand (screen mode, Rec Room grip pose)', 'paddle 0 given to the local player (Blue, screen)', 'first paddle hit: paddle 0 at 9.0 m/s', 'goal for Blue: blue 1 - red 0' (1 wall bounce on the way), 'automatic serve by Red (rally 3)' (the empty side serves after 2 s scored + 1 s), and 'goal for Red: blue 1 - red 1'.
PNGs:
- paddleball_overview: the tube with its glowing frame, the green Rec Room goal frames on both ends, the stands with paddles, the gallery benches, the door and START.
- paddleball_tube: the view down the tube to the red goal and the far scoreboard.
- paddleball_serve: the camera ended up just behind the blue goal face, so the image is tinted green, but it shows the Rec Room paddle held in the screen hand and the strip reading 'BLUE SERVES' with 0-0.
- paddleball_rally: the boards reading 1 - 0 and 'RALLY 0', the ball's yellow trail flying toward blue, and the goal sparkles.
Both runs' only errors were two URP shadergraph import errors from the harness project.
### Not done
- Networked play is untested: no second client. Verdicts and claims were tested offline only, where the local player is the authority and a fake opponent (id 77) stood in. Untested online: resending claims, RrNetBody throw ids and kind int across clients, the paddle hit taking the ball from another client (LocalTouch), and seat assignment for 2v2.
- Untested in the harness: VR paddle hits from the face box (the harness called the same PaddleHit path directly), screen-mode swings, screen-mode charged throws (the test used the same velocity math), the screen catch assist, real catches (VR or screen), the centre-line and out-of-court check, the bench teleport and freeze, bringing a teammate back after a catch, and match over with the return to the lobby.
- Catch rule: the task asks for 'catch = thrower out + a teammate returns', and that is the default. rr_sports.md Â§2.2 says Rec Room only cancelled the throw; RrDodgeballRoom.CatchOutsThrower = false gives that rule.
- Rec Room had no timer on dodgeball rounds, so ours has none (RoundSeconds 0). Players who hide can stall a round; only a player leaving ends it early.
- The paddleball ball is Rec Room's (TennisBall) scaled to 0.1 m, because the real ball is in the encrypted level. The serve point, 5 s auto-serve, 5 %-per-hit speed-up, 14 m/s cap and 4 m/s auto-serve are GUESSES from the research.
- The paddleball goal faces are the Rec Room sandbox goal's own green, not tinted to the team colours.
- The scoreboard info strip (text under each board) was added after the dodgeball run and has no screenshot. It compiles and the paddleball run shows it working.
- Sounds were checked only as clip names and log lines. The harness listener is muted, and duplicate names such as Horn01 (2.45 s and 0.25 s) load whichever one the catalog finds.
- Out players are frozen with SetMovement(0,false). VR players can still walk off the bench physically.
- ArrivalStatus is not overridden (Lobby by default).
### Requests
- Games/Shared (RrScoreboard): the Custom text replaces the team scores. Please add a separate info line. Until then both games use RrDodgeballRoom.InfoLine(board, width), a dark strip with text under the board.
- Interact (RrTools.MakeTool): it adds RrRespawnable (ToolCleanup) to game props (dodgeballs, paddles), and both games Destroy it again. An option such as MakeTool(inst, popIn, respawn: false) would be cleaner.
- Compilecheck: Unity rejected CS0136 (a lambda local shadowing an enclosing local, in RrRecCenterRoom.cs:417) but the offline check passed it. The offline compiler's LangVersion is probably newer than Unity's; please pin it to -langversion:9.
- Harness: run.sh with a scenario path (scenarios/x.json, as the task text suggests) fails to boot with 'unreadable', because the path is resolved against unityharness. Pass the bare name (run.sh dodgeball_gym). Please accept paths or fix the task text.
- Rooms/Watch: both games register Category "Sports". Cover and icon are "GiftBox_DodgeBall_Tex|8,276,380,268" and "GiftBox_Paddleball_Tex|8,276,380,268", as in RrWatchArt.
## quests
### Experience
1. The player comes through the GOLDEN TROPHY door in the Rec Center and arrives in a castle courtyard, 24 x 16 m. The walls are flagstone with battlements, and there are red banners, torches, stone pillars, barrels and hay. The Golden Trophy "(Golden_Bucket)" turns slowly on a lit pedestal. GoldenTrophyExplore plays quietly. A REC CENTER door (RrPortal) leads back.
2. Three wooden racks hold Rec Room's own quest weapons: 4 "(QuestSword)", 4 "(QuestShield)" and 4 "(Longbow)". The player takes one by clicking it or pressing E on desktop, or by gripping it in VR. The emptied peg refills 3 s later. The sword and shield get RrMelee; the bow gets RrWeapon.
3. Anyone presses the red START QUEST button by the gate. A 5 s countdown runs, then the whole team is teleported into Act 1, and each client moves its own player. An "ACT 1 / The Forest Camp" title shows for 3.5 s.
4. Waves of Rec Room's goblins pop in with a smoke poof. The goblin is the skinned rig from the Maker spawner, with its idle and walk clips. GoldenTrophyCombat2 starts. Waves follow the act's triggers: act start, the previous wave down to N alive, the previous wave cleared, or entering a zone. Waves are sized for 4 players and scaled down to 55/70/85 % for 1/2/3 players.
5. Kills and smashed clay pots earn team gold. A goblin is worth its max health (100); "(FatPot)" gives 50 and "(FatPot_Large)" 100. Gold appears as the [VFX_GoldCoins] burst with Quest_Coins_Spawn01 and a floating "+100". A HUD shows GOLD n and HP.
6. When every wave is dead, GoldenTrophyClearFanfare2 plays, the toast "The way is open!" appears and the wooden double doors swing open. Walking through the door moves the team into the next act, where the exit door of the previous act stays open.
7. The acts:
   - Act 2, ramp hall: imps arrive; a wave spawns "Behind you!"; a side room holds 4 large pots and a crossbow rack.
   - Act 3, boulder ramps: players press PUSH THE BOULDER, and Rec Room's LavaBoulder rolls down three ramps and crushes goblins.
   - Act 4, lava bridges: barrel knights arrive; only the target on their back takes damage; imps knocked down over the lava die in it.
   - Act 5, split path: elite goblins and imps.
   - Act 6, courtyard towers: ramps up the towers; bows and a crossbow wait on the tops.
   - Act 7, hedge maze: enemies spawn ahead of and behind the team as it walks through.
   - Act 8, half-lava hall.
   - Act 9, throne room: the Goblin King.
8. Players stepping into lava go down and are moved to the act's entry.
9. Arrow and bolt hits stun basic goblins and imps for 1.5 s; elites are immune. A held blade parries goblin swings.
10. At 0 HP the player is down: slowed to 30 %, cannot fight, and enemies ignore them. Each down costs the team 2000 gold. Teammates see a red ring and a glowing REVIVE orb at the downed player's feet.
11. A teammate within 2.5 m revives them by holding E while looking at the orb (desktop) or by gripping the orb (VR), staying within 1 m, while Quest_Revive plays. The downed player gets back up with 50 HP and 3 s of invincibility.
12. If nobody revives a downed player within 30 s, they get up at the act's checkpoint. Everyone who is still down gets up and is healed when the team enters the next act.
13. If the whole team is down, the run fails: "Golden Trophy - Our Heroes Have Failed" plays, the HUD shows OUR HEROES HAVE FAILED with gold and rating, and after 8 s everyone is back in the lobby with pots and racks reset.
14. The Goblin King is Rec Room's red barrel knight scaled x1.75 (about 6 m) with a gold crown. He has 3000 x (0.55 + 0.15 x party) HP and only his back target takes damage. He chases, aims and charges, and every 4 s fires three large fire orbs. A red boss bar shows his health.
15. Each quarter of health he loses calls in 2 goblins and 2 imps; from the third quarter an elite goblin and an elite imp join them.
16. When he dies, the [VFX_BossGoldCoins] burst and Quest_BossCoins play. The Golden Trophy drops onto the throne and four treasure chests open.
17. 6 s later the victory screen shows: "Crimson Cauldron - Our Heroes Have Triumphed" (the clip Goblin1 reuses), OUR HEROES ARE VICTORIOUS!, gold and an S/A/B/C rating. After 20 s everyone is back in the lobby.
18. The throne room also has a REC CENTER door.

Networking: the instance authority runs the act machine in the instance custom ints, and runs every enemy in actor blocks 0-1 (30 slots). Pots use the bitset in block 2. Other members draw follower copies of the enemies. Enemy attacks are replayed on each client against that client's own player. Other players' held weapons are drawn from presence hand items.
### Verified
Compile: `build.sh project --editor` passes after every change. The last run reported 0 errors and 0 warnings on the server-like build (197 files), "all 113 Fusion/Somnium members are proven", and 0 errors on both the editor-like runtime build and the editor tools build. metas.sh wrote the .meta files, and the last run had nothing new to write.

Harness: 4 runs, which is the limit.
- Run 1 exited 2 after 11 s because I passed the scenario as a path; it did not boot. run.sh needs the scenario name.
- The other three booted with 0 step failures.
- None of them logged an exception from our code. The only 2 errors in the logs are Unity's own shader-import errors.

Run "goldentrophy_run" (34 steps):
- The quest was built 4.2 s after boot at (-1000, 0, 6000): 9 acts, 46 pots worth 2900 Gold, 7 racks, 77 kit props.
- Start: act 1 wave 1 had 2 goblins for a party of 1, so the scaling works.
- HitAll twice gave "kills 2, gold 200", and wave 2 triggered at once.
- NextAct cleared the act, walked the player through the door, and the team was moved to act 2.
- In act 3 the boulder was released for a 16.0 s roll.
- Act 4 spawned the knight and goblins.
- In act 9 "the Goblin King rises: 2100 HP (party 1), 6.0 m tall", and his first fire orb was launched.
- Clearing the act dropped the trophy, and "VICTORY ... rating C" followed.

Run "goldentrophy_acts":
- BreakPots gave pots broken 3 and gold 150 (3 x 50).
- Act 7's maze waves waited for the entry zone, as they should.
- The Down hook took the 2000 penalty. That gave "FAILED" 1 s later, then "back in the lobby": gold 0, pots reset, player teleported to the lobby.

Run "goldentrophy_final":
- 1080 boxes textured with Rec Room's Stones_Modular_Theater_Mat and Wood_Floor_Matte_mat, 79 tilings.
- VICTORY again.

What the PNGs show:
- gt_lobby / gt2_lobby / gt3_lobby: the castle courtyard with battlements, red banners, torches, stone pillars, barrels, hay, the gate, the START QUEST button and the trophy pedestal.
- gt_racks / gt2_lobby: 4 blue quest swords hanging point-up on the wooden rack. In run 1 the shields lay flat; in run 2 they face the players.
- gt_act1 / gt_act1_camp: the forest camp with Rec Room's cardboard trees, campfires, the log pile, the stump, hay, pots, and two skinned green goblins with swords.
- gt_act2 / gt3_act2: the ramp hall with the ramp, the upper floor, the exit door, banners and an imp.
- gt_act3_boulder: the LavaBoulder rock rolling across landing 1.
- gt3_act3: the boulder at the foot of ramp 3, with two goblins idling in the arena off its lane.
- gt_act4_lava (run 1): the lava was invisible, because Rec Room's Water_Lava_Mat draws nothing on a plain box.
- gt2_act4: the lava shows as our glow with crust plates. It was too yellow, so I toned it down; that change is not re-shot.
- gt2_act7: the green hedge maze with the end room beyond.
- gt_boss_close / gt2_boss: the red barrel knight at 6 m with its halberd. The crown floated in run 1; from run 2 it sits on the head.
- gt_victory / gt3_victory: the Golden Trophy on the throne with coin sparks, and the four chests open with gold.

Fixes made from the pictures: trees turned to show their painted side, shield rotation, lava material, crown height, wall and floor textures, and the rock prefab names. The run-1 name "(Rock_01)Variant" was not found. I switched to the kit list's "(Rock_01)Variant_Sandbox)Variant", which has not been confirmed.
### Not done
- Networked play is completely untested: the actor-block publish and follow, the EnemyAttack and EnemyVolley replay on watchers, the Request repeats, the Revive event, the pot bits on other clients, the remote held-weapon display (RrQuestGear), authority hand-over mid-act, and late joiners. All of it is compile-checked only.
- Nothing checked the last visual changes in the harness (the run limit was reached): the toned-down lava colour, the flagstone texture on the throne-room and act walls in every act (only the lobby, act 3 and the victory shot were seen), the new rock prefab name '(Rock_01)Variant_Sandbox)Variant', and moving act 3's goblins into the boulder's lane with a wider crush radius. In gt3_act3 the boulder did not kill the goblins, because they idled off its path.
- Screen HUD overlays (title card, toasts, score, HP, boss bar, revive prompt) are not visible in harness shots, whose camera renders only the world. They show up only as log lines. The VR world-space HUD placement is untested.
- The bow has no draw: RrWeapon fires the Longbow on the trigger at full speed. Arrow stun works only through the (Longbow)/(Crossbow) tool ids in RrCombat.OnActorHit and is untested at runtime.
- Projectiles do not break pots (RrProjectile does not call RrBreakable). Blades, throws and a screen-mode click do. Only the broken state of pots is networked, not their position.
- Enemies have no NavMesh (RrEnemy steers only), so they cannot path up ramps around obstacles. In act 3 the goblins wait at the bottom, and in the maze they may get stuck on hedges.
- Parts of the Goblin King are not done: he does not turn toward whoever hit him, a shield bash does not stun him, and thrown swords get no x1.5. His axe is the barrel knight's own halberd.
- Health potions (act 7 GUESS), holstering, per-player chest rewards, storing the best rating, and the potion and double-damage buffs are not done.
- The acts are open to the sky and lit by the world's daylight sun plus torches; there is no interior darkness. The act titles ('ACT n / name') are our addition.
- Base RrGameRoom still plays the announcer lines 'Ready', 'Gameon' and 'Gameover' in the quest, which Rec Room's quests do not have.
- The desktop revive key is E, the grab key (held while looking at the marker), and it may also try to grab. The VR revive is a grip on the orb, not a real handshake with the downed player's hand.
- Rec Room quests' VR teleport limit (4 m, 1 s delay) and walk speed override of 3.5 m/s are not applied.
### Requests
- Games/Shared (RrGameRoom): add a virtual (for example `protected virtual bool Announces => true`) so quests can turn off the Ready/Gameon/Gameover lines.
- Games/Shared (RrEnemy): (a) add a static `OnAttackStarted(RrEnemy, Vector3 aim)` event raised in StartAttack/Shoot, so RrQuestEnemies can replay the exact shots instead of rebuilding them from public tuning; (b) add a public `SetTarget(int playerId, float stickySeconds)` for 'turns to whoever hits him'; (c) make `Height`/`Radius` rescaling official (RrGoblinKing scales the root x1.75 and multiplies Height and Radius itself); (d) add a NavMesh or waypoint fallback so ground enemies can climb ramps.
- Games/Shared (RrWeapon): add a bow draw (the second VR hand pulls the string 0-0.6 m; on desktop, hold for 0-1 s) that scales arrow speed and damage 17-67 from the Longbow data.
- Interact or Games/Shared: when an RrProjectile hits a collider whose object has an RrBreakable, call Break(point, velocity), so arrows and bolts smash pots (rr_quests 2.4).
- Player: expose a read-only right/left 'held tool' pose for desktop remote players (the screen-hand position). RrQuestGear now guesses root + (0.3, 1.05, 0.35).
- Net: nothing is blocking. Note for other games: RrNet.Now is local time (Time.unscaledTimeAsDouble), not a shared clock, so shared hazards must use the instance timer, as RrQuestRoom.HazardSeconds does. Quests use RrNetActors local blocks 0-1 (enemies) and 2 (pot bitset) per instance.
- Rooms/README owner: add the quest framework API below to the module README; future quests (Crimson Cauldron, Lost Skulls, Jumbotron, Crescendo) build on Games/Quests/Shared.
