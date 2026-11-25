# The Rise of Jumbotron - what it really looks like (reference research, jt10)

Agent: reference (jt10). Read-only research; no code edited. Sources are web footage/screens/wiki + local reference data.
Status: DONE (sections 0-6 = synthesis; the LOG at the bottom has every measurement with its source/timestamp).
Colour values "#rrggbb" are sRGB averages of 1/6 x 1/6 screen cells measured on the real frames (canvas grab of the
playing video / the wiki image; nothing downloaded or stored). Luma = mean Rec.601 luma of the frame (0-255).
Our pictures measured the same way (LOG, last entry): light9_jt_foyer_after luma 40 (iter9's own metric 35.8),
jt_foyer_console 58, jt_act1_arrival 51 (flat violet #471eca), jt_armory_spawn 25 (#002029 teal), jt_boss_arrival 16
(#150130). NOTE: the LOG entries "10:45 SPACE" and "13:10 HANGAR" were mislabelled and are corrected in the LOG's
"CORRECTION" entry (they are the Hangar and Supply); section 2 already uses the corrected mapping.

## 0. Sources used
- Rec Room fandom wiki: The_Rise_of_Jumbotron (Steam screenshots dated 2017-04-25: 20170425030203_1 dressing room,
  030306_1 / 030320_1 dressing-room consoles, 030507_1 ship console room, 030601_1 credits on a purple floor,
  154422_1 Supply Easter egg; Jumbotron78.png boss render; Key Art 2560x1440) and The_Rise_of_Jumbotron/Image_Gallery
  (22 concept sheets with the artist's captions).
- YouTube (watched in the browser pane, frames grabbed by seek, never downloaded):
  - EMn5_Cgd7E8 "Rec Room Rise of Jumbotron quest gameplay (Stages 1-4)" (Mira Lee Kindlewood, ~2025, MOBILE = the
    Android levels we converted): 0:40 dressing room, 0:45-1:30 act 1, 1:40-2:05 act 2, 2:30 door, 2:40-4:20 act 3,
    5:00-6:20 act 4.
  - 9mEGOn2XI5g "(Stage 8 & Boss Level)" (same uploader, ~2025, mobile): 0:10-2:50 Supply, 3:00 boss door, 3:25-4:10
    Stadium, 4:35 victory.
  - 0hH3OvhnbrU "Rec Room - GAMEPLAY - The Rise of Jumbotron" (SinoWick Archive, 2022, PC 1080p, full run): 0:00 Foyer
    at GAME ON, 0:15-1:45 dressing room, 2:30-4:00 acts 1-3, 5:30 Bridge, 7:00 Flight Deck door, 7:20-9:10 Flight
    Deck, 9:10-10:20 Space, 10:45-13:00 Hangar, 13:00 SUPPLY door, 13:05-16:20 Supply, 16:30-17:30 Stadium.
  - C9kjCNcZtFQ "Boethiah teaches Freddy How to Play JumboTron" (2022-06, PC): 0:50-2:05 the Foyer pre-game (both ends).
  - bElxbXGLh7s (Quest 2, 2021): 0:05-1:40 acts 1-2 (same palette).
- Local, read only: DN/refdata/apk/Jumbotron_*.rrsk (L lights, E fog/ambient, G platemat colours), RecRoomAssets
  (.unity files/APPPLESARCHIVE/APPPLESARCHIVE-JumbotronBoss-PostProcessingProfile.asset; 2D Textures names), rrtool idx
  objects.tsv (what the PC install has), DN/research/rr_quests.md + rr_latest_rgames_story.md (act content),
  DN/iter10/jt_lighting.md + jt_materials.md (sibling agents' technical tables, cross-checked only).
- The quest was never re-art-directed: the 2017 wiki shots, the 2021-2022 PC/Quest videos and the 2025 mobile videos
  show the same rooms, props and light colours (PC is somewhat more saturated than mobile).

## 1. Overall look / art direction
- Art direction (concept captions): a laser-tag ride built by the theatre department under a stadium - "neon laser
  tag 80s style", "glow in the dark tape and cardboard", robots from old computer parts. So: DARK rooms, simple
  pale/grey surfaces (cinderblock, painted steel, cardboard, linoleum, black curtains), painted by SATURATED COLOURED
  STAGE LIGHT, plus lots of small GLOWING trims.
- The colour is in the LIGHT, not the albedo. Surfaces are grey/white/pale-blue (Cinderblock vertex colour 0.20,0.48,
  0.69 in the Foyer, 0.44/0.67 grey elsewhere, green 0.03,0.45,0.08 only in the dressing room); each room has 1-3 big
  coloured key lights (spots of 15-60 m range) - magenta, cyan, red, violet, green, orange - over a dark coloured fill.
  Result: strong two-tone contrast inside one view (hot magenta faces vs deep navy shadows; teal vs black), smooth
  gradients across walls, bright pools on floors. Shadows are deep NAVY / PLUM (#011039, #1d0b27, #2d1635), not black.
- Brightness: Foyer pre-game and the between-act door vestibules are BRIGHT (luma 45-108, cool blue); the act rooms
  are dark-to-mid (luma 20-70) with strong highlights; the boss arena is dark (22-54) with beams and a glowing screen.
- Emissive trims everywhere (and they BLOOM): cyan strips on barriers/cover walls/steps, cyan Rec Room logos on black
  crates, pink/red light slots on the octagonal door frames, red neon room signs (ARMORY, SERVERS, BRIDGE, FLIGHT DECK,
  SUPPLY, JUMBOTRON, EXIT), yellow credit monitors, console screens, hanging cyan glow sticks, white ball lamps,
  white cross-shaped sparkle lamps (Stadium), red laser beams (Engineering, Space).
- Air: each room has a coloured linear fog (mostly teal-blue 0,0.40,0.60 from 5 to 45 m) plus soft local haze
  (FogSin mist layers, PortalFog glow curtains/sheets) and VISIBLE LIGHT CONES (light shafts under spots: magenta on
  the Foyer marquee, blue cones in the Stadium, white cones in doorways). All soft and additive - never a solid box.
- Post (PC): Bloom threshold 1.04 / intensity 0.15 / scatter 0.6, Neutral tonemapping, exposure +0.1, contrast +10,
  white balance +5, lift slightly blue (captured profile, section 4). Downed = full-screen greyscale.

## 2. Per area (what a player sees; sources in the LOG)
### 2.1 Foyer (lobby / theatre / briefing) - Jumbotron_Foyer
- Pre-game (bright, luma 45-60): the lobby end is an AZURE-BLUE cinderblock room - walls #0130b7..#0572ce (lit
  #0872c8), navy floor #001e5b..#001c4e, dark navy ceiling with ~20 short hanging CYAN GLOW STICKS (more lying on the
  floor as cyan dashes), teal double doors with a small "Rec Center" sign and a red EXIT sign, black weapon crates with
  cyan logos and pistols on top, a wall TV, round wall speakers, light blue haze. No violet, no pink here.
- The theatre end: rows of black folding chairs face a raised stage; above it the NEON MARQUEE "THE RISE OF JUMBOTRON /
  A REC ROOM QUEST" (pink outline, cyan/white letters) - the brightest object; the stage lit LAVENDER-PINK (#926bb6,
  #685fb1) with a fan of light rays behind it and a pink haze curtain in front (Portal_Pink_Foyer); pink pillars
  (Pillar_SciFi_Pink) reading lavender in the blue light; low opaque cover-wall blocks in front of the stage with cyan
  logo emissives; black scaffolding trusses; a low cyan mist over the floor.
- At "GAME ON!" the house lights go down: navy (luma 16), the magenta spot cone on the marquee and the cyan bits stay.
### 2.2 Armory (dressing room / gear up) - Jumbotron_Armory
- GREEN: green-painted cinderblock (#0c4522 in shade), lime lockers, a strong green light pool from the upper right
  (#3f9663; level spot 0.54,1,0.70), mean luma 50-58. The floor is a UV BLACKLIGHT CARPET: navy base with glowing royal
  blue spirals, lime dots/spirals and magenta dotted arcs (looks self-lit). Left wall: makeup mirror ringed with bright
  CYAN bulbs (Emit_Armory_Mirror 0.29,0.87,1); back wall: exit door with red indicator lights under a lit blue "ARMORY"
  sign, green-lit "RELOAD!" posters, crates with white logos, a light-grey console with red/orange/green buttons and a
  cyan clock, hanging cone lamps, open black steel truss ceiling; weapon racks (4 pistols + 4 SMGs).
### 2.3 Act 1 SS Coach (Battlefield1)
- You step through an octagonal PORT (dark steel, cyan emissive corner slots; the near side is near-black purple
  #1d0b27) into a HOT-MAGENTA hall: walls/ceiling #a00a96..#c60faf, lavender pillars #8461aa / #755e98, low barriers
  with CYAN strips, black crates with cyan logos, yellow credit monitors, a red hazard panel, a big window wall into a
  BLUE-lit area (cyan haze) behind; glossy dark plum floor #5b0d3e..#6c0d49. Luma ~60.
### 2.4 Act 2 Cargo Bay (Hallway1)
- CRIMSON/RED + magenta: cinderblock washed deep red (#a8183d / #9a1238), black steel truss towers, violet-grey back
  walls (#5a3767), container/crate stacks with big cyan logos, pink-red floor pools (#942542), magenta haze, a white
  spot in the distance. Luma 48-56. Level: red spots (1,0.03,0.16) R49, blue points, Pillar_SciFi_Red.
### 2.5 Act 3 Engineering (Hallway2) - "SERVERS"
- DARK VIOLET/INDIGO (walls #3f1a4f..#4e1b42, blue patches #151640, floor #1f0d30..#441131; luma 25-37) with black
  theatre curtains, server racks and consoles with glowing screens, THIN BRIGHT RED LASER BEAMS, a red "SERVERS" sign,
  magenta pads; one HOT-PINK cinderblock room (luma 69, #b82b7b..#c12474) with a white spot cone from a doorway; a
  red-magenta corridor with sagging black cable bundles (#481440..#7a1b51).
### 2.6 Act 4 Bridge (Reception)
- TEAL/CYAN throughout (luma ~52): walls #09314d..#134f76 lit #2798aa / #327085, dark navy ceiling #061f31, floor
  #082636..#194b6a; a reception desk under a cyan Rec Room logo sign, grey arcade-style consoles with screens, studio
  lamps on stands, steps with cyan edge strips, a big cyan-framed glass door, red "BRIDGE" neon sign at the entry.
### 2.7 Act 5 Flight Deck (StadiumConcession)
- Door vestibule cyan/teal (#33b9d9 / #5dd4ea) with a red "FLIGHT DECK" sign. The deck is a DEEP SATURATED BLUE void
  (#0029b4, #01105f) with violet patches (#6e2aa0), short CYAN light streaks floating in the air, ORANGE-red strips and
  ramps (#905842 / #a04634; orange points 1,0.31,0), glowing blue/pink sheets below the open edges; the parked ship is
  dark steel with a green "Blast Off!" button ringed in yellow emissive, windows onto black space. Luma ~35.
### 2.8 Act 6 Space walk (StadiumEntry)
- VIOLET/PURPLE + DEEP BLUE (luma ~42): violet hot spots #8215a9..#9818b9, deep blue-violet walls #0d0661..#190ba1,
  purple floor #3b0c84..#55118b, a RED LASER GRID criss-crossing a doorway, round white ball lamps, magenta door frames,
  cyan floor light lines, dark blue side rooms with cyan pools; distance lost in navy fog (0..354 m).
### 2.9 Act 7 Hangar (Garage)
- DARK NAVY + CYAN (luma ~41): air #002b63..#00326b, big cyan light pools #1f82b5 / #008585, Jersey barriers lit cyan
  with green/black hazard stripes, orange/black hazard-striped pillars, green barrels, painted floor arrows and parking
  lines, studio lamps on tripods, crates with cyan logos; ends at a red "SUPPLY" door (cyan room, roll-up shutter).
### 2.10 Act 8 Supply (Cargo1)
- HOT MAGENTA vs NAVY (luma ~68 where lit): pale surfaces hit by the magenta spot go #e61fad..#f824b1, mid #903d94,
  shadows #011039 / #0a1a4b; pink shipping containers stamped "REC ROOM", orange/black hazard barriers, a steel
  catwalk, tall black warehouse shelving, cyan pools on walls, blue windows; the candle-lit ORANGE Easter-egg side room
  (walls #511611 where lit, goblin "Kevin" and a pot, black cobweb drapes, luma 27).
### 2.11 Act 9 Stadium / Jumbotron boss
- Pale-blue boss vestibule (#66a2e7, luma 84) with a red "JUMBOTRON" sign and pink door strips. The arena is the old
  gym re-dressed: arched gridded windows, red Rec Room banners, dark red-brown wood floor (#30051b in the dark; green
  turf on the raised cover platforms), dark indigo air (#110431..#1e0640), many BLUE volumetric cones from the roof
  (#124e8b, #1673bf), a big CYAN-BLUE glow under the Jumbotron (#32b5fc), white cross sparkle lamps, red steel trusses,
  black cable bundles, red/orange stage beams, dark cover blocks with magenta strips. The JUMBOTRON: a 4-sided
  scoreboard cube, each face a hot-pink scanline screen (#f95dbc / #fe82cf) with white "> <" eyes, a cyan LED ticker
  "JUMBOTRON SEZ: GET REC'T", red "!!!" LED band on top, a grey twin-barrel turret below. Luma 22-62. On victory the
  arena lights come up (luma 99, warm orange floor light).

## 3. Our current pictures vs the real thing
- light9_jt_foyer_after.png (Foyer, luma 40): (1) hue - ours is saturated BLUE-VIOLET (#3e1ebb / #5431b5 pillars/walls);
  the real lobby end is pure AZURE blue (#0130b7..#0572ce: red channel ~0) and only the stage end is lavender-pink;
  (2) ours is FLAT - one colour per surface, no pools, no gradients; the real room has a lit centre, darker corners,
  a haze, and the stage glow; (3) the see-through PINK boxes are not in the real room: they are (most likely) the
  cover walls (CoverWall_End/_Mid 1 x 1 x 0.33 m, Cover_Glass 1 x 2.5 m panes) whose material colour carries alpha
  0.354 - in Rec Room they are OPAQUE grey steel lit blue/lavender with cyan logo emissives (only the glass pane is
  clear); (4) floor: ours near-black violet #11023b, real navy #001e5b (brighter, blue); (5) ceiling: ours flat
  teal-grey, real dark navy with hanging cyan glow sticks; (6) missing: the neon marquee glow, the magenta spot cone,
  the pink stage haze, the glow sticks, the lit EXIT/Rec Center signs.
- jt_foyer_console.png: the console card is right; the room around is violet where it should be azure blue, and the
  chairs/stage read flat.
- jt_act1_arrival.png: ours = a bright flat VIOLET tunnel (#471eca on every wall cell, luma 51) with a navy floor; real = the port approach is
  near-black purple and the hall beyond is HOT MAGENTA with lavender pillars, cyan barrier strips and blue windows.
  Ours has no magenta key light and no contrast.
- jt_armory_spawn.png (dressing room, luma 25, #002029..#003634): ours is almost black teal with only the cyan octagon and yellow credit
  signs; real = luma 50-58, green cinderblock with a green light pool, the glowing blacklight carpet, cyan mirror
  bulbs, the lit ARMORY sign, posters. Missing: the carpet pattern (level-only texture), the green key light's
  strength, the mirror emissive, the sign.
- jt_boss_arrival.png (Stadium, luma 16, #150130 - the indigo AIR hue is right): ours near-black purple with one pink skylight strip and an orange cone;
  real = indigo air full of BLUE light cones, a cyan-blue glow under the pink-screened Jumbotron, white sparkles, red
  trusses, red-brown floor. The Jumbotron itself should be the brightest object (hot-pink emissive faces).
- In general ours: albedo too saturated (violet paint instead of grey surfaces under coloured light), lighting too
  uniform, emissives too weak (no bloom-level glow), haze drawn as solid boxes or absent, light shafts absent (the
  sibling notes find them rendered as opaque grey cones), too dark in the dressing room / boss, not blue enough in the
  Foyer.

## 4. Local reference data (read-only)
- APK levels (DN/refdata/apk/Jumbotron_*.rrsk): the L records reproduce every key colour seen in the footage (table in
  the LOG): Foyer magenta spot (1,0.17,0.98) R45.8 + blue/cyan points; dressing room green spot (0.54,1,0.70); act 1
  magenta spot (1,0,0.68) + violet points (0.94,0,1) I13.7 + cyan spot R43.6; Cargo Bay red spots; Engineering
  magenta-red spots + violet points; Bridge cyan points/spots; Flight Deck cyan + orange; Space violet + blue spots R60 +
  orange; Hangar cyan only; Supply magenta spots/points + cyan; Stadium 18 white-blue sparkle points + cyan points.
  E fog: teal-blue linear 5..45 m in most rooms, navy in Engineering/Bridge/Space, deep blue (0,0.03,0.34) in the Stadium.
- APPPLESARCHIVE-JumbotronBoss-PostProcessingProfile.asset: Bloom 1.04/0.15/0.6, Neutral tonemap, exposure +0.1,
  contrast +10, WB temp +5, lift (0.92,0.94,1), gamma (1,0.98,0.96), gain (1,0.99,0.96,+0.034).
- Install stand-ins for the look (rrtool idx): Scifi_Rug_Mat, Glowstick_Cyan/Red/Purple/Orange_Mat, Light_Marquee_mat,
  Jumbotron_Marquee_Emissive_Stencil, StuntRunner_JumbotronMarquee_Mat/_Emissive, StuntRunner_Jumbotron_Mat/_Tex,
  Stagelight_Fog_Mat, LightShaft_Yellow_Mat, LightShaft_Flashlight_Cyan_Mat, DormRoom_LightShaft_Gold_Mat,
  StageLight_Can_On_Mat, Bucket_BlackLight_Swirl_Skin_Tex/_Emit (closest to the carpet; the carpet's own texture is
  level-only, only its _Norm is installed). Level-only (no install copy): Scifi_AreaFog_*, Portal_*, Pillar_SciFi_*,
  Wall_SciFi_Decor_Blue, StageTop, Concrete_PaleBlue_200x, Enemy_Jumbotron_Mat, Emit_Armory_Mirror_Mat.
- RecRoomAssets/2D Textures holds reference PNGs (Carpet_Blacklight_Swirls_Texture, Jumbotron_*, Glowstick_*,
  Door_Scifi_*) - reference only, never imported.

## 5. Key findings / what a player should see
1. Grey/pale surfaces + saturated coloured KEY LIGHTS (per room) + dark coloured fill; colour contrast inside a view.
2. Per-room palette: Foyer azure blue (stage lavender-pink, marquee pink/cyan); dressing room green + blacklight
   carpet; act 1 hot magenta + cyan trims + blue windows; Cargo Bay crimson red + magenta; Engineering dark violet +
   red lasers + one hot-pink room; Bridge teal/cyan; Flight Deck deep blue + orange strips; Space violet/deep blue +
   red laser grid; Hangar navy + cyan pools + green hazard stripes; Supply hot magenta vs navy; Stadium indigo + blue
   cones + pink Jumbotron.
3. Door vestibules between acts are bright cool blue/teal with a red neon room-name sign and pink door slots.
4. Emissives must glow (bloom): cyan strips/logos, pink door slots, red signs, glow sticks, sparkle lamps, screens.
5. Soft coloured haze + visible light cones; never solid or see-through coloured boxes.
6. Cover walls are opaque dark/grey steel blocks with cyan logos (only the glass panes are clear).
7. Brightness targets (frame luma): Foyer pre-game 45-60 (16 after GAME ON), vestibules 60-108, dressing room 50-58,
   act rooms 25-70, Stadium 22-62.

## 6. Open questions
- Whether the Foyer's "house lights down" at GAME ON is a scripted light change in the level (not verified in data).
- The exact floor of the Stadium arena (footage: dark red-brown wood; mobile shows green turf on the platforms).
- A striped orange/yellow "retro sun" disc seen in the Hangar/Space footage (0hH3OvhnbrU 12:40) - a prop or VFX? GUESS.
- The Jumbotron's own art is not in the install (StuntRunner_Jumbotron_* is the closest install asset - unverified).

## LOG (appended as found)
- Our current pictures (read): light9_jt_foyer_after.png = saturated violet/blue (#4a1ed0-ish) pillars and walls, all one flat
  colour, near-black violet floor (#140840), translucent PINK boxes (the area-fog / glass volumes drawn as solid boxes) on both
  sides, teal-grey ceiling. jt_act1_arrival.png = a violet tunnel (flat, no shading) with a dark navy floor, the port frame dark
  navy with cyan dots. jt_armory_spawn.png = almost black teal-green room, only the cyan hologram octagon and yellow credit
  signs visible. jt_boss_arrival.png = near-black purple, one pink skylight strip, an orange/yellow cone (the turret beam?).
  jt_foyer_console.png = blue/violet everything, the yellow console card is the only warm colour.
- Web sources found: fandom wiki The_Rise_of_Jumbotron + /Image_Gallery; YouTube XEF_A4qjY2Q (Full Play Through, 2017-04),
  EMn5_Cgd7E8 (stages 1-4), 9mEGOn2XI5g (stage 8 + boss), mpnFzqB7l4M, HrKGw_d-UEw; Steam guides 2008860357, 2797825064;
  Steam news 4041374038287321743 (release post).
- Concept art captions (wiki Image_Gallery, art by the RR artist, 22 sheets): the art direction is "laser tag glow in the
  dark tape and cardboard", "neon laser tag 80s style", robots "made from old computer parts the theater department found",
  "its meant to be a ride but then it all goes wrong", "under the stadium", "some kind of overheating core" (Engineering).
  => the quest is a school-theatre/laser-tag-arena SET of a space ship: dark rooms + GLOWING neon tape / blacklight carpet,
  not a flat-lit violet box.
- WIKI screenshot 20170425030203_1.jpg (1333x1260, Steam 2017-04-25, caption "Starting room") = the DRESSING ROOM (our
  "Armory" room, the one with the 4 pistols). Measured by canvas 6x6 average grid, mean luma 57.6 (ours jt_armory_spawn ~20):
    row0 (ceiling, dark steel truss) #263a5a #28394e #293b47 #2a3c42 #2a3c40 #293d3e   (dark blue-grey, not black)
    row1-2 (walls) #20445c #1f3b48 #283f40 #305445 #326f4f #36865c / #1c4c47 #275b53 #355e4c #416e57 #3f9663 #2a7849
           -> walls are dark TEAL/GREEN, washed by a strong GREEN light pool from the upper right (#3f9663 hot spot)
    row4-5 (floor) #131f4c #111e44 #16254c #1c3252 ... #0c1b4a #081033 -> deep navy/indigo BLACKLIGHT CARPET with bright
           glowing swirls: royal blue spirals, lime-green dots/spirals, magenta/violet dotted arcs (UV-reactive laser-tag
           carpet; averages dark navy but the pattern pops at full brightness = emissive-looking).
  Seen: left wall = makeup mirror with bright CYAN bulbs (emissive); back wall = door with red indicator lights, a lit blue
  "ARMORY" sign above it, a green-lit "RELOAD!" poster each side, crates with white Rec Room logo, hanging cone lamps
  (green), a console with red/orange/green buttons on the right. Ceiling = open black steel truss over a dark ceiling.
- WIKI 20170425030306_1.jpg + 030320_1.jpg (dressing room close-ups): the walls are GREEN-PAINTED CINDERBLOCK
  (#0c4522 .. #0d4423 in shade), lime-green lockers (#143e0f in shade, bright lime where lit), a light-grey metal console
  with emissive screens (orange/red/green buttons, a cyan "10:72" clock), the blacklight carpet (navy base, glowing
  lime/blue/magenta swirls - pattern is saturated and looks self-lit). Mean luma 49.5.
- WIKI 20170425030507_1.jpg (a ship room, probably act 1/2): walls = dark neutral blue-grey metal panels #1a2229 (shade) ..
  #283038 (lit), floor near-black flecked #101519, the light-grey console #313d48 in shade; screens are the only bright
  things (cyan map, orange numbers). Mean luma 38.4. => ship interiors are dark, low-saturation steel, NOT violet.
- WIKI 20170425030601_1.jpg (481x455, "credits on the ground"): a dark room lit by COLOURED LIGHT POOLS - a magenta pool on
  the floor at the left (#4b245e), teal/cyan light washing the back wall (#2c3c5c), the walls otherwise desaturated indigo
  #303153, the floor dark plum #2d1635..#2a1834 with fleck texture. Mean luma 44.7. Saturation is LOW (walls ~#30-#35 R/G,
  #50-#5c B); ours are #4a1ed0-class fully saturated violet.
- WIKI 20170425154422_1.jpg (Supply's Golden Trophy Easter-egg side room): WARM - orange/red cinderblock lit only by
  candles (orange point lights) - walls #511611..#56190f where lit, #250607 in the dark corners, floor #220705. Mean luma
  27.5 (dark, but coloured, never black). Black cobweb drapes over the doorway, a hanging goblin, a pot on a crate, a
  grey spaceship-style white/grey bench beyond. Magenta neon diamond markers on the left pillar.
- WIKI Jumbotron78.png (render of the boss, "as seen in game"): a 4-sided arena scoreboard cube, each face a HOT-PINK
  scanline screen (#fe82cf bright .. #ef5d96) with two white angry-eye slashes, thick dark bezel frames, a black top band
  with red LED "!!!" / "..." ticker, a cyan LED ticker belt (#5faaac) reading "JUMBOTRON SEZ: GET REC'T", a pole down to a
  light-grey (#a0a2a3) twin-barrel turret box. The screens are EMISSIVE (they light the arena pink).
- WIKI The_Rise_of_Jumbotron_-_Key_Art.png (2560x1440 marketing render, not gameplay): the octagonal sci-fi door frame
  (the quest door) in steel LIT by violet/magenta light: frame #40175b (top, shade) / #344575 (inner, blue-lit) / outer
  wall #742c8d (magenta-lit), pale cyan-white emissive light slots around the frame (#859cb4 mid-sample; core near white),
  yellow/black hazard stripes left, red/black right, a deep magenta-red room beyond (#600043), floor #561752. => the
  "purple" of Jumbotron is COLOURED LIGHT on grey steel (violet/magenta key + blue fill + cyan emissive slots), not a flat
  violet albedo.
- YT EMn5_Cgd7E8 "Rec Room Rise of Jumbotron quest gameplay (Stages 1-4)" (Mira Lee Kindlewood, ~2025, 6:25, MOBILE
  player = the same Android levels we converted; 1080p stream). Frames grabbed by seek + canvas (no download).
  - 0:40 dressing room: IDENTICAL to the 2017 wiki shot (green cinderblock, green light, blacklight carpet, ARMORY sign,
    RELOAD posters) -> the level was never re-art-directed; 2017 shots are valid for the final version.
  - 1:00 act 1 hall (after the dressing-room door; pillars, low barriers, crates): HOT MAGENTA. 6x6 grid, luma 60.0:
      top   #3b1945 #5f1a67 #680a6f #a00a96 #c60faf #b7349f   (walls/ceiling washed by saturated magenta light)
      mid   #873ea0 #5b3d85 #8461aa #755e98 #893675 (pillars/back windows lavender-violet, blue-lit area beyond windows)
      floor #670e48 #610f4a #4a1349 / #5b0d3e #6c0d49 #400831  (dark plum-magenta floor, glossy)
    Emissives: CYAN strips on the low barriers and crates (Rec Room logo in cyan on dark crates), yellow credit-symbol
    monitors, a red/pink hazard panel, big windows at the back into a BLUE-lit area. The magenta is LIGHT colour (the
    same grey steel reads lavender where the blue fill wins).
  - 0:52 the octagonal PORT frame (dark steel, cyan emissive corner slots #00ffff-ish) seen from the dark side, the
    magenta act-1 hall beyond (#b619a6 through the opening); the near side is near-black purple (#1d0b27, luma 38.6).
  - 1:15-1:30: "is Down" -> Rec Room DESATURATES the view when downed (grey-blue frames). Ignore those for colour.
  - 1:40 (#t=100) crimson/RED-lit room: cinderblock wall washed deep red (#a8183d / #9a1238 at left), black steel truss
    tower, violet-grey back wall (#5a3767), a white spot in the distance. Mean luma 56.
  - 2:05 (Cargo Bay-type container stacks): dark crates (#2b1328..#311934 in shade) each with a big CYAN emissive Rec
    Room logo, magenta/violet haze behind (#734972), red-lit crates in the distance, floor #772747..#942542 (pink-red
    light pool on the floor near the player). Luma 47.7.
  - 2:40 dark violet room (luma 25.8): #2a1932..#3f2055 walls, #10061c floor corners; cyan/magenta small emissives only.
  - 2:55 HOT-PINK cinderblock room (luma 68.8!): walls #b82b7b..#c12474, darker #5f1d53..#661f59 in shade, floor
    #300926..#5b1138, a WHITE spotlight cone (visible volumetric beam) from a raised doorway, a dark floor mat.
  - 3:10-3:20 ENGINEERING ("SERVERS" red sign over the door): dark indigo/purple (walls #3f1a4f..#4e1b42, blue patches
    #151640 / #1e264f, floor #1f0d30..#441131), RED LASER beams (bright red lines, thin, glowing), server racks with
    emissive screens, a magenta pad. Luma 37.
  - 3:50 (#t=230) computer room: dark navy-indigo (#101035..#1e1e41 top, #251b4f..#1d234c walls), consoles with
    emissive screens (cyan / white static), plum floor #2f082b..#390d35. Luma 31.
  - 2:30 (#t=150) BETWEEN-ACT DOOR (the sci-fi quest double door where the scoreboard card shows): BRIGHT (luma 108!):
    pale sky-blue steel door + frame (#7196b4 .. #75a1c0, lit side #247af5 .. #3f80e3 saturated blue), white-cyan
    window slits, red/grey hazard-stripe side panels, PINK emissive accent strips, a dark-red floor beyond. => door
    vestibules are well lit, cool blue, not dark.
  - 4:20 (#t=260) red-magenta corridor with BLACK CABLE BUNDLES sagging from the ceiling; walls #481440..#7a1b51,
    floor #290c2a..#400f32; luma 40.
  - 5:00-6:20 ACT 4 BRIDGE (red "BRIDGE" neon sign over the entry; "Reception" level = a reception hall with a front desk):
    TEAL / CYAN everywhere (grid luma 52.4): walls #09314d..#134f76, lit #2798aa / #327085 / #456f7f, ceiling dark navy
    #061f31..#102438, floor #082636..#194b6a. A cyan emissive Rec Room logo sign over the desk, grey arcade-style
    consoles with emissive screens, white/cyan lamp heads on stands (studio lights), low steps with cyan edge strips
    (6:20), a big cyan-framed glass door (6:05). Magenta/pink appears only as small accents (a pink tag light).
- YT 9mEGOn2XI5g "(Stage 8 & Boss Level)" (same mobile uploader, ~2025, 5:13; 854x370 stream).
  - 0:10 ACT 8 SUPPLY: violet/magenta room (luma 43.7): hot magenta light at the top centre (#a92c9e, #9a3099), a BLUE
    window/area at the upper left (#0f3a5e, #283f5b), lavender mid (#8a3c86 / #8b4195), floor near-black plum
    #110320..#2b1142; a black/yellow hazard-stripe barrier, pink neon window frames in the back.
  - 1:10 dark teal area with red bots; 2:10 dark blue-grey wall + a bright cyan light panel (emissive block, flat #00ffff-ish).
  - 2:40 (#t=160) magenta room with sagging cables, a red-lit shelf; walls #5a1a79..#6a217d, floor #22092d..#3c154d,
    luma 31.5.
  - 2:52 dark violet corridor with a red bot + cyan/violet strips (pre-boss).
  - 3:00 (#t=180) BOSS VESTIBULE: bright pale BLUE-GREY cinderblock walls (#6c9bd9 / #66a2e7 lit, #7187d1), a red
    "JUMBOTRON" neon sign above the sci-fi door, PINK emissive strips on the door frame, teal floor with a white-outlined
    teleport pad. Luma 84. (Like every between-act door: well lit, cool blue.)
  - 3:25-4:05 THE STADIUM (boss): DARK GREEN TURF floor (#0a1a09..#132111), a huge CYAN-BLUE glow/haze in the middle
    (#2499d9, #32b5fc! - the volumetric light under/around the Jumbotron), white-cyan sparkle particles drifting,
    magenta/violet haze higher up (#280f37..#435d95), RED + ORANGE stage spotlight beams from the roof trusses
    (red steel truss, black cable bundles), the Jumbotron screen glowing red-pink with white eyes. Cover blocks: dark
    steel with cyan logo + hazard stripes. Luma 48-62 during the fight.
  - 4:35 (#t=275) after VICTORY (end card "Next game in 10 seconds"): the whole arena reads much brighter (luma 99):
    warm ORANGE floor light (#a56c31, #bb7246, #ad5549), magenta/violet haze on the left, blue on the right (#326ac5).
- YT 0hH3OvhnbrU "Rec Room - GAMEPLAY - The Rise of Jumbotron" (SinoWick Archive, 2022, 17:38, PC 1080p, full run).
  PC rendering is MORE saturated than mobile (pure deep blues #0029b4, #0036a4) but the same palette per room.
  - 0:15-1:45 dressing room ("Gather All Players To Unlock The Next Room!"): the blacklight carpet dominates the view
    (royal blue + lime + magenta swirls on navy, clearly glowing), teal-green walls, cyan emissive bits.
  - 2:30 through the port: near-black foreground, the hall beyond hot magenta/red with cyan-logo crates.
  - 4:00 dark magenta room with a red EXIT sign, cyan emissive strips and logos.
  - 5:30 Bridge: teal/cyan consoles (same as mobile).
  - 7:00 (#t=420) FLIGHT DECK door vestibule: cyan/teal (#0389ad, #33b9d9, #5dd4ea lit; #000926 shadows), red
    "FLIGHT DECK" sign, pink emissive slots on the octagon door. Luma 68.
  - 7:20-8:20 FLIGHT DECK: the parked ship's cockpit with the green "Blast Off!" button ringed in yellow emissive;
    windows onto black space with small cyan/white star-like lights; the deck is DEEP SATURATED BLUE (#0029b4, #01105f,
    #001d89) with violet/purple patches (#6e2aa0, #8b30af) and orange-red strips/ramps (#905842, #a04634). Luma 35.
    9:20 a MAGENTA part (#?? hot pink walls) with round white glowing lamps and red laser lines.
  - 10:30 (#t=630) vestibule again: cinderblock lit saturated blue (#0036a4 / #003dac), pink emissive door slots,
    black-outlined teleport pad (luma 59).
  - 10:45 (#t=645) SPACE / hull: blue-cyan hall with a huge bright cyan light pool on the left (volumetric), hazard-stripe
    blocks, crates with cyan logos. 12:32 very dark blue-black (luma 19) with an assassin bot's pink laser and cyan hit
    sparks; a distant striped orange-yellow "retro sun" disc seen at 12:40 (GUESS: part of the Space backdrop).
  - 13:20 (#t=800) hot MAGENTA room with a cyan spot pool on the wall, railings, red bots (Hangar?).
  - 13:50 (#t=830) HANGAR: hot MAGENTA/pink light on everything (a big shipping container printed "REC ROOM" glows pink),
    black/orange-red hazard stripes on barriers, a CYAN spot pool on a wall, dark navy background, red bots.
  - 14:30-15:50 SUPPLY: 14:30 a dark room with an orange-lit wall (the Easter-egg side room, candles), a cyan-lit crate;
    15:10 magenta-lit corridor with red bots; 15:50 magenta room with tall black warehouse shelving/racks, stacked crates,
    cyan lamps; black steel trusses overhead.
  - 16:35 (#t=995) THE STADIUM = the old Rec Center GYM re-dressed: arched gym windows with grid panes, red Rec Room logo
    banners on the walls, dark RED-BROWN wood floor (#30051b in the dark), dark indigo/violet air (#110431..#1e0640),
    BLUE volumetric spot cones from the roof (#124e8b, #024e98), white cross-shaped sparkle lights hanging, black cable
    bundles + trusses overhead, dark cover blocks with MAGENTA emissive strips. Luma 22 (dark; beams + screen carry it).
  - 17:20 (#t=1040) Jumbotron close: PINK scanline screen #f95dbc with white "> <" eyes, cyan LED ticker "JUMBOTRON SEZ:
    GET REC'T", a cyan logo emblem under it, the air filled with overlapping BLUE light cones (#4163b5 / #1673bf /
    #3b5097), RED steel trusses and the red/magenta arched roof beams, hanging cone lamps. Luma 54.
- YT 0hH3OvhnbrU 0:00 FOYER at "GAME ON!": VERY DARK (luma 15.6): deep navy walls #000b48..#000947 (blue, not violet),
  near-black floor #000117..#000325, a MAGENTA volumetric spotlight CONE (visible beam) falling onto the big neon
  MARQUEE "THE RISE OF JUMBOTRON / A REC ROOM QUEST" (pink neon outline + cyan letters, the brightest thing in the
  room), the booth under it lit pink-red, a pale-blue light beam at the left, black crates with cyan logos in front.
  Mobile EMn5 0:36 "GAME ON!" is the same dark navy.
- APK level lights (DN/refdata/apk/Jumbotron_*.rrsk L records, measured; colour rgb, intensity, range) - the key lights
  per room match the footage: Foyer spot (1,0.17,0.98) magenta I2.98 R45.8 (= the marquee cone) + cyan/blue points;
  Armory (dressing room) spot (0.54,1,0.70) GREEN I2.96 R15 (= the green light pool) + cyan points; Battlefield1 spots
  magenta (1,0,0.68) I3.67 R21.7 + cyan (0.23,0.72,0.79) R43.6 + points violet (0.94,0,1) I13.7 R18.6; Hallway1 red spots
  (1,0.03,0.16) R49 / (0.84,0,0) + blue points (0,0.48,1) I5.9; Hallway2 magenta-red spots (0.84,0,0.33) + violet points;
  Reception cyan (0.15,0.86,1) points + cyan spots R43.6 (=teal Bridge); StadiumConcession cyan/blue points + ORANGE
  (1,0.31,0) points/spot (=the orange strips of the Flight Deck); StadiumEntry violet (0.55,0,0.9) + blue spots R60 +
  orange spot; Garage cyan spot I11.9; Cargo1 magenta (0.91,0.11,0.78) / violet (0.84,0.09,1) / cyan spots R41-47;
  Stadium 18 white-blue points I74.7 R0.5 (the sparkle lamps) + many cyan points + a warm white spot.
  Fog (E fog linear rgb start end): mostly teal-blue (0,0.40,0.60) 5..45 m; Hallway2 navy (0,0.16,0.38) 0..40;
  Reception (0,0.11,0.26) 6..34; StadiumConcession violet (0.24,0.15,0.37); StadiumEntry navy (0,0.03,0.18) 0..354;
  Stadium deep blue (0,0.03,0.34) 5..61.
- YT C9kjCNcZtFQ "Boethiah teaches Freddy How to Play JumboTron" (2022-06, PC 1080p, 19:46) - THE FOYER PRE-GAME in detail:
  - 0:50-2:05 (pre-game, "PRE-GAME / THE RISE OF JUMBOTRON (INVITE ONLY)" console card, START button): the lobby end
    of the Foyer is a cinderblock room lit SATURATED AZURE BLUE, well lit (luma 45-60): walls #0130b7 .. #0572ce (lit
    centre #0872c8), floor navy #001e5b .. #001c4e, ceiling dark navy with ~20 short WHITE-CYAN GLOW STICKS hanging
    (Glowstick_Cyan_Mat - gtscene leaves them out) and a few lying on the floor as cyan dashes; teal double doors with
    a small "Rec Center" sign + a red EXIT sign; black weapon crates (cyan emissive Rec Room logos) with pistols on top;
    a wall-mounted TV monitor; round wall speakers; a light blue haze. NO violet, NO pink in this end.
  - 1:43 (#t=103) the THEATRE end: rows of black folding chairs face a raised stage; above it the neon MARQUEE (pink
    outline, cyan/white letters "THE RISE OF JUMBOTRON / A REC ROOM QUEST"); the stage is lit LAVENDER-PINK (#926bb6,
    #685fb1) with a fan of volumetric light rays behind it; grey steel pillars each side lit lavender/blue; in front of
    the stage low opaque grey-violet barrier panels (the Quest_SciFi_CoverWall / Cover_Glass sets) with cyan logo
    emissives; the room around stays blue (#013f95 .. #063a8c), floor navy with teleport-pad outlines. Luma 58.
  - When the quest starts ("GAME ON!", 0hH3OvhnbrU 0:00) the room goes DARK navy (luma 16) and only the magenta
    spot cone on the marquee + cyan bits remain (house lights down, GUESS: the level animates its lights on start).
  - 2:10 the dressing room (mirror bulbs green-white, green walls, blacklight carpet); 2:30 act-1 magenta hall.
- 0hH3OvhnbrU 10:50-12:20 ACT 6 SPACE (StadiumEntry; after the Flight Deck film + vestibule): a huge DARK NAVY open
  space in deep blue fog (#002b62..#003470 air, #00123b far), a big CYAN light pool/beam (#0079bf, #0692cf, #1787ca)
  washing one side, concrete Jersey barriers with GREEN/black hazard stripes, crates with cyan logos, a cyan light bar
  on the floor, lots of floating MAGENTA/pink particle specks drifting in the air (space dust), explosive barrels;
  luma 30-44. No walls visible in most views - fog swallows the distance (matches the level fog navy 0..354 m).
- NOTE for frame grabs: after loading a watch page the first seeks can return stale frames; seek once, wait 2 s, then
  grab (the helper: seek via movie_player.seekTo, draw the <video> onto a canvas overlay; canvas is not tainted).
- 0hH3OvhnbrU 13:10-14:10 ACT 7 HANGAR (Garage): HOT MAGENTA spot-lit (luma 68): lit surfaces #e61fad / #f824b1 /
  #ec56dd (near-saturated pink where the magenta spot hits pale surfaces), mid #903d94 / #b12f8d, shadows DEEP NAVY
  #011039 / #0a1a4b / #001846 (the room's fill is blue, the key is magenta -> strong pink-vs-navy contrast, no grey).
  Pink shipping containers stamped "REC ROOM", low cover barriers with orange/black hazard stripes, a steel railing
  catwalk, round cyan light pools on walls (#cyan spot), red bots with cyan/pink emissives.
- 0hH3OvhnbrU 7:32 (#t=452) FLIGHT DECK open side: deep saturated blue void (#0029b4-class) full of short CYAN light
  streaks/dashes floating (star-field / speed lines), black cable bundles above; the parked ship is dark steel with
  yellow-ringed emissive console buttons.
- Downed state: the whole screen goes FULL GREYSCALE ("You're Down! -1200 Credits") - frames at 1:15 (EMn5), 8:05, 9:08
  (0hH3) are grey; not a room look.
- LOCAL reference (read only): RecRoomAssets/.unity files/APPPLESARCHIVE/APPPLESARCHIVE-JumbotronBoss-PostProcessingProfile
  .asset = a URP volume captured in the boss scene: Bloom threshold 1.04 / intensity 0.15 / scatter 0.6 (HQ off),
  White Balance temperature +5, Tonemapping mode 1 (Neutral), Color Adjustments postExposure +0.1, contrast +10
  (saturation -100 present but override OFF), Lift (0.92,0.94,1) / Gamma (1,0.98,0.96) / Gain (1,0.99,0.96,+0.034),
  Channel Mixer identity. The .unity/.lighting next to it are a stub (Unity default ambient, fog off) - useless.
  RecRoomAssets/2D Textures has reference PNGs (Carpet_Blacklight_Swirls_Texture, Jumbotron_Texture/_Emissive/_Teal,
  Jumbotron_Marquee_Emissive, Glowstick_*, Door_Scifi_*, Light_Marquee_*) - REFERENCE ONLY (never import).
- INSTALL (rrtool idx objects.tsv) has usable stand-ins for the look: Scifi_Rug_Mat/Tex, Glowstick_Cyan/Red/Purple/
  Orange_Mat, Light_Marquee_mat (+Light_Marquee_EmitScroll), Jumbotron_Marquee_Emissive_Stencil (Texture2D),
  StuntRunner_JumbotronMarquee_Mat + StuntRunner_JumbotronMarquee_Emissive, StuntRunner_Jumbotron_Mat/Tex (a Jumbotron
  screen skin), MakerPen_Skin_JumboTron_Emit, Stagelight_Fog_Mat / LightShaft_Yellow_Mat / LightShaft_Flashlight_Cyan_Mat
  / DormRoom_LightShaft_Gold_Mat (light-shaft cones), StageLight_Can_On_Mat, LockerFountainStageLight_mat,
  Bucket_BlackLight_Swirl_Skin_Tex/_Emit (a blacklight-swirl pattern: closest install match for the dressing-room
  carpet, whose Carpet_Blacklight_Swirls_Tex is level-only; only Carpet_Blacklight_Swirls_Norm is in the install).
  NOT in the install (level-only): Scifi_AreaFog_*_Mat (FogSin), Portal_*_Mat (PortalFog), Pillar_SciFi_Pink/Red_mat,
  Wall_SciFi_Decor_Blue_mat, StageTop_mat, Concrete_PaleBlue_200x_Mat, Enemy_Jumbotron_Mat, Emit_Armory_Mirror_Mat,
  Carpet_Blacklight_Swirls_mat (see DN/iter10/jt_materials.md, a sibling agent's table).
- Foyer cover walls in the level: G dress Quest_SciFi_CoverWall_End x8 / _Mid x4 (half extents 0.5 x 0.51 x 0.16 m =
  1 x 1 x 0.33 m blocks) + Cover_Glass x4 (0.97 x 2.5 m panes, ?SciFi_CoverSideWall_Glass_mat) + Cover_Wall x2; their
  material colour carries alpha 0.354 (c=1,1,1,0.354, like Door_SciFi_Cardboard_mat and Speaker_Intercom_mat) - in the
  footage the cover walls are OPAQUE (only the Cover_Glass pane is see-through). Our pink see-through boxes = these.
- CORRECTION of the 0hH3OvhnbrU act boundaries (confirmed by door signs + level light data): Flight Deck 7:00-~9:10 ->
  (the ship "arrives", no vestibule) SPACE ~9:10-10:20 -> vestibule 10:30 -> HANGAR 10:45-13:00 -> red "SUPPLY" door
  vestibule 13:00 (cyan-lit room with white parking-line floor markings + a roll-up shutter = the Garage's end) ->
  SUPPLY 13:05-16:20 -> Stadium 16:30. So the entries above labelled "10:45 SPACE" are the HANGAR and "13:10 HANGAR"
  is SUPPLY:
  - SPACE (StadiumEntry, 9:20-10:20): VIOLET/PURPLE + DEEP BLUE (t=572 grid, luma 42: #8215a9 / #852cb4 / #9818b9 violet
    hot spots, #0d0661..#190ba1 deep blue-violet walls, floor #3b0c84..#55118b purple), a RED LASER GRID (many thin
    bright red beams criss-crossing, Quest_SciFi_LaserTrail), round WHITE glowing ball lamps, magenta door frames, cyan
    floor light lines (9:45), dark blue side rooms with cyan light pools (10:05). Level: violet spot (0.55,0,0.9) R60 +
    blue spot (0,0.49,0.9) R60 + orange spot (1,0.45,0) R41, navy fog 0..354 m.
  - HANGAR (Garage, 10:45-13:00): DARK NAVY + CYAN (t=692 grid, luma 41: air #002b63..#00326b, cyan pools #1f82b5 /
    #0c6b8c / #008585), Jersey barriers lit cyan with GREEN/black hazard stripes, black/orange hazard-striped pillars,
    green metal barrels, painted floor arrows/parking lines (Garage markings), studio lamps on tripods, crates with cyan
    logos, "REC ROOM" signage; the elite tread bot's PINK orb sprays (the magenta specks are its projectiles, not dust).
    Level: all cyan/blue lights (spot (0,0.6,0.83) I11.9, cyan points), teal fog (0,0.40,0.60) 5..45 m.
  - SUPPLY (Cargo1, 13:05-16:20): the HOT MAGENTA room (t=810 grid above) with pink REC ROOM shipping containers, hazard
    barriers, catwalk railings, cyan wall pools; warehouse shelving; the candle-lit orange Easter-egg side room (14:30).
    Level: magenta spots (0.91,0.11,0.78) R41 / (0.84,0.09,1), magenta points (0.89,0.09,0.77) I8.7, a cyan spot R47.
- OUR pictures measured the same way (6x6 cell averages, System.Drawing):
  - light9_jt_foyer_after: luma 40; walls/pillars #3e1ebb / #242390 / #5431b5 (VIOLET: R 0x3e-0x54), mid #114182,
    floor #11023b (violet-black) everywhere. Real pre-game: walls #0130b7..#0572ce (R ~0), floor #001e5b; real GAME ON:
    walls #000b48, floor #000117.
  - jt_foyer_console: luma 58; room #08279e / #0b317f / #153c72 (close to the real azure in hue at the edges), floor
    #081842 / #080c3d; the card #aa9d73.
  - jt_act1_arrival: luma 51; tunnel walls #471eca / #451dc7 flat violet on every cell, floor #081742 / #09133f.
    Real: near side #1d0b27 near-black purple, the hall #c60faf / #a00a96 magenta + lavender #8461aa.
  - jt_armory_spawn: luma 25; everything #002029 .. #003634 (dark teal). Real: #0c4522 walls in shade, #3f9663 green
    pool, carpet navy with bright swirls, luma 50-58.
  - jt_boss_arrival: luma 16; #150130 everywhere (dark indigo - the real arena AIR is similar, #110431..#1e0640) but no
    blue cones (#124e8b / #1673bf), no cyan glow (#32b5fc), no sparkles, no pink screen (#f95dbc).
