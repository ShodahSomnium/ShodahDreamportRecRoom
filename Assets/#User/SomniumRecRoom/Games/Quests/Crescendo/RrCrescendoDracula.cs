using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using C = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo.RrCrescendoRoom;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo
{
    /// <summary>
    /// Dracula in the Concert Hall (rr_latest_rgames_story.md 7.1, the final behaviour; simplified). Dracula is not in the
    /// install: the musket skeleton's body (DraculaBellhop) stands in, 1.35 x, and only the light mirrors hurt him.
    ///
    /// - He starts at his organ on the stage (the level's DraculaBossFight/SpawnPoint), then moves between the stage
    ///   front and the two balconies (the fight's own spawn points), firing a horizontal line of 5 fireballs at the team:
    ///   1 burst per stand, 2 after the first mirror hit, 3 after the second (the wiki's "up to 3 waves before he
    ///   teleports again").
    /// - Standing next to him is an instant down (the "dog bite zone").
    /// - The three light mirrors ([Quest_Dracula1_MirrorCannon], at the level's poses) are fired with the button on
    ///   their stand: the beam goes from the mirror to him and hits when nothing of the hall is in between (GUESS: Rec
    ///   Room's mirrors are aimed by hand; here they aim themselves and the team picks the mirror that sees him). A
    ///   mirror fires once per press, then recharges 6 s (the 2024 fix: "the mirror kept shooting every second").
    /// - A hit turns him into a cloud of bats: he is gone, decoy bats swarm (no silver), his minions come in at the side
    ///   doors (one maid on the top-right balcony), and one bat - bigger here - is the real Dracula. Knocking it out
    ///   brings him back and the decoys go. Three hits kill him (the boss's death ends the fight).
    /// - While he is on stage, 2 minions come in every 16 s (up to 7 alive): every kind except the fire bats.
    ///
    /// The authority runs the fight in the quest's ints (BossPhase, the room's QHits and QBeam); every client shows it.
    /// </summary>
    public sealed class RrCrescendoDracula : MonoBehaviour
    {
        public const int PhaseIntro = 0, PhaseFight = 1, PhaseBats = 2;
        public const int HitsToKill = 3;
        public const float BodyScale = 1.35f, IntroSeconds = 4f, VolleyEvery = 2.6f, AddsEvery = 16f, MirrorCooldown = 6f, BiteRange = 1.6f;
        public const string BeamClip = "Quest_Dracula1_WeaponChargeRelease01";
        public const string TeleportClip = "Quest_Dracula1_WhipTraversal_Teleport_01";

        /// <summary>The organ point (act-local): where he starts, and where the victory silver pops.</summary>
        public Transform Stage;

        private sealed class Mirror
        {
            public Transform Root;
            public LineRenderer Beam;
            public float BeamUntil;
            public float ReadyAt;
        }

        private readonly List<Vector3> _stands = new List<Vector3>();
        private readonly List<Vector3> _doors = new List<Vector3>();
        private readonly List<Vector3> _bats = new List<Vector3>();
        private readonly List<Vector3> _balcony = new List<Vector3>();
        private readonly List<Mirror> _mirrors = new List<Mirror>();

        // every client
        private RrEnemy _count;
        private int _seenBeam = -1;
        private float _biteGuard;
        private bool _hidden;

        // authority
        private float _phaseAt = -1f, _nextVolley, _nextAdds;
        private int _burstsLeft, _stand;
        private RrEnemy _realBat;
        private readonly List<RrEnemy> _decoys = new List<RrEnemy>();

        public static RrCrescendoDracula Create(Transform actRoot, List<Vector3> stands, List<Pose> mirrors, List<Vector3> doors, List<Vector3> bats, List<Vector3> balconyRight)
        {
            var go = RrBuild.Group(actRoot, "Dracula");
            var d = go.AddComponent<RrCrescendoDracula>();
            d._stands.AddRange(stands);
            d._doors.AddRange(doors);
            d._bats.AddRange(bats);
            d._balcony.AddRange(balconyRight);
            if (d._stands.Count == 0)
            {
                d._stands.Add(new Vector3(36f, -2.3f, 0f));
            }

            d.Stage = RrBuild.Group(go.transform, "Organ", d._stands[0]).transform;
            for (int i = 0; i < mirrors.Count && i < 3; i++)
            {
                d.BuildMirror(go.transform, mirrors[i], i);
            }

            RrLog.Info("Crescendo", "Concert Hall: Dracula with " + d._stands.Count + " stands, " + d._mirrors.Count + " light mirrors, " + d._doors.Count + " door points, " + d._bats.Count + " bat points");
            return d;
        }

        /// <summary>A light mirror on its stand at the level's pose, turned towards the stage, with its fire button.</summary>
        private void BuildMirror(Transform parent, Pose pose, int index)
        {
            Vector3 to = _stands[0] - pose.position;
            to.y = 0f;
            var rot = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to.normalized) : pose.rotation;
            var root = RrBuild.Group(parent, "Light Mirror " + (index + 1), pose.position, rot).transform;
            var dark = RrQuestKit.WoodDark ?? RrLook.Solid(RrLook.DarkWood);
            var gold = RrQuestKit.Gold ?? RrLook.Solid(RrLook.Yellow);
            RrBuild.Box(root, "Post", new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.1f), dark, true);
            RrBuild.Box(root, "Foot", new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.08f, 0.6f), dark, true);
            RrBuild.Box(root, "Frame", new Vector3(0f, 1.65f, 0f), new Vector3(1.0f, 1.2f, 0.08f), gold, true);
            RrBuild.Box(root, "Glass", new Vector3(0f, 1.65f, 0.045f), new Vector3(0.86f, 1.06f, 0.02f), RrLook.Emissive(new Color(0.85f, 0.9f, 1f), 0.6f), false);
            RrBuild.PointLight(root, "Glint", new Vector3(0f, 1.65f, 0.4f), new Color(0.8f, 0.9f, 1f), 4f, 0.6f);
            int i = index;
            RrPressable.Create(root, "Fire Mirror", new Vector3(0f, 1.0f, -0.12f), Quaternion.Euler(-60f, 0f, 0f), new Color(1f, 0.85f, 0.3f), 0.2f, () =>
            {
                var q = RrQuestRoom.Current as C;
                if (q != null)
                {
                    q.FireMirror(i);
                }
            });
            var line = new GameObject("Beam").AddComponent<LineRenderer>();
            line.transform.SetParent(root, false);
            line.positionCount = 2;
            line.startWidth = 0.18f;
            line.endWidth = 0.1f;
            line.sharedMaterial = RrLook.Emissive(new Color(1f, 0.95f, 0.75f), 4f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            _mirrors.Add(new Mirror { Root = root, Beam = line });
        }

        private Vector3 Muzzle(Mirror m)
        {
            return m.Root.TransformPoint(new Vector3(0f, 1.65f, 0.1f));
        }

        // ------------------------------------------------------------------ every client

        /// <summary>Every client, as his body is ready: the stand-in body, big, still, and out of reach of weapons.</summary>
        public void Setup(RrEnemy e)
        {
            _count = e;
            _hidden = false;
            e.Passive = true;
            e.MoveSpeed = 0f;
            e.AggroRange = 80f;
            e.MaxHealth = 100000;
            e.Health = e.MaxHealth;
            e.transform.localScale = Vector3.one * BodyScale;
            RrLog.Info("Crescendo", "Dracula appears at his organ (" + e.Prefab + " body, x" + BodyScale + ")");
        }

        /// <summary>Every client: a decoy bat, or the real Dracula (bigger).</summary>
        public void SetupBat(RrEnemy e, bool real)
        {
            e.transform.localScale = Vector3.one * (real ? 1.6f : 1f);
            if (real)
            {
                e.MaxHealth = Mathf.Max(e.MaxHealth, 150);
                e.Health = e.MaxHealth;
            }
        }

        /// <summary>
        /// Every client, every frame in the Concert Hall (active: the run is on and the intro title is over): his body
        /// hidden while he is bats, the mirror beams, and the bite zone on the local player.
        /// </summary>
        public void ClientStep(C room, bool active)
        {
            var e = _count != null && _count.Alive ? _count : null;
            int phase = room.QuestInt(RrQuestInts.BossPhase);
            bool hide = active && phase == PhaseBats;
            if (e != null && hide != _hidden)
            {
                _hidden = hide;
                e.transform.localScale = Vector3.one * (hide ? 0.001f : BodyScale);
            }

            int beam = room.QuestInt(C.QBeam);
            if (beam != _seenBeam)
            {
                if (_seenBeam >= 0 && active)
                {
                    ShowBeam(room, (beam >> 1) & 3, (beam & 1) == 1, e);
                }

                _seenBeam = beam;
            }

            foreach (var m in _mirrors)
            {
                if (m.Beam != null && m.Beam.enabled && Time.time > m.BeamUntil)
                {
                    m.Beam.enabled = false;
                }
            }

            if (active && e != null && phase == PhaseFight && Time.time > _biteGuard)
            {
                Vector3 feet = RrPlayer.FeetPosition;
                Vector3 d = feet - e.transform.position;
                float dy = Mathf.Abs(d.y);
                d.y = 0f;
                var h = RrHealth.Local;
                if (d.magnitude < BiteRange && dy < 2f && h != null && h.InCombat && !h.IsOut && !h.Invincible)
                {
                    _biteGuard = Time.time + 2f;
                    RrQuestEnemies.HurtLocal(100, feet + Vector3.up);
                    room.Hud.Toast("Bitten! Keep your distance from Dracula", 2.5f);
                }
            }
        }

        private void ShowBeam(C room, int index, bool hit, RrEnemy e)
        {
            if (index < 0 || index >= _mirrors.Count)
            {
                return;
            }

            var m = _mirrors[index];
            Vector3 from = Muzzle(m);
            Vector3 to = e != null ? e.Center : from + m.Root.forward * 30f;
            if (!hit)
            {
                RaycastHit rh;
                if (Physics.Raycast(from, (to - from).normalized, out rh, (to - from).magnitude, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
                {
                    to = rh.point;
                }
            }

            m.Beam.SetPosition(0, m.Beam.transform.InverseTransformPoint(from));
            m.Beam.SetPosition(1, m.Beam.transform.InverseTransformPoint(to));
            m.Beam.useWorldSpace = false;
            m.Beam.enabled = true;
            m.BeamUntil = Time.time + 0.8f;
            RrSound.Play(BeamClip, from, 1f);
            RrFx.Spawn("[Quest_Dracula1_Silver_VFX]", to, Quaternion.identity, 0.6f);
            room.Hud.Toast(hit ? "The light strikes Dracula!" : "The light missed - pick a mirror that can see him", 2.5f);
        }

        // ------------------------------------------------------------------ authority

        /// <summary>Whether the mirrors have killed him (the act's extra clear condition).</summary>
        public bool Defeated(C room)
        {
            return room.QuestInt(C.QHits) >= HitsToKill;
        }

        private RrEnemy FindCount(C room)
        {
            for (int s = 0; s < RrQuestEnemies.Slots; s++)
            {
                var e = room.Enemies.At(s);
                if (e != null && e.Alive && room.Enemies.TypeAt(s) == C.Dracula)
                {
                    return e;
                }
            }

            return null;
        }

        private void SetPhase(C room, int phase)
        {
            room.SetQuestInt(RrQuestInts.BossPhase, phase);
            _phaseAt = Time.time;
        }

        /// <summary>Authority, every frame of the Concert Hall's combat.</summary>
        public void AuthorityStep(C room)
        {
            var e = FindCount(room);
            if (e == null)
            {
                return;
            }

            int hits = room.QuestInt(C.QHits);
            if (_phaseAt < 0f)
            {
                hits = 0;
                room.SetQuestInt(C.QHits, 0);
                SetPhase(room, PhaseIntro);
                _stand = 0;
                _nextAdds = Time.time + AddsEvery;
            }

            // only the mirrors hurt him
            e.Health = e.MaxHealth;
            room.SetQuestInt(RrQuestInts.BossHealth, Mathf.Max(1, 1000 - hits * 333));
            int phase = room.QuestInt(RrQuestInts.BossPhase);
            if (phase != PhaseBats)
            {
                FaceTeam(room, e);
            }

            switch (phase)
            {
                case PhaseIntro:
                    if (Time.time - _phaseAt > IntroSeconds)
                    {
                        SetPhase(room, PhaseFight);
                        _burstsLeft = 1 + hits;
                        _nextVolley = Time.time + 1f;
                        room.Broadcast("Dracula rises from his organ!");
                    }

                    break;
                case PhaseFight:
                    if (Time.time >= _nextVolley)
                    {
                        if (_burstsLeft > 0)
                        {
                            Volley(room, e);
                            _burstsLeft--;
                            _nextVolley = Time.time + VolleyEvery;
                        }
                        else
                        {
                            Teleport(e);
                            _burstsLeft = 1 + hits;
                            _nextVolley = Time.time + 1.4f;
                        }
                    }

                    if (Time.time >= _nextAdds)
                    {
                        _nextAdds = Time.time + AddsEvery;
                        Adds(room, 2);
                    }

                    break;
                case PhaseBats:
                    if (_realBat == null || !_realBat.Alive || Time.time - _phaseAt > 45f)
                    {
                        foreach (var d in _decoys)
                        {
                            if (d != null && d.Alive)
                            {
                                d.Die(RrCombat.NoPlayer, false);
                            }
                        }

                        _decoys.Clear();
                        _realBat = null;
                        Teleport(e);
                        SetPhase(room, PhaseFight);
                        _burstsLeft = 1 + hits;
                        _nextVolley = Time.time + 1.5f;
                        _nextAdds = Time.time + AddsEvery;
                        room.Broadcast("Dracula is back!");
                        RrLog.Info("Crescendo", "the real bat is down: Dracula returns (hits " + hits + ")");
                    }

                    break;
            }
        }

        /// <summary>Authority: a player fired mirror index. A hit when he is on stage and the hall does not block the beam.</summary>
        public void AuthorityMirror(C room, int playerId, int index)
        {
            if (index < 0 || index >= _mirrors.Count)
            {
                return;
            }

            var m = _mirrors[index];
            var e = FindCount(room);
            int phase = room.QuestInt(RrQuestInts.BossPhase);
            if (e == null || Time.time < m.ReadyAt || phase == PhaseIntro)
            {
                return;
            }

            m.ReadyAt = Time.time + MirrorCooldown;
            Vector3 from = Muzzle(m);
            Vector3 to = e.Center;
            float dist = Vector3.Distance(from, to);
            RaycastHit rh;
            bool blocked = Physics.Raycast(from, (to - from).normalized, out rh, dist, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore) && rh.distance < dist - 1.5f;
            bool hit = phase == PhaseFight && !blocked && dist < 60f;
            int beam = room.QuestInt(C.QBeam);
            room.SetQuestInt(C.QBeam, (((beam >> 3) + 1) << 3) | (index << 1) | (hit ? 1 : 0));
            if (!hit)
            {
                return;
            }

            int hits = room.QuestInt(C.QHits) + 1;
            room.SetQuestInt(C.QHits, hits);
            RrLog.Info("Crescendo", "mirror " + (index + 1) + " (player " + playerId + ") hits Dracula: " + hits + "/" + HitsToKill);
            if (hits >= HitsToKill)
            {
                room.SetQuestInt(RrQuestInts.BossHealth, 0);
                e.Die(playerId, true);
                return;
            }

            SetPhase(room, PhaseBats);
            SpawnBats(room, hits, e.transform.position);
            room.Broadcast("Dracula bursts into bats! Knock out the real one - the big bat");
        }

        private void FaceTeam(C room, RrEnemy e)
        {
            var feet = room.StandingPlayerFeet();
            if (feet.Count == 0)
            {
                return;
            }

            Vector3 best = feet[0];
            float bd = float.MaxValue;
            foreach (var f in feet)
            {
                float d = (f - e.transform.position).sqrMagnitude;
                if (d < bd)
                {
                    bd = d;
                    best = f;
                }
            }

            Vector3 dir = best - e.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                e.transform.rotation = Quaternion.Slerp(e.transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 4f);
            }
        }

        /// <summary>A horizontal line of 5 fireballs at a standing player's chest.</summary>
        private void Volley(C room, RrEnemy e)
        {
            var feet = room.StandingPlayerFeet();
            if (feet.Count == 0)
            {
                return;
            }

            Vector3 target = feet[Random.Range(0, feet.Count)] + Vector3.up * 1.2f;
            Vector3 origin = e.Center + e.transform.forward * 0.7f + Vector3.up * 0.3f;
            room.Enemies.Volley(e, origin, target, 5, 44f, 9f, 0.3f, 100, 0f);
        }

        /// <summary>To the next stand (stage front, a balcony, the organ), never the one he is on.</summary>
        private void Teleport(RrEnemy e)
        {
            if (_stands.Count < 2)
            {
                return;
            }

            int next = _stand;
            while (next == _stand)
            {
                next = Random.Range(0, _stands.Count);
            }

            _stand = next;
            Vector3 from = e.transform.position;
            e.transform.position = transform.parent.TransformPoint(_stands[next]);
            RrSound.Play(TeleportClip, from, 0.9f);
            RrSound.Play(TeleportClip, e.transform.position, 0.9f);
        }

        /// <summary>Minions at the side doors (every kind but the fire bats), at most 7 alive.</summary>
        private void Adds(C room, int n)
        {
            var kinds = new[] { C.Spicer, C.Maid, C.Dog, C.Marksman, C.DiveBat, C.Spicer };
            for (int i = 0; i < n && room.Enemies.Alive < 7; i++)
            {
                int type = kinds[Random.Range(0, kinds.Length)];
                Vector3 local = type == C.Maid && _balcony.Count > 0 ? _balcony[0] : _doors.Count > 0 ? _doors[Random.Range(0, _doors.Count)] : _stands[0] + Vector3.left * 8f;
                room.Enemies.Spawn(type, transform.parent.TransformPoint(local), 0f);
            }
        }

        /// <summary>The bat cloud: 3 + hits decoys and the real bat around where he stood, and his minions at the doors.</summary>
        private void SpawnBats(C room, int hits, Vector3 where)
        {
            _decoys.Clear();
            int count = 3 + hits;
            int real = Random.Range(0, count + 1);
            for (int i = 0; i <= count; i++)
            {
                Vector3 p = _bats.Count > 0 && i % 2 == 1 ? transform.parent.TransformPoint(_bats[i % _bats.Count]) + Vector3.up * 2f
                    : where + Quaternion.Euler(0f, i * 360f / (count + 1), 0f) * new Vector3(2.5f, 2f, 0f);
                var b = room.Enemies.Spawn(i == real ? C.RealBat : C.DecoyBat, p, Random.Range(0f, 360f));
                if (i == real)
                {
                    _realBat = b;
                }
                else if (b != null)
                {
                    _decoys.Add(b);
                }
            }

            Adds(room, 2);
            if (_balcony.Count > 0 && room.Enemies.Alive < 12)
            {
                room.Enemies.Spawn(C.Maid, transform.parent.TransformPoint(_balcony[0]), 0f);
            }
        }

        /// <summary>A new run: the fight starts over.</summary>
        public void ResetBoss()
        {
            _phaseAt = -1f;
            _burstsLeft = 0;
            _decoys.Clear();
            _realBat = null;
            _count = null;
            _hidden = false;
            _seenBeam = -1;
            foreach (var m in _mirrors)
            {
                m.ReadyAt = 0f;
                if (m.Beam != null)
                {
                    m.Beam.enabled = false;
                }
            }
        }
    }
}
