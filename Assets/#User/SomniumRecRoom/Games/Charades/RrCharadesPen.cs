using System;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// Drawing with Rec Room's Charades Pen (container "cb9", rr_party.md §1.3) while the local player holds it.
    ///
    /// VR: the tube comes out of the pen's <c>ExtrusionPoint</c> (pen-local (0, 0.036, 0.152)) while the trigger is
    /// pulled; the pull sets the thickness (the 2020 "trigger-sensitive tube size"), 4-22 mm radius. A (the free
    /// primary button) undoes the last stroke.
    /// Screen mode: Rec Room's "Draw Mode" - the ink goes where the cursor points, on a sphere
    /// <see cref="ScreenDepth"/> m from the eye, and the tube thins with cursor speed as
    /// <c>CharadesPenScreenToolBehaviorConfig</c> says (1.0 at rest to 0.15 at 5 viewport/s). Left mouse draws;
    /// a click that lands on a world button presses it instead (the podium's Correct/Skip/colours), Z undoes.
    ///
    /// The Maker Pen's tube loop <c>sfx_ui_maker_pen_draw_tube_ribbon_loop_01</c> follows the draw speed
    /// (volume curve 0.005 -> 0.22 over 0..1 m/s, MakerPenAudioContent); undo plays one of the five
    /// <c>sfx_ui_maker_pen_created_object_delete_0N</c> clips.
    /// </summary>
    public sealed class RrCharadesPen : MonoBehaviour
    {
        public const string DrawLoopClip = "sfx_ui_maker_pen_draw_tube_ribbon_loop_01";
        public const string DeleteClipPrefix = "sfx_ui_maker_pen_created_object_delete_0";
        public const float ScreenDepth = 1.6f;
        public const float MinRadius = 0.004f;
        public const float MaxRadius = 0.022f;
        public const float ScreenRadius = 0.016f;

        public RrGrabbable Grab;
        public Transform Tip;
        public RrCharadesInk Ink;

        /// <summary>The room says whether the local player may draw now (performer, drawing phase).</summary>
        public Func<bool> CanDraw;

        /// <summary>Palette index of the ink (see <see cref="RrCharadesInk.Palette"/>).</summary>
        public byte Colour = 2;

        private GameObject _cursor;
        private Renderer _cursorRenderer;
        private byte _cursorColour = 255;
        private AudioSource _loop;
        private bool _drawing;
        private bool _suppress;
        private Vector3 _lastPoint;
        private Vector3 _lastDir;
        private float _speed;
        private float _screenDepth = ScreenDepth;

        public int Strokes { get; private set; }

        private void Update()
        {
            bool held = Grab != null && Grab.IsHeld && Grab.Holder != null;
            bool allowed = held && Ink != null && Ink.LocalPerformer && (CanDraw == null || CanDraw());
            if (!allowed)
            {
                StopDrawing();
                ShowCursor(false, Vector3.zero, 0f);
                _suppress = false;
                return;
            }

            var hand = Grab.Holder;
            bool screen = hand.IsScreenHand;
            Vector3 point;
            bool want;
            float radius;
            if (screen)
            {
                var ray = RrInput.AimRay;
                point = ray.origin + ray.direction * _screenDepth;
                want = RrInput.UseHeld;
                if (RrInput.UseDown && RrPressable.PressAlong(ray, RrPressable.ScreenReach, "pen"))
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
                if (RrInput.ReloadDown)
                {
                    Undo();
                }
            }
            else
            {
                point = Tip != null ? Tip.position : transform.TransformPoint(new Vector3(0f, 0.036f, 0.152f));
                float pull = Mathf.Clamp01((hand.Trigger - 0.08f) / 0.92f);
                want = hand.Trigger > 0.08f || hand.TriggerHeld;
                radius = Mathf.Lerp(MinRadius, MaxRadius, pull);
                if (hand.PrimaryDown)
                {
                    Undo();
                }
            }

            if (!want)
            {
                _suppress = false;
            }

            ShowCursor(true, point, radius);
            if (want && !_suppress)
            {
                if (!_drawing)
                {
                    _drawing = true;
                    Strokes++;
                    Ink.BeginStroke(Colour, point, radius);
                    _lastPoint = point;
                    _speed = 0f;
                }
                else
                {
                    Ink.AddPoint(point, radius);
                }

                if (Time.deltaTime > 0f)
                {
                    _speed = Mathf.Lerp(_speed, (point - _lastPoint).magnitude / Time.deltaTime, 0.3f);
                }

                _lastPoint = point;
                if (hand != null && !screen)
                {
                    hand.Haptic(0.05f + 0.1f * Mathf.Clamp01(_speed), 0.02f);
                }
            }
            else
            {
                StopDrawing();
            }

            UpdateLoop();
        }

        /// <summary>Removes the last stroke with Rec Room's delete sound.</summary>
        public void Undo()
        {
            if (Ink != null && Ink.Undo())
            {
                RrSound.Play(DeleteClipPrefix + UnityEngine.Random.Range(1, 6), transform.position, 0.35f);
            }
        }

        private void StopDrawing()
        {
            if (_drawing)
            {
                _drawing = false;
                if (Ink != null)
                {
                    Ink.EndStroke();
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

        /// <summary>A small ball of the current ink colour where the ink will come out (local player only).</summary>
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
                _cursor.name = "Charades Pen Cursor";
                Destroy(_cursor.GetComponent<Collider>());
                _cursor.transform.SetParent(transform.parent, false);
                _cursorRenderer = _cursor.GetComponent<Renderer>();
                _cursorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (_cursorColour != Colour)
            {
                _cursorColour = Colour;
                _cursorRenderer.sharedMaterial = RrLook.Emissive(RrCharadesInk.Palette[Colour % RrCharadesInk.Palette.Length], 0.6f);
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

            if (_loop != null)
            {
                RrSound.Stop(_loop);
            }
        }
    }
}
