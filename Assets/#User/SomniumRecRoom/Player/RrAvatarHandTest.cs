using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Harness hooks for <see cref="RrAvatarHand"/>. There is no Somnium client in the harness, so no avatar and no
    /// anchors. These hooks build what the client would have and feed it through the internal test override
    /// (<c>RrAvatarHand.SetTestAvatar</c>). The build is a synthetic arm per side: GameObjects named like real rigs (left
    /// Mixamo "mixamorig:LeftForeArm" with a roll bone before the hand, right Unreal "lowerarm_r"/"index_01_r"), every
    /// bone turned to a random rotation (only positions may matter), a fake "LeftHandOffset" with a "Stabilized" child
    /// holding a "LeftHandVRIKPoint" at the client's local IK pose, and the RPM Men humanoid from SomniumSpaceTemplates
    /// when the project has it (editor only). <see cref="Run"/> checks the computed wrist, palm and Rec Room frames
    /// numerically and logs "HandsTest: PASS name (...)" / "HandsTest: FAIL name (...)", then "HandsTest: summary" and,
    /// with no failure, "HandsTest: ALL PASS".
    ///
    /// Reusable by the watch task: <see cref="BuildTestArm"/> puts an arm's drawn wrist at a given pose (API
    /// convention: +Z along the forearm, +Y out of the back of the hand) and <see cref="BuildDefaultArms"/> is the
    /// zero-argument variant for scenarios (both arms thumbs-up, forearms forward, in front of the harness camera).
    /// Nothing here runs unless called; <see cref="ClearTestArms"/> removes everything and the override.
    /// </summary>
    public static class RrAvatarHandTest
    {
        /// <summary>A synthetic arm (all transforms are ours, under "Rr Hands Test").</summary>
        public sealed class TestArm
        {
            public RrHandSide Side;
            public Pose Wrist;
            public float Scale;
            public Transform UpperArm, LowerArm, Roll, Hand, Index, Middle, Ring, Little, Thumb;
            public Transform HandOffset, Anchor, Stabilized, IkPoint;

            /// <summary>The grip pose the client's default IK would need for this wrist (= <see cref="Anchor"/>).</summary>
            public Pose Grip;
        }

        public const string RpmPath = "Assets/SomniumSpaceTemplates/ItemsTemplate/Models/Avatars/RPM Men.fbx";
        public const float ForearmLength = 0.25f;
        public const float HandLength = 0.092f;

        private static Transform _root;
        private static Transform _avatar;
        private static Transform _visuals;
        private static readonly TestArm[] Arms = new TestArm[2];
        private static readonly List<string> Failures = new List<string>();
        private static int _pass;
        private static readonly List<GameObject> Held = new List<GameObject>();
        private static GameObject _rpm;
        private static System.Random _random = new System.Random(20260924);

        // ---- public hooks ------------------------------------------------------------------------------------------

        /// <summary>The display pose used by <see cref="BuildDefaultArms"/>: thumbs up, forearm forward, 1.35 m high, 0.8 m in front of the harness camera.</summary>
        public static Pose DefaultWrist(RrHandSide side)
        {
            bool left = side == RrHandSide.Left;
            Vector3 back = left ? Vector3.left : Vector3.right;
            return new Pose(new Vector3(left ? -0.2f : 0.2f, 1.35f, -2.2f), Quaternion.LookRotation(Vector3.forward, back));
        }

        /// <summary>
        /// Builds (or rebuilds) one synthetic arm whose drawn wrist is exactly <paramref name="wrist"/> (+Z along the
        /// forearm, +Y out of the back of the hand) at <paramref name="scale"/> (1 = 0.25 m forearm, 0.092 m hand).
        /// It also builds the fake anchor that the client's default IK would need for that wrist, and feeds both arms
        /// to <see cref="RrAvatarHand"/> (bones and IK points; no controller guess).
        /// </summary>
        public static TestArm BuildTestArm(RrHandSide side, Pose wrist, float scale = 1f)
        {
            EnsureRoot();
            int i = side == RrHandSide.Left ? 0 : 1;
            DestroyArm(Arms[i]);
            var arm = new TestArm { Side = side, Wrist = wrist, Scale = scale };
            Arms[i] = arm;
            bool left = side == RrHandSide.Left;
            Vector3 z = wrist.rotation * Vector3.forward;
            Vector3 back = wrist.rotation * Vector3.up;
            Vector3 x = wrist.rotation * Vector3.right;
            Vector3 thumbSide = left ? x : -x;
            Vector3 palmOut = -back;
            float s = scale;

            // Bones: parents first (rotation, then position), so setting a child's world pose is final.
            Transform hips = Bone(left ? "mixamorig:Hips" : "pelvis", _avatar, wrist.position - z * 0.55f * s - back * 0.35f * s);
            if (left)
            {
                arm.UpperArm = Bone("mixamorig:LeftArm", hips, wrist.position - z * (0.25f + 0.28f) * s);
                arm.LowerArm = Bone("mixamorig:LeftForeArm", arm.UpperArm, wrist.position - z * 0.25f * s);
                arm.Roll = Bone("mixamorig:LeftForeArmRoll", arm.LowerArm, wrist.position - z * 0.02f * s);
                arm.Hand = Bone("mixamorig:LeftHand", arm.Roll, wrist.position);
                arm.Index = Bone("mixamorig:LeftHandIndex1", arm.Hand, wrist.position + (z * 0.088f + thumbSide * 0.022f) * s);
                arm.Middle = Bone("mixamorig:LeftHandMiddle1", arm.Hand, wrist.position + z * HandLength * s);
                arm.Ring = Bone("mixamorig:LeftHandRing1", arm.Hand, wrist.position + (z * 0.086f - thumbSide * 0.019f) * s);
                arm.Little = Bone("mixamorig:LeftHandPinky1", arm.Hand, wrist.position + (z * 0.078f - thumbSide * 0.036f) * s);
                arm.Thumb = Bone("mixamorig:LeftHandThumb1", arm.Hand, wrist.position + (z * 0.03f + thumbSide * 0.028f + palmOut * 0.018f) * s);
                Bone("mixamorig:LeftHandIndex2", arm.Index, arm.Index.position + z * 0.035f * s);
            }
            else
            {
                arm.UpperArm = Bone("upperarm_r", hips, wrist.position - z * (0.25f + 0.28f) * s);
                arm.LowerArm = Bone("lowerarm_r", arm.UpperArm, wrist.position - z * 0.25f * s);
                arm.Hand = Bone("hand_r", arm.LowerArm, wrist.position);
                Transform meta = Bone("index_metacarpal_r", arm.Hand, wrist.position + (z * 0.03f + thumbSide * 0.01f) * s);
                arm.Index = Bone("index_01_r", meta, wrist.position + (z * 0.088f + thumbSide * 0.022f) * s);
                arm.Middle = Bone("middle_01_r", arm.Hand, wrist.position + z * HandLength * s);
                arm.Ring = Bone("ring_01_r", arm.Hand, wrist.position + (z * 0.086f - thumbSide * 0.019f) * s);
                arm.Little = Bone("pinky_01_r", arm.Hand, wrist.position + (z * 0.078f - thumbSide * 0.036f) * s);
                arm.Thumb = Bone("thumb_01_r", arm.Hand, wrist.position + (z * 0.03f + thumbSide * 0.028f + palmOut * 0.018f) * s);
                Bone("lowerarm_twist_01_r", arm.LowerArm, wrist.position - z * 0.1f * s);
            }

            // The client's rig for this wrist: HandOffset (rig scale) > Anchor (the grip) and Stabilized > VRIK point.
            Quaternion rr0 = RrAvatarHand.RecRoomInIkDefault(side);
            Vector3 palmOut0 = left ? rr0 * Vector3.right : -(rr0 * Vector3.right);
            Quaternion wristInGrip = Quaternion.LookRotation(rr0 * Vector3.forward, -palmOut0);
            Quaternion gripRot = wrist.rotation * Quaternion.Inverse(wristInGrip);
            Vector3 gripPos = wrist.position - gripRot * (RrAvatarHand.ClientIkPosition(side) * s);
            arm.Grip = new Pose(gripPos, gripRot);
            arm.HandOffset = new GameObject(left ? "LeftHandOffset" : "RightHandOffset").transform;
            arm.HandOffset.SetParent(_root, false);
            arm.HandOffset.SetPositionAndRotation(gripPos, gripRot);
            arm.HandOffset.localScale = Vector3.one * s;
            arm.Anchor = new GameObject(left ? "LeftHandAnchor" : "RightHandAnchor").transform;
            arm.Anchor.SetParent(arm.HandOffset, false);
            arm.Stabilized = new GameObject("Stabilized").transform;
            arm.Stabilized.SetParent(arm.HandOffset, false);
            arm.IkPoint = new GameObject(left ? "LeftHandVRIKPoint" : "RightHandVRIKPoint").transform;
            arm.IkPoint.SetParent(arm.Stabilized, false);
            arm.IkPoint.localPosition = RrAvatarHand.ClientIkPosition(side);
            arm.IkPoint.localRotation = RandomRotation();
            arm.IkPoint.localScale = Vector3.one * 0.08647f;
            Feed(true);
            return arm;
        }

        /// <summary>Zero-argument variant: both arms at <see cref="DefaultWrist"/>, scale 1, with bone visuals and frame axes.</summary>
        public static IEnumerator BuildDefaultArms()
        {
            BuildTestArm(RrHandSide.Left, DefaultWrist(RrHandSide.Left));
            BuildTestArm(RrHandSide.Right, DefaultWrist(RrHandSide.Right));
            DrawArms();
            yield return null;
            Log("built the default test arms: " + RrAvatarHand.Describe());
        }

        public static TestArm Arm(RrHandSide side)
        {
            return Arms[side == RrHandSide.Left ? 0 : 1];
        }

        /// <summary>Removes every test object and the override.</summary>
        public static void ClearTestArms()
        {
            DestroyArm(Arms[0]);
            DestroyArm(Arms[1]);
            Arms[0] = Arms[1] = null;
            foreach (GameObject g in Held)
            {
                if (g != null)
                {
                    Object.Destroy(g);
                }
            }

            Held.Clear();
            if (_rpm != null)
            {
                Object.Destroy(_rpm);
            }

            _rpm = null;
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = _avatar = _visuals = null;
            RrAvatarHand.ClearTestAvatar();
        }

        // ---- the test ----------------------------------------------------------------------------------------------

        /// <summary>All numeric checks (about 5 s: two 2-second waits for the one-shot IK comparison log).</summary>
        public static IEnumerator Run()
        {
            Failures.Clear();
            _pass = 0;
            _random = new System.Random(20260924);
            ClearTestArms();

            CheckConstants();

            // 1. Bones by name, scale 1, arbitrary poses, then scale 1.3 at other poses.
            var lw = new Pose(new Vector3(-0.4f, 1.1f, 0.3f), Quaternion.Euler(20f, -35f, 70f));
            var rw = new Pose(new Vector3(0.45f, 1.25f, 0.1f), Quaternion.Euler(-40f, 110f, -25f));
            BuildTestArm(RrHandSide.Left, lw);
            BuildTestArm(RrHandSide.Right, rw);
            yield return null;
            CheckBones("named-left", Arms[0], RrAvatarHandSource.NamedBones);
            CheckBones("named-right", Arms[1], RrAvatarHandSource.NamedBones);

            // Cost: a frame's typical use is a few queries (grab both hands, the watch).
            Pose pp;
            float fl;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int n = 0; n < 2000; n++)
            {
                RrAvatarHand.TryGetPalm(RrHandSide.Left, out pp);
                RrAvatarHand.TryGetWrist(RrHandSide.Right, out pp, out fl);
            }

            sw.Stop();
            double perCall = sw.Elapsed.TotalMilliseconds * 1000.0 / 4000.0;
            Result("cost", perCall < 12.5, perCall.ToString("0.00") + " us per query (bones by name), so 4 queries a frame = " + (perCall * 4 / 1000.0).ToString("0.000") + " ms");

            // The one-shot comparison with the IK constants runs 2 s after the avatar was adopted.
            yield return new WaitForSecondsRealtime(2.2f);
            Pose unused;
            RrAvatarHand.TryGetPalm(RrHandSide.Left, out unused);
            RrAvatarHand.TryGetPalm(RrHandSide.Right, out unused);

            BuildTestArm(RrHandSide.Left, new Pose(new Vector3(2f, 0.4f, -1f), Quaternion.Euler(-80f, 10f, 5f)), 1.3f);
            BuildTestArm(RrHandSide.Right, new Pose(new Vector3(-3f, 2f, 4f), Quaternion.Euler(33f, 200f, 150f)), 0.8f);
            yield return null;
            CheckBones("scaled-left", Arms[0], RrAvatarHandSource.NamedBones);
            CheckBones("scaled-right", Arms[1], RrAvatarHandSource.NamedBones);

            // 2. The avatar is destroyed (avatar change): the next query falls back to the IK target by itself.
            Object.Destroy(_avatar.gameObject);
            _avatar = null;
            yield return null;
            CheckIk("ik-left", Arms[0]);
            CheckIk("ik-right", Arms[1]);

            // 3. Nothing but the controller: the right screen hand of the harness (desktop), the left one is untracked.
            RrAvatarHand.SetTestAvatar(null, null, null, true);
            yield return null;
            CheckController();

            // 4. A real humanoid (editor projects that carry SomniumSpaceTemplates).
            yield return CheckHumanoid();

            ClearTestArms();
            Log("summary " + _pass + " passed, " + Failures.Count + " failed" + (Failures.Count > 0 ? ": " + string.Join("; ", Failures) : ""));
            if (Failures.Count == 0)
            {
                Log("ALL PASS");
            }
        }

        /// <summary>
        /// A scene to look at, four stations of two arms each (thumbs up, forearms forward, 0.4 m apart; bone sticks
        /// and frame axes: red X, green Y, blue Z, long = Rec Room frame at the palm, short = wrist):
        /// x -1.05 "old": the first build's placement (RrTools' FullBody-first choice, the pose inverted, under the
        /// grip): root beer bottle left, paintball pistol right. x -0.35 "new": the section-4 conversion on the drawn
        /// hand (legacy Pickup transform when set, else FullBody): bottle left, pistol right. x +0.35: cup left, sword
        /// right. x +1.05: charades pen left, paddle right. The RPM humanoid (when present) stands behind at x 1.6 with
        /// its drawn-hand frames.
        /// </summary>
        public static IEnumerator Scene()
        {
            ClearTestArms();
            string[,] tools =
            {
                { "(Consumables_RootBeer_Bottle)", "(PaintballGun)" },
                { "(Consumables_RootBeer_Bottle)", "(PaintballGun)" },
                { "(SoloCup)", "(QuestSword)" },
                { "(CharadesPen)", "(Paddleball_Paddle)" }
            };
            float[] xs = { -1.05f, -0.35f, 0.35f, 1.05f };
            for (int station = 0; station < xs.Length; station++)
            {
                foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
                {
                    Pose w = DefaultWrist(side);
                    w.position += new Vector3(xs[station], 0f, 0f);
                    BuildTestArm(side, w);
                }

                yield return null;
                DrawArms();
                yield return HoldTool(tools[station, 0], RrHandSide.Left, station == 0);
                yield return HoldTool(tools[station, 1], RrHandSide.Right, station == 0);
            }

            GameObject prefab = LoadRpm();
            if (prefab != null)
            {
                _rpm = Object.Instantiate(prefab, _root);
                _rpm.name = "Rr Test RPM";
                _rpm.transform.SetPositionAndRotation(new Vector3(1.6f, 0f, -0.6f), Quaternion.Euler(0f, 180f, 0f));
                RrAvatarHand.SetTestAvatar(_rpm.transform, null, null, false);
                yield return null;
                DrawFrames(RrHandSide.Left, 0.08f);
                DrawFrames(RrHandSide.Right, 0.08f);
                RrAvatarHand.SetTestAvatar(_avatar, Arms[0].IkPoint, Arms[1].IkPoint, false);
            }

            Log("scene ready: " + RrAvatarHand.Describe());
        }

        // ---- checks ------------------------------------------------------------------------------------------------

        private static void CheckConstants()
        {
            foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
            {
                string n = side == RrHandSide.Left ? "left" : "right";
                Pose palm = RrAvatarHand.GripToPalmDefault(side);
                Pose rr = RrAvatarHand.GripToRecRoomDefault(side);
                Quaternion expected = Quaternion.Normalize(side == RrHandSide.Left ? new Quaternion(0.5396f, -0.0777f, -0.0576f, 0.8363f) : new Quaternion(0.5396f, 0.0777f, 0.0577f, 0.8363f));
                float toAim = Quaternion.Angle(rr.rotation, RrHand.GripToAim);
                float toExpected = Quaternion.Angle(rr.rotation, expected);
                Vector3 fingers = rr.rotation * Vector3.forward;
                var expFingers = new Vector3(side == RrHandSide.Left ? -0.192f : 0.192f, -0.894f, 0.406f);
                Result("constants-" + n, palm.position.magnitude < 0.001f && Mathf.Abs(toAim - 26.7f) < 0.3f && toExpected < 0.2f && (fingers - expFingers).magnitude < 0.01f,
                       "GripToPalmDefault p " + RrHand.Fmt(palm.position) + ", Rec Room frame " + toAim.ToString("0.00") + " deg from GripToAim, " + toExpected.ToString("0.000") +
                       " deg from the worked-out value, fingers " + RrHand.Fmt(fingers) + ", palm " + Quaternion.Angle(palm.rotation, Quaternion.identity).ToString("0.0") + " deg from the grip");
            }

            Result("names", RrAvatarHand.PartFor(RrAvatarHand.Canon("mixamorig:LeftHand"), true) == "hand" && RrAvatarHand.PartFor(RrAvatarHand.Canon("hand_l"), true) == "hand" &&
                            RrAvatarHand.PartFor(RrAvatarHand.Canon("Bip01 L Hand"), true) == "hand" && RrAvatarHand.PartFor(RrAvatarHand.Canon("J_Bip_R_Hand"), false) == "hand" &&
                            RrAvatarHand.PartFor(RrAvatarHand.Canon("Left_Hand"), false) == null && RrAvatarHand.PartFor(RrAvatarHand.Canon("LeftHandIndex1"), true) != "hand" &&
                            RrAvatarHand.PartFor(RrAvatarHand.Canon("Hand.R"), false) == "hand" && RrAvatarHand.PartFor(RrAvatarHand.Canon("LeftHandle"), true) != "hand",
                   "mixamorig:LeftHand, hand_l, Bip01 L Hand, J_Bip_R_Hand, Hand.R match; Left_Hand not right; LeftHandIndex1 and LeftHandle not a hand");
        }

        private static void CheckBones(string name, TestArm arm, RrAvatarHandSource expectedSource)
        {
            RrHandSide side = arm.Side;
            bool left = side == RrHandSide.Left;
            float s = arm.Scale;
            Vector3 z = arm.Wrist.rotation * Vector3.forward;
            Vector3 back = arm.Wrist.rotation * Vector3.up;
            Vector3 x = arm.Wrist.rotation * Vector3.right;
            Vector3 thumbSide = left ? x : -x;
            Vector3 palmOut = -back;
            Quaternion expRr = Quaternion.LookRotation(z, thumbSide);
            Vector3 expPalm = arm.Wrist.position + z * (RrAvatarHand.PalmAlong * HandLength * s) + palmOut * (RrAvatarHand.PalmInward * HandLength * s) +
                              thumbSide * (RrAvatarHand.PalmThumbward * HandLength * s);
            Pose wrist, palm, hand;
            float forearm;
            bool okW = RrAvatarHand.TryGetWrist(side, out wrist, out forearm);
            bool okP = RrAvatarHand.TryGetPalm(side, out palm);
            bool okR = RrAvatarHand.TryGetRecRoomHand(side, out hand);
            RrAvatarHandSource src = RrAvatarHand.Source(side);
            float dW = (wrist.position - arm.Wrist.position).magnitude;
            float aW = Quaternion.Angle(wrist.rotation, arm.Wrist.rotation);
            float dP = (palm.position - expPalm).magnitude;
            float aP = Quaternion.Angle(palm.rotation, expRr * Quaternion.Inverse(RrHand.GripToAim));
            float aR = Quaternion.Angle(hand.rotation, expRr);
            float dF = Mathf.Abs(forearm - ForearmLength * s);
            // Rec Room frame semantics: +X out of the left palm / back of the right hand, +Y toward the thumb.
            float xSense = Vector3.Dot(hand.rotation * Vector3.right, left ? palmOut : back);
            float ySense = Vector3.Dot(hand.rotation * Vector3.up, thumbSide);
            bool ok = okW && okP && okR && src == expectedSource && dW < 0.0005f && aW < 0.2f && dP < 0.0005f && aP < 0.2f && aR < 0.2f && dF < 0.0005f && xSense > 0.999f && ySense > 0.999f;
            Result(name, ok, "source " + src + ", wrist " + Mm(dW) + " / " + aW.ToString("0.00") + " deg, forearm " + forearm.ToString("0.0000") + " m (want " + (ForearmLength * s).ToString("0.0000") +
                             "), palm " + Mm(dP) + " / " + aP.ToString("0.00") + " deg, Rec Room frame " + aR.ToString("0.00") + " deg, X.palm/back " + xSense.ToString("0.000") +
                             ", Y.thumb " + ySense.ToString("0.000") + ", scale " + s.ToString("0.0"));
        }

        private static void CheckIk(string name, TestArm arm)
        {
            RrHandSide side = arm.Side;
            float s = arm.Scale;
            Pose wrist, palm;
            float forearm;
            bool okW = RrAvatarHand.TryGetWrist(side, out wrist, out forearm);
            bool okP = RrAvatarHand.TryGetPalm(side, out palm);
            RrAvatarHandSource src = RrAvatarHand.Source(side);
            Pose def = RrAvatarHand.GripToPalmDefault(side);
            Vector3 expPalm = arm.Grip.position + arm.Grip.rotation * (def.position * s);
            Quaternion expPalmRot = arm.Grip.rotation * def.rotation;
            float dW = (wrist.position - arm.Wrist.position).magnitude;
            float aW = Quaternion.Angle(wrist.rotation, arm.Wrist.rotation);
            float dP = (palm.position - expPalm).magnitude;
            float aP = Quaternion.Angle(palm.rotation, expPalmRot);
            bool ok = okW && okP && src == RrAvatarHandSource.IkTarget && dW < 0.0005f && aW < 0.2f && dP < 0.0005f && aP < 0.2f &&
                      Mathf.Abs(forearm - RrAvatarHand.DefaultForearmLength * s) < 0.0005f;
            Result(name, ok, "source " + src + " after the avatar was destroyed, wrist " + Mm(dW) + " / " + aW.ToString("0.00") + " deg from the synthetic wrist, palm " + Mm(dP) +
                             " / " + aP.ToString("0.00") + " deg from grip * GripToPalmDefault, forearm " + forearm.ToString("0.000"));
        }

        private static void CheckController()
        {
            RrHand right = RrHand.Right;
            RrHand left = RrHand.Left;
            if (right == null || !right.Tracked || right.Grip == null)
            {
                Log("SKIP controller: no tracked right hand in this run (" + (right == null ? "no RrHand" : "untracked") + ")");
                return;
            }

            Pose palm, hand;
            bool ok = RrAvatarHand.TryGetPalm(RrHandSide.Right, out palm) & RrAvatarHand.TryGetRecRoomHand(RrHandSide.Right, out hand);
            RrAvatarHandSource src = RrAvatarHand.Source(RrHandSide.Right);
            RrAvatarHandSource leftSrc = RrAvatarHand.Source(RrHandSide.Left);
            float d = (palm.position - right.Grip.position).magnitude;
            float a = Quaternion.Angle(hand.rotation, right.AimRotation);
            bool leftOk = left == null || left.Tracked || leftSrc == RrAvatarHandSource.None;
            Result("controller", ok && src == RrAvatarHandSource.ControllerGuess && d < 0.0005f && a < 0.2f && leftOk,
                   "right source " + src + " (" + right.Source + "), palm " + Mm(d) + " from the grip, Rec Room frame " + a.ToString("0.00") + " deg from AimRotation; left " + leftSrc);
        }

        private static IEnumerator CheckHumanoid()
        {
            GameObject prefab = LoadRpm();
            if (prefab == null)
            {
                Log("SKIP humanoid: " + RpmPath + " is not loadable here (player build or project without SomniumSpaceTemplates)");
                yield break;
            }

            EnsureRoot();
            _rpm = Object.Instantiate(prefab, _root);
            _rpm.name = "Rr Test RPM";
            Quaternion facing = Quaternion.Euler(0f, 30f, 0f);
            _rpm.transform.SetPositionAndRotation(new Vector3(5f, 0f, 5f), facing);
            Animator anim = _rpm.GetComponentInChildren<Animator>();
            bool human = anim != null && anim.avatar != null && anim.avatar.isValid && anim.avatar.isHuman;
            Result("humanoid-detect", human, "RPM Men: Animator " + (anim != null) + ", humanoid Avatar " + human);
            if (!human)
            {
                yield break;
            }

            // The FBX rests in an A-pose; the client's bind pose is the Avatar's own skeleton (humanDescription.skeleton,
            // AnimatorHandler.GetDefaultBoneByName), so put the rig in that pose first.
            int posed = ApplySkeletonPose(_rpm.transform, anim);
            Log("RPM Men: applied the Avatar's skeleton pose to " + posed + " bones");
            RrAvatarHand.SetTestAvatar(_rpm.transform, null, null, false);
            yield return null;
            foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
            {
                bool left = side == RrHandSide.Left;
                string n = left ? "left" : "right";
                Transform handBone = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Transform lower = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                Transform middle = anim.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                Pose wrist, hand, palm;
                float forearm;
                RrAvatarHand.TryGetWrist(side, out wrist, out forearm);
                RrAvatarHand.TryGetRecRoomHand(side, out hand);
                RrAvatarHand.TryGetPalm(side, out palm);
                RrAvatarHandSource src = RrAvatarHand.Source(side);
                float handLen = middle != null ? Vector3.Distance(handBone.position, middle.position) : 0f;
                // Rest pose = T-pose: fingers along the model's -X (left) / +X (right), thumb forward, left palm down.
                Vector3 fingersWant = facing * (left ? Vector3.left : Vector3.right);
                Vector3 thumbWant = facing * Vector3.forward;
                Vector3 xWant = facing * (left ? Vector3.down : Vector3.up);
                float aF = Vector3.Angle(hand.rotation * Vector3.forward, fingersWant);
                float aT = Vector3.Angle(hand.rotation * Vector3.up, thumbWant);
                float aX = Vector3.Angle(hand.rotation * Vector3.right, xWant);
                bool ok = src == RrAvatarHandSource.HumanoidBones && handBone != null && (wrist.position - handBone.position).magnitude < 0.0005f &&
                          lower != null && Mathf.Abs(forearm - Vector3.Distance(handBone.position, lower.position)) < 0.0005f && aF < 25f && aT < 25f && aX < 25f;
                Result("humanoid-" + n, ok, "source " + src + ", hand '" + (handBone != null ? handBone.name : "?") + "', forearm " + forearm.ToString("0.000") + " m, wrist->middle knuckle " +
                                           handLen.ToString("0.000") + " m, rest-pose Rec Room frame vs ideal T-pose: fingers " + aF.ToString("0.0") + " deg, thumb " + aT.ToString("0.0") +
                                           " deg, X " + aX.ToString("0.0") + " deg; palm " + Mm((palm.position - wrist.position).magnitude) + " from the wrist");
            }

            // Re-create VRIK's hand target on the real rig: bone rotation = S * _handRot * Inverse(_handDefaultDelta) * B,
            // position = S-frame IK position; the drawn Rec Room frame should then be S * the constant.
            Quaternion bindRoot = Quaternion.Inverse(_rpm.transform.rotation);
            var stabilized = new Pose(new Vector3(5.3f, 1.2f, 5.4f), Quaternion.Euler(-15f, 40f, 12f));
            foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
            {
                Transform handBone = anim.GetBoneTransform(side == RrHandSide.Left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Quaternion bind = bindRoot * handBone.rotation;
                handBone.rotation = stabilized.rotation * RrAvatarHand.ClientHandRotation(side) * Quaternion.Inverse(RrAvatarHand.ClientHandDefaultDelta(side)) * bind;
                handBone.position = stabilized.position + stabilized.rotation * RrAvatarHand.ClientIkPosition(side);
            }

            yield return null;
            foreach (RrHandSide side in new[] { RrHandSide.Left, RrHandSide.Right })
            {
                string n = side == RrHandSide.Left ? "left" : "right";
                Pose hand, wrist;
                float forearm;
                RrAvatarHand.TryGetRecRoomHand(side, out hand);
                RrAvatarHand.TryGetWrist(side, out wrist, out forearm);
                Quaternion predicted = stabilized.rotation * RrAvatarHand.RecRoomInIkDefault(side);
                float a = Quaternion.Angle(hand.rotation, predicted);
                Vector3 inGrip = Quaternion.Inverse(stabilized.rotation) * (hand.position - stabilized.position);
                Result("vrik-" + n, a < 15f, "RPM hand posed like VRIK's target: drawn Rec Room frame " + a.ToString("0.0") + " deg from Stabilized * constant (the rig's rest pose vs an ideal T-pose); " +
                                            "drawn palm at " + RrHand.Fmt(inGrip) + " in the grip frame (reference hand: (0,0,0))");
            }

            Object.Destroy(_rpm);
            _rpm = null;
            RrAvatarHand.ClearTestAvatar();
        }

        // ---- helpers ---------------------------------------------------------------------------------------------

        /// <summary>Sets every bone named in the Avatar's skeleton (below the model root) to its skeleton local pose.</summary>
        private static int ApplySkeletonPose(Transform model, Animator anim)
        {
            SkeletonBone[] skeleton = anim.avatar.humanDescription.skeleton;
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t != model && !byName.ContainsKey(t.name))
                {
                    byName.Add(t.name, t);
                }
            }

            int n = 0;
            foreach (SkeletonBone b in skeleton)
            {
                Transform t;
                if (b.name != null && byName.TryGetValue(b.name, out t))
                {
                    t.localPosition = b.position;
                    t.localRotation = b.rotation;
                    n++;
                }
            }

            return n;
        }

        private static GameObject LoadRpm()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RpmPath);
#else
            return null;
#endif
        }

        private static void Feed(bool withIk)
        {
            RrAvatarHand.SetTestAvatar(_avatar, withIk && Arms[0] != null ? Arms[0].IkPoint : null, withIk && Arms[1] != null ? Arms[1].IkPoint : null, false);
        }

        private static void EnsureRoot()
        {
            if (_root == null)
            {
                _root = new GameObject("Rr Hands Test").transform;
                Transform world = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                if (world != null)
                {
                    _root.SetParent(world, false);
                }
            }

            if (_avatar == null)
            {
                _avatar = new GameObject("Rr Test Avatar").transform;
                _avatar.SetParent(_root, false);
                _avatar.rotation = RandomRotation();
            }

            if (_visuals == null)
            {
                _visuals = new GameObject("Rr Hands Test Visuals").transform;
                _visuals.SetParent(_root, false);
            }
        }

        private static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.rotation = RandomRotation();
            t.position = position;
            return t;
        }

        private static void DestroyArm(TestArm arm)
        {
            if (arm == null)
            {
                return;
            }

            if (arm.UpperArm != null)
            {
                Transform hips = arm.UpperArm.parent;
                Object.Destroy(hips != null && hips != _avatar ? hips.gameObject : arm.UpperArm.gameObject);
            }

            if (arm.HandOffset != null)
            {
                Object.Destroy(arm.HandOffset.gameObject);
            }
        }

        private static Quaternion RandomRotation()
        {
            var v = new Vector4((float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble() * 2 - 1), (float)(_random.NextDouble() * 2 - 1));
            if (v.sqrMagnitude < 1e-4f)
            {
                return Quaternion.identity;
            }

            v.Normalize();
            return new Quaternion(v.x, v.y, v.z, v.w);
        }

        /// <summary>
        /// Rec Room's hold pose for a tool: the legacy {Left,Right}HandPickupTransform when it is set (the tool's pose in
        /// the PlayerHand frame), else FullBody{Left,Right}HandGripTransform; with <paramref name="firstBuild"/>, the
        /// first build's rule instead (FullBody first).
        /// </summary>
        private static bool HoldPose(RrConfig interaction, RrHandSide side, bool firstBuild, out Vector3 gp, out Quaternion gr, out string field)
        {
            bool left = side == RrHandSide.Left;
            string legacy = left ? "LeftHandPickupTransform" : "RightHandPickupTransform";
            string full = left ? "FullBodyLeftHandGripTransform" : "FullBodyRightHandGripTransform";
            foreach (string f in firstBuild ? new[] { full, legacy } : new[] { legacy, full })
            {
                RrConfig t = interaction != null ? interaction.Sub(f) : null;
                if (t == null)
                {
                    continue;
                }

                gp = RrTools.V3(t.Raw("Position"));
                gr = RrTools.Q(t.Raw("Rotation"));
                if (gp.sqrMagnitude > 1e-8f)
                {
                    field = f;
                    return true;
                }
            }

            gp = Vector3.zero;
            gr = Quaternion.identity;
            field = null;
            return false;
        }

        private static IEnumerator HoldTool(string prefab, RrHandSide side, bool firstBuild)
        {
            RrInstance inst = null;
            bool done = false;
            RrSpawn.Spawn(prefab, _root, Vector3.zero, Quaternion.identity, i =>
            {
                inst = i;
                done = true;
            }, RrSpawnOptions.Visual());
            float until = Time.realtimeSinceStartup + 90f;
            while (!done && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (inst == null)
            {
                Log("SKIP hold " + prefab + ": did not spawn");
                yield break;
            }

            Held.Add(inst.gameObject);
            RrConfig tool = inst.Config("Tool");
            RrConfig interaction = tool != null ? tool.Sub("interactionSettings") : null;
            Vector3 gp;
            Quaternion gr;
            string field;
            if (!HoldPose(interaction, side, firstBuild, out gp, out gr, out field))
            {
                Log("SKIP hold " + prefab + ": no pickup/grip transform");
                yield break;
            }

            Transform t = inst.transform;
            float k = t.lossyScale.x;
            TestArm arm = Arm(side);
            Pose hand;
            string how;
            if (firstBuild)
            {
                // The first build (RrGrab): local rotation Inverse(gr), position -(Inverse(gr) * gp), under the grip.
                Quaternion rest = Quaternion.Inverse(gr);
                t.SetPositionAndRotation(arm.Grip.position + arm.Grip.rotation * -(rest * (gp * k)), arm.Grip.rotation * rest);
                how = "OLD placement (inverse pose under the grip)";
            }
            else if (RrAvatarHand.TryGetRecRoomHand(side, out hand))
            {
                // hands_frames.md section 4: Rec Room's transform is the tool's local pose in Rec Room's hand frame.
                t.SetPositionAndRotation(hand.position + hand.rotation * (gp * k), hand.rotation * gr);
                how = "drawn hand, section-4 conversion";
            }
            else
            {
                Log("SKIP hold " + prefab + ": no drawn hand");
                yield break;
            }

            Log("holding " + prefab + " in the " + (side == RrHandSide.Left ? "left" : "right") + " hand (" + how + "): " + field + " p " + RrHand.Fmt(gp) + " r " +
                RrHand.Fmt(gr.eulerAngles) + ", tool up " + RrHand.Fmt(t.up) + ", tool forward " + RrHand.Fmt(t.forward));
        }

        private static void DrawArms()
        {
            foreach (TestArm arm in Arms)
            {
                if (arm == null)
                {
                    continue;
                }

                Color c = arm.Side == RrHandSide.Left ? RrLook.Teal : RrLook.Orange;
                Stick(arm.UpperArm.position, arm.LowerArm.position, 0.03f * arm.Scale, c);
                Stick(arm.LowerArm.position, arm.Hand.position, 0.025f * arm.Scale, c);
                foreach (Transform f in new[] { arm.Index, arm.Middle, arm.Ring, arm.Little, arm.Thumb })
                {
                    Stick(arm.Hand.position, f.position, 0.012f * arm.Scale, RrLook.Cream);
                    Vector3 dir = f == arm.Thumb ? (f.position - arm.Hand.position).normalized : arm.Wrist.rotation * Vector3.forward;
                    Stick(f.position, f.position + dir * 0.06f * arm.Scale, 0.01f * arm.Scale, RrLook.Cream);
                }

                DrawFrames(arm.Side, 0.08f);
            }
        }

        private static void DrawFrames(RrHandSide side, float length)
        {
            Pose hand, wrist;
            float forearm;
            if (RrAvatarHand.TryGetRecRoomHand(side, out hand))
            {
                Axes(hand, length, 0.006f);
            }

            if (RrAvatarHand.TryGetWrist(side, out wrist, out forearm))
            {
                Axes(wrist, length * 0.5f, 0.004f);
            }
        }

        private static void Axes(Pose p, float length, float thick)
        {
            Stick(p.position, p.position + p.rotation * Vector3.right * length, thick, RrLook.Red);
            Stick(p.position, p.position + p.rotation * Vector3.up * length, thick, RrLook.Green);
            Stick(p.position, p.position + p.rotation * Vector3.forward * length, thick, RrLook.Blue);
        }

        private static void Stick(Vector3 a, Vector3 b, float thick, Color c)
        {
            EnsureRoot();
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f)
            {
                return;
            }

            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(g.GetComponent<Collider>());
            g.name = "Rr Test Stick";
            g.transform.SetParent(_visuals, false);
            g.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d));
            g.transform.localScale = new Vector3(thick, thick, d.magnitude);
            g.GetComponent<Renderer>().sharedMaterial = RrLook.Unlit(c);
        }

        private static string Mm(float metres)
        {
            return (metres * 1000f).ToString("0.00") + " mm";
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
            RrLog.Info("Hands", "HandsTest: " + text);
        }
    }
}
