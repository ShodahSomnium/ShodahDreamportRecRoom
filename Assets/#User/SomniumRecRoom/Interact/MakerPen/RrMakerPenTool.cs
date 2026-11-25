using SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// Drawing with the local player's Maker Pen (on the spawned <c>(MakerPen)</c>) into the current place's
    /// <see cref="RrMakerPenInk"/>. In a maker room the player may build in (<see cref="RrMakerEdits.CanEdit"/>) the
    /// frame goes to <see cref="RrMakerPenBuild"/> instead (Rec Room's build modes; ink there becomes room objects).
    ///
    /// VR: the tube comes out of the pen's <c>ExtrusionPoint</c> while the trigger is pulled, the pull setting the
    /// thickness (3-20 mm radius); A/X undoes the last stroke; the palette above the other hand is used by
    /// touching it with the tip (colour, Erase, Undo, Clear). In erase mode the trigger deletes any of your strokes
    /// the tip touches.
    /// Screen mode: the ink goes where the crosshair points, at arm's length (<see cref="ScreenDepth"/> m), thinning
    /// with cursor speed as the Charades Pen's screen config says; left mouse draws (a click on a world button
    /// presses it instead), Z undoes, right mouse (or left mouse in erase mode) deletes your stroke under the
    /// crosshair, 1 / 2 change colour, 3 toggles erase mode, 4 clears your ink.
    ///
    /// Sounds are the install's Maker Pen clips: the tube loop <c>sfx_ui_maker_pen_draw_tube_ribbon_loop_01</c>
    /// following the draw speed (0.005 -> 0.22 over 0..1 m/s, MakerPenAudioContent) and one of the five
    /// <c>sfx_ui_maker_pen_created_object_delete_0N</c> on undo / erase / clear. Let go, the pen hovers; left
    /// alone for <see cref="IdleTimeout"/> s, or when its room is left, it is put away.
    /// </summary>
    public sealed class RrMakerPenTool : MonoBehaviour
    {
        public const string DrawLoopClip = "sfx_ui_maker_pen_draw_tube_ribbon_loop_01";
        public const string DeleteClipPrefix = "sfx_ui_maker_pen_created_object_delete_0";
        public const float ScreenDepth = 0.7f;
        public const float MinRadius = 0.003f;
        public const float MaxRadius = 0.02f;
        public const float ScreenRadius = 0.01f;
        public const float EraseTolerance = 0.015f;
        public const float ScreenEraseReach = 8f;
        public const float IdleTimeout = 60f;
        public const float PoseEvery = 0.2f;
        public static readonly Vector3 TipFallback = new Vector3(0f, 0.036f, 0.152f);
        private static readonly Color EraserColour = new Color(1f, 0.55f, 0.7f, 0.45f);

        public RrGrabbable Grab;
        public Transform Tip;

        /// <summary>The room the pen was given in; leaving it puts the pen away.</summary>
        public RrRoom Room;

        /// <summary>Palette index of the ink (<see cref="RrMakerPenWire.Palette"/>).</summary>
        public byte Colour = RrMakerPenWire.DefaultColour;

        /// <summary>Erase mode: the trigger / click deletes your strokes instead of drawing.</summary>
        public bool Eraser;

        public int Strokes { get; private set; }
        public int Erased { get; private set; }

        private RrMakerPenPalette _palette;
        private RrMakerPenBuild _build;
        private GameObject _cursor;
        private Renderer _cursorRenderer;
        private int _cursorLook = -1;
        private AudioSource _loop;
        private bool _drawing;
        private bool _suppress;
        private bool _wasHeld;
        private bool _gone;
        private Vector3 _lastPoint;
        private Vector3 _lastDir;
        private float _speed;
        private float _poseAt;
        private float _releasedAt;

        /// <summary>Where the palette and the cursor live: the room (the pen itself is under the hand while held).</summary>
        private Transform Home
        {
            get { return Room != null ? Room.transform : transform.parent; }
        }

        private static RrMakerPenInk Ink
        {
            get
            {
                var s = RrMakerPenService.Instance;
                return s != null ? s.Ink : null;
            }
        }

        private void Start()
        {
            _releasedAt = Time.unscaledTime;
            _palette = RrMakerPenPalette.Create(Home, this);
        }

        private void Update()
        {
            if (_gone)
            {
                return;
            }

            if (Room == null || RrTravel.CurrentRoom != Room || transform.position.y < -50f)
            {
                PutAway(false);
                return;
            }

            float now = Time.unscaledTime;
            bool held = Grab != null && Grab.IsHeld && Grab.Holder != null;
            if (held != _wasHeld)
            {
                _wasHeld = held;
                if (!held)
                {
                    _releasedAt = now;
                    SendPen(false);
                }
                else
                {
                    PoseSoon();
                }
            }

            // In a maker room the player may build in, the pen is Rec Room's build tool (RrMakerPenBuild).
            if (held && RrMakerEdits.CanEdit)
            {
                if (_build == null)
                {
                    _build = gameObject.AddComponent<RrMakerPenBuild>();
                    _build.Tool = this;
                    RrLog.Info("MakerPen", "build tools on (maker room" + (RrMakerEdits.Host.Info != null ? " '" + RrMakerEdits.Host.Info.Name + "'" : "") + ")");
                }

                StopDrawing();
                ShowCursor(false, Vector3.zero, 0f);
                if (_palette != null)
                {
                    _palette.Hide();
                }

                _build.Tick(Grab.Holder);
                if (now >= _poseAt)
                {
                    _poseAt = now + PoseEvery;
                    SendPen(true);
                }

                return;
            }

            if (_build != null)
            {
                _build.Idle();
            }

            var ink = Ink;
            if (!held || ink == null)
            {
                StopDrawing();
                ShowCursor(false, Vector3.zero, 0f);
                if (_palette != null)
                {
                    _palette.Hide();
                }

                _suppress = false;
                if (!held && now - _releasedAt > IdleTimeout)
                {
                    RrLog.Info("MakerPen", "the Maker Pen was left alone for " + IdleTimeout + " s: put away");
                    PutAway(true);
                }

                return;
            }

            var hand = Grab.Holder;
            bool screen = hand.IsScreenHand;
            if (_palette != null)
            {
                _palette.Show(screen, hand);
            }

            Vector3 point;
            bool want;
            float radius;
            if (screen)
            {
                var ray = RrInput.AimRay;
                point = ray.origin + ray.direction * ScreenDepth;
                want = RrInput.UseHeld;
                if (RrInput.UseDown && RrPressable.PressAlong(ray, RrPressable.ScreenReach, "maker pen"))
                {
                    _suppress = true;
                }

                var cam = RrPlayer.Camera;
                float fov = cam != null ? cam.fieldOfView : 60f;
                float viewportSpeed = _lastDir.sqrMagnitude > 0.5f && Time.deltaTime > 0f
                    ? Vector3.Angle(_lastDir, ray.direction) / Mathf.Max(1f, fov) / Time.deltaTime
                    : 0f;
                _lastDir = ray.direction;
                radius = Mathf.Max(MinRadius, ScreenRadius * Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(viewportSpeed / 5f)));
                ScreenKeys(ray);
                if (Eraser)
                {
                    if (RrInput.UseDown && !_suppress)
                    {
                        EraseAlong(ray);
                    }

                    want = false;
                }
            }
            else
            {
                point = Tip != null ? Tip.position : transform.TransformPoint(TipFallback);
                float pull = Mathf.Clamp01((hand.Trigger - 0.08f) / 0.92f);
                want = hand.Trigger > 0.08f || hand.TriggerHeld;
                radius = Mathf.Lerp(MinRadius, MaxRadius, pull);
                if (hand.PrimaryDown)
                {
                    Undo();
                }

                if (_palette != null && _palette.Touch(point, hand))
                {
                    want = false;
                }

                if (Eraser)
                {
                    if (want)
                    {
                        EraseNear(point, hand);
                    }

                    want = false;
                }
            }

            if (!want)
            {
                _suppress = false;
            }

            ShowCursor(true, point, Eraser ? EraseTolerance : radius);
            if (want && !_suppress)
            {
                if (!_drawing)
                {
                    if (ink.BeginStroke(Colour, point, radius))
                    {
                        _drawing = true;
                        Strokes++;
                        _lastPoint = point;
                        _speed = 0f;
                    }
                }
                else
                {
                    ink.AddPoint(point, radius);
                }

                if (_drawing && Time.deltaTime > 0f)
                {
                    _speed = Mathf.Lerp(_speed, (point - _lastPoint).magnitude / Time.deltaTime, 0.3f);
                }

                _lastPoint = point;
                if (_drawing && !screen)
                {
                    hand.Haptic(0.05f + 0.1f * Mathf.Clamp01(_speed), 0.02f);
                }
            }
            else
            {
                StopDrawing();
            }

            UpdateLoop();
            if (now >= _poseAt)
            {
                _poseAt = now + PoseEvery;
                SendPen(true);
            }
        }

        private void ScreenKeys(Ray ray)
        {
            if (RrInput.ReloadDown)
            {
                Undo();
            }

            if (RrInput.AltDown)
            {
                EraseAlong(ray);
            }

            switch (RrInput.SlotDown)
            {
                case 1:
                    Cycle(-1);
                    break;
                case 2:
                    Cycle(1);
                    break;
                case 3:
                    ToggleEraser();
                    break;
                case 4:
                    ClearMine();
                    break;
            }
        }

        // ------------------------------------------------------------------ actions (palette and keys)

        public void SetColour(byte colour)
        {
            Colour = (byte)(colour % RrMakerPenWire.Palette.Length);
            Eraser = false;
        }

        public void Cycle(int step)
        {
            int n = RrMakerPenWire.Palette.Length;
            SetColour((byte)(((Colour + step) % n + n) % n));
        }

        public void ToggleEraser()
        {
            StopDrawing();
            Eraser = !Eraser;
        }

        /// <summary>Removes your last stroke with Rec Room's delete sound.</summary>
        public void Undo()
        {
            StopDrawing();
            var ink = Ink;
            if (ink != null && ink.UndoMine())
            {
                DeleteSound();
            }
        }

        /// <summary>Removes all your strokes in this place.</summary>
        public void ClearMine()
        {
            StopDrawing();
            var ink = Ink;
            int n = ink != null ? ink.ClearMine() : 0;
            if (n > 0)
            {
                DeleteSound();
                RrLog.Info("MakerPen", "cleared my " + n + " strokes");
            }
        }

        private void EraseNear(Vector3 point, RrHand hand)
        {
            var ink = Ink;
            int group = ink != null ? ink.FindMineNear(point, EraseTolerance) : 0;
            if (group > 0 && ink.DeleteMine(group))
            {
                Erased++;
                DeleteSound();
                if (hand != null)
                {
                    hand.Haptic(0.35f, 0.05f);
                }
            }
        }

        private void EraseAlong(Ray ray)
        {
            var ink = Ink;
            int group = ink != null ? ink.FindMineAlong(ray, ScreenEraseReach) : 0;
            if (group > 0 && ink.DeleteMine(group))
            {
                Erased++;
                DeleteSound();
            }
        }

        private void DeleteSound()
        {
            RrSound.Play(DeleteClipPrefix + Random.Range(1, 6), transform.position, 0.35f);
        }

        /// <summary>Sends the pen's pose at the next frame (someone asked for the place's ink).</summary>
        public void PoseSoon()
        {
            _poseAt = 0f;
        }

        private void SendPen(bool held)
        {
            var s = RrMakerPenService.Instance;
            if (s != null)
            {
                s.SendPen(held, transform);
            }
        }

        /// <summary>Takes the pen out of the hand and removes it.</summary>
        public void PutAway(bool sound)
        {
            if (_gone)
            {
                return;
            }

            _gone = true;
            StopDrawing();
            if (Grab != null && Grab.IsHeld)
            {
                Grab.ForceRelease();
            }

            SendPen(false);
            if (sound)
            {
                DeleteSound();
            }

            RrMakerPen.Forget(this);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ drawing state, sound, cursor

        private void StopDrawing()
        {
            if (_drawing)
            {
                _drawing = false;
                var ink = Ink;
                if (ink != null)
                {
                    ink.EndStroke();
                }
            }

            _speed = 0f;
            UpdateLoop();
        }

        private void UpdateLoop()
        {
            float volume = _drawing ? Mathf.Lerp(0.005f, 0.22f, Mathf.Clamp01(_speed)) : 0f;
            if (_loop == null)
            {
                if (!_drawing)
                {
                    return;
                }

                _loop = RrSound.Loop(DrawLoopClip, transform, 0f, 15f);
                if (_loop == null)
                {
                    return;
                }
            }

            _loop.volume = Mathf.MoveTowards(_loop.volume, volume * RrSound.Volume, Time.deltaTime * 2f);
        }

        /// <summary>A small ball of the ink colour where the ink comes out (pink glass in erase mode); local only.</summary>
        private void ShowCursor(bool on, Vector3 point, float radius)
        {
            if (!on)
            {
                if (_cursor != null && _cursor.activeSelf)
                {
                    _cursor.SetActive(false);
                }

                return;
            }

            if (_cursor == null)
            {
                _cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _cursor.name = "Maker Pen Cursor";
                Destroy(_cursor.GetComponent<Collider>());
                _cursor.transform.SetParent(Home, false);
                _cursorRenderer = _cursor.GetComponent<Renderer>();
                _cursorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            int look = Eraser ? 1000 : Colour;
            if (_cursorLook != look)
            {
                _cursorLook = look;
                _cursorRenderer.sharedMaterial = Eraser ? RrLook.Glass(EraserColour) : RrLook.Emissive(RrMakerPenWire.ColourOf(Colour), 0.6f);
            }

            if (!_cursor.activeSelf)
            {
                _cursor.SetActive(true);
            }

            _cursor.transform.position = point;
            _cursor.transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2.2f);
        }

        private void OnDisable()
        {
            StopDrawing();
            ShowCursor(false, Vector3.zero, 0f);
        }

        private void OnDestroy()
        {
            if (_cursor != null)
            {
                Destroy(_cursor);
            }

            if (_palette != null)
            {
                Destroy(_palette.gameObject);
            }

            if (_loop != null)
            {
                RrSound.Stop(_loop);
            }

            RrMakerPen.Forget(this);
        }
    }
}
