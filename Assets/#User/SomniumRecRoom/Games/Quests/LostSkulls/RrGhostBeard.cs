using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// Ghost Beard (rr_latest_rgames_story.md 8.1; nothing of him is in the plain bundles, 8.2): the ghost pirate captain
    /// of act 8, floating over the water west of the ship the team boards. Built from our own shapes (rr_quests.md 4.3
    /// GUESS look): a translucent green body with a wispy tail, a dark beard, glowing eyes, a black tricorn with a skull.
    ///
    /// Rules (final wiki): only cannons damage him, 3 hits, first with the rear cannon, then the front, then either. He
    /// moves between the level's own [GhostBeardEncounter] Fight Positions after each hit: position 1 (north) is in the
    /// arc of the rear cannon only, position 2 (south) of the front one only, position 3 of both (the arcs of
    /// <see cref="RrLostSkullsCannon"/>, measured on the level), which gives the wiki's order. He is out of reach while he
    /// moves (2 s). He fires cannon shots at the team (a slow ball every 5 s, 2.5 m blast), and each hit brings
    /// swashbucklers over the bridges. The body is the roster's boss type (a flintlock skeleton, kept passive and
    /// hidden at his first position): it carries the health, the 10,000 gold and the framework's boss rules; the
    /// authority's cannon hits go to it.
    /// </summary>
    public sealed class RrGhostBeard : MonoBehaviour
    {
        public const int Hits = 3;
        public const int BodyHealth = 3000;
        public const float MoveSeconds = 2f;
        public const float ShotEvery = 5f;
        public const float ShotSpeed = 14f;

        private static readonly Color Ghost = new Color(0.45f, 1f, 0.6f, 0.45f);

        private Vector3[] _spots;
        private Vector3 _retreat;
        private Vector3 _face;
        private RrEnemy _e;
        private int _shownPhase = -1;
        private Vector3 _from, _to;
        private float _moveStart = -10f;
        private float _nextShot;
        private float _rehideAt;
        private float _sink;
        private float _flash;
        private Transform _model;
        private Light _light;
        private bool _laughed;

        /// <summary>The boss body while he lives (null before the fight).</summary>
        public RrEnemy Body
        {
            get { return _e; }
        }

        /// <summary>Ghost Beard under parent (act-local positions): his start, the three fight positions, the retreat point.</summary>
        public static RrGhostBeard Build(Transform parent, Vector3 start, Vector3[] spots, Vector3 retreat)
        {
            var go = RrBuild.Group(parent, "Ghost Beard", start);
            var g = go.AddComponent<RrGhostBeard>();
            g._spots = spots;
            g._retreat = retreat;
            // He faces the ship the team boards (east of the water, x -140 in the level).
            g._face = start + new Vector3(16f, 0f, 0f);
            g.BuildModel();
            g.ResetBoss();
            return g;
        }

        private void BuildModel()
        {
            _model = RrBuild.Group(transform, "Model").transform;
            var ghost = RrLook.Glass(Ghost);
            var beard = RrLook.Glass(new Color(0.12f, 0.35f, 0.2f, 0.8f));
            var hat = RrLook.Solid(new Color(0.06f, 0.06f, 0.07f));
            var eyes = RrLook.Emissive(new Color(0.6f, 1f, 0.7f), 3f);
            var bone = RrLook.Solid(new Color(0.9f, 0.88f, 0.8f));
            var m = _model;
            RrBuild.Primitive(PrimitiveType.Capsule, m, "Body", new Vector3(0f, 0.2f, 0f), Quaternion.identity, new Vector3(1.6f, 1.3f, 1.3f), ghost, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Tail", new Vector3(0f, -1.3f, -0.2f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(1.1f, 1.8f, 0.9f), ghost, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Tail Tip", new Vector3(0f, -2.5f, -0.6f), Quaternion.identity, new Vector3(0.5f, 0.8f, 0.5f), ghost, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Head", new Vector3(0f, 1.85f, 0.05f), Quaternion.identity, new Vector3(1.25f, 1.3f, 1.2f), ghost, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Cube, m, "Beard", new Vector3(0f, 1.3f, 0.5f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.95f, 1.0f, 0.3f), beard, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Eye L", new Vector3(-0.24f, 1.98f, 0.58f), Quaternion.identity, Vector3.one * 0.2f, eyes, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Eye R", new Vector3(0.24f, 1.98f, 0.58f), Quaternion.identity, Vector3.one * 0.2f, eyes, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Cylinder, m, "Hat Brim", new Vector3(0f, 2.45f, 0f), Quaternion.identity, new Vector3(1.9f, 0.04f, 1.6f), hat, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Cylinder, m, "Hat Crown", new Vector3(0f, 2.72f, 0f), Quaternion.identity, new Vector3(1.0f, 0.26f, 0.95f), hat, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Sphere, m, "Hat Skull", new Vector3(0f, 2.72f, 0.47f), Quaternion.identity, new Vector3(0.22f, 0.22f, 0.08f), bone, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Capsule, m, "Arm L", new Vector3(-1.0f, 0.6f, 0.2f), Quaternion.Euler(0f, 0f, -50f), new Vector3(0.45f, 0.8f, 0.45f), ghost, false, RrLayers.Environment);
            RrBuild.Primitive(PrimitiveType.Capsule, m, "Arm R", new Vector3(1.0f, 0.6f, 0.2f), Quaternion.Euler(0f, 0f, 50f), new Vector3(0.45f, 0.8f, 0.45f), ghost, false, RrLayers.Environment);
            var lgo = RrBuild.Group(transform, "Glow", new Vector3(0f, 1.2f, 1f));
            _light = lgo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.45f, 1f, 0.6f);
            _light.range = 12f;
            _light.intensity = 2f;
        }

        /// <summary>The boss body spawned (every client): passive, still, hidden; the health for 3 cannon hits.</summary>
        public void Setup(RrEnemy e)
        {
            _e = e;
            e.Passive = true;
            e.MoveSpeed = 0f;
            e.AggroRange = 80f;
            e.MaxHealth = BodyHealth;
            e.Health = BodyHealth;
            Hide();
            _nextShot = Time.time + 4f;
            RrLog.Info("LostSkulls", "Ghost Beard appears: " + Hits + " cannon hits, a cannon shot every " + ShotEvery + " s");
        }

        private void Hide()
        {
            if (_e == null)
            {
                return;
            }

            foreach (var r in _e.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = false;
            }

            foreach (var v in _e.GetComponentsInChildren<RrHitVolume>(true))
            {
                v.gameObject.SetActive(false);
            }

            _rehideAt = Time.time + 1f;
        }

        /// <summary>A new run: back at his start, whole and hidden until the fight.</summary>
        public void ResetBoss()
        {
            _e = null;
            _shownPhase = -1;
            _laughed = false;
            _sink = 0f;
            _from = _to = transform.localPosition = _spots != null && _spots.Length > 0 ? _spots[0] : transform.localPosition;
            if (_model != null)
            {
                _model.gameObject.SetActive(false);
            }

            if (_light != null)
            {
                _light.enabled = false;
            }
        }

        /// <summary>Where he is (world) when a cannon can hit him: in the fight, alive, not moving.</summary>
        public bool Hittable(out Vector3 world)
        {
            world = _model != null ? _model.position + Vector3.up * 0.8f : transform.position;
            return _e != null && _e.Alive && _model != null && _model.gameObject.activeInHierarchy && Time.time - _moveStart > MoveSeconds;
        }

        /// <summary>Authority: a cannon's ball reached him (the cannon's arc and his position decided it).</summary>
        public void AuthorityCannonHit(RrLostSkullsRoom room, int cannon)
        {
            Vector3 at;
            if (!Hittable(out at))
            {
                return;
            }

            int phase = Mathf.Clamp(room.QuestInt(RrQuestInts.BossPhase), 0, Hits);
            int damage = Mathf.CeilToInt(BodyHealth / (float)Hits) + 1;
            _e.TakeLocalHit(damage, RrBodyPart.Body, at, RrCombat.UnknownTool);
            room.SetQuestInt(RrQuestInts.BossPhase, Mathf.Min(Hits, phase + 1));
            room.Broadcast(phase + 1 >= Hits ? "Ghost Beard is sinking!" : "Direct hit! (" + (phase + 1) + " of " + Hits + ")");
            RrLog.Info("LostSkulls", "Ghost Beard hit by cannon " + cannon + " (" + (phase + 1) + "/" + Hits + "), health " + _e.Health);
            if (phase + 1 < Hits)
            {
                Boarders(room, phase + 1);
            }
        }

        /// <summary>After each hit: swashbucklers come over the bridges (2, then 3).</summary>
        private static void Boarders(RrLostSkullsRoom room, int phase)
        {
            var act = room.CurrentAct;
            System.Collections.Generic.List<Vector3> points;
            if (act == null || act.Root == null || !act.Groups.TryGetValue("boarders", out points) || points.Count == 0)
            {
                return;
            }

            int n = RrQuestWave.Scaled(1 + phase, room.QuestInt(RrQuestInts.Party), 3);
            for (int i = 0; i < n; i++)
            {
                room.Enemies.Spawn(RrLostSkullsRoom.Swash, act.Root.TransformPoint(points[(i + phase * 2) % points.Count]), 90f);
            }
        }

        /// <summary>Authority, every frame of the fight: the boss bar, his cannon shots at the team.</summary>
        public void AuthorityStep(RrLostSkullsRoom room)
        {
            var e = _e;
            if (e == null)
            {
                return;
            }

            if (!e.Alive)
            {
                room.SetQuestInt(RrQuestInts.BossHealth, 0);
                return;
            }

            float frac = e.Health / (float)Mathf.Max(1, e.MaxHealth);
            room.SetQuestInt(RrQuestInts.BossHealth, Mathf.Clamp(Mathf.CeilToInt(1000f * frac), 1, 1000));
            if (Time.time < _nextShot || e.Target == RrCombat.NoPlayer || _model == null)
            {
                return;
            }

            _nextShot = Time.time + ShotEvery;
            Vector3 origin = _model.position + _model.forward * 1.2f + Vector3.up * 1.2f;
            room.Enemies.Volley(e, origin, RrQuestEnemies.TargetChest(e), 1, 0f, ShotSpeed, 0.45f, 100, 2.5f);
        }

        /// <summary>Every client, every frame of the run: shown in the fight, moves to his phase's position, sinks when beaten.</summary>
        public void ClientStep(RrLostSkullsRoom room, bool fighting, bool defeated)
        {
            if (_model == null || _spots == null || _spots.Length == 0)
            {
                return;
            }

            bool show = (fighting && _e != null) || defeated;
            if (_model.gameObject.activeSelf != show)
            {
                _model.gameObject.SetActive(show);
                _light.enabled = show;
            }

            if (!show)
            {
                return;
            }

            if (fighting && !_laughed)
            {
                _laughed = true;
                RrSound.Play("SR_Crowd_Laughter_01", transform.position, 1f, 0.6f, 80f);
            }

            int phase = Mathf.Clamp(room.QuestInt(RrQuestInts.BossPhase), 0, Hits);
            if (phase != _shownPhase)
            {
                if (_shownPhase >= 0 && phase > _shownPhase)
                {
                    RrFx.Spawn(RrLostSkullsCannon.BlastFx, _model.position + Vector3.up, Quaternion.identity, 2.5f);
                    _flash = 1f;
                }

                _shownPhase = phase;
                _from = transform.localPosition;
                _to = phase < Hits ? _spots[Mathf.Min(phase, _spots.Length - 1)] : _retreat;
                _moveStart = Time.time;
            }

            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _moveStart) / MoveSeconds));
            Vector3 p = Vector3.Lerp(_from, _to, k);
            if (defeated || (_e != null && !_e.Alive))
            {
                _sink = Mathf.MoveTowards(_sink, 1f, Time.deltaTime / 4f);
                p = _to + Vector3.down * 12f * _sink;
            }

            transform.localPosition = p;
            _model.localPosition = new Vector3(0f, 0.25f * Mathf.Sin(Time.time * 1.6f), 0f);
            Vector3 look = _face - transform.localPosition;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(look), Mathf.Clamp01(Time.deltaTime * 3f));
            }

            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2f);
            _model.localScale = Vector3.one * (1f + 0.15f * _flash) * (1f - 0.6f * _sink);
            if (_e != null && _e.Alive && Time.time >= _rehideAt)
            {
                Hide();
            }
        }
    }
}
