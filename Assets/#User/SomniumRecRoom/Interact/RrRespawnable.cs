using System;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's ToolCleanup (1,023 prefabs): a prop moved and left alone goes back where it belongs. Defaults on 840:
    /// once it is more than 0.1 m or 30 degrees from its home pose, not held, and nobody is within 3 m, it waits 15 s,
    /// animates out and reappears at home (animating in, with the tool's ResetAudio). Variants read from the prefab:
    /// consumable empties never (999,999 s), standees only when asleep, grenades 1 m, the whip 1.2 s, flags 30 s.
    /// A prop that falls below y = -50 (out of the world) comes back at once.
    ///
    /// The home pose is where it was when this was set up (<see cref="SetHome"/> moves it), relative to the parent so
    /// a room can be built anywhere. Only the client that simulates a shared prop (RrNetBody.LocallyControlled)
    /// respawns it. <see cref="RrBreakable"/> and <see cref="RrEdible"/> use <see cref="Respawn"/> to bring a broken
    /// bottle or a finished slice back whole.
    /// </summary>
    public sealed class RrRespawnable : MonoBehaviour
    {
        public float MinDisplacement = 0.1f;
        public float MinRotation = 30f;
        public float MinPlayerDistance = 3f;
        public float Delay = 15f;
        public bool UseEffects = true;
        public bool OnlyWhenSleeping;
        public RrClipInfo ResetClip;

        /// <summary>Below this height it is back at once (Somnium respawns players at -100; floors stay above -50).</summary>
        public float KillHeight = -50f;

        public Vector3 HomePosition { get; private set; }
        public Quaternion HomeRotation { get; private set; }

        /// <summary>Seconds it has been displaced and left alone.</summary>
        public float Waited { get; private set; }

        public int Respawns { get; private set; }

        public event Action OnRespawned;

        private const float CheckInterval = 0.5f;
        private float _nextCheck;
        private bool _busy;
        private RrGrabbable _grabbable;
        private Rigidbody _body;

        public void Setup(RrInstance inst, RrConfig c, RrConfig tool)
        {
            MinDisplacement = c.Float("MinDisplacementBeforeCleanup", 0.1f);
            MinRotation = c.Float("MinRotationAngleBeforeCleanup", 30f);
            MinPlayerDistance = c.Float("defaultMinPlayerDistanceBeforeCleanup", 3f);
            Delay = c.Float("defaultDelayUntilCleanup", 15f);
            UseEffects = c.Bool("defaultUseVisualEffects", true);
            OnlyWhenSleeping = c.Bool("OnlyCleanupWhenSleeping");
            var audio = tool != null ? tool.Sub("audioSettings") : null;
            if (audio != null)
            {
                ResetClip = audio.Clip("ResetAudio");
            }

            SetHome();
        }

        private void Awake()
        {
            _grabbable = GetComponent<RrGrabbable>();
            _body = GetComponent<Rigidbody>();
            SetHome();
        }

        /// <summary>Home is where it is now.</summary>
        public void SetHome()
        {
            HomePosition = transform.localPosition;
            HomeRotation = transform.localRotation;
            Waited = 0f;
        }

        private void Update()
        {
            if (_busy || Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + CheckInterval;
            var net = _grabbable != null ? _grabbable.NetBody : null;
            if (net != null && !net.LocallyControlled)
            {
                Waited = 0f;
                return;
            }

            if (transform.position.y < KillHeight)
            {
                RrTools.Once("Respawn", "fell:" + name, name + " fell out of the world: back home");
                Respawn(false);
                return;
            }

            if (Delay >= 99999f || (_grabbable != null && _grabbable.IsHeld) || !Displaced() || (OnlyWhenSleeping && _body != null && !_body.IsSleeping()) ||
                RrTools.PlayerNear(transform.position, MinPlayerDistance))
            {
                Waited = 0f;
                return;
            }

            Waited += CheckInterval;
            if (Waited >= Delay)
            {
                RrTools.Once("Respawn", "first", "first cleanup: " + name + " left displaced " + Delay + " s with nobody within " + MinPlayerDistance + " m");
                Respawn(UseEffects);
            }
        }

        /// <summary>Moved or turned away from home past the thresholds.</summary>
        public bool Displaced()
        {
            float d = (transform.localPosition - HomePosition).magnitude;
            float a = Quaternion.Angle(transform.localRotation, HomeRotation);
            return d > MinDisplacement || a > MinRotation;
        }

        /// <summary>Back home now; <paramref name="animate"/> plays the out/in pop around the move.</summary>
        public void Respawn(bool animate)
        {
            if (_busy)
            {
                return;
            }

            if (_grabbable != null)
            {
                _grabbable.ForceRelease();
            }

            var anim = GetComponent<RrAnimateInOut>();
            if (animate && anim != null && anim.isActiveAndEnabled)
            {
                _busy = true;
                anim.AnimateOut(() =>
                {
                    MoveHome();
                    anim.AnimateIn();
                    _busy = false;
                });
                return;
            }

            MoveHome();
            if (anim != null && anim.isActiveAndEnabled)
            {
                anim.AnimateIn();
            }
        }

        private void MoveHome()
        {
            if (this == null)
            {
                return;
            }

            Waited = 0f;
            Respawns++;
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            transform.localPosition = HomePosition;
            transform.localRotation = HomeRotation;
            if (_body != null)
            {
                _body.position = transform.position;
                _body.rotation = transform.rotation;
                if (!_body.isKinematic)
                {
                    _body.Sleep();
                }
            }

            var net = _grabbable != null ? _grabbable.NetBody : null;
            if (net != null)
            {
                net.ResetTo(transform.position, transform.rotation);
            }

            var breakable = GetComponent<RrBreakable>();
            if (breakable != null && breakable.IsBroken)
            {
                breakable.Mend();
            }

            var drink = GetComponent<RrDrinkable>();
            if (drink != null && (drink.RefillOnRespawn || !drink.KeepEmptyOnReset))
            {
                drink.Refill();
            }

            var food = GetComponent<RrEdible>();
            if (food != null)
            {
                food.ResetBites();
            }

            var shake = GetComponent<RrShakeSound>();
            if (shake != null)
            {
                shake.ResetHistory();
            }

            RrTools.Play(ResetClip, transform.position, 1f, 20f);
            if (OnRespawned != null)
            {
                OnRespawned();
            }
        }
    }
}
