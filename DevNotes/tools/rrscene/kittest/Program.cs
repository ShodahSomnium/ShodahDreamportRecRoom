using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using SomniumSpace.Worlds.SomniumRecRoom;
using UnityEngine;

// Offline check of RrSceneData.Parse and RrSceneKitData.Decode against the refdata .rrsk files and the generated
// RrScene_* classes compiled into this exe (class name = RrScene_<file base><suffix char>).
static class Program
{
    static int Main(string[] args)
    {
        string dir = args[0];
        var types = new List<Type>();
        foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            if (t.Name.StartsWith("RrScene_")) types.Add(t);
        types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        double tOrig = 0, tGen = 0; long aOrig = 0, aGen = 0; int bad = 0, n = 0; long recs = 0;
        double worstPos = 0, worstRot = 0, worstScale = 0; string worstAt = "";
        double maxOrigMs = 0; string maxOrigAt = "";
        foreach (var t in types)
        {
            string file = Path.Combine(dir, t.Name.Substring(8, t.Name.Length - 9) + ".rrsk");
            if (!File.Exists(file)) { Console.WriteLine("no file for " + t.Name); continue; }
            string text = File.ReadAllText(file);
            string gen = (string)t.GetMethod("Text").Invoke(null, null);
            RrSceneData.Parse(text); RrSceneData.Parse(gen); // warm
            long a0 = GC.GetAllocatedBytesForCurrentThread(); var sw = Stopwatch.StartNew();
            var d0 = RrSceneData.Parse(text); double ms0 = sw.Elapsed.TotalMilliseconds; long a1 = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart(); var d1 = RrSceneData.Parse(gen); double ms1 = sw.Elapsed.TotalMilliseconds; long a2 = GC.GetAllocatedBytesForCurrentThread();
            tOrig += ms0; tGen += ms1; aOrig += a1 - a0; aGen += a2 - a1; n++;
            if (ms0 > maxOrigMs) { maxOrigMs = ms0; maxOrigAt = t.Name + " (" + text.Length + " chars)"; }
            recs += d0.Renderers.Count + d0.Colliders.Count + d0.StandIns.Count + d0.Markers.Count + d0.Prefabs.Count + d0.Lights.Count + d0.Audio.Count;
            string c0 = Counts(d0), c1 = Counts(d1);
            bool same = c0 == c1 && d0.Name == d1.Name;
            double p = 0, r = 0, s = 0;
            for (int i = 0; same && i < d0.Renderers.Count; i++) { Cmp(d0.Renderers[i].Position, d1.Renderers[i].Position, ref p); CmpQ(d0.Renderers[i].Rotation, d1.Renderers[i].Rotation, ref r); Cmp(d0.Renderers[i].Scale, d1.Renderers[i].Scale, ref s); }
            for (int i = 0; same && i < d0.Colliders.Count; i++) { Cmp(d0.Colliders[i].Center, d1.Colliders[i].Center, ref p); CmpQ(d0.Colliders[i].Rotation, d1.Colliders[i].Rotation, ref r); Cmp(d0.Colliders[i].Size, d1.Colliders[i].Size, ref s); }
            for (int i = 0; same && i < d0.StandIns.Count; i++) { Cmp(d0.StandIns[i].Center, d1.StandIns[i].Center, ref p); CmpQ(d0.StandIns[i].Rotation, d1.StandIns[i].Rotation, ref r); Cmp(d0.StandIns[i].Extents, d1.StandIns[i].Extents, ref s); }
            for (int i = 0; same && i < d0.Prefabs.Count; i++) { Cmp(d0.Prefabs[i].Position, d1.Prefabs[i].Position, ref p); CmpQ(d0.Prefabs[i].Rotation, d1.Prefabs[i].Rotation, ref r); Cmp(d0.Prefabs[i].Scale, d1.Prefabs[i].Scale, ref s); }
            for (int i = 0; same && i < d0.Markers.Count; i++)
            {
                Cmp(d0.Markers[i].Position, d1.Markers[i].Position, ref p); CmpQ(d0.Markers[i].Rotation, d1.Markers[i].Rotation, ref r);
                if ((d0.Markers[i].Keys == null ? 0 : d0.Markers[i].Keys.Length) != (d1.Markers[i].Keys == null ? 0 : d1.Markers[i].Keys.Length) || d0.Markers[i].Name != d1.Markers[i].Name) same = false;
            }
            for (int i = 0; same && i < d0.Meshes.Count; i++) if (d0.Meshes[i] != d1.Meshes[i]) same = false;
            if (!same) { bad++; Console.WriteLine("MISMATCH " + t.Name + "\n  " + c0 + "\n  " + c1); }
            if (p > worstPos) { worstPos = p; worstAt = t.Name; }
            worstRot = Math.Max(worstRot, r); worstScale = Math.Max(worstScale, s);
            if (d0.Bad > 0 || d0.Unknown > 0) Console.WriteLine(t.Name + ": source bad " + d0.Bad + " unknown " + d0.Unknown + " " + (d0.Problems.Count > 0 ? d0.Problems[0] : ""));
            if (args.Length > 1) Console.WriteLine(t.Name + " " + d0.Describe() + " | parse " + ms0.ToString("0.0") + " ms / gen " + ms1.ToString("0.0") + " ms, alloc " + ((a1 - a0) / 1024) + " KB / " + ((a2 - a1) / 1024) + " KB | " + Estimate(d0));
        }

        Console.WriteLine("scenes " + n + ", records " + recs + ", mismatches " + bad + "; parse source " + tOrig.ToString("0") + " ms (" + (aOrig >> 20) + " MB alloc), generated " + tGen.ToString("0") + " ms (" + (aGen >> 20) + " MB alloc incl. inflate); slowest source parse " + maxOrigMs.ToString("0.0") + " ms " + maxOrigAt);
        Console.WriteLine("max diff: position " + worstPos.ToString("0.#####") + " m (" + worstAt + "), rotation " + worstRot.ToString("0.#########") + " (1-|dot|), scale " + worstScale.ToString("0.#####"));

        // parser edge cases
        var e = RrSceneData.Parse("# RRSK 1 Edge%20Case\r\nM 0 My%20Mesh\r\nT 0 Mat\r\nR 0 0 1 2 3\r\nR 0 0,0 1 2 3 2\r\nR 0 0 1 2 3 1 -2 3\r\nR 0 0 1 2 3 0 0.7071 0 0.7071\r\nR 0 0 1 2 3 0 0.7071 0 0.7071 3\r\nR 0 0 1e1 -.5 +2.\r\nR 0 0 1 2\r\nZ 1 2 3\r\nG spawn p%201 1 2 3 team=0 note=a%20b flag\r\nC capsule 1 2 3 0.5 2\r\nX cyl 0 1 0 0.5 1 0.5 0.2 0.4 0.6\r\n#U something 1 2 3\r\nE ambient 0.1 0.2 0.3 mode=trilight intensity=1 equator=0.3,0.3,0.3 ground=0.1,0.1,0.1\r\n");
        Console.WriteLine("edge: " + e.Describe() + " name='" + e.Name + "' mesh0='" + e.Meshes[0] + "' r1.scale=" + e.Renderers[1].Scale + " mats=" + e.Renderers[1].MaterialCount + " r2.scale=" + e.Renderers[2].Scale + " r3.rot=" + e.Renderers[3].Rotation + " r4.scale=" + e.Renderers[4].Scale + " r5.pos=" + e.Renderers[5].Position + " marker=" + e.Markers[0].Name + " keys=" + string.Join("|", e.Markers[0].Keys) + " vals=" + string.Join("|", e.Markers[0].Values) + " ambient=" + e.Ambient + " " + e.AmbientMode + " eq=" + e.AmbientEquator + " problems=" + string.Join(" / ", e.Problems));
        return bad == 0 ? 0 : 1;
    }

    // What RrSceneKit's defaults would make of a scene, before the install is asked (every mesh taken as readable and
    // under 60k vertices per batch): merged renderer objects (material x 32 m cell, first material only), stand-in
    // meshes (colour in 31 steps x cell) and collider-carrying objects (box/sphere/capsule rotations to 1/1000,
    // 256 per object) plus one object per mesh collider.
    static string Estimate(RrSceneData d)
    {
        var rend = new HashSet<long>();
        foreach (var r in d.Renderers)
        {
            int m = r.MaterialCount > 0 ? d.MaterialIndices[r.MaterialStart] : -1;
            rend.Add(((long)(m + 1) << 24) | Cell(r.Position));
        }

        var stand = new HashSet<long>();
        foreach (var x in d.StandIns)
        {
            long c = (long)Math.Round(x.Color.r * 31) << 18 | (long)Math.Round(x.Color.g * 31) << 12 | (long)Math.Round(x.Color.b * 31) << 6 | (long)(x.Color.a < 0.99f ? 1 : 0);
            stand.Add(c << 24 | Cell(x.Center));
        }

        var rots = new Dictionary<long, int>();
        int meshCols = 0;
        foreach (var c in d.Colliders)
        {
            if (c.Shape == RrSceneData.ColliderShape.Mesh) { meshCols++; continue; }
            var q = c.Shape == RrSceneData.ColliderShape.Sphere ? Quaternion.identity : c.Rotation;
            if (q.w < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            long k = (R(q.x) << 48) | (R(q.y) << 32) | (R(q.z) << 16) | R(q.w);
            int n; rots.TryGetValue(k, out n); rots[k] = n + 1;
        }

        int hosts = 0;
        foreach (var kv in rots) hosts += (kv.Value + 255) / 256;
        return "estimate: " + d.Renderers.Count + " renderers -> " + rend.Count + " merged objects, " + d.StandIns.Count + " stand-ins -> " + stand.Count + " meshes, " +
               (d.Colliders.Count - meshCols) + " primitive colliders on " + hosts + " objects (" + rots.Count + " rotations) + " + meshCols + " mesh colliders";
    }

    static long R(float v) { return (long)(Math.Round(v * 1000f) + 32768) & 0xFFFF; }

    static long Cell(Vector3 p)
    {
        long x = (long)Math.Round(p.x / 32f) & 0xFF, y = (long)Math.Round(p.y / 32f) & 0xFF, z = (long)Math.Round(p.z / 32f) & 0xFF;
        return (x << 16) | (y << 8) | z;
    }

    static string Counts(RrSceneData d)
    {
        return d.Meshes.Count + "/" + d.Materials.Count + "/" + d.Renderers.Count + "/" + d.Prefabs.Count + "/" + d.StandIns.Count + "/" + d.Colliders.Count + "/" + d.Lights.Count + "/" + d.Audio.Count + "/" + d.Markers.Count + "/" + d.HasFog + "/" + d.HasAmbient + "/" + d.Sky;
    }

    static void Cmp(Vector3 a, Vector3 b, ref double m) { m = Math.Max(m, Math.Max(Math.Abs(a.x - b.x), Math.Max(Math.Abs(a.y - b.y), Math.Abs(a.z - b.z)))); }

    static void CmpQ(Quaternion a, Quaternion b, ref double m) { double dot = Math.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w); m = Math.Max(m, 1 - dot); }
}
