using System;
using System.Collections.Generic;
using System.Globalization;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Cover and card art for places (RrRoomInfo.CoverSprite / IconSprite) as the loading card and the activity
    /// doors show it.
    ///
    /// Rec Room served its room pictures from rec.net, so none of the official covers are in the bundles
    /// (rr_watch_ui.md §3.3); the stand-ins are sprites (the menu illustrations of the Rec Center and the dorm)
    /// or whole textures (gift-box prints, t-shirt decals, camera-filter renders) of which only a part is the
    /// picture. A name is therefore looked up as a Sprite first (class 213), then as a Texture2D (class 28), and
    /// may carry a crop in texture pixels with the origin bottom-left, as the research tables give them:
    /// "GiftBox_Paintball_Tex|8,276,380,268". A crop on a sprite is taken inside the sprite's texture.
    ///
    /// Results (and misses) are cached for the visit; the textures stay owned by <see cref="RrAssets"/>.
    /// </summary>
    public static class RrCoverArt
    {
        private static readonly Dictionary<string, RrArt> Done = new Dictionary<string, RrArt>();
        private static readonly Dictionary<string, List<Action<RrArt>>> Waiting = new Dictionary<string, List<Action<RrArt>>>();

        /// <summary>Loads a picture by name (with an optional "|x,y,w,h" crop); calls back with null when there is none.</summary>
        public static void Load(string spec, Action<RrArt> done)
        {
            if (done == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(spec) || RrAssets.Instance == null)
            {
                done(null);
                return;
            }

            RrArt art;
            if (Done.TryGetValue(spec, out art))
            {
                done(art);
                return;
            }

            List<Action<RrArt>> list;
            if (Waiting.TryGetValue(spec, out list))
            {
                list.Add(done);
                return;
            }

            Waiting[spec] = new List<Action<RrArt>> { done };
            string name;
            Rect? crop;
            Parse(spec, out name, out crop);
            var assets = RrAssets.Instance;
            RrEntry entry;
            if (assets.TryFind(name, 213, out entry))
            {
                assets.Load<Sprite>(entry, sprite => Finish(spec, FromSprite(sprite, crop)));
            }
            else if (assets.TryFind(name, 28, out entry))
            {
                assets.Load<Texture2D>(entry, tex => Finish(spec, FromTexture(tex, crop)));
            }
            else
            {
                RrLog.WarnOnce("Rooms", "art:" + name, "no sprite or texture named " + name + " for a cover");
                Finish(spec, null);
            }
        }

        private static void Finish(string spec, RrArt art)
        {
            Done[spec] = art;
            List<Action<RrArt>> list;
            if (!Waiting.TryGetValue(spec, out list))
            {
                return;
            }

            Waiting.Remove(spec);
            foreach (var cb in list)
            {
                try
                {
                    cb(art);
                }
                catch (Exception e)
                {
                    RrLog.Error("Rooms", "cover art callback failed", e);
                }
            }
        }

        private static void Parse(string spec, out string name, out Rect? crop)
        {
            crop = null;
            int bar = spec.IndexOf('|');
            if (bar < 0)
            {
                name = spec.Trim();
                return;
            }

            name = spec.Substring(0, bar).Trim();
            var parts = spec.Substring(bar + 1).Split(',');
            if (parts.Length != 4)
            {
                return;
            }

            var v = new float[4];
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
                {
                    return;
                }
            }

            if (v[2] > 0f && v[3] > 0f)
            {
                crop = new Rect(v[0], v[1], v[2], v[3]);
            }
        }

        private static RrArt FromSprite(Sprite sprite, Rect? crop)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            var tex = sprite.texture;
            if (crop.HasValue)
            {
                return FromTexture(tex, crop);
            }

            // The sprite's own UVs bound its place in the (possibly packed) texture; the rect gives its size.
            var uvs = sprite.uv;
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (uvs != null && uvs.Length > 0)
            {
                Vector2 min = uvs[0];
                Vector2 max = uvs[0];
                for (int i = 1; i < uvs.Length; i++)
                {
                    min = Vector2.Min(min, uvs[i]);
                    max = Vector2.Max(max, uvs[i]);
                }

                uv = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            var size = sprite.rect.size;
            return new RrArt { Sprite = sprite, Texture = tex, Uv = uv, Aspect = size.y > 0f ? size.x / size.y : 1f };
        }

        private static RrArt FromTexture(Texture tex, Rect? crop)
        {
            if (tex == null)
            {
                return null;
            }

            float w = Mathf.Max(1f, tex.width);
            float h = Mathf.Max(1f, tex.height);
            if (!crop.HasValue)
            {
                return new RrArt { Texture = tex, Uv = new Rect(0f, 0f, 1f, 1f), Aspect = w / h };
            }

            var c = crop.Value;
            return new RrArt { Texture = tex, Uv = new Rect(c.x / w, c.y / h, c.width / w, c.height / h), Aspect = c.width / c.height };
        }

        public static void Shutdown()
        {
            Done.Clear();
            Waiting.Clear();
        }
    }
}
