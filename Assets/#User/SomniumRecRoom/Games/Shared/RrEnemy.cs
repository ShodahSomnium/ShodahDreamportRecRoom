using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>How an enemy fights (rr_quests.md 3.4 archetypes).</summary>
    public enum RrEnemyKind : byte
    {
        Melee,
        Ranged,
        Flyer,
        Charger,
        Exploder,
        Heavy,
        Thrower,
        Lunger
    }

    /// <summary>What an enemy is doing; also the anim state of the actor block (networking 5.8).</summary>
    public enum RrEnemyState : byte
    {
        Spawning,
        Idle,
        Move,
        Windup,
        Attack,
        Recover,
        Stunned,
        Dying,
        Dead
    }

    /// <summary>
    /// A quest / laser-tag enemy. It is spawned from Rec Room's Maker spawner prefab "(QuestAIController)Name"
    /// (rr_quests.md 2.2.1): the spawner's QuestAIController.type picks the archetype, QuestAiConfig_EnemyCommon gives
    /// maxHealth and movementSpeed, QuestAiConfig_Dash / _Projectile / _Charge the dash, projectile and charge numbers
    /// (all measured: sword goblin 100 HP at 3 m/s). The body is the spawner's posed preview mesh with its real
    /// material, or, for the three enemies that exist as skinned rigs (goblin 0/5, swashbuckler 21, laser bot 9/19,
    /// 2.2.2), Rec Room's player-transformation rig driven by its own idle / walk clips through a PlayableGraph,
    /// scaled to the spawner's height and recoloured with the spawner's material (elites). Everything else
    /// (windup, lunge, hurt squash, stun, tip-over) is procedural on our own pivot, and on the rig's arm bone.
    ///
    /// AI: chase / keep range / orbit by archetype (the timing and damage table of 3.4, GUESS), steering with
    /// obstacle and ledge probes (no NavMesh), separation between enemies, ground snap. Attacks follow the victim-
    /// decides rule: each client tests only its own local player (melee sphere, projectiles via RrProjectile with
    /// OwnerEnemy, blasts) and a held blade can parry (<see cref="RrMelee.TryBlock"/>).
    ///
    /// Authority: in a quest (<see cref="ExternalSync"/>, set by RrQuestRoom while the player is in one) only the
    /// instance authority simulates; it publishes <see cref="CaptureState"/> in the instance's actor blocks and the
    /// watchers follow with <see cref="ApplyState"/> and replay attacks from the EnemyAttack event (RrQuestEnemies,
    /// rr_quests.md 3.6). Without it (offline, or a game with no actor sync) every client simulates its own copy of each
    /// enemy (same ids in spawn order) and player hits are broadcast so every copy loses the same health.
    /// </summary>
    public sealed class RrEnemy : MonoBehaviour
    {
        /// <summary>Set by the Net layer once enemies travel in actor blocks: then only the authority simulates.</summary>
        public static bool ExternalSync;

        /// <summary>Use the skinned rigs where Rec Room has them (else the posed spawner mesh everywhere).</summary>
        public static bool UseRigs = true;

        public static readonly List<RrEnemy> All = new List<RrEnemy>();

        public static event Action<RrEnemy> OnSpawned;

        /// <summary>An enemy died: the enemy and the killer's PlayerId (-1: none). Raised where it is simulated.</summary>
        public static event Action<RrEnemy, int> OnKilled;

        /// <summary>Damage applied: enemy, damage, attacker.</summary>
        public static event Action<RrEnemy, int, int> OnDamaged;

        // ---- identity ----
        public int Id;
        public int Type = -1;
        public string Prefab;
        public RrEnemyKind Kind;
        public bool Elite;

        // ---- measured (spawner configs) ----
        public int MaxHealth = 100;
        public int Health = 100;
        public float MoveSpeed = 3f;
        public float ProjectileSpeed = 10f;
        public float DashSpeed = 4f;
        public float DashMin = 2f;
        public float DashMax = 4f;
        public float ChargeDistance = 8f;
        public float ChargeSpeed = 12f;

        // ---- tuning (GUESS, rr_quests.md 3.4) ----
        public float AttackRange = 1.1f;
        public float KeepMin = 6f;
        public float KeepMax = 12f;
        public float Windup = 0.6f;
        public float ActiveTime = 0.2f;
        public float Cooldown = 1.4f;
        public int Damage = 20;
        public float Splash;
        public int Burst = 1;
        public int Volley = 1;
        public float Hover = 3f;
        public float AggroRange = 30f;
        public int Points = 100;
        public Color ShotColor = new Color(1f, 0.55f, 0.1f);
        public string MuzzleFx;
        public string ShotClip;
        public string CoinFx;

        /// <summary>No aggro: stands and idles (tests, scripted scenes).</summary>
        public bool Passive;

        public float Height = 1.3f;
        public float Radius = 0.35f;

        public RrEnemyState State { get; private set; } = RrEnemyState.Spawning;

        /// <summary>The targeted player (Fusion PlayerId, the local id for us), or -1.</summary>
        public int Target { get; private set; } = RrCombat.NoPlayer;

        public bool ModelReady { get; private set; }
        public bool HasRig { get; private set; }

        private static int _nextId = 1;
        private static bool _loggedFirst;
        private static bool _loggedRig;
        private static bool _loggedNoTarget;

        private Transform _pivot;
        private RrInstance _model;
        private RrInstance _rig;
        private Renderer[] _renderers = new Renderer[0];
        private readonly List<RrHitVolume> _volumes = new List<RrHitVolume>();
        private MaterialPropertyBlock _block;
        private RrShotSpec _shot;
        private float _stateTime;
        private float _nextAttack;
        private float _nextRetarget;
        private float _nextDash;
        private float _dashLeft;
        private Vector3 _dashDir;
        private float _stunUntil;
        private float _flash;
        private float _squash;
        private float _speedNow;
        private float _vy;
        private float _orbitSign = 1f;
        private float _phase;
        private float _turnRate;
        private int _shotsLeft;
        private float _nextBurstShot;
        private bool _attackResolved;
        private Vector3 _attackDir;
        private float _charged;
        private bool _hasTarget;
        private Vector3 _targetFeet;
        private Vector3 _targetChest;
        private Transform _targetRoot;
        private Transform _targetHead;
        private int _killer = RrCombat.NoPlayer;
        private bool _finished;
        private LineRenderer _aimLine;

        // skinned rig
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private Transform _armBone;
        private Quaternion _armRest;
        private float _armAngle;

        // remote follow (ExternalSync watchers)
        private Vector3 _netPos;
        private float _netYaw;
        private bool _hasNet;

        public bool Alive
        {
            get { return Health > 0 && State != RrEnemyState.Dying && State != RrEnemyState.Dead; }
        }

        /// <summary>This client runs the AI (offline, the instance authority, or everyone while there is no actor sync).</summary>
        public bool Simulating
        {
            get { return !ExternalSync || !RrNet.Online || RrNet.IsInstanceAuthority; }
        }

        public Vector3 Center
        {
            get { return transform.position + Vector3.up * (Height * 0.5f); }
        }

        public static RrEnemy Find(int id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i].Id == id)
                {
                    return All[i];
                }
            }

            return null;
        }

        /// <summary>Restarts enemy ids (a new act / wave set); every client must spawn in the same order afterwards.</summary>
        public static void ResetIds()
        {
            _nextId = 1;
        }

        // ---- spawning ----

        /// <summary>
        /// Spawns an enemy from a "(QuestAIController)..." spawner prefab at a world position, facing yaw. Returns at
        /// once (it is already an actor with default numbers); ready: once the body is loaded (null on failure).
        /// id 0 takes the next id.
        /// </summary>
        public static RrEnemy Spawn(string spawnerPrefab, Transform parent, Vector3 position, float yaw, Action<RrEnemy> ready = null, int id = 0)
        {
            RrCombat.Ensure();
            var go = new GameObject("Rr Enemy " + spawnerPrefab);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var e = go.AddComponent<RrEnemy>();
            e.Id = id > 0 ? id : _nextId++;
            if (id >= _nextId)
            {
                _nextId = id + 1;
            }

            e.Prefab = spawnerPrefab;
            e._pivot = new GameObject("Pivot").transform;
            e._pivot.SetParent(go.transform, false);
            e.ApplyTuning(TypeFromName(spawnerPrefab));
            RrImpacts.Fx("VFX_Generic_SmokePoof01", position + Vector3.up * 0.6f, Quaternion.identity);
            RrSpawn.Spawn(spawnerPrefab, e._pivot, Vector3.zero, Quaternion.identity, inst => e.ModelLoaded(inst, ready), RrSpawnOptions.Visual());
            return e;
        }

        private static int TypeFromName(string prefab)
        {
            string n = prefab ?? "";
            if (n.Contains("GoblinBasicGroundMelee")) return 0;
            if (n.Contains("GoblinEliteGroundMelee")) return 5;
            if (n.Contains("GoblinBasicFlyingRanged")) return 1;
            if (n.Contains("GoblinEliteFlyingRanged")) return 7;
            if (n.Contains("SciFiBasicGroundRanged")) return 9;
            if (n.Contains("SciFiEliteGroundRanged")) return 19;
            return -1;
        }

        private void ModelLoaded(RrInstance inst, Action<RrEnemy> ready)
        {
            if (this == null)
            {
                if (inst != null)
                {
                    Destroy(inst.gameObject);
                }

                return;
            }

            if (inst == null)
            {
                RrLog.WarnOnce("Enemy", "spawn:" + Prefab, "could not spawn " + Prefab + ": using a stand-in body");
                BuildStandIn();
                FinishModel();
                if (ready != null)
                {
                    ready(this);
                }

                return;
            }

            _model = inst;
            ReadConfigs(inst);
            var preview = inst.Find("Preview");
            _renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (preview != null)
            {
                foreach (var r in _renderers)
                {
                    if (!r.transform.IsChildOf(preview))
                    {
                        r.enabled = false;
                    }
                }
            }

            _renderers = Array.FindAll(_renderers, r => r.enabled && r.gameObject.activeInHierarchy);
            MeasureBody();
            FinishModel();
            if (UseRigs)
            {
                string rig = RigOf(Type);
                if (rig != null)
                {
                    LoadRig(rig);
                }
            }

            if (ready != null)
            {
                ready(this);
            }
        }

        private void ReadConfigs(RrInstance inst)
        {
            var ctrl = inst.Config("QuestAIController");
            if (ctrl != null && ctrl.Has("type"))
            {
                ApplyTuning(ctrl.Int("type", Type));
            }

            var common = inst.Config("QuestAiConfig_EnemyCommon");
            if (common != null)
            {
                MaxHealth = Mathf.Max(1, common.Int("maxHealth", MaxHealth));
                MoveSpeed = common.Float("movementSpeed", MoveSpeed);
            }

            var dash = inst.Config("QuestAiConfig_Dash");
            if (dash != null)
            {
                DashSpeed = dash.Float("dashSpeed", DashSpeed);
                DashMin = dash.Float("minDashDistance", DashMin);
                DashMax = Mathf.Max(DashMin, dash.Float("maxDashDistance", DashMax));
            }

            var proj = inst.Config("QuestAiConfig_Projectile");
            if (proj != null)
            {
                ProjectileSpeed = proj.Float("projectileSpeed", ProjectileSpeed);
            }

            var charge = inst.Config("QuestAiConfig_Charge");
            if (charge != null)
            {
                ChargeDistance = charge.Float("chargeDistance", ChargeDistance);
                ChargeSpeed = charge.Float("chargeSpeed", ChargeSpeed);
            }

            Health = MaxHealth;
            Points = MaxHealth;
        }

        /// <summary>Archetype and GUESS tuning per QuestAIController.type (rr_quests.md 3.4).</summary>
        private void ApplyTuning(int type)
        {
            Type = type;
            Elite = type == 5 || type == 7 || type == 18 || type == 19;
            Kind = RrEnemyKind.Melee;
            AttackRange = 1.1f;
            Windup = 0.6f;
            ActiveTime = 0.2f;
            Cooldown = 1.4f;
            Damage = 20;
            Splash = 0f;
            Burst = 1;
            Volley = 1;
            switch (type)
            {
                case 5:
                case 12:
                    Damage = 25;
                    AttackRange = type == 12 ? 1.8f : 1.1f;
                    break;
                case 4:
                case 9:
                case 19:
                case 26:
                case 28:
                case 27:
                    Kind = RrEnemyKind.Ranged;
                    Windup = 1f;
                    Cooldown = 2.5f;
                    Damage = type == 4 ? 20 : type == 26 || type == 28 ? 30 : 10;
                    Splash = type == 4 ? 1.5f : 0f;
                    Burst = type == 9 || type == 19 ? 3 : 1;
                    KeepMin = 6f;
                    KeepMax = 12f;
                    if (type == 27)
                    {
                        Damage = 15;
                        Windup = 1.2f;
                        Cooldown = 3f;
                    }

                    break;
                case 1:
                case 7:
                case 11:
                case 13:
                case 18:
                case 30:
                    Kind = RrEnemyKind.Flyer;
                    Windup = 0.8f;
                    Cooldown = 2.5f;
                    Damage = type == 1 || type == 7 || type == 13 ? 15 : 10;
                    Burst = type == 11 || type == 18 ? 3 : 1;
                    Volley = type == 7 || type == 13 ? 5 : 1;
                    Hover = 3f;
                    KeepMin = 7f;
                    KeepMax = 10f;
                    break;
                case 2:
                case 8:
                    Kind = RrEnemyKind.Charger;
                    Windup = 1.2f;
                    Cooldown = 3f;
                    Damage = 40;
                    AttackRange = 8f;
                    break;
                case 10:
                    Kind = RrEnemyKind.Exploder;
                    Windup = 0.4f;
                    Damage = 35;
                    Splash = 2.5f;
                    AttackRange = 1.2f;
                    break;
                case 14:
                    Kind = RrEnemyKind.Heavy;
                    Windup = 2.4f;
                    Cooldown = 4f;
                    Damage = 50;
                    KeepMin = 12f;
                    KeepMax = 20f;
                    break;
                case 22:
                case 23:
                case 29:
                    Kind = RrEnemyKind.Thrower;
                    Windup = 0.9f;
                    Cooldown = 2.2f;
                    Damage = type == 22 ? 25 : 15;
                    Splash = type == 22 ? 2f : 0f;
                    KeepMin = 5f;
                    KeepMax = 9f;
                    break;
                case 31:
                case 35:
                    Kind = RrEnemyKind.Lunger;
                    Windup = 0.5f;
                    Cooldown = 2f;
                    Damage = type == 35 ? 25 : 10;
                    AttackRange = 5f;
                    break;
            }

            bool scifi = type == 9 || type == 10 || type == 11 || type == 14 || type == 18 || type == 19;
            bool goblin = type == 0 || type == 1 || type == 2 || type == 4 || type == 5 || type == 7 || type == 8 || type == 12 || type == 13;
            bool dracula = type >= 28 && type <= 35;
            CoinFx = goblin ? "[VFX_GoldCoins]" : scifi ? "[VFX_CreditChips]" : dracula ? "[Quest_Dracula1_Silver_VFX]" : null;
            MuzzleFx = scifi ? "[VFX_Laser_MuzzelFlash]" : goblin ? "[VFX_Wand_MuzzelFlash]" : type == 26 || type == 28 ? "[Quest_Pirate1_FlintlockFire_VFX]" : null;
            ShotClip = scifi ? "LaserPistol_01" : null;
            ShotColor = scifi ? (Elite ? new Color(0.7f, 0.25f, 1f) : new Color(1f, 0.15f, 0.1f))
                : goblin ? new Color(1f, 0.55f, 0.1f)
                : type == 30 ? new Color(0.4f, 0.9f, 0.2f)
                : new Color(0.25f, 0.25f, 0.25f);
        }

        /// <summary>The skinned player-transformation rig for this type, when Rec Room has one (rr_quests.md 2.2.2).</summary>
        private static string RigOf(int type)
        {
            switch (type)
            {
                case 0:
                case 5:
                    return "Goblin_Ground_PlayerTransformation";
                case 21:
                    return "Pirate_Ground_PlayerTransformation";
                case 9:
                case 19:
                    return "SciFi_Enemy_Ground_PlayerTransformation";
                default:
                    return null;
            }
        }

        private static void RigClips(int type, out string idle, out string walk, out string[] arm)
        {
            switch (type)
            {
                case 21:
                    idle = "Pirate_MeleeEnemy_Idle";
                    walk = "Pirate_MeleeEnemy_Walk";
                    arm = new[] { "Skeleton_RArmUpper_Jnt" };
                    return;
                case 9:
                case 19:
                    idle = "SciFi_Enemy_Ground_Stand";
                    walk = "SciFi_Enemy_Ground_Walk";
                    arm = new[] { "Cannon", "SciFi_Ground_Cannon" };
                    return;
                default:
                    idle = "Goblin_Idle";
                    walk = "Goblin_WalkCycle";
                    arm = new[] { "Goblin_Ground_LeftArm", "LeftArm" };
                    return;
            }
        }

        /// <summary>Height and radius from the visible renderers (the spawner AABBs of 2.2.1), then the hit volumes.</summary>
        private void MeasureBody()
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in _renderers)
            {
                if (r == null)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (any)
            {
                float bottom = b.min.y - transform.position.y;
                Height = Mathf.Clamp(b.max.y - b.min.y, 0.3f, 4f);
                Radius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.7f, 0.2f, 0.9f);
                if (Kind != RrEnemyKind.Flyer && Mathf.Abs(bottom) > 0.02f)
                {
                    _pivot.localPosition -= new Vector3(0f, bottom, 0f);
                }
            }

            BuildVolumes();
        }

        private void BuildVolumes()
        {
            foreach (var v in _volumes)
            {
                if (v != null)
                {
                    Destroy(v.gameObject);
                }
            }

            _volumes.Clear();
            var root = transform;
            if (Kind == RrEnemyKind.Flyer)
            {
                var body = RrHitVolume.Create(root, "Hit Body", RrBodyPart.Body, false, Mathf.Max(Radius, Height * 0.45f), 0f);
                body.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
                _volumes.Add(body);
            }
            else
            {
                var body = RrHitVolume.Create(root, "Hit Body", RrBodyPart.Body, true, Radius, Height * 0.8f);
                body.transform.localPosition = new Vector3(0f, Height * 0.42f, 0f);
                _volumes.Add(body);
                float hr = Mathf.Clamp(Height * 0.15f, 0.12f, 0.35f);
                var head = RrHitVolume.Create(root, "Hit Head", RrBodyPart.Head, false, hr, 0f);
                head.transform.localPosition = new Vector3(0f, Height - hr, Radius * 0.2f);
                _volumes.Add(head);
            }

            if (Kind == RrEnemyKind.Charger || Kind == RrEnemyKind.Heavy)
            {
                var weak = RrHitVolume.Create(root, "Hit Weak Spot", RrBodyPart.WeakSpot, false, 0.25f, 0f);
                weak.transform.localPosition = new Vector3(0f, Height * 0.45f, -Radius - 0.05f);
                _volumes.Add(weak);
            }

            foreach (var v in _volumes)
            {
                v.Enemy = this;
            }
        }

        /// <summary>No Rec Room body (bundle missing): a plain capsule in the enemy's colour, so the game still works.</summary>
        private void BuildStandIn()
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(_pivot, false);
            cap.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            cap.transform.localScale = new Vector3(0.6f, 0.65f, 0.6f);
            cap.GetComponent<Renderer>().sharedMaterial = RrLook.Solid(new Color(0.3f, 0.6f, 0.2f));
            _renderers = new[] { cap.GetComponent<Renderer>() };
            Height = 1.3f;
            Radius = 0.3f;
            BuildVolumes();
        }

        private void FinishModel()
        {
            ModelReady = true;
            State = RrEnemyState.Spawning;
            _stateTime = 0f;
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                RrLog.Info("Enemy", "first enemy " + Prefab + ": type " + Type + " " + Kind + (Elite ? " (elite)" : "") + ", " + MaxHealth + " HP, " + MoveSpeed + " m/s, height "
                    + Height.ToString("0.00") + " m, radius " + Radius.ToString("0.00") + ", projectile " + ProjectileSpeed + " m/s, simulated " + (Simulating ? "here" : "by the authority")
                    + (ExternalSync ? "" : " (no actor sync: every client simulates)"));
            }

            var h = OnSpawned;
            if (h != null)
            {
                h(this);
            }
        }

        // ---- skinned rig ----

        private void LoadRig(string rigName)
        {
            RrSpawn.Spawn(rigName, _pivot, Vector3.zero, Quaternion.identity, rig =>
            {
                if (rig == null || this == null || !Alive && State == RrEnemyState.Dead)
                {
                    if (rig != null)
                    {
                        Destroy(rig.gameObject);
                    }

                    return;
                }

                _rig = rig;
                rig.gameObject.SetActive(false);
                string idle, walk;
                string[] arm;
                RigClips(Type, out idle, out walk, out arm);
                AnimationClip idleClip = null, walkClip = null;
                int left = 2;
                Action step = () =>
                {
                    left--;
                    if (left == 0)
                    {
                        SetupRig(idleClip, walkClip, arm);
                    }
                };
                if (RrAssets.Instance == null)
                {
                    SetupRig(null, null, arm);
                    return;
                }

                RrAssets.Instance.Load<AnimationClip>(idle, 74, c =>
                {
                    idleClip = c;
                    step();
                });
                RrAssets.Instance.Load<AnimationClip>(walk, 74, c =>
                {
                    walkClip = c;
                    step();
                });
            }, RrSpawnOptions.Visual());
        }

        private void SetupRig(AnimationClip idle, AnimationClip walk, string[] arm)
        {
            if (this == null || _rig == null)
            {
                return;
            }

            var rigGo = _rig.gameObject;
            var smrs = rigGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs.Length == 0 || idle == null)
            {
                RrLog.WarnOnce("Enemy", "rig:" + rigGo.name, "rig " + rigGo.name + " unusable (" + smrs.Length + " skinned renderers, idle clip " + (idle != null) + "): keeping the spawner mesh");
                Destroy(rigGo);
                _rig = null;
                return;
            }

            // The spawner's material carries the elite colours; the face texture of the bot keeps its own.
            Material body = null;
            foreach (var r in _renderers)
            {
                if (r != null && r.sharedMaterial != null)
                {
                    body = r.sharedMaterial;
                    break;
                }
            }

            foreach (var smr in smrs)
            {
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (body != null && (mats[i] == null || mats[i].name.IndexOf("Face", StringComparison.OrdinalIgnoreCase) < 0))
                    {
                        mats[i] = body;
                    }
                }

                smr.sharedMaterials = mats;
                smr.updateWhenOffscreen = true;
            }

            var animator = rigGo.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = rigGo.AddComponent<Animator>();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rigGo.SetActive(true);
            _graph = PlayableGraph.Create("Rr Enemy " + Id);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Anim", animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            var a = AnimationClipPlayable.Create(_graph, idle);
            _graph.Connect(a, 0, _mixer, 0);
            _mixer.SetInputWeight(0, 1f);
            if (walk != null)
            {
                var w = AnimationClipPlayable.Create(_graph, walk);
                _graph.Connect(w, 0, _mixer, 1);
                _mixer.SetInputWeight(1, 0f);
            }

            output.SetSourcePlayable(_mixer);
            _graph.Play();
            _graph.Evaluate(0f);

            // Size: the bind-space AABB is useless (2.2.2); bake the posed mesh and fit it to the spawner's height.
            var main = smrs[0];
            foreach (var s in smrs)
            {
                if (s.sharedMesh != null && (main.sharedMesh == null || s.sharedMesh.vertexCount > main.sharedMesh.vertexCount))
                {
                    main = s;
                }
            }

            var rt = _rig.transform;
            _pivot.localRotation = Quaternion.identity;
            _pivot.localScale = Vector3.one;
            Bounds posed;
            if (!Baked(main, out posed) || posed.size.y < 0.05f)
            {
                RrLog.WarnOnce("Enemy", "rigsize:" + rigGo.name, "rig " + rigGo.name + " could not be measured: keeping the spawner mesh");
                DestroyRig();
                return;
            }

            float s0 = Mathf.Clamp(Height / posed.size.y, 0.05f, 20f);
            rt.localScale *= s0;
            _graph.Evaluate(0f);
            Baked(main, out posed);
            var local = transform.InverseTransformPoint(posed.center);
            float bottom = transform.InverseTransformPoint(posed.min).y;
            rt.localPosition -= _pivot.InverseTransformVector(transform.TransformVector(new Vector3(local.x, bottom, local.z)));
            foreach (var r in _renderers)
            {
                if (r != null)
                {
                    r.enabled = false;
                }
            }

            _renderers = rigGo.GetComponentsInChildren<Renderer>(true);
            foreach (var n in arm)
            {
                _armBone = _rig.Find(n);
                if (_armBone != null)
                {
                    _armRest = _armBone.localRotation;
                    break;
                }
            }

            HasRig = true;
            if (!_loggedRig)
            {
                _loggedRig = true;
                RrLog.Info("Enemy", "skinned rig " + rigGo.name + " on type " + Type + ": posed height fitted x" + s0.ToString("0.###") + " to " + Height.ToString("0.00") + " m, clips "
                    + idle.name + "/" + (walk != null ? walk.name : "-") + ", arm bone " + (_armBone != null ? _armBone.name : "-"));
            }
        }

        private static bool Baked(SkinnedMeshRenderer smr, out Bounds world)
        {
            world = new Bounds();
            if (smr == null || smr.sharedMesh == null)
            {
                return false;
            }

            var mesh = new Mesh();
            try
            {
                smr.BakeMesh(mesh, false);
                var v = mesh.vertices;
                if (v.Length == 0)
                {
                    return false;
                }

                var m = smr.transform.localToWorldMatrix;
                world = new Bounds(m.MultiplyPoint3x4(v[0]), Vector3.zero);
                for (int i = 1; i < v.Length; i++)
                {
                    world.Encapsulate(m.MultiplyPoint3x4(v[i]));
                }

                return true;
            }
            finally
            {
                Destroy(mesh);
            }
        }

        private void DestroyRig()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_rig != null)
            {
                Destroy(_rig.gameObject);
                _rig = null;
            }

            HasRig = false;
            _armBone = null;
        }

        // ---- lifecycle ----

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        internal static void ResetStatics()
        {
            All.Clear();
            OnSpawned = null;
            OnKilled = null;
            OnDamaged = null;
            ExternalSync = false;
            UseRigs = true;
            _nextId = 1;
            _loggedFirst = false;
            _loggedRig = false;
            _loggedNoTarget = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !ModelReady)
            {
                return;
            }

            _stateTime += dt;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            _squash = Mathf.MoveTowards(_squash, 0f, dt * 8f);
            if (!Simulating)
            {
                FollowNet(dt);
            }
            else
            {
                Simulate(dt);
            }

            Animate(dt);
            ApplyFlash();
        }

        private void Simulate(float dt)
        {
            switch (State)
            {
                case RrEnemyState.Dead:
                    return;
                case RrEnemyState.Spawning:
                    if (_stateTime > 0.5f)
                    {
                        SetState(RrEnemyState.Idle);
                    }

                    Ground(dt);
                    return;
                case RrEnemyState.Dying:
                    Dying(dt);
                    return;
                case RrEnemyState.Stunned:
                    if (Time.time >= _stunUntil)
                    {
                        SetState(RrEnemyState.Idle);
                    }

                    Ground(dt);
                    return;
            }

            if (Time.time >= _nextRetarget)
            {
                _nextRetarget = Time.time + 0.5f;
                PickTarget();
            }

            UpdateTarget();
            if (!_hasTarget || Passive)
            {
                if (State != RrEnemyState.Idle)
                {
                    SetState(RrEnemyState.Idle);
                }

                _speedNow = 0f;
                if (Kind == RrEnemyKind.Flyer)
                {
                    Fly(Vector3.zero, dt, false);
                }
                else
                {
                    Ground(dt);
                }

                return;
            }

            Vector3 pos = transform.position;
            Vector3 to = _targetFeet - pos;
            to.y = 0f;
            float dist = to.magnitude;
            Vector3 dirTo = dist > 1e-3f ? to / dist : transform.forward;
            switch (State)
            {
                case RrEnemyState.Windup:
                    Face(Kind == RrEnemyKind.Charger || Kind == RrEnemyKind.Lunger ? _attackDir : dirTo, dt, 540f);
                    UpdateAimLine();
                    if (Kind == RrEnemyKind.Flyer)
                    {
                        Fly(Vector3.zero, dt, false);
                    }
                    else
                    {
                        Ground(dt);
                    }

                    if (_stateTime >= Windup)
                    {
                        StartAttack();
                    }

                    return;
                case RrEnemyState.Attack:
                    Attack(dt, dirTo);
                    return;
                case RrEnemyState.Recover:
                    if (_stateTime > 0.4f)
                    {
                        SetState(RrEnemyState.Move);
                    }

                    break;
            }

            // Idle / Move / Recover: steer, and start an attack when in position.
            Vector3 want = Desire(dist, dirTo);
            if (Kind == RrEnemyKind.Flyer)
            {
                Fly(want, dt, true);
            }
            else
            {
                Walk(want, dt);
            }

            bool facing = Vector3.Dot(transform.forward, dirTo) > 0.7f;
            if (State != RrEnemyState.Recover && Time.time >= _nextAttack && InRange(dist) && (facing || Kind == RrEnemyKind.Flyer))
            {
                _attackDir = dirTo;
                SetState(RrEnemyState.Windup);
                if (Kind == RrEnemyKind.Melee)
                {
                    RrSound.Play("Throw01", Center, 0.3f, 0.8f);
                }
            }
            else if (State == RrEnemyState.Idle && want.sqrMagnitude > 0.01f)
            {
                SetState(RrEnemyState.Move);
            }
        }

        private bool InRange(float dist)
        {
            switch (Kind)
            {
                case RrEnemyKind.Melee:
                case RrEnemyKind.Exploder:
                    return dist <= AttackRange;
                case RrEnemyKind.Charger:
                case RrEnemyKind.Lunger:
                    return dist <= AttackRange && HasLine();
                default:
                    return dist <= KeepMax + 4f && HasLine();
            }
        }

        /// <summary>Can the enemy see the target's chest (no wall between)?</summary>
        private bool HasLine()
        {
            Vector3 eye = transform.position + Vector3.up * (Height * 0.7f);
            Vector3 d = _targetChest - eye;
            return !Physics.Raycast(eye, d.normalized, d.magnitude, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Where the archetype wants to go (unit or zero): close in, keep range, strafe, orbit.</summary>
        private Vector3 Desire(float dist, Vector3 dirTo)
        {
            var side = Vector3.Cross(Vector3.up, dirTo) * _orbitSign;
            switch (Kind)
            {
                case RrEnemyKind.Melee:
                case RrEnemyKind.Exploder:
                    return dist > AttackRange * 0.85f ? dirTo : Vector3.zero;
                case RrEnemyKind.Charger:
                    return dist > 3f ? dirTo : Vector3.zero;
                case RrEnemyKind.Lunger:
                    if (dist > 6f)
                    {
                        return dirTo;
                    }

                    return dist < 4f ? (-dirTo + side).normalized : side;
                case RrEnemyKind.Flyer:
                {
                    float radial = Mathf.Clamp((dist - (KeepMin + KeepMax) * 0.5f) * 0.5f, -1f, 1f);
                    return (dirTo * radial + side * 0.8f).normalized;
                }
                default:
                    if (dist > KeepMax)
                    {
                        return dirTo;
                    }

                    if (dist < KeepMin)
                    {
                        return -dirTo;
                    }

                    if (_dashLeft <= 0f && Time.time >= _nextDash && DashSpeed > 0f)
                    {
                        _nextDash = Time.time + UnityEngine.Random.Range(2f, 4f);
                        _orbitSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                        _dashDir = Vector3.Cross(Vector3.up, dirTo) * _orbitSign;
                        _dashLeft = UnityEngine.Random.Range(DashMin, DashMax);
                    }

                    return Vector3.zero;
            }
        }

        // ---- movement ----

        private void Walk(Vector3 want, float dt)
        {
            Vector3 pos = transform.position;
            float speed = MoveSpeed;
            if (_dashLeft > 0f)
            {
                want = _dashDir;
                speed = DashSpeed;
            }

            if (want.sqrMagnitude > 1e-4f)
            {
                want = Avoid(want.normalized);
            }

            want += Separation() * 0.8f;
            if (want.sqrMagnitude > 1f)
            {
                want.Normalize();
            }

            Vector3 step = want * speed * dt;
            if (_dashLeft > 0f)
            {
                _dashLeft -= step.magnitude;
                if (want.sqrMagnitude < 1e-4f)
                {
                    _dashLeft = 0f;
                }
            }

            transform.position = pos + step;
            _speedNow = step.magnitude / dt;
            if (_hasTarget && Kind != RrEnemyKind.Melee && Kind != RrEnemyKind.Exploder && Kind != RrEnemyKind.Charger)
            {
                Vector3 to = _targetFeet - transform.position;
                to.y = 0f;
                Face(to, dt, 360f);
            }
            else if (step.sqrMagnitude > 1e-6f)
            {
                Face(step, dt, 360f);
            }
            else if (_hasTarget)
            {
                Face(_targetFeet - transform.position, dt, 360f);
            }

            Ground(dt);
        }

        /// <summary>Flyers: hover above the ground, orbit the target, bank into turns (no ground snap).</summary>
        private void Fly(Vector3 want, float dt, bool face)
        {
            Vector3 pos = transform.position;
            float speed = _dashLeft > 0f ? DashSpeed : MoveSpeed;
            if (want.sqrMagnitude > 1e-4f)
            {
                want = Avoid(want.normalized);
            }

            want += Separation();
            Vector3 step = Vector3.ClampMagnitude(want, 1f) * speed * dt;
            float groundY = GroundY(pos, out bool found);
            float desired = (found ? groundY : pos.y) + Hover;
            if (_hasTarget)
            {
                desired = Mathf.Max(desired, _targetFeet.y + Hover * 0.8f);
            }

            float y = Mathf.MoveTowards(pos.y, desired, 2f * dt);
            transform.position = new Vector3(pos.x + step.x, y, pos.z + step.z);
            _speedNow = step.magnitude / dt;
            if (face && _hasTarget)
            {
                Face(_targetFeet - transform.position, dt, 240f);
            }
        }

        /// <summary>Obstacle and ledge probes: the wanted direction, or the nearest free one within 110 degrees.</summary>
        private Vector3 Avoid(Vector3 dir)
        {
            if (!Blocked(dir))
            {
                return dir;
            }

            float[] tries = { 35f, -35f, 70f, -70f, 110f, -110f };
            foreach (float a in tries)
            {
                var d = Quaternion.Euler(0f, a * _orbitSign, 0f) * dir;
                if (!Blocked(d))
                {
                    return d;
                }
            }

            _orbitSign = -_orbitSign;
            return Vector3.zero;
        }

        private bool Blocked(Vector3 dir)
        {
            Vector3 from = transform.position + Vector3.up * Mathf.Min(0.6f, Height * 0.5f);
            float probe = Radius + 0.6f;
            if (Physics.SphereCast(from, Mathf.Min(Radius * 0.6f, 0.3f), dir, out _, probe, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            if (Kind == RrEnemyKind.Flyer)
            {
                return false;
            }

            // Ledge: no floor within 1.5 m below a point just ahead.
            Vector3 ahead = transform.position + dir * (Radius + 0.4f) + Vector3.up * 0.8f;
            return !Physics.Raycast(ahead, Vector3.down, 2.3f, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            Vector3 pos = transform.position;
            foreach (var o in All)
            {
                if (o == null || o == this || !o.Alive)
                {
                    continue;
                }

                Vector3 d = pos - o.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                float min = Radius + o.Radius + 0.2f;
                if (m < min && m > 1e-3f)
                {
                    push += d / m * (1f - m / min);
                }
            }

            return push;
        }

        private float GroundY(Vector3 pos, out bool found)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out hit, 60f, RrProjectile.WorldMask, QueryTriggerInteraction.Ignore))
            {
                found = true;
                return hit.point.y;
            }

            found = false;
            return pos.y;
        }

        private void Ground(float dt)
        {
            if (Kind == RrEnemyKind.Flyer && State != RrEnemyState.Dying)
            {
                return;
            }

            var pos = transform.position;
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out hit, 1.2f + Mathf.Max(0.6f, -_vy * dt + 0.1f), RrProjectile.WorldMask, QueryTriggerInteraction.Ignore))
            {
                _vy = 0f;
                pos.y = Mathf.MoveTowards(pos.y, hit.point.y, 6f * dt);
            }
            else
            {
                _vy -= 9.81f * dt;
                pos.y += _vy * dt;
                if (pos.y < -50f)
                {
                    RrLog.WarnOnce("Enemy", "fell:" + Id, "enemy " + Id + " fell out of the world: removed");
                    Health = 0;
                    SetState(RrEnemyState.Dead);
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = pos;
        }

        private void Face(Vector3 dir, float dt, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
            {
                _turnRate = 0f;
                return;
            }

            var want = Quaternion.LookRotation(dir);
            float before = transform.eulerAngles.y;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, degPerSec * dt);
            _turnRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / dt;
        }

        // ---- targets ----

        private void PickTarget()
        {
            _hasTarget = false;
            Target = RrCombat.NoPlayer;
            _targetRoot = null;
            _targetHead = null;
            float best = AggroRange;
            Vector3 pos = transform.position;
            var health = RrHealth.Local;
            if (health == null || health.Targetable)
            {
                float d = Vector3.Distance(pos, RrPlayer.FeetPosition);
                if (d < best && RrPlayer.Camera != null)
                {
                    best = d;
                    Target = RrCombat.LocalId;
                    _hasTarget = true;
                }
            }

            var remotes = RrRemotePlayers.All;
            if (!_hasTarget && !Passive && !_loggedNoTarget)
            {
                _loggedNoTarget = true;
                RrLog.Info("Enemy", "enemy " + Id + " at " + pos.ToString("F1") + " sees no local target: camera " + (RrPlayer.Camera != null ? RrPlayer.Camera.name : "none") + ", feet "
                    + RrPlayer.FeetPosition.ToString("F1") + ", targetable " + (health == null || health.Targetable) + ", aggro " + AggroRange + " m, remotes " + (remotes != null ? remotes.Count : 0));
            }

            if (remotes == null)
            {
                return;
            }

            foreach (var r in remotes)
            {
                // Players in other rooms stand at the same world origin, invisible: never target them.
                if (r == null || r.Root == null || r.Id == RrCombat.LocalId || !r.WithMe)
                {
                    continue;
                }

                var p = RrNet.Presence(r.Id);
                if (p.Valid && (p.Status == Rooms.RrStatus.Down || p.Status == Rooms.RrStatus.Spectating))
                {
                    continue;
                }

                float d = Vector3.Distance(pos, r.Root.position);
                if (d < best)
                {
                    best = d;
                    Target = r.Id;
                    _targetRoot = r.Root;
                    _targetHead = r.Head;
                    _hasTarget = true;
                }
            }
        }

        private void UpdateTarget()
        {
            if (!_hasTarget)
            {
                return;
            }

            if (Target == RrCombat.LocalId && _targetRoot == null)
            {
                var health = RrHealth.Local;
                if (health != null && !health.Targetable)
                {
                    _hasTarget = false;
                    return;
                }

                _targetFeet = RrPlayer.FeetPosition;
                var head = RrPlayer.HeadPosition;
                _targetChest = Vector3.Lerp(_targetFeet, head, 0.75f);
                return;
            }

            if (_targetRoot == null)
            {
                _hasTarget = false;
                return;
            }

            _targetFeet = _targetRoot.position;
            _targetChest = _targetHead != null ? Vector3.Lerp(_targetFeet, _targetHead.position, 0.75f) : _targetFeet + Vector3.up * 1.2f;
        }

        // ---- attacks (the victim decides: only the local player is tested here) ----

        private void StartAttack()
        {
            SetState(RrEnemyState.Attack);
            _attackResolved = false;
            _charged = 0f;
            _shotsLeft = Burst;
            _nextBurstShot = 0f;
            HideAimLine();
            if (Kind == RrEnemyKind.Charger || Kind == RrEnemyKind.Lunger)
            {
                RrSound.Play("Throw01", Center, 0.6f, 0.7f);
            }
        }

        private void Attack(float dt, Vector3 dirTo)
        {
            switch (Kind)
            {
                case RrEnemyKind.Melee:
                    if (!_attackResolved)
                    {
                        _attackResolved = true;
                        float reach = AttackRange > 1.5f ? 1.2f : 0.9f;
                        MeleeStrike(transform.position + transform.forward * reach + Vector3.up * (Height * 0.5f), 0.6f, 2f);
                    }

                    Ground(dt);
                    if (_stateTime >= ActiveTime)
                    {
                        EndAttack();
                    }

                    return;
                case RrEnemyKind.Charger:
                case RrEnemyKind.Lunger:
                {
                    float speed = Kind == RrEnemyKind.Charger ? ChargeSpeed : Mathf.Max(DashSpeed * 2f, 6f);
                    float distance = Kind == RrEnemyKind.Charger ? ChargeDistance : Mathf.Max(DashMax * 1.5f, 3f);
                    Vector3 step = _attackDir * speed * dt;
                    if (Blocked(_attackDir) || _charged >= distance)
                    {
                        EndAttack();
                        return;
                    }

                    transform.position += step;
                    _charged += step.magnitude;
                    _speedNow = speed;
                    Ground(dt);
                    if (!_attackResolved && LocalPlayerWithin(Center, Radius + 0.45f))
                    {
                        _attackResolved = true;
                        HurtLocal(Damage, Center);
                    }

                    return;
                }
                case RrEnemyKind.Exploder:
                    Explode();
                    return;
                default:
                    if (Kind == RrEnemyKind.Flyer)
                    {
                        Fly(Vector3.zero, dt, true);
                    }
                    else
                    {
                        Ground(dt);
                        Face(dirTo, dt, 360f);
                    }

                    if (_shotsLeft > 0 && _stateTime >= _nextBurstShot)
                    {
                        _shotsLeft--;
                        _nextBurstShot = _stateTime + 0.15f;
                        Shoot();
                    }

                    if (_shotsLeft <= 0 && _stateTime >= Mathf.Max(ActiveTime, 0.15f * Burst))
                    {
                        EndAttack();
                    }

                    return;
            }
        }

        private void EndAttack()
        {
            _nextAttack = Time.time + Cooldown;
            SetState(RrEnemyState.Recover);
        }

        private void MeleeStrike(Vector3 center, float radius, float blockStun)
        {
            RrSound.Play("Throw01", center, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f));
            RrMelee blocker;
            if (RrMelee.TryBlock(center, radius, out blocker))
            {
                Stun(Type == 21 || Type == 34 ? blockStun : 0.5f);
                return;
            }

            if (LocalPlayerWithin(center, radius))
            {
                HurtLocal(Damage, center);
            }
        }

        private void Shoot()
        {
            if (_shot == null)
            {
                _shot = Kind == RrEnemyKind.Thrower
                    ? new RrShotSpec { Name = "enemy lob", Speed = ProjectileSpeed, Gravity = 9.81f, Radius = 0.08f, Color = ShotColor, TeamColored = false, HeadDamage = Damage, BodyDamage = Damage, ExplosionRadius = Splash, ExplosionDamage = Damage, Crits = false }
                    : RrShotSpec.Bolt(Mathf.Max(ProjectileSpeed, 3f), Damage, ShotColor);
                if (Kind != RrEnemyKind.Thrower && Splash > 0f)
                {
                    _shot.ExplosionRadius = Splash;
                    _shot.ExplosionDamage = Damage;
                }

                _shot.Tool = RrCombat.EnemyTool;
                _shot.Life = 6f;
            }

            Vector3 origin = transform.position + Vector3.up * (Height * 0.6f) + transform.forward * (Radius + 0.15f);
            Vector3 aim = _targetChest - origin;
            Vector3 v;
            if (Kind == RrEnemyKind.Thrower)
            {
                v = Lob(aim, _shot.Speed, _shot.Gravity);
            }
            else
            {
                v = aim.normalized * _shot.Speed;
            }

            int n = Mathf.Max(1, Volley);
            for (int i = 0; i < n; i++)
            {
                float yaw = n > 1 ? Mathf.Lerp(-20f, 20f, i / (float)(n - 1)) : 0f;
                RrProjectile.Launch(_shot, origin, Quaternion.Euler(0f, yaw, 0f) * v, RrCombat.NoPlayer, false, 0, transform, this, ShotColor);
            }

            if (!string.IsNullOrEmpty(MuzzleFx))
            {
                RrImpacts.Fx(MuzzleFx, origin, Quaternion.LookRotation(v), 1f);
            }

            if (!string.IsNullOrEmpty(ShotClip))
            {
                RrSound.Play(ShotClip, origin, 0.7f, UnityEngine.Random.Range(0.85f, 1f));
            }
        }

        /// <summary>The low ballistic arc that lands on the target at this speed (45 degrees when out of reach).</summary>
        private static Vector3 Lob(Vector3 to, float speed, float g)
        {
            var flat = new Vector3(to.x, 0f, to.z);
            float d = flat.magnitude;
            float h = to.y;
            float v2 = speed * speed;
            float root = v2 * v2 - g * (g * d * d + 2f * h * v2);
            float angle = root >= 0f && d > 0.01f ? Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * d)) : Mathf.PI / 4f;
            var dir = d > 0.01f ? flat / d : Vector3.forward;
            return (dir * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)) * speed;
        }

        private void Explode()
        {
            Vector3 c = Center;
            RrImpacts.Fx("[Arena_Grenade_Explosion_VFX] JumboTron Variant", c, Quaternion.identity, 1f);
            RrSound.Play("Paintball_GrenadeExplode01", c, 0.9f, 1f);
            var health = RrHealth.Local;
            if (health != null)
            {
                float d = Vector3.Distance(c, ClosestOnLocal(c));
                if (d <= Splash)
                {
                    float k = d <= Splash * 0.666f ? 1f : Mathf.Lerp(1f, 0.2f, (d - Splash * 0.666f) / (Splash * 0.334f));
                    HurtLocal(Mathf.RoundToInt(Damage * k), c);
                }
            }

            Die(RrCombat.NoPlayer, false);
        }

        private void HurtLocal(int damage, Vector3 point)
        {
            var health = RrHealth.Local;
            if (health != null && damage > 0)
            {
                health.ApplyDamage(damage, RrCombat.NoPlayer, RrBodyPart.Body, point, RrCombat.EnemyTool);
            }
        }

        private static Vector3 ClosestOnLocal(Vector3 p)
        {
            Vector3 a = RrPlayer.FeetPosition;
            Vector3 b = RrPlayer.HeadPosition;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        /// <summary>The local player's body (a capsule feet-head, r 0.25) meets a sphere.</summary>
        private static bool LocalPlayerWithin(Vector3 center, float radius)
        {
            if (RrPlayer.Camera == null)
            {
                return false;
            }

            return Vector3.Distance(ClosestOnLocal(center), center) <= radius + 0.25f;
        }

        private void UpdateAimLine()
        {
            if (Kind != RrEnemyKind.Heavy || !_hasTarget)
            {
                return;
            }

            if (_aimLine == null)
            {
                var go = new GameObject("Aim Laser");
                go.transform.SetParent(transform, false);
                _aimLine = go.AddComponent<LineRenderer>();
                _aimLine.positionCount = 2;
                _aimLine.startWidth = 0.02f;
                _aimLine.endWidth = 0.02f;
                _aimLine.sharedMaterial = RrLook.Emissive(new Color(1f, 0.2f, 0.8f), 3f);
                _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            _aimLine.enabled = true;
            Vector3 origin = transform.position + Vector3.up * (Height * 0.6f) + transform.forward * (Radius + 0.15f);
            _aimLine.SetPosition(0, origin);
            if (_stateTime < Windup - 0.4f)
            {
                _aimLine.SetPosition(1, _targetChest);
            }
        }

        private void HideAimLine()
        {
            if (_aimLine != null)
            {
                _aimLine.enabled = false;
            }
        }

        // ---- damage ----

        /// <summary>
        /// The local player's weapon hit this enemy (projectile or blade). Feedback at once; the damage is applied
        /// where the enemy is simulated and reported to the others (ActorHit).
        /// </summary>
        public void TakeLocalHit(int damage, RrBodyPart part, Vector3 point, byte tool)
        {
            if (!Alive)
            {
                return;
            }

            _flash = 1f;
            bool simulating = Simulating;
            bool send = RrNet.Online && !(ExternalSync && simulating);
            RrCombat.SendActorHit(Id, damage, part, tool, point, send, ExternalSync);
            if (simulating)
            {
                Apply(damage, RrCombat.LocalId, part, point);
            }
        }

        /// <summary>Another member's ActorHit (to the authority, or broadcast while every client simulates).</summary>
        public static void ApplyRemoteHit(RrCombat.ActorHitInfo a)
        {
            var e = Find(a.Actor);
            if (e == null || !e.Alive)
            {
                return;
            }

            e._flash = 1f;
            if (e.Simulating)
            {
                e.Apply(a.Damage, a.Attacker, a.Part, a.Point);
            }
        }

        private void Apply(int damage, int attacker, RrBodyPart part, Vector3 point)
        {
            if (!Alive || damage <= 0)
            {
                return;
            }

            if (Kind == RrEnemyKind.Charger && part != RrBodyPart.WeakSpot)
            {
                RrSound.Play("Quest_Sword_Impactshield", point, 0.7f, 1f);
                return;
            }

            if (Kind == RrEnemyKind.Heavy)
            {
                damage = part == RrBodyPart.WeakSpot ? Mathf.RoundToInt(damage * 1.5f) : Mathf.Max(1, damage / 2);
            }

            Health = Mathf.Max(0, Health - damage);
            _squash = 1f;
            var h = OnDamaged;
            if (h != null)
            {
                h(this, damage, attacker);
            }

            if (Health <= 0)
            {
                Die(attacker, true);
            }
        }

        /// <summary>Knocked out of action for a while (a parried swing, a game effect).</summary>
        public void Stun(float seconds)
        {
            if (!Alive || seconds <= 0f)
            {
                return;
            }

            _stunUntil = Time.time + seconds;
            HideAimLine();
            SetState(RrEnemyState.Stunned);
        }

        /// <summary>Dies now (also for games: kill volumes, act resets). points: raise OnKilled with the killer.</summary>
        public void Die(int killer, bool points)
        {
            if (State == RrEnemyState.Dying || State == RrEnemyState.Dead)
            {
                return;
            }

            Health = 0;
            _killer = killer;
            HideAimLine();
            SetState(RrEnemyState.Dying);
            foreach (var v in _volumes)
            {
                if (v != null)
                {
                    v.gameObject.SetActive(false);
                }
            }

            if (points)
            {
                var h = OnKilled;
                if (h != null)
                {
                    try
                    {
                        h(this, killer);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Enemy", "kill handler failed", e);
                    }
                }
            }
        }

        private void Dying(float dt)
        {
            if (Kind == RrEnemyKind.Flyer)
            {
                Ground(dt);
            }

            if (_stateTime < 0.45f || _finished)
            {
                return;
            }

            _finished = true;
            Vector3 c = Center;
            RrImpacts.Fx("VFX_Generic_SmokePoof01", c, Quaternion.identity, Mathf.Clamp(Height, 0.8f, 2f));
            if (!string.IsNullOrEmpty(CoinFx) && _killer != RrCombat.NoPlayer)
            {
                RrImpacts.Fx(CoinFx, c, Quaternion.identity, 1f);
                RrSound.Play("Quest_Coins_Spawn01", c, 0.8f, 1f);
            }

            SetState(RrEnemyState.Dead);
            Destroy(gameObject, 0.05f);
        }

        private void SetState(RrEnemyState s)
        {
            if (State != s)
            {
                State = s;
                _stateTime = 0f;
                if (s != RrEnemyState.Windup)
                {
                    HideAimLine();
                }
            }
        }

        // ---- network adapter (for the Net actor channel, see the class notes) ----

        public RrActorState CaptureState()
        {
            int flags = RrActorWire.FlagUsed;
            if (Alive)
            {
                flags |= RrActorWire.FlagAlive;
            }

            if (State == RrEnemyState.Attack || State == RrEnemyState.Windup)
            {
                flags |= RrActorWire.FlagAttacking;
            }

            if (_squash > 0f)
            {
                flags |= RrActorWire.FlagHurt;
            }

            if (State == RrEnemyState.Stunned)
            {
                flags |= RrActorWire.FlagStunned;
            }

            return new RrActorState
            {
                Position = transform.position - RrCombat.Origin,
                Yaw = transform.eulerAngles.y,
                Type = Type & 255,
                Anim = (int)State,
                Health = Mathf.CeilToInt(100f * Health / Mathf.Max(1, MaxHealth)),
                Flags = flags
            };
        }

        /// <summary>A watcher's copy follows the authority's state (position and yaw are smoothed in Update).</summary>
        public void ApplyState(RrActorState s)
        {
            _netPos = RrCombat.Origin + s.Position;
            _netYaw = s.Yaw;
            if (!_hasNet)
            {
                _hasNet = true;
                transform.SetPositionAndRotation(_netPos, Quaternion.Euler(0f, _netYaw, 0f));
            }

            Health = Mathf.RoundToInt(MaxHealth * s.Health / 100f);
            var st = (RrEnemyState)Mathf.Clamp(s.Anim, 0, (int)RrEnemyState.Dead);
            if (!s.Alive && Alive)
            {
                Die(RrCombat.NoPlayer, false);
            }
            else if (st != State && State != RrEnemyState.Dying && State != RrEnemyState.Dead)
            {
                SetState(st);
            }
        }

        private void FollowNet(float dt)
        {
            if (State == RrEnemyState.Dying)
            {
                Dying(dt);
            }

            if (!_hasNet)
            {
                return;
            }

            Vector3 before = transform.position;
            transform.position = Vector3.Lerp(before, _netPos, 1f - Mathf.Exp(-10f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _netYaw, 0f), 1f - Mathf.Exp(-10f * dt));
            _speedNow = (transform.position - before).magnitude / dt;
        }

        // ---- procedural animation (our pivot and the rig's arm; never the model's own transforms) ----

        private void Animate(float dt)
        {
            if (_pivot == null)
            {
                return;
            }

            float k = _stateTime;
            float moving = Mathf.Clamp01(_speedNow / Mathf.Max(0.5f, MoveSpeed));
            _phase += dt * (2f + 2f * moving) * Mathf.PI;
            Vector3 pos = Vector3.zero;
            Vector3 euler = Vector3.zero;
            Vector3 scale = Vector3.one;
            bool flyer = Kind == RrEnemyKind.Flyer;
            switch (State)
            {
                case RrEnemyState.Spawning:
                    scale = Vector3.one * Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(k / 0.4f));
                    break;
                case RrEnemyState.Windup:
                {
                    float w = Mathf.Clamp01(k / Mathf.Max(0.05f, Windup));
                    euler.x = -12f * w;
                    scale = Vector3.one * (1f + 0.05f * w);
                    break;
                }
                case RrEnemyState.Attack:
                {
                    float a = Mathf.Clamp01(k / 0.15f);
                    if (Kind == RrEnemyKind.Melee)
                    {
                        pos.z = 0.25f * Mathf.Sin(Mathf.Min(1f, k / Mathf.Max(0.05f, ActiveTime)) * Mathf.PI);
                    }

                    euler.x = 20f * a;
                    break;
                }
                case RrEnemyState.Stunned:
                    euler.z = 70f * Mathf.Clamp01(k / 0.2f);
                    break;
                case RrEnemyState.Dying:
                case RrEnemyState.Dead:
                {
                    float d = Mathf.Clamp01(k / 0.4f);
                    euler.x = flyer ? 0f : 90f * d * d;
                    euler.z = flyer ? 120f * d : 0f;
                    scale = Vector3.one * Mathf.Lerp(1f, 0.4f, d);
                    break;
                }
                default:
                    if (flyer)
                    {
                        pos.y = 0.08f * Mathf.Sin(Time.time * 1.5f * 2f * Mathf.PI + Id);
                        euler.z = Mathf.Clamp(-_turnRate * 0.15f, -25f, 25f);
                    }
                    else if (!HasRig)
                    {
                        pos.y = 0.03f * Mathf.Abs(Mathf.Sin(_phase)) * moving;
                        euler.x = 6f * moving;
                        euler.z = (Kind == RrEnemyKind.Charger ? 8f : 2f) * Mathf.Sin(_phase * 0.5f) * moving;
                        if (moving < 0.1f)
                        {
                            scale = new Vector3(1f, 1f + 0.01f * (1f + Mathf.Sin(Time.time * Mathf.PI)), 1f);
                        }
                    }

                    break;
            }

            if (_squash > 0f)
            {
                scale = Vector3.Scale(scale, Vector3.Lerp(Vector3.one, new Vector3(1.1f, 0.9f, 1.1f), _squash));
            }

            // Tip over around the feet: the pivot's origin is on the ground, so rotating it tips the body.
            float bottom = _pivot.localPosition.y;
            _pivot.localPosition = new Vector3(pos.x, bottom, pos.z) + new Vector3(0f, pos.y - _lastBob, 0f);
            _lastBob = pos.y;
            _pivot.localRotation = Quaternion.Euler(euler);
            _pivot.localScale = scale;

            if (_mixer.IsValid())
            {
                float w = State == RrEnemyState.Move || State == RrEnemyState.Recover || State == RrEnemyState.Attack ? moving : 0f;
                _mixer.SetInputWeight(0, 1f - w);
                if (_mixer.GetInputCount() > 1 && _mixer.GetInput(1).IsValid())
                {
                    _mixer.SetInputWeight(1, w);
                }
            }

            float target = 0f;
            if (State == RrEnemyState.Windup)
            {
                target = -90f * Mathf.Clamp01(k / Mathf.Max(0.05f, Windup));
            }
            else if (State == RrEnemyState.Attack)
            {
                target = Mathf.Lerp(-90f, 30f, Mathf.Clamp01(k / Mathf.Max(0.05f, ActiveTime)));
            }

            _armAngle = State == RrEnemyState.Attack || State == RrEnemyState.Windup ? target : Mathf.MoveTowards(_armAngle, 0f, dt * 300f);
        }

        private float _lastBob;

        private void LateUpdate()
        {
            // After the graph wrote the pose: the swing is added to the arm bone.
            if (_armBone != null && Mathf.Abs(_armAngle) > 0.01f)
            {
                var basePose = _graph.IsValid() ? _armBone.localRotation : _armRest;
                _armBone.localRotation = basePose * Quaternion.Euler(_armAngle, 0f, 0f);
            }
        }

        private void ApplyFlash()
        {
            if (_renderers == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            bool on = _flash > 0.01f;
            if (!on && !_flashShown)
            {
                return;
            }

            _flashShown = on;
            var c = Color.white * (_flash * 1.5f);
            foreach (var r in _renderers)
            {
                if (r == null)
                {
                    continue;
                }

                if (on)
                {
                    r.GetPropertyBlock(_block);
                    _block.SetColor("_EmissionColor", c);
                    _block.SetFloat("_EmissivePower", _flash * 2f);
                    r.SetPropertyBlock(_block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }

        private bool _flashShown;
    }
}
