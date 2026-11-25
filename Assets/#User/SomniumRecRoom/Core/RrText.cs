using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's typefaces for every label this world draws.
    ///
    /// The bundles carry the fonts as ordinary Font assets (TTF data): Rec Room's classic Futura bold (menus,
    /// scoreboards, signs), Jost (the newer interface) and Roboto. They are made into TextMeshPro font assets at
    /// runtime with TMP_FontAsset.CreateFontAsset - dynamic SDF atlases built by TextCore, which is on the
    /// client's module list - rather than loading Rec Room's own TMP_FontAsset objects, whose serialized form
    /// belongs to Unity 6.0's TMP and is not proven to read in the client's 6.3.
    /// The scene's status label carries LiberationSans SDF into the upload; it is the fallback.
    /// </summary>
    public static class RrText
    {
        private static readonly string[] TitleFonts = { "legacy_FUTURA-T-OT-BOLD-CONDENSED", "legacy_FuturaT_Bold", "Jost-500-Medium" };
        private static readonly string[] BodyFonts = { "Jost-500-Medium", "Roboto-Regular", "legacy_FuturaT_Bold" };

        /// <summary>Body text (menus, descriptions).</summary>
        public static TMP_FontAsset Font { get; private set; }

        /// <summary>Titles, scoreboards, signs.</summary>
        public static TMP_FontAsset Title { get; private set; }

        public static TMP_FontAsset Fallback
        {
            get
            {
                var main = RrMain.Instance;
                if (main != null && main.StatusLabel != null && main.StatusLabel.font != null)
                {
                    return main.StatusLabel.font;
                }

                return TMP_Settings.defaultFontAsset;
            }
        }

        public static void Init()
        {
            // Everything happens in Load (awaited by RrMain); kept for the boot step list.
        }

        public static IEnumerator Load()
        {
            TMP_FontAsset title = null;
            TMP_FontAsset body = null;
            foreach (var name in TitleFonts)
            {
                yield return Make(name, f => title = f);
                if (title != null)
                {
                    break;
                }
            }

            foreach (var name in BodyFonts)
            {
                yield return Make(name, f => body = f);
                if (body != null)
                {
                    break;
                }
            }

            Title = title != null ? title : Fallback;
            Font = body != null ? body : Title;
            RrLog.Info("Text", "title font " + (Title != null ? Title.name : "none") + ", body font " + (Font != null ? Font.name : "none"));
        }

        private static IEnumerator Make(string fontName, Action<TMP_FontAsset> done)
        {
            Font font = null;
            if (RrAssets.Instance != null)
            {
                yield return RrAssets.Instance.LoadRoutine<Font>(fontName, 128, f => font = f);
            }

            if (font == null)
            {
                done(null);
                yield break;
            }

            TMP_FontAsset asset = null;
            try
            {
                asset = TMP_FontAsset.CreateFontAsset(font);
                if (asset != null)
                {
                    asset.name = fontName + " (rr)";
                    var fallback = Fallback;
                    if (fallback != null && fallback != asset)
                    {
                        asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("Text", "font " + fontName + " could not be made into a TMP font: " + e.Message);
            }

            done(asset);
        }

        /// <summary>Applies the body or title font to a label.</summary>
        public static void Style(TMP_Text label, bool title = false)
        {
            if (label == null)
            {
                return;
            }

            var f = title ? Title : Font;
            if (f != null)
            {
                label.font = f;
            }
        }

        public static void Shutdown()
        {
            Font = null;
            Title = null;
        }
    }
}
