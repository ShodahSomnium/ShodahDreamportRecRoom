using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// How Rec Room's screen mode (desktop, mobile) throws a held tool: the tool's
    /// Tool.screenSettings.ScreenBehaviorConfig, a ScriptableObject of the Throw/Drink/Eat...ToolBehaviorConfig family.
    ///
    /// Measured values (rr_dorm_core.md §4.1.3): the generic ThrowToolBehaviorConfig throws at 8 m/s toward the
    /// gaze target, with a pitch curve that is almost the identity but lifts a level throw by about 15 degrees
    /// (keys -90 -> -90 slope 1.317 and 90 -> 90 slope 0.653); BasketballThrowToolBehaviorConfig throws at 12 m/s
    /// and turns a level gaze into a 70 degree lob at 0.593 of the speed (7.1 m/s: 3.3 m away, 2.3 m high), a
    /// 45 degree gaze into a 0.988 throw. The drink and eat configs only carry a speed (12 and 5 m/s).
    ///
    /// Reading of the fields (throwTowardsGazeTarget, transformGazePitchWithCurves,
    /// throwTowardsGazeTargetCompensatingForGravity): the throw heads for the point under the crosshair, the
    /// pitch of that heading is the "gaze pitch" fed to the curves, and the curves give the launch pitch and the
    /// fraction of throwSpeed. Gravity compensation (0 in every config seen) aims the lower ballistic arc at the
    /// target instead.
    /// </summary>
    public sealed class RrScreenThrow
    {
        /// <summary>The config's name ("BasketballThrowToolBehaviorConfig"), for the log.</summary>
        public string Name = "ThrowToolBehaviorConfig";

        public float Speed = 8f;

        /// <summary>Spin given with the throw, degrees per second (throwAngularSpeed: 360 generic, 180 basketball).</summary>
        public float AngularSpeed = 360f;

        public bool TowardsGazeTarget = true;
        public bool CompensateGravity;
        public bool UseCurves = true;
        public AnimationCurve PitchByGazePitch;
        public AnimationCurve VelocityByGazePitch;

        /// <summary>How far the crosshair's target is taken when the ray hits nothing.</summary>
        public float FarTarget = 30f;

        /// <summary>Rec Room's generic ThrowToolBehaviorConfig (bundle 1df8e0ef..., pathId -6877966307972784836).</summary>
        public static RrScreenThrow Generic()
        {
            var t = new RrScreenThrow();
            t.PitchByGazePitch = new AnimationCurve(new Keyframe(-90f, -90f, 1.31665f, 1.31665f), new Keyframe(90f, 90f, 0.652806f, 0.652806f));
            t.VelocityByGazePitch = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
            return t;
        }

        /// <summary>A screen behaviour config's fields; null when it is not a throwing kind (no throwSpeed).</summary>
        public static RrScreenThrow FromConfig(RrConfig c)
        {
            if (c == null || !c.Has("throwSpeed"))
            {
                return null;
            }

            var generic = Generic();
            var t = new RrScreenThrow
            {
                Name = c.String("m_Name", "screen config"),
                Speed = c.Float("throwSpeed", 8f),
                AngularSpeed = c.Float("throwAngularSpeed", 360f),
                TowardsGazeTarget = c.Has("throwTowardsGazeTarget") ? c.Bool("throwTowardsGazeTarget") : true,
                CompensateGravity = c.Bool("throwTowardsGazeTargetCompensatingForGravity"),
                UseCurves = c.Has("transformGazePitchWithCurves") ? c.Bool("transformGazePitchWithCurves") : true
            };
            t.PitchByGazePitch = RrTools.CurveOf(c.Curve("pitchByGazePitch")) ?? generic.PitchByGazePitch;
            t.VelocityByGazePitch = RrTools.CurveOf(c.Curve("normalizedVelocityByGazePitch")) ?? generic.VelocityByGazePitch;
            return t;
        }

        /// <summary>
        /// The launch velocity for an object at <paramref name="from"/> thrown along the player's aim, and the spin
        /// (world angular velocity, radians per second, about the aim's right axis).
        /// </summary>
        public Vector3 Velocity(Vector3 from, Ray aim, out Vector3 angularVelocity)
        {
            Vector3 dir = aim.direction.sqrMagnitude > 1e-6f ? aim.direction.normalized : Vector3.forward;
            Vector3 target = aim.origin + dir * FarTarget;
            if (TowardsGazeTarget)
            {
                RaycastHit hit;
                int mask = RrLayers.WorldMask | (1 << RrLayers.Bullet);
                if (Physics.Raycast(aim, out hit, 60f, mask, QueryTriggerInteraction.Ignore) && hit.distance > 0.3f)
                {
                    target = hit.point;
                }

                Vector3 d = target - from;
                if (d.sqrMagnitude > 0.04f)
                {
                    dir = d.normalized;
                }
            }

            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                flat = Vector3.forward;
            }

            float horizontal = flat.magnitude;
            flat /= horizontal;
            float gazePitch = Mathf.Atan2(dir.y, horizontal) * Mathf.Rad2Deg;
            float pitch = gazePitch;
            float speed = Speed;
            if (TowardsGazeTarget && CompensateGravity)
            {
                pitch = BallisticPitch(from, target, speed, gazePitch);
            }
            else if (UseCurves)
            {
                if (PitchByGazePitch != null && PitchByGazePitch.length > 0)
                {
                    pitch = PitchByGazePitch.Evaluate(gazePitch);
                }

                if (VelocityByGazePitch != null && VelocityByGazePitch.length > 0)
                {
                    speed *= Mathf.Max(0f, VelocityByGazePitch.Evaluate(gazePitch));
                }
            }

            pitch = Mathf.Clamp(pitch, -89.5f, 89.5f) * Mathf.Deg2Rad;
            Vector3 launch = (flat * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * speed;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            angularVelocity = right * (AngularSpeed * Mathf.Deg2Rad);
            return launch;
        }

        /// <summary>The lower launch angle that reaches the target at this speed, or the gaze pitch when out of range.</summary>
        private static float BallisticPitch(Vector3 from, Vector3 target, float speed, float fallback)
        {
            Vector3 d = target - from;
            float x = new Vector2(d.x, d.z).magnitude;
            float y = d.y;
            float g = -Physics.gravity.y;
            if (x < 0.01f || g <= 0f)
            {
                return fallback;
            }

            float v2 = speed * speed;
            float root = v2 * v2 - g * (g * x * x + 2f * y * v2);
            if (root < 0f)
            {
                return 45f;
            }

            return Mathf.Atan2(v2 - Mathf.Sqrt(root), g * x) * Mathf.Rad2Deg;
        }

        public string Describe()
        {
            return Name + " (" + Speed.ToString("0.#") + " m/s" + (UseCurves ? ", pitch curve: level gaze -> " + (PitchByGazePitch != null ? PitchByGazePitch.Evaluate(0f).ToString("0") : "0") + " deg" : "") + ")";
        }

        internal static List<RrKey> NoKeys = new List<RrKey>();
    }
}
