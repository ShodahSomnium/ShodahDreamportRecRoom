# Iteration 10 - Jumbotron material map (agent: materials)

Goal: a Golden-Trophy-style Plan() for the Jumbotron hull: each level material -> closest INSTALL AG material
(template) + install texture + measured tint + tiling, so RrLightGrid lights it. Data-only analysis, no CODE edits.
Scratch: NSP/jt10/materials/

## TL;DR (for the implementer of the Jumbotron Plan())
- 101 level materials on 63,503 plates / 122,571 m2 (surfaces 54,325 / 113,546 m2) + 1,233 dress boxes. 18 materials
  cover 94% of the surface area; 136 of 222 level materials are AG/Standard-Opaque (the shader RrLightGrid lights).
- Four measured causes of today's flat violet look (sec. 4c): (1) the VertColor tints (c=) are gamma-decoded by
  SetColor (Rec Room multiplies them raw/linear): walls 3-8x too dark, the cinderblock blue over-saturated; (2) the
  props' pink/cyan _BaseColor is masked by texture alpha (~1% of texels): the Pillar_SciFi pillars are grey 0.38 with
  thin pink strips, not solid pink; (3) _BaseColor.a 0.354 on opaque AG materials is not transparency (cover walls,
  wall decor, vents drawn as glass = the see-through boxes); (4) plate tiling must use the measured UV density.
- Plan() in one paragraph: every surface = clone of an INSTALL AG/Standard-* template (grid-lit), install texture
  (same name where it exists; Concrete_Tex + Brick_Norm/Brick_Spec for the missing Brick_Tex; Tile_Linoleum_Flecked_Tex
  for Linoleum_Flecked_Tex; Door_Scifi_Cardboard_Tex for the missing sci-fi panel textures), the level's _BaseColor
  (or a measured tint when the substitute's mean differs), the level's _Smoothness, _EmissivePower, c= as raw mesh
  vertex colours, tile = 1/(UV density x level scale). Transparency only from the shader. Table: sec. 5.


## 1. Level materials used by plates/dress (counts summed over 11 rooms)
Source: `^G plate` records of the 11 room RRSKs (Jumbotron_Additive has none). Area = 4*hx*hy (e= are HALF extents;
gtscene's header m2 = this). Totals: 63,503 plates / 122,571 m2; of which surfaces (not fog/portal) 54,325 / 113,546 m2.
Counting plates is misleading (StageTop_mat = 8,860 plates but 405 m2 of tiny stage-edge slivers); cumulative % is by AREA.
StadiumEntry: gtscene's 1.16 M backdrop plates were already cut to 5.1 k in iteration 8 (jumbotron.md).
Full data: NSP/jt10/materials/mat_area.tsv (all 101 materials, rooms), surf_area.txt.

| material | plates | m2 | cum % (area, surfaces) | platemat |
| --- | --- | --- | --- | --- |
| Wall_Vertcolor_mat | 2227 | 24432 | 21.5% | col=1,1,1,1 tex=Concrete_Tex scale=25,25 inst=0 |
| Cinderblock_VertColor_Mat | 1986 | 23479 | 42.2% | col=1,1,1,1 tex=Brick_Tex scale=2,2 inst=0 |
| Paint_DarkGrey_Mat | 342 | 11471 | 52.3% | col=0.33,0.33,0.33,1 tex=Paint_Tex scale=50,50 inst=1 |
| Linoleum_Flecked_Brown_Mat | 1751 | 9605 | 60.8% | col=0.578,0.547,0.529,1 tex=Linoleum_Flecked_Tex scale=1,1 inst=0 |
| Concrete_LightGrey_2x_Mat | 1536 | 8976 | 68.7% | col=0.934,0.934,0.934,1 tex=Paint_Tex scale=2,2 inst=1 |
| Unlit_Black_mat | 205 | 3896 | 72.1% | col=1,1,1,1 tex=- scale=1,1 inst=1 |
| Metal_Corrugated_Blue_Mat | 137 | 3529 | 75.2% | col=0.085,0.334,0.462,1 tex=Metal_Corrugated_Tex scale=1,0.5 inst=0 |
| Concrete_VertColor_Mat | 737 | 3048 | 77.9% | col=0.934,0.934,0.934,1 tex=Paint_Tex scale=2,2 inst=0 |
| Brick_Painted_LightGrey_Mat | 94 | 2760 | 80.3% | col=0.985,0.985,0.985,1 tex=Brick_Tex scale=0.5,0.5 inst=0 |
| Concrete_Grey_3x_Mat | 268 | 2722 | 82.7% | col=0.824,0.824,0.824,1 tex=Concrete_Tex scale=3,3 inst=0 |
| Metal_Dull_Dark_Mat | 1666 | 2117 | 84.6% | col=0.01,0.031,0.083,1 tex=Metal_Tex scale=4,4 inst=1 |
| Brick_Painted_Grey_Mat | 68 | 1975 | 86.3% | col=0.666,0.666,0.666,1 tex=Brick_Tex scale=0.5,0.5 inst=0 |
| Wallpaper_SciFiPanels_Big_Mat | 1491 | 1893 | 88.0% | col=1,1,1,1 tex=Wallpaper_SciFiPanels_Tex scale=0.2,0.2 inst=0 |
| Wood_Table_Mat | 2803 | 1736 | 89.5% | col=0.686,0.455,0.231,1 tex=wood_grain_Tex scale=2,3 inst=1 |
| Metal_LightGrey_Dull_Mat | 1049 | 1566 | 90.9% | col=1,1,1,1 tex=Metal_Tex scale=4,4 inst=1 |
| Door_SciFi_Cardboard_mat | 4357 | 1429 | 92.2% | col=0,1,1,0.354 tex=Door_Scifi_Cardboard_Tex scale=1,1 inst=1 |
| Linoleum_Flecked_Brown_Mat_FloorQueue | 1 | 1296 | 93.3% | col=0.578,0.547,0.529,1 tex=Linoleum_Flecked_Tex scale=1,1 inst=0 |
| Pillar_SciFi_Pink_mat | 1842 | 945 | 94.1% | col=1,0.392,0.946,1 tex=Pillar_SciFi_tex scale=1,1 inst=0 |
| Concrete_PaleBlue_200x_Mat | 6156 | 823 | 94.8% | col=0.734,0.883,1,1 tex=Concrete_Tex scale=200,200 inst=0 |
| Metal_FloorTread_Mat | 2321 | 603 | 95.4% | col=1,1,1,1 tex=Metal_FloorTread_Tex scale=1,1 inst=0 |
| Wood_Floor_Matte_mat | 9 | 600 | 95.9% | col=1,0.735,0.491,1 tex=Wood%20FloorTiled_tex scale=10,40 inst=1 |
| Plastic_Grey_Gloss_Mat | 1234 | 471 | 96.3% | col=0.591,0.582,0.535,1 tex=Plastic_Tex scale=2,2 inst=1 |
| Curtain_Black_Mat | 3011 | 425 | 96.7% | col=0.245,0.245,0.245,1 tex=Curtains_Tex scale=1,1 inst=0 |
| StageTop_mat | 8860 | 405 | 97.1% | col=0.566,0.386,0.117,1 tex=StageTop_tex scale=1,1 inst=0 |
| Wood_Floor_Wide_mat | 194 | 402 | 97.4% | col=0.578,0.46,0.395,1 tex=Wood%20FloorTiled_tex scale=0.2,0.75 inst=0 |
| Metal_Dull_Mat | 903 | 372 | 97.7% | col=0.596,0.596,0.596,1 tex=Metal_Tex scale=3,3 inst=0 |
| Scifi_Rug_Mat | 890 | 343 | 98.0% | col=1,1,1,1 tex=Scifi_Rug_Tex scale=1,1 inst=1 |
| Metal_Ribbed_Gray_mat | 353 | 327 | 98.3% | col=0.599,0.646,0.679,1 tex=Metal_Ribbed_Tex scale=0.25,1 inst=0 |
| GarageMarkings_Mat | 327 | 296 | 98.6% | col=1,1,1,1 tex=GarageMarkings_tex scale=1,1 inst=0 |
| Vent_Modular_Mat | 522 | 198 | 98.8% | col=0.838,0.906,1,1 tex=Vent_Modular_tex scale=1,1 inst=1 |
| Pillar_SciFi_Red_mat | 313 | 169 | 98.9% | col=1,0.108,0.454,1 tex=Pillar_SciFi_tex scale=1,1 inst=0 |
| Carpet_Blacklight_Swirls_mat | 1 | 138 | 99.0% | col=1,1,1,1 tex=Carpet_Blacklight_Swirls_Tex scale=0.5,0.5 inst=0 |
| SciFi_DoorTrigger_Mat | 682 | 128 | 99.1% | col=1,0,0.269,1 tex=DoorTrigger_Tex scale=1,1 inst=1 |
| 80Sunset_mat | 1 | 123 | 99.3% | col=0,0,0,1 tex=Sunset_Tex scale=1,1 inst=0 |
| Wallpaper_Dark_Mat | 48 | 88 | 99.3% | col=0.182,0.182,0.182,1 tex=Paint_Tex scale=1,1 inst=0 |
| MetalBarrel_Green_Mat | 1213 | 82 | 99.4% | col=0.148,0.669,0.475,1 tex=MetalBarrel_Tex scale=1,1 inst=0 |
| Enemy_Jumbotron_Mat | 434 | 75 | 99.5% | col=1,0.241,0.558,1 tex=Jumbotron_Tex scale=1,1 inst=0 |
| Wood_Planks_Mat | 392 | 72 | 99.5% | col=1,1,1,1 tex=Wood_Planks scale=1.5,1.5 inst=0 |
| Crane_Red_Mat | 747 | 72 | 99.6% | col=1,1,1,1 tex=Crane_Red_Tex scale=1,1 inst=0 |
| Paint_Red_Mat | 50 | 71 | 99.7% | col=0.915,0.228,0.117,1 tex=Paint_Tex scale=100,100 inst=0 |
| Wall_SciFi_Decor_Blue_mat | 1711 | 66 | 99.7% | col=0.114,0.68,0.961,0.354 tex=Wall_SciFi_Decor_Blue_Tex scale=1,1 inst=0 |
| Metal_Grey_Gloss_Mat | 344 | 53 | 99.8% | col=1,1,1,1 tex=Metal_Tex scale=4,4 inst=1 |
| SciFi_Emit_Cyan_Mat | 6 | 45 | 99.8% | col=0,0.586,1,1 tex=- scale=1,1 inst=0 |
| WallSign_Red_Mat | 195 | 42 | 99.8% | col=1,0.074,0.074,1 tex=WallSign_Tex scale=1,1 inst=0 |
| Wires_Mat | 4 | 35 | 99.9% | col=1,1,1,1 tex=Wires_Tex scale=1,1 inst=0 |
| Terminal_SciFi_Prop_mat | 82 | 28 | 99.9% | col=1,1,1,1 tex=Terminal_SciFi_Prop_Tex scale=1,1 inst=0 |
| Plaster_Black_mat | 4 | 24 | 99.9% | col=0,0,0,1 tex=Plaster_tex scale=15,15 inst=1 |
| Wood_Horizontal_DarkOrange_5x_Mat | 60 | 19 | 99.9% | col=0.51,0.345,0.271,1 tex=wood_grain_Tex scale=5,5 inst=1 |
| Paintball_TractorTire_Mat | 202 | 16 | 99.9% | col=1,1,1,1 tex=Paintball_TireStack_Tex scale=1,1 inst=1 |
| Banner_Gym_Shield_Mat | 104 | 14 | 100.0% | col=0.957,0.603,0.41,1 tex=GymPosterShield scale=1,1 inst=0 |
| Wood_Horizontal_Orange_Mat | 56 | 12 | 100.0% | col=0.844,0.553,0.219,1 tex=wood_grain_Tex scale=2,3 inst=1 |
| SciFi_Emit_Blue_Mat | 47 | 8 | 100.0% | col=0.189,0.704,1,1 tex=- scale=1,1 inst=0 |
| EventRoom_Moon_mat | 33 | 7 | 100.0% | col=0.965,1.027,1.498,1 tex=Moon_Tex scale=1,1 inst=1 |
| Concrete_Barrier_Mat | 96 | 5 | 100.0% | col=1,1,1,1 tex=Concrete_Barier_Tex scale=1,1 inst=1 |
| WallSign_Blue_Mat | 13 | 3 | 100.0% | col=0.287,0.616,1,1 tex=WallSign_Tex scale=1,1 inst=0 |
| Speaker_mat | 18 | 3 | 100.0% | col=0.5,0.5,0.5,1 tex=Speaker_Tex scale=1,1 inst=1 |
| Couch_Plush_Purple_Mat | 35 | 3 | 100.0% | col=1,1,1,1 tex=Couch_Plush_Purple_tex scale=1,1 inst=0 |
| Barrel_Table_Crate_Mat | 4 | 3 | 100.0% | col=1,1,1,1 tex=Barrel_Table_Crate_Tex scale=1,1 inst=1 |
| Poster_ShakeHands_Mat | 1 | 2 | 100.0% | col=1,1,1,1 tex=Shake%20hands%20poster scale=1,1 inst=1 |
| Concrete_MutedBlue_50x_Mat | 87 | 0 | 100.0% | col=0.529,0.613,0.697,1 tex=Concrete_Tex scale=50,50 inst=0 |
| Charades_Blue_Mat | 2 | 0 | 100.0% | col=0.325,0.62,0.914,1 tex=Concrete_Tex scale=50,50 inst=0 |
| surface | total | plates | 54325 | |

Special / not-surface materials by area (plates m2): Portal_Blue_Concession 1294/1481, Portal_Red_Stadium 1/1470,
Portal_Pink_Concession 1045/1362, Portal_Cyan_Concession 1/729, Portal_Blue_Garage 3/651, Scifi_AreaFog_Cyan 4467/544,
Portal_Pink_Foyer 1/483, Portal_Cyan_StadiumEntry 5/473, Portal_Purple_StadiumEntry 2/336, Portal_Blue 4/269, ...,
Scifi_AreaFog_Purple 1147/110, Scifi_AreaFog_Pink 778/71, Scifi_AreaFog_Red 278/24 (see section 6).

### Dress records (1,233 over 11 rooms; `?` = material not in the install)
312 ?Wall_SciFi_Cover_Modular_mat (Quest_SciFi_CoverWall_End 203 / _Mid 109, c=1,1,1,0.354), 199 ?Metal_Dull_Mat
(Scaffolding_6M 193), 118 ?Terminal_SciFi_Prop_mat (terminals), 97 ?Wall_SciFi_Decor_Blue_mat (wall_LOD0 50, bits_LOD0 47,
c=0.114,0.680,0.961,0.354), 81 Rock_Boulder_Mat (rockvox), 60 ?Shipping_Container_Instanced_mat (containers + their doors),
50/34/27 ?SciFi_CoverWall03/02/01_mat (Cover_Wall + Cover_Glass, c=0.294,0.883,1,1), 42 ?GarageMarkings_Mat (drips, oil
spots, arrows), 33 ?Wires_Mat, 32 ?WallVent_mat (c=1,1,1,0.354), 28 ?SciFi_CoverSideWall_Glass_mat, 23 ?Sandbag_Mat,
11 Speaker_mat (Quest_SciFi_Lazer), 11 LockerFountainStageLight_mat, 11 ?StageLight_Glow_RedOrange_Mat (Quad), 10 LockIcon_Mat,
9+9 clock (?Goblet_Clock_Mat, ?Clock_Glass_Mat a=0.082), 8+3+3 books, 7 ?Speaker_Intercom_mat (a=0.354), 5 LanternPost_Mat, ...
Data: NSP/jt10/materials/dress.txt.

**Alpha 0.354 is NOT transparency.** Door_SciFi_Cardboard_mat, Wall_SciFi_Decor_Blue_mat, Wall_SciFi_Cover_Modular_mat,
WallVent_mat, Speaker_Intercom_mat, Dropship_Prop_Mat all carry _BaseColor.a = 0.354 on the OPAQUE shader
AG/Standard-Opaque (queue 2000, _Surface 0, ZWrite 1). RrJumbotronHull treats col.a < 0.99 as RrLook.Glass (plates:
MaterialFor line 436; dress: line 347) -> the 312 cover walls + 97 wall decor + 32 vents + 7 speakers are drawn as
see-through glass boxes. Only a real transparent shader (AG/Standard-Transparent*, AG/Unlit-Additive, Fresnel) may be glass.


## 2. Vertex-colour tints (c=) for *VertColor* materials
gtscene writes c= only when the face's vertex colour is not white (Gt.cs l.287), so "no c=" = white (1,1,1).
Area-weighted over the 11 rooms, tints rounded to 1/20 like RrJumbotronHull does (m2):

| material | tint (vertex colour) | m2 | share | rooms (m2) |
| --- | --- | --- | --- | --- |
| Wall_Vertcolor_mat (24,432 m2) | 0.15,0.15,0.15 (dark grey) | 11,495 | 47% | StadiumConcession 6672, Hallway2 3509, Reception 1283, Foyer 32 |
| | white | 8,625 | 35% | Reception 4406, Hallway2 4149, Stadium 71 |
| | 0.05,0.05,0.05 (near black) | 2,594 | 11% | Hallway1 |
| | 0.45,0.45,0.45 | 1,095 | 4% | Hallway1 981, Armory 114 |
| | 0.65,0.65,0.65 | 623 | 3% | Stadium |
| Cinderblock_VertColor_Mat (23,479 m2) | 0.20,0.50,0.70 (steel blue) | 10,609 | 45% | StadiumEntry 9399, Foyer 1210 |
| | 0.65,0.65,0.65 | 5,018 | 21% | Reception 1839, StadiumConcession 1633, Hallway1 1371, Foyer 174 |
| | white | 4,679 | 20% | Hallway2 2588, StadiumEntry 1873, Reception 218 |
| | 0.55,0.10,0.05 (brick red) | 1,585 | 7% | StadiumEntry 966, StadiumConcession 378, Hallway1 241 |
| | 0.45,0.45,0.45 | 1,368 | 6% | Stadium |
| | 0.05,0.45,0.10 (green) | 205 | 1% | Armory |
| Concrete_VertColor_Mat (3,048 m2) | white | 1,311 | 43% | Cargo1 803, Hallway2 507 |
| | 0.65,0.70,0.70 | 983 | 32% | Cargo1 837, Hallway2 146 |
| | 0.70,0.75,0.75 | 576 | 19% | Cargo1 |
| | 0.90,0.90,0.90 / 0.75 / 0.95 | 173 | 6% | Cargo1 |
| Paint_DarkGrey_Mat (inst=1, 11,471 m2) | white | 6,856 | 60% | Garage 4770, Battlefield1 1441, Hallway2 645 |
| | 0.20,0.50,0.70 | 3,684 | 32% | StadiumEntry |
| | 0.15,0.15,0.15 | 931 | 8% | Foyer (the ceiling / upper walls) |
| Wallpaper_SciFiPanels_Big / Brick_Painted_LightGrey / Metal_FloorTread | a few c= plates | 372 / 161 / 76 | | |

The values are the mesh's raw vertex colours (Color32/255, the numbers the shader sees - not converted to linear).

**How Rec Room uses the vertex colour (shdis, install AG/Standard-* forward pixel shader, NSP/shdis/agstd
p4_s1_55674E.asm / p4_s1_5544E2.asm):**
```
r0 = float4(v6.xyz /*TEXCOORD9 = VS o4 = mesh COLOR*/, 1) * cb3[4]      // vertex colour x _BaseColor (UnityPerMaterial[4])
r1 = _BaseMap sample
albedo = r1.rgb + r1.a * (r0.rgb * r1.rgb - r1.rgb)  = tex.rgb * lerp(1, vc * _BaseColor, tex.a)
```
So in AG/Standard-Opaque the vertex colour always multiplies the tint (no keyword: Cinderblock_VertColor_Mat has the same
keywords as Concrete_PaleBlue_200x_Mat) and BOTH are masked by the base texture's ALPHA (alpha 0 = texture colour shown
untinted; Rec Room's paint-mask trick). Consequences for the runtime:
- The VertColor walls can use the INSTALL AG template directly with the c= tint as MESH VERTEX COLOURS (mesh.colors)
  on our plate meshes: one grid-lit install material per level material, no per-tint material, and the tint is applied
  exactly like Rec Room (x tex.a). Plates without c= must get white vertex colours explicitly (never rely on a
  missing COLOR stream).
- _BaseColor tints (col= of platemat) are likewise masked by tex.a: a substitute texture with a different alpha than the
  level texture tints differently. Measured alpha means are listed in section 3.


## 3. APK material details (shader, keywords, colours, textures, avg texel colour)
`apkscene mat <work> levelN .` on levels 38-48 (NSP/jt10/materials/mat_level*.txt, compact: matsum.txt) + my scratch tool
NSP/jt10/materials/texavg (compiled with the apkscene sources, not Main.cs; decodes one ~64 px ASTC mip of each level
texture and prints only its MEAN colour; nothing else is kept). "mean" = plain mean of the stored (sRGB) texel values;
a = mean alpha (AG: "_BaseMap = Albedo(RGB) Emissive(A)", see sec. 2 math: the tint is lerped in by a).
Short names: Std- = AG/Standard-, MSG = _METALLICSPECGLOSSMAP, N = _NORMALMAP, FAST = _FAST_MODE_SHADING; emis = _EmissionColor x _EmissivePower.
Full lines (all 109 used materials, incl. _TintColor, smoothness, metallic): NSP/jt10/materials/matfull_sorted.tsv.

| material | plates/m2 | dress | shader [kw] | _BaseColor | emis x power | _BaseMap x scale | | sm / met | tex mean sRGB, alpha |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Wall_Vertcolor_mat | 2227p/24432m2 | 0 | Std-Opaque [FAST MSG N] | 1,1,1,1 | 0,0,0,1x0 | Concrete_Tex x25,25 |  sm=0.774 met=0 | 0.734,0.736,0.737 a=1.000 |
| Cinderblock_VertColor_Mat | 1986p/23479m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Brick_Tex x2,2 |  sm=0.177 met=0 | 0.735,0.735,0.735 a=1.000 |
| Paint_DarkGrey_Mat | 342p/11471m2 | 0 | Std-Opaque [MSG N] | 0.3302,0.3302,0.3302,1 | 0,0,0,1x0 | Paint_Tex x50,50 |  sm=0.897 met=0 | 0.811,0.811,0.811 a=1.000 |
| Linoleum_Flecked_Brown_Mat | 1751p/9605m2 | 0 | Std-Opaque [MSG N] | 0.578,0.5465,0.5294,1 | 0,0,0,1x0 | Linoleum_Flecked_Tex x1,1 |  sm=0.85 met=0 | 0.511,0.511,0.511 a=1.000 |
| Concrete_LightGrey_2x_Mat | 1536p/8976m2 | 0 | Std-Opaque [FAST MSG N] | 0.934,0.934,0.934,1 | 0,0,0,1x0 | Paint_Tex x2,2 |  sm=0.901 met=0 | 0.811,0.811,0.811 a=1.000 |
| Unlit_Black_mat | 205p/3896m2 | 0 | AG/Unlit-Color [] | - | 0,0,0,1x1 | - |  sm=- met=0 | - |
| Metal_Corrugated_Blue_Mat | 137p/3529m2 | 0 | Std-Opaque [MSG N] | 0.085,0.3343,0.4623,1 | 0,0,0,1x0 | Metal_Corrugated_Tex x1,0.5 |  sm=0.177 met=0 | 0.902,0.902,0.902 a=1.000 |
| Concrete_VertColor_Mat | 737p/3048m2 | 0 | Std-Opaque [FAST MSG N] | 0.934,0.934,0.934,1 | 0,0,0,1x0 | Paint_Tex x2,2 |  sm=0.901 met=0 | 0.811,0.811,0.811 a=1.000 |
| Brick_Painted_LightGrey_Mat | 94p/2760m2 | 0 | Std-Opaque [FAST MSG N] | 0.9853,0.9853,0.9853,1 | 0,0,0,1x0 | Brick_Tex x0.5,0.5 |  sm=0.3 met=0 | 0.735,0.735,0.735 a=1.000 |
| Concrete_Grey_3x_Mat | 268p/2722m2 | 0 | Std-Opaque [MSG N] | 0.8235,0.8235,0.8235,1 | 0,0,0,1x0 | Concrete_Tex x3,3 |  sm=0.662 met=0 | 0.734,0.736,0.737 a=1.000 |
| Metal_Dull_Dark_Mat | 1666p/2117m2 | 0 | Std-Opaque [FAST MSG N] | 0.0103,0.031,0.0828,1 | 0,0,0,1x0 | Metal_Tex x4,4 |  sm=0.654 met=0 | 0.384,0.384,0.384 a=1.000 |
| Brick_Painted_Grey_Mat | 68p/1975m2 | 0 | Std-Opaque [MSG N] | 0.666,0.666,0.666,1 | 0,0,0,1x0 | Brick_Tex x0.5,0.5 |  sm=0.5 met=0 | 0.735,0.735,0.735 a=1.000 |
| Wallpaper_SciFiPanels_Big_Mat | 1491p/1893m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Wallpaper_SciFiPanels_Tex x0.2,0.2 |  sm=0.768 met=0 | 0.381,0.381,0.381 a=1.000 |
| Wood_Table_Mat | 2803p/1736m2 | 0 | Std-Opaque [MSG N] | 0.6863,0.4549,0.2314,1 | 0,0,0,1x0 | wood_grain_Tex x2,3 |  sm=1 met=0 | 0.797,0.797,0.797 a=1.000 |
| Metal_LightGrey_Dull_Mat | 1049p/1566m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Metal_Tex x4,4 |  sm=0.81 met=0 | 0.384,0.384,0.384 a=1.000 |
| Portal_Blue_Concession_Mat | 1294p/1481m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.0726,0.1813,0.5809,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Red_Stadium_Mat | 1p/1470m2 | 0 | Shader Forge/Particles/PortalFogFresnel [] | 1,0,0.354,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Door_SciFi_Cardboard_mat | 4357p/1429m2 | 0 | Std-Opaque [MSG N] | 0,1,1,0.354 | 0,1,1,1x2.37 | Door_Scifi_Cardboard_Tex x1,1 |  sm=0.855 met=0.85 | 0.339,0.338,0.335 a=0.014 |
| Portal_Pink_Concession_Mat | 1045p/1362m2 | 0 | Shader Forge/Particles/PortalFog [] | 1,0.3464,0,0.878 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Linoleum_Flecked_Brown_Mat_FloorQueue | 1p/1296m2 | 0 | Std-Opaque [MSG N] | 0.578,0.5465,0.5294,1 | 0,0,0,1x0 | Linoleum_Flecked_Tex x1,1 |  sm=0.85 met=0 | 0.511,0.511,0.511 a=1.000 |
| Pillar_SciFi_Pink_mat | 1842p/945m2 | 0 | Std-Opaque [MSG N] | 1,0.3915,0.9463,1 | 0.9338,0.2531,0.8305,1x0.5 | Pillar_SciFi_tex x1,1 |  sm=1 met=0.85 | 0.376,0.375,0.374 a=0.011 |
| Concrete_PaleBlue_200x_Mat | 6156p/823m2 | 0 | Std-Opaque [MSG N] | 0.734,0.8835,1,1 | 0,0,0,1x0 | Concrete_Tex x200,200 |  sm=0.309 met=0 | 0.734,0.736,0.737 a=1.000 |
| Portal_Cyan_Concession_Mat | 1p/729m2 | 0 | Shader Forge/Particles/PortalFogFresnel [] | 0,0.9172,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Blue_Garage_Mat | 3p/651m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.038,0.6766,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Metal_FloorTread_Mat | 2321p/603m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Metal_FloorTread_Tex x1,1 |  sm=0.88 met=0 | 0.313,0.339,0.382 a=0.384 |
| Wood_Floor_Matte_mat | 9p/600m2 | 0 | Std-Opaque [MSG N] | 1,0.7347,0.491,1 | 0,0,0,1x0 | - |  sm=0.541 met=0 | Wood FloorTiled_tex 512x512 fmt51 mip3=64x64 srgbmean 0.722,0.722,0.722 a 1.000 lin->srgb 0.724,0.724,0.724 lumaRange 0.64-0.83 cs1 |
| Scifi_AreaFog_Cyan_Mat | 4467p/544m2 | 0 | Shader Forge/Particles/FogSin [] | 0,0.4471,0.553,1 | 0,0,0,1x- | - | tint=1,1,1,1 sm=- met=0 | - |
| Portal_Pink_Foyer_Mat | 1p/483m2 | 0 | Shader Forge/Particles/PortalFog [] | 1,0,0.7655,0.878 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Cyan_StadiumEntry_Mat | 5p/473m2 | 0 | Shader Forge/Particles/PortalFog [] | 0,0.715,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Plastic_Grey_Gloss_Mat | 1234p/471m2 | 0 | Std-Opaque [MSG N] | 0.591,0.5824,0.5345,1 | 0,0,0,1x0 | Plastic_Tex x2,2 |  sm=0.832 met=0 | 0.885,0.885,0.885 a=1.000 |
| Curtain_Black_Mat | 3011p/425m2 | 0 | Std-Opaque [MSG N] | 0.2453,0.2453,0.2453,1 | 0,0,0,1x0 | Curtains_Tex x1,1 |  sm=0.5 met=0 | 0.932,0.907,0.869 a=0.851 |
| StageTop_mat | 8860p/405m2 | 0 | Std-Opaque [N] | 0.5662,0.3863,0.1166,1 | 0,0,0,1x0 | StageTop_tex x1,1 |  sm=0.5 met=0 | 0.479,0.479,0.479 a=1.000 |
| Wood_Floor_Wide_mat | 194p/402m2 | 0 | Std-Opaque [MSG N] | 0.578,0.4595,0.3952,1 | 0,0,0,1x0 | - |  sm=0.32 met=0 | Wood FloorTiled_tex 512x512 fmt51 mip3=64x64 srgbmean 0.722,0.722,0.722 a 1.000 lin->srgb 0.724,0.724,0.724 lumaRange 0.64-0.83 cs1 |
| Metal_Dull_Mat | 903p/372m2 | 199 | Std-Opaque [MSG N] | 0.5956,0.5956,0.5956,1 | 0,0,0,1x0 | Metal_Tex x3,3 |  sm=0.177 met=0 | 0.384,0.384,0.384 a=1.000 |
| Scifi_Rug_Mat | 890p/343m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,0x0 | Scifi_Rug_Tex x1,1 |  sm=0.283 met=0 | 0.224,0.246,0.273 a=1.000 |
| Portal_Purple_StadiumEntry_Mat | 2p/336m2 | 0 | Shader Forge/Particles/PortalFogFresnel [] | 0.3448,0.038,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Metal_Ribbed_Gray_mat | 353p/327m2 | 0 | Std-Opaque [MSG N] | 0.5991,0.6459,0.6792,1 | 0,0,0,1x0 | Metal_Ribbed_Tex x0.25,1 |  sm=0.177 met=0 | 0.796,0.796,0.796 a=1.000 |
| GarageMarkings_Mat | 327p/296m2 | 42 | Std-Transparent [MSG N] | 1,1,1,1 | 0,0,0,1x0 | GarageMarkings_tex x1,1 |  sm=0.923 met=0 | 0.662,0.613,0.520 a=0.418 |
| Portal_Blue_Mat | 4p/269m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.038,0.2785,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Vent_Modular_Mat | 522p/198m2 | 0 | Std-Opaque [MSG N] | 0.8382,0.9063,1,1 | 0,0,0,1x0 | Vent_Modular_tex x1,1 |  sm=1 met=0 | 0.396,0.376,0.366 a=0.482 |
| Portal_Purple_StadiumEntry2_Mat | 2p/173m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.356,0,1,0.297 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Pillar_SciFi_Red_mat | 313p/169m2 | 0 | Std-Opaque [MSG N] | 1,0.1085,0.4538,1 | 0.9338,0.084,0.2217,1x0.4 | Pillar_SciFi_tex x1,1 |  sm=0.799 met=0.85 | 0.376,0.375,0.374 a=0.011 |
| Portal_Red_Hallway2_Mat | 1p/160m2 | 0 | Shader Forge/Particles/PortalFog [] | 1,0,0.354,0.616 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Cyan_Mat | 140p/145m2 | 0 | Shader Forge/Particles/PortalFog [] | 0,0.9172,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Carpet_Blacklight_Swirls_mat | 1p/138m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0.751,0.751,0.751,1x1 | Carpet_Blacklight_Swirls_Tex x0.5,0.5 |  sm=0.015 met=0 | 0.103,0.133,0.299 a=0.279 |
| SciFi_DoorTrigger_Mat | 682p/128m2 | 0 | Std-Opaque [N] | 1,0,0.269,1 | 1,0,0.269,1x1.37 | DoorTrigger_Tex x1,1 |  sm=0.693 met=0 | 0.251,0.249,0.243 a=0.021 |
| 80Sunset_mat | 1p/123m2 | 0 | Std-Transparent-Emission [] | 0,0,0,1 | 2,2,2,1x0 | Sunset_Tex x1,1 |  sm=1 met=0 | 0.421,0.211,0.044 a=0.420 |
| Scifi_AreaFog_Purple_Mat | 1147p/110m2 | 0 | Shader Forge/Particles/FogSin [] | 0.1817,0.0925,0.2206,1 | 0,0,0,1x- | - | tint=1,1,1,1 sm=- met=0 | - |
| Portal_Purple_Concession_Mat | 1p/108m2 | 0 | Shader Forge/Particles/PortalFogFresnel [] | 1,0.4345,0,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Purple_Hallway2_Mat | 1p/106m2 | 0 | Shader Forge/Particles/PortalFog [] | 0,0.454,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Purple_Cargo_Mat | 2p/90m2 | 0 | Shader Forge/Particles/PortalFogFresnel [] | 0.3824,0,0.8529,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Wallpaper_Dark_Mat | 48p/88m2 | 0 | Std-Opaque [MSG N] | 0.182,0.182,0.182,1 | 0,0,0,1x0 | Paint_Tex x1,1 |  sm=0.357 met=0 | 0.811,0.811,0.811 a=1.000 |
| MetalBarrel_Green_Mat | 1213p/82m2 | 0 | Std-Opaque [MSG N] | 0.1476,0.6691,0.4749,1 | 0,0,0,1x0 | MetalBarrel_Tex x1,1 |  sm=0.963 met=0 | 0.587,0.587,0.587 a=0.862 |
| Enemy_Jumbotron_Mat | 434p/75m2 | 0 | Std-Opaque [MSG N] | 1,0.2406,0.5579,1 | 3,0,2.142,1x3 | Jumbotron_Tex x1,1 |  sm=0.868 met=0 | 0.261,0.228,0.240 a=0.020 |
| Portal_Red_Mat | 1p/74m2 | 0 | Shader Forge/Particles/PortalFog [] | 1,0,0,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Wood_Planks_Mat | 392p/72m2 | 0 | Std-Opaque [FAST N] | 1,1,1,1 | 0,0,0,1x0 | Wood_Planks x1.5,1.5 |  sm=0.5 met=0 | 0.432,0.288,0.119 a=1.000 |
| Crane_Red_Mat | 747p/72m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Crane_Red_Tex x1,1 |  sm=0.177 met=0 | 0.498,0.266,0.182 a=1.000 |
| Scifi_AreaFog_Pink_Mat | 778p/71m2 | 0 | Shader Forge/Particles/FogSin [] | 0.781,0.1687,0.6216,1 | 0,0,0,1x- | - | tint=1,1,1,1 sm=- met=0 | - |
| Paint_Red_Mat | 50p/71m2 | 0 | Std-Opaque [MSG N] | 0.9151,0.2285,0.1165,1 | 0,0,0,1x0 | Paint_Tex x100,100 |  sm=0.309 met=0 | 0.811,0.811,0.811 a=1.000 |
| Wall_SciFi_Decor_Blue_mat | 1711p/66m2 | 97 | Std-Opaque [MSG N] | 0.1137,0.6801,0.9608,0.354 | 0.5515,0.833,1,1x1.24 | Wall_SciFi_Decor_Blue_Tex x1,1 |  sm=0 met=0.85 | 0.436,0.436,0.437 a=0.004 |
| Portal_Purple_Hallway2_Faint_Mat | 1p/65m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.3824,0,0.8529,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Portal_Pink_Mat | 1p/54m2 | 0 | Shader Forge/Particles/PortalFog [] | 1,0,0.7655,0.878 | 0,0,0,1x- | - |  sm=- met=0 | - |
| Metal_Grey_Gloss_Mat | 344p/53m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Metal_Tex x4,4 |  sm=0.874 met=0 | 0.384,0.384,0.384 a=1.000 |
| SciFi_Emit_Cyan_Mat | 6p/45m2 | 0 | Std-Opaque [MSG N] | 0,0.5862,1,1 | 0,1.9239,3.282,1x10 | - |  sm=1 met=0 | - |
| Portal_Blue_Cargo1_Mat | 2p/44m2 | 0 | Shader Forge/Particles/PortalFog [] | 0.038,0.2785,1,1 | 0,0,0,1x- | - |  sm=- met=0 | - |
| WallSign_Red_Mat | 195p/42m2 | 0 | Std-Opaque [MSG N] | 1,0.0735,0.0735,1 | 1,0.0735,0.0735,1x9 | WallSign_Tex x1,1 |  sm=0.187 met=0 | 0.637,0.637,0.637 a=0.422 |
| Wires_Mat | 4p/35m2 | 33 | Std-Transparent [] | 1,1,1,1 | 0,0,0,1x0 | Wires_Tex x1,1 |  sm=0.426 met=0 | 0.209,0.209,0.209 a=0.103 |
| Terminal_SciFi_Prop_mat | 82p/28m2 | 118 | Std-Opaque [MSG N] | 1,1,1,1 | 1,1,1,1x1 | Terminal_SciFi_Prop_Tex x1,1 |  sm=0.541 met=0.85 | 0.448,0.461,0.453 a=0.074 |
| Scifi_AreaFog_Red_Mat | 278p/24m2 | 0 | Shader Forge/Particles/FogSin [] | 0.766,0.1655,0.1655,1 | 0,0,0,1x- | - | tint=1,1,1,1 sm=- met=0 | - |
| Plaster_Black_mat | 4p/24m2 | 0 | Std-Opaque [MSG N] | 0,0,0,1 | 0,0,0,1x0 | Plaster_tex x15,15 |  sm=0.5 met=0 | 0.971,0.971,0.971 a=1.000 |
| Wood_Horizontal_DarkOrange_5x_Mat | 60p/19m2 | 0 | Std-Opaque [MSG N] | 0.5098,0.3451,0.2706,1 | 0,0,0,1x0 | wood_grain_Tex x5,5 |  sm=1 met=0 | 0.797,0.797,0.797 a=1.000 |
| Paintball_TractorTire_Mat | 202p/16m2 | 3 | Std-Opaque [N] | 1,1,1,1 | 0,0,0,1x0 | Paintball_TireStack_Tex x1,1 |  sm=0.126 met=0 | 0.215,0.215,0.215 a=0.104 |
| Banner_Gym_Shield_Mat | 104p/14m2 | 0 | Std-Opaque [] | 0.9569,0.6032,0.4095,1 | 0,0,0,1x0 | GymPosterShield x1,1 |  sm=0.309 met=0 | 0.612,0.607,0.608 a=1.000 |
| Wood_Horizontal_Orange_Mat | 56p/12m2 | 0 | Std-Opaque [FAST MSG N] | 0.844,0.5526,0.2186,1 | 0,0,0,1x0 | wood_grain_Tex x2,3 |  sm=0.14 met=0 | 0.797,0.797,0.797 a=1.000 |
| SciFi_Emit_Blue_Mat | 47p/8m2 | 0 | Std-Opaque [MSG N] | 0.189,0.7037,1,1 | 0,0.978,3.282,1x4.45 | - |  sm=1 met=0 | - |
| EventRoom_Moon_mat | 33p/7m2 | 0 | AG/Unlit-Transparent [FOG_DISABLED] | 0.9647,1.0275,1.498,1 | 1,1,1,1x3.53 | Moon_Tex x1,1 | tint=0.614,0.6315,0.747,1 sm=- met=0 | 0.855,0.850,0.853 a=0.709 |
| Concrete_Barrier_Mat | 96p/5m2 | 0 | Std-Opaque [FAST MSG N] | 1,1,1,1 | 0,0,0,1x0 | Concrete_Barier_Tex x1,1 |  sm=0.5 met=0 | 0.760,0.752,0.723 a=0.871 |
| WallSign_Blue_Mat | 13p/3m2 | 0 | Std-Opaque [MSG N] | 0.2868,0.6163,1,1 | 0.2868,0.6163,1,1x1.72 | WallSign_Tex x1,1 |  sm=0.187 met=0 | 0.637,0.637,0.637 a=0.422 |
| Speaker_mat | 18p/3m2 | 11 | Std-Opaque [MSGANDCOLOR N] | 0.5,0.5,0.5,1 | 0,0,0,1x0 | Speaker_Tex x1,1 |  sm=0.588 met=0.85 | 0.168,0.168,0.168 a=0.216 |
| Couch_Plush_Purple_Mat | 35p/3m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Couch_Plush_Purple_tex x1,1 |  sm=0.36 met=0 | 0.415,0.285,0.535 a=1.000 |
| Barrel_Table_Crate_Mat | 4p/3m2 | 0 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Barrel_Table_Crate_Tex x1,1 |  sm=0.5 met=0 | 0.356,0.241,0.112 a=1.000 |
| Poster_ShakeHands_Mat | 1p/2m2 | 0 | Std-Opaque [] | 1,1,1,1 | 0,0,0,1x0 | - |  sm=0.151 met=0 | Shake hands poster 256x256 fmt48 mip2=64x64 srgbmean 0.652,0.617,0.584 a 1.000 lin->srgb 0.707,0.664,0.623 lumaRange 0.13-0.88 cs1 |
| Wall_SciFi_Cover_Modular_mat | - | 312 | Std-Opaque [MSG N] | 1,1,1,0.354 | 0,0,0,1x0 | Wall_SciFi_Cover_Modular_Tex x1,1 |  sm=0.4 met=0.85 | 0.447,0.445,0.435 a=0.238 |
| WallVent_mat | - | 32 | Std-Opaque [MSG N] | 1,1,1,0.354 | 0,0,0,1x0 | WallVent_tex x1,1 |  sm=0.855 met=0.85 | 0.421,0.421,0.421 a=1.000 |
| StageLight_Glow_RedOrange_Mat | - | 11 | AG/Unlit-Additive [] | 1,0.0896,0.0896,1 | 0,0,0,1x- | StarBoxBlur x1,1 | tint=0.228,0.1599,0.0577,0.578 sm=- met=0 | 0.101,0.101,0.101 a=1.000 |
| Speaker_Intercom_mat | - | 7 | Std-Opaque [N] | 1,1,1,0.354 | 0,0,0,1x0 | Speaker_Intercom_tex x1,1 |  sm=0.647 met=0.85 | 0.689,0.629,0.544 a=1.000 |
| Shipping_Container_Instanced_mat | - | 60 | Std-Opaque-Instanced [FAST MSG] | 1,1,1,1 | 0,0,0,1x0 | Shipping_container_Instanced_Tex x1,1 |  sm=0.5 met=0 | 0.867,0.803,0.723 a=0.588 |
| SciFi_CoverWall03_mat | - | 50 | Std-Opaque [MSG N] | 0.2941,0.8832,1,1 | 0.2941,0.8832,1,1x1 | Scifi_CoverWall03_Tex x1,1 |  sm=0.799 met=0.85 | 0.502,0.615,0.652 a=0.227 |
| SciFi_CoverWall02_mat | - | 34 | Std-Opaque [MSG N] | 0.2941,0.8832,1,1 | 0.2941,0.8832,1,1x1 | Scifi_CoverWall02_Tex x1,1 |  sm=0.799 met=0.85 | 0.421,0.531,0.567 a=0.271 |
| SciFi_CoverWall01_mat | - | 27 | Std-Opaque [MSG N] | 0.2941,0.8832,1,1 | 0.2941,0.8832,1,1x1 | Scifi_CoverWall01_Tex x1,1 |  sm=0.799 met=0.85 | 0.383,0.491,0.527 a=0.228 |
| SciFi_CoverSideWall_Glass_mat | - | 28 | Std-Transparent-Emission [MSG N] | 1,1,1,1 | 0,0.8527,1,1x0 | Scifi_CoverWall01_Tex x1,1 |  sm=0.444 met=0.85 | 0.383,0.491,0.527 a=0.228 |
| Sandbag_Mat | - | 23 | Std-Opaque [N] | 1,1,1,1 | 0,0,0,1x0 | Sandbag_Tex x1,1 |  sm=0.222 met=0 | 0.466,0.434,0.361 a=1.000 |
| Rock_Boulder_Mat | - | 82 | Std-Opaque-Decal [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Cracked_Rock_D x2.98,2.89 |  sm=0.5 met=0 | 0.440,0.440,0.440 a=1.000 |
| Quest_Poster_Mat | - | 1 | Std-Opaque [] | 1,1,1,1 | 0,0,0,1x0 | - |  sm=0.5 met=0 | QUEST POSTER 512x512 fmt51 mip3=64x64 srgbmean 0.466,0.358,0.224 a 1.000 lin->srgb 0.520,0.406,0.263 lumaRange 0.03-0.89 cs1 |
| Quest_Poster_Case_Mat | - | 1 | Std-Opaque [] | 1,0.8535,0.517,1 | 1.3694,1.7208,0.5496,1x4 | Quest_Poster_Case_Tex x1,1 |  sm=0.167 met=0 | 0.442,0.371,0.327 a=0.098 |
| Quest_Jumbotron_Cauldron_Marquee_Tex | - | 1 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x1.18 | Quest_Jumbotron_Cauldron_Marquee_Tex x1,1 |  sm=0.542 met=0 | 0.386,0.313,0.294 a=1.000 |
| Mirror_Quest_Mat | - | 1 | AG/Mirror [] | - | 0,0,0,1x- | Smude_Tex x1.39,1 |  sm=- met=0 | 0.972,0.972,0.972 a=1.000 |
| LockerFountainStageLight_mat | - | 11 | Std-Opaque [MSG N] | 1,1,1,1 | 0,0,0,1x0 | Locker_Fountain_Spotlight_Tex x1,1 |  sm=1 met=0 | 0.412,0.465,0.539 a=0.008 |
| LockIcon_Mat | - | 10 | Std-Transparent [] | 0,0,0,1 | 0,0,0,1x0 | Lock_Icon_Tex x1,1 |  sm=0 met=0.24 | 1.000,1.000,1.000 a=0.246 |
| LanternPost_Mat | - | 5 | Std-Opaque [] | 1,0.4662,0.1179,1 | 0.915,0.3597,0,1x0.76 | Wall_Torch_Lantern_Post_Tex x1,1 |  sm=0.105 met=0 | 0.299,0.233,0.157 a=0.007 |
| Goblet_Clock_Mat | - | 9 | Std-Opaque [N] | 0.794,0.794,0.794,1 | 0,0,0,1x0 | Goblet_WallClock_Tex x1,1 |  sm=0.5 met=0 | 0.720,0.577,0.429 a=0.917 |
| Dropship_Prop_Mat | - | 1 | Std-Opaque [N] | 0.7206,0.7206,0.7206,0.354 | 0,0,0,1x0 | Dropship_Prop_Tex x1,1 |  sm=0.19 met=0.85 | 0.209,0.326,0.329 a=0.995 |
| Concrete_MutedBlue_50x_Mat | 87p/0m2 | 0 | Std-Opaque [MSG N] | 0.529,0.613,0.697,1 | 0,0,0,1x0 | Concrete_Tex x50,50 |  sm=0.593 met=0 | 0.734,0.736,0.737 a=1.000 |
| Clock_Glass_Mat | - | 9 | Std-Transparent [MSG N] | 0.6131,0.7664,0.909,0.0824 | 0,0,0,1x0 | - |  sm=0.984 met=0.659 | - |
| Charades_Green_Mat | - | 1 | Std-Opaque [MSG N] | 0.3021,0.803,0.286,1 | 0,0,0,1x0 | Concrete_Tex x50,50 |  sm=0.5 met=0 | 0.734,0.736,0.737 a=1.000 |
| Charades_Blue_Mat | 2p/0m2 | 0 | Std-Opaque [MSG N] | 0.3255,0.6196,0.9137,1 | 0,0,0,1x0 | Concrete_Tex x50,50 |  sm=0.194 met=0 | 0.734,0.736,0.737 a=1.000 |
| Book_Green_Mat | - | 8 | Std-Opaque [FAST N] | 1,1,1,1 | 0,0,0,1x0 | Book_Green_Tex x1,1 |  sm=0.5 met=0 | 0.218,0.460,0.279 a=1.000 |
| Book_Brown_Mat | - | 3 | Std-Opaque [FAST N] | 1,1,1,1 | 0,0,0,1x0 | Book_Brown_Tex x1,1 |  sm=0.5 met=0 | 0.487,0.373,0.131 a=1.000 |
| Book_Blue_Mat | - | 3 | Std-Opaque [FAST N] | 1,1,1,1 | 0,0,0,1x0 | Book_Navy_Tex x1,1 |  sm=0.5 met=0 | 0.264,0.274,0.286 a=1.000 |

Observations:
- Nearly every Jumbotron surface is AG/Standard-Opaque (136 of 222 level materials) - the shader RrLightGrid lights.
  The glowing ones are AG/Standard-Opaque with _EmissivePower > 0 (Pillar_SciFi_Pink 0.5, Pillar_SciFi_Red,
  Door_SciFi_Cardboard 2.37, Wall_SciFi_Decor_Blue, Terminal_SciFi_Prop, SciFi_Emit_*...) or AG/Standard-Opaque-Emission.
- The wall textures are near-grey (Brick_Tex 0.735, Concrete_Tex 0.735, Paint_Tex 0.811, Linoleum_Flecked 0.511,
  Wallpaper_SciFiPanels 0.381): the colour of a Jumbotron room comes from vertex colours x _BaseColor and its lights.


## 4. Install substitutes (same material / same texture / closest AG)
Install data: rrtool objects.tsv (SP/rrtool/idx: 4,133 Texture2D names, 2,300 Material names), "rrtool dump" of 82
candidate install materials (NSP/jt10/materials/inst/*.json, compact: instsum.txt) and my scratch tool
NSP/jt10/materials/insttex (rrtool Core.cs + a DXT1/DXT5 decoder; one ~64 px mip -> mean colour only): insttex.txt.
Cross-check APK (ASTC) vs PC (DXT) means of the same textures: Concrete_Tex 0.734/0.734, Door_Scifi_Cardboard_Tex
0.339 a.014 / 0.339 a.013, DoorTrigger_Tex 0.251/0.249, Scifi_Rug_Tex 0.224,0.246,0.273 / 0.224,0.245,0.273,
Vent_Modular_tex 0.396/0.394, Linoleum_Flecked_Tex 0.511 vs install Tile_Linoleum_Flecked_Tex 0.505 (same image).

### 4a. Level textures the install LACKS (of the used materials' slots)
Brick_Tex (Cinderblock / Brick_Painted_*: the #2 wall material!), Linoleum_Flecked_Tex + Tile_Linoleum_Flecked_Spec,
Wallpaper_SciFiPanels_Tex/Norm/Spec, Pillar_SciFi_tex/norm/spec, Metal_FloorTread_Tex/Norm, Metal_Ribbed_Tex/Norm,
StageTop_tex/norm, Wall_SciFi_Decor_Blue_Tex/Norm/Spec, Wall_SciFi_Cover_Modular_Tex/Spec, Scifi_CoverWall01-03_Tex,
Terminal_SciFi_Prop_Tex/Norm/Spec, WallSign_Tex, WallVent_tex, Speaker_Intercom_tex, Shipping_container_Instanced_Tex,
Sandbag_Tex, Crane_Red_Tex, Wood_Planks(+Norm), Jumbotron_Tex, GarageMarkings_tex, Wires_Tex, Sunset_Tex,
Carpet_Blacklight_Swirls_Tex, Couch_Plush_Purple_tex, GymPosterShield, Dropship_Prop_Tex, Quest_* poster/marquee.
PRESENT (so the level material can be rebuilt exactly): Concrete_Tex/Norm/Spec (Wall_Vertcolor, Concrete_*),
Paint_Tex/Norm/Spec (Concrete_VertColor, Wallpaper_Dark, Paint_Red), **Brick_Norm + Brick_Spec** (cinderblock relief),
Tile_Linoleum_Flecked_Norm, Metal_Corrugated_Tex/Norm, Metal_Glossy_Spec, Metal_Tex (two copies: @1df8e0ef = 0.384
= the level's; @43ea1176 = 0.958), Metal_Norm, Curtains_Tex, Cloth_Norm/Spec, MetalBarrel_Tex/Norm/Spec,
"Wood FloorTiled_tex/Norm/spec", wood_grain_Tex, Plastic_Norm/Spec, Carpet_Burber_norm/Spec.

### 4b. Install templates (all AG/Standard-* = RrLightGrid.UsesGrid; all have _BaseMap/_BaseColor/_EmissivePower)
| install material | shader [kw] | _BaseColor | textures (base mean sRGB, alpha) | use for |
| --- | --- | --- | --- | --- |
| Concrete_Blue_50x_Mat | Std-Opaque [MSG N] | 0.325,0.620,0.914 | Concrete_Tex x50 (0.734 a1) + Concrete_Norm + Concrete_Spec | Wall_Vertcolor, Cinderblock, Brick_Painted_*, Concrete_Grey_3x, Concrete_PaleBlue_200x |
| Concrete_LightGrey_2x_Mat (level has it) | Std-Opaque [FAST MSG N] | 0.934 | Paint_Tex x2 (0.804 a1) + Paint_Norm + Paint_Spec | Concrete_VertColor (identical textures + base) |
| Tile_Linoleum_Brown_Mat | Std-Opaque [MSG N] | 0.529,0.448,0.448 | Tile_Linoleum_Flecked_Tex x50 (0.505 a1) + _Norm + Tile_Flecked_Matte_Spec | Linoleum_Flecked_Brown (+_FloorQueue) |
| Metal_StepTrim_Mat | Std-Opaque [MSG N] | 1 | Metal_Corrugated_Tex x1,17 (0.898 a1) + Metal_Corrugated_Norm | Metal_Corrugated_Blue, Metal_Ribbed_Gray, Shipping_Container |
| Metal_LightGrey_Dull_Mat (level has it) | Std-Opaque [MSG N] | 1 | Metal_Tex@1df8e0ef x4 (0.384 a1) + Metal_Norm + Metal_Glossy_Spec | Metal_Dull (plates + scaffolding), Wallpaper_SciFiPanels_Big (safe choice) |
| Metal_FloorTread_Shiny_Mat | Std-Opaque [MSG N] sm 1 | 0.489 | Metal_FloorTread_Shiny_Tex (0.468,0.476,0.532 a1) | Metal_FloorTread |
| **Door_SciFi_Cardboard_mat** (level has it) | Std-Opaque [MSG N] met .85 sm .855, power 2.37 | 0,1,1 | Door_Scifi_Cardboard_Tex (0.339 a0.013: grey sci-fi panel, glow strips in alpha) + Norm + Spec | Pillar_SciFi_Pink/Red, Wall_SciFi_Decor_Blue, Wall_SciFi_Cover_Modular, SciFi_CoverWall01-03 |
| ControlPanel_Mat | Std-Opaque-Emission [MSG N] power .77 | 0.532,0.559,0.585 | ControlPanels_Tex (0.702 a.795) + ControlPanels_Emit map | Terminal_SciFi_Prop (terminals) |
| Arena_Cyber_Emit_Teal_Mat / _Orange_ | Std-Opaque [] power 2, no texture | 0,1,0.651 | none | SciFi_Emit_Cyan/Blue, WallSign_Red/Blue |
| Scifi_Rug_Mat (level has it) | Std-Opaque [MSG N] | 1 | Scifi_Rug_Tex (0.224,0.245,0.273 a1); + install SciFi_Carpet_Tex (0.096,0.103,0.187 a1) | Carpet_Blacklight_Swirls |
| Wood_Floor_Matte_mat (level has it) | Std-Opaque [MSG N] | 1,0.735,0.491 | "Wood FloorTiled_tex" x10,40 (0.718 a1) | Wood_Floor_Wide (as the Golden Trophy) |
| Wood_Horizontal_DarkBrown_5x_Mat | Std-Opaque [MSG N] | 0.510,0.345,0.271 | wood_diffuse_horiz_desat (0.719 a1) | StageTop |
| Wood_Horizontal_Orange_Mat (level has it) | Std-Opaque [FAST MSG N] | 0.844,0.553,0.219 | wood_grain_Tex x2,3 (0.791 a1) | Wood_Planks |
| Paint_RedOrange_Mat / Paint_DarkGrey_Mat | Std-Opaque [MSG N] | 1,0.447,0.247 / 0.330 | Paint_Tex@1df8e0ef (0.804 a1) | Paint_Red, Crane_Red / Wallpaper_Dark |
| MetalBarrel_Blue_Mat | Std-Opaque [FAST MSG N] | 0.061,0.719,1 | MetalBarrel_Tex (0.584 a.861) | MetalBarrel_Green |
| Vent_Modular_Mat (level has it) | Std-Opaque [MSG N] | 0.838,0.906,1 | Vent_Modular_tex (0.394 a.483) | WallVent (dress) |
| Curtain_Red_Mat | Std-Opaque-Instanced [MSG N] | 0.509,0.094,0.094 | Curtains_Tex (0.931,0.907,0.870 a.851) | Curtain_Black (GT-approved template) |
| Glass_Teal_mat | Std-Transparent [MSG N] | 0.319,0.558,0.608,0.110 | Glass_Tex | SciFi_CoverSideWall_Glass, Clock_Glass (real glass only) |

Rejected: Brick_Red_Mat for the cinderblock (Brick_Grout_tex is RED 0.461,0.308,0.288 with a=0.817: the blue/grey vertex
tints turn mauve), Concrete_Block_Mat (Concrete_Block_Tex a=0.010: no tint can apply), Scifi_Carpet_Mat / SolidColor_*
(AG/Avatar shaders), Enemy_* textures (robot atlases), Pistol/Railgun SciFi skins (weapon atlases).

### 4c. KEY FINDINGS behind the look (why the Jumbotron is flat violet today)
1. **Vertex colours are LINEAR multipliers; our code gamma-decodes them.** AG/Standard-* multiplies the raw mesh
   COLOR (Color32/255, never converted) by _BaseColor (which Unity DOES convert sRGB->linear). RrJumbotronHull
   multiplies c= into mi.Col and calls SetColor, so Unity linearises it: c=0.15 (47% of Wall_Vertcolor) becomes 0.019
   instead of 0.15 (8x darker), the cinderblock's 0.20,0.50,0.70 becomes 0.033,0.214,0.448 (a deep saturated blue that
   the pink/purple lights turn violet). Fix: put c= into mesh.colors (raw) on the install AG template, or if a tint
   material is kept: _BaseColor = LinearToGamma(c) (0.15 -> 0.424, 0.05 -> 0.248, 0.45 -> 0.701, 0.65 -> 0.827,
   0.20,0.50,0.70 -> 0.485,0.735,0.854, 0.55,0.10,0.05 -> 0.767,0.349,0.248, 0.05,0.45,0.10 -> 0.248,0.701,0.349,
   0.65,0.70,0.70 -> 0.827,0.854,0.854). Caveat: this assumes Rec Room renders in linear space like Somnium (its light
   arrays hold linear colour x intensity, light9.md); check in-world: a c=0.15 Hallway2 wall should read mid-dark grey.
2. **_BaseColor is only applied where the base texture's alpha is high** (albedo = tex * lerp(1, vc*_BaseColor, tex.a),
   emission = tex * tex.a * _EmissivePower * vc*_BaseColor). The saturated pink/red/cyan of the sci-fi props lives
   in ~1% of their texels: Pillar_SciFi_tex a=0.011, so Rec Room's pink pillar is GREY 0.38 with thin pink glow strips;
   we draw it solid (1,0.39,0.95) (the big violet pillars of the Foyer). Same for Wall_SciFi_Decor_Blue (a 0.004),
   Terminal (0.074), Door_SciFi_Cardboard (0.014, installed: fine), DoorTrigger (0.021), Enemy_Jumbotron (0.020).
   Measured effective mean albedo / emission per material: NSP/jt10/materials/eff.txt (sec. 5 column "RR mean").
3. **_BaseColor.a = 0.354 is not transparency** (sec. 1): 312 cover walls, 97 wall decor, 32 vents, 7 speakers
   and the 1,711 Wall_SciFi_Decor_Blue plates are drawn as RrLook.Glass (the see-through boxes in the Foyer).
   Decide transparency by the SHADER (AG/Standard-Transparent*, AG/Unlit-*, Shader Forge fog), never by alpha.
4. **Level UVs are metres only for ProBuilder faces.** Measured UV density (UV units per world metre, area-weighted
   median, my scratch tool NSP/jt10/materials/uvden -> uvden.txt): 1.0 for most walls/floors, but Cinderblock 0.25,
   Pillar_SciFi 0.267, Door_SciFi 0.149, Curtain_Black 0.162, StageTop 0.446, Scifi_Rug 0.235, Vent 0.427, Wood_Table 0.02,
   Metal_LightGrey_Dull 0.02, Concrete_PaleBlue_200x 0.02, Wood_Floor_Matte 0.018, Paint_Red 0.02. Our plate UVs are
   world metres, so texture repeats per metre = density x the level's _BaseMap scale (also for inst=1 materials:
   Wood_Table_Mat's x2,3 on metre UVs is 50x too dense). The "tile" column in sec. 5 is metres per repeat.


## 5. PROPOSED MATERIAL TABLE
How to read: template = install material cloned per level material (new Material(template)); "tex" = install texture
set on _BaseMap (and bump/spec where named; "-" = the template's own); tint = _BaseColor as an sRGB colour for SetColor;
vc = the plate's c= written RAW into mesh.colors (white where no c=; always write the stream); tile = metres per texture
repeat on our metre-UV plates (= 1 / (UV density x level scale)); glow = _EmissivePower (the emission colour is
tex x tex.a x vc x _BaseColor, so only the texture's alpha areas glow). "RR mean" = measured effective mean albedo of
the level material (sRGB, white vc), what the substitute reproduces. Everything opaque unless it says transparent.
Covers 100% of the surface area; top rows first (cum. area %).

| level material | plates / m2 (cum %) | APK: shader, colour, texture (mean, alpha) -> RR mean | proposed runtime look | why |
| --- | --- | --- | --- | --- |
| Wall_Vertcolor_mat | 2227 / 24432 (21.5) | Std-Opaque FAST, white, Concrete_Tex x25 (0.734 a1) + Concrete_Norm/Spec, sm .774 -> 0.735 x vc | template **Concrete_Blue_50x_Mat**, tex - (it IS Concrete_Tex + Concrete_Norm + Concrete_Spec), tint 1,1,1, sm 0.774, **vc raw**, tile 0.04 m (25/m, UV 1/m) | every level texture is in the install: exact rebuild; vc = the room colour (0.15 dark grey 47%, white 35%, 0.05 11%) |
| Cinderblock_VertColor_Mat | 1986 / 23479 (42.2) | Std-Opaque, white, Brick_Tex x2 (0.735 a1, level-only) + Brick_Norm/Spec, sm .177 -> 0.735 x vc | template **Concrete_Blue_50x_Mat**, tex Concrete_Tex (0.734 a1) + _BumpMap **Brick_Norm** + _SpecGlossMap **Brick_Spec**, tint 1,1,1, sm 0.177, **vc raw**, tile 2 m (UV 0.25/m x 2) | Concrete_Tex has Brick_Tex's mean AND alpha (tint fully applied); the cinderblock relief comes from the level's own normal map, which the install has. vc: 0.20,0.50,0.70 steel blue 45% (StadiumEntry, Foyer), 0.65 grey 21%, white 20%, brick red 0.55,0.10,0.05 7% |
| Paint_DarkGrey_Mat (inst) | 342 / 11471 (52.3) | Std-Opaque, 0.330, Paint_Tex x50 -> 0.261 | the install material as it is, **vc raw** (StadiumEntry 0.20,0.50,0.70; Foyer 0.15), tile 0.02 m | today the c= plates leave the install material for a gamma-tinted maker-pen copy |
| Linoleum_Flecked_Brown_Mat (+ _FloorQueue) | 1752 / 10901 (60.8) | Std-Opaque, 0.578,0.547,0.529, Linoleum_Flecked_Tex x1 (0.511 a1, level-only) + Tile_Linoleum_Flecked_Norm, sm .85 -> 0.285,0.268,0.259 | template **Tile_Linoleum_Brown_Mat**, tex - (Tile_Linoleum_Flecked_Tex 0.505 a1 = same image), tint 0.585,0.554,0.536, sm 0.85, tile 1 m | same texture under another name, measured tint reproduces the mean |
| Concrete_LightGrey_2x_Mat (inst) | 1536 / 8976 (68.7) | Std-Opaque FAST, 0.934, Paint_Tex x2 -> 0.757 | as it is, tile 0.5 m | installed |
| Unlit_Black_mat (inst) | 205 / 3896 (72.1) | AG/Unlit-Color (black) | as it is (unlit black, not grid-lit on purpose) | void backdrops behind windows / screens |
| Metal_Corrugated_Blue_Mat | 137 / 3529 (75.2) | Std-Opaque, 0.085,0.334,0.462, Metal_Corrugated_Tex x1,0.5 (0.902 a1) + Norm + Metal_Glossy_Spec -> 0.073,0.300,0.416 | template **Metal_StepTrim_Mat**, tex - (Metal_Corrugated_Tex + _Norm), tint 0.085,0.334,0.462, sm 0.177, tile 1 m x 2 m | all level textures in the install; exact |
| Concrete_VertColor_Mat | 737 / 3048 (77.9) | Std-Opaque, 0.934, Paint_Tex x2 + Paint_Norm/Spec -> 0.757 x vc | template **Concrete_LightGrey_2x_Mat** (identical textures and colour), **vc raw** (white 43%, 0.65,0.70,0.70 32%, 0.70,0.75,0.75 19%), tile 0.5 m | = Concrete_LightGrey_2x + vertex colour |
| Brick_Painted_LightGrey_Mat | 94 / 2760 (80.3) | Std-Opaque, 0.985, Brick_Tex x0.5 + Brick_Norm/Spec, sm .3 -> 0.724 | as Cinderblock (Concrete_Blue_50x_Mat + Concrete_Tex + Brick_Norm + Brick_Spec), tint 0.985, sm 0.3, tile 2 m | Cargo1 brick walls |
| Concrete_Grey_3x_Mat | 268 / 2722 (82.7) | Std-Opaque, 0.824, Concrete_Tex x3 + Norm/Spec, sm .662 -> 0.602 | template Concrete_Blue_50x_Mat, tex -, tint 0.824, sm 0.662, tile 0.33 m | exact |
| Metal_Dull_Dark_Mat (inst) | 1666 / 2117 (84.6) | Std-Opaque FAST, 0.010,0.031,0.083 (navy), Metal_Tex x4 -> 0.001,0.004,0.012 | as it is, tile 0.25 m | installed; near-black navy trim is Rec Room's |
| Brick_Painted_Grey_Mat | 68 / 1975 (86.3) | Std-Opaque, 0.666, Brick_Tex x0.5 -> 0.485 | as Cinderblock, tint 0.666, sm 0.5, tile 2 m | Battlefield1 walls |
| Wallpaper_SciFiPanels_Big_Mat | 1491 / 1893 (88.0) | Std-Opaque, white, Wallpaper_SciFiPanels_Tex x0.2 (0.381 a1, level-only), sm .768 -> 0.382 | template **Metal_LightGrey_Dull_Mat** (Metal_Tex@1df8e0ef 0.384 a1), tint 1,1,1, sm 0.768, _Metallic 0, tile 1 m (a plain metal grain; the level's 5 m panel seams are lost). Option to try in-world: Door_SciFi_Cardboard_mat, _EmissivePower 0, _Metallic 0, tile 5 m (panel seams, 0.339) | same mean + alpha; the only install panel texture (the door) is a whole door face, may read as repeated doors |
| Wood_Table_Mat (inst) | 2803 / 1736 (89.5) | Std-Opaque, 0.686,0.455,0.231, wood_grain_Tex x2,3 -> 0.544,0.357,0.176 | as it is, **tile 25 m x 16.7 m** (UV 0.02/m) | installed; the default metre tiling is 50x too dense |
| Metal_LightGrey_Dull_Mat (inst) | 1049 / 1566 (90.9) | Std-Opaque, white, Metal_Tex x4 -> 0.385 | as it is, tile 12.5 m (UV 0.02/m) | installed |
| Door_SciFi_Cardboard_mat (inst) | 4357 / 1429 (92.2) | Std-Opaque met .85, 0,1,1 a.354, power 2.37, tex 0.339 a0.013 -> 0.351,0.366,0.363, cyan strips | as it is, OPAQUE, tile 6.7 m (UV 0.149/m) | installed (the a=0.354 must not make it glass) |
| Pillar_SciFi_Pink_mat | 1842 / 945 (94.1) | Std-Opaque met .85 sm 1, 1,0.392,0.946, power 0.5, Pillar_SciFi_tex (0.376 a**0.011**, level-only) -> **GREY 0.381,0.372,0.378** + pink strips | template **Door_SciFi_Cardboard_mat**, tex - (0.339 a0.013: same grey-panel + 1% glow-strip profile), tint 1,0.392,0.946, glow 0.5, sm 1, tile 3.75 m (UV 0.267/m) | the pink is only in the alpha strips; today a solid pink maker-pen = the violet pillars |
| Concrete_PaleBlue_200x_Mat | 6156 / 823 (94.8) | Std-Opaque, 0.734,0.884,1, Concrete_Tex x200 + Norm/Spec, sm .309 -> 0.535,0.649,0.737 | template Concrete_Blue_50x_Mat, tex -, tint 0.734,0.884,1, sm 0.309, tile 0.25 m (UV 0.02/m x 200) | exact (Stadium) |
| Metal_FloorTread_Mat | 2321 / 603 (95.4) | Std-Opaque, white, Metal_FloorTread_Tex (0.313,0.339,0.382 a.384, level-only), sm .88 -> 0.314,0.341,0.384 | template **Metal_FloorTread_Shiny_Mat**, tex -, tint 0.688,0.731,0.734, sm 0.88, tile 1 m | the install's tread plate; tint from the measured means |
| Wood_Floor_Matte_mat (inst) | 9 / 600 (95.9) | Std-Opaque, 1,0.735,0.491, "Wood FloorTiled_tex" x10,40 -> 0.724,0.528,0.348 | as it is, tile 5.6 m x 1.4 m (UV 0.018/m) | installed |
| Plastic_Grey_Gloss_Mat (inst) | 1234 / 471 (96.3) | Std-Opaque, 0.591,0.582,0.535, Plastic_Tex x2 | as it is, tile 1.8 m (UV 0.279/m) | installed |
| Curtain_Black_Mat | 3011 / 425 (96.7) | Std-Opaque, 0.245, Curtains_Tex (0.932 a.851) + Cloth_Norm/Spec -> 0.368,0.316,0.254 | template **Curtain_Red_Mat**, tex - (Curtains_Tex), tint 0.245,0.245,0.245, tile 6.2 m (UV 0.162/m) | same texture; GT-approved template |
| StageTop_mat | 8860 / 405 (97.1) | Std-Opaque, 0.566,0.386,0.117, StageTop_tex (0.479 a1, level-only) + norm -> 0.281,0.184,0.038 | template **Wood_Horizontal_DarkBrown_5x_Mat**, tex -, tint 0.403,0.272,0.072, tile 2.2 m (UV 0.446/m) | dark wood stage edge (8,860 tiny slivers: merge or drop < 0.05 m2) |
| Wood_Floor_Wide_mat | 194 / 402 (97.4) | Std-Opaque, 0.578,0.460,0.395, "Wood FloorTiled_tex" x0.2,0.75 (install) -> 0.412,0.325,0.277 | template **Wood_Floor_Matte_mat**, tex -, tint 0.578,0.460,0.395, sm 0.32, tile 5 m x 1.33 m | Foyer floor; the Golden Trophy's rule |
| Metal_Dull_Mat (+199 scaffolding dress) | 903 / 372 (97.7) | Std-Opaque, 0.596, Metal_Tex x3 -> 0.216 | template **Metal_LightGrey_Dull_Mat**, tex -, tint 0.596, sm 0.177, tile 0.31 m (UV 1.06/m) | same texture (the @1df8e0ef copy is the level's) |
| Scifi_Rug_Mat (inst) | 890 / 343 (98.0) | Std-Opaque, white, Scifi_Rug_Tex | as it is, tile 4.3 m (UV 0.235/m) | installed |
| Metal_Ribbed_Gray_mat | 353 / 327 (98.3) | Std-Opaque, 0.599,0.646,0.679, Metal_Ribbed_Tex x0.25,1 (0.796 a1, level-only) -> 0.473,0.511,0.538 | template **Metal_StepTrim_Mat**, tex -, tint 0.527,0.570,0.599, sm 0.177, tile 4 m x 1 m | ribbed ~ corrugated; measured tint |
| GarageMarkings_Mat (+42 dress) | 327 / 296 (98.6) | **AG/Standard-Transparent**, GarageMarkings_tex (a .42, level-only) | NOT a surface: leave out (floor decals: drips, oil spots, arrows) | without its texture it is a pale opaque rectangle |
| Vent_Modular_Mat (inst) | 522 / 198 (98.8) | Std-Opaque | as it is, tile 2.3 m | installed |
| Pillar_SciFi_Red_mat | 313 / 169 (98.9) | as Pillar pink, 1,0.108,0.454, power 0.4, sm .8 | as Pillar_SciFi_Pink, tint 1,0.108,0.454, glow 0.4, sm 0.8 | same |
| Carpet_Blacklight_Swirls_mat | 1 / 138 (99.0) | Std-Opaque, white, emis .751 x1, tex (0.103,0.133,0.299 a.279, level-only) -> 0.161,0.213,0.386 | template Scifi_Rug_Mat, tex **SciFi_Carpet_Tex** (0.096,0.103,0.187 a1), tint 1,1,1, glow 0.3, tile 2 m | Armory blacklight carpet; closest dark-blue carpet texture |
| SciFi_DoorTrigger_Mat (inst) | 682 / 128 | Std-Opaque, 1,0,0.269, power 1.37 | as it is | installed |
| 80Sunset_mat | 1 / 123 | **AG/Standard-Transparent-Emission**, Sunset_Tex (level-only) | special (sec. 6) | a backdrop picture |
| Wallpaper_Dark_Mat | 48 / 88 | Std-Opaque, 0.182, Paint_Tex x1 -> 0.140 | template Paint_DarkGrey_Mat, tint 0.182, tile 1 m | same texture (GT rule used DormRoom plaster; here Paint_Tex is exact) |
| MetalBarrel_Green_Mat | 1213 / 82 | Std-Opaque, 0.148,0.669,0.475, MetalBarrel_Tex (install) | template MetalBarrel_Blue_Mat, tint 0.148,0.669,0.475, tile 2.85 m | same texture |
| Enemy_Jumbotron_Mat | 434 / 75 | Std-Opaque, 1,0.241,0.558, power 3, Jumbotron_Tex (a .020) | special: the Jumbotron screen block (boss code rebuilds it) | sec. 6 |
| Wood_Planks_Mat | 392 / 72 | Std-Opaque, white, Wood_Planks x1.5 (0.432,0.288,0.119, level-only) | template Wood_Horizontal_Orange_Mat, tint 0.552,0.372,0.162, tile 2.7 m | measured tint |
| Crane_Red_Mat | 747 / 72 | Std-Opaque, white, Crane_Red_Tex (0.498,0.266,0.182, level-only) | template Paint_RedOrange_Mat, tint 0.736,0.354,0.266, tile 2 m | measured tint |
| Paint_Red_Mat | 50 / 71 | Std-Opaque, 0.915,0.229,0.117, Paint_Tex x100 | template Paint_RedOrange_Mat, tint 0.915,0.229,0.117, tile 0.5 m | same texture |
| Wall_SciFi_Decor_Blue_mat (+97 dress) | 1711 / 66 | Std-Opaque met .85, 0.114,0.680,0.961 **a.354**, power 1.24, tex 0.436 a**0.004** -> GREY 0.458 | template **Door_SciFi_Cardboard_mat**, tint 0.114,0.680,0.961, glow 1.24, OPAQUE, tile 5.8 m | grey panel with faint blue strips, not a cyan glass box |
| Metal_Grey_Gloss_Mat (inst) | 344 / 53 | Std-Opaque | as it is | installed |
| SciFi_Emit_Cyan_Mat / SciFi_Emit_Blue_Mat | 53 / 53 | Std-Opaque, no texture, 0,0.586,1 x10 / 0.189,0.704,1 x4.45 | template **Arena_Cyber_Emit_Teal_Mat** (no texture), tint = level colour, glow 10 / 4.45 | glowing strips, grid-lit base |
| WallSign_Red_Mat / WallSign_Blue_Mat | 208 / 45 | Std-Opaque, 1,0.074,0.074 x9 / 0.287,0.616,1 x1.72, WallSign_Tex (a .42, level-only) | template Arena_Cyber_Emit_Orange_Mat (no texture), tint = level colour, glow 0.9 / 0.17 (= power x the texture's alpha-weighted mean 0.101) | exit signs glow red/blue at Rec Room's mean |
| Terminal_SciFi_Prop_mat (+118 dress) | 82 / 28 | Std-Opaque met .85, white, power 1, tex (0.448 a.074, level-only) -> 0.503 | template **ControlPanel_Mat** (AG/Standard-Opaque-Emission, ControlPanels_Tex + Emit map), tint 0.59,0.60,0.59, glow as template (0.77) | the install's own sci-fi control-panel texture with screen emission |
| Plaster_Black_mat, Wood_Horizontal_DarkOrange_5x, Paintball_TractorTire, Wood_Horizontal_Orange, EventRoom_Moon, Concrete_Barrier, Speaker, Barrel_Table_Crate, Poster_ShakeHands (inst) | 571 / 91 | | as they are | installed |
| Banner_Gym_Shield_Mat, Couch_Plush_Purple_Mat, Concrete_MutedBlue_50x_Mat, Charades_Blue_Mat | 228 / 17 | tiny | maker-pen colour = RR mean (0.727,0.453,0.304 / 0.427,0.293,0.551 / 0.529,0.613,0.697 / 0.325,0.62,0.914) | < 0.02% of the area |

Dress (1,233 stand-in boxes; UVs 0..1 per box face):
| dress material | count | proposed |
| --- | --- | --- |
| ?Wall_SciFi_Cover_Modular_mat (cover walls End/Mid) | 312 | Std-Opaque met .85 base white a.354 power 0, tex 0.447 a.238 -> 0.463 grey: template **Door_SciFi_Cardboard_mat**, tint 1,1,1, glow 0, OPAQUE (or Metal_LightGrey_Dull_Mat tint 1.19 = 0.463 mean, plain) - these are the see-through boxes today |
| ?Metal_Dull_Mat (Scaffolding_6M/2M) | 199 | Metal_LightGrey_Dull_Mat tint 0.596 (as the plates) |
| ?Terminal_SciFi_Prop_mat (terminals) | 118 | ControlPanel_Mat tint 0.59 |
| ?Wall_SciFi_Decor_Blue_mat (wall_LOD0, bits_LOD0) | 97 | Door_SciFi_Cardboard_mat tint 0.114,0.680,0.961 glow 1.24, OPAQUE |
| Rock_Boulder_Mat (inst, rockvox) | 82 | as it is |
| ?Shipping_Container_Instanced_mat (+ its doors) | 60 | Metal_StepTrim_Mat (corrugated) tint 0.976,0.929,0.874 |
| ?SciFi_CoverWall01/02/03_mat (Cover_Wall, Cover_Glass) | 111 | Std-Opaque met .85, 0.294,0.883,1, power 1, tex a~0.25 (level-only): Door_SciFi_Cardboard_mat tint 0.294,0.883,1, glow ~5 (the door's alpha area is 1/20 of theirs) |
| ?GarageMarkings_Mat | 42 | leave out (decals) |
| ?Wires_Mat (Wires_Horizontal) | 33 | Std-Transparent cutout (a .10): leave out, or thin maker-pen 0.22 boxes |
| ?WallVent_mat | 32 | Vent_Modular_Mat as it is (tint white), OPAQUE |
| ?SciFi_CoverSideWall_Glass_mat | 28 | real glass (Std-Transparent-Emission): Glass_Teal_mat, tint 0.29,0.88,1 alpha 0.3 |
| ?Sandbag_Mat | 23 | maker-pen 0.485,0.446,0.367 |
| Speaker_mat, LockerFountainStageLight_mat, LanternPost_Mat, Book_* (inst) | 41 | as they are |
| ?StageLight_Glow_RedOrange_Mat (Quad) | 11 | AG/Unlit-Additive glow card: leave out or additive (sec. 6) |
| LockIcon_Mat | 10 | Std-Transparent icon decal: leave out |
| ?Goblet_Clock_Mat / ?Clock_Glass_Mat | 9+9 | maker-pen 0.794 / real glass |
| ?Speaker_Intercom_mat (a .354) | 7 | maker-pen 0.713,0.650,0.560 OPAQUE |
| ?Dropship_Prop_Mat (a .354) | 1 | maker-pen 0.255,0.346,0.350 OPAQUE |
| ?Quest_Jumbotron_Cauldron_Marquee_Tex, ?Quest_Poster(_Case)_Mat, ?Mirror_Quest_Mat | 4 | special (sec. 6) |


## 6. Not-surfaces / special materials
| material(s) | shader | plates / m2 | treat as |
| --- | --- | --- | --- |
| Scifi_AreaFog_Cyan/Purple/Pink/Red_Mat | Shader Forge/Particles/FogSin (base 0,0.447,0.553 / 0.182,0.092,0.221 / 0.781,0.169,0.622 / 0.766,0.166,0.166) | 6,670 / 750 | NOT surfaces: animated volumetric fog boxes (already skipped). At most feed their colour to the room's fog; never draw as boxes (iteration 8's pink view tint) |
| Portal_* (19 materials: Blue/Pink/Cyan_Concession, Red_Stadium, Blue_Garage, Cyan/Purple_StadiumEntry, Red/Purple_Hallway2, Purple_Cargo, Blue_Cargo1, Pink_Foyer, Cyan, Blue, Red, Pink ...) | Shader Forge/Particles/PortalFog / PortalFogFresnel (distance fog: _StartDistance 11.5, _FadeDistance 18.7, _EdgeDistance 32.9 on Portal_Blue_Concession) | 2,508 / 8,273 (Portal_Red_Stadium alone 1 plate / 1,470 m2) | NOT surfaces: depth-faded additive colour fog in doorways and big rooms (skipped today). If a room needs its glow: an additive, depth-faded unlit quad at ~10-20% of the colour, never opaque, no collider |
| InvisibleWall_Nav_Mat, Seat_InvisibleOutline_Mat | AG/DoNotRender | (2,726 m2 in the hull meshes; gtscene drops them) | never drawn (nav / invisible walls) |
| LightShaft_* (Yellow, Cyan, HotPink, Green, Red, Purple, Blue_Stadium, Quest_Projector, Quest_Orange) | AG/Unlit-Additive(-Soft) | gtscene drops them | light shafts: additive cards only (RrLook glow, no light) or leave out |
| StageLight_Glow_* (Pink, RedOrange, plain) | AG/Unlit-Additive, StarBoxBlur | 11 dress quads | glow cards at stage lights: additive / leave out |
| Scifi_FogVent_Mat | Shader Forge/Particles/AdditiveFadeDistance | - | vent steam: particles, not a surface |
| SciFi_Blue_Emit_Cargo1_Mat | Shader Forge/Lighting/EmitBake | - | Cargo1 emissive (bake helper): emissive blue strip if it has plates, else skip |
| Enemy_Jumbotron_Mat | Std-Opaque, 1,0.241,0.558 power 3, Jumbotron_Tex (a .020) | 434 / 75 (Stadium, y 8.4-13.6) | THE JUMBOTRON SCREEN BLOCK: the boss code rebuilds it (small plates dropped). If its hull plates are kept: Door_SciFi_Cardboard template tint 1,0.241,0.558 glow 3 (grey 0.30 body, pink strips), or the install's MakerPen_Skin_Jumbotron_Mat / StuntRunner_Jumbotron_Mat (AG/Standard-Opaque(-Emission), a real Jumbotron skin texture) |
| Quest_SciFi_VideoScreen | AG/UnlitTextureTintColor | - | the video screens: unlit, special |
| Quest_Jumbotron_Cauldron_Marquee_Tex (material) | Std-Opaque power 1.18 | 1 dress (Quest_Jumbotron_Marquee, Foyer) | the lobby marquee: install StuntRunner_JumbotronMarquee_Mat (with Jumbotron_Marquee_Emissive_Stencil / StuntRunner_JumbotronMarquee_Emissive) is the closest |
| 80Sunset_mat | AG/Standard-Transparent-Emission, base black, emis 2 x0, Sunset_Tex (0.421,0.211,0.044 a .42) | 1 / 123 | a backdrop picture in a window: unlit orange gradient (0.65,0.35,0.10) or leave black; not grid-lit |
| Unlit_Black_mat (inst) | AG/Unlit-Color | 205 / 3,896 | keep: black void, deliberately unlit |
| GarageMarkings_Mat, LockIcon_Mat, Wires_Mat | AG/Standard-Transparent (decals / cutouts, texture alpha .10-.42, textures level-only) | 331 / 331 + 85 dress | decals: leave out (they would be opaque rectangles) |
| SciFi_CoverSideWall_Glass_mat, Clock_Glass_Mat | AG/Standard-Transparent(-Emission) | 37 dress | the ONLY real glass: Glass_Teal_mat / RrLook.Glass |
| Mirror_Quest_Mat / Emit_Armory_Mirror_Mat | AG/Mirror / Std-Opaque | 1 dress / 4 m2 | Armory mirror: leave out or a dark glossy panel |
| Linoleum_Flecked_Brown_Mat_FloorQueue | Std-Opaque, render queue variant | 1 / 1,296 | the same look as Linoleum_Flecked_Brown_Mat |
| Rock_Boulder_Mat (inst) | AG/Standard-Opaque-Decal | 82 dress (StadiumEntry rocks) | as it is (install); UsesGrid accepts "AG/Standard-" |
| Shipping_Container_Instanced_mat, Curtain_Red_Mat (template) | AG/Standard-Opaque-Instanced | - | instanced variant: check once in-world that per-renderer _BaseColor is honoured (GT's curtains were fine) |
| alpha 0.354 on Door_SciFi_Cardboard, Wall_SciFi_Decor_Blue, Wall_SciFi_Cover_Modular, WallVent, Speaker_Intercom, Dropship_Prop | Std-Opaque | 6,000+ plates, 450 dress | NOT glass (see 4c.3) |


## Log
- started 2026-09-25
- sec1+2 written (plate/area table, dress, tints, AG vertex-colour math)
- sec3 written (APK shader/colour/texture means for all 109 used materials)
- sec4 written (install textures/templates, 4 key findings: linear vc, alpha-masked tint, alpha!=glass, UV density)
- sec5 (table) + sec6 (specials) written
- TL;DR added; scratch tools: NSP/jt10/materials/{texavg,insttex,uvden} (numbers only, nothing copied)
