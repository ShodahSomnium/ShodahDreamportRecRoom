# Rec Room bundles at runtime: measured facts (2026-09-23/24)

Source: the Unity 6000.3.5f2 batchmode probe (harness copy of the project at `SP\unityharness\RRHarness`, probe
`Assets/Editor/RRProbe.cs`, log `SP\unityharness\logs\probe_editor.txt`, renders `SP\research\feasibility_png\*.png`),
plus rrtool dumps. SP = `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad`.

## Loading
- `AssetBundle.LoadFromFile` works on every Rec Room bundle tried (6000.0.27 bundles in a 6000.3 player/editor),
  0 errors, 0 warnings. 0.42 ms per bundle to open (header only; LZ4 chunked, random access). A 300 MB bundle opens
  as cheaply as a small one; memory is paid only for assets actually loaded.
- **Only Addressables container entries can be loaded by API.** `bundle.LoadAsset<T>(containerKey)` works (keys are
  short ids like "cf", "d2", "bf6"; a few are full paths). `LoadAsset(objectName)` returns NULL,
  `LoadAllAssets<AudioClip>()` / `<Texture2D>()` return only container entries (0 for most bundles).
- **Implicit assets become resident when a container asset that references them is loaded**, including assets
  referenced only by Rec Room's (missing-script) MonoBehaviours: after `LoadAsset("cf")` (the root beer bottle) all 13
  AudioClips its DrinkableBottle/BreakableObject/WeaponAudio/ShakeAudio reference were resident. Find them with
  `Resources.FindObjectsOfTypeAll<AudioClip>()` / `<Texture2D>()` / `<GameObject>()` by name
  (`Object.FindObjectsByType` does NOT return assets). The referenced assets' bundles must be loaded first.
- Our dependency pass `RrReach.Bundles(bundle, pathId)` gives what Unity components need (bottle: 4 bundles instead
  of the 128 of the full dependency closure). For MonoBehaviour-referenced assets (clips, particle prefabs), load that
  asset's own bundle (+ its reach) as well.
- Unloading: `AssetBundle.UnloadAllAssetBundles(true)` 40 ms for ~400 bundles.

## Prefabs
- Instantiating a Rec Room prefab gives the built-in components (Transform, MeshFilter/Renderer, SkinnedMeshRenderer,
  colliders, Rigidbody, ParticleSystem(+Renderer), TrailRenderer, LineRenderer, LODGroup, Light, AudioSource,
  Animator) working, and Rec Room's own MonoBehaviours as inert **missing scripts** (no warnings on instantiate).
- Scripts that DO bind in Unity (43 classes): UnityEngine.UI (Image, Text, Button, layouts, masks, ScrollRect, ...),
  TMPro (TextMeshPro, TextMeshProUGUI, TMP_FontAsset, TMP_SpriteAsset), URP (Volume, VolumeProfile, Bloom, Tonemapping,
  ColorAdjustments..., UniversalAdditionalCameraData/LightData), EventSystem/EventTrigger. **Strip Volume, EventSystem,
  EventTrigger, Button, camera/light additional data from any instance** (they would act on the client) - instantiate
  under an inactive parent, DestroyImmediate them, then activate. Keep Image/TMP/layout on UI prefabs only if wanted.
- Layers used by Rec Room prefabs: 10 (tools), 17 (fractured/broken pieces), 2, 0, 5 (UI). Reassign to our layers.
- Rigidbody settings come through (bottle: mass 1, drag 0, angDrag 0.05).
- Rec Room's watch UI page prefab loads with UGUI/TMP bound but renders as grey blocks (its theming/rounded-corner
  scripts are missing) - build our own watch UI from its sprites/fonts/colours instead.

## Rendering
- Rec Room is a **URP** game. Its shaders (84 distinct, e.g. `AG/Standard-Opaque`, `AG/Standard-Opaque-Instanced`,
  `AG/Standard-Opaque-Emission`, `AG/MakerPenShape`, `AG/Unlit-Additive-Soft`, `Shader Forge/Particles/...`) all report
  `isSupported` under the project's URP 17.3 and **render correctly with their original materials** (bunk bed wood
  recolour, goblin, bottle: see `feasibility_png/editor_*_original.png`). Converting to URP Lit LOSES the look (the bunk
  bed's recolour mask turns white) - **keep the original materials**.
- **Depth priming hazard:** the Somnium client forces depth priming with a DepthNormals prepass (HBAO). Opaque shaders
  without a `DepthNormals` pass are invisible there. Measured pass names: `AG/Standard-Opaque` has ForwardLit,
  ShadowCaster, DepthOnly, DepthNormals (OK). `AG/Standard-Opaque-Instanced`, `AG/Standard-Opaque-Emission`,
  `AG/MakerPenShape` have ForwardLit, ShadowCaster, DepthOnly only. **Fix:** for a material whose shader has no pass
  named "DepthNormals" (`material.FindPass("DepthNormals") < 0`) and `renderQueue <= 2500`, set
  `renderQueue = 2501` (drawn in the transparent pass with its own ZWrite/ZTest, unaffected by priming).
- Particle materials often use the built-in `Sprites/Default` (not stereo-instanced: invisible in the right eye in
  VR). Swap to `Universal Render Pipeline/Particles/Unlit` (copy `_MainTex`->`_BaseMap`, `_Color`->`_BaseColor`,
  surface transparent, blend alpha or additive by the original's `_SrcBlend/_DstBlend` if present), keep the rest.
- Textures are DXT1/DXT5/BC7 with mip streaming flags; meshes readable or not (MeshCollider works either way).

## Audio
- Clips are ordinary Unity AudioClips (Vorbis in .resource; loadType DecompressOnLoad / CompressedInMemory /
  Streaming). They play through AudioSource directly.
- Rec Room clip struct: `{audioClip, configureVolumeInDecibel, volume, pitchVariation, pitchShift, subtitles}`.

## Timing
- 50 random bundles + closure (188 bundles, 189 MB): open 79 ms; LoadAllAssets on the 50: 553 ms.
- Instantiate a prop: 0.3-1.4 ms. UI page activate: 74 ms.
