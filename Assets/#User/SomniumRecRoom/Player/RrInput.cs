using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Screen-mode (desktop) and VR input folded into the few actions Rec Room uses, on keys and buttons the Somnium
    /// client does not own (conventions §11.1, somnium_player.md §4.2 and §5.4). Every field is written once per frame
    /// by <see cref="RrInputPump"/> before any other script's Update, so edges (…Down/…Up) are seen exactly once and
    /// in the frame they happen; treat the fields as read-only.
    ///
    /// Desktop map (Rec Room's own screen-mode keys where the client leaves them free):
    /// - Left mouse: use / fire / throw (<see cref="UseDown"/>). Outside play mode the cursor is free and a click on a
    ///   thing is how it gets picked up (the grab system tests <see cref="AimRay"/>, which then follows the mouse).
    ///   A click on UI (the client's tablet, our panels) is not a use.
    /// - E grab, Q tap drop / Q hold (0.33 s, Rec Room's minLongPressDelay) underhand toss, right mouse aim/secondary,
    ///   Left Ctrl crouch: <b>only in play mode</b>. Outside it Q/E rotate the client's body and right mouse orbits its
    ///   camera; in play mode <see cref="RrPlayer"/> writes the body's yaw every LateUpdate, which neutralises Q/E.
    /// - Z reload (R is the client's pop-up menu), 1-4 slots, V third person (always, unless blocked).
    /// - H opens/closes the watch menu (Tab/Escape are the client's; rr_watch_ui.md §1.2), Backspace goes back. Both
    ///   are reported even while blocked, so the menu can close itself.
    ///
    /// VR: <see cref="UseDown"/> is the right trigger and <see cref="GrabDown"/>/<see cref="GrabUp"/> the right grip
    /// (per-hand values are on <see cref="RrHand"/>). No button is free for the menu: the Watch module opens it from a
    /// wrist glance. Throwing is releasing the grip while moving.
    ///
    /// <see cref="Blocked"/>: a menu has the input - ours (<see cref="SetBlocked"/>), the client's tablet (found and
    /// read as ETS does, <see cref="TabletOpen"/>), or a focused text field (chat). While blocked every gameplay flag
    /// is false and a held use/aim is released with an Up edge.
    /// </summary>
    public static class RrInput
    {
        public const KeyCode GrabKey = KeyCode.E;
        public const KeyCode DropKey = KeyCode.Q;
        public const KeyCode MenuKey = KeyCode.H;
        public const KeyCode BackKey = KeyCode.Backspace;
        public const KeyCode ReloadKey = KeyCode.Z;
        public const KeyCode ThirdPersonKey = KeyCode.V;
        public const KeyCode CrouchKey = KeyCode.LeftControl;

        /// <summary>Rec Room's ScreenControlsMap minLongPressDelay (and maxDoublePressDelay), seconds.</summary>
        public const float LongPress = 0.33f;

        public const string DesktopHelp = "Left click: use / throw (click a thing to pick it up)\n" +
                                          "E: grab   Q: drop (hold: toss)   Right click: aim\n" +
                                          "Z: reload   1-4: slots   Ctrl: crouch   V: third person\n" +
                                          "H: watch menu";

        public const string VrHelp = "Grip: grab and hold   Trigger: use\n" +
                                     "Let go while moving to throw\n" +
                                     "Look at your left wrist: watch menu";

        /// <summary>The ray the player points with: camera centre (play mode, locked or hidden cursor), the mouse (free cursor), or the right hand's aim in VR.</summary>
        public static Ray AimRay
        {
            get
            {
                Camera c = RrPlayer.Camera;
                if (RrPlayer.InVr)
                {
                    RrHand right = RrHand.Right;
                    if (right != null && right.Tracked)
                    {
                        return right.AimRay;
                    }

                    return c != null ? new Ray(c.transform.position, c.transform.forward) : new Ray(Vector3.zero, Vector3.forward);
                }

                if (c == null)
                {
                    return new Ray(Vector3.zero, Vector3.forward);
                }

                if (Cursor.lockState == CursorLockMode.None && Cursor.visible && !RrPlayer.InPlayMode)
                {
                    Vector3 mouse = Input.mousePosition;
                    if (mouse.x >= 0f && mouse.y >= 0f && mouse.x <= Screen.width && mouse.y <= Screen.height)
                    {
                        return c.ScreenPointToRay(mouse);
                    }
                }

                return c.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
        }

        /// <summary>Use / fire / drink (desktop: left mouse; VR: right trigger).</summary>
        public static bool UseDown, UseHeld, UseUp;

        /// <summary>Grab (desktop: E in play mode), grip (VR, right hand).</summary>
        public static bool GrabDown, GrabUp;

        /// <summary>Underhand toss of what is held (desktop: Q held 0.33 s); VR throws by releasing the grip while moving.</summary>
        public static bool ThrowDown;

        /// <summary>Drop what is held (desktop: Q tapped, released before 0.33 s).</summary>
        public static bool DropDown;

        /// <summary>Secondary action / aim down sights (desktop: right mouse, play mode only).</summary>
        public static bool AltDown, AltHeld, AltUp;

        /// <summary>Reload (desktop: Z).</summary>
        public static bool ReloadDown;

        /// <summary>Holster slot pressed this frame: 1-4, 0 for none (desktop digits).</summary>
        public static int SlotDown;

        /// <summary>Crouch / slide held (desktop: Left Ctrl, play mode only).</summary>
        public static bool CrouchHeld;

        /// <summary>Third-person toggle pressed (desktop: V); <see cref="RrPlayer"/> acts on it.</summary>
        public static bool ThirdPersonDown;

        /// <summary>Open or close the watch menu (desktop: H; reported while blocked).</summary>
        public static bool MenuDown;

        /// <summary>Back in a menu (desktop: Backspace; reported while blocked).</summary>
        public static bool BackDown;

        private static bool _manualBlock;
        private static readonly List<string> Owners = new List<string>();

        /// <summary>
        /// True while a menu (ours or the client's tablet) or a text field has the input: gameplay ignores the rest.
        /// Setting it is the anonymous owner "external"; prefer <see cref="SetBlocked"/> with a name.
        /// </summary>
        public static bool Blocked
        {
            get { return _manualBlock || Owners.Count > 0 || TabletOpen || ChatFocused; }
            set { _manualBlock = value; }
        }

        /// <summary>The client's tablet is open (VR and desktop), when it was found.</summary>
        public static bool TabletOpen
        {
            get { return RrClientTablet.IsOpen; }
        }

        /// <summary>A text field (the client's chat, our inputs) has keyboard focus.</summary>
        public static bool ChatFocused { get; private set; }

        /// <summary>Desktop, free cursor: the mouse is over UI (EventSystem), so a click belongs to that UI.</summary>
        public static bool PointerOverUi { get; private set; }

        /// <summary>Who blocks the input right now, for logs and the heartbeat ("" when nobody).</summary>
        public static string BlockedBy
        {
            get
            {
                if (!Blocked)
                {
                    return "";
                }

                string s = TabletOpen ? "tablet" : "";
                if (ChatFocused)
                {
                    s += (s.Length > 0 ? "+" : "") + "text field";
                }

                if (_manualBlock)
                {
                    s += (s.Length > 0 ? "+" : "") + "external";
                }

                for (int i = 0; i < Owners.Count; i++)
                {
                    s += (s.Length > 0 ? "+" : "") + Owners[i];
                }

                return s;
            }
        }

        /// <summary>Blocks gameplay input while <paramref name="owner"/> (e.g. "watch") has it; several owners may overlap.</summary>
        public static void SetBlocked(string owner, bool on)
        {
            if (string.IsNullOrEmpty(owner))
            {
                owner = "external";
            }

            int i = Owners.IndexOf(owner);
            if (on && i < 0)
            {
                Owners.Add(owner);
            }
            else if (!on && i >= 0)
            {
                Owners.RemoveAt(i);
            }
        }

        public static bool IsBlockedBy(string owner)
        {
            return Owners.Contains(owner);
        }

        private static int _polledFrame = -1;
        private static bool _useArmed;
        private static bool _altArmed;
        private static bool _grabArmed;
        private static bool _qHeld;
        private static bool _qFired;
        private static float _qDownAt;
        private static bool _swallowClick;
        private static bool _wasBlocked;
        private static int _blockLogs;
        private static GameObject _selected;
        private static TMP_InputField _selectedTmp;
        private static UnityEngine.UI.InputField _selectedUi;

        /// <summary>The next left click is consumed (it resumed play mode) and is not a use.</summary>
        internal static void SwallowClick()
        {
            _swallowClick = true;
        }

        /// <summary>Once per frame, from the input pump (after the VR hands have read their buttons).</summary>
        internal static void Poll()
        {
            if (_polledFrame == Time.frameCount)
            {
                return;
            }

            _polledFrame = Time.frameCount;
            bool vr = RrPlayer.InVr;
            ReadFocus(vr);
            bool blocked = Blocked;
            if (blocked != _wasBlocked)
            {
                _wasBlocked = blocked;
                if (_blockLogs < 20)
                {
                    _blockLogs++;
                    RrLog.Info("Input", blocked ? "blocked by " + BlockedBy : "unblocked");
                }
            }

            bool useWas = UseHeld;
            bool altWas = AltHeld;
            UseDown = UseHeld = UseUp = false;
            GrabDown = GrabUp = ThrowDown = DropDown = false;
            AltDown = AltHeld = AltUp = false;
            ReloadDown = CrouchHeld = ThirdPersonDown = false;
            SlotDown = 0;
            MenuDown = !vr && Input.GetKeyDown(MenuKey);
            BackDown = !vr && Input.GetKeyDown(BackKey);

            if (vr)
            {
                _useArmed = _altArmed = _grabArmed = _qHeld = false;
                RrHand right = RrHand.Right;
                if (!blocked && right != null)
                {
                    UseDown = right.TriggerDown;
                    UseHeld = right.TriggerHeld;
                    UseUp = right.TriggerUp;
                    GrabDown = right.GripDown;
                    GrabUp = right.GripUp;
                }
                else if (useWas)
                {
                    UseUp = true;
                }

                return;
            }

            bool play = RrPlayer.InPlayMode;

            // Left mouse: use. Not when it lands on UI with a free cursor, nor the click that resumes play mode after
            // a menu or focus loss suspended it (that click only takes the camera and cursor back).
            if (Input.GetMouseButtonDown(0))
            {
                if (!blocked && !PointerOverUi && RrScreenCamera.WaitingForClick)
                {
                    RrScreenCamera.ResumeRequested = true;
                    _swallowClick = true;
                }

                _useArmed = !blocked && !_swallowClick && (play || !PointerOverUi);
                _swallowClick = false;
                UseDown = _useArmed;
            }

            if (_useArmed)
            {
                if (Input.GetMouseButton(0) && !blocked)
                {
                    UseHeld = true;
                }
                else
                {
                    _useArmed = false;
                    UseUp = true;
                }
            }
            else if (useWas)
            {
                UseUp = true;
            }

            if (!play || blocked)
            {
                if (_altArmed || altWas)
                {
                    AltUp = true;
                }

                if (_grabArmed)
                {
                    GrabUp = true;
                }

                _altArmed = _grabArmed = _qHeld = false;
            }
            else
            {
                // Right mouse: secondary / aim down sights.
                if (Input.GetMouseButtonDown(1))
                {
                    _altArmed = true;
                    AltDown = true;
                }

                if (_altArmed)
                {
                    if (Input.GetMouseButton(1))
                    {
                        AltHeld = true;
                    }
                    else
                    {
                        _altArmed = false;
                        AltUp = true;
                    }
                }

                // E: grab.
                if (Input.GetKeyDown(GrabKey))
                {
                    _grabArmed = true;
                    GrabDown = true;
                }

                if (_grabArmed && !Input.GetKey(GrabKey))
                {
                    _grabArmed = false;
                    GrabUp = true;
                }

                // Q: tap drops, a long press tosses (fires once, when the hold passes 0.33 s).
                if (Input.GetKeyDown(DropKey))
                {
                    _qHeld = true;
                    _qFired = false;
                    _qDownAt = Time.unscaledTime;
                }

                if (_qHeld)
                {
                    if (!Input.GetKey(DropKey))
                    {
                        _qHeld = false;
                        DropDown = !_qFired;
                    }
                    else if (!_qFired && Time.unscaledTime - _qDownAt >= LongPress)
                    {
                        _qFired = true;
                        ThrowDown = true;
                    }
                }

                CrouchHeld = Input.GetKey(CrouchKey);
            }

            if (!blocked)
            {
                ReloadDown = Input.GetKeyDown(ReloadKey);
                ThirdPersonDown = Input.GetKeyDown(ThirdPersonKey);
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    SlotDown = 1;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    SlotDown = 2;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    SlotDown = 3;
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    SlotDown = 4;
                }
            }
        }

        /// <summary>Chat focus (only re-inspected when the selection changes) and UI under a free cursor.</summary>
        private static void ReadFocus(bool vr)
        {
            EventSystem es = EventSystem.current;
            GameObject selected = es != null ? es.currentSelectedGameObject : null;
            if (selected != _selected)
            {
                _selected = selected;
                _selectedTmp = selected != null ? selected.GetComponent<TMP_InputField>() : null;
                _selectedUi = selected != null ? selected.GetComponent<UnityEngine.UI.InputField>() : null;
            }

            ChatFocused = (_selectedTmp != null && _selectedTmp.isFocused) || (_selectedUi != null && _selectedUi.isFocused);
            PointerOverUi = !vr && es != null && Cursor.lockState == CursorLockMode.None && es.IsPointerOverGameObject();
        }

        /// <summary>Statics survive a world re-entry: cleared by RrPlayer.Shutdown.</summary>
        internal static void Reset()
        {
            UseDown = UseHeld = UseUp = false;
            GrabDown = GrabUp = ThrowDown = DropDown = false;
            AltDown = AltHeld = AltUp = false;
            ReloadDown = CrouchHeld = ThirdPersonDown = MenuDown = BackDown = false;
            SlotDown = 0;
            _manualBlock = false;
            Owners.Clear();
            ChatFocused = false;
            PointerOverUi = false;
            _polledFrame = -1;
            _useArmed = _altArmed = _grabArmed = _qHeld = _qFired = _swallowClick = _wasBlocked = false;
            _blockLogs = 0;
            _selected = null;
            _selectedTmp = null;
            _selectedUi = null;
        }
    }
}
