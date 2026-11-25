using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// Act 3's rolling boulder (wiki: "a big stone boulder that players roll down ramps to crush groups of enemies"):
    /// Rec Room's "(LavaBoulder)" rock (Quest_Rock04 + Rocks_Frontier_Mat, bundle 85515de1) scaled to 2.2 m, on a
    /// scripted path down the three ramps. Pressing the lever sends a hazard request; the authority sets hazard bit 0
    /// with the roll's duration and starts the synced instance timer, so every client rolls its own copy to the same
    /// spot (6 m/s, GUESS) without a shared clock. The authority crushes enemies it touches (they die for gold); each
    /// client hurts only its own player (40, once per roll). At the bottom the rock bursts into dust.
    /// </summary>
    public sealed class RrGoldenTrophyBoulder : MonoBehaviour
    {
        public const float Speed = 6f;
        public const float Radius = 1.1f;

        public float Length { get; private set; }

        private Vector3[] _path;
        private float[] _cum;
        private Transform _ball;
        private GameObject _fallback;
        private bool _broken, _hitLocal;
        private float _lastDist;

        /// <summary>The rumble while it rolls: Rec Room's bowling-ball roll, pitched down for a rock (no boulder clip ships in the install; GUESS).</summary>
        private AudioSource _roll;

        private const string RollClip = "Bowling_BallRoll02";
        private const float RollPitch = 0.55f;

        private void StopRoll()
        {
            if (_roll != null)
            {
                Destroy(_roll.gameObject);
            }

            _roll = null;
        }

        private void OnDestroy()
        {
            StopRoll();
        }

        public static RrGoldenTrophyBoulder Create(RrGoldenTrophyRoom room, Transform actRoot, Vector3[] path)
        {
            var go = RrBuild.Group(actRoot, "Boulder");
            var b = go.AddComponent<RrGoldenTrophyBoulder>();
            b._path = path;
            b._cum = new float[path.Length];
            for (int i = 1; i < path.Length; i++)
            {
                b._cum[i] = b._cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            }

            b.Length = b._cum[path.Length - 1];
            b._ball = RrBuild.Group(go.transform, "Ball", path[0]).transform;
            b._fallback = RrBuild.Sphere(b._ball, "Rock", Vector3.zero, Radius, RrLook.Solid(new Color(0.52f, 0.47f, 0.42f)), false);
            RrSpawn.Spawn("(LavaBoulder)", b._ball, Vector3.zero, Quaternion.identity, inst =>
            {
                if (inst == null)
                {
                    return;
                }

                if (b == null)
                {
                    Destroy(inst.gameObject);
                    return;
                }

                var bounds = inst.WorldBounds();
                float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                if (size > 0.01f)
                {
                    inst.transform.localScale *= Radius * 2f / size;
                    bounds = inst.WorldBounds();
                    inst.transform.position += b._ball.position - bounds.center;
                }

                b._fallback.SetActive(false);
            }, RrSpawnOptions.Visual());
            return b;
        }

        public void ResetBoulder()
        {
            StopRoll();
            _broken = false;
            _hitLocal = false;
            _lastDist = 0f;
            if (_ball != null)
            {
                _ball.gameObject.SetActive(true);
                _ball.localPosition = _path[0];
                _ball.localRotation = Quaternion.identity;
            }
        }

        private Vector3 At(float d, out Vector3 dir)
        {
            for (int i = 1; i < _path.Length; i++)
            {
                if (d <= _cum[i] || i == _path.Length - 1)
                {
                    float seg = Mathf.Max(0.001f, _cum[i] - _cum[i - 1]);
                    dir = (_path[i] - _path[i - 1]) / seg;
                    return Vector3.Lerp(_path[i - 1], _path[i], Mathf.Clamp01((d - _cum[i - 1]) / seg));
                }
            }

            dir = Vector3.forward;
            return _path[_path.Length - 1];
        }

        private bool Rolling(RrGoldenTrophyRoom room, out float d)
        {
            d = 0f;
            if ((room.QuestInt(RrQuestInts.Hazards) & 1) == 0)
            {
                return false;
            }

            d = room.HazardSeconds * Speed;
            return true;
        }

        /// <summary>Every client, every frame (active: the team is in act 3 and the run is on).</summary>
        public void ClientStep(RrGoldenTrophyRoom room, bool active)
        {
            if (_ball == null)
            {
                return;
            }

            float d;
            if (!active || !Rolling(room, out d))
            {
                if (_lastDist > 0f && !active)
                {
                    ResetBoulder();
                }

                return;
            }

            if (d >= Length)
            {
                if (!_broken)
                {
                    _broken = true;
                    Vector3 end = _ball.position;
                    RrFx.Spawn("VFX_Generic_SmokePoof01", end, Quaternion.identity, 3f);
                    RrSound.Play("Quest_PotterySmash02", end, 1f, 0.5f);
                    StopRoll();
                    _ball.gameObject.SetActive(false);
                }

                return;
            }

            Vector3 dir;
            Vector3 local = At(d, out dir);
            float step = d - _lastDist;
            if (step > 0f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, dir);
                if (axis.sqrMagnitude > 1e-4f)
                {
                    _ball.localRotation = Quaternion.AngleAxis(step / Radius * Mathf.Rad2Deg, axis.normalized) * _ball.localRotation;
                }

                if (_lastDist <= 0.01f)
                {
                    RrSound.Play("Quest_Sword_ImpactHeavy01", _ball.position, 1f, 0.4f);
                }

                if (_roll == null)
                {
                    _roll = RrSound.Loop(RollClip, _ball, 1f, 35f);
                    if (_roll != null)
                    {
                        _roll.pitch = RollPitch;
                    }
                }
            }

            _lastDist = d;
            _ball.localPosition = local;
            if (!_hitLocal && RrQuestEnemies.LocalWithin(_ball.position, Radius))
            {
                _hitLocal = true;
                RrQuestEnemies.HurtLocal(40, _ball.position);
            }
        }

        /// <summary>Authority: crushes every enemy the rolling rock touches.</summary>
        public void AuthorityStep(RrGoldenTrophyRoom room)
        {
            float d;
            if (_ball == null || !Rolling(room, out d) || d >= Length || d <= 0f)
            {
                return;
            }

            Vector3 c = _ball.position;
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = room.Enemies.At(s);
                if (e != null && e.Alive && Vector3.Distance(e.Center, c) < Radius + e.Radius + 1f)
                {
                    e.Die(RrCombat.NoPlayer, true);
                    RrSound.Play("Quest_MeleeImpact_01", e.Center, 1f, 0.7f);
                }
            }
        }
    }
}
