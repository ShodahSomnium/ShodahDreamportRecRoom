using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen's palette (Rec Room's moveable palette that comes with the pen): a small cream board with the
    /// 32 colour swatches in an 8 x 4 grid and three tool keys, Erase, Undo and Clear, shown while the local player
    /// holds the pen. In a maker room the local player may build in, <see cref="RrMakerPenBuildPalette"/> is shown
    /// instead.
    ///
    /// VR: it floats above the other hand, facing the head, and is used by touching a swatch or key with the pen
    /// tip (<see cref="Touch"/>), as Rec Room's palette is. It has no colliders and no RrPressable: the empty
    /// hand under it would poke every key.
    /// Screen mode: it is a small strip low on the left of the view (world-space, placed in front of the camera
    /// every frame, never parented to it) that shows the colour and the mode; the keys are on the keyboard
    /// (see the hint line), and it hides while a menu has the input.
    /// </summary>
    public sealed class RrMakerPenPalette : MonoBehaviour
    {
        public const int Columns = 8;
        public const float Spacing = 0.034f;
        public const float SwatchRadius = 0.012f;
        public const float TouchSlack = 0.012f;
        public const float HudScale = 0.55f;
        public static readonly Vector3 HudOffset = new Vector3(-0.2f, -0.12f, 0.42f);

        public const byte KindColour = 0, KindEraser = 1, KindUndo = 2, KindClear = 3;

        private struct Key
        {
            public Transform T;
            public byte Kind;
            public byte Colour;
            public float Radius;
            public Vector3 Rest;
            public Vector3 Scale;
        }

        public RrMakerPenTool Tool;

        private readonly List<Key> _keys = new List<Key>();
        private TextMeshPro _hint;
        private Vector2 _half;
        private bool _screen;
        private bool _hintScreen = true;
        private RrHand _penHand;
        private int _touching = -1;
        private float _pressedAt = -10f;
        private byte _shownColour = 255;
        private bool _shownEraser;

        public int Presses { get; private set; }

        public static RrMakerPenPalette Create(Transform parent, RrMakerPenTool tool)
        {
            var go = new GameObject("Maker Pen Palette");
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<RrMakerPenPalette>();
            p.Tool = tool;
            p.Build();
            go.SetActive(false);
            return p;
        }

        private void Build()
        {
            var t = transform;
            int rows = (RrMakerPenWire.Palette.Length + Columns - 1) / Columns;
            float w = Columns * Spacing + 0.02f;
            float h = rows * Spacing + Spacing + 0.05f;
            _half = new Vector2(w * 0.5f, h * 0.5f);
            RrBuild.Box(t, "Board", new Vector3(0f, 0f, 0.004f), new Vector3(w, h, 0.006f), RrLook.Solid(RrLook.Cream), false);

            float x0 = -(Columns - 1) * 0.5f * Spacing;
            float y0 = h * 0.5f - 0.01f - Spacing * 0.5f;
            for (int i = 0; i < RrMakerPenWire.Palette.Length; i++)
            {
                var pos = new Vector3(x0 + (i % Columns) * Spacing, y0 - (i / Columns) * Spacing, -0.006f);
                var s = RrBuild.Sphere(t, "Swatch " + i, pos, SwatchRadius, RrLook.Solid(RrMakerPenWire.Palette[i]), false);
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _keys.Add(new Key { T = s.transform, Kind = KindColour, Colour = (byte)i, Radius = SwatchRadius, Rest = pos, Scale = s.transform.localScale });
            }

            float yTools = y0 - rows * Spacing;
            ToolKey("Erase", KindEraser, new Vector3(-0.05f, yTools, -0.004f), new Color(1f, 0.62f, 0.72f));
            ToolKey("Undo", KindUndo, new Vector3(0f, yTools, -0.004f), new Color(0.72f, 0.80f, 0.95f));
            ToolKey("Clear", KindClear, new Vector3(0.05f, yTools, -0.004f), new Color(0.95f, 0.55f, 0.45f));

            _hint = RrBuild.Sign(t, "Hint", "", new Vector3(0f, -h * 0.5f + 0.012f, -0.002f), Quaternion.identity, 0.07f, RrLook.DarkGrey, false, w - 0.01f);
            _hint.enableAutoSizing = true;
            _hint.fontSizeMin = 0.03f;
            _hint.fontSizeMax = 0.07f;
            _hint.rectTransform.sizeDelta = new Vector2(w - 0.01f, 0.02f);
            SetHint(false);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void ToolKey(string label, byte kind, Vector3 pos, Color colour)
        {
            var size = new Vector3(0.044f, 0.024f, 0.008f);
            var box = RrBuild.Box(transform, "Key " + label, pos, size, RrLook.Solid(colour), false);
            box.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var text = RrBuild.Sign(transform, "Key " + label + " Label", label, pos + new Vector3(0f, 0f, -0.007f), Quaternion.identity, 0.1f, RrLook.DarkGrey, true, 0.042f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.04f;
            text.fontSizeMax = 0.1f;
            text.rectTransform.sizeDelta = new Vector2(0.042f, 0.022f);
            _keys.Add(new Key { T = box.transform, Kind = kind, Radius = 0.014f, Rest = pos, Scale = box.transform.localScale });
        }

        private void SetHint(bool screen)
        {
            if (_hint == null || screen == _hintScreen)
            {
                return;
            }

            _hintScreen = screen;
            _hint.text = screen ? "LMB draw  Z undo  RMB erase  1/2 colour  3 eraser  4 clear" : "Touch a colour with the pen tip.  A/X undo";
        }

        /// <summary>Shows the palette for this frame's holder (placed in LateUpdate, after the hands move).</summary>
        public void Show(bool screen, RrHand penHand)
        {
            _screen = screen;
            _penHand = penHand;
            SetHint(screen);
            bool visible = !(screen && RrInput.Blocked);
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
                _touching = -1;
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            _touching = -1;
        }

        private void LateUpdate()
        {
            if (_screen)
            {
                var cam = RrPlayer.Camera;
                if (cam == null)
                {
                    return;
                }

                var ct = cam.transform;
                transform.SetPositionAndRotation(ct.position + ct.rotation * HudOffset, ct.rotation);
                transform.localScale = Vector3.one * HudScale;
            }
            else
            {
                var off = _penHand != null && _penHand.Side == RrHandSide.Left ? RrHand.Right : RrHand.Left;
                Vector3 head = RrPlayer.HeadPosition;
                Vector3 anchor = off != null && off.Tracked
                    ? off.Position + Vector3.up * 0.12f
                    : head + RrPlayer.HeadRotation * new Vector3(-0.18f, -0.25f, 0.35f);
                Vector3 look = anchor - head;
                transform.position = anchor;
                transform.rotation = look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
                transform.localScale = Vector3.one;
            }

            RefreshSelection();
        }

        /// <summary>
        /// VR: the pen tip at this world point; a key it enters is pressed (once per touch). Returns true while the
        /// tip is at the board, so the pen does not draw on it.
        /// </summary>
        public bool Touch(Vector3 tip, RrHand hand)
        {
            if (_screen || !gameObject.activeInHierarchy)
            {
                return false;
            }

            var local = transform.InverseTransformPoint(tip);
            bool near = Mathf.Abs(local.x) < _half.x + 0.02f && Mathf.Abs(local.y) < _half.y + 0.02f && local.z > -0.05f && local.z < 0.03f;
            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _keys.Count; i++)
            {
                float d = (_keys[i].T.position - tip).magnitude - _keys[i].Radius;
                if (d < TouchSlack && d < best)
                {
                    best = d;
                    hit = i;
                }
            }

            if (hit != _touching)
            {
                _touching = hit;
                if (hit >= 0 && Time.unscaledTime - _pressedAt > 0.25f)
                {
                    _pressedAt = Time.unscaledTime;
                    Press(hit);
                    if (hand != null)
                    {
                        hand.Haptic(0.3f, 0.04f);
                    }
                }
            }

            return near;
        }

        private void Press(int index)
        {
            var k = _keys[index];
            Presses++;
            RrSound.Play(RrPressable.DefaultSound, k.T.position, 0.3f);
            if (Tool == null)
            {
                return;
            }

            switch (k.Kind)
            {
                case KindColour:
                    Tool.SetColour(k.Colour);
                    break;
                case KindEraser:
                    Tool.ToggleEraser();
                    break;
                case KindUndo:
                    Tool.Undo();
                    break;
                case KindClear:
                    Tool.ClearMine();
                    break;
            }
        }

        /// <summary>The chosen swatch stands out (bigger, forward); the Erase key does while erasing.</summary>
        private void RefreshSelection()
        {
            if (Tool == null || (Tool.Colour == _shownColour && Tool.Eraser == _shownEraser))
            {
                return;
            }

            _shownColour = Tool.Colour;
            _shownEraser = Tool.Eraser;
            foreach (var k in _keys)
            {
                bool on = k.Kind == KindColour ? !Tool.Eraser && k.Colour == Tool.Colour : k.Kind == KindEraser && Tool.Eraser;
                if (k.Kind == KindColour)
                {
                    k.T.localScale = on ? k.Scale * 1.4f : k.Scale;
                    k.T.localPosition = on ? k.Rest + new Vector3(0f, 0f, -0.006f) : k.Rest;
                }
                else
                {
                    k.T.localScale = on ? Vector3.Scale(k.Scale, new Vector3(1.15f, 1.15f, 1f)) : k.Scale;
                }
            }
        }
    }
}
