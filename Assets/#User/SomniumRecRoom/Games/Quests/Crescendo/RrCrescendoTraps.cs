using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo
{
    /// <summary>
    /// The Lower Sanctum's arrow traps (rr_latest_rgames_story.md 7.1: "arrow traps (they kill; the silver penalty is
    /// x0.1), laser walls and swinging axes (these don't kill)"). Rec Room's dart shooters are level-only objects; their
    /// two "Dart Traps" encounter volumes are the danger zones. Every <see cref="Period"/> s the traps click, then fire
    /// 0.6 s later: a player standing in a zone at that moment is downed (each client judges its own player, as with
    /// every enemy hit). Each client keeps its own rhythm (a click, then the arrows).
    /// GUESS: the period and the warning (Rec Room fires on its own circuit timing).
    /// </summary>
    public sealed class RrCrescendoTraps : MonoBehaviour
    {
        public const float Period = 3.2f;
        public const float Warn = 0.6f;
        public const string ClickClip = "Goblin2_UpgradedCrossbow_BoltLoaded01";
        public const string FireClip = "Goblin2_UpgradedCrossbow_Fire01";
        public const string WhizClip = "Goblin2_UpgradedCrossbow_Whizby01";

        private readonly List<Bounds> _zones = new List<Bounds>();
        private int _lastCycle = -1;
        private bool _warned;
        private float _toastAt;

        public static RrCrescendoTraps Create(Transform actRoot, List<Bounds> zones)
        {
            var go = RrBuild.Group(actRoot, "Arrow Traps");
            var t = go.AddComponent<RrCrescendoTraps>();
            t._zones.AddRange(zones);
            RrLog.Info("Crescendo", "Lower Sanctum: " + zones.Count + " arrow-trap zones");
            return t;
        }

        /// <summary>Every client, every frame (from the room's QuestStep); active only during the Lower Sanctum's combat.</summary>
        public void Step(RrCrescendoRoom room, bool active)
        {
            if (!active || _zones.Count == 0)
            {
                _lastCycle = -1;
                return;
            }

            float clock = Time.time;
            int cycle = (int)(clock / Period);
            if (_lastCycle < 0)
            {
                _lastCycle = cycle;
                _warned = false;
                return;
            }

            float phase = clock - cycle * Period;
            if (!_warned && phase >= Period - Warn)
            {
                _warned = true;
                foreach (var z in _zones)
                {
                    RrSound.Play(ClickClip, transform.TransformPoint(z.center), 0.6f, 1f, 18f);
                }
            }

            if (cycle != _lastCycle)
            {
                _lastCycle = cycle;
                if (_warned)
                {
                    Fire(room);
                }

                _warned = false;
            }
        }

        private void Fire(RrCrescendoRoom room)
        {
            Vector3 feet = RrPlayer.FeetPosition;
            Vector3 local = transform.InverseTransformPoint(feet);
            foreach (var z in _zones)
            {
                RrSound.Play(FireClip, transform.TransformPoint(z.center), 0.7f, 1f, 22f);
                if (!z.Contains(local) && !z.Contains(local + Vector3.up))
                {
                    continue;
                }

                RrSound.Play(WhizClip, feet + Vector3.up, 0.8f);
                var h = RrHealth.Local;
                if (h != null && h.InCombat && !h.IsOut && !h.Invincible)
                {
                    RrQuestEnemies.HurtLocal(100, feet + Vector3.up);
                    if (Time.time > _toastAt)
                    {
                        _toastAt = Time.time + 3f;
                        room.Hud.Toast("Arrow trap!", 2f);
                    }
                }
            }
        }
    }
}
