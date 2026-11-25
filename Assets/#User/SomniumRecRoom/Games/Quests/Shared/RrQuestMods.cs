using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// The RRO quest mods (NEWS 2025-12-02, rr_latest_rgames_story.md 5.1 / 5.3 item 9): Golden Trophy's last content
    /// update, three modifiers picked on a mod board in the lobby. Rec Room charged an "RRO Mod Ticket" per mod; ours
    /// are free toggles. The flags are one authority-written quest int (<see cref="RrQuestInts.Mods"/>), toggled only
    /// in the lobby through a request, so every client applies the same mods to the whole run.
    ///
    /// Rec Room published only the one-line descriptions; every number here is GUESS (open question 12.2.1):
    /// - Turbo, "everything is faster": enemies move, charge and shoot 1.5x faster, attack again after 0.67x the
    ///   cooldown, and waves pop in 0.67x as far apart. (The player's own walk is left alone: the downed slow-down
    ///   already owns the movement scale.)
    /// - Coconut Chaos, "Coconut Bombs drop from the sky": every 6-10 s of combat a <c>(Quest_Pirate1_CoconutBomb)</c>
    ///   (B43 key 91) falls from 12 m over each standing player and blows up 4 s after it lands (the wiki's ~4 s
    ///   fuse): 80 damage in 2 m to enemies (the Pirate grenade's numbers, rr_quests.md 2.3) and a down for a player.
    ///   Catching and throwing it back is not built (the bomb is a visual that every client drops the same way).
    /// - Laser Weapons, "Swaps all weapons in the quest for Laser Tag variants": the racks hold Laser Tag guns
    ///   instead (sword -> Arena_Shotgun, shield -> Arena_AutomaticGun, bow -> Arena_RailGun, crossbow ->
    ///   Arena_Pistol; Rec Room's own mapping is unpublished), and laser hits stun basic goblins and imps as arrows do.
    /// A win with Turbo and Coconut Chaos shows Rec Room's "Turbo Goblin" badge as a toast.
    /// </summary>
    public sealed class RrQuestMods
    {
        public const int Turbo = 1, CoconutChaos = 2, LaserWeapons = 4;

        public const float TurboSpeed = 1.5f;
        public const float TurboCooldown = 0.67f;
        public const float CoconutHeight = 12f;
        public const float CoconutFuse = 4f;
        public const float CoconutRadius = 2f;
        public const int CoconutDamage = 80;
        public const string CoconutPrefab = "(Quest_Pirate1_CoconutBomb)";

        /// <summary>The Laser Tag guns (rr_shooters.md 3.2), appended to the quest's weapons so remote players see them held.</summary>
        public static readonly string[] LaserGuns = { "(Arena_Shotgun)", "(Arena_AutomaticGun)", "(Arena_RailGun)", "(Arena_Pistol)" };

        private static readonly string[] Names = { "TURBO", "COCONUT CHAOS", "LASER WEAPONS" };
        private static readonly int[] Bits = { Turbo, CoconutChaos, LaserWeapons };

        private readonly RrQuestRoom _room;
        private readonly List<Renderer> _lamps = new List<Renderer>();
        private readonly List<int> _lampBits = new List<int>();
        private readonly byte[] _laserTools = new byte[4];
        private int _seen = -1;
        private int _asked = -1;
        private float _askUntil, _nextAsk;
        private float _nextDrop = -1f;
        private bool _loggedDrop, _loggedModel;
        private Material _on, _off;

        public RrQuestMods(RrQuestRoom room)
        {
            _room = room;
            for (int i = 0; i < LaserGuns.Length; i++)
            {
                _laserTools[i] = RrWeaponSpec.ToolIdOf(LaserGuns[i]);
            }
        }

        /// <summary>The current mod bits (every client; from the instance ints).</summary>
        public int Flags
        {
            get { return _room.QuestInt(RrQuestInts.Mods); }
        }

        public bool Has(int bit)
        {
            return (Flags & bit) != 0;
        }

        /// <summary>"TURBO + COCONUT CHAOS", or "" with no mods.</summary>
        public string Describe(int flags)
        {
            var parts = new List<string>();
            for (int i = 0; i < Bits.Length; i++)
            {
                if ((flags & Bits[i]) != 0)
                {
                    parts.Add(Names[i]);
                }
            }

            return string.Join(" + ", parts.ToArray());
        }

        /// <summary>Adds the Laser Tag guns to the quest's weapon list (same order on every client), once.</summary>
        public void AddWeapons(RrQuestDef def)
        {
            foreach (var g in LaserGuns)
            {
                if (!def.Weapons.Contains(g))
                {
                    def.Weapons.Add(g);
                }
            }
        }

        /// <summary>What a rack peg holds under the current mods (the Laser Weapons swap; anything else unchanged).</summary>
        public string RackWeapon(string prefab)
        {
            if (!Has(LaserWeapons) || string.IsNullOrEmpty(prefab))
            {
                return prefab;
            }

            // "(Crossbow)" contains "bow": test it before the longbow.
            if (prefab.Contains("Crossbow"))
            {
                return "(Arena_Pistol)";
            }

            if (prefab.Contains("Sword"))
            {
                return "(Arena_Shotgun)";
            }

            if (prefab.Contains("Shield"))
            {
                return "(Arena_AutomaticGun)";
            }

            if (prefab.Contains("bow") || prefab.Contains("Bow"))
            {
                return "(Arena_RailGun)";
            }

            return prefab;
        }

        /// <summary>Whether a hit's tool is a Laser Tag gun while Laser Weapons is on (it stuns like an arrow).</summary>
        public bool StunsLikeArrow(byte tool)
        {
            if (!Has(LaserWeapons) || tool == RrCombat.UnknownTool)
            {
                return false;
            }

            for (int i = 0; i < _laserTools.Length; i++)
            {
                if (_laserTools[i] == tool)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ requests

        /// <summary>Any member, in the lobby: asks the authority to flip a mod (repeated until the int shows it).</summary>
        public void Toggle(int bit)
        {
            if (_room.Phase != RrPhase.Lobby)
            {
                _room.Hud.Toast("Mods are picked in the lobby", 2f);
                return;
            }

            _asked = Flags ^ bit;
            _askUntil = Time.time + 3f;
            _nextAsk = 0f;
            Resend();
        }

        private void Resend()
        {
            if (_asked < 0)
            {
                return;
            }

            if (Flags == _asked || Time.time > _askUntil || _room.Phase != RrPhase.Lobby)
            {
                _asked = -1;
                return;
            }

            if (Time.time >= _nextAsk)
            {
                _nextAsk = Time.time + 0.5f;
                _room.Ask(RrQuestEvents.ReqMods, (byte)_asked);
            }
        }

        /// <summary>Authority: a member's mod request (the whole new flag set; only in the lobby).</summary>
        public void AuthoritySet(int flags)
        {
            if (!_room.IsAuthority || _room.Phase != RrPhase.Lobby)
            {
                return;
            }

            flags &= Turbo | CoconutChaos | LaserWeapons;
            if (flags != Flags)
            {
                _room.SetQuestInt(RrQuestInts.Mods, flags);
                RrLog.Info("Quest", "mods: " + (flags == 0 ? "none" : Describe(flags)));
            }
        }

        // ------------------------------------------------------------------ every client

        /// <summary>Every frame: lamps, the rack swap when Laser Weapons flips, the request resend, coconut drops.</summary>
        public void Step()
        {
            Resend();
            int f = Flags;
            if (f != _seen)
            {
                // Racks watch the Laser Weapons bit themselves (RrQuestRack.Update), registered or not.
                _seen = f;
                for (int i = 0; i < _lamps.Count; i++)
                {
                    if (_lamps[i] != null)
                    {
                        _lamps[i].sharedMaterial = (f & _lampBits[i]) != 0 ? _on : _off;
                    }
                }
            }

            if (_room.IsAuthority)
            {
                AuthorityCoconuts();
            }
        }

        /// <summary>Every client, as an enemy's body is ready: Turbo speeds it up (authority and watcher copies alike).</summary>
        public void EnemyReady(RrEnemy e)
        {
            if (e == null || !Has(Turbo))
            {
                return;
            }

            e.MoveSpeed *= TurboSpeed;
            e.ChargeSpeed *= TurboSpeed;
            e.DashSpeed *= TurboSpeed;
            e.ProjectileSpeed *= TurboSpeed;
            e.Cooldown *= TurboCooldown;
        }

        /// <summary>Every client, at the run's end: the "Turbo Goblin" badge for a Turbo + Coconut Chaos win.</summary>
        public void RunOver(bool victory)
        {
            int f = Flags;
            if (f != 0)
            {
                RrLog.Info("Quest", "run over with mods " + Describe(f) + (victory ? ": won" : ": lost"));
            }

            if (victory && (f & (Turbo | CoconutChaos)) == (Turbo | CoconutChaos))
            {
                _room.Hud.Toast("Badge: TURBO GOBLIN!", 5f);
            }
        }

        // ------------------------------------------------------------------ the board

        /// <summary>
        /// The mod board: a wooden table with three buttons (Turbo, Coconut Chaos, Laser Weapons), a lamp behind each
        /// that is lit while the mod is on, and a title. Players stand on its -z side, as at the start button.
        /// </summary>
        public Transform Board(Transform parent, Vector3 local, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var root = RrBuild.Group(parent, "Mod Board", local, rot).transform;
            var wood = RrQuestKit.Wood ?? RrLook.Solid(RrLook.Wood);
            var dark = RrQuestKit.WoodDark ?? RrLook.Solid(RrLook.DarkWood);
            // Lit lamps: a saturated green at a low emission (2.5 x a pale green read as white in the first shot).
            _on = RrLook.Emissive(new Color(0.15f, 0.8f, 0.25f), 1.1f);
            _off = RrLook.Solid(new Color(0.22f, 0.22f, 0.24f));
            RrBuild.Box(root, "Table", new Vector3(0f, 0.48f, 0f), new Vector3(2.6f, 0.96f, 0.7f), wood);
            RrBuild.Box(root, "Top", new Vector3(0f, 0.98f, 0f), new Vector3(2.7f, 0.05f, 0.8f), dark, false);
            RrBuild.Box(root, "Back", new Vector3(0f, 1.72f, 0.36f), new Vector3(2.7f, 1.44f, 0.06f), dark, false);
            RrBuild.Sign(root, "Title", "RRO MODS", new Vector3(0f, 2.22f, 0.32f), Quaternion.identity, 3f, new Color(1f, 0.85f, 0.4f), true, 3f);
            for (int i = 0; i < Bits.Length; i++)
            {
                float x = -0.9f + 0.9f * i;
                int bit = Bits[i];
                RrPressable.Create(root, "Mod " + Names[i], new Vector3(x, 1.03f, -0.05f), Quaternion.identity, new Color(0.95f, 0.65f, 0.15f), 0.3f, () => Toggle(bit));
                var lamp = RrBuild.Sphere(root, "Lamp " + Names[i], new Vector3(x, 1.25f, 0.3f), 0.08f, _off, false);
                _lamps.Add(lamp.GetComponent<Renderer>());
                _lampBits.Add(bit);
                RrBuild.Sign(root, "Label " + Names[i], Names[i], new Vector3(x, 1.6f, 0.32f), Quaternion.identity, 1.4f, Color.white, true, 0.9f);
            }

            RrBuild.Sign(root, "Help", "Press to toggle - free, for the whole run", new Vector3(0f, 1.9f, 0.32f), Quaternion.identity, 1.1f, new Color(0.9f, 0.9f, 0.9f), false, 2.6f);
            _seen = -1;
            RrLog.Info("Quest", "mod board built: Turbo, Coconut Chaos, Laser Weapons");
            return root;
        }

        // ------------------------------------------------------------------ Coconut Chaos

        private void AuthorityCoconuts()
        {
            if (!Has(CoconutChaos) || _room.Phase != RrPhase.Playing || _room.Stage != RrQuestStage.Combat)
            {
                _nextDrop = -1f;
                return;
            }

            if (_nextDrop < 0f)
            {
                _nextDrop = Time.time + Random.Range(6f, 10f);
                return;
            }

            if (Time.time < _nextDrop)
            {
                return;
            }

            _nextDrop = Time.time + Random.Range(6f, 10f);
            int n = 0;
            foreach (var at in _room.StandingPlayerFeet())
            {
                var b = new byte[12];
                int i = 0;
                RrQuestEvents.Pos(b, ref i, at + Vector3.up * CoconutHeight);
                RrNet.Send(RrQuestEvents.Coconut, b);
                n++;
            }

            if (!_loggedDrop)
            {
                _loggedDrop = true;
                RrLog.Info("Quest", "Coconut Chaos: first drop over " + n + " player(s), " + CoconutHeight + " m up, " + CoconutFuse + " s fuse after landing");
            }
        }

        /// <summary>Every client: a coconut bomb starts falling from this world point.</summary>
        public void OnCoconut(int sender, byte[] body)
        {
            if (body == null || body.Length < 12 || _room.Phase != RrPhase.Playing)
            {
                return;
            }

            int i = 0;
            Vector3 from = RrQuestEvents.Pos(body, ref i);
            var go = new GameObject("Coconut Bomb");
            go.transform.SetParent(_room.transform, false);
            go.transform.position = from;
            var c = go.AddComponent<RrQuestCoconut>();
            c.Room = _room;
            RrSpawn.Spawn(CoconutPrefab, go.transform, Vector3.zero, Quaternion.identity, inst =>
            {
                if (!_loggedModel)
                {
                    _loggedModel = true;
                    RrLog.Info("Quest", "Coconut Chaos bomb: " + (inst != null ? CoconutPrefab + " from the install" : "our brown sphere (" + CoconutPrefab + " did not load)"));
                }

                if (inst == null)
                {
                    if (go != null)
                    {
                        RrBuild.Sphere(go.transform, "Coconut", Vector3.zero, 0.15f, RrLook.Solid(new Color(0.35f, 0.22f, 0.12f)), false);
                    }

                    return;
                }

                if (go == null)
                {
                    Object.Destroy(inst.gameObject);
                }
            }, RrSpawnOptions.Visual());
        }
    }

    /// <summary>One falling coconut bomb (Coconut Chaos): falls, lands on the first surface, fizzes, blows up.</summary>
    public sealed class RrQuestCoconut : MonoBehaviour
    {
        public RrQuestRoom Room;
        private static bool _loggedBlast;
        private float _vy;
        private float _landedAt = -1f;
        private float _born;

        private void Start()
        {
            _born = Time.time;
        }

        private void Update()
        {
            if (Room == null || Room.Phase != RrPhase.Playing || Time.time - _born > 20f)
            {
                Destroy(gameObject);
                return;
            }

            if (_landedAt < 0f)
            {
                float dt = Time.deltaTime;
                _vy -= 9.81f * dt;
                float step = -_vy * dt;
                RaycastHit hit;
                int mask = 1 << RrLayers.Environment;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, step + 0.15f, mask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + Vector3.up * 0.15f;
                    _landedAt = Time.time;
                    RrSound.Play("Quest_Sword_Impactshield", transform.position, 0.5f, 0.6f);
                }
                else
                {
                    transform.position += Vector3.down * step;
                }

                return;
            }

            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
            if (Time.time - _landedAt >= RrQuestMods.CoconutFuse)
            {
                Explode();
            }
        }

        private void Explode()
        {
            Vector3 c = transform.position;
            RrImpacts.Fx("[Arena_Grenade_Explosion_VFX] JumboTron Variant", c, Quaternion.identity, 1f);
            RrSound.Play("Paintball_GrenadeExplode01", c, 0.9f, 1f);
            // Victim decides: each client downs only its own player.
            bool local = RrQuestEnemies.LocalWithin(c, RrQuestMods.CoconutRadius);
            if (local)
            {
                RrQuestEnemies.HurtLocal(RrQuestMods.CoconutDamage, c);
            }

            // The authority, which simulates the enemies, hurts every enemy in the blast.
            int hit = 0;
            if (Room.IsAuthority && Room.Enemies != null)
            {
                for (int s = 0; s < RrQuestEnemies.Slots; s++)
                {
                    var e = Room.Enemies.At(s);
                    if (e != null && e.Alive && Vector3.Distance(e.Center, c) <= RrQuestMods.CoconutRadius + e.Radius)
                    {
                        RrEnemy.ApplyRemoteHit(new RrCombat.ActorHitInfo { Actor = e.Id, Attacker = RrCombat.NoPlayer, Damage = RrQuestMods.CoconutDamage, Part = RrBodyPart.Body, Point = c, Tool = RrCombat.EnemyTool });
                        hit++;
                    }
                }
            }

            if (!_loggedBlast)
            {
                _loggedBlast = true;
                RrLog.Info("Quest", "Coconut Chaos: first blast at " + c.ToString("F1") + ": " + hit + " enemies hit, local player " + (local ? "caught" : "clear"));
            }

            Destroy(gameObject);
        }
    }
}
