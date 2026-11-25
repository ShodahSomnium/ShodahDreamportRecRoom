using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag
{
    /// <summary>
    /// A laser weapon where Rec Room's level lays one (a weapon_pickup marker of the "Weapons" set: the pickup
    /// spawner's pose, the gun on its crate or shelf), or the map's power weapon. The install's prefab lies there as a
    /// visual; taking it swaps the tool in your hand for it (you spawn with the laser pistol, everything else is picked
    /// up in the arena). Take it with a click or the trigger ray (desktop, VR), a poke, or by closing a hand on it.
    ///
    /// Shared like Paintball's field pickups: the taker sends <see cref="RrLaserTagRoom.EvPickup"/> (map, spot) and
    /// every client hides that pickup until it comes back (15 s; the power weapon 60 s, GUESS) with Rec Room's
    /// [VFX_ObjectSpawn] puff. A grenade becomes an <see cref="RrLaserTagGrenade"/> in the hand.
    /// </summary>
    public sealed class RrLaserTagPickup : MonoBehaviour
    {
        public int Index = -1;
        public int MapIndex = -1;
        public string Prefab;
        public float RespawnSeconds = 15f;
        public float AvailableAt;
        public Transform Gun;
        private RrLaserTagRoom _room;
        private BoxCollider _click;
        private bool _shown = true;

        public bool Available
        {
            get { return Time.time >= AvailableAt; }
        }

        /// <summary>A pickup at a parent-local pose: the prefab lies exactly as the level placed it.</summary>
        public static RrLaserTagPickup Create(RrLaserTagRoom room, Transform parent, Pose local, string prefab, float respawnSeconds)
        {
            var g = RrBuild.Group(parent, "Pickup " + RrLaserTagRoom.GunName(prefab), local.position, local.rotation);
            var p = g.AddComponent<RrLaserTagPickup>();
            p._room = room;
            p.Prefab = prefab;
            p.RespawnSeconds = respawnSeconds;
            var click = new GameObject("Click");
            click.transform.SetParent(g.transform, false);
            p._click = click.AddComponent<BoxCollider>();
            p._click.size = new Vector3(0.5f, 0.35f, 0.7f);
            RrPressable.Make(click, () => p.Take("press", RrHandSide.Right));
            // Purely visual (no physics, no scripts): the weapon as the level showed it.
            RrSpawn.Spawn(prefab, g.transform, Vector3.zero, Quaternion.identity, inst =>
            {
                if (inst == null)
                {
                    RrLog.WarnOnce("LaserTag", "pickup:" + prefab, "pickup " + prefab + " could not be spawned; the spot is a bare press box");
                    return;
                }

                if (p == null)
                {
                    Object.Destroy(inst.gameObject);
                    return;
                }

                p.Gun = inst.transform;
                var bounds = inst.WorldBounds();
                if (bounds.size.sqrMagnitude > 1e-4f)
                {
                    click.transform.position = bounds.center;
                    click.transform.rotation = Quaternion.identity;
                    p._click.size = new Vector3(Mathf.Max(0.3f, bounds.size.x + 0.12f), Mathf.Max(0.3f, bounds.size.y + 0.12f), Mathf.Max(0.3f, bounds.size.z + 0.12f));
                }

                p.Show(p.Available, false);
            }, RrSpawnOptions.Visual());
            return p;
        }

        /// <summary>Local player takes the tool (button press, VR grip or a harness call).</summary>
        public bool Take(string how, RrHandSide side)
        {
            if (!Available || _room == null || !_room.CanTakeGuns)
            {
                return false;
            }

            RrLoadout.Clear();
            bool grenade = Prefab == RrLaserTagRoom.Grenade;
            var room = _room;
            RrLoadout.Give(Prefab, side, held =>
            {
                if (grenade && held != null)
                {
                    RrLaserTagGrenade.Attach(held, room);
                }
            });
            RrLog.Info("LaserTag", "took the " + RrLaserTagRoom.GunName(Prefab) + " from pickup " + Index + " (" + how + ")");
            Hide();
            RrNet.Send(RrLaserTagRoom.EvPickup, new[] { (byte)Mathf.Max(0, MapIndex), (byte)Index });
            return true;
        }

        /// <summary>Every client: the tool was taken, back after its respawn time.</summary>
        public void Hide()
        {
            AvailableAt = Time.time + RespawnSeconds;
            Show(false, false);
        }

        /// <summary>Back at once (a new match).</summary>
        public void Restock()
        {
            AvailableAt = 0f;
            Show(true, false);
        }

        private void Show(bool on, bool puff)
        {
            _shown = on;
            if (Gun != null)
            {
                Gun.gameObject.SetActive(on);
                if (on && puff && Vector3.Distance(RrPlayer.FeetPosition, Gun.position) < 40f)
                {
                    RrFx.Spawn("[VFX_ObjectSpawn]", Gun.position, Quaternion.identity);
                }
            }

            if (_click != null)
            {
                _click.enabled = on;
            }
        }

        private void Update()
        {
            if (!_shown && Available)
            {
                Show(true, true);
            }

            if (!_shown || !RrPlayer.InVr)
            {
                return;
            }

            // VR: closing a hand on the tool takes it.
            TryGrip(RrHand.Right, RrHandSide.Right);
            TryGrip(RrHand.Left, RrHandSide.Left);
        }

        private void TryGrip(RrHand hand, RrHandSide side)
        {
            if (hand != null && hand.GripDown && hand.Grip != null && _click != null && Vector3.Distance(hand.Grip.position, _click.transform.position) < 0.4f)
            {
                Take("grip", side);
            }
        }
    }
}
