using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// A playable Rec Room radio (the kiosk, cafe and upstairs lounge boomboxes). Pressing it steps through the
    /// prefab's playlist (Rec Room's `Radio` component: 41 tracks), then "off", then the first track again. The
    /// current track is one custom instance int (0 = the room's default, n = track n-1, -1 = off) so everybody in
    /// the instance hears the same song: the authority writes it, others send event 65 {slot, seen value} and
    /// repeat every 0.6 s until the int changes (the authority only applies a request whose seen value is still
    /// current, so repeats never skip two tracks). The authority also moves to the next track when a song ends;
    /// every client loops its track until then. The sound is a 3D loop on the prop (RrSound.Loop).
    /// </summary>
    public sealed class RrRecCenterRadio : MonoBehaviour
    {
        public const float RepeatEvery = 0.6f;
        public const float GiveUpAfter = 4f;
        public const float LabelSeconds = 4f;

        private static readonly List<RrRecCenterRadio> AllRadios = new List<RrRecCenterRadio>();

        public int Slot;
        public string DisplayName;
        public string[] Clips;
        public string[] Titles;
        public int DefaultTrack;
        public float MaxDistance = 12f;
        public float Volume = 0.3f;

        /// <summary>The track playing now (-1 off, -2 not started).</summary>
        public int PlayingTrack { get; private set; } = -2;

        public int Changes { get; private set; }
        public int Presses { get; private set; }

        public string PlayingTitle
        {
            get { return PlayingTrack >= 0 && Titles != null && PlayingTrack < Titles.Length ? Titles[PlayingTrack] : "off"; }
        }

        private AudioSource _source;
        private TextMeshPro _label;
        private float _labelUntil;
        private float _startedAt;
        private int _pendingFrom = int.MinValue;
        private float _pendingSince;
        private float _pendingSent;

        public static IReadOnlyList<RrRecCenterRadio> All
        {
            get { return AllRadios; }
        }

        public void Setup(int slot, string displayName, string[] clips, string[] titles, int defaultTrack, float maxDistance, float volume)
        {
            Slot = slot;
            DisplayName = displayName;
            Clips = clips;
            Titles = titles;
            DefaultTrack = defaultTrack;
            MaxDistance = maxDistance;
            Volume = volume;
        }

        /// <summary>The local player pressed the radio.</summary>
        public void Press()
        {
            Presses++;
            int state = State;
            if (RrNet.Instance.CanWrite)
            {
                Write(Next(state, false));
            }
            else
            {
                _pendingFrom = state;
                _pendingSince = Time.realtimeSinceStartup;
                SendRequest(state);
            }

            ShowLabel(RrNet.Instance.CanWrite ? null : "...");
        }

        /// <summary>Event 65 at the authority: {slot, 4-byte seen value}.</summary>
        internal static void OnRequest(int sender, byte[] body)
        {
            if (body == null || body.Length < 5 || !RrNet.Instance.CanWrite)
            {
                return;
            }

            int slot = body[0];
            int from = body[1] | (body[2] << 8) | (body[3] << 16) | (body[4] << 24);
            for (int i = 0; i < AllRadios.Count; i++)
            {
                var r = AllRadios[i];
                if (r != null && r.Slot == slot && r.State == from)
                {
                    r.Write(r.Next(from, false));
                }
            }
        }

        private int State
        {
            get { return RrRecCenterRoom.IntsValid ? RrNet.Instance.Get(Slot) : 0; }
        }

        private int TrackOf(int state)
        {
            if (state == 0)
            {
                return DefaultTrack;
            }

            return state < 0 ? -1 : state - 1;
        }

        /// <summary>The state after this one: next track, off after the last (or wrap to 0 when a song ends).</summary>
        private int Next(int state, bool wrap)
        {
            int track = TrackOf(state);
            int count = Clips != null ? Clips.Length : 0;
            if (count == 0)
            {
                return -1;
            }

            if (track < 0)
            {
                return 1;
            }

            int next = track + 1;
            if (next >= count)
            {
                return wrap ? 1 : -1;
            }

            return next + 1;
        }

        private void Write(int state)
        {
            var s = RrNet.Instance;
            if (!s.CanWrite)
            {
                return;
            }

            if (!RrRecCenterRoom.IntsValid)
            {
                s.Set(RrRecCenterRoom.IntMagic, RrRecCenterRoom.Magic);
            }

            s.Set(Slot, state);
        }

        private void SendRequest(int from)
        {
            _pendingSent = Time.realtimeSinceStartup;
            RrNet.Send(RrRecCenterRoom.EvRadio, new[] { (byte)Slot, (byte)from, (byte)(from >> 8), (byte)(from >> 16), (byte)(from >> 24) }, true);
        }

        private void OnEnable()
        {
            if (!AllRadios.Contains(this))
            {
                AllRadios.Add(this);
            }
        }

        private void OnDisable()
        {
            AllRadios.Remove(this);
        }

        private void OnDestroy()
        {
            AllRadios.Remove(this);
            if (_source != null)
            {
                RrSound.Stop(_source);
                _source = null;
            }
        }

        private void Update()
        {
            if (Clips == null)
            {
                return;
            }

            int state = State;
            int track = TrackOf(state);
            if (track != PlayingTrack)
            {
                Switch(track);
            }

            float now = Time.realtimeSinceStartup;
            if (_pendingFrom != int.MinValue)
            {
                if (state != _pendingFrom || now - _pendingSince > GiveUpAfter)
                {
                    _pendingFrom = int.MinValue;
                }
                else if (now - _pendingSent > RepeatEvery)
                {
                    SendRequest(_pendingFrom);
                }
            }

            // The authority moves on when the song ends (Rec Room's radio played through its list).
            if (RrNet.Instance.CanWrite && track >= 0 && _source != null && _source.clip != null && Time.time - _startedAt > _source.clip.length + 0.5f)
            {
                Write(Next(state, true));
            }

            if (_label != null && _label.gameObject.activeSelf)
            {
                if (Time.time > _labelUntil)
                {
                    _label.gameObject.SetActive(false);
                }
                else
                {
                    var cam = RrPlayer.Camera;
                    if (cam != null)
                    {
                        var d = _label.transform.position - cam.transform.position;
                        d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f)
                        {
                            _label.transform.rotation = Quaternion.LookRotation(d);
                        }
                    }
                }
            }
        }

        private void Switch(int track)
        {
            bool first = PlayingTrack == -2;
            PlayingTrack = track;
            if (_source != null)
            {
                RrSound.Stop(_source);
                _source = null;
            }

            if (track >= 0 && track < Clips.Length)
            {
                _source = RrSound.Loop(Clips[track], transform, Volume, MaxDistance);
                _startedAt = Time.time;
            }

            if (!first)
            {
                Changes++;
                ShowLabel(null);
                RrLog.Info("RecCenter", DisplayName + ": " + (track >= 0 ? "playing " + Titles[track] + " (" + Clips[track] + ")" : "off"));
            }
        }

        private void ShowLabel(string text)
        {
            if (_label == null)
            {
                var b = Bounds();
                var go = new GameObject(DisplayName + " Label");
                go.transform.SetParent(transform.parent, false);
                go.transform.position = new Vector3(b.center.x, b.max.y + 0.3f, b.center.z);
                _label = go.AddComponent<TextMeshPro>();
                RrText.Style(_label, true);
                _label.alignment = TextAlignmentOptions.Center;
                _label.fontSize = 1.6f;
                _label.color = new Color(1f, 0.62f, 0.3f);
                _label.rectTransform.sizeDelta = new Vector2(2.4f, 0.4f);
                _label.enableAutoSizing = true;
                _label.fontSizeMin = 0.6f;
                _label.fontSizeMax = 1.6f;
            }

            _label.text = text ?? (PlayingTrack >= 0 ? "NOW PLAYING\n" + PlayingTitle : DisplayName.ToUpperInvariant() + " OFF");
            _label.gameObject.SetActive(true);
            _labelUntil = Time.time + LabelSeconds;
        }

        private Bounds Bounds()
        {
            var rs = GetComponentsInChildren<Renderer>();
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    b = rs[i].bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(rs[i].bounds);
                }
            }

            return b;
        }
    }
}
