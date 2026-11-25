using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Plays Rec Room's own sounds the way its components describe them.
    ///
    /// Every Rec Room component that makes a noise stores the same struct: audioClip, configureVolumeInDecibel,
    /// volume, pitchVariation, pitchShift (the root beer bottle: uncork 0.75 / 0.2, swallow 0.5 / 0.15, collision
    /// pitch shift -0.156). Volume is linear unless flagged as decibels. Pitch is read as 1 + shift, then varied
    /// by up to +-variation each play (GUESS at Rec Room's exact formula; the numbers only make sense as
    /// fractions of 1). Sounds are 3D with a logarithmic roll-off from 1.5 m to 40 m, like the CollisionAudio
    /// default roll-off in the bundles (2 m to 100 m) tightened for a world where rooms are close together.
    ///
    /// Clips come through <see cref="RrAssets"/>, so the first play of a clip may be a frame or two late while it
    /// loads; after that it is immediate. A pool of 40 sources is reused oldest-first.
    /// </summary>
    public static class RrSound
    {
        private const int PoolSize = 40;
        private static readonly List<AudioSource> Pool = new List<AudioSource>();
        private static int _next;
        private static Transform _root;
        private static AudioSource _ui;
        private static readonly Dictionary<AudioSource, Transform> Following = new Dictionary<AudioSource, Transform>();

        /// <summary>Master volume for everything this world plays (watch settings).</summary>
        public static float Volume = 1f;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                    _root = new GameObject("Rr Sounds").transform;
                    _root.SetParent(host, false);
                    _root.gameObject.AddComponent<RrSoundDriver>();
                }

                return _root;
            }
        }

        public static float PitchOf(RrClipInfo info)
        {
            float p = 1f + info.PitchShift;
            if (info.PitchVariation > 0f)
            {
                p += Random.Range(-info.PitchVariation, info.PitchVariation);
            }

            return Mathf.Clamp(p, 0.1f, 3f);
        }

        public static float VolumeOf(RrClipInfo info)
        {
            return info.VolumeInDecibel ? Mathf.Pow(10f, info.Volume / 20f) : info.Volume;
        }

        /// <summary>Plays a Rec Room clip struct at a point (optionally following a transform).</summary>
        public static void Play(RrClipInfo info, Vector3 position, float volumeScale = 1f, Transform follow = null)
        {
            if (!info.HasClip || RrAssets.Instance == null)
            {
                return;
            }

            float volume = VolumeOf(info) * volumeScale;
            float pitch = PitchOf(info);
            RrAssets.Instance.Resolve<AudioClip>(info.Clip, clip =>
            {
                if (clip != null)
                {
                    Play(clip, follow != null ? follow.position : position, volume, pitch, 40f, follow);
                }
            });
        }

        /// <summary>A clip struct with its own audible range and an extra pitch offset (e.g. pitch rising with speed).</summary>
        public static void Play(RrClipInfo info, Vector3 position, float volumeScale, float maxDistance, float pitchAdd, Transform follow = null)
        {
            if (!info.HasClip || RrAssets.Instance == null)
            {
                return;
            }

            float volume = VolumeOf(info) * volumeScale;
            float pitch = Mathf.Clamp(PitchOf(info) + pitchAdd, 0.1f, 3f);
            RrAssets.Instance.Resolve<AudioClip>(info.Clip, clip =>
            {
                if (clip != null)
                {
                    Play(clip, follow != null ? follow.position : position, volume, pitch, maxDistance, follow);
                }
            });
        }

        /// <summary>A looping clip struct following a transform (a bottle's fizz, a buggy's engine); stop it with Stop(source).</summary>
        public static AudioSource Loop(RrClipInfo info, Transform follow, float volumeScale = 1f, float maxDistance = 30f)
        {
            if (!info.HasClip || RrAssets.Instance == null || follow == null)
            {
                return null;
            }

            var go = new GameObject("Rr Loop");
            go.transform.SetParent(follow, false);
            var source = go.AddComponent<AudioSource>();
            Configure(source, maxDistance);
            source.loop = true;
            source.volume = Mathf.Clamp01(VolumeOf(info) * volumeScale * Volume);
            source.pitch = PitchOf(info);
            RrAssets.Instance.Resolve<AudioClip>(info.Clip, clip =>
            {
                if (clip != null && source != null)
                {
                    source.clip = clip;
                    source.Play();
                }
            });
            return source;
        }

        /// <summary>Plays one of a list of clip structs, chosen at random (Rec Room's "pool" arrays).</summary>
        public static void PlayAny(List<RrClipInfo> infos, Vector3 position, float volumeScale = 1f)
        {
            if (infos == null || infos.Count == 0)
            {
                return;
            }

            Play(infos[Random.Range(0, infos.Count)], position, volumeScale);
        }

        /// <summary>Plays a clip by its name in Rec Room's bundles.</summary>
        public static void Play(string clipName, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f)
        {
            if (RrAssets.Instance == null || string.IsNullOrEmpty(clipName))
            {
                return;
            }

            RrAssets.Instance.Load<AudioClip>(clipName, 83, clip =>
            {
                if (clip != null)
                {
                    Play(clip, position, volume, pitch, maxDistance);
                }
            });
        }

        public static AudioSource Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 40f, Transform follow = null)
        {
            if (clip == null)
            {
                return null;
            }

            var source = Next();
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * Volume);
            source.pitch = pitch;
            source.loop = false;
            source.spatialBlend = 1f;
            source.maxDistance = maxDistance;
            source.Play();
            if (follow != null)
            {
                Following[source] = follow;
            }
            else
            {
                Following.Remove(source);
            }

            return source;
        }

        /// <summary>A 2D interface sound (watch clicks, notifications).</summary>
        public static void PlayUi(string clipName, float volume = 0.8f)
        {
            if (RrAssets.Instance == null)
            {
                return;
            }

            RrAssets.Instance.Load<AudioClip>(clipName, 83, clip =>
            {
                if (clip == null)
                {
                    return;
                }

                EnsureUi();
                _ui.PlayOneShot(clip, Mathf.Clamp01(volume * Volume));
            });
        }

        /// <summary>A looping 3D sound that follows a transform; the caller stops it with Stop(source).</summary>
        public static AudioSource Loop(string clipName, Transform follow, float volume = 1f, float maxDistance = 30f)
        {
            if (RrAssets.Instance == null || follow == null)
            {
                return null;
            }

            var go = new GameObject("Rr Loop " + clipName);
            go.transform.SetParent(follow, false);
            var source = go.AddComponent<AudioSource>();
            Configure(source, maxDistance);
            source.loop = true;
            source.volume = Mathf.Clamp01(volume * Volume);
            RrAssets.Instance.Load<AudioClip>(clipName, 83, clip =>
            {
                if (clip != null && source != null)
                {
                    source.clip = clip;
                    source.Play();
                }
            });
            return source;
        }

        public static void Stop(AudioSource loop)
        {
            if (loop != null)
            {
                Object.Destroy(loop.gameObject);
            }
        }

        /// <summary>
        /// Stops every pooled 3D sound (called by travel when a room is left). The pool lives under the world root,
        /// not under the room, and every room is built at the world origin, so a long one-shot of the old room
        /// (an announcer line, an explosion) would otherwise go on playing where the next room stands. The 2D
        /// interface source is left alone. Returns how many were playing.
        /// </summary>
        public static int StopWorld()
        {
            int n = 0;
            foreach (var s in Pool)
            {
                if (s != null && s.isPlaying)
                {
                    s.Stop();
                    n++;
                }

                if (s != null)
                {
                    s.clip = null;
                }
            }

            Following.Clear();
            return n;
        }

        /// <summary>A 2D interface sound from a clip already in hand.</summary>
        public static void PlayUi(AudioClip clip, float volume = 0.8f)
        {
            if (clip == null)
            {
                return;
            }

            EnsureUi();
            _ui.PlayOneShot(clip, Mathf.Clamp01(volume * Volume));
        }

        private static void EnsureUi()
        {
            if (_ui == null)
            {
                _ui = new GameObject("Rr Ui Sound").AddComponent<AudioSource>();
                _ui.transform.SetParent(Root, false);
                _ui.spatialBlend = 0f;
                _ui.playOnAwake = false;
            }
        }

        private static AudioSource Next()
        {
            while (Pool.Count < PoolSize)
            {
                var go = new GameObject("Rr Sound " + Pool.Count);
                go.transform.SetParent(Root, false);
                var s = go.AddComponent<AudioSource>();
                Configure(s, 40f);
                Pool.Add(s);
            }

            // Prefer an idle source; otherwise the oldest.
            for (int i = 0; i < PoolSize; i++)
            {
                int k = (_next + i) % PoolSize;
                if (Pool[k] != null && !Pool[k].isPlaying)
                {
                    _next = (k + 1) % PoolSize;
                    return Pool[k];
                }
            }

            var oldest = Pool[_next];
            _next = (_next + 1) % PoolSize;
            return oldest;
        }

        private static void Configure(AudioSource s, float maxDistance)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1.5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
        }

        /// <summary>Moves sounds that follow a transform (a thrown bottle's fizz, a held gun's shots).</summary>
        internal static void UpdateFollowers()
        {
            if (Following.Count == 0)
            {
                return;
            }

            List<AudioSource> gone = null;
            foreach (var kv in Following)
            {
                if (kv.Key == null || kv.Value == null || !kv.Key.isPlaying)
                {
                    if (gone == null)
                    {
                        gone = new List<AudioSource>();
                    }

                    gone.Add(kv.Key);
                    continue;
                }

                kv.Key.transform.position = kv.Value.position;
            }

            if (gone != null)
            {
                foreach (var g in gone)
                {
                    Following.Remove(g);
                }
            }
        }

        public static void Shutdown()
        {
            Pool.Clear();
            Following.Clear();
            _root = null;
            _ui = null;
            _next = 0;
            Volume = 1f;
        }
    }

    /// <summary>Per-frame hook for <see cref="RrSound"/>.</summary>
    public sealed class RrSoundDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            RrSound.UpdateFollowers();
        }
    }
}
