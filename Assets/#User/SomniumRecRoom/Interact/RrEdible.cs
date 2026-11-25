using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's DisposableItem, the food (88 pieces: pizza slices, apples, cakes, donuts, sushi...). The piece carries
    /// one visual per stage (<c>visualsAfterEachBite</c>: pizza whole / half / last) and advances one stage per bite.
    /// A bite happens while the piece is held within sqrt(minDistanceFromHeadSq) of the mouth (0.2 m; cake 0.32) and
    /// minBiteInterval has passed (pizza, apple, donut 1.5 s; cake, chocolate 1 s). Each bite plays a random chew
    /// (pizza Consumable_Pizza_Chew01/02 v0.33, linear rolloff to 12 m) and the bite particles. When the last visual is
    /// eaten the piece is gone; an inedible last piece (cake-pop stick, bubble tea cup) stays and is cleaned up after
    /// inedibleCleanupDelay; with destroyFinalPieceOnCollision (pizza) a final piece that hits something vanishes.
    ///
    /// Screen mode: "use" brings the piece to the mouth (<see cref="RrGrabbable.ToMouth"/>) for one bite, as Rec Room's
    /// Eat...ToolBehaviorConfig plays its eat animation. A piece with an <see cref="RrRespawnable"/> comes back whole
    /// at home when finished (Dreamport: a dorm's food does not run out); otherwise it is destroyed as in Rec Room.
    /// </summary>
    public sealed class RrEdible : MonoBehaviour
    {
        public readonly List<GameObject> Visuals = new List<GameObject>();
        public float MinBiteInterval = 1.5f;
        public float BiteDistance = 0.2f;
        public bool LastPieceInedible;
        public float InedibleCleanupDelay;
        public bool DestroyFinalPieceOnCollision;
        public List<RrClipInfo> BiteClips = new List<RrClipInfo>();
        public float AudioMaxDistance = 12f;
        public readonly List<ParticleSystem> BiteParticles = new List<ParticleSystem>();
        public Transform EatPoint;

        /// <summary>Respawn whole when finished (needs an RrRespawnable), instead of being destroyed.</summary>
        public bool RespawnWhenEaten = true;

        /// <summary>Screen mode: degrees the piece tips toward the face at the mouth.</summary>
        public float ScreenTilt = 15f;

        /// <summary>Bites a whole piece takes.</summary>
        public int Bites
        {
            get { return Mathf.Max(0, LastPieceInedible ? Visuals.Count - 1 : Visuals.Count); }
        }

        /// <summary>Bites taken so far (the visible visual's index).</summary>
        public int Stage { get; private set; }

        public int BitesLeft
        {
            get { return Mathf.Max(0, Bites - Stage); }
        }

        public bool IsFinished { get; private set; }

        public event Action<int> OnBite;
        public event Action OnEaten;

        private RrGrabbable _grabbable;
        private float _lastBite = -100f;
        private bool _screenEat;
        private float _inedibleSince = -1f;

        public void Setup(RrInstance inst, RrConfig c)
        {
            Visuals.Clear();
            foreach (var t in RrTools.NodesOf(inst, c, "visualsAfterEachBite"))
            {
                if (t != transform)
                {
                    Visuals.Add(t.gameObject);
                }
            }

            MinBiteInterval = c.Float("minBiteInterval", 1.5f);
            BiteDistance = Mathf.Sqrt(Mathf.Max(0.0001f, c.Float("minDistanceFromHeadSq", 0.04f)));
            LastPieceInedible = c.Bool("isLastPieceNonEdible");
            InedibleCleanupDelay = c.Float("inedibleCleanupDelay");
            DestroyFinalPieceOnCollision = c.Bool("destroyFinalPieceOnCollision");
            BiteClips = c.Clips("biteAudioClips");
            var rolloff = c.Sub("audioRolloff");
            if (rolloff != null)
            {
                AudioMaxDistance = Mathf.Max(2f, rolloff.Float("maxDistance", 12f));
            }

            BiteParticles.Clear();
            foreach (var t in RrTools.NodesOf(inst, c, "biteParticles"))
            {
                var ps = t.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    BiteParticles.Add(ps);
                }
            }

            EatPoint = RrTools.NodeOf(inst, c, "eatPositionOverride");
            ShowStage(0);
            if (_grabbable != null)
            {
                _grabbable.MouthPoint = EatPoint;
                _grabbable.MouthTilt = ScreenTilt;
            }
        }

        private void Awake()
        {
            _grabbable = GetComponent<RrGrabbable>();
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
                _grabbable.OnReleased += Released;
                _grabbable.MouthPoint = EatPoint;
                _grabbable.MouthTilt = ScreenTilt;
            }
        }

        private void OnDisable()
        {
            if (_grabbable != null)
            {
                _grabbable.OnUseDown -= UseDown;
                _grabbable.OnReleased -= Released;
                _grabbable.ToMouth = 0f;
            }

            _screenEat = false;
        }

        private void UseDown(RrHand hand)
        {
            if (hand == null ? !RrPlayer.InVr : hand.IsScreenHand)
            {
                ScreenBite();
            }
        }

        /// <summary>Screen mode: raise to the mouth for one bite (also the test hook).</summary>
        public void ScreenBite()
        {
            if (BitesLeft > 0 && Time.time - _lastBite >= MinBiteInterval)
            {
                _screenEat = true;
            }
        }

        private void Released(RrHand hand, Vector3 velocity)
        {
            _screenEat = false;
            if (_grabbable != null)
            {
                _grabbable.ToMouth = 0f;
            }
        }

        private void Update()
        {
            if (IsFinished || _grabbable == null)
            {
                return;
            }

            bool held = _grabbable.IsHeld;
            _grabbable.ToMouth = Mathf.MoveTowards(_grabbable.ToMouth, held && _screenEat ? 1f : 0f, Time.deltaTime / 0.3f);
            if (held && BitesLeft > 0 && Time.time - _lastBite >= MinBiteInterval)
            {
                bool screenAtMouth = _screenEat && _grabbable.ToMouth >= 0.95f;
                Vector3 p = EatPoint != null ? EatPoint.position : transform.position;
                if (screenAtMouth || (p - RrMouth.Position).sqrMagnitude < BiteDistance * BiteDistance)
                {
                    _screenEat = false;
                    Bite();
                }
            }

            if (LastPieceInedible && BitesLeft == 0 && !held)
            {
                if (_inedibleSince < 0f)
                {
                    _inedibleSince = Time.time;
                }
                else if (InedibleCleanupDelay > 0f && Time.time - _inedibleSince >= InedibleCleanupDelay)
                {
                    Finish();
                }
            }
            else
            {
                _inedibleSince = -1f;
            }
        }

        /// <summary>Takes one bite now (the test hook; the behaviour calls it at the mouth).</summary>
        public void Bite()
        {
            if (IsFinished || BitesLeft <= 0)
            {
                return;
            }

            _lastBite = Time.time;
            RrTools.PlayAny(BiteClips, RrMouth.Position, 1f, AudioMaxDistance);
            for (int i = 0; i < BiteParticles.Count; i++)
            {
                var ps = BiteParticles[i];
                if (ps != null)
                {
                    ps.gameObject.SetActive(true);
                    ps.Play(true);
                }
            }

            int stage = Stage + 1;
            RrTools.Once("Eat", "first", "first bite of " + name + " (" + Bites + " bites, every " + MinBiteInterval + " s within " + BiteDistance.ToString("0.00") + " m)");
            if (OnBite != null)
            {
                OnBite(Mathf.Max(0, Bites - stage));
            }

            if (stage >= Visuals.Count)
            {
                Stage = stage;
                Finish();
                return;
            }

            ShowStage(stage);
        }

        private void ShowStage(int stage)
        {
            Stage = stage;
            for (int i = 0; i < Visuals.Count; i++)
            {
                if (Visuals[i] != null)
                {
                    Visuals[i].SetActive(i == stage);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!DestroyFinalPieceOnCollision || IsFinished || Visuals.Count == 0 || Stage != Visuals.Count - 1)
            {
                return;
            }

            if (_grabbable != null && (_grabbable.IsHeld || Time.time - _grabbable.LastReleaseTime < 0.15f))
            {
                return;
            }

            if (collision.collider != null && collision.collider.gameObject.layer != 7 && collision.relativeVelocity.sqrMagnitude > 1f)
            {
                Finish();
            }
        }

        /// <summary>Gone: eaten, or the final piece hit something.</summary>
        private void Finish()
        {
            if (IsFinished)
            {
                return;
            }

            IsFinished = true;
            RrTools.Once("Eat", "finished", name + " finished");
            if (OnEaten != null)
            {
                OnEaten();
            }

            var respawn = GetComponent<RrRespawnable>();
            if (_grabbable != null)
            {
                _grabbable.ForceRelease();
            }

            if (RespawnWhenEaten && respawn != null)
            {
                for (int i = 0; i < Visuals.Count; i++)
                {
                    if (Visuals[i] != null)
                    {
                        Visuals[i].SetActive(false);
                    }
                }

                respawn.Respawn(false);
                return;
            }

            Destroy(gameObject);
        }

        /// <summary>Whole again (respawn).</summary>
        public void ResetBites()
        {
            IsFinished = false;
            _screenEat = false;
            _inedibleSince = -1f;
            ShowStage(0);
        }
    }
}
