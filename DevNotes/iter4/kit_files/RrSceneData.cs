using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// One scene's placement, parsed from "RRSK 1" text (the contract between the scene extractor and the kit, see
    /// DevNotes/iter4/kit.md): the mesh and material tables, renderers, whole install prefabs, stand-ins,
    /// colliders, lights, ambient audio, gameplay markers and environment, all in the scene's own Unity world
    /// space (Y up, not re-centred).
    ///
    /// <see cref="Parse"/> is pure C# plus Unity's plain structs (Vector3, Quaternion, Color, Bounds - no engine
    /// calls), so it runs on <see cref="RrMain.Worker"/>. It avoids per-token strings: numbers are read in place,
    /// only names are cut out. A 40k-record scene (Rec Royale's size) is ~3 MB of text; splitting it into tokens the
    /// usual way would allocate ~500k strings, and a garbage collection that big stops the main thread too.
    ///
    /// <b>Accepted forms.</b> Besides the full records of the contract, a transform may be shortened (what
    /// rrsk2cs.sh writes when it compacts): after the fixed fields, 3 numbers = position; 4 = position and a
    /// uniform scale; 6 = position and scale; 7 = position and rotation; 8 = position, rotation and uniform scale;
    /// 10 = the full form. The same applies to C box (centre, rotation, size) and C mesh. X accepts 9/10/13/14
    /// numbers (rotation and alpha optional). Unknown record letters are counted and skipped, so a later contract
    /// revision does not break an older kit. The text may use \n or \r\n.
    ///
    /// <b>What the extractor writes besides the contract</b> (DevNotes/refdata, 2026-09-24): names it could not
    /// match to the install carry a leading "?" (kept here as written; <see cref="RrSceneKit"/> skips them without a
    /// lookup), "#U ..." lines list renderers whose mesh bounds were unknown (comments here), and "E ambient" may be
    /// followed by mode=trilight intensity= equator=r,g,b ground=r,g,b (read into the Ambient* fields).
    /// Measured offline on the 50 refdata scenes (8.1M characters, 100k records, .NET 6): 45-66 ms for all of them,
    /// 10-17 ms for Rec Royale (1.85M characters), 21 MB allocated.
    /// </summary>
    public sealed class RrSceneData
    {
        public struct RendererRecord
        {
            public int Mesh;
            public int MaterialStart;   // into MaterialIndices
            public int MaterialCount;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        public struct PrefabRecord
        {
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        public struct StandInRecord
        {
            public bool Cylinder;
            public Vector3 Center;
            public Vector3 Extents;     // half extents
            public Quaternion Rotation;
            public Color Color;
        }

        public enum ColliderShape
        {
            Box,
            Mesh,
            Sphere,
            Capsule
        }

        public struct ColliderRecord
        {
            public ColliderShape Shape;
            public int Mesh;             // Mesh shape: mesh table index
            public Vector3 Center;       // Box/Sphere/Capsule centre, Mesh position
            public Quaternion Rotation;
            public Vector3 Size;         // Box: full size; Mesh: scale
            public float Radius;
            public float Height;
        }

        public struct LightRecord
        {
            public LightType Type;
            public Vector3 Position;
            public Quaternion Rotation;
            public Color Color;
            public float Intensity;
            public float Range;
            public float SpotAngle;
        }

        public struct AudioRecord
        {
            public string Clip;
            public Vector3 Position;
            public float Volume;
            public float MinDistance;
            public float MaxDistance;
            public bool Loop;
        }

        public struct MarkerRecord
        {
            public string Kind;
            public string Name;
            public Vector3 Position;
            public Quaternion Rotation;
            public string[] Keys;       // null when the record has no key=value pairs
            public string[] Values;
        }

        public string Name = string.Empty;
        public int Version;
        public bool HasBounds;
        public Bounds Bounds;

        /// <summary>Mesh and material tables: index -> install asset name (null where the text left a gap).</summary>
        public readonly List<string> Meshes = new List<string>();
        public readonly List<string> Materials = new List<string>();

        /// <summary>Material table indices of all renderers, in order (see RendererRecord.MaterialStart).</summary>
        public readonly List<int> MaterialIndices = new List<int>();

        public readonly List<RendererRecord> Renderers = new List<RendererRecord>();
        public readonly List<PrefabRecord> Prefabs = new List<PrefabRecord>();
        public readonly List<StandInRecord> StandIns = new List<StandInRecord>();
        public readonly List<ColliderRecord> Colliders = new List<ColliderRecord>();
        public readonly List<LightRecord> Lights = new List<LightRecord>();
        public readonly List<AudioRecord> Audio = new List<AudioRecord>();
        public readonly List<MarkerRecord> Markers = new List<MarkerRecord>();

        public bool HasFog;
        public string FogMode;
        public Color FogColor;
        public float FogDensity;
        public float FogStart;
        public float FogEnd;
        /// <summary>E ambient: the colour (the sky colour in trilight mode), and the optional mode/intensity/equator/ground values.</summary>
        public bool HasAmbient;
        public Color Ambient;
        public string AmbientMode;
        public float AmbientIntensity = 1f;
        public Color AmbientEquator;
        public Color AmbientGround;
        public string Sky;

        /// <summary>Lines read, records of unknown kinds, malformed lines, and the first few problems.</summary>
        public int Lines;
        public int Unknown;
        public int Bad;
        public readonly List<string> Problems = new List<string>();

        /// <summary>Characters of RRSK text parsed (after inflating a compressed payload).</summary>
        public int TextLength;

        public string Describe()
        {
            return "'" + Name + "' " + Lines + " lines: meshes " + Meshes.Count + ", materials " + Materials.Count + ", renderers " + Renderers.Count + ", prefabs " + Prefabs.Count +
                   ", stand-ins " + StandIns.Count + ", colliders " + Colliders.Count + ", lights " + Lights.Count + ", audio " + Audio.Count + ", markers " + Markers.Count +
                   (Unknown > 0 ? ", unknown " + Unknown : "") + (Bad > 0 ? ", bad " + Bad : "");
        }

        /// <summary>A mesh table name, or null.</summary>
        public string MeshName(int index)
        {
            return index >= 0 && index < Meshes.Count ? Meshes[index] : null;
        }

        public string MaterialName(int index)
        {
            return index >= 0 && index < Materials.Count ? Materials[index] : null;
        }

        // ---- parsing ----

        /// <summary>Parses RRSK text or an RRSZ1 payload (<see cref="RrSceneKitData.Decode"/>). Never throws for bad records.</summary>
        public static RrSceneData Parse(string text)
        {
            var d = new RrSceneData();
            string t = RrSceneKitData.Decode(text);
            d.TextLength = t.Length;
            var c = new Cursor(t);
            int lineStart = 0;
            int n = t.Length;
            bool computeBounds = true;
            var bounds = new Bounds();
            bool anyPoint = false;
            while (lineStart < n)
            {
                int lineEnd = t.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                {
                    lineEnd = n;
                }

                d.Lines++;
                c.Reset(lineStart, lineEnd);
                lineStart = lineEnd + 1;
                int ks, kl;
                if (!c.Token(out ks, out kl))
                {
                    continue;
                }

                char k = t[ks];
                if (k == '#')
                {
                    if (d.Version == 0)
                    {
                        d.Header(t, c, ks, kl);
                    }

                    continue;
                }

                if (kl != 1)
                {
                    d.Unknown++;
                    continue;
                }

                bool ok;
                Vector3 point;
                bool hasPoint;
                try
                {
                    ok = d.Record(k, t, c, out point, out hasPoint);
                }
                catch (Exception e)
                {
                    ok = false;
                    hasPoint = false;
                    point = Vector3.zero;
                    d.Problem("line " + d.Lines + ": " + e.GetType().Name);
                }

                if (!ok)
                {
                    d.Bad++;
                    if (d.Problems.Count < 8)
                    {
                        d.Problem("line " + d.Lines + ": " + Clip(t, ks, lineEnd));
                    }

                    continue;
                }

                if (k == 'B')
                {
                    computeBounds = false;
                }

                if (hasPoint && computeBounds)
                {
                    if (!anyPoint)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        anyPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!d.HasBounds && anyPoint)
            {
                d.Bounds = bounds;
                d.HasBounds = true;
            }

            return d;
        }

        private void Problem(string text)
        {
            if (Problems.Count < 8)
            {
                Problems.Add(text);
            }
        }

        private static string Clip(string t, int s, int e)
        {
            int len = Math.Min(e - s, 80);
            return t.Substring(s, len).TrimEnd('\r');
        }

        // "# RRSK 1 <scene name>"
        private void Header(string t, Cursor c, int ks, int kl)
        {
            int s, l;
            // "#RRSK" or "# RRSK"
            bool glued = kl > 1;
            if (glued)
            {
                if (!Same(t, ks + 1, kl - 1, "RRSK"))
                {
                    return;
                }
            }
            else if (!c.Token(out s, out l) || !Same(t, s, l, "RRSK"))
            {
                return;
            }

            if (!c.Token(out s, out l))
            {
                return;
            }

            Version = (int)Cursor.Number(t, s, l, 1f);
            Name = Decode(c.Rest());
        }

        private static bool Same(string t, int s, int l, string word)
        {
            return l == word.Length && string.CompareOrdinal(t, s, word, 0, l) == 0;
        }

        private readonly float[] _f = new float[24];

        private bool Record(char k, string t, Cursor c, out Vector3 point, out bool hasPoint)
        {
            point = Vector3.zero;
            hasPoint = false;
            int s, l, count;
            switch (k)
            {
                case 'B':
                    if (c.Floats(_f, 6) != 6)
                    {
                        return false;
                    }

                    var min = new Vector3(_f[0], _f[1], _f[2]);
                    var max = new Vector3(_f[3], _f[4], _f[5]);
                    Bounds = new Bounds((min + max) * 0.5f, Abs(max - min));
                    HasBounds = true;
                    return true;

                case 'M':
                case 'T':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    int index = (int)Cursor.Number(t, s, l, -1f);
                    string name = Decode(c.Rest());
                    if (index < 0 || index > 1000000 || name.Length == 0)
                    {
                        return false;
                    }

                    var table = k == 'M' ? Meshes : Materials;
                    while (table.Count <= index)
                    {
                        table.Add(null);
                    }

                    table[index] = name;
                    return true;
                }

                case 'R':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    int mesh = (int)Cursor.Number(t, s, l, -1f);
                    if (mesh < 0 || !c.Token(out s, out l))
                    {
                        return false;
                    }

                    var r = new RendererRecord { Mesh = mesh, MaterialStart = MaterialIndices.Count };
                    // "3" or "3,4,7"
                    int p = s, end = s + l;
                    while (p < end)
                    {
                        int q = t.IndexOf(',', p, end - p);
                        if (q < 0)
                        {
                            q = end;
                        }

                        if (q > p)
                        {
                            MaterialIndices.Add((int)Cursor.Number(t, p, q - p, -1f));
                            r.MaterialCount++;
                        }

                        p = q + 1;
                    }

                    count = c.Floats(_f, 10);
                    if (!Trs(count, 0, out r.Position, out r.Rotation, out r.Scale))
                    {
                        MaterialIndices.RemoveRange(r.MaterialStart, r.MaterialCount);
                        return false;
                    }

                    Renderers.Add(r);
                    point = r.Position;
                    hasPoint = true;
                    return true;
                }

                case 'P':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    var p = new PrefabRecord { Name = Decode(t.Substring(s, l)) };
                    count = c.Floats(_f, 10);
                    if (!Trs(count, 0, out p.Position, out p.Rotation, out p.Scale))
                    {
                        return false;
                    }

                    Prefabs.Add(p);
                    point = p.Position;
                    hasPoint = true;
                    return true;
                }

                case 'X':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    var x = new StandInRecord { Cylinder = l >= 3 && (t[s] == 'c' || t[s] == 'C') };
                    count = c.Floats(_f, 14);
                    x.Center = new Vector3(_f[0], _f[1], _f[2]);
                    x.Extents = Abs(new Vector3(_f[3], _f[4], _f[5]));
                    int ci;
                    if (count == 14 || count == 13)
                    {
                        x.Rotation = Rot(6);
                        ci = 10;
                    }
                    else if (count == 10 || count == 9)
                    {
                        x.Rotation = Quaternion.identity;
                        ci = 6;
                    }
                    else
                    {
                        return false;
                    }

                    x.Color = new Color(_f[ci], _f[ci + 1], _f[ci + 2], count == 14 || count == 10 ? _f[ci + 3] : 1f);
                    StandIns.Add(x);
                    point = x.Center;
                    hasPoint = true;
                    return true;
                }

                case 'C':
                    return Collider(t, c, out point, out hasPoint);

                case 'L':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    var lr = new LightRecord();
                    if (Same(t, s, l, "point"))
                    {
                        lr.Type = LightType.Point;
                    }
                    else if (Same(t, s, l, "spot"))
                    {
                        lr.Type = LightType.Spot;
                    }
                    else if (Same(t, s, l, "dir") || Same(t, s, l, "directional"))
                    {
                        lr.Type = LightType.Directional;
                    }
                    else
                    {
                        return false;
                    }

                    count = c.Floats(_f, 13);
                    if (count < 12)
                    {
                        return false;
                    }

                    lr.Position = new Vector3(_f[0], _f[1], _f[2]);
                    lr.Rotation = Rot(3);
                    lr.Color = new Color(_f[7], _f[8], _f[9], 1f);
                    lr.Intensity = _f[10];
                    lr.Range = _f[11];
                    lr.SpotAngle = count > 12 ? _f[12] : 30f;
                    Lights.Add(lr);
                    point = lr.Position;
                    hasPoint = lr.Type != LightType.Directional;
                    return true;
                }

                case 'A':
                {
                    if (!c.Token(out s, out l))
                    {
                        return false;
                    }

                    var a = new AudioRecord { Clip = Decode(t.Substring(s, l)) };
                    if (c.Floats(_f, 7) != 7)
                    {
                        return false;
                    }

                    a.Position = new Vector3(_f[0], _f[1], _f[2]);
                    a.Volume = _f[3];
                    a.MinDistance = _f[4];
                    a.MaxDistance = _f[5];
                    a.Loop = _f[6] > 0.5f;
                    Audio.Add(a);
                    point = a.Position;
                    hasPoint = true;
                    return true;
                }

                case 'G':
                    return Marker(t, c, out point, out hasPoint);

                case 'E':
                    return Environment(t, c);

                default:
                    Unknown++;
                    return true;
            }
        }

        private bool Collider(string t, Cursor c, out Vector3 point, out bool hasPoint)
        {
            point = Vector3.zero;
            hasPoint = false;
            int s, l, count;
            if (!c.Token(out s, out l))
            {
                return false;
            }

            var r = new ColliderRecord { Rotation = Quaternion.identity, Mesh = -1 };
            Vector3 scale;
            if (Same(t, s, l, "box"))
            {
                r.Shape = ColliderShape.Box;
                count = c.Floats(_f, 10);
                if (!Trs(count, 0, out r.Center, out r.Rotation, out scale))
                {
                    return false;
                }

                r.Size = Abs(scale);
            }
            else if (Same(t, s, l, "mesh"))
            {
                r.Shape = ColliderShape.Mesh;
                if (!c.Token(out s, out l))
                {
                    return false;
                }

                r.Mesh = (int)Cursor.Number(t, s, l, -1f);
                count = c.Floats(_f, 10);
                if (r.Mesh < 0 || !Trs(count, 0, out r.Center, out r.Rotation, out scale))
                {
                    return false;
                }

                r.Size = scale;
            }
            else if (Same(t, s, l, "sphere"))
            {
                r.Shape = ColliderShape.Sphere;
                if (c.Floats(_f, 4) != 4)
                {
                    return false;
                }

                r.Center = new Vector3(_f[0], _f[1], _f[2]);
                r.Radius = Mathf.Abs(_f[3]);
            }
            else if (Same(t, s, l, "capsule"))
            {
                r.Shape = ColliderShape.Capsule;
                count = c.Floats(_f, 9);
                if (count == 9)
                {
                    r.Center = new Vector3(_f[0], _f[1], _f[2]);
                    r.Rotation = Rot(3);
                    r.Radius = Mathf.Abs(_f[7]);
                    r.Height = Mathf.Abs(_f[8]);
                }
                else if (count == 5)
                {
                    r.Center = new Vector3(_f[0], _f[1], _f[2]);
                    r.Radius = Mathf.Abs(_f[3]);
                    r.Height = Mathf.Abs(_f[4]);
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            Colliders.Add(r);
            point = r.Center;
            hasPoint = true;
            return true;
        }

        private bool Marker(string t, Cursor c, out Vector3 point, out bool hasPoint)
        {
            point = Vector3.zero;
            hasPoint = false;
            int s, l;
            if (!c.Token(out s, out l))
            {
                return false;
            }

            var m = new MarkerRecord { Kind = Decode(t.Substring(s, l)), Rotation = Quaternion.identity };
            if (!c.Token(out s, out l))
            {
                return false;
            }

            m.Name = Decode(t.Substring(s, l));
            int count = c.Floats(_f, 7);
            if (count == 3)
            {
                m.Position = new Vector3(_f[0], _f[1], _f[2]);
            }
            else if (count == 7)
            {
                m.Position = new Vector3(_f[0], _f[1], _f[2]);
                m.Rotation = Rot(3);
            }
            else
            {
                return false;
            }

            // key=value pairs
            List<string> keys = null, values = null;
            while (c.Token(out s, out l))
            {
                int eq = t.IndexOf('=', s, l);
                if (keys == null)
                {
                    keys = new List<string>();
                    values = new List<string>();
                }

                if (eq < 0)
                {
                    keys.Add(Decode(t.Substring(s, l)));
                    values.Add(string.Empty);
                }
                else
                {
                    keys.Add(Decode(t.Substring(s, eq - s)));
                    values.Add(Decode(t.Substring(eq + 1, s + l - eq - 1)));
                }
            }

            if (keys != null)
            {
                m.Keys = keys.ToArray();
                m.Values = values.ToArray();
            }

            Markers.Add(m);
            point = m.Position;
            hasPoint = true;
            return true;
        }

        private bool Environment(string t, Cursor c)
        {
            int s, l;
            if (!c.Token(out s, out l))
            {
                return false;
            }

            if (Same(t, s, l, "fog"))
            {
                if (!c.Token(out s, out l))
                {
                    return false;
                }

                FogMode = t.Substring(s, l);
                int count = c.Floats(_f, 6);
                if (count < 3)
                {
                    return false;
                }

                FogColor = new Color(_f[0], _f[1], _f[2], 1f);
                FogDensity = count > 3 ? _f[3] : 0f;
                FogStart = count > 4 ? _f[4] : 0f;
                FogEnd = count > 5 ? _f[5] : 0f;
                HasFog = true;
                return true;
            }

            if (Same(t, s, l, "ambient"))
            {
                if (c.Floats(_f, 3) != 3)
                {
                    return false;
                }

                Ambient = new Color(_f[0], _f[1], _f[2], 1f);
                AmbientEquator = Ambient;
                AmbientGround = Ambient;
                AmbientMode = "flat";
                AmbientIntensity = 1f;
                HasAmbient = true;
                // mode=trilight intensity=1 equator=r,g,b ground=r,g,b (what the extractor writes after the colour)
                while (c.Token(out s, out l))
                {
                    int eq = t.IndexOf('=', s, l);
                    if (eq < 0)
                    {
                        continue;
                    }

                    int vs = eq + 1, ve = s + l;
                    if (Same(t, s, eq - s, "mode"))
                    {
                        AmbientMode = t.Substring(vs, ve - vs);
                    }
                    else if (Same(t, s, eq - s, "intensity"))
                    {
                        AmbientIntensity = Cursor.Number(t, vs, ve - vs, 1f);
                    }
                    else if (Same(t, s, eq - s, "equator"))
                    {
                        AmbientEquator = Rgb(t, vs, ve, Ambient);
                    }
                    else if (Same(t, s, eq - s, "ground"))
                    {
                        AmbientGround = Rgb(t, vs, ve, Ambient);
                    }
                }

                return true;
            }

            if (Same(t, s, l, "sky"))
            {
                Sky = Decode(c.Rest());
                return Sky.Length > 0;
            }

            Unknown++;
            return true;
        }

        // "r,g,b" between s and e.
        private static Color Rgb(string t, int s, int e, Color fallback)
        {
            var v = new float[3];
            int n = 0, p = s;
            while (p < e && n < 3)
            {
                int q = t.IndexOf(',', p, e - p);
                if (q < 0)
                {
                    q = e;
                }

                float f;
                if (!Cursor.TryNumber(t, p, q - p, out f))
                {
                    return fallback;
                }

                v[n++] = f;
                p = q + 1;
            }

            return n == 3 ? new Color(v[0], v[1], v[2], 1f) : fallback;
        }

        // Transform from `count` numbers starting at _f[at] (forms in the class summary).
        private bool Trs(int count, int at, out Vector3 p, out Quaternion q, out Vector3 s)
        {
            p = new Vector3(_f[at], _f[at + 1], _f[at + 2]);
            q = Quaternion.identity;
            s = Vector3.one;
            switch (count)
            {
                case 3:
                    return true;
                case 4:
                    s = Vector3.one * _f[at + 3];
                    return true;
                case 6:
                    s = new Vector3(_f[at + 3], _f[at + 4], _f[at + 5]);
                    return true;
                case 7:
                    q = Rot(at + 3);
                    return true;
                case 8:
                    q = Rot(at + 3);
                    s = Vector3.one * _f[at + 7];
                    return true;
                case 10:
                    q = Rot(at + 3);
                    s = new Vector3(_f[at + 7], _f[at + 8], _f[at + 9]);
                    return true;
                default:
                    return false;
            }
        }

        // A quaternion from four numbers, renormalised (4 decimals are not quite unit length; Unity warns on
        // non-unit rotations). Zero or NaN becomes identity.
        private Quaternion Rot(int at)
        {
            float x = _f[at], y = _f[at + 1], z = _f[at + 2], w = _f[at + 3];
            float m = x * x + y * y + z * z + w * w;
            if (!(m > 1e-8f) || float.IsInfinity(m))
            {
                return Quaternion.identity;
            }

            float inv = 1f / (float)Math.Sqrt(m);
            return new Quaternion(x * inv, y * inv, z * inv, w * inv);
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Math.Abs(v.x), Math.Abs(v.y), Math.Abs(v.z));
        }

        /// <summary>Decodes %XX escapes (the contract encodes spaces as %20); other text is kept as it is.</summary>
        public static string Decode(string s)
        {
            if (s == null || s.IndexOf('%') < 0)
            {
                return s ?? string.Empty;
            }

            var sb = new StringBuilder(s.Length);
            List<byte> bytes = null;
            for (int i = 0; i < s.Length; i++)
            {
                int hi, lo;
                if (s[i] == '%' && i + 2 < s.Length && (hi = Hex(s[i + 1])) >= 0 && (lo = Hex(s[i + 2])) >= 0)
                {
                    if (bytes == null)
                    {
                        bytes = new List<byte>();
                    }

                    bytes.Add((byte)(hi * 16 + lo));
                    i += 2;
                    continue;
                }

                if (bytes != null && bytes.Count > 0)
                {
                    sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));
                    bytes.Clear();
                }

                sb.Append(s[i]);
            }

            if (bytes != null && bytes.Count > 0)
            {
                sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));
            }

            return sb.ToString();
        }

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return -1;
        }

        /// <summary>Reads space-separated tokens of one line in place.</summary>
        private sealed class Cursor
        {
            private readonly string _t;
            private int _p;
            private int _end;

            public Cursor(string t)
            {
                _t = t;
            }

            public void Reset(int start, int end)
            {
                _p = start;
                _end = end;
                // \r\n line ends and trailing blanks
                while (_end > _p && (_t[_end - 1] == '\r' || _t[_end - 1] == ' ' || _t[_end - 1] == '\t'))
                {
                    _end--;
                }
            }

            public bool Token(out int start, out int length)
            {
                while (_p < _end && (_t[_p] == ' ' || _t[_p] == '\t'))
                {
                    _p++;
                }

                start = _p;
                while (_p < _end && _t[_p] != ' ' && _t[_p] != '\t')
                {
                    _p++;
                }

                length = _p - start;
                return length > 0;
            }

            /// <summary>The rest of the line, trimmed (names in M/T/E sky and the header).</summary>
            public string Rest()
            {
                while (_p < _end && (_t[_p] == ' ' || _t[_p] == '\t'))
                {
                    _p++;
                }

                string s = _p < _end ? _t.Substring(_p, _end - _p) : string.Empty;
                _p = _end;
                return s;
            }

            /// <summary>
            /// Reads up to max numbers into f; stops before the first token that is not a number (a marker's
            /// key=value) and returns how many were read.
            /// </summary>
            public int Floats(float[] f, int max)
            {
                int n = 0;
                while (n < max)
                {
                    int save = _p;
                    int s, l;
                    if (!Token(out s, out l))
                    {
                        break;
                    }

                    float v;
                    if (!TryNumber(_t, s, l, out v))
                    {
                        _p = save;
                        break;
                    }

                    f[n++] = v;
                }

                for (int i = n; i < max; i++)
                {
                    f[i] = 0f;
                }

                return n;
            }

            public static float Number(string t, int s, int l, float fallback)
            {
                float v;
                return TryNumber(t, s, l, out v) ? v : fallback;
            }

            private static readonly double[] Pow10 = { 1, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18 };

            // [-+]digits[.digits][e[-+]digits]; anything else (nan, inf, odd forms) through float.TryParse.
            public static bool TryNumber(string t, int s, int l, out float value)
            {
                int p = s, end = s + l;
                bool neg = false;
                if (p < end && (t[p] == '-' || t[p] == '+'))
                {
                    neg = t[p] == '-';
                    p++;
                }

                long mant = 0;
                int digits = 0, frac = 0;
                bool any = false;
                while (p < end && t[p] >= '0' && t[p] <= '9')
                {
                    if (digits < 18)
                    {
                        mant = mant * 10 + (t[p] - '0');
                        digits++;
                    }
                    else
                    {
                        frac--; // more integer digits than we keep: scale up instead
                    }

                    any = true;
                    p++;
                }

                if (p < end && t[p] == '.')
                {
                    p++;
                    while (p < end && t[p] >= '0' && t[p] <= '9')
                    {
                        if (digits < 18)
                        {
                            mant = mant * 10 + (t[p] - '0');
                            digits++;
                            frac++;
                        }

                        any = true;
                        p++;
                    }
                }

                int exp = 0;
                if (any && p < end && (t[p] == 'e' || t[p] == 'E'))
                {
                    p++;
                    bool eneg = false;
                    if (p < end && (t[p] == '-' || t[p] == '+'))
                    {
                        eneg = t[p] == '-';
                        p++;
                    }

                    int ed = 0;
                    while (p < end && t[p] >= '0' && t[p] <= '9')
                    {
                        exp = exp * 10 + (t[p] - '0');
                        ed++;
                        p++;
                    }

                    if (ed == 0)
                    {
                        any = false;
                    }

                    if (eneg)
                    {
                        exp = -exp;
                    }
                }

                if (!any || p != end)
                {
                    return float.TryParse(t.Substring(s, l), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                }

                int e10 = exp - frac;
                double d = mant;
                if (e10 < 0)
                {
                    d = -e10 < Pow10.Length ? d / Pow10[-e10] : d * Math.Pow(10, e10);
                }
                else if (e10 > 0)
                {
                    d = e10 < Pow10.Length ? d * Pow10[e10] : d * Math.Pow(10, e10);
                }

                value = (float)(neg ? -d : d);
                return true;
            }
        }
    }
}
