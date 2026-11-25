# Iteration 10 - Jumbotron in-engine lighting audit (agent: audit)

Status: DONE - 2 of 5 harness runs used. The consolidated answer is in the "FINAL" section at the end of this file.

## 0. Setup / scenario
- scenario: SP/harness/scenarios/jt10_audit.json (149 lines, 29 shots, 33 dumps). Foyer: arrival + wide + grid-off +
  env-off (RrEnvironmentTest.TestOff = world default lighting) + cover-wall boxes + floor + view back; Armory and acts 1-9:
  arrival (1.6 m, fov 75, 12 m along EntryYaw) + wide (fov 100, tilted up); grid-off pairs in Armory and act 1.
  Per area: RrLightTest.GridWait (grid report + room census), RrEnvironmentTest.Report, dumps "Rr Scene <key>",
  "Rr Floors <key>", "Hull <key>" (material name + shader + queue per renderer).
- Arrival yaws (jumbotron_run summary): Armory 269 (-x), act1 359 (+z), act2 269, act3 89, act4 89, act5 269, act6 179,
  act7 359, act8 89, act9 324 (NW). Act i root = ActSlot(i) = ((i%4-1.5)*250, 0, 250*(1+i/4)); entry = local (0,0,0).
- No harness/CODE edits (DescribeScope takes an argument, so only DescribeJumboFoyer is callable; the rest of the census
  comes from the dumps + the rrsk platemat/plate/dress records + RrJumbotronHull.MaterialFor).
- runs used: 2 / 5 (run 1 = jt10_audit.json full tour, exit 0; run 2 = jt10_audit2.json env-off / own-env / GT-env)

## 1. Code + data reading (before the run)
- RrJumbotronHull.MaterialFor: platemat inst=1 and the install has it -> the install material (RrMaterials.Fix).
  Else: col.a < 0.99 -> RrLook.Glass (URP Lit transparent, smoothness 0.9, q3000, NOT grid-lit);
  name has Emit/Glowstick/Glow_ -> RrLook.Emissive (URP Lit + _EMISSION colour x1.4, NOT grid-lit);
  else RrLook.Solid (copy of AG-MakerPenShape, _BaseColor = col x plate tint c=; grid-lit) + install texture on
  _BaseMap/_MainTex if the shader has them (Linoleum_Flecked_Tex -> Tile_Linoleum_Flecked_Tex).
- Dress boxes: m="?name" (level-only) -> no material lookup -> c.a < 0.99 -> Glass, Emit/Glow -> Emissive, else Solid(c).
- Floors (RrJumbotronArea.GroundMaterial): the H height grids: named install material, or Metal_FloorTread_Shiny_Mat /
  Concrete_LightGrey_2x_Mat by kind, else RrLook.Solid(linoleum 0.36/0.30/0.27, vertcolor 0.42/0.44/0.50, 0.30/0.31/0.34).
- Environment table Jumbotron_Foyer: amb=trilight sky 0.0152,0.0437,0.1248 / EQUATOR 0.3104,0.0793,1.0815 /
  ground 0.0242,0.0437,0.1651; fog linear (0,0.404,0.604) 5-45.3 m. -> any vertical surface gets a strong violet ambient
  (blue channel 1.08!), up-facing floors get almost nothing (sky 0.015/0.044/0.125) -> hypothesis for (a) and (c).
  Other scenes: Armory ambient near zero (0.01-0.05), Hallway1 equator 0.47,0.10,0.27, Reception equator 0,0.37,0.47,
  Stadium equator 0.20,0.02,0.33, StadiumEntry 0.16,0,0.31, Garage equator 0,0.044,0.10 (all bluish/violet, mostly dark).
- Foyer rrsk (G records): 2,664 plates, 52 dress. Plate area by material: Unlit_Black_mat 3,374 m2 (inst=1),
  Cinderblock_VertColor_Mat 1,384 m2 (inst=0; c=0.196,0.482,0.69 on 1,210 m2 = the blue walls; 0.667 grey on 174 m2),
  Linoleum_Flecked_Brown_Mat 999 m2 (floor, inst=0, col 0.578,0.547,0.529), Paint_DarkGrey_Mat 931 m2 (inst=1, c=0.133),
  Portal_Pink_Foyer_Mat 483 m2 (skipped), Wood_Floor_Wide_mat 402 m2 (inst=0), Wallpaper_SciFiPanels_Big_Mat 372 m2
  (inst=0, c=0.667), Scifi_AreaFog_Cyan_Mat 190 m2 (skipped), Wallpaper_Dark_Mat 88, Door_SciFi_Cardboard_mat 79 (inst=1,
  col alpha 0.354), Pillar_SciFi_Pink_mat 74 (inst=0, col 1,0.392,0.946), Scifi_Rug 30, Terminal 28, WallSign_Red 3.
- Foyer dress: 22 Scaffolding (0.60 grey, Solid), 12 Quest_SciFi_CoverWall_End/Mid with c=1,1,1,0.354 and
  m=?Wall_SciFi_Cover_Modular_mat -> RrLook.Glass (35 % white, URP transparent) at local x -20..-25, z +-2..5 = the
  suspected "see-through pink boxes"; 4 wall/bits_LOD0 c=0.11,0.68,0.96,0.354 (?Wall_SciFi_Decor_Blue_mat) -> Glass too;
  WallVent 0.354 -> Glass; Clock glass 0.08 alpha (really glass). Alpha 0.354 recurs on opaque level materials
  (Speaker_Intercom_mat, Door_SciFi_Cardboard_mat): it is an unused _Color alpha, not transparency.

## 2. Per-area results (luma, grid stats, surface census)

## 3. Findings
### (a) flat violet walls
### (b) see-through pink boxes in the Foyer
### (c) near-black floors
### (d) other problems

## 4. Screenshot descriptions

## 5. Summary / recommendations

## RUN 1 - first look (Foyer shots, while the run continues)
- KEY: audit_foyer_arrival_envoff (RrEnvironmentTest.TestOff = the world default white trilight + sun, no fog, grid on)
  looks like Rec Room: white PropBox crates, brown chairs, flecked grey linoleum floor with its texture and sun shadows,
  light-blue cinderblock walls (c=0.196,0.482,0.69), pink pillars + striped curtains + lit stage at the far end, grey
  ceiling beams. SAME materials with Jumbotron_Foyer's environment on (audit_foyer_arrival) = everything violet/indigo,
  floor near-black navy, ceiling teal (fog), crates saturated violet. => the flat violet look and black floors come from
  the ENVIRONMENT (trilight equator 0.31/0.08/1.08 + sky 0.015/0.044/0.125 + teal fog 5-45 m), not from the materials.
- audit_foyer_arrival_gridoff vs audit_foyer_arrival: practically identical; grid on only adds a faint pink glow on the
  far stage. The grid's 15 lights do not reach the spawn/audience area.

## RUN 1 results (exit 0, 139 steps; all shots + hull/kit dumps OK; the 11 "Rr Floors" dumps FAILED = no such object:
## the Jumbotron levels' visible H grids were dropped/none, the floors are hull plates). Copies: NSP/jt10/audit/run1/.
Mean luma (post on, 1280x720): Foyer arrival 47 (grid off 46, ENV OFF 108), wide 34, boxes 34, floor 29, back 31;
Armory 32 / wide 25 (grid off 29); act1 44/44 (grid off 44); act2 40/36; act3 17/17; act4 29/28; act5 30/32; act6 25/22;
act7 31/24; act8 40/36; act9 31/28.
Room census (RrLightTest.Report = whole quest room, active areas = current + next):
  Foyer 153 renderers: 130 grid shaders (130 lit), 23 URP; 15 Unity lights. Armory(+act1) 245: 195 grid / 50 URP.
  act1(+2) 300: 225/75; act2(+3) 425: 342/83; act3(+4) 492: 396/96; act4(+5) 392: 341/51; act5(+6) 499: 445/54;
  act6(+7) 518: 449/69; act7(+8) 497: 388/109; act8(+9) 725: 455/270; act9 413: 226/187 (boss + enemies URP).
  => every grid-shader renderer holds the grid block (lit == grid count except 3-6 enemies); the grid is working.
Grids (lights / cells lit): Foyer 15 (4 hemi) 6624/6912 cells 1.73x1.27x1.46 m; Armory 10 6912/6912 0.66x0.8x0.55 m;
  Battlefield1 18 5485; Hallway1 32 5415; Hallway2 34 3781; Reception 30 6660; StadiumConcession 23 4866;
  StadiumEntry 46 1705 (cells 5.3x7.4x7.5 m); Garage 33 2455; Cargo1 66 4340; Stadium 74 (64 Unity) 4491.

### BIG FINDING 2: acts 1, 2, 5, 8 are lit with the FOYER's violet environment at their arrival
EnvTest reports after each TestActN: act1 -> "env: Jumbotron_Foyer", act2 -> Jumbotron_Foyer, act3 Hallway2 OK, act4
Reception OK, act5 -> Jumbotron_Foyer, act6 StadiumEntry OK, act7 Garage OK, act8 -> Jumbotron_Foyer, act9 Stadium OK.
Cause: RrEnvironment.SceneBuilt registers each scene with the KIT's placed-record bounds (+margin), and RrEnvironment.Pick
falls back to the activity default (Jumbotron_Foyer) when the head is in no scene. The Jumbotron hull (plates) is built
after the kit and is not in those bounds, and the arrivals sit at the rooms' edges:
  Battlefield1 registered z 255..300 world, arrival z 250 (5 m outside); Hallway1 x 28.6..124.6, arrival x 125 (0.4 m out);
  StadiumConcession x -189.6..-126.6, arrival -125 (1.4 m out); Cargo1 x -371.7..-307.7, arrival -375 (3.3 m out).
  (StadiumEntry holds its arrival by 0.7 m only.) DescribeScenes: Foyer 37x17x33, Armory 19x9x15, Battlefield1 43x13x45,
  Hallway1 96x14x44, Hallway2 90x15x81, Reception 53x12x32, StadiumConcession 63x17x44, StadiumEntry 35x15x113,
  Garage 57x15x65, Cargo1 64x15x66, Stadium 39x15x51.
Fix idea (for the fix phase): register the Jumbotron scene with the rrsk B box (or the hull's bounds) - or make the
quest's own act the environment key (RrEnvironment.ForcedKey / area) while the act is current.

## 4. Screenshot descriptions (run 1; DN/iter10/screens/audit_*.png)
- audit_foyer_arrival (47): console hologram left, two practice-weapon crates (PropBox, install AG/Standard) saturated
  violet, chairs pale, aisle to the stage; walls flat blue-violet, ceiling teal (fog on Unlit_Black), floor near-black navy.
- audit_foyer_arrival_gridoff (46): same picture; only the far stage loses a faint pink glow.
- audit_foyer_arrival_envoff (108): the SAME scene under the world's white default ambient + sun, no fog: white crates,
  brown/black chairs, flecked grey linoleum floor with shadows, light-blue walls, grey ceiling beams, pink-lit stage with
  pink pillars and cream curtains. Reads as Rec Room: materials are fine; the environment makes it violet.
- audit_foyer_wide (34): fov 100 from the spawn: huge flat violet slabs left/right (walls by the entry), ceiling beams
  violet-blue in teal fog, the audience far away.
- audit_foyer_boxes (34): the stage from the audience: 12 translucent pink/violet boxes on the stage = the
  Quest_SciFi_CoverWall_End/Mid dress boxes as Rr Glass FFFFFF (URP Lit transparent, alpha 0.354); violet curtains and
  pillars, pink LED wall behind; the wood stage floor dark purple.
- audit_foyer_floor (29): the audience chairs on a navy floor that shows no texture or light pool; a standee violet.
- audit_foyer_back (31): from the stage back to the entrance: violet curtains/pillars, see-through pink cover boxes at
  both sides, the near-black wood stage floor, teal-fogged ceiling.
- audit_armory_arrival (32): everything murky teal: fog (0,0.60,0.48) from 2.9 m over near-zero ambient; the lit
  sci-fi exit door at the end; locker (teal) and stools (olive) at the sides; big black wedge lower right (a dark
  plate / bench top seen edge-on); walls and floor unreadable.
- audit_armory_arrival_gridoff (29): same but the locker and stools go black-grey: the grid lights the install props.
- audit_armory_wide (25): same, the ceiling a black mass.
- audit_act1_arrival (44, FOYER env): a straight corridor with flat violet walls and ceiling and a navy floor, no
  texture/shading; the port's sci-fi frame ahead with the teal-fogged launch bay behind it.
- audit_act1_wide (44): same, wider. audit_act1_arrival_gridoff (44) identical (no grid light in the corridor).
- audit_act2_arrival (40, FOYER env): long hall, a row of blue-violet lockers on the left (install), flat violet wall
  and a window/panel on the right, navy ceiling and floor, the next room's door far ahead in teal fog.
- audit_act2_wide (36): same, wider.
- audit_act3_arrival (17, Hallway2 env: ambient ~0.00/0.02/0.11, fog 0-40 m navy): near-black navy corridor; install
  lockers left (teal, grid-lit) and money signs glowing; enemy laser beams; walls/floor/ceiling unreadable.
- audit_act3_wide (17): same darkness.
- audit_act4_arrival (29, Reception env: equator 0/0.37/0.47): flat teal walls, black ceiling, navy floor, the
  trigger pad and door ahead; lockers at both edges; a pink-lit box far ahead.
- audit_act4_wide (28): same corridor wider; flat teal slabs, black ceiling mass.
- audit_act5_arrival (30, FOYER env): saturated royal-blue door leaves/blocks (install Door_SciFi_Cardboard?) against
  violet walls, navy floor; teal fog at the far end.
- audit_act6_arrival (25, StadiumEntry env): the only "lit" arrival: a red-lit concrete floor pool (grid lights on the
  install Concrete_LightGrey_2x_Mat), crates and a barrier, navy walls, green-grey sky band. Reads as a lit space.
- audit_act6_wide (22): same with a red/purple-lit ceiling.
- audit_act7_arrival (31, Garage env ~0 ambient): murky blue-grey hangar; grey concrete floor faintly lit, truss beam,
  navy walls; readable but dull.
- audit_act8_arrival (40, FOYER env): violet everything - walls, floor, beams; teal ceiling; a green-grey pillar.
- audit_act9_arrival (31, Stadium env): two big translucent grey-blue boxes left/right right in front of the camera
  (cover walls as Rr Glass - 109 of them in the arena), dark purple ceiling/walls, the arena beyond reads well
  (orange-lit floor, red seats, magenta signs).
- audit_act9_wide (28): same, the glass boxes frame the view.

## Census from the run-1 dumps (hull = "Hull <key>", kit = "Rr Scene <key>")
- Foyer hull: 14 plate meshes, ALL level-only ones on AG/MakerPenShape copies (grid-lit): Cinderblock_VertColor_Mat|0.2,
  0.5,0.7 and |0.65, Wallpaper_SciFiPanels_Big_Mat|0.65, Linoleum_Flecked_Brown_Mat, Wood_Floor_Wide_mat,
  Pillar_SciFi_Pink_mat, Wallpaper_Dark_Mat, Wall_Vertcolor_mat|0.15, Paint_DarkGrey_Mat|0.15, Terminal, WallSign_Red;
  install: Door_SciFi_Cardboard_mat + Scifi_Rug_Mat (AG/Standard-Opaque), Unlit_Black_mat (AG/Unlit-Color, not lit
  by anything by design). Dress: 22 Rr Solid 989898 (scaffolding), 4 Rr Solid 4BE1FF, 4 LockerFountainStageLight_mat,
  and 18 URP Lit q3000 GLASS: 13 Rr Glass FFFFFF (8 Quest_SciFi_CoverWall_End + 4 _Mid + 1 SciFi_WallVent_LOD0),
  4 Rr Glass 1DADF5 (wall_LOD0 x2, bits_LOD0 x2), 1 Rr Glass 9CC3E8 (Clock_Housing).
  Foyer kit: 39 merged MeshRenderers (32 AG/Standard-Opaque, 7 maker-pen), 15 lights, 12 particle systems.
  => the "18 floor renderers URP Lit" of light9 were these 18 glass dress boxes; the hull itself is grid-lit maker-pen.
- Glass (URP Lit transparent) dress boxes per act: act1 46, act2 25, act3 50, act4 38, act5 9, act6 36, act7 25,
  act8 79, act9 133 - mostly Quest_SciFi_CoverWall_End/Mid (Foyer 12, act1 33, act3 28, act8 45, act9 109 !) plus
  wall_LOD0/bits_LOD0 (Wall_SciFi_Decor_Blue) and SciFi_WallVent_LOD0. All come from c alpha 0.354 on an OPAQUE level
  material (Wall_SciFi_Cover_Modular_mat etc.) -> RrJumbotronHull.Build "x.Col.a < 0.99 -> RrLook.Glass".
- Hull shader counts per act: maker-pen 18 (Armory) / 37 / 36 / 121 / 56 / 84 / 163 / 62 / 114 / 83 (act9); install
  AG/Standard-Opaque 4-19 per act; AG/Standard-Transparent 1 each (the door trigger pad); URP Lit q2000 0-6
  (RrLook.Emissive: SciFi_Emit_*_Mat strips); AG/Unlit-Color: Unlit_Black_mat (Foyer, Armory, act5).
- No "Rr Floors" objects: RrSceneKitTerrain built 0 grids in every Jumbotron room (build log "floors 0 grids"); all
  floors are hull plates (maker-pen, grid-lit).
- Build summaries: Foyer kit 142 renderers (merged), hull 1076 plates/14 meshes, 52 dress, 1588 left out (area fog);
  Armory 386 plates/10; Battlefield1 3203/11, 78 dress; Hallway1 3302/11; Hallway2 4989/15, 172 dress; Reception
  2500/19; StadiumConcession 12383/20; StadiumEntry 5034/18, 204 dress; Garage 1866/11; Cargo1 6081/29, 178 dress;
  Stadium 12806/22, 206 dress.

## Level textures vs the install (SP/rrtool/idx/objects.tsv, Texture2D names)
- In the install: Concrete_Tex, Paint_Tex, Tile_Linoleum_Flecked_Tex (used for Linoleum_Flecked_Tex), Metal_Tex,
  Metal_Corrugated_Tex, "Wood FloorTiled_tex" (WITH A SPACE).
- NOT in the install: Brick_Tex (Cinderblock_VertColor_Mat + Brick_Painted_*: most walls -> flat colour),
  Wallpaper_SciFiPanels_Tex (flat), Pillar_SciFi_tex, Carpet_Blacklight_Swirls_Tex (only its _Norm), Terminal_SciFi_Prop_Tex.
- BUG: the rrsk writes tex=Wood%20FloorTiled_tex (URL-encoded); MaterialFor looks that name up verbatim -> the Foyer
  stage floor (Wood_Floor_Wide_mat, 402 m2) gets no wood texture although the install has "Wood FloorTiled_tex".

## Foyer lights (rrsk L records): all 15 are saturated neon, none white
- fill point (33.7,-1.7,54.6) colour (0,0.60,1) x0.58 range 26.2 m (the only light over the audience);
  magenta key spot (32.0,0.9,51.0) (1,0.17,0.98) x2.98 r 45.8 43.6 deg (shadows); magenta stage floor light
  (15.9,-6.6,54.5) x3.62 r 14.4; red (3.7,-1.5,54.6) x3.62 r 5; cyan points x0.6-1.9 r 2.3-7.3; two x21.1 r 0.75 and
  one pink x13.2 r 1.36 (tiny fixture glows). So the level's own light is blue/magenta; with the violet equator ambient
  and a teal fog there is no neutral light anywhere in the Foyer data.
- CORRECTION: tex=Wood%20FloorTiled_tex is NOT a bug - RrSceneData decodes %XX in marker values, and
  audit2_foyer_boxes_envoff shows the stage's wood planks.

## RUN 2 results (jt10_audit2.json, exit 0; copies NSP/jt10/audit/run2/, shots DN/iter10/screens/audit2_*.png)
Mean luma, environment OFF (world default white trilight + sun, no fog, grid on) vs ON (run 1, same camera):
  Foyer arrival 108 vs 47 (wide 120 vs 34, boxes 123 vs 34, back 116 vs 31); Armory 174 vs 32 (wide 169 vs 25);
  act1 170 vs 44; act2 116 vs 40; act3 154 vs 17; act4 111 vs 29; act5 155 vs 30; act6 137 vs 25; act7 103 vs 31;
  act8 178 vs 40; act9 139 vs 31 (wide 136 vs 28). => the Jumbotron environments cut the picture to 1/3 - 1/9.
Own environment after TestFightSpawn walked the player inside (same arrival camera):
  act1 -> Jumbotron_Battlefield1 (ambient sky 0.07/0.06/0.09): luma 42 (Foyer env 44): muted purple walls, navy floor.
  act2 -> player at (105.8, 4.3, 249.9) on an upper level, env STILL Jumbotron_Foyer (head y 5.9 above the registered
         top 5.6): luma 40, identical violet.
  act5 -> Jumbotron_StadiumConcession: luma 36: dark teal-navy, floor black.
  act8 -> Jumbotron_Cargo1 (sky 0.01/0.03/0.08): luma 17: almost black.
  So fixing the scene bounds turns the violet into each room's own (darker) look - it does not by itself fix the rooms.
Golden Trophy lobby environment forced on the Jumbotron Foyer (UseGoldenLobby): luma 34, a dark red-brown murk - GT's
  look came from its warm torches + grid, not from its environment numbers; copying the env does not transfer.
Grid probe (Rec Room's formula, up-facing, linear) in the Foyer: stage (20,-6.5,54.5) 0.84/0.06/0.93 magenta (5 lights),
  audience (30,-6.5,54.5) 0.00/0.14/0.44 blue (2 lights), mid-air (22,-4,50.5) 0.41/0.09/0.71, (22,-4,58.5) 0/0.08/0.34.
  -> the audience floor receives only the blue fill; red channel zero.
PairJumboNoInstancing: 37 instanced grid-lit slots; luma 35.0 -> 35.8 as in iteration 9 (instancing irrelevant).

Run-2 screenshot descriptions:
- audit2_foyer_arrival_gtenv (34): Foyer under the GT lobby env: brown-red darkness, crates dark maroon, stage faint pink.
- audit2_foyer_wide_envoff (120): the full theatre readable: blue walls, beige ceiling beams under a BLACK ceiling
  (Unlit_Black_mat, by design - under the JT env the fog turns it teal), white crates, linoleum floor, pink stage.
- audit2_foyer_boxes_envoff (123): the stage: wood plank floor (texture OK), pink pillars and curtains, cream drapes;
  the 12 cover walls are milky see-through boxes standing on the stage.
- audit2_foyer_back_envoff (116): from the stage to the entrance: pink pillars, grey-pink drapes, see-through boxes,
  blue walls, beige beams, black ceiling.
- audit2_armory_arrival_envoff (174): a green dressing room: bright green cinderblock walls (c=0.031,0.447,0.075), cream
  walls, a CREAM/WHITE carpet floor (Carpet_Blacklight_Swirls_mat: its texture is not in the install -> col 1,1,1),
  weapons on the wall, lit exit door, red sign, locker, stools, a grey bench/counter plane at right (the black wedge).
- audit2_armory_wide_envoff (169): same, wider; the ceiling a grey-teal gradient (Wall_Vertcolor ceiling at 4.3 m).
- audit2_act1_arrival_envoff (170): cream corridor walls/ceiling, flecked linoleum, the port frame, pink pillars inside.
- audit2_act1_arrival_ownenv (42): same corridor in Battlefield1's env: dull purple walls, navy floor.
- audit2_act2_arrival_envoff (116): lockers (blue), beige wall, pale-blue window wall, linoleum floor, dark ceiling.
- audit2_act2_arrival_ownenv (40): identical to run 1's violet (env still Foyer: head above the registered box).
- audit2_act3_arrival_envoff (154): cream walls, linoleum, dark navy ceiling, lockers; the "lasers" are wires/cables
  from two fixtures; shadows on the floor.
- audit2_act4_arrival_envoff (111): beige corridor, navy-black ceiling (Metal_Dull_Dark_Mat col 0.01,0.03,0.08),
  linoleum, pink pillars and grey curtain ahead.
- audit2_act5_arrival_envoff (155): cyan door blocks with pink tops, cream walls and columns, linoleum, black ceiling.
- audit2_act5_arrival_ownenv (36): the same in StadiumConcession's env: teal-navy blocks, black floor, purple haze.
- audit2_act6_arrival_envoff (137): Space: teal walls, cream floor, crates, barrier, wires, pale sky band.
- audit2_act7_arrival_envoff (103): hangar: grey concrete floor with white markings, beige walls, blue corrugated wall,
  truss, green barrels.
- audit2_act8_arrival_envoff (178): Supply: cream walls, light floor, beams, blue glass wall, dumpster, pink/cyan posts.
- audit2_act8_arrival_ownenv (17): Cargo1's env: near black, only the fogged far end readable.
- audit2_act9_arrival_envoff (139) / _wide (136): the arena beyond two big milky see-through cover-wall boxes right at
  the arrival; orange seats, pink/cyan stand-ins, grey roof.
- audit2_jt_foyer_noinst_before/after (35.0/35.8): iteration 9's pair, unchanged.

# FINAL

## 2. Per-area table (run 1 env ON / run 2 env OFF; grid = lights, cells lit of 6912; census = hull shaders)
| area (scene key) | env at arrival | luma on / off | grid | dominant surfaces (plate m2 -> runtime material) |
| --- | --- | --- | --- | --- |
| Foyer (Jumbotron_Foyer) | Foyer | 47 / 108 | 15 l, 6624 | Unlit_Black ceiling 3374 (install AG/Unlit-Color); Cinderblock_VertColor c=0.196,0.482,0.69 walls 1210 (maker-pen, no Brick_Tex); Linoleum floor 999 (maker-pen + Tile_Linoleum tex); Paint_DarkGrey c=0.133 931 (maker-pen, albedo 0.044); Wood_Floor stage 402 (wood tex); SciFiPanels c=0.667 372 (no tex); 18 glass dress |
| Armory (Jumbotron_Armory) | Armory | 32 / 174 | 10, 6912 | Unlit_Black 457; Cinderblock c=0.031,0.447,0.075 (green) 205; Carpet_Blacklight 138 (no tex -> white); Wall_Vertcolor c=0.435 114 (ceiling, Concrete_Tex) |
| act1 Battlefield1 | FOYER (bounds) | 44 / 170 | 18, 5485 | Brick_Painted_Grey 1975 (no tex); Linoleum 1810; Paint_DarkGrey 1441 (install); SciFiPanels 1004; 46 glass |
| act2 Hallway1 | FOYER (bounds; also inside, on the upper level) | 40 / 116 | 32, 5415 | Wall_Vertcolor c=0.059 2594 (black); Cinderblock c=0.667 1371; Linoleum 995; Wall_Vertcolor c=0.435 981; 25 glass |
| act3 Hallway2 | Hallway2 | 17 / 154 | 34, 3781 | Wall_Vertcolor 4149 + c=0.133 3509; Cinderblock 2588; Linoleum 1840; Curtain_Black 425; 50 glass |
| act4 Reception | Reception | 29 / 111 | 30, 6660 | Wall_Vertcolor 4406 + c=0.133 1283; Cinderblock c=0.667 1839; Linoleum 1737; Metal_Dull_Dark ceiling; 38 glass |
| act5 StadiumConcession | FOYER (bounds) | 30 / 155 | 23, 4866 | Wall_Vertcolor c=0.133 6672; Cinderblock c=0.667 1633; Linoleum 1486; 9 glass |
| act6 StadiumEntry | StadiumEntry | 25 / 137 | 46, 1705 (cells 5.3x7.4x7.5 m) | Cinderblock c=0.196,0.482,0.69 9399; Concrete_LightGrey_2x 8976 (install); Paint_DarkGrey c=blue 3684; Metal_Corrugated_Blue 2978; 36 glass |
| act7 Garage | Garage | 31 / 103 | 33, 2455 | Paint_DarkGrey 4770 (install); Concrete_Grey_3x 2708 (Concrete_Tex); 25 glass |
| act8 Cargo1 | FOYER (bounds) | 40 / 178 | 66, 4340 | Brick_Painted_LightGrey 2599 (no tex); Metal_Dull_Dark 1856 (install); Concrete_VertColor ~2200; 79 glass |
| act9 Stadium | Stadium | 31 / 139 | 74, 4491 | Wood_Table 1736 + Metal_LightGrey_Dull 1566 (install); Cinderblock c=0.435 1368; Linoleum 2035; 133 glass (109 cover walls) |

The grid block reaches everything it should: in every area each renderer with a grid shader holds the block (in
RrLightTest.Report, "lit" equals the grid-shader count, minus 3-6 enemies that had only just spawned).

## 3. Findings
### (a) flat violet walls
- WHAT: the Foyer walls are hull plates of Cinderblock_VertColor_Mat tinted c=0.196,0.482,0.69, a LIGHT BLUE covering
  1,210 m2. They use an AG/MakerPenShape copy, so they are grid-lit, not emissive and not URP. Next to them are
  Wallpaper_SciFiPanels_Big_Mat c=0.667, the pink pillars Pillar_SciFi_Pink_mat (1,0.39,0.95) and the install
  curtains. Under neutral light they show as light blue, grey and pink (shots audit_foyer_arrival_envoff and
  audit2_foyer_*_envoff).
- WHY VIOLET: the Jumbotron_Foyer environment.
  - The trilight EQUATOR is 0.31/0.08/1.08 (blue above 1), so every vertical surface is lit violet.
  - The level's 15 lights are all neon: a blue fill (0,0.6,1) x0.58 with range 26, a magenta key spot and cyan
    points. There is no white light at all.
  - Teal fog runs from 5 to 45 m.
  - Proof: with the environment off, luma goes 47 -> 108 and the violet is gone. With the grid off, luma goes 47 -> 46.
- WHY FLAT:
  - The textures of the wall materials are not in the install (Brick_Tex, Wallpaper_SciFiPanels_Tex, Pillar_SciFi_tex),
    so each wall is one colour.
  - Nearly all the light is ambient, and ambient is uniform, because the grid's lights barely reach the walls and the
    audience.
  - Rec Room surfaces get no additional-light shadows.
- Acts 1, 2, 5 and 8 show the same violet because at their arrivals they fall back to the Foyer environment (see d1).
- light9's "emissive/URP hull" is wrong. The hull is maker-pen. The only URP-shaded hull pieces are the glass dress
  boxes and 0-6 SciFi_Emit strips per act (RrLook.Emissive).
### (b) see-through pink boxes in the Foyer
- These are 12 G dress boxes on the stage:
  - 8 x Quest_SciFi_CoverWall_End (1.0 x 1.31 x 0.44 m) and 4 x Quest_SciFi_CoverWall_Mid (1.0 x 1.01 x 0.33 m).
  - Their material is level-only (?Wall_SciFi_Cover_Modular_mat) with c=1,1,1,0.354.
  - RrJumbotronHull.Build takes `x.Col.a < 0.99 ? RrLook.Glass(x.Col)`, which gives "Rr Glass FFFFFF": URP Lit
    transparent, alpha 0.354, smoothness 0.9, q3000, no shadows, NOT grid-lit.
  - They look pink because the only light on them is the Unity twins of the magenta stage lights plus the violet ambient.
- The same path catches wall_LOD0 x2 and bits_LOD0 x2 (?Wall_SciFi_Decor_Blue_mat, Rr Glass 1DADF5) and
  SciFi_WallVent_LOD0 (FFFFFF). Clock_Housing (alpha 0.08) really is glass.
- 0.354 is the _Color alpha of OPAQUE Rec Room materials (Door_SciFi_Cardboard_mat and Speaker_Intercom_mat carry it
  too). It does not mean transparency.
- Across the quest there are 459 glass dress boxes (Foyer 18 + acts 1-9). The cover walls are see-through in every combat room: act 1 has 33,
  act 3 28, act 8 45 and act 9 109. Two of them frame the view at the boss-arena arrival.
### (c) near-black floors
- The floors are hull plates; RrSceneKitTerrain built no grids in any Jumbotron room.
  - Most are Linoleum_Flecked_Brown_Mat on a maker-pen copy with col 0.578/0.547/0.529 and the install's
    Tile_Linoleum_Flecked_Tex. Under neutral light they correctly show grey flecked tiles.
  - The Foyer stage is Wood_Floor_Wide_mat with its wood texture.
- They are near-black for two reasons:
  - An up-facing surface gets the trilight SKY term, which is black in every room: Foyer 0.015/0.044/0.125, Armory
    0.012/0.044/0.053, Hallway2 0.00/0.016/0.109, Garage 0/0.014/0.036, Cargo1 0.010/0.031/0.083, Stadium 0.084/0/0.101.
  - The grid's only light on the audience floor is the blue fill: the probe gives 0.00/0.14/0.44 (red 0) at
    (30,-6.5,54.5), against magenta 0.84/0.06/0.93 on the stage.
  - Fog then adds teal with distance.
- Armory floor: Carpet_Blacklight_Swirls_mat has lost its texture (the install only has its _Norm), so col is 1,1,1.
  It would be white under neutral light; under the environment it is teal-black.
### (d) other problems
1. ENV BOUNDS (new bug): acts 1, 2, 5 and 8 use the Foyer environment at their arrivals. RrEnvironment.SceneBuilt
   registers the bounds of the kit's placed records. The Jumbotron hull is not in them, and the arrivals sit 0.4-5 m
   outside:
   - Battlefield1: z 255-300, arrival at z 250.
   - Hallway1: x up to 124.6, arrival at x 125; also the top is y 5.6, but on the upper level the head is at y 5.9.
   - StadiumConcession: x up to -126.6, arrival at x -125.
   - Cargo1: x from -371.7, arrival at x -375.
   Pick then falls back to the activity default, Jumbotron_Foyer.
2. Tinted INSTALL materials lose the install material. Paint_DarkGrey_Mat is inst=1, but its plates carry c=:
   c=0.133 on 931 m2 in the Foyer and c=0.196,0.482,0.69 on 3,684 m2 in StadiumEntry. The "bar >= 0" branch builds a
   maker-pen copy with col 0.33 x 0.133 = 0.044 (near black) instead of using the install AG material.
3. Every JT environment is very dark: ambient sky at most 0.13, and fog that starts at 0-7 m (Armory 2.9 m teal,
   Hallway2 0 m, StadiumEntry 0 m).
   - Luma against neutral light: Armory 32 vs 174, Engineering 17 vs 154, Cargo1 (its own environment) 17 vs 178.
   - The table rows say lightmapped=0 and lightprobegroups=1 for every JT level. Rec Room lit these rooms with baked
     LIGHT PROBES, which we do not reproduce (open question).
4. The Unlit_Black_mat ceilings (Foyer 3,374 m2, Armory, act 5) are black by design; the fog turns them teal, which
   reads as a "teal sky".
5. Level textures missing from the install leave flat colours:
   - Missing: Brick_Tex (Cinderblock_VertColor and Brick_Painted_*, which are most walls), Wallpaper_SciFiPanels_Tex,
     Pillar_SciFi_tex, Carpet_Blacklight_Swirls_Tex, Terminal_SciFi_Prop_Tex.
   - Present: Concrete_Tex, Paint_Tex, Tile_Linoleum_Flecked_Tex, "Wood FloorTiled_tex", Metal_Tex, Metal_Corrugated_Tex.
     Brick_Grout_tex, Brick_Norm and Brick_Spec are possible brick substitutes.
6. Some walls are near-black because of the data itself: Wall_Vertcolor c=0.059 in Hallway1 (2,594 m2) and
   Wall_Vertcolor c=0.133 in Hallway2, Reception and StadiumConcession (1,283-6,672 m2). These are the level's own
   vertex colours.
7. Not found: z-fighting, missing ceilings (every arrival view has one, of Unlit_Black, Metal_Dull_Dark or
   Wall_Vertcolor), and stand-ins in the way other than the glass cover walls. The Armory's black wedge (lower right) is
   a grey bench or counter plane that the environment leaves unlit.
8. The grid works. It holds 10-74 lights per room and lights install props well (the Armory locker and stools; the red
   pool on act 6's floor). StadiumEntry's grid is coarse (5.3 x 7.4 x 7.5 m cells, 1,705 of 6,912 lit) because its
   level box is 504 x 110 x 404 m.

## 5. Summary / recommendations for the fix phase
- The Jumbotron's look is decided by its ENVIRONMENT, not by the grid.
  - With Rec Room's per-scene ambient and fog the rooms are 1/3 to 1/9 as bright as under neutral light, and tinted
    violet, teal or navy. The grid's neon lights reach little.
  - Forcing the Golden Trophy's environment does not help (luma 34, red-brown).
  - So "do the same" means three things: correct scene bounds; a brighter, less saturated ambient per JT room (above
    all the up-facing term), ideally derived from the levels' light probes (APK data, measured numbers only); and the
    GT-style material plan (closest install AG material plus texture).
- R1: register the Jumbotron scenes with the rrsk B box (or the hull bounds), or force the act's environment key while
  the act is current.
- R2: dress boxes become Glass only when they really are glass (the name contains Glass/Window, or the level shader is
  transparent). Make the cover walls, wall_LOD0, bits_LOD0 and WallVent opaque Solid in their rgb, or use the closest
  install cover material.
- R3: tinted plates of inst=1 materials keep the install material; apply the tint through _Color only if the shader
  uses it.
- R4: substitute install textures for the missing level textures (brick, sci-fi panels, pillar, carpet).
