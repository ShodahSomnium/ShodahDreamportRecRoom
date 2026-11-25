using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Where Rec Room's screen mode (desktop) holds a tool: the pose of its hand frame in the view, as the tool's own
    /// <c>Tool.screenSettings.IdleAnimation</c> records it. Held tools go at <c>view * hand * (gp, gr)</c>, the same
    /// Rec Room hold pose (gp, gr) as in VR, so a screen-mode gun points at the crosshair and a bottle stands in the
    /// lower right of the view.
    ///
    /// <b>The data.</b> <c>IdleAnimation.Animation</c> is a <c>RecRoom.PlayerAnimationAsset</c>: a recording of a VR
    /// player's head and hands ("frames": Time, LeftHand / RightHand / Head poses in the body frame, trigger and finger
    /// states) plus per-asset offsets. The screen hand here is frame 0's RightHand pose, moved by
    /// <c>rightHandPositionOffset</c> (body frame) and turned by <c>Euler(rightHandRotationOffset)</c> in its own frame,
    /// relative to Rec Room's screen camera: <c>root + (0, 1.55, 0.01)</c> (FirstPersonScreenPlayerCameraSettings pivot
    /// (0, 1.35, -0.08) + camera (0, 0.2, 0.09)) + <c>cameraPositionOffset</c>. That reading was checked against the
    /// tools the pose must suit (measured 2026-09-24, DN/phase1/grab.md): with it the paintball pistol's barrel lies
    /// 4.1 deg from the view's forward, the burst rifle's 1.0, the crossbow's 1.6, the charades pen's tip 4.3; a Solo
    /// cup stands 3.4 deg from the view's up and the root beer bottle leans 15.6 deg toward the face.
    ///
    /// <b>Where it comes from at runtime.</b> An animation in the prefab's own bundle (most sports and paintball tools)
    /// is read from it, like the screen throw configs. The generic animations live in another bundle (1df8e0ef, not
    /// opened for this), so the ones the Dreamport's tools use are in <see cref="Known"/>, measured with the same
    /// reading. Anything else, and every tool without an IdleAnimation (the pool cue), gets <see cref="Default"/>:
    /// Rec Room's generic "Screens_Default_Idle_HoldingTool_02". Objects with no Rec Room hold pose at all are not
    /// placed through this (RrGrab centres them upright in the view).
    /// </summary>
    public static class RrScreenHold
    {
        /// <summary>Rec Room's first-person screen camera above the player's root (FirstPersonScreenPlayerCameraSettings).</summary>
        public static readonly Vector3 RecRoomCamera = new Vector3(0f, 1.55f, 0.01f);

        /// <summary>One measured screen hand: the IdleAnimation's pathId, its name, the right hand in the view (metres, view axes).</summary>
        public struct Entry
        {
            public readonly long PathId;
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public Entry(long pathId, string name, Vector3 position, Quaternion rotation)
            {
                PathId = pathId;
                Name = name;
                Position = position;
                // The table's 4-decimal values are ~4e-5 off unit length, which alone reads as 1 deg in Quaternion.Angle.
                Rotation = Quaternion.Normalize(rotation);
            }

            public Pose Pose
            {
                get { return new Pose(Position, Rotation); }
            }
        }

        /// <summary>
        /// The IdleAnimations of the Dreamport's tools, measured from the bundles with the reading above (right hand;
        /// the left one is the mirror). Bundle 43ea1176 entries are also read at runtime; the harness compares both.
        /// </summary>
        public static readonly Entry[] Known =
        {
            new Entry(432506639366587634L, "Screens_FPS_PBPistol_AttackReady", new Vector3(0.1265f, -0.1998f, 0.5134f), new Quaternion(-0.2088f, -0.0037f, 0.0012f, 0.9779f)),
            new Entry(-1548489987572327908L, "Screens_FPS_BurstGun_AttackReady", new Vector3(0.1634f, -0.2377f, 0.2186f), new Quaternion(-0.1793f, -0.0063f, -0.0008f, 0.9838f)),
            new Entry(6182427797016904464L, "Screens_FPS_Crossbow_Idle", new Vector3(0.1474f, -0.2509f, 0.4917f), new Quaternion(0.0416f, 0.0131f, -0.0090f, 0.9990f)),
            new Entry(6174749835601728300L, "Screens_FPS_Longbow_Idle", new Vector3(-0.0906f, -0.1235f, 0.4177f), new Quaternion(-0.4098f, -0.1174f, 0.4514f, 0.7839f)),
            new Entry(-5624923231098364159L, "Screens_RecRoyalePotionIdle_01", new Vector3(0.1682f, -0.1196f, 0.4049f), new Quaternion(-0.1899f, 0.0498f, -0.0155f, 0.9804f)),
            new Entry(-1620251211297073511L, "Screens_GenericHoldIdle_02", new Vector3(0.1982f, -0.2496f, 0.4849f), new Quaternion(-0.0071f, -0.0566f, -0.0292f, 0.9979f)),
            new Entry(4105407807365469649L, "Screens_CharadesPen_Idle", new Vector3(0.1785f, -0.1698f, 0.3811f), new Quaternion(-0.2952f, -0.0309f, 0.0027f, 0.9549f)),
            new Entry(-4179572484253039610L, "Screens_FPS_QuestSwordAndShield_Idle_01", new Vector3(0.2030f, -0.2751f, 0.4701f), new Quaternion(-0.1167f, 0.0159f, -0.0283f, 0.9926f)),
            new Entry(-2452710493553015242L, "Screens_PaddleballPaddle_Idle", new Vector3(0.3652f, -0.4167f, 0.4132f), new Quaternion(-0.1965f, 0.3422f, 0.2113f, 0.8942f)),
            new Entry(8873110154974575101L, "Screens_PaddleballPaddle_ActionReady", new Vector3(0.4487f, -0.0144f, 0.4522f), new Quaternion(-0.5603f, 0.5192f, 0.4385f, 0.4735f)),
            new Entry(-6281822192477224967L, "Screens_Basketball_Idle", new Vector3(0.1954f, -0.0498f, 0.3639f), new Quaternion(-0.2253f, 0.0334f, -0.4976f, 0.8369f)),
            new Entry(3713113946385890874L, "Screens_Dodgeball_Idle", new Vector3(0.2210f, -0.0920f, 0.3375f), new Quaternion(-0.3749f, 0.2509f, -0.4184f, 0.7883f)),
            new Entry(DefaultPathId, "Screens_Default_Idle_HoldingTool_02", new Vector3(0.2165f, -0.2337f, 0.3717f), new Quaternion(-0.0325f, 0.1052f, -0.0086f, 0.9939f))
        };

        /// <summary>Rec Room's generic "holding a tool" screen idle (bundle 1df8e0ef #2259509279752159487).</summary>
        public const long DefaultPathId = 2259509279752159487L;

        private static readonly Dictionary<string, Pose?> Parsed = new Dictionary<string, Pose?>();
        private static int _readAtRuntime;
        private static int _fromTable;
        private static int _defaulted;

        /// <summary>The generic screen hand (right hand, view space): 22 cm right, 23 cm down, 37 cm ahead, turned 12 deg inward.</summary>
        public static Pose Default
        {
            get { return Known[Known.Length - 1].Pose; }
        }

        /// <summary>The known entry for an animation pathId.</summary>
        public static bool TryKnown(long pathId, out Entry entry)
        {
            for (int i = 0; i < Known.Length; i++)
            {
                if (Known[i].PathId == pathId)
                {
                    entry = Known[i];
                    return true;
                }
            }

            entry = default(Entry);
            return false;
        }

        /// <summary>The same hand for the other side: x mirrored, rotation (x, -y, -z, w) (Rec Room's own left/right rule).</summary>
        public static Pose Mirror(Pose p)
        {
            return new Pose(new Vector3(-p.position.x, p.position.y, p.position.z), new Quaternion(p.rotation.x, -p.rotation.y, -p.rotation.z, p.rotation.w));
        }

        /// <summary>
        /// The screen hand for a tool's IdleAnimation reference: read from the prefab's own bundle when it is there,
        /// else the measured table. False (use <see cref="Default"/>) for a null reference or an unknown animation in
        /// another bundle. <paramref name="how"/> says which ("bundle", "table", "none").
        /// </summary>
        public static bool Resolve(RrInstance inst, RrTarget anim, out Pose hand, out string how)
        {
            hand = Default;
            how = "none";
            if (anim.IsNull)
            {
                _defaulted++;
                return false;
            }

            var data = inst != null ? inst.Data : null;
            if (data != null && anim.Bundle == data.Bundle && data.File != null)
            {
                string key = anim.Bundle + ":" + anim.PathId;
                Pose? cached;
                if (!Parsed.TryGetValue(key, out cached))
                {
                    cached = null;
                    try
                    {
                        RrObjectInfo o;
                        if (data.File.TryGet(anim.PathId, out o))
                        {
                            Pose p;
                            if (TryRead(new RrConfig(data, data.File.Read(o)), out p))
                            {
                                cached = p;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        RrLog.WarnOnce("Tools", "screenhold:" + key, "screen idle animation " + key + " unreadable: " + e.Message);
                    }

                    Parsed[key] = cached;
                }

                if (cached.HasValue)
                {
                    hand = cached.Value;
                    how = "bundle";
                    _readAtRuntime++;
                    return true;
                }
            }

            Entry known;
            if (TryKnown(anim.PathId, out known))
            {
                hand = known.Pose;
                how = "table";
                _fromTable++;
                return true;
            }

            _defaulted++;
            return false;
        }

        /// <summary>Reads a PlayerAnimationAsset's first frame as a view-space right hand (the reading in the class summary).</summary>
        public static bool TryRead(RrConfig asset, out Pose hand)
        {
            hand = Default;
            if (asset == null)
            {
                return false;
            }

            var frames = asset.List("frames");
            if (frames == null || frames.Count == 0)
            {
                return false;
            }

            var right = frames[0].Sub("RightHand");
            if (right == null)
            {
                return false;
            }

            Vector3 position = RrTools.V3(right.Raw("Position")) + RrTools.V3(asset.Raw("rightHandPositionOffset"));
            Vector3 euler = RrTools.V3(asset.Raw("rightHandRotationOffset"));
            Quaternion rotation = RrTools.Q(right.Raw("Rotation")) * Quaternion.Euler(euler);
            Vector3 camera = RecRoomCamera + RrTools.V3(asset.Raw("cameraPositionOffset"));
            if (position.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            if (rotation.w < 0f)
            {
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            }

            hand = new Pose(position - camera, rotation);
            return true;
        }

        public static string Describe()
        {
            return "screen hands: " + _readAtRuntime + " read from the tool's bundle, " + _fromTable + " from the measured table, " + _defaulted + " default";
        }

        internal static void Shutdown()
        {
            Parsed.Clear();
            _readAtRuntime = 0;
            _fromTable = 0;
            _defaulted = 0;
        }
    }
}
