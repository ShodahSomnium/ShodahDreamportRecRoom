using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Avatar (iteration 15): choose between your Somnium avatar (the default) and a classic Rec Room avatar dressed from
    /// your own install, and dress it. Rec Room's avatar customisation in basic form: category tabs on the left (Skin,
    /// Eyes, Mouth, then the outfit slots), the choices in the middle (items by name - the install has no item
    /// thumbnails; eyes and mouths show their own textures; skin tones as swatches), and on the right the selected
    /// item's dye swatches and its designs (Rec Room's own variant materials). While the page shows, a preview of the
    /// outfit stands in front of you (the Dorm mirror shows you too). Everything is saved on this PC and others see it.
    /// </summary>
    public sealed class RrWatchAvatarPage : RrWatchPage
    {
        private enum Tab
        {
            Skin,
            Eyes,
            Mouth,
            Slot
        }

        private const float Top = 140f;
        private const float TabsX = Margin;
        private const float TabsW = 300f;
        private const float GridX = Margin + TabsW + 30f;
        private const float GridW = 900f;
        private const float SideX = GridX + GridW + 30f;
        private const float SideW = Width - Margin - SideX;
        private const float ListH = Height - Top - 40f;

        private RrUiButton _somnium;
        private RrUiButton _recRoom;
        private TextMeshProUGUI _status;
        private RectTransform _grid;
        private ScrollRect _gridScroll;
        private RectTransform _side;
        private RectTransform _designs;
        private ScrollRect _designScroll;
        private TextMeshProUGUI _sideTitle;
        private readonly List<KeyValuePair<RrUiButton, int>> _tabs = new List<KeyValuePair<RrUiButton, int>>();
        private readonly List<KeyValuePair<RrUiButton, int>> _cards = new List<KeyValuePair<RrUiButton, int>>();
        private readonly Dictionary<RrUiButton, Image> _rings = new Dictionary<RrUiButton, Image>();
        private readonly List<KeyValuePair<Image, int>> _swatches = new List<KeyValuePair<Image, int>>();
        private readonly List<KeyValuePair<RrUiButton, string>> _designButtons = new List<KeyValuePair<RrUiButton, string>>();
        private Tab _tab = Tab.Slot;
        private RrAvatarSlot _slot = RrAvatarSlot.Hair;
        private string _builtFor = "";
        private string _designsFor = "";
        private bool _wasReady;

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Avatar; }
        }

        public override void Build()
        {
            Title("Avatar");
            _somnium = Chip(Root, "Wear Somnium avatar", Width - Margin - 900f, 40f, 440f);
            _recRoom = Chip(Root, "Wear Rec Room avatar", Width - Margin - 440f, 40f, 440f);
            _somnium.onClick.AddListener(() =>
            {
                RrAvatars.SetWear(false);
                RrWatchSounds.Play(RrWatchSounds.ToggleOff);
                Refresh();
            });
            _recRoom.onClick.AddListener(() =>
            {
                RrAvatars.SetWear(true);
                RrWatchSounds.Play(RrWatchSounds.ToggleOn);
                Refresh();
            });

            // Category tabs.
            float y = Top;
            AddTab("Skin", (int)Tab.Skin * 100, ref y);
            AddTab("Eyes", (int)Tab.Eyes * 100, ref y);
            AddTab("Mouth", (int)Tab.Mouth * 100, ref y);
            for (int i = 0; i < RrAvatarOutfit.SlotCount; i++)
            {
                AddTab(RrAvatarCatalog.SlotLabels[i], (int)Tab.Slot * 100 + i, ref y);
            }

            _gridScroll = RrUiKit.Scroll(Root, "Choices", GridX, Top, GridW, ListH, out _grid);
            var back = RrUiKit.TopLeft(Root, "Choices Back", GridX, Top, GridW, ListH);
            RrUiKit.Rounded(back, RrUiKit.WithAlpha(Color.white, 0.55f), 20f);
            back.SetAsFirstSibling();

            _side = RrUiKit.TopLeft(Root, "Side", SideX, Top, SideW, ListH);
            RrUiKit.Rounded(_side, Color.white, 20f);
            _sideTitle = RrUiKit.Label(_side, "Side Title", "Colour", 24f, 14f, SideW - 48f, 50f, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            BuildSwatches();
            RrUiKit.Label(_side, "Design Title", "Design", 24f, 430f, SideW - 48f, 50f, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true);
            _designScroll = RrUiKit.Scroll(_side, "Designs", 12f, 480f, SideW - 24f, ListH - 560f, out _designs);
            _status = RrUiKit.Label(_side, "Status", "", 24f, ListH - 70f, SideW - 48f, 56f, 22f, RrUiKit.InkSoft, TextAlignmentOptions.MidlineLeft, false, false, true);
            RrUiKit.UiLayer(Root.gameObject);
        }

        private void AddTab(string label, int code, ref float y)
        {
            var b = Chip(Root, label, TabsX, y, TabsW, 58f, 22f);
            int c = code;
            b.onClick.AddListener(() =>
            {
                _tab = (Tab)(c / 100);
                if (_tab == Tab.Slot)
                {
                    _slot = (RrAvatarSlot)(c % 100);
                }

                _builtFor = "";
                Refresh();
            });
            _tabs.Add(new KeyValuePair<RrUiButton, int>(b, code));
            y += 64f;
        }

        private static RrUiButton Chip(Transform parent, string text, float x, float y, float w, float h = 70f, float textSize = 24f)
        {
            var rt = RrUiKit.TopLeft(parent, "Chip " + text, x, y, w, h);
            var img = RrUiKit.Rounded(RrUiKit.Fill(rt, "Background"), Color.white, 100f, true);
            var outline = RrUiKit.Rounded(RrUiKit.Fill(rt, "Border", -3f, -3f, -3f, -3f), RrUiKit.WithAlpha(RrUiKit.SecondaryText, 0.6f), 100f);
            outline.transform.SetAsFirstSibling();
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.Sound = RrUiSound.Default;
            b.Label = RrUiKit.Text(RrUiKit.Fill(rt, "Label", 12f, 0f, 12f, 0f), text.ToUpperInvariant(), textSize, RrUiKit.SecondaryText, TextAlignmentOptions.Center, true);
            Select(b, false);
            return b;
        }

        private static void Select(RrUiButton b, bool selected)
        {
            b.FillStates = selected
                ? new[] { RrUiKit.PrimaryTo, RrUiKit.PrimaryHoverTo, RrUiKit.PrimaryPressedTo }
                : new[] { Color.white, RrUiKit.SecondaryHover, RrUiKit.SecondaryPressed };
            if (b.Label != null)
            {
                b.Label.color = selected ? Color.white : RrUiKit.SecondaryText;
            }

            b.Refresh();
        }

        // ------------------------------------------------------------------ swatches (dye)

        private void BuildSwatches()
        {
            const float size = 54f;
            const float gap = 8f;
            int perRow = Mathf.Max(1, (int)((SideW - 48f + gap) / (size + gap)));
            int n = RrAvatarOutfit.Palette.Length + 1;
            for (int i = 0; i < n; i++)
            {
                float x = 24f + (i % perRow) * (size + gap);
                float y = 70f + (i / perRow) * (size + gap);
                var rt = RrUiKit.TopLeft(_side, "Swatch " + i, x, y, size, size);
                var ring = RrUiKit.Rounded(RrUiKit.Fill(rt, "Ring", -5f, -5f, -5f, -5f), RrUiKit.PrimaryTo, 18f);
                ring.enabled = false;
                var fill = RrUiKit.Rounded(RrUiKit.Fill(rt, "Fill"), i == 0 ? Color.white : RrUiKit.WithAlpha(RrAvatarOutfit.Palette[i - 1], 1f), 14f, true);
                if (i == 0)
                {
                    RrUiKit.Label(rt, "Original", "ORIG", 0f, 0f, size, size, 14f, RrUiKit.Ink, TextAlignmentOptions.Center, true);
                }

                var b = rt.gameObject.AddComponent<RrUiButton>();
                b.targetGraphic = fill;
                b.Sound = RrUiSound.Default;
                int color = i;
                b.onClick.AddListener(() => PickColor(color));
                _swatches.Add(new KeyValuePair<Image, int>(ring, i));
            }
        }

        private void PickColor(int color)
        {
            if (_tab == Tab.Skin)
            {
                // The skin tab uses the swatch row for tones (index 1.. = tone index 0..).
                return;
            }

            if (_tab != Tab.Slot)
            {
                return;
            }

            var slot = _slot;
            RrAvatars.Edit(o => o.Slot(slot).color = color);
            Refresh();
        }

        // ------------------------------------------------------------------ the middle list

        private void Rebuild()
        {
            foreach (var kv in _cards)
            {
                if (kv.Key != null)
                {
                    UnityEngine.Object.Destroy(kv.Key.gameObject);
                }
            }

            _cards.Clear();
            _rings.Clear();
            for (int i = _grid.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_grid.GetChild(i).gameObject);
            }

            float h = 0f;
            switch (_tab)
            {
                case Tab.Skin:
                    h = Swatches(RrAvatarOutfit.SkinTones, i => RrAvatars.Edit(o => o.skin = i));
                    break;
                case Tab.Eyes:
                    h = Pictures(RrAvatarCatalog.Eyes, i => RrAvatars.Edit(o => o.eyes = i));
                    break;
                case Tab.Mouth:
                    h = Pictures(RrAvatarCatalog.Mouths, i => RrAvatars.Edit(o => o.mouth = i));
                    break;
                default:
                    h = ItemCards();
                    break;
            }

            _grid.sizeDelta = new Vector2(0f, Mathf.Max(h, ListH));
            _grid.anchoredPosition = Vector2.zero;
            RrUiKit.UiLayer(_grid.gameObject);
        }

        private float ItemCards()
        {
            var slot = _slot;
            var items = RrAvatarCatalog.ItemsOf(slot);
            const float w = 270f;
            const float hh = 76f;
            const float gap = 12f;
            const int perRow = 3;
            int count = items.Count + 1;
            for (int i = 0; i < count; i++)
            {
                float x = 24f + (i % perRow) * (w + gap);
                float y = 24f + (i / perRow) * (hh + gap);
                string label = i == 0 ? "None" : items[i - 1].Label;
                var b = Card(_grid, label, x, y, w, hh);
                int index = i - 1;
                b.onClick.AddListener(() =>
                {
                    string prefab = index >= 0 ? RrAvatarCatalog.NameAt(slot, index) : "";
                    RrAvatars.Edit(o =>
                    {
                        var c = o.Slot(slot);
                        if (c.item != prefab)
                        {
                            c.item = prefab;
                            c.style = "";
                        }
                    });
                    Refresh();
                });
                _cards.Add(new KeyValuePair<RrUiButton, int>(b, index));
            }

            return 24f + ((count + perRow - 1) / perRow) * (hh + gap) + 24f;
        }

        private static RrUiButton Card(RectTransform parent, string label, float x, float y, float w, float h)
        {
            var rt = RrUiKit.TopLeft(parent, "Card " + label, x, y, w, h);
            var img = RrUiKit.Rounded(RrUiKit.Fill(rt, "Background"), Color.white, 18f, true);
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.Sound = RrUiSound.Card;
            b.Label = RrUiKit.Text(RrUiKit.Fill(rt, "Label", 14f, 0f, 14f, 0f), label, 24f, RrUiKit.Ink, TextAlignmentOptions.Center, true);
            Select(b, false);
            return b;
        }

        private float Swatches(Color[] colors, Action<int> pick)
        {
            const float size = 120f;
            const float gap = 18f;
            const int perRow = 6;
            for (int i = 0; i < colors.Length; i++)
            {
                float x = 24f + (i % perRow) * (size + gap);
                float y = 24f + (i / perRow) * (size + gap);
                var rt = RrUiKit.TopLeft(_grid, "Tone " + i, x, y, size, size);
                var ring = RrUiKit.Rounded(RrUiKit.Fill(rt, "Ring", -7f, -7f, -7f, -7f), RrUiKit.PrimaryTo, 30f);
                var fill = RrUiKit.Rounded(RrUiKit.Fill(rt, "Fill"), colors[i], 24f, true);
                var b = rt.gameObject.AddComponent<RrUiButton>();
                b.targetGraphic = fill;
                b.Sound = RrUiSound.Default;
                _rings[b] = ring;
                int index = i;
                b.onClick.AddListener(() =>
                {
                    pick(index);
                    Refresh();
                });
                _cards.Add(new KeyValuePair<RrUiButton, int>(b, i));
            }

            return 24f + ((colors.Length + perRow - 1) / perRow) * (size + gap) + 24f;
        }

        private float Pictures(IReadOnlyList<RrAvatarCatalog.Style> styles, Action<int> pick)
        {
            const float size = 150f;
            const float gap = 16f;
            const int perRow = 5;
            for (int i = 0; i < styles.Count; i++)
            {
                float x = 24f + (i % perRow) * (size + gap);
                float y = 24f + (i / perRow) * (size + gap);
                var rt = RrUiKit.TopLeft(_grid, "Style " + i, x, y, size, size);
                var ring = RrUiKit.Rounded(RrUiKit.Fill(rt, "Ring", -6f, -6f, -6f, -6f), RrUiKit.PrimaryTo, 26f);
                var fill = RrUiKit.Rounded(RrUiKit.Fill(rt, "Fill"), RrAvatarOutfit.Skin(RrAvatars.Outfit.skin), 22f, true);
                var pic = RrUiKit.Fill(rt, "Picture", 14f, 14f, 14f, 14f).gameObject.AddComponent<RawImage>();
                pic.raycastTarget = false;
                pic.enabled = false;
                var entry = styles[i].Eye;
                var assets = RrAssets.Instance;
                if (assets != null)
                {
                    int was = assets.Scope;
                    assets.Scope = 0;
                    assets.Load<Texture2D>(entry, t =>
                    {
                        if (pic != null && t != null)
                        {
                            pic.texture = t;
                            pic.enabled = true;
                        }
                    });
                    assets.Scope = was;
                }

                var b = rt.gameObject.AddComponent<RrUiButton>();
                b.targetGraphic = fill;
                b.Sound = RrUiSound.Default;
                _rings[b] = ring;
                int index = i;
                b.onClick.AddListener(() =>
                {
                    pick(index);
                    Refresh();
                });
                _cards.Add(new KeyValuePair<RrUiButton, int>(b, i));
            }

            return 24f + ((styles.Count + perRow - 1) / perRow) * (size + gap) + 24f;
        }

        // ------------------------------------------------------------------ designs (right column)

        private void RebuildDesigns()
        {
            foreach (var kv in _designButtons)
            {
                if (kv.Key != null)
                {
                    UnityEngine.Object.Destroy(kv.Key.gameObject);
                }
            }

            _designButtons.Clear();
            float h = 0f;
            if (_tab == Tab.Slot)
            {
                var slot = _slot;
                var item = RrAvatarCatalog.Find(slot, RrAvatars.Outfit.Slot(slot).item);
                if (item != null)
                {
                    var styles = RrAvatarCatalog.StylesOf(item);
                    float w = SideW - 60f;
                    for (int i = 0; i <= styles.Count; i++)
                    {
                        string name = i == 0 ? "" : styles[i - 1].Name;
                        string label = i == 0 ? "Default" : RrAvatarCatalog.StyleLabel(item, styles[i - 1]);
                        var b = Card(_designs, label, 12f, 8f + i * 64f, w, 56f);
                        b.Label.fontSize = 20f;
                        string pickName = name;
                        b.onClick.AddListener(() =>
                        {
                            RrAvatars.Edit(o => o.Slot(slot).style = pickName);
                            Refresh();
                        });
                        _designButtons.Add(new KeyValuePair<RrUiButton, string>(b, name));
                    }

                    h = 16f + (styles.Count + 1) * 64f;
                }
            }

            _designs.sizeDelta = new Vector2(0f, Mathf.Max(h, ListH - 560f));
            _designs.anchoredPosition = Vector2.zero;
            RrUiKit.UiLayer(_designs.gameObject);
        }

        // ------------------------------------------------------------------ state

        public override void Show()
        {
            _builtFor = "";
            _designsFor = "";
            base.Show();
        }

        public override void Refresh()
        {
            if (_somnium == null)
            {
                return;
            }

            var o = RrAvatars.Outfit;
            Select(_somnium, !o.wear);
            Select(_recRoom, o.wear);
            foreach (var kv in _tabs)
            {
                var tab = (Tab)(kv.Value / 100);
                Select(kv.Key, tab == _tab && (tab != Tab.Slot || (RrAvatarSlot)(kv.Value % 100) == _slot));
            }

            bool ready = RrAvatars.Ready;
            string key = ready ? _tab + ":" + _slot : "";
            if (key != _builtFor)
            {
                _builtFor = key;
                if (ready)
                {
                    Rebuild();
                }
            }

            // Which choice is picked.
            int picked = _tab == Tab.Skin ? o.skin : _tab == Tab.Eyes ? o.eyes : _tab == Tab.Mouth ? o.mouth : RrAvatarCatalog.IndexOf(_slot, o.Slot(_slot).item);
            foreach (var kv in _cards)
            {
                if (kv.Key == null)
                {
                    continue;
                }

                bool on = kv.Value == picked;
                Image ring;
                if (_rings.TryGetValue(kv.Key, out ring))
                {
                    if (ring != null)
                    {
                        ring.enabled = on;
                    }
                }
                else
                {
                    Select(kv.Key, on);
                }
            }

            // Dye and design apply to the selected outfit slot.
            bool slotTab = _tab == Tab.Slot;
            int color = slotTab ? o.Slot(_slot).color : -1;
            foreach (var kv in _swatches)
            {
                kv.Key.enabled = slotTab && kv.Value == color;
                kv.Key.transform.parent.gameObject.SetActive(slotTab);
            }

            _sideTitle.text = slotTab ? RrAvatarCatalog.SlotLabels[(int)_slot] + " colour" : "";
            string designKey = slotTab ? _slot + ":" + o.Slot(_slot).item : "";
            if (designKey != _designsFor && ready)
            {
                _designsFor = designKey;
                RebuildDesigns();
            }

            string style = slotTab ? o.Slot(_slot).style : null;
            foreach (var kv in _designButtons)
            {
                if (kv.Key != null)
                {
                    Select(kv.Key, kv.Value == style);
                }
            }

            _status.text = !ready
                ? "Reading Rec Room's avatar items from your install..."
                : o.wear
                    ? "You are a Rec Room avatar. Others see it too. Your preview stands in front of you."
                    : "You wear your Somnium avatar. Dress the preview, then pick WEAR REC ROOM AVATAR.";
        }

        public override void Tick()
        {
            RrAvatars.WantPreview();
            if (!_wasReady && RrAvatars.Ready)
            {
                _wasReady = true;
                Refresh();
            }
        }
    }
}
