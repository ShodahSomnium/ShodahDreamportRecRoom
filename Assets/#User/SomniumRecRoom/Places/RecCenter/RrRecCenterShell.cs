using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using L = SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter.RrRecCenterLayout;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The final Rec Center's architecture, rebuilt to the July 2025 scene's measurements (rr_reccenter_cache.md §2,
    /// room frame of <see cref="RrRecCenterLayout"/>): the 25 x 33 m hall with its curved far end and two big curved
    /// multi-pane windows; the cream plaster ceiling at 9.0 m under heavy dark timber beams (underside 8.4) with the
    /// two gridded skylights over the kiosk; the upstairs balcony on the left (floor 3.10, rail top 4.15) with front
    /// and back stairs (visible steps over invisible ramp colliders); the red-brick stage section on the right with
    /// red curtains and the lit marquee; the merch booth; the gym with its court at the back left; the sunk theatre;
    /// the left wing (cafe downstairs, an upper floor to the Featured Rooms door); the hallway to the Dorm; rugs,
    /// fluorescent fixtures and lights. Wall paint follows the rec.net picture (cream walls, blue lower walls and
    /// bands, terracotta gym): the scene stores it in vertex colours that were not read (GUESS colours).
    /// Big planes are split into ~10 m pieces so each piece gets its own nearby point lights (URP's per-object light
    /// limit is 8); small repeated parts (frames, beams, rails, bulbs) are merged per surface (<see cref="RrRcBatch"/>).
    /// </summary>
    internal static class RrRecCenterShell
    {
        private const float T = RrRecCenterLayout.Wall;

        private struct Band
        {
            public float Y0;
            public float Y1;
            public RrRcSurface S;

            public Band(float y0, float y1, RrRcSurface s)
            {
                Y0 = y0;
                Y1 = y1;
                S = s;
            }
        }

        private static RrRcSurfaces _s;
        private static int _count;
        private static RrRcBatch _navy;
        private static RrRcBatch _glass;
        private static RrRcBatch _timber;

        public static IEnumerator Build(Transform root, RrRcSurfaces s, Action<float> progress)
        {
            _s = s;
            _count = 0;
            var shell = RrBuild.Group(root, "Shell").transform;
            _navy = new RrRcBatch(s.Navy);
            _glass = new RrRcBatch(s.Glass);
            _timber = new RrRcBatch(s.Timber);

            Floors(shell);
            progress(0.1f);
            yield return null;
            HallWalls(shell);
            progress(0.2f);
            yield return null;
            CurvedWall(shell);
            progress(0.3f);
            yield return null;
            Ceiling(shell);
            Beams();
            progress(0.4f);
            yield return null;
            Balcony(shell);
            Stairs(shell);
            progress(0.5f);
            yield return null;
            Gym(shell);
            Theatre(shell);
            progress(0.6f);
            yield return null;
            Wing(shell);
            Hallway(shell);
            progress(0.7f);
            yield return null;
            Rugs(shell);
            MerchBooth(shell);
            StageFront(shell);
            RrRecCenterKiosk.Build(shell, s);
            progress(0.8f);
            yield return null;
            Lights(shell);
            _navy.Build(shell, "Window Frames");
            _glass.Build(shell, "Window Glass");
            _timber.Build(shell, "Timber");
            _count += _navy.Count + _glass.Count + _timber.Count;
            RrRecCenterOutdoors.Build(root, s);
            progress(0.9f);
            RrLog.Info("RecCenter", "shell built: " + _count + " pieces (" + _navy.Count + " frame, " + _glass.Count + " glass, "
                + _timber.Count + " timber merged), " + (s.FoundCount > 0 ? "Rec Room surfaces" : "fallback colours"));
            _s = null;
            _navy = null;
            _glass = null;
            _timber = null;
        }

        // ------------------------------------------------------------------ floors

        private static void Floors(Transform p)
        {
            var f = RrBuild.Group(p, "Floors").transform;
            // Main rectangle, under the side and entrance walls too (the stage and cafe openings have thresholds).
            float[] xs = { L.HallLeft - T, -5.5f, 3.0f, L.HallRight + T };
            float[] zs = { L.Entrance - T, -7.9f, -0.3f, L.CurveCenter.y };
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Slab(f, "Floor", xs[i], -0.3f, zs[j], xs[i + 1], 0f, zs[j + 1], _s.Floor);
                }
            }

            // Back-left strip toward the gym, and the curved end as a convex prism (radius to the wall's outer face).
            Slab(f, "Floor Back Left", L.HallLeft - T, -0.3f, L.CurveCenter.y, -1.5f, 0f, L.BackLeftZ, _s.Floor);
            var cap = new List<Vector2> { new Vector2(-1.5f, L.CurveCenter.y) };
            float r = L.CurveRadius + T;
            float end = Mathf.Acos((-1.5f - L.CurveCenter.x) / r) * Mathf.Rad2Deg;
            cap.AddRange(RrRcGeo.Arc(L.CurveCenter, r, 0f, end, 24));
            RrRcGeo.Prism(f, "Floor Curve", cap, -0.3f, 0f, _s.Floor);
            _count += 11;

            // Gym, left wing (cafe), hallway, theatre.
            Slab(f, "Gym Floor", L.GymLeft - T, -0.3f, L.BackLeftZ, -1.5f, 0f, L.GymBack + T, _s.Floor);
            WingPlan(f, "Cafe Floor", -0.3f, 0f, _s.Floor, true);
            Slab(f, "Hallway Floor", L.HallwayX0 - T, -0.3f, L.HallwayEnd - T, L.HallwayX1 + T, 0f, L.Entrance - T - 0.001f, _s.Floor);
            Slab(f, "Theatre Floor", L.TheatreX0, L.TheatreFloor - 0.3f, L.TheatreZ0 - T, L.TheatreX1 + T, L.TheatreFloor, L.TheatreZ1 + T, _s.Floor);
            _count += 4;
        }

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// A wall along X (alongX) or Z with its inner face at <paramref name="inner"/>, thickness toward
        /// <paramref name="outward"/>, from a0 to a1 and y0 to y1, with rectangular openings (a0, a1, y0, y1) and
        /// painted bands by height. Pieces longer than 10 m are split (per-object light limit).
        /// </summary>
        private static void Wall(Transform p, string name, bool alongX, float inner, float outward, float a0, float a1, float y0, float y1, Band[] bands, params Vector4[] holes)
        {
            var cuts = new List<float> { a0, a1 };
            for (int i = 0; i < holes.Length; i++)
            {
                cuts.Add(Mathf.Clamp(holes[i].x, a0, a1));
                cuts.Add(Mathf.Clamp(holes[i].y, a0, a1));
            }

            cuts.Sort();
            float o0 = Mathf.Min(inner, inner + outward * T);
            float o1 = Mathf.Max(inner, inner + outward * T);
            for (int c = 0; c < cuts.Count - 1; c++)
            {
                float u = cuts[c];
                float w = cuts[c + 1];
                if (w - u < 0.001f)
                {
                    continue;
                }

                // Solid vertical ranges of this column: y0..y1 minus the holes spanning it.
                var ranges = new List<Vector2> { new Vector2(y0, y1) };
                for (int h = 0; h < holes.Length; h++)
                {
                    if (holes[h].x <= u + 0.001f && holes[h].y >= w - 0.001f)
                    {
                        ranges = Subtract(ranges, holes[h].z, holes[h].w);
                    }
                }

                int pieces = Mathf.Max(1, Mathf.CeilToInt((w - u) / 10f));
                for (int k = 0; k < pieces; k++)
                {
                    float pu = Mathf.Lerp(u, w, k / (float)pieces);
                    float pw = Mathf.Lerp(u, w, (k + 1) / (float)pieces);
                    for (int rg = 0; rg < ranges.Count; rg++)
                    {
                        for (int b = 0; b < bands.Length; b++)
                        {
                            float ya = Mathf.Max(ranges[rg].x, bands[b].Y0);
                            float yb = Mathf.Min(ranges[rg].y, bands[b].Y1);
                            if (yb - ya < 0.001f)
                            {
                                continue;
                            }

                            if (alongX)
                            {
                                RrRcGeo.Slab(p, name, new Vector3(pu, ya, o0), new Vector3(pw, yb, o1), bands[b].S);
                            }
                            else
                            {
                                RrRcGeo.Slab(p, name, new Vector3(o0, ya, pu), new Vector3(o1, yb, pw), bands[b].S);
                            }

                            _count++;
                        }
                    }
                }
            }
        }

        private static List<Vector2> Subtract(List<Vector2> ranges, float h0, float h1)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < ranges.Count; i++)
            {
                var r = ranges[i];
                if (h1 <= r.x || h0 >= r.y)
                {
                    result.Add(r);
                    continue;
                }

                if (h0 > r.x)
                {
                    result.Add(new Vector2(r.x, h0));
                }

                if (h1 < r.y)
                {
                    result.Add(new Vector2(h1, r.y));
                }
            }

            return result;
        }

        private static Band[] Bands(params object[] spec)
        {
            // (y0, surface, y1, surface, y2 ...): alternating heights and surfaces, the last height closes.
            var l = new List<Band>();
            for (int i = 0; i + 2 < spec.Length; i += 2)
            {
                l.Add(new Band(Convert.ToSingle(spec[i]), Convert.ToSingle(spec[i + 2]), (RrRcSurface)spec[i + 1]));
            }

            return l.ToArray();
        }

        private static Vector4 Hole(float a0, float a1, float y0, float y1)
        {
            return new Vector4(a0, a1, y0, y1);
        }

        /// <summary>
        /// A multi-pane window in a wall's opening: glass in the wall's centre plane (solid, so nobody walks out) and
        /// a navy frame with mullions every <paramref name="paneW"/> and transoms every <paramref name="paneH"/>
        /// (Window_* meshes in LockerRoom_Metal_Mat, navy (0.070, 0.171, 0.382)).
        /// </summary>
        private static void Window(bool alongX, float inner, float outward, float a0, float a1, float y0, float y1, float paneW, float paneH)
        {
            float mid = inner + outward * T * 0.5f;
            const float Bar = 0.08f;
            const float Depth = 0.14f;
            var glassSize = alongX ? new Vector3(a1 - a0, y1 - y0, 0.03f) : new Vector3(0.03f, y1 - y0, a1 - a0);
            _glass.Box(Point(alongX, (a0 + a1) * 0.5f, (y0 + y1) * 0.5f, mid), Quaternion.identity, glassSize, true);
            int nu = Mathf.Max(1, Mathf.RoundToInt((a1 - a0) / paneW));
            int nv = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / paneH));
            for (int i = 0; i <= nu; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)nu);
                float bw = i == 0 || i == nu ? Bar * 1.6f : Bar;
                _navy.Box(Point(alongX, a, (y0 + y1) * 0.5f, mid), Quaternion.identity, alongX ? new Vector3(bw, y1 - y0, Depth) : new Vector3(Depth, y1 - y0, bw));
            }

            for (int j = 0; j <= nv; j++)
            {
                float y = Mathf.Lerp(y0, y1, j / (float)nv);
                float bw = j == 0 || j == nv ? Bar * 1.6f : Bar;
                _navy.Box(Point(alongX, (a0 + a1) * 0.5f, y, mid), Quaternion.identity, alongX ? new Vector3(a1 - a0, bw, Depth) : new Vector3(Depth, bw, a1 - a0));
            }
        }

        private static Vector3 Point(bool alongX, float a, float y, float plane)
        {
            return alongX ? new Vector3(a, y, plane) : new Vector3(plane, y, a);
        }

        private static void HallWalls(Transform p)
        {
            var w = RrBuild.Group(p, "Hall Walls").transform;
            var s = _s;
            var lower = Bands(0f, s.Blue, 1.2f, s.Cream, 12.2f);

            // Entrance wall (z -15.6): the hallway opening; the left end rises under the balcony's sloped ceiling.
            Wall(w, "Entrance Wall", true, L.Entrance, -1f, L.HallLeft - T, L.HallRight + T, 0f, L.CeilingY, lower,
                Hole(L.HallwayX0, L.HallwayX1, 0f, L.HallwayCeiling));
            Wall(w, "Entrance Wall High", true, L.Entrance, -1f, L.HallLeft - T, -9.32f, L.CeilingY, 12.2f, lower);

            // Right wall (x 11.5): cream with a blue wainscot toward the entrance, blue pillars flanking the red-brick
            // stage section (opening z -8.7..-1.8, 4.4 m), then cream under a blue band past the merch booth.
            Wall(w, "Right Wall", false, L.HallRight, 1f, L.Entrance - T, -9.9f, 0f, L.CeilingY, lower);
            Wall(w, "Right Pillar", false, L.HallRight, 1f, -9.9f, -9.3f, 0f, L.CeilingY, Bands(0f, s.Blue, 12f));
            Wall(w, "Stage Brick", false, L.HallRight, 1f, -9.3f, -1.2f, 0f, L.CeilingY, Bands(0f, s.Brick, 12f), Hole(L.StageZ0, L.StageZ1, 0f, L.StageTop));
            Wall(w, "Right Pillar", false, L.HallRight, 1f, -1.2f, -0.6f, 0f, L.CeilingY, Bands(0f, s.Blue, 12f));
            Wall(w, "Right Wall", false, L.HallRight, 1f, -0.6f, L.CurveCenter.y, 0f, L.CeilingY, Bands(0f, s.Cream, L.CurveWindowTop, s.Blue, 12f));
            // The pillars stand 0.2 m proud of the wall, full height to the beams.
            RrRcGeo.Slab(w, "Pillar", new Vector3(L.HallRight - 0.2f, 0f, -9.9f), new Vector3(L.HallRight, L.BeamBottom, -9.3f), s.Blue);
            RrRcGeo.Slab(w, "Pillar", new Vector3(L.HallRight - 0.2f, 0f, -1.2f), new Vector3(L.HallRight, L.BeamBottom, -0.6f), s.Blue);

            // Left wall (x -13.9, the balcony's back wall): blue under the balcony, cream above; the two cafe
            // openings, the upstairs opening to the wing's upper floor and the three clerestory windows (Window_Small).
            var left = Bands(0f, s.Blue, L.BalconyUnder, s.Cream, 12.2f);
            float[] cz = { -11.35f, -7.97f, -6.57f, -3.19f, -1.70f, 1.68f };
            Wall(w, "Left Wall", false, L.HallLeft, -1f, L.Entrance - T, L.BackLeftZ, 0f, 12.2f, left,
                Hole(-4.2f, -0.3f, 0f, 2.6f), Hole(0.3f, 4.2f, 0f, 2.6f), Hole(-13.1f, -10.9f, L.BalconyY, 5.6f),
                Hole(cz[0], cz[1], 9.53f, 10.86f), Hole(cz[2], cz[3], 9.53f, 10.86f), Hole(cz[4], cz[5], 9.53f, 10.86f));
            for (int i = 0; i < 3; i++)
            {
                Window(false, L.HallLeft, -1f, cz[i * 2], cz[i * 2 + 1], 9.53f, 10.86f, 0.85f, 0.67f);
            }

            // Opening trims (timber) around the two cafe openings and the upstairs opening.
            Trim(false, L.HallLeft, 1f, -4.2f, -0.3f, 0f, 2.6f);
            Trim(false, L.HallLeft, 1f, 0.3f, 4.2f, 0f, 2.6f);
            Trim(false, L.HallLeft, 1f, -13.1f, -10.9f, L.BalconyY, 5.6f);
            Trim(true, L.Entrance, 1f, L.HallwayX0, L.HallwayX1, 0f, L.HallwayCeiling);
        }

        /// <summary>
        /// A dark timber frame 0.14 m wide around an opening, standing 3 cm proud of the wall face at
        /// <paramref name="face"/> toward the room (<paramref name="toward"/> = +1 or -1 along the wall's normal axis).
        /// </summary>
        private static void Trim(bool alongX, float face, float toward, float a0, float a1, float y0, float y1)
        {
            const float W = 0.14f;
            const float d = 0.06f;
            float plane = face + toward * 0.03f;
            _timber.Box(Point(alongX, a0 - W * 0.5f, (y0 + y1) * 0.5f, plane), Quaternion.identity, alongX ? new Vector3(W, y1 - y0, d) : new Vector3(d, y1 - y0, W));
            _timber.Box(Point(alongX, a1 + W * 0.5f, (y0 + y1) * 0.5f, plane), Quaternion.identity, alongX ? new Vector3(W, y1 - y0, d) : new Vector3(d, y1 - y0, W));
            _timber.Box(Point(alongX, (a0 + a1) * 0.5f, y1 + W * 0.5f, plane), Quaternion.identity, alongX ? new Vector3(a1 - a0 + 2f * W, W, d) : new Vector3(d, W, a1 - a0 + 2f * W));
        }

        /// <summary>
        /// The far end's curve (BackCurveWallCollision, 8.5 m): ~4 deg segments from the right wall round to x -1.5,
        /// the two big curved multi-pane windows (sill 0.55 in dark wood, glass to 5.7, navy panes ~0.75 x 1.7 m),
        /// cream wall between them, the blue band on top (the picture's "curved cream wall topped by a blue band").
        /// </summary>
        private static void CurvedWall(Transform p)
        {
            var w = RrBuild.Group(p, "Curved Wall").transform;
            var windows = new[] { new Vector2(L.WideFrom, L.WideTo), new Vector2(L.NarrowFrom, L.NarrowTo) };
            ArcWall(w, "Curve", L.CurveCenter, L.CurveRadius, L.CurveRadius, 0f, L.CurveEnd, windows, L.CurveSill, L.CurveWindowTop, L.CeilingY,
                _s.Cream, _s.Cream, _s.Blue, new[] { 2.25f, 3.95f });
        }

        /// <summary>
        /// A wall along an ellipse arc (inner face through centre + (rx cos a, rz sin a), a in degrees, 0 = +X toward
        /// +Z), built from ~4 deg chords with the thickness outward; inside the angle ranges of
        /// <paramref name="windows"/> it is a sill, a glass pane and a navy frame (a mullion at every chord's
        /// edge, transoms at <paramref name="transoms"/>), with the wall above the window top.
        /// </summary>
        private static void ArcWall(Transform p, string name, Vector2 center, float rx, float rz, float from, float to, Vector2[] windows,
            float sill, float windowTop, float top, RrRcSurface below, RrRcSurface solid, RrRcSurface above, float[] transoms)
        {
            var low = new RrRcBatch(below);
            var mid = solid.Material == below.Material ? low : new RrRcBatch(solid);
            var high = new RrRcBatch(above);
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / 4f));
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.Lerp(from, to, i / (float)n);
                float a1 = Mathf.Lerp(from, to, (i + 1) / (float)n);
                var p0 = Ellipse(center, rx, rz, a0);
                var p1 = Ellipse(center, rx, rz, a1);
                var chord = p1 - p0;
                var outward = new Vector3(chord.z, 0f, -chord.x).normalized;
                var cc = new Vector3(center.x, 0f, center.y);
                if (Vector3.Dot(outward, (p0 + p1) * 0.5f - cc) < 0f)
                {
                    outward = -outward;
                }

                var rot = Quaternion.LookRotation(outward, Vector3.up);
                float len = chord.magnitude + 0.04f;
                var c = (p0 + p1) * 0.5f + outward * (T * 0.5f);
                high.Box(c + Vector3.up * (windowTop + top) * 0.5f, rot, new Vector3(len, top - windowTop, T), true);
                if (!InRanges(windows, (a0 + a1) * 0.5f))
                {
                    mid.Box(c + Vector3.up * windowTop * 0.5f, rot, new Vector3(len, windowTop, T), true);
                    continue;
                }

                low.Box(c + Vector3.up * sill * 0.5f, rot, new Vector3(len, sill, T), true);
                // Dark wood sill (Beams/WindowSill, y 0.52..0.60), 0.36 m deep, mostly inside.
                _timber.Box(c - outward * 0.2f + Vector3.up * (sill + 0.03f), rot, new Vector3(len, 0.06f, 0.36f));
                _glass.Box(c + Vector3.up * (sill + windowTop) * 0.5f, rot, new Vector3(len, windowTop - sill, 0.03f), true);
                float h = windowTop - sill;
                _navy.Box(p0 + outward * (T * 0.5f) + Vector3.up * (sill + windowTop) * 0.5f, rot, new Vector3(0.1f, h, 0.16f));
                float next = a1 + (a1 - a0) * 0.5f;
                if (!InRanges(windows, next) || i == n - 1)
                {
                    _navy.Box(p1 + outward * (T * 0.5f) + Vector3.up * (sill + windowTop) * 0.5f, rot, new Vector3(0.14f, h, 0.16f));
                }

                _navy.Box(c + Vector3.up * (sill + 0.04f), rot, new Vector3(len, 0.12f, 0.16f));
                _navy.Box(c + Vector3.up * (windowTop - 0.04f), rot, new Vector3(len, 0.12f, 0.16f));
                for (int k = 0; k < transoms.Length; k++)
                {
                    _navy.Box(c + Vector3.up * transoms[k], rot, new Vector3(len, 0.08f, 0.16f));
                }
            }

            low.Build(p, name + " Low");
            if (mid != low)
            {
                mid.Build(p, name + " Wall");
                _count += mid.Count;
            }

            high.Build(p, name + " High");
            _count += low.Count + high.Count;
        }

        private static Vector3 Ellipse(Vector2 c, float rx, float rz, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(c.x + rx * Mathf.Cos(a), 0f, c.y + rz * Mathf.Sin(a));
        }

        private static List<Vector2> EllipseArc(Vector2 c, float rx, float rz, float from, float to, int segments)
        {
            var l = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                var q = Ellipse(c, rx, rz, Mathf.Lerp(from, to, i / (float)segments));
                l.Add(new Vector2(q.x, q.z));
            }

            return l;
        }

        private static bool InRanges(Vector2[] ranges, float a)
        {
            for (int i = 0; i < ranges.Length; i++)
            {
                if (a > Mathf.Min(ranges[i].x, ranges[i].y) && a < Mathf.Max(ranges[i].x, ranges[i].y))
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ ceiling, skylights, beams

        private static void Ceiling(Transform p)
        {
            var c = RrBuild.Group(p, "Ceiling").transform;
            var s = _s.Ceiling;
            float y0 = L.CeilingY;
            float y1 = L.CeilingY + 0.3f;
            float xl = -9.32f;
            float xr = L.HallRight + T;
            var f = L.SkyFront;
            var b = L.SkyBack;
            Slab(c, "Ceiling", xl, y0, L.Entrance - T, xr, y1, f.x, s, false);
            Slab(c, "Ceiling", xl, y0, f.x, L.SkyX0, y1, f.y, s, false);
            Slab(c, "Ceiling", L.SkyX1, y0, f.x, xr, y1, f.y, s, false);
            Slab(c, "Ceiling", xl, y0, f.y, xr, y1, b.x, s, false);
            Slab(c, "Ceiling", xl, y0, b.x, L.SkyX0, y1, L.CurveCenter.y, s, false);
            Slab(c, "Ceiling", L.SkyX1, y0, b.x, xr, y1, L.CurveCenter.y, s, false);
            Slab(c, "Ceiling", xl, y0, L.CurveCenter.y, L.SkyX0, y1, b.y, s, false);
            Slab(c, "Ceiling", xl, y0, b.y, -1.5f, y1, L.BackLeftZ + T, s, false);
            _count += 8;

            // Right of the back skylight up to the curve, and the curved end beyond it (convex prisms).
            float r = L.CurveRadius + T;
            float a1 = Mathf.Asin((b.y - L.CurveCenter.y) / r) * Mathf.Rad2Deg;
            float end = Mathf.Acos((-1.5f - L.CurveCenter.x) / r) * Mathf.Rad2Deg;
            var right = new List<Vector2> { new Vector2(L.SkyX1, L.CurveCenter.y) };
            right.AddRange(RrRcGeo.Arc(L.CurveCenter, r, 0f, a1, 6));
            right.Add(new Vector2(L.SkyX1, b.y));
            RrRcGeo.Prism(c, "Ceiling Curve Right", right, y0, y1, s, false);
            var cap = new List<Vector2> { new Vector2(-1.5f, b.y) };
            cap.AddRange(RrRcGeo.Arc(L.CurveCenter, r, a1, end, 20));
            RrRcGeo.Prism(c, "Ceiling Curve", cap, y0, y1, s, false);
            _count += 2;

            // Over the balcony the roof rises to the left wall (GUESS: 9.0 at the balcony edge to 11.9 at x -13.9).
            var lo = new Vector2(-9.32f, y0);
            var hi = new Vector2(L.HallLeft - 0.2f, L.SlopeTop + 0.13f);
            var d = lo - hi;
            float len = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angle);
            var n = rot * Vector3.up;
            var mid = new Vector3((lo.x + hi.x) * 0.5f, (lo.y + hi.y) * 0.5f, (L.Entrance - T + L.BackLeftZ + T) * 0.5f) + n * 0.15f;
            RrRcGeo.Box(c, "Ceiling Slope", mid, rot, new Vector3(len + 0.1f, 0.3f, L.BackLeftZ - L.Entrance + 2f * T), s, false);
            _count++;

            // Skylights (Window_Skylight, 6.8 x 6.1 m, glass 9.25..9.57): cream wells, glass, a navy 6 x 6 pane grid.
            Skylight(c, f);
            Skylight(c, b);
        }

        private static void Skylight(Transform p, Vector2 z)
        {
            float y0 = L.CeilingY;
            float yg = 9.55f;
            RrRcGeo.Slab(p, "Skylight Well", new Vector3(L.SkyX0 - 0.1f, y0, z.x - 0.1f), new Vector3(L.SkyX1 + 0.1f, 9.65f, z.x), _s.Ceiling, false);
            RrRcGeo.Slab(p, "Skylight Well", new Vector3(L.SkyX0 - 0.1f, y0, z.y), new Vector3(L.SkyX1 + 0.1f, 9.65f, z.y + 0.1f), _s.Ceiling, false);
            RrRcGeo.Slab(p, "Skylight Well", new Vector3(L.SkyX0 - 0.1f, y0, z.x), new Vector3(L.SkyX0, 9.65f, z.y), _s.Ceiling, false);
            RrRcGeo.Slab(p, "Skylight Well", new Vector3(L.SkyX1, y0, z.x), new Vector3(L.SkyX1 + 0.1f, 9.65f, z.y), _s.Ceiling, false);
            _glass.Box(new Vector3((L.SkyX0 + L.SkyX1) * 0.5f, yg, (z.x + z.y) * 0.5f), Quaternion.identity, new Vector3(L.SkyX1 - L.SkyX0, 0.03f, z.y - z.x), true);
            for (int i = 0; i <= 6; i++)
            {
                float x = Mathf.Lerp(L.SkyX0, L.SkyX1, i / 6f);
                _navy.Box(new Vector3(x, yg - 0.07f, (z.x + z.y) * 0.5f), Quaternion.identity, new Vector3(i == 0 || i == 6 ? 0.16f : 0.09f, 0.14f, z.y - z.x));
                float zz = Mathf.Lerp(z.x, z.y, i / 6f);
                _navy.Box(new Vector3((L.SkyX0 + L.SkyX1) * 0.5f, yg - 0.07f, zz), Quaternion.identity, new Vector3(L.SkyX1 - L.SkyX0, 0.14f, i == 0 || i == 6 ? 0.16f : 0.09f));
            }

            _count += 4;
        }

        /// <summary>
        /// Timber beams (Beams/Beams, dark brown, underside 8.4, top 9.0): the measured cross beams at z -15.39,
        /// -2.18 and 2.41, the short ones over the balcony at -12.17 and -7.27, the lengthwise beams at x -9.32 and
        /// -13.83, the entrance-wall corner posts; plus GUESS trimmers round the skylights and posts under the
        /// balcony edge beam, for the picture's crossing timber-frame look.
        /// </summary>
        private static void Beams()
        {
            float y = (L.BeamBottom + L.CeilingY) * 0.5f;
            float h = L.CeilingY - L.BeamBottom;
            void Cross(float z, float x0, float x1)
            {
                _timber.Box(new Vector3((x0 + x1) * 0.5f, y, z), Quaternion.identity, new Vector3(x1 - x0, h, 0.42f));
            }

            void Along(float x, float z0, float z1)
            {
                _timber.Box(new Vector3(x, y - 0.03f, (z0 + z1) * 0.5f), Quaternion.identity, new Vector3(0.4f, h - 0.06f, z1 - z0));
            }

            void Post(float x, float z, float top)
            {
                _timber.Box(new Vector3(x, top * 0.5f, z), Quaternion.identity, new Vector3(0.36f, top, 0.36f));
            }

            Cross(-15.39f, L.HallLeft, L.HallRight);
            Cross(-12.17f, L.HallLeft, -9.15f);
            Cross(-7.27f, L.HallLeft, -9.15f);
            Cross(-9.7f, -9.32f, L.HallRight);
            Cross(-2.18f, L.HallLeft, L.HallRight);
            Cross(2.41f, L.HallLeft, L.HallRight);
            Cross(10.65f, L.HallLeft, 10.9f);
            Along(-9.32f, L.Entrance, L.BackLeftZ);
            Along(-13.73f, L.Entrance, L.BackLeftZ);
            Along(L.SkyX0 - 0.15f, L.Entrance, 10.65f);
            Along(L.SkyX1 + 0.15f, L.Entrance, 10.65f);
            Post(-13.7f, -15.4f, L.CeilingY);
            Post(7.7f, -15.4f, L.CeilingY);
            float[] pz = { -12.17f, -7.27f, -2.18f, 2.41f, 8.5f };
            for (int i = 0; i < pz.Length; i++)
            {
                Post(-9.25f, pz[i], L.BeamBottom);
            }
        }

        // ------------------------------------------------------------------ balcony and stairs

        private static void Balcony(Transform p)
        {
            var b = RrBuild.Group(p, "Balcony").transform;
            float x0 = L.HallLeft;
            float x1 = L.BalconyEdge;
            float[] zs = { L.Entrance, -6f, 3f, L.BalconyExtEnd };
            for (int i = 0; i < 3; i++)
            {
                Slab(b, "Balcony Deck", x0, L.BalconyY - 0.05f, zs[i], x1, L.BalconyY, zs[i + 1], _s.Floor);
                Slab(b, "Balcony Underside", x0, L.BalconyUnder, zs[i], x1, L.BalconyY - 0.05f, zs[i + 1], _s.Ceiling);
            }

            // Landing at the top of the back stairs, and the front stair's top joint.
            Slab(b, "Landing", x0, L.BalconyY - 0.05f, L.BalconyExtEnd, L.BackStairHigh, L.BalconyY, L.BackLeftZ, _s.Floor);
            Slab(b, "Landing Underside", x0, L.BalconyUnder, L.BalconyExtEnd, L.BackStairHigh, L.BalconyY - 0.05f, L.BackLeftZ, _s.Ceiling);
            _count += 8;

            // Timber fascia along the balcony edge.
            _timber.Box(new Vector3(x1 + 0.05f, (L.BalconyUnder + L.BalconyY) * 0.5f, (L.FrontStairZ1 + L.BalconyExtEnd) * 0.5f), Quaternion.identity, new Vector3(0.12f, L.BalconyY - L.BalconyUnder + 0.04f, L.BalconyExtEnd - L.FrontStairZ1));

            // Railing: along the edge from the front stair's top to the extension's end, then along the extension's
            // back edge (over the back stairs). Posts ~2.35 m (measured z -11.65 .. 7.01), timber handrail, navy rails.
            Railing(b, new Vector3(x1 + 0.03f, L.BalconyY, L.FrontStairZ1), new Vector3(x1 + 0.03f, L.BalconyY, L.BalconyExtEnd));
            Railing(b, new Vector3(x1 + 0.03f, L.BalconyY, L.BalconyExtEnd - 0.03f), new Vector3(L.BackStairHigh, L.BalconyY, L.BalconyExtEnd - 0.03f));

            // Under the balcony's end: the Magic Door's wall (the Campus Jams door's spot) closing a small room.
            var blue = Bands(0f, _s.Blue, 12f);
            Wall(b, "Magic Door Wall", false, -9.45f, -1f, L.BalconyEnd, L.BalconyExtEnd, 0f, L.BalconyUnder, blue);
            Wall(b, "Under Balcony End", true, L.BalconyEnd, 1f, x0, -9.45f, 0f, L.BalconyUnder, blue);
            Wall(b, "Back Stair Side", true, L.BalconyExtEnd, -1f, x0, -9.45f, 0f, L.BalconyUnder, blue);

            // Fluorescent fixtures under the balcony (Light_Flourescent at x -11.63, y 3.10, z -5.84 / -1.44 / 2.74).
            float[] fz = { -5.84f, -1.44f, 2.74f, -10.5f };
            for (int i = 0; i < fz.Length; i++)
            {
                RrRcGeo.Box(b, "Fluorescent", new Vector3(-11.63f, L.BalconyUnder - 0.03f, fz[i]), Quaternion.identity, new Vector3(0.35f, 0.05f, 1.25f), _s.Fluorescent, false);
            }

            _count += 4;
        }

        /// <summary>A straight railing from a to b on a floor at a.y: timber posts and handrail (top 4.15 - 3.10 = 1.05 m), 2 navy rails, a guard collider.</summary>
        private static void Railing(Transform p, Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = new Vector2(d.x, d.z).magnitude;
            var dir = new Vector3(d.x, 0f, d.z).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float h = L.RailTop - L.BalconyY;
            var mid = (a + b) * 0.5f;
            _timber.Box(mid + Vector3.up * (h - 0.04f), rot, new Vector3(0.12f, 0.08f, len));
            _navy.Box(mid + Vector3.up * (h * 0.62f), rot, new Vector3(0.04f, 0.04f, len));
            _navy.Box(mid + Vector3.up * (h * 0.3f), rot, new Vector3(0.04f, 0.04f, len));
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.35f));
            for (int i = 0; i <= posts; i++)
            {
                _timber.Box(a + dir * (len * i / posts) + Vector3.up * (h * 0.5f), rot, new Vector3(0.1f, h, 0.1f));
            }

            RrRcGeo.Solid(p, "Railing Guard", mid + Vector3.up * 0.6f, rot, new Vector3(0.1f, 1.2f, len));
        }

        /// <summary>
        /// Front stairs (Collision/Floor/FrontStairs: along the entrance wall, 2.7 m wide, climbing toward -X from
        /// x -3.9 to the balcony) and back stairs (GUESS layout in the measured stairwell: 1.8 m wide along the gym's
        /// front wall, climbing toward -X from x -7.0 to the landing). 18 visible steps each (rise 0.172 m) over a
        /// ramp collider through the step centres (29 deg), so the controller glides instead of catching on steps.
        /// </summary>
        private static void Stairs(Transform p)
        {
            var s = RrBuild.Group(p, "Stairs").transform;
            Flight(s, "Front Stairs", L.FrontStairLow, L.BalconyEdge, L.Entrance, L.FrontStairZ1);
            Flight(s, "Back Stairs", L.BackStairLow, L.BackStairHigh, L.BalconyExtEnd, L.BackLeftZ);

            // Hall-side rails and guards: the front stairs' open side (z -12.9) and the back stairs' open part.
            SlopedRail(s, new Vector3(L.FrontStairLow, 0f, L.FrontStairZ1 + 0.05f), new Vector3(L.BalconyEdge, L.BalconyY, L.FrontStairZ1 + 0.05f));
            float xm = L.BalconyEdge;
            float ym = L.BalconyY * (L.BackStairLow - xm) / (L.BackStairLow - L.BackStairHigh);
            SlopedRail(s, new Vector3(L.BackStairLow, 0f, L.BalconyExtEnd + 0.05f), new Vector3(xm, ym, L.BalconyExtEnd + 0.05f));
        }

        private static void Flight(Transform p, string name, float xLow, float xHigh, float z0, float z1)
        {
            const int Steps = 18;
            float run = (xLow - xHigh) / Steps;
            float rise = L.BalconyY / Steps;
            var steps = new RrRcBatch(_s.Floor);
            for (int i = 0; i < Steps; i++)
            {
                float xa = xLow - (i + 1) * run;
                float xb = xLow - i * run;
                steps.Slab(new Vector3(xa, 0f, z0), new Vector3(xb, (i + 1) * rise, z1));
            }

            steps.Build(p, name);
            _count += steps.Count;
            float zc = (z0 + z1) * 0.5f;
            float w = z1 - z0;
            RrRcGeo.Ramp(p, name + " Ramp", new Vector3(xLow + run * 0.5f, 0f, zc), new Vector3(xHigh + run * 0.5f, L.BalconyY, zc), w);
            // The top joint to the balcony, and solid fill under the ramp (the steps have no colliders), kept 0.25 m
            // under the ramp's surface so it never pokes through: nobody walks into the space under the stairs.
            RrRcGeo.Solid(p, name + " Top", new Vector3(xHigh + 0.1f, L.BalconyY - 0.1f, zc), Quaternion.identity, new Vector3(0.6f, 0.2f, w));
            float span = xLow - xHigh;
            for (int k = 1; k < 3; k++)
            {
                float xa = xLow - k * span / 3f;
                float xb = xLow - (k + 1) * span / 3f;
                float h = L.BalconyY * k / 3f - 0.25f;
                RrRcGeo.Solid(p, name + " Fill", new Vector3((xa + xb) * 0.5f, h * 0.5f, zc), Quaternion.identity, new Vector3(xa - xb, h, w));
            }
        }

        private static void SlopedRail(Transform p, Vector3 a, Vector3 b)
        {
            var d = b - a;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            var mid = (a + b) * 0.5f;
            const float H = 0.95f;
            _timber.Box(mid + Vector3.up * H, rot, new Vector3(0.1f, 0.08f, d.magnitude));
            _navy.Box(mid + Vector3.up * (H * 0.5f), rot, new Vector3(0.04f, 0.04f, d.magnitude));
            for (int i = 0; i <= 3; i++)
            {
                var q = a + d * (i / 3f);
                _timber.Box(q + Vector3.up * (H * 0.5f), Quaternion.identity, new Vector3(0.09f, H, 0.09f));
            }

            RrRcGeo.Solid(p, "Stair Guard", mid + Vector3.up * 0.6f, rot, new Vector3(0.1f, 1.2f, d.magnitude));
        }

        // ------------------------------------------------------------------ gym

        /// <summary>
        /// The gym (back left, x -18..-1.8, z 13.3..25.9): terracotta walls ("a corridor with orange walls ... a big
        /// window onto green trees"), the flat back windows (Window_Flat_Wide x -16.88..-8.78, _Narrow -6.08..-3.07,
        /// y 0.87..5.05), the high side windows on the hoop wall (y 3.65..5.73), a two-hoop court with painted keys
        /// (the scene's key: x -17.7..-11.2, z 17.7..21.3), ceiling 6.5 (GUESS).
        /// </summary>
        private static void Gym(Transform p)
        {
            var g = RrBuild.Group(p, "Gym").transform;
            var bands = Bands(0f, _s.Terracotta, 3.6f, _s.Cream, 12.2f);
            float top = L.GymCeiling;
            Wall(g, "Gym Front Wall", true, L.GymFront, -1f, L.GymLeft - T, L.GymOpening0, 0f, top, bands);
            Wall(g, "Gym Header", true, L.GymFront, -1f, L.GymLeft - T, -1.5f, top, L.CeilingY, bands);
            Wall(g, "Gym Header High", true, L.GymFront, -1f, L.HallLeft - T, -9.32f, L.CeilingY, 12.2f, bands);
            Wall(g, "Gym Right Wall", false, L.GymRight, 1f, L.BackLeftZ, 17.8f, 0f, L.CeilingY, bands);
            Wall(g, "Gym Right Wall", false, L.GymRight, 1f, 17.8f, L.GymBack + T, 0f, top, bands);
            Wall(g, "Gym Left Wall", false, L.GymLeft, -1f, L.BackLeftZ, L.GymBack + T, 0f, top, bands,
                Hole(14.6f, 17.3f, 3.65f, 5.73f), Hole(21.6f, 24.3f, 3.65f, 5.73f));
            Wall(g, "Gym Back Wall", true, L.GymBack, 1f, L.GymLeft - T, L.GymRight + T, 0f, top, bands,
                Hole(-16.88f, -8.78f, 0.87f, 5.05f), Hole(-6.08f, -3.07f, 0.87f, 5.05f));
            Window(false, L.GymLeft, -1f, 14.6f, 17.3f, 3.65f, 5.73f, 0.9f, 1.05f);
            Window(false, L.GymLeft, -1f, 21.6f, 24.3f, 3.65f, 5.73f, 0.9f, 1.05f);
            Window(true, L.GymBack, 1f, -16.88f, -8.78f, 0.87f, 5.05f, 1.0f, 1.05f);
            Window(true, L.GymBack, 1f, -6.08f, -3.07f, 0.87f, 5.05f, 1.0f, 1.05f);
            Slab(g, "Gym Ceiling", L.GymLeft - T, top, L.GymFront - T, L.GymRight + T, top + 0.3f, L.GymBack + T, _s.Ceiling, false);
            Trim(true, L.GymFront - T, -1f, L.GymOpening0, L.GymRight, 0f, top - 0.2f);
            _count++;

            // Court: two orange keys with white lines, free-throw circles, a centre line and the boundary.
            var court = RrBuild.Group(g, "Court").transform;
            var line = new RrRcBatch(new RrRcSurface(RrLook.Solid(new Color(0.97f, 0.96f, 0.93f)), 1f, false));
            float cz = 19.5f;
            for (int side = 0; side < 2; side++)
            {
                float wall = side == 0 ? L.GymLeft + 0.3f : L.GymRight - 0.3f;
                float ft = side == 0 ? wall + 4.8f : wall - 4.8f;
                float xa = Mathf.Min(wall, ft);
                float xb = Mathf.Max(wall, ft);
                RrRcGeo.Slab(court, "Key", new Vector3(xa, 0f, cz - 1.8f), new Vector3(xb, 0.006f, cz + 1.8f), _s.Orange, false);
                Line(line, new Vector2(xa, cz - 1.8f), new Vector2(xb, cz - 1.8f));
                Line(line, new Vector2(xa, cz + 1.8f), new Vector2(xb, cz + 1.8f));
                Line(line, new Vector2(ft, cz - 1.8f), new Vector2(ft, cz + 1.8f));
                ArcLine(line, new Vector2(ft, cz), 1.8f, side == 0 ? -90f : 90f, side == 0 ? 90f : 270f, 14);
                _count++;
            }

            float cx = (L.GymLeft + L.GymRight) * 0.5f;
            Line(line, new Vector2(cx, 14.0f), new Vector2(cx, 25.0f));
            Line(line, new Vector2(L.GymLeft + 0.3f, 14.0f), new Vector2(L.GymRight - 0.3f, 14.0f));
            Line(line, new Vector2(L.GymLeft + 0.3f, 25.0f), new Vector2(L.GymRight - 0.3f, 25.0f));
            Line(line, new Vector2(L.GymLeft + 0.3f, 14.0f), new Vector2(L.GymLeft + 0.3f, 25.0f));
            Line(line, new Vector2(L.GymRight - 0.3f, 14.0f), new Vector2(L.GymRight - 0.3f, 25.0f));
            line.Build(court, "Court Lines");
            _count += line.Count;

            // Posters: Coach says STAY HYDRATED by the vending machine, the Charades posters on the front wall.
            if (_s.HydratePoster != null)
            {
                RrRcGeo.Quad(g, "Stay Hydrated", new Vector3(L.GymRight - 0.02f, 1.8f, 22.4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(0.6f, 0.9f), _s.HydratePoster);
            }

            if (_s.CharadesPoster != null)
            {
                RrRcGeo.Quad(g, "Event Posters", new Vector3(-13.0f, 1.9f, L.GymFront + 0.02f), Quaternion.Euler(0f, 180f, 0f), new Vector2(1.6f, 1.1f), _s.CharadesPoster);
            }
        }

        private static void Line(RrRcBatch b, Vector2 a, Vector2 c)
        {
            var d = c - a;
            float len = d.magnitude;
            if (len < 0.001f)
            {
                return;
            }

            var m = (a + c) * 0.5f;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            b.Box(new Vector3(m.x, 0.008f, m.y), Quaternion.Euler(0f, yaw, 0f), new Vector3(0.05f, 0.004f, len + 0.02f));
        }

        /// <summary>An arc on the floor; angle 0 = +X from the centre, positive toward +Z.</summary>
        private static void ArcLine(RrRcBatch b, Vector2 c, float r, float from, float to, int segments)
        {
            var pts = RrRcGeo.Arc(c, r, from, to, segments);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Line(b, pts[i], pts[i + 1]);
            }
        }

        // ------------------------------------------------------------------ theatre

        /// <summary>
        /// The theatre behind the stage opening (x 11.8..21.6, z -10.3..1.8, floor -0.79): 4 steps down over a ramp,
        /// plum walls, a black ceiling (GUESS 5.5 m), a little stage with a red backdrop at its +Z end (the scene's
        /// inner curtains at z 1.18), a rug, and the 4 quest poster cases flanking the QUESTS door on the far wall
        /// (Quest_Poster_Case at x 21.52, z -8.54 / -6.58 / -2.17 / -0.21), showing the quests' cover art.
        /// </summary>
        private static void Theatre(Transform p)
        {
            var t = RrBuild.Group(p, "Theatre").transform;
            float f = L.TheatreFloor;
            var plum = Bands(f - 0.3f, _s.Plum, 12f);
            Wall(t, "Theatre Far Wall", false, L.TheatreX1, 1f, L.TheatreZ0 - T, L.TheatreZ1 + T, f, L.TheatreCeiling, plum);
            Wall(t, "Theatre Wall", true, L.TheatreZ0, -1f, L.TheatreX0, L.TheatreX1 + T, f, L.TheatreCeiling, plum);
            Wall(t, "Theatre Wall", true, L.TheatreZ1, 1f, L.TheatreX0, L.TheatreX1 + T, f, L.TheatreCeiling, plum);
            Slab(t, "Theatre Plinth", L.HallRight, f - 0.3f, L.TheatreZ0 - T, L.TheatreX0, -0.3f, L.TheatreZ1 + T, _s.Plum);
            Slab(t, "Theatre Ceiling", L.TheatreX0 - T, L.TheatreCeiling, L.TheatreZ0 - T, L.TheatreX1 + T, L.TheatreCeiling + 0.3f, L.TheatreZ1 + T, _s.Black, false);

            // Steps down (4 x 0.1975 m) over a ramp collider from the hall floor to the theatre floor (26 deg).
            var steps = new RrRcBatch(_s.Floor);
            float run = (L.TheatreStepsEnd - L.TheatreX0) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float top = f * (i + 1) / 5f;
                steps.Slab(new Vector3(L.TheatreX0 + i * run, f, L.StageZ0), new Vector3(L.TheatreX0 + (i + 1) * run, top, L.StageZ1));
            }

            steps.Build(t, "Theatre Steps");
            float zc = (L.StageZ0 + L.StageZ1) * 0.5f;
            RrRcGeo.Ramp(t, "Theatre Ramp", new Vector3(L.HallRight, 0f, zc), new Vector3(L.TheatreStepsEnd, f, zc), L.StageZ1 - L.StageZ0);
            _count += 9;

            // The little stage and its red backdrop.
            Slab(t, "Theatre Stage", 14.2f, f, -0.2f, 19.9f, f + 0.3f, L.TheatreZ1, _s.LightWood);
            Curtains(t, 14.2f, 19.9f, L.TheatreZ1 - 0.08f, f + 0.3f, 4.6f, true);
            RrRcGeo.Box(t, "Theatre Rug", new Vector3(16.77f, f + 0.006f, -4.46f), Quaternion.identity, new Vector3(3.2f, 0.012f, 4.4f), _s.CarpetNavy, false);
            _count += 2;

            // Quest poster cases (Quest_Poster_Case_Mat (1, 0.854, 0.517) + emission) with the quests' covers.
            var gold = RrLook.Emissive(new Color(1f, 0.854f, 0.517f), 0.6f);
            RrActivity[] quests = { RrActivity.GoldenTrophy, RrActivity.CrimsonCauldron, RrActivity.Jumbotron, RrActivity.LostSkulls };
            float[] qz = { -8.54f, -6.58f, -2.17f, -0.21f };
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(L.TheatreX1 - 0.06f, f + 1.45f, qz[i]);
                RrBuild.Box(t, "Quest Poster Case", c, new Vector3(0.1f, 2.0f, 1.4f), gold, false);
                var quad = RrRcGeo.Quad(t, "Quest Poster", c + Vector3.left * 0.06f, Quaternion.Euler(0f, 90f, 0f), new Vector2(1.2f, 1.8f), RrLook.Unlit(new Color(0.2f, 0.18f, 0.22f)));
                var info = RrRooms.Get(quests[i]);
                string title = RrCategoryDoor.TitleOf(quests[i]).ToUpperInvariant() + (RrCategoryDoor.IsBuilt(quests[i]) ? "" : "\n<size=70%>COMING SOON</size>");
                var label = RrRcGeo.Label(t, "Quest Poster Title", title, c + new Vector3(-0.08f, -0.75f, 0f), 90f, 0.12f, 1.1f, new Color(1f, 0.93f, 0.75f));
                label.textWrappingMode = TMPro.TextWrappingModes.Normal;
                label.rectTransform.sizeDelta = new Vector2(1.1f, 0.4f);
                Cover(quad, info != null ? info.CoverSprite : null);
                _count += 3;
            }
        }

        /// <summary>Puts an activity's cover art on a poster quad once it loads (the quad may be gone by then).</summary>
        private static void Cover(GameObject quad, string sprite)
        {
            if (string.IsNullOrEmpty(sprite))
            {
                return;
            }

            RrCoverArt.Load(sprite, art =>
            {
                if (quad == null || art == null || art.Texture == null)
                {
                    return;
                }

                var m = new Material(RrLook.Unlit(Color.white)) { name = "Rr Rec Center Quest Poster" };
                m.SetTexture("_BaseMap", art.Texture);
                m.SetTextureScale("_BaseMap", new Vector2(art.Uv.width, art.Uv.height));
                m.SetTextureOffset("_BaseMap", new Vector2(art.Uv.x, art.Uv.y));
                m.SetColor("_BaseColor", Color.white);
                RrRcGeo.Own(m);
                quad.GetComponent<Renderer>().sharedMaterial = m;
            });
        }

        /// <summary>A gathered curtain from a0 to a1 at <paramref name="plane"/> (red folds alternating 6 cm).</summary>
        private static void Curtains(Transform p, float a0, float a1, float plane, float y0, float y1, bool alongX)
        {
            var b = new RrRcBatch(_s.Curtain);
            int folds = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 0.22f));
            float w = (a1 - a0) / folds;
            for (int i = 0; i < folds; i++)
            {
                float a = a0 + (i + 0.5f) * w;
                float off = (i % 2 == 0 ? 0.03f : -0.03f);
                b.Box(Point(alongX, a, (y0 + y1) * 0.5f, plane + off), Quaternion.identity, alongX ? new Vector3(w + 0.02f, y1 - y0, 0.06f) : new Vector3(0.06f, y1 - y0, w + 0.02f));
            }

            b.Build(p, "Curtain");
            _count += b.Count;
        }

        /// <summary>
        /// The stage front on the hall side: red curtains gathered at both sides of the opening and a valance
        /// ([Curtain] at z -8.68 / -1.80, [Curtain_Flat] at y 4.21), and the lit marquee above it
        /// ([RecCenter_Marquee] x 10.87..11.29, y 4.60..7.96, z -9.33..-1.32: bulbs around a pink face). The marquee's
        /// text is a GUESS (its texture was not read): it points the way to the Quests door.
        /// </summary>
        private static void StageFront(Transform p)
        {
            var s = RrBuild.Group(p, "Stage").transform;
            float x = L.HallRight + 0.15f;
            Curtains(s, L.StageZ0, L.StageZ0 + 1.0f, x, 0f, L.StageTop, false);
            Curtains(s, L.StageZ1 - 1.0f, L.StageZ1, x, 0f, L.StageTop, false);
            Curtains(s, L.StageZ0, L.StageZ1, x - 0.1f, L.StageTop - 0.5f, L.StageTop, false);

            // Marquee: a pink face in a dark frame, 60 bulbs, two lines of text.
            var face = RrLook.Solid(new Color(0.86f, 0.33f, 0.52f));
            var frame = RrLook.Solid(new Color(0.30f, 0.10f, 0.18f));
            float mx = 11.08f;
            float y0 = 4.6f;
            float y1 = 7.96f;
            float z0 = -9.33f;
            float z1 = -1.32f;
            RrBuild.Box(s, "Marquee Frame", new Vector3(L.HallRight - 0.2f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.4f, y1 - y0, z1 - z0), frame, false);
            RrBuild.Box(s, "Marquee Face", new Vector3(mx - 0.04f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.02f, y1 - y0 - 0.5f, z1 - z0 - 0.5f), face, false);
            var bulbs = new RrRcBatch(new RrRcSurface(RrLook.Emissive(new Color(1f, 0.86f, 0.46f), 2.2f), 1f, false));
            float spacing = 0.38f;
            int nz = Mathf.RoundToInt((z1 - z0 - 0.3f) / spacing);
            int ny = Mathf.RoundToInt((y1 - y0 - 0.3f) / spacing);
            for (int i = 0; i <= nz; i++)
            {
                float z = Mathf.Lerp(z0 + 0.15f, z1 - 0.15f, i / (float)nz);
                bulbs.Box(new Vector3(mx - 0.06f, y0 + 0.15f, z), Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.1f);
                bulbs.Box(new Vector3(mx - 0.06f, y1 - 0.15f, z), Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.1f);
            }

            for (int j = 1; j < ny; j++)
            {
                float y = Mathf.Lerp(y0 + 0.15f, y1 - 0.15f, j / (float)ny);
                bulbs.Box(new Vector3(mx - 0.06f, y, z0 + 0.15f), Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.1f);
                bulbs.Box(new Vector3(mx - 0.06f, y, z1 - 0.15f), Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.1f);
            }

            bulbs.Build(s, "Marquee Bulbs");
            _count += bulbs.Count + 2;
            var cream = new Color(1f, 0.95f, 0.8f);
            RrRcGeo.Label(s, "Marquee Top", "NOW PLAYING", new Vector3(mx - 0.07f, 7.1f, (z0 + z1) * 0.5f), 90f, 0.42f, 6.5f, new Color(1f, 0.88f, 0.4f));
            RrRcGeo.Label(s, "Marquee Title", "QUESTS", new Vector3(mx - 0.07f, 5.75f, (z0 + z1) * 0.5f), 90f, 1.2f, 7.0f, cream);
        }

        // ------------------------------------------------------------------ wing (cafe + upper floor)

        /// <summary>
        /// The left wing (x -25..-14.2): the cafe downstairs (counter [RecCenter_CafeCounter] across the wing at
        /// z -5.86..-4.49, back counter and two chalkboards on the back wall z -7.4, the big cafe window
        /// Window_Cafe x -25, z -3.6..4.72, y 0.58..5.93, 5 tables), and the upper floor (y 3.10) over its front part
        /// with a railing overlooking the cafe (the scene's curved railing, straight here) and the runner to the
        /// FEATURED ROOMS door. Roof at 8.4 (fluorescents upstairs at 8.27).
        /// </summary>
        private static void Wing(Transform p)
        {
            var w = RrBuild.Group(p, "Wing").transform;
            var s = _s;
            var bands = Bands(0f, s.Teal, 1.2f, s.Cream, L.BalconyUnder, s.Cream, L.BalconyY, s.Blue, 4.3f, s.Cream, 12.2f);
            float bayZ = L.CafeBayCenter.y;
            Wall(w, "Wing Far Wall", false, L.WingLeft, -1f, L.WingFront - T, bayZ, 0f, L.WingRoof, bands);
            ArcWall(w, "Cafe Bay", L.CafeBayCenter, L.CafeBayRx, L.CafeBayRz, 180f, 90f, new[] { new Vector2(90f, 180f) }, L.CafeBaySill, L.CafeBayTop, L.WingRoof,
                s.Teal, s.Cream, s.Cream, new[] { 2.36f, 4.14f });
            Wall(w, "Wing Front Wall", true, L.WingFront, -1f, L.WingLeft - T, L.WingRight, 0f, L.WingRoof, bands);
            Wall(w, "Wing Back Wall", true, L.WingBack, 1f, L.CafeBayCenter.x, L.WingRight, 0f, L.WingRoof, bands);
            Wall(w, "Cafe Back Wall", true, L.CafeBackWall, -1f, L.WingLeft, L.WingRight, 0f, L.BalconyUnder, Bands(0f, s.Teal, 1.2f, s.Cream, 3f));
            WingPlan(w, "Wing Roof", L.WingRoof, L.WingRoof + 0.3f, s.Ceiling, false);

            // Upper floor over the front part, its underside over the cafe counter, and the railing at its edge.
            Slab(w, "Upper Floor", L.WingLeft, L.BalconyY - 0.05f, L.WingFront, L.WingRight, L.BalconyY, L.UpperEdge, s.Floor);
            Slab(w, "Upper Floor Underside", L.WingLeft, L.BalconyUnder, L.WingFront, L.WingRight, L.BalconyY - 0.05f, L.UpperEdge, s.Ceiling);
            _timber.Box(new Vector3((L.WingLeft + L.WingRight) * 0.5f, (L.BalconyUnder + L.BalconyY) * 0.5f, L.UpperEdge + 0.05f), Quaternion.identity, new Vector3(L.WingRight - L.WingLeft, 0.24f, 0.12f));
            Railing(w, new Vector3(L.WingLeft, L.BalconyY, L.UpperEdge - 0.03f), new Vector3(L.WingRight, L.BalconyY, L.UpperEdge - 0.03f));
            _count += 3;

            // Cafe counter (cafe-atlas colours: teal body, cream band, light wood top), a glass sneeze guard over the
            // cake end, the back counter, shelves and two chalkboards.
            float top = L.CafeCounterTop;
            Slab(w, "Cafe Counter", L.WingLeft, 0f, -5.86f, L.WingRight, top - 0.12f, -4.49f, s.Teal);
            Slab(w, "Cafe Counter Band", L.WingLeft, top - 0.12f, -5.88f, L.WingRight, top - 0.04f, -4.47f, s.Cream);
            Slab(w, "Cafe Counter Top", L.WingLeft, top - 0.04f, -5.9f, L.WingRight, top, -4.42f, s.LightWood);
            RrBuild.Box(w, "Sneeze Guard", new Vector3(-19.44f, top + 0.22f, -4.52f), new Vector3(3.62f, 0.44f, 0.02f), _s.Glass.Material, false);
            Slab(w, "Back Counter", L.WingLeft, 0f, -7.41f, L.WingRight, 1.06f, -6.73f, s.Teal);
            Slab(w, "Back Counter Top", L.WingLeft, 1.02f, -7.41f, L.WingRight, 1.06f, -6.7f, s.LightWood);
            float[] shelfY = { 1.34f, 1.82f, 2.29f };
            for (int i = 0; i < shelfY.Length; i++)
            {
                _timber.Box(new Vector3(-19.4f, shelfY[i], -7.25f), Quaternion.identity, new Vector3(2.2f, 0.05f, 0.3f));
            }

            Chalkboard(w, new Vector3(-23.02f, 1.99f, L.CafeBackWall + 0.03f), "COFFEE\n<size=60%>drip - latte - cocoa</size>");
            Chalkboard(w, new Vector3(-15.76f, 1.99f, L.CafeBackWall + 0.03f), "TREATS\n<size=60%>donuts - cakes - pizza</size>");
            RrRcGeo.Label(w, "Cafe Sign", "REC CENTER CAFE", new Vector3(-19.44f, 2.62f, L.CafeBackWall + 0.03f), 180f, 0.26f, 3.6f, new Color(0.96f, 0.55f, 0.20f));
            RrRcGeo.Box(w, "Cafe Fluorescent", new Vector3(-19.42f, L.BalconyUnder - 0.03f, -6.39f), Quaternion.identity, new Vector3(1.25f, 0.05f, 0.35f), s.Fluorescent, false);
            _count += 9;

            // Pendant cords from the roof to each pendant's top.
            for (int i = 0; i < L.CafePendants.Length; i++)
            {
                var pp = L.CafePendants[i];
                _timber.Box(new Vector3(pp.x, (pp.y + L.WingRoof) * 0.5f, pp.z), Quaternion.identity, new Vector3(0.02f, L.WingRoof - pp.y, 0.02f));
            }

            // Upstairs fluorescents (y 8.27, x -21.28 / -17.02, z -12.06) and a "PUZZLES & ART" sign over its corner.
            RrRcGeo.Box(w, "Upper Fluorescent", new Vector3(-21.28f, L.WingRoof - 0.03f, -12.06f), Quaternion.identity, new Vector3(1.25f, 0.05f, 0.35f), s.Fluorescent, false);
            RrRcGeo.Box(w, "Upper Fluorescent", new Vector3(-17.02f, L.WingRoof - 0.03f, -12.06f), Quaternion.identity, new Vector3(1.25f, 0.05f, 0.35f), s.Fluorescent, false);
            RrRcGeo.Panel(w, "Puzzles Sign", "PUZZLES & ART", new Vector3(L.WingLeft + 0.03f, 5.5f, -8.3f), 270f, new Vector2(2.6f, 0.5f), s.Orange, new Color(0.99f, 0.97f, 0.93f));
            _count += 3;
        }

        /// <summary>The wing's plan (floor or roof): the part in front of the bay, the strip beside it, the quarter-ellipse bay.</summary>
        private static void WingPlan(Transform p, string name, float y0, float y1, RrRcSurface s, bool collider)
        {
            var c = L.CafeBayCenter;
            Slab(p, name, L.WingLeft - T, y0, L.WingFront - T, L.WingRight, y1, c.y, s, collider);
            Slab(p, name, c.x, y0, c.y, L.WingRight, y1, L.WingBack + T, s, collider);
            var bay = new List<Vector2> { c };
            bay.AddRange(EllipseArc(c, L.CafeBayRx + T, L.CafeBayRz + T, 90f, 180f, 16));
            RrRcGeo.Prism(p, name + " Bay", bay, y0, y1, s, collider);
            _count += 3;
        }

        private static void Chalkboard(Transform p, Vector3 c, string text)
        {
            RrRcGeo.Box(p, "Chalkboard Frame", c + Vector3.forward * 0.01f, Quaternion.identity, new Vector3(1.6f, 1.82f, 0.04f), _s.Timber, false);
            RrRcGeo.Box(p, "Chalkboard", c + Vector3.forward * 0.03f, Quaternion.identity, new Vector3(1.44f, 1.66f, 0.02f), _s.Chalk, false);
            var t = RrRcGeo.Label(p, "Chalkboard Text", text, c + Vector3.forward * 0.05f, 180f, 0.24f, 1.3f, new Color(0.95f, 0.95f, 0.9f), false);
            t.textWrappingMode = TMPro.TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(1.3f, 1.4f);
            _count += 3;
        }

        // ------------------------------------------------------------------ hallway

        private static void Hallway(Transform p)
        {
            var h = RrBuild.Group(p, "Hallway").transform;
            var bands = Bands(0f, _s.Blue, 1.2f, _s.Cream, 12f);
            float top = L.HallwayCeiling;
            Wall(h, "Hallway West Wall", false, L.HallwayX0, -1f, L.HallwayEnd - T, L.Entrance - T, 0f, top, bands);
            Wall(h, "Hallway East Wall", false, L.HallwayX1, 1f, L.HallwayEnd - T, L.Entrance - T, 0f, top, bands);
            Wall(h, "Hallway End Wall", true, L.HallwayEnd, -1f, L.HallwayX0 - T, L.HallwayX1 + T, 0f, top, bands);
            Slab(h, "Hallway Ceiling", L.HallwayX0 - T, top, L.HallwayEnd - T, L.HallwayX1 + T, top + 0.3f, L.Entrance - T, _s.Ceiling, false);
            RrRcGeo.Box(h, "Hallway Fluorescent", new Vector3(-0.65f, top - 0.03f, -18.11f), Quaternion.identity, new Vector3(0.35f, 0.05f, 1.25f), _s.Fluorescent, false);
            RrRcGeo.Box(h, "Hallway Fluorescent", new Vector3(-0.65f, top - 0.03f, -21.86f), Quaternion.identity, new Vector3(0.35f, 0.05f, 1.25f), _s.Fluorescent, false);
            if (_s.ExitSign != null)
            {
                RrRcGeo.Quad(h, "Exit Sign", new Vector3(-0.65f, 2.75f, L.HallwayEnd + 0.02f), Quaternion.Euler(0f, 180f, 0f), new Vector2(0.6f, 0.25f), _s.ExitSign);
            }

            _count += 3;
        }

        // ------------------------------------------------------------------ rugs, merch booth

        /// <summary>
        /// Rugs (taupe/navy carpets with the gold border RecCenter_CarpetBorder_Mat): the big round brown rug under the
        /// kiosk (the picture; the scene's Rug_Kiosk AABB is 8.6 x 11.0 m), the hallway runner (x -1.80..0.54,
        /// z -23.72..-16.44), the balcony's navy runner (x -12.68..-10.33, z -14.46..6.56), the gym rug in front of the
        /// SPORTS & PVP door, the upstairs runner to FEATURED ROOMS (x -22.80..-15.52, z -13.17..-10.82), and the
        /// Campus Jams green carpet in front of the Magic Door.
        /// </summary>
        private static void Rugs(Transform p)
        {
            var r = RrBuild.Group(p, "Rugs").transform;
            var k = new Vector2(L.Kiosk.x, L.Kiosk.z);
            RrRcGeo.Prism(r, "Kiosk Rug Border", RrRcGeo.Arc(k, 4.55f, 0f, 360f, 48), 0f, 0.008f, _s.Gold, false);
            RrRcGeo.Prism(r, "Kiosk Rug", RrRcGeo.Arc(k, 4.3f, 0f, 360f, 48), 0f, 0.016f, _s.CarpetBrown, false);
            Rug(r, "Hallway Rug", -1.80f, -23.72f, 0.54f, -16.44f, 0f, _s.CarpetTaupe);
            Rug(r, "Balcony Rug", -12.68f, -14.46f, -10.33f, 6.56f, L.BalconyY, _s.CarpetNavy);
            Rug(r, "Gym Rug", -9.17f, 21.71f, -5.71f, 24.75f, 0f, _s.CarpetNavy);
            Rug(r, "Upstairs Runner", -22.80f, -13.17f, -15.52f, -10.82f, L.BalconyY, _s.CarpetNavy);
            Rug(r, "Magic Door Carpet", -9.4f, 8.75f, -6.6f, 10.45f, 0f, _s.CarpetGreen);
            _count += 12;
        }

        private static void Rug(Transform p, string name, float x0, float z0, float x1, float z1, float y, RrRcSurface s)
        {
            RrRcGeo.Slab(p, name + " Border", new Vector3(x0 - 0.15f, y, z0 - 0.15f), new Vector3(x1 + 0.15f, y + 0.008f, z1 + 0.15f), _s.Gold, false);
            RrRcGeo.Slab(p, name, new Vector3(x0, y, z0), new Vector3(x1, y + 0.016f, z1), s, false);
        }

        /// <summary>
        /// The merch booth on the right past the stage ([MerchBooth] counter x 9.05..12.01, z -0.49..5.70, top 0.91):
        /// the picture's low blue counter with a light top and a small sign, shelves of merch on the wall behind it.
        /// Decorative: nothing is sold (the avatar is never changed).
        /// </summary>
        private static void MerchBooth(Transform p)
        {
            var m = RrBuild.Group(p, "Merch Booth").transform;
            var s = _s;
            Slab(m, "Merch Counter", 9.1f, 0f, -0.3f, 9.9f, 0.86f, 5.5f, s.Blue);
            Slab(m, "Merch Counter Top", 9.05f, 0.86f, -0.35f, 9.95f, 0.91f, 5.55f, s.LightWood);
            Slab(m, "Merch Side", 9.1f, 0f, -0.5f, L.HallRight, 0.91f, -0.3f, s.Blue);
            Slab(m, "Merch Side", 9.1f, 0f, 5.5f, L.HallRight, 0.91f, 5.7f, s.Blue);
            var items = new RrRcBatch(new RrRcSurface(RrLook.Solid(new Color(0.95f, 0.58f, 0.2f)), 1f, false));
            var items2 = new RrRcBatch(new RrRcSurface(RrLook.Solid(new Color(0.25f, 0.62f, 0.72f)), 1f, false));
            var items3 = new RrRcBatch(new RrRcSurface(RrLook.Solid(new Color(0.96f, 0.93f, 0.86f)), 1f, false));
            float[] ys = { 1.25f, 1.75f, 2.25f };
            for (int i = 0; i < ys.Length; i++)
            {
                _timber.Box(new Vector3(L.HallRight - 0.17f, ys[i], 2.6f), Quaternion.identity, new Vector3(0.34f, 0.05f, 5.0f));
                for (int k = 0; k < 9; k++)
                {
                    var b = (k + i) % 3 == 0 ? items : (k + i) % 3 == 1 ? items2 : items3;
                    b.Box(new Vector3(L.HallRight - 0.17f, ys[i] + 0.16f, 0.5f + k * 0.52f), Quaternion.identity, new Vector3(0.24f, 0.27f, 0.32f));
                }
            }

            items.Build(m, "Merch");
            items2.Build(m, "Merch");
            items3.Build(m, "Merch");
            RrRcGeo.Panel(m, "Merch Header", "REC ROOM SHOP", new Vector3(L.HallRight - 0.03f, 3.2f, 2.6f), 90f, new Vector2(3.6f, 0.62f), s.Orange, new Color(0.99f, 0.97f, 0.93f));
            RrRcGeo.Box(m, "Merch Sign", new Vector3(9.5f, 1.07f, 2.6f), Quaternion.identity, new Vector3(0.06f, 0.32f, 0.7f), s.Cream, false);
            RrRcGeo.Label(m, "Merch Sign Text", "MERCH", new Vector3(9.46f, 1.07f, 2.6f), 90f, 0.14f, 0.62f, new Color(0.2f, 0.36f, 0.62f));
            _count += 36;
        }

        // ------------------------------------------------------------------ lights

        /// <summary>
        /// Warm daylight (the scene's key light is a warm directional sun, its fills "Warm" (1, 0.95, 0.7) and "Cool"
        /// (0.79, 0.85, 1.0) points along the windows): point lights through the hall at 7.5 m, a white downlight under
        /// each skylight, fluorescent-white under the balcony and in the hallway, pendants in the cafe, the gym, the
        /// theatre and the upper floor, a pink glow at the marquee. No shadows (VR budget).
        /// </summary>
        private static void Lights(Transform p)
        {
            var l = RrBuild.Group(p, "Lights").transform;
            var warm = new Color(1f, 0.93f, 0.80f);
            var white = new Color(1f, 0.98f, 0.94f);
            var cool = new Color(0.79f, 0.85f, 1f);
            float[] hx = { -5.5f, 2.5f, 9.0f };
            float[] hz = { -11.5f, -4f, 3.5f };
            for (int i = 0; i < hx.Length; i++)
            {
                for (int j = 0; j < hz.Length; j++)
                {
                    RrBuild.PointLight(l, "Hall Light", new Vector3(hx[i], 7.5f, hz[j]), warm, 12f, 0.9f);
                }
            }

            RrBuild.PointLight(l, "Curve Light", new Vector3(5.0f, 5.5f, 12.5f), cool, 10f, 0.8f);
            RrBuild.PointLight(l, "Curve Light", new Vector3(-4.5f, 5.5f, 11.0f), warm, 10f, 0.8f);
            RrBuild.PointLight(l, "Skylight", new Vector3(0.1f, 8.2f, -6.5f), white, 10f, 1.1f);
            RrBuild.PointLight(l, "Skylight", new Vector3(0.1f, 8.2f, 7.5f), white, 10f, 1.1f);
            RrBuild.PointLight(l, "Under Balcony", new Vector3(-11.6f, 2.5f, -9f), white, 7f, 0.8f);
            RrBuild.PointLight(l, "Under Balcony", new Vector3(-11.6f, 2.5f, -1.4f), white, 7f, 0.8f);
            RrBuild.PointLight(l, "Under Balcony", new Vector3(-11.6f, 2.5f, 6f), white, 7f, 0.8f);
            RrBuild.PointLight(l, "Balcony", new Vector3(-11.6f, 6.5f, -10f), warm, 9f, 0.8f);
            RrBuild.PointLight(l, "Balcony", new Vector3(-11.6f, 6.5f, 4f), warm, 9f, 0.8f);
            RrBuild.PointLight(l, "Hallway", new Vector3(-0.65f, 2.8f, -18.1f), white, 6f, 0.9f);
            RrBuild.PointLight(l, "Hallway", new Vector3(-0.65f, 2.8f, -21.9f), white, 6f, 0.9f);
            RrBuild.PointLight(l, "Gym", new Vector3(-13.5f, 5.8f, 16.5f), warm, 10f, 0.9f);
            RrBuild.PointLight(l, "Gym", new Vector3(-6.0f, 5.8f, 16.5f), warm, 10f, 0.9f);
            RrBuild.PointLight(l, "Gym", new Vector3(-13.5f, 5.8f, 22.8f), warm, 10f, 0.9f);
            RrBuild.PointLight(l, "Gym", new Vector3(-6.0f, 5.8f, 22.8f), warm, 10f, 0.9f);
            RrBuild.PointLight(l, "Theatre", new Vector3(14.5f, 4.4f, -4.5f), warm, 8f, 0.7f);
            RrBuild.PointLight(l, "Theatre", new Vector3(19.5f, 4.4f, -4.5f), warm, 8f, 0.9f);
            RrBuild.PointLight(l, "Marquee Glow", new Vector3(10.2f, 6.2f, -5.3f), new Color(1f, 0.6f, 0.75f), 5f, 0.8f);
            RrBuild.PointLight(l, "Cafe", new Vector3(-19.4f, 2.5f, -6.2f), white, 6f, 0.8f);
            RrBuild.PointLight(l, "Cafe", new Vector3(-21f, 6.5f, 0f), warm, 10f, 0.8f);
            for (int i = 0; i < L.CafePendants.Length; i++)
            {
                RrBuild.PointLight(l, "Cafe Pendant", L.CafePendants[i] + Vector3.down * 1.3f, new Color(1f, 0.78f, 0.55f), 4f, 1.0f);
            }

            RrBuild.PointLight(l, "Upper Floor", new Vector3(-21.3f, 7.6f, -12f), white, 9f, 0.8f);
            RrBuild.PointLight(l, "Upper Floor", new Vector3(-17.0f, 7.6f, -8f), white, 9f, 0.8f);
            RrBuild.PointLight(l, "Merch", new Vector3(10f, 3.5f, 2.6f), warm, 6f, 0.7f);
        }

        // ------------------------------------------------------------------ helpers

        private static void Slab(Transform p, string name, float x0, float y0, float z0, float x1, float y1, float z1, RrRcSurface s, bool collider = true)
        {
            RrRcGeo.Slab(p, name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), s, collider);
        }
    }
}
