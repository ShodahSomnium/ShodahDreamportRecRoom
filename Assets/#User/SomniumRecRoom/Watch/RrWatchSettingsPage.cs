using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Settings (rr_watch_ui.md §3.1): what Dreamport can honour of Rec Room's settings tabs - the three volumes
    /// (interface, sound effects, music) and, for headsets, which wrist wears the watch, whether it opens by
    /// looking at it or by tapping it, and the clock's second hand. Locomotion and snap turn belong to the Somnium
    /// client, so they are not here. Every change is saved on this PC (<see cref="RrWatchSettings"/>:
    /// persistentDataPath/SomniumRecRoom/settings.json) and restored when the world starts.
    ///
    /// Rows are Rec Room's settings rows: label on the left in Roboto 32 #393939 on a white rounded row (r 20),
    /// the control on the right; sliders have a #DAD3CE track with an orange fill and a white knob, choices are
    /// a pair of chips (selected = orange with white text, the other white with #D45427 text).
    /// </summary>
    public sealed class RrWatchSettingsPage : RrWatchPage
    {
        private const float RowHeight = 84f;
        private const float RowGap = 10f;
        private const float RowWidth = 1180f;

        private readonly List<Action> _sync = new List<Action>();
        private float _y;
        private float _x;

        public override RrWatchPageId Id
        {
            get { return RrWatchPageId.Settings; }
        }

        public override void Build()
        {
            Title("Settings");
            _x = (Width - RowWidth) * 0.5f;
            _y = 120f;

            Section("Audio");
            Slider("Interface volume", () => RrWatchSounds.Volume, v => RrWatchSounds.Volume = v);
            Slider("Sound effects volume", () => RrSound.Volume, v => RrSound.Volume = v);
            Slider("Music volume", () => RrMusic.Volume, v => RrMusic.Volume = v);

            // Live tuning of Rec Room's post-processing on top of the base correction (RrPostFx): the middle is the
            // default; the values go to the log, so a tester can send the ones that look right.
            Section("Look");
            Slider("Brightness", () => RrPostFx.UserExposure * 0.5f + 0.5f, v => RrPostFx.UserExposure = (v - 0.5f) * 2f, v => ((v - 0.5f) * 2f).ToString("+0.0;-0.0;0.0") + " EV");
            Slider("Warmth", () => RrPostFx.UserWarmth / 60f + 0.5f, v => RrPostFx.UserWarmth = (v - 0.5f) * 60f, v => ((v - 0.5f) * 60f).ToString("+0;-0;0"));

            Section("Watch (VR)");
            Choice("Open the menu by", "Looking", "Tapping", () => RrWatch.TapToOpen, v => RrWatch.TapToOpen = v);
            Choice("Wear the watch on", "Left wrist", "Right wrist", () => RrWatch.RightWrist, v => RrWatch.RightWrist = v);
            Choice("Clock second hand", "Hidden", "Shown", () => RrWatch.ShowSeconds, v => RrWatch.ShowSeconds = v);

            _y += RowGap;
            RrUiKit.Label(Root, "Build", "Dreamport: Rec Room  ·  " + RrVersion.Label + "  ·  " + RrVersion.Summary, _x, _y, RowWidth, 40f, 24f, RrUiKit.InkSoft, TextAlignmentOptions.MidlineLeft);
        }

        public override void Refresh()
        {
            foreach (var s in _sync)
            {
                s();
            }
        }

        public override void Tick()
        {
            RrWatchSettings.Flush(false);
        }

        private void Section(string text)
        {
            RrUiKit.Label(Root, "Section " + text, text, _x, _y, RowWidth, 50f, 32f, RrUiKit.InkSoft, TextAlignmentOptions.MidlineLeft, true);
            _y += 48f;
        }

        private RectTransform Row(string label)
        {
            var row = RrUiKit.TopLeft(Root, "Row " + label, _x, _y, RowWidth, RowHeight);
            RrUiKit.Rounded(row, Color.white, 20f);
            RrUiKit.Label(row, "Label", label, 40f, 0f, 520f, RowHeight, 32f, RrUiKit.Ink);
            _y += RowHeight + RowGap;
            return row;
        }

        private void Slider(string label, Func<float> get, Action<float> set, Func<float, string> format = null)
        {
            if (format == null)
            {
                format = v => Mathf.RoundToInt(v * 100f) + "%";
            }

            var row = Row(label);
            var rt = RrUiKit.TopLeft(row, "Slider", 580f, 0f, 480f, RowHeight);
            var track = RrUiKit.TopLeft(rt, "Track", 0f, (RowHeight - 16f) * 0.5f, 480f, 16f);
            RrUiKit.Rounded(track, RrUiKit.Track, 8f, true);
            var fillArea = RrUiKit.Fill(track, "Fill Area");
            var fill = RrUiKit.Fill(fillArea, "Fill");
            var fillImg = RrUiKit.Rounded(fill, Color.white, 8f);
            RrUiKit.Gradient(fillImg, RrUiKit.PrimaryFrom, RrUiKit.PrimaryTo, 0f);
            // The slider stretches the handle over the slide area's height, so the area is the knob's height and the
            // handle only sets its width (Unity's default slider layout).
            var handleArea = RrUiKit.TopLeft(rt, "Handle Slide Area", 22f, (RowHeight - 44f) * 0.5f, 480f - 44f, 44f);
            var handle = RrUiKit.Centered(handleArea, "Handle", 0f, 0f, 44f, 0f);
            var knob = RrUiKit.Circle(handle, Color.white);
            knob.raycastTarget = true;
            var ring = RrUiKit.Rounded(RrUiKit.Fill(handle, "Ring", -3f, -3f, -3f, -3f), RrUiKit.WithAlpha(RrUiKit.Ink, 0.25f), 100f);
            ring.transform.SetAsFirstSibling();
            var value = RrUiKit.Label(row, "Value", "", 1080f, 0f, 90f, RowHeight, 30f, RrUiKit.Ink, TextAlignmentOptions.MidlineRight);

            var slider = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = knob;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Rec Room's slider sounds: a tick on grab and on release.
            var trigger = rt.gameObject.AddComponent<RrWatchSliderSounds>();
            trigger.enabled = true;

            bool syncing = false;
            slider.onValueChanged.AddListener(v =>
            {
                value.text = format(v);
                if (!syncing)
                {
                    set(v);
                    RrWatchSettings.Changed();
                }
            });

            _sync.Add(() =>
            {
                syncing = true;
                slider.value = Mathf.Clamp01(get());
                value.text = format(slider.value);
                syncing = false;
            });
        }

        private void Choice(string label, string off, string on, Func<bool> get, Action<bool> set)
        {
            var row = Row(label);
            var a = Chip(row, off, 580f);
            var b = Chip(row, on, 830f);
            Action sync = () =>
            {
                bool v = get();
                Select(a, !v);
                Select(b, v);
            };

            a.onClick.AddListener(() =>
            {
                set(false);
                RrWatchSettings.Changed();
                RrWatchSounds.Play(RrWatchSounds.ToggleOff);
                sync();
            });
            b.onClick.AddListener(() =>
            {
                set(true);
                RrWatchSettings.Changed();
                RrWatchSounds.Play(RrWatchSounds.ToggleOn);
                sync();
            });
            _sync.Add(sync);
        }

        private static RrUiButton Chip(RectTransform row, string text, float x)
        {
            var rt = RrUiKit.TopLeft(row, "Chip " + text, x, 20f, 230f, RowHeight - 40f);
            var img = RrUiKit.Rounded(RrUiKit.Fill(rt, "Background"), Color.white, 100f, true);
            var outline = RrUiKit.Rounded(RrUiKit.Fill(rt, "Border", -3f, -3f, -3f, -3f), RrUiKit.WithAlpha(RrUiKit.SecondaryText, 0.6f), 100f);
            outline.transform.SetAsFirstSibling();
            var b = rt.gameObject.AddComponent<RrUiButton>();
            b.targetGraphic = img;
            b.Fill = img;
            b.Sound = RrUiSound.Default;
            b.Label = RrUiKit.Text(RrUiKit.Fill(rt, "Label"), text.ToUpperInvariant(), 24f, RrUiKit.SecondaryText, TextAlignmentOptions.Center, true);
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
    }

    /// <summary>The watch's settings as saved on this PC (JsonUtility, readable; unknown or missing fields keep their defaults).</summary>
    [Serializable]
    public sealed class RrWatchSettingsFile
    {
        public int Format = 1;
        public float InterfaceVolume = 1f;
        public float EffectsVolume = 1f;
        public float MusicVolume = 0.6f;

        /// <summary>Look: <see cref="RrPostFx.UserExposure"/> (EV, -1..1) and <see cref="RrPostFx.UserWarmth"/> (-30..30).</summary>
        public float Brightness;
        public float Warmth;

        public bool TapToOpen;
        public bool RightWrist;
        public bool ShowSeconds = true;
    }

    /// <summary>
    /// Saves the Settings page's values locally - Application.persistentDataPath/SomniumRecRoom/settings.json, next to
    /// avatar.json and the MakerRooms folder (nothing on a server) - and restores them at start (<see cref="RrWatch.Init"/>).
    /// A change is captured at once and written <see cref="SaveDelay"/> s later, so dragging a slider writes once; the watch
    /// closing and the world shutting down write a pending change at once. Writes go to a .tmp file that then replaces the
    /// old one (as the avatar's RrAvatarOutfit.Save).
    /// </summary>
    public static class RrWatchSettings
    {
        public const float SaveDelay = 1f;

        private static string _file;
        private static RrWatchSettingsFile _pending;
        private static float _changedAt;

        /// <summary>persistentDataPath/SomniumRecRoom/settings.json</summary>
        public static string FilePath
        {
            get
            {
                if (_file == null)
                {
                    _file = Path.Combine(Path.Combine(Application.persistentDataPath, "SomniumRecRoom"), "settings.json");
                }

                return _file;
            }
        }

        /// <summary>Restores the saved settings (none saved: the defaults stay).</summary>
        public static void Load()
        {
            try
            {
                string f = FilePath;
                if (!File.Exists(f))
                {
                    return;
                }

                var s = JsonUtility.FromJson<RrWatchSettingsFile>(File.ReadAllText(f, Encoding.UTF8));
                if (s == null)
                {
                    return;
                }

                RrWatchSounds.Volume = Unit(s.InterfaceVolume, 1f);
                RrSound.Volume = Unit(s.EffectsVolume, 1f);
                RrMusic.Volume = Unit(s.MusicVolume, 0.6f);
                RrPostFx.UserExposure = Finite(s.Brightness);
                RrPostFx.UserWarmth = Finite(s.Warmth);
                RrWatch.TapToOpen = s.TapToOpen;
                RrWatch.RightWrist = s.RightWrist;
                RrWatch.ShowSeconds = s.ShowSeconds;
                RrLog.Info("Watch", "settings restored from settings.json: volumes " + RrWatchSounds.Volume.ToString("0.00") + " / " + RrSound.Volume.ToString("0.00") + " / " + RrMusic.Volume.ToString("0.00")
                    + ", brightness " + RrPostFx.UserExposure.ToString("+0.00;-0.00;0") + " EV, warmth " + RrPostFx.UserWarmth.ToString("+0;-0;0")
                    + ", " + (RrWatch.TapToOpen ? "tap" : "look") + " to open, " + (RrWatch.RightWrist ? "right" : "left") + " wrist, seconds " + (RrWatch.ShowSeconds ? "shown" : "hidden"));
            }
            catch (Exception e)
            {
                RrLog.Warn("Watch", "the saved settings could not be read (" + e.Message + "): defaults");
            }
        }

        /// <summary>A setting changed: the current values are captured now and written a moment later.</summary>
        public static void Changed()
        {
            _pending = new RrWatchSettingsFile
            {
                InterfaceVolume = RrWatchSounds.Volume,
                EffectsVolume = RrSound.Volume,
                MusicVolume = RrMusic.Volume,
                Brightness = RrPostFx.UserExposure,
                Warmth = RrPostFx.UserWarmth,
                TapToOpen = RrWatch.TapToOpen,
                RightWrist = RrWatch.RightWrist,
                ShowSeconds = RrWatch.ShowSeconds
            };
            _changedAt = Time.realtimeSinceStartup;
        }

        /// <summary>Writes a pending change once it is <see cref="SaveDelay"/> s old (now: at once).</summary>
        public static void Flush(bool now)
        {
            if (_pending == null || (!now && Time.realtimeSinceStartup - _changedAt < SaveDelay))
            {
                return;
            }

            var s = _pending;
            _pending = null;
            try
            {
                string f = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s, true), Encoding.UTF8);
                if (File.Exists(f))
                {
                    File.Delete(f);
                }

                File.Move(tmp, f);
            }
            catch (Exception e)
            {
                RrLog.Warn("Watch", "saving the settings failed: " + e.Message);
            }
        }

        /// <summary>World shutdown: writes a pending change, forgets the path.</summary>
        public static void Shutdown()
        {
            Flush(true);
            _file = null;
        }

        private static float Unit(float v, float fallback)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp01(v);
        }

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }
    }

    /// <summary>Rec Room's slider sounds (SliderDown / SliderUp pools) on a settings slider.</summary>
    internal sealed class RrWatchSliderSounds : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            RrWatchSounds.Play(RrWatchSounds.SliderDown);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            RrWatchSounds.Play(RrWatchSounds.SliderUp);
        }
    }
}
