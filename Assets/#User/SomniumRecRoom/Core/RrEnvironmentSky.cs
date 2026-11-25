using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's own sky shader, AG/Skybox/Simple Gradient, as the install carries it: the one sky shader in the PC
    /// bundles (185 shaders indexed), used by Rec Room's ComfortModeSkybox material and by Stunt Runner's levels. Six
    /// colours from the zenith to the nadir, an exposure, a gradient power and a ground-to-sky transition width.
    ///
    /// Stunt Runner's own values (Android level92, Stuntrunner_Skybox) are the reference for the property meaning:
    /// top (0.458, 0.377, 0.661), mid (0.357, 0.336, 0.576), horizon (0.273, 0.273, 0.525) above the horizon and
    /// ground horizon (0.239, 0.239, 0.416), ground mid (0.154, 0.145, 0.251), ground "top" (0.163, 0.145, 0.251)
    /// below it, exposure 0.43, power 1, transition 0.03. So "GroundTop" is the far end of the ground half (the
    /// nadir), mirroring the sky half.
    ///
    /// Measured in the harness (RrEnvironmentTest.SkyProbe, iteration 8: pure test colours, the sky rendered alone at
    /// every 10 degrees): the ground-horizon colour sits AT the horizon and blends up into the sky-horizon colour,
    /// which is reached at +20 degrees; the mid colour at about +40, the top at +90; below the horizon the ground-mid
    /// colour at about -30 and the ground "top" at -90. Exposure scales the colour as authored (grey 0.5 at exposure
    /// 0.43 renders 0.2 sRGB); the gradient power made no visible difference at 1 and 3. The table's colours are the
    /// room's own sky averaged at those elevations (DevNotes/tools/apkscene envcs).
    /// </summary>
    public static class RrEnvironmentSky
    {
        /// <summary>The install material the runtime sky is copied from (its shader is AG/Skybox/Simple Gradient).</summary>
        public const string InstallMaterial = "ComfortModeSkybox";

        public const string ShaderName = "AG/Skybox/Simple Gradient";

        private static readonly int SkyTop = Shader.PropertyToID("_SkyTopColor");
        private static readonly int SkyMid = Shader.PropertyToID("_SkyMidColor");
        private static readonly int SkyHorizon = Shader.PropertyToID("_SkyHorizonColor");
        private static readonly int GroundHorizon = Shader.PropertyToID("_GroundHorizonColor");
        private static readonly int GroundMid = Shader.PropertyToID("_GroundMidColor");
        private static readonly int GroundTop = Shader.PropertyToID("_GroundTopColor");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int Power = Shader.PropertyToID("_GradientPow");
        private static readonly int Transition = Shader.PropertyToID("_GroundToSkyTransition");

        /// <summary>Writes a setting's six colours (sRGB, as a material inspector would) and its three numbers.</summary>
        public static void Set(Material m, RrEnvironmentSetting s)
        {
            if (m == null || s == null)
            {
                return;
            }

            Set(m, s.Gradient[0], s.Gradient[1], s.Gradient[2], s.Gradient[3], s.Gradient[4], s.Gradient[5], s.SkyExposure, s.SkyPower, s.SkyTransition);
        }

        public static void Set(Material m, Color top, Color mid, Color horizon, Color groundHorizon, Color groundMid, Color groundBottom, float exposure, float power, float transition)
        {
            m.SetColor(SkyTop, top);
            m.SetColor(SkyMid, mid);
            m.SetColor(SkyHorizon, horizon);
            m.SetColor(GroundHorizon, groundHorizon);
            m.SetColor(GroundMid, groundMid);
            m.SetColor(GroundTop, groundBottom);
            m.SetFloat(Exposure, exposure);
            m.SetFloat(Power, power);
            m.SetFloat(Transition, transition);
        }
    }
}
