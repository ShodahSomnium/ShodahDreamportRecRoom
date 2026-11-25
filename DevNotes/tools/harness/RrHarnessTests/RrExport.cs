using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RrHarness
{
    /// <summary>
    /// Research export for a scenario {"call": "RrHarness.RrExport.ExportArt"}: loads named Sprites / Texture2Ds from
    /// Rec Room's plain Addressables bundles through <see cref="RrAssets"/> and writes each one as a PNG to
    /// SP/harness/out/export/ (the sibling of the scenario's own out folder), so the research docs can look at Rec
    /// Room's own imagery of its rooms (watch illustrations, campus map, posters, loading art).
    ///
    /// Why a blit: the bundle textures are GPU-compressed (DXT/BC) and not CPU-readable, so each texture is drawn into
    /// an sRGB ARGB32 RenderTexture and read back with ReadPixels; with a Linear-space project the sRGB target
    /// re-encodes the sampled colours, so the PNG holds the texture's authored sRGB values. A Sprite is cut out of its
    /// atlas by <c>Sprite.textureRect</c> (texture pixels, origin bottom-left, same as ReadPixels).
    ///
    /// Textures are written twice: "name.png" with alpha and "name_rgb.png" with alpha forced to 1, because Rec Room's
    /// recolour textures keep a mask in alpha and would otherwise look empty.
    ///
    /// The name list is "export_art.txt" next to the scenario file (one name per line, '#' comments) when it exists,
    /// otherwise <see cref="DefaultNames"/>. A name is looked up as a Sprite (class 213) first, then as a Texture2D
    /// (class 28), like RrCoverArt does.
    /// </summary>
    public static class RrExport
    {
        private const int SpriteClass = 213;
        private const int TextureClass = 28;
        private const int MaxSide = 4096;
        private const float LoadTimeout = 60f;

        private const string FallbackOut = "C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-d1a8-4847-ac44-3e6fa88a3591/scratchpad/harness/out/export";

        /// <summary>Rec Room's own pictures of the Dorm, the Rec Center and their furniture (rplaces research, 2026-09-24).</summary>
        private static readonly string[] DefaultNames =
        {
            // watch / menu illustrations (atlas RRUI_Illustration_Atlas(1024), bundle f124b399)
            "illustration_HomeButton_RecCenter",
            "illustration_HomeButton_DormRoom",
            "Illustration_Menu_RecCenter",
            "Illustration_Menu_RecCenter-Outlined",
            "Illustration_Menu_Dorm",
            "Illustration_Menu_Play",
            "Illustration_Menu_Play_Alt",
            "Illustration_Exit",
            "MenuIcon_Sprites_3(HR)_Rec_Center_Icon",
            "MenuIcon_Sprites_3(HR)_Dorm_Icon",
            "icon_AppNav_RecCenter",
            "icon_AppNav_Dorm",
            // campus map, loading art
            "DormRoom_Map_Recolor_Tex",
            "SpawnableToolTex_DORM_CAMPUS_MAP",
            "LoadingScreen_Tex",
            "CustomLoadingScreenDefault",
            "RecRoom_Logo_SteamSplash_Tex",
            // dorm furniture / boards / posters
            "DormRoom_BulletinBoard_tex",
            "DormRoom_BunkBed_Recolor_Tex",
            "DormRoom_Rug_Recolor_Tex",
            "Dorm_Ladder_Recolor_Tex",
            "DormRoom_Light_Fluorescent_Tex",
            "SpawnableToolTex_DORM_BUNK_BED",
            "SpawnableToolTex_DORM_COMMUNITY_BOARD",
            "SpawnableToolTex_DORM_MIRROR_BUTTONS",
            "SpawnableToolTex_DORM_HELP_BUTTONS",
            "SpawnableToolTex_DORM_WINDOW_LARGE_FRAME",
            "SpawnableToolTex_DORM_SKYLIGHT_WINDOW",
            "SpawnableToolTex_DORM_LIGHT_SHAFTS",
            "CodeOfConduct_Tex",
            "Shake hands poster",
            "Stop Gesture poster",
            "Nux_Stop_Gesture2",
            "Nux_Shake_hands2",
            "Nux_Fist_Bump2",
            "Nux_Open_Watch2",
            "Nux_Menu_Default_Screen",
            "ArtCanvas_Tex",
            // Rec Center furniture / signs
            "VendingMachine_FriendoTron_tex",
            "VendingMachine_tex",
            "GiftBox_FriendoTron_Tex",
            "RecCenter_CafeAssets_tex",
            "RecCenter_ReceptionBell_tex",
            "Poster_Hydration_Tex",
            "Poster_OpenMic_Signup_tex",
            "Poster_ContentShowcase_Frame_Tex",
            "Charades_Posters_Tex",
            "Poster",
            "Banner_Shop",
            "Banner_RRPlus",
        };

        /// <summary>Scenario entry point (runs as a coroutine). Logs "[RrHarness] export art done" at the end.</summary>
        public static IEnumerator ExportArt()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                Debug.LogError("[RrHarness] export art: RrAssets.Instance is null (boot failed?)");
                yield break;
            }

            string outDir = ResolveOutDir();
            Directory.CreateDirectory(outDir);
            List<string> names = ReadNames();
            var report = new StringBuilder("[RrHarness] export art -> " + outDir + " (" + names.Count + " names)");
            int written = 0;
            int missing = 0;

            foreach (string name in names)
            {
                RrEntry entry;
                Object loaded = null;
                bool finished = false;
                bool isSprite = assets.TryFind(name, SpriteClass, out entry);
                if (isSprite)
                {
                    assets.Load<Sprite>(entry, sp =>
                    {
                        loaded = sp;
                        finished = true;
                    });
                }
                else if (assets.TryFind(name, TextureClass, out entry))
                {
                    assets.Load<Texture2D>(entry, tx =>
                    {
                        loaded = tx;
                        finished = true;
                    });
                }
                else
                {
                    missing++;
                    report.Append("\n  MISSING (no Sprite/Texture2D of that name in the catalog) ").Append(name);
                    continue;
                }

                float started = Time.realtimeSinceStartup;
                while (!finished && Time.realtimeSinceStartup - started < LoadTimeout)
                {
                    yield return null;
                }

                if (loaded == null)
                {
                    missing++;
                    report.Append("\n  LOAD FAILED ").Append(name).Append(" (bundle ").Append(entry.Bundle).Append(" #").Append(entry.PathId).Append(')');
                    continue;
                }

                try
                {
                    string safe = Safe(name);
                    if (isSprite)
                    {
                        var sprite = (Sprite)loaded;
                        Rect r = SpriteRect(sprite);
                        byte[] png = Encode(sprite.texture, r, false);
                        File.WriteAllBytes(Path.Combine(outDir, safe + ".png"), png);
                        report.Append("\n  sprite ").Append(name).Append(" ").Append((int)r.width).Append('x').Append((int)r.height)
                              .Append(" at ").Append((int)r.x).Append(',').Append((int)r.y).Append(" in ").Append(sprite.texture.name)
                              .Append(" (").Append(sprite.texture.width).Append('x').Append(sprite.texture.height).Append(") -> ").Append(safe).Append(".png");
                    }
                    else
                    {
                        var tex = (Texture2D)loaded;
                        var full = new Rect(0, 0, Mathf.Min(tex.width, MaxSide), Mathf.Min(tex.height, MaxSide));
                        File.WriteAllBytes(Path.Combine(outDir, safe + ".png"), Encode(tex, full, false));
                        File.WriteAllBytes(Path.Combine(outDir, safe + "_rgb.png"), Encode(tex, full, true));
                        report.Append("\n  texture ").Append(name).Append(" ").Append(tex.width).Append('x').Append(tex.height)
                              .Append(' ').Append(tex.format).Append(" -> ").Append(safe).Append(".png + _rgb.png");
                    }

                    written++;
                }
                catch (Exception ex)
                {
                    missing++;
                    report.Append("\n  EXPORT FAILED ").Append(name).Append(": ").Append(ex.GetType().Name).Append(' ').Append(ex.Message);
                }

                yield return null;
            }

            report.Append("\n[RrHarness] export art done: ").Append(written).Append(" written, ").Append(missing).Append(" missing/failed");
            Debug.Log(report.ToString());
        }

        private static string ResolveOutDir()
        {
            string rrOut = Environment.GetEnvironmentVariable("RR_OUT");
            if (!string.IsNullOrEmpty(rrOut))
            {
                string parent = Path.GetDirectoryName(rrOut.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(parent))
                {
                    return Path.Combine(parent, "export");
                }
            }

            return FallbackOut;
        }

        private static List<string> ReadNames()
        {
            var list = new List<string>();
            string scenario = Environment.GetEnvironmentVariable("RR_SCENARIO");
            if (!string.IsNullOrEmpty(scenario))
            {
                string txt = Path.Combine(Path.GetDirectoryName(scenario) ?? "", "export_art.txt");
                if (File.Exists(txt))
                {
                    foreach (string line in File.ReadAllLines(txt))
                    {
                        string n = line.Trim();
                        if (n.Length > 0 && !n.StartsWith("#"))
                        {
                            list.Add(n);
                        }
                    }

                    if (list.Count > 0)
                    {
                        Debug.Log("[RrHarness] export art: " + list.Count + " names from " + txt);
                        return list;
                    }
                }
            }

            list.AddRange(DefaultNames);
            return list;
        }

        /// <summary>The sprite's rectangle in its texture; tight-packed atlas sprites have no textureRect, so fall back to rect.</summary>
        private static Rect SpriteRect(Sprite sprite)
        {
            try
            {
                return sprite.textureRect;
            }
            catch (Exception)
            {
                return sprite.rect;
            }
        }

        private static byte[] Encode(Texture src, Rect px, bool opaque)
        {
            int w = src.width;
            int h = src.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Texture2D read = null;
            try
            {
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                int x = Mathf.Clamp(Mathf.RoundToInt(px.x), 0, w - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(px.y), 0, h - 1);
                int rw = Mathf.Clamp(Mathf.RoundToInt(px.width), 1, w - x);
                int rh = Mathf.Clamp(Mathf.RoundToInt(px.height), 1, h - y);
                read = new Texture2D(rw, rh, TextureFormat.RGBA32, false, false);
                read.ReadPixels(new Rect(x, y, rw, rh), 0, 0);
                read.Apply();
                if (opaque)
                {
                    Color32[] pixels = read.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].a = 255;
                    }

                    read.SetPixels32(pixels);
                    read.Apply();
                }

                return read.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (read != null)
                {
                    Object.Destroy(read);
                }
            }
        }

        private static string Safe(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return sb.ToString();
        }
    }
}
