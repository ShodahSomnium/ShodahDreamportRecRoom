using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// The screen-mode crosshair: Rec Room's own <c>Screens_Crosshairs_Center</c> sprite (DefaultGameplayReticleCursorSettings,
    /// bundle 1df8e0ef..., somnium_player.md §5.3) on a screen-space overlay canvas, shown only while desktop play mode is
    /// in effect. Until the sprite arrives (or if the install lacks it) a small drawn dot stands in. Overlay canvases do
    /// not reach a headset (conventions §24 item 16); play mode never runs in VR anyway.
    /// </summary>
    internal static class RrCrosshair
    {
        private const string SpriteName = "Screens_Crosshairs_Center";
        private const int SpriteClass = 213;

        private static GameObject _canvas;
        private static Image _image;
        private static Texture2D _dotTexture;
        private static Sprite _dot;
        private static bool _requested;

        internal static void Show(bool on)
        {
            if (!on)
            {
                if (_canvas != null && _canvas.activeSelf)
                {
                    _canvas.SetActive(false);
                }

                return;
            }

            if (_canvas == null)
            {
                Build();
            }

            if (_canvas != null && !_canvas.activeSelf)
            {
                _canvas.SetActive(true);
            }
        }

        private static void Build()
        {
            Transform parent = RrPlayer.PlayerObject;
            if (parent == null)
            {
                return;
            }

            _canvas = new GameObject("Rr Crosshair");
            _canvas.layer = RrLayers.Ui;
            _canvas.transform.SetParent(parent, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var go = new GameObject("Reticle");
            go.layer = RrLayers.Ui;
            go.transform.SetParent(_canvas.transform, false);
            _image = go.AddComponent<Image>();
            _image.raycastTarget = false;
            _image.sprite = Dot();
            _image.color = new Color(1f, 1f, 1f, 0.9f);
            _image.preserveAspect = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(22f, 22f);

            RrAssets assets = RrAssets.Instance;
            if (!_requested && assets != null)
            {
                _requested = true;
                assets.Load<Sprite>(SpriteName, SpriteClass, s =>
                {
                    if (s == null)
                    {
                        RrLog.WarnOnce("Screen", "crosshair", "Rec Room's " + SpriteName + " not found - drawn dot crosshair (logged once)");
                        return;
                    }

                    if (_image != null)
                    {
                        _image.sprite = s;
                        RrLog.Info("Screen", "crosshair: Rec Room's " + SpriteName + " (" + s.rect.width.ToString("0") + "x" + s.rect.height.ToString("0") + " px)");
                    }
                });
            }
        }

        /// <summary>A soft white dot with a dark rim, readable on any background.</summary>
        private static Sprite Dot()
        {
            if (_dot != null)
            {
                return _dot;
            }

            const int n = 32;
            _dotTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Rr Crosshair Dot", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    byte a = (byte)(255f * Mathf.Clamp01((1f - d) * 6f));
                    byte v = d < 0.55f ? (byte)255 : (byte)30;
                    pixels[y * n + x] = new Color32(v, v, v, a);
                }
            }

            _dotTexture.SetPixels32(pixels);
            _dotTexture.Apply(false, true);
            _dot = Sprite.Create(_dotTexture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            _dot.name = "Rr Crosshair Dot";
            return _dot;
        }

        internal static void Reset()
        {
            if (_canvas != null)
            {
                Object.Destroy(_canvas);
            }

            if (_dot != null)
            {
                Object.Destroy(_dot);
            }

            if (_dotTexture != null)
            {
                Object.Destroy(_dotTexture);
            }

            _canvas = null;
            _image = null;
            _dot = null;
            _dotTexture = null;
            _requested = false;
        }
    }
}
