using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// A small keyboard on the watch page (Rec Room's room-name dialog had the system keyboard; Dreamport has none): digits,
    /// letters, a few marks, shift, space, delete, Cancel and OK, all ordinary watch buttons, so it works with the VR ray and
    /// the desktop mouse alike. On a screen the physical keyboard types too while it is open (<see cref="Typing"/> keeps the
    /// watch's H / Backspace keys from closing or going back meanwhile).
    /// </summary>
    public sealed class RrWatchKeyboard
    {
        private static RrWatchKeyboard _active;

        /// <summary>A keyboard is open on the watch page that is showing: the menu's own keys (H, Backspace) stand aside.</summary>
        public static bool Typing
        {
            get { return _active != null && _active.Root != null && _active.Root.gameObject.activeInHierarchy && RrWatch.IsOpen; }
        }

        private const float KeyW = 118f;
        private const float KeyH = 84f;
        private const float Gap = 10f;

        private static readonly string[] Rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL'", "ZXCVBNM-.!" };

        public RectTransform Root;
        public int MaxLength = 40;

        private TextMeshProUGUI _title;
        private TextMeshProUGUI _field;
        private TextMeshProUGUI _shiftLabel;
        private readonly List<KeyValuePair<char, TextMeshProUGUI>> _letters = new List<KeyValuePair<char, TextMeshProUGUI>>();
        private readonly StringBuilder _text = new StringBuilder();
        private bool _shift = true;
        private Action<string> _ok;
        private float _blink;

        public bool IsOpen
        {
            get { return Root != null && Root.gameObject.activeSelf; }
        }

        /// <summary>Builds the (hidden) keyboard as an overlay over a whole page.</summary>
        public static RrWatchKeyboard Build(RectTransform page)
        {
            var k = new RrWatchKeyboard();
            k.Root = RrUiKit.Fill(page, "Keyboard");
            RrUiKit.Solid(k.Root, RrUiKit.WithAlpha(RrUiKit.Page, 0.97f), true);
            float width = 10 * KeyW + 9 * Gap;
            float x0 = (RrWatchPage.Width - width) * 0.5f;
            k._title = RrUiKit.Label(k.Root, "Title", "", x0, 40f, width, 60f, 40f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft, true, true);
            var field = RrUiKit.TopLeft(k.Root, "Field", x0, 110f, width, 96f);
            RrUiKit.Rounded(field, Color.white, 20f);
            k._field = RrUiKit.Text(RrUiKit.Fill(field, "Text", 30f, 0f, 30f, 0f), "", 42f, RrUiKit.Ink, TextAlignmentOptions.MidlineLeft);
            k._field.overflowMode = TextOverflowModes.Ellipsis;

            float y = 236f;
            foreach (var row in Rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    var b = RrUiKit.Secondary(k.Root, "Key " + c, c.ToString(), x0 + i * (KeyW + Gap), y, KeyW, KeyH, 36f);
                    b.onClick.AddListener(() => k.Type(c));
                    if (char.IsLetter(c))
                    {
                        k._letters.Add(new KeyValuePair<char, TextMeshProUGUI>(c, b.Label));
                    }
                }

                y += KeyH + Gap;
            }

            var shift = RrUiKit.Secondary(k.Root, "Shift", "Shift", x0, y, KeyW * 2f + Gap, KeyH, 28f);
            k._shiftLabel = shift.Label;
            shift.onClick.AddListener(() =>
            {
                k._shift = !k._shift;
                k.ShowCase();
            });
            var space = RrUiKit.Secondary(k.Root, "Space", "Space", x0 + 2f * (KeyW + Gap), y, KeyW * 6f + Gap * 5f, KeyH, 28f);
            space.onClick.AddListener(() => k.Type(' '));
            var del = RrUiKit.Secondary(k.Root, "Delete", "Delete", x0 + 8f * (KeyW + Gap), y, KeyW * 2f + Gap, KeyH, 28f);
            del.onClick.AddListener(k.Backspace);
            y += KeyH + Gap + 16f;

            var cancel = RrUiKit.Secondary(k.Root, "Cancel", "Cancel", x0, y, 400f, 96f, 32f);
            cancel.onClick.AddListener(k.Close);
            var ok = RrUiKit.Primary(k.Root, "OK", "OK", x0 + width - 400f, y, 400f, 96f, 36f);
            ok.onClick.AddListener(k.Submit);
            RrUiKit.UiLayer(k.Root.gameObject);
            k.Root.gameObject.SetActive(false);
            return k;
        }

        /// <summary>Opens with a title and the current text; ok gets the text when OK is pressed.</summary>
        public void Open(string title, string text, Action<string> ok)
        {
            _title.text = title ?? "";
            _text.Length = 0;
            _text.Append(text ?? "");
            _ok = ok;
            _shift = _text.Length == 0;
            ShowCase();
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            _active = this;
            Show();
        }

        public void Close()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }

            _ok = null;
            if (_active == this)
            {
                _active = null;
            }
        }

        private void Submit()
        {
            var ok = _ok;
            string text = _text.ToString().Trim();
            Close();
            if (ok != null && text.Length > 0)
            {
                ok(text);
            }
        }

        private void Type(char c)
        {
            if (_text.Length >= MaxLength)
            {
                return;
            }

            if (char.IsLetter(c))
            {
                c = _shift ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                if (_shift)
                {
                    _shift = false;
                    ShowCase();
                }
            }

            if (c == ' ' && (_text.Length == 0 || _text[_text.Length - 1] == ' '))
            {
                return;
            }

            _text.Append(c);
            if (c == ' ')
            {
                _shift = true;
                ShowCase();
            }

            Show();
        }

        private void Backspace()
        {
            if (_text.Length > 0)
            {
                _text.Length--;
            }

            Show();
        }

        private void ShowCase()
        {
            foreach (var kv in _letters)
            {
                if (kv.Value != null)
                {
                    kv.Value.text = (_shift ? char.ToUpperInvariant(kv.Key) : char.ToLowerInvariant(kv.Key)).ToString();
                }
            }

            if (_shiftLabel != null)
            {
                _shiftLabel.text = _shift ? "SHIFT ON" : "SHIFT";
            }
        }

        private void Show()
        {
            if (_field != null)
            {
                bool caret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
                _field.text = _text + (caret ? "|" : " ");
            }
        }

        /// <summary>From the page's Tick: the caret, and on a screen the physical keyboard.</summary>
        public void Tick()
        {
            if (!IsOpen)
            {
                return;
            }

            if (Time.unscaledTime >= _blink)
            {
                _blink = Time.unscaledTime + 0.3f;
                Show();
            }

            if (RrWatch.VrLayout)
            {
                return;
            }

            string typed;
            try
            {
                typed = Input.inputString;
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(typed))
            {
                return;
            }

            foreach (char c in typed)
            {
                if (c == '\b')
                {
                    Backspace();
                }
                else if (c == '\n' || c == '\r')
                {
                    Submit();
                    return;
                }
                else if (!char.IsControl(c) && c != '<' && c != '>' && _text.Length < MaxLength)
                {
                    if (c == ' ' && (_text.Length == 0 || _text[_text.Length - 1] == ' '))
                    {
                        continue;
                    }

                    _text.Append(c);
                    _shift = false;
                    ShowCase();
                }
            }

            Show();
        }

        /// <summary>The watch closes or the world goes: no keyboard is open any more.</summary>
        public static void Reset()
        {
            _active = null;
        }
    }
}
