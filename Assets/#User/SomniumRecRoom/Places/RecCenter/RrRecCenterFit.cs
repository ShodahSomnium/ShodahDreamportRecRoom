using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Stands our interactives on Rec Room's own cached building (<see cref="RrRecCenterCache"/>): the layout was
    /// measured from this very scene, but from boxes and door signs, so each item is checked against the real
    /// colliders and moved onto the real floor, counter or wall, and every move is logged (harness 2026-09-24).
    /// <list type="bullet">
    /// <item>Floors: a ray down from 0.6 m above the planned spot. Scenery moves at most 0.4 m on a floor (the gym is
    /// 0.25 above the hall, the balcony's floor 3.32 where the layout says 3.10) and 0.25 m on a counter (the kiosk
    /// counter is 1.19, the cafe counter 0.77). Loose props are fitted last, when everything stands, onto whatever is
    /// under them (the building or our own props: a paddle on our ping-pong table, a plate on the plate below it, a
    /// frisbee on the building's side table) up to 3 m down.</item>
    /// <item>Walls: hoops hang on the wall behind them, a ray from 1 m in front back toward the wall,
    /// accepted within 1 m. Doors go onto the wall behind them within 0.6 m, but never behind the plane of the
    /// building's own door sign above them: at the MakerWorld door the collider stands 0.3 m behind the painted wall,
    /// and the door vanished into it.</item>
    /// </list>
    /// </summary>
    internal sealed class RrRecCenterFit
    {
        public const float SceneryTolerance = 0.4f;
        public const float CounterTolerance = 0.25f;
        public const float LooseDrop = 3.0f;
        public const float WallTolerance = 0.6f;

        private readonly Transform _room;
        private readonly Transform _frame;
        private readonly List<string> _moved = new List<string>();
        private readonly List<string> _unfit = new List<string>();
        private int _checked;
        private int _fine;
        private float _maxMove;

        public RrRecCenterFit(Transform room, Transform frame)
        {
            _room = room;
            _frame = frame;
        }

        /// <summary>
        /// First hit along a ray on the cached building (or, with ours, on anything of the room but self); triggers
        /// are ignored.
        /// </summary>
        public bool Cast(Vector3 from, Vector3 dir, float max, out RaycastHit hit, bool ours = false, Transform self = null)
        {
            var hits = Physics.RaycastAll(from, dir, max, (1 << RrLayers.Environment) | (1 << RrLayers.Grabbable) | (1 << RrLayers.Default), QueryTriggerInteraction.Ignore);
            hit = default(RaycastHit);
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.distance >= best || h.collider == null)
                {
                    continue;
                }

                var t = h.collider.transform;
                if (self != null && t.IsChildOf(self))
                {
                    continue;
                }

                bool building = t.IsChildOf(_frame);
                if (!building && !(ours && t.IsChildOf(_room)))
                {
                    continue;
                }

                if (building && h.collider.gameObject.layer != RrLayers.Environment)
                {
                    continue;
                }

                best = h.distance;
                hit = h;
                found = true;
            }

            return found;
        }

        /// <summary>The real floor's height (room frame) under a room-frame spot, searching from above it.</summary>
        public bool FloorUnder(Vector3 local, float above, float depth, out float y, bool ours = false, Transform self = null)
        {
            RaycastHit hit;
            y = local.y;
            if (!Cast(_room.TransformPoint(local + Vector3.up * above), -_room.up, above + depth, out hit, ours, self))
            {
                return false;
            }

            y = _room.InverseTransformPoint(hit.point).y;
            return true;
        }

        /// <summary>
        /// Moves a spawned item onto the surface under it (Snap items) and records the offset. Loose props (loose true)
        /// land on the building or on our props; scenery only on the building, within tolerance.
        /// </summary>
        public void Floor(RrInstance inst, RrRcItem item, float tolerance, bool loose)
        {
            if (item.Place != RrRcPlace.Snap || inst == null)
            {
                return;
            }

            _checked++;
            var b = inst.WorldBounds();
            var centre = b.size.sqrMagnitude > 1e-6f ? _room.InverseTransformPoint(b.center) : item.Pos;
            float bottom = b.size.sqrMagnitude > 1e-6f ? _room.InverseTransformPoint(b.min).y : item.Pos.y;
            var spot = new Vector3(centre.x, bottom, centre.z);
            float y;
            if (!FloorUnder(spot, 0.6f, loose ? LooseDrop : tolerance, out y, loose, inst.transform))
            {
                _unfit.Add(Name(item) + " no surface under " + Fmt(spot));
                return;
            }

            float dy = y - bottom;
            if (!loose && Mathf.Abs(dy) > tolerance)
            {
                _unfit.Add(Name(item) + " surface " + dy.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " m away (kept)");
                return;
            }

            // Loose props sit 4 mm up and settle.
            float lift = dy + (loose ? 0.004f : 0f);
            if (Mathf.Abs(lift) < 0.01f)
            {
                _fine++;
                return;
            }

            inst.transform.position += _room.up * lift;
            Moved(Name(item), new Vector3(0f, lift, 0f));
        }

        /// <summary>
        /// Hangs a transform on the wall behind it (its -forward in the room): the wall face plus gap, within tolerance
        /// of where it is. Returns the move along its forward (0 when not moved).
        /// </summary>
        public float Wall(Transform t, string name, float gap, float height, float tolerance, bool apply = true)
        {
            _checked++;
            var fwd = Forward(t);
            var at = _room.InverseTransformPoint(t.position);
            var from = new Vector3(at.x, height, at.z) + fwd * 1.0f;
            RaycastHit hit;
            if (!Cast(_room.TransformPoint(from), _room.TransformDirection(-fwd), 1.0f + tolerance + 0.5f, out hit))
            {
                _unfit.Add(name + " no wall behind " + Fmt(at));
                return float.NaN;
            }

            var face = _room.InverseTransformPoint(hit.point);
            float along = Vector3.Dot(face - at, fwd) + gap;
            if (Mathf.Abs(along) > tolerance)
            {
                _unfit.Add(name + " wall " + along.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " m away (kept)");
                return float.NaN;
            }

            if (apply)
            {
                Along(t, name, along);
            }

            return along;
        }

        /// <summary>
        /// A door: onto the wall behind it (6 cm off the collider face), but not behind the plane of the building's own
        /// sign for it (sign may be null: the Magic Door has none), then onto the real floor in front of it.
        /// </summary>
        public void Door(Transform door, string name, Transform sign, bool useWall)
        {
            var fwd = Forward(door);
            var at = _room.InverseTransformPoint(door.position);
            float along = useWall ? Wall(door, name, 0.06f, 1.2f, WallTolerance, false) : float.NaN;
            if (sign != null)
            {
                float bySign = Vector3.Dot(Centre(sign) - at, fwd) + 0.04f;
                if (Mathf.Abs(bySign) <= WallTolerance && (float.IsNaN(along) || bySign > along))
                {
                    along = bySign;
                    name += " (by its sign)";
                }
            }

            if (!float.IsNaN(along))
            {
                Along(door, name, along);
            }

            at = _room.InverseTransformPoint(door.position);
            float y;
            _checked++;
            if (FloorUnder(at + fwd * 0.5f, 1.0f, 1.0f, out y))
            {
                float dy = y - at.y;
                if (Mathf.Abs(dy) >= 0.01f && Mathf.Abs(dy) <= SceneryTolerance)
                {
                    door.position += _room.up * dy;
                    Moved(name + " floor", new Vector3(0f, dy, 0f));
                }
                else if (Mathf.Abs(dy) > SceneryTolerance)
                {
                    _unfit.Add(name + " floor " + dy.ToString("+0.00;-0.00", CultureInfo.InvariantCulture) + " m away (kept)");
                }
                else
                {
                    _fine++;
                }
            }
            else
            {
                _unfit.Add(name + " no floor in front");
            }
        }

        private void Along(Transform t, string name, float along)
        {
            var move = Forward(t) * along;
            if (move.magnitude < 0.01f)
            {
                _fine++;
                return;
            }

            t.position += _room.TransformVector(move);
            Moved(name, move);
        }

        private Vector3 Forward(Transform t)
        {
            var fwd = _room.InverseTransformDirection(t.forward);
            fwd.y = 0f;
            return fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
        }

        public void Moved(string name, Vector3 move)
        {
            _maxMove = Mathf.Max(_maxMove, move.magnitude);
            _moved.Add(name + " " + Fmt(move));
        }

        public void Note(string text)
        {
            _unfit.Add(text);
        }

        /// <summary>One log line with every move and every item that could not be fitted.</summary>
        public void Log(string what)
        {
            var sb = new StringBuilder();
            sb.Append("fitted ").Append(what).Append(" to Rec Room's building: ").Append(_checked).Append(" checks, ").Append(_fine).Append(" already right, ")
              .Append(_moved.Count).Append(" moved (largest ").Append(_maxMove.ToString("0.00", CultureInfo.InvariantCulture)).Append(" m)");
            if (_moved.Count > 0)
            {
                sb.Append(": ").Append(string.Join(", ", _moved.ToArray()));
            }

            if (_unfit.Count > 0)
            {
                sb.Append("; not fitted: ").Append(string.Join(", ", _unfit.ToArray()));
            }

            RrLog.Info("RecCenter", sb.ToString());
            _moved.Clear();
            _unfit.Clear();
            _checked = 0;
            _fine = 0;
            _maxMove = 0f;
        }

        /// <summary>
        /// The cached building's object whose name starts with prefix nearest to a room-frame spot (within radius,
        /// horizontal distance of its renderers' centre); the topmost of a run of same-named objects.
        /// </summary>
        public Transform Nearest(string prefix, Vector3 local, float radius, HashSet<Transform> skip, out Vector3 centre)
        {
            Transform best = null;
            centre = Vector3.zero;
            float bestD = radius;
            foreach (var t in _frame.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(prefix, System.StringComparison.Ordinal) || !t.gameObject.activeInHierarchy || (skip != null && skip.Contains(t)))
                {
                    continue;
                }

                if (t.parent != null && t.parent.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var c = Centre(t);
                float d = new Vector2(c.x - local.x, c.z - local.z).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                    centre = c;
                }
            }

            return best;
        }

        /// <summary>Room-frame centre of a transform's renderers (its position if it has none).</summary>
        public Vector3 Centre(Transform t)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            return _room.InverseTransformPoint(any ? b.center : t.position);
        }

        /// <summary>Room-frame yaw of a transform.</summary>
        public float Yaw(Transform t)
        {
            return (Quaternion.Inverse(_room.rotation) * t.rotation).eulerAngles.y;
        }

        private static string Name(RrRcItem item)
        {
            return item.Role ?? item.Prefab;
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.00", CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.00", CultureInfo.InvariantCulture) + ", "
                + v.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }
    }
}
