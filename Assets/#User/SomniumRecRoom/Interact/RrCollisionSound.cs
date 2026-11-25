using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's CollisionAudio (270 prefabs): impact sounds chosen by what was hit. Each mapping has a Rec Room layer
    /// mask, an optional surface tag (<see cref="RrRefs"/>), a velocity-to-volume curve, a pool of clips and an
    /// InstanceLimitDuration during which the mapping does not replay. Examples (rr_dorm_core.md §4.4): bottles
    /// "Static" (layers 0+10+11, curve 0 -> 0.08 .. 1 m/s -> 1, limit 0.5 s); the basketball "Bounce" (layers 10+11,
    /// full volume from 0.25 m/s, BasketballBounce01 at 0.79, limit 0.05 s) and "Rim" (any layer, tag
    /// CollisionSfx_Table, BasketballHitRim01 at 0.15). Rolloff 2-100 m unless the prefab sets its own.
    ///
    /// Reading (GUESS, consistent with the data): x of the curve is the impact's relative speed in m/s, clamped to
    /// the curve's keys. A mapping whose tag matches the surface wins over an untagged one (otherwise the ball's
    /// "Bounce" would shadow its "Rim", both matching the hoop's layer). Rec Room's layers become ours: its 0
    /// (world) -> our Default and Environment, 10 (props) -> Grabbable, 11 (kinematic furniture, which in Rec Room
    /// includes the floors and walls of kit rooms) -> Grabbable, Environment, Default, Click and Vehicle.
    /// Sounds are held back for 0.5 s after the object appears (props settling while a room builds) and for
    /// contacts with the Somnium hand boxes (layer 7) just after a release.
    /// </summary>
    public sealed class RrCollisionSound : MonoBehaviour
    {
        public sealed class Mapping
        {
            public string Name;
            public int Mask;
            public string Tag;
            public AnimationCurve Volume;
            public List<RrClipInfo> Clips = new List<RrClipInfo>();
            public float InstanceLimit;
            [NonSerialized] public float LastPlayed = -10f;
        }

        /// <summary>At most this many impact sounds start per frame, all objects together.</summary>
        public const int MaxPerFrame = 6;

        private static int _frame = -1;
        private static int _playedThisFrame;

        public readonly List<Mapping> Mappings = new List<Mapping>();
        public float MaxDistance = 100f;

        /// <summary>No sound under this volume factor (a resting contact).</summary>
        public float MinVolume = 0.02f;

        /// <summary>Fired for every impact that played a sound: mapping name, impact speed (m/s), the other collider.</summary>
        public event Action<string, float, Collider> OnImpact;

        /// <summary>Impacts that played a sound (the harness reads it).</summary>
        public int Played { get; private set; }

        private float _armedAt;
        private RrGrabbable _grabbable;

        public int MappingCount
        {
            get { return Mappings.Count; }
        }

        public void Setup(RrInstance inst, RrConfig c)
        {
            Mappings.Clear();
            foreach (var m in c.List("audioMappings"))
            {
                var map = new Mapping
                {
                    Name = m.String("Name", "Static"),
                    Mask = OurMask(MaskOf(m.Raw("Layers"))),
                    Tag = m.String("Tag"),
                    Volume = RrTools.CurveOf(m.Curve("velocityToVolumeCurve")),
                    Clips = m.Clips("Clips"),
                    InstanceLimit = m.Float("InstanceLimitDuration")
                };
                if (string.IsNullOrEmpty(map.Tag))
                {
                    map.Tag = null;
                }

                if (map.Clips.Count > 0)
                {
                    Mappings.Add(map);
                }
            }

            if (c.Bool("useCustomRolloff"))
            {
                var r = c.Sub("customRolloff");
                if (r != null)
                {
                    MaxDistance = Mathf.Max(2f, r.Float("maxDistance", 100f));
                }
            }
        }

        private void Awake()
        {
            _armedAt = Time.time + 0.5f;
            _grabbable = GetComponent<RrGrabbable>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Mappings.Count == 0 || Time.time < _armedAt || collision.collider == null)
            {
                return;
            }

            var other = collision.collider;
            if (other.gameObject.layer == 7 && _grabbable != null && (_grabbable.IsHeld || Time.time - _grabbable.LastReleaseTime < 0.3f))
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Impact(other, speed, point);
        }

        /// <summary>Plays the sound for an impact against a collider (also the test hook); returns the mapping used, or null.</summary>
        public Mapping Impact(Collider other, float speed, Vector3 point)
        {
            var map = Choose(other);
            if (map == null || Time.time - map.LastPlayed < map.InstanceLimit)
            {
                return null;
            }

            float k = RrTools.Evaluate(map.Volume, speed, Mathf.Clamp01(speed));
            if (k < MinVolume)
            {
                return null;
            }

            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _playedThisFrame = 0;
            }

            if (_playedThisFrame >= MaxPerFrame)
            {
                return null;
            }

            _playedThisFrame++;
            map.LastPlayed = Time.time;
            Played++;
            RrTools.PlayAny(map.Clips, point, k, MaxDistance);
            RrTools.Once("Collision", "first", "first impact sound: " + name + " '" + map.Name + "' at " + speed.ToString("0.00") + " m/s, volume x" + k.ToString("0.00"));
            if (OnImpact != null)
            {
                OnImpact(map.Name, speed, other);
            }

            return map;
        }

        /// <summary>The mapping for a surface: a tagged mapping matching the surface's tag first, then the first untagged one.</summary>
        public Mapping Choose(Collider other)
        {
            int layerBit = other != null ? 1 << other.gameObject.layer : 1;
            string tag = RrRefs.TagOf(other);
            Mapping untagged = null;
            for (int i = 0; i < Mappings.Count; i++)
            {
                var m = Mappings[i];
                if ((m.Mask & layerBit) == 0)
                {
                    continue;
                }

                if (m.Tag != null)
                {
                    if (tag != null && string.Equals(m.Tag, tag, StringComparison.Ordinal))
                    {
                        return m;
                    }
                }
                else if (untagged == null)
                {
                    untagged = m;
                }
            }

            return untagged;
        }

        /// <summary>The first mapping with this name, or null (RrBall looks up its "Rim" sound).</summary>
        public Mapping Find(string mappingName)
        {
            for (int i = 0; i < Mappings.Count; i++)
            {
                if (string.Equals(Mappings[i].Name, mappingName, StringComparison.OrdinalIgnoreCase))
                {
                    return Mappings[i];
                }
            }

            return null;
        }

        private static long MaskOf(object raw)
        {
            var d = raw as Dictionary<string, object>;
            object bits = raw;
            if (d != null && !d.TryGetValue("m_Bits", out bits))
            {
                return 0xFFFFFFFFL;
            }

            try
            {
                return Convert.ToInt64(bits) & 0xFFFFFFFFL;
            }
            catch (Exception)
            {
                return 0xFFFFFFFFL;
            }
        }

        /// <summary>A Rec Room layer mask in our layers (see the class notes).</summary>
        public static int OurMask(long rrMask)
        {
            if ((rrMask & 0xFFFFFFFFL) == 0xFFFFFFFFL)
            {
                return ~0;
            }

            int m = 0;
            if ((rrMask & 1L) != 0)
            {
                m |= (1 << RrLayers.Default) | (1 << RrLayers.Environment);
            }

            if ((rrMask & (1L << 10)) != 0 || (rrMask & (1L << 17)) != 0)
            {
                m |= 1 << RrLayers.Grabbable;
            }

            if ((rrMask & (1L << 11)) != 0)
            {
                m |= (1 << RrLayers.Grabbable) | (1 << RrLayers.Environment) | (1 << RrLayers.Default) | (1 << RrLayers.Click) | (1 << RrLayers.Vehicle);
            }

            for (int i = 12; i < 32; i++)
            {
                if (i != 17 && (rrMask & (1L << i)) != 0)
                {
                    m |= 1 << RrLayers.Environment;
                }
            }

            return m;
        }

        internal static void ClearStatics()
        {
            _frame = -1;
            _playedThisFrame = 0;
        }
    }
}
