# Iteration 11 - The Rise of Jumbotron: result (integrate)

Status: DONE, nothing tested in the headset yet. The compile check is clean. The harness ran twice (`jt10_audit`): run 1
checked the builders' work, and run 2 came after the one fix I made. Nothing is uploaded.

The user's second request in this round, reloading the gun with the other hand, is not part of the Jumbotron work.
This job did not touch it (see "Open").

Screens (run 2): `DN/iter11/screens/jt11_foyer.png`, `jt11_armory.png`, `jt11_act1.png` ... `jt11_act9.png` (arrivals),
`jt11_foyer_edge.png` and `jt11_act1_edge.png` (close shots along a wall), and `jt11_act4_edge_before_after.png` (run 1
next to run 2, act 4's right vestibule edge enlarged). All 37 shots of both runs are in `NSP/int11/run1` and `NSP/int11/run2`.

## 1. Checks

- **Compile** (`SP/compilecheck/build.sh project --editor`), before and after my change:
  - Server-like build: 0 errors and 0 warnings (293 files).
  - API guard: 118/118.
  - Editor runtime: 0 errors. Editor tools: 0 errors.
- **Size budget:**
  - All `RrScene_*.cs` files: 5,090,559 B, against the ~6.0 MB limit.
  - The Jumbotron scene files: 3,257,919 B, against the 3.5 MB budget. Iteration 10 was 1.40 MB.
- **Harness scenario** (`SP/harness/scenarios/jt10_audit.json`; the original is kept at `NSP/int11/jt10_audit.json.orig`):
  - The 11 broken `dump *_floors` steps are removed.
  - Eight close shots are added. For the Foyer and acts 1, 5 and 9 there are two each:
    - `*_wall`: a wall 3-3.5 m ahead.
    - `*_edge`: a view along a wall 0.6 m to the side.
  - I picked the positions with a raycast over the plates (`NSP/int11/ray.pl`) and offline gtscene previews.
- **Runs:** both booted, with 0 step failures out of 136, in 80.5 s and 79.0 s.
  - Errors: only the editor's two "Host type is not matching" messages and the package-cache
    `DirectoryNotFoundException`.
  - No exception from our code.
- **Golden Trophy:** unchanged. Its files are untouched. The patched gtscene with jt off writes byte-identical rrsk to
  the unpatched tool for level19 Foyer, level24 DeadIsland (with its REF2 file) and level28 Battlefield.

## 2. What changed this iteration (the builders, as seen in the harness)

| Complaint | Before (iteration 10) | Now (run 2) |
| --- | --- | --- |
| Floating boxes | 1,233 level-only props drawn as solid bounds boxes: 6 m truss slabs, 7 m poles, terminals and doors as blocks | Props are re-measured into plates, per-piece boxes or voxel shells. Trusses read as open black frames (acts 8 and 9, Armory ceiling) and tripod lamps as legs, a pole and a head (acts 6 and 7). |
| Act 5 light-cyan square | A 1 m teal glowing cube 9.7 m ahead of the arrival | Gone (`jt11_act5.png`) |
| Foyer magenta box/cone | A solid cone hanging by the lamp (`jt10_foyer_wide.png`) | A soft magenta shaft from the lamp to the marquee, plus cyan shafts from the tripods (`jt11_foyer.png`) |
| See-through gaps | Trims, lids, edges and chamfers under 0.05 m2 were dropped | Rectangles from 1 cm in every material, on each face's own axes. None of the 8 close shots shows a gap. In act 4 the last slits were closed by my fix (section 3). |
| Wrong textures | Atlas textures tiled across walls: repeated hazard stripes and icons on pillars and crates, white or cyan patches | Plain surfaces in Rec Room's measured colours. Act 5's crate stacks are plain grey-blue. The ribbed walls run vertically. The linoleum repeats every 2 m. The stage is wooden planks. |
| Next room visible | The current and the next act were both on | Only the current act is on. Active renderers per act fell from 167-317 to 84-148 (Stadium 238). |
| Marquee | Read "STUNTRUNNER" | Reads "THE RISE OF JUMBOTRON" from the seats (`audit_foyer_back`) |

Lumas of the arrival and wide shots, iteration 10 (jt10_result) then now (run 2), with the Rec Room target:

| Area | Arrival | Wide | Target |
| --- | --- | --- | --- |
| Foyer | 53 -> 56 | 53 -> 53 | 45-60 |
| Armory | 74 -> 80 | 75 -> 80 | 50-58 |
| Act 1 | 29 -> 28 | 27 -> 26 | ~60 in the hall |
| Act 2 | 35 -> 33 | 39 -> 36 | 48-56 |
| Act 3 | 32 -> 31 | 33 -> 36 | 25-37 |
| Act 4 | 36 -> 37 | 34 -> 35 | ~52 |
| Act 5 | 30 -> 39 | 30 -> 33 | ~35 |
| Act 6 | 33 -> 35 | 29 -> 30 | ~42 |
| Act 7 | 29 -> 29 | 25 -> 25 | ~41 |
| Act 8 | 24 -> 24 | 23 -> 22 | ~68 where lit |
| Act 9 | 35 -> 36 | 40 -> 40 | 22-62 |

## 3. My fix: see-through teeth along diagonal edges (gtscene jt=1)

**What run 1 showed.** In act 4's arrival (the Reception vestibule), a vertical sawtooth ran up both wall edges with grey,
magenta and red slivers showing through it. See the left half of `jt11_act4_edge_before_after.png`.

**Why.** At (201.07, y, 24.89) in the Reception rrsk, a wedge triangle 0.3 x 3.8 m (a vestibule wall cheek) cannot be
rectangles. It was staircased in 5 cm strips, keeping each cell only when its centre was inside the face. That leaves a
row of see-through triangles, each 5 cm x 0.31 m, all along the diagonal edge.

**Change** (`DN/tools/gtscene/Gt.cs`, `Plates.OfJt`, jt=1 only):
- Staircase cells now use `CoverTouch`: a cell is kept when any of its 5 test points is inside the face. This is the rule
  the tool already used for small slivers.
- A diagonal edge now overshoots by at most one 2-5 cm cell. In a room corner the overshoot hides behind the neighbouring
  face. On an outer edge it shows as small tabs in the face's own colour.
- `GTJT_STAIRCENTRE=1` brings back the old test. With it, the Reception output is byte-identical to the checked-in file.
- The header comment is updated.
- The 11 rooms were regenerated with `DN/iter8/jumbotron_convert.sh NSP/apkwork NSP/int11/tools`.

**Measured:**
- Offline survey (the geometry agent's `holes.sh`: 11 rooms x 6 points x 5 views), background pixels before and after:
  - Reception 70 -> 30, Garage 13 -> 5, Cargo1 44 -> 20, Hallway1 200 -> 117.
  - Space and Stadium are sky, about equal.
  - The rest were already 0-12.
- Plates: -0.1 to -1.7 % per room (Hallway2 +1.7 %), because the filled cells merge into bigger rectangles.
- Run 2: the slivers are gone. What is left is a sawtooth outline of lighter tabs in the same hue (right half of
  `jt11_act4_edge_before_after.png`). No other shot changed (identical lumas).

Backups: the geometry agent's Gt.cs and the iteration-11 rrsk and Scenes files before my change are in `NSP/int11/backup/`.

## 4. What still differs from Rec Room

1. **Sawtooth outlines** stay where a face has a diagonal edge that no rectangle can follow, such as act 4's vestibule
   cheeks. They are no longer see-through, but you can see them up close. A finer staircase step (currently 2-5 cm,
   `Math.Clamp(min side / 4, 0.02, 0.05)` in OfJt) would halve them at some plate cost. Only true triangles would remove
   them, and the data rule allows rectangles and boxes only.
2. **Brightness:** the Armory is still brighter than Rec Room (80 against 50-58). Acts 1, 2, 7 and 8 are darker than the
   footage (22-36 against 41-68), and the door vestibules are navy rather than bright cool blue.
3. **Plain atlas surfaces:** the door panels have lost their cyan glow strips and the pillars their pink dots. The big
   sci-fi wallpaper panels are plain metal (materials open issue: a per-plate glow key from the level UVs).
4. **Black curtains** use the install's red curtain at 0.245, so up close they show a dark knit with red dots (act 9's
   arena edges, act 4's doorway). Rec Room's are plain black cloth.
5. **Armory:** the posters are black rectangles, the ARMORY sign has no letters, and the ceiling is a bright teal gradient
   instead of an open black truss ceiling.
6. **Small leftovers:**
   - Hairline slits at some ribbed-barrel facets (geometry open issue).
   - Portal-glow fade direction is still a guess.
   - The lamp fixtures the rebuilt shafts come from are only partly drawn.

## 5. In-world risks

- **Build time at quest start:** "built Jumbotron in" 8.0-8.7 s in the harness, against 7.7-8.4 s in iteration 10. All
  11 rooms build behind the loading card, and the haze bakes are frame-sliced at 4 ms. The client PC may be slower;
  watch the loading card time.
- **Plate counts** (hull plates drawn in run 2, plus dress boxes):

  | Room | Hull plates | Dress boxes |
  | --- | --- | --- |
  | Foyer | 5,462 | 1,842 |
  | Armory | 3,498 | 342 |
  | Act 1 | 10,028 | 948 |
  | Act 2 | 9,760 | 956 |
  | Act 3 | 16,079 | 1,206 |
  | Act 4 | 8,972 | 220 |
  | Act 5 | 12,692 | 3,377 |
  | Act 6 | 19,087 | 2,789 |
  | Act 7 | 10,825 | 344 |
  | Act 8 | 20,806 | 2,752 |
  | Act 9 | 30,857 | 3,262 |

  That is about 4x iteration 10.
- **Frame rate:** only the current act is active. The worst case is act 9: about 62 k plate triangles plus about 39 k box
  triangles in 45 hull meshes, and 238 active renderers (252 material draws). The harness cannot measure VR frame time
  (the editor's longest frame, 425 ms, is shader compilation). Check the frame rate in the Stadium and Supply (act 8)
  in the headset.
- **Late weapons:** weapons at a room's spots now load when the team arrives (only the current act is on), so they may
  appear a moment late.
- **Room switch at the teleport:** the next room turns on in the same frame as the teleport. Its light grid builds in
  8-25 ms, so the first frames there may be flat-lit.
- **Rebuilt light shafts:** their strength has not been seen in the headset. Tune `AddShaft` in RrJumbotronHaze.cs if
  they read too strong or too weak.
- **Ownership:** everything a room makes is owned by the room (`RrJumbotronHull.Owned`) and freed with it. There is no
  DontDestroyOnLoad, and nothing from Rec Room ships.

## 6. Files changed by integrate

- `DevNotes/tools/gtscene/Gt.cs`: the staircase CoverTouch change, the `GTJT_STAIRCENTRE` knob and the header note.
  `out/gtscene.dll` is rebuilt.
- `DevNotes/refdata/apk/Jumbotron_{Foyer,Armory,Battlefield1,Hallway1,Hallway2,Reception,StadiumConcession,StadiumEntry,Garage,Cargo1,Stadium}.rrsk`
  (regenerated).
- `Assets/#User/SomniumRecRoom/Games/Quests/Jumbotron/Scenes/RrScene_Jumbotron_*.cs` (11 files, regenerated).
- `SP/harness/scenarios/jt10_audit.json` (harness scenario; not part of the project).
- `DevNotes/iter11/integrate.md`, this file, and `DevNotes/iter11/screens/jt11_*.png`.

## Open

- **Reloading with the other hand** (the user's report) is not addressed by this Jumbotron job and needs its own fix.
- The sawtooth outlines (section 4.1), the brightness gaps (4.2), the glow strips on plain surfaces (4.3) and the curtain
  material (4.4).
- Frame rate in acts 8 and 9 in the headset.
