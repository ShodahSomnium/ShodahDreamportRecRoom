using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's DrinkableBottle + Potion (67 drinkables: root beer, water bottle, potions, bubbly, lattes). The first
    /// use pulls the cork (uncorkAudio: root beer Consumable_RootBeer_Uncap01 v0.75). Tipped over, an open bottle
    /// pours: the stream particles run, the fizz loop plays (pourAudio v0.1) and the liquid runs out after
    /// <see cref="LifeSpan"/> seconds of pouring (8 s root beer); an empty bottle only sputters, its loop raised by
    /// emptyPourPitchShift (0.5). While the bottle's mouth is within intersectionDrinkRadius (0.25 m) of the player's
    /// mouth (<see cref="RrMouth"/>), or the stream falls within particleDrinkRadius of it, the player drinks: a swallow
    /// clip every drinkInterval (0.68 s root beer, 0.5 potions, 1.25 bubbly). Otherwise a splash
    /// ((WaterBottleSplashFx)) appears where the stream lands every pourInterval. Potion counts the swallows:
    /// drinksToActivate (root beer 5, potions 2) within drinkResetTime (2 s) fires <see cref="OnPotionActivated"/>.
    ///
    /// Screen mode (desktop) has no wrist to tip: "use" uncorks and raises the bottle to the mouth
    /// (<see cref="RrGrabbable.ToMouth"/>, tilted 115 degrees), where it drinks until "use" is released, as Rec Room's
    /// DrinkRootBeerToolBehaviorConfig plays its drink animation. Pouring counts as tipped past
    /// <see cref="PourAngle"/> degrees from upright (GUESS: Rec Room's threshold is not in the data).
    /// </summary>
    public sealed class RrDrinkable : MonoBehaviour
    {
        public GameObject Cork;
        public Transform Mouth;
        public ParticleSystem Stream;
        public RrClipInfo UncorkClip;
        public RrClipInfo PourClip;
        public List<RrClipInfo> DrinkClips = new List<RrClipInfo>();
        public RrTarget SplashFx;
        public string SplashFxName;

        public float LifeSpan = 8f;
        public bool OnlyPourWhileHeld = true;
        public float EmptyPourPitchShift = 0.5f;
        public float PourInterval = 0.5f;
        public float DrinkInterval = 0.68f;
        public float StreamDrinkRadius = 0.25f;
        public float MouthDrinkRadius = 0.25f;
        public bool KeepEmptyOnReset = true;

        /// <summary>Dreamport: a respawned bottle is full and corked again (Rec Room keeps it empty: keepIsEmptyStateOnReset).</summary>
        public bool RefillOnRespawn = true;

        /// <summary>Tipped past this angle from upright, an open bottle pours.</summary>
        public float PourAngle = 95f;

        /// <summary>Screen mode: degrees the bottle is tipped toward the face at the mouth.</summary>
        public float ScreenTilt = 115f;

        // Potion
        public int DrinksToActivate = 5;
        public float DrinkResetTime = 2f;
        public int PowerupType = -1;
        public bool Shareable;
        public Transform LiquidLevel;
        public List<float> LiquidScaleByDrinks = new List<float>();

        public bool IsCorked { get; private set; }
        public float Remaining { get; private set; }
        public bool IsEmpty
        {
            get { return Remaining <= 0f; }
        }

        public bool IsPouring { get; private set; }
        public bool IsDrinking { get; private set; }

        /// <summary>Swallows since it was filled.</summary>
        public int Swallows { get; private set; }

        public event Action OnUncorked;
        public event Action<int> OnSwallow;
        public event Action OnEmptied;
        public event Action<int> OnPotionActivated;

        private RrGrabbable _grabbable;
        private RrBreakable _breakable;
        private AudioSource _pour;
        private float _drinkTimer;
        private float _splashTimer;
        private float _emptyPourTime;
        private bool _screenDrink;
        private int _potionCount;
        private float _lastSwallow = -100f;
        private Vector3 _liquidBase = Vector3.one;
        private bool _hadCork;

        public void Setup(RrInstance inst, RrConfig c, RrConfig potion)
        {
            var cork = RrTools.NodeOf(inst, c, "cork");
            Cork = cork != null && cork != transform ? cork.gameObject : null;
            var cap = RrTools.NodeOf(inst, c, "capTransform");
            var mouth = RrTools.NodeOf(inst, c, "mouthTrigger");
            Mouth = mouth != null ? mouth : cap;
            var stream = RrTools.NodeOf(inst, c, "waterParticles");
            Stream = stream != null ? stream.GetComponentInChildren<ParticleSystem>(true) : null;
            if (Stream == null && cap != null)
            {
                Stream = cap.GetComponentInChildren<ParticleSystem>(true);
            }

            UncorkClip = c.Clip("uncorkAudio");
            PourClip = c.Clip("pourAudio");
            DrinkClips = c.Clips("drinkAudioClips");
            SplashFx = c.Ref("drinkSplashPrefab");
            SplashFxName = SplashFx.IsNull ? null : "(WaterBottleSplashFx)";
            LifeSpan = c.Float("lifeSpan", 8f);
            OnlyPourWhileHeld = c.Bool("onlyPourWhileHeld", true);
            EmptyPourPitchShift = c.Float("emptyPourPitchShift", 0.5f);
            PourInterval = Mathf.Max(0.05f, c.Float("pourInterval", 0.5f));
            DrinkInterval = Mathf.Max(0.1f, c.Float("drinkInterval", 0.68f));
            StreamDrinkRadius = c.Float("particleDrinkRadius", 0.25f);
            MouthDrinkRadius = c.Float("intersectionDrinkRadius", 0.25f);
            KeepEmptyOnReset = c.Bool("keepIsEmptyStateOnReset", true);
            if (potion != null)
            {
                DrinksToActivate = Mathf.Max(1, potion.Int("drinksToActivate", 5));
                DrinkResetTime = potion.Float("drinkResetTime", 2f);
                PowerupType = potion.Int("powerupType", -1);
                Shareable = potion.Bool("shareable");
                LiquidLevel = RrTools.NodeOf(inst, potion, "liquidLevelTransform");
                LiquidScaleByDrinks = potion.Floats("liquidScaleByDrinks");
            }

            if (LiquidLevel != null)
            {
                _liquidBase = LiquidLevel.localScale;
            }

            _hadCork = Cork != null && Cork.activeSelf;
            IsCorked = _hadCork;
            Remaining = LifeSpan;
            if (_grabbable != null && Mouth != null)
            {
                _grabbable.MouthPoint = Mouth;
                _grabbable.MouthTilt = ScreenTilt;
            }

            SetStream(false);
        }

        private void Awake()
        {
            _grabbable = GetComponent<RrGrabbable>();
            Remaining = LifeSpan;
        }

        private void OnEnable()
        {
            if (_grabbable == null)
            {
                _grabbable = GetComponent<RrGrabbable>();
            }

            if (_grabbable != null)
            {
                _grabbable.OnUseDown += UseDown;
                _grabbable.OnUseUp += UseUp;
                _grabbable.OnReleased += Released;
                if (Mouth != null)
                {
                    _grabbable.MouthPoint = Mouth;
                    _grabbable.MouthTilt = ScreenTilt;
                }
            }
        }

        private void OnDisable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnUseDown -= UseDown;
                _grabbable.OnUseUp -= UseUp;
                _grabbable.OnReleased -= Released;
                _grabbable.ToMouth = 0f;
            }

            StopPouring();
        }

        private void UseDown(RrHand hand)
        {
            if (IsCorked)
            {
                Uncork();
            }

            if (hand == null ? !RrPlayer.InVr : hand.IsScreenHand)
            {
                _screenDrink = true;
            }
        }

        private void UseUp(RrHand hand)
        {
            _screenDrink = false;
        }

        private void Released(RrHand hand, Vector3 velocity)
        {
            _screenDrink = false;
            if (_grabbable != null)
            {
                _grabbable.ToMouth = 0f;
            }
        }

        /// <summary>Pulls the cork (the first use).</summary>
        public void Uncork()
        {
            if (!IsCorked)
            {
                return;
            }

            IsCorked = false;
            if (Cork != null)
            {
                Cork.SetActive(false);
            }

            Vector3 p = Mouth != null ? Mouth.position : transform.position;
            RrTools.Play(UncorkClip, p, 1f, 12f);
            RrTools.Once("Drink", "uncork", "uncorked " + name + " (life span " + LifeSpan + " s, swallow every " + DrinkInterval + " s)");
            if (OnUncorked != null)
            {
                OnUncorked();
            }
        }

        /// <summary>Full and corked again (respawn, a refill station).</summary>
        public void Refill()
        {
            StopPouring();
            Remaining = LifeSpan;
            Swallows = 0;
            _potionCount = 0;
            IsCorked = _hadCork;
            if (Cork != null)
            {
                Cork.SetActive(_hadCork);
            }

            SetLiquid();
        }

        /// <summary>Screen-mode drink held from code (the harness; a game's "drink" button).</summary>
        public void SetScreenDrink(bool on)
        {
            if (on && IsCorked)
            {
                Uncork();
            }

            _screenDrink = on;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool held = _grabbable != null && _grabbable.IsHeld;
            if (_grabbable != null)
            {
                float target = held && _screenDrink ? 1f : 0f;
                _grabbable.ToMouth = Mathf.MoveTowards(_grabbable.ToMouth, target, dt / 0.35f);
            }

            if (_breakable == null)
            {
                _breakable = GetComponent<RrBreakable>();
            }

            bool screenAtMouth = held && _screenDrink && _grabbable.ToMouth >= 0.95f;
            bool tipped = screenAtMouth || Tipped();
            bool canPour = !IsCorked && tipped && (held || !OnlyPourWhileHeld) && (_breakable == null || !_breakable.IsBroken);
            if (!canPour)
            {
                if (IsPouring)
                {
                    StopPouring();
                }

                _emptyPourTime = 0f;
                return;
            }

            if (IsEmpty)
            {
                // An empty bottle sputters for a moment at a higher pitch, then goes quiet.
                _emptyPourTime += dt;
                bool sputter = _emptyPourTime < 0.6f;
                if (sputter && _pour == null)
                {
                    _pour = RrTools.Loop(PourClip, Mouth != null ? Mouth : transform);
                    RrTools.SetLoop(_pour, PourClip, 0.6f, EmptyPourPitchShift);
                }
                else if (!sputter && _pour != null)
                {
                    RrSound.Stop(_pour);
                    _pour = null;
                }

                SetStream(false);
                IsPouring = false;
                IsDrinking = false;
                return;
            }

            if (!IsPouring)
            {
                IsPouring = true;
                _drinkTimer = 0f;
                _splashTimer = 0f;
                SetStream(true);
                if (_pour == null)
                {
                    _pour = RrTools.Loop(PourClip, Mouth != null ? Mouth : transform);
                }
            }

            Remaining -= dt;
            IsDrinking = screenAtMouth || AtMouth();
            if (IsDrinking)
            {
                _drinkTimer += dt;
                if (_drinkTimer >= DrinkInterval)
                {
                    _drinkTimer -= DrinkInterval;
                    Swallow();
                }
            }
            else
            {
                _drinkTimer = 0f;
                _splashTimer += dt;
                if (_splashTimer >= PourInterval)
                {
                    _splashTimer = 0f;
                    Splash();
                }
            }

            if (Remaining <= 0f)
            {
                Remaining = 0f;
                StopPouring();
                RrTools.Once("Drink", "empty", name + " is empty after " + LifeSpan + " s of pouring (" + Swallows + " swallows)");
                if (OnEmptied != null)
                {
                    OnEmptied();
                }
            }
        }

        private bool Tipped()
        {
            Vector3 axis = transform.up;
            if (Mouth != null)
            {
                Vector3 d = Mouth.position - transform.position;
                if (d.sqrMagnitude > 1e-4f)
                {
                    axis = d.normalized;
                }
            }

            return Vector3.Angle(axis, Vector3.up) > PourAngle;
        }

        /// <summary>The bottle's mouth at the player's mouth, or its stream falling onto it.</summary>
        private bool AtMouth()
        {
            Vector3 m = Mouth != null ? Mouth.position : transform.position;
            Vector3 lips = RrMouth.Position;
            if ((m - lips).sqrMagnitude < MouthDrinkRadius * MouthDrinkRadius)
            {
                return true;
            }

            // The stream: straight down from the bottle's mouth, 0.6 m long.
            if (m.y < lips.y - StreamDrinkRadius)
            {
                return false;
            }

            Vector3 onStream = new Vector3(m.x, Mathf.Clamp(lips.y, m.y - 0.6f, m.y), m.z);
            return (onStream - lips).sqrMagnitude < StreamDrinkRadius * StreamDrinkRadius;
        }

        private void Swallow()
        {
            Swallows++;
            RrTools.PlayAny(DrinkClips, RrMouth.Position, 1f, 12f);
            if (Time.time - _lastSwallow > DrinkResetTime)
            {
                _potionCount = 0;
            }

            _lastSwallow = Time.time;
            _potionCount++;
            SetLiquid();
            RrTools.Once("Drink", "swallow", "first swallow from " + name);
            if (OnSwallow != null)
            {
                OnSwallow(Swallows);
            }

            if (_potionCount >= DrinksToActivate)
            {
                _potionCount = 0;
                RrTools.Once("Drink", "potion", name + ": potion effect after " + DrinksToActivate + " swallows (powerup " + PowerupType + ")");
                if (OnPotionActivated != null)
                {
                    OnPotionActivated(PowerupType);
                }
            }
        }

        private void Splash()
        {
            Vector3 m = Mouth != null ? Mouth.position : transform.position;
            RaycastHit hit;
            if (Physics.Raycast(m, Vector3.down, out hit, 3f, RrLayers.WorldMask, QueryTriggerInteraction.Ignore))
            {
                RrTools.SpawnFx(SplashFx, SplashFxName, hit.point, Quaternion.identity);
            }
        }

        private void SetLiquid()
        {
            if (LiquidLevel == null || LiquidScaleByDrinks == null || LiquidScaleByDrinks.Count == 0)
            {
                return;
            }

            float s = LiquidScaleByDrinks[Mathf.Clamp(Swallows, 0, LiquidScaleByDrinks.Count - 1)];
            LiquidLevel.localScale = new Vector3(_liquidBase.x, _liquidBase.y * s, _liquidBase.z);
        }

        private void SetStream(bool on)
        {
            if (Stream == null)
            {
                return;
            }

            if (on)
            {
                if (!Stream.gameObject.activeSelf)
                {
                    Stream.gameObject.SetActive(true);
                }

                Stream.Play(true);
            }
            else if (Stream.isPlaying)
            {
                Stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Stops the stream and the pour loop (also when it breaks).</summary>
        public void StopPouring()
        {
            IsPouring = false;
            IsDrinking = false;
            SetStream(false);
            if (_pour != null)
            {
                RrSound.Stop(_pour);
                _pour = null;
            }
        }
    }
}
