using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// Jumbotron himself (rr_quests.md 1.5 / 4.4, rr_latest_rgames_story.md 9.1; nothing of him is in the bundles, 9.2):
    /// the arena's hanging four-sided screen. The Stadium level (level48) hangs "RoomGeo/Jumbotron" at scene
    /// (89.46, 0.76, -96.31) with its BossSpawnPoint (1) at y 2.89 under the block; the block itself is an editor-merged
    /// mesh in Enemy_Jumbotron_Mat (pink; gtscene: 434 plates over 83.2-96.5 x 8.4-13.6 x -103.5..-89.4, hanging from the
    /// gym roof's trusses), dropped here (its small plates; the roof beams through it stay) and rebuilt at the same place: a dark 8.4 x 4.6 x 8.4 m box with a 7.2 x 3.8 m pink screen on each side showing his
    /// face (our shapes, iteration 11 after the footage: white "&gt; &lt;" eyes, a cyan LED ticker, a red LED band on top; a
    /// cyan-blue glow under the block), and an invincible turret hanging under it.
    ///
    /// Rules (final wiki): the four screens are the panels. One screen at a time is lit and takes damage; each one
    /// destroyed makes him yell "Reinforcements!" (tread and bat bots, then the elite ones) and the next screen lights;
    /// the fourth destroys the turret and every remaining enemy (the framework's boss death). The turret targets one
    /// player (the nearest, then the last attacker, as bots do) with streams of fast lasers aimed ahead of them - the
    /// only enemy that leads its shots. Numbers (GUESS unless marked): a screen takes about 14 rail-gun shots (a player
    /// tip; the rail gun hits for 70) - 1,300 x (0.55 + 0.15 x party) health per screen, hit as a weak spot (x1.5, so a
    /// full party needs 14 rail-gun shots per screen); a stream of 6 lasers every 3.2 s at 30 m/s, 0.12 s apart.
    ///
    /// The body is the roster's boss type, Rec Room's heavy assassin bot "(QuestAIController)SciFiKnightAssassin",
    /// kept passive and invisible under the screens: it carries the health, the kill credits and the framework's boss
    /// rules; the four screens' hit volumes point at it (<see cref="RrHitVolume.Enemy"/>).
    /// </summary>
    public sealed class RrJumbotronBoss : MonoBehaviour
    {
        public const int PanelHealth = 1300;
        public const float StreamEvery = 3.2f;
        public const int StreamShots = 6;
        public const float ShotGap = 0.12f;
        public const float LaserSpeed = 30f;

        /// <summary>The screens' hot pink (#f95dbc in the footage).</summary>
        private static readonly Color Pink = new Color(0.98f, 0.36f, 0.74f);

        private readonly Transform[] _faces = new Transform[4];
        private readonly Renderer[] _screens = new Renderer[4];
        private readonly List<GameObject>[] _features = { new List<GameObject>(), new List<GameObject>(), new List<GameObject>(), new List<GameObject>() };
        private readonly RrHitVolume[] _volumes = new RrHitVolume[4];
        private Material _lit, _pulse, _dead;
        private Transform _turret, _barrel;
        private RrEnemy _e;
        private int _shownPhase = -1;
        private float _nextStream;
        private int _shotsLeft;
        private float _nextShot;
        private Vector3 _lastTarget;
        private float _lastTargetAt = -1f;
        private Vector3 _targetVelocity;
        private float _rehideAt;
        private bool _laughed;

        /// <summary>The boss enemy while he lives (null before the fight).</summary>
        public RrEnemy Enemy
        {
            get { return _e; }
        }

        /// <summary>The screen block, centred at local (the block's centre), under parent.</summary>
        public static RrJumbotronBoss Build(Transform parent, Vector3 local)
        {
            var root = RrBuild.Group(parent, "Jumbotron", local).transform;
            var b = root.gameObject.AddComponent<RrJumbotronBoss>();
            b.BuildParts();
            return b;
        }

        private void BuildParts()
        {
            var t = transform;
            var metal = RrLook.Solid(new Color(0.12f, 0.12f, 0.16f));
            var trim = RrLook.Solid(new Color(0.32f, 0.33f, 0.38f));
            _lit = RrLook.Emissive(Pink, 1.6f);
            // Our own instance: the lit screen breathes by its emission colour (RrLook caches shared materials).
            _pulse = new Material(RrLook.Emissive(Pink, 2.6f)) { name = "Jumbotron Screen (pulse)" };
            _dead = RrLook.Solid(new Color(0.06f, 0.06f, 0.07f));
            var white = RrLook.Emissive(Color.white, 1.8f);
            var black = RrLook.Unlit(new Color(0.03f, 0.02f, 0.04f));
            var ticker = RrLook.Emissive(new Color(0.2f, 0.85f, 1f), 1.6f);
            var band = RrLook.Emissive(new Color(1f, 0.08f, 0.1f), 1.6f);
            RrBuild.Box(t, "Body", Vector3.zero, new Vector3(8.4f, 4.6f, 8.4f), metal, false);
            RrBuild.Box(t, "Cap", new Vector3(0f, 2.55f, 0f), new Vector3(8.8f, 0.5f, 8.8f), trim, false);
            RrBuild.Box(t, "Rim", new Vector3(0f, -2.45f, 0f), new Vector3(8.8f, 0.3f, 8.8f), trim, false);
            for (int i = 0; i < 4; i++)
            {
                // Hangers up to the arena's roof truss.
                float sx = (i & 1) == 0 ? -3.6f : 3.6f, sz = (i & 2) == 0 ? -3.6f : 3.6f;
                RrBuild.Box(t, "Hanger " + i, new Vector3(sx, 4.3f, sz), new Vector3(0.15f, 3f, 0.15f), trim, false);
            }

            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, 90f * i, 0f);
                var face = RrBuild.Group(t, "Screen " + (i + 1), rot * new Vector3(0f, 0f, 4.22f), rot).transform;
                _faces[i] = face;
                var screen = RrBuild.Box(face, "Screen", Vector3.zero, new Vector3(7.2f, 3.8f, 0.06f), _lit, false);
                _screens[i] = screen.GetComponent<Renderer>();
                // His face as the footage shows it (rr_jumbotron_look 2.11; our shapes, no Jumbotron art is in the install):
                // white "> <" eyes on the hot-pink screen, a cyan LED ticker along its foot, a red LED band above it.
                var f = _features[i];
                f.Add(RrBuild.Box(face, "Eye L top", new Vector3(-1.6f, 0.95f, 0.05f), Quaternion.Euler(0f, 0f, -26.6f), new Vector3(1.3f, 0.3f, 0.04f), white, false));
                f.Add(RrBuild.Box(face, "Eye L low", new Vector3(-1.6f, 0.45f, 0.05f), Quaternion.Euler(0f, 0f, 26.6f), new Vector3(1.3f, 0.3f, 0.04f), white, false));
                f.Add(RrBuild.Box(face, "Eye R top", new Vector3(1.6f, 0.95f, 0.05f), Quaternion.Euler(0f, 0f, 26.6f), new Vector3(1.3f, 0.3f, 0.04f), white, false));
                f.Add(RrBuild.Box(face, "Eye R low", new Vector3(1.6f, 0.45f, 0.05f), Quaternion.Euler(0f, 0f, -26.6f), new Vector3(1.3f, 0.3f, 0.04f), white, false));
                f.Add(RrBuild.Box(face, "Ticker", new Vector3(0f, -1.45f, 0.05f), new Vector3(6.6f, 0.42f, 0.04f), ticker, false));
                for (int k = 0; k < 11; k++)
                {
                    // The ticker's letters read as dark gaps between LED blocks.
                    f.Add(RrBuild.Box(face, "Ticker Gap", new Vector3(-3.0f + 0.6f * k, -1.45f, 0.08f), new Vector3(0.08f, 0.42f, 0.03f), black, false));
                }

                RrBuild.Box(face, "LED Band", new Vector3(0f, 2.1f, 0.03f), new Vector3(7.6f, 0.26f, 0.05f), band, false);
            }

            // The invincible turret under the block (it turns to its target).
            _turret = RrBuild.Group(t, "Turret", new Vector3(0f, -3.2f, 0f)).transform;
            RrBuild.Cylinder(_turret, "Mount", new Vector3(0f, 0.45f, 0f), 0.35f, 0.9f, trim, false);
            RrBuild.Sphere(_turret, "Head", Vector3.zero, 0.75f, metal, false);
            _barrel = RrBuild.Group(_turret, "Barrel Pivot", Vector3.zero).transform;
            RrBuild.Box(_barrel, "Barrel", new Vector3(0f, 0f, 0.9f), new Vector3(0.28f, 0.28f, 1.3f), trim, false);
            RrBuild.Box(_barrel, "Muzzle", new Vector3(0f, 0f, 1.58f), new Vector3(0.2f, 0.2f, 0.1f), RrLook.Emissive(Pink, 2f), false);
            // "A big cyan-blue glow under the Jumbotron" (#32b5fc, rr_jumbotron_look 2.11).
            RrBuild.PointLight(t, "Screen Glow", new Vector3(0f, -3.2f, 0f), new Color(0.2f, 0.71f, 0.99f), 18f, 1.4f);
        }

        /// <summary>Every client: the boss enemy is ready - hide its body, point four screen volumes at it.</summary>
        public void Setup(RrEnemy e, int party)
        {
            _e = e;
            e.Passive = true;
            e.MoveSpeed = 0f;
            e.AggroRange = 80f;
            e.MaxHealth = Mathf.RoundToInt(4f * PanelHealth * (0.55f + 0.15f * Mathf.Clamp(party, 1, 4)));
            e.Health = e.MaxHealth;
            Hide();
            for (int i = 0; i < 4; i++)
            {
                if (_volumes[i] != null)
                {
                    Destroy(_volumes[i].gameObject);
                }

                var v = RrHitVolume.Create(_faces[i], "Hit Screen", RrBodyPart.WeakSpot, false, 1.9f, 0f);
                v.transform.localPosition = new Vector3(0f, 0f, -1.2f);
                v.Enemy = e;
                _volumes[i] = v;
            }

            _shownPhase = -1;
            _nextStream = Time.time + 4f;
            RrLog.Info("Jumbotron", "Jumbotron rises: " + e.MaxHealth + " HP over 4 screens (party " + party + "), turret " + StreamShots + " lasers every " + StreamEvery + " s");
        }

        /// <summary>The assassin body is only the health: no renderer, none of its own hit volumes.</summary>
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

        private void OnDestroy()
        {
            if (_pulse != null)
            {
                Destroy(_pulse);
            }
        }

        /// <summary>A new run: every screen lit again, no body.</summary>
        public void ResetBoss()
        {
            _e = null;
            _shownPhase = -1;
            _laughed = false;
            for (int i = 0; i < 4; i++)
            {
                if (_volumes[i] != null)
                {
                    Destroy(_volumes[i].gameObject);
                    _volumes[i] = null;
                }

                ShowPanel(i, true, false);
            }

            if (_turret != null)
            {
                _turret.gameObject.SetActive(true);
            }
        }

        private void ShowPanel(int i, bool alive, bool active)
        {
            if (_screens[i] != null)
            {
                _screens[i].sharedMaterial = !alive ? _dead : active ? _pulse : _lit;
            }

            foreach (var f in _features[i])
            {
                if (f != null)
                {
                    f.SetActive(alive);
                }
            }

            if (_volumes[i] != null)
            {
                _volumes[i].gameObject.SetActive(alive && active);
            }
        }

        /// <summary>Every client, every frame of the run: screens by the boss phase, the turret turns to its target.</summary>
        public void ClientStep(RrJumbotronRoom room, bool fighting, bool defeated)
        {
            int phase = fighting ? Mathf.Clamp(room.QuestInt(RrQuestInts.BossPhase), 0, 4) : 0;
            bool alive = !defeated && _e != null && _e.Alive;
            if (defeated || (fighting && _e != null && !_e.Alive))
            {
                phase = 4;
            }

            if (phase != _shownPhase)
            {
                if (_shownPhase >= 0 && phase > _shownPhase)
                {
                    for (int i = _shownPhase; i < phase && i < 4; i++)
                    {
                        Vector3 at = _faces[i].position;
                        RrFx.Spawn("[Arena_Grenade_Explosion_VFX] JumboTron Variant", at, Quaternion.identity, 2f);
                        RrSound.Play("SR_Buzzer_Long01", at, 1f, 0.8f);
                    }
                }

                _shownPhase = phase;
                for (int i = 0; i < 4; i++)
                {
                    ShowPanel(i, i >= phase, fighting && i == phase && alive);
                }

                if (_turret != null)
                {
                    _turret.gameObject.SetActive(phase < 4);
                }
            }

            if (fighting && alive && !_laughed)
            {
                _laughed = true;
                RrSound.Play("SR_Crowd_Laughter_01", transform.position, 1f, 0.7f);
            }

            if (alive && Time.time >= _rehideAt)
            {
                Hide();
            }

            // The lit screen breathes; the turret looks at its target (or sweeps).
            if (fighting && phase < 4 && _pulse != null)
            {
                float k = 1.8f + 0.9f * Mathf.Sin(Time.time * 5f);
                var c = Pink * k;
                if (_pulse.HasProperty("_EmissionColor"))
                {
                    _pulse.SetColor("_EmissionColor", c);
                }
            }

            if (_barrel != null && _turret != null && _turret.gameObject.activeSelf)
            {
                Vector3 look = alive && _e.Target != RrCombat.NoPlayer ? RrQuestEnemies.TargetChest(_e) - _turret.position : Quaternion.Euler(0f, Time.time * 20f, 0f) * new Vector3(0f, -0.5f, 1f);
                if (look.sqrMagnitude > 0.01f)
                {
                    _barrel.rotation = Quaternion.Slerp(_barrel.rotation, Quaternion.LookRotation(look), Mathf.Clamp01(Time.deltaTime * 6f));
                }
            }
        }

        /// <summary>Authority, every frame of the finale's combat: boss bar, screens, reinforcements, turret streams.</summary>
        public void AuthorityStep(RrJumbotronRoom room)
        {
            var e = _e;
            if (e == null)
            {
                return;
            }

            if (!e.Alive)
            {
                room.SetQuestInt(RrQuestInts.BossHealth, 0);
                room.SetQuestInt(RrQuestInts.BossPhase, 4);
                return;
            }

            float frac = e.Health / (float)Mathf.Max(1, e.MaxHealth);
            room.SetQuestInt(RrQuestInts.BossHealth, Mathf.Clamp(Mathf.CeilToInt(1000f * frac), 1, 1000));
            int phase = Mathf.Clamp(Mathf.FloorToInt((1f - frac) * 4f + 1e-4f), 0, 3);
            if (phase > room.QuestInt(RrQuestInts.BossPhase))
            {
                room.SetQuestInt(RrQuestInts.BossPhase, phase);
                room.Broadcast("Reinforcements!");
                Reinforcements(room, phase);
            }

            if (e.Target == RrCombat.NoPlayer || _turret == null)
            {
                return;
            }

            Vector3 target = RrQuestEnemies.TargetChest(e);
            if (_lastTargetAt > 0f && Time.time - _lastTargetAt > 0.02f)
            {
                var v = (target - _lastTarget) / (Time.time - _lastTargetAt);
                _targetVelocity = Vector3.Lerp(_targetVelocity, Vector3.ClampMagnitude(v, 8f), 0.25f);
            }

            _lastTarget = target;
            _lastTargetAt = Time.time;
            if (_shotsLeft <= 0 && Time.time >= _nextStream)
            {
                _shotsLeft = StreamShots;
                _nextShot = Time.time;
                _nextStream = Time.time + StreamEvery;
            }

            if (_shotsLeft > 0 && Time.time >= _nextShot)
            {
                _shotsLeft--;
                _nextShot = Time.time + ShotGap;
                Vector3 origin = _barrel != null ? _barrel.TransformPoint(new Vector3(0f, 0f, 1.7f)) : _turret.position;
                // "Shoots in front of you": the only enemy that leads (the target's velocity x the flight time).
                float t = Vector3.Distance(origin, target) / LaserSpeed;
                room.Enemies.Volley(e, origin, target + _targetVelocity * t, 1, 0f, LaserSpeed, 0.16f, 6, 0f);
            }
        }

        /// <summary>After each screen: 2 tread + 2 bat bots, then elites (research 4.4: "normal, then elites").</summary>
        private static void Reinforcements(RrJumbotronRoom room, int phase)
        {
            var act = room.CurrentAct;
            if (act == null || act.Root == null)
            {
                return;
            }

            int[] types = phase <= 1
                ? new[] { RrJumbotronRoom.Tread, RrJumbotronRoom.Bat, RrJumbotronRoom.Tread, RrJumbotronRoom.Bat }
                : phase == 2
                    ? new[] { RrJumbotronRoom.Tread, RrJumbotronRoom.EliteBat, RrJumbotronRoom.EliteTread, RrJumbotronRoom.Bat }
                    : new[] { RrJumbotronRoom.EliteTread, RrJumbotronRoom.EliteBat, RrJumbotronRoom.EliteTread, RrJumbotronRoom.EliteBat, RrJumbotronRoom.Mouse, RrJumbotronRoom.Mouse };
            for (int i = 0; i < types.Length; i++)
            {
                bool flyer = types[i] == RrJumbotronRoom.Bat || types[i] == RrJumbotronRoom.EliteBat;
                bool mouse = types[i] == RrJumbotronRoom.Mouse;
                List<Vector3> points;
                if (!act.Groups.TryGetValue(flyer ? "reinf_fly" : mouse ? "reinf_mice" : "reinf_ground", out points) || points.Count == 0)
                {
                    continue;
                }

                Vector3 p = points[(i + phase * 3) % points.Count] + (flyer ? Vector3.up * 0.5f : Vector3.zero);
                room.Enemies.Spawn(types[i], act.Root.TransformPoint(p), 180f);
            }

            RrLog.Info("Jumbotron", "screen " + phase + " destroyed: Reinforcements! (" + types.Length + " bots)");
        }
    }
}
