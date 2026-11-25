using System;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// The Rec Room watch: the menu every player carries (rr_watch_ui.md §1, §3). One "Rr Watch" object under
    /// <see cref="RrMain.WorldRoot"/> holds the two halves:
    ///
    /// - <see cref="RrWatchModel"/>, the physical watch on the wrist (Rec Room's <c>Wrist_Watch</c> mesh with its
    ///   own material, clock hands on local time) that sits on the avatar's DRAWN wrist in VR
    ///   (<see cref="RrAvatarHand.TryGetWrist"/>, read every frame after the client's IK), never parented to the avatar,
    ///   and decides when the wrist is raised: face within 60 deg of the view, pitch at most 45 deg and roll at
    ///   most 30 deg for 0.4 s opens the menu; beyond 60 / 50 / 50 deg it closes (Rec Room's WatchGazeTracker).
    ///   With <see cref="TapToOpen"/> the other hand touching the face toggles it instead.
    /// - <see cref="RrWatchUi"/>, the menu itself: one world-space canvas in Rec Room's RRUI look (cream page over
    ///   the orange navigation bar), its pages, the show/hide/transition animations and sounds.
    ///
    /// Desktop (screen mode): key H (<see cref="RrInput.MenuDown"/>; Tab and Escape belong to the Somnium client)
    /// toggles the menu, drawn 0.8 m in front of the camera and 0.9 m wide, with the cursor freed; Backspace goes
    /// back. While the menu is open, gameplay input is blocked (<see cref="RrInput.SetBlocked"/>, owner "watch").
    /// </summary>
    public static partial class RrWatch
    {
        /// <summary>Owner name of the input block while the menu is open.</summary>
        public const string InputOwner = "watch";

        /// <summary>Setting "Tap Watch to Open Menu": VR opens by touching the face instead of looking at it.</summary>
        public static bool TapToOpen;

        /// <summary>Setting: wear the watch on the right wrist (left-handed players).</summary>
        public static bool RightWrist;

        /// <summary>Setting: the clock's second hand moves.</summary>
        public static bool ShowSeconds = true;

        /// <summary>
        /// Test switch: lay the menu out as in a headset (150-tall navigation bar, detach button, dimmed world)
        /// without one. Without a tracked hand the menu floats in front of the view, as when detached.
        /// </summary>
        public static bool ForceVrLayout;

        /// <summary>Raised when the menu opens (true) or starts to close (false).</summary>
        public static event Action<bool> OnOpenChanged;

        public static RrWatchUi Ui { get; private set; }

        public static RrWatchModel Model { get; private set; }

        private static GameObject _root;

        /// <summary>The menu is open (or opening).</summary>
        public static bool IsOpen
        {
            get { return Ui != null && Ui.IsOpen; }
        }

        /// <summary>The menu is built and can open at once.</summary>
        public static bool Ready
        {
            get { return Ui != null && Ui.Built; }
        }

        /// <summary>The menu uses its headset layout (a headset is active, or <see cref="ForceVrLayout"/>).</summary>
        public static bool VrLayout
        {
            get { return ForceVrLayout || RrPlayer.InVr; }
        }

        public static void Init()
        {
            if (_root != null)
            {
                return;
            }

            // The Settings page's saved values (volumes, look, wrist...) before the watch's parts read them.
            RrWatchSettings.Load();
            _root = new GameObject("Rr Watch");
            var parent = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            Model = _root.AddComponent<RrWatchModel>();
            Ui = _root.AddComponent<RrWatchUi>();
            _root.AddComponent<Interact.MakerPen.RrMakerPenService>(); // Maker Pen ink and events, in every place
            RrLog.Info("Watch", "started (" + (RrPlayer.InVr ? "VR: look at your left wrist" : "desktop: H opens the menu, Backspace goes back") + ")");
        }

        /// <summary>Opens the menu (on its current page); if it is still being built, as soon as it is.</summary>
        public static void Open()
        {
            if (Ui != null)
            {
                Ui.Open();
            }
        }

        public static void Close()
        {
            if (Ui != null)
            {
                Ui.Close();
            }
        }

        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>Opens the menu on a page.</summary>
        public static void Show(RrWatchPageId page)
        {
            if (Ui == null)
            {
                return;
            }

            Ui.ShowPage(page);
            Ui.Open();
        }

        internal static void RaiseOpenChanged(bool open)
        {
            if (!open)
            {
                RrWatchSettings.Flush(true);
            }

            var h = OnOpenChanged;
            if (h == null)
            {
                return;
            }

            try
            {
                h(open);
            }
            catch (Exception e)
            {
                RrLog.Error("Watch", "OnOpenChanged handler failed", e);
            }
        }

        public static void Shutdown()
        {
            RrWatchSettings.Shutdown();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }

            _root = null;
            Ui = null;
            Model = null;
            OnOpenChanged = null;
            RrInput.SetBlocked(InputOwner, false);
            TapToOpen = false;
            RightWrist = false;
            ShowSeconds = true;
            ForceVrLayout = false;
            RrWatchUi.TestPose = null;
            RrWatchModel.TestPose = null;
            RrWatchModel.TestWrist = false;
            RrWatchModel.TestGaze = false;
            Interact.MakerPen.RrMakerPen.Shutdown();
            RrWatchSounds.Shutdown();
            RrWatchFit.Shutdown();
            RrWatchWorld.Shutdown();
            RrWatchArt.Shutdown();
            RrWatchKeyboard.Reset();
            RrUiKit.Shutdown();
        }
    }
}
