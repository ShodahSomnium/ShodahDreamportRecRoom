using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Harness hooks for <see cref="RrEnvironment"/> (scenarios env_*.json, "call" steps; nothing here runs in the
    /// client unless a scenario calls it). Before/after pairs: <see cref="TestOff"/> gives iteration 7's look (the
    /// world scene's own sun, ambient and sky, kit shadows off), <see cref="TestOn"/> Rec Room's.
    /// </summary>
    public static class RrEnvironmentTest
    {
        private const string Area = "EnvTest";

        public static void TestOff()
        {
            RrEnvironment.Suppress(true);
            int n = RrSceneKit.SetShadows(false);
            RrLog.Info(Area, "before: the world scene's own lighting, kit shadows off on " + n + " renderers; " + RrEnvironment.Report());
        }

        public static void TestOn()
        {
            RrEnvironment.Suppress(false);
            int n = RrSceneKit.SetShadows(true);
            RrLog.Info(Area, "after: Rec Room's lighting, kit shadows on on " + n + " renderers; " + RrEnvironment.Report());
        }

        public static void Report()
        {
            RrLog.Info(Area, RrEnvironment.Report());
        }

        public static void Auto()
        {
            RrEnvironment.ForcedKey = null;
            RrLog.Info(Area, "lighting follows the player again");
        }

        public static void UseRiver()
        {
            Use("Paintball_River");
        }

        public static void UseSpillway()
        {
            Use("Paintball_Spillway");
        }

        public static void UseLobby()
        {
            Use("Paintball_Lobby");
        }

        public static void UseGoldenLobby()
        {
            Use("GoldenLobby");
        }

        public static void UseArmory()
        {
            Use("GoldenTrophy_Armory");
        }

        public static void UseAct1()
        {
            Use("GTact1");
        }

        private static void Use(string key)
        {
            RrEnvironment.ForcedKey = key;
            RrEnvironment.Suppress(RrEnvironment.Suppressed);
            RrLog.Info(Area, "forced " + key + ": " + RrEnvironment.Report());
        }

        /// <summary>Logs every registered kit scene with its world centre (for placing harness shots).</summary>
        public static void Scenes()
        {
            RrLog.Info(Area, RrEnvironment.DescribeScenes());
        }

        public static IEnumerator ShotArmory()
        {
            return ShotScene("GoldenTrophy_Armory", "env_gt_armory");
        }

        public static IEnumerator ShotFoyer()
        {
            return ShotScene("GoldenTrophy_Foyer", "env_gt_foyer_inside");
        }

        /// <summary>The same camera as <see cref="ShotFoyer"/> without Rec Room's fog (to tell fog from lighting in the picture).</summary>
        public static IEnumerator ShotFoyerNoFog()
        {
            RrEnvironment.UseFog = false;
            var e = ShotScene("GoldenTrophy_Foyer", "env_gt_foyer_inside_nofog");
            while (e.MoveNext())
            {
                yield return e.Current;
            }

            RrEnvironment.UseFog = true;
            RrEnvironment.Suppress(false);
        }

        /// <summary>The same camera as <see cref="ShotFoyer"/> with iteration 7's lighting (the world scene's own, kit shadows off).</summary>
        public static IEnumerator ShotFoyerBefore()
        {
            return ShotScene("GoldenTrophy_Foyer", "env_gt_foyer_inside_before", true);
        }

        public static IEnumerator ShotHallway1()
        {
            return ShotScene("GoldenTrophy_Hallway1", "env_gt_hallway1");
        }

        public static IEnumerator ShotJumboFoyer()
        {
            return ShotScene("Jumbotron_Foyer", "env_jt_foyer");
        }

        public static IEnumerator ShotJumboArmory()
        {
            return ShotScene("Jumbotron_Armory", "env_jt_armory");
        }

        /// <summary>
        /// A post-processed shot inside a registered scene, lit as it would be with the player there (the key is forced
        /// for the shot): from the floor under its centre (raycast down from the centre, Environment layer) at 1.6 m, looking along its
        /// longer side. Written to the harness output folder (RR_OUT) as &lt;name&gt;.png.
        /// </summary>
        private static IEnumerator ShotScene(string key, string name, bool before = false)
        {
            Bounds b;
            if (!RrEnvironment.SceneWorldBounds(key, out b))
            {
                RrLog.Warn(Area, "shot " + name + ": no registered scene " + key + "; " + RrEnvironment.DescribeScenes());
                yield break;
            }

            string was = RrEnvironment.ForcedKey;
            RrEnvironment.ForcedKey = key;
            RrEnvironment.Suppress(before);
            RrSceneKit.SetShadows(!before);
            yield return null;
            yield return null;
            // From the centre down (from above the bounds the ray meets the roof first).
            RaycastHit hit;
            Vector3 eye = Physics.Raycast(b.center, Vector3.down, out hit, b.extents.y + 5f, 1 << RrLayers.Environment) ? hit.point + Vector3.up * 1.6f : b.center;
            Vector3 along = b.size.x >= b.size.z ? Vector3.right : Vector3.forward;
            Vector3 look = eye + along * 10f - Vector3.up * 0.3f;
            string outDir = System.Environment.GetEnvironmentVariable("RR_OUT");
            var go = new GameObject("Env Shot Camera");
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = 80f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 2000f;
                cam.allowHDR = true;
                cam.clearFlags = CameraClearFlags.Skybox;
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                data.renderPostProcessing = true;
                go.transform.position = eye;
                go.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);
                cam.targetTexture = rt;
                cam.Render();
                var resolved = RenderTexture.GetTemporary(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rt, resolved);
                var prev = RenderTexture.active;
                RenderTexture.active = resolved;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(resolved);
                cam.targetTexture = null;
                string file = string.IsNullOrEmpty(outDir) ? null : System.IO.Path.Combine(outDir, name + ".png");
                if (file != null)
                {
                    System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
                }

                RrLog.Info(Area, "shot " + name + " in " + key + " from " + eye.ToString("F1") + " toward " + look.ToString("F1") + (file != null ? " -> " + file : " (no RR_OUT: not written)") + "; " + RrEnvironment.Report());
            }
            finally
            {
                Object.Destroy(go);
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
                RrEnvironment.ForcedKey = was;
                if (before)
                {
                    RrEnvironment.Suppress(false);
                    RrSceneKit.SetShadows(true);
                }
            }
        }

        /// <summary>Rec Room's shader feature set (its global keywords): full, mobile, or neither (the default here).</summary>
        public static void FeatureFull()
        {
            Shader.DisableKeyword("_RENDER_FEATURESET_MOBILE");
            Shader.EnableKeyword("_RENDER_FEATURESET_FULL");
            RrLog.Info(Area, "global keyword _RENDER_FEATURESET_FULL on");
        }

        public static void FeatureMobile()
        {
            Shader.DisableKeyword("_RENDER_FEATURESET_FULL");
            Shader.EnableKeyword("_RENDER_FEATURESET_MOBILE");
            RrLog.Info(Area, "global keyword _RENDER_FEATURESET_MOBILE on");
        }

        public static void FeatureNone()
        {
            Shader.DisableKeyword("_RENDER_FEATURESET_FULL");
            Shader.DisableKeyword("_RENDER_FEATURESET_MOBILE");
            RrLog.Info(Area, "Rec Room feature-set keywords off");
        }

        /// <summary>
        /// How AG/Skybox/Simple Gradient spreads its six colours: renders the sky alone (culling mask 0) looking at
        /// elevations from -90 to 90 and logs the colour at the centre, first with pure test colours per property, then
        /// with a flat grey at exposure 1 and 0.43 (the exposure's scale), then Unity's procedural sky with Golden
        /// Trophy's parameters next to the gradient the table computed from them.
        /// </summary>
        public static IEnumerator SkyProbe()
        {
            float wait = Time.realtimeSinceStartup + 20f;
            Material gradient = null;
            while (gradient == null && Time.realtimeSinceStartup < wait)
            {
                var sky = RenderSettings.skybox;
                if (sky != null && sky.shader != null && sky.shader.name == RrEnvironmentSky.ShaderName)
                {
                    gradient = sky;
                }

                yield return null;
            }

            if (gradient == null)
            {
                RrLog.Warn(Area, "sky probe: no AG gradient sky is applied (" + RrEnvironment.Report() + ")");
                yield break;
            }

            var saved = RenderSettings.skybox;
            var test = new Material(gradient) { name = "sky probe" };
            RenderSettings.skybox = test;
            RrEnvironmentSky.Set(test, Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan, 1f, 1f, 0.03f);
            Log("primaries top=red mid=green horizon=blue groundHorizon=yellow groundMid=magenta groundTop=cyan", Sample());
            yield return null;
            var g = new Color(0.5f, 0.5f, 0.5f);
            RrEnvironmentSky.Set(test, g, g, g, g, g, g, 1f, 1f, 0.03f);
            Log("grey 0.5 exposure 1", Sample());
            RrEnvironmentSky.Set(test, g, g, g, g, g, g, 0.43f, 1f, 0.03f);
            Log("grey 0.5 exposure 0.43", Sample());
            RrEnvironmentSky.Set(test, Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan, 1f, 3f, 0.03f);
            Log("primaries power 3", Sample());
            Object.Destroy(test);

            var gt = RrEnvironment.Find("GoldenLobby");
            var shader = Shader.Find("Skybox/Procedural");
            if (gt != null && shader != null)
            {
                var p = new Material(shader);
                p.SetColor("_SkyTint", gt.ProcTint);
                p.SetColor("_GroundColor", gt.ProcGround);
                p.SetFloat("_AtmosphereThickness", gt.ProcThickness);
                p.SetFloat("_Exposure", 1f);
                p.EnableKeyword("_SUNDISK_NONE");
                RenderSettings.skybox = p;
                Log("procedural GoldenLobby params at exposure 1 (table gradient x100: " + Hex(gt.Gradient[0] * 100f) + " " + Hex(gt.Gradient[1] * 100f) + " " + Hex(gt.Gradient[2] * 100f) + ")", Sample());
                Object.Destroy(p);
            }

            RenderSettings.skybox = saved;
        }

        private static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        private static void Log(string what, string samples)
        {
            RrLog.Info(Area, "sky probe " + what + ": " + samples);
        }

        private static string Sample()
        {
            var go = new GameObject("Env Sky Probe Camera");
            var rt = new RenderTexture(16, 16, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(16, 16, TextureFormat.RGB24, false);
            var sb = new StringBuilder();
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.fieldOfView = 4f;
                cam.targetTexture = rt;
                for (int e = -90; e <= 90; e += 10)
                {
                    go.transform.rotation = Quaternion.Euler(-Mathf.Clamp(e, -89.5f, 89.5f), 30f, 0f);
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    var c = tex.GetPixel(8, 8);
                    sb.Append(e.ToString(CultureInfo.InvariantCulture)).Append(':').Append(ColorUtility.ToHtmlStringRGB(c)).Append(' ');
                }

                cam.targetTexture = null;
            }
            finally
            {
                Object.Destroy(go);
                rt.Release();
                Object.Destroy(rt);
                Object.Destroy(tex);
            }

            return sb.ToString();
        }
    }
}
