# rcscene - Rec Room's cached July 2025 Rec Center -> RRSK (measurements only)

Source (read in place, never copied): `%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache` -
scene bundle `1AF.data` (`Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity`), its dependency bundles
`183`..`1AE`, and the room data save `182.data` (plain protobuf). Output: names and numbers only.

Build: `bash build.sh` (dotnet SDK 8; Gt.cs's Plates class is extracted at build time; GtPoly.cs and the world's
RrJumbotronPoly.cs are compiled in by path). Run: `dotnet out/bin/rcscene.dll <command>`.

## Commands
| command | what |
| --- | --- |
| `convert <out> [stats=] [name=]` | the iteration-16 data build (rectangle plates; byte-identical to `iter16/work/RecCenter_Final.rrsk`) |
| `convert <out> poly=1 name=RecCenter_Cache [stats=]` | the room-frame layout with the shell as clean polygons (below) |
| `framecheck <a.rrsk> <b.rrsk>` | rigid XZ fit + Y offset on the install pieces (R records, same mesh) both layouts place |
| `roomdata` | the room data's named objects with world poses |
| `pbdump [depth] [skip] [take]`, `pbscan`, `pbtop` | generic protobuf views of 182.data |
| `findguid <hex32>...` | scene objects holding a room-data GUID |
| `mats`, `nodes [re]`, `comps <re>`, `ext`, `dump` | scene inspection |
| `texprofile <material re>` | (round 2) a material texture's seam lines (brick courses, plank rows) counted along u and v |
| `pngprofile <png> <x> <y0> <y1>` | (round 2) one column's luminance of a local reference render (to measure a repeat) |
| `texpattern <material re>` | (round 3) a tiling texture's sparse motif as numbers: ground and motif colour, share, blob count, size, row widths, centres (UV). `RC_TEXPROP=_SpecGlossMap` (or `_BumpMap`) measures that map instead; `RC_PATTHR`, `RC_PATMIN`, `RC_PATLIST` tune it |

## Frame
The cache scene's world space IS the Android level7 frame (the world's `Layouts/RecCenter_Builtin`, `RrRecCenterLayout`):
`framecheck RecCenter_Cache_poly.rrsk RecCenter_Builtin_poly.rrsk` -> 106 install pieces (15 meshes), rotation -0.019 deg,
offset (-0.002, 0.000, -0.002) m, residual median 8.6 mm. Also: terrain origin (-42.2, -0.84, -40.51) identical; the
MakerWorld sign (-12.827, 5.903, -15.715) = level7's Creative sign; all door roots in 182.data = level7's door roots.
So cache -> room = identity.

## poly=1 output (`refdata/apk/jt/RecCenter_Cache_poly.rrsk`)
Installed in the world as `Layouts/RecCenter_Builtin.bytes` = `gzip -9 -n < RecCenter_Cache_poly.rrsk`. Regenerate with
`dotnet out/bin/rcscene.dll convert ../../refdata/apk/jt/RecCenter_Cache_poly.rrsk poly=1 name=RecCenter_Cache stats=out/cache_poly/stats.tsv`
(deterministic: the same bytes every run).
- Shell: every level-only planar piece -> `G poly` (GtPoly: per material, plane, vertex-colour band; outline rings with
  holes; simplified 1 cm). Small boxy bits are `G dress ..._box` (as before). `gtscene polycheck` must show 0
  untriangulated polygons.
- Curved pieces (round 2, T7): a big curved piece is no longer one fitted rectangle per triangle pair. A bar (its box long
  and thin: longest >= 3x the middle, middle <= 0.12 m, filled; the tilted sash bars, the armchair frames) is one oriented
  `G dress ..._box` fitted to it; anything else (balcony fascia and underside, curved floor trims) is written as its facets
  (`G poly`, one polygon per planar facet group: exact, gap-free). Small curved bits (< 0.6 m) are left out as before,
  except those of the vectorised materials (the quest poster cases' bulb studs), which are `G dress ..._stud` boxes with
  their sa=/sg=. Fitted rectangles under 1 cm across are dropped. (The foliage/Mirror materials keep fitted rectangles.)
- `G platemat`: col, tex, scale, inst, mean (texture mean), glow, ds, clear, **avg** (mean x col, sRGB), **itex** (the
  install has a Texture2D of that name, rrtoolidx objects.tsv).
- `R`: install meshes (same name, vertex count, bounds); level-only materials of such renderers as `?` T entries with `K`
  (col, avg, mode, cut, tex/tile when the install has it, emit, vc, shader; without an install texture col = avg).
  Static-batched renderers are matched by their GameObject name, also with the prefab brackets off (`[Light_Flourescent]`
  -> mesh `Light_Flourescent`), and (round 2) re-exports are accepted: the same submesh count with different index counts
  when the batch's vertices, taken back into the node's own space, have the install mesh's local bounds within 2 cm per
  axis (July 2025: 21 chair_stackable_LOD0 with `?Chair_Stackable_White_Mat`, 6 Fig_LOD0 at their node scale, plus the
  root BulletinBoard; `RC_REEXPORT_SKIP`, default `^Curtain_LOD0$`, leaves the [Curtain]s to the room code, which stands
  the install curtains itself). July 2025: 153 R records.
- The community board `Systems/CommunityOutpost/BulletinBoard` is no longer a replaced object (round 2): it is the
  install's BulletinBoard with DormRoom_BulletinBoard_mat (an R record at -12.983 1.374 -13.613); its `G spot
  BulletinBoard` no longer carries replaced=1. The room-data `G spot community_board` stays. (The iteration-16 build
  without poly=1 still leaves it out and stays byte-identical.)
- `E` records (round 2, contract C1), from the scene's RenderSettings in apkscene's format and colour convention (as
  serialized, sRGB): `E fog linear r g b density start end`, `E ambient r g b mode=trilight intensity= equator= ground=`,
  `E sun px py pz qx qy qz qw r g b intensity shadows=soft strength= name=` (RenderSettings.sun, else the brightest enabled
  directional light: KeyLight), `E refl intensity= mode=`. No `E sky` (the cache skybox material is level-only).
- Measured colours (round 2, contract C3): `sa=r,g,b` on `G poly` / `G plate` / `G dress` = the mean albedo of the level
  texture under that record's own UV triangles (texels passing the alpha test; albedo = tex x col, or tex x lerp(1, col,
  tex.a) for Rec Room's alpha-masked tint) - the vertex colour is NOT in it (c= keeps that). `sg=r,g,b` = the mean
  emission (_EmissionColor as serialized x the emission map; may exceed 1, as glow=). Written for atlases (the world's
  IsAtlasTexture words) and level-only (itex=0) non-tiling textures (scale 1,1, no wood/brick/carpet/tile/... in the
  name); not for the texture-frame materials below. Triangles group into one polygon only with equal sa/sg (within 0.03).
- Colour regions (round 2): `RC_VEC` (default `RecCenter_Marquee_Mat|Banner_Mat|Quest_Poster_Case_Mat`) and any
  alpha-test material: each planar facet group (>= 0.02 m2) is rasterised on its plane (cells of 2 texels, >= 1.5 cm,
  sampled through its UVs), clustered into <= 8 colours, cleaned (3x3 majority, regions < 8 cells joined), and the
  triangles clipped to the regions -> one `G poly` per region with its own sa=/sg= (outlines only). A planar group mostly
  cut away by the alpha test is dropped.
- Texture frames (round 2, contract C4): `ua=x,y,z rep=mu,mv` on the `G poly` records of `RC_FRAME` (default
  Wood_Floor_Mat, RecCenter_Brick_Red, LockerRoom_FloorTile_mat, RecCenter_Carpet_Taupe_Mat, RecCenter_Carpet_mat (round 3),
  Fence_Brick_Mat): the world
  direction of +u (dP/du) and the metres per texture repeat along u and v incl. the material tiling, per UV island
  (triangles agreeing within 5 degrees and 5 %). July 2025: wood floor rep 7-8 x 1.14-1.36 m (5 plank rows per v
  repeat: planks ~0.23-0.27 m wide, running along ua); Brick_Grout_tex has 6 courses x 3 bricks per repeat: theatre walls
  rep 1.56 m -> bricks 0.52 x 0.26 m, outside walls rep 2.12 m -> 0.71 x 0.35 m (`texprofile` counts the seams).
- `node=<prefab> part=<object>` (round 2, contract C6) on RecCenter_Posters_Mat records, e.g.
  `node=[RecCenter_Posters]%20(1) part=Poster3`.
- `G tree <pine|aspen|bush>`: the 59 level trees (oaks as aspen), base = prefab root, h, r (90th pct leaf reach), crown
  (5th pct leaf height), and (round 2, contract C5) `avg=r,g,b`: the leaf material's mean over its leaf triangles' own
  UV texels passing the alpha test x colour (area-weighted; the atlas's unused texels do not count). July 2025: oak
  0.369,0.403,0.188 (hue 69, olive), bush 0.337,0.408,0.134, aspen gold 0.566,0.559,0.117, pine 0.208,0.312,0.19.
- `G spot <kind> ... src=roomdata`: the objects 182.data spawns (doors with cat=/sign=/front_yaw=, hoops with score=,
  bells, vending (Replicator), cafe_store, community_board, drinking_fountain, laser_fan, try_on, welcome_mat,
  store_menu, invisible_collision). The scene's prefab-root `G spot`s stay as before.
- `H grid` with gcol= (base terrain layer's mean), lowered 10 cm under every floor polygon within one 2 m cell; no
  underlays.
- Textures are decoded in memory with ../rcref_cache's decoders (RcCacheRef.cs compiled in by path); only the measured
  numbers are written. Debug: `RC_DEBUG=<mesh>` (install matches), `RC_VECDBG=1` (vectorised groups),
  `texprofile <material re>` (a texture's seam counts), `pngprofile <png> x y0 y1` (one column of a reference render).

## Room data (182.data) layout, as far as decoded
Top level: 1 = version (38), 2 x89 = runtime objects (1 = id, 11.1 = prefab GUID, 53.1 = scale, 65 = transform:
2 own id, 3 parent id, 4 local position, 5 local euler), 4 = room GUID text, 6 = subroom id, 28 = the room's object
graph: 28.2.11 x216 = scene objects (6 = name, 8.2 = world position, 8.3 = rotation x,y,z,w (zero fields omitted),
12/13 = ids, 104 = CV2 chip data), 28.2.14 = circuit boards (6 = owner id), 28.2.5 = graph links. Strings name the door
menus ("Quests", "Sports & PVP", "Featured Rooms", "Campus Jams"). No radio, ping-pong table, Friend-O-Tron, mirror or
Magic Door object is in the July 2025 save.

## Round 3 (2026-11-23): the navy carpet
RecCenter_Carpet_mat (theatre floor, upstairs runner, gym rug, door runner) is Carpet_LowPile_Navy_tex x5 (level-only),
_BumpMap Carpet_Burber_norm, _SpecGlossMap Carpet_Burber_Spec (mean 0.204), smoothness 0.616, URP Lit specular setup.
`texpattern ^RecCenter_Carpet_mat$`: 512x512, ground 0.158,0.214,0.302, motif ~0.41,0.31,0.23 (4.1 % of texels), 72
four-pointed stars 17x17 texels on a staggered 6 x (2x6) lattice (rows at v 0.069 / 0.1425 + k/6, u 0.1385 / 0.0553 + j/6).
The world draws it from these numbers (RrJumbotronMotif). Its four G poly records now carry ua=/rep= (1.25, 1.58, 0.853,
1.32 m); nothing else in the layout changed (md5 54f336de.. -> 543ffb72..).
