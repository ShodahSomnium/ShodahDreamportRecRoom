using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Harness hooks for the hold pose (scenario SP/harness/scenarios/grab_pose.json; nothing here runs unless called).
    ///
    /// <see cref="Vr"/> puts both hands in VR test mode (<see cref="RrHand.SetTestVr"/>) on fake client anchors whose
    /// grips are a synthetic UPRIGHT FIST POINTING FORWARD (thumb up, knuckles to +Z: grip = Euler(-90, 0, 0), so the
    /// hand's aim is world forward). The right anchor is scaled 0.889 (the user's rig) and carries a made-up
    /// calibration offset, so the wire check below is not trivially exact. It holds Rec Room's root beer bottle (left)
    /// and paintball pistol (right), then the basketball (left) and the paddleball paddle (right), through
    /// <see cref="RrGrab.Give"/> - the path games and the grip button use - and logs each held object's world axes
    /// against what a hand holding it should show: "GrabTest: PASS|FAIL name (...)". Expectations (hands_frames.md 4,
    /// DN/phase1/grab.md): bottle up ~18 deg back from world up (it follows the drawn fingers, which point 24 deg
    /// above the fist's front), pistol barrel ~11 deg from the hand's aim, ball centre ~12.6 cm to the palm side,
    /// paddle head ~18 deg from up; each object also exactly at grip * HoldFrame * (gp, gr), and each wire pose
    /// rebuilt on the anchor within quantisation. The first build's placement is logged next to it.
    ///
    /// <see cref="Screen"/> holds the bottle, then the pistol, in the screen hand with the view pinned to
    /// <see cref="ScreenEye"/> looking +Z (<see cref="RrHand.TestView"/>), so the scenario's shots from that pose
    /// are the player's view. <see cref="Clear"/> ends test mode and removes everything.
    /// </summary>
    public static class RrGrabTest
    {
        public const string Bottle = "(Consumables_RootBeer_Bottle)";
        public const string Pistol = "(PaintballGun)";
        public const string Ball = "(Basketball)";
        public const string Paddle = "(Paddleball_Paddle)";

        /// <summary>The fists (grip centres) for the VR checks, in front of the shot cameras of grab_pose.json.</summary>
        public static readonly Vector3 LeftFist = new Vector3(-0.2f, 1.25f, -2.2f);
        public static readonly Vector3 RightFist = new Vector3(0.2f, 1.25f, -2.2f);

        /// <summary>The pinned screen-mode eye (looking +Z) for <see cref="Screen"/>.</summary>
        public static readonly Vector3 ScreenEye = new Vector3(0f, 1.55f, -2.6f);

        /// <summary>An upright fist pointing forward, in the grip convention: +Z (thumb) up, -Y (front of the fist) forward.</summary>
        public static readonly Quaternion UprightFist = Quaternion.Euler(-90f, 0f, 0f);

        private static readonly Dictionary<string, RrInstance> Spawned = new Dictionary<string, RrInstance>();
        private static readonly List<string> Failures = new List<string>();
        private static readonly List<GameObject> Made = new List<GameObject>();
        private static Transform _root;
        private static Transform _anchorL, _anchorR;
        private static int _pass;

        private static void Log(string text)
        {
            RrLog.Info("Grab", "GrabTest: " + text);
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

        // ------------------------------------------------------------------ VR

        public static IEnumerator Vr()
        {
            Failures.Clear();
            _pass = 0;
            EnsureRoot();
            if (RrHand.Left == null || RrHand.Right == null)
            {
                Result("hands", false, "RrHand.Left/Right missing (the Player module did not start)");
                yield break;
            }

            // Fake client anchors: grip = anchor * offset * (1 / anchor scale), so the anchors go where that grip is the fist.
            var offL = new Pose(Vector3.zero, Quaternion.identity);
            var offR = new Pose(new Vector3(0.004f, -0.012f, 0.02f), Quaternion.Euler(4f, -7f, 3f));
            _anchorL = Anchor("Rr Test Anchor L", LeftFist, offL, 1f);
            _anchorR = Anchor("Rr Test Anchor R", RightFist, offR, 0.889f);
            RrHand.SetTestVr(_anchorL, offL, _anchorR, offR);
            yield return null;
            yield return null;
            float gripErr = (RrHand.Right.Grip.position - RightFist).magnitude + (RrHand.Left.Grip.position - LeftFist).magnitude;
            float gripAng = Quaternion.Angle(RrHand.Right.Grip.rotation, UprightFist) + Quaternion.Angle(RrHand.Left.Grip.rotation, UprightFist);
            Result("test-rig", RrHand.VrMode && RrHand.Right.Source == RrHand.PoseSource.BridgeAnchor && gripErr < 0.001f && gripAng < 0.1f,
                "VrMode " + RrHand.VrMode + ", right source " + RrHand.Right.Source + ", grips " + Mm(gripErr) + " / " + gripAng.ToString("0.00") + " deg from the upright fists, right grip lossy scale " +
                RrHand.Right.Grip.lossyScale.x.ToString("0.000") + ", aim " + (RrHand.Right.AimRotation * Vector3.forward).ToString("F2"));
            DrawHand(RrHand.Left);
            DrawHand(RrHand.Right);

            yield return SpawnAll(new[] { Bottle, Pistol, Ball, Paddle });

            // Round 1: bottle left, pistol right.
            yield return Hold(Bottle, RrHand.Left);
            yield return Hold(Pistol, RrHand.Right);
            CheckBottle(Get(Bottle), RrHand.Left);
            CheckPistol(Get(Pistol), RrHand.Right);
            CheckWire(Bottle, RrHand.Left, _anchorL);
            CheckWire(Pistol, RrHand.Right, _anchorR);
            Log("round 1 held (bottle left, pistol right) - shots grab_vr_1_*");
        }

        /// <summary>Round 2: the bottle and pistol go back; the basketball (left) and the paddle (right).</summary>
        public static IEnumerator VrRound2()
        {
            Park(Bottle, new Vector3(-1.2f, 0.05f, -1.2f));
            Park(Pistol, new Vector3(-0.8f, 0.05f, -1.2f));
            yield return Hold(Ball, RrHand.Left);
            yield return Hold(Paddle, RrHand.Right);
            CheckBall(Get(Ball), RrHand.Left);
            CheckPaddle(Get(Paddle), RrHand.Right);
            CheckWire(Ball, RrHand.Left, _anchorL);
            CheckWire(Paddle, RrHand.Right, _anchorR);
            Log("round 2 held (basketball left, paddle right) - shots grab_vr_2_*");
        }

        /// <summary>Ends VR test mode (grips back under the hands) and reports.</summary>
        public static IEnumerator VrEnd()
        {
            Park(Ball, new Vector3(0.8f, 0.2f, -1.2f));
            Park(Paddle, new Vector3(1.2f, 0.05f, -1.2f));

            // No Rec Room pose: grabbed where it is (VR keeps it there), and given (centred in the fist, upright in the hand).
            var hand = RrHand.Right;
            var box = NoPoseBox(RightFist + new Vector3(0.03f, 0.05f, 0.06f), Quaternion.Euler(10f, 30f, 5f));
            Vector3 was = box.transform.position;
            Quaternion wasRot = box.transform.rotation;
            RrGrab.Grab(hand, box, true);
            yield return null;
            yield return null;
            float moved = (box.transform.position - was).magnitude;
            float turned = Quaternion.Angle(box.transform.rotation, wasRot);
            Result("vr-nopose-where", box.IsHeld && box.transform.parent == hand.Grip && moved < 0.0005f && turned < 0.1f,
                "held " + box.IsHeld + ", on the grip " + (box.transform.parent == hand.Grip) + ", moved " + Mm(moved) + " / " + turned.ToString("0.00") + " deg (held where it was taken)");
            RrGrab.Release(box, Vector3.zero, Vector3.zero);
            RrGrab.Give(box, hand);
            yield return null;
            Pose frame = hand.RecRoomFrame;
            Vector3 centre = hand.Grip.TransformPoint(frame.position);
            Quaternion upright = hand.Grip.rotation * frame.rotation;
            float off = (box.transform.position - centre).magnitude;
            float ang = Quaternion.Angle(box.transform.rotation, upright);
            Result("vr-nopose-given", box.IsHeld && off < 0.001f && ang < 0.1f,
                "given without a pose: centre " + Mm(off) + " from the fist centre, " + ang.ToString("0.00") + " deg from the hand frame (+Y up the thumb side: " +
                Vector3.Angle(box.transform.up, Vector3.up).ToString("0.0") + " deg from world up in the upright fist)");
            RrGrab.Release(box, Vector3.zero, Vector3.zero);
            Object.Destroy(box.gameObject);

            RrHand.SetTestVr(null, Pose.identity, null, Pose.identity);
            yield return null;
            yield return null;
            Result("test-mode-off", !RrHand.VrMode && RrHand.Right.Grip.parent == RrHand.Right.transform && RrHand.Left.Grip.parent == RrHand.Left.transform,
                "VrMode " + RrHand.VrMode + ", grips back under the hands " + (RrHand.Right.Grip.parent == RrHand.Right.transform) + "/" + (RrHand.Left.Grip.parent == RrHand.Left.transform) +
                ", right source " + RrHand.Right.Source);
            Summary("VR");
        }

        // ------------------------------------------------------------------ screen

        public static IEnumerator Screen()
        {
            Failures.Clear();
            _pass = 0;
            EnsureRoot();
            if (Spawned.Count == 0)
            {
                yield return SpawnAll(new[] { Bottle, Pistol });
            }

            RrHand.TestView = new Pose(ScreenEye, Quaternion.identity);
            yield return null;
            yield return null;
            var hand = RrHand.Right;
            Result("screen-hand", hand != null && hand.IsScreenHand && !RrHand.VrMode,
                "source " + (hand != null ? hand.Source.ToString() : "none") + ", grip " + (hand != null ? RrHand.Fmt(hand.Grip.position) : "-") + ", aim " +
                (hand != null ? (hand.AimRotation * Vector3.forward).ToString("F2") : "-"));
            if (hand == null)
            {
                yield break;
            }

            yield return Hold(Bottle, hand);
            var b = Get(Bottle);
            if (b != null)
            {
                Vector3 up = b.transform.up;
                Vector3 c = Centre(b);
                Vector3 inView = c - ScreenEye;
                float elev = Mathf.Atan2(inView.y, inView.z) * Mathf.Rad2Deg;
                float az = Mathf.Atan2(inView.x, inView.z) * Mathf.Rad2Deg;
                float lean = Vector3.Angle(up, Vector3.up);
                Result("screen-bottle", lean < 25f && az > 5f && az < 40f && elev < 0f && elev > -29f,
                    "up " + lean.ToString("0.0") + " deg from the view's up (Rec Room's potion idle: 15.6, leaning to the face: z " + up.z.ToString("0.00") + "), centre " +
                    az.ToString("0.0") + " deg right, " + (-elev).ToString("0.0") + " deg below the view centre, " + (inView.magnitude * 100f).ToString("0") + " cm away; screen hand from " +
                    (b.HasScreenHand ? "its IdleAnimation " + b.ScreenHandPosition.ToString("F3") : "the default"));
                CheckScreenWire(Bottle, hand);
            }

            Log("screen bottle held - shot grab_screen_bottle");
        }

        public static IEnumerator ScreenPistol()
        {
            var hand = RrHand.Right;
            Park(Bottle, new Vector3(-1.2f, 0.05f, -1.2f));
            yield return Hold(Pistol, hand);
            var g = Get(Pistol);
            if (g != null)
            {
                Vector3 barrel = BarrelDir(g);
                Vector3 c = Centre(g) - ScreenEye;
                float elev = Mathf.Atan2(c.y, c.z) * Mathf.Rad2Deg;
                float az = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg;
                float off = Vector3.Angle(barrel, Vector3.forward);
                Result("screen-pistol", off < 8f && az > 3f && az < 35f && elev < 0f && elev > -29f,
                    "barrel " + off.ToString("0.0") + " deg from the view's forward (Rec Room's AttackReady: 4.1), gun " + az.ToString("0.0") + " deg right, " + (-elev).ToString("0.0") +
                    " deg below the centre, " + (c.magnitude * 100f).ToString("0") + " cm away; screen hand from " + (g.HasScreenHand ? "its IdleAnimation" : "the default") +
                    "; " + RrScreenHold.Describe());
                CheckScreenWire(Pistol, hand);
                CheckRuntimeRead(g);
            }

            Log("screen pistol held - shot grab_screen_pistol");
        }

        public static IEnumerator ScreenEnd()
        {
            Park(Pistol, new Vector3(-0.8f, 0.05f, -1.2f));

            // No Rec Room pose in screen mode: centred on the screen hand, upright to the view (Rec Room's lower right).
            var hand = RrHand.Right;
            var box = NoPoseBox(new Vector3(0.5f, 0.3f, -1.5f), Quaternion.Euler(0f, 40f, 20f));
            RrGrab.Give(box, hand);
            yield return new WaitForSeconds(0.3f);
            Vector3 want = ScreenEye + RrHand.ScreenOffset;
            float off = (box.transform.position - want).magnitude;
            float ang = Quaternion.Angle(box.transform.rotation, Quaternion.identity);
            Result("screen-nopose", box.IsHeld && off < 0.001f && ang < 0.1f,
                "centred " + Mm(off) + " from the screen hand (" + RrHand.Fmt(RrHand.ScreenOffset) + " in the view), " + ang.ToString("0.00") + " deg from upright to the view (the first build: " +
                Quaternion.Angle(Quaternion.Euler(-90f, 0f, 0f), Quaternion.identity).ToString("0") + " deg, its up toward the camera)");
            RrGrab.Release(box, Vector3.zero, Vector3.zero);
            Object.Destroy(box.gameObject);
            RrHand.TestView = null;
            Summary("screen");
        }

        /// <summary>An 8 cm grabbable box with no Rec Room data (kinematic, on the grabbable layer).</summary>
        private static RrGrabbable NoPoseBox(Vector3 at, Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Rr Test No-Pose Box";
            go.layer = RrLayers.Grabbable;
            go.transform.SetParent(_root, false);
            go.transform.SetPositionAndRotation(at, rotation);
            go.transform.localScale = Vector3.one * 0.08f;
            go.GetComponent<Renderer>().sharedMaterial = RrLook.Unlit(RrLook.Red);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            return go.AddComponent<RrGrabbable>();
        }

        /// <summary>Everything off: test modes, held objects, spawned props and drawings.</summary>
        public static void Clear()
        {
            foreach (var kv in Spawned)
            {
                if (kv.Value != null)
                {
                    var g = kv.Value.GetComponent<RrGrabbable>();
                    if (g != null && g.IsHeld)
                    {
                        RrGrab.Release(g, Vector3.zero, Vector3.zero);
                    }

                    Object.Destroy(kv.Value.gameObject);
                }
            }

            Spawned.Clear();
            RrHand.TestView = null;
            if (RrHand.VrMode && !RrPlayer.InVr)
            {
                RrHand.SetTestVr(null, Pose.identity, null, Pose.identity);
            }

            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
            _anchorL = _anchorR = null;
            Made.Clear();
            Log("cleared");
        }

        // ------------------------------------------------------------------ checks

        private static void CheckBottle(RrGrabbable g, RrHand hand)
        {
            if (!Exact("bottle-pose", g, hand))
            {
                return;
            }

            float up = Vector3.Angle(g.transform.up, Vector3.up);
            float old = Vector3.Angle(OldRotation(g, hand) * Vector3.up, Vector3.up);
            Result("bottle", up < 30f && g.transform.up.z < 0f,
                "bottle up " + up.ToString("0.0") + " deg from world up (expected 17.9: the drawn fingers point 24 deg above the fist's front; leans " + (g.transform.up.z < 0f ? "back" : "forward") +
                "); the first build's placement: " + old.ToString("0.0") + " deg (lying along the forearm); from " + g.GripSource);
        }

        private static void CheckPistol(RrGrabbable g, RrHand hand)
        {
            if (!Exact("pistol-pose", g, hand))
            {
                return;
            }

            Vector3 aim = hand.AimRotation * Vector3.forward;
            Vector3 barrel = BarrelDir(g);
            Vector3 fingers = hand.Grip.rotation * hand.RecRoomFrame.rotation * Vector3.forward;
            float toAim = Vector3.Angle(barrel, aim);
            float old = Vector3.Angle(OldRotation(g, hand) * Vector3.forward, aim);
            Vector3 handle = g.transform.position - hand.Grip.position;
            Result("pistol", toAim < 15f && barrel.y > -0.2f,
                "gun barrel " + toAim.ToString("0.0") + " deg from the hand's aim (expected 11.4: 4 up, 10.7 outward, along the drawn fingers; " + Vector3.Angle(barrel, fingers).ToString("0.0") +
                " deg below the fingers = the pose's 20), barrel " + barrel.ToString("F2") + "; gun root " + Mm(handle.magnitude) + " from the grip; the first build's: " + old.ToString("0.0") +
                " deg (at the floor); from " + g.GripSource + ", lock on pickup " + g.LockOnPickup);
        }

        private static void CheckBall(RrGrabbable g, RrHand hand)
        {
            if (!Exact("ball-pose", g, hand))
            {
                return;
            }

            // Left hand: Rec Room's +X is out of the palm.
            Quaternion rr = hand.Grip.rotation * hand.RecRoomFrame.rotation;
            Vector3 palm = rr * (hand.Side == RrHandSide.Left ? Vector3.right : Vector3.left);
            Vector3 d = Centre(g) - hand.Grip.position;
            float side = Vector3.Dot(d, palm);
            Vector3 oldCentre = hand.Grip.TransformPoint(OldPosition(g, hand)) - hand.Grip.position;
            Result("basketball", side > 0.08f && d.magnitude < 0.2f,
                "ball centre " + (side * 100f).ToString("0.0") + " cm to the palm side, " + (d.magnitude * 100f).ToString("0.0") + " cm from the grip (expected 12.6 on the palm side; radius 12.4); the first build's: " +
                (Vector3.Dot(oldCentre, palm) * 100f).ToString("0.0") + " cm (on the back of the hand); from " + g.GripSource);
        }

        private static void CheckPaddle(RrGrabbable g, RrHand hand)
        {
            if (!Exact("paddle-pose", g, hand))
            {
                return;
            }

            var inst = g.GetComponent<RrInstance>();
            Transform pivot = inst != null ? inst.Find("Pivot") : null;
            Vector3 head = pivot != null ? pivot.up : g.transform.up;
            float up = Vector3.Angle(head, Vector3.up);
            Result("paddle", up < 35f,
                "paddle head " + up.ToString("0.0") + " deg from world up, " + Vector3.Angle(head, Vector3.forward).ToString("0.0") + " from forward (expected 17.6 / 74; " +
                (pivot != null ? "Pivot +Y" : "root +Y, no Pivot") + "); from " + g.GripSource + ", lock on pickup " + g.LockOnPickup);
        }

        /// <summary>The object is exactly at grip * HoldFrame * (gp, gr) (the code path, independent of the numbers).</summary>
        private static bool Exact(string name, RrGrabbable g, RrHand hand)
        {
            if (g == null || !g.IsHeld || g.Holder != hand)
            {
                Result(name, false, "not held by the " + (hand != null ? hand.Side.ToString() : "?") + " hand (" + (g == null ? "no tool" : "holder " + (g.Holder != null ? g.Holder.Side.ToString() : "none")) + ")");
                return false;
            }

            if (!g.HasGrip)
            {
                Result(name, false, g.name + " has no Rec Room hold pose");
                return false;
            }

            Vector3 gp;
            Quaternion gr;
            g.GripFor(hand.Side, out gp, out gr);
            Pose frame = RrAvatarHand.GripToRecRoomDefault(hand.Side);
            Transform grip = hand.Grip;
            Vector3 wantPos = grip.TransformPoint(frame.position + frame.rotation * gp);
            Quaternion wantRot = grip.rotation * frame.rotation * gr;
            float dp = (g.transform.position - wantPos).magnitude;
            float da = Quaternion.Angle(g.transform.rotation, wantRot);
            Result(name, dp < 0.001f && da < 0.1f, g.name + " at grip * GripToRecRoomDefault * (gp, gr): " + Mm(dp) + " / " + da.ToString("0.000") + " deg; gp " + gp.ToString("F4") + " gr " + gr.eulerAngles.ToString("F1"));
            return true;
        }

        /// <summary>The wire pose (sender) rebuilt on the anchor (receiver) against the object's real pose, and the first build's pair.</summary>
        private static void CheckWire(string prefab, RrHand hand, Transform anchor)
        {
            var g = Get(prefab);
            if (g == null || !g.IsHeld || anchor == null)
            {
                return;
            }

            Transform t = g.transform;
            Vector3 wp;
            Quaternion wr;
            RrNetBody.WireHeldPose(hand, t.localPosition, t.localRotation, out wp, out wr);
            bool left = hand.Side == RrHandSide.Left;
            var rec = new int[RrPresenceWire.Ints];
            RrPresenceWire.PutGrip(rec, left, wp, wr);
            Vector3 pos;
            Quaternion rot;
            RrNetBody.PlaceFromWire(anchor, null, null, RrPresenceWire.GripPosition(rec, left), RrPresenceWire.GripRotation(rec, left), out pos, out rot);
            float dp = (pos - t.position).magnitude;
            float da = Quaternion.Angle(rot, t.rotation);
            float oldDp = (anchor.TransformPoint(t.localPosition) - t.position).magnitude;
            float oldDa = Quaternion.Angle(anchor.rotation * t.localRotation, t.rotation);
            Result("wire-" + prefab.Trim('(', ')'), dp < 0.002f && da < 0.5f,
                "rebuilt on the " + hand.Side + " anchor " + Mm(dp) + " / " + da.ToString("0.00") + " deg from the held pose (wire " + wp.ToString("F3") + "); the first build's sender + receiver: " +
                Mm(oldDp) + " / " + oldDa.ToString("0.0") + " deg (anchor scale " + anchor.lossyScale.x.ToString("0.000") + ")");
        }

        /// <summary>Screen hand: a remote viewer's copy at our head (eye - ScreenEye) in our yaw, with the view level.</summary>
        private static void CheckScreenWire(string prefab, RrHand hand)
        {
            var g = Get(prefab);
            if (g == null || !g.IsHeld)
            {
                return;
            }

            Transform t = g.transform;
            Vector3 wp;
            Quaternion wr;
            RrNetBody.WireHeldPose(hand, t.localPosition, t.localRotation, out wp, out wr);
            var head = new GameObject("Rr Test Remote Head").transform;
            head.SetParent(_root, false);
            head.SetPositionAndRotation(ScreenEye - RrNetBody.ScreenEye, Quaternion.identity);
            Made.Add(head.gameObject);
            Vector3 pos;
            Quaternion rot;
            RrNetBody.PlaceFromWire(null, head, head, wp, wr, out pos, out rot);
            float dp = (pos - t.position).magnitude;
            float da = Quaternion.Angle(rot, t.rotation);
            Result("screen-wire-" + prefab.Trim('(', ')'), dp < 0.002f && da < 0.5f,
                "a desktop holder's prop rebuilt at their head and yaw " + Mm(dp) + " / " + da.ToString("0.00") + " deg from where they see it (view level); the first build drew it nowhere (desktop players have no Bridge hands)");
        }

        /// <summary>The pistol's IdleAnimation read from its own bundle agrees with the measured table.</summary>
        private static void CheckRuntimeRead(RrGrabbable g)
        {
            RrScreenHold.Entry known;
            if (!RrScreenHold.TryKnown(432506639366587634L, out known))
            {
                return;
            }

            float dp = (g.ScreenHandPosition - known.Position).magnitude;
            float da = Quaternion.Angle(g.ScreenHandRotation, known.Rotation);
            Result("screen-hand-data", g.HasScreenHand && dp < 0.001f && da < 0.1f,
                "pistol's screen hand " + g.ScreenHandPosition.ToString("F4") + " vs the table's " + known.Name + " " + known.Position.ToString("F4") + ": " + Mm(dp) + " / " + da.ToString("0.00") + " deg");
        }

        private static void Summary(string what)
        {
            Log(what + " summary: " + _pass + " passed, " + Failures.Count + " failed" + (Failures.Count > 0 ? " (" + string.Join(", ", Failures) + ")" : ""));
            if (Failures.Count == 0)
            {
                Log(what + " ALL PASS");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Quaternion OldRotation(RrGrabbable g, RrHand hand)
        {
            Vector3 gp;
            Quaternion gr;
            g.GripFor(hand.Side, out gp, out gr);
            return hand.Grip.rotation * Quaternion.Inverse(gr);
        }

        private static Vector3 OldPosition(RrGrabbable g, RrHand hand)
        {
            Vector3 gp;
            Quaternion gr;
            g.GripFor(hand.Side, out gp, out gr);
            return -(Quaternion.Inverse(gr) * gp);
        }

        /// <summary>The pistol's barrel: a Rec Room barrel/muzzle node's forward when there is one, else the root's +Z.</summary>
        private static Vector3 BarrelDir(RrGrabbable g)
        {
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("muzzle") || n == "barrel")
                {
                    return t.forward;
                }
            }

            return g.transform.forward;
        }

        private static Vector3 Centre(RrGrabbable g)
        {
            var inst = g.GetComponent<RrInstance>();
            if (inst != null)
            {
                Bounds b = inst.WorldBounds();
                if (b.size != Vector3.zero)
                {
                    return b.center;
                }
            }

            return g.transform.position;
        }

        private static RrGrabbable Get(string prefab)
        {
            RrInstance inst;
            return Spawned.TryGetValue(prefab, out inst) && inst != null ? inst.GetComponent<RrGrabbable>() : null;
        }

        private static IEnumerator Hold(string prefab, RrHand hand)
        {
            var g = Get(prefab);
            if (g == null || hand == null)
            {
                Result("hold-" + prefab, false, "no tool " + prefab);
                yield break;
            }

            RrGrab.Give(g, hand);
            // The slide into the hand (0.1 s VR, 0.18 s screen) plus a few frames.
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static void Park(string prefab, Vector3 at)
        {
            var g = Get(prefab);
            if (g == null)
            {
                return;
            }

            if (g.IsHeld)
            {
                RrGrab.Release(g, Vector3.zero, Vector3.zero);
            }

            g.transform.SetPositionAndRotation(at, Quaternion.identity);
            if (g.Body != null)
            {
                g.Body.position = at;
                g.Body.linearVelocity = Vector3.zero;
                g.Body.angularVelocity = Vector3.zero;
                g.Body.isKinematic = true;
            }
        }

        private static IEnumerator SpawnAll(string[] prefabs)
        {
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            int pending = 0;
            for (int i = 0; i < prefabs.Length; i++)
            {
                string prefab = prefabs[i];
                if (Get(prefab) != null)
                {
                    continue;
                }

                pending++;
                var at = new Vector3(-1.2f + 0.8f * i, 0.1f, -1.2f);
                RrSpawn.Spawn(prefab, root, at, Quaternion.identity, inst =>
                {
                    if (inst != null)
                    {
                        RrTools.MakeTool(inst);
                        var body = inst.GetComponent<Rigidbody>();
                        if (body != null)
                        {
                            body.isKinematic = true;
                        }
                    }

                    Spawned[prefab] = inst;
                    pending--;
                }, RrSpawnOptions.Prop());
            }

            float until = Time.realtimeSinceStartup + 180f;
            while (pending > 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            var names = new List<string>();
            foreach (string p in prefabs)
            {
                var g = Get(p);
                names.Add(p + (g == null ? " MISSING" : g.HasGrip ? " " + g.GripSource : " no pose"));
            }

            Result("spawn", pending == 0 && names.TrueForAll(s => !s.Contains("MISSING")), string.Join(", ", names));
        }

        private static Transform Anchor(string name, Vector3 fist, Pose offset, float scale)
        {
            var a = new GameObject(name).transform;
            a.SetParent(_root, false);
            a.localScale = Vector3.one * scale;
            Quaternion rot = UprightFist * Quaternion.Inverse(offset.rotation);
            a.SetPositionAndRotation(fist - rot * (offset.position * scale), rot);
            return a;
        }

        private static void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = new GameObject("Rr Grab Test").transform;
            var world = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _root.SetParent(world, false);
        }

        /// <summary>A stick hand for the pictures: Rec Room's frame (red X, green Y, blue Z = fingers) and a forearm.</summary>
        private static void DrawHand(RrHand hand)
        {
            Transform g = hand.Grip;
            Quaternion rr = g.rotation * hand.RecRoomFrame.rotation;
            Vector3 p = g.position;
            Stick(p, p + rr * Vector3.right * 0.07f, 0.006f, RrLook.Red);
            Stick(p, p + rr * Vector3.up * 0.07f, 0.006f, RrLook.Green);
            Stick(p, p + rr * Vector3.forward * 0.09f, 0.006f, RrLook.Blue);
            Stick(p + rr * new Vector3(0f, 0f, -0.07f), p + rr * new Vector3(0f, 0f, -0.33f), 0.05f, hand.Side == RrHandSide.Left ? RrLook.Teal : RrLook.Orange);
            Stick(p + rr * new Vector3(0f, 0f, -0.05f), p + rr * new Vector3(0f, 0f, 0.05f), 0.07f, RrLook.Cream);
        }

        private static void Stick(Vector3 a, Vector3 b, float thick, Color c)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f)
            {
                return;
            }

            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(s.GetComponent<Collider>());
            s.name = "Rr Test Stick";
            s.transform.SetParent(_root, false);
            s.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d));
            s.transform.localScale = new Vector3(thick, thick, d.magnitude);
            s.GetComponent<Renderer>().sharedMaterial = RrLook.Unlit(c);
            Made.Add(s);
        }

        private static string Mm(float metres)
        {
            return (metres * 1000f).ToString("0.00") + " mm";
        }
    }
}
