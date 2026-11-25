# Iteration 10 - The Rise of Jumbotron: result (integrate)

Status: DONE. Compile check clean. Two harness runs of `jt10_audit`: run 1 checked the builders' work, and run 2 came
after the one fix I made. Nothing is uploaded or tested in-world yet.

Screens (final, run 2, arrival views): `DN/iter10/screens/jt10_foyer.png`, `jt10_armory.png`, `jt10_act1.png` ...
`jt10_act9.png`, plus `jt10_foyer_wide.png`, `jt10_foyer_back.png`, `jt10_foyer_boxes.png`, `jt10_armory_wide.png`,
`jt10_act9_wide.png`. The old shots are the `audit_*.png` files in the same folder.

## 1. Checks

- Compile (`SP/compilecheck/build.sh project --editor`), before and after my fix: server-like build 0 errors and 0
  warnings (293 files), API guard 118/118, editor runtime 0 errors, editor tools 0 errors.
- Harness `jt10_audit` (run 1 took 113 s, run 2 took 98 s). Both runs booted, and 128 of 139 steps passed. The 11
  failures are all `dump *_floors: root not found`. The old audit run failed the same way: the quest has no
  "Rr Floors" roots, so the scenario is at fault, not the code.
- Exceptions: none in either run, apart from the editor's startup `DirectoryNotFoundException` (package cache).
- The log confirms these points in every room:
  - Each act has its own environment (`Jumbotron: lighting: Jumbotron_<scene> (act n)`). EnvTest reports each act's own
    key, so acts 1, 2, 5 and 8 no longer use the Foyer's violet.
  - The post profiles work: the Foyer uses `Scifi_Default_SSFX_Profile`, the Armory crossfades to
    `Scifi_Armory_SSFX_Profile`, and acts 1-9 go back to Scifi_Default.
  - StadiumEntry's light grid box is cut from (161, 88, 181) to (35, 15, 113) m.
  - Every hull material came from an install template: 24/24 in the Foyer, 17/17 in the Armory, and so on up to 31/31
    in the Stadium. No template was missing and none fell back to URP Lit or RrLook.
  - Every room has a haze line (mist, portals, kit glow).

## 2. What changed, per builder

### hull (RrJumbotronHull.cs, new RrJumbotronLook.cs)
- Surfaces follow the Golden Trophy approach. Each level material maps to an install AG/Standard-* template, with the
  texture, tint, relief and tiling from the jt_materials section 5 table (`RrJumbotronLook.Plan`, a switch with no
  static state).
- The raw c= vertex colours go into the mesh colours (white where a plate has no c=). They are never folded into
  _BaseColor. A group whose shader doesn't read vertex colour gets one clone per colour instead.
- Alpha below 0.99 no longer makes a surface transparent. Only SciFi_CoverSideWall_Glass and Clock_Glass are glass, so
  the pink see-through boxes are gone. The Foyer cover walls and the act 9 vestibule boxes are now opaque door-panel
  steel with hazard stripes and cyan logos.
- Glow uses the install emissive templates (Arena_Cyber_Emit_*) at the level's power.
- Area fog, portal, light-shaft and nav plates, plus the decals, are left out of the hull. The haze builder draws them.
- Every surface except glass now casts shadows.

### light (RrJumbotronRoom.cs, RrJumbotronArea.cs, RrJumbotronActs.cs, Core/RrPostFxProfiles.cs)
- `ShowLighting(act)` calls `RrEnvironment.SetArea(SceneOf(act))` and `RrPostFx.SetArea`. `RegisterWholeRoom`
  re-registers each scene's box over all its renderers and the arrival point.
- Two new post profiles use the Android measurements: ScifiDefault (ACES, exposure +0.5, contrast -14, bloom
  1.1/0.5/0.617) and ScifiArmory (Neutral, contrast +48.3, bloom 1.1/0.7/0.844). Both clamp bloom at 16; that value is
  ours, for VR.
- StadiumEntry's light grid is cut to the kit core (`GridToCore`).
- No light-probe data was found: the APK's LightProbes assets are empty.

### haze (RrJumbotronHaze.cs, was a stub)
- Scifi_AreaFog_* mist uses a clone of Sandbox_Fog_Mat, whose camera fade is built into the shader. The edges are baked
  into vertex alpha by raycasts.
- Portal glows use the same mist shader, plus a Stagelight_Fog_Mat clone for the 5 fresnel ones.
- Kit glow: light shafts use LightShaft_Yellow_Mat clones in the level colour, and the red lasers use the same clone in
  red. PropBox_SciFi crates are cyan, the explosive barrels orange and the Cargo strip teal. The black curtains use
  Curtain_Red_Mat at 0.245. Concession's nav wall is no longer drawn.
- All of it is frame-sliced at 4 ms and owned by the room. The biggest bake is Concession's: 81,148 rays, 339 ms spread
  over frames.

### integrate (me): one fix
- **The Foyer marquee read mirrored** from the seats and upside down from the stage (run-1 crops in
  `NSP/integ/crop_marquee.png` and `crop_marquee_back.png`). It is a dress box with 0..1 face UVs (`FaceUv`).
  `BoxMesh` ran u along r with Cross(r, u) = n, and in Unity's left-handed frame that puts u to the viewer's left. The
  marquee box's own axes are also rolled.
- **Fix:** a new `RrJumbotronHull.PictureUvs`, used only for FaceUv faces. V runs along the face axis nearest to up,
  pointing up, and U runs to the viewer's right, Cross(n, V). Tiled surfaces are unchanged.
- **Result in run 2:** the marquee reads the right way round from both sides (`NSP/integ/crop_marquee_run2.png`), and
  every luma is identical to run 1.

## 3. Lumas, before (the old audit run, 12:37) and after (run 2)

| Area | Arrival before | Arrival after | Wide before | Wide after | Rec Room target (rr_jumbotron_look 5.7) |
| --- | --- | --- | --- | --- | --- |
| Foyer (pre-game) | 47 | 53 | 34 | 53 | 45-60 |
| Foyer boxes / back / floor | 34 / 31 / 29 | 53 / 61 / 39 | | | |
| Armory | 32 | 74 | 25 | 75 | 50-58 |
| Act 1 Battlefield1 (SS Coach) | 44 | 29 | 44 | 27 | ~60 in the hall (the arrival is the dark port) |
| Act 2 Hallway1 (Cargo Bay) | 40 | 35 | 36 | 39 | 48-56 |
| Act 3 Hallway2 (Engineering) | 17 | 32 | 17 | 33 | 25-37 |
| Act 4 Reception (Bridge) | 29 | 36 | 28 | 34 | ~52 |
| Act 5 StadiumConcession (Flight Deck) | 30 | 30 | 32 | 30 | ~35 (vestibule brighter) |
| Act 6 StadiumEntry (Space walk) | 25 | 33 | 22 | 29 | ~42 |
| Act 7 Garage (Hangar) | 31 | 29 | 24 | 25 | ~41 |
| Act 8 Cargo1 (Supply) | 40 | 24 | 36 | 23 | ~68 where lit |
| Act 9 Stadium (boss) | 31 | 35 | 28 | 40 | 22-62 (vestibule ~84) |

Why the numbers moved:
- The Foyer, Armory and act 3 got brighter; the Armory is now above its target.
- Acts 1 and 8 got darker. Their "before" was the Foyer's bright violet environment wrongly applied, plus flat
  pastel paint. Now their own near-black trilight ambient applies, and the colour comes from the lights, as in
  Rec Room.
- Contrast (sd) went up in the Armory (20 to 51) and act 3 (10 to 30).

## 4. What it looks like now (arrival shots)

- **Foyer:**
  - What's right: the lobby end is azure blue (no violet), the stage end is lavender-pink with red curtains and a fan
    of light behind them, the cover walls are opaque textured crates, the floor is navy, and a soft magenta light cone
    comes down from the ceiling.
  - Close to the reference.
- **Armory:**
  - What's right: green walls, the blacklight carpet (royal-blue spirals, lime dots, red arcs), lockers, consoles with
    lit buttons, weapon racks, and the cyan octagon door.
  - Very close to the footage.
- **Act 1:**
  - What's right: a near-black navy port tunnel opening onto a hot-magenta hall with lavender pillars, cyan strips and
    a blue window area behind. This matches the reference ("the port approach is near-black purple").
- **Act 2:**
  - What's right: a locker corridor with a red band on the walls, door-panel crate stacks with cyan logos, and blue
    ceiling mist.
  - Differs: less crimson than Rec Room.
- **Act 3:** dark navy server corridor with thin red laser beams and lit server-rack screens.
- **Act 5:** a teal/navy corridor of crate stacks toward a red-lit room.
- **Act 9:** a navy vestibule of opaque door-panel boxes (they used to be see-through). The arena beyond shows the pink
  floor, the stands and the trusses.

## 5. What still differs from Rec Room

1. **The marquee** is the install stand-in `StuntRunner_JumbotronMarquee_Mat`, so it reads "STUNTRUNNER...", not
   "THE RISE OF JUMBOTRON / A REC ROOM QUEST". The level's texture is level-only. `Jumbotron_Marquee_Emissive_Stencil`
   / `Light_Marquee_mat` might be closer. The marquee also barely glows; in Rec Room it is the brightest object.
2. **Armory:**
   - Brighter than Rec Room (74 vs 50-58).
   - The ceiling reads as a bright teal gradient; Rec Room has an open black truss ceiling.
   - The ARMORY sign is a flat red box with no letters.
   - The posters are black rectangles (level-only textures).
3. **Vestibules too dark:** Rec Room's door vestibules are bright cool blue/teal (luma 60-108). Ours are navy: the act 5
   and act 9 arrivals are 30-35, against 84 for the boss vestibule.
4. **Acts still dimmer than the footage:** act 2 is not crimson enough, and acts 7 and 8 are dark navy with no visible
   cyan pools or magenta spot from the arrival. The ambient is Rec Room's own near-black trilight, and there is no
   baked probe data. The key lights and the grid carry everything.
5. **Act 5:** a flat, unlit light-cyan square hangs in the doorway. It is a level-only material with no install
   stand-in, and I have not identified it.
6. **Not covered:**
   - The Foyer's hanging cyan glow sticks are not seen.
   - The StageLight_Glow dress quads are left out.
   - Several level-only kit props stay the grey placeholder: posters, books, chairs, Coiled_Wires,
     Dumpster_SciFi_Orange and a few more.
7. **Haze:**
   - The mist is approximated by a normal-based bake, not the real line of sight.
   - The U direction on some portals is a guess, so they could fade from the wrong side (Pink, Purple_Hallway2,
     Purple_Cargo, Pink_Concession).
8. **The Jumbotron boss cube** (Enemy_Jumbotron hot-pink faces) was not looked at in this run.

## 6. Known risks for the in-world test

- **Vertex colours** are raw and assume Rec Room renders in linear space. Check that the c = 0.15 walls in Hallway2
  read mid-dark grey, not black or pale.
- **Armory contrast +48.3** is Rec Room's own value. It looked fine in the harness, but check the dark corners in the
  headset.
- **The bloom clamp of 16** is our own value. If glows look capped, remove the two `.Set("Bloom","clamp",...)` lines in
  RrPostFxProfiles.
- **StadiumEntry's grid cut** only takes effect if the grid has not built yet. The harness showed the cut; in-world,
  the grid starts after travel or after 30 s.
- **Haze bake cost:** Concession runs 81k raycasts at 4 ms per frame, so the room takes a few more frames to finish.
  Watch for a hitch when the quest builds.
- **Bigger registered scene boxes:** StadiumEntry's is (114, 37, 153) m. Rooms are 250 m apart, and in the harness each
  act picked its own environment.
- **Nothing Rec Room ships with the world.** Everything comes from the install templates at runtime, and every clone
  and mesh is owned by the room and freed with it.

## 7. Files changed by integrate

- `Assets/#User/SomniumRecRoom/Games/Quests/Jumbotron/RrJumbotronHull.cs`: BoxMesh FaceUv branch and the new
  `PictureUvs`.
- `DevNotes/iter10/impl_integrate.md`, this file, and the `DevNotes/iter10/screens/jt10_*.png` shots.
