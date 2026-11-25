using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's own post-processing, room by room, as plain data: for each profile, the URP component and the
    /// parameters Rec Room overrode ("m_OverrideState: 1"), with their values. <see cref="RrPostFx"/> builds its runtime
    /// Volume profile from these.
    ///
    /// Where the numbers come from (read 2026-09-24, iteration 5; DevNotes/iter5/postfx.md):
    /// - The 16 <c>&lt;scene&gt;-PostProcessingProfile.asset</c> files of Rec Room's Studio copies of its official
    ///   levels (REF2 ".unity files"). Each is a URP VolumeProfile whose components are URP's stock ones (the m_Script
    ///   GUIDs are URP's own: 0b2db861 Bloom, 97c23e3b Tonemapping, 221518ef WhiteBalance, 66f335fb ColorAdjustments,
    ///   5485954d LiftGammaGain, cdfbdbb8 ChannelMixer). Every scene's Volume is global, priority 0, weight 1.
    ///   ChannelMixer is in every file but overrides nothing, so it is left out. Inactive components (RecRoyale's
    ///   LiftGammaGain, BurningBurg's SplitToning) are left out as well, because Rec Room did not apply them.
    /// - The user's download cache (approved for measurements only): <c>1AF.data</c>, the July 2025 Rec Center
    ///   (<c>RecCenter-RecCenter_071825-PostProcessingProfile</c>), and <c>35C.data</c>, Make It To Midnight.
    /// - The user's Android build (APK read as data only, iteration 10; DevNotes/iter10/jt_lighting.md findings A/D): the
    ///   global Volume of every Rise of Jumbotron level (Quest_Scifi_*, levels 38-49) and its two profiles,
    ///   <c>Scifi_Default_SSFX_Profile</c> (sharedassets38 #118, every room but the Armory) and
    ///   <c>Scifi_Armory_SSFX_Profile</c> (sharedassets39 #54). Only the numbers were read.
    /// - The install: <c>StuntRunner_Default_SSFX_Profile</c> (bundle 1a69c270, used by the Maker mood "Stunt Runner").
    ///   Only 2 VolumeProfiles exist in the install. The other one, the camera rig's <c>CameraRigProfile</c>, holds only
    ///   Rec Room's own ComfortEffects (the VR locomotion vignette), a type that does not exist outside Rec Room. The
    ///   rig's room volume (priority 1) has no profile in the bundle, because Rec Room plugs the room's profile in at run
    ///   time. The 8 Maker "Scene Moods" all carry the same post values (bloom threshold 1.04, intensity 0.3, diffusion 6;
    ///   temperature 5, exposure 0.1, contrast 10). They match the house profile below except for the bloom intensity.
    ///
    /// The house look: 8 of the 16 REF2 files are identical, and 14 share the same bloom, Neutral tonemapping,
    /// white balance and gamma/gain. Those 8 include the Golden Trophy lobby and "Testing", the blank Studio scene.
    /// The house look is: bloom threshold 1.04, intensity 0.15, scatter 0.6; Neutral tonemapping; temperature +5;
    /// exposure +0.1 EV; contrast 10; cool shadows (lift 0.92/0.94/1) and warm mid-tones and highlights (gamma
    /// 1/0.98/0.96, gain 1/0.99/0.96 +0.034). Every room that has no measured profile of its own (the Dorm and the
    /// games) uses <see cref="Default"/>, the blank Studio scene's profile.
    ///
    /// Not in the data: depth of field, motion blur, lens distortion, chromatic aberration, film grain and vignette.
    /// None of Rec Room's 19 measured profiles has any of them.
    /// </summary>
    public static class RrPostFxProfiles
    {
        /// <summary>One overridden parameter: component and parameter as Rec Room's YAML names them, value in x..w.</summary>
        public struct Value
        {
            public string Component;
            public string Parameter;

            /// <summary>Floats and enums in x (Tonemapping.mode 1 = Neutral), colours as rgba, vectors as xyzw.</summary>
            public Vector4 V;
        }

        /// <summary>One Rec Room VolumeProfile: its asset name, where it was read, and its overridden parameters.</summary>
        public sealed class Profile
        {
            public readonly string Name;
            public readonly string Source;
            public readonly List<Value> Values = new List<Value>();

            public Profile(string name, string source)
            {
                Name = name;
                Source = source;
            }

            public Profile Set(string component, string parameter, float x, float y = 0f, float z = 0f, float w = 0f)
            {
                Values.Add(new Value { Component = component, Parameter = parameter, V = new Vector4(x, y, z, w) });
                return this;
            }

            /// <summary>The value Rec Room set, if it overrode this parameter.</summary>
            public bool TryGet(string component, string parameter, out Vector4 v)
            {
                for (int i = 0; i < Values.Count; i++)
                {
                    if (Values[i].Component == component && Values[i].Parameter == parameter)
                    {
                        v = Values[i].V;
                        return true;
                    }
                }

                v = Vector4.zero;
                return false;
            }

            /// <summary>For the log: "Bloom threshold 1.04 intensity 0.15 ...; Tonemapping mode 1; ...".</summary>
            public string Describe()
            {
                var sb = new StringBuilder();
                string last = null;
                foreach (var v in Values)
                {
                    if (v.Component != last)
                    {
                        if (last != null)
                        {
                            sb.Append("; ");
                        }

                        sb.Append(v.Component);
                        last = v.Component;
                    }

                    sb.Append(' ').Append(v.Parameter).Append(' ');
                    bool vector = v.Parameter == "lift" || v.Parameter == "gamma" || v.Parameter == "gain" || v.Parameter == "colorFilter" || v.Parameter == "tint" && v.Component == "Bloom";
                    if (vector)
                    {
                        sb.Append('(').Append(F(v.V.x)).Append(',').Append(F(v.V.y)).Append(',').Append(F(v.V.z)).Append(',').Append(F(v.V.w)).Append(')');
                    }
                    else
                    {
                        sb.Append(F(v.V.x));
                    }
                }

                return sb.ToString();
            }

            private static string F(float f)
            {
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            }
        }

        private const string Ref2 = "REF2 .unity files/APPPLESARCHIVE/APPPLESARCHIVE-";

        // Values shared by the Studio-port quest scenes (gamma and gain are the same in 13 of the 14 files).
        private const float GammaG = 0.98246425f, GammaB = 0.9597683f;
        private const float GainG = 0.9898097f, GainB = 0.9557981f, GainW = 0.033898305f;

        private static Profile Studio(string scene, float liftR, float liftG, float exposure, float contrast)
        {
            var p = new Profile("APPPLESARCHIVE-" + scene + "-PostProcessingProfile", Ref2 + scene + "-PostProcessingProfile.asset")
                .Set("Bloom", "threshold", 1.04f).Set("Bloom", "intensity", 0.15f).Set("Bloom", "scatter", 0.6f).Set("Bloom", "highQualityFiltering", 0f)
                .Set("Tonemapping", "mode", 1f)
                .Set("WhiteBalance", "temperature", 5f);
            if (exposure != 0f)
            {
                p.Set("ColorAdjustments", "postExposure", exposure);
            }

            if (contrast != 0f)
            {
                p.Set("ColorAdjustments", "contrast", contrast);
            }

            return p.Set("LiftGammaGain", "lift", liftR, liftG, 1f, 0f)
                .Set("LiftGammaGain", "gamma", 1f, GammaG, GammaB, 0f)
                .Set("LiftGammaGain", "gain", 1f, GainG, GainB, GainW);
        }

        private const float HouseLiftR = 0.9215543f, HouseLiftG = 0.9418663f;

        // ---- REF2: Rec Room's Studio copies of its official levels (all 16 files) ----

        /// <summary>Golden Trophy lobby: the house profile.</summary>
        public static readonly Profile GoldenLobby = Studio("GoldenLobby", HouseLiftR, HouseLiftG, 0.1f, 10f);

        /// <summary>The blank Studio scene: the house profile (8 of the 16 files hold exactly these values).</summary>
        public static readonly Profile Testing = Studio("Testing", HouseLiftR, HouseLiftG, 0.1f, 10f);

        public static readonly Profile CastleGrounds = Studio("CastleGrounds", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile GibbetJungle = Studio("GibbetJungle", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile GrandHall4 = Studio("GrandHall4", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile JumbotronBoss = Studio("JumbotronBoss", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile PortPlunder = Studio("PortPlunder", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile StuntLobby = Studio("StuntLobby", HouseLiftR, HouseLiftG, 0.1f, 10f);
        public static readonly Profile ConcertHall = Studio("ConcertHall", 0.94699204f, 0.9625681f, 0.1f, 10f);
        public static readonly Profile IsleLobby = Studio("IsleLobby", 0.95928746f, 0.9656327f, 0.1f, 10f);
        public static readonly Profile PiratesBanquet = Studio("PiratesBanquet", 0.9510367f, 0.95993674f, 0.1f, 10f);

        /// <summary>Crescendo of Dracula lobby: brighter (+0.2 EV), a 3 degree hue shift and a bluer lift.</summary>
        public static readonly Profile CrescendoLobby = Studio("CrescendoLobby", 0.8648265f, 0.92987067f, 0.2f, 10f)
            .Set("ColorAdjustments", "hueShift", 3f);

        /// <summary>Crescendo act 2 (the carriage ride): +0.3 EV and no gamma override (Rec Room left gamma at 1).</summary>
        public static readonly Profile ForestRoad = new Profile("APPPLESARCHIVE-ForestRoad-PostProcessingProfile", Ref2 + "ForestRoad-PostProcessingProfile.asset")
            .Set("Bloom", "threshold", 1.04f).Set("Bloom", "intensity", 0.15f).Set("Bloom", "scatter", 0.6f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", 5f)
            .Set("ColorAdjustments", "postExposure", 0.3f).Set("ColorAdjustments", "contrast", 10f)
            .Set("LiftGammaGain", "lift", HouseLiftR, HouseLiftG, 1f, 0f)
            .Set("LiftGammaGain", "gain", 1f, GainG, GainB, GainW);

        /// <summary>Crescendo act 1 (the burning town): saturation +9 instead of exposure and contrast.</summary>
        public static readonly Profile BurningBurg = Studio("BurningBurg", 0.9378788f, 0.9653842f, 0f, 0f)
            .Set("ColorAdjustments", "saturation", 9f);

        /// <summary>Rec Royale: stronger, tighter bloom, +0.38 EV, more contrast, a cool green tint; its LiftGammaGain is inactive.</summary>
        public static readonly Profile RecRoyale = new Profile("RecRoyale__PVP-RecRoyale-PostProcessingProfile", "REF2 .unity files/RecRoyale__PVP/RecRoyale__PVP-RecRoyale-PostProcessingProfile.asset")
            .Set("Bloom", "threshold", 1.91f).Set("Bloom", "intensity", 0.22f).Set("Bloom", "scatter", 0.32f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", 5.5f).Set("WhiteBalance", "tint", -13.3f)
            .Set("ColorAdjustments", "postExposure", 0.38f).Set("ColorAdjustments", "contrast", 13.44f).Set("ColorAdjustments", "hueShift", -5f).Set("ColorAdjustments", "saturation", 1.8f);

        /// <summary>Run The Block (a community room in REF2): almost no bloom, cool white balance.</summary>
        public static readonly Profile TheBlock = new Profile("RunTheBlock-TheBlock-PostProcessingProfile", "REF2 .unity files/RunTheBlock/RunTheBlock-TheBlock-PostProcessingProfile.asset")
            .Set("Bloom", "threshold", 9.2f).Set("Bloom", "intensity", 0.01f).Set("Bloom", "scatter", 0.853f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", -6f).Set("WhiteBalance", "tint", 0f)
            .Set("ColorAdjustments", "postExposure", 0.1f).Set("ColorAdjustments", "contrast", 10f).Set("ColorAdjustments", "saturation", 10f)
            .Set("LiftGammaGain", "lift", HouseLiftR, HouseLiftG, 1f, 0f)
            .Set("LiftGammaGain", "gamma", 1f, GammaG, GammaB, 0f)
            .Set("LiftGammaGain", "gain", 1f, GainG, GainB, GainW);

        // ---- the user's download cache (measurements only) ----

        /// <summary>
        /// The July 2025 Rec Center (cache 1AF, RecCenter_071825): brighter than the quests (+0.2 EV), half their
        /// contrast (5), saturation +8, bloom 0.25 from 1.1, and a much warmer gamma (blue 0.868): the warm hall.
        /// </summary>
        public static readonly Profile RecCenter = new Profile("RecCenter-RecCenter_071825-PostProcessingProfile", "cache 1AF.data (RecCenter_071825, measurements only)")
            .Set("Bloom", "threshold", 1.1f).Set("Bloom", "intensity", 0.25f).Set("Bloom", "scatter", 0.617f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", 5f)
            .Set("ColorAdjustments", "postExposure", 0.2f).Set("ColorAdjustments", "contrast", 5f).Set("ColorAdjustments", "saturation", 8f)
            .Set("LiftGammaGain", "lift", 0.88991f, 0.950062f, 1f, 0f)
            .Set("LiftGammaGain", "gamma", 1f, 0.924874f, 0.867988f, 0f)
            .Set("LiftGammaGain", "gain", 1f, 0.988927f, 0.940286f, 0.0338983f);

        /// <summary>Make It To Midnight (cache 35C): strong bloom (1.4 from 0.98) for the neon town, warm gain +0.106.</summary>
        public static readonly Profile Midnight = new Profile("MakeItToMidnight-Home-PostProcessingProfile", "cache 35C.data (MakeItToMidnight-Home, measurements only)")
            .Set("Bloom", "threshold", 0.98f).Set("Bloom", "intensity", 1.4f).Set("Bloom", "scatter", 0.64f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("ColorAdjustments", "postExposure", 0.1f).Set("ColorAdjustments", "contrast", 5f).Set("ColorAdjustments", "saturation", -2f)
            .Set("LiftGammaGain", "lift", 0.930508f, 0.942829f, 1f, 0f)
            .Set("LiftGammaGain", "gamma", 1f, 0.995392f, 0.944616f, 0f)
            .Set("LiftGammaGain", "gain", 1f, 0.916038f, 0.883374f, 0.105932f);

        // ---- the install ----

        /// <summary>The install's Stunt Runner profile (bundle 1a69c270, path 7229030070958564233); also loadable as is (RrPostFx.TestBundleProfile).</summary>
        public static readonly Profile StuntRunnerSsfx = new Profile("StuntRunner_Default_SSFX_Profile", "install bundle 1a69c270f22345b84a4334a6d1fed7b4")
            .Set("Bloom", "threshold", 1.1f).Set("Bloom", "intensity", 0.5f).Set("Bloom", "scatter", 0.617f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", 2.8f)
            .Set("ColorAdjustments", "postExposure", 0.21f).Set("ColorAdjustments", "contrast", 2.1f)
            .Set("LiftGammaGain", "lift", 0.934006f, 0.966924f, 1f, -0.005f)
            .Set("LiftGammaGain", "gamma", 1f, 0.981122f, 0.976335f, -0.0393701f)
            .Set("LiftGammaGain", "gain", 1f, 0.976032f, 0.952409f, 0.0458529f);

        // ---- the user's Android build (measurements only) ----

        /// <summary>
        /// Bloom clamp for the two Jumbotron profiles. This is our value, not Rec Room's (Rec Room left URP's 65472).
        /// Those rooms have "Hotspot" lights of intensity 20-90 within 0.5 m, so a few pixels reach HDR 20-45. At the
        /// headset's quarter-resolution bloom, those pixels flare and shimmer as the head moves. 16 is above the brightest
        /// glow strip (SciFi_Emit_Cyan, 10) and leaves every emissive material as Rec Room drew it.
        /// </summary>
        public const float ScifiBloomClamp = 16f;

        /// <summary>
        /// The Rise of Jumbotron, every room but the Armory (APK level38 Volume -> sharedassets38 #118; levels 40-49 use the
        /// same profile). Compared with the house look: ACES instead of Neutral, +0.5 EV, contrast -14, 3.3x the bloom
        /// (0.5 from 1.1, scatter 0.617), a slightly blue lift in the shadows and a warm-neutral gain (+0.126).
        /// </summary>
        public static readonly Profile ScifiDefault = new Profile("Scifi_Default_SSFX_Profile", "APK sharedassets38.assets #118 (Quest_Scifi levels 38, 40-49; measurements only)")
            .Set("Bloom", "threshold", 1.1f).Set("Bloom", "intensity", 0.5f).Set("Bloom", "scatter", 0.617f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Bloom", "clamp", ScifiBloomClamp)
            .Set("Tonemapping", "mode", 2f)
            .Set("WhiteBalance", "temperature", 0f)
            .Set("ColorAdjustments", "postExposure", 0.5f).Set("ColorAdjustments", "contrast", -14f)
            .Set("LiftGammaGain", "lift", 0.8929f, 0.9395f, 1f, 0f)
            .Set("LiftGammaGain", "gamma", 1f, 0.9814f, 0.9961f, 0f)
            .Set("LiftGammaGain", "gain", 1f, 0.9311f, 0.9386f, 0.1264f);

        /// <summary>
        /// The Jumbotron's Armory (APK level39 Volume -> sharedassets39 #54): Neutral tonemapping, no exposure offset,
        /// contrast +48.3, a stronger and wider bloom (0.7 from 1.1, scatter 0.844), and a less blue lift. Gamma and gain
        /// are the same as in <see cref="ScifiDefault"/>.
        /// </summary>
        public static readonly Profile ScifiArmory = new Profile("Scifi_Armory_SSFX_Profile", "APK sharedassets39.assets #54 (Quest_Scifi_Armory, level 39; measurements only)")
            .Set("Bloom", "threshold", 1.1f).Set("Bloom", "intensity", 0.7f).Set("Bloom", "scatter", 0.844f).Set("Bloom", "highQualityFiltering", 0f)
            .Set("Bloom", "clamp", ScifiBloomClamp)
            .Set("Tonemapping", "mode", 1f)
            .Set("WhiteBalance", "temperature", 0f)
            .Set("ColorAdjustments", "postExposure", 0f).Set("ColorAdjustments", "contrast", 48.3f)
            .Set("LiftGammaGain", "lift", 0.9087f, 0.97f, 1f, 0f)
            .Set("LiftGammaGain", "gamma", 1f, 0.9814f, 0.9961f, 0f)
            .Set("LiftGammaGain", "gain", 1f, 0.9311f, 0.9386f, 0.1264f);

        /// <summary>Rec Room's default room look: the blank Studio scene's profile (the house values).</summary>
        public static readonly Profile Default = Testing;

        /// <summary>Every profile above, for lookups by Rec Room's asset name.</summary>
        public static readonly Profile[] All =
        {
            GoldenLobby, Testing, CastleGrounds, GibbetJungle, GrandHall4, JumbotronBoss, PortPlunder, StuntLobby, ConcertHall, IsleLobby,
            PiratesBanquet, CrescendoLobby, ForestRoad, BurningBurg, RecRoyale, TheBlock, RecCenter, Midnight, StuntRunnerSsfx,
            ScifiDefault, ScifiArmory
        };

        // (activity, area) -> profile. Area 0 is the room itself (a quest's lobby); area n is act n.
        private static readonly Dictionary<int, Profile> Table = new Dictionary<int, Profile>();

        static RrPostFxProfiles()
        {
            Map(RrActivity.Dorm, 0, Default);
            Map(RrActivity.RecCenter, 0, RecCenter);

            // Golden Trophy: the lobby's profile is exported. The six act scenes (GTact1..6) point at profiles that are
            // not in REF2 (GUIDs f0120f8c...5c99a2c6 and so on). The Studio port gave 14 of its 16 exported quest scenes
            // the house values, the lobby included, so acts 1-6 use the lobby's values. So do our research-built acts 7-9.
            for (int act = 0; act <= 9; act++)
            {
                Map(RrActivity.GoldenTrophy, act, GoldenLobby);
            }

            // Crescendo of Dracula: lobby, act 1 the burning town, act 2 the carriage ride, act 3 the castle entry
            // (CastleGroundsDay's own profile is not exported; CastleGrounds' is), the grand hall acts, act 13 Dracula.
            Map(RrActivity.Crescendo, 0, CrescendoLobby);
            Map(RrActivity.Crescendo, 1, BurningBurg);
            Map(RrActivity.Crescendo, 2, ForestRoad);
            Map(RrActivity.Crescendo, 3, CastleGrounds);
            Map(RrActivity.Crescendo, 4, GrandHall4);
            Map(RrActivity.Crescendo, 7, GrandHall4);
            Map(RrActivity.Crescendo, 10, GrandHall4);
            Map(RrActivity.Crescendo, 12, GrandHall4);
            Map(RrActivity.Crescendo, 13, ConcertHall);

            // Isle of Lost Skulls: lobby, act 2 the banquet hallway, act 4 the first beach, act 5 the jungle beach.
            Map(RrActivity.LostSkulls, 0, IsleLobby);
            Map(RrActivity.LostSkulls, 2, PiratesBanquet);
            Map(RrActivity.LostSkulls, 4, PortPlunder);
            Map(RrActivity.LostSkulls, 5, GibbetJungle);

            Map(RrActivity.Midnight, 0, Midnight);
            Map(RrActivity.RecRoyale, 0, RecRoyale);
            // The Rise of Jumbotron: the levels' own Android profiles (iteration 10; REF2's JumbotronBoss is the Studio
            // port's house copy). Area 0 is the Foyer, area 1 the Armory (act 0), areas 2-10 are acts 1-9 (the Foyer's).
            Map(RrActivity.Jumbotron, 0, ScifiDefault);
            Map(RrActivity.Jumbotron, 1, ScifiArmory);
            Map(RrActivity.StuntRunner, 0, StuntLobby);
            Map(RrActivity.StuntRunner, 1, StuntRunnerSsfx);
        }

        private static void Map(RrActivity activity, int area, Profile p)
        {
            Table[Key(activity, area)] = p;
        }

        private static int Key(RrActivity activity, int area)
        {
            return (int)activity * 1000 + Mathf.Clamp(area, 0, 999);
        }

        /// <summary>The profile of a room's area: the area's own, else the room's (area 0), else <see cref="Default"/>.</summary>
        public static Profile For(RrActivity activity, int area)
        {
            Profile p;
            if (Table.TryGetValue(Key(activity, area), out p))
            {
                return p;
            }

            if (area != 0 && Table.TryGetValue(Key(activity, 0), out p))
            {
                return p;
            }

            return Default;
        }

        /// <summary>A profile by Rec Room's asset name (case-insensitive), or null.</summary>
        public static Profile Find(string name)
        {
            foreach (var p in All)
            {
                if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }
    }
}
