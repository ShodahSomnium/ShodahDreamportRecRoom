using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// A prop shared with the other members of the instance through a "Rr Net Prop p" (somnium_networking.md §5.5;
    /// ints in <see cref="RrPropWire"/>). Put it on the prop's root (with its Rigidbody) and set
    /// <see cref="LocalPropId"/> (the prop's number within its activity) before it is enabled; the instance authority
    /// leases a prop object for it and everyone finds it by that lease.
    ///
    /// Owner-simulated: whoever holds the prop object's state authority simulates the real Rigidbody and writes -
    /// the holder (Held: nothing streams, others draw it at the holder's Bridge hand - or a desktop holder's head - with
    /// the wire pose from the holder's presence slot, <see cref="WireHeldPose"/>), the thrower (Flying: up to 30 samples/s plus one at every collision, until it rests), or the
    /// instance authority for resting and orphaned props. Everyone else draws a puppet: gravity off and all
    /// constraints frozen (not isKinematic, which the grab system records and restores itself), placed at the
    /// holder's hand or on the interpolated <see cref="RrTimeline"/>. Grabs are optimistic (<see cref="RrGrabClaim"/>):
    /// the loser of a simultaneous grab lets go when it reads the other holder.
    ///
    /// Offline, in unnetworked rooms, or before the lease exists it is plain local physics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RrNetBody : MonoBehaviour
    {
        public const double WriteEvery = 1.0 / 30.0;
        public const float RestSpeed = 0.05f;
        public const double RestAfter = 0.5;
        public const double TouchCooldown = 0.25;
        public const double TouchHold = 1.0;

        private static readonly List<RrNetBody> All = new List<RrNetBody>();

        /// <summary>The prop's number within its activity (e.g. dodgeball balls 0-5, 0..255); set before Start.</summary>
        public int LocalPropId;

        private readonly RrGrabClaim _grab = new RrGrabClaim();
        private readonly RrTimeline _timeline = new RrTimeline { LeadIn = true };
        private readonly int[] _rec = new int[RrPropWire.Ints];
        private Rigidbody _rb;
        private bool _puppet, _savedGravity, _needPose, _forceWrite, _pendingReset;
        private RigidbodyConstraints _savedConstraints;
        private RigidbodyInterpolation _savedInterpolation;
        private RrHandSide _hand = RrHandSide.Right;
        private Vector3 _gripPosition, _resetPosition;
        private Quaternion _gripRotation = Quaternion.identity, _resetRotation = Quaternion.identity;
        private int _lastSeq = int.MinValue, _lastHolder = -1, _localThrowId;
        private double _writeAt, _stillSince = -1.0, _touchUntil = -1.0, _touchCooldownUntil;

        /// <summary>The prop object p leased for this prop in the current instance (-1: local physics).</summary>
        public int PropObject
        {
            get { return RrNet.Online && LocalPropId >= 0 && LocalPropId < RrNetCore.MaxLocalIds ? RrNet.Core.PropOf[LocalPropId] : -1; }
        }

        public bool Networked
        {
            get { return PropObject >= 0; }
        }

        private bool HaveAuthority(int p)
        {
            return p >= 0 && RrNet.Core.PropAuth[p];
        }

        /// <summary>This client simulates the prop (holder, thrower, or authority when nobody does; always offline).</summary>
        public bool LocallyControlled
        {
            get
            {
                int p = PropObject;
                return p < 0 || HaveAuthority(p) || _grab.Current != RrGrabClaim.Stage.Idle || RrNet.Now < _touchUntil;
            }
        }

        /// <summary>PlayerId of the holder, -1 when free.</summary>
        public int HolderId
        {
            get
            {
                int p = PropObject;

                if (p < 0)
                {
                    return -1;
                }

                if (_grab.Current != RrGrabClaim.Stage.Idle)
                {
                    return RrNet.LocalId;
                }

                int[] net = RrNet.Core.Prop[p];
                return RrPropWire.State(net) == RrPropWire.StateHeld ? RrPropWire.Writer(net) : -1;
            }
        }

        public RrHandSide HolderHand
        {
            get
            {
                int p = PropObject;

                if (p < 0 || _grab.Current != RrGrabClaim.Stage.Idle)
                {
                    return _hand;
                }

                return RrPropWire.LeftHand(RrNet.Core.Prop[p]) ? RrHandSide.Left : RrHandSide.Right;
            }
        }

        /// <summary>Increments on every throw (catch/hit arbitration: dodgeball).</summary>
        public int ThrowId
        {
            get
            {
                int p = PropObject;
                return p >= 0 ? RrPropWire.ThrowId(RrNet.Core.Prop[p]) : _localThrowId;
            }
        }

        /// <summary>The kind-specific int 11 (dodgeball live/team, disc flags, bottle fill...).</summary>
        public int KindInt
        {
            get
            {
                int p = PropObject;
                return p >= 0 ? RrNet.Core.Prop[p][RrPropWire.AtKind] : _rec[RrPropWire.AtKind];
            }
        }

        /// <summary>Another player took it in hand (their PlayerId).</summary>
        public event Action<int> OnRemoteGrab;

        /// <summary>Another player let it go (their PlayerId).</summary>
        public event Action<int> OnRemoteRelease;

        // ------------------------------------------------------------------ calls from the grab system and games

        /// <summary>
        /// The held pose as the other clients can rebuild it, from the pose under our <see cref="RrHand.Grip"/> (the
        /// grab system's rest pose). They draw the prop from what THEY have of us: a headset player's Bridge hand
        /// (the networked LeftHandAnchor / RightHandAnchor), or for a desktop player, whose Bridge hands are null, the
        /// head and the body's yaw (somnium_player.md 8.2). So:
        /// - VR on the client's anchor: the pose relative to that anchor, in metres and without its scale (our grip is
        ///   the anchor times a measured calibration offset and a 1/lossy-scale compensation, neither of which the
        ///   receiver knows; the user's log measured the offset as zero and the anchor's scale as 0.889);
        /// - the screen hand: the pose relative to the head in the body's yaw frame, as it looks with the view level
        ///   (screen grip = ScreenOffset in the view turned back by GripToAim; our eye is 0.12 m ahead of the head and
        ///   0.02 m up, RrScreenCamera) - remote viewers see it held in front of the player, lower right;
        /// - VR without a Bridge hand (the editor's tracked-device fallback): grip-relative, as before.
        /// <see cref="PlaceFromWire"/> is the receiving half; the harness checks the pair (grab_pose.json).
        /// </summary>
        public static void WireHeldPose(RrHand hand, Vector3 gripLocalPosition, Quaternion gripLocalRotation, out Vector3 position, out Quaternion rotation)
        {
            position = gripLocalPosition;
            rotation = gripLocalRotation;
            if (hand == null || hand.Grip == null)
            {
                return;
            }

            if (hand.IsScreenHand)
            {
                Vector3 offset = RrHand.ScreenOffset;
                if (hand.Side == RrHandSide.Left)
                {
                    offset.x = -offset.x;
                }

                Quaternion aimToGrip = Quaternion.Inverse(RrHand.GripToAim);
                position = ScreenEye + offset + aimToGrip * gripLocalPosition;
                rotation = aimToGrip * gripLocalRotation;
                return;
            }

            Transform anchor = hand.Anchor;
            if (anchor == null || hand.Grip.parent != anchor)
            {
                return;
            }

            Transform grip = hand.Grip;
            Quaternion toAnchor = Quaternion.Inverse(anchor.rotation);
            position = toAnchor * (grip.TransformPoint(gripLocalPosition) - anchor.position);
            rotation = toAnchor * (grip.rotation * gripLocalRotation);
        }

        /// <summary>Our screen-mode eye relative to the head, in the body's yaw frame (RrScreenCamera: 0.12 m ahead, 0.02 m up).</summary>
        public static readonly Vector3 ScreenEye = new Vector3(0f, 0.02f, 0.12f);

        /// <summary>
        /// Where a remote holder's prop goes, from the wire pose (<see cref="WireHeldPose"/>): on their Bridge hand
        /// (rotation only - the remote anchor's scale is not applied to a pose sent in metres), else, for a desktop
        /// player, at their head in their body's yaw. False when neither is known (the prop keeps its timeline).
        /// </summary>
        public static bool PlaceFromWire(Transform hand, Transform head, Transform root, Vector3 position, Quaternion rotation, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            if (hand != null)
            {
                worldPosition = hand.position + hand.rotation * position;
                worldRotation = hand.rotation * rotation;
                return true;
            }

            if (head != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane((root != null ? root : head).forward, Vector3.up);
                Quaternion yaw = fwd.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fwd, Vector3.up) : Quaternion.identity;
                worldPosition = head.position + yaw * position;
                worldRotation = yaw * rotation;
                return true;
            }

            worldPosition = Vector3.zero;
            worldRotation = Quaternion.identity;
            return false;
        }

        /// <summary>The local player grabbed it: takes the authority, writes Held and the wire pose (<see cref="WireHeldPose"/>).</summary>
        public void LocalGrab(RrHandSide hand, Vector3 gripLocalPosition, Quaternion gripLocalRotation)
        {
            _hand = hand;
            _gripPosition = gripLocalPosition;
            _gripRotation = gripLocalRotation;
            SetPuppet(false, null);
            int p = PropObject;

            if (p < 0)
            {
                return;
            }

            _grab.Begin(RrNet.Now);
            RrNet.SetHeldProp(hand == RrHandSide.Left, p, gripLocalPosition, gripLocalRotation);
        }

        /// <summary>The local player let go (with the release velocity already applied to the rigidbody).</summary>
        public void LocalRelease()
        {
            RrNet.SetHeldProp(_hand == RrHandSide.Left, -1, Vector3.zero, Quaternion.identity);
            bool held = _grab.Current == RrGrabClaim.Stage.Holding;
            _grab.End();
            _localThrowId = (_localThrowId + 1) & 0xFFFF;
            int p = PropObject;

            if (p < 0 || !HaveAuthority(p))
            {
                return;
            }

            int[] net = RrNet.Core.Prop[p];
            WriteMotion(p, RrPropWire.StateFlying, RrPropWire.Epoch(net) + 1, held ? RrPropWire.ThrowId(net) + 1 : RrPropWire.ThrowId(net));
            _stillSince = -1.0;
        }

        /// <summary>A local touch (paddle, kick) that should take over simulation (last toucher owns, 0.25 s cooldown).</summary>
        public void LocalTouch()
        {
            int p = PropObject;
            double now = RrNet.Now;

            if (p < 0 || now < _touchCooldownUntil)
            {
                return;
            }

            _touchCooldownUntil = now + TouchCooldown;

            if (!HaveAuthority(p))
            {
                SetPuppet(false, RrNet.Core.Prop[p]);
                RrNet.RequestNow(RrNetCore.KindProp, p);
                _touchUntil = now + TouchHold;
            }

            _forceWrite = true;
        }

        /// <summary>Authority: puts the prop somewhere at rest (reset, respawn).</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);

            if (Body != null && !Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }

            int p = PropObject;

            if (p < 0)
            {
                return;
            }

            if (HaveAuthority(p))
            {
                WriteMotion(p, RrPropWire.StateResting, RrPropWire.Epoch(RrNet.Core.Prop[p]) + 1, RrPropWire.ThrowId(RrNet.Core.Prop[p]));
            }
            else if (RrNet.IsInstanceAuthority)
            {
                _pendingReset = true;
                _resetPosition = position;
                _resetRotation = rotation;
                RrNet.RequestNow(RrNetCore.KindProp, p);
            }
        }

        /// <summary>Controller: sets the kind-specific int 11 (written with the next sample, or now when resting).</summary>
        public void SetKindInt(int value)
        {
            _rec[RrPropWire.AtKind] = value;
            int p = PropObject;

            if (p >= 0 && HaveAuthority(p) && RrNet.Core.Prop[p][RrPropWire.AtKind] != value)
            {
                Array.Copy(RrNet.Core.Prop[p], _rec, _rec.Length);
                _rec[RrPropWire.AtKind] = value;
                RrNet.PushNow(RrNetCore.KindProp, p, _rec);
            }
        }

        // ------------------------------------------------------------------ Unity

        private Rigidbody Body
        {
            get
            {
                if (_rb == null)
                {
                    _rb = GetComponent<Rigidbody>();
                }

                return _rb;
            }
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
            {
                All.Add(this);
            }
        }

        private void OnDisable()
        {
            All.Remove(this);
            SetPuppet(false, null);
        }

        private void Update()
        {
            int p = PropObject;

            if (p < 0)
            {
                SetPuppet(false, null);
                _grab.End();
                return;
            }

            double now = RrNet.Now;
            int[] net = RrNet.Core.Prop[p];
            bool auth = HaveAuthority(p);
            int local = RrNet.LocalId;
            _grab.Step(now, auth, net, local, RrNet.Present);

            if (_grab.Ask && !auth)
            {
                RrNet.RequestNow(RrNetCore.KindProp, p);
            }

            if (_grab.WriteHeld)
            {
                WriteHeld(p, net);
            }

            if (_grab.Drop)
            {
                RrNet.Info("prop " + LocalPropId + " (object " + p + "): grab lost - " + _grab.Why);
                ForceLocalDrop();
            }

            if (auth)
            {
                _touchUntil = -1.0;
            }

            if (LocallyControlled)
            {
                SetPuppet(false, net);

                if (auth)
                {
                    ControllerUpdate(p, net, local);
                }
            }
            else
            {
                SetPuppet(true, net);
                PuppetUpdate(net, now, local);
            }

            TrackHolder(net, local);
        }

        private void FixedUpdate()
        {
            int p = PropObject;

            if (p < 0 || !HaveAuthority(p) || _grab.Current != RrGrabClaim.Stage.Idle)
            {
                return;
            }

            Rigidbody rb = Body;
            int[] net = RrNet.Core.Prop[p];
            int state = RrPropWire.State(net);
            double now = RrNet.Now;
            bool moving = rb != null && !rb.isKinematic &&
                          (rb.linearVelocity.sqrMagnitude > RestSpeed * RestSpeed || rb.angularVelocity.sqrMagnitude > 0.01f);

            if (moving)
            {
                _stillSince = -1.0;

                if (_forceWrite || state != RrPropWire.StateFlying || now >= _writeAt)
                {
                    WriteMotion(p, RrPropWire.StateFlying, RrPropWire.Epoch(net), RrPropWire.ThrowId(net));
                }

                return;
            }

            if (state == RrPropWire.StateFlying)
            {
                if (_stillSince < 0.0)
                {
                    _stillSince = now;
                }
                else if (now - _stillSince >= RestAfter)
                {
                    WriteMotion(p, RrPropWire.StateResting, RrPropWire.Epoch(net), RrPropWire.ThrowId(net));
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // A sample exactly on the bounce (§5.5.3 step 6).
            _forceWrite = true;
        }

        // ------------------------------------------------------------------ controller

        private void ControllerUpdate(int p, int[] net, int local)
        {
            if (_grab.Current != RrGrabClaim.Stage.Idle)
            {
                return;
            }

            if (_pendingReset)
            {
                _pendingReset = false;
                transform.SetPositionAndRotation(_resetPosition, _resetRotation);
                WriteMotion(p, RrPropWire.StateResting, RrPropWire.Epoch(net) + 1, RrPropWire.ThrowId(net));
                return;
            }

            int writer = RrPropWire.Writer(net);

            if (writer != local)
            {
                // Adopted (an orphan, a toucher's take-over): continue from here, receivers snap to the new epoch.
                bool moving = Body != null && !Body.isKinematic && Body.linearVelocity.sqrMagnitude > RestSpeed * RestSpeed;
                WriteMotion(p, moving ? RrPropWire.StateFlying : RrPropWire.StateResting, RrPropWire.Epoch(net) + 1, RrPropWire.ThrowId(net));
                return;
            }

            if (_needPose)
            {
                _needPose = false;
                WriteMotion(p, RrPropWire.StateResting, RrPropWire.Epoch(net), RrPropWire.ThrowId(net));
            }
        }

        private void WriteHeld(int p, int[] net)
        {
            Array.Copy(net, _rec, _rec.Length);
            _rec[RrPropWire.AtState] = RrPropWire.PackState(RrPropWire.StateHeld, _hand == RrHandSide.Left, RrPropWire.Epoch(net), RrNet.LocalId);
            _rec[RrPropWire.AtSeq] = net[RrPropWire.AtSeq] + 1;
            _rec[RrPropWire.AtStamp] = Stamp();
            RrPropWire.PutMotion(_rec, transform.position, transform.rotation, Vector3.zero, Vector3.zero);
            RrNet.PushNow(RrNetCore.KindProp, p, _rec);
            RrNet.SetHeldProp(_hand == RrHandSide.Left, p, _gripPosition, _gripRotation);
        }

        private void WriteMotion(int p, int state, int epoch, int throwId)
        {
            int[] net = RrNet.Core.Prop[p];
            Rigidbody rb = Body;
            Array.Copy(net, _rec, _rec.Length);
            _rec[RrPropWire.AtState] = RrPropWire.PackState(state, _hand == RrHandSide.Left, epoch, RrNet.LocalId);
            _rec[RrPropWire.AtSeq] = net[RrPropWire.AtSeq] + 1;
            _rec[RrPropWire.AtStamp] = Stamp();
            bool dynamic = rb != null && !rb.isKinematic;
            RrPropWire.PutMotion(_rec, transform.position, transform.rotation,
                dynamic && state == RrPropWire.StateFlying ? rb.linearVelocity : Vector3.zero,
                dynamic && state == RrPropWire.StateFlying ? rb.angularVelocity : Vector3.zero);
            RrPropWire.SetThrowId(_rec, throwId);
            RrNet.PushNow(RrNetCore.KindProp, p, _rec);
            _writeAt = RrNet.Now + WriteEvery;
            _forceWrite = false;
        }

        private static int Stamp()
        {
            return RrNetCore.Stamp(Time.inFixedTimeStep ? Time.fixedTimeAsDouble : Time.timeAsDouble);
        }

        private void ForceLocalDrop()
        {
            try
            {
                var grabbable = GetComponent<Interact.RrGrabbable>();

                if (grabbable != null && grabbable.IsHeld)
                {
                    Interact.RrGrab.Release(grabbable, Vector3.zero, Vector3.zero);
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("Net", "dropping prop " + LocalPropId + " locally failed: " + e.Message);
            }

            RrNet.SetHeldProp(_hand == RrHandSide.Left, -1, Vector3.zero, Quaternion.identity);
        }

        // ------------------------------------------------------------------ puppet

        private void PuppetUpdate(int[] net, double now, int local)
        {
            int state = RrPropWire.State(net);
            int writer = RrPropWire.Writer(net);

            if (state == RrPropWire.StateHeld && writer >= 0 && writer != local)
            {
                bool left = RrPropWire.LeftHand(net);
                RrRemotePlayers.Remote remote = null;

                try
                {
                    remote = RrRemotePlayers.Get(writer);
                }
                catch (Exception)
                {
                    // the player module is not up
                }

                Transform hand = remote != null ? (left ? remote.LeftHand : remote.RightHand) : null;
                Transform head = remote != null && !remote.Vr ? remote.Head : null;
                int[] slot = RrNet.PresenceRecord(writer);
                Vector3 wp;
                Quaternion wr;

                if (slot != null && PlaceFromWire(hand, head, remote != null ? remote.Root : null,
                        RrPresenceWire.GripPosition(slot, left), RrPresenceWire.GripRotation(slot, left), out wp, out wr))
                {
                    transform.SetPositionAndRotation(wp, wr);
                    _timeline.Clear();
                    _lastSeq = int.MinValue;
                    return;
                }
            }

            if (net[RrPropWire.AtSeq] != _lastSeq)
            {
                _lastSeq = net[RrPropWire.AtSeq];
                _timeline.Add(RrPropWire.ToState(net), now);
            }

            RrNetState s;

            if (_timeline.Sample(now, out s) && RrWire.Finite(s.Position))
            {
                transform.SetPositionAndRotation(s.Position, s.Rotation);
            }
        }

        private void SetPuppet(bool on, int[] net)
        {
            Rigidbody rb = Body;

            if (on == _puppet)
            {
                return;
            }

            _puppet = on;

            if (rb == null)
            {
                return;
            }

            if (on)
            {
                _savedGravity = rb.useGravity;
                _savedConstraints = rb.constraints;
                _savedInterpolation = rb.interpolation;

                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.interpolation = RigidbodyInterpolation.None;
                _timeline.Clear();
                _lastSeq = int.MinValue;
                return;
            }

            rb.useGravity = _savedGravity;
            rb.constraints = _savedConstraints;
            rb.interpolation = _savedInterpolation;

            if (net != null && !rb.isKinematic && RrPropWire.State(net) == RrPropWire.StateFlying)
            {
                RrNetState s = RrPropWire.ToState(net);
                rb.linearVelocity = s.Velocity;
                rb.angularVelocity = s.AngularVelocity;
            }
        }

        private void TrackHolder(int[] net, int local)
        {
            int holder = RrPropWire.State(net) == RrPropWire.StateHeld ? RrPropWire.Writer(net) : -1;

            if (holder == _lastHolder)
            {
                return;
            }

            int previous = _lastHolder;
            _lastHolder = holder;

            try
            {
                if (previous >= 0 && previous != local && OnRemoteRelease != null)
                {
                    OnRemoteRelease(previous);
                }

                if (holder >= 0 && holder != local && OnRemoteGrab != null)
                {
                    OnRemoteGrab(holder);
                }
            }
            catch (Exception e)
            {
                RrLog.Error("Net", "prop " + LocalPropId + " grab handler failed", e);
            }
        }

        // ------------------------------------------------------------------ registry

        /// <summary>The local prop ids of the enabled bodies (the current instance's props, which its authority leases).</summary>
        internal static void FillNeeds(List<int> needs)
        {
            needs.Clear();

            for (int i = All.Count - 1; i >= 0; i--)
            {
                RrNetBody b = All[i];

                if (b == null)
                {
                    All.RemoveAt(i);
                    continue;
                }

                int id = b.LocalPropId & 0xFF;

                if (!needs.Contains(id))
                {
                    needs.Add(id);
                }
            }
        }

        /// <summary>The body whose local prop id is this (null none).</summary>
        public static RrNetBody Find(int localPropId)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i].LocalPropId == localPropId)
                {
                    return All[i];
                }
            }

            return null;
        }

        internal static void OnLeased(int localPropId)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i] != null && (All[i].LocalPropId & 0xFF) == localPropId)
                {
                    All[i]._needPose = true;
                }
            }
        }

        internal static void ClearAll()
        {
            All.Clear();
        }
    }
}
