using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>The outfit slots of the classic Rec Room (bean) avatar, in the order they are networked.</summary>
    public enum RrAvatarSlot : byte
    {
        Hair = 0,
        Hat = 1,
        Top = 2,
        Glasses = 3,
        FacialHair = 4,
        Neck = 5,
        Ears = 6,
        Back = 7,
        Belt = 8,
        Wrist = 9
    }

    /// <summary>One worn item: the Rec Room outfit prefab ("(BB_Baseball_Hat)"), a dye colour and a design.</summary>
    [Serializable]
    public sealed class RrAvatarItemChoice
    {
        /// <summary>Prefab name, empty = nothing in this slot. Saved by name so a save survives a list change.</summary>
        public string item = "";

        /// <summary>0 = the item's own colours, 1..<see cref="RrAvatarOutfit.Palette"/> length = a dye from the palette.</summary>
        public int color;

        /// <summary>Design material name (one of the item's "..._Mat" variants), empty = the item's default.</summary>
        public string style = "";

        public RrAvatarItemChoice Copy()
        {
            return new RrAvatarItemChoice { item = item ?? "", color = color, style = style ?? "" };
        }
    }

    /// <summary>
    /// What a player looks like as a Rec Room avatar, and whether they wear it at all. Saved locally as
    /// persistentDataPath/SomniumRecRoom/avatar.json (JsonUtility, readable) and sent to everyone in the player's
    /// presence record, ints <see cref="RrAvatarWire"/>. The default outfit (wear = false) is the Somnium avatar.
    /// Palettes are ours: Rec Room's skin and hair colour vaults hold no colours in the install (they came from its
    /// servers), so the tones below were picked to match Rec Room's look (the default skin is Skin_Mat's own colour).
    /// </summary>
    [Serializable]
    public sealed class RrAvatarOutfit
    {
        public const int SlotCount = 10;
        public const int Version = 1;

        public int version = Version;
        public bool wear;
        public int skin = 4;
        public int eyes;
        public int mouth;
        public RrAvatarItemChoice[] items = NewItems();

        /// <summary>Skin tones: Rec Room-like human tones, then a few fun ones.</summary>
        public static readonly Color[] SkinTones =
        {
            new Color(0.996f, 0.851f, 0.741f), new Color(0.957f, 0.784f, 0.659f), new Color(0.906f, 0.702f, 0.553f),
            new Color(0.831f, 0.600f, 0.447f), new Color(0.651f, 0.443f, 0.325f), new Color(0.722f, 0.494f, 0.341f),
            new Color(0.561f, 0.369f, 0.243f), new Color(0.443f, 0.282f, 0.180f), new Color(0.341f, 0.212f, 0.141f),
            new Color(0.231f, 0.149f, 0.106f), new Color(0.937f, 0.765f, 0.612f), new Color(0.776f, 0.588f, 0.408f),
            new Color(0.459f, 0.722f, 0.937f), new Color(0.529f, 0.851f, 0.506f), new Color(0.769f, 0.588f, 0.910f),
            new Color(0.62f, 0.62f, 0.64f)
        };

        /// <summary>Dye palette for hair and items (index 1.. in a choice; 0 = the item's own colours).</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.06f, 0.05f, 0.05f), new Color(0.20f, 0.12f, 0.07f), new Color(0.38f, 0.22f, 0.12f), new Color(0.58f, 0.38f, 0.20f),
            new Color(0.55f, 0.20f, 0.08f), new Color(0.80f, 0.40f, 0.12f), new Color(0.93f, 0.78f, 0.45f), new Color(0.96f, 0.92f, 0.78f),
            new Color(0.55f, 0.55f, 0.56f), new Color(0.96f, 0.96f, 0.96f), new Color(0.25f, 0.25f, 0.27f), new Color(0.80f, 0.12f, 0.12f),
            new Color(0.50f, 0.06f, 0.10f), new Color(0.96f, 0.42f, 0.11f), new Color(0.98f, 0.80f, 0.12f), new Color(0.55f, 0.82f, 0.18f),
            new Color(0.13f, 0.55f, 0.27f), new Color(0.06f, 0.30f, 0.20f), new Color(0.10f, 0.64f, 0.62f), new Color(0.33f, 0.78f, 0.95f),
            new Color(0.13f, 0.40f, 0.85f), new Color(0.08f, 0.14f, 0.40f), new Color(0.45f, 0.22f, 0.75f), new Color(0.70f, 0.45f, 0.90f),
            new Color(0.96f, 0.55f, 0.72f), new Color(0.92f, 0.20f, 0.52f), new Color(0.82f, 0.70f, 0.55f), new Color(0.45f, 0.40f, 0.30f)
        };

        public static RrAvatarItemChoice[] NewItems()
        {
            var a = new RrAvatarItemChoice[SlotCount];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = new RrAvatarItemChoice();
            }

            return a;
        }

        /// <summary>A first outfit: Rec Room's look out of the box - short hair, a T-shirt, the default face.</summary>
        public static RrAvatarOutfit Default()
        {
            var o = new RrAvatarOutfit();
            o.items[(int)RrAvatarSlot.Hair].item = "(BB_WavyShort_Hair)";
            o.items[(int)RrAvatarSlot.Hair].color = 3;
            o.items[(int)RrAvatarSlot.Top].item = "(BB_MaleTShirt_Shirt)";
            o.items[(int)RrAvatarSlot.Top].color = 21;
            return o;
        }

        public RrAvatarItemChoice Slot(RrAvatarSlot slot)
        {
            Repair();
            return items[(int)slot];
        }

        public RrAvatarOutfit Copy()
        {
            Repair();
            var o = new RrAvatarOutfit { version = version, wear = wear, skin = skin, eyes = eyes, mouth = mouth, items = new RrAvatarItemChoice[SlotCount] };
            for (int i = 0; i < SlotCount; i++)
            {
                o.items[i] = items[i].Copy();
            }

            return o;
        }

        /// <summary>Fills what JsonUtility leaves null or out of range.</summary>
        public void Repair()
        {
            if (items == null || items.Length != SlotCount)
            {
                var old = items;
                items = NewItems();
                if (old != null)
                {
                    for (int i = 0; i < old.Length && i < SlotCount; i++)
                    {
                        if (old[i] != null)
                        {
                            items[i] = old[i];
                        }
                    }
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (items[i] == null)
                {
                    items[i] = new RrAvatarItemChoice();
                }

                items[i].item = items[i].item ?? "";
                items[i].style = items[i].style ?? "";
                items[i].color = Mathf.Clamp(items[i].color, 0, Palette.Length);
            }

            skin = Mathf.Clamp(skin, 0, SkinTones.Length - 1);
            eyes = Mathf.Max(0, eyes);
            mouth = Mathf.Max(0, mouth);
        }

        public static Color Skin(int index)
        {
            return SkinTones[Mathf.Clamp(index, 0, SkinTones.Length - 1)];
        }

        /// <summary>A dye (1-based); index 0 or out of range = white (no tint).</summary>
        public static Color Dye(int index)
        {
            return index >= 1 && index <= Palette.Length ? Palette[index - 1] : Color.white;
        }

        // ------------------------------------------------------------------ saving

        private static string _file;

        /// <summary>persistentDataPath/SomniumRecRoom/avatar.json</summary>
        public static string FilePath
        {
            get
            {
                if (_file == null)
                {
                    _file = Path.Combine(Path.Combine(Application.persistentDataPath, "SomniumRecRoom"), "avatar.json");
                }

                return _file;
            }
        }

        /// <summary>The saved outfit, or null when there is none (or it cannot be read).</summary>
        public static RrAvatarOutfit Load()
        {
            try
            {
                string f = FilePath;
                if (!File.Exists(f))
                {
                    return null;
                }

                var o = JsonUtility.FromJson<RrAvatarOutfit>(File.ReadAllText(f, Encoding.UTF8));
                if (o == null)
                {
                    return null;
                }

                o.Repair();
                return o;
            }
            catch (Exception e)
            {
                RrLog.Warn("Avatar", "the saved avatar could not be read (" + e.Message + "): starting from the default");
                return null;
            }
        }

        public bool Save()
        {
            try
            {
                Repair();
                string f = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this, true), Encoding.UTF8);
                if (File.Exists(f))
                {
                    File.Delete(f);
                }

                File.Move(tmp, f);
                return true;
            }
            catch (Exception e)
            {
                RrLog.Warn("Avatar", "saving the avatar failed: " + e.Message);
                return false;
            }
        }

        internal static void ResetStatics()
        {
            _file = null;
        }
    }

    /// <summary>
    /// The outfit in a presence record (Net/RrPresenceWire ints 48-58), so everyone - late joiners included - sees it
    /// without an event:
    ///   48  wear (bit 0) | version (1-7) | skin (8-15) | eyes (16-23) | mouth (24-31)
    ///   49-58  one per slot: item index + 1 (0-11, 0 = none) | dye (12-17) | design index + 1 (18-25, 0 = default)
    /// Item and design indices are positions in lists built from the install's catalog (<see cref="RrAvatarCatalog"/>),
    /// sorted by name, so they agree between clients with the same Rec Room install (its files no longer change).
    /// </summary>
    public static class RrAvatarWire
    {
        public const int At = Net.RrPresenceWire.AtAvatar;
        public const int Ints = 1 + RrAvatarOutfit.SlotCount;

        public static bool Wearing(int[] record)
        {
            return record != null && record.Length >= At + Ints && (record[At] & 1) != 0 && ((record[At] >> 1) & 0x7F) == RrAvatarOutfit.Version;
        }

        /// <summary>Writes the outfit into the record's avatar ints; false when nothing changed.</summary>
        public static bool Write(int[] record, RrAvatarOutfit o)
        {
            if (record == null || record.Length < At + Ints)
            {
                return false;
            }

            bool changed = false;
            int head = o.wear ? 1 | (RrAvatarOutfit.Version << 1) | ((o.skin & 0xFF) << 8) | ((o.eyes & 0xFF) << 16) | ((o.mouth & 0xFF) << 24) : 0;
            changed |= Put(record, At, head);
            for (int i = 0; i < RrAvatarOutfit.SlotCount; i++)
            {
                int v = 0;
                if (o.wear)
                {
                    var c = o.items[i];
                    int item = RrAvatarCatalog.IndexOf((RrAvatarSlot)i, c.item);
                    if (item >= 0)
                    {
                        int style = RrAvatarCatalog.StyleIndex((RrAvatarSlot)i, item, c.style);
                        v = ((item + 1) & 0xFFF) | ((c.color & 0x3F) << 12) | (((style + 1) & 0xFF) << 18);
                    }
                }

                changed |= Put(record, At + 1 + i, v);
            }

            return changed;
        }

        private static bool Put(int[] r, int at, int v)
        {
            if (r[at] == v)
            {
                return false;
            }

            r[at] = v;
            return true;
        }

        /// <summary>A key that changes whenever any avatar int changes (to rebuild only on change).</summary>
        public static string Key(int[] record)
        {
            if (!Wearing(record))
            {
                return "";
            }

            var sb = new StringBuilder(96);
            for (int i = 0; i < Ints; i++)
            {
                sb.Append(record[At + i]).Append(',');
            }

            return sb.ToString();
        }

        /// <summary>Reads an outfit back (wear false when the record says the Somnium avatar).</summary>
        public static RrAvatarOutfit Read(int[] record)
        {
            var o = new RrAvatarOutfit();
            if (!Wearing(record))
            {
                o.wear = false;
                return o;
            }

            int head = record[At];
            o.wear = true;
            o.skin = (head >> 8) & 0xFF;
            o.eyes = (head >> 16) & 0xFF;
            o.mouth = (head >> 24) & 0xFF;
            for (int i = 0; i < RrAvatarOutfit.SlotCount; i++)
            {
                int v = record[At + 1 + i];
                int item = (v & 0xFFF) - 1;
                var c = o.items[i];
                c.item = item >= 0 ? RrAvatarCatalog.NameAt((RrAvatarSlot)i, item) : "";
                c.color = (v >> 12) & 0x3F;
                int style = ((v >> 18) & 0xFF) - 1;
                c.style = item >= 0 && style >= 0 ? RrAvatarCatalog.StyleName((RrAvatarSlot)i, item, style) : "";
            }

            o.Repair();
            return o;
        }
    }
}
