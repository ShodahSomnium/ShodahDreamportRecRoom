using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's lighting for one scene, as the level had it: fog, ambient, reflection intensity, the key light (the
    /// sun), the sky and which of its lights cast shadows. Read from one line of <see cref="RrEnvironmentTable"/>
    /// (written by DevNotes/tools/apkscene envcs from the Android build's RenderSettings, lights and sky materials,
    /// DevNotes/refdata/apk/env/*.env), or made from a kit scene's own E and L records when the table has no line.
    ///
    /// Line format, space-separated key=value tokens (colours are sRGB as Unity's inspector shows them):
    /// <code>
    /// key=Paintball_Lobby src=apk_level113
    /// fog=linear,r,g,b,density,start,end        (linear|exp|exp2|off)
    /// amb=trilight,sky r,g,b,equator r,g,b,ground r,g,b,intensity
    /// refl=0.348                                 reflection intensity (reflc=1: of a baked environment cubemap, else of the sky)
    /// sun=qx,qy,qz,qw,r,g,b,intensity,soft,0.904 the key light's rotation in the scene's frame, colour, intensity,
    ///                                            shadows (none|hard|soft), shadow strength; absent = no sun
    /// sky=top/mid/horizon/groundHorizon/groundMid/groundBottom/exposure/power/transition   (each colour r,g,b)
    /// proc=tint r,g,b,a/ground r,g,b,a/thickness/exposure/sunDisk/sunSize/convergence     (Skybox/Procedural)
    /// lights=13                                  point and spot lights Rec Room ran in realtime
    /// shadow=x,y,z;x,y,z                         the ones with shadows (scene frame)
    /// rig=type,x,y,z,qx,qy,qz,qw,r,g,b,intensity,range,spot,shadow;...   every point/spot light (levels with 16 or fewer)
    /// urp=x,y,z,spec,hemi,hin,hout,ispot;...  iteration 9: Rec Room's URPLight per light (specular contribution, hemisphere
    ///                                            light 0/1 with inner/outer angles) and a spot's inner angle (-1: none), for
    ///                                            the lights that are not plain (spec 1, no hemisphere) or are spots (scene frame)
    /// </code>
    /// Measured facts behind it (DevNotes/iter8/env.md): none of the 61 Android levels has a lightmap and every light
    /// in them is realtime, so these numbers are the whole of Rec Room's lighting, not a baked-away remainder.
    /// </summary>
    public sealed class RrEnvironmentSetting
    {
        public string Key = "";
        public string Source = "";

        public bool Fog;
        public FogMode FogMode = FogMode.Linear;
        public Color FogColor = Color.gray;
        public float FogDensity = 0.01f;
        public float FogStart = 10f;
        public float FogEnd = 300f;

        public bool HasAmbient;
        public AmbientMode AmbientMode = AmbientMode.Trilight;
        public Color AmbientSky = Color.gray;
        public Color AmbientEquator = Color.gray;
        public Color AmbientGround = Color.gray;
        public float AmbientIntensity = 1f;

        public float Reflection = 1f;

        /// <summary>The level reflected a baked environment cubemap (custom reflection) rather than its sky.</summary>
        public bool ReflectionCustom;

        public bool HasSun;
        /// <summary>The key light's rotation in the scene's own frame (the kit root's rotation turns it into the world's).</summary>
        public Quaternion SunRotation = Quaternion.Euler(50f, -30f, 0f);
        public Color SunColor = Color.white;
        public float SunIntensity = 1f;
        public LightShadows SunShadows = LightShadows.Soft;
        public float SunShadowStrength = 1f;

        public bool HasGradient;
        /// <summary>top, mid, horizon, ground horizon, ground mid, ground bottom (sRGB).</summary>
        public readonly Color[] Gradient = new Color[6];
        public float SkyExposure = 1f;
        public float SkyPower = 1f;
        public float SkyTransition = 0.03f;

        public bool HasProcedural;
        public Color ProcTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        public Color ProcGround = new Color(0.369f, 0.349f, 0.341f, 1f);
        public float ProcThickness = 1f;
        public float ProcExposure = 1.3f;
        public int ProcSunDisk = 2;
        public float ProcSunSize = 0.04f;
        public float ProcConvergence = 5f;

        public int Lights;
        public readonly List<Vector3> ShadowLights = new List<Vector3>();

        /// <summary>One of the level's point or spot lights (scene frame), for a room the kit does not build (the Dorm).</summary>
        public struct RigLight
        {
            public LightType Type;
            public Vector3 Position;
            public Quaternion Rotation;
            public Color Color;
            public float Intensity, Range, SpotAngle;
            public bool Shadows;
        }

        /// <summary>The whole light rig, written for levels with at most 16 point/spot lights.</summary>
        public readonly List<RigLight> Rig = new List<RigLight>();

        /// <summary>Rec Room's URPLight values of one light, found by its position (iteration 9, "urp=").</summary>
        public struct UrpLight
        {
            public Vector3 Position;
            public float Specular;
            public bool Hemisphere;
            public float HemiInner, HemiOuter, InnerSpot;
        }

        public readonly List<UrpLight> Urp = new List<UrpLight>();

        /// <summary>The URPLight values of the light at <paramref name="p"/> (within 0.3 m, scene frame), if the table lists it.</summary>
        public bool FindUrp(Vector3 p, out UrpLight u)
        {
            u = default(UrpLight);
            float best = 0.09f;
            bool found = false;
            foreach (var x in Urp)
            {
                float d = (x.Position - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    u = x;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Filled from a kit scene's records rather than the table (fewer facts: no shadow data, no sky colours).</summary>
        public bool Derived;

        private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

        public static RrEnvironmentSetting Parse(string line)
        {
            var s = new RrEnvironmentSetting();
            if (string.IsNullOrEmpty(line))
            {
                return s;
            }

            foreach (var token in line.Split(' '))
            {
                int eq = token.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string k = token.Substring(0, eq), v = token.Substring(eq + 1);
                try
                {
                    switch (k)
                    {
                        case "key":
                            s.Key = v;
                            break;
                        case "src":
                            s.Source = v.Replace('_', ' ');
                            break;
                        case "fog":
                        {
                            var p = v.Split(',');
                            s.Fog = p[0] != "off";
                            s.FogMode = p[0] == "exp" ? FogMode.Exponential : p[0] == "exp2" ? FogMode.ExponentialSquared : FogMode.Linear;
                            s.FogColor = new Color(F(p[1]), F(p[2]), F(p[3]), 1f);
                            s.FogDensity = F(p[4]);
                            s.FogStart = F(p[5]);
                            s.FogEnd = F(p[6]);
                            break;
                        }
                        case "amb":
                        {
                            var p = v.Split(',');
                            s.HasAmbient = true;
                            s.AmbientMode = p[0] == "flat" ? AmbientMode.Flat : p[0] == "skybox" ? AmbientMode.Skybox : AmbientMode.Trilight;
                            s.AmbientSky = new Color(F(p[1]), F(p[2]), F(p[3]), 1f);
                            s.AmbientEquator = new Color(F(p[4]), F(p[5]), F(p[6]), 1f);
                            s.AmbientGround = new Color(F(p[7]), F(p[8]), F(p[9]), 1f);
                            s.AmbientIntensity = p.Length > 10 ? F(p[10]) : 1f;
                            break;
                        }
                        case "refl":
                            s.Reflection = F(v);
                            break;
                        case "reflc":
                            s.ReflectionCustom = v == "1";
                            break;
                        case "sun":
                        {
                            var p = v.Split(',');
                            s.HasSun = true;
                            s.SunRotation = Norm(new Quaternion(F(p[0]), F(p[1]), F(p[2]), F(p[3])));
                            s.SunColor = new Color(F(p[4]), F(p[5]), F(p[6]), 1f);
                            s.SunIntensity = F(p[7]);
                            s.SunShadows = p[8] == "none" ? LightShadows.None : p[8] == "hard" ? LightShadows.Hard : LightShadows.Soft;
                            s.SunShadowStrength = F(p[9]);
                            break;
                        }
                        case "sky":
                        {
                            var p = v.Split('/');
                            for (int i = 0; i < 6; i++)
                            {
                                s.Gradient[i] = Rgb(p[i]);
                            }

                            s.SkyExposure = F(p[6]);
                            s.SkyPower = F(p[7]);
                            s.SkyTransition = F(p[8]);
                            s.HasGradient = true;
                            break;
                        }
                        case "proc":
                        {
                            var p = v.Split('/');
                            s.ProcTint = Rgb(p[0]);
                            s.ProcGround = Rgb(p[1]);
                            s.ProcThickness = F(p[2]);
                            s.ProcExposure = F(p[3]);
                            s.ProcSunDisk = (int)F(p[4]);
                            s.ProcSunSize = F(p[5]);
                            s.ProcConvergence = F(p[6]);
                            s.HasProcedural = true;
                            break;
                        }
                        case "lights":
                            s.Lights = (int)F(v);
                            break;
                        case "rig":
                            foreach (var one in v.Split(';'))
                            {
                                var p = one.Split(',');
                                if (p.Length < 15)
                                {
                                    continue;
                                }

                                s.Rig.Add(new RigLight
                                {
                                    Type = p[0] == "spot" ? LightType.Spot : LightType.Point,
                                    Position = new Vector3(F(p[1]), F(p[2]), F(p[3])),
                                    Rotation = Norm(new Quaternion(F(p[4]), F(p[5]), F(p[6]), F(p[7]))),
                                    Color = new Color(F(p[8]), F(p[9]), F(p[10]), 1f),
                                    Intensity = F(p[11]),
                                    Range = F(p[12]),
                                    SpotAngle = F(p[13]),
                                    Shadows = p[14] == "1"
                                });
                            }

                            break;
                        case "urp":
                            foreach (var one in v.Split(';'))
                            {
                                var p = one.Split(',');
                                if (p.Length < 8)
                                {
                                    continue;
                                }

                                s.Urp.Add(new UrpLight
                                {
                                    Position = new Vector3(F(p[0]), F(p[1]), F(p[2])),
                                    Specular = F(p[3]),
                                    Hemisphere = p[4] == "1",
                                    HemiInner = F(p[5]),
                                    HemiOuter = F(p[6]),
                                    InnerSpot = F(p[7])
                                });
                            }

                            break;
                        case "shadow":
                            foreach (var xyz in v.Split(';'))
                            {
                                var p = xyz.Split(',');
                                if (p.Length == 3)
                                {
                                    s.ShadowLights.Add(new Vector3(F(p[0]), F(p[1]), F(p[2])));
                                }
                            }

                            break;
                    }
                }
                catch (Exception)
                {
                    // One bad token costs that token only (the table is generated; a hand-edited line may be short).
                }
            }

            return s;
        }

        /// <summary>
        /// What a kit scene's own records say when the table has no line for it: fog, ambient and the directional light
        /// (L dir) as the level had them; the sky from the ambient colours (sky, equator towards the horizon, ground).
        /// </summary>
        public static RrEnvironmentSetting FromScene(RrSceneData d)
        {
            var s = new RrEnvironmentSetting { Key = d.Name ?? "", Source = "scene records", Derived = true };
            if (d.HasFog)
            {
                s.Fog = !string.Equals(d.FogMode, "off", StringComparison.OrdinalIgnoreCase);
                s.FogMode = string.Equals(d.FogMode, "exp", StringComparison.OrdinalIgnoreCase) ? FogMode.Exponential : string.Equals(d.FogMode, "exp2", StringComparison.OrdinalIgnoreCase) ? FogMode.ExponentialSquared : FogMode.Linear;
                s.FogColor = d.FogColor;
                s.FogDensity = d.FogDensity;
                s.FogStart = d.FogStart;
                s.FogEnd = d.FogEnd;
            }

            if (d.HasAmbient)
            {
                s.HasAmbient = true;
                bool tri = string.Equals(d.AmbientMode, "trilight", StringComparison.OrdinalIgnoreCase);
                s.AmbientMode = tri ? AmbientMode.Trilight : AmbientMode.Flat;
                s.AmbientSky = d.Ambient;
                s.AmbientEquator = tri ? d.AmbientEquator : d.Ambient;
                s.AmbientGround = tri ? d.AmbientGround : d.Ambient;
                s.AmbientIntensity = d.AmbientIntensity;
                s.Gradient[0] = s.AmbientSky;
                s.Gradient[1] = Color.Lerp(s.AmbientSky, s.AmbientEquator, 0.5f);
                s.Gradient[2] = s.Fog ? s.FogColor : s.AmbientEquator;
                s.Gradient[3] = Color.Lerp(s.AmbientGround, s.Gradient[2], 0.35f);
                s.Gradient[4] = s.AmbientGround;
                s.Gradient[5] = s.AmbientGround;
                s.HasGradient = true;
            }

            if (d.Reflection >= 0f)
            {
                s.Reflection = d.Reflection;
            }

            float best = -1f;
            foreach (var l in d.Lights)
            {
                if (l.Type != LightType.Directional && l.Shadows)
                {
                    s.ShadowLights.Add(l.Position);
                }

                if (d.HasSun || d.NoSun)
                {
                    if (l.Type != LightType.Directional)
                    {
                        s.Lights++;
                    }

                    continue;
                }

                if (l.Type == LightType.Directional && l.Intensity > best)
                {
                    best = l.Intensity;
                    s.HasSun = true;
                    s.SunRotation = Norm(l.Rotation);
                    s.SunColor = l.Color;
                    s.SunIntensity = l.Intensity;
                    s.SunShadows = LightShadows.Soft;
                    // Rec Room's key lights cast soft shadows at 0.84-1.0 strength (Android levels, 13 measured).
                    s.SunShadowStrength = 0.9f;
                }
                else if (l.Type != LightType.Directional)
                {
                    s.Lights++;
                }
            }

            // "E sun" (apkscene, iteration 8): the key light as the level had it, shadow type and strength included.
            if (d.HasSun)
            {
                s.HasSun = true;
                s.SunRotation = Norm(d.SunRotation);
                s.SunColor = d.SunColor;
                s.SunIntensity = d.SunIntensity;
                s.SunShadows = d.SunShadows == "none" ? LightShadows.None : d.SunShadows == "hard" ? LightShadows.Hard : LightShadows.Soft;
                s.SunShadowStrength = d.SunStrength;
            }

            return s;
        }

        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Key).Append(" (").Append(Source).Append("): ");
            sb.Append(HasSun ? "sun " + Hex(SunColor) + " x" + SunIntensity.ToString("0.##", Ic) + " " + SunShadows + " shadows " + SunShadowStrength.ToString("0.##", Ic) + " at " + SunRotation.eulerAngles.ToString("F0") : "no sun");
            if (HasAmbient)
            {
                sb.Append(", ambient ").Append(AmbientMode).Append(' ').Append(Hex(AmbientSky)).Append('/').Append(Hex(AmbientEquator)).Append('/').Append(Hex(AmbientGround));
            }

            sb.Append(", fog ").Append(Fog ? FogMode + " " + Hex(FogColor) + " " + FogStart.ToString("0.#", Ic) + "-" + FogEnd.ToString("0.#", Ic) + " m" : "off");
            sb.Append(", reflections x").Append(Reflection.ToString("0.###", Ic));
            sb.Append(", sky ").Append(HasProcedural ? "procedural (exposure " + ProcExposure.ToString("0.##", Ic) + ")" : HasGradient ? "gradient " + Hex(Gradient[0]) + "/" + Hex(Gradient[2]) + "/" + Hex(Gradient[4]) : "none");
            sb.Append(", ").Append(Lights).Append(" realtime lights (").Append(ShadowLights.Count).Append(" with shadows)");
            return sb.ToString();
        }

        private static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        private static float F(string s)
        {
            return float.Parse(s, NumberStyles.Float, Ic);
        }

        private static Color Rgb(string s)
        {
            var p = s.Split(',');
            return new Color(F(p[0]), F(p[1]), F(p[2]), p.Length > 3 ? F(p[3]) : 1f);
        }

        private static Quaternion Norm(Quaternion q)
        {
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return n < 1e-6f ? Quaternion.identity : new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n);
        }
    }
}
