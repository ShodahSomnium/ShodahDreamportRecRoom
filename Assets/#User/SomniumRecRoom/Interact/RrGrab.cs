using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// The grab system: hover, highlight, grab, hold, use, throw and drop, for both VR hands and the screen-mode hand,
    /// reading <see cref="RrHand"/> and <see cref="RrInput"/>.
    ///
    /// VR (somnium_player.md §3-4): the client grabs only XRI interactables on its interaction layer 3, never plain
    /// rigidbodies, so our props are ours alone - the hand's grip (hysteresis in RrHand) picks up the nearest
    /// grabbable within 12 cm of the grip frame, the trigger uses what is held, opening the grip throws it with the
    /// hand's velocity (plus the spin's share at the object's centre) times the tool's ThrowSpeedMultiplier, clamped
    /// to its MaxThrowSpeed when it has one. Held objects hang off <see cref="RrHand.Grip"/> (parenting gives the
    /// before-render pose for free) at the tool's Rec Room hold pose.
    ///
    /// <b>The hold pose (fixed 2026-09-24 after the user's VR test: "they need to be rotated up 90 degrees").</b> Rec
    /// Room's {Left,Right}HandPickupTransform / FullBody*GripTransform is the TOOL's local pose in Rec Room's hand
    /// frame (+Z fingers, +Y thumb, +X out of the left palm), so a held tool sits at grip * <see cref="HoldFrame"/> *
    /// (gp, gr). The first build used Inverse(gr) and -(Inverse(gr) * gp) straight in the grip frame, whose +Z is the
    /// thumb axis: a bottle lay along the forearm (96 deg from upright in an upright fist) and a pistol pointed at the
    /// floor (110 deg from the aim). Now, for an upright fist pointing forward: bottle 17.9 deg back from upright,
    /// following the drawn fingers; pistol barrel 11.4 deg from the controller's aim (4 deg up, 10.7 outward, along
    /// the drawn fingers); basketball centre 12.6 cm on the palm side (hands_frames.md 4; harness grab_pose.json).
    /// The throw velocity still comes from the controller (<see cref="RrHand.Velocity"/>); objects without a Rec Room
    /// pose are held where they were taken (VR) or centred upright in the view (screen).
    ///
    /// Screen mode (desktop): the aim ray finds a grabbable within 2.5 m (Rec Room's reach feels like an arm plus a
    /// step), <see cref="RrInput.GrabDown"/> takes it into the screen hand (RrHand.Right, placed in front of the camera
    /// by the Player module) or drops what is held, <see cref="RrInput.UseDown"/> uses it, and
    /// <see cref="RrInput.ThrowDown"/> throws it the way Rec Room's screen mode does: toward the crosshair's target
    /// along the tool's ThrowToolBehaviorConfig curves (<see cref="RrScreenThrow"/>).
    ///
    /// Networked props (a <see cref="Net.RrNetBody"/> on the object) are told about every grab and release, and a
    /// prop another player holds cannot be taken.
    /// </summary>
    public static class RrGrab
    {
        /// <summary>VR reach: grabbables whose colliders come within this distance of the grip frame.</summary>
        public const float VrReach = 0.12f;

        /// <summary>Screen-mode reach along the aim ray.</summary>
        public const float ScreenReach = 2.5f;

        /// <summary>Releases faster than this play the tool's ThrowAudio, slower ones its DropAudio (GUESS).</summary>
        public const float ThrowSoundSpeed = 2f;

        /// <summary>No release is faster than this, whatever the tracking reports (a teleport mid-throw).</summary>
        public const float SanityMaxSpeed = 40f;

        /// <summary>How long a pickup slides into the hand (RigidbodyPickup.interpolateOnPickup); longer from afar on desktop.</summary>
        public const float PickupSlide = 0.1f;
        public const float ScreenPickupSlide = 0.18f;

        public static bool HighlightEnabled = true;

        public static event Action<RrGrabbable, RrHand> OnGrab;
        public static event Action<RrGrabbable, RrHand, Vector3> OnRelease;

        private sealed class Hold
        {
            public RrGrabbable Item;
            public RrHand Hand;
            public Transform Grip;
            public Transform OldParent;
            public Rigidbody Body;
            public bool WasKinematic;
            public RigidbodyInterpolation Interpolation;
            public CollisionDetectionMode Detection;
            public readonly List<Collider> Colliders = new List<Collider>();
            public readonly List<bool> WasTrigger = new List<bool>();
            public readonly List<int> Excluded = new List<int>();
            public Vector3 RestPosition;
            public Quaternion RestRotation;
            public Vector3 FromPosition;
            public Quaternion FromRotation;
            public float Slide;
            public float SlideTime;
            public bool AtMouth;
            public int GrabFrame;
        }

        private static readonly Dictionary<RrGrabbable, Hold> Holds = new Dictionary<RrGrabbable, Hold>();
        private static readonly List<Hold> HoldList = new List<Hold>();
        private static readonly Dictionary<Collider, RrGrabbable> Owners = new Dictionary<Collider, RrGrabbable>();
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly RrGrabbable[] Hover = new RrGrabbable[2];
        private static readonly RrGrabbable[] Lit = new RrGrabbable[2];
        private static readonly List<Collider> ColliderScratch = new List<Collider>();
        private static GameObject _driver;
        private static bool _loggedFirstGrab;
        private static bool _loggedFirstScreen;
        private static bool _loggedFirstVr;
        private static readonly bool[] LoggedPalm = new bool[2];
        private static bool _loggedMode;
        private static bool _loggedNoHands;

        // ------------------------------------------------------------------ public API

        /// <summary>What a hand hovers (null when nothing, or when it holds something).</summary>
        public static RrGrabbable Hovered(RrHandSide side)
        {
            return Hover[side == RrHandSide.Left ? 0 : 1];
        }

        /// <summary>What a hand holds, or null.</summary>
        public static RrGrabbable HeldBy(RrHand hand)
        {
            if (hand == null)
            {
                return null;
            }

            for (int i = 0; i < HoldList.Count; i++)
            {
                if (HoldList[i].Hand == hand && HoldList[i].Item != null)
                {
                    return HoldList[i].Item;
                }
            }

            return null;
        }

        /// <summary>Puts an object straight into a hand (e.g. a game hands out a paintball gun).</summary>
        public static void Give(RrGrabbable item, RrHand hand)
        {
            if (item != null && hand != null)
            {
                Grab(hand, item, false);
            }
        }

        /// <summary>
        /// Takes an object into a hand: at the tool's grip pose when it has one, else where it is (VR) or centred on
        /// the screen hand (desktop). Whatever the hand held is dropped first; an object in the other hand changes hands.
        /// Returns false when it cannot be taken (disabled, or held by another player).
        /// </summary>
        public static bool Grab(RrHand hand, RrGrabbable item, bool fromWhereItIs = true)
        {
            if (hand == null || item == null || !item.isActiveAndEnabled)
            {
                return false;
            }

            if (item.Holder == hand && Holds.ContainsKey(item))
            {
                return true;
            }

            var net = item.NetBody;
            if (net != null && net.HolderId >= 0 && net.HolderId != RrPlayer.LocalId)
            {
                return false;
            }

            if (item.IsHeld)
            {
                // Hand to hand: no sound, no velocity.
                Unhold(item, Vector3.zero, Vector3.zero, false);
            }

            var previous = HeldBy(hand);
            if (previous != null)
            {
                Release(previous, hand.Velocity, Vector3.zero);
            }

            SetHover(hand.Side, null);
            var t = item.transform;
            var grip = hand.Grip != null ? hand.Grip : hand.transform;
            var h = new Hold { Item = item, Hand = hand, Grip = grip, OldParent = t.parent, Body = item.Body, GrabFrame = Time.frameCount };

            // Physics off while held: velocities first (a kinematic body must not be given one), then continuous
            // detection off (kinematic bodies only take speculative), then kinematic.
            if (h.Body != null)
            {
                h.WasKinematic = h.Body.isKinematic;
                h.Interpolation = h.Body.interpolation;
                h.Detection = h.Body.collisionDetectionMode;
                if (!h.Body.isKinematic)
                {
                    h.Body.linearVelocity = Vector3.zero;
                    h.Body.angularVelocity = Vector3.zero;
                }

                h.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                h.Body.interpolation = RigidbodyInterpolation.None;
                h.Body.isKinematic = true;
            }

            ColliderScratch.Clear();
            item.GetComponentsInChildren(true, ColliderScratch);
            foreach (var c in ColliderScratch)
            {
                h.Colliders.Add(c);
                h.WasTrigger.Add(c.isTrigger);
                h.Excluded.Add(c.excludeLayers.value);
                c.excludeLayers = c.excludeLayers.value | (1 << 7);
                if (!item.SolidWhileHeld && CanBeTrigger(c))
                {
                    c.isTrigger = true;
                }
            }

            ColliderScratch.Clear();

            t.SetParent(grip, true);
            h.FromPosition = t.localPosition;
            h.FromRotation = t.localRotation;
            bool screen = IsScreen(hand);
            Pose frame = HoldFrame(hand, item);
            float k = UniformScale(t);
            if (item.HasGrip)
            {
                // Rec Room's pose is the tool's local pose in its hand frame: tool = grip * frame * (gp, gr).
                Vector3 gp;
                Quaternion gr;
                item.GripFor(hand.Side, out gp, out gr);
                h.RestRotation = frame.rotation * gr;
                h.RestPosition = frame.position + frame.rotation * (gp * k);
            }
            else if (screen)
            {
                // Nothing to hold it by: centred on the screen hand, upright to the view (the grip is the view
                // turned back by GripToAim, so GripToAim is the view's own rotation here).
                h.RestRotation = RrHand.GripToAim;
                h.RestPosition = -(h.RestRotation * (CentreLocal(item) * k));
            }
            else if (!fromWhereItIs)
            {
                // Given into a VR hand without a pose: centred in the fist, upright in the hand (+Y up the thumb side).
                h.RestRotation = frame.rotation;
                h.RestPosition = frame.position - frame.rotation * (CentreLocal(item) * k);
            }
            else
            {
                h.RestPosition = t.localPosition;
                h.RestRotation = t.localRotation;
            }

            h.SlideTime = screen ? ScreenPickupSlide : PickupSlide;
            if (item.InterpolateOnPickup && fromWhereItIs)
            {
                h.Slide = 0f;
            }
            else
            {
                h.Slide = 1f;
                t.localPosition = h.RestPosition;
                t.localRotation = h.RestRotation;
            }

            Holds[item] = h;
            HoldList.Add(h);
            hand.Held = item;
            item.NotifyGrabbed(hand);
            PlayClip(item.PickupClip, item.PickupSound, t.position);
            if (RrPlayer.InVr)
            {
                hand.Haptic(0.3f, 0.05f);
            }

            if (net != null)
            {
                Vector3 wp;
                Quaternion wr;
                Net.RrNetBody.WireHeldPose(hand, h.RestPosition, h.RestRotation, out wp, out wr);
                net.LocalGrab(hand.Side, wp, wr);
            }

            if (!_loggedFirstGrab || (screen ? !_loggedFirstScreen : !_loggedFirstVr))
            {
                _loggedFirstGrab = true;
                if (screen)
                {
                    _loggedFirstScreen = true;
                }
                else
                {
                    _loggedFirstVr = true;
                }

                Quaternion world = grip.rotation * h.RestRotation;
                RrLog.Info("Grab", "first " + (screen ? "screen-mode" : "VR") + " pickup: " + item.name + " in the " + hand.Side + " hand, " +
                                   (item.HasGrip ? "Rec Room " + item.GripSource + " pose in " + (screen ? (item.HasScreenHand ? "the tool's screen hand" : "Rec Room's default screen hand") : "the drawn hand's frame (grip * " + Fmt(frame.rotation) + ")") : screen ? "centred upright in the view" : fromWhereItIs ? "held where taken" : "centred in the fist") +
                                   "; its up " + Vector3.Angle(world * Vector3.up, Vector3.up).ToString("0") + " deg from world up, forward " +
                                   Vector3.Angle(world * Vector3.forward, hand.AimRotation * Vector3.forward).ToString("0") + " deg from the hand's aim");
            }

            if (OnGrab != null)
            {
                OnGrab(item, hand);
            }

            return true;
        }

        /// <summary>
        /// The frame Rec Room's hold poses are applied in, in the hand's <see cref="RrHand.Grip"/> local space (the
        /// grip carries no scale, so metres). VR: <see cref="RrHand.RecRoomFrame"/>, the client's default-IK drawn
        /// hand (26.7 deg from the grip's AimRotation, centred on the grip). Screen mode: the tool's own Rec Room
        /// screen hand (<see cref="RrGrabbable.HasScreenHand"/>, else <see cref="RrScreenHold.Default"/>), a view-space
        /// pose carried into the screen grip, which sits at <see cref="RrHand.ScreenOffset"/> in the view turned back
        /// by GripToAim: local = (GripToAim * (hand.position - ScreenOffset), GripToAim * hand.rotation). The left hand
        /// uses the mirror image.
        /// </summary>
        public static Pose HoldFrame(RrHand hand, RrGrabbable item)
        {
            if (hand == null)
            {
                return new Pose(Vector3.zero, RrHand.GripToAim);
            }

            if (!IsScreen(hand))
            {
                return hand.RecRoomFrame;
            }

            Pose view = item != null && item.HasScreenHand ? new Pose(item.ScreenHandPosition, item.ScreenHandRotation) : RrScreenHold.Default;
            view.position = IntoView(view.position);
            Vector3 offset = RrHand.ScreenOffset;
            if (hand.Side == RrHandSide.Left)
            {
                view = RrScreenHold.Mirror(view);
                offset.x = -offset.x;
            }

            return new Pose(RrHand.GripToAim * (view.position - offset), RrHand.GripToAim * view.rotation);
        }

        /// <summary>Largest angles below and beside the view axis a screen hand may sit at: Rec Room's default screen hand (32 deg down, 30 deg right).</summary>
        public const float ScreenHandMaxDown = 32f, ScreenHandMaxSide = 30f;

        /// <summary>
        /// Rec Room's screen camera is wider than ours (60 deg vertical here), so a few screen hands - the paddleball
        /// paddle's idle hand at 45 deg down and 41 deg right - would hold the tool out of sight. The hand keeps its
        /// depth and rotation and is pulled in to at most the default screen hand's angles, which show the tool.
        /// </summary>
        private static Vector3 IntoView(Vector3 p)
        {
            if (p.z <= 0.05f)
            {
                return p;
            }

            float side = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            float down = Mathf.Atan2(-p.y, p.z) * Mathf.Rad2Deg;
            float s = Mathf.Clamp(side, -ScreenHandMaxSide, ScreenHandMaxSide);
            float d = Mathf.Min(down, ScreenHandMaxDown);
            if (Mathf.Approximately(s, side) && Mathf.Approximately(d, down))
            {
                return p;
            }

            return new Vector3(p.z * Mathf.Tan(s * Mathf.Deg2Rad), -p.z * Mathf.Tan(d * Mathf.Deg2Rad), p.z);
        }

        /// <summary>
        /// Where a Rec Room hold pose (the tool's pose in the hand frame, e.g. a flag's LeftHandPickupTransform) puts the
        /// tool in the world for this hand right now - for objects that follow a hand without being grabbed.
        /// </summary>
        public static Pose WorldHoldPose(RrHand hand, Vector3 gp, Quaternion gr, RrGrabbable item = null)
        {
            var grip = hand.Grip != null ? hand.Grip : hand.transform;
            Pose frame = HoldFrame(hand, item);
            return new Pose(grip.TransformPoint(frame.position + frame.rotation * gp), grip.rotation * frame.rotation * gr);
        }

        /// <summary>A hand that holds things the screen-mode way: the virtual screen hand, or any hand outside VR.</summary>
        public static bool IsScreen(RrHand hand)
        {
            return !RrHand.VrMode || (hand != null && hand.IsScreenHand);
        }

        private static float UniformScale(Transform t)
        {
            Vector3 s = t.localScale;
            float k = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return k > 1e-6f ? k : 1f;
        }

        private static string Fmt(Quaternion q)
        {
            return "(" + q.x.ToString("0.0000") + ", " + q.y.ToString("0.0000") + ", " + q.z.ToString("0.0000") + ", " + q.w.ToString("0.0000") + ")";
        }

        /// <summary>Lets go with this velocity (world, m/s) and spin (rad/s). Also ends holds made outside the grab system.</summary>
        public static void Release(RrGrabbable item, Vector3 velocity, Vector3 angularVelocity)
        {
            Unhold(item, velocity, angularVelocity, true);
        }

        /// <summary>Opens the hand: the object leaves with the hand's own motion (VR throw, screen-mode drop).</summary>
        public static void Drop(RrHand hand)
        {
            var item = HeldBy(hand);
            if (item == null)
            {
                return;
            }

            Vector3 w;
            Vector3 v = HandThrowVelocity(hand, item, out w);
            Release(item, v, w);
        }

        /// <summary>Screen-mode throw of what the hand holds, along the player's aim (<see cref="RrInput.AimRay"/>).</summary>
        public static void Throw(RrHand hand)
        {
            ThrowAlong(hand, RrInput.AimRay);
        }

        /// <summary>Screen-mode throw along a given aim: the tool's <see cref="RrScreenThrow"/> curves.</summary>
        public static void ThrowAlong(RrHand hand, Ray aim)
        {
            var item = HeldBy(hand);
            if (item == null)
            {
                return;
            }

            Vector3 w;
            Vector3 v = ScreenThrowVelocity(item, aim, out w);
            Release(item, v, w);
        }

        /// <summary>The velocity a screen-mode throw gives this object (tool multiplier and clamp included).</summary>
        public static Vector3 ScreenThrowVelocity(RrGrabbable item, Ray aim, out Vector3 angularVelocity)
        {
            var config = item.ScreenThrow ?? DefaultScreenThrow;
            Vector3 from = item.Body != null ? item.Body.worldCenterOfMass : item.transform.position;
            Vector3 v = config.Velocity(from, aim, out angularVelocity) * item.ThrowMultiplier;
            return Clamp(item, v);
        }

        /// <summary>The release velocity from the hand's tracked motion (VR throws, drops).</summary>
        public static Vector3 HandThrowVelocity(RrHand hand, RrGrabbable item, out Vector3 angularVelocity)
        {
            angularVelocity = hand.AngularVelocity;
            Vector3 v = hand.Velocity;
            var grip = hand.Grip != null ? hand.Grip : hand.transform;
            Vector3 centre = item.Body != null ? item.Body.worldCenterOfMass : item.transform.position;
            v += Vector3.Cross(hand.AngularVelocity, centre - grip.position);
            return Clamp(item, v * item.ThrowMultiplier);
        }

        private static RrScreenThrow _defaultScreenThrow;

        private static RrScreenThrow DefaultScreenThrow
        {
            get
            {
                if (_defaultScreenThrow == null)
                {
                    _defaultScreenThrow = RrScreenThrow.Generic();
                }

                return _defaultScreenThrow;
            }
        }

        private static Vector3 Clamp(RrGrabbable item, Vector3 v)
        {
            if (item.HasMaxThrowSpeed && item.MaxThrowSpeed > 0f)
            {
                v = Vector3.ClampMagnitude(v, item.MaxThrowSpeed);
            }

            return Vector3.ClampMagnitude(v, SanityMaxSpeed);
        }

        /// <summary>The grabbable a collider belongs to, or null.</summary>
        public static RrGrabbable Owner(Collider c)
        {
            if (c == null)
            {
                return null;
            }

            RrGrabbable g;
            if (Owners.TryGetValue(c, out g) && (g == null || c.transform.IsChildOf(g.transform)))
            {
                if (g != null || ReferenceEquals(g, null))
                {
                    return g;
                }
            }

            g = c.GetComponentInParent<RrGrabbable>();
            Owners[c] = g;
            return g;
        }

        public static void Init()
        {
            if (_driver != null)
            {
                return;
            }

            _driver = new GameObject("Rr Grab");
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _driver.transform.SetParent(root, false);
            _driver.AddComponent<RrGrabDriver>();
            RrLog.Info("Grab", "ready: VR reach " + VrReach + " m, screen reach " + ScreenReach + " m, held props kinematic + triggers, layer 7 excluded");
        }

        public static void Shutdown()
        {
            for (int i = HoldList.Count - 1; i >= 0; i--)
            {
                var h = HoldList[i];
                if (h.Item != null)
                {
                    Unhold(h.Item, Vector3.zero, Vector3.zero, false);
                }
            }

            Holds.Clear();
            HoldList.Clear();
            Owners.Clear();
            Hover[0] = Hover[1] = null;
            Lit[0] = Lit[1] = null;
            RrHighlight.Shutdown();
            RrPressable.Shutdown();
            RrRefs.Shutdown();
            RrMouth.Shutdown();
            RrTools.Shutdown();
            RrGrabbable.ClearRegistry();
            OnGrab = null;
            OnRelease = null;
            _defaultScreenThrow = null;
            _loggedFirstGrab = false;
            _loggedFirstScreen = false;
            _loggedFirstVr = false;
            LoggedPalm[0] = LoggedPalm[1] = false;
            _loggedMode = false;
            _loggedNoHands = false;
            if (_driver != null)
            {
                UnityEngine.Object.Destroy(_driver);
            }

            _driver = null;
        }

        // ------------------------------------------------------------------ per frame

        internal static void Tick()
        {
            CleanHolds();
            bool vr = RrHand.VrMode;
            if (!_loggedMode)
            {
                _loggedMode = true;
                RrLog.Info("Grab", vr ? "VR: grip grabs within " + VrReach + " m, trigger uses, opening the grip throws" : "screen mode: aim within " + ScreenReach + " m, grab/use/throw from RrInput");
            }

            if (vr)
            {
                VrHand(RrHand.Left);
                VrHand(RrHand.Right);
            }
            else
            {
                SetHover(RrHandSide.Left, null);
                ScreenHand(RrHand.Right != null ? RrHand.Right : RrHand.Left);
            }

            if (RrHand.Left == null && RrHand.Right == null && !_loggedNoHands)
            {
                _loggedNoHands = true;
                RrLog.Info("Grab", "no hands yet (RrHand.Left/Right are null): only RrGrab.Give works until the Player module creates them");
            }

            RrPressable.Tick(vr);
            UpdateHighlights();
        }

        internal static void LateTick()
        {
            float dt = Time.deltaTime;
            bool vr = RrHand.VrMode;
            for (int i = 0; i < HoldList.Count; i++)
            {
                var h = HoldList[i];
                if (h.Item == null)
                {
                    continue;
                }

                var t = h.Item.transform;
                if (t.parent != h.Grip)
                {
                    continue;
                }

                if (h.Slide < 1f)
                {
                    h.Slide = Mathf.Min(1f, h.Slide + dt / Mathf.Max(0.01f, h.SlideTime));
                    float s = 1f - (1f - h.Slide) * (1f - h.Slide);
                    t.localPosition = Vector3.LerpUnclamped(h.FromPosition, h.RestPosition, s);
                    t.localRotation = Quaternion.SlerpUnclamped(h.FromRotation, h.RestRotation, s);
                }
                else if (vr && Time.frameCount - h.GrabFrame > 90)
                {
                    LogDrawnPalm(h.Hand);
                }

                if (!vr && h.Item.ToMouth > 0.001f)
                {
                    PoseAtMouth(h);
                    h.AtMouth = true;
                }
                else if (h.AtMouth)
                {
                    h.AtMouth = false;
                    if (h.Slide >= 1f)
                    {
                        t.localPosition = h.RestPosition;
                        t.localRotation = h.RestRotation;
                    }
                }
            }
        }

        /// <summary>
        /// Once per hand and session, a second into a VR hold: where the avatar's DRAWN hand (RrAvatarHand, read at
        /// this LateUpdate after the client's IK) is against the constant frame held objects use. Decision
        /// (DN/phase1/grab.md): the client's default IK draws the palm on the grip centre (0.1 mm), so objects stay on
        /// the grip with the default frame's rotation; this line tells the next headset session how far a real avatar's
        /// hand (its own rest pose, the client's 5 deg / 5 cm transform stabiliser) sits from it.
        /// </summary>
        private static void LogDrawnPalm(RrHand hand)
        {
            if (hand == null)
            {
                return;
            }

            int i = hand.Side == RrHandSide.Left ? 0 : 1;
            if (LoggedPalm[i])
            {
                return;
            }

            LoggedPalm[i] = true;
            Pose live;
            var source = RrAvatarHand.Source(hand.Side);
            if (!RrAvatarHand.TryGetGripToPalm(hand.Side, out live))
            {
                RrLog.Info("Grab", hand.Side + " hand: no drawn hand to compare with (" + source + ")");
                return;
            }

            Pose frame = hand.RecRoomFrame;
            Quaternion liveFrame = live.rotation * RrHand.GripToAim;
            RrLog.Info("Grab", hand.Side + " hand: the drawn palm (" + source + ") sits " + ((live.position - frame.position).magnitude * 100f).ToString("0.0") + " cm and " +
                               Quaternion.Angle(liveFrame, frame.rotation).ToString("0.0") + " deg from the frame held objects use (fingers " +
                               Vector3.Angle(liveFrame * Vector3.forward, frame.rotation * Vector3.forward).ToString("0.0") + " deg apart)");
        }

        /// <summary>Screen mode: brings the object's mouth point to the player's mouth, tilted toward the face.</summary>
        private static void PoseAtMouth(Hold h)
        {
            var item = h.Item;
            var t = item.transform;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(item.ToMouth));
            Quaternion restWorldRot = h.Grip.rotation * h.RestRotation;
            Vector3 restWorldPos = h.Grip.TransformPoint(h.RestPosition);
            var cam = RrPlayer.Camera;
            Vector3 right = cam != null ? cam.transform.right : h.Grip.right;
            Vector3 forward = cam != null ? cam.transform.forward : h.Grip.forward;
            Quaternion targetRot = Quaternion.AngleAxis(-item.MouthTilt, right) * restWorldRot;
            Vector3 mouthLocal = item.MouthPoint != null ? t.InverseTransformPoint(item.MouthPoint.position) : Vector3.zero;
            Vector3 scaled = Vector3.Scale(t.lossyScale, mouthLocal);
            Vector3 targetPos = RrMouth.Position + forward * 0.03f - targetRot * scaled;
            Quaternion worldRot = Quaternion.Slerp(restWorldRot, targetRot, k);
            Vector3 worldPos = Vector3.Lerp(restWorldPos, targetPos, k);
            t.localRotation = Quaternion.Inverse(h.Grip.rotation) * worldRot;
            t.localPosition = h.Grip.InverseTransformPoint(worldPos);
        }

        private static void VrHand(RrHand hand)
        {
            if (hand == null || !hand.isActiveAndEnabled)
            {
                return;
            }

            int side = hand.Side == RrHandSide.Left ? 0 : 1;
            var held = HeldBy(hand);
            if (held != null)
            {
                SetHover(hand.Side, null);
                if (hand.TriggerDown)
                {
                    held.NotifyUseDown(hand);
                }

                if (hand.TriggerUp)
                {
                    held.NotifyUseUp(hand);
                }

                bool release = held.LockOnPickup
                    ? hand.GripDown && Holds[held].GrabFrame != Time.frameCount
                    : hand.GripUp || (!hand.GripHeld && Holds[held].GrabFrame != Time.frameCount && hand.GripValue < 0.2f);
                if (release)
                {
                    Drop(hand);
                }

                return;
            }

            if (RrInput.Blocked)
            {
                SetHover(hand.Side, null);
                return;
            }

            var grip = hand.Grip != null ? hand.Grip : hand.transform;
            var near = Nearest(grip.position, hand);
            SetHover(hand.Side, near);
            if (near != null && hand.GripDown)
            {
                Grab(hand, near, true);
                return;
            }

            if (hand.TriggerDown)
            {
                RrPressable.PressAlong(hand.AimRay, 5f, "trigger ray");
            }

            Hover[side] = Hover[side];
        }

        private static void ScreenHand(RrHand hand)
        {
            if (hand == null)
            {
                return;
            }

            var held = HeldBy(hand);
            if (held != null)
            {
                SetHover(hand.Side, null);
                if (RrInput.Blocked)
                {
                    return;
                }

                if (RrInput.UseDown)
                {
                    held.NotifyUseDown(hand);
                }

                if (RrInput.UseUp)
                {
                    held.NotifyUseUp(hand);
                }

                if (RrInput.ThrowDown)
                {
                    Throw(hand);
                }
                else if (RrInput.GrabDown && Holds[held].GrabFrame != Time.frameCount)
                {
                    Drop(hand);
                }

                return;
            }

            if (RrInput.Blocked)
            {
                SetHover(hand.Side, null);
                return;
            }

            var aim = RrInput.AimRay;
            var target = Aimed(aim);
            SetHover(hand.Side, target);
            if (target != null && RrInput.GrabDown)
            {
                Grab(hand, target, true);
                return;
            }

            if (RrInput.UseDown)
            {
                RrPressable.PressAlong(aim, RrPressable.ScreenReach, "use key");
            }
        }

        /// <summary>The nearest grabbable to a point within <see cref="VrReach"/> (VR hover).</summary>
        private static RrGrabbable Nearest(Vector3 p, RrHand hand)
        {
            int n = Physics.OverlapSphereNonAlloc(p, VrReach, Overlaps, RrGrabbable.LayerMask, QueryTriggerInteraction.Collide);
            RrGrabbable best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = Overlaps[i];
                var g = Owner(c);
                if (g == null || !g.CanGrab || !g.isActiveAndEnabled || g.Holder == hand)
                {
                    continue;
                }

                if (g.IsHeld && !Holds.ContainsKey(g))
                {
                    continue;
                }

                float d = Distance(c, p);
                if (d < bestD)
                {
                    bestD = d;
                    best = g;
                }
            }

            return best;
        }

        /// <summary>The grabbable under the screen-mode aim within <see cref="ScreenReach"/>, unless a wall is in front.</summary>
        private static RrGrabbable Aimed(Ray aim)
        {
            int n = Physics.SphereCastNonAlloc(aim, 0.03f, Hits, ScreenReach, RrGrabbable.LayerMask, QueryTriggerInteraction.Collide);
            RrGrabbable best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = Hits[i];
                var g = Owner(hit.collider);
                if (g == null || !g.CanGrab || !g.isActiveAndEnabled || g.IsHeld)
                {
                    continue;
                }

                float d = hit.distance <= 0f ? Vector3.Distance(aim.origin, hit.collider.bounds.center) : hit.distance;
                if (d < bestD)
                {
                    bestD = d;
                    best = g;
                }
            }

            if (best == null)
            {
                return null;
            }

            RaycastHit wall;
            int walls = (1 << RrLayers.Default) | (1 << RrLayers.Environment);
            if (Physics.Raycast(aim, out wall, bestD, walls, QueryTriggerInteraction.Ignore) && wall.distance < bestD - 0.05f)
            {
                return null;
            }

            return best;
        }

        private static float Distance(Collider c, Vector3 p)
        {
            var mc = c as MeshCollider;
            if ((mc != null && !mc.convex) || c is TerrainCollider || c is WheelCollider)
            {
                return (c.bounds.ClosestPoint(p) - p).sqrMagnitude;
            }

            return (c.ClosestPoint(p) - p).sqrMagnitude;
        }

        private static void SetHover(RrHandSide side, RrGrabbable item)
        {
            int i = side == RrHandSide.Left ? 0 : 1;
            if (Hover[i] == item)
            {
                return;
            }

            Hover[i] = item;
            if (item != null && RrPlayer.InVr)
            {
                var hand = RrHand.Get(side);
                if (hand != null)
                {
                    hand.Haptic(0.1f, 0.02f);
                }
            }
        }

        private static void UpdateHighlights()
        {
            for (int i = 0; i < 2; i++)
            {
                var want = HighlightEnabled && Hover[i] != null && !Hover[i].IsHeld ? Hover[i] : null;
                if (Lit[i] == want)
                {
                    continue;
                }

                var old = Lit[i];
                Lit[i] = want;
                if (old != null && old != Lit[1 - i])
                {
                    RrHighlight.Set(old, false);
                    old.IsHovered = false;
                }

                if (want != null)
                {
                    RrHighlight.Set(want, true);
                    want.IsHovered = true;
                }
            }
        }

        private static void CleanHolds()
        {
            for (int i = HoldList.Count - 1; i >= 0; i--)
            {
                var h = HoldList[i];
                if (h.Item == null || h.Hand == null)
                {
                    if (h.Hand != null && ReferenceEquals(h.Hand.Held, h.Item))
                    {
                        h.Hand.Held = null;
                    }

                    HoldList.RemoveAt(i);
                    RemoveKey(h);
                    continue;
                }

                // Someone moved the object out of the hand (a game reset it): the hold is over.
                if (h.Item.transform.parent != h.Grip)
                {
                    Unhold(h.Item, Vector3.zero, Vector3.zero, false);
                }
            }
        }

        private static void RemoveKey(Hold h)
        {
            RrGrabbable stale = null;
            foreach (var kv in Holds)
            {
                if (kv.Value == h)
                {
                    stale = kv.Key;
                    break;
                }
            }

            if (!ReferenceEquals(stale, null))
            {
                Holds.Remove(stale);
            }
        }

        private static void Unhold(RrGrabbable item, Vector3 velocity, Vector3 angularVelocity, bool sounds)
        {
            if (ReferenceEquals(item, null))
            {
                return;
            }

            Hold h;
            if (!Holds.TryGetValue(item, out h))
            {
                // Held without the grab system (a game called NotifyGrabbed): just tell it.
                if (item != null && item.Holder != null)
                {
                    var holder = item.Holder;
                    if (ReferenceEquals(holder.Held, item))
                    {
                        holder.Held = null;
                    }

                    item.NotifyReleased(holder, velocity);
                }

                return;
            }

            Holds.Remove(item);
            HoldList.Remove(h);
            if (item == null)
            {
                return;
            }

            var t = item.transform;
            if (t.parent == h.Grip)
            {
                var parent = h.OldParent;
                if (parent == null && RrMain.Instance != null)
                {
                    parent = RrMain.Instance.WorldRoot;
                }

                t.SetParent(parent, true);
            }

            for (int i = 0; i < h.Colliders.Count; i++)
            {
                var c = h.Colliders[i];
                if (c == null)
                {
                    continue;
                }

                c.isTrigger = h.WasTrigger[i];
                c.excludeLayers = h.Excluded[i];
            }

            if (h.Body != null)
            {
                h.Body.isKinematic = h.WasKinematic;
                h.Body.interpolation = h.Interpolation;
                if (!h.Body.isKinematic)
                {
                    h.Body.collisionDetectionMode = h.Detection;
                    h.Body.linearVelocity = velocity;
                    h.Body.angularVelocity = angularVelocity;
                    h.Body.WakeUp();
                }
            }

            if (h.Hand != null && ReferenceEquals(h.Hand.Held, item))
            {
                h.Hand.Held = null;
            }

            item.ToMouth = 0f;
            item.NotifyReleased(h.Hand, velocity);
            if (sounds)
            {
                if (velocity.magnitude >= ThrowSoundSpeed)
                {
                    PlayClip(item.ThrowClip, item.ThrowSound, t.position);
                }
                else
                {
                    PlayClip(item.DropClip, null, t.position);
                }
            }

            var net = item.NetBody;
            if (net != null)
            {
                net.LocalRelease();
            }

            if (OnRelease != null)
            {
                OnRelease(item, h.Hand, velocity);
            }
        }

        private static bool CanBeTrigger(Collider c)
        {
            var mc = c as MeshCollider;
            return mc == null || mc.convex;
        }

        /// <summary>The centre of an object's renderers in its own local frame (screen-mode hold point).</summary>
        private static Vector3 CentreLocal(RrGrabbable item)
        {
            var inst = item.GetComponent<RrInstance>();
            Bounds b;
            if (inst != null)
            {
                b = inst.WorldBounds();
            }
            else
            {
                var r = item.GetComponentInChildren<Renderer>();
                if (r == null)
                {
                    return Vector3.zero;
                }

                b = r.bounds;
            }

            if (b.size == Vector3.zero)
            {
                return Vector3.zero;
            }

            Vector3 local = item.transform.InverseTransformPoint(b.center);
            Vector3 s = item.transform.localScale;
            return new Vector3(s.x != 0f ? local.x : 0f, s.y != 0f ? local.y : 0f, s.z != 0f ? local.z : 0f);
        }

        /// <summary>Plays a Rec Room clip struct when it has a clip, else a clip by name.</summary>
        internal static void PlayClip(RrClipInfo info, string fallbackName, Vector3 position)
        {
            if (info.HasClip)
            {
                RrSound.Play(info, position);
            }
            else if (!string.IsNullOrEmpty(fallbackName))
            {
                RrSound.Play(fallbackName, position);
            }
        }
    }

    /// <summary>Per-frame hook for <see cref="RrGrab"/>: late, so the hands (RrHand) have been read and posed first.</summary>
    [DefaultExecutionOrder(10050)]
    internal sealed class RrGrabDriver : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                RrGrab.Tick();
            }
            catch (Exception e)
            {
                RrLog.Error("Grab", "tick failed", e);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            RrGrab.LateTick();
        }
    }
}
