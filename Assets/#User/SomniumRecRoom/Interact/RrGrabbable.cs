using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Anything a player can pick up: our own component, added to a spawned Rec Room prop (usually by
    /// <see cref="RrTools.MakeTool"/>) or to procedural objects. The grab system (<see cref="RrGrab"/>) moves it with
    /// the hand; the behaviours on the same object (drink, eat, break, shoot, swing) listen to the events.
    ///
    /// The numbers mirror Rec Room's Tool.interactionSettings, read from the prefab's data by RrTools: of 1,058 Tool
    /// instances in the bundles 1,022 throw at x1 and most have no speed limit (HasMaxThrowSpeed 0), the exceptions
    /// being balls and weapons (dodgeball and bowling ball 15 m/s, soccer ball 35, quest melee 8). The hold pose
    /// is the TOOL's pose in Rec Room's hand frame (+Z fingers, +Y thumb side, +X out of the left palm / back of the
    /// right hand; hands_frames.md 2.4 and 3): the legacy {Left,Right}HandPickupTransform when it is set, else the
    /// full-body grip (basketball: the ball sits 0.126 m to the palm side; bottle: 3 cm to the palm side, 3.5 cm
    /// down). RrGrab puts that frame on the hand (<see cref="RrGrab.HoldFrame"/>). Objects without one are held where
    /// they were grabbed, as Rec Room holds a tool whose pickup transform is zero.
    ///
    /// While held the object is kinematic, its colliders become triggers and ignore layer 7: the Somnium client's
    /// physical hand boxes (1 kg on a 150 N joint, layer 7) would otherwise shove the held prop, and the client's VR
    /// far ray, which ignores triggers, would otherwise stop on it (somnium_player.md §3.3).
    /// </summary>
    public class RrGrabbable : MonoBehaviour
    {
        private static readonly List<RrGrabbable> Registry = new List<RrGrabbable>();
        private static int _layerMask;
        private static bool _maskDirty = true;

        /// <summary>
        /// How the right hand holds it: the OBJECT's local pose in Rec Room's hand frame (tool = hand * (position,
        /// rotation); metres at the prefab's scale), exactly Rec Room's Tool.interactionSettings value. Used when
        /// <see cref="HasGrip"/> is set; <see cref="LeftGripPosition"/> is the left hand's (Rec Room stores both,
        /// mirrored: position x -> -x, rotation (x, y, z, w) -> (x, -y, -z, w)). The first build read these the other
        /// way round (as the hand in the object) and in the controller's grip frame - the "turned 90 degrees" report.
        /// </summary>
        public Vector3 GripPosition;
        public Quaternion GripRotation = Quaternion.identity;
        public Vector3 LeftGripPosition;
        public Quaternion LeftGripRotation = Quaternion.identity;

        /// <summary>The grip poses above are a fixed hold pose (Rec Room data); false: held where it was grabbed.</summary>
        public bool HasGrip;

        /// <summary>Which Rec Room field the grip came from ("legacy", "full-body", "full-body via RootOverride", "weapon spec"), for logs.</summary>
        public string GripSource;

        /// <summary>
        /// Screen mode: where Rec Room's screen mode holds this tool's hand, in the view (right hand; metres, view axes),
        /// from its Tool.screenSettings.IdleAnimation (<see cref="RrScreenHold"/>). False: Rec Room's generic
        /// holding pose (<see cref="RrScreenHold.Default"/>).
        /// </summary>
        public bool HasScreenHand;
        public Vector3 ScreenHandPosition;
        public Quaternion ScreenHandRotation = Quaternion.identity;

        /// <summary>RigidbodyPickup.interpolateOnPickup (1 on 632 of 659 pickups): slide into the hand instead of snapping.</summary>
        public bool InterpolateOnPickup = true;

        /// <summary>Tool.LockOnPickup (guns, maker pen, frying pan): stays in the hand when the grip opens; the next grip drops it.</summary>
        public bool LockOnPickup;

        /// <summary>Rec Room's Tool.interactionSettings: ThrowSpeedMultiplier, HasMaxThrowSpeed / MaxThrowSpeed.</summary>
        public float ThrowMultiplier = 1f;
        public bool HasMaxThrowSpeed = true;
        public float MaxThrowSpeed = 15f;

        /// <summary>Can be picked up at all (games lock balls between rounds, etc.).</summary>
        public bool CanGrab = true;

        /// <summary>Two-handed tools (rifles) may be steadied by the other hand; not required.</summary>
        public bool TwoHanded;

        /// <summary>Keep the colliders solid while held (paddles, bats); normally they become triggers.</summary>
        public bool SolidWhileHeld;

        /// <summary>Clip names (Rec Room) for pickup and throw; optional. The clip structs below win when set.</summary>
        public string PickupSound;
        public string ThrowSound;

        /// <summary>Tool.audioSettings: PickupAudio, ThrowAudio, DropAudio, ResetAudio (clip structs).</summary>
        public RrClipInfo PickupClip;
        public RrClipInfo ThrowClip;
        public RrClipInfo DropClip;
        public RrClipInfo ResetClip;

        /// <summary>How the screen-mode (desktop) throw launches it; null = Rec Room's generic ThrowToolBehaviorConfig.</summary>
        public RrScreenThrow ScreenThrow;

        /// <summary>
        /// Screen mode has no arm to lift: behaviours that drink or eat raise <see cref="ToMouth"/> from 0 to 1 and the
        /// grab system brings <see cref="MouthPoint"/> (the bottle's mouth, the slice's bite point) to the player's mouth,
        /// tilted by <see cref="MouthTilt"/> degrees toward the face.
        /// </summary>
        [NonSerialized] public float ToMouth;
        public Transform MouthPoint;
        public float MouthTilt;

        /// <summary>Renderers that glow on hover (Tool.highlightRenderers); null = every mesh renderer under the object.</summary>
        public List<Renderer> HighlightRenderers;

        private Rigidbody _body;
        private bool _bodyLooked;
        private RrNetBody _net;
        private bool _netLooked;
        private int _ownMask;

        /// <summary>The hand holding it, or null.</summary>
        public RrHand Holder { get; protected set; }

        public bool IsHeld
        {
            get { return Holder != null; }
        }

        /// <summary>A hand is hovering it (the highlight is on).</summary>
        public bool IsHovered { get; internal set; }

        /// <summary>Time.time of the last release (behaviours ignore their own drop for a moment).</summary>
        public float LastReleaseTime { get; private set; } = -10f;

        /// <summary>The velocity it was released with.</summary>
        public Vector3 LastReleaseVelocity { get; private set; }

        public event Action<RrHand> OnGrabbed;
        public event Action<RrHand, Vector3> OnReleased;
        public event Action<RrHand> OnUseDown;
        public event Action<RrHand> OnUseUp;

        /// <summary>The root rigidbody (Rec Room tools carry one), or null.</summary>
        public Rigidbody Body
        {
            get
            {
                if (!_bodyLooked || (_body == null && ReferenceEquals(_body, null) == false))
                {
                    _body = GetComponent<Rigidbody>();
                    _bodyLooked = true;
                }

                return _body;
            }
        }

        /// <summary>The networked-prop component when the room shares this prop (RrProps), or null.</summary>
        public RrNetBody NetBody
        {
            get
            {
                if (!_netLooked)
                {
                    _net = GetComponent<RrNetBody>();
                    _netLooked = true;
                }

                return _net;
            }
        }

        /// <summary>Every enabled grabbable (the grab system's candidates).</summary>
        public static IReadOnlyList<RrGrabbable> All
        {
            get { return Registry; }
        }

        /// <summary>The union of the layers our grabbables' colliders are on: the mask of the hover tests.</summary>
        public static int LayerMask
        {
            get
            {
                if (_maskDirty)
                {
                    _maskDirty = false;
                    int m = 1 << RrLayers.Grabbable;
                    for (int i = 0; i < Registry.Count; i++)
                    {
                        if (Registry[i] != null)
                        {
                            m |= Registry[i]._ownMask;
                        }
                    }

                    _layerMask = m;
                }

                return _layerMask;
            }
        }

        /// <summary>The object's pose in Rec Room's hand frame for this hand (see <see cref="GripPosition"/>).</summary>
        public void GripFor(RrHandSide side, out Vector3 position, out Quaternion rotation)
        {
            if (side == RrHandSide.Left)
            {
                position = LeftGripPosition;
                rotation = LeftGripRotation;
            }
            else
            {
                position = GripPosition;
                rotation = GripRotation;
            }
        }

        /// <summary>Called when colliders were added or moved to other layers (after a swap of visuals).</summary>
        public void RefreshLayers()
        {
            int m = 1 << gameObject.layer;
            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                m |= 1 << colliders[i].gameObject.layer;
            }

            _ownMask = m;
            _maskDirty = true;
        }

        protected virtual void OnEnable()
        {
            RefreshLayers();
            if (!Registry.Contains(this))
            {
                Registry.Add(this);
            }

            _maskDirty = true;
        }

        protected virtual void OnDisable()
        {
            Registry.Remove(this);
            _maskDirty = true;
            if (Holder != null)
            {
                RrGrab.Release(this, Vector3.zero, Vector3.zero);
            }

            if (IsHovered)
            {
                RrHighlight.Set(this, false);
                IsHovered = false;
            }
        }

        /// <summary>Called by the grab system.</summary>
        public virtual void NotifyGrabbed(RrHand hand)
        {
            Holder = hand;
            if (OnGrabbed != null)
            {
                OnGrabbed(hand);
            }
        }

        public virtual void NotifyReleased(RrHand hand, Vector3 velocity)
        {
            Holder = null;
            LastReleaseTime = Time.time;
            LastReleaseVelocity = velocity;
            if (OnReleased != null)
            {
                OnReleased(hand, velocity);
            }
        }

        public virtual void NotifyUseDown(RrHand hand)
        {
            if (OnUseDown != null)
            {
                OnUseDown(hand);
            }
        }

        public virtual void NotifyUseUp(RrHand hand)
        {
            if (OnUseUp != null)
            {
                OnUseUp(hand);
            }
        }

        /// <summary>Makes the holder drop it (e.g. when a round ends).</summary>
        public virtual void ForceRelease()
        {
            if (Holder != null)
            {
                RrGrab.Release(this, Vector3.zero, Vector3.zero);
            }
        }

        /// <summary>Statics survive a world re-entry: cleared by RrGrab.Shutdown.</summary>
        internal static void ClearRegistry()
        {
            Registry.Clear();
            _maskDirty = true;
        }
    }
}
