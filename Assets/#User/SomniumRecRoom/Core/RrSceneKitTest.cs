using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Play-mode harness hooks for <see cref="RrSceneKit"/> (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.RrSceneKitTest.Build"}</c>,
    /// scenario DevNotes/tools/harness/scenarios/scenekit_test.json). Nothing here runs unless called.
    ///
    /// <see cref="Build"/> builds <see cref="Sample"/> (DevNotes/tools/rrscene/kit_sample.rrsk: a bench row, curtains,
    /// barrels, lockers, boxes, an unreadable two-material turkey leg, a mirrored bench, an LOD1 duplicate, stand-ins,
    /// two install prefabs, grouped and mesh colliders, three lights plus a directional one, one ambient loop, one
    /// one-shot and gameplay markers, with deliberately missing and "?"-unknown names) at offset (-10, 0, -4) under
    /// the world root, so it stands on the Arrival floor 2-10 m in front of the harness camera at (0, 1.6, -3).
    /// It also parses the generated RRSZ1 copy (<see cref="RrSceneKitSampleZ"/>) and checks both parse alike, then
    /// compares the build's counts with what the sample must give and logs one line
    /// "SceneKitTest: PASS ..." or "SceneKitTest: FAIL &lt;what&gt; ...". <see cref="Stress"/> builds a generated
    /// 2,000-renderer / 2,000-box scene and logs its timing; <see cref="Cancel"/> destroys the parent mid-build.
    ///
    /// Every name in the sample was checked against rrtool's index of the install (objects.tsv, 2026-09-24).
    /// </summary>
    public static class RrSceneKitTest
    {
        /// <summary>Where the sample's scene origin goes: scene (10, 0, 10) lands at (0, 0, 6).</summary>
        public static readonly Vector3 Offset = new Vector3(-10f, 0f, -4f);

        private static GameObject _holder;

        /// <summary>The last test build (for dumps).</summary>
        public static RrSceneKitResult Last;

        /// <summary>DevNotes/tools/rrscene/kit_sample.rrsk, as the plain form of rrsk2cs.sh writes it (uncompacted).</summary>
        public const string Sample =
            "# RRSK 1 KitSample\n"
            + "# Test placement for Core/RrSceneKitTest.cs (a copy is inline there; the RRSZ1 copy Core/RrSceneKitSampleZ.cs is generated from this file).\n"
            + "# Install names verified in rrtool's objects.tsv (2026-09-24): meshes Bench_LOD0/LOD1, Curtain_LOD0, WineBarrel_LOD0, Lockers_LOD0,\n"
            + "# PropBox_Square (readable), TurkeyLeg_Whole_LOD0 (NOT readable, 2 submeshes); their renderers' materials; prefabs\n"
            + "# (Sandbox_Barrel), (Sandbox_Table_Coffee_Large); clips AmbianceSummerOutdoor, Paintball_Ambiance_ClearCut01.\n"
            + "# The *_KitTest and ?names are deliberately missing / unknown. Scene centre (10, 0, 10); the test builds it at offset (-10, 0, -4).\n"
            + "B 5.5 -0.2 5.5 14.5 3 14.5\n"
            + "M 0 Bench_LOD0\n"
            + "M 1 Bench_LOD1\n"
            + "M 2 Curtain_LOD0\n"
            + "M 3 WineBarrel_LOD0\n"
            + "M 4 Lockers_LOD0\n"
            + "M 5 PropBox_Square\n"
            + "M 6 TurkeyLeg_Whole_LOD0\n"
            + "M 7 NoSuchMesh_KitTest\n"
            + "M 8 ?UnknownMesh_KitTest\n"
            + "T 0 Table_Booth_Diner_Mat\n"
            + "T 1 Curtain_Red_Mat\n"
            + "T 2 Barrel_Table_Crate_Mat\n"
            + "T 3 LockerFountainStageLight_mat\n"
            + "T 4 PropBox_Makerpen_mat\n"
            + "T 5 Consumable_TurkeyLeg_Tray_Mat\n"
            + "T 6 Consumable_TurkeyLeg_Mat\n"
            + "T 7 NoSuchMaterial_KitTest\n"
            + "R 0 0 7 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 8.2 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 9.4 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 10.6 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 11.8 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 13 0 12 0 0 0 1 1 1 1\n"
            + "R 1 0 7 0 12 0 0 0 1 1 1 1\n"
            + "R 0 0 10 0 13.5 0 1 0 0 -1 1 1\n"
            + "R 2 1 6 0 14.3 0 0 0 1 1 1 1\n"
            + "R 2 1 14 0 14.3 0 0 0 1 1 1 1\n"
            + "R 3 2 8 0 9.5 0 0.2588 0 0.9659 1 1 1\n"
            + "R 3 2 8.7 0 9.5 0 0 0 1 1 1 1\n"
            + "R 4 3 13.8 0 11 0 -0.7071 0 0.7071 1 1 1\n"
            + "R 5 4 11.2 0 9.5 0 0 0 1 0.5 0.5 0.5\n"
            + "R 6 5,6 10 0.8 9.5 0 0 0 1 1 1 1\n"
            + "R 7 7 10 0 11 0 0 0 1 1 1 1\n"
            + "R 8 0 10 0 11 0 0 0 1 1 1 1\n"
            + "R 5 7 12 0 9.5 0 0 0 1 0.5 0.5 0.5\n"
            + "X box 10 -0.1 10 4.5 0.1 4.5 0 0 0 1 0.55 0.45 0.35 1\n"
            + "X cyl 6.5 1.25 13 0.25 1.25 0.25 0 0 0 1 0.8 0.8 0.85 1\n"
            + "X box 13.5 0.3 8.5 0.3 0.3 0.3 0 0.3827 0 0.9239 0.9 0.6 0.2 1\n"
            + "X box 10 1.5 14.45 4 1.5 0.05 0 0 0 1 0.6 0.8 1 0.35\n"
            + "P (Sandbox_Barrel) 7.5 0 8.5 0 0 0 1 1 1 1\n"
            + "P (Sandbox_Table_Coffee_Large) 12 0 7.5 0 0.7071 0 0.7071 1 1 1\n"
            + "P (NoSuchPrefab_KitTest) 10 0 8 0 0 0 1 1 1 1\n"
            + "C box 10 -0.1 10 0 0 0 1 9 0.2 9\n"
            + "C box 10 1.5 14.5 0 0 0 1 9 3 0.2\n"
            + "C box 13.5 0.3 8.5 0 0.3827 0 0.9239 0.6 0.6 0.6\n"
            + "C sphere 7 0.5 11 0.5\n"
            + "C capsule 6.5 1.25 13 0 0 0 1 0.25 2.5\n"
            + "C mesh 0 7 0 12 0 0 0 1 1 1 1\n"
            + "L point 8 2.5 10 0 0 0 1 1 0.9 0.8 1.2 8 30\n"
            + "L point 12 2.5 10 0 0 0 1 1 0.9 0.8 1.2 8 30\n"
            + "L spot 10 3 8 0.7071 0 0 0.7071 1 1 1 2 10 60\n"
            + "L dir 0 10 0 0.4082 0.2345 0.1085 0.8754 1 1 1 1 0 0\n"
            + "A AmbianceSummerOutdoor 10 1 10 0.2 1.5 20 1\n"
            + "A Paintball_Ambiance_ClearCut01 10 1 12 0.5 1 15 0\n"
            + "A ?UnknownClip_KitTest 10 1 10 1 1 10 1\n"
            + "G spawn Spawn%20A 9 0 7 0 0 0 1 team=0 index=0\n"
            + "G spawn Spawn%20B 11 0 7 0 0 0 1 team=1 index=1\n"
            + "G flag_base BlueFlag 7 0 13 0 0 0 1 team=0\n"
            + "G trigger_zone Goal 13 1 13 0 0 0 1 shape=box size=2,2,2\n"
            + "E fog linear 0.7 0.8 0.9 0.01 10 80\n"
            + "E ambient 0.5 0.55 0.6 mode=trilight intensity=1 equator=0.45,0.45,0.45 ground=0.3,0.28,0.25\n"
            + "E sky Default-Skybox\n";

        private static void Log(string text)
        {
            RrLog.Info(RrSceneKit.Area, "SceneKitTest: " + text);
        }

        private static IEnumerator WaitReady()
        {
            float until = Time.realtimeSinceStartup + 180f;
            while (!RrMain.Ready && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static Transform Holder(string name)
        {
            Clear();
            var parent = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _holder = new GameObject(name);
            _holder.transform.SetParent(parent, false);
            return _holder.transform;
        }

        /// <summary>Destroys what the hooks built.</summary>
        /// <summary>
        /// Harness hook (iteration 5): one line per live effects group (<see cref="RrSceneKitFx"/>) with its counts and
        /// the world position of every placed effect and loop that is running, so shots can be aimed at them
        /// ("SceneKitTest: effects GTact4: ...").
        /// </summary>
        public static void Effects()
        {
            var all = Object.FindObjectsByType<RrSceneKitFx>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var ci = CultureInfo.InvariantCulture;
            foreach (var fx in all)
            {
                var sb = new StringBuilder("SceneKitTest: effects ");
                sb.Append(fx.Result != null ? fx.Result.Scene : fx.name).Append(fx.gameObject.activeInHierarchy ? "" : " (inactive)").Append(": placed ").Append(fx.Placed)
                  .Append(", no wall ").Append(fx.NoWall).Append(", moved onto a wall ").Append(fx.Moved).Append(", sounds ").Append(fx.Sounds).Append(':');
                var t = fx.transform;
                for (int i = 0; i < t.childCount; i++)
                {
                    var c = t.GetChild(i);
                    var ps = c.GetComponentInChildren<ParticleSystem>(true);
                    var a = c.GetComponentInChildren<AudioSource>(true);
                    if (ps == null && a == null)
                    {
                        continue;
                    }

                    var p = c.position;
                    sb.Append(' ').Append(ps != null ? (c.gameObject.activeSelf ? "fx" : "fx(off)") : (a.isPlaying ? "snd" : "snd(off)")).Append('(')
                      .Append(p.x.ToString("0.0", ci)).Append(',').Append(p.y.ToString("0.0", ci)).Append(',').Append(p.z.ToString("0.0", ci)).Append(')');
                    if (ps != null)
                    {
                        sb.Append('n').Append(ps.particleCount);
                    }
                    else if (a.clip != null)
                    {
                        sb.Append(a.clip.name);
                    }
                }

                RrLog.Info("SceneKitTest", sb.ToString().Substring("SceneKitTest: ".Length));
                // Shot poses for the first wall effects: 2.2 m out from the wall, 0.3 m up, looking at the flame.
                for (int w = 0; w < fx.OnWalls.Count && w < 3; w++)
                {
                    var ray = fx.OnWalls[w];
                    var eye = ray.origin + ray.direction * 2.2f + Vector3.up * 0.3f;
                    RrLog.Info("SceneKitTest", "wall effect " + w + " of " + (fx.Result != null ? fx.Result.Scene : fx.name) + ": \"pos\": [" + eye.x.ToString("0.00", ci) + ", " + eye.y.ToString("0.00", ci) + ", " + eye.z.ToString("0.00", ci)
                        + "], \"look\": [" + ray.origin.x.ToString("0.00", ci) + ", " + ray.origin.y.ToString("0.00", ci) + ", " + ray.origin.z.ToString("0.00", ci) + "]");
                }

                // The first effect's renderer, for "why can't I see it" (material, blend, colour, bounds, layer).
                var first = t.GetComponentInChildren<ParticleSystemRenderer>(true);
                if (first != null)
                {
                    var m = first.sharedMaterial;
                    var fps = first.GetComponent<ParticleSystem>();
                    RrLog.Info("SceneKitTest", "effect renderer " + first.name + ": active " + first.gameObject.activeInHierarchy + ", enabled " + first.enabled + ", layer " + first.gameObject.layer
                        + ", mode " + first.renderMode + ", material " + (m != null ? m.name + " / " + m.shader.name + " queue " + m.renderQueue
                        + (m.HasProperty("_DstBlend") ? " dst " + m.GetFloat("_DstBlend") : "") + (m.HasProperty("_BaseColor") ? " colour " + m.GetColor("_BaseColor") : "")
                        + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? " map " + m.GetTexture("_BaseMap").name : m.mainTexture != null ? " tex " + m.mainTexture.name : " no texture") : "none")
                        + ", particles " + (fps != null ? fps.particleCount + " size " + fps.main.startSizeMultiplier.ToString("0.###", ci) + " colour " + fps.main.startColor.color : "-")
                        + ", bounds " + first.bounds.center.ToString("F1") + " size " + first.bounds.size.ToString("F2"));
                }
            }

            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mr.name != "Light Shafts")
                {
                    continue;
                }

                var m = mr.sharedMaterial;
                RrLog.Info("SceneKitTest", "light shafts: active " + mr.gameObject.activeInHierarchy + ", layer " + mr.gameObject.layer + ", material " + (m != null ? m.name + " / " + m.shader.name + " queue " + m.renderQueue
                    + (m.HasProperty("_BaseColor") ? " colour " + m.GetColor("_BaseColor") : "") + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? " map " + m.GetTexture("_BaseMap").name : "") : "none")
                    + ", bounds " + mr.bounds.center.ToString("F1") + " size " + mr.bounds.size.ToString("F1"));
            }

            if (all.Length == 0)
            {
                RrLog.Info("SceneKitTest", "effects: no live effect groups");
            }
        }

        public static void Clear()
        {
            if (_holder != null)
            {
                Object.Destroy(_holder);
            }

            _holder = null;
        }

        public static IEnumerator Build()
        {
            var wait = WaitReady();
            while (wait.MoveNext())
            {
                yield return wait.Current;
            }

            if (!RrMain.Ready)
            {
                Log("FAIL RrMain not ready");
                yield break;
            }

            // 1. The inline text and the generated RRSZ1 copy parse to the same records.
            RrSceneData plain = null, packed = null;
            string error = null;
            var p1 = RrSceneKit.Parse(Sample, (d, e) =>
            {
                plain = d;
                error = e;
            });
            while (p1.MoveNext())
            {
                yield return p1.Current;
            }

            var p2 = RrSceneKit.Parse(RrSceneKitSampleZ.Text(), (d, e) =>
            {
                packed = d;
                error = error ?? e;
            });
            while (p2.MoveNext())
            {
                yield return p2.Current;
            }

            if (plain == null || packed == null)
            {
                Log("FAIL parse: " + error);
                yield break;
            }

            var fails = new List<string>();
            // Records only: the inline copy keeps comment lines that the generator drops, so the line counts differ
            // (measured 69 vs 64 for the same 18 renderers, 4 stand-ins, 6 colliders, 4 lights, 3 audio, 4 markers).
            if (Records(plain) != Records(packed))
            {
                fails.Add("plain/RRSZ1 parse differ (" + plain.Describe() + " vs " + packed.Describe() + ")");
            }

            // 2. Build the plain one.
            var parent = Holder("Rr SceneKit Test");
            RrSceneKitResult r = null;
            var build = RrSceneKit.Build(plain, parent, new RrSceneKitOptions { Position = Offset }, x => r = x);
            while (build.MoveNext())
            {
                yield return build.Current;
            }

            Last = r;
            if (r == null)
            {
                Log("FAIL no result");
                yield break;
            }

            // 3. What the sample must give (see kit_sample.rrsk).
            Expect(fails, "error", r.Error == null ? 0 : 1, 0);
            Expect(fails, "renderers", r.Renderers, 15);             // 18 R - LOD1 - missing mesh - unknown mesh
            Expect(fails, "merged meshes", r.Batches, 6);            // bench row, curtains, barrels, lockers, box, fallback box
            Expect(fails, "separate", r.Separate, 2);                // mirrored bench + turkey leg
            Expect(fails, "unreadable", r.Unreadable, 1);            // TurkeyLeg_Whole_LOD0 (m_IsReadable false in the bundle)
            Expect(fails, "mirrored", r.Mirrored, 1);
            Expect(fails, "LODs dropped", r.LodsDropped, 1);
            Expect(fails, "skipped renderers", r.SkippedRenderers, 2);
            Expect(fails, "stand-ins", r.StandIns, 4);
            Expect(fails, "stand-in meshes", r.StandInBatches, 4);   // four colours (one glass)
            Expect(fails, "prefabs", r.Prefabs, 2);
            Expect(fails, "box colliders", r.BoxColliders, 3);
            Expect(fails, "other colliders", r.OtherColliders, 2);   // sphere + capsule
            Expect(fails, "mesh colliders", r.MeshColliders, 1);
            Expect(fails, "collider objects", r.ColliderObjects, 2); // identity-rotation group + the 45-degree crate
            Expect(fails, "lights", r.Lights, 3);                    // the directional one is skipped
            Expect(fails, "lights dropped", r.LightsDropped, 1);
            Expect(fails, "audio loops", r.AudioLoops, 1);
            Expect(fails, "markers", r.Markers.Count, 5);            // 4 G + the one-shot audio source
            Expect(fails, "unknown names", r.UnknownNames, 2);
            Expect(fails, "missing meshes", r.MissingMeshes.Count, 1);
            Expect(fails, "missing materials", r.MissingMaterials.Count, 1);
            Expect(fails, "missing prefabs", r.MissingPrefabs.Count, 1);
            Expect(fails, "missing clips", r.MissingClips.Count, 0);
            Expect(fails, "fog reported", r.Fog != null ? 1 : 0, 1);

            // Markers are posed in the parent: Spawn A at scene (9, 0, 7) -> (-1, 0, 3); the world pose follows the root.
            var spawn = r.Marker("spawn", "Spawn A");
            if (spawn == null || (spawn.Position - new Vector3(-1f, 0f, 3f)).sqrMagnitude > 1e-6f || spawn.GetInt("team", -1) != 0)
            {
                fails.Add("Spawn A marker " + (spawn == null ? "missing" : spawn.ToString()));
            }
            else if (r.Root != null && (r.WorldPose(spawn).position - parent.TransformPoint(spawn.Position)).sqrMagnitude > 1e-6f)
            {
                fails.Add("Spawn A world pose");
            }

            var goal = r.Marker("trigger_zone");
            if (goal == null || goal.Get("size") != "2,2,2")
            {
                fails.Add("trigger zone values");
            }

            // Everything built is on the environment layer, and the root owns its merged meshes.
            if (r.Root != null)
            {
                int wrongLayer = 0;
                foreach (var c in r.Root.GetComponentsInChildren<Collider>(true))
                {
                    if (c.gameObject.layer != RrLayers.Environment)
                    {
                        wrongLayer++;
                    }
                }

                Expect(fails, "colliders off layer 23", wrongLayer, 0);
                var host = r.Root.GetComponent<RrSceneKitRoot>();
                Expect(fails, "owned meshes", host != null ? host.Owned : -1, r.Batches + r.StandInBatches);
            }

            Log((fails.Count == 0 ? "PASS" : "FAIL " + string.Join("; ", fails.ToArray())) + " | " + r.Summary());
        }

        private static string Records(RrSceneData d)
        {
            return d.Describe().Replace(" " + d.Lines + " lines:", ":");
        }

        private static void Expect(List<string> fails, string what, int got, int want)
        {
            if (got != want)
            {
                fails.Add(what + " " + got + " (want " + want + ")");
            }
        }

        /// <summary>A generated scene the size of a quest act: build timing and draw objects.</summary>
        public static IEnumerator Stress()
        {
            var wait = WaitReady();
            while (wait.MoveNext())
            {
                yield return wait.Current;
            }

            string text = Synthetic(2000, 2000, 400);
            var parent = Holder("Rr SceneKit Stress");
            RrSceneKitResult r = null;
            var build = RrSceneKit.Build(text, parent, new RrSceneKitOptions { Position = new Vector3(0f, 0f, 8f) }, x => r = x);
            while (build.MoveNext())
            {
                yield return build.Current;
            }

            Last = r;
            int renderers = r != null && r.Root != null ? r.Root.GetComponentsInChildren<MeshRenderer>(true).Length : -1;
            int objects = r != null && r.Root != null ? r.Root.GetComponentsInChildren<Transform>(true).Length : -1;
            Log("stress " + text.Length + " chars: " + (r != null ? r.Summary() : "no result") + " | MeshRenderers " + renderers + ", objects " + objects);
        }

        /// <summary>Destroys the parent three frames into a build: the kit must stop, clean up and report Cancelled.</summary>
        public static IEnumerator Cancel()
        {
            var wait = WaitReady();
            while (wait.MoveNext())
            {
                yield return wait.Current;
            }

            string text = Synthetic(2000, 2000, 0);
            var parent = Holder("Rr SceneKit Cancel");
            RrSceneKitResult r = null;
            var build = RrSceneKit.Build(text, parent, null, x => r = x);
            int frames = 0;
            while (build.MoveNext())
            {
                if (++frames == 3)
                {
                    Clear();
                }

                yield return build.Current;
            }

            yield return null;
            bool ok = r != null && r.Cancelled && r.Root == null;
            Log((ok ? "PASS" : "FAIL") + " cancel after " + frames + " frames: " + (r != null ? r.Summary() : "no result") + ", live scenes " + RrSceneKit.LiveScenes);
        }

        /// <summary>
        /// RRSK text for n benches (Bench_LOD0 in a 1.2 m grid), boxes box colliders at four rotations and stand-in
        /// crates in eight colours: install names checked as for the sample.
        /// </summary>
        public static string Synthetic(int benches, int boxes, int standIns)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(64 * (benches + boxes + standIns) + 256);
            sb.Append("# RRSK 1 KitStress\n");
            sb.Append("M 0 Bench_LOD0\nM 1 PropBox_Square\nT 0 Table_Booth_Diner_Mat\nT 1 PropBox_Makerpen_mat\n");
            int side = Mathf.CeilToInt(Mathf.Sqrt(benches));
            for (int i = 0; i < benches; i++)
            {
                float x = (i % side) * 1.2f - side * 0.6f, z = (i / side) * 1.2f;
                sb.Append(i % 7 == 0 ? "R 1 1 " : "R 0 0 ").Append(x.ToString("0.###", ci)).Append(" 0 ").Append(z.ToString("0.###", ci)).Append(" 0 ").Append(i % 2 == 0 ? "0 0 1" : "0.7071 0 0.7071").Append(" 1 1 1\n");
            }

            string[] rots = { "0 0 0 1", "0 0.7071 0 0.7071", "0 0.3827 0 0.9239", "0.7071 0 0 0.7071" };
            for (int i = 0; i < boxes; i++)
            {
                float x = (i % 50) * 2f - 50f, z = (i / 50) * 2f;
                sb.Append("C box ").Append(x.ToString("0.###", ci)).Append(" -0.1 ").Append(z.ToString("0.###", ci)).Append(' ').Append(rots[i % 4]).Append(" 1.9 0.2 1.9\n");
            }

            for (int i = 0; i < standIns; i++)
            {
                float x = (i % 20) * 3f - 30f, z = (i / 20) * 3f;
                int c = i % 8;
                sb.Append(i % 3 == 0 ? "X cyl " : "X box ").Append(x.ToString("0.###", ci)).Append(" 0.5 ").Append(z.ToString("0.###", ci)).Append(" 0.4 0.5 0.4 0 0 0 1 ")
                  .Append(((c & 1) * 0.6f + 0.2f).ToString("0.##", ci)).Append(' ').Append((((c >> 1) & 1) * 0.6f + 0.2f).ToString("0.##", ci)).Append(' ')
                  .Append((((c >> 2) & 1) * 0.6f + 0.2f).ToString("0.##", ci)).Append(" 1\n");
            }

            return sb.ToString();
        }
    }
}
