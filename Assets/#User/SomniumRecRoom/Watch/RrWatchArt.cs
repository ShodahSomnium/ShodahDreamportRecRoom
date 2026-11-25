using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>How an activity's picture fills its card.</summary>
    public enum RrArtFit : byte
    {
        /// <summary>Covers the card, cropping the overflow (the 16:9 quest renders, the gift-box prints).</summary>
        Cover = 0,

        /// <summary>Fits inside the card over its background colours (logos, stickers, cut-out faces).</summary>
        Fit = 1
    }

    /// <summary>The picture a card shows for one activity (rr_watch_ui.md 3.3).</summary>
    public sealed class RrActivityArt
    {
        public RrActivity Activity;
        public string Title;
        public string Genre;
        public string Asset;

        /// <summary>A second picture beside the first (Bowling: pin and ball).</summary>
        public string Asset2;

        /// <summary>Crop of a texture in pixels, origin bottom-left; zero = the whole texture or sprite.</summary>
        public Rect Crop;

        public RrArtFit Fit;
        public Color BackFrom;
        public Color BackTo;

        /// <summary>Tint for monochrome icons (white glove); white otherwise.</summary>
        public Color Tint = Color.white;
    }

    /// <summary>
    /// Every picture the watch draws, from the player's Rec Room install: the RRUI icon and illustration
    /// sprites (packed in RRUI_Icon_Atlas(512) and RRUI_Illustration_Atlas(1024), bundle f124b399), and the
    /// best in-bundle art for each activity. Rec Room served its room cover images from rec.net, so none are in
    /// the bundles; the research picked a replacement per activity - the gift-box prints of the sports and
    /// party games, the quest villains' camera-filter renders, logos and decals - with the crop rectangles
    /// measured on the exported textures (table 3.3), which are kept here.
    ///
    /// Names are tried as sprites first and then as textures (cropped into a sprite here), so a room's
    /// <see cref="RrRoomInfo.CoverSprite"/> may name either. Results are cached by name; a missing picture
    /// costs one warning and leaves its image hidden.
    ///
    /// Also here: the white rounded-rectangle sprite that every rounded surface of the watch is drawn with,
    /// standing in for RRUI.RoundedCorners (a missing script at runtime). It is one 9-sliced texture whose
    /// corner radius is set per image through pixelsPerUnitMultiplier, so every radius Rec Room uses (16, 20, 30,
    /// 40, pills) comes out exact from the same sprite.
    /// </summary>
    public static class RrWatchArt
    {
        /// <summary>Corner radius of the generated sprite, in its own pixels.</summary>
        public const float SpriteRadius = 64f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, List<Action<Sprite>>> Waiting = new Dictionary<string, List<Action<Sprite>>>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        private static readonly Dictionary<RrActivity, RrActivityArt> Activities = new Dictionary<RrActivity, RrActivityArt>();
        private static Sprite _rounded;
        private static Sprite _ring;

        private static Color Hex(uint rgb)
        {
            return RrUiKit.Hex(rgb);
        }

        // ---- generated sprites ----

        /// <summary>White rounded rectangle, 9-sliced with a 64 px border: use with <see cref="RrUiKit.Rounded"/>.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null)
                {
                    _rounded = MakeRounded(132, 132, SpriteRadius, 0f, "Rr Watch Rounded");
                }

                return _rounded;
            }
        }

        /// <summary>White ring (the watch's dwell visual, radial fill).</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null)
                {
                    _ring = MakeRounded(128, 128, 64f, 12f, "Rr Watch Ring");
                }

                return _ring;
            }
        }

        private static Sprite MakeRounded(int w, int h, float radius, float ringWidth, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[w * h];
            float hx = w * 0.5f;
            float hy = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - hx) - (hx - radius);
                    float qy = Mathf.Abs(y + 0.5f - hy) - (hy - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - outside);
                    if (ringWidth > 0f)
                    {
                        a *= Mathf.Clamp01(0.5f + outside + ringWidth);
                    }

                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            tex.SetPixels32(px);
            tex.Apply(true, true);
            Owned.Add(tex);
            float border = ringWidth > 0f ? 0f : radius;
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            Owned.Add(sprite);
            return sprite;
        }

        // ---- Rec Room sprites ----

        /// <summary>Loads a Rec Room sprite (or a texture, made into a sprite, optionally cropped) by name.</summary>
        public static void Get(string name, Action<Sprite> done, Rect crop = default(Rect))
        {
            if (string.IsNullOrEmpty(name))
            {
                done(null);
                return;
            }

            string key = crop.width > 0f ? name + "@" + crop.x + "," + crop.y + "," + crop.width + "," + crop.height : name;
            Sprite ready;
            if (Cache.TryGetValue(key, out ready) && ready != null)
            {
                done(ready);
                return;
            }

            if (Missing.Contains(key) || RrAssets.Instance == null)
            {
                done(null);
                return;
            }

            List<Action<Sprite>> list;
            if (Waiting.TryGetValue(key, out list))
            {
                list.Add(done);
                return;
            }

            Waiting[key] = new List<Action<Sprite>> { done };
            var assets = RrAssets.Instance;
            Rr.RrEntry entry;
            if (crop.width <= 0f && assets.TryFind(name, 213, out entry))
            {
                assets.Load<Sprite>(entry, s => Finish(key, name, s));
                return;
            }

            if (assets.TryFind(name, 28, out entry))
            {
                assets.Load<Texture2D>(entry, t => Finish(key, name, FromTexture(t, crop)));
                return;
            }

            if (assets.TryFind(name, 213, out entry))
            {
                assets.Load<Sprite>(entry, s => Finish(key, name, s));
                return;
            }

            Finish(key, name, null);
        }

        private static Sprite FromTexture(Texture2D tex, Rect crop)
        {
            if (tex == null)
            {
                return null;
            }

            var r = crop.width > 0f ? crop : new Rect(0, 0, tex.width, tex.height);
            r.x = Mathf.Clamp(r.x, 0, tex.width - 1);
            r.y = Mathf.Clamp(r.y, 0, tex.height - 1);
            r.width = Mathf.Clamp(r.width, 1, tex.width - r.x);
            r.height = Mathf.Clamp(r.height, 1, tex.height - r.y);
            var s = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name + " (watch)";
            Owned.Add(s);
            return s;
        }

        private static void Finish(string key, string name, Sprite sprite)
        {
            if (sprite != null)
            {
                Cache[key] = sprite;
            }
            else
            {
                Missing.Add(key);
                RrLog.WarnOnce("Watch", "art:" + key, "picture not found in Rec Room's bundles: " + name);
            }

            List<Action<Sprite>> list;
            if (!Waiting.TryGetValue(key, out list))
            {
                return;
            }

            Waiting.Remove(key);
            foreach (var cb in list)
            {
                try
                {
                    cb(sprite);
                }
                catch (Exception e)
                {
                    RrLog.Error("Watch", "picture callback for " + name + " failed", e);
                }
            }
        }

        /// <summary>
        /// Puts a Rec Room sprite on an image when it arrives; the image stays hidden until then (a white square
        /// would flash otherwise) and stays hidden if the picture does not exist.
        /// </summary>
        public static void Apply(Image image, string name, Rect crop = default(Rect))
        {
            if (image == null)
            {
                return;
            }

            image.enabled = false;
            Get(name, s =>
            {
                if (image == null)
                {
                    return;
                }

                image.sprite = s;
                image.enabled = s != null;
            }, crop);
        }

        // ---- activity art (rr_watch_ui.md table 3.3) ----

        private static void Define()
        {
            if (Activities.Count > 0)
            {
                return;
            }

            var gift = new Rect(8, 276, 380, 268);
            Put(RrActivity.Dorm, "Dorm Room", "Home", "Illustration_Menu_Dorm", default(Rect), RrArtFit.Fit, Hex(0xD7E7E3), Hex(0xBFD8D2));
            Put(RrActivity.RecCenter, "The Rec Center", "Hangout", "Illustration_Menu_RecCenter", default(Rect), RrArtFit.Fit, Hex(0xF5D2B8), Hex(0xF9F3EF));
            Put(RrActivity.Charades, "3D Charades", "Party", "GiftBox_Charades_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.Bowling, "Bowling", "Sports", "SpawnableToolTex_BOWLING_PIN", default(Rect), RrArtFit.Fit, Hex(0xC68A4B), Hex(0x8A5A2B)).Asset2 = "SpawnableToolTex_BOWLING_BALL";
            Put(RrActivity.Crescendo, "Crescendo of the Blood Moon", "Quest", "Consumable_Polaroid_Dracula_Tex(2048)", default(Rect), RrArtFit.Cover, Hex(0x5B0E1C), Hex(0x12060B));
            Put(RrActivity.DiscGolf, "Disc Golf", "Sports", "Giftbox_DiscGolf_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.Dodgeball, "Dodgeball", "Sports", "GiftBox_DodgeBall_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.LostSkulls, "Isle of Lost Skulls", "Quest", "Consumable_Polaroid_GhostBeard_Tex(2048)", default(Rect), RrArtFit.Cover, Hex(0x1E6F8C), Hex(0x0B2230));
            Put(RrActivity.LaserTag, "Laser Tag", "PvP", "MaleTShirt_Shirt_LaserTag_Alpha_DECAL", new Rect(80, 414, 860, 530), RrArtFit.Fit, Hex(0x3C1B5F), Hex(0x1C1831));
            Put(RrActivity.Midnight, "Make It To Midnight", "Party", "Weapon_BonkyHammer_Skin_BonkyFace_Tex", new Rect(700, 604, 220, 260), RrArtFit.Fit, Hex(0xF0A030), Hex(0xE0101A));
            Put(RrActivity.LittleMonsters, "My Little Monsters", "Party", "UntuckedTee_Shirt_MLM_Mothman_Tex", new Rect(70, 564, 320, 380), RrArtFit.Fit, Hex(0x7B64C8), Hex(0x5A48A0));
            Put(RrActivity.Paddleball, "Paddleball", "Sports", "GiftBox_Paddleball_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.Paintball, "Paintball", "PvP", "GiftBox_Paintball_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.PunchUp, "Punch Up", "Party", "MenuIcon_Sprites_2_Gloves", default(Rect), RrArtFit.Fit, Hex(0xF76B1C), Hex(0xF63E16));
            Put(RrActivity.GoldenTrophy, "Quest for the Golden Trophy", "Quest", "Consumable_Polaroid_Goblin1_Tex(2048)", default(Rect), RrArtFit.Cover, Hex(0xB8923E), Hex(0x5A4318));
            Put(RrActivity.RecRally, "Rec Rally", "Racing", "GiftBox_RecRally_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.RecRoyale, "Rec Royale", "PvP", "Rec_Royale_Badge_Logo", default(Rect), RrArtFit.Fit, Hex(0x5B9AA0), Hex(0x3F6E73));
            Put(RrActivity.Showdown, "Rec Room Showdown", "PvP", "Standee_CactusTumbleweed_Tex", new Rect(70, 364, 340, 620), RrArtFit.Fit, Hex(0xF2A541), Hex(0xC1502E));
            Put(RrActivity.Soccer, "Soccer", "Sports", "GiftBox_Soccer_Tex", gift, RrArtFit.Cover, Hex(0xF3EDE3), Hex(0xE8DFD0));
            Put(RrActivity.StuntRunner, "Stunt Runner", "Racing", "StuntRunner_Decal_Tex", new Rect(70, 234, 880, 585), RrArtFit.Fit, Hex(0x2A1B4A), Hex(0x120A24));
            Put(RrActivity.CrimsonCauldron, "The Curse of the Crimson Cauldron", "Quest", "Consumable_Polaroid_Witch_Tex(2048)", default(Rect), RrArtFit.Cover, Hex(0x3C1B5F), Hex(0x190B28));
            Put(RrActivity.Jumbotron, "The Rise of Jumbotron", "Quest", "Consumable_Polaroid_Jumbotron_Tex(2048)", default(Rect), RrArtFit.Cover, Hex(0x3A0A3C), Hex(0x12040F));
        }

        private static RrActivityArt Put(RrActivity a, string title, string genre, string asset, Rect crop, RrArtFit fit, Color from, Color to)
        {
            var art = new RrActivityArt { Activity = a, Title = title, Genre = genre, Asset = asset, Crop = crop, Fit = fit, BackFrom = from, BackTo = to };
            Activities[a] = art;
            return art;
        }

        /// <summary>
        /// The picture for an activity. A room may name its own cover (<see cref="RrRoomInfo.CoverSprite"/>);
        /// when that is one of the researched assets its measured crop is used, otherwise the whole picture.
        /// </summary>
        public static RrActivityArt For(RrActivity activity, RrRoomInfo info = null)
        {
            Define();
            RrActivityArt art;
            Activities.TryGetValue(activity, out art);

            // Registries name covers in RrCoverArt's form, "Name|x,y,w,h" (a crop in texture pixels, origin bottom-left):
            // look the name up, keep the crop.
            string cover = info != null ? info.CoverSprite : null;
            Rect crop = default(Rect);
            if (!string.IsNullOrEmpty(cover))
            {
                int bar = cover.IndexOf('|');
                if (bar >= 0)
                {
                    crop = ParseCrop(cover.Substring(bar + 1));
                    cover = cover.Substring(0, bar).Trim();
                }
            }

            if (string.IsNullOrEmpty(cover) || (art != null && cover == art.Asset))
            {
                return art ?? new RrActivityArt { Activity = activity, Title = activity.ToString(), Genre = "", BackFrom = RrUiKit.PrimaryFrom, BackTo = RrUiKit.PrimaryTo };
            }

            return new RrActivityArt
            {
                Activity = activity,
                Title = art != null ? art.Title : activity.ToString(),
                Genre = art != null ? art.Genre : "",
                Asset = cover,
                Crop = crop,
                Fit = RrArtFit.Cover,
                BackFrom = art != null ? art.BackFrom : RrUiKit.PrimaryFrom,
                BackTo = art != null ? art.BackTo : RrUiKit.PrimaryTo
            };
        }

        /// <summary>"x,y,w,h" (invariant culture) to a pixel rect; an empty rect (the whole picture) when it does not parse.</summary>
        private static Rect ParseCrop(string text)
        {
            string[] parts = text.Split(',');
            if (parts.Length != 4)
            {
                return default(Rect);
            }

            var v = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v[i]))
                {
                    return default(Rect);
                }
            }

            return v[2] > 0f && v[3] > 0f ? new Rect(v[0], v[1], v[2], v[3]) : default(Rect);
        }

        /// <summary>Rec Room's name for an activity, when no room has registered a title.</summary>
        public static string TitleOf(RrActivity activity)
        {
            var info = RrRooms.Get(activity);
            if (info != null && !string.IsNullOrEmpty(info.Title))
            {
                return info.Title;
            }

            var art = For(activity);
            return art != null ? art.Title : activity.ToString();
        }

        /// <summary>Starts loading every activity's picture (the Play page's cards) so they are ready when shown.</summary>
        public static void Preload()
        {
            Define();
            foreach (var kv in Activities)
            {
                Get(kv.Value.Asset, s => { }, kv.Value.Crop);
                if (!string.IsNullOrEmpty(kv.Value.Asset2))
                {
                    Get(kv.Value.Asset2, s => { });
                }
            }
        }

        public static void Shutdown()
        {
            foreach (var o in Owned)
            {
                if (o != null)
                {
                    UnityEngine.Object.Destroy(o);
                }
            }

            Owned.Clear();
            Cache.Clear();
            Waiting.Clear();
            Missing.Clear();
            _rounded = null;
            _ring = null;
        }
    }
}
