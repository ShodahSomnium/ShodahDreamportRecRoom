using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// How <see cref="RrSceneKit"/> builds a placement. The defaults suit a room built behind the loading card in
    /// VR: everything on, meshes merged, lights capped with no shadows, global render settings left alone.
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

        public bool CastShadows;

        /// <summary>Box colliders on one object with the same rotation (a static body with many shapes).</summary>
        public int MaxCollidersPerObject = 256;

        /// <summary>Pre-cook mesh colliders on the worker (Physics.BakeMesh is thread safe) instead of on assignment.</summary>
        public bool BakeMeshCollidersOnWorker = true;

        /// <summary>Give stand-ins box colliders of their own (the scene's C records normally cover them).</summary>
        public bool StandInColliders;

        /// <summary>Stand-in colours are rounded to this many steps per channel to keep the material count low.</summary>
        public int ColourSteps = 32;

        /// <summary>Realtime lights kept (brightest by intensity x range); shadows are always off.</summary>
        public int MaxLights = 8;

        /// <summary>Scene directional lights are skipped: the world has its own sun.</summary>
        public bool DirectionalLights;

        public float LightIntensityScale = 1f;

        /// <summary>Keep the lights inside spawned prefabs (not counted against MaxLights; off by default).</summary>
        public bool PrefabLights;

        /// <summary>Looping ambient sources started (loudest first).</summary>
        public int MaxAudio = 12;

        public float AudioVolumeScale = 1f;

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
