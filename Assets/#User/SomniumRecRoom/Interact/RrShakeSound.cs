using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's ShakeAudio (79 prefabs: potions, root beer, bubbly): a slosh when the object is shaken. The
    /// acceleration is the change of velocity between lookBackStartTime (0.1 s) and lookBackEndTime (0.05 s) ago;
    /// above a definition's minimum (potions and root beer: 100 m/s2 linear or 120 angular; bubbly: 8 and 6) a random
    /// clip of that definition plays (Goblin2_Potion_Slosh01-03), louder toward the maximum, no more often than
    /// MinimumInterval (0.4 s; bubbly 0.85). onlyPlayAudioWhileHeldByPlayer (1 on 19 of 79) limits it to a held object.
    ///
    /// GUESSES: angular acceleration is taken in rad/s2 (in deg/s2 the minimum of 120 would be crossed by hand tremor
    /// alone), the two thresholds are alternatives, and the volume goes from 60 % at the minimum to 100 % at the
    /// maximum. Velocities come from the transform, since a held object is kinematic.
    /// </summary>
    public sealed class RrShakeSound : MonoBehaviour
    {
        public sealed class Definition
        {
            public float MinLinear, MaxLinear, MinAngular, MaxAngular, Interval;
            public List<RrClipInfo> Clips = new List<RrClipInfo>();
            [NonSerialized] public float LastPlayed = -10f;
        }

        public readonly List<Definition> Definitions = new List<Definition>();
        public bool OnlyWhileHeld;
        public float LookBackStart = 0.1f;
        public float LookBackEnd = 0.05f;

        /// <summary>Sloshes played (the harness reads it).</summary>
        public int Played { get; private set; }

        public event Action<float> OnShake;

        private const int Samples = 32;
        private readonly float[] _time = new float[Samples];
        private readonly Vector3[] _vel = new Vector3[Samples];
        private readonly Vector3[] _ang = new Vector3[Samples];
        private int _next;
        private int _count;
        private Vector3 _lastPos;
        private Quaternion _lastRot;
        private bool _hasLast;
        private RrGrabbable _grabbable;
        private float _armedAt;

        public void Setup(RrInstance inst, RrConfig c)
        {
            OnlyWhileHeld = c.Bool("onlyPlayAudioWhileHeldByPlayer");
            LookBackStart = Mathf.Max(0.02f, c.Float("lookBackStartTime", 0.1f));
            LookBackEnd = Mathf.Clamp(c.Float("lookBackEndTime", 0.05f), 0f, LookBackStart - 0.01f);
            Definitions.Clear();
            foreach (var d in c.List("shakeDefinitions"))
            {
                var def = new Definition
                {
                    MinLinear = d.Float("MinimumLinearAcceleration", 100f),
                    MaxLinear = d.Float("MaximumLinearAcceleration", 10000f),
                    MinAngular = d.Float("MinimumAngularAcceleration", 120f),
                    MaxAngular = d.Float("MaximumAngularAcceleration", 10000f),
                    Interval = d.Float("MinimumInterval", 0.4f),
                    Clips = d.Clips("AudioClips")
                };
                if (def.Clips.Count > 0)
                {
                    Definitions.Add(def);
                }
            }
        }

        private void Awake()
        {
            _grabbable = GetComponent<RrGrabbable>();
            _armedAt = Time.time + 0.5f;
        }

        private void OnEnable()
        {
            ResetHistory();
        }

        /// <summary>Forgets the motion (after a teleport such as a respawn, which is not a shake).</summary>
        public void ResetHistory()
        {
            _count = 0;
            _hasLast = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            var t = transform;
            if (!_hasLast || dt <= 1e-5f)
            {
                _lastPos = t.position;
                _lastRot = t.rotation;
                _hasLast = true;
                return;
            }

            Vector3 v = (t.position - _lastPos) / dt;
            Quaternion dq = t.rotation * Quaternion.Inverse(_lastRot);
            float angle;
            Vector3 axis;
            dq.ToAngleAxis(out angle, out axis);
            if (angle > 180f)
            {
                angle -= 360f;
            }

            Vector3 w = float.IsNaN(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
            _lastPos = t.position;
            _lastRot = t.rotation;
            _time[_next] = Time.time;
            _vel[_next] = v;
            _ang[_next] = w;
            _next = (_next + 1) % Samples;
            _count = Mathf.Min(_count + 1, Samples);
            if (Definitions.Count == 0 || Time.time < _armedAt || (OnlyWhileHeld && (_grabbable == null || !_grabbable.IsHeld)))
            {
                return;
            }

            Vector3 v0, w0, v1, w1;
            if (!Sample(Time.time - LookBackStart, out v0, out w0) || !Sample(Time.time - LookBackEnd, out v1, out w1))
            {
                return;
            }

            float span = LookBackStart - LookBackEnd;
            Evaluate((v1 - v0).magnitude / span, (w1 - w0).magnitude / span);
        }

        /// <summary>Plays the slosh for an acceleration (linear m/s2, angular rad/s2); also the test hook.</summary>
        public bool Evaluate(float linear, float angular)
        {
            for (int i = 0; i < Definitions.Count; i++)
            {
                var d = Definitions[i];
                bool lin = linear >= d.MinLinear;
                bool ang = angular >= d.MinAngular;
                if ((!lin && !ang) || Time.time - d.LastPlayed < d.Interval)
                {
                    continue;
                }

                float k = Mathf.Max(lin ? Mathf.InverseLerp(d.MinLinear, d.MaxLinear, linear) : 0f, ang ? Mathf.InverseLerp(d.MinAngular, d.MaxAngular, angular) : 0f);
                d.LastPlayed = Time.time;
                Played++;
                RrTools.PlayAny(d.Clips, transform.position, Mathf.Lerp(0.6f, 1f, k), 12f, transform);
                RrTools.Once("Shake", "first", "first slosh: " + name + " at " + linear.ToString("0") + " m/s2, " + angular.ToString("0") + " rad/s2");
                if (OnShake != null)
                {
                    OnShake(linear);
                }

                return true;
            }

            return false;
        }

        /// <summary>The recorded velocity nearest to a past time.</summary>
        private bool Sample(float when, out Vector3 v, out Vector3 w)
        {
            v = w = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < _count; i++)
            {
                float d = Mathf.Abs(_time[i] - when);
                if (d < best)
                {
                    best = d;
                    v = _vel[i];
                    w = _ang[i];
                    found = true;
                }
            }

            return found && best < 0.05f;
        }
    }
}
