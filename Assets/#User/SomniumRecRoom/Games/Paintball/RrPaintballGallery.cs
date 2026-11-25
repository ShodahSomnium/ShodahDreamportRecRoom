using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// The lobby's shooting gallery (the 2025 lobby: "a wooden booth with a waist-high counter, opening onto a yard
    /// fenced with wooden planks... round bullseye targets on posts and on cable spools... a yellow START button on
    /// the counter, and a small score sign (NOW / BEST)", rr_paintball_footage.md). The booth, the yard and the
    /// targets are the level's own; this adds the round: START on the level's own button, 30 s, one point per hit
    /// on any of the gallery's standees and bullseyes (practice targets in the hit-volume kit, so every gun's
    /// paint counts as it would on a player), NOW and BEST on a sign over the counter. Local only (each player's
    /// own round); the pistol on the counter is the gallery's gun.
    /// </summary>
    public sealed class RrPaintballGallery : MonoBehaviour
    {
        public const float RoundSeconds = 30f;

        private readonly List<int> _targets = new List<int>();
        private readonly List<Transform> _targetRoots = new List<Transform>();
        private TextMeshPro _sign;
        private float _endsAt = -1f;
        private int _now;
        private int _best;
        private float _nextSign;

        public bool Running
        {
            get { return _endsAt > 0f; }
        }

        public int Score
        {
            get { return _now; }
        }

        public static RrPaintballGallery Current { get; private set; }

        internal static RrPaintballGallery Create(RrPaintballRoom room, Transform lobby)
        {
            var go = new GameObject("Shooting Gallery");
            go.transform.SetParent(lobby, false);
            var g = go.AddComponent<RrPaintballGallery>();
            Current = g;
            Vector3 start = RrPaintballLobby.Local(RrPaintballLobby.GalleryStartScene);
            var press = new GameObject("Gallery START");
            press.transform.SetParent(go.transform, false);
            press.transform.localPosition = start + new Vector3(0f, 0.05f, 0f);
            press.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.35f, 0.5f);
            RrPressable.Make(press, g.Begin);
            // The sign over the counter, facing into the booth (the targets lie west, the shooter faces them).
            g._sign = RrBuild.Sign(go.transform, "Gallery Score", "", start + new Vector3(0.3f, 1.25f, 0f), Quaternion.Euler(0f, -90f, 0f), 2f, RrLook.Yellow, true, 1.8f);
            foreach (var p in RrPaintballLobby.GalleryTargets)
            {
                Vector3 local = RrPaintballLobby.Local(p);
                bool standee = p.y < -5f;
                var t = new GameObject(standee ? "Gallery Standee" : "Gallery Bullseye").transform;
                t.SetParent(go.transform, false);
                // A standee is a person-sized cut-out (hit volumes from its feet); a bullseye a head-sized disc.
                t.localPosition = standee ? local : local + Vector3.down * 1.3f;
                var head = new GameObject("Head").transform;
                head.SetParent(t, false);
                head.localPosition = new Vector3(0f, standee ? 1.55f : 1.3f, 0f);
                g._targetRoots.Add(t);
            }

            return g;
        }

        private void OnEnable()
        {
            RrCombat.OnHit += OnHit;
        }

        private void OnDisable()
        {
            RrCombat.OnHit -= OnHit;
            End(false);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>START on the counter: a 30 s round.</summary>
        public void Begin()
        {
            if (Running)
            {
                return;
            }

            _now = 0;
            _endsAt = Time.time + RoundSeconds;
            for (int i = 0; i < _targetRoots.Count; i++)
            {
                _targets.Add(RrHitVolumes.AddTarget(_targetRoots[i], _targetRoots[i].Find("Head")));
            }

            RrSound.PlayUi("Start", 0.8f);
            RrAnnouncer.Say("Gameon");
            RrLog.Info("Paintball", "shooting gallery: round started (" + _targets.Count + " targets, " + RoundSeconds + " s)");
        }

        private void End(bool announce)
        {
            if (!Running)
            {
                return;
            }

            _endsAt = -1f;
            for (int i = 0; i < _targets.Count; i++)
            {
                RrHitVolumes.RemoveTarget(_targets[i]);
            }

            _targets.Clear();
            if (_now > _best)
            {
                _best = _now;
            }

            if (announce)
            {
                RrSound.PlayUi("Start", 0.6f);
                RrLog.Info("Paintball", "shooting gallery: " + _now + " hits (best " + _best + ")");
            }
        }

        private void OnHit(RrCombat.HitInfo h)
        {
            if (!Running || h.Shooter != RrNet.LocalId || !_targets.Contains(h.Victim))
            {
                return;
            }

            _now++;
            RrSound.PlayUi("ScoreSFX", 0.4f);
        }

        private void Update()
        {
            if (Running && Time.time >= _endsAt)
            {
                End(true);
            }

            if (_sign != null && Time.time >= _nextSign)
            {
                _nextSign = Time.time + 0.2f;
                _sign.text = Running ? "NOW " + _now + "   " + Mathf.CeilToInt(_endsAt - Time.time) + "s\nBEST " + _best : "PRESS START\nNOW " + _now + "  BEST " + _best;
            }
        }

        /// <summary>Harness: a round with n hits scored through the real hit path's bookkeeping.</summary>
        internal void TestScore(int n)
        {
            Begin();
            for (int i = 0; i < n && _targets.Count > 0; i++)
            {
                OnHit(new RrCombat.HitInfo { Shooter = RrNet.LocalId, Victim = _targets[i % _targets.Count], Damage = 18 });
            }

            _endsAt = Time.time;
        }
    }
}
