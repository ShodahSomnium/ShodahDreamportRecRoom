using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using B = SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.RrPaintballBuilder;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// A weapon pickup where Rec Room's level has one (a weapon_pickup marker: the pickup spawner's own pose, the
    /// gun lying on its crate, shelf or table). The Rec Room prefab lies there as a visual; taking it swaps the
    /// tool in your hand for it (Rec Room hands you the pistol at spawn; everything else is picked up in the field).
    /// Take it with a click or the trigger ray (desktop, VR), a VR poke, or by closing a hand on it (VR grip).
    ///
    /// Field pickups are shared: the taker sends <see cref="RrPaintballRoom.EvRack"/> (map, spot) and every client
    /// in the instance hides that pickup until it comes back 15 s later, with Rec Room's [VFX_ObjectSpawn] puff.
    /// Lobby practice pickups (the "Try 'em out!" wall, the shooting gallery) never run out. Grenades and paint mines
    /// become <see cref="RrPaintballThrowable"/>s in the hand.
    /// </summary>
    public sealed class RrPaintballRack : MonoBehaviour
    {
        public const float RespawnSeconds = 15f;

        public int Index = -1;
        public int MapIndex = -1;
        public string Prefab;
        public bool Networked;
        public Transform Gun;
        public float AvailableAt;
        private RrPaintballRoom _room;
        private BoxCollider _click;
        private bool _shown = true;

        public bool Available
        {
            get { return Time.time >= AvailableAt; }
        }

        /// <summary>
        /// A pickup at a room-local (parent-local) pose: the prefab lies exactly as the level placed it. With a
        /// wallSize the level already shows the tool (the lobby's test wall): only a press volume of that size is made.
        /// </summary>
        internal static RrPaintballRack CreateSpot(RrPaintballRoom room, B b, Transform parent, Vector3 localPos, Quaternion localRot, string prefab, bool networked,
                                                   Vector3? wallSize = null)
        {
            var g = RrBuild.Group(parent, "Pickup " + RrPaintballRoom.GunName(prefab), localPos, localRot);
            var rack = g.AddComponent<RrPaintballRack>();
            rack._room = room;
            rack.Prefab = prefab;
            rack.Networked = networked;
            if (!networked && room != null)
            {
                room.RegisterLobbyRack(rack);
            }

            var click = new GameObject("Click");
            click.transform.SetParent(g.transform, false);
            rack._click = click.AddComponent<BoxCollider>();
            rack._click.size = new Vector3(0.5f, 0.35f, 0.7f);
            RrPressable.Make(click, () => rack.Take("press", RrHandSide.Right));
            if (wallSize.HasValue)
            {
                rack._click.size = wallSize.Value + new Vector3(0.06f, 0.06f, 0.06f);
                return rack;
            }

            // Purely visual (no physics, no scripts): the gun, grenade or mine as the level showed it.
            b.Prop(prefab, g.transform, Vector3.zero, 0f, 1f, false, RrSpawnOptions.Visual(), inst =>
            {
                rack.Gun = inst.transform;
                inst.transform.localRotation = Quaternion.identity;
                // The click box follows the prop's own bounds (a sniper is 1.3 m long, a grenade 10 cm).
                var bounds = inst.WorldBounds();
                if (bounds.size.sqrMagnitude > 1e-4f)
                {
                    click.transform.position = bounds.center;
                    click.transform.rotation = Quaternion.identity;
                    rack._click.size = new Vector3(Mathf.Max(0.3f, bounds.size.x + 0.12f), Mathf.Max(0.3f, bounds.size.y + 0.12f), Mathf.Max(0.3f, bounds.size.z + 0.12f));
                }

                rack.Show(rack.Available, false);
            });
            return rack;
        }

        /// <summary>Local player takes the tool (button press, VR grip or a harness call).</summary>
        public bool Take(string how, RrHandSide side)
        {
            if (!Available || _room == null || !_room.CanTakeGuns(Networked))
            {
                return false;
            }

            RrLoadout.Clear();
            bool mine = Prefab == RrPaintballRoom.Mine;
            bool throwable = mine || Prefab == RrPaintballRoom.Grenade;
            RrLoadout.Give(Prefab, side, held =>
            {
                if (throwable && held != null)
                {
                    RrPaintballThrowable.Attach(held, mine);
                }
            });
            RrSound.PlayUi(Prefab == RrPaintballRoom.Shield ? "paintballShield_Pickup" : Prefab == RrPaintballRoom.Thrower ? "sfx_paintball_paintthrower_pickup01" :
                           Prefab == RrPaintballRoom.Pistol ? "paintballPistol_pickUp_03" : "paintballShotgun_Pickup_02", 0.6f);
            RrLog.Info("Paintball", "took the " + RrPaintballRoom.GunName(Prefab) + " from " + (Networked ? "pickup " + Index : "the practice wall") + " (" + how + ")");
            if (Networked)
            {
                Hide();
                RrNet.Send(RrPaintballRoom.EvRack, new[] { (byte)Mathf.Max(0, MapIndex), (byte)Index });
            }

            return true;
        }

        /// <summary>Every client: the tool was taken, back in 15 s.</summary>
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
                if (on && puff && RrPlayer.FeetPosition != Vector3.zero && Vector3.Distance(RrPlayer.FeetPosition, Gun.position) < 40f)
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

            if (!_shown || Gun == null || !RrPlayer.InVr)
            {
                return;
            }

            // VR: closing a hand on the tool takes it.
            TryGrip(RrHand.Right, RrHandSide.Right);
            TryGrip(RrHand.Left, RrHandSide.Left);
        }

        private void TryGrip(RrHand hand, RrHandSide side)
        {
            if (hand != null && hand.GripDown && hand.Grip != null && Vector3.Distance(hand.Grip.position, _click != null ? _click.transform.position : Gun.position) < 0.4f)
            {
                Take("grip", side);
            }
        }
    }
}
