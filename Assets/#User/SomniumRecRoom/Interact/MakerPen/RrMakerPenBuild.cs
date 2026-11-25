using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.XR;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// The Maker Pen's build tools in a maker room the local player may build in (<see cref="RrMakerEdits.CanEdit"/>):
    /// Rec Room's pen modes in their basic form, on the pen next to <see cref="RrMakerPenTool"/>, which hands the
    /// frame over to <see cref="Tick"/> while they apply (elsewhere the pen draws ephemeral ink as before).
    ///
    /// Everything goes through the room's <see cref="IRrMakerRoomHost"/> (create / update / delete / save); the room
    /// builds every object, sends the edits to everyone and keeps them for the next save. Modes:
    /// Draw (a tube that becomes a room object), Shapes (the install's 12 extrusion shapes: tap for a 25 cm shape,
    /// hold and pull to stretch a box), Props (a catalogue of install props; the new prop follows the pen until the
    /// trigger is let go), Select (grab: the object follows the pen; scale with a thumbstick or +/-), Clone (grab a
    /// copy), Recolor, Delete and Physics (apply Decoration / Environment / Physical / Sticky, toggle Freeze or
    /// Grabbable). Snap puts positions on a 5 cm grid and rotations on 15 degrees; undo / redo keep 10 of your own
    /// edits (<see cref="RrMakerPenHistory"/>); Save asks the room to save.
    ///
    /// VR: everything is on the palette above the other hand (touch with the tip, <see cref="RrMakerPenBuildPalette"/>);
    /// the trigger acts; objects are found by touching them with the tip or by the laser from the tip; A/X undoes;
    /// while an object is held either thumbstick (up / down) scales it.
    /// Screen: the crosshair places and picks within <see cref="ScreenReach"/> m. 1-8 modes, LMB act (hold to drag),
    /// RMB cancel a drag, Z undo, Y redo, C / Shift+C colour, wheel next item (while dragging: size; Shift+wheel:
    /// distance), T / Shift+T prop category, - / = size, [ / ] turn, , / . tilt, G snap, F5 save. E and Q are left to
    /// the grab system (drop / throw the pen).
    /// </summary>
    public sealed class RrMakerPenBuild : MonoBehaviour
    {
        public const float SnapGrid = 0.05f;
        public const float SnapAngle = 15f;
        public const float LaserReach = 6f;
        public const float ScreenReach = 8f;
        public const float ScreenPlaceDepth = 2.5f;
        public const float TouchRadius = 0.02f;
        public const float SendEvery = 0.2f;
        public const float DefaultShapeSize = 0.25f;
        public const float MinShapeSize = 0.02f;
        public const float StretchStart = 0.03f;
        public const float InkMinStep = 0.008f;
        public const int InkMaxPoints = 200;
        public const float TriggerOn = 0.3f;
        public const float StickDeadZone = 0.25f;
        public const float StepScale = 1.1f;
        public const float MinScale = 0.01f;
        public const float MaxScale = 200f;
        public const float FitTimeout = 15f;

        public const string PlaceClip = "sfx_ui_maker_pen_created_object_place_01";
        public const string PickupClip = "sfx_ui_maker_pen_created_object_pickup_01";
        public const string SpawnClipPrefix = "sfx_ui_prop_spawn_0";
        public const string ColourClipPrefix = "sfx_ui_maker_pen_created_object_apply_color_0";
        public const string ConfigureClipPrefix = "sfx_ui_maker_pen_created_object_apply_material_generic_0";
        public const string SnapClip = "sfx_ui_maker_pen_magnetic_snap";
        public const string StretchLoopClip = "sfx_ui_maker_pen_draw_primitive_loop_medium_01";
        public const string DeniedClip = "comped_sfx_rrui_watch_ui_select_negative_01";

        private static readonly Color FrozenColour = new Color(0.6f, 0.75f, 0.95f);
        private static readonly Color HeldColour = new Color(1f, 1f, 1f);

        /// <summary>Per shape name: the built shape's box in Scale = 1 units (centre and size), measured once.</summary>
        private static readonly Dictionary<string, Bounds> ShapeUnits = new Dictionary<string, Bounds>();

        private static readonly Bounds AssumedUnit = new Bounds(Vector3.zero, Vector3.one);

        private sealed class Drag
        {
            public uint Id;
            public bool Fresh;
            public string What;
            public RrMakerObject Before;
            public RrMakerObject Data;
            public Vector3 BaseScale;
            public float Factor = 1f;
            public float AppliedFactor = 1f;

            /// <summary>The visual centre's offset from the pivot, in the object's frame, at Factor 1.</summary>
            public Vector3 CentreLocal;

            /// <summary>VR: the centre and rotation in the pen's frame.</summary>
            public Vector3 RelCentre;

            public Quaternion RelRot = Quaternion.identity;

            /// <summary>Screen: distance along the aim ray, centre offset and rotation in the view's yaw frame.</summary>
            public float Distance;

            public Vector3 Offset;
            public Quaternion YawRot = Quaternion.identity;

            public Vector3 StartCentre;
            public Vector3 Centre;
            public Quaternion Rotation = Quaternion.identity;
            public bool Moved;
            public float SentAt;
            public bool Dirty;
            public Vector3 LastSnap;
        }

        private sealed class Fit
        {
            public uint Id;
            public bool Shape;
            public Vector3 Centre;
            public Quaternion Rotation;
            public Vector3 Size;
            public bool Settle;
            public float SurfaceY;
            public bool Touched;
            public float Until;
        }

        private sealed class Entry
        {
            public GameObject Go;
            public Bounds Bounds;
            public bool HasCollider;
            public float ColliderAt;
            public bool Seen;
        }

        private sealed class InkHandoff
        {
            public uint Id;
            public GameObject Preview;
            public Mesh Mesh;
            public float Until;
        }

        public RrMakerPenTool Tool;

        public RrMakerMode Mode { get; private set; }
        public int Shape { get; private set; }
        public int Category { get; private set; }
        public int Prop { get; private set; }
        public RrMakerPhysicsBrush Brush { get; private set; } = RrMakerPhysicsBrush.Physical;
        public bool Snap { get; private set; }

        /// <summary>Desktop ink radius (VR: the trigger pull).</summary>
        public float DrawRadius { get; private set; } = RrMakerPenTool.ScreenRadius;

        public readonly RrMakerPenHistory History = new RrMakerPenHistory();

        public string ToastText { get; private set; } = "";
        public bool ToastBad { get; private set; }
        public float ToastUntil { get; private set; }

        /// <summary>The object under the tip / laser / crosshair (0 none).</summary>
        public uint Hover { get; private set; }

        /// <summary>The last object grabbed or made (desktop +/- and turn keys act on it when nothing is held).</summary>
        public uint Selected { get; private set; }

        public bool ScreenMode { get; private set; }
        public int Made, Removed, Changed;

        public bool Dragging
        {
            get { return _drag != null; }
        }

        private RrMakerPenBuildPalette _palette;
        private RrMakerPenGizmos _gizmos;
        private Drag _drag;
        private readonly List<Fit> _fits = new List<Fit>();
        private readonly Dictionary<uint, Entry> _entries = new Dictionary<uint, Entry>();
        private readonly List<uint> _gone = new List<uint>();
        private readonly List<InkHandoff> _handoffs = new List<InkHandoff>();
        private readonly HashSet<uint> _dirtyIds = new HashSet<uint>();
        private readonly List<Rigidbody> _heldBodies = new List<Rigidbody>();
        private readonly List<Rigidbody> _bodyScratch = new List<Rigidbody>();
        private GameObject _heldGo;
        private float _entriesAt;
        private float _dirtyAt;
        private bool _entriesDirty = true;
        private Transform _frame;
        private bool _active;
        private bool _trigWas;
        private bool _suppress;
        private Transform _lit;
        private bool _hoverTouched;
        private Vector3 _hoverPoint;
        private float _hoverDistance;
        private float _maxPull;
        private float _snapSoundAt;

        // shape drawing
        private bool _shaping;
        private bool _stretched;
        private Vector3 _shapeA;
        private Vector3 _shapeNormal;
        private bool _shapeOnSurface;
        private Quaternion _shapeFrame = Quaternion.identity;
        private Vector3 _shapeCentre;
        private Quaternion _shapeRot = Quaternion.identity;
        private Vector3 _shapeSize;

        // ink drawing
        private readonly List<Vector3> _inkPoints = new List<Vector3>();
        private readonly List<float> _inkRadii = new List<float>();
        private GameObject _inkPreview;
        private Mesh _inkMesh;
        private float _inkRadius;
        private float _inkSpeed;
        private Vector3 _inkLast;

        private AudioSource _loop;
        private string _loopClip;

        private static readonly RaycastHit[] Hits = new RaycastHit[48];
        private static readonly Collider[] Overlaps = new Collider[32];

        private static int PointMask
        {
            get { return ~((1 << RrLayers.IgnoreRaycast) | (1 << RrLayers.Ui)); }
        }

        private Transform Home
        {
            get { return Tool != null && Tool.Room != null ? Tool.Room.transform : transform.parent; }
        }

        private static IRrMakerRoomHost Host
        {
            get { return RrMakerEdits.Host; }
        }

        public RrMakerPropCategory CurrentCategory
        {
            get
            {
                var cats = RrMakerPenCatalog.Categories;
                return cats[Mathf.Clamp(Category, 0, cats.Count - 1)];
            }
        }

        public RrMakerPropEntry CurrentProp
        {
            get
            {
                var c = CurrentCategory;
                return c.Items.Count > 0 ? c.Items[Mathf.Clamp(Prop, 0, c.Items.Count - 1)] : null;
            }
        }

        public RrMakerShapeEntry CurrentShape
        {
            get { return RrMakerPenCatalog.Shapes[Mathf.Clamp(Shape, 0, RrMakerPenCatalog.Shapes.Length - 1)]; }
        }

        public byte Colour
        {
            get { return Tool != null ? Tool.Colour : RrMakerPenWire.DefaultColour; }
        }

        // ------------------------------------------------------------------ life

        private void OnEnable()
        {
            RrMakerEdits.Changed += OnChanged;
        }

        private void OnDisable()
        {
            RrMakerEdits.Changed -= OnChanged;
            Idle();
        }

        private void OnDestroy()
        {
            RrMakerEdits.Changed -= OnChanged;
            EndAll(false);
            SetLit(null);
            StopLoop();
            if (_gizmos != null)
            {
                _gizmos.Destroy();
            }

            if (_palette != null)
            {
                Destroy(_palette.gameObject);
            }

            DestroyInkPreview();
            foreach (var h in _handoffs)
            {
                DestroyHandoff(h);
            }

            _handoffs.Clear();
        }

        private void OnChanged(uint id)
        {
            _entriesDirty = true;
            _dirtyIds.Add(id);
            var host = Host;
            if (_drag != null && _drag.Id == id && host != null && host.Get(id) == null)
            {
                ReleaseBodies();
                _drag = null;
                Toast("Someone deleted it", true);
            }

            if (Selected == id && host != null && host.Get(id) == null)
            {
                Selected = 0;
            }
        }

        /// <summary>Not building this frame (pen let go, no rights, another room): drags are let go where they are.</summary>
        public void Idle()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            EndAll(true);
            SetLit(null);
            Hover = 0;
            StopLoop();
            if (_gizmos != null)
            {
                _gizmos.HideAll();
            }

            if (_palette != null)
            {
                _palette.Hide();
            }

            _trigWas = false;
            _suppress = false;
        }

        private void EndAll(bool commit)
        {
            if (_drag != null)
            {
                EndDrag(commit);
            }

            if (_shaping)
            {
                _shaping = false;
                if (_gizmos != null)
                {
                    _gizmos.Preview(false, Vector3.zero, Quaternion.identity, Vector3.zero, Color.white);
                }
            }

            if (_inkPoints.Count > 0)
            {
                if (commit)
                {
                    EndInk();
                }
                else
                {
                    _inkPoints.Clear();
                    _inkRadii.Clear();
                    DestroyInkPreview();
                }
            }
        }

        // ------------------------------------------------------------------ frame

        /// <summary>One frame of building with the pen in <paramref name="hand"/> (called by the tool instead of drawing ink).</summary>
        public void Tick(RrHand hand)
        {
            var host = Host;
            if (host == null || !host.CanEdit || hand == null)
            {
                Idle();
                return;
            }

            if (_gizmos == null)
            {
                _gizmos = new RrMakerPenGizmos(Home);
            }

            if (_palette == null)
            {
                _palette = RrMakerPenBuildPalette.Create(Home, this);
            }

            _active = true;
            float now = Time.unscaledTime;
            bool screen = hand.IsScreenHand;
            ScreenMode = screen;
            _palette.Show(screen, hand);
            RefreshEntries(host, now);

            Vector3 tip;
            Quaternion penRot;
            Ray ray;
            if (screen)
            {
                ray = RrInput.AimRay;
                tip = ray.origin + ray.direction * RrMakerPenTool.ScreenDepth;
                penRot = Quaternion.LookRotation(ray.direction, Vector3.up);
            }
            else
            {
                tip = Tool.Tip != null ? Tool.Tip.position : transform.TransformPoint(RrMakerPenTool.TipFallback);
                penRot = transform.rotation;
                ray = new Ray(tip, transform.forward);
            }

            bool down, held;
            float pull = 1f;
            if (screen)
            {
                down = RrInput.UseDown;
                held = RrInput.UseHeld;
                if (down && RrPressable.PressAlong(ray, RrPressable.ScreenReach, "maker pen"))
                {
                    _suppress = true;
                }

                if (!RrInput.Blocked)
                {
                    ScreenKeys(host);
                }
            }
            else
            {
                pull = Mathf.Clamp01((hand.Trigger - 0.08f) / 0.92f);
                held = hand.Trigger > TriggerOn || hand.TriggerHeld;
                down = held && !_trigWas;
                _trigWas = held;
                if (hand.PrimaryDown)
                {
                    UndoStep();
                }

                if (_palette.Touch(tip, hand) && down)
                {
                    _suppress = true;
                }
            }

            if (_suppress)
            {
                if (!held)
                {
                    _suppress = false;
                }

                down = false;
                held = false;
            }

            // what the pen points at (not while something is held or drawn)
            bool targeting = _drag == null && !_shaping && _inkPoints.Count == 0 &&
                             (Mode == RrMakerMode.Select || Mode == RrMakerMode.Clone || Mode == RrMakerMode.Recolor ||
                              Mode == RrMakerMode.Delete || Mode == RrMakerMode.Physics);
            if (targeting)
            {
                Hover = FindTarget(host, screen, tip, ray, out _hoverTouched, out _hoverPoint, out _hoverDistance);
            }
            else
            {
                Hover = 0;
            }

            switch (Mode)
            {
                case RrMakerMode.Draw:
                    DrawTick(host, screen, tip, ray, held, pull, hand);
                    break;
                case RrMakerMode.Shapes:
                    ShapeTick(host, screen, tip, ray, penRot, down, held, hand);
                    break;
                case RrMakerMode.Props:
                    if (down && _drag == null)
                    {
                        PlaceProp(host, screen, tip, ray, hand);
                    }

                    break;
                case RrMakerMode.Select:
                case RrMakerMode.Clone:
                    if (down && _drag == null && Hover != 0)
                    {
                        Grab(host, Hover, Mode == RrMakerMode.Clone, screen, tip, penRot, ray, hand);
                    }

                    break;
                case RrMakerMode.Recolor:
                case RrMakerMode.Delete:
                case RrMakerMode.Physics:
                    if (down && Hover != 0)
                    {
                        Apply(host, Hover, hand);
                    }

                    break;
            }

            if (_drag != null)
            {
                if (!held)
                {
                    EndDrag(true);
                }
                else
                {
                    DragTick(host, screen, tip, penRot, ray, hand, now);
                }
            }

            Visuals(host, screen, tip, ray);
            TickFits(host, now);
            TickHandoffs(host, now);
            UpdateLoop();
        }

        // ------------------------------------------------------------------ desktop keys

        private void ScreenKeys(IRrMakerRoomHost host)
        {
            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SetMode((RrMakerMode)i);
                }
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (RrInput.ReloadDown)
            {
                UndoStep();
            }

            if (Input.GetKeyDown(KeyCode.Y))
            {
                RedoStep();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                CycleColour(shift ? -1 : 1);
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                PickCategory(Category + (shift ? -1 : 1));
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                ToggleSnap();
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                Save();
            }

            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                ScaleStep(-1);
            }

            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                ScaleStep(1);
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                RotateStep(Vector3.up, -1);
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                RotateStep(Vector3.up, 1);
            }

            if (Input.GetKeyDown(KeyCode.Comma))
            {
                RotateStep(Vector3.right, -1);
            }

            if (Input.GetKeyDown(KeyCode.Period))
            {
                RotateStep(Vector3.right, 1);
            }

            if (RrInput.AltDown && _drag != null)
            {
                CancelDrag();
            }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }

            int step = wheel > 0f ? 1 : -1;
            if (_drag != null)
            {
                if (shift)
                {
                    _drag.Distance = Mathf.Clamp(_drag.Distance + step * 0.25f, 0.4f, ScreenReach * 2f);
                }
                else
                {
                    ScaleDrag(step > 0 ? StepScale : 1f / StepScale);
                }

                return;
            }

            switch (Mode)
            {
                case RrMakerMode.Shapes:
                    PickShape(Shape - step);
                    break;
                case RrMakerMode.Props:
                    PickProp(Prop - step);
                    break;
                case RrMakerMode.Physics:
                    SetBrush((RrMakerPhysicsBrush)(((int)Brush - step + 6) % 6));
                    break;
                default:
                    CycleColour(-step);
                    break;
            }
        }

        // ------------------------------------------------------------------ palette / key actions

        public void SetMode(RrMakerMode mode)
        {
            if (mode == Mode)
            {
                return;
            }

            EndAll(true);
            Mode = mode;
            Hover = 0;
            SetLit(null);
        }

        public void PickShape(int index)
        {
            int n = RrMakerPenCatalog.Shapes.Length;
            Shape = ((index % n) + n) % n;
            if (Mode != RrMakerMode.Shapes)
            {
                SetMode(RrMakerMode.Shapes);
            }
        }

        public void PickCategory(int index)
        {
            int n = RrMakerPenCatalog.Categories.Count;
            int c = ((index % n) + n) % n;
            if (c != Category)
            {
                Category = c;
                Prop = 0;
            }

            if (Mode != RrMakerMode.Props)
            {
                SetMode(RrMakerMode.Props);
            }
        }

        public void PickProp(int index)
        {
            int n = CurrentCategory.Items.Count;
            if (n > 0)
            {
                Prop = ((index % n) + n) % n;
            }

            if (Mode != RrMakerMode.Props)
            {
                SetMode(RrMakerMode.Props);
            }
        }

        public void PickColour(byte colour)
        {
            if (Tool != null)
            {
                Tool.SetColour(colour);
            }

            if (Mode != RrMakerMode.Draw && Mode != RrMakerMode.Shapes && Mode != RrMakerMode.Recolor)
            {
                SetMode(RrMakerMode.Recolor);
            }
        }

        public void CycleColour(int step)
        {
            if (Tool != null)
            {
                Tool.Cycle(step);
            }
        }

        public void SetBrush(RrMakerPhysicsBrush brush)
        {
            Brush = brush;
            if (Mode != RrMakerMode.Physics)
            {
                SetMode(RrMakerMode.Physics);
            }
        }

        public void ToggleSnap()
        {
            Snap = !Snap;
            Toast(Snap ? "Snap on: 5 cm, 15 degrees" : "Snap off", false);
        }

        public void UndoStep()
        {
            EndAll(true);
            string what = History.Undo(Host);
            if (what.Length > 0)
            {
                Toast("Undo " + what, false);
                RrSound.Play(ConfigureClipPrefix + Random.Range(1, 5), transform.position, 0.3f);
            }
            else
            {
                Toast("Nothing to undo", true);
            }
        }

        public void RedoStep()
        {
            EndAll(true);
            string what = History.Redo(Host);
            if (what.Length > 0)
            {
                Toast("Redo " + what, false);
                RrSound.Play(ConfigureClipPrefix + Random.Range(1, 5), transform.position, 0.3f);
            }
            else
            {
                Toast("Nothing to redo", true);
            }
        }

        public void Save()
        {
            var host = Host;
            if (host == null)
            {
                return;
            }

            EndAll(true);
            bool ok = host.Save();
            var info = host.Info;
            if (ok)
            {
                Toast("Room saved" + (info != null ? " (version " + info.Version + ")" : ""), false);
                RrSound.PlayUi(RrPressable.DefaultSound, 0.6f);
            }
            else
            {
                Toast("Not saved: only the owner and co-owners can save", true);
                RrSound.PlayUi(DeniedClip, 0.6f);
            }

            RrLog.Info("MakerPen", "save asked: " + (ok ? "saved" : "refused") + (info != null ? " '" + info.Name + "' v" + info.Version : ""));
        }

        /// <summary>+ / -: the held object, else the selected one (an undo step), else (Draw) the desktop tube size.</summary>
        public void ScaleStep(int dir)
        {
            float f = dir > 0 ? StepScale : 1f / StepScale;
            if (_drag != null)
            {
                ScaleDrag(f);
                return;
            }

            if (Mode == RrMakerMode.Draw)
            {
                DrawRadius = Mathf.Clamp(DrawRadius * (dir > 0 ? 1.25f : 0.8f), RrMakerPenTool.MinRadius, RrMakerPenTool.MaxRadius * 2f);
                Toast("Tube " + (DrawRadius * 200f).ToString("0.0") + " cm", false);
                return;
            }

            EditSelected(f, Quaternion.identity, dir > 0 ? "Grow" : "Shrink");
        }

        /// <summary>Turn (about up) or tilt (about the view's right) by one snap step: the held or selected object.</summary>
        public void RotateStep(Vector3 axis, int dir)
        {
            var dq = Quaternion.AngleAxis(SnapAngle * dir, axis);
            if (_drag != null)
            {
                _drag.YawRot = dq * _drag.YawRot;
                _drag.RelRot = dq * _drag.RelRot;
                _drag.Dirty = true;
                return;
            }

            var cam = RrPlayer.Camera;
            Quaternion yaw = YawOf(cam != null ? cam.transform.forward : Vector3.forward, false);
            EditSelected(1f, yaw * dq * Quaternion.Inverse(yaw), "Turn");
        }

        private void EditSelected(float factor, Quaternion turn, string what)
        {
            var host = Host;
            uint id = Selected;
            var before = id != 0 && host != null ? host.Get(id) : null;
            if (before == null)
            {
                Toast("Select something first", true);
                return;
            }

            if (before.Frozen)
            {
                Denied("Frozen: unfreeze it in Physics");
                return;
            }

            var go = host.Find(id);
            var after = before.Clone();
            Vector3 pos = ToWorld(before.Position);
            Quaternion rot = RotToWorld(before.Rotation);
            Vector3 centre = pos;
            Bounds b;
            if (go != null && RrMakerPenMeasure.WorldBounds(go, out b))
            {
                centre = b.center;
            }

            Quaternion newRot = turn * rot;
            Vector3 newPos = centre + turn * ((pos - centre) * factor);
            if (Snap)
            {
                newRot = SnapRotation(newRot);
                newPos = SnapPosition(newPos);
            }

            after.Position = ToData(newPos);
            after.Rotation = RotToData(newRot);
            after.Scale = Scaled(before.Kind, before.Scale, factor);
            host.Update(after);
            History.Changed(before, after, what);
            Changed++;
        }

        // ------------------------------------------------------------------ targets

        /// <summary>
        /// The bounds of every object (for touching and pointing at objects without colliders): all of them once a
        /// second, and the ones the room reported changed (<see cref="RrMakerEdits.Changed"/>) within 0.1 s.
        /// </summary>
        private void RefreshEntries(IRrMakerRoomHost host, float now)
        {
            if (now < _entriesAt)
            {
                if (_entriesDirty && now >= _dirtyAt)
                {
                    _entriesDirty = false;
                    _dirtyAt = now + 0.1f;
                    foreach (uint id in _dirtyIds)
                    {
                        if (host.Get(id) == null)
                        {
                            _entries.Remove(id);
                        }
                        else
                        {
                            RefreshEntry(host, id, now);
                        }
                    }

                    _dirtyIds.Clear();
                }

                return;
            }

            _entriesAt = now + 1f;
            _entriesDirty = false;
            _dirtyIds.Clear();
            foreach (var kv in _entries)
            {
                kv.Value.Seen = false;
            }

            foreach (uint id in host.Ids)
            {
                RefreshEntry(host, id, now);
            }

            _gone.Clear();
            foreach (var kv in _entries)
            {
                if (!kv.Value.Seen)
                {
                    _gone.Add(kv.Key);
                }
            }

            foreach (uint id in _gone)
            {
                _entries.Remove(id);
            }
        }

        private void RefreshEntry(IRrMakerRoomHost host, uint id, float now)
        {
            Entry e;
            if (!_entries.TryGetValue(id, out e))
            {
                e = new Entry();
                _entries[id] = e;
            }

            e.Seen = true;
            var go = host.Find(id);
            if (go != e.Go || now >= e.ColliderAt)
            {
                e.HasCollider = RrMakerPenMeasure.HasCollider(go);
                e.ColliderAt = now + 3f;
            }

            e.Go = go;
            if (go == null)
            {
                return;
            }

            Bounds b;
            e.Bounds = RrMakerPenMeasure.WorldBounds(go, out b) ? b : new Bounds(go.transform.position, Vector3.one * 0.15f);
            if (_frame == null && go.transform.parent != null)
            {
                _frame = go.transform.parent;
            }
        }

        private uint FindTarget(IRrMakerRoomHost host, bool screen, Vector3 tip, Ray ray, out bool touched, out Vector3 point, out float distance)
        {
            touched = false;
            point = tip;
            distance = 0f;
            if (!screen)
            {
                uint t = TouchAt(host, tip);
                if (t != 0)
                {
                    touched = true;
                    return t;
                }
            }

            return PointAt(host, ray, screen ? ScreenReach : LaserReach, out point, out distance);
        }

        private bool IsMine(Collider c)
        {
            return c != null && (c.transform.IsChildOf(transform) || (_palette != null && c.transform.IsChildOf(_palette.transform)));
        }

        private uint TouchAt(IRrMakerRoomHost host, Vector3 tip)
        {
            uint best = 0;
            float bestVolume = float.MaxValue;
            int n = Physics.OverlapSphereNonAlloc(tip, TouchRadius, Overlaps, PointMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = Overlaps[i];
                if (IsMine(c))
                {
                    continue;
                }

                uint id = host.IdOf(c);
                if (id == 0)
                {
                    continue;
                }

                float v = Volume(id);
                if (v < bestVolume)
                {
                    bestVolume = v;
                    best = id;
                }
            }

            if (best != 0)
            {
                return best;
            }

            foreach (var kv in _entries)
            {
                var e = kv.Value;
                if (e.Go == null || e.HasCollider)
                {
                    continue;
                }

                var b = e.Bounds;
                b.Expand(TouchRadius * 2f);
                if (!b.Contains(tip))
                {
                    continue;
                }

                float v = Volume(kv.Key);
                if (v < bestVolume)
                {
                    bestVolume = v;
                    best = kv.Key;
                }
            }

            return best;
        }

        private float Volume(uint id)
        {
            Entry e;
            if (!_entries.TryGetValue(id, out e))
            {
                return 1e6f;
            }

            var s = e.Bounds.size;
            return Mathf.Max(1e-6f, s.x * s.y * s.z);
        }

        private uint PointAt(IRrMakerRoomHost host, Ray ray, float reach, out Vector3 point, out float distance)
        {
            uint best = 0;
            float bestD = reach;
            point = ray.origin + ray.direction * reach;
            distance = reach;
            int n = Physics.RaycastNonAlloc(ray, Hits, reach, PointMask, QueryTriggerInteraction.Collide);
            SortHits(n);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (IsMine(h.collider))
                {
                    continue;
                }

                uint id = host.IdOf(h.collider);
                if (id != 0)
                {
                    best = id;
                    bestD = h.distance;
                    break;
                }

                if (!h.collider.isTrigger)
                {
                    bestD = h.distance;
                    break;
                }
            }

            // objects without colliders (decorations, lights): by their bounds, in front of what the ray hit
            foreach (var kv in _entries)
            {
                var e = kv.Value;
                if (e.Go == null || e.HasCollider || e.Bounds.Contains(ray.origin))
                {
                    continue;
                }

                float d;
                if (e.Bounds.IntersectRay(ray, out d) && d < bestD)
                {
                    bestD = d;
                    best = kv.Key;
                }
            }

            point = ray.origin + ray.direction * bestD;
            distance = bestD;
            return best;
        }

        private static void SortHits(int n)
        {
            for (int i = 1; i < n; i++)
            {
                var h = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > h.distance)
                {
                    Hits[j + 1] = Hits[j];
                    j--;
                }

                Hits[j + 1] = h;
            }
        }

        /// <summary>Where the crosshair puts things: the first solid surface within reach, or a point in the air.</summary>
        private bool PlacePoint(Ray ray, out Vector3 point, out Vector3 normal)
        {
            int n = Physics.RaycastNonAlloc(ray, Hits, ScreenReach, PointMask, QueryTriggerInteraction.Ignore);
            SortHits(n);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (IsMine(h.collider) || (_drag != null && Host != null && Host.IdOf(h.collider) == _drag.Id))
                {
                    continue;
                }

                point = h.point;
                normal = h.normal;
                return true;
            }

            point = ray.origin + ray.direction * ScreenPlaceDepth;
            normal = -ray.direction;
            return false;
        }

        // ------------------------------------------------------------------ frames, snapping

        private Transform Frame
        {
            get
            {
                if (_frame != null)
                {
                    return _frame;
                }

                return Tool != null && Tool.Room != null ? Tool.Room.transform : null;
            }
        }

        private Vector3 ToData(Vector3 world)
        {
            var f = Frame;
            return f != null ? f.InverseTransformPoint(world) : world;
        }

        private Vector3 ToWorld(Vector3 data)
        {
            var f = Frame;
            return f != null ? f.TransformPoint(data) : data;
        }

        private Quaternion RotToData(Quaternion world)
        {
            var f = Frame;
            return f != null ? Quaternion.Inverse(f.rotation) * world : world;
        }

        private Quaternion RotToWorld(Quaternion data)
        {
            var f = Frame;
            return f != null ? f.rotation * data : data;
        }

        private Vector3 SnapPosition(Vector3 world)
        {
            var d = ToData(world);
            d = new Vector3(Mathf.Round(d.x / SnapGrid) * SnapGrid, Mathf.Round(d.y / SnapGrid) * SnapGrid, Mathf.Round(d.z / SnapGrid) * SnapGrid);
            return ToWorld(d);
        }

        private Quaternion SnapRotation(Quaternion world)
        {
            var e = RotToData(world).eulerAngles;
            e = new Vector3(Mathf.Round(e.x / SnapAngle) * SnapAngle, Mathf.Round(e.y / SnapAngle) * SnapAngle, Mathf.Round(e.z / SnapAngle) * SnapAngle);
            return RotToWorld(Quaternion.Euler(e));
        }

        /// <summary>An upright frame facing along <paramref name="forward"/> (snapped to 15 degrees with Snap).</summary>
        private Quaternion YawOf(Vector3 forward, bool snap)
        {
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-4f)
            {
                flat = Vector3.forward;
            }

            var q = Quaternion.LookRotation(flat.normalized, Vector3.up);
            if (snap)
            {
                float yaw = Mathf.Round(q.eulerAngles.y / SnapAngle) * SnapAngle;
                q = Quaternion.Euler(0f, yaw, 0f);
            }

            return q;
        }

        private static Vector3 ClampScale(Vector3 s)
        {
            return new Vector3(Mathf.Clamp(s.x, MinScale, MaxScale), Mathf.Clamp(s.y, MinScale, MaxScale), Mathf.Clamp(s.z, MinScale, MaxScale));
        }

        /// <summary>A size change: uniform for objects; a light's Scale is range, intensity, spot angle - the angle stays.</summary>
        private static Vector3 Scaled(RrMakerKind kind, Vector3 scale, float factor)
        {
            if (kind == RrMakerKind.Light)
            {
                return new Vector3(Mathf.Clamp(scale.x * factor, 0.5f, 60f), Mathf.Clamp(scale.y * factor, 0.05f, 20f), scale.z);
            }

            return ClampScale(scale * factor);
        }

        // ------------------------------------------------------------------ draw (ink objects)

        private void DrawTick(IRrMakerRoomHost host, bool screen, Vector3 tip, Ray ray, bool held, float pull, RrHand hand)
        {
            Vector3 point = screen ? ray.origin + ray.direction * RrMakerPenTool.ScreenDepth : tip;
            if (!held)
            {
                if (_inkPoints.Count > 0)
                {
                    EndInk();
                }

                return;
            }

            if (_inkPoints.Count == 0)
            {
                _maxPull = pull;
                _inkRadius = screen ? DrawRadius : Mathf.Lerp(RrMakerPenTool.MinRadius, RrMakerPenTool.MaxRadius, pull);
                _inkSpeed = 0f;
                AddInk(point);
                return;
            }

            if (!screen && pull > _maxPull + 0.02f)
            {
                _maxPull = pull;
                _inkRadius = Mathf.Lerp(RrMakerPenTool.MinRadius, RrMakerPenTool.MaxRadius, _maxPull);
                for (int i = 0; i < _inkRadii.Count; i++)
                {
                    _inkRadii[i] = _inkRadius;
                }

                RebuildInkPreview();
            }

            float d = (point - _inkLast).magnitude;
            if (Time.deltaTime > 0f)
            {
                _inkSpeed = Mathf.Lerp(_inkSpeed, d / Time.deltaTime, 0.3f);
            }

            if (d < InkMinStep)
            {
                return;
            }

            if (d > 1f)
            {
                // a teleport while drawing: end this stroke and start a new one here
                EndInk();
                AddInk(point);
                return;
            }

            AddInk(point);
            if (!screen && hand != null)
            {
                hand.Haptic(0.05f + 0.1f * Mathf.Clamp01(_inkSpeed), 0.02f);
            }

            if (_inkPoints.Count >= InkMaxPoints)
            {
                var last = _inkPoints[_inkPoints.Count - 1];
                EndInk();
                AddInk(last);
            }
        }

        private void AddInk(Vector3 world)
        {
            _inkPoints.Add(world);
            _inkRadii.Add(_inkRadius);
            _inkLast = world;
            RebuildInkPreview();
        }

        private void RebuildInkPreview()
        {
            if (_inkPreview == null)
            {
                _inkPreview = new GameObject("Maker Pen Ink Preview");
                _inkPreview.transform.SetParent(Home, false);
                _inkMesh = new Mesh { name = "Maker Pen Ink Preview" };
                _inkMesh.MarkDynamic();
                _inkPreview.AddComponent<MeshFilter>().sharedMesh = _inkMesh;
                var mr = _inkPreview.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            _inkPreview.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _inkPreview.GetComponent<MeshRenderer>().sharedMaterial = RrLook.Solid(RrMakerPenWire.ColourOf(Colour));
            RrMakerPenTube.Build(_inkMesh, _inkPoints, _inkRadii);
        }

        /// <summary>The stroke becomes a room object (Kind Ink): centre = Position, points object-local, Width = diameter.</summary>
        private void EndInk()
        {
            var host = Host;
            if (_inkPoints.Count == 0)
            {
                return;
            }

            var bounds = new Bounds(_inkPoints[0], Vector3.zero);
            foreach (var p in _inkPoints)
            {
                bounds.Encapsulate(p);
            }

            Vector3 centre = bounds.center;
            var frameRot = RotToWorld(Quaternion.identity);
            var inv = Quaternion.Inverse(frameRot);
            var points = new List<Vector3>(_inkPoints.Count);
            foreach (var p in _inkPoints)
            {
                points.Add(RrMakerPenWire.Quantise(inv * (p - centre)));
            }

            var o = new RrMakerObject
            {
                Kind = RrMakerKind.Ink,
                Asset = "Tube",
                Position = ToData(centre),
                Rotation = Quaternion.identity,
                Scale = Vector3.one,
                Color = RrMakerPenCatalog.Colour32(Colour),
                Physics = RrMakerPhysics.Decoration,
                Frozen = false,
                Grabbable = false,
                Width = _inkRadius * 2f,
                Points = points
            };
            uint id = host != null && host.CanEdit ? host.Create(o) : 0;
            _inkPoints.Clear();
            _inkRadii.Clear();
            if (id == 0)
            {
                DestroyInkPreview();
                Toast("The room did not take the stroke", true);
                return;
            }

            o.Id = id;
            History.Created(o, "Ink");
            Made++;
            Selected = id;

            // keep the preview until the room has built the tube (no gap), at most 3 s
            if (_inkPreview != null)
            {
                _inkPreview.name = "Maker Pen Ink Handoff";
                _handoffs.Add(new InkHandoff { Id = id, Preview = _inkPreview, Mesh = _inkMesh, Until = Time.unscaledTime + 3f });
                _inkPreview = null;
                _inkMesh = null;
            }
        }

        private void DestroyInkPreview()
        {
            if (_inkPreview != null)
            {
                Destroy(_inkPreview);
            }

            if (_inkMesh != null)
            {
                Destroy(_inkMesh);
            }

            _inkPreview = null;
            _inkMesh = null;
        }

        private void TickHandoffs(IRrMakerRoomHost host, float now)
        {
            for (int i = _handoffs.Count - 1; i >= 0; i--)
            {
                var h = _handoffs[i];
                Bounds b;
                var go = host.Find(h.Id);
                if (now > h.Until || host.Get(h.Id) == null || (go != null && RrMakerPenMeasure.WorldBounds(go, out b)))
                {
                    DestroyHandoff(h);
                    _handoffs.RemoveAt(i);
                }
            }
        }

        private static void DestroyHandoff(InkHandoff h)
        {
            if (h.Preview != null)
            {
                Destroy(h.Preview);
            }

            if (h.Mesh != null)
            {
                Destroy(h.Mesh);
            }
        }

        // ------------------------------------------------------------------ shapes

        private void ShapeTick(IRrMakerRoomHost host, bool screen, Vector3 tip, Ray ray, Quaternion penRot, bool down, bool held, RrHand hand)
        {
            if (down && !_shaping)
            {
                _shaping = true;
                _stretched = false;
                if (screen)
                {
                    _shapeOnSurface = PlacePoint(ray, out _shapeA, out _shapeNormal);
                    var cam = RrPlayer.Camera;
                    Vector3 camFwd = cam != null ? cam.transform.forward : ray.direction;
                    if (_shapeOnSurface)
                    {
                        Vector3 f = Vector3.ProjectOnPlane(camFwd, _shapeNormal);
                        if (f.sqrMagnitude < 1e-4f)
                        {
                            f = Vector3.ProjectOnPlane(cam != null ? cam.transform.up : Vector3.forward, _shapeNormal);
                        }

                        _shapeFrame = Quaternion.LookRotation(f.normalized, _shapeNormal);
                        if (Snap && Mathf.Abs(_shapeNormal.y) > 0.9f)
                        {
                            _shapeFrame = YawOf(f, true);
                        }
                    }
                    else
                    {
                        _shapeFrame = YawOf(camFwd, Snap);
                    }
                }
                else
                {
                    _shapeA = tip;
                    _shapeNormal = transform.forward;
                    _shapeOnSurface = false;
                    _shapeFrame = YawOf(transform.forward, Snap);
                }

                if (hand != null && !screen)
                {
                    hand.Haptic(0.2f, 0.03f);
                }
            }

            if (!_shaping)
            {
                return;
            }

            // the box from the press point to where the pen (crosshair) is now
            Vector3 b;
            if (screen)
            {
                var planeNormal = _shapeNormal;
                var plane = new Plane(planeNormal, _shapeA);
                float enter;
                b = plane.Raycast(ray, out enter) && enter < ScreenReach * 2f ? ray.GetPoint(enter) : _shapeA;
            }
            else
            {
                b = tip;
            }

            if (!_stretched && (b - _shapeA).magnitude >= StretchStart)
            {
                _stretched = true;
            }

            float def = DefaultShapeSize;
            if (!_stretched)
            {
                _shapeSize = Vector3.one * def;
                _shapeRot = _shapeFrame;
                if (screen)
                {
                    _shapeCentre = _shapeOnSurface ? _shapeA + _shapeNormal * (def * 0.5f) : _shapeA;
                }
                else
                {
                    _shapeCentre = _shapeA + transform.forward * (def * 0.5f);
                }
            }
            else
            {
                var d = Quaternion.Inverse(_shapeFrame) * (b - _shapeA);
                Vector3 size;
                Vector3 mid;
                if (screen && _shapeOnSurface)
                {
                    // footprint on the surface, a default-height slab outward
                    size = new Vector3(Mathf.Abs(d.x), def, Mathf.Abs(d.z));
                    mid = new Vector3(d.x * 0.5f, def * 0.5f, d.z * 0.5f);
                }
                else if (screen)
                {
                    size = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), def);
                    mid = new Vector3(d.x * 0.5f, d.y * 0.5f, 0f);
                }
                else
                {
                    size = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
                    mid = d * 0.5f;
                }

                float min = Snap ? SnapGrid : MinShapeSize;
                for (int i = 0; i < 3; i++)
                {
                    float s = size[i];
                    if (Snap)
                    {
                        s = Mathf.Round(s / SnapGrid) * SnapGrid;
                    }

                    s = Mathf.Max(min, s);
                    // keep the box on the press point's side when a side is clamped
                    float sign = d[i] < 0f ? -1f : 1f;
                    if (!(screen && _shapeOnSurface && i == 1) && !(screen && !_shapeOnSurface && i == 2))
                    {
                        mid[i] = sign * s * 0.5f;
                    }

                    size[i] = s;
                }

                _shapeSize = size;
                _shapeRot = _shapeFrame;
                _shapeCentre = _shapeA + _shapeFrame * mid;
            }

            _gizmos.Preview(true, _shapeCentre, _shapeRot, _shapeSize, RrMakerPenWire.ColourOf(Colour));
            if (!held)
            {
                _shaping = false;
                _gizmos.Preview(false, Vector3.zero, Quaternion.identity, Vector3.zero, Color.white);
                CreateShape(host, _shapeCentre, _shapeRot, _shapeSize, hand, screen);
            }
        }

        private static Bounds UnitOf(string shape)
        {
            Bounds u;
            return shape != null && ShapeUnits.TryGetValue(shape, out u) ? u : AssumedUnit;
        }

        /// <summary>Scale and pivot that put a shape's box at (centre, rotation, size), from its measured unit box.</summary>
        private void ShapeTransform(string shape, Vector3 centre, Quaternion rot, Vector3 size, out Vector3 pivot, out Vector3 scale)
        {
            var u = UnitOf(shape);
            var s0 = u.size;
            scale = ClampScale(new Vector3(size.x / Mathf.Max(1e-3f, s0.x), size.y / Mathf.Max(1e-3f, s0.y), size.z / Mathf.Max(1e-3f, s0.z)));
            pivot = centre - rot * Vector3.Scale(scale, u.center);
            if (Snap)
            {
                pivot = SnapPosition(pivot);
            }
        }

        private void CreateShape(IRrMakerRoomHost host, Vector3 centre, Quaternion rot, Vector3 size, RrHand hand, bool screen)
        {
            if (Snap)
            {
                rot = SnapRotation(rot);
            }

            string name = CurrentShape.Name;
            Vector3 pivot, scale;
            ShapeTransform(name, centre, rot, size, out pivot, out scale);
            var o = new RrMakerObject
            {
                Kind = RrMakerKind.Shape,
                Asset = name,
                Position = ToData(pivot),
                Rotation = RotToData(rot),
                Scale = scale,
                Color = RrMakerPenCatalog.Colour32(Colour),
                Physics = RrMakerPhysics.Environment,
                Frozen = false,
                Grabbable = false
            };
            uint id = host.Create(o);
            if (id == 0)
            {
                Toast("The room did not take the shape", true);
                RrSound.PlayUi(DeniedClip, 0.5f);
                return;
            }

            o.Id = id;
            History.Created(o, CurrentShape.Label);
            Made++;
            Selected = id;
            _fits.Add(new Fit { Id = id, Shape = true, Centre = centre, Rotation = rot, Size = size, Until = Time.unscaledTime + FitTimeout });
            RrSound.Play(PlaceClip, centre, 0.4f);
            if (hand != null && !screen)
            {
                hand.Haptic(0.4f, 0.06f);
            }
        }

        // ------------------------------------------------------------------ props

        private void PlaceProp(IRrMakerRoomHost host, bool screen, Vector3 tip, Ray ray, RrHand hand)
        {
            var e = CurrentProp;
            if (e == null)
            {
                return;
            }

            Vector3 pivot;
            Quaternion rot;
            bool floor = false;
            float floorY = 0f;
            float distance;
            var cam = RrPlayer.Camera;
            Vector3 view = cam != null ? cam.transform.forward : ray.direction;
            if (screen)
            {
                Vector3 n;
                bool hit = PlacePoint(ray, out pivot, out n);
                if (hit && n.y < 0.7f)
                {
                    pivot += n * 0.05f;
                }

                floor = hit && n.y >= 0.7f;
                floorY = pivot.y;
                distance = (pivot - ray.origin).magnitude;
            }
            else
            {
                pivot = tip + transform.forward * 0.05f;
                distance = 0f;
            }

            // facing the player, upright (Rec Room hands a prop over the right way up); a VR light points where the pen does
            rot = e.Kind == RrMakerKind.Light && !screen ? transform.rotation : YawOf(-view, Snap);
            if (Snap)
            {
                pivot = SnapPosition(pivot);
                rot = SnapRotation(rot);
            }

            var colour = (Color)RrMakerPenCatalog.Colour32(Colour);
            var o = new RrMakerObject
            {
                Kind = e.Kind,
                Asset = e.Asset,
                Position = ToData(pivot),
                Rotation = RotToData(rot),
                Scale = e.Kind == RrMakerKind.Light ? new Vector3(6f, 1.5f, 60f) : Vector3.one,
                Color = e.Kind == RrMakerKind.Light && colour.maxColorComponent > 0.3f ? (Color32)colour : new Color32(255, 255, 255, 255),
                Physics = e.Handheld ? RrMakerPhysics.Physical : RrMakerPhysics.Environment,
                Frozen = false,
                Grabbable = e.Handheld
            };
            if (e.Kind == RrMakerKind.Light)
            {
                o.Physics = RrMakerPhysics.Decoration;
            }

            uint id = host.Create(o);
            if (id == 0)
            {
                Toast("The room did not take " + e.Label, true);
                RrSound.PlayUi(DeniedClip, 0.5f);
                return;
            }

            o.Id = id;
            Made++;
            Selected = id;
            RrSound.Play(SpawnClipPrefix + Random.Range(1, 5), pivot, 0.5f);
            if (hand != null && !screen)
            {
                hand.Haptic(0.4f, 0.06f);
            }

            if (floor)
            {
                _fits.Add(new Fit { Id = id, Settle = true, SurfaceY = floorY, Until = Time.unscaledTime + FitTimeout });
            }

            // the new prop follows the pen until the trigger is let go
            BeginDrag(id, true, e.Label, null, o, pivot, rot, Vector3.zero, screen, tip, transform.rotation, ray, distance);
        }

        // ------------------------------------------------------------------ select / clone (drags)

        private void Grab(IRrMakerRoomHost host, uint id, bool clone, bool screen, Vector3 tip, Quaternion penRot, Ray ray, RrHand hand)
        {
            var o = host.Get(id);
            if (o == null)
            {
                return;
            }

            if (o.Frozen)
            {
                Denied("Frozen: unfreeze it in Physics");
                return;
            }

            var go = host.Find(id);
            Vector3 pos = go != null ? go.transform.position : ToWorld(o.Position);
            Quaternion rot = go != null ? go.transform.rotation : RotToWorld(o.Rotation);
            Vector3 centreLocal = Vector3.zero;
            Vector3 c, size;
            if (go != null && RrMakerPenMeasure.Oriented(go, pos, rot, out c, out size))
            {
                centreLocal = Quaternion.Inverse(rot) * (c - pos);
            }

            float distance = screen ? Mathf.Max(0.4f, _hoverDistance) : 0f;
            if (clone)
            {
                var copy = o.Clone();
                copy.Id = 0;
                uint cid = host.Create(copy);
                if (cid == 0)
                {
                    Toast("The room did not take the copy", true);
                    RrSound.PlayUi(DeniedClip, 0.5f);
                    return;
                }

                copy.Id = cid;
                Made++;
                Selected = cid;
                RrSound.Play(SpawnClipPrefix + Random.Range(1, 5), pos, 0.45f);
                BeginDrag(cid, true, "Clone", null, copy, pos, rot, centreLocal, screen, tip, penRot, ray, distance);
            }
            else
            {
                Selected = id;
                RrSound.Play(PickupClip, pos, 0.4f);
                BeginDrag(id, false, "Move", o, o.Clone(), pos, rot, centreLocal, screen, tip, penRot, ray, distance);
            }

            if (hand != null && !screen)
            {
                hand.Haptic(0.35f, 0.05f);
            }
        }

        private void BeginDrag(uint id, bool fresh, string what, RrMakerObject before, RrMakerObject data, Vector3 pos, Quaternion rot, Vector3 centreLocal,
            bool screen, Vector3 tip, Quaternion penRot, Ray ray, float distance)
        {
            var d = new Drag
            {
                Id = id,
                Fresh = fresh,
                What = what,
                Before = before,
                Data = data,
                BaseScale = data.Scale,
                CentreLocal = centreLocal,
                Rotation = rot,
                SentAt = Time.unscaledTime
            };
            d.Centre = pos + rot * centreLocal;
            d.StartCentre = d.Centre;
            if (screen)
            {
                Vector3 grab = distance > 0f ? ray.GetPoint(distance) : d.Centre;
                d.Distance = distance > 0f ? distance : Mathf.Max(0.4f, (d.Centre - ray.origin).magnitude);
                var yaw = YawOf(ray.direction, false);
                d.Offset = Quaternion.Inverse(yaw) * (d.Centre - grab);
                d.YawRot = Quaternion.Inverse(yaw) * rot;
            }
            else
            {
                d.RelCentre = Quaternion.Inverse(penRot) * (d.Centre - tip);
                d.RelRot = Quaternion.Inverse(penRot) * rot;
            }

            foreach (var f in _fits)
            {
                if (f.Id == id && f.Shape)
                {
                    f.Touched = true;
                }
            }

            _drag = d;
        }

        private void ScaleDrag(float f)
        {
            if (_drag == null)
            {
                return;
            }

            float target = _drag.Factor * f;
            var s = _drag.BaseScale * target;
            if (_drag.Data.Kind == RrMakerKind.Light)
            {
                s.z = 1f;
            }

            float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            if (max > MaxScale || min < MinScale)
            {
                return;
            }

            _drag.Factor = target;
            _drag.Dirty = true;
        }

        private void DragTick(IRrMakerRoomHost host, bool screen, Vector3 tip, Quaternion penRot, Ray ray, RrHand hand, float now)
        {
            var d = _drag;
            if (host.Get(d.Id) == null)
            {
                ReleaseBodies();
                _drag = null;
                return;
            }

            if (!screen)
            {
                // either thumbstick, up / down: grow / shrink what is held
                float y = Stick(RrHandSide.Left) + Stick(RrHandSide.Right);
                if (Mathf.Abs(y) > StickDeadZone)
                {
                    ScaleDrag(Mathf.Exp(Mathf.Clamp(y, -1f, 1f) * 1.2f * Time.unscaledDeltaTime));
                }
            }

            Vector3 centre;
            Quaternion rot;
            if (screen)
            {
                var yaw = YawOf(ray.direction, false);
                centre = ray.GetPoint(d.Distance) + yaw * d.Offset;
                rot = yaw * d.YawRot;
            }
            else
            {
                centre = tip + penRot * d.RelCentre;
                rot = penRot * d.RelRot;
            }

            if (Snap)
            {
                rot = SnapRotation(rot);
            }

            Vector3 pivot = centre - rot * (d.CentreLocal * d.Factor);
            if (Snap)
            {
                var snapped = SnapPosition(pivot);
                if ((snapped - d.LastSnap).sqrMagnitude > 1e-6f)
                {
                    d.LastSnap = snapped;
                    if (now - _snapSoundAt > 0.08f)
                    {
                        _snapSoundAt = now;
                        RrSound.Play(SnapClip, snapped, 0.12f);
                    }
                }

                pivot = snapped;
            }

            if (!d.Moved && (centre - d.StartCentre).sqrMagnitude > 0.02f * 0.02f)
            {
                d.Moved = true;
            }

            d.Centre = pivot + rot * (d.CentreLocal * d.Factor);
            d.Rotation = rot;
            var go = host.Find(d.Id);
            if (go != null)
            {
                HoldBodies(go);
                go.transform.SetPositionAndRotation(pivot, rot);
                if (Mathf.Abs(d.Factor - d.AppliedFactor) > 1e-5f && d.Data.Kind != RrMakerKind.Light)
                {
                    go.transform.localScale *= d.Factor / d.AppliedFactor;
                    d.AppliedFactor = d.Factor;
                }
            }
            else
            {
                d.AppliedFactor = d.Factor;
            }

            var p = ToData(pivot);
            var r = RotToData(rot);
            var s = Scaled(d.Data.Kind, d.BaseScale, d.Factor);
            if ((p - d.Data.Position).sqrMagnitude > 1e-8f || Quaternion.Angle(r, d.Data.Rotation) > 0.05f || (s - d.Data.Scale).sqrMagnitude > 1e-8f)
            {
                d.Data.Position = p;
                d.Data.Rotation = r;
                d.Data.Scale = s;
                d.Dirty = true;
            }

            if (d.Dirty && now - d.SentAt >= SendEvery)
            {
                d.SentAt = now;
                d.Dirty = false;
                host.Update(d.Data.Clone());
            }
        }

        /// <summary>
        /// A physical object being moved by the pen must not fall or be pushed meanwhile: its rigidbodies are made
        /// kinematic until it is let go (then the room's physics takes it again, at rest).
        /// </summary>
        private void HoldBodies(GameObject go)
        {
            if (go == _heldGo)
            {
                return;
            }

            ReleaseBodies();
            _heldGo = go;
            go.GetComponentsInChildren(true, _bodyScratch);
            foreach (var rb in _bodyScratch)
            {
                if (rb != null && !rb.isKinematic)
                {
                    rb.isKinematic = true;
                    _heldBodies.Add(rb);
                }
            }

            _bodyScratch.Clear();
        }

        private void ReleaseBodies()
        {
            foreach (var rb in _heldBodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.WakeUp();
                }
            }

            _heldBodies.Clear();
            _heldGo = null;
        }

        private void EndDrag(bool commit)
        {
            ReleaseBodies();
            var d = _drag;
            _drag = null;
            var host = Host;
            if (d == null || host == null || host.Get(d.Id) == null)
            {
                return;
            }

            // a tapped clone lands beside its original instead of inside it
            if (d.Fresh && d.What == "Clone" && !d.Moved && commit)
            {
                var go = host.Find(d.Id);
                Bounds b;
                var cam = RrPlayer.Camera;
                Vector3 right = cam != null ? cam.transform.right : Vector3.right;
                right.y = 0f;
                right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
                float width = 0.3f;
                if (go != null && RrMakerPenMeasure.WorldBounds(go, out b))
                {
                    var e = b.extents;
                    width = 2f * (Mathf.Abs(right.x) * e.x + Mathf.Abs(right.z) * e.z) + 0.05f;
                }

                var pivot = ToWorld(d.Data.Position) + right * width;
                if (Snap)
                {
                    pivot = SnapPosition(pivot);
                }

                d.Data.Position = ToData(pivot);
                if (go != null)
                {
                    go.transform.position = pivot;
                }
            }

            host.Update(d.Data.Clone());

            // A new prop dragged away from where it was clicked does not settle onto that first floor point any more
            // (it would jump back down to its height once loaded).
            if (d.Moved)
            {
                _fits.RemoveAll(f => f.Id == d.Id && f.Settle);
            }

            if (d.Fresh)
            {
                History.Created(d.Data, d.What);
            }
            else if (d.Moved || (d.Before != null && (d.Before.Scale - d.Data.Scale).sqrMagnitude > 1e-8f) ||
                     (d.Before != null && Quaternion.Angle(d.Before.Rotation, d.Data.Rotation) > 0.05f))
            {
                History.Changed(d.Before, d.Data, d.What);
                Changed++;
            }

            RrSound.Play(PlaceClip, ToWorld(d.Data.Position), 0.35f);
        }

        /// <summary>RMB while dragging: puts it back (a new prop or copy is removed).</summary>
        private void CancelDrag()
        {
            ReleaseBodies();
            var d = _drag;
            _drag = null;
            var host = Host;
            if (d == null || host == null)
            {
                return;
            }

            if (d.Fresh)
            {
                host.Delete(d.Id);
                Made = Mathf.Max(0, Made - 1);
                if (Selected == d.Id)
                {
                    Selected = 0;
                }
            }
            else if (d.Before != null)
            {
                host.Update(d.Before.Clone());
            }

            Toast("Cancelled", false);
        }

        private static float Stick(RrHandSide side)
        {
            var dev = RrXr.Controller(side);
            Vector2 v;
            if (dev.isValid && dev.TryGetFeatureValue(CommonUsages.primary2DAxis, out v))
            {
                return Mathf.Abs(v.y) > StickDeadZone && Mathf.Abs(v.y) > Mathf.Abs(v.x) ? v.y : 0f;
            }

            return 0f;
        }

        // ------------------------------------------------------------------ recolor / delete / physics

        private void Apply(IRrMakerRoomHost host, uint id, RrHand hand)
        {
            var before = host.Get(id);
            if (before == null)
            {
                return;
            }

            var go = host.Find(id);
            Vector3 at = go != null ? go.transform.position : ToWorld(before.Position);
            if (Mode != RrMakerMode.Physics && before.Frozen)
            {
                Denied("Frozen: unfreeze it in Physics");
                return;
            }

            switch (Mode)
            {
                case RrMakerMode.Delete:
                    host.Delete(id);
                    History.Deleted(before, "Delete");
                    Removed++;
                    if (Selected == id)
                    {
                        Selected = 0;
                    }

                    Hover = 0;
                    SetLit(null);
                    RrSound.Play(RrMakerPenTool.DeleteClipPrefix + Random.Range(1, 6), at, 0.4f);
                    break;
                case RrMakerMode.Recolor:
                {
                    var c = RrMakerPenCatalog.Colour32(Colour);
                    if (before.Color.r == c.r && before.Color.g == c.g && before.Color.b == c.b)
                    {
                        return;
                    }

                    var after = before.Clone();
                    after.Color = c;
                    host.Update(after);
                    History.Changed(before, after, "Recolor");
                    Changed++;
                    RrSound.Play(ColourClipPrefix + Random.Range(1, 5), at, 0.4f);
                    break;
                }
                case RrMakerMode.Physics:
                {
                    var after = before.Clone();
                    string said;
                    switch (Brush)
                    {
                        case RrMakerPhysicsBrush.Freeze:
                            after.Frozen = !before.Frozen;
                            said = after.Frozen ? "Frozen" : "Unfrozen";
                            break;
                        case RrMakerPhysicsBrush.Grabbable:
                            after.Grabbable = !before.Grabbable;
                            said = after.Grabbable ? "Grabbable" : "Not grabbable";
                            break;
                        default:
                            after.Physics = (RrMakerPhysics)(byte)Brush;
                            said = RrMakerPenCatalog.BrushNames[(int)Brush];
                            break;
                    }

                    host.Update(after);
                    History.Changed(before, after, said);
                    Changed++;
                    Toast(RrMakerPenCatalog.Describe(after) + ": " + said, false);
                    RrSound.Play(ConfigureClipPrefix + Random.Range(1, 5), at, 0.4f);
                    break;
                }
            }

            if (hand != null && !hand.IsScreenHand)
            {
                hand.Haptic(0.4f, 0.05f);
            }
        }

        private void Denied(string why)
        {
            Toast(why, true);
            RrSound.PlayUi(DeniedClip, 0.5f);
        }

        // ------------------------------------------------------------------ fits (placement corrected once built)

        private void TickFits(IRrMakerRoomHost host, float now)
        {
            for (int i = _fits.Count - 1; i >= 0; i--)
            {
                var f = _fits[i];
                var o = host.Get(f.Id);
                if (o == null || now > f.Until)
                {
                    _fits.RemoveAt(i);
                    continue;
                }

                if (_drag != null && _drag.Id == f.Id)
                {
                    continue;
                }

                var go = host.Find(f.Id);
                if (go == null)
                {
                    continue;
                }

                if (f.Shape)
                {
                    Vector3 pos = go.transform.position;
                    Quaternion rot = go.transform.rotation;
                    Vector3 c, size;
                    if (!RrMakerPenMeasure.Oriented(go, pos, rot, out c, out size))
                    {
                        continue;
                    }

                    _fits.RemoveAt(i);
                    var s = o.Scale;
                    if (Mathf.Abs(s.x) < 1e-4f || Mathf.Abs(s.y) < 1e-4f || Mathf.Abs(s.z) < 1e-4f)
                    {
                        continue;
                    }

                    var lc = Quaternion.Inverse(rot) * (c - pos);
                    var unit = new Bounds(new Vector3(lc.x / s.x, lc.y / s.y, lc.z / s.z), new Vector3(size.x / s.x, size.y / s.y, size.z / s.z));
                    if (unit.size.x < 1e-3f || unit.size.y < 1e-3f || unit.size.z < 1e-3f)
                    {
                        continue;
                    }

                    if (!ShapeUnits.ContainsKey(o.Asset))
                    {
                        RrLog.Info("MakerPen", "shape " + o.Asset + " measured: unit centre " + unit.center.ToString("F3") + " size " + unit.size.ToString("F3"));
                    }

                    ShapeUnits[o.Asset] = unit;
                    if (f.Touched)
                    {
                        continue;
                    }

                    Vector3 pivot, scale;
                    ShapeTransform(o.Asset, f.Centre, f.Rotation, f.Size, out pivot, out scale);
                    var p = ToData(pivot);
                    if ((p - o.Position).sqrMagnitude > 0.002f * 0.002f || (scale - o.Scale).sqrMagnitude > 1e-6f)
                    {
                        o.Position = p;
                        o.Scale = scale;
                        host.Update(o);
                        History.Amend(o.Id, o);
                    }
                }
                else if (f.Settle)
                {
                    Bounds b;
                    if (!RrMakerPenMeasure.WorldBounds(go, out b))
                    {
                        continue;
                    }

                    _fits.RemoveAt(i);
                    float dy = f.SurfaceY - b.min.y;
                    if (Mathf.Abs(dy) > 0.01f && Mathf.Abs(dy) < 5f)
                    {
                        var pivot = go.transform.position + Vector3.up * dy;
                        o.Position = ToData(pivot);
                        host.Update(o);
                        History.Amend(o.Id, o);
                    }
                }
                else
                {
                    _fits.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ visuals, sound, toast

        private void Visuals(IRrMakerRoomHost host, bool screen, Vector3 tip, Ray ray)
        {
            Color modeColour = RrMakerPenCatalog.ModeColour(Mode);
            uint outlined = 0;
            Color outlineColour = modeColour;
            if (_drag != null)
            {
                outlined = _drag.Id;
                outlineColour = HeldColour;
            }
            else if (Hover != 0)
            {
                outlined = Hover;
                var o = host.Get(Hover);
                if (o != null && o.Frozen && Mode != RrMakerMode.Physics)
                {
                    outlineColour = FrozenColour;
                }
                else if (Mode == RrMakerMode.Recolor)
                {
                    outlineColour = RrMakerPenWire.ColourOf(Colour);
                }
            }

            var go = outlined != 0 ? host.Find(outlined) : null;
            Vector3 c, size;
            if (go != null && RrMakerPenMeasure.Oriented(go, go.transform.position, go.transform.rotation, out c, out size))
            {
                _gizmos.Outline(true, c, go.transform.rotation, size, outlineColour, screen ? 0.012f : 0f);
            }
            else if (Selected != 0 && _drag == null && (Mode == RrMakerMode.Select || Mode == RrMakerMode.Shapes || Mode == RrMakerMode.Props) &&
                     (go = host.Find(Selected)) != null && RrMakerPenMeasure.Oriented(go, go.transform.position, go.transform.rotation, out c, out size))
            {
                _gizmos.Outline(true, c, go.transform.rotation, size, new Color(0.85f, 0.85f, 0.85f), screen ? 0.006f : 0.003f);
            }
            else
            {
                _gizmos.Outline(false, Vector3.zero, Quaternion.identity, Vector3.zero, Color.white);
            }

            SetLit(_drag == null && Hover != 0 ? (go != null ? go.transform : null) : null);

            // the laser (VR): in the pointing modes, when not touching
            bool laser = !screen && _drag == null && !_hoverTouched &&
                         (Mode == RrMakerMode.Select || Mode == RrMakerMode.Clone || Mode == RrMakerMode.Recolor || Mode == RrMakerMode.Delete || Mode == RrMakerMode.Physics);
            if (laser)
            {
                Vector3 end = Hover != 0 ? _hoverPoint : ray.origin + ray.direction * (_hoverDistance > 0f ? _hoverDistance : LaserReach);
                _gizmos.Laser(true, ray.origin, end, Hover != 0 ? outlineColour : new Color(modeColour.r, modeColour.g, modeColour.b) * 0.8f);
            }
            else
            {
                _gizmos.Laser(false, Vector3.zero, Vector3.zero, Color.white);
            }

            // where Draw / Shapes / Props will put things
            Color colour = RrMakerPenWire.ColourOf(Colour);
            if (Mode == RrMakerMode.Draw)
            {
                float r = screen ? DrawRadius : (_inkPoints.Count > 0 ? _inkRadius : RrMakerPenTool.MinRadius * 2f);
                _gizmos.Cursor(true, screen ? ray.origin + ray.direction * RrMakerPenTool.ScreenDepth : tip, r * 1.1f, colour);
            }
            else if ((Mode == RrMakerMode.Shapes || Mode == RrMakerMode.Props) && _drag == null && !_shaping)
            {
                if (screen)
                {
                    Vector3 p, n;
                    PlacePoint(ray, out p, out n);
                    _gizmos.Cursor(true, p, Mathf.Clamp((p - ray.origin).magnitude * 0.006f, 0.008f, 0.05f), Mode == RrMakerMode.Shapes ? colour : modeColour);
                }
                else
                {
                    _gizmos.Cursor(true, tip, 0.008f, Mode == RrMakerMode.Shapes ? colour : modeColour);
                }
            }
            else
            {
                _gizmos.Cursor(false, Vector3.zero, 0f, Color.white);
            }
        }

        /// <summary>The hover tint (RrHighlight's property-block brighten) on the object under the pen.</summary>
        private void SetLit(Transform t)
        {
            if (t == _lit)
            {
                return;
            }

            if (_lit != null)
            {
                RrHighlight.Set(_lit, null, false);
            }

            _lit = t;
            if (t != null)
            {
                RrHighlight.Set(t, null, true);
            }
        }

        private void UpdateLoop()
        {
            string clip = null;
            float volume = 0f;
            if (_inkPoints.Count > 0)
            {
                clip = RrMakerPenTool.DrawLoopClip;
                volume = Mathf.Lerp(0.005f, 0.22f, Mathf.Clamp01(_inkSpeed));
            }
            else if (_shaping && _stretched)
            {
                clip = StretchLoopClip;
                volume = 0.12f;
            }

            if (clip != _loopClip && clip != null)
            {
                StopLoop();
                _loopClip = clip;
                _loop = RrSound.Loop(clip, transform, 0f, 15f);
            }

            if (_loop != null)
            {
                _loop.volume = Mathf.MoveTowards(_loop.volume, volume * RrSound.Volume, Time.unscaledDeltaTime * 2f);
                if (clip == null && _loop.volume <= 0.0001f)
                {
                    StopLoop();
                }
            }
        }

        private void StopLoop()
        {
            if (_loop != null)
            {
                RrSound.Stop(_loop);
            }

            _loop = null;
            _loopClip = null;
        }

        public void Toast(string text, bool bad)
        {
            ToastText = text ?? "";
            ToastBad = bad;
            ToastUntil = Time.unscaledTime + (bad ? 3f : 2.5f);
        }

        /// <summary>The mode line of the palette: "Shapes: Cube", "Props: Furniture / Couch" ...</summary>
        public string Readout()
        {
            string s = RrMakerPenCatalog.ModeName(Mode);
            switch (Mode)
            {
                case RrMakerMode.Draw:
                    s += ScreenMode ? ": tube " + (DrawRadius * 200f).ToString("0.0") + " cm" : ": tube";
                    break;
                case RrMakerMode.Shapes:
                    s += ": " + CurrentShape.Label;
                    break;
                case RrMakerMode.Props:
                    var p = CurrentProp;
                    s += ": " + CurrentCategory.Name + " / " + (p != null ? p.Label : "-");
                    break;
                case RrMakerMode.Physics:
                    s += ": " + RrMakerPenCatalog.BrushNames[(int)Brush];
                    break;
            }

            if (Snap)
            {
                s += "   [snap]";
            }

            return s;
        }

        /// <summary>The second line: what is held or pointed at, its physics and flags.</summary>
        public string Info()
        {
            var host = Host;
            if (host == null)
            {
                return "";
            }

            uint id = _drag != null ? _drag.Id : Hover;
            var o = id != 0 ? host.Get(id) : null;
            if (o == null)
            {
                if (_shaping && _stretched)
                {
                    return (_shapeSize.x * 100f).ToString("0") + " x " + (_shapeSize.y * 100f).ToString("0") + " x " + (_shapeSize.z * 100f).ToString("0") + " cm";
                }

                return "";
            }

            string s = RrMakerPenCatalog.Describe(o) + " - " + o.Physics;
            if (o.Frozen)
            {
                s += ", frozen";
            }

            if (o.Grabbable)
            {
                s += ", grabbable";
            }

            if (_drag != null && Mathf.Abs(_drag.Factor - 1f) > 0.001f)
            {
                s += "   x" + _drag.Factor.ToString("0.00");
            }

            return s;
        }

        /// <summary>Clears the statics (world shutdown).</summary>
        public static void Shutdown()
        {
            ShapeUnits.Clear();
        }
    }
}
