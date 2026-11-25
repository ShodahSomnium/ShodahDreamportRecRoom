using System;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// The local player's hit points in a game (one per world, "Rr Health" under the world root). The victim decides
    /// (somnium_networking.md §5.7): hits from other shooters arrive as Hit events through <see cref="RrCombat.OnHit"/>,
    /// enemy attacks are applied here directly by the enemy's code on this client, and at zero this client sends
    /// Death and runs the out / respawn cycle itself.
    ///
    /// Rec Room's numbers: Paintball's game configuration has MaxHealth 1 (any hit is out), AutoRespawn after 5 s,
    /// RespawnInvincibilityDuration 5 s, RestoreHealthOnRespawn, DamageRedFlashEffect and DeadMonochromeEffect
    /// (rr_shooters.md §4.1); Laser Tag uses 100 HP, and the quests 1 as well (one hit downs, Rec Room's final quest
    /// rule, rr_latest_rgames_story.md 5.3). Games set these with <see cref="Begin"/>.
    ///
    /// Feedback never touches the Somnium camera or avatar: on desktop a screen-space overlay canvas draws a red
    /// edge vignette (hit) or a grey veil (out); in VR, where overlay canvases do not render, our own quad is placed
    /// 0.3 m in front of the head just before rendering (Application.onBeforeRender), which reads the same in both eyes.
    /// </summary>
    public sealed class RrHealth : MonoBehaviour
    {
        public static RrHealth Local { get; private set; }

        /// <summary>A game is running for the local player: damage counts. Outside a game every hit is ignored.</summary>
        public bool InCombat;

        public int MaxHealth = 100;
        public int Health = 100;

        /// <summary>Seconds from out to respawn (Paintball AutoRespawn 5 s); 0 or AutoRespawn off: the game calls <see cref="Respawn"/>.</summary>
        public float RespawnDelay = 5f;
        public bool AutoRespawn = true;

        /// <summary>Paintball RespawnInvincibilityDuration 5 s.</summary>
        public float RespawnInvincibility = 5f;

        /// <summary>Movement while out (restored on respawn); 1 = unchanged. Quests use 0.3 while downed (rr_quests.md 3.7).</summary>
        public float OutMovementScale = 1f;

        /// <summary>Where the player comes back: a game's own choice; null uses the room's spawn for the local team.</summary>
        public Func<Pose> RespawnPoint;

        /// <summary>
        /// A game's veto on a hit before it counts: (damage, attacker PlayerId or -1, tool id) -> false ignores it. Null
        /// (every game but the quests) accepts every hit. The quests use it because Rec Room's final rule is "one hit
        /// from an enemy (or an ally's ranged weapon) will down you": an ally's sword or shield must not. Cleared by
        /// <see cref="End"/>, so it never outlives the game that set it.
        /// </summary>
        public Func<int, int, byte, bool> Accepts;

        public RrStatus Status { get; private set; } = RrStatus.Playing;
        public float InvincibleUntil { get; private set; }
        public float OutSince { get; private set; } = -1f;
        public int LastAttacker { get; private set; } = RrCombat.NoPlayer;

        /// <summary>Damage taken (after the checks): amount, attacker (or -1), point.</summary>
        public event Action<int, int, Vector3> OnDamaged;

        /// <summary>The local player went out: killer (or -1), tool.</summary>
        public event Action<int, byte> OnOut;

        public event Action OnRespawned;

        private const string HitClip = "HitSound01";
        private const string OutClip = "OutSFX";
        private static readonly Color FlashColor = new Color(0.95f, 0.05f, 0.05f, 1f);
        private static readonly Color OutColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        private float _flash;
        private float _respawnAt = -1f;
        private bool _movementChanged;
        private bool _loggedFirstHit;
        private Canvas _canvas;
        private RawImage _screen;
        private GameObject _quad;
        private Material _quadMaterial;
        private Texture2D _vignette;
        private bool _subscribed;

        public bool IsOut
        {
            get { return Status == RrStatus.Down || Status == RrStatus.Spectating; }
        }

        /// <summary>Enemies pick the local player only while this is true (not out, and in a game or no game at all).</summary>
        public bool Targetable
        {
            get { return !IsOut; }
        }

        /// <summary>Can hold up a weapon and fire (false while out).</summary>
        public static bool CanAct
        {
            get { return Local == null || !Local.IsOut; }
        }

        public bool Invincible
        {
            get { return Time.time < InvincibleUntil; }
        }

        public static RrHealth Ensure()
        {
            if (Local != null)
            {
                return Local;
            }

            RrCombat.Ensure();
            RrHitVolumes.Ensure();
            var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            var go = new GameObject("Rr Health");
            go.transform.SetParent(host, false);
            return go.AddComponent<RrHealth>();
        }

        /// <summary>A game starts for the local player: full health, damage counts. maxHealth 1 = one hit is out (Paintball).</summary>
        public void Begin(int maxHealth, float respawnDelay = 5f, float respawnInvincibility = 5f)
        {
            MaxHealth = Mathf.Max(1, maxHealth);
            Health = MaxHealth;
            RespawnDelay = respawnDelay;
            RespawnInvincibility = respawnInvincibility;
            InCombat = true;
            _respawnAt = -1f;
            OutSince = -1f;
            RestoreMovement();
            SetStatus(RrStatus.Playing);
            RrLog.Info("Health", "combat on: " + MaxHealth + " HP, respawn " + RespawnDelay + " s, invincible " + RespawnInvincibility + " s");
        }

        /// <summary>The game is over (or the player left): damage stops counting, everything visual is cleared.</summary>
        public void End()
        {
            InCombat = false;
            Accepts = null;
            Health = MaxHealth;
            _respawnAt = -1f;
            OutSince = -1f;
            _flash = 0f;
            RestoreMovement();
            SetStatus(RrStatus.Playing);
        }

        /// <summary>
        /// Damage to the local player (from a Hit event, an enemy or a blast). attacker: a PlayerId or -1 (enemy,
        /// fall...). Returns the damage that counted.
        /// </summary>
        public int ApplyDamage(int damage, int attacker, RrBodyPart part, Vector3 point, byte tool)
        {
            if (!InCombat || IsOut || damage <= 0 || Invincible)
            {
                return 0;
            }

            if (Accepts != null && !Accepts(damage, attacker, tool))
            {
                return 0;
            }

            int taken = Mathf.Min(damage, Health);
            Health -= taken;
            LastAttacker = attacker;
            _flash = 1f;
            if (!_loggedFirstHit)
            {
                _loggedFirstHit = true;
                RrLog.Info("Health", "first hit on the local player: " + damage + " (" + part + ") from " + (attacker >= 0 ? "player " + attacker : "an enemy") + ", " + Health + "/" + MaxHealth + " left");
            }

            RrSound.PlayUi(HitClip, 0.5f);
            var hand = RrHand.Right;
            if (hand != null)
            {
                hand.Haptic(0.6f, 0.08f);
            }

            var h = OnDamaged;
            if (h != null)
            {
                h(taken, attacker, point);
            }

            if (Health <= 0)
            {
                GoOut(attacker, tool, point);
            }
            else
            {
                Publish();
            }

            return taken;
        }

        public void Heal(int amount)
        {
            if (!IsOut && amount > 0)
            {
                Health = Mathf.Min(MaxHealth, Health + amount);
                Publish();
            }
        }

        /// <summary>Out now (hit to zero, out of bounds, a game rule): sends Death, starts the respawn timer.</summary>
        public void GoOut(int killer, byte tool, Vector3 point)
        {
            if (IsOut)
            {
                return;
            }

            Health = 0;
            OutSince = Time.time;
            SetStatus(RrStatus.Down);
            RrCombat.SendDeath(killer, tool, point);
            RrSound.PlayUi(OutClip, 0.5f);
            if (OutMovementScale < 0.999f)
            {
                RrPlayer.SetMovement(OutMovementScale);
                _movementChanged = true;
            }

            _respawnAt = AutoRespawn && RespawnDelay > 0f ? Time.time + RespawnDelay : -1f;
            RrLog.Info("Health", "local player out (killer " + killer + ", tool " + tool + ")" + (_respawnAt > 0f ? ", respawn in " + RespawnDelay + " s" : ""));
            var h = OnOut;
            if (h != null)
            {
                h(killer, tool);
            }
        }

        /// <summary>Back in: full health, invincible for a moment, teleported to the respawn point (teleport false: stay).</summary>
        public void Respawn(bool teleport = true)
        {
            Health = MaxHealth;
            OutSince = -1f;
            _respawnAt = -1f;
            InvincibleUntil = Time.time + RespawnInvincibility;
            RestoreMovement();
            SetStatus(RrStatus.Playing);
            if (teleport)
            {
                var pose = RespawnPose();
                RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y);
            }

            var h = OnRespawned;
            if (h != null)
            {
                h();
            }
        }

        /// <summary>A downed player brought back by a teammate (quests): some health, both briefly invincible.</summary>
        public void Revive(int health = 50, float invincible = 3f)
        {
            Respawn(false);
            Health = Mathf.Clamp(health, 1, MaxHealth);
            InvincibleUntil = Time.time + invincible;
            RrSound.PlayUi("RecRoyale_Revive_Revived01", 0.7f);
            Publish();
        }

        public Pose RespawnPose()
        {
            if (RespawnPoint != null)
            {
                try
                {
                    return RespawnPoint();
                }
                catch (Exception e)
                {
                    RrLog.Error("Health", "respawn point failed", e);
                }
            }

            var room = RrTravel.CurrentRoom;
            if (room != null)
            {
                int team = RrTeams.LocalTeam;
                return room.WorldSpawn(team >= 0 ? team : 0);
            }

            return new Pose(RrPlayer.FeetPosition, Quaternion.Euler(0f, RrPlayer.Yaw, 0f));
        }

        // ---- lifecycle ----

        private void Awake()
        {
            Local = this;
            Health = MaxHealth;
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                _subscribed = true;
                RrCombat.OnHit += OnCombatHit;
                Application.onBeforeRender += PlaceQuad;
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                _subscribed = false;
                RrCombat.OnHit -= OnCombatHit;
                Application.onBeforeRender -= PlaceQuad;
            }
        }

        private void OnDestroy()
        {
            RestoreMovement();
            if (Local == this)
            {
                Local = null;
            }

            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }

            if (_quad != null)
            {
                Destroy(_quad);
            }

            if (_quadMaterial != null)
            {
                Destroy(_quadMaterial);
            }

            if (_vignette != null)
            {
                Destroy(_vignette);
            }
        }

        internal static void ResetStatics()
        {
            Local = null;
        }

        private void OnCombatHit(RrCombat.HitInfo info)
        {
            if (info.Victim == RrCombat.LocalId && info.Shooter != RrCombat.LocalId)
            {
                ApplyDamage(info.Damage, info.Shooter, info.Part, info.Point, info.Tool);
            }
        }

        private void Update()
        {
            if (_respawnAt > 0f && Time.time >= _respawnAt)
            {
                Respawn();
            }

            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 2.5f);
            float outAlpha = IsOut && InCombat ? 0.45f : 0f;
            float hitAlpha = _flash * 0.85f;
            DrawFeedback(hitAlpha, outAlpha);
        }

        private void SetStatus(RrStatus s)
        {
            Status = s;
            Publish();
        }

        private void Publish()
        {
            RrPresenceStats.Publish(Health, RrCombat.LocalKills, RrCombat.LocalDeaths, InCombat ? Status : RrStatus.Playing);
        }

        private void RestoreMovement()
        {
            if (_movementChanged)
            {
                _movementChanged = false;
                RrPlayer.SetMovement(1f);
            }
        }

        // ---- feedback (never on the client's camera) ----

        private void DrawFeedback(float hit, float veil)
        {
            bool show = hit > 0.01f || veil > 0.01f;
            if (!show && _screen == null && _quad == null)
            {
                return;
            }

            Color c = hit >= veil ? FlashColor : OutColor;
            c.a = Mathf.Max(hit, veil);
            bool vr = RrPlayer.InVr;
            if (!vr)
            {
                if (_screen == null && show)
                {
                    BuildScreen();
                }

                if (_screen != null)
                {
                    _screen.enabled = show;
                    _screen.color = c;
                }

                if (_quad != null)
                {
                    _quad.SetActive(false);
                }
            }
            else
            {
                if (_quad == null && show)
                {
                    BuildQuad();
                }

                if (_quad != null)
                {
                    _quad.SetActive(show);
                    _quadMaterial.SetColor("_BaseColor", c);
                }

                if (_screen != null)
                {
                    _screen.enabled = false;
                }
            }
        }

        /// <summary>Clear in the middle, opaque at the edges (the look of Rec Room's red damage flash).</summary>
        private Texture2D Vignette()
        {
            if (_vignette != null)
            {
                return _vignette;
            }

            const int n = 64;
            _vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Rr Damage Vignette", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.15f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            _vignette.SetPixels32(px);
            _vignette.Apply(false, true);
            return _vignette;
        }

        private void BuildScreen()
        {
            var go = new GameObject("Rr Health Overlay");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            var img = new GameObject("Vignette");
            img.transform.SetParent(go.transform, false);
            _screen = img.AddComponent<RawImage>();
            _screen.texture = Vignette();
            _screen.raycastTarget = false;
            var rt = _screen.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void BuildQuad()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                RrLog.WarnOnce("Health", "vignette", "no URP unlit shader: VR hit vignette off");
                return;
            }

            _quadMaterial = new Material(shader) { name = "Rr Damage Vignette" };
            _quadMaterial.SetFloat("_Surface", 1f);
            _quadMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _quadMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _quadMaterial.SetFloat("_ZWrite", 0f);
            _quadMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _quadMaterial.SetTexture("_BaseMap", Vignette());
            _quadMaterial.renderQueue = 4000;
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "Rr Health Vignette";
            Destroy(_quad.GetComponent<Collider>());
            _quad.transform.SetParent(transform, false);
            _quad.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            var mr = _quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _quadMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _quad.SetActive(false);
        }

        /// <summary>Just before rendering: the quad sits 0.3 m ahead of the eyes, facing them.</summary>
        private void PlaceQuad()
        {
            if (_quad == null || !_quad.activeSelf)
            {
                return;
            }

            var cam = RrPlayer.Camera;
            if (cam == null)
            {
                return;
            }

            var t = cam.transform;
            _quad.transform.SetPositionAndRotation(t.position + t.forward * 0.3f, t.rotation);
        }
    }
}
