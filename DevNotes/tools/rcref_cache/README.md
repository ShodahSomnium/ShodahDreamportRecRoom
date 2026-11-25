# rcref_cache: reference renders of Rec Room's cached July 2025 Rec Center (local only)

Sibling of `../rcref` (the Android level7 renderer). It reads Rec Room's own Studio Rec Center
`RecCenter-RecCenter_071825.unity` from the user's download cache
(`%USERPROFILE%\AppData\LocalLow\Against Gravity\Rec Room\HTTPCache`, scene bundle `1AF.data` + dependency bundles
`183..1AE`). The bundles are read in place, as data. The outputs are:
- the frame numbers in `DevNotes/refdata/rc_cache/frame.json`;
- rendered pixels in local comparison PNGs under `DevNotes/refdata/rc_cache_ref/`.

Nothing (mesh, texture, UV) is copied, nothing goes into the world, nothing is loaded into Unity.

## Build and run
`bash build.sh` (dotnet SDK 8), then `bash run.sh <cmd> ...` (`RC_CACHE` overrides the cache folder).
The build compiles these in by path, unchanged: `../rrtool_cache/*.cs` (UnityFS reader, LZMA) and `../apkscene/Geo.cs`.
`CRc.cs` is a snapshot copy of `../rcscene/Rc.cs`'s reader classes (Rc, MeshGeo, Node, the scene walk), so this tool
keeps building while rcscene changes.

| command | what |
| --- | --- |
| `nodes [re]` | Every GameObject as TSV: path, active, world position, yaw, scale, mesh (`*` = static batch), world AABB. For a static batch the AABB is the union of its world-space submesh boxes. Saved as `refdata/rc_cache/cache_nodes.tsv`. |
| `frame <level7_nodes.tsv> <out.json>` | The rigid transform cache -> room frame (= Android level7 frame). Details below. |
| `render <poses.json> <frame.json> <outDir> [opts]` | Renders the poses (ROOM frame, as in rcref's `poses.json`), moved into the cache frame with frame.json. Writes one `<name>.png` per pose, plus 2x2 sheets for poses with `sheet`/`tile` (`rc_arrival.png`, 800x450 tiles, like RrTourSteps). |
| `mats` | Every drawn material as interpreted: shader, mode, cull, texture, mean alpha, tint, emission, specular, keywords. |
| `env` | Lights and RenderSettings as read. |
| `skydump <png>` | The skybox cubemap as an equirectangular image, plus the BC6H modes used (a decoder check). |

Render options:
- `only=<re>`;
- `ss=2` (supersampling);
- `texmax=1024`;
- `shadow=4096` (0 = off);
- `att=rr|urp`: point/spot falloff. `rr` = Rec Room's compact smoothstep falloff, as rcref; `urp` = URP's inverse square with smooth range;
- `amb=trilight|probe`;
- `env=amb|sky`: environment reflection from the trilight ambient (default) or from the skybox cubemap;
- `specmap=0|1`: use the `_SpecGlossMap` (default 0: `_SpecColor` / `_Smoothness` only);
- `spec=1`;
- `exp=1`;
- `lights=1`;
- `shafts=0|1`: the skylight light-shaft meshes (default off);
- `skipmat=<re>`;
- `sky=1`.

## The frame (refdata/rc_cache/frame.json)
`bash ../rcref/run.sh nodes ../../apkwork level7 > level7_nodes.tsv` dumps the level7 nodes.
`bash run.sh frame level7_nodes.tsv ../../refdata/rc_cache/frame.json` then:
1. Pairs objects drawn in both scenes. It pairs them first by the same path, with the Android root `StaticObjects/`
   dropped (224 pairs), then by a unique parent/name.
2. Tries yaw 0/90/180/270. For each yaw it keeps the pairs whose box sizes agree within 10 cm and takes the median
   shift of the box centres. It picks the yaw with the most pairs within 15 cm.
3. Refines the translation as the mean over those pairs. As a check, it also fits a free yaw (2D Procrustes).
4. Matches the pieces the Studio port renamed geometrically, as a check only (Beams/Beams = beams/pCube*,
   Windows/Window_Skylight = skylight/frame, kiosk/Beams = kiosk posts pCube169/170/174-177).

**Result:** yaw 0, translation (-0.001, 0, 0.001), so the cache scene is in the level7 frame.
- 221 of 224 pairs are within 15 cm (median 0.1 cm, p90 2.3 cm).
- The free-yaw check gives -0.0002 deg.
- The 3 outliers are props that moved between the builds: a short locker table by 1.5 m, a flat curtain by 0.26 m up,
  and a fig plant by 0.17 m.
- Door signs (all 6 active), the REC ROOM logo, the kiosk floor/counter/walls, walls, floor, ceiling and roof agree
  within 1 mm. The Dorm sign is off by 3 cm.
- The geometric check gives skylight frames 1-2 cm and beams 0-13 cm. The kiosk posts are exact in x/z; their y
  centre differs because the posts' lengths differ.

The old world's `RrRecCenterCache.FramePosition (2.05, -0.09, -7.23)` / `FrameYaw 90` is the transform into the frame
of `research/rr_reccenter_cache.md` (entrance at -Z, origin at the kiosk floor centre). It is not the level7 frame.

## What is drawn
- **Renderers.** Every active, enabled, LOD0 MeshRenderer and SkinnedMeshRenderer (bind pose) with a mesh: 430 of the
  838 renderers, 416k triangles.
  - A static-batched renderer draws its submesh range of `Combined Mesh (root: scene) N`. Those vertices are already in
    world space and are shared once per combined mesh.
  - A mirrored transform reverses the winding.
  - Unity's built-in Quad/Cube/Plane are made from their known shapes.
- **Materials.** The Studio shader bundle is not in the cache. The shader PPtr ids name the stock shaders: URP Lit,
  Unlit, Terrain Lit, TMP, particles, trees. Everything else comes from the saved properties:
  - `_BaseMap` with its tiling and offset, and `_BaseColor`;
  - surface and blend modes: fade, add, premultiplied;
  - alpha clip, `_Cull`;
  - `_EMISSION` with `_EmissionColor` x `_EmissionMap`;
  - the specular setup: `_SpecColor` and `_Smoothness`.
  - Material colours are converted sRGB -> linear.
- **Albedo:** Rec Room's AG rule, `tex.rgb * lerp(1, vc * tint, tex.a)`, for opaque materials. For DXT1 `a = 1`, which
  gives `tex * vc * tint`. Transparent materials use `tex * vc * tint`, with alpha `tex.a * tint.a * vc.a`.
- **Textures:** DXT1, DXT5, BC4, BC5, BC7, Alpha8, R8, RGB(A) and RGB565 at mip <= texmax, plus BC6H and RGBAHalf for
  cubemaps. Each has a box mip pyramid and per-pixel mip selection. Row 0 = v 0 (posters read correctly).
- **Lighting** (linear):
  - the RenderSettings trilight ambient, integrated over the hemisphere;
  - the KeyLight (directional, colour 0.973/0.942/0.842 sRGB, shadows 0.948) with a 4096^2 sun shadow map, 2 cm texels
    over the building;
  - 39 point/spot lights;
  - URP specular-setup BRDF: diffuse x (1 - max spec), sun highlight, environment term;
  - emission;
  - linear fog 5..400 m, colour (1, 0.898, 0.639).
- **Sky:** the skybox material `SkyboxLockerRoom_Baked`, with cubemap `ReflectionProbe_Skybox_Baked` (1024^2 BC6H,
  decoded at 256^2), times `_Tint` x 4.59 x `_Exposure` 1.13. The sun direction in the cubemap matches the KeyLight.
- **Terrain:** RecCenterTerrain, 257^2 over 64 m, with 3 layers blended by the 512^2 splat map.
- **Output:** each sample is clamped to 0..1, box-filtered ss x ss, then encoded to sRGB. There is no tonemapping, bloom
  or post-processing (Rec Room's post profile is in iter6/cachecenter.md).

## Not drawn / known differences
- TextMeshPro door-sign texts (built at runtime), particles and UI.
- The room-data objects that are not in the scene bundle: doors, hoops, ping-pong, radios, mirror, Friend-O-Tron. They
  are in `182.data`, which is not decoded.
- The 6 skylight light-shaft meshes (`LightShaftLocker_Mat`, additive, HDR tint) are off by default. Turn them on with
  `shafts=1`.
- Normal maps, occlusion and spec-gloss maps (unless `specmap=1`) are not used.
- The environment reflection is the trilight ambient by default. The sky cubemap (`env=sky`) made the navy window
  frames (smoothness 1) and the floor reflect the bright sky, unlike the in-game shots.
- Lights are per pixel with the `rr` falloff. Rec Room's PC runtime (shaders swapped by its ShaderPropertyTranslator)
  may differ.

## Example
```
bash ../rcref/run.sh nodes ../../apkwork level7 > ../../refdata/rc_cache/level7_nodes.tsv
bash run.sh nodes > ../../refdata/rc_cache/cache_nodes.tsv
bash run.sh frame ../../refdata/rc_cache/level7_nodes.tsv ../../refdata/rc_cache/frame.json
bash run.sh render ../../refdata/rc_ref/poses.json ../../refdata/rc_cache/frame.json ../../refdata/rc_cache_ref      # 21 poses, ~11 s
bash run.sh render ../../refdata/rc_cache_ref/cc_compare_poses.json ../../refdata/rc_cache/frame.json ../../refdata/rc_cache_ref
```
