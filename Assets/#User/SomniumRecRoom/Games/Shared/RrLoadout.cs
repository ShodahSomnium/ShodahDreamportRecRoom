using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Starting equipment: Rec Room hands every player the game's MainHandToolId / OffHandToolId at match start and
    /// resets tools on game start (ResetToolsOnGameStart, rr_shooters.md §4.1). A game names its loadout
    /// (<see cref="RrGameRoom"/>'s Loadout, or <see cref="GiveAll"/>); each prefab is spawned, turned into an
    /// <see cref="RrWeapon"/> (RangedWeapon data) or <see cref="RrMelee"/> (MeleeWeapon data), and put into the
    /// local hand with RrGrab.Give. The first item goes to the right hand; a second one to the left hand in VR only
    /// (screen mode has one hand).
    ///
    /// When the Player module has no hand to offer (RrHand.Right null: editor harness, a client without our hands),
    /// the item is held by our own "Rr Screen Hand", an object that follows the camera lower right of the view (where
    /// Rec Room screen mode holds tools); it is never a child of the client's camera.
    /// </summary>
    public static class RrLoadout
    {
        /// <summary>Items this loadout gave (destroyed by <see cref="Clear"/>).</summary>
        public static readonly List<GameObject> Given = new List<GameObject>();

        /// <summary>Screen-hand offset from the camera (metres, camera frame).</summary>
        public static Vector3 ScreenHandOffset = new Vector3(0.2f, -0.22f, 0.42f);

        private static Transform _screenHand;
        private static int _pending;

        public static Transform ScreenHand
        {
            get
            {
                if (_screenHand == null)
                {
                    var host = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                    var go = new GameObject("Rr Screen Hand");
                    go.transform.SetParent(host, false);
                    go.AddComponent<RrScreenHandFollow>();
                    _screenHand = go.transform;
                    RrLog.Info("Loadout", "no RrHand for held tools: using our own screen hand (camera-following)");
                }

                return _screenHand;
            }
        }

        public static bool Busy
        {
            get { return _pending > 0; }
        }

        /// <summary>Called by RrGameRoom on every phase change: Playing gives the loadout (once), Lobby / MatchOver take it back.</summary>
        public static void OnPhase(RrPhase now, IList<string> loadout)
        {
            if (now == RrPhase.Playing)
            {
                if (loadout != null && loadout.Count > 0 && Given.Count == 0 && _pending == 0)
                {
                    GiveAll(loadout);
                }
            }
            else if (now == RrPhase.Lobby || now == RrPhase.MatchOver)
            {
                Clear();
            }
        }

        /// <summary>Gives each prefab: the first to the right hand, the second to the left (VR).</summary>
        public static void GiveAll(IList<string> prefabs, Action done = null)
        {
            int n = prefabs.Count;
            if (!RrPlayer.InVr)
            {
                n = Mathf.Min(n, 1);
            }

            n = Mathf.Min(n, 2);
            if (n == 0)
            {
                if (done != null)
                {
                    done();
                }

                return;
            }

            int left = n;
            for (int i = 0; i < n; i++)
            {
                Give(prefabs[i], i == 0 ? RrHandSide.Right : RrHandSide.Left, c =>
                {
                    left--;
                    if (left == 0 && done != null)
                    {
                        done();
                    }
                });
            }
        }

        /// <summary>
        /// Spawns a tool and puts it in a local hand. done: the RrWeapon or RrMelee on it (or its RrGrabbable for
        /// anything else), null on failure. A gun reports only once its spec is read, so it can fire at once.
        /// </summary>
        public static void Give(string prefabName, RrHandSide side, Action<Component> done = null)
        {
            _pending++;
            var room = RrTravel.CurrentRoom;
            Transform parent = room != null ? room.transform : RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            var cam = RrPlayer.Camera;
            Vector3 pos = cam != null ? cam.transform.position + cam.transform.forward * 0.5f : RrPlayer.HeadPosition;
            Vector3 local = parent != null ? parent.InverseTransformPoint(pos) : pos;
            RrSpawn.Spawn(prefabName, parent, local, Quaternion.identity, inst =>
            {
                if (inst == null)
                {
                    _pending--;
                    RrLog.WarnOnce("Loadout", "spawn:" + prefabName, "could not spawn " + prefabName);
                    if (done != null)
                    {
                        done(null);
                    }

                    return;
                }

                Given.Add(inst.gameObject);
                string cls;
                if (RrWeaponSpec.RangedConfig(inst.Data, out cls) != null)
                {
                    RrWeapon.Attach(inst, prefabName, w =>
                    {
                        _pending--;
                        if (inst == null)
                        {
                            return;
                        }

                        Hold(inst, side, w);
                        if (done != null)
                        {
                            done(w != null ? (Component)w : inst.GetComponent<RrGrabbable>());
                        }
                    });
                    return;
                }

                _pending--;
                Component result;
                if (inst.Data != null && inst.Data.Config("MeleeWeapon") != null)
                {
                    result = RrMelee.Attach(inst, prefabName);
                }
                else
                {
                    var g = inst.GetComponent<RrGrabbable>();
                    result = g != null ? g : inst.gameObject.AddComponent<RrGrabbable>();
                }

                Hold(inst, side, null);
                if (done != null)
                {
                    done(result);
                }
            }, RrSpawnOptions.Prop());
        }

        private static void Hold(RrInstance inst, RrHandSide side, RrWeapon weapon)
        {
            var g = inst.GetComponent<RrGrabbable>();
            var hand = RrHand.Get(side);
            if (hand != null && g != null)
            {
                RrGrab.Give(g, hand);
                RrLog.Info("Loadout", "gave " + inst.name + " to the " + side + " hand");
                return;
            }

            // No hand: our own screen hand holds it, barrel along the view.
            var anchor = ScreenHand;
            var body = inst.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
            {
                c.isTrigger = true;
            }

            var t = inst.transform;
            t.SetParent(anchor, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            if (weapon != null && weapon.Barrel != null)
            {
                var barrelInRoot = Quaternion.Inverse(t.rotation) * weapon.Barrel.rotation;
                t.localRotation = Quaternion.Inverse(barrelInRoot);
                weapon.ScreenHeld = true;
            }

            var melee = inst.GetComponent<RrMelee>();
            if (melee != null)
            {
                melee.ScreenHeld = true;
                t.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            }

            RrLog.Info("Loadout", "screen hand holds " + inst.name);
        }

        /// <summary>Takes every given tool back (out of the hands, destroyed).</summary>
        public static void Clear()
        {
            for (int i = Given.Count - 1; i >= 0; i--)
            {
                var go = Given[i];
                if (go == null)
                {
                    continue;
                }

                var g = go.GetComponent<RrGrabbable>();
                if (g != null && g.IsHeld)
                {
                    g.ForceRelease();
                }

                UnityEngine.Object.Destroy(go);
            }

            Given.Clear();
        }

        internal static void ResetStatics()
        {
            Given.Clear();
            _screenHand = null;
            _pending = 0;
        }
    }

    /// <summary>Keeps "Rr Screen Hand" at the camera's lower right, before Update users read it and before rendering.</summary>
    internal sealed class RrScreenHandFollow : MonoBehaviour
    {
        private void OnEnable()
        {
            Application.onBeforeRender += Follow;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= Follow;
        }

        private void LateUpdate()
        {
            Follow();
        }

        private void Follow()
        {
            var cam = RrPlayer.Camera;
            if (cam == null)
            {
                return;
            }

            var c = cam.transform;
            transform.SetPositionAndRotation(c.position + c.rotation * RrLoadout.ScreenHandOffset, c.rotation);
        }
    }
}
