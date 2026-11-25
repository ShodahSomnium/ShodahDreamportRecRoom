using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Harness hooks for the watch on the avatar's drawn wrist (DN/phase1/watch.md, scenario watch_wrist.json). The
    /// harness has no client, headset or avatar, so the arms come from <see cref="RrAvatarHandTest"/> (synthetic arms
    /// by bone name, fed to <see cref="RrAvatarHand"/> through its test override) and from the RPM Men / RPM Women
    /// humanoids of SomniumSpaceTemplates (editor only), and <see cref="RrWatchModel.TestWrist"/> lets the watch use
    /// them without a tracked hand.
    ///
    /// <see cref="Run"/>: numeric checks, logged "WatchTest: PASS|FAIL name (...)". The watch origin must be within 1.5 cm
    /// of the point on the forearm axis it belongs at (computed from the arm's elbow and hand bones, not from
    /// RrAvatarHand), the face normal within 10 deg of the back-of-hand normal, 3 o'clock along the forearm and
    /// 12 o'clock on the little-finger side within 10 deg. It also checks the IK-target fallback, the gesture angles and
    /// the tap region, and MEASURES: where the first build's grip-based watch was, the inner contour of Rec Room's
    /// watch mesh, and the RPM forearms' cross-section under the strap (clearance, plus the snuggest mesh scale and
    /// lift). The Station* hooks build one arm at <see cref="StationWrist"/> for photos. The Menu* hooks run the gaze
    /// and tap gestures end to end against the harness camera. <see cref="Summary"/> logs the totals ("WatchTest: ALL
    /// PASS"); <see cref="Clear"/> removes everything. Nothing here runs unless the harness calls it.
    /// </summary>
    public static class RrWatchWristTest
    {
        public const string RpmMenPath = RrAvatarHandTest.RpmPath;
        public const string RpmWomenPath = "Assets/SomniumSpaceTemplates/ItemsTemplate/Models/Avatars/RPM Women.fbx";

        /// <summary>Where the photo stations put the drawn wrist joint: 0.8 m ahead of the harness camera at (0, 1.6, -3), 0.3 m lower.</summary>
        public static readonly Vector3 StationWrist = new Vector3(0f, 1.3f, -2.2f);

        /// <summary>Back of the hand at the stations: up and toward the camera (normalised (0, 0.8, -0.6)).</summary>
        public static readonly Vector3 StationBack = new Vector3(0f, 0.8f, -0.6f);

        /// <summary>Clearance the fit wants between the skin and the strap everywhere, and on the back of the wrist (a sleeve).</summary>
        public const float MinClearance = 0.002f;

        public const float SleeveClearance = 0.004f;

        private static readonly Color Skin = new Color(0.86f, 0.68f, 0.55f);
        private static readonly Color SkinDark = new Color(0.74f, 0.55f, 0.43f);

        private static Transform _root;
        private static GameObject _rpm;
        private static readonly List<string> Failures = new List<string>();
        private static int _pass;
        private static System.Random _random = new System.Random(924);
        private static Vector3 _menuArmWrist;
        private static Quaternion _menuArmRotation;

        // ---- entry points ------------------------------------------------------------------------------------------

        /// <summary>All numeric checks and measurements (about 3 s after the watch mesh is loaded).</summary>
        public static IEnumerator Run()
        {
            Failures.Clear();
            _pass = 0;
            _random = new System.Random(924);
            yield return WaitWatch();
            if (RrWatch.Model == null)
            {
                Result("setup", false, "no RrWatch.Model");
                yield break;
            }

            Clear();
            Begin();
            CheckConstants();

            // 1. The drawn wrist in many poses, both sides, several avatar sizes (0.5 and 1.8 exercise the clamp).
            float[] scales = { 1f, 0.8f, 1.3f, 0.5f, 1.8f };
            for (int i = 0; i < scales.Length; i++)
            {
                foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
                {
                    Pose w = i == 0 ? StationPose(side) : new Pose(new Vector3(Rand(-2f, 2f), Rand(0.5f, 2f), Rand(-3f, 3f)), RandomRotation());
                    RrWatch.RightWrist = side == RrHandSide.Right;
                    RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(side, w, scales[i]);
                    yield return Frames(2);
                    CheckPlacement((side == RrHandSide.Left ? "left" : "right") + "-" + (i == 0 ? "station" : "pose" + i) + "-x" + scales[i].ToString("0.0"), arm, RrWatchSource.AvatarBones);
                }
            }

            // 2. No avatar bones (avatar change, or none found): the client's VRIK targets carry the same wrist.
            RrAvatarHandTest.TestArm leftArm = RrAvatarHandTest.Arm(RrHandSide.Left);
            RrAvatarHandTest.TestArm rightArm = RrAvatarHandTest.Arm(RrHandSide.Right);
            RrAvatarHand.SetTestAvatar(null, leftArm.IkPoint, rightArm.IkPoint, false);
            RrWatch.RightWrist = false;
            yield return Frames(2);
            CheckPlacement("left-ik-fallback", leftArm, RrWatchSource.IkTarget);
            RrWatch.RightWrist = true;
            yield return Frames(2);
            CheckPlacement("right-ik-fallback", rightArm, RrWatchSource.IkTarget);

            // 3. Nothing at all: hidden, not floating somewhere.
            RrAvatarHand.SetTestAvatar(null, null, null, false);
            yield return Frames(2);
            Result("no-wrist-hidden", !RrWatch.Model.OnWrist && RrWatch.Model.Source == RrWatchSource.None, "OnWrist " + RrWatch.Model.OnWrist + ", source " + RrWatch.Model.Source);

            // 4. The station arm again: the first build's placement, the gesture and the tap region on it.
            RrWatch.RightWrist = false;
            RrAvatarHandTest.TestArm station = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, StationPose(RrHandSide.Left));
            yield return Frames(2);
            MeasureFirstBuild(station);
            CheckGesture();
            CheckTapRegion();

            // 5. How Rec Room's strap fits real Somnium forearms.
            MeasureRing();
            yield return MeasureRpm(RpmMenPath, "RPM Men");
            yield return MeasureRpm(RpmWomenPath, "RPM Women");

            RrAvatarHandTest.ClearTestArms();
            Log("run done: " + _pass + " passed, " + Failures.Count + " failed so far");
        }

        /// <summary>Photo station 1: a synthetic LEFT arm (0.25 m forearm, skin-coloured stand-in 6.0 x 4.4 cm at the wrist) in the look-at-your-watch pose.</summary>
        public static IEnumerator StationSynthLeft()
        {
            Clear();
            Begin();
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, StationPose(RrHandSide.Left));
            DrawArm(arm);
            yield return Frames(3);
            CheckPlacement("station-synth-left", arm, RrWatchSource.AvatarBones);
            LogPose("station-synth-left");
        }

        /// <summary>Photo station 2: the RPM Men humanoid's real skinned LEFT arm (a baggy sweater over a shirt cuff) posed to look at the watch, head hidden like the client's first person.</summary>
        public static IEnumerator StationRpmMen()
        {
            return StationRpm(RpmMenPath, "station-rpm-men");
        }

        /// <summary>Photo station 3: the same with RPM Women.</summary>
        public static IEnumerator StationRpmWomen()
        {
            return StationRpm(RpmWomenPath, "station-rpm-women");
        }

        private static IEnumerator StationRpm(string path, string name)
        {
            Clear();
            Begin();
            yield return SpawnPosedRpm(path, RrHandSide.Left, StationWrist, Vector3.right, StationBack);
            if (_rpm == null)
            {
                yield break;
            }

            yield return WaitFit();
            Animator anim = _rpm.GetComponentInChildren<Animator>();
            CheckRpmPlacement(name, anim, RrHandSide.Left);
            Pose w;
            float forearm;
            if (RrAvatarHand.TryGetWrist(RrHandSide.Left, out w, out forearm))
            {
                var baked = new List<Vector3>(1024);
                BakedPoints(w, forearm, baked);
                CheckFit(name + "-fit", RrHandSide.Left, baked, forearm);
            }

            LogPose(name);
        }

        /// <summary>Photo station 4: the right-wrist option on a synthetic RIGHT arm 1.2x the size (0.30 m forearm), forearm pointing left.</summary>
        public static IEnumerator StationSynthRight()
        {
            Clear();
            Begin();
            RrWatch.RightWrist = true;
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Right, StationPose(RrHandSide.Right), 1.2f);
            DrawArm(arm);
            yield return Frames(3);
            CheckPlacement("station-synth-right", arm, RrWatchSource.AvatarBones);
            LogPose("station-synth-right");
        }

        /// <summary>
        /// The gaze gesture end to end: a left arm 0.38 m ahead of and 0.28 m below the view (the harness camera is the
        /// head), its watch face turned to the eyes. The menu must open by itself (0.4 s dwell), in the headset layout, on
        /// the wrist, and sit where RrWatchUi puts it relative to the face.
        /// </summary>
        public static IEnumerator MenuGaze()
        {
            Clear();
            Begin();
            float until = Time.realtimeSinceStartup + 60f;
            while (!RrWatch.Ready && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrWatch.ForceVrLayout = true;
            RrWatchModel.TestGaze = true;
            Vector3 head = RrPlayer.HeadPosition;
            Quaternion view = RrPlayer.HeadRotation;
            Vector3 fwd = view * Vector3.forward;
            Vector3 p = head + fwd * 0.38f + Vector3.down * 0.28f;
            Vector3 back = (head - p).normalized;
            Vector3 along = Vector3.ProjectOnPlane(view * Vector3.right, back).normalized;
            _menuArmWrist = p;
            _menuArmRotation = Quaternion.LookRotation(along, back);
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, new Pose(p, _menuArmRotation));
            DrawArm(arm);
            Log("gaze: head " + F(head) + " looking " + F(fwd) + ", left wrist at " + F(p));
            float t0 = Time.realtimeSinceStartup;
            until = t0 + 4f;
            while (!RrWatch.IsOpen && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            float took = Time.realtimeSinceStartup - t0;
            var ui = RrWatch.Ui;
            Result("gaze-open", RrWatch.IsOpen && RrWatch.Model.OpenedByWrist && ui != null && ui.VrLayout && !ui.Detached,
                   "open " + RrWatch.IsOpen + " after " + took.ToString("0.00") + " s (dwell " + RrWatchModel.DwellTime + " s), opened by wrist " + RrWatch.Model.OpenedByWrist + ", VR layout " + (ui != null && ui.VrLayout));
            yield return Seconds(1.2f);
            CheckMenuOnWrist("menu-on-wrist");
        }

        /// <summary>The open menu follows the wrist: the arm moves 8 cm right and 5 cm up.</summary>
        public static IEnumerator MenuFollow()
        {
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, new Pose(_menuArmWrist + new Vector3(0.08f, 0.05f, 0f), _menuArmRotation));
            ClearVisuals();
            DrawArm(arm);
            yield return Seconds(0.8f);
            Result("menu-still-open", RrWatch.IsOpen, "open " + RrWatch.IsOpen + " after the wrist moved 9.4 cm");
            CheckMenuOnWrist("menu-follows");
        }

        /// <summary>Turning the face away from the eyes (the forearm rolled 100 deg) closes a menu the wrist opened.</summary>
        public static IEnumerator MenuGazeAway()
        {
            Pose w = new Pose(_menuArmWrist + new Vector3(0.08f, 0.05f, 0f), Quaternion.AngleAxis(100f, _menuArmRotation * Vector3.forward) * _menuArmRotation);
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, w);
            ClearVisuals();
            DrawArm(arm);
            float until = Time.realtimeSinceStartup + 2f;
            while (RrWatch.IsOpen && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Result("gaze-close", !RrWatch.IsOpen, "open " + RrWatch.IsOpen + " after the face turned away");
            yield return Seconds(0.8f);
        }

        /// <summary>
        /// Tap to open: the watch back at the gaze pose with the setting on, the right hand's drawn fingertip
        /// (Rec Room's Fingertip on RrAvatarHand's hand frame) pushed onto the face. The menu must open.
        /// </summary>
        public static IEnumerator MenuTap()
        {
            RrWatch.Close();
            RrWatch.TapToOpen = true;
            RrAvatarHandTest.TestArm arm = RrAvatarHandTest.BuildTestArm(RrHandSide.Left, new Pose(_menuArmWrist, _menuArmRotation));
            ClearVisuals();
            DrawArm(arm);
            yield return Seconds(0.8f);
            var model = RrWatch.Model;
            Vector3 n = model.Rotation * Vector3.forward;
            Vector3 target = model.Position + model.Rotation * new Vector3(0f, 0f, 0.05f * model.Scale);

            // The poking hand: fingers down into the face, forearm coming from above, back of the hand toward the eyes.
            Vector3 up = Vector3.ProjectOnPlane(RrPlayer.HeadPosition - target, n).normalized;
            if (up.sqrMagnitude < 0.5f)
            {
                up = Vector3.ProjectOnPlane(Vector3.up, n).normalized;
            }

            var poke = new Pose(target + n * 0.35f, Quaternion.LookRotation(-n, up));
            RrAvatarHandTest.BuildTestArm(RrHandSide.Right, poke);
            yield return Frames(1);
            Vector3 tip;
            Vector3 palm;
            if (RrWatchModel.TryPokePoints(RrHandSide.Right, out tip, out palm))
            {
                poke.position += target - tip;
            }

            float t0 = Time.realtimeSinceStartup;
            RrAvatarHandTest.TestArm pokeArm = RrAvatarHandTest.BuildTestArm(RrHandSide.Right, poke);
            DrawArm(pokeArm);
            float until = t0 + 2f;
            while (!RrWatch.IsOpen && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrWatchModel.TryPokePoints(RrHandSide.Right, out tip, out palm);
            Result("tap-open", RrWatch.IsOpen, "open " + RrWatch.IsOpen + " after " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s; fingertip " + Mm((tip - target).magnitude) +
                                               " from the tap centre, inside " + RrWatchModel.TapRegionContains(model.Position, model.Rotation, model.Scale, tip));
            yield return Seconds(1f);
        }

        /// <summary>Totals of every check since <see cref="Run"/>.</summary>
        public static void Summary()
        {
            Log("summary " + _pass + " passed, " + Failures.Count + " failed" + (Failures.Count > 0 ? ": " + string.Join("; ", Failures) : ""));
            if (Failures.Count == 0 && _pass > 0)
            {
                Log("ALL PASS");
            }
        }

        /// <summary>Removes every test object and switch (arms, RPM, visuals, menu).</summary>
        public static void Clear()
        {
            RrAvatarHandTest.ClearTestArms();
            if (_rpm != null)
            {
                Object.Destroy(_rpm);
            }

            _rpm = null;
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
            RrWatchModel.TestWrist = false;
            RrWatchModel.TestGaze = false;
            RrWatch.RightWrist = false;
            RrWatch.TapToOpen = false;
            if (RrWatch.IsOpen)
            {
                RrWatch.Close();
            }

            RrWatch.ForceVrLayout = false;
        }

        // ---- checks ------------------------------------------------------------------------------------------------

        private static void CheckConstants()
        {
            var wantLeft = new Quaternion(0f, -0.70710678f, -0.70710678f, 0f);
            var wantRight = new Quaternion(-0.70710678f, 0f, 0f, 0.70710678f);
            float aL = Quaternion.Angle(RrWatchModel.LeftInWrist, wantLeft);
            float aR = Quaternion.Angle(RrWatchModel.RightInWrist, wantRight);
            Vector3 ideal = -(RrWatchModel.FaceView * Vector3.forward);
            float tilt = Vector3.Angle(ideal, Vector3.forward);
            float threeAxis = Vector3.Angle(RrWatchModel.FaceView * Vector3.right, Vector3.up);
            Result("constants", aL < 0.1f && aR < 0.1f && Mathf.Abs(tilt - 10.1f) < 0.3f && threeAxis < 1f,
                   "LeftInWrist " + aL.ToString("0.000") + " deg and RightInWrist " + aR.ToString("0.000") + " deg from hands_frames.md 5; ideal eye " + tilt.ToString("0.0") +
                   " deg off the face normal toward " + F(ideal) + "; face-view X " + threeAxis.ToString("0.0") + " deg from 3 o'clock");
        }

        /// <summary>Checks the live watch against the arm's own bones (independent of RrAvatarHand's frame code).</summary>
        private static void CheckPlacement(string name, RrAvatarHandTest.TestArm arm, RrWatchSource want)
        {
            var model = RrWatch.Model;
            bool left = arm.Side == RrHandSide.Left;
            Vector3 hand = arm.Hand.position;
            Vector3 elbow = arm.LowerArm.position;
            float forearm = Vector3.Distance(hand, elbow);
            Vector3 along = (hand - elbow) / forearm;
            Vector3 back = Vector3.ProjectOnPlane(arm.Wrist.rotation * Vector3.up, along).normalized;
            Vector3 thumbSide = left ? arm.Wrist.rotation * Vector3.right : -(arm.Wrist.rotation * Vector3.right);
            CheckAgainst(name, model, left, hand, along, back, thumbSide, forearm, want);
        }

        private static void CheckAgainst(string name, RrWatchModel model, bool left, Vector3 hand, Vector3 along, Vector3 back, Vector3 thumbSide, float forearm, RrWatchSource want)
        {
            float k = RrWatchModel.SizeOf(forearm);
            Vector3 expected = hand - along * (RrWatchModel.BackDistance * k) + back * model.LiftUsed;
            float d = (model.Position - expected).magnitude;
            float face = Vector3.Angle(model.Rotation * Vector3.forward, back);
            float three = Vector3.Angle(model.Rotation * Vector3.up, left ? along : -along);
            float twelve = Vector3.Angle(model.Rotation * Vector3.right, -thumbSide);
            float scaleErr = Mathf.Abs(model.Scale - RrWatchModel.MeshScale * k);
            Vector3 faceWant = model.Position + model.Rotation * Vector3.forward * (RrWatchModel.FaceHeight * model.Scale);
            bool ok = model.OnWrist && model.Source == want && d < 0.015f && face < 10f && three < 10f && twelve < 10f && scaleErr < 0.01f && (model.FacePosition - faceWant).magnitude < 0.001f;
            Result(name, ok, "source " + model.Source + ", centre " + Mm(d) + " from the forearm point " + (RrWatchModel.BackDistance * k * 100f).ToString("0.0") + " cm behind the wrist, face normal " +
                             face.ToString("0.0") + " deg from the back of the hand, 3 o'clock " + three.ToString("0.0") + " deg from " + (left ? "the fingers" : "the elbow") + ", 12 o'clock " +
                             twelve.ToString("0.0") + " deg from the little-finger side, forearm " + forearm.ToString("0.000") + " m, mesh scale " + model.Scale.ToString("0.000") + ", lift " + Mm(model.LiftUsed) + (model.Fitted ? " (fitted)" : " (default)"));
        }

        /// <summary>The RPM humanoid: the same checks from its humanoid bones (hand frame from the knuckles).</summary>
        private static void CheckRpmPlacement(string name, Animator anim, RrHandSide side)
        {
            bool left = side == RrHandSide.Left;
            Transform hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform lower = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform index = anim.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform little = anim.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            Transform middle = anim.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            if (hand == null || lower == null || index == null || little == null || middle == null)
            {
                Result(name, false, "RPM bones missing");
                return;
            }

            float forearm = Vector3.Distance(hand.position, lower.position);
            Vector3 along = (hand.position - lower.position) / forearm;
            Vector3 fingers = (middle.position - hand.position).normalized;
            Vector3 thumbSide = Vector3.ProjectOnPlane(index.position - little.position, fingers).normalized;
            // Rec Room frame: +Z fingers, +Y thumb, +X = Y x Z = out of the left palm / the back of the right hand.
            Vector3 x = Vector3.Cross(thumbSide, fingers);
            Vector3 back = Vector3.ProjectOnPlane(left ? -x : x, along).normalized;
            CheckAgainst(name, RrWatch.Model, left, hand.position, along, back, Vector3.ProjectOnPlane(thumbSide, along).normalized, forearm, RrWatchSource.AvatarBones);
        }

        /// <summary>Where the first build's grip-based watch was on this arm (the client's default IK needs <see cref="RrAvatarHandTest.TestArm.Grip"/> to draw it).</summary>
        private static void MeasureFirstBuild(RrAvatarHandTest.TestArm arm)
        {
            var model = RrWatch.Model;
            Vector3 oldPos;
            Quaternion oldRot;
            RrWatchModel.FirstBuildPose(arm.Side, arm.Grip, arm.Scale, out oldPos, out oldRot);
            Vector3 along = (arm.Hand.position - arm.LowerArm.position).normalized;
            Vector3 fromWrist = oldPos - arm.Hand.position;
            float alongAxis = Vector3.Dot(fromWrist, along);
            float offAxis = Vector3.ProjectOnPlane(fromWrist, along).magnitude;
            Vector3 back = arm.Wrist.rotation * Vector3.up;
            float offBack = Vector3.Dot(Vector3.ProjectOnPlane(fromWrist, along), back);
            Log("MEASURE first-build: the grip-based watch sat " + Mm((oldPos - model.Position).magnitude) + " from the drawn-wrist watch: " + Mm(offAxis) + " off the forearm axis (" +
                Mm(offBack) + " toward the back of the hand), " + Mm(alongAxis) + (alongAxis >= 0f ? " past the wrist joint into the hand" : " up the forearm") + "; face turned " +
                Vector3.Angle(oldRot * Vector3.forward, model.Rotation * Vector3.forward).ToString("0.0") + " deg, strap " + Vector3.Angle(oldRot * Vector3.right, model.Rotation * Vector3.right).ToString("0.0") +
                " deg, 3 o'clock " + Vector3.Angle(oldRot * Vector3.up, along).ToString("0.0") + " deg from the fingers");
        }

        /// <summary>The gesture angles on the live watch: ideal eye, tilts along the forearm (pitch) and across the wrist (roll), and the face normal.</summary>
        private static void CheckGesture()
        {
            var model = RrWatch.Model;
            Quaternion fv = model.Rotation * RrWatchModel.FaceView;
            Vector3 ideal = -(fv * Vector3.forward);
            Vector3 face = model.FacePosition;
            Vector3 along = model.Rotation * Vector3.up;
            float axisErr = Vector3.Angle(fv * Vector3.right, along);
            var sb = new StringBuilder();
            bool ok = axisErr < 1f;
            ok &= Gaze(sb, "ideal", face, ideal, true);
            ok &= Gaze(sb, "along 40", face, Quaternion.AngleAxis(40f, fv * Vector3.up) * ideal, true);
            ok &= Gaze(sb, "along 55", face, Quaternion.AngleAxis(55f, fv * Vector3.up) * ideal, false);
            ok &= Gaze(sb, "across 25", face, Quaternion.AngleAxis(25f, fv * Vector3.right) * ideal, true);
            ok &= Gaze(sb, "across -40", face, Quaternion.AngleAxis(-40f, fv * Vector3.right) * ideal, false);
            ok &= Gaze(sb, "face normal", face, model.Rotation * Vector3.forward, true);
            ok &= Gaze(sb, "palm side", face, -(model.Rotation * Vector3.forward), false);
            Result("gesture", ok, "face-view 3 o'clock axis " + axisErr.ToString("0.0") + " deg from the forearm; " + sb);
        }

        private static bool Gaze(StringBuilder sb, string label, Vector3 face, Vector3 toEye, bool wantRaised)
        {
            var model = RrWatch.Model;
            Vector3 head = face + toEye.normalized * 0.45f;
            float angle;
            float pitch;
            float roll;
            bool facing;
            RrWatchModel.GazeAngles(face, model.Rotation, head, -toEye.normalized, out angle, out pitch, out roll, out facing);
            bool raised = facing && angle <= RrWatchModel.OpenAngle && pitch <= RrWatchModel.OpenPitch && roll <= RrWatchModel.OpenRoll;
            sb.Append(label).Append(": pitch ").Append(pitch.ToString("0.0")).Append(" roll ").Append(roll.ToString("0.0")).Append(raised ? " opens" : " no").Append(raised == wantRaised ? "" : " (WRONG)").Append("; ");
            return raised == wantRaised;
        }

        private static void CheckTapRegion()
        {
            var m = RrWatch.Model;
            Vector3 n = m.Rotation * Vector3.forward;
            Vector3 strap = m.Rotation * Vector3.right;
            bool centre = RrWatchModel.TapRegionContains(m.Position, m.Rotation, m.Scale, m.Position + n * (0.05f * m.Scale));
            bool onFace = RrWatchModel.TapRegionContains(m.Position, m.Rotation, m.Scale, m.FacePosition + n * 0.005f);
            bool aside = RrWatchModel.TapRegionContains(m.Position, m.Rotation, m.Scale, m.FacePosition + strap * 0.07f);
            bool palmSide = RrWatchModel.TapRegionContains(m.Position, m.Rotation, m.Scale, m.Position - n * 0.03f);
            Result("tap-region", centre && onFace && !aside && !palmSide, "centre " + centre + ", 5 mm over the face " + onFace + ", 7 cm along the strap " + aside + ", under the wrist " + palmSide +
                                                                         " (box " + Mm(0.08f * m.Scale) + " x " + Mm(0.06f * m.Scale) + " x " + Mm(0.05f * m.Scale) + ")");
        }

        private static void CheckMenuOnWrist(string name)
        {
            var ui = RrWatch.Ui;
            var model = RrWatch.Model;
            if (ui == null || ui.Canvas == null)
            {
                Result(name, false, "no menu");
                return;
            }

            // RrWatchUi.Place, headset on the wrist: 0.22 m above the face and 0.06 m further from the head, facing the eyes.
            Vector3 head = RrPlayer.HeadPosition;
            Vector3 face = model.FacePosition;
            Vector3 away = face - head;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            Vector3 want = face + Vector3.up * 0.22f + away * 0.06f;
            Vector3 at = ui.Canvas.transform.position;
            float d = (at - want).magnitude;
            float facing = Vector3.Angle(ui.Canvas.transform.forward, at - head);
            Result(name, RrWatch.IsOpen && d < 0.01f && facing < 5f && model.Source == RrWatchSource.AvatarBones,
                   "menu at " + F(at) + ", " + Mm(d) + " from face + 0.22 up + 0.06 away, " + facing.ToString("0.0") + " deg from facing the eyes; watch face at " + F(face) + " (" + model.Source + ")");
        }

        // ---- measurements ------------------------------------------------------------------------------------------

        /// <summary>Inner and outer contour of Rec Room's watch mesh round its origin (the wrist axis), in 15 deg bins from the face (0) toward 12 o'clock (90).</summary>
        private static void MeasureRing()
        {
            Mesh mesh = WatchMesh();
            if (mesh == null)
            {
                Log("MEASURE ring: no Wrist_Watch mesh on the model");
                return;
            }

            if (!mesh.isReadable)
            {
                Log("MEASURE ring: Wrist_Watch is not readable here; bounds " + F(mesh.bounds.center) + " +- " + F(mesh.bounds.extents));
                return;
            }

            float[] inner = RrWatchFit.Contour(mesh);
            var outer = new float[RrWatchFit.Bins];
            Vector3[] v = mesh.vertices;
            foreach (Vector3 p in v)
            {
                if (Mathf.Abs(p.y) <= RrWatchFit.BandHalf)
                {
                    int b = RrWatchFit.Bin(p.x, p.z);
                    outer[b] = Mathf.Max(outer[b], Mathf.Sqrt(p.x * p.x + p.z * p.z));
                }
            }

            var sb = new StringBuilder();
            for (int i = 0; i < RrWatchFit.Bins; i += 2)
            {
                sb.Append(i * 15).Append(":").Append(inner[i] < float.MaxValue ? (inner[i] * 1000f).ToString("0") : "open").Append("/").Append((outer[i] * 1000f).ToString("0")).Append(" ");
            }

            float[] live = RrWatch.Model.Contour;
            bool same = live != null;
            for (int i = 0; same && i < RrWatchFit.Bins; i++)
            {
                same = Mathf.Approximately(live[i], inner[i]);
            }

            Log("MEASURE ring: Wrist_Watch " + v.Length + " verts, bounds " + F(mesh.bounds.center) + " +- " + F(mesh.bounds.extents) + "; inner/outer radius (mm) by angle from the face toward 12 o'clock: " + sb);
            Result("contour", same, "the model's own contour " + (live != null ? (same ? "matches" : "differs") : "is missing"));
        }

        /// <summary>
        /// A Somnium template avatar in its skeleton (T) pose, each wrist: the surface under the strap from BakeMesh (Unity's
        /// own skinning, independent of RrWatchFit's), the snuggest mesh scale and lift for it, and the live model's fit
        /// checked against it.
        /// </summary>
        private static IEnumerator MeasureRpm(string path, string label)
        {
            GameObject prefab = LoadAsset(path);
            if (prefab == null)
            {
                Log("SKIP measure " + label + ": " + path + " is not loadable here");
                yield break;
            }

            EnsureRoot();
            _rpm = Object.Instantiate(prefab, _root);
            _rpm.name = "Rr Watch Test " + label;
            _rpm.transform.SetPositionAndRotation(new Vector3(6f, 0f, 6f), Quaternion.identity);
            Animator anim = _rpm.GetComponentInChildren<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
            {
                Log("SKIP measure " + label + ": no humanoid Animator");
                Object.Destroy(_rpm);
                _rpm = null;
                yield break;
            }

            ApplySkeletonPose(_rpm.transform, anim);
            RrAvatarHand.SetTestAvatar(_rpm.transform, null, null, false);
            yield return null;
            foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
            {
                RrWatch.RightWrist = side == RrHandSide.Right;
                yield return WaitFit();
                MeasureRpmSide(label, side);
            }

            RrWatch.RightWrist = false;
            Object.Destroy(_rpm);
            _rpm = null;
            RrAvatarHand.ClearTestAvatar();
            yield return null;
        }

        private static void MeasureRpmSide(string label, RrHandSide side)
        {
            bool left = side == RrHandSide.Left;
            string n = label + " " + (left ? "left" : "right");
            Pose w;
            float forearm;
            if (!RrAvatarHand.TryGetWrist(side, out w, out forearm))
            {
                Log("SKIP measure " + n + ": no wrist");
                return;
            }

            var pts = new List<Vector3>(2048);
            BakedPoints(w, forearm, pts);
            float k = RrWatchModel.SizeOf(forearm);
            float centreZ = -RrWatchModel.BackDistance * k;
            float half = RrWatchFit.BandHalf * RrWatchModel.MeshScale * k;
            float dorsal = float.MinValue, palmar = float.MaxValue, xMax = float.MinValue, xMin = float.MaxValue;
            int inBand = 0;
            foreach (Vector3 p in pts)
            {
                if (Mathf.Abs(p.z - centreZ) > half)
                {
                    continue;
                }

                inBand++;
                dorsal = Mathf.Max(dorsal, p.y);
                palmar = Mathf.Min(palmar, p.y);
                xMax = Mathf.Max(xMax, p.x);
                xMin = Mathf.Min(xMin, p.x);
            }

            if (inBand == 0)
            {
                Log("MEASURE " + n + ": no skin vertices under the strap (" + pts.Count + " near the wrist)");
                return;
            }

            // How the back of the wrist rises along the forearm (a sleeve's cuff and body).
            var profile = new StringBuilder();
            for (int cm = 1; cm >= -5; cm--)
            {
                float rise = float.MinValue;
                foreach (Vector3 p in pts)
                {
                    if (Mathf.Abs(p.z - cm * 0.01f) <= 0.0025f && Mathf.Abs(p.x) < 0.04f)
                    {
                        rise = Mathf.Max(rise, p.y);
                    }
                }

                profile.Append(cm).Append(" cm: ").Append(rise > float.MinValue ? Mm(rise) : "-").Append("; ");
            }

            string cross = "surface under the strap (" + inBand + " verts, " + Mm(2f * half) + " band centred " + Mm(-centreZ) + " up the forearm): back of the wrist " + Mm(dorsal) + " above the joint axis, palm side " +
                           Mm(-palmar) + " below, " + (left ? "thumb" : "little-finger") + " side " + Mm(xMax) + ", " + (left ? "little-finger" : "thumb") + " side " + Mm(-xMin) + " (width " + Mm(xMax - xMin) + ", thickness " +
                           Mm(dorsal - palmar) + "); back of the wrist by distance from the joint (+ = into the hand): " + profile;
            float[] contour = RrWatch.Model.Contour;
            if (contour == null)
            {
                Log("MEASURE " + n + ": forearm " + forearm.ToString("0.000") + " m; " + cross + "no ring contour to fit");
                return;
            }

            float bestS = -1f, bestH = 0f, bestTop = 0f, bestBottom = 0f, bestSide = 0f, bestMin = 0f;
            for (float s = 0.4f; s <= 1.2001f && bestS < 0f; s += 0.01f)
            {
                for (float h = -0.012f; h <= 0.0201f; h += 0.0005f)
                {
                    float min = RrWatchFit.Clearance(pts, side, s, h, RrWatchModel.BackDistance * k, contour, out float top, out int topN, out float bottom, out float sideGap);
                    if (topN >= RrWatchFit.MinTopPoints && min >= MinClearance && top >= RrWatchFit.SleeveClearance)
                    {
                        bestS = s;
                        bestH = h;
                        bestTop = top;
                        bestBottom = bottom;
                        bestSide = sideGap;
                        bestMin = min;
                        break;
                    }
                }
            }

            Log("MEASURE " + n + ": forearm " + forearm.ToString("0.000") + " m (size " + k.ToString("0.000") + "); " + cross + "snuggest all-round fit " +
                (bestS > 0f ? "mesh scale " + bestS.ToString("0.00") + " (MeshScale " + (bestS / k).ToString("0.000") + "), lift " + Mm(bestH) + ": min " + Mm(bestMin) + ", under the case " + Mm(bestTop) +
                              ", under the wrist " + Mm(bestBottom) + ", sides " + Mm(bestSide) : "none in 0.4..1.2 (a sleeve bigger than the strap)"));
            CheckFit("fit-" + label.Replace(" ", "-").ToLowerInvariant() + "-" + (left ? "left" : "right"), side, pts, forearm);
        }

        /// <summary>The live model's fitted lift checked against Unity's own skinning (BakeMesh points): 4 mm under the case (1.5 mm tolerance), or the cap.</summary>
        private static void CheckFit(string name, RrHandSide side, List<Vector3> baked, float forearm)
        {
            var m = RrWatch.Model;
            float k = RrWatchModel.SizeOf(forearm);
            if (m.Contour == null)
            {
                Result(name, false, "no contour");
                return;
            }

            float min = RrWatchFit.Clearance(baked, side, RrWatchModel.MeshScale * k, m.LiftUsed, RrWatchModel.BackDistance * k, m.Contour, out float top, out int n, out float bottom, out float sides);
            bool capped = m.LiftUsed >= RrWatchFit.MaxLift * k - 1e-4f;
            bool ok = m.Fitted && m.Source == RrWatchSource.AvatarBones && n >= RrWatchFit.MinTopPoints && (capped || Mathf.Abs(top - RrWatchFit.SleeveClearance) <= 0.0015f);
            RrWatchFitReport r = m.FitReport;
            Result(name, ok, "fitted " + m.Fitted + ", lift " + Mm(m.LiftUsed) + (capped ? " (CAP)" : "") + "; on BakeMesh points: " + n + " under the case, gap " + Mm(top) + " (want " +
                             Mm(RrWatchFit.SleeveClearance) + "), under the wrist " + Mm(bottom) + ", sides " + Mm(sides) + ", min " + Mm(min) + "; sampler: " + r.Matched + " of " + r.Renderers +
                             " meshes matched, " + r.Points + " points from " + r.Vertices + " vertices in " + r.Milliseconds.ToString("0.00") + " ms [" + r.Names + "]");
        }

        /// <summary>
        /// Unity's own skinning of the test RPM (BakeMesh) near the wrist, in the wrist frame, with RrWatchFit's selection
        /// (vertices whose main bone is the lower arm or below) and triangle midpoints, so only the skinning differs from
        /// the live sampler.
        /// </summary>
        private static void BakedPoints(Pose w, float forearm, List<Vector3> pts)
        {
            var baked = new Mesh();
            var tris = new List<int>(8192);
            float k = RrWatchModel.SizeOf(forearm);
            RrWatchFit.Window(k, out float zMin, out float zMax, out float rMax2);
            Vector3 elbow = w.position - (w.rotation * Vector3.forward) * forearm;
            foreach (SkinnedMeshRenderer smr in _rpm.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh shared = smr.sharedMesh;
                bool[] arm = shared != null ? RrWatchFit.ArmBones(smr.bones, w.position, elbow) : null;
                if (arm == null)
                {
                    continue;
                }

                smr.BakeMesh(baked, true);
                Matrix4x4 m = smr.transform.localToWorldMatrix;
                Vector3[] v = baked.vertices;
                BoneWeight[] weights = shared.boneWeights;
                var local = new Vector3[v.Length];
                var keep = new bool[v.Length];
                for (int i = 0; i < v.Length && i < weights.Length; i++)
                {
                    int b0 = weights[i].boneIndex0;
                    keep[i] = b0 >= 0 && b0 < arm.Length && arm[b0];
                    local[i] = Quaternion.Inverse(w.rotation) * (m.MultiplyPoint3x4(v[i]) - w.position);
                }

                for (int sub = 0; sub < shared.subMeshCount; sub++)
                {
                    shared.GetTriangles(tris, sub);
                    RrWatchFit.AddSurface(pts, local, keep, v.Length, tris, zMin, zMax, rMax2);
                }
            }

            Object.Destroy(baked);
        }

        /// <summary>Asks the model to fit again and waits (at most 3 s) until it has.</summary>
        private static IEnumerator WaitFit()
        {
            var m = RrWatch.Model;
            yield return Frames(2);
            m.RefitNow();
            float until = Time.realtimeSinceStartup + 3f;
            yield return null;
            while (!m.FitDone && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return Frames(2);
        }

        // ---- scene building ----------------------------------------------------------------------------------------

        /// <summary>The station pose: wrist at <see cref="StationWrist"/>, back of the hand <see cref="StationBack"/>, forearm pointing across the body (left arm to +X, right arm to -X).</summary>
        public static Pose StationPose(RrHandSide side)
        {
            Vector3 along = side == RrHandSide.Left ? Vector3.right : Vector3.left;
            return new Pose(StationWrist, Quaternion.LookRotation(along, StationBack.normalized));
        }

        private static void Begin()
        {
            RrWatchModel.TestPose = null;
            RrWatchModel.TestWrist = true;
            RrWatchModel.TestGaze = false;
            RrWatch.RightWrist = false;
            RrWatch.TapToOpen = false;
        }

        /// <summary>A skin-coloured stand-in for a synthetic arm: a tapered elliptical forearm (6.0 x 4.4 cm at the wrist, 7.4 x 5.8 cm at the elbow), a palm, fingers and a thumb.</summary>
        private static void DrawArm(RrAvatarHandTest.TestArm arm)
        {
            EnsureRoot();
            Pose w = arm.Wrist;
            float s = arm.Scale;
            Vector3 z = w.rotation * Vector3.forward;
            Vector3 y = w.rotation * Vector3.up;
            Vector3 x = w.rotation * Vector3.right;
            Vector3 thumb = arm.Side == RrHandSide.Left ? x : -x;
            Quaternion cylinder = Quaternion.LookRotation(y, z);
            float[] widths = { 0.060f, 0.067f, 0.074f };
            float[] thick = { 0.044f, 0.051f, 0.058f };
            for (int i = 0; i < 3; i++)
            {
                float len = 0.25f / 3f;
                Vector3 c = w.position - z * ((i + 0.5f) * len * s);
                Shape(PrimitiveType.Cylinder, c, cylinder, new Vector3(widths[i], len * 0.5f, thick[i]) * s, Skin);
            }

            Quaternion box = Quaternion.LookRotation(z, y);
            Shape(PrimitiveType.Cube, w.position + z * (0.05f * s), box, new Vector3(0.080f, 0.028f, 0.100f) * s, Skin);
            Shape(PrimitiveType.Cube, w.position + (z * 0.135f - y * 0.004f) * s, box, new Vector3(0.076f, 0.020f, 0.075f) * s, SkinDark);
            Shape(PrimitiveType.Cube, w.position + (z * 0.06f + thumb * 0.048f - y * 0.012f) * s, Quaternion.LookRotation(z + thumb * 0.4f, y), new Vector3(0.022f, 0.020f, 0.06f) * s, SkinDark);
        }

        private static void Shape(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Color c)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Rr Watch Test Arm";
            g.transform.SetParent(_root, false);
            g.transform.SetPositionAndRotation(pos, rot);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = RrLook.Solid(c);
        }

        private static void ClearVisuals()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
        }

        /// <summary>
        /// An RPM humanoid in its skeleton pose with one arm posed: forearm along <paramref name="forearmDir"/>, turned
        /// about itself so the back of the hand faces <paramref name="backDir"/> (the wrist stays straight). Of nine
        /// upper-arm directions (down, forward, in or out) the one that needs the least forearm twist is used, because
        /// the RPM rig has no twist bones and a big twist tears the sleeve. The other arm hangs down, the head is scaled
        /// away as the client does in first person, and the whole model is moved so the wrist joint is at
        /// <paramref name="wristAt"/>.
        /// </summary>
        private static IEnumerator SpawnPosedRpm(string path, RrHandSide side, Vector3 wristAt, Vector3 forearmDir, Vector3 backDir)
        {
            GameObject prefab = LoadAsset(path);
            if (prefab == null)
            {
                Log("SKIP station: " + path + " is not loadable here");
                yield break;
            }

            EnsureRoot();
            _rpm = Object.Instantiate(prefab, _root);
            _rpm.name = "Rr Watch Test RPM";
            _rpm.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2.6f), Quaternion.identity);
            Animator anim = _rpm.GetComponentInChildren<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
            {
                Log("SKIP station: no humanoid Animator");
                Object.Destroy(_rpm);
                _rpm = null;
                yield break;
            }

            forearmDir = forearmDir.normalized;
            Vector3 best = Vector3.down;
            float bestTwist = float.MaxValue;
            foreach (float x in new[] { -0.2f, 0.1f, 0.4f })
            {
                foreach (float z in new[] { 0.3f, 0.8f, 1.3f })
                {
                    Vector3 u = new Vector3(side == RrHandSide.Left ? x : -x, -1f, z).normalized;
                    float t = PoseArm(anim, side, u, forearmDir, backDir, false);
                    if (Mathf.Abs(t) < Mathf.Abs(bestTwist))
                    {
                        bestTwist = t;
                        best = u;
                    }
                }
            }

            PoseArm(anim, side, best, forearmDir, backDir, true);
            Transform hand = anim.GetBoneTransform(side == RrHandSide.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            _rpm.transform.position += wristAt - hand.position;
            yield return null;
            Pose w;
            float forearm;
            RrAvatarHand.TryGetWrist(side, out w, out forearm);
            Log("RPM posed: upper arm " + F(best) + ", forearm twisted " + bestTwist.ToString("0") + " deg; wrist " + F(w.position) + ", forearm " + F(w.rotation * Vector3.forward) + " (" + forearm.ToString("0.000") +
                " m), back of the hand " + F(w.rotation * Vector3.up) + ", " + RrAvatarHand.Source(side));
        }

        /// <summary>Skeleton pose, then the arm: returns the twist (deg) about the forearm that the back of the hand needs, applied when <paramref name="apply"/>.</summary>
        private static float PoseArm(Animator anim, RrHandSide side, Vector3 upperDir, Vector3 forearmDir, Vector3 backDir, bool apply)
        {
            ApplySkeletonPose(_rpm.transform, anim);
            bool left = side == RrHandSide.Left;
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                head.localScale = Vector3.one * 0.0001f;
            }

            Transform otherUpper = anim.GetBoneTransform(left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform otherLower = anim.GetBoneTransform(left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            if (otherUpper != null && otherLower != null)
            {
                otherUpper.rotation = Quaternion.FromToRotation(otherLower.position - otherUpper.position, Vector3.down) * otherUpper.rotation;
            }

            Transform upper = anim.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, upperDir) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, forearmDir) * lower.rotation;
            RrAvatarHand.SetTestAvatar(_rpm.transform, null, null, false);
            Pose w;
            float forearm;
            if (!RrAvatarHand.TryGetWrist(side, out w, out forearm))
            {
                return 0f;
            }

            float twist = Vector3.SignedAngle(w.rotation * Vector3.up, Vector3.ProjectOnPlane(backDir, forearmDir), forearmDir);
            if (apply)
            {
                lower.rotation = Quaternion.AngleAxis(twist, forearmDir) * lower.rotation;
            }

            return twist;
        }

        /// <summary>Sets every bone named in the Avatar's skeleton to its skeleton local pose (the client's bind pose; the FBX rests in an A-pose).</summary>
        private static void ApplySkeletonPose(Transform model, Animator anim)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t != model && !byName.ContainsKey(t.name))
                {
                    byName.Add(t.name, t);
                }
            }

            foreach (SkeletonBone b in anim.avatar.humanDescription.skeleton)
            {
                Transform t;
                if (b.name != null && byName.TryGetValue(b.name, out t))
                {
                    t.localPosition = b.position;
                    t.localRotation = b.rotation;
                }
            }
        }

        private static void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("Rr Watch Wrist Test").transform;
            Transform world = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            if (world != null)
            {
                _root.SetParent(world, false);
            }
        }

        private static GameObject LoadAsset(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        private static Mesh WatchMesh()
        {
            var model = RrWatch.Model;
            Transform t = model != null ? model.transform.Find("Rr Watch Model/Wrist_Watch") : null;
            MeshFilter mf = t != null ? t.GetComponent<MeshFilter>() : null;
            return mf != null ? mf.sharedMesh : null;
        }

        private static IEnumerator WaitWatch()
        {
            float until = Time.realtimeSinceStartup + 60f;
            while ((RrWatch.Model == null || WatchMesh() == null) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float s)
        {
            float until = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static void LogPose(string name)
        {
            var m = RrWatch.Model;
            Log("pose " + name + ": watch origin " + F(m.Position) + ", face " + F(m.FacePosition) + ", face normal " + F(m.Rotation * Vector3.forward) + ", 3 o'clock " + F(m.Rotation * Vector3.up) +
                ", mesh scale " + m.Scale.ToString("0.000"));
        }

        private static float Rand(float a, float b)
        {
            return a + (float)_random.NextDouble() * (b - a);
        }

        private static Quaternion RandomRotation()
        {
            var v = new Vector4(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f));
            if (v.sqrMagnitude < 1e-4f)
            {
                return Quaternion.identity;
            }

            v.Normalize();
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        private static string F(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " + v.z.ToString("0.000") + ")";
        }

        private static string Mm(float metres)
        {
            return (metres * 1000f).ToString("0.0") + " mm";
        }

        private static void Result(string name, bool ok, string detail)
        {
            if (ok)
            {
                _pass++;
            }
            else
            {
                Failures.Add(name);
            }

            Log((ok ? "PASS " : "FAIL ") + name + " (" + detail + ")");
        }

        private static void Log(string text)
        {
            RrLog.Info("Watch", "WatchTest: " + text);
        }
    }
}
