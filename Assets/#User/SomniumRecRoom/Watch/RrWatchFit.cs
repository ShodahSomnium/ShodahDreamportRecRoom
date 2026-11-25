using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>What <see cref="RrWatchFit.SampleSurface"/> found (for the log and the tests).</summary>
    public sealed class RrWatchFitReport
    {
        /// <summary>Skinned meshes in the scene, those with a bone at the drawn wrist or elbow, and how many of those were not readable.</summary>
        public int Renderers, Matched, Unreadable;

        /// <summary>Vertices looked at, and surface points kept near the wrist.</summary>
        public int Vertices, Points;

        public float Milliseconds;

        public string Names = "";

        /// <summary>The first mesh that gave points: when it is destroyed (avatar or outfit change) the watch fits again.</summary>
        public SkinnedMeshRenderer First;

        public void Clear()
        {
            Renderers = Matched = Unreadable = Vertices = Points = 0;
            Milliseconds = 0f;
            Names = "";
            First = null;
        }
    }

    /// <summary>
    /// Fits Rec Room's watch to the forearm the local avatar actually has, sleeves included.
    ///
    /// <b>Why.</b> One fixed lift cannot serve every Somnium outfit. Measured in the harness on the two template
    /// avatars of SomniumSpaceTemplates (skinned mesh baked in the client's bind pose, the band under the strap 1.8 cm
    /// up the forearm): RPM Women's forearm is 66 x 52 mm (back of the wrist 27.8 mm above the joint axis), and there
    /// Rec Room's strap fits snugly at mesh scale 0.97 with 0.5 mm of lift. RPM Men wears a baggy sweater, 126 x 119 mm
    /// at the same place (back 46 mm above the axis), and a watch placed for bare skin disappears inside it entirely.
    ///
    /// <b>How.</b> <see cref="Contour"/> reads the inner outline of Rec Room's <c>Wrist_Watch</c> mesh (readable: its
    /// m_IsReadable is set in the bundle) round its origin, 24 bins of 15 deg: 30 mm under the case, 49-50 mm at the
    /// sides, 28 mm under the wrist, open between 105 and 165 deg (the strap's gap), for a ~98 x 58 mm oval.
    /// <see cref="SampleSurface"/> finds the skinned meshes with a bone at the drawn wrist or elbow (the avatar, its
    /// mirror copy and wearables; other players' bones are elsewhere). For the readable ones it skins the vertices
    /// whose main bone is the drawn lower arm or below it (<see cref="ArmBones"/>: never the chest a forearm lies
    /// across) with the current bone matrices and bindposes (linear blend, 4 weights), one mesh per frame, and adds
    /// triangle midpoints for low-poly sleeves. It keeps the points near the wrist, in <see cref="RrAvatarHand.TryGetWrist"/>'s frame, from one
    /// consistent snapshot. <see cref="SolveLift"/> then finds the smallest lift that leaves
    /// <see cref="SleeveClearance"/> between the surface and the underside of the case (the bins within 30 deg of the
    /// face). That lift can be negative on thin wrists and is capped at <see cref="MinLift"/>..<see cref="MaxLift"/>
    /// times the avatar size. The scale stays the forearm-based one, so on a bulky sleeve the case sits on top of the
    /// sleeve and the strap runs inside it, out of sight. With no readable mesh (a client build with Read/Write off),
    /// <see cref="RrWatchModel"/> keeps its default lift and logs why.
    ///
    /// <b>Cost</b> (the fit log line prints it; harness figures in DN/phase1/watch.md: 0.3-4 ms per 8-11k-vertex RPM
    /// avatar). One FindObjectsByType of SkinnedMeshRenderer, then one matched mesh per frame: a copy of its
    /// vertices, weights, bindposes and triangles, and one pass that skins only the forearm's vertices. It runs
    /// once per avatar, outfit or wrist change.
    /// </summary>
    public static class RrWatchFit
    {
        public const int Bins = 24;

        /// <summary>Half width (mesh units) of the strap band along the forearm that the contour and the fit look at.</summary>
        public const float BandHalf = 0.02f;

        /// <summary>Gap wanted between the avatar's surface and the underside of the case (a thin sleeve's worth).</summary>
        public const float SleeveClearance = 0.004f;

        /// <summary>Lift range (m at a 0.25 m forearm): down onto a thin wrist, up onto a bulky sleeve.</summary>
        public const float MinLift = -0.012f;

        public const float MaxLift = 0.035f;

        /// <summary>Surface points needed under the case for a fit.</summary>
        public const int MinTopPoints = 6;

        private const int VerticesPerFrame = 8000;
        private const float BoneMatch = 0.003f;

        private static readonly List<Vector3> Verts = new List<Vector3>(4096);
        private static readonly List<BoneWeight> Weights = new List<BoneWeight>(4096);
        private static readonly List<Matrix4x4> Binds = new List<Matrix4x4>(128);
        private static readonly List<int> Tris = new List<int>(8192);
        private static Vector3[] _local = new Vector3[0];
        private static bool[] _keep = new bool[0];

        /// <summary>Releases the sampling buffers (world shutdown).</summary>
        public static void Shutdown()
        {
            Verts.Clear();
            Weights.Clear();
            Binds.Clear();
            Tris.Clear();
            _local = new Vector3[0];
            _keep = new bool[0];
        }

        /// <summary>Angle bin in the watch's XZ plane: 0 = the face direction (+Z), 90 = 12 o'clock (+X).</summary>
        public static int Bin(float x, float z)
        {
            float deg = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            if (deg < 0f)
            {
                deg += 360f;
            }

            return Mathf.Clamp((int)(deg / 15f), 0, Bins - 1);
        }

        /// <summary>Inner radius (mesh units) of the watch per bin round its origin, from the vertices within <see cref="BandHalf"/> of the strap's centre line; float.MaxValue where the strap is open; null if unreadable.</summary>
        public static float[] Contour(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable)
            {
                return null;
            }

            var inner = new float[Bins];
            for (int i = 0; i < Bins; i++)
            {
                inner[i] = float.MaxValue;
            }

            mesh.GetVertices(Verts);
            foreach (Vector3 p in Verts)
            {
                if (Mathf.Abs(p.y) > BandHalf)
                {
                    continue;
                }

                int b = Bin(p.x, p.z);
                inner[b] = Mathf.Min(inner[b], Mathf.Sqrt(p.x * p.x + p.z * p.z));
            }

            Verts.Clear();
            return inner;
        }

        /// <summary>
        /// Smallest gap (m, negative = the surface through the strap) between the strap's inner contour and the
        /// surface points under the band, for a mesh scale and lift. Also returns the gap under the case (bins
        /// within 30 deg of the face) with its point count, under the wrist (150-210 deg) and at the sides (60-120,
        /// 240-300 deg). Points are in the wrist frame; <paramref name="back"/> = origin distance up the forearm.
        /// </summary>
        public static float Clearance(List<Vector3> pts, RrHandSide side, float scale, float lift, float back, float[] contour, out float top, out int topPoints, out float bottom, out float sides)
        {
            Quaternion inv = Quaternion.Inverse(side == RrHandSide.Left ? RrWatchModel.LeftInWrist : RrWatchModel.RightInWrist);
            var origin = new Vector3(0f, lift, -back);
            float min = float.MaxValue;
            top = bottom = sides = float.MaxValue;
            topPoints = 0;
            float s = Mathf.Max(0.01f, scale);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 q = inv * (pts[i] - origin) / s;
                if (Mathf.Abs(q.y) > BandHalf)
                {
                    continue;
                }

                int b = Bin(q.x, q.z);
                float inner = contour[b];
                if (inner == float.MaxValue)
                {
                    continue;
                }

                float gap = (inner - Mathf.Sqrt(q.x * q.x + q.z * q.z)) * s;
                min = Mathf.Min(min, gap);
                float deg = b * 15f + 7.5f;
                if (deg <= 30f || deg >= 330f)
                {
                    top = Mathf.Min(top, gap);
                    topPoints++;
                }
                else if (deg >= 150f && deg <= 210f)
                {
                    bottom = Mathf.Min(bottom, gap);
                }
                else if ((deg >= 60f && deg <= 120f) || (deg >= 240f && deg <= 300f))
                {
                    sides = Mathf.Min(sides, gap);
                }
            }

            return min;
        }

        /// <summary>
        /// The smallest lift in <paramref name="minLift"/>..<paramref name="maxLift"/> (0.5 mm steps) with
        /// <see cref="SleeveClearance"/> under the case. When even the cap is not enough (a costume sleeve) the lift is the
        /// cap and <paramref name="topGap"/> tells by how much it falls short. False only with too few surface points.
        /// </summary>
        public static bool SolveLift(List<Vector3> pts, RrHandSide side, float scale, float back, float[] contour, float minLift, float maxLift, out float lift, out float topGap)
        {
            lift = maxLift;
            topGap = 0f;
            if (contour == null || pts == null || pts.Count < MinTopPoints)
            {
                return false;
            }

            bool seen = false;
            for (float h = minLift; h <= maxLift + 1e-5f; h += 0.0005f)
            {
                float bottom;
                float sides;
                int n;
                Clearance(pts, side, scale, h, back, contour, out topGap, out n, out bottom, out sides);
                if (n >= MinTopPoints)
                {
                    seen = true;
                }

                if (n >= MinTopPoints && topGap >= SleeveClearance)
                {
                    lift = h;
                    return true;
                }
            }

            if (seen)
            {
                float b2;
                float s2;
                int n2;
                Clearance(pts, side, scale, maxLift, back, contour, out topGap, out n2, out b2, out s2);
                lift = maxLift;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Which of a skinned mesh's bones move with the drawn forearm: the bone at the drawn elbow (the lower arm)
        /// and every bone below it (roll bones, hand, fingers). Failing that, the bone at the wrist and below. Null
        /// when neither is in the list. Only vertices whose main bone is one of these count, so the chest behind a
        /// forearm held across it, the upper arm and the other hand are never taken for a sleeve.
        /// </summary>
        public static bool[] ArmBones(Transform[] bones, Vector3 wrist, Vector3 elbow)
        {
            float tol2 = BoneMatch * BoneMatch;
            Transform root = null;
            foreach (Transform b in bones)
            {
                if (b != null && (b.position - elbow).sqrMagnitude < tol2)
                {
                    root = b;
                    break;
                }
            }

            if (root == null)
            {
                foreach (Transform b in bones)
                {
                    if (b != null && (b.position - wrist).sqrMagnitude < tol2)
                    {
                        root = b;
                        break;
                    }
                }
            }

            if (root == null)
            {
                return null;
            }

            var mask = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                mask[i] = bones[i] != null && (bones[i] == root || bones[i].IsChildOf(root));
            }

            return mask;
        }

        /// <summary>
        /// Adds the kept vertices (wrist frame) inside the window, plus the edge midpoints and centroid of every
        /// triangle whose three vertices are kept: low-poly sleeves have rings of vertices 4-5 cm apart (RPM Men's
        /// sweater), and the case must clear the surface between them too.
        /// </summary>
        public static void AddSurface(List<Vector3> into, Vector3[] local, bool[] keep, int vertexCount, List<int> triangles, float zMin, float zMax, float rMax2)
        {
            int count = Mathf.Min(vertexCount, Mathf.Min(local.Length, keep.Length));
            for (int v = 0; v < count; v++)
            {
                if (keep[v])
                {
                    AddIfNear(into, local[v], zMin, zMax, rMax2);
                }
            }

            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int a = triangles[t];
                int b = triangles[t + 1];
                int c = triangles[t + 2];
                if (a >= count || b >= count || c >= count || !keep[a] || !keep[b] || !keep[c])
                {
                    continue;
                }

                Vector3 pa = local[a];
                Vector3 pb = local[b];
                Vector3 pc = local[c];
                if (!Near(pa, zMin, zMax, rMax2) && !Near(pb, zMin, zMax, rMax2) && !Near(pc, zMin, zMax, rMax2))
                {
                    continue;
                }

                AddIfNear(into, (pa + pb) * 0.5f, zMin, zMax, rMax2);
                AddIfNear(into, (pb + pc) * 0.5f, zMin, zMax, rMax2);
                AddIfNear(into, (pc + pa) * 0.5f, zMin, zMax, rMax2);
                AddIfNear(into, (pa + pb + pc) / 3f, zMin, zMax, rMax2);
            }
        }

        private static bool Near(Vector3 p, float zMin, float zMax, float rMax2)
        {
            return p.z > zMin && p.z < zMax && p.x * p.x + p.y * p.y < rMax2;
        }

        private static void AddIfNear(List<Vector3> into, Vector3 p, float zMin, float zMax, float rMax2)
        {
            if (Near(p, zMin, zMax, rMax2))
            {
                into.Add(p);
            }
        }

        /// <summary>The window <see cref="SampleSurface"/> keeps, for a forearm size: 9 cm up the forearm to 2 cm past the joint, within 12 cm of the axis.</summary>
        public static void Window(float k, out float zMin, out float zMax, out float rMax2)
        {
            zMin = -0.09f * k;
            zMax = 0.02f * k;
            rMax2 = 0.12f * 0.12f * k * k;
        }

        /// <summary>
        /// Collects surface points of the local avatar round the drawn wrist of <paramref name="side"/> into
        /// <paramref name="into"/> (wrist frame: x across, y out of the back of the hand, z along the forearm; see
        /// <see cref="Window"/>). Only the vertices that move with the drawn forearm (<see cref="ArmBones"/>) count, and
        /// triangles add their midpoints (<see cref="AddSurface"/>). Each mesh is skinned from one snapshot of its bones
        /// and of the wrist, taken in the same frame (before the animator and IK of that frame, so both are last frame's
        /// final pose).
        /// </summary>
        public static IEnumerator SampleSurface(RrHandSide side, List<Vector3> into, RrWatchFitReport report)
        {
            into.Clear();
            report.Clear();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Pose wrist;
            float forearm;
            if (!RrAvatarHand.TryGetWrist(side, out wrist, out forearm))
            {
                yield break;
            }

            var matched = new List<SkinnedMeshRenderer>();
            FindArmRenderers(wrist, forearm, matched, report);
            report.Milliseconds += (float)clock.Elapsed.TotalMilliseconds;
            float k = RrWatchModel.SizeOf(forearm);
            float zMin;
            float zMax;
            float rMax2;
            Window(k, out zMin, out zMax, out rMax2);
            var names = new System.Text.StringBuilder();
            foreach (SkinnedMeshRenderer smr in matched)
            {
                yield return null;
                clock.Reset();
                clock.Start();
                if (smr == null || smr.sharedMesh == null || !RrAvatarHand.TryGetWrist(side, out wrist, out forearm))
                {
                    continue;
                }

                Mesh mesh = smr.sharedMesh;
                Transform[] bones = smr.bones;
                Vector3 elbow = wrist.position - (wrist.rotation * Vector3.forward) * forearm;
                bool[] arm = ArmBones(bones, wrist.position, elbow);
                if (arm == null)
                {
                    continue;
                }

                mesh.GetVertices(Verts);
                mesh.GetBoneWeights(Weights);
                mesh.GetBindposes(Binds);
                int nb = Mathf.Min(bones.Length, Binds.Count);
                int count = Verts.Count;
                if (Weights.Count != count || nb == 0)
                {
                    continue;
                }

                // The snapshot: bone matrices and the wrist frame of this frame.
                var skin = new Matrix4x4[nb];
                for (int i = 0; i < nb; i++)
                {
                    if (bones[i] != null)
                    {
                        skin[i] = bones[i].localToWorldMatrix * Binds[i];
                    }
                }

                Quaternion inv = Quaternion.Inverse(wrist.rotation);
                Vector3 w0 = wrist.position;
                if (_local.Length < count)
                {
                    _local = new Vector3[count];
                    _keep = new bool[count];
                }

                for (int v = 0; v < count; v++)
                {
                    BoneWeight bw = Weights[v];
                    int b0 = bw.boneIndex0;
                    bool keep = b0 >= 0 && b0 < nb && arm[b0];
                    _keep[v] = keep;
                    if (keep)
                    {
                        _local[v] = inv * (Skin(skin, nb, bw, Verts[v]) - w0);
                    }
                }

                int before = into.Count;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                    {
                        continue;
                    }

                    mesh.GetTriangles(Tris, sub);
                    AddSurface(into, _local, _keep, count, Tris, zMin, zMax, rMax2);
                }

                report.Vertices += count;
                report.Milliseconds += (float)clock.Elapsed.TotalMilliseconds;
                if (into.Count > before)
                {
                    if (report.First == null)
                    {
                        report.First = smr;
                    }

                    names.Append(names.Length > 0 ? ", " : "").Append(smr.name).Append(" (").Append(into.Count - before).Append(")");
                }

                if (count > VerticesPerFrame)
                {
                    yield return null;
                }
            }

            Verts.Clear();
            Weights.Clear();
            Binds.Clear();
            Tris.Clear();
            report.Points = into.Count;
            report.Names = names.ToString();
        }

        private static Vector3 Skin(Matrix4x4[] skin, int nb, BoneWeight bw, Vector3 v)
        {
            Vector3 p = skin[bw.boneIndex0].MultiplyPoint3x4(v) * bw.weight0;
            if (bw.weight1 > 0f && bw.boneIndex1 >= 0 && bw.boneIndex1 < nb)
            {
                p += skin[bw.boneIndex1].MultiplyPoint3x4(v) * bw.weight1;
            }

            if (bw.weight2 > 0f && bw.boneIndex2 >= 0 && bw.boneIndex2 < nb)
            {
                p += skin[bw.boneIndex2].MultiplyPoint3x4(v) * bw.weight2;
            }

            if (bw.weight3 > 0f && bw.boneIndex3 >= 0 && bw.boneIndex3 < nb)
            {
                p += skin[bw.boneIndex3].MultiplyPoint3x4(v) * bw.weight3;
            }

            float sum = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
            return sum > 1e-4f && Mathf.Abs(sum - 1f) > 1e-3f ? p / sum : p;
        }

        /// <summary>Enabled skinned meshes with a bone within 3 mm of the drawn wrist or elbow (the drawn arm's own bones or their mirror / wearable copies).</summary>
        private static void FindArmRenderers(Pose wrist, float forearm, List<SkinnedMeshRenderer> matched, RrWatchFitReport report)
        {
            Vector3 elbow = wrist.position - (wrist.rotation * Vector3.forward) * forearm;
            SkinnedMeshRenderer[] all = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            report.Renderers = all.Length;
            float tol2 = BoneMatch * BoneMatch;
            foreach (SkinnedMeshRenderer smr in all)
            {
                if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy || smr.sharedMesh == null)
                {
                    continue;
                }

                bool hit = false;
                Transform[] bones;
                try
                {
                    bones = smr.bones;
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (Transform b in bones)
                {
                    if (b != null && ((b.position - wrist.position).sqrMagnitude < tol2 || (b.position - elbow).sqrMagnitude < tol2))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    continue;
                }

                report.Matched++;
                if (smr.sharedMesh.isReadable)
                {
                    matched.Add(smr);
                }
                else
                {
                    report.Unreadable++;
                }
            }
        }
    }
}
