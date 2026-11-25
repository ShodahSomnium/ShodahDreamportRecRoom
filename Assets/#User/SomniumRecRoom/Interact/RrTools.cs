using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Turns a spawned Rec Room prop into a working tool. Rec Room's own scripts arrive as missing scripts, so their
    /// data is read from the prefab (<see cref="RrInstance.Config"/>) and our behaviours are added for every Rec Room
    /// component found: Tool (grip poses, throw multiplier and clamp, pickup/throw/drop/reset clips, hover renderers,
    /// the screen-mode behaviour config) becomes an <see cref="RrGrabbable"/>; CollisionAudio, BreakableObject,
    /// DrinkableBottle (+ Potion), ShakeAudio, DisposableItem, Basketball, AnimateInOut and ToolCleanup become
    /// <see cref="RrCollisionSound"/>, <see cref="RrBreakable"/>, <see cref="RrDrinkable"/>, <see cref="RrShakeSound"/>,
    /// <see cref="RrEdible"/>, <see cref="RrBall"/>, <see cref="RrAnimateInOut"/> and <see cref="RrRespawnable"/>.
    /// A basketball hoop (a prefab with the Rim.ProxyInner collider) gets an <see cref="RrHoop"/>.
    ///
    /// Of 1,058 Tool instances 216 cannot be picked up (furniture, doors, kit pieces) and 330 prefabs carry a
    /// KinematicRigidbodyPickup (movable only with the Maker Pen): both stay put here, though their sounds and
    /// effects are still wired. The helpers below (clip playback with a range, loops, effect references) are shared
    /// by the behaviours.
    /// </summary>
    public static class RrTools
    {
        private static readonly Dictionary<string, RrScreenThrow> ScreenThrows = new Dictionary<string, RrScreenThrow>();
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <summary>How many prefabs were made into tools this session (for the log and the harness).</summary>
        public static int Made { get; private set; }

        /// <summary>Wires every behaviour whose Rec Room component is on the prefab; returns the grabbable or null.</summary>
        public static RrGrabbable MakeTool(RrInstance inst)
        {
            return MakeTool(inst, false);
        }

        /// <summary>
        /// As <see cref="MakeTool(RrInstance)"/>; <paramref name="popIn"/> plays Rec Room's spawn animation (the
        /// prop grows in with a pop), for objects that appear in front of players rather than with a room's build.
        /// </summary>
        public static RrGrabbable MakeTool(RrInstance inst, bool popIn)
        {
            if (inst == null)
            {
                return null;
            }

            var go = inst.gameObject;
            var parts = new List<string>();
            var tool = inst.Config("Tool");
            RrGrabbable grabbable = go.GetComponent<RrGrabbable>();
            if (grabbable == null && CanPickUp(inst, tool))
            {
                grabbable = go.AddComponent<RrGrabbable>();
                ApplyTool(inst, tool, grabbable, parts);
            }

            var collision = inst.Config("CollisionAudio");
            if (collision != null && go.GetComponent<RrCollisionSound>() == null)
            {
                var c = go.AddComponent<RrCollisionSound>();
                c.Setup(inst, collision);
                parts.Add("CollisionSound(" + c.MappingCount + ")");
            }

            var breakable = inst.Config("BreakableObject");
            if (breakable != null && go.GetComponent<RrBreakable>() == null)
            {
                go.AddComponent<RrBreakable>().Setup(inst, breakable);
                parts.Add("Breakable");
            }

            var drink = inst.Config("DrinkableBottle");
            if (drink != null && go.GetComponent<RrDrinkable>() == null)
            {
                go.AddComponent<RrDrinkable>().Setup(inst, drink, inst.Config("Potion"));
                parts.Add("Drinkable");
            }

            var shake = inst.Config("ShakeAudio");
            if (shake != null && go.GetComponent<RrShakeSound>() == null)
            {
                go.AddComponent<RrShakeSound>().Setup(inst, shake);
                parts.Add("ShakeSound");
            }

            var food = inst.Config("DisposableItem");
            if (food != null && go.GetComponent<RrEdible>() == null)
            {
                var e = go.AddComponent<RrEdible>();
                e.Setup(inst, food);
                if (e.Bites == 0)
                {
                    UnityEngine.Object.Destroy(e);
                }
                else
                {
                    parts.Add("Edible(" + e.Bites + " bites)");
                }
            }

            var ball = inst.Config("Basketball");
            if (ball != null && go.GetComponent<RrBall>() == null)
            {
                go.AddComponent<RrBall>().Setup(inst, ball);
                parts.Add("Ball");
            }

            if (inst.Find("Rim.ProxyInner") != null && go.GetComponent<RrHoop>() == null)
            {
                go.AddComponent<RrHoop>().Setup(inst);
                parts.Add("Hoop");
            }

            var anim = inst.Config("AnimateInOut");
            RrAnimateInOut animate = go.GetComponent<RrAnimateInOut>();
            bool suppress = tool != null && tool.Sub("renderSettings") != null && tool.Sub("renderSettings").Bool("SuppressAnimateInOut");
            if (anim != null && animate == null && !suppress && !anim.Bool("SuppressAnimation"))
            {
                animate = go.AddComponent<RrAnimateInOut>();
                animate.Setup(inst, anim);
                parts.Add("AnimateInOut");
            }

            var cleanup = inst.Config("ToolCleanup");
            if (cleanup != null && grabbable != null && !cleanup.Bool("excludeFromCleanup") && go.GetComponent<RrRespawnable>() == null)
            {
                go.AddComponent<RrRespawnable>().Setup(inst, cleanup, tool);
                parts.Add("Respawnable");
            }

            Made++;
            string name = string.IsNullOrEmpty(inst.PrefabName) ? go.name : inst.PrefabName;
            Once("Tools", "make:" + name, name + ": " + (grabbable != null ? Describe(grabbable) : "not grabbable") +
                (parts.Count > 0 ? " + " + string.Join(" + ", parts) : ""));
            if (popIn && animate != null)
            {
                animate.AnimateIn();
            }

            return grabbable;
        }

        private static bool CanPickUp(RrInstance inst, RrConfig tool)
        {
            if (tool == null)
            {
                // No Tool (spawned without data, or a plain physics prop): a free rigidbody is still something to pick up.
                var body = inst.GetComponent<Rigidbody>();
                return body != null && !body.isKinematic;
            }

            var interaction = tool.Sub("interactionSettings");
            if (interaction != null && !interaction.Bool("CanBePickedUp", true))
            {
                return false;
            }

            return !(inst.Has("KinematicRigidbodyPickup") && !inst.Has("RigidbodyPickup"));
        }

        private static void ApplyTool(RrInstance inst, RrConfig tool, RrGrabbable g, List<string> parts)
        {
            var pickup = inst.Config("RigidbodyPickup") ?? inst.Config("StabilizedRigidbodyPickup");
            if (pickup != null)
            {
                g.InterpolateOnPickup = pickup.Bool("interpolateOnPickup", true);
            }

            if (tool == null)
            {
                g.HasMaxThrowSpeed = false;
                return;
            }

            var i = tool.Sub("interactionSettings");
            if (i != null)
            {
                g.LockOnPickup = i.Bool("LockOnPickup");
                g.ThrowMultiplier = i.Float("ThrowSpeedMultiplier", 1f);
                g.HasMaxThrowSpeed = i.Bool("HasMaxThrowSpeed");
                g.MaxThrowSpeed = i.Float("MaxThrowSpeed", 10f);
                g.TwoHanded = i.Int("Handedness") == 2;
            }

            ApplyHold(inst, tool, g);
            var a = tool.Sub("audioSettings");
            if (a != null)
            {
                g.PickupClip = a.Clip("PickupAudio");
                g.ThrowClip = a.Clip("ThrowAudio");
                g.DropClip = a.Clip("DropAudio");
                g.ResetClip = a.Clip("ResetAudio");
            }

            g.HighlightRenderers = HighlightRenderersOf(inst, tool);
            var screen = tool.Sub("screenSettings");
            if (screen != null)
            {
                g.ScreenThrow = ScreenThrowOf(inst, screen.Ref("ScreenBehaviorConfig"));
            }
        }

        /// <summary>
        /// Reads a prefab's Rec Room hold poses into a grabbable (also for weapons and melee tools, which make their own
        /// grabbable): see <see cref="ApplyHold(RrInstance, RrConfig, RrGrabbable)"/>. True when a hold pose was found.
        /// </summary>
        public static bool ApplyHold(RrInstance inst, RrGrabbable g)
        {
            return inst != null && ApplyHold(inst, inst.Config("Tool"), g);
        }

        /// <summary>
        /// The hold poses, as the TOOL's local pose in Rec Room's hand frame (hands_frames.md 3): the legacy
        /// {Left,Right}HandPickupTransform first - the PlayerHand-frame pose every tool checked is consistent in (the
        /// pistol's handle passes 1.1 cm from the fist centre) - and the FullBody{Left,Right}HandGripTransform only when
        /// both legacy positions are zero (bottle, basketball, dodgeball). FullBody poses are relative to
        /// HandVisualPickupInfo.RootOverride when that is set (the pistol's is its FBX root, turned 180 deg about Y,
        /// which is why the first build's FullBody-first pistol pointed backward), so they are carried back to the
        /// tool's root. A side Rec Room left empty is the other side mirrored: position x -> -x, rotation
        /// (x, y, z, w) -> (x, -y, -z, w) (the stored pairs obey it, e.g. the ping-pong paddle's). Also reads the
        /// screen-mode hand from Tool.screenSettings.IdleAnimation (<see cref="RrScreenHold"/>).
        /// </summary>
        private static bool ApplyHold(RrInstance inst, RrConfig tool, RrGrabbable g)
        {
            if (tool == null || g == null)
            {
                return false;
            }

            Vector3 rp, lp;
            Quaternion rr, lr;
            string source;
            bool found = FindHold(inst, tool, out rp, out rr, out lp, out lr, out source);
            if (found)
            {
                g.HasGrip = true;
                g.GripSource = source;
                g.GripPosition = rp;
                g.GripRotation = rr;
                g.LeftGripPosition = lp;
                g.LeftGripRotation = lr;
                Once("Tools", "hold:" + source, "hold poses: " + inst.name + " uses the " + source + " pose (right " + rp.ToString("F3") + " " + rr.eulerAngles.ToString("F1") +
                                                 ") - the tool's pose in Rec Room's hand frame (logged once per kind)");
            }

            var screen = tool.Sub("screenSettings");
            var idle = screen != null ? screen.Sub("IdleAnimation") : null;
            Pose hand;
            string how;
            if (idle != null && RrScreenHold.Resolve(inst, idle.Ref("Animation"), out hand, out how))
            {
                g.HasScreenHand = true;
                g.ScreenHandPosition = hand.position;
                g.ScreenHandRotation = hand.rotation;
                Once("Tools", "screenhand:" + how, "screen hand: " + inst.name + " from its IdleAnimation (" + how + "): " + hand.position.ToString("F3") + " in the view, " +
                                                  hand.rotation.eulerAngles.ToString("F1") + " (logged once per source)");
            }

            return found;
        }

        /// <summary>
        /// The tool's hold pose for one hand, in Rec Room's hand frame (see <see cref="ApplyHold(RrInstance, RrGrabbable)"/>),
        /// for code that draws a tool in someone's hand without a grabbable (remote players' quest gear). False when the
        /// prefab has no hold pose.
        /// </summary>
        public static bool TryHoldPose(RrInstance inst, Player.RrHandSide side, out Vector3 position, out Quaternion rotation)
        {
            Vector3 rp, lp;
            Quaternion rr, lr;
            string source;
            if (inst == null || !FindHold(inst, inst.Config("Tool"), out rp, out rr, out lp, out lr, out source))
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            position = side == Player.RrHandSide.Left ? lp : rp;
            rotation = side == Player.RrHandSide.Left ? lr : rr;
            return true;
        }

        /// <summary>Both hands' hold poses (a side Rec Room left empty is the other one mirrored).</summary>
        private static bool FindHold(RrInstance inst, RrConfig tool, out Vector3 rightPos, out Quaternion rightRot, out Vector3 leftPos, out Quaternion leftRot, out string source)
        {
            rightPos = leftPos = Vector3.zero;
            rightRot = leftRot = Quaternion.identity;
            source = "none";
            var i = tool != null ? tool.Sub("interactionSettings") : null;
            if (i == null)
            {
                return false;
            }

            Vector3 rp, lp;
            Quaternion rr, lr;
            bool right = GripOf(i, "RightHandPickupTransform", out rp, out rr);
            bool left = GripOf(i, "LeftHandPickupTransform", out lp, out lr);
            source = "legacy";
            if (!right && !left)
            {
                right = GripOf(i, "FullBodyRightHandGripTransform", out rp, out rr);
                left = GripOf(i, "FullBodyLeftHandGripTransform", out lp, out lr);
                source = "full-body";
                Transform over = OverrideNode(inst, i);
                if ((right || left) && over != null && over != inst.transform)
                {
                    // node = root * O, hand * fb = node  =>  root = hand * fb * Inverse(O).
                    Quaternion io = Quaternion.Inverse(Quaternion.Inverse(inst.transform.rotation) * over.rotation);
                    Vector3 iop = -(io * inst.transform.InverseTransformPoint(over.position));
                    if (right)
                    {
                        rp += rr * iop;
                        rr *= io;
                    }

                    if (left)
                    {
                        lp += lr * iop;
                        lr *= io;
                    }

                    source = "full-body via RootOverride '" + over.name + "'";
                }
            }

            if (!right && !left)
            {
                return false;
            }

            rightPos = right ? rp : MirrorPosition(lp);
            rightRot = right ? rr : MirrorRotation(lr);
            leftPos = left ? lp : MirrorPosition(rp);
            leftRot = left ? lr : MirrorRotation(rr);
            return true;
        }

        /// <summary>Rec Room's mirror of a hold position between the hands (x -> -x).</summary>
        public static Vector3 MirrorPosition(Vector3 p)
        {
            return new Vector3(-p.x, p.y, p.z);
        }

        /// <summary>Rec Room's mirror of a hold rotation between the hands ((x, y, z, w) -> (x, -y, -z, w)).</summary>
        public static Quaternion MirrorRotation(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        private static Transform OverrideNode(RrInstance inst, RrConfig interaction)
        {
            var info = interaction.Sub("HandVisualPickupInfo");
            if (info == null)
            {
                return null;
            }

            var t = info.Ref("RootOverride");
            if (t.IsNull || !t.IsInPrefab)
            {
                return null;
            }

            return inst.NodeOf(t);
        }

        private static string Describe(RrGrabbable g)
        {
            return (g.HasGrip ? "grip (" + g.GripSource + ")" : "held where grabbed") +
                   ", throw x" + g.ThrowMultiplier.ToString("0.##") +
                   (g.HasMaxThrowSpeed ? " max " + g.MaxThrowSpeed.ToString("0.#") + " m/s" : " unclamped") +
                   (g.LockOnPickup ? ", lock on pickup" : "") +
                   (g.HighlightRenderers != null ? ", " + g.HighlightRenderers.Count + " hover renderers" : "") +
                   ", screen " + (g.ScreenThrow != null ? g.ScreenThrow.Name + " " + g.ScreenThrow.Speed.ToString("0.#") + " m/s" : "generic throw");
        }

        private static bool GripOf(RrConfig interaction, string field, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            var t = interaction.Sub(field);
            if (t == null)
            {
                return false;
            }

            position = V3(t.Raw("Position"));
            rotation = Q(t.Raw("Rotation"));
            return position.sqrMagnitude > 1e-8f;
        }

        /// <summary>Tool.highlightRenderers minus renderSettings.RenderersThatDoNotHoverHighlight; null when none resolve.</summary>
        private static List<Renderer> HighlightRenderersOf(RrInstance inst, RrConfig tool)
        {
            var excluded = new HashSet<Renderer>();
            var render = tool.Sub("renderSettings");
            if (render != null)
            {
                foreach (var t in render.Refs("RenderersThatDoNotHoverHighlight"))
                {
                    var r = RendererOf(inst, t);
                    if (r != null)
                    {
                        excluded.Add(r);
                    }
                }
            }

            var result = new List<Renderer>();
            foreach (var entry in tool.List("highlightRenderers"))
            {
                var r = RendererOf(inst, entry.Ref("Renderer"));
                if (r != null && !excluded.Contains(r) && !result.Contains(r))
                {
                    result.Add(r);
                }
            }

            return result.Count > 0 ? result : null;
        }

        private static Renderer RendererOf(RrInstance inst, RrTarget t)
        {
            if (t.IsNull || !t.IsInPrefab)
            {
                return null;
            }

            var node = inst.NodeOf(t);
            return node != null ? node.GetComponent<Renderer>() : null;
        }

        /// <summary>
        /// The screen behaviour config (a ScriptableObject) read from the prefab's own bundle, where the ball, food
        /// and drink configs live; configs in other bundles fall back to Rec Room's generic throw.
        /// </summary>
        private static RrScreenThrow ScreenThrowOf(RrInstance inst, RrTarget t)
        {
            if (t.IsNull || inst.Data == null)
            {
                return null;
            }

            string key = t.Bundle + ":" + t.PathId;
            RrScreenThrow result;
            if (ScreenThrows.TryGetValue(key, out result))
            {
                return result;
            }

            result = null;
            try
            {
                RrObjectInfo o;
                if (t.Bundle == inst.Data.Bundle && inst.Data.File != null && inst.Data.File.TryGet(t.PathId, out o))
                {
                    result = RrScreenThrow.FromConfig(new RrConfig(inst.Data, inst.Data.File.Read(o)));
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Tools", "screen:" + key, "screen behaviour config " + key + " unreadable: " + e.Message);
            }

            ScreenThrows[key] = result;
            return result;
        }

        // ------------------------------------------------------------------ shared helpers

        /// <summary>A Rec Room AnimationCurve's keys as a Unity curve; null when the curve has no keys.</summary>
        public static AnimationCurve CurveOf(List<RrKey> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return null;
            }

            var frames = new Keyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                frames[i] = new Keyframe(keys[i].Time, keys[i].Value, keys[i].InSlope, keys[i].OutSlope);
            }

            var curve = new AnimationCurve(frames);
            curve.preWrapMode = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
            return curve;
        }

        /// <summary>A curve evaluated with its ends clamped (Rec Room's Pre/PostInfinity 2); <paramref name="fallback"/> when null.</summary>
        public static float Evaluate(AnimationCurve curve, float x, float fallback = 1f)
        {
            if (curve == null || curve.length == 0)
            {
                return fallback;
            }

            float t0 = curve.keys[0].time;
            float t1 = curve.keys[curve.length - 1].time;
            return curve.Evaluate(Mathf.Clamp(x, t0, t1));
        }

        /// <summary>A Vector3 from the parser's raw value ({x,y,z} or [x,y,z]).</summary>
        public static Vector3 V3(object raw)
        {
            var d = raw as Dictionary<string, object>;
            if (d != null)
            {
                return new Vector3(F(d, "x"), F(d, "y"), F(d, "z"));
            }

            var l = raw as List<object>;
            if (l != null && l.Count >= 3)
            {
                return new Vector3(RrConfig.ToFloat(l[0], 0f), RrConfig.ToFloat(l[1], 0f), RrConfig.ToFloat(l[2], 0f));
            }

            var f = raw as float[];
            return f != null && f.Length >= 3 ? new Vector3(f[0], f[1], f[2]) : Vector3.zero;
        }

        /// <summary>A rotation from the parser's raw value ({x,y,z,w}); identity when zero or missing.</summary>
        public static Quaternion Q(object raw)
        {
            var d = raw as Dictionary<string, object>;
            if (d == null)
            {
                return Quaternion.identity;
            }

            var q = new Quaternion(F(d, "x"), F(d, "y"), F(d, "z"), F(d, "w"));
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-6f)
            {
                return Quaternion.identity;
            }

            float s = 1f / Mathf.Sqrt(n);
            return new Quaternion(q.x * s, q.y * s, q.z * s, q.w * s);
        }

        private static float F(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? RrConfig.ToFloat(v, 0f) : 0f;
        }

        /// <summary>The live transform of a config reference into the prefab (a GameObject, Transform or component), or null.</summary>
        public static Transform NodeOf(RrInstance inst, RrConfig c, string field)
        {
            if (inst == null || c == null)
            {
                return null;
            }

            var t = c.Ref(field);
            return t.IsNull || !t.IsInPrefab ? null : inst.NodeOf(t);
        }

        /// <summary>Every live transform a config list of references points at (missing ones skipped).</summary>
        public static List<Transform> NodesOf(RrInstance inst, RrConfig c, string field)
        {
            var result = new List<Transform>();
            if (inst == null || c == null)
            {
                return result;
            }

            foreach (var t in c.Refs(field))
            {
                if (!t.IsNull && t.IsInPrefab)
                {
                    var n = inst.NodeOf(t);
                    if (n != null)
                    {
                        result.Add(n);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Plays a clip struct with a chosen range and an extra pitch (RrSound.Play fixes the range at 40 m):
        /// volume = clip volume x <paramref name="volumeScale"/>, pitch = 1 + pitchShift +- pitchVariation + <paramref name="pitchAdd"/>.
        /// </summary>
        public static void Play(RrClipInfo info, Vector3 position, float volumeScale = 1f, float maxDistance = 40f, float pitchAdd = 0f, Transform follow = null)
        {
            if (!info.HasClip || RrAssets.Instance == null || volumeScale <= 0f)
            {
                return;
            }

            float volume = RrSound.VolumeOf(info) * volumeScale;
            float pitch = Mathf.Clamp(RrSound.PitchOf(info) + pitchAdd, 0.1f, 3f);
            RrAssets.Instance.Resolve<AudioClip>(info.Clip, clip =>
            {
                if (clip != null)
                {
                    RrSound.Play(clip, follow != null ? follow.position : position, volume, pitch, maxDistance, follow);
                }
            });
        }

        /// <summary>One clip of a Rec Room pool, at random (a clip listed several times is more likely, as in Rec Room).</summary>
        public static void PlayAny(List<RrClipInfo> infos, Vector3 position, float volumeScale = 1f, float maxDistance = 40f, Transform follow = null)
        {
            if (infos != null && infos.Count > 0)
            {
                Play(infos[UnityEngine.Random.Range(0, infos.Count)], position, volumeScale, maxDistance, 0f, follow);
            }
        }

        /// <summary>
        /// A looping 3D source on <paramref name="follow"/> for a clip struct (the pour fizz); it starts when the clip
        /// has loaded. Stop it with <see cref="RrSound.Stop"/>; <see cref="SetLoop"/> changes its level and pitch.
        /// </summary>
        public static AudioSource Loop(RrClipInfo info, Transform follow, float maxDistance = 12f)
        {
            if (!info.HasClip || RrAssets.Instance == null || follow == null)
            {
                return null;
            }

            var go = new GameObject("Rr Loop");
            go.transform.SetParent(follow, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = maxDistance;
            source.volume = Mathf.Clamp01(RrSound.VolumeOf(info) * RrSound.Volume);
            source.pitch = 1f + info.PitchShift;
            RrAssets.Instance.Resolve<AudioClip>(info.Clip, clip =>
            {
                if (clip != null && source != null)
                {
                    source.clip = clip;
                    source.Play();
                }
            });
            return source;
        }

        public static void SetLoop(AudioSource source, RrClipInfo info, float volumeScale, float pitchAdd)
        {
            if (source != null)
            {
                source.volume = Mathf.Clamp01(RrSound.VolumeOf(info) * RrSound.Volume * volumeScale);
                source.pitch = Mathf.Clamp(1f + info.PitchShift + pitchAdd, 0.1f, 3f);
            }
        }

        /// <summary>
        /// Spawns an effect prefab a config references. Configs point at the effect's PooledParticle component, which
        /// the catalog does not index (it lists prefab roots), so the known effect's prefab name is the fallback.
        /// </summary>
        public static void SpawnFx(RrTarget prefabRef, string fallbackName, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (RrAssets.Instance == null)
            {
                return;
            }

            if (!prefabRef.IsNull && !prefabRef.IsInPrefab && RrAssets.Instance.Entry(prefabRef.Bundle, prefabRef.PathId).HasValue)
            {
                RrFx.Spawn(prefabRef, position, rotation, scale);
                return;
            }

            if (!string.IsNullOrEmpty(fallbackName))
            {
                RrFx.Spawn(fallbackName, position, rotation, scale);
            }
        }

        /// <summary>Logs a fact once per session (key), at Info level.</summary>
        public static void Once(string area, string key, string text)
        {
            if (Logged.Add(area + "|" + key))
            {
                RrLog.Info(area, text);
            }
        }

        /// <summary>The local player and the remote players in view: is anyone within <paramref name="distance"/> of a point?</summary>
        public static bool PlayerNear(Vector3 point, float distance)
        {
            float d2 = distance * distance;
            Vector3 feet = Player.RrPlayer.FeetPosition;
            Vector3 head = Player.RrPlayer.HeadPosition;
            if (FlatDistance2(feet, head, point) < d2)
            {
                return true;
            }

            var remotes = Player.RrRemotePlayers.All;
            if (remotes != null)
            {
                for (int i = 0; i < remotes.Count; i++)
                {
                    var r = remotes[i];
                    if (r == null || !r.WithMe)
                    {
                        continue;
                    }

                    Transform a = r.Root != null ? r.Root : r.Head;
                    Transform b = r.Head != null ? r.Head : r.Root;
                    if (a != null && FlatDistance2(a.position, b.position, point) < d2)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Squared distance from a point to the segment feet-head (a standing player).</summary>
        private static float FlatDistance2(Vector3 feet, Vector3 head, Vector3 p)
        {
            Vector3 ab = head - feet;
            float len2 = ab.sqrMagnitude;
            float k = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - feet, ab) / len2) : 0f;
            return (feet + ab * k - p).sqrMagnitude;
        }

        internal static void Shutdown()
        {
            ScreenThrows.Clear();
            RrScreenHold.Shutdown();
            Logged.Clear();
            Made = 0;
            RrBall.ClearStatics();
            RrHoop.ClearStatics();
            RrCollisionSound.ClearStatics();
        }
    }
}
