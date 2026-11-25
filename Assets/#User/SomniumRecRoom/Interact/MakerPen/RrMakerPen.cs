using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// Rec Room's Maker Pen, handed out from the watch (Home, "Maker Pen"). The pen is the install's own tool,
    /// container <c>(MakerPen)</c> (MakerPen_fbx with MakerPen_mat, <c>ExtrusionPoint</c> at pen-local
    /// (0, 0.036, 0.152), Tool with LockOnPickup), spawned under the current room so it goes with the room, made
    /// holdable by <see cref="RrTools.MakeTool(RrInstance)"/> and driven by <see cref="RrMakerPenTool"/>. Its laser
    /// pointer, its tool menu and its moveable palette child (a second tool) are removed: our own palette
    /// (<see cref="RrMakerPenPalette"/>) replaces them. The pen is personal: nobody else can pick it up (the others
    /// see a visual copy, <see cref="RrMakerPenService"/>), and when let go it hovers where it was released.
    /// </summary>
    public static class RrMakerPen
    {
        public const string Prefab = "(MakerPen)";
        public const string PaletteChildPrefix = "MakerPenPalette";
        public const string SpawnClipPrefix = "sfx_ui_prop_spawn_0";

        private static RrMakerPenTool _tool;
        private static bool _pending;

        /// <summary>The local player's pen, or null.</summary>
        public static RrMakerPenTool Tool
        {
            get { return _tool; }
        }

        /// <summary>The pen is out (or on its way).</summary>
        public static bool IsOut
        {
            get { return _tool != null || _pending; }
        }

        public static int Given { get; private set; }

        /// <summary>The watch button: gives the pen, or puts it away when it is out.</summary>
        public static void Toggle()
        {
            if (IsOut)
            {
                PutAway();
            }
            else
            {
                Give();
            }
        }

        /// <summary>Spawns the pen in front of the player and puts it in the right hand (VR) or the screen hand.</summary>
        public static void Give(Action<RrMakerPenTool> done = null)
        {
            if (_pending)
            {
                return;
            }

            if (_tool != null)
            {
                Hold(_tool);
                if (done != null)
                {
                    done(_tool);
                }

                return;
            }

            var room = RrTravel.CurrentRoom;
            if (room == null)
            {
                RrLog.WarnOnce("MakerPen", "no-room", "no Maker Pen while travelling");
                if (done != null)
                {
                    done(null);
                }

                return;
            }

            _pending = true;
            var cam = RrPlayer.Camera;
            Vector3 pos = cam != null ? cam.transform.position + cam.transform.forward * 0.4f : RrPlayer.HeadPosition;
            RrSpawn.Spawn(Prefab, room.transform, room.transform.InverseTransformPoint(pos), Quaternion.identity, inst =>
            {
                _pending = false;
                if (inst == null)
                {
                    RrLog.Warn("MakerPen", "the Maker Pen " + Prefab + " did not load");
                    if (done != null)
                    {
                        done(null);
                    }

                    return;
                }

                if (room == null || RrTravel.CurrentRoom != room)
                {
                    Object.Destroy(inst.gameObject);
                    if (done != null)
                    {
                        done(null);
                    }

                    return;
                }

                _tool = Setup(inst, room);
                Given++;
                Hold(_tool);
                RrSound.Play(SpawnClipPrefix + UnityEngine.Random.Range(1, 5), inst.transform.position, 0.5f);
                RrLog.Info("MakerPen", "Maker Pen out in " + room.name + ": tip " + (_tool.Tip != null ? "ExtrusionPoint" : "fallback") +
                                       ", lock on pickup " + _tool.Grab.LockOnPickup + (RrPlayer.InVr ? " (VR)" : " (screen)"));
                if (done != null)
                {
                    done(_tool);
                }
            }, RrSpawnOptions.Prop());
        }

        /// <summary>Puts the local pen away (it disappears with Rec Room's delete sound).</summary>
        public static void PutAway()
        {
            if (_tool != null)
            {
                _tool.PutAway(true);
            }
        }

        private static void Hold(RrMakerPenTool tool)
        {
            if (tool == null || tool.Grab == null || tool.Grab.IsHeld)
            {
                return;
            }

            var hand = RrHand.Right != null ? RrHand.Right : RrHand.Left;
            if (hand == null)
            {
                RrLog.WarnOnce("MakerPen", "no-hand", "no hand to hold the Maker Pen: it hovers in front of you");
                return;
            }

            RrGrab.Give(tool.Grab, hand);
        }

        private static RrMakerPenTool Setup(RrInstance inst, RrRoom room)
        {
            StripExtras(inst);
            var rb = inst.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = inst.gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
            }

            rb.isKinematic = true; // let go, it hovers (the grab restores this on release)
            var grab = RrTools.MakeTool(inst);
            if (grab == null)
            {
                grab = inst.gameObject.AddComponent<RrGrabbable>();
            }

            var respawn = inst.GetComponent<RrRespawnable>();
            if (respawn != null)
            {
                Object.Destroy(respawn); // a personal pen has no home to go back to
            }

            grab.CanGrab = true;
            var tool = inst.gameObject.AddComponent<RrMakerPenTool>();
            tool.Grab = grab;
            tool.Tip = inst.Find("ExtrusionPoint");
            tool.Room = room;
            return tool;
        }

        /// <summary>Removes the laser pointer, the tool menu, the palette child tool and the holographic overlay material.</summary>
        public static void StripExtras(RrInstance inst)
        {
            if (inst == null)
            {
                return;
            }

            foreach (var line in inst.GetComponentsInChildren<LineRenderer>(true))
            {
                line.enabled = false;
            }

            var menu = inst.Find("ToolMenu");
            if (menu != null)
            {
                menu.gameObject.SetActive(false);
            }

            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t != inst.transform && t.name.StartsWith(PaletteChildPrefix, StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(t.gameObject);
                    break;
                }
            }

            var keep = new List<Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (mats.Length < 2)
                {
                    continue;
                }

                keep.Clear();
                foreach (var m in mats)
                {
                    if (m == null || m.name.IndexOf("Holographic", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        keep.Add(m);
                    }
                }

                if (keep.Count > 0 && keep.Count < mats.Length)
                {
                    r.sharedMaterials = keep.ToArray();
                }
            }
        }

        internal static void Forget(RrMakerPenTool tool)
        {
            if (_tool == tool)
            {
                _tool = null;
            }
        }

        /// <summary>Clears the statics (world shutdown; the pen itself goes with its room).</summary>
        public static void Shutdown()
        {
            _tool = null;
            _pending = false;
            Given = 0;
            RrMakerPenBuild.Shutdown();
            RrMakerPenArt.Shutdown();
            RrMakerPenCatalog.Shutdown();
        }
    }
}
