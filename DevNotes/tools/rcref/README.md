# rcref: reference renders of an Android level (local only)

A software renderer for the user's Android build of Rec Room. It was written for the built-in Rec Center (level7), so
the world's Rec Center can be compared with the real level from the exact camera poses the world's screenshot scenario
uses. The PNGs are LOCAL comparison references under `DevNotes/refdata/rc_ref/`. They are never shipped and never go
into the world's Assets. The Android files are read in place as data only, through `../apkscene`'s reader (compiled in
by path, unchanged). Nothing is installed, executed or loaded.

## Build and run
`bash build.sh` (Unity's bundled Roslyn + .NET 6.0.21, like gtscene). `bash run.sh <cmd> ...` sets
`APK_DIR=DevNotes/apkwork/apk` and `RRTOOL_IDX=DevNotes/rrtoolidx` unless they are already set.

| command | what |
| --- | --- |
| `poses <scenario.json> <tiles.txt> <summary.txt> <out.json> [extra=<json>]` | The Rec Center part of the scenario (after `"goto": "RecCenter"`). It takes the `shot` steps (frame room = level coordinates; the `aim: hoop` pose comes from the run's summary.txt as rim + 0.3 m up, like RrTourSteps.Aim). It adds the `views` tiles: the tiles.txt camera and yaw, looking 4.6 degrees down as in RrTourSteps.EyeView. Extra poses are appended. |
| `render <work> <levelN> <poses.json> <outDir> [only=<re>] [ss=2] [texmax=512] [shadow=4096] [amb=trilight\|probe] [exp=1] [sky=1] [skipmat=<re>]` | Writes one `<name>.png` per pose. Poses with `sheet`/`tile` are also composed into an 800x450 2x2 sheet like RrTourSteps (`rc_arrival.png`). A pose may set `ymax` (drop triangles whose lowest point is above it) and `grid` (world grid lines every n metres, with x=0 / z=0 in red) for top-down layout views. |
| `list <work> <levelN>` | Every renderer left out (disabled, inactive) and every drawn material: shader, mode, culling, texture, tint, vc/amask/lit, blend, smoothness/spec. `RCREF_DBG=<material re>` prints each drawn submesh of those materials. |
| `glsl <work> <levelN> <matRe> <out.txt> [kwRe]` | The full GLES3 text of a material's forward-pass variant (debugging). |
| `nodes <work> <levelN> [pathRe]` | Every GameObject as TSV: path, active, world position, yaw, scale, mesh, world AABB of the renderer's mesh bounds. Input of `../rcref_cache frame` (DevNotes/refdata/rc_cache/level7_nodes.tsv). |
| `psdump <work>` | PlayerSettings raw words. m_ActiveColorSpace = 1 (linear), after the 1024/768/960/600 default screen sizes and m_StereoRenderingPath = 1. |

Debug environment variables: `RCREF_PIX=x,y` prints what is drawn at a (supersampled) pixel: material, renderer and
colour. `RCREF_SHADE=<material re>` prints shading terms.

Example (21 poses, about 16 s): see `DevNotes/refdata/rc_ref/` (`poses.json`, `extra_poses.json`).
```
bash run.sh poses "<roomshots>/scenarios/rc_dorm.json" "<roomshots>/out/rc_dorm/rc_arrival.tiles.txt" "<roomshots>/out/rc_dorm/summary.txt" ../../refdata/rc_ref/poses.json extra=../../refdata/rc_ref/extra_poses.json
bash run.sh render ../../apkwork level7 ../../refdata/rc_ref/poses.json ../../refdata/rc_ref
```

## What is drawn
- **Renderers.** Every active (whole parent chain), enabled, not-LOD1+ MeshRenderer / SkinnedMeshRenderer with a
  mesh. Skinned meshes are drawn in bind pose at their GameObject. All submeshes are drawn, with material k on submesh
  k (submeshes beyond the material count are not drawn, as in Unity). World TRS comes from apkscene's Model (parent
  scale times local scale, no skew). Mirrored transforms reverse the winding.
- **Mesh decode.** Position, normal, colour and UV0 are read in any vertex format (float, half, unorm/snorm 8/16), from
  the inline vertex data or the `.resS` stream. Missing vertex colours read as white.
- **Materials.** apkscene matdesc's `MatDesc.Describe` is called by reflection. It gives the colour property, main
  texture slot, tiling/offset, alpha test and cutoff, blend (opaque / cutout / fade / add, with src/dst factors),
  vertex colours, the alpha-masked tint, `_EmissivePower` glow, `_EmissionColor` and hidden shaders. All of it is
  decided from the forward pass GLES3 GLSL. Face culling comes from that pass's state (a property-bound value is
  resolved through the material). The material is "lit" when the GLSL reads `unity_SHAr` / `_MainLightColor`.
  `_SpecColor` / `_Smoothness` are read when the GLSL uses them, and the material keyword `_RECEIVE_SHADOWS_OFF` is
  honoured.
- **Textures.** The main texture is decoded from the first mip of size <= texmax (ASTC through `Astc.cs`;
  ETC/uncompressed through `MatTex.cs`), with a box-filtered mip pyramid. Bilinear repeat sampling is used, and the mip
  is picked per pixel from the triangle's texel density, distance and view angle. Texture row 0 is v = 0. This was
  checked on readable posters.
- **Albedo** (linear; vertex colours raw, material colours sRGB to linear, HDR colours included):
  `lerp(tex, tex * vc * _BaseColor, tex.a)` where the shader has that alpha mask, else `tex * vc * _BaseColor`.
- **Lighting** follows the AG/Standard forward GLSL (Wood_Floor_Mat, RecCenter_Carpet_Taupe_mat):
  - diffuse = albedo * (1 - max(spec));
  - ambient = irradiance(n) * diffuse. The default is RenderSettings' trilight (sky / equator / ground) cosine-integrated
    over the sphere: Unity recomputes the ambient SH from the colours in trilight mode. `amb=probe` uses the serialized
    ambient probe instead (it differs: up 0.38,0.54,0.70 against trilight 0.26,0.34,0.52);
  - environment specular from the custom reflection cubemap (ReflectionProbe_Env_Baked, ASTC RGBM, mip by roughness),
    times `1/(r^2+1) * lerp(spec, grazing, (1-n.v)^4)`;
  - the KeyLight: Lambert plus the shader's specular term, shadowed by a 4096^2 sun shadow map over the level core
    (2.5 cm texels, 3x3 PCF, strength 0.934, alpha-tested casters, `m_CastShadows` respected);
  - Rec Room's compact additional lights, which the AG shaders evaluate per vertex: colour * intensity * n.l *
    smoothstep(1 - d^2/r^2) * smoothstep(cone)^2, with no inverse-square term. URPLight hemisphere lights use their
    inner/outer angles as the cone around the light's forward. Spots use inner/outer spot angles. This is evaluated
    per pixel here;
  - glow = tex.rgb * tex.a * _EmissivePower * vc * _BaseColor; _EmissionColor where the GLSL reads it;
  - linear fog from RenderSettings (not on additive materials).
- **Transparency.** Fade and additive triangles are drawn back to front after the opaque pass. Each is depth-tested
  and blended with its own src factor (SrcAlpha or One). Alpha = tex.a * _BaseColor.a * vc.a. Glass-named fade
  materials are capped at 0.3.
- **Terrain.** The height grid uses the TerrainData scale and position. Each layer's diffuse texture is tiled by its
  TerrainLayer tile size and blended by the splat map, times the layer's diffuse remap.
- **Background.** The level's sky cubemap is shown at mip 2 (RGBM, times _Exposure * _Tint * unity_ColorSpaceDouble).
- **Output.** Rendering is supersampled ss x ss. Each sample is clamped to 0..1 (an LDR target), the samples are
  box-filtered, then encoded to sRGB. There is no tonemapping, bloom or post-processing.

## Not drawn / known differences
- TextMeshPro text (door signs such as "DORM ROOM", boards): its meshes are built at runtime. UI canvases, particle
  systems and line renderers are not drawn either. Images that DynamicPoster scripts load at runtime are missing, so
  the poster cases are empty. AG/Mirror is drawn as its smudge texture, without a reflection.
- Store merch booths (`Systems/Storefront/...`), portal couches and the balls, discs and confetti guns are inactive in
  the level file. A script may enable them at runtime.
- The skylight light shafts (`LightShaftLocker_Mat`, AG/Unlit-Additive-Soft) have an HDR `_BaseColor` of (2, 1.75,
  0.64, 0.078). In linear space that is (4.95, 3.67, 0.37). Additive (SrcAlpha, One), they come out as strong yellow
  columns. This is what the data says. The game may soften them with bloom or tonemapping, which rcref does not have.
- The additional lights are per pixel here and per vertex in the game. Shading on large faces is therefore smoother
  here.
