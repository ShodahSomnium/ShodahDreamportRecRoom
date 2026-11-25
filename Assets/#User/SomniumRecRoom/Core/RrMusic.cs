using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// One music track at a time, 2D, crossfaded - Rec Room's room and activity themes (GoldenTrophyExplore,
    /// Bowling_MusicGameplay, MUS_BGM_3DCharades2_Music, CBM_Ballroom...) straight from its bundles.
    /// </summary>
    public static class RrMusic
    {
        private static AudioSource _a;
        private static AudioSource _b;
        private static RrMusicDriver _driver;
        private static string _current;

        /// <summary>Music volume (watch settings), multiplied with <see cref="RrSound.Volume"/>.</summary>
        public static float Volume = 0.6f;

        public static string Current
        {
            get { return _current; }
        }

        public static void Play(string clipName, float volume = 0.5f, bool loop = true)
        {
            if (clipName == _current || RrAssets.Instance == null)
            {
                return;
            }

            _current = clipName;
            Ensure();
            RrAssets.Instance.Load<AudioClip>(clipName, 83, clip =>
            {
                if (clip == null || _current != clipName || _driver == null)
                {
                    return;
                }

                var incoming = _a.isPlaying && _a.volume > 0.001f ? _b : _a;
                var outgoing = incoming == _a ? _b : _a;
                incoming.clip = clip;
                incoming.loop = loop;
                incoming.volume = 0f;
                incoming.Play();
                _driver.Fade(incoming, volume, outgoing, 1.5f);
            });
        }

        public static void Stop(float fade = 1f)
        {
            _current = null;
            if (_driver != null)
            {
                _driver.Fade(null, 0f, _a, fade);
                _driver.Fade(null, 0f, _b, fade);
            }
        }

        private static void Ensure()
        {
            if (_driver != null)
            {
                return;
            }

            var go = new GameObject("Rr Music");
            if (RrMain.Instance != null)
            {
                go.transform.SetParent(RrMain.Instance.WorldRoot, false);
            }

            _driver = go.AddComponent<RrMusicDriver>();
            _a = Make(go);
            _b = Make(go);
        }

        private static AudioSource Make(GameObject go)
        {
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.loop = true;
            return s;
        }

        public static void Shutdown()
        {
            _a = null;
            _b = null;
            _driver = null;
            _current = null;
            Volume = 0.6f;
        }
    }

    /// <summary>Crossfades for <see cref="RrMusic"/>.</summary>
    public sealed class RrMusicDriver : MonoBehaviour
    {
        private AudioSource _in;
        private AudioSource _out;
        private float _target;
        private float _rate;

        public void Fade(AudioSource incoming, float target, AudioSource outgoing, float seconds)
        {
            _in = incoming;
            _out = outgoing;
            _target = target;
            _rate = 1f / Mathf.Max(0.05f, seconds);
        }

        private void Update()
        {
            float step = _rate * Time.unscaledDeltaTime;
            float master = RrMusic.Volume * RrSound.Volume;
            if (_in != null)
            {
                _in.volume = Mathf.MoveTowards(_in.volume, _target * master, step * Mathf.Max(0.01f, _target));
            }

            if (_out != null && _out != _in)
            {
                _out.volume = Mathf.MoveTowards(_out.volume, 0f, step);
                if (_out.volume <= 0.001f && _out.isPlaying)
                {
                    _out.Stop();
                }
            }
        }
    }
}
