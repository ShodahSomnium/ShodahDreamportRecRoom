using System;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>
    /// What the player's Rec Room install offers the avatar editor, read from the bundle catalog at run time (no tables
    /// in code: the assembly's string heap is nearly full, DevNotes/iter15/avatar.md):
    /// - the classic ("bean") outfit items: prefab roots named "(BB_..._Hair)", "(BB_..._Hat)", "_Shirt", "_Eye",
    ///   "_Mouth", "_Neck", "_Ear...", "_Shoulder", "_Belt", "_Wrist" (137 in the final install), sorted by name per slot;
    /// - an item's designs: the materials named "&lt;Item&gt;_..._Mat" (Rec Room's AvatarItemMaterial combinations), sorted;
    /// - eye and mouth styles: every bundle with an "Eye_Default" / "Mouth_Smile" texture (one bundle per style), sorted by
    ///   bundle file; each style bundle also has the blink and expression textures under the same names.
    /// Indices are positions in these sorted lists, which is what <see cref="RrAvatarWire"/> sends.
    /// </summary>
    public static class RrAvatarCatalog
    {
        public sealed class Item
        {
            public RrAvatarSlot Slot;
            public string Prefab;
            public string Label;
            public RrEntry Entry;
            internal List<RrEntry> Styles;
        }

        public sealed class Style
        {
            public RrEntry Eye;
            public RrEntry Blink;
            public string Label;
        }

        private static readonly List<Item>[] Items = new List<Item>[RrAvatarOutfit.SlotCount];
        private static readonly List<Style> EyeList = new List<Style>();
        private static readonly List<Style> MouthList = new List<Style>();
        private static List<RrEntry> _materials;
        private static RrCatalog _built;

        public static bool Ready
        {
            get { return _built != null && RrAssets.Instance != null && ReferenceEquals(_built, RrAssets.Instance.Catalog); }
        }

        public static readonly string[] SlotLabels = { "Hair", "Hats", "Tops", "Glasses", "Facial Hair", "Neck", "Ears", "Back", "Belts", "Wrists" };

        /// <summary>Builds the lists once the assets are up (cheap: one pass over the catalog's entries).</summary>
        public static bool EnsureBuilt()
        {
            if (Ready)
            {
                return true;
            }

            var assets = RrAssets.Instance;
            if (assets == null || assets.Catalog == null)
            {
                return false;
            }

            var cat = assets.Catalog;
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i] = new List<Item>();
            }

            EyeList.Clear();
            MouthList.Clear();
            _materials = new List<RrEntry>();
            var entries = cat.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string n = e.Name;
                if (string.IsNullOrEmpty(n))
                {
                    continue;
                }

                if (e.ClassId == 1 && n.StartsWith("(BB_", StringComparison.Ordinal) && n.EndsWith(")", StringComparison.Ordinal))
                {
                    int slot = SlotOf(n);
                    if (slot >= 0 && e.LoaderBundle >= 0)
                    {
                        Items[slot].Add(new Item { Slot = (RrAvatarSlot)slot, Prefab = n, Label = LabelOf(n), Entry = e });
                    }
                }
                else if (e.ClassId == 21 && n.EndsWith("_mat", StringComparison.OrdinalIgnoreCase))
                {
                    _materials.Add(e);
                }
                else if (e.ClassId == 28 && e.LoaderBundle >= 0)
                {
                    if (n == "Eye_Default")
                    {
                        EyeList.Add(new Style { Eye = e });
                    }
                    else if (n == "Mouth_Smile")
                    {
                        MouthList.Add(new Style { Eye = e });
                    }
                }
            }

            for (int i = 0; i < Items.Length; i++)
            {
                Items[i].Sort((a, b) => string.CompareOrdinal(a.Prefab, b.Prefab));
            }

            _materials.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Comparison<Style> byBundle = (a, b) => string.CompareOrdinal(cat.Bundles[a.Eye.Bundle].File, cat.Bundles[b.Eye.Bundle].File);
            EyeList.Sort(byBundle);
            MouthList.Sort(byBundle);
            var blinks = cat.Find("Eye_Blink", 28);
            for (int i = 0; i < EyeList.Count; i++)
            {
                EyeList[i].Label = "Eyes " + (i + 1);
                foreach (var b in blinks)
                {
                    if (b.Bundle == EyeList[i].Eye.Bundle)
                    {
                        EyeList[i].Blink = b;
                    }
                }
            }

            for (int i = 0; i < MouthList.Count; i++)
            {
                MouthList[i].Label = "Mouth " + (i + 1);
            }

            _built = cat;
            int total = 0;
            var sb = new StringBuilder();
            for (int i = 0; i < Items.Length; i++)
            {
                total += Items[i].Count;
                sb.Append(SlotLabels[i]).Append(' ').Append(Items[i].Count).Append(i < Items.Length - 1 ? ", " : "");
            }

            RrLog.Info("Avatar", "catalog: " + total + " classic outfit items (" + sb + "), " + EyeList.Count + " eye and " + MouthList.Count + " mouth styles, " + _materials.Count + " item materials");
            return true;
        }

        /// <summary>The slot an item prefab name belongs to, -1 for none (internal, team-coloured, UGC templates).</summary>
        private static int SlotOf(string n)
        {
            if (n.IndexOf("INTERNALUSEONLY", StringComparison.Ordinal) >= 0 || n.IndexOf("UGC", StringComparison.Ordinal) >= 0 ||
                n.IndexOf("Default_Unknown", StringComparison.Ordinal) >= 0 || n.IndexOf("_Team", StringComparison.Ordinal) >= 0)
            {
                return -1;
            }

            if (n.EndsWith("_Hair)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Hair;
            }

            if (n.EndsWith("_Hat)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Hat;
            }

            if (n.EndsWith("_Shirt)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Top;
            }

            if (n.EndsWith("_Eye)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Glasses;
            }

            if (n.EndsWith("_Mouth)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.FacialHair;
            }

            if (n.EndsWith("_Neck)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Neck;
            }

            if (n.IndexOf("_Ear", StringComparison.Ordinal) >= 0)
            {
                return (int)RrAvatarSlot.Ears;
            }

            if (n.EndsWith("_Shoulder)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Back;
            }

            if (n.EndsWith("_Belt)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Belt;
            }

            if (n.EndsWith("_Wrist)", StringComparison.Ordinal))
            {
                return (int)RrAvatarSlot.Wrist;
            }

            return -1;
        }

        /// <summary>"(BB_WavyMiddlePart_Hair)" -> "Wavy Middle Part".</summary>
        private static string LabelOf(string prefab)
        {
            string s = prefab.Substring(4, prefab.Length - 5);
            int cut = s.LastIndexOf('_');
            if (cut > 0)
            {
                string tail = s.Substring(cut + 1);
                if (tail == "Hair" || tail == "Hat" || tail == "Shirt" || tail == "Eye" || tail == "Mouth" || tail == "Neck" || tail == "Shoulder" || tail == "Belt" || tail == "Wrist" || tail == "Ear")
                {
                    s = s.Substring(0, cut);
                }
            }

            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(c) && s[i - 1] != '_' && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]) && char.IsUpper(s[i - 1]))))
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        public static IReadOnlyList<Item> ItemsOf(RrAvatarSlot slot)
        {
            return Ready ? Items[(int)slot] : (IReadOnlyList<Item>)Array.Empty<Item>();
        }

        public static int IndexOf(RrAvatarSlot slot, string prefab)
        {
            if (!Ready || string.IsNullOrEmpty(prefab))
            {
                return -1;
            }

            var list = Items[(int)slot];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Prefab == prefab)
                {
                    return i;
                }
            }

            return -1;
        }

        public static Item ItemAt(RrAvatarSlot slot, int index)
        {
            if (!Ready || index < 0)
            {
                return null;
            }

            var list = Items[(int)slot];
            return index < list.Count ? list[index] : null;
        }

        public static string NameAt(RrAvatarSlot slot, int index)
        {
            var it = ItemAt(slot, index);
            return it != null ? it.Prefab : "";
        }

        public static Item Find(RrAvatarSlot slot, string prefab)
        {
            return ItemAt(slot, IndexOf(slot, prefab));
        }

        /// <summary>The item's design materials ("&lt;Item&gt;_..._Mat"), computed once per item.</summary>
        public static List<RrEntry> StylesOf(Item it)
        {
            if (it == null)
            {
                return new List<RrEntry>();
            }

            if (it.Styles != null)
            {
                return it.Styles;
            }

            it.Styles = new List<RrEntry>();
            if (_materials == null)
            {
                return it.Styles;
            }

            // "(BB_Baseball_Hat)" -> "Baseball_Hat_"; Rec Room's combination materials are "Baseball_Hat_ThreeColorBlackOrange_Mat".
            string stem = it.Prefab.Substring(4, it.Prefab.Length - 5) + "_";
            var seen = new HashSet<string>();
            for (int i = 0; i < _materials.Count; i++)
            {
                var m = _materials[i];
                // Longer than "<stem>mat": the item's own default material is not a design.
                if (m.Name.Length > stem.Length + 4 && m.Name.StartsWith(stem, StringComparison.OrdinalIgnoreCase) && m.LoaderBundle >= 0 && seen.Add(m.Name))
                {
                    it.Styles.Add(m);
                }
            }

            return it.Styles;
        }

        public static int StyleIndex(RrAvatarSlot slot, int item, string style)
        {
            if (string.IsNullOrEmpty(style))
            {
                return -1;
            }

            var styles = StylesOf(ItemAt(slot, item));
            for (int i = 0; i < styles.Count; i++)
            {
                if (styles[i].Name == style)
                {
                    return i;
                }
            }

            return -1;
        }

        public static string StyleName(RrAvatarSlot slot, int item, int style)
        {
            var styles = StylesOf(ItemAt(slot, item));
            return style >= 0 && style < styles.Count ? styles[style].Name : "";
        }

        /// <summary>A design's label: "Baseball_Hat_ThreeColorBlackOrange_Mat" -> "Three Color Black Orange".</summary>
        public static string StyleLabel(Item it, RrEntry style)
        {
            string n = style.Name;
            string stem = it.Prefab.Substring(4, it.Prefab.Length - 5) + "_";
            if (n.Length > stem.Length + 4)
            {
                n = n.Substring(stem.Length, n.Length - stem.Length - 4);
            }

            return LabelOf("(BB_" + n + ")");
        }

        public static IReadOnlyList<Style> Eyes
        {
            get { return EyeList; }
        }

        public static IReadOnlyList<Style> Mouths
        {
            get { return MouthList; }
        }

        public static void Shutdown()
        {
            for (int i = 0; i < Items.Length; i++)
            {
                Items[i] = null;
            }

            EyeList.Clear();
            MouthList.Clear();
            _materials = null;
            _built = null;
        }
    }
}
