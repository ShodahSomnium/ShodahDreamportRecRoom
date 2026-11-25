using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// Rec Room's sound description, the struct its components use for every clip they play:
    /// audioClip, configureVolumeInDecibel, volume, pitchVariation, pitchShift, subtitles.
    /// Measured on the root beer bottle: uncork volume 0.75, pitch variation 0.2; swallow 0.5 / 0.15.
    /// Pitch shift and variation are read as semitone-free ratios around 1 by <see cref="RrSound"/>.
    /// </summary>
    public struct RrClipInfo
    {
        public RrTarget Clip;
        public bool VolumeInDecibel;
        public float Volume;
        public float PitchVariation;
        public float PitchShift;

        public bool HasClip
        {
            get { return !Clip.IsNull; }
        }
    }

    /// <summary>A keyframe of a serialized AnimationCurve.</summary>
    public struct RrKey
    {
        public float Time;
        public float Value;
        public float InSlope;
        public float OutSlope;
    }

    /// <summary>
    /// Typed reads over a component's serialized fields (as <see cref="RrSerializedFile.Read"/>
    /// returns them), with references resolved in the context of the prefab they came from.
    /// Missing fields give the default: Rec Room's classes change between versions, and a
    /// renamed field should cost a default, not a crash.
    /// </summary>
    public sealed class RrConfig
    {
        public readonly RrPrefabData Prefab;
        public readonly Dictionary<string, object> Fields;

        public RrConfig(RrPrefabData prefab, Dictionary<string, object> fields)
        {
            Prefab = prefab;
            Fields = fields ?? new Dictionary<string, object>();
        }

        public bool Has(string name)
        {
            return Fields.ContainsKey(name);
        }

        public object Raw(string name)
        {
            object v;
            return Fields.TryGetValue(name, out v) ? v : null;
        }

        public float Float(string name, float fallback = 0f)
        {
            return ToFloat(Raw(name), fallback);
        }

        public int Int(string name, int fallback = 0)
        {
            object v = Raw(name);
            if (v == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(v);
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public bool Bool(string name, bool fallback = false)
        {
            object v = Raw(name);
            if (v is bool)
            {
                return (bool)v;
            }

            if (v is byte)
            {
                return (byte)v != 0;
            }

            return v == null ? fallback : Int(name) != 0;
        }

        public string String(string name, string fallback = null)
        {
            return Raw(name) as string ?? fallback;
        }

        /// <summary>A Vector3f ({x,y,z}) as three floats.</summary>
        public float[] Vector3(string name)
        {
            return Vec(Raw(name) as Dictionary<string, object>, "x", "y", "z");
        }

        /// <summary>A Quaternionf ({x,y,z,w}).</summary>
        public float[] Quaternion(string name)
        {
            return Vec(Raw(name) as Dictionary<string, object>, "x", "y", "z", "w");
        }

        /// <summary>A ColorRGBA ({r,g,b,a}).</summary>
        public float[] Color(string name)
        {
            return Vec(Raw(name) as Dictionary<string, object>, "r", "g", "b", "a");
        }

        public RrConfig Sub(string name)
        {
            var d = Raw(name) as Dictionary<string, object>;
            return d == null ? null : new RrConfig(Prefab, d);
        }

        public List<RrConfig> List(string name)
        {
            var result = new List<RrConfig>();
            var l = Raw(name) as List<object>;
            if (l != null)
            {
                foreach (var item in l)
                {
                    var d = item as Dictionary<string, object>;
                    if (d != null)
                    {
                        result.Add(new RrConfig(Prefab, d));
                    }
                }
            }

            return result;
        }

        public List<float> Floats(string name)
        {
            var result = new List<float>();
            var l = Raw(name) as List<object>;
            if (l != null)
            {
                foreach (var item in l)
                {
                    result.Add(ToFloat(item, 0f));
                }
            }

            return result;
        }

        public RrTarget Ref(string name)
        {
            object v = Raw(name);
            if (v is RrPPtr)
            {
                return Prefab != null ? Prefab.Resolve((RrPPtr)v) : new RrTarget { PPtr = (RrPPtr)v };
            }

            return default(RrTarget);
        }

        public List<RrTarget> Refs(string name)
        {
            var result = new List<RrTarget>();
            var l = Raw(name) as List<object>;
            if (l != null)
            {
                foreach (var item in l)
                {
                    if (item is RrPPtr)
                    {
                        result.Add(Prefab != null ? Prefab.Resolve((RrPPtr)item) : new RrTarget { PPtr = (RrPPtr)item });
                    }
                }
            }

            return result;
        }

        /// <summary>One of Rec Room's clip structs.</summary>
        public RrClipInfo Clip(string name)
        {
            var d = Raw(name) as Dictionary<string, object>;
            return d == null ? default(RrClipInfo) : ClipOf(new RrConfig(Prefab, d));
        }

        public List<RrClipInfo> Clips(string name)
        {
            var result = new List<RrClipInfo>();
            foreach (var c in List(name))
            {
                var info = ClipOf(c);
                if (info.HasClip)
                {
                    result.Add(info);
                }
            }

            return result;
        }

        public static RrClipInfo ClipOf(RrConfig c)
        {
            return new RrClipInfo
            {
                Clip = c.Ref("audioClip"),
                VolumeInDecibel = c.Bool("configureVolumeInDecibel"),
                Volume = c.Float("volume", 1f),
                PitchVariation = c.Float("pitchVariation"),
                PitchShift = c.Float("pitchShift")
            };
        }

        /// <summary>An AnimationCurve's keys ({m_Curve: [{time, value, inSlope, outSlope, ...}]}).</summary>
        public List<RrKey> Curve(string name)
        {
            var result = new List<RrKey>();
            var d = Raw(name) as Dictionary<string, object>;
            var keys = d != null ? d["m_Curve"] as List<object> : null;
            if (keys == null)
            {
                return result;
            }

            foreach (var k in keys)
            {
                var kd = k as Dictionary<string, object>;
                if (kd == null)
                {
                    continue;
                }

                result.Add(new RrKey
                {
                    Time = ToFloat(Get(kd, "time"), 0f),
                    Value = ToFloat(Get(kd, "value"), 0f),
                    InSlope = ToFloat(Get(kd, "inSlope"), 0f),
                    OutSlope = ToFloat(Get(kd, "outSlope"), 0f)
                });
            }

            return result;
        }

        private static object Get(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }

        private static float[] Vec(Dictionary<string, object> d, params string[] keys)
        {
            var r = new float[keys.Length];
            if (d == null)
            {
                return r;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                r[i] = ToFloat(Get(d, keys[i]), 0f);
            }

            return r;
        }

        public static float ToFloat(object v, float fallback)
        {
            if (v == null)
            {
                return fallback;
            }

            if (v is float)
            {
                return (float)v;
            }

            try
            {
                return Convert.ToSingle(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
