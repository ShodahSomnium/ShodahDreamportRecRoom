using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>What a button sounds like: Rec Room's RRUI button audio types (AudioPalette_Default).</summary>
    public enum RrUiSound : byte
    {
        None = 0,
        Default = 1,
        Negative = 2,
        Primary = 3,
        Sidebar = 4,
        Card = 5,
        Inactive = 6,
        Tab = 7
    }

    /// <summary>
    /// The watch's interface sounds, as Rec Room's audio palette defines them (rr_watch_ui.md 2.7): every
    /// button type has a hover, a pointer-down and a click pool; a pool is a few variations of one sound with a
    /// volume, a random pitch range and a pitch offset, and each play picks one variation at random.
    ///
    /// Rec Room's volumes are tiny linear values (0.008 for a card hover, 0.2 for the primary click) because its
    /// interface mixer bus is boosted; here one gain of 4 (GUESS, tuned by ear against the world's other
    /// sounds) restores the level and keeps their ratios. Everything is scaled by the world's master volume
    /// (<see cref="RrSound.Volume"/>) and the watch's own interface volume (<see cref="Volume"/>).
    ///
    /// The clips are Rec Room's own, loaded through <see cref="RrAssets"/> by name; they are requested when the
    /// watch starts so the first hover is not silent. Pitch varies per play, which the shared 2D source of
    /// <see cref="RrSound.PlayUi"/> cannot do, so the watch has a few 2D sources of its own.
    /// </summary>
    public static class RrWatchSounds
    {
        public const float UiGain = 4f;

        /// <summary>Interface volume (watch settings), 0..1.</summary>
        public static float Volume = 1f;

        public const string Open = "open";
        public const string OpenStereo = "open2";
        public const string Close = "close";
        public const string CloseStereo = "close2";
        public const string Cloth = "cloth";
        public const string Transition = "transition";
        public const string SelectDown = "down";
        public const string SelectClick = "click";
        public const string HoverGeneral = "hover";
        public const string NegativeClick = "negative";
        public const string PrimaryDown = "primaryDown";
        public const string PrimaryClick = "primaryClick";
        public const string PrimaryHover = "primaryHover";
        public const string SidebarHover = "sidebarHover";
        public const string CardHover = "cardHover";
        public const string InactiveDown = "inactiveDown";
        public const string InactiveHover = "inactiveHover";
        public const string ToggleOn = "toggleOn";
        public const string ToggleOff = "toggleOff";
        public const string TabClick = "tab";
        public const string SliderDown = "sliderDown";
        public const string SliderUp = "sliderUp";
        public const string ScrollEnd = "scrollEnd";
        public const string CardLoad = "cardLoad";
        public const string FlavorHome = "navHome";
        public const string FlavorPeople = "navPeople";
        public const string FlavorThisRoom = "navThisRoom";
        public const string FlavorSettings = "navSettings";
        public const string FlavorProfile = "navProfile";

        private sealed class Pool
        {
            public string[] Clips;
            public float Volume;
            public float PitchVariation;
            public float PitchShift;
        }

        private static readonly Dictionary<string, Pool> Pools = new Dictionary<string, Pool>();
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly HashSet<string> Requested = new HashSet<string>();
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static Transform _root;
        private static float _lastHoverAt;

        private static void Define()
        {
            if (Pools.Count > 0)
            {
                return;
            }

            // Watch open / close (RecRoom.Core.WatchUI + the WatchMenu audio pools, bundle c513202a).
            Add(Open, 0.15f, 0.17f, 0f, Series("sfx_rrui_watch_ui_open_paper_0", 1, 3));
            Add(OpenStereo, 0.025f, 0f, 0f, Series("sfx_rrui_watch_ui_open_stereo_0", 1, 3));
            Add(Close, 0.10f, 0.17f, 0f, Series("sfx_rrui_watch_ui_close_paper_0", 1, 3));
            Add(CloseStereo, 0.02f, 0f, 0f, Series("sfx_rrui_watch_ui_close_stereo_0", 1, 3));
            Add(Cloth, 0.1f, 0.05f, 0f, Series("sfx_rrui_watch_open_cloth_0", 1, 3));
            Add(Transition, 0.05f, 0f, 0f, Series("sfx_rrui_watch_ui_transition_open_0", 1, 6));

            // AudioPalette_Default pools.
            Add(SelectDown, 0.10f, 0f, 0f, Series("sfx_rrui_watch_ui_select_down_0", 1, 6));
            Add(SelectClick, 0.12f, 0f, 0f, "sfx_rrui_watch_ui_select_click_01", "sfx_rrui_watch_ui_select_click_02", "sfx_rrui_watch_ui_select_click_03", "sfx_rrui_watch_ui_select_click_05", "sfx_rrui_watch_ui_select_click_06");
            Add(HoverGeneral, 0.012f, 0.1f, 0f, Series("sfx_rrui_watch_ui_button_hover_general_0", 1, 6));
            Add(NegativeClick, 0.2f, 0f, 0f, "comped_sfx_rrui_watch_ui_select_negative_01");
            Add(PrimaryDown, 0.1f, 0f, 0f, "sfx_rrui_watch_ui_select_primary_down_01");
            Add(PrimaryClick, 0.2f, 0f, 0f, "comped_sfx_rrui_watch_ui_select_primary_01");
            Add(PrimaryHover, 0.01f, 0f, -0.65f, Series("sfx_rrui_watch_ui_button_hover_card_0", 1, 6));
            Add(SidebarHover, 0.01f, 0.14f, 0f, Series("sfx_rrui_watch_ui_button_hover_sidebar_0", 1, 5));
            Add(CardHover, 0.008f, 0.1f, 0f, Series("sfx_rrui_watch_ui_button_hover_card_0", 1, 6));
            Add(InactiveDown, 0.1f, 0f, 0f, "comped_sfx_rrui_watch_ui_select_input_toggle_inactive_01");
            Add(InactiveHover, 0.01f, 0.1f, 0.4f, Series("sfx_rrui_watch_ui_button_hover_card_0", 1, 6));
            Add(ToggleOn, 0.03f, 0f, 0f, Series("sfx_rrui_watch_ui_toggle_button_on_0", 1, 3));
            Add(ToggleOff, 0.02f, 0f, 0f, Series("sfx_rrui_watch_ui_toggle_button_off_0", 1, 3));
            Add(TabClick, 0.03f, 0.2f, 0f, "sfx_rrui_watch_ui_category_tab_01", "sfx_rrui_watch_ui_category_tab_02", "sfx_rrui_watch_ui_category_tab_03", "sfx_rrui_watch_ui_tab_over_05", "sfx_rrui_watch_ui_tab_over_06");
            Add(SliderDown, 0.02f, 0.21f, 0f, "sfx_rrui_watch_ui_slider_activate_01");
            Add(SliderUp, 0.015f, 0.21f, 0f, "sfx_rrui_watch_ui_slider_deactivate_01");
            Add(ScrollEnd, 0.01f, 0.16f, 0f, "sfx_rrui_watch_ui_scroll_vert_stop_01");
            Add(CardLoad, 0.02f, 0f, 0f, Series("sfx_rrui_watch_ui_card_load_in_0", 1, 8));

            // Nav-bar "flavour" stingers, played with the click when a tab becomes active.
            Add(FlavorHome, 0.05f, 0f, 0f, Series("sfx_rrui_watch_ui_sidebar_home_0", 1, 3));
            Add(FlavorPeople, 0.08f, 0.12f, 0f, Series("sfx_rrui_watch_ui_sidebar_people_0", 1, 3));
            Add(FlavorThisRoom, 0.025f, 0f, 0f, Series("sfx_rrui_watch_ui_sidebar_thisroom_0", 1, 3));
            Add(FlavorSettings, 0.04f, 0f, 0f, Series("sfx_rrui_watch_ui_sidebar_settings_0", 1, 3));
            Add(FlavorProfile, 0.02f, 0f, 0f, Series("sfx_rrui_watch_ui_sidebar_profile_0", 1, 3));
        }

        private static string[] Series(string prefix, int first, int last)
        {
            var a = new string[last - first + 1];
            for (int i = first; i <= last; i++)
            {
                a[i - first] = prefix + i;
            }

            return a;
        }

        private static void Add(string id, float volume, float pitchVariation, float pitchShift, params string[] clips)
        {
            Pools[id] = new Pool { Clips = clips, Volume = volume, PitchVariation = pitchVariation, PitchShift = pitchShift };
        }

        /// <summary>Creates the watch's sources under the given object and starts loading every clip.</summary>
        public static void Init(Transform root)
        {
            Define();
            _root = root;
            foreach (var kv in Pools)
            {
                foreach (var clip in kv.Value.Clips)
                {
                    Request(clip, null);
                }
            }
        }

        private static void Request(string clipName, System.Action<AudioClip> done)
        {
            AudioClip ready;
            if (Clips.TryGetValue(clipName, out ready))
            {
                if (done != null)
                {
                    done(ready);
                }

                return;
            }

            if (RrAssets.Instance == null)
            {
                return;
            }

            bool first = Requested.Add(clipName);
            if (!first && done == null)
            {
                return;
            }

            RrAssets.Instance.Load<AudioClip>(clipName, 83, clip =>
            {
                if (clip != null)
                {
                    Clips[clipName] = clip;
                }

                if (done != null && clip != null && _root != null)
                {
                    done(clip);
                }
            });
        }

        /// <summary>Plays a random variation of a pool.</summary>
        public static void Play(string poolId)
        {
            Pool pool;
            if (string.IsNullOrEmpty(poolId) || _root == null || !Pools.TryGetValue(poolId, out pool) || pool.Clips.Length == 0)
            {
                return;
            }

            string clipName = pool.Clips[Random.Range(0, pool.Clips.Length)];
            float pitch = Mathf.Clamp(1f + pool.PitchShift + Random.Range(-pool.PitchVariation, pool.PitchVariation), 0.1f, 3f);
            float volume = Mathf.Clamp01(pool.Volume * UiGain * Volume * RrSound.Volume);
            if (volume <= 0.0001f)
            {
                return;
            }

            float asked = Time.unscaledTime;
            Request(clipName, clip =>
            {
                // A clip that took long to load would sound out of place: drop it.
                if (Time.unscaledTime - asked < 0.4f)
                {
                    Emit(clip, volume, pitch);
                }
            });
        }

        private static void Emit(AudioClip clip, float volume, float pitch)
        {
            if (_root == null || clip == null)
            {
                return;
            }

            AudioSource source = null;
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i] != null && !Sources[i].isPlaying)
                {
                    source = Sources[i];
                    break;
                }
            }

            if (source == null)
            {
                if (Sources.Count < 8)
                {
                    var go = new GameObject("Rr Watch Sound " + Sources.Count);
                    go.transform.SetParent(_root, false);
                    source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.dopplerLevel = 0f;
                    Sources.Add(source);
                }
                else
                {
                    source = Sources[Random.Range(0, Sources.Count)];
                }
            }

            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        /// <summary>The hover sound of a button type (rate-limited: a ray sweeping a row makes one tick per button, not a buzz).</summary>
        public static void Hover(RrUiSound sound)
        {
            if (Time.unscaledTime - _lastHoverAt < 0.04f)
            {
                return;
            }

            _lastHoverAt = Time.unscaledTime;
            switch (sound)
            {
                case RrUiSound.Default:
                case RrUiSound.Negative:
                case RrUiSound.Tab:
                    Play(HoverGeneral);
                    break;
                case RrUiSound.Primary:
                    Play(PrimaryHover);
                    break;
                case RrUiSound.Sidebar:
                    Play(SidebarHover);
                    break;
                case RrUiSound.Card:
                    Play(CardHover);
                    break;
                case RrUiSound.Inactive:
                    Play(InactiveHover);
                    break;
            }
        }

        public static void Down(RrUiSound sound)
        {
            switch (sound)
            {
                case RrUiSound.None:
                    break;
                case RrUiSound.Primary:
                    Play(PrimaryDown);
                    break;
                case RrUiSound.Inactive:
                    Play(InactiveDown);
                    break;
                default:
                    Play(SelectDown);
                    break;
            }
        }

        public static void Click(RrUiSound sound)
        {
            switch (sound)
            {
                case RrUiSound.Default:
                case RrUiSound.Sidebar:
                case RrUiSound.Card:
                    Play(SelectClick);
                    break;
                case RrUiSound.Negative:
                    Play(NegativeClick);
                    break;
                case RrUiSound.Primary:
                    Play(PrimaryClick);
                    break;
                case RrUiSound.Tab:
                    Play(TabClick);
                    break;
            }
        }

        public static void Shutdown()
        {
            Clips.Clear();
            Requested.Clear();
            Sources.Clear();
            _root = null;
            _lastHoverAt = 0f;
            Volume = 1f;
        }
    }
}
