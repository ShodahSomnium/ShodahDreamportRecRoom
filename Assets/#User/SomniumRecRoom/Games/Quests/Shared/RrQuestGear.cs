using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// Shows what the other members of the quest hold. Quest weapons are local tools on each client, so the local
    /// player's held weapons are published in the presence slot's hand items (<see cref="RrNet.SetHand"/>: index + 1
    /// into <see cref="RrQuestDef.Weapons"/>, 0 = empty), and every client draws a visual-only copy of each remote
    /// member's weapons at their hands (VR players: the Bridge hand; desktop players, who have no tracked hands: at
    /// the right hip-front of their body, where Rec Room's screen players hold tools). Checked 5 times a second.
    /// Their shots, hits and parries already travel as combat events; this is only the picture.
    /// </summary>
    public sealed class RrQuestGear
    {
        private sealed class Shown
        {
            public int Item = -1;
            public GameObject Visual;
            public bool Loading;
        }

        private readonly RrQuestRoom _room;
        private readonly Dictionary<long, Shown> _shown = new Dictionary<long, Shown>();
        private readonly List<long> _drop = new List<long>();
        private int _myLeft = -1, _myRight = -1;
        private float _nextPublish;

        public RrQuestGear(RrQuestRoom room)
        {
            _room = room;
        }

        public void Clear()
        {
            foreach (var kv in _shown)
            {
                if (kv.Value.Visual != null)
                {
                    Object.Destroy(kv.Value.Visual);
                }
            }

            _shown.Clear();
            if (_myLeft > 0 || _myRight > 0)
            {
                RrNet.SetHand(RrHandSide.Left, 0, 0);
                RrNet.SetHand(RrHandSide.Right, 0, 0);
            }

            _myLeft = _myRight = -1;
        }

        public void Step()
        {
            if (Time.time >= _nextPublish)
            {
                _nextPublish = Time.time + 0.2f;
                Publish();
            }

            if (!RrNet.Online)
            {
                return;
            }

            _drop.Clear();
            foreach (var kv in _shown)
            {
                _drop.Add(kv.Key);
            }

            int me = RrNet.LocalId;
            foreach (int id in RrNet.MembersOfMyInstance())
            {
                if (id == me)
                {
                    continue;
                }

                var rec = RrNet.PresenceRecord(id);
                var remote = RrRemotePlayers.Get(id);
                if (rec == null || remote == null || remote.Root == null)
                {
                    continue;
                }

                for (int side = 0; side < 2; side++)
                {
                    long key = ((long)id << 1) | (long)side;
                    _drop.Remove(key);
                    Show(key, RrPresenceWire.HandItem(rec, side == 0), remote, side == 0);
                }
            }

            foreach (var key in _drop)
            {
                Shown s;
                if (_shown.TryGetValue(key, out s) && s.Visual != null)
                {
                    Object.Destroy(s.Visual);
                }

                _shown.Remove(key);
            }
        }

        private void Publish()
        {
            int left = ItemOf(RrHand.Left != null ? RrHand.Left.Held : null);
            int right = ItemOf(RrHand.Right != null ? RrHand.Right.Held : null);
            if (right == 0)
            {
                // Screen mode: the loadout's screen hand, or any of our weapons flagged as screen-held.
                foreach (var m in RrMelee.All)
                {
                    if (m != null && m.ScreenHeld)
                    {
                        right = Index(m.PrefabName);
                    }
                }

                foreach (var w in RrWeapon.All)
                {
                    if (w != null && w.ScreenHeld)
                    {
                        right = Index(w.PrefabName);
                    }
                }
            }

            if (left != _myLeft)
            {
                _myLeft = left;
                RrNet.SetHand(RrHandSide.Left, left, 0);
            }

            if (right != _myRight)
            {
                _myRight = right;
                RrNet.SetHand(RrHandSide.Right, right, 0);
            }
        }

        private int ItemOf(Object held)
        {
            var c = held as Component;
            if (c == null)
            {
                return 0;
            }

            var inst = c.GetComponentInParent<RrInstance>();
            return inst != null ? Index(inst.PrefabName) : 0;
        }

        private int Index(string prefab)
        {
            var def = _room.Def;
            if (def == null || string.IsNullOrEmpty(prefab))
            {
                return 0;
            }

            int i = def.Weapons.IndexOf(prefab);
            return i >= 0 ? i + 1 : 0;
        }

        private void Show(long key, int item, RrRemotePlayers.Remote remote, bool left)
        {
            Shown s;
            if (!_shown.TryGetValue(key, out s))
            {
                s = new Shown();
                _shown.Add(key, s);
            }

            var def = _room.Def;
            if (item != s.Item)
            {
                s.Item = item;
                if (s.Visual != null)
                {
                    Object.Destroy(s.Visual);
                    s.Visual = null;
                }

                if (item > 0 && def != null && item <= def.Weapons.Count)
                {
                    s.Loading = true;
                    int want = item;
                    RrSpawn.Spawn(def.Weapons[item - 1], _room.transform, Vector3.zero, Quaternion.identity, inst =>
                    {
                        s.Loading = false;
                        if (inst == null)
                        {
                            return;
                        }

                        if (s.Item != want || _room == null)
                        {
                            Object.Destroy(inst.gameObject);
                            return;
                        }

                        s.Visual = inst.gameObject;
                    }, RrSpawnOptions.Visual());
                }
            }

            if (s.Visual == null)
            {
                return;
            }

            Transform hand = left ? remote.LeftHand : remote.RightHand;
            if (hand != null)
            {
                // The remote Bridge hand is their controller's OpenXR grip (hands_frames.md), so the weapon sits as a
                // local held tool does: grip * the drawn Rec Room hand frame * the tool's own Rec Room hold pose.
                var side = left ? RrHandSide.Left : RrHandSide.Right;
                Pose frame = RrAvatarHand.GripToRecRoomDefault(side);
                Vector3 gp;
                Quaternion gr;
                if (!Interact.RrTools.TryHoldPose(s.Visual.GetComponent<RrInstance>(), side, out gp, out gr))
                {
                    gp = Vector3.zero;
                    gr = Quaternion.identity;
                }

                Quaternion handRot = hand.rotation * frame.rotation;
                Vector3 handPos = hand.position + hand.rotation * frame.position;
                s.Visual.transform.SetPositionAndRotation(handPos + handRot * Vector3.Scale(s.Visual.transform.localScale, gp), handRot * gr);
            }
            else
            {
                var root = remote.Root;
                var rot = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
                Vector3 offset = new Vector3(left ? -0.3f : 0.3f, 1.05f, 0.35f);
                s.Visual.transform.SetPositionAndRotation(root.position + rot * offset, rot);
            }
        }
    }
}
