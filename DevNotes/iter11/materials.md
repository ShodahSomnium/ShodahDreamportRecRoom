# iter11 materials (owner: RrJumbotronLook.cs, RrJumbotronHull.cs)

## Plan
1. Read DN/iter10/jt_materials.md sec 1/5, current RrJumbotronLook mapping.
2. For top ~25 materials by area: decode APK texture (NSP/jt10/materials tools) + install template texture -> PNG, look, fix substitute / tiling.
3. See-through: no transparent/alpha-clip except glass; support platemat ds=1 in Hull (flipped copy); dress box materials.
4. Keep iter10 fixes (mesh.colors raw, 0.354 alpha not glass, emission strength, Marquee).
5. Compile-check (SP/compilecheck/build.sh project --editor).

## Log
- (start) notes created.
- Built NSP/iter11mat/texpng (look-only contact sheets: level _BaseMap rgb|alpha vs install tex rgb|alpha; NSP/iter11mat/png/sheet0-4.png, spec1.txt).
  FINDINGS: Door_Scifi_Cardboard_Tex is an ATLAS (door face, hazard stripes, white top-right corner, alpha glow strip) -> world-tiled on
  Pillar_SciFi_Pink/Red, Door_SciFi_Cardboard plates, Wall_SciFi_Decor_Blue, Enemy_Jumbotron, SciFi_CoverWall* = the repeated hazard stripes
  and icons on act 5 crate stacks (jt10_act5.png) and likely the flat light-cyan square (door alpha corner x cyan x glow 2.37).
  Level Pillar_SciFi_tex = plain grey panel + tiny white dots; Wall_SciFi_Decor = grey panel; -> plain grey metal is closer.
  Curtains_Tex = atlas (rope strip, gold fringe) -> plain dark cloth. Linoleum install tex = 2x2 of the level's -> tile 2 m.
  Metal_Ribbed (vertical ribs) vs corrugated (horizontal) -> rotate UV 90. StageTop/Wood_Planks = planks -> Wood FloorTiled template.
  Carpet blacklight vs SciFi_Carpet_Tex: very close. Tread: close. Cinderblock: Concrete_Tex + Brick_Norm ok.
  gtscene plate axes: walls u = horizontal, v = world up (same as ProBuilder planar UVs).
- CULL (NSP/iter11mat texpng shcull): every AG/Standard-Opaque ForwardLit pass is a CONSTANT Cull Back (2/<noninit>), AG/Unlit-Color too;
  only additive/portal/leaf shaders are Cull Off (matches geometry.md). So no hull material is double-sided; ds=1 support added anyway
  (harmless, geometry may emit it).
- effall.tsv (NSP/iter11mat): effective mean albedo/emission (AG math) for all 223 level materials -> measured-mean table for the
  unplanned level-only ones (Brick_Painted_* variants, props, posters) so Default never paints a flat 0.7 grey.
- DECISIONS (Plan): atlas materials on plates -> plain surfaces at the measured mean (Metal_LightGrey_Dull when the tint fits, else the
  Paint_LightGrey pen): Door_SciFi_Cardboard (plates), Pillar_SciFi_Pink/Red, Wall_SciFi_Decor_Blue, Enemy_Jumbotron, SciFi_CoverWall01-03,
  Wall_SciFi_Cover_Modular (plates; dress keeps the door picture), Curtain_Black (0.225 cloth), Crane_Red.
  Linoleum tile 2 m; Metal_Ribbed rotated corrugated 1.5 m; StageTop / Wood_Planks on Wood_Floor_Matte (planks) 1 m / 2.25 m;
  Scifi_Rug = picture fitted per plate (Fit); EventRoom_Moon skipped (transparent picture). Dress Cover_Glass frames skipped (the glass
  box stays: the opaque frame bounds hid it = a flat cyan slab). Scaffolding dress -> open truss frames.
  Resolve guard: any install/template material that is transparent / alpha-clipped and not glass -> alpha-clip clones get _Cutoff 0,
  blended ones are re-planned by name, then the pen.
- CODE (1st pass, before compile): RrJumbotronLook.cs rewritten (Plan changes above, Plain/Mean/Fitted/Turned helpers, SeeThrough/ClipOnly/Unclip,
  measured-mean cases for the unplanned level-only materials, brick_painted_* rule, fence = glass). RrJumbotronHull.cs: MatInfo.Ds/Plate.Ds
  (ds=1 on platemat or plate), Emit chunks by quads, PlateMesh back faces + Fit + Rotate, BoxMesh Rotate, Resolve see-through guard
  (Stats.Solidified), dress: GlassFrame skip, Frame() -> Truss/Tripod open frames (Stats.Framed), st.Dress counts records.
  Backups: NSP/iter11mat/backup/*.iter10
- COMPILE 1: SP/compilecheck build.sh project --editor -> server-like 0 errors 0 warnings (293 files), guard 118/118, editor 0/0.
- Swatches (NSP/iter11mat/png/swatch1.png, texpng swatch mode: 4x3 m walls with our tiling/tints): cinderblock blocks ~0.67 m ok,
  linoleum 2 m tiles ok, ribbed rotated = vertical ribs ok, planks ok, rug fit ok, plain metals ok.
- 2nd look pass (png2/sheet0-1): DoorTrigger_Tex / MetalBarrel_Tex / Locker_Fountain_Spotlight_Tex are atlases too -> SciFi_DoorTrigger plain
  metal, MetalBarrel_Green plain paint green, LockerFountainStageLight plain on dress (tripods/fountains). Poster_ShakeHands fitted.
  80Sunset_mat (StadiumEntry, 1 plate 11x11 m) was a flat glowing orange SQUARE; Sunset_Tex = striped yellow->magenta disc ->
  RrJumbotronHull.Sunset draws the disc (rows of quads, stripes, raw vertex-colour gradient on Arena_Cyber_Emit_Orange clone, glow 1.6).
- COMPILE 2: 0 errors, 0 warnings, guard 118/118, editor 0/0.
- Coordination (read geometry.md / layout.md): layout found act 5's cyan square = a kit Cube with SciFi_Blue_Emit_Cargo1_Mat (haze, fixed
  there); geometry's jt mode re-measures stand-ins into plates / per-piece oriented "G dress" boxes / "<mesh>_vox" shells and keeps
  Glowstick plates. So: Frame() guarded (never *_vox; Truss only for whole-truss bounds: smallest half >= 0.15, long >= 1.5x mid;
  Tripod only >= 0.5/0.3/0.3), Glowstick_* -> Arena_Cyber_Emit_Teal in the level colour glow 3, install prop atlases on plates/dress
  (ActivityDoor, crates, books, bookshelf, chairs, computer, signs, lantern, barrels, target, pingpong, pipes, pot, projector, speaker,
  cone, bottle, jug, tractor tire) -> Plain at their measured means (effall.tsv); Poster_StayHydrated fitted.
- COMPILE 3: 0 errors, 0 warnings, guard 118/118, editor 0/0.
- PenOf keeps Disc too. COMPILE 4 (final): server-like 0 errors 0 warnings (293 files), API guard 118/118, editor runtime 0, tools 0.

## Result (materials, DONE)
Files: Games/Quests/Jumbotron/RrJumbotronLook.cs, Games/Quests/Jumbotron/RrJumbotronHull.cs (backups NSP/iter11mat/backup/*.iter10).
- Wrong textures: atlas textures no longer world-tiled on plates -> plain surfaces at Rec Room's measured mean (Plain = dull metal when
  the tint fits, else paint): Door_SciFi_Cardboard (plates), Pillar_SciFi_Pink/Red, Wall_SciFi_Decor_Blue, Enemy_Jumbotron, SciFi_DoorTrigger,
  Wall_SciFi_Cover_Modular (plates), SciFi_CoverWall01-03 (paint + soft teal glow 0.25), Curtain_Black (0.225 cloth), MetalBarrel_Green,
  20+ install prop atlases, Glowsticks (emissive). Tiling/orientation: linoleum 2 m, Metal_Ribbed turned (vertical ribs), StageTop and
  Wood_Planks on the plank floor, rugs + posters fitted per plate. 80Sunset = striped yellow->magenta disc (was an 11 m orange square).
  EventRoom_Moon skipped. Unplanned level-only materials get measured means (no flat 0.7 grey); Brick_Painted_* on the brick relief.
- See-through: every template guarded (SeeThrough/ClipOnly/Unclip; Stats "N see-through templates made solid"); only SciFi_CoverSideWall_Glass,
  Clock_Glass, *glass*/*fence* are transparent. ds=1 (platemat or plate) -> flipped back quads (Stats "N double-sided"). APK check: all
  opaque level shaders are Cull Back, so none is needed from materials.
- Dress: Cover_Glass frame boxes skipped (glass pane stays); whole-truss Scaffolding bounds -> open trusses (4 chords, rings /1.5 m, braces),
  Tripod -> legs + pole + head (Stats "(N as open frames)"); guards keep per-piece boxes and *_vox shells as boxes.
- Kept: raw c= in mesh.colors, 0.354 alpha not glass, emission powers, Marquee neon, PictureUvs.

## Open issues
- The door panel's cyan glow strips and the pillars' pink dots are gone with the plain surfaces (a glow on the plain templates would light
  whole faces). Geometry could tag plates whose level UVs sit in a texture's alpha (glow) region (e.g. "a=" per plate); the hull could then
  draw those as emissive strips.
- Wallpaper_SciFiPanels_Big (1,893 m2) is plain metal: no install texture has its 5 m panel pattern.
- Not verified in-world (no harness by instruction): check that the pillars/door frames read as grey metal in the coloured light, the
  sunset disc faces the space walk, and trusses under the Foyer / Cargo Bay ceilings read as open black frames.
