using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// The local player's quest HUD (rr_quests.md 3.1 "RrQuestHud"): team gold, health, the act title card, toasts,
    /// the revive prompt/progress, the boss bar and the end screen - plus floating "+100" numbers where gold was
    /// earned. Desktop: a screen overlay. VR: the same canvas in world space (1.8 m wide, 2 m ahead), placed once
    /// in front of the player and re-placed only when they turn away or walk off (a head-welded panel cannot be read,
    /// conventions 24.15). Text uses Rec Room's font (<see cref="RrText"/>).
    /// </summary>
    public sealed class RrQuestHud : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.83f, 0.3f);

        private Canvas _canvas;
        private RectTransform _root;
        private TextMeshProUGUI _score, _health, _title, _sub, _toast, _prompt, _bossName;
        private Image _bossBack, _bossFill, _promptBar, _promptBack;
        private float _titleUntil, _toastUntil, _promptSeen = -1f;
        private bool _vr;
        private Vector3 _anchorPos;
        private float _anchorYaw;
        private readonly List<TextMeshPro> _pops = new List<TextMeshPro>();
        private readonly List<float> _popStart = new List<float>();

        public static RrQuestHud Create(Transform parent)
        {
            var go = new GameObject("Rr Quest Hud");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<RrQuestHud>();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            gameObject.layer = RrLayers.Ui;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = (RectTransform)transform;
            _score = Label("Score", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 60f), 40f, GoldColor, true);
            _health = Label("Health", new Vector2(0f, 0f), new Vector2(170f, 50f), new Vector2(300f, 60f), 34f, Color.white, true);
            _title = Label("Title", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 120f), 84f, GoldColor, true);
            _sub = Label("Sub", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1400f, 70f), 38f, Color.white, false);
            _toast = Label("Toast", new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 60f), 40f, Color.white, true);
            _prompt = Label("Prompt", new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1200f, 60f), 32f, Color.white, false);
            _promptBack = Box("Prompt Back", new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(420f, 16f), new Color(0f, 0f, 0f, 0.5f));
            _promptBar = Box("Prompt Bar", new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(420f, 16f), new Color(0.4f, 1f, 0.5f));
            _bossName = Label("Boss", new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(900f, 50f), 34f, new Color(1f, 0.5f, 0.4f), true);
            _bossBack = Box("Boss Back", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 22f), new Color(0f, 0f, 0f, 0.55f));
            _bossFill = Box("Boss Fill", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 22f), new Color(0.9f, 0.2f, 0.15f));
            SetBoss(null, -1f);
            SetPromptVisible(false);
            _title.text = _sub.text = _toast.text = "";
        }

        private TextMeshProUGUI Label(string name, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize, Color color, bool title)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = RrLayers.Ui;
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            RrText.Style(t, title);
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            t.raycastTarget = false;
            return t;
        }

        private Image Box(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = RrLayers.Ui;
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ API

        public void Title(string title, string sub, float seconds, Color? color = null)
        {
            _title.text = title ?? "";
            _title.color = color ?? GoldColor;
            _sub.text = sub ?? "";
            _titleUntil = seconds > 0f ? Time.time + seconds : float.MaxValue;
            Replace();
        }

        public void ClearTitle()
        {
            _titleUntil = 0f;
        }

        public void Toast(string text, float seconds = 2.5f)
        {
            _toast.text = text ?? "";
            _toastUntil = Time.time + seconds;
            Replace();
        }

        public void SetScore(string text)
        {
            if (_score.text != text)
            {
                _score.text = text;
            }
        }

        public void SetHealth(string text, Color color)
        {
            if (_health.text != text)
            {
                _health.text = text;
            }

            _health.color = color;
        }

        /// <summary>A prompt for this frame (call every frame while it applies); progress 0..1 shows a bar, &lt; 0 none.</summary>
        public void Prompt(string text, float progress = -1f)
        {
            _prompt.text = text;
            _promptSeen = Time.time;
            SetPromptVisible(true);
            bool bar = progress >= 0f;
            _promptBack.enabled = bar;
            _promptBar.enabled = bar;
            if (bar)
            {
                var rt = _promptBar.rectTransform;
                rt.sizeDelta = new Vector2(420f * Mathf.Clamp01(progress), 16f);
                rt.anchoredPosition = new Vector2(-210f + 210f * Mathf.Clamp01(progress), 100f);
            }
        }

        public void SetBoss(string name, float fraction)
        {
            bool on = fraction >= 0f && !string.IsNullOrEmpty(name);
            _bossName.enabled = on;
            _bossBack.enabled = on;
            _bossFill.enabled = on;
            if (on)
            {
                _bossName.text = name;
                var rt = _bossFill.rectTransform;
                rt.sizeDelta = new Vector2(700f * Mathf.Clamp01(fraction), 22f);
                rt.anchoredPosition = new Vector2(-350f + 350f * Mathf.Clamp01(fraction), -140f);
            }
        }

        /// <summary>A floating number where gold was earned ("+100"), rising and fading over 1.4 s.</summary>
        public void Pop(Vector3 world, string text, Color color)
        {
            TextMeshPro t = null;
            int slot = -1;
            for (int i = 0; i < _pops.Count; i++)
            {
                if (_pops[i] != null && !_pops[i].gameObject.activeSelf)
                {
                    t = _pops[i];
                    slot = i;
                    break;
                }
            }

            if (t == null)
            {
                if (_pops.Count >= 12)
                {
                    slot = 0;
                    t = _pops[0];
                }
                else
                {
                    t = RrBuild.Sign(transform.parent, "Pop", text, Vector3.zero, Quaternion.identity, 3f, color, true, 3f);
                    _pops.Add(t);
                    _popStart.Add(0f);
                    slot = _pops.Count - 1;
                }
            }

            t.text = text;
            t.color = color;
            t.transform.position = world;
            t.gameObject.SetActive(true);
            _popStart[slot] = Time.time;
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            float now = Time.time;
            if (now > _titleUntil && (_title.text.Length > 0 || _sub.text.Length > 0))
            {
                _title.text = "";
                _sub.text = "";
            }

            if (now > _toastUntil && _toast.text.Length > 0)
            {
                _toast.text = "";
            }

            if (_promptSeen >= 0f && now - _promptSeen > 0.25f)
            {
                _promptSeen = -1f;
                SetPromptVisible(false);
            }

            Mode();
            AnimatePops(now);
        }

        private void SetPromptVisible(bool on)
        {
            _prompt.enabled = on;
            if (!on)
            {
                _promptBack.enabled = false;
                _promptBar.enabled = false;
            }
        }

        private void Mode()
        {
            bool vr = RrPlayer.InVr;
            if (vr != _vr)
            {
                _vr = vr;
                _canvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                if (vr)
                {
                    _root.sizeDelta = new Vector2(1600f, 900f);
                    _root.localScale = Vector3.one * 0.00115f;
                    _canvas.worldCamera = RrPlayer.Camera;
                    Replace();
                }
                else
                {
                    _root.localScale = Vector3.one;
                }
            }

            if (!_vr)
            {
                return;
            }

            // Re-place when the player turned more than 40 degrees away or walked 1.2 m.
            Vector3 head = RrPlayer.HeadPosition;
            float yaw = RrPlayer.Yaw;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, _anchorYaw)) > 40f || Vector3.Distance(head, _anchorPos) > 1.2f)
            {
                Replace();
            }
        }

        private void Replace()
        {
            if (!_vr)
            {
                return;
            }

            Vector3 head = RrPlayer.HeadPosition;
            float yaw = RrPlayer.Yaw;
            _anchorPos = head;
            _anchorYaw = yaw;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            transform.SetPositionAndRotation(head + rot * new Vector3(0f, -0.15f, 2f), rot);
        }

        private void AnimatePops(float now)
        {
            var cam = RrPlayer.Camera;
            for (int i = 0; i < _pops.Count; i++)
            {
                var t = _pops[i];
                if (t == null || !t.gameObject.activeSelf)
                {
                    continue;
                }

                float k = (now - _popStart[i]) / 1.4f;
                if (k >= 1f)
                {
                    t.gameObject.SetActive(false);
                    continue;
                }

                t.transform.position += Vector3.up * (Time.deltaTime * 0.8f);
                if (cam != null)
                {
                    t.transform.rotation = Quaternion.LookRotation(t.transform.position - cam.transform.position);
                }

                var c = t.color;
                c.a = 1f - k * k;
                t.color = c;
            }
        }
    }
}
