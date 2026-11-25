using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>What <see cref="RrSceneKit.Build"/> made: the root, the gameplay markers, counts and what was missing.</summary>
    public sealed class RrSceneKitResult
    {
        public string Scene = string.Empty;

        /// <summary>The kit's root (child of the parent, at the options' offset); null if the build failed or was cancelled.</summary>
        public GameObject Root;

        /// <summary>Parsed data (kept so a game can read records the kit does not build).</summary>
        public RrSceneData Data;

        /// <summary>Set when nothing could be built (parse failure, no RrAssets) or the parent went away.</summary>
        public string Error;

        public bool Cancelled;

        /// <summary>The scene's bounds in its own space, and in the parent's (offset applied; axis-aligned box of the rotated bounds).</summary>
        public Bounds SceneBounds;

        public Bounds Bounds;

        public readonly List<RrSceneMarker> Markers = new List<RrSceneMarker>();

        // Renderers
        public int Renderers;            // R records built (merged or separate)
        public int Batches;              // merged meshes made
        public int BatchesFailed;        // merges Unity refused (their renderers are left out; logged once)
        public int Separate;             // renderers kept as their own object
        public int Unreadable;           // ... of those, because the install mesh is not readable
        public int Mirrored;             // ... because of a negative scale
        public int LodsDropped;
        public int SkippedRenderers;     // missing mesh, skipped name, outside the filter
        public int Vertices;             // in merged meshes

        public int Prefabs;
        public int StandIns;
        public int StandInBatches;

        public int BoxColliders;
        public int ColliderObjects;      // objects carrying the grouped box/sphere colliders
        public int MeshColliders;
        public int OtherColliders;
        public int MeshesBaked;

        public int Lights;
        public int LightsDropped;
        public int ShadowLights;         // kept lights given shadows (Rec Room's shadowed spots/points)
        public float CellSize;           // merge cell side used (smaller when many lights are kept)

        /// <summary>Iteration 9: Rec Room's light grid for this scene's Rec Room shaders (null: no point/spot lights, or LightGrid off).</summary>
        public RrLightGrid Grid;

        public int GridLights;

        /// <summary>Overlaps removed (iteration 8): exact duplicates, stand-ins / dress markers that repeat install art.</summary>
        public int DuplicateRenderers;
        public int DuplicateStandIns;
        public int DuplicatePrefabs;
        public int StandInsOverArt;
        public int DressOverArt;
        public int StandInsInset;
        public int AudioLoops;
        public int AudioDropped;

        /// <summary>F records kept for placement, and (filled in when the room is ready, <see cref="RrSceneKitFx"/>) placed, left out for want of a wall, and their loops.</summary>
        public int Effects;
        public int EffectsPlaced;
        public int EffectsNoWall;
        public int EffectSounds;
        public int EffectTemplates;
        public readonly List<string> MissingEffects = new List<string>();

        /// <summary>Names the extractor marked unknown ("?name": not matched to the install); skipped without a lookup.</summary>
        public int UnknownNames;

        public int MeshesLoaded;
        public int MaterialsLoaded;

        public readonly List<string> MissingMeshes = new List<string>();
        public readonly List<string> MissingMaterials = new List<string>();
        public readonly List<string> MissingPrefabs = new List<string>();
        public readonly List<string> MissingClips = new List<string>();

        /// <summary>Environment as the scene stored it (RrEnvironment applies it while the player is in the scene).</summary>
        public string Fog;

        public bool HasAmbient;
        public Color Ambient;
        public string Sky;
        public bool AmbientApplied;
        public bool SkyApplied;

        // Timing
        public float ParseMs;
        public float LoadMs;
        public float BuildMs;            // wall time from start to done
        public float WorkMs;             // main-thread time spent in the kit
        public float LongestStepMs;
        public int Frames;

        /// <summary>Markers of one kind (in file order).</summary>
        public List<RrSceneMarker> MarkersOf(string kind)
        {
            var list = new List<RrSceneMarker>();
            foreach (var m in Markers)
            {
                if (m.Kind == kind)
                {
                    list.Add(m);
                }
            }

            return list;
        }

        /// <summary>The first marker of a kind (and name, when given), or null.</summary>
        public RrSceneMarker Marker(string kind, string name = null)
        {
            foreach (var m in Markers)
            {
                if (m.Kind == kind && (name == null || m.Name == name))
                {
                    return m;
                }
            }

            return null;
        }

        /// <summary>A marker's pose in world space (follows the root if the room moved), or its parent-space pose without a root.</summary>
        public Pose WorldPose(RrSceneMarker m)
        {
            if (Root == null)
            {
                return m.Pose;
            }

            var t = Root.transform;
            return new Pose(t.TransformPoint(m.ScenePosition), t.rotation * m.SceneRotation);
        }

        /// <summary>The one-line summary the kit logs.</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(Scene).Append(": ");
            if (Error != null)
            {
                sb.Append(Cancelled ? "CANCELLED " : "FAILED ").Append(Error).Append("; ");
            }

            sb.Append("renderers ").Append(Renderers).Append(" -> ").Append(Batches).Append(" merged (").Append(Vertices).Append(" verts) + ").Append(Separate).Append(" separate");
            if (Separate > 0)
            {
                sb.Append(" (").Append(Unreadable).Append(" unreadable, ").Append(Mirrored).Append(" mirrored)");
            }

            if (BatchesFailed > 0)
            {
                sb.Append(", MERGES FAILED ").Append(BatchesFailed);
            }

            if (LodsDropped > 0)
            {
                sb.Append(", LODs dropped ").Append(LodsDropped);
            }

            if (SkippedRenderers > 0)
            {
                sb.Append(", skipped ").Append(SkippedRenderers);
            }

            sb.Append("; prefabs ").Append(Prefabs).Append("; stand-ins ").Append(StandIns).Append(" in ").Append(StandInBatches).Append(" meshes");
            sb.Append("; colliders box ").Append(BoxColliders).Append(" + other ").Append(OtherColliders).Append(" on ").Append(ColliderObjects).Append(" objects, mesh ").Append(MeshColliders);
            if (MeshesBaked > 0)
            {
                sb.Append(" (").Append(MeshesBaked).Append(" pre-baked)");
            }

            sb.Append("; lights ").Append(Lights).Append(ShadowLights > 0 ? " (" + ShadowLights + " with shadows)" : "").Append(LightsDropped > 0 ? " (" + LightsDropped + " not as Unity lights)" : "");
            if (GridLights > 0)
            {
                sb.Append(", ").Append(GridLights).Append(" in Rec Room's light grid");
            }
            if (CellSize > 0f)
            {
                sb.Append(", merge cells ").Append(CellSize.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(" m");
            }

            int overlaps = DuplicateRenderers + DuplicateStandIns + DuplicatePrefabs + StandInsOverArt + DressOverArt;
            if (overlaps > 0 || StandInsInset > 0)
            {
                sb.Append("; overlaps removed ").Append(overlaps).Append(" (duplicate renderers ").Append(DuplicateRenderers).Append(", stand-ins ").Append(DuplicateStandIns).Append(", prefabs ").Append(DuplicatePrefabs)
                  .Append("; stand-ins over install art ").Append(StandInsOverArt).Append(", dress markers over install art ").Append(DressOverArt).Append("), stand-ins inset ").Append(StandInsInset);
            }
            sb.Append("; audio ").Append(AudioLoops).Append(AudioDropped > 0 ? " (" + AudioDropped + " dropped)" : "");
            if (Effects > 0)
            {
                sb.Append("; effects ").Append(Effects).Append(" from ").Append(EffectTemplates).Append(" install templates (placed when the room is ready)");
            }

            sb.Append("; markers ").Append(Markers.Count);
            if (UnknownNames > 0)
            {
                sb.Append("; unknown names ").Append(UnknownNames);
            }

            sb.Append("; assets meshes ").Append(MeshesLoaded).Append(", materials ").Append(MaterialsLoaded);
            AppendMissing(sb, "meshes", MissingMeshes);
            AppendMissing(sb, "materials", MissingMaterials);
            AppendMissing(sb, "prefabs", MissingPrefabs);
            AppendMissing(sb, "clips", MissingClips);
            AppendMissing(sb, "effects", MissingEffects);
            if (Fog != null)
            {
                sb.Append("; fog ").Append(Fog);
            }

            if (HasAmbient)
            {
                sb.Append("; ambient ").Append(ColorUtility.ToHtmlStringRGB(Ambient)).Append(AmbientApplied ? " applied" : " reported");
            }

            if (Sky != null)
            {
                sb.Append("; sky ").Append(Sky).Append(SkyApplied ? " applied" : " reported");
            }

            sb.Append("; parse ").Append(ParseMs.ToString("0")).Append(" ms, loads ").Append(LoadMs.ToString("0")).Append(" ms, build ").Append(BuildMs.ToString("0"))
              .Append(" ms (main thread ").Append(WorkMs.ToString("0")).Append(" ms over ").Append(Frames).Append(" frames, longest step ").Append(LongestStepMs.ToString("0.0")).Append(" ms)");
            return sb.ToString();
        }

        private static void AppendMissing(StringBuilder sb, string what, List<string> names)
        {
            if (names.Count == 0)
            {
                return;
            }

            sb.Append("; missing ").Append(what).Append(' ').Append(names.Count).Append(" (");
            for (int i = 0; i < names.Count && i < 6; i++)
            {
                sb.Append(i > 0 ? ", " : "").Append(names[i]);
            }

            sb.Append(names.Count > 6 ? ", ...)" : ")");
        }
    }
}
