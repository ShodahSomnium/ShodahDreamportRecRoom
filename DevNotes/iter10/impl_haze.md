# Iteration 10 - implementation notes: haze (RrJumbotronHaze.cs)

Owner: haze agent. Owns ONLY Games/Quests/Jumbotron/RrJumbotronHaze.cs.
Status: DONE 2026-09-25 - implemented, compile-checked (0 errors), NOT run in the harness (results phase).

## Plan (from task + jt_lighting 4.3/4.4/4.5, findings C/F/H, jt_materials section 6)
1. Area mist: h.Plates keys whose material starts with Scifi_AreaFog -> one additive mesh per key (merged plates),
   install material clone (Sandbox_Fog_Mat "Shader Forge/Particles/AdditiveFadeDistance" or AG/Unlit-Additive-Soft),
   level tint, camera fade (0 within ~2 m, full 10-20 m). No colliders, no shadows, not on the grid.
2. Portal glows: Portal_* plates (Foyer curtain, Flight Deck floor glows, Stadium red sheet) from Stagelight_Fog_Mat
   (PortalFogFresnel) or closest install additive; UVs 0..1 along plate (vertical mask); simple distance fade.
3. Kit glow: find kit renderers under root for level-only LightArchShaft*, red laser trails, cyan PropBox_SciFi crates,
   explosive barrels -> additive / emissive install clones.
All created objects -> owned.Things; yield when > 4 ms; one RrLog summary line per room.

## Findings while reading (filled in below)

## Log
(written 2026-09-25, before coding)
- Hull data: AreaFog / Portal plates have NO c= tint; colour + alpha = h.Mats[name].Col (the level's _BaseColor).
  Other shader numbers (start/end/fade/power/U/V, edge) are not in the data -> a small table in the code from
  NSP/jt10/lighting/portal_values.txt and mats_glow.txt (measured numbers only).
- DISCREPANCY: jt_lighting finding C lists cyan mist Edge 2.23 / Start 10 and pink 5.55 / 20; the raw dump
  (mats_glow.txt) says Cyan Edge 5.55 Start 20 End 2, Pink Edge 2.23 Start 10 End 2, Red 1.97/10/1, Purple 2.95/10/2
  (EdgePower 1.01 all). Using the raw dump.
- Install shaders (from the investigators' DXBC dumps): blend states are hard-coded:
  AdditiveFadeDistance (Sandbox_Fog_Mat) = SrcAlpha OneMinusSrcAlpha; out.rgb = 2*tex*vcol*_TintColor,
  a = tex.a*vcol.a*_TintColor.a * smoothstep((d-_EndDistance)/(_StartDistance-_EndDistance)) * soft; without
  _DEPTH_TEXTURE soft = saturate(((far-near)-d)/_SoftParticles) ~ 1.
  AG/Unlit-Additive-Soft = SrcAlpha One; without _DEPTH_TEXTURE soft = saturate((far - eyeDepth)*_InvFade) ~ 1.
  PortalFogFresnel = One One, Cull Off, N.V fresnel (back faces add 0), _VERTEX_COLOR multiplies rgb.
  _DEPTH_TEXTURE is not a material keyword on the templates (Rec Room sets it globally) -> off in our world.
- Kit '?' materials: RrSceneKit.MaterialOf gives every '?' (and missing) material ONE shared fallback
  RrLook.Solid(0.62,0.62,0.64) = "Rr Solid 9E9EA3"; merged batches ("Batch <mat> n") are keyed by material+mask+cell,
  so shafts / lasers / crates share batches with curtains, wires, posters; batch meshes are UploadMeshData(true)
  (not readable). Separate objects (unreadable / mirrored / _separateAll small torch-lit rooms) are named after the
  mesh at localPosition = record position. RrSceneKitRoot.Result.Data (RrSceneData) stays on the kit root.
  Kit '?' renderers per room (rrsk R records): LightArchShaft ?LightShaft_* 1-20, CubeBulged_LOD2
  ?Quest_SciFi_LaserTrail (Hallway2 15, StadiumEntry 18), PropBox_* ?PropBox_SciFi_mat 2-45, Barrel_Plastic_LOD0
  ?Barrel_SciFi_Explosive_Mat 4-25, Cube ?SciFi_Blue_Emit_Cargo1_Mat 1, Cube ?InvisibleWall_Nav_Mat 1 (Concession:
  drawn as a grey cube today), Curtain(_Flat)_LOD0 ?Curtain_Black_Mat, others (wires, posters, books, chairs,
  dumpsters, cardboard) stay the grey fallback.
- Light grid scope = the kit root's parent = our root: new AG renderers under root are picked up by its 3 s rescan;
  the haze shaders (Shader Forge / AG/Unlit) are not grid shaders.
- Rec Center note: AG/Unlit-Additive-Soft "drew nothing" there - explained by DormRoom_LightShaft_Gold_Mat's own
  colour (0.12,0.04,0.009) -> linear ~0.01; LightShaft_Yellow_Mat is (0.76,0.39,0.07,0.18).

## Design
A. Mist (Scifi_AreaFog_*): clone install Sandbox_Fog_Mat (AFD). _BaseMap white, _TintColor (SetVector, linear) =
   FogSin colour (linear) / (2*MistAlpha) with MistAlpha 0.1 -> added colour = FogSin's, 10 % darkening per layer;
   _StartDistance/_EndDistance = FogSin's; _SoftParticles 0.001 (soft factor always 1). One-sided plates (Cull Back as
   FogSin), vertex alpha = baked edge fade saturate(gap/_EdgeDistance)^_EdgePower, gap = raycast from the vertex
   along -normal against the room's Environment colliders (cap 50 m). Queue 3050, no shadows, no colliders.
B. Portals (Portal_*): clone install Stagelight_Fog_Mat (PortalFogFresnel): _BaseColor = level colour+alpha
   (SetColor, as Rec Room's own material), Start/End/U/V from the table, _FadeDistance 0.001 (shader depth fade
   always 1), _MobileDistanceEstimate 10000, _VERTEX_COLOR on. Plates subdivided into <= 2.5 m cells, TWO one-sided
   copies (front, flipped back), each vertex colour = baked saturate(gap behind that face / F)^P. UV 0..1 across
   the plate, v bottom -> top (world up) for the (u+U)(v+V) mask; horizontal plates uv (1,1) = mask 1.
C. Kit glow: find the kit root under root, its Data; material names '?' + Result.MissingMaterials = fallback set.
   Separate objects: swap their submesh materials in place. Merged fallback batches: disable them, rebuild the
   fallback submeshes of the records inside them (LOD/duplicate rules as the kit), merged per material+mask, with:
   ?LightShaft_* -> LightShaft_Yellow_Mat clone + level colour (Projector alpha 0 = hidden); ?Quest_SciFi_LaserTrail
   -> LightShaft_Yellow_Mat clone (2,0,0,1), white map; ?PropBox_SciFi_mat -> Door_SciFi_Cardboard_mat clone tint
   (0.096,0.7875,1) EP 2.32; ?Barrel_SciFi_Explosive_Mat -> Arena_Cyber_Emit_Orange_Mat clone tint (0.597,0.279,0.239)
   EP 0.09; ?SciFi_Blue_Emit_Cargo1_Mat -> Arena_Cyber_Emit_Teal_Mat (0,0.42,1) EP 3.6; ?Curtain_Black_Mat ->
   Curtain_Red_Mat clone tint 0.245 (GT-approved); ?InvisibleWall_Nav_Mat -> not drawn; the rest keep the fallback.
- 14:3x plan done; writing RrJumbotronHaze.cs (mist + portals + kit glow in one file, all in owned.Things).
- 14:4x wrote RrJumbotronHaze.cs (mist, portals, kit glow; ~1,360 lines). Next: compile check.
- 14:5x compile 1: 0 errors. Fixes after review: no null cached in owned.Materials (others may iterate it); unused
  params removed; UInt32 index when the combined source meshes exceed 65k (as the kit); batch boxes +0.5 m.
  Verified from the DXBC dumps: PortalFogFresnel N.V is max(0, .) (two one-sided copies do not double), its
  cb0[134..135] = _FadeDistance/_DistancePower/_StartDistance/_UValue, _VValue/_EndDistance/_MobileDistanceEstimate;
  AFD cb0[135] = _SoftParticles/_StartDistance/_EndDistance (fade from End to Start).
  Harness audit run1: Foyer kit 142 renderers -> 21 merged + 0 separate (the rebuild path is the common one).
- 14:5x REVIEW FINDING: horizontal sheets (Concession floor pools 2,800 m2, Stadium red sheet) are always seen at
  grazing angles; Rec Room's depth fade uses the gap along the VIEW ray (h / sin(view angle)), our normal bake uses h.
  Normal bake + PortalFogFresnel's N.V would make them ~30x too dim (Stadium sheet: RR ~0.12 at a 6 deg view, ours
  0.004). Change: (1) flat faces (|n.y| > 0.7) bake gap / 0.1 (a ~6 deg view from standing height at ~10 m; the
  steep views near you are hidden by the camera fade anyway); (2) only the 5 PortalFogFresnel materials (Cyan/Purple
  _Concession, Purple_Cargo, Purple_StadiumEntry, Red_Stadium) use Stagelight_Fog_Mat; the 14 PortalFog ones (no
  fresnel in Rec Room) use the mist's AdditiveFadeDistance clone (two one-sided copies, their (u+U)(v+V) mask and
  depth fade baked into the vertex alpha, tint = colour x a / (2 x 0.1)), so they do not dim at oblique views.
- 15:0x restructured: Bake struct (TwoSided / IntoAlpha / EdgeBlack / Mask), HazeMaterial (AFD, colour x strength),
  PortalSpec.Fresnel; the fresnel portal falls back to AFD (and back) if its template is missing. Compile next.
- 15:1x compile 3 (after the restructure): 0 errors, 0 warnings (server-like + editor). Hull now keys h.Plates by
  material name and Plate has Vc (c=); haze plates carry none - my key split on '|' is harmless.

## Result (what RrJumbotronHaze.Build does per room, one RrLog "Jumbotron" line "<scene>: haze: ...")
1. Mist: every Scifi_AreaFog_* plate list -> clone of Sandbox_Fog_Mat (AFD; white map, _TintColor linear
   colour/(2*0.1) a 0.1, Start/End from the level, _SoftParticles 0.001, queue 3050); one-sided, <= 2.5 m cells,
   vertex alpha = saturate(gap/Edge)^EdgePower * saturate(gap) (gap along -n; / 0.1 on horizontal faces).
2. Portals: PortalFog (14 materials) -> the same AFD clone type with colour x alpha, Start/End, the (u+U)(v+V) mask
   and saturate(gap/Fade)^Power in the vertex alpha; PortalFogFresnel (5) -> Stagelight_Fog_Mat clone (_BaseColor
   = level colour, Start/End/U/V, _FadeDistance 0.001, _MobileDistanceEstimate 10000, _VERTEX_COLOR) with the fade
   in the vertex colour. Two one-sided copies per plate (front / flipped back), each baked with its own gap.
3. Kit glow: kit root "Rr Scene <name>" under root -> Result.Data; '?' + missing materials = the kit's grey;
   separate objects swapped in place; merged grey "Batch" objects disabled and their records' grey submeshes rebuilt
   under "<kit root>/Haze glow" merged per material + vertex-attribute mask (glow ones in: LightShaft_Yellow_Mat
   clones for shafts (level colour) and lasers ((2,0,0,1), white map); Door_SciFi_Cardboard_mat (0.096,0.7875,1)
   EP 2.32 crates; Arena_Cyber_Emit_Orange_Mat (0.597,0.279,0.239) EP 0.09 barrels; Arena_Cyber_Emit_Teal_Mat
   (0,0.42,1) EP 3.6 Cargo strip; Curtain_Red_Mat 0.245 curtains; InvisibleWall_Nav_Mat and the Projector shafts
   (alpha 0) not drawn; everything else the same grey).
All clones / meshes in owned.Things (clones cached as "haze:*" in owned.Materials, shared by the rooms; no nulls
stored); 4 ms frame slices; no colliders, no shadows from haze / additive glow; no static state.

## Tuning knobs (if the in-world look is off)
- RrJumbotronHaze.HazeAlpha 0.1 (darkening per mist/portal layer; the added colour is independent of it).
- RrJumbotronHaze.GrazingSin 0.1 (horizontal sheets / mist tops: smaller = brighter floor pools and Stadium sheet).
- Cell 2.5 m (bake resolution), MaxGap 50 m (open space = full strength).
- Per-material numbers in the Mists / Portals / Glows tables (measured values).

## Open issues / risks
- Not verified in-engine (no harness this phase). First things to look at in the headset: Foyer pink curtain above
  the stage + low cyan mist; Concession floor pools; Stadium red sheet (fresnel, from the arena floor); shafts in
  every room; red laser grid in Hallway2 / StadiumEntry; crate look (door-panel texture on PropBox UVs is a guess).
- UV direction of vertical portals is our guess (u along the plate's local x): materials with U = 0 (Pink, Purple_
  Hallway2, Purple_Cargo, Pink_Concession) fade across u, possibly from the wrong side.
- The bake is view-independent: Rec Room's view-ray depth fade is approximated by the normal gap (vertical) and
  gap / 0.1 (horizontal); PortalFogFresnel's N.V makes the 5 fresnel portals dimmer at oblique views, as in Rec Room.
- If the client's depth texture is known to exist, enabling _DEPTH_TEXTURE on the clones (and the real
  _FadeDistance / _SoftParticles / _InvFade) would give Rec Room's exact soft edges instead of the bake.
- Hull dress quads "?StageLight_Glow_RedOrange_Mat" (11, Foyer/stage glow cards) are the hull's dress, not handled here.
- Kit '?' materials left grey on purpose (no install stand-in chosen): Coiled_Wires, Posters, Books, chairs,
  Dumpster_SciFi_Orange, Box_Cardboard_Discs, PropBox_mat, PropBox_StuntRunner_TEMP.
