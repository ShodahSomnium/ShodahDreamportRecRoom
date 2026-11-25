using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// How <see cref="RrSceneKit"/> builds a placement. The defaults suit a room built behind the loading card in
    /// VR: everything on, meshes merged, shadows cast, Rec Room's own light rig (its lights, the few shadowed ones),
    /// overlaps removed; global render settings are <see cref="RrEnvironment"/>'s (the scene registers itself).
    /// </summary>
    public sealed class RrSceneKitOptions
    {
        /// <summary>
        /// Where the scene's origin goes in the parent: the kit's root object gets this local pose, and every
        /// record keeps its own scene-space transform under it. Markers are reported in the parent's space.
        /// </summary>
        public Vector3 Position;

        public Quaternion Rotation = Quaternion.identity;

        /// <summary>Only records whose position (or centre) is inside, in scene space before the offset; null = all.</summary>
        public Bounds? Filter;

        public bool SkipRenderers;
        public bool SkipPrefabs;
        public bool SkipStandIns;
        public bool SkipColliders;
        public bool SkipMeshColliders;
        public bool SkipLights;
        public bool SkipAudio;
        public bool SkipMarkers;

        /// <summary>Install names (meshes or prefabs) to leave out; marker kinds to leave out.</summary>
        public HashSet<string> SkipNames;
        public HashSet<string> SkipMarkerKinds;

        /// <summary>
        /// Drop X_LOD1..X_LOD7 renderers when X_LOD0 is in the same mesh table (a scene lists every LOD level of a
        /// LODGroup; without Rec Room's LODGroup they would all draw on top of each other).
        /// </summary>
        public bool DropHigherLods = true;

        /// <summary>Merge readable static meshes per material, vertex layout and spatial cell.</summary>
        public bool Combine = true;

        /// <summary>Vertices per merged mesh (16-bit indices stay possible below 65,535).</summary>
        public int MaxBatchVertices = 60000;

        /// <summary>Side of the cube cells merged meshes are split by (metres), so far parts can be culled. 0 = no split.</summary>
        public float CellSize = 32f;

        /// <summary>
        /// Renderers, stand-ins and prefabs cast shadows (iteration 8: on). Rec Room lit every built-in level in realtime
        /// with a soft-shadow key light and no lightmaps (61 Android levels measured, DevNotes/iter8/env.md), so the
        /// shadows are part of its look; with them off the rooms were flat ("there is no shadows or anything").
        /// </summary>
        public bool CastShadows = true;

        /// <summary>
        /// Light the scene the way Rec Room did (<see cref="RrEnvironment.ForScene"/>): keep as many point and spot lights
        /// as the level ran (up to <see cref="MaxEnvironmentLights"/>, at least <see cref="MaxLights"/>), give shadows to
        /// the ones that had them (at most <see cref="MaxShadowSpots"/> spots and <see cref="MaxShadowPoints"/> points),
        /// and merge meshes in smaller cells when many lights are kept (<see cref="LightCellSize"/>).
        /// </summary>
        public bool EnvironmentLights = true;

        /// <summary>Upper bound of the lights kept by <see cref="EnvironmentLights"/> (the Golden Trophy areas ran 30-70).</summary>
        public int MaxEnvironmentLights = 48;

        /// <summary>Shadowed spot lights at most (Rec Room: 1-3 per area; each costs one shadow-map tile).</summary>
        public int MaxShadowSpots = 3;

        /// <summary>Shadowed point lights at most (six tiles each; only the Armory had any).</summary>
        public int MaxShadowPoints = 1;

        /// <summary>
        /// Merge cell side (metres) when more than <see cref="LightCellThreshold"/> point/spot lights are kept in a scene
        /// without a key light (quest areas; under a sun they are accents): URP's
        /// forward renderer lights each object with at most 4 additional lights (the client's per-object limit), so one
        /// merged mesh across a torch-lit hall would take 4 torches for the whole hall. 0 = keep <see cref="CellSize"/>; a
        /// CellSize the caller set at most 1.5 x this is kept.
        /// </summary>
        public float LightCellSize = 6f;

        public int LightCellThreshold = 8;

        /// <summary>
        /// Iteration 9: light Rec Room's own shaders through Rec Room's light grid (<see cref="RrLightGrid"/>). Rec Room's
        /// shaders ignore URP's per-object lights; without the grid the client lights them with the sun and ambient only
        /// (the Golden Trophy torches lit nothing). Every point/spot light of the scene goes in (up to 160, as in Rec Room),
        /// the Unity lights (<see cref="MaxLights"/>, <see cref="MaxEnvironmentLights"/>) stay for URP-shaded objects and
        /// avatars. With it on, meshes merge in <see cref="CellSize"/> cells whatever the light count.
        /// </summary>
        public bool LightGrid = true;

        /// <summary>
        /// In such a scene (many lights, no key light) with at most this many renderers, nothing is merged: Rec Room drew
        /// them separately, and each renderer then picks its own nearest lights. Above it, <see cref="LightCellSize"/> cells.
        /// </summary>
        public int LightSeparateMax = 400;

        /// <summary>
        /// Overlaps (iteration 8, "there is also some mesh overlapping"): drop exact duplicate renderers, stand-ins and
        /// prefabs (same mesh and pose), drop stand-ins and "dress" markers that are the same object as an install
        /// renderer or prefab (box IoU at least <see cref="OverlapIoU"/>), and inset stand-in faces by 3-6 mm so a face
        /// coplanar with install art or another stand-in does not z-fight.
        /// </summary>
        public bool FixOverlaps = true;

        public float OverlapIoU = 0.5f;

        /// <summary>Box colliders on one object with the same rotation (a static body with many shapes).</summary>
        public int MaxCollidersPerObject = 256;

        /// <summary>Pre-cook mesh colliders on the worker (Physics.BakeMesh is thread safe) instead of on assignment.</summary>
        public bool BakeMeshCollidersOnWorker = true;

        /// <summary>Give stand-ins box colliders of their own (the scene's C records normally cover them).</summary>
        public bool StandInColliders;

        /// <summary>Stand-in colours are rounded to this many steps per channel to keep the material count low.</summary>
        public int ColourSteps = 32;

        /// <summary>
        /// Realtime lights kept at least (brightest by intensity x range); with <see cref="EnvironmentLights"/> as many as
        /// the level had, and the ones Rec Room shadowed get shadows.
        /// </summary>
        public int MaxLights = 8;

        /// <summary>Scene directional lights are skipped: the world's sun (<see cref="RrEnvironment"/>) takes the key light's values.</summary>
        public bool DirectionalLights;

        public float LightIntensityScale = 1f;

        /// <summary>Keep the lights inside spawned prefabs (not counted against MaxLights; off by default).</summary>
        public bool PrefabLights;

        /// <summary>Looping ambient sources started (loudest first).</summary>
        public int MaxAudio = 12;

        public float AudioVolumeScale = 1f;

        /// <summary>Leave out the level effects (F records).</summary>
        public bool SkipEffects;

        /// <summary>
        /// Level effects active at once, the nearest to the local head (<see cref="RrSceneKitFx"/>): 40 is well above
        /// what one Golden Trophy area holds (9-19) and keeps a Crescendo hall (29 torches) whole.
        /// </summary>
        public int MaxEffects = 40;

        /// <summary>Effects placed per scene at most (each is a particle system clone).</summary>
        public int MaxEffectsBuilt = 96;

        /// <summary>The effects' own loops (a torch's crackle) playing at once, the nearest first.</summary>
        public int MaxEffectSounds = 8;

        /// <summary>Particles per system at most (act 4's lava asks for 1,000 two-metre ones).</summary>
        public int MaxEffectParticles = 150;

        /// <summary>
        /// Place the flame cards (F card=1: the candle's LaternFlame_Mat card on each wall torch, the flame Rec Room drew
        /// as part of its torch mesh). Off leaves the torches with their embers and crackle only.
        /// </summary>
        public bool FlameCards = true;

        /// <summary>
        /// Apply the scene's ambient (flat, or trilight when the scene says so) / skybox material to RenderSettings
        /// while the scene exists; the previous values come back when the kit's root is destroyed. Off by default:
        /// global state. Fog is never applied - the world scene is saved with fog off, so fog variants are stripped
        /// (harness README, "Fog choice").
        /// </summary>
        public bool ApplyAmbient;

        public bool ApplySky;

        /// <summary>Main-thread work per frame before the build yields (ms).</summary>
        public float FrameBudgetMs = 4f;

        /// <summary>Asset loads in flight at once.</summary>
        public int MaxConcurrentLoads = 24;

        /// <summary>
        /// Seconds to wait for the install's assets before building with what arrived. Below RrTravel.BuildTimeout
        /// (90 s for a whole room build, after which travel gives the room up), so a slow load still leaves a room.
        /// </summary>
        public float LoadTimeout = 60f;

        /// <summary>Name of the kit's root object (default "Rr Scene &lt;scene name&gt;").</summary>
        public string RootName;
    }
}
