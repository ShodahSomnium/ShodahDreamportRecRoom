using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// A quest weapon rack (rr_quests.md 3.11 "start-room racks, one set per player slot"; Golden Trophy's dressing
    /// room has swords, shields and bows): a wooden stand whose pegs hold Rec Room's own quest weapons, spawned from
    /// the player's install and given our behaviours (<see cref="RrWeapon"/> for the bow and crossbows from their
    /// RangedWeapon data, <see cref="RrMelee"/> for the sword and shield from their MeleeWeapon data), exactly as
    /// <see cref="RrLoadout.Give"/> does. A weapon hangs still (kinematic) until someone takes it; it falls
    /// normally once let go. A taken peg is refilled 3 s later, like Rec Room's racks. The weapons are local to each
    /// client: other players see what you hold through <see cref="RrQuestGear"/>.
    ///
    /// Two shapes: <see cref="Create"/> is a free-standing stand, <see cref="CreateOnWall"/> hangs the weapons on a
    /// board against a wall (Golden Trophy's final dressing room: "2 shields, 4 swords and 2 bows on the back wall",
    /// WIKI walkthrough). A peg list may mix weapons. While the Laser Weapons mod is on (<see cref="RrQuestMods"/>)
    /// every peg holds the weapon's Laser Tag stand-in instead.
    /// </summary>
    public sealed class RrQuestRack : MonoBehaviour
    {
        private sealed class Peg
        {
            public string Prefab;
            public Vector3 Local;
            public Quaternion Rotation;
            /// <summary>The level's own pose (<see cref="AtPoses"/>): the weapon's root goes exactly to Local / Rotation.</summary>
            public bool Exact;
            public RrInstance Current;
            public float RefillAt = -1f;

            /// <summary>The next fill is a refill in play: it pops in with Rec Room's spawn puff and sound.</summary>
            public bool Pop;
            public bool Loading;
        }

        public float RefillSeconds = 3f;
        private readonly List<Peg> _pegs = new List<Peg>();
        private readonly List<GameObject> _taken = new List<GameObject>();
        private RrQuestRoom _room;
        private bool _laser;
        private static bool _loggedRestock;

        /// <summary>Pegs on this rack.</summary>
        public int Count
        {
            get { return _pegs.Count; }
        }

        /// <summary>
        /// A rack at a local position facing -z of yaw (players stand in front), one peg per weapon name, spaced 0.6 m.
        /// </summary>
        public static RrQuestRack Create(Transform parent, Vector3 localPos, float yaw, string label, params string[] weapons)
        {
            var go = RrBuild.Group(parent, "Quest Rack", localPos, Quaternion.Euler(0f, yaw, 0f));
            var rack = go.AddComponent<RrQuestRack>();
            rack.BuildStand(weapons, label);
            return rack;
        }

        /// <summary>
        /// Weapons hung on a wall: localPos is the floor point at the wall's face, the rack faces -z of yaw (the wall
        /// is behind it, at +z), one peg per weapon name, spacing m apart (0.6 m like the stand), at 1.15 m. The board
        /// is 0.05 m thick, so nothing pokes into the wall.
        /// </summary>
        public static RrQuestRack CreateOnWall(Transform parent, Vector3 localPos, float yaw, string label, float spacing, params string[] weapons)
        {
            var go = RrBuild.Group(parent, "Quest Wall Rack", localPos, Quaternion.Euler(0f, yaw, 0f));
            var rack = go.AddComponent<RrQuestRack>();
            rack.BuildWall(weapons, label, Mathf.Max(0.35f, spacing));
            return rack;
        }

        /// <summary>
        /// Golden Trophy's final dressing-room set on one wall: 4 swords, 2 shields and 2 bows (WIKI walkthrough), in
        /// that order from left to right as seen by the players.
        /// </summary>
        public static RrQuestRack DressingRoomWall(Transform parent, Vector3 localPos, float yaw, string sword, string shield, string bow)
        {
            return CreateOnWall(parent, localPos, yaw, "WEAPONS", 0.6f, sword, sword, sword, sword, shield, shield, bow, bow);
        }

        /// <summary>
        /// Weapons at a level's own poses (Rec Room's "Default Weapons" set: on the dressing-room wall hooks, on tables,
        /// against walls): no stand is built, each peg spawns its weapon's root exactly at local[i] / rotations[i] in
        /// parent space and refills it 3 s after it is taken, like any rack. Under the Laser Weapons mod a peg's gun
        /// hangs upright by its middle at the same point (the level's pose is for the quest weapon).
        /// </summary>
        public static RrQuestRack AtPoses(Transform parent, string name, string[] prefabs, Vector3[] local, Quaternion[] rotations)
        {
            var go = RrBuild.Group(parent, name ?? "Quest Weapons");
            var rack = go.AddComponent<RrQuestRack>();
            for (int i = 0; i < prefabs.Length && i < local.Length && i < rotations.Length; i++)
            {
                rack._pegs.Add(new Peg { Prefab = prefabs[i], Local = local[i], Rotation = rotations[i], Exact = true });
            }

            return rack;
        }

        private void BuildWall(string[] weapons, string label, float spacing)
        {
            var t = transform;
            int n = Mathf.Max(1, weapons.Length);
            float width = spacing * n + 0.3f;
            var dark = RrQuestKit.WoodDark ?? RrLook.Solid(RrLook.DarkWood);
            var wood = RrQuestKit.Wood ?? RrLook.Solid(RrLook.Wood);
            RrBuild.Box(t, "Board", new Vector3(0f, 1.25f, -0.025f), new Vector3(width, 1.5f, 0.05f), dark, false);
            RrBuild.Box(t, "Rail", new Vector3(0f, 1.8f, -0.07f), new Vector3(width, 0.08f, 0.05f), wood, false);
            if (!string.IsNullOrEmpty(label))
            {
                RrBuild.Sign(t, "Label", label, new Vector3(0f, 2.25f, -0.06f), Quaternion.identity, 2.4f, new Color(1f, 0.85f, 0.4f), true, width + 0.4f);
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                float x = -spacing * 0.5f * (weapons.Length - 1) + spacing * i;
                RrBuild.Box(t, "Peg " + i, new Vector3(x, 1.75f, -0.15f), new Vector3(0.05f, 0.05f, 0.2f), dark, false);
                _pegs.Add(new Peg { Prefab = weapons[i], Local = new Vector3(x, 1.15f, -0.2f), Rotation = HangRotation(weapons[i]) });
            }
        }

        /// <summary>Replaces every weapon on the pegs and clears the taken ones (the weapon set changed: Laser Weapons).</summary>
        public void Restock()
        {
            foreach (var go in _taken)
            {
                var g = go != null ? go.GetComponent<RrGrabbable>() : null;
                if (go != null && (g == null || !g.IsHeld))
                {
                    Destroy(go);
                }
            }

            _taken.Clear();
            if (!_loggedRestock && _pegs.Count > 0)
            {
                _loggedRestock = true;
                RrLog.Info("Quest", "rack restocked: " + _pegs[0].Prefab + " -> " + WeaponFor(_pegs[0].Prefab) + " (" + _pegs.Count + " pegs)");
            }

            foreach (var p in _pegs)
            {
                if (p.Current != null)
                {
                    Destroy(p.Current.gameObject);
                    p.Current = null;
                }

                p.RefillAt = -1f;
                if (!p.Loading)
                {
                    Fill(p);
                }
            }
        }

        /// <summary>
        /// How a weapon hangs, in the rack's frame (players look along +z). Rec Room tools aim along their +z with
        /// +y up. A sword or crossbow stands upright, point / nose up (-90 about x); a shield shows its face (180
        /// about y); a longbow turns 90 about y so its limbs stay vertical and the arc shows side-on (upright like a
        /// sword it was seen edge-on, limbs pointing at the viewer: harness shot gtr_wallrack); a Laser Tag gun
        /// stands barrel-up with its side to the viewer (90 about y, then 90 about z), so guns 0.6 m apart do not
        /// overlap as they did lying flat.
        /// </summary>
        public static Quaternion HangRotation(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
            {
                return Quaternion.identity;
            }

            if (prefab.Contains("Shield"))
            {
                return Quaternion.Euler(0f, 180f, 0f);
            }

            if (prefab.Contains("Arena_") || prefab.Contains("Gun") || prefab.Contains("Pistol"))
            {
                return Quaternion.AngleAxis(90f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.up);
            }

            if (!prefab.Contains("Crossbow") && (prefab.Contains("Longbow") || prefab.Contains("Bow")))
            {
                return Quaternion.Euler(0f, 90f, 0f);
            }

            return Quaternion.Euler(-90f, 180f, 0f);
        }

        /// <summary>The weapon a peg holds under the current mods.</summary>
        private string WeaponFor(string prefab)
        {
            var room = Room();
            return room != null && room.Mods != null ? room.Mods.RackWeapon(prefab) : prefab;
        }

        private bool WeaponsSwapped()
        {
            var room = Room();
            return room != null && room.Mods != null && room.Mods.Has(RrQuestMods.LaserWeapons);
        }

        private RrQuestRoom Room()
        {
            if (_room == null)
            {
                _room = GetComponentInParent<RrQuestRoom>();
            }

            return _room;
        }

        private void BuildStand(string[] weapons, string label)
        {
            var t = transform;
            int n = Mathf.Max(1, weapons.Length);
            float width = 0.6f * n + 0.4f;
            var wood = RrQuestKit.Wood ?? RrLook.Solid(RrLook.Wood);
            var dark = RrQuestKit.WoodDark ?? RrLook.Solid(RrLook.DarkWood);
            RrBuild.Box(t, "Post L", new Vector3(-width * 0.5f, 1.05f, 0.1f), new Vector3(0.12f, 2.1f, 0.12f), dark);
            RrBuild.Box(t, "Post R", new Vector3(width * 0.5f, 1.05f, 0.1f), new Vector3(0.12f, 2.1f, 0.12f), dark);
            RrBuild.Box(t, "Bar Top", new Vector3(0f, 1.95f, 0.1f), new Vector3(width + 0.2f, 0.12f, 0.12f), wood);
            RrBuild.Box(t, "Bar Low", new Vector3(0f, 0.35f, 0.1f), new Vector3(width, 0.1f, 0.3f), wood);
            RrBuild.Box(t, "Back", new Vector3(0f, 1.15f, 0.2f), new Vector3(width, 1.6f, 0.05f), dark, false);
            if (!string.IsNullOrEmpty(label))
            {
                RrBuild.Sign(t, "Label", label, new Vector3(0f, 2.3f, 0.05f), Quaternion.identity, 2.4f, new Color(1f, 0.85f, 0.4f), true, width + 0.6f);
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                float x = -0.3f * (weapons.Length - 1) + 0.6f * i;
                RrBuild.Box(t, "Peg " + i, new Vector3(x, 1.75f, -0.02f), new Vector3(0.05f, 0.05f, 0.22f), dark, false);
                // Hang points in front of the back board; the weapon is laid upright, grip down.
                _pegs.Add(new Peg { Prefab = weapons[i], Local = new Vector3(x, 1.15f, -0.12f), Rotation = HangRotation(weapons[i]) });
            }
        }

        private void Start()
        {
            _laser = WeaponsSwapped();
            foreach (var p in _pegs)
            {
                Fill(p);
            }
        }

        /// <summary>Removes every weapon taken from this rack and refills the pegs (a new run).</summary>
        public void ResetRack()
        {
            foreach (var go in _taken)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _taken.Clear();
            foreach (var p in _pegs)
            {
                if (p.Current == null && !p.Loading)
                {
                    Fill(p);
                }
            }
        }

        private void Update()
        {
            // The Laser Weapons mod flipped (in the lobby): every peg gets the other weapon set.
            bool laser = WeaponsSwapped();
            if (laser != _laser)
            {
                _laser = laser;
                Restock();
                return;
            }

            for (int i = 0; i < _pegs.Count; i++)
            {
                var p = _pegs[i];
                if (p.Current != null)
                {
                    var g = p.Current.GetComponent<RrGrabbable>();
                    if ((g != null && g.IsHeld) || (p.Current.transform.position - transform.TransformPoint(p.Local)).sqrMagnitude > 0.25f)
                    {
                        Taken(p);
                    }
                }
                else if (!p.Loading && p.RefillAt > 0f && Time.time >= p.RefillAt)
                {
                    p.RefillAt = -1f;
                    p.Pop = true;
                    Fill(p);
                }
            }
        }

        private void Taken(Peg p)
        {
            var inst = p.Current;
            p.Current = null;
            p.RefillAt = Time.time + RefillSeconds;
            _taken.Add(inst.gameObject);
            var g = inst.GetComponent<RrGrabbable>();
            if (g != null)
            {
                g.OnReleased += (hand, velocity) => StartCoroutine(Loosen(inst));
            }
            else
            {
                StartCoroutine(Loosen(inst));
            }

            // Old weapons lying around are capped at 3 per peg (the oldest goes).
            if (_taken.Count > _pegs.Count * 3)
            {
                var old = _taken[0];
                _taken.RemoveAt(0);
                var og = old != null ? old.GetComponent<RrGrabbable>() : null;
                if (old != null && (og == null || !og.IsHeld))
                {
                    Destroy(old);
                }
            }
        }

        /// <summary>Once released, the weapon falls like any prop (the grab system restores the kinematic flag it saw).</summary>
        private IEnumerator Loosen(RrInstance inst)
        {
            yield return null;
            if (inst == null)
            {
                yield break;
            }

            var g = inst.GetComponent<RrGrabbable>();
            if (g != null && g.IsHeld)
            {
                yield break;
            }

            var rb = inst.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        private void Fill(Peg p)
        {
            p.Loading = true;
            string prefab = WeaponFor(p.Prefab);
            bool exact = p.Exact && prefab == p.Prefab;
            Quaternion rotation = exact ? p.Rotation : HangRotation(prefab);
            RrSpawn.Spawn(prefab, transform, p.Local, rotation, inst =>
            {
                p.Loading = false;
                if (inst == null)
                {
                    RrLog.WarnOnce("Quest", "rack:" + prefab, "could not spawn " + prefab + " for a rack");
                    return;
                }

                if (this == null)
                {
                    Destroy(inst.gameObject);
                    return;
                }

                // The set changed while it loaded (Laser Weapons flipped): drop it and hang the right one.
                if (WeaponFor(p.Prefab) != prefab)
                {
                    Destroy(inst.gameObject);
                    Fill(p);
                    return;
                }

                Arm(inst, prefab);
                var rb = inst.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = inst.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 1f;
                }

                rb.isKinematic = true;
                // Hang it by its middle on the peg (a level pose is the root's own: it stays exactly there).
                var b = inst.WorldBounds();
                if (!exact && b.size.sqrMagnitude > 1e-4f)
                {
                    inst.transform.position += transform.TransformPoint(p.Local) - b.center;
                }

                p.Current = inst;
                if (p.Pop)
                {
                    // Rec Room's pop-in: the [VFX_ObjectSpawn] puff and one of its four prop-spawn sounds (RrAnimateInOut).
                    p.Pop = false;
                    Vector3 at = inst.transform.position;
                    RrFx.Spawn("[VFX_ObjectSpawn]", at, Quaternion.identity);
                    RrSound.Play(Interact.RrAnimateInOut.SpawnSounds[Random.Range(0, Interact.RrAnimateInOut.SpawnSounds.Length)], at, 0.7f);
                }
            }, RrSpawnOptions.Prop());
        }

        /// <summary>Gives a spawned quest weapon its behaviour (RangedWeapon data: gun/bow; MeleeWeapon: blade/shield).</summary>
        public static void Arm(RrInstance inst, string prefab)
        {
            string cls;
            if (RrWeaponSpec.RangedConfig(inst.Data, out cls) != null)
            {
                RrWeapon.Attach(inst, prefab);
            }
            else if (inst.Data != null && inst.Data.Config("MeleeWeapon") != null)
            {
                RrMelee.Attach(inst, prefab);
            }
            else if (inst.GetComponent<RrGrabbable>() == null)
            {
                RrTools.MakeTool(inst);
            }
        }
    }
}
