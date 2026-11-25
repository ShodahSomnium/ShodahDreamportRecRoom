# Iteration 10 - impl "hull" (RrJumbotronHull.cs + new RrJumbotronLook.cs)

Status: DONE 2026-09-25 (compile-checked, 0 errors; not run in the harness by instruction)

Owned files: Games/Quests/Jumbotron/RrJumbotronHull.cs, Games/Quests/Jumbotron/RrJumbotronLook.cs (new).

## Plan
1. Read jt_materials.md s5 (table), 4c (shader findings), 6 (special); jt_audit / jt_lighting as needed; GT hull Plan/Prepare/Tinted.
2. New RrJumbotronLook.cs: per-material plan table (level material -> install template, texture, tint, tiling m, emissive power, glass flag).
3. RrJumbotronHull: group plates per material (not per tint), c= RAW into mesh.colors (white default), templates cloned + owned.
4. Dress boxes: same plan, box mesh with UVs in metres and white vertex colour; no RrLook.Glass for alpha < 0.99.
5. NotASurface plates stay in Data (haze uses them), not drawn.
6. One log line per room "hull surfaces: material -> template".
7. Compile check (SP/compilecheck/build.sh project --editor).

## Log
- 14:2x read jt_materials 1/2/4/5/6, jt_lighting F + 4.5, jt_audit 3, GT hull Plan/Prepare/Tinted, RrLightGrid, RrMaterials.Fix.
  Checks done:
  - install index (SP/rrtool/idx/objects.tsv): every template in the table exists (Concrete_Blue_50x_Mat, Tile_Linoleum_Brown_Mat,
    Metal_StepTrim_Mat, Metal_LightGrey_Dull_Mat, Metal_FloorTread_Shiny_Mat, Door_SciFi_Cardboard_mat, ControlPanel_Mat,
    Arena_Cyber_Emit_Teal/Orange_Mat, Scifi_Rug_Mat, Wood_Floor_Matte_mat, Wood_Horizontal_DarkBrown_5x/Orange_Mat,
    Paint_RedOrange/DarkGrey/LightGrey_Mat, MetalBarrel_Blue_Mat, Vent_Modular_Mat, Curtain_Red_Mat, Glass_Teal_mat,
    Rocks_Tiling_Background_Mat, Metal_Grey_Gloss_Mat, StuntRunner_JumbotronMarquee_Mat); textures Brick_Norm, Brick_Spec,
    SciFi_Carpet_Tex exist. Rock_Boulder_Mat (dress, marked inst) is NOT in the index -> falls back to Rocks_Tiling_Background_Mat.
  - vertex colour: shdis VS disassembly - AG/Standard-Opaque (agstd) 408/424 VS and AG/Standard-Opaque-Emission 400/408 VS
    read COLOR (the rest are depth/shadow passes); PS multiplies it by _BaseColor (jt_materials 2). AG/Standard-Opaque-Instanced
    (Curtain_Red_Mat) is not disassembled -> treated as NOT reading vc (only matters for c= plates; Curtain_Black has none).
    Any group whose final material does not read vc falls back to per-tint clones, _BaseColor = (base.linear * vc).gamma.
  - AG UnityPerMaterial uses only _BaseMap_ST (all maps), _BaseColor, _Surface, _EmissivePower: smoothness comes from the
    _SpecGlossMap texture, so the table's "sm" values are not set (no effect on these shaders).
  Decisions:
  - Data.Plates is now keyed by the plain level material name (was name|tint); each Plate carries Vc (raw c=, Color32;
    (0,0,0,0) = none = white). Haze agent keys on material names (Scifi_AreaFog*, Portal_*) - unaffected.
  - Tiling lives in the UVs: UV = repeats / s, repeats = metres / tile (the table's metres per repeat), s = the final
    material's _BaseMap scale (clones reset to 1; as-is install materials keep theirs). Per-plate integer shift of the
    repeat so UVs stay small (precision) while coplanar plates still tile seamlessly.
  - Dress boxes merged per material (24 verts/box, <= 2,666 boxes per mesh), UVs in metres / tile from each face's corner,
    white vc; marquee uses 0..1 face UVs. No colliders (as before).
  - No static state: the plan is a switch in RrJumbotronLook.Plan; all caches live in Owned.Materials (room) or Build locals.
- 14:3x WROTE Games/Quests/Jumbotron/RrJumbotronLook.cs (+ .meta, fresh guid): Look class + Plan(mat, col, scale, inst, dress)
  switch over the jt_materials 5 table (plates + dress rows), Default() by name for the rest (glass->Glass_Teal_mat only
  for names with "glass"; emit/glow -> Arena_Cyber_Emit_Teal_Mat; rock -> Rocks_Tiling_Background_Mat; concrete/metal/wood
  templates; else Paint_LightGrey_Mat with the level colour), PenOf() last resort, ReadsVertexColour(). "maker-pen = RR
  mean" rows use Paint_LightGrey_Mat with tint = gamma(linear(mean)/0.61) (Paint_Tex linear mean) so the mean matches.
  Choices beyond the table: dress door-panel tile 1.5 m (cover walls ~1.0 x 1.31 m faces); 80Sunset -> Arena_Cyber_Emit_Orange
  tint 0.65,0.35,0.10 glow 1; Mirror_Quest -> Metal_Grey_Gloss_Mat tint 0.3,0.32,0.35; Clock_Glass -> Glass_Teal_mat
  tint 0.9,0.95,1 a 0.15; Enemy_Jumbotron plates left outside the boss drop box -> door panel tint 1,0.241,0.558 glow 3;
  StageLight_Glow_* dress, GarageMarkings, LockIcon, Wires: not drawn (decals / glow cards).
- 14:4x REWROTE Games/Quests/Jumbotron/RrJumbotronHull.cs: Extract keys plates per material + Plate.Vc (raw c=);
  Build: per material Plan -> Resolve (install template as is / clone with texture, relief, tint, glow; AsIs failing ->
  Default by name -> paint template -> RrLook.Solid) -> vertex-colour meshes (ReadsVertexColour) or per-colour clones
  (_BaseColor = (base.linear * vc).gamma). Dress merged per material (BoxMesh, metres UVs, white vc, FaceUv for marquee).
  NotASurface now internal (haze may use it). Stats + VertexColour/PerColour. Log "<scene> hull surfaces: mat=look [material / shader];".
  Next: compile check.
- 14:5x compile check 1: 0 errors / 0 warnings (293 files; editor-like + editor tools 0 errors).
- 14:5x fixes after self-review: stop after Emit when the room is gone; Resolve makes no clone once Owned is destroyed
  (no leaked materials); dress merged per material AND tiling/face-UV (two looks sharing one material); dress materials
  counted in Stats.Materials / InstallMaterials. Shadow rule: off only for glass / queue >= 2900 (the GT hull casts
  shadows from every surface; the old "> 2500" rule turned shadows off for the maker-pen walls). Compile check 2: 0 errors.
- Coverage check (NSP/jt10/materials/mat_area.tsv vs the Plan switch): every level-only plate material has a table row;
  the only ones on the default path are inst=1 (Metal_Grey_Gloss, Plaster_Black, Wood_Horizontal_DarkOrange_5x,
  Paintball_TractorTire, Wood_Horizontal_Orange, EventRoom_Moon, Concrete_Barrier, Speaker, Barrel_Table_Crate,
  Poster_ShakeHands: the install material as it is at the level's tiling). Dress: all 33 dress materials covered
  (?Book_Blue_Mat, ?Quest_Poster(_Case)_Mat -> paint template in their dress colour).

## Summary of what the hull now does
- Plates: one mesh per level material (<= 16,000 plates), raw c= in mesh.colors32 (white otherwise), install AG template
  per jt_materials 5 (as is or cloned with texture / Brick_Norm+Brick_Spec relief / tint / _EmissivePower), tiling in the
  UVs. Per-colour clones only when a final shader does not read vertex colour (runtime check).
- Dress: same plan, merged boxes, UVs in metres, white vc; glass only SciFi_CoverSideWall_Glass / Clock_Glass (Glass_Teal_mat).
- Not drawn: AreaFog / Portal_ / LightShaft / InvisibleWall / Nav plates (kept in Data for the haze), GarageMarkings,
  LockIcon, Wires, StageLight_Glow_* dress.
- Log: "Jumbotron <scene> hull surfaces: <mat>=<template>[+tex][ tint][ glow] tile ..m [<material> / <shader>] (+vc);".

## Open issues / to verify in-world
- Vertex colours assume Rec Room renders linear (jt_materials 4c.1 caveat): a c=0.15 Hallway2 wall should read mid-dark grey.
- AG/Standard-Opaque-Instanced (Curtain_Red_Mat template) not disassembled: treated as not reading vc (curtains have no c=).
- Level smoothness (sm) values not applied: the AG shaders take gloss from _SpecGlossMap only.
- Wallpaper_SciFiPanels_Big -> plain Metal_LightGrey_Dull_Mat (the level's 5 m panel seams are lost); option: door panel 5 m.
- Dress door-panel tile 1.5 m, Mirror_Quest (Metal_Grey_Gloss tint 0.3), 80Sunset (orange glow 1), Clock_Glass tint are my picks.
- Rock_Boulder_Mat is not in the install index (despite "inst") -> Rocks_Tiling_Background_Mat by the name rule.
- StageTop_mat's 8,860 slivers are all kept (one mesh).
- Kit '?' glow props (LightShaft, laser trails, PropBox_SciFi, explosive barrels) are the kit's renderers, not the hull's.
