using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen's tube mesh (the "Tube" shape of the pen's menu): an 8-sided tube along the stroke's points,
    /// one ring per point with its own radius, rings parallel-transported so the tube does not twist, and a short
    /// cone on each end so the ends look rounded. The same builder as the Charades Pen's ink.
    /// </summary>
    public static class RrMakerPenTube
    {
        public const int Sides = 8;

        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<Vector3> Normals = new List<Vector3>();
        private static readonly List<int> Tris = new List<int>();

        /// <summary>Rebuilds <paramref name="mesh"/> from the points (mesh-local) and their radii.</summary>
        public static void Build(Mesh mesh, List<Vector3> pts, List<float> radii)
        {
            mesh.Clear();
            int n = pts.Count;
            if (n == 0)
            {
                return;
            }

            Verts.Clear();
            Normals.Clear();
            Tris.Clear();

            Vector3 prevT = Vector3.up;
            if (n > 1)
            {
                prevT = (pts[1] - pts[0]).normalized;
                if (prevT.sqrMagnitude < 0.5f)
                {
                    prevT = Vector3.up;
                }
            }

            Vector3 nrm = Vector3.Cross(prevT, Mathf.Abs(prevT.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 firstT = prevT, lastT = prevT;
            int rings = n == 1 ? 2 : n;
            for (int i = 0; i < rings; i++)
            {
                Vector3 p = n == 1 ? pts[0] + prevT * (i == 0 ? -0.001f : 0.001f) : pts[i];
                Vector3 t;
                if (n == 1)
                {
                    t = prevT;
                }
                else
                {
                    t = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                    t = t.sqrMagnitude > 1e-10f ? t.normalized : prevT;
                }

                if (i > 0)
                {
                    nrm = Quaternion.FromToRotation(prevT, t) * nrm;
                    nrm = (nrm - Vector3.Dot(nrm, t) * t).normalized;
                    if (nrm.sqrMagnitude < 0.5f)
                    {
                        nrm = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    }
                }
                else
                {
                    firstT = t;
                }

                lastT = t;
                prevT = t;
                Vector3 bin = Vector3.Cross(t, nrm);
                float r = radii[Mathf.Min(i, n - 1)];
                for (int k = 0; k < Sides; k++)
                {
                    float a = k * (Mathf.PI * 2f / Sides);
                    Vector3 dir = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                    Verts.Add(p + dir * r);
                    Normals.Add(dir);
                }
            }

            for (int i = 0; i < rings - 1; i++)
            {
                int ring = i * Sides;
                int next = ring + Sides;
                for (int k = 0; k < Sides; k++)
                {
                    int a = ring + k;
                    int b = ring + (k + 1) % Sides;
                    int c = next + k;
                    int d = next + (k + 1) % Sides;
                    Tris.Add(a);
                    Tris.Add(b);
                    Tris.Add(c);
                    Tris.Add(b);
                    Tris.Add(d);
                    Tris.Add(c);
                }
            }

            Vector3 p0 = n == 1 ? pts[0] - firstT * 0.001f : pts[0];
            Vector3 p1 = n == 1 ? pts[0] + lastT * 0.001f : pts[n - 1];
            float r0 = radii[0];
            float r1 = radii[n - 1];
            int tip0 = Verts.Count;
            Verts.Add(p0 - firstT * r0 * 0.9f);
            Normals.Add(-firstT);
            int tip1 = Verts.Count;
            Verts.Add(p1 + lastT * r1 * 0.9f);
            Normals.Add(lastT);
            int lastRing = (rings - 1) * Sides;
            for (int k = 0; k < Sides; k++)
            {
                int k1 = (k + 1) % Sides;
                Tris.Add(tip0);
                Tris.Add(k1);
                Tris.Add(k);
                Tris.Add(tip1);
                Tris.Add(lastRing + k);
                Tris.Add(lastRing + k1);
            }

            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Squared distance from a point to the segment a-b.</summary>
        public static float SqrDistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return (a + ab * t - p).sqrMagnitude;
        }

        /// <summary>
        /// Closest approach of the segment a-b to the ray segment o + d * s (s in [0, length], d unit): the squared
        /// distance, and the ray distance s where it happens.
        /// </summary>
        public static float SqrDistanceSegmentRay(Vector3 a, Vector3 b, Vector3 o, Vector3 d, float length, out float along)
        {
            Vector3 u = b - a;
            Vector3 w = a - o;
            float uu = Vector3.Dot(u, u);
            float ud = Vector3.Dot(u, d);
            float uw = Vector3.Dot(u, w);
            float dw = Vector3.Dot(d, w);
            float den = uu - ud * ud; // |d| = 1
            float sc = den > 1e-9f ? Mathf.Clamp01((ud * dw - uw) / den) : 0f;
            float tc = Mathf.Clamp(dw + ud * sc, 0f, length);
            // re-project the segment parameter onto the clamped ray point
            sc = uu > 1e-12f ? Mathf.Clamp01(Vector3.Dot(o + d * tc - a, u) / uu) : 0f;
            tc = Mathf.Clamp(Vector3.Dot(a + u * sc - o, d), 0f, length);
            along = tc;
            return (a + u * sc - (o + d * tc)).sqrMagnitude;
        }
    }
}
