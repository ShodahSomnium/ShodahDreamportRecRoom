using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>
    /// "Be a Rec Room avatar" (iteration 15, opt-in): the local player can wear a classic Rec Room avatar dressed from
    /// their own install, or keep their Somnium avatar - the default. The choice and the outfit are saved locally
    /// (<see cref="RrAvatarOutfit.FilePath"/>) and come back on the next visit; the watch's Avatar page edits them.
    ///
    /// - <b>Local</b>: while worn, an <see cref="RrAvatarRig"/> follows the player's view (VR camera; desktop: the Bridge
    ///   head anchor, or the camera in first-person play mode) and hands (VR: the grip frames turned to Rec Room's hand
    ///   frame, <see cref="RrHand.RecRoomFrame"/>; desktop: resting beside the torso), and the player's Somnium avatar
    ///   renderers are hidden by <see cref="RrAvatarHide"/> - nothing else of it is touched, and switching back or
    ///   leaving the world shows it again exactly.
    /// - <b>Others</b> see it through the presence record (ints 48-58, <see cref="RrAvatarWire"/>): every client builds a
    ///   rig for each remote player whose record says "wearing", posed from the Bridge's networked head and controller
    ///   anchors (RrRemotePlayers; desktop players have no hands -> rest pose), hides that player's Somnium avatar, and
    ///   shows the rig only while the player is with them (isolation: players in other places stay hidden).
    /// - <b>First person</b>: a rig's head is hidden from a camera inside it (the player's own view) and shown to every
    ///   other camera, so mirrors show you; the camera callback is kept last in the render pipeline's list so a mirror
    ///   rendered from inside the main camera's callback still sees the head.
    /// - <b>Preview</b>: while the Avatar page shows, a second rig of your outfit stands in front of you facing you.
    /// Nothing is kept across world visits (statics cleared in <see cref="Shutdown"/>); no DontDestroyOnLoad.
    /// </summary>
    public static class RrAvatars
    {
        private sealed class RemoteRig
        {
            public RrAvatarRig Rig;
            public string Key;
        }

        private static GameObject _object;
        private static RrAvatarOutfit _outfit;
        private static RrAvatarRig _local;
        private static RrAvatarRig _preview;
        private static readonly Dictionary<int, RemoteRig> Remotes = new Dictionary<int, RemoteRig>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly HashSet<int> Seen = new HashSet<int>();
        private static string _localKey = "";
        private static string _previewKey = "";
        private static float _saveAt = -1f;
        private static float _previewWantedAt = -100f;

        /// <summary>Seconds the preview stays after the Avatar page was last shown.</summary>
        public const float PreviewLinger = 8f;

        private static Vector3 _previewAt;
        private static bool _previewPlaced;
        private static bool _loggedReady;
        private static int _errors;
        private static bool _subscribed;

        /// <summary>The local outfit (read only: change it through <see cref="Edit"/>).</summary>
        public static RrAvatarOutfit Outfit
        {
            get
            {
                if (_outfit == null)
                {
                    _outfit = RrAvatarOutfit.Load() ?? RrAvatarOutfit.Default();
                }

                return _outfit;
            }
        }

        /// <summary>The local player wears the Rec Room avatar (false = the Somnium avatar, the default).</summary>
        public static bool Wearing
        {
            get { return Outfit.wear; }
        }

        /// <summary>The install's lists are up (the editor can show items).</summary>
        public static bool Ready
        {
            get { return RrAvatarCatalog.Ready; }
        }

        /// <summary>The outfit or the choice changed.</summary>
        public static event Action Changed;

        public static void Init()
        {
            if (_object != null)
            {
                return;
            }

            Transform parent = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _object = new GameObject("Rr Avatars");
            if (parent != null)
            {
                _object.transform.SetParent(parent, false);
            }

            _object.AddComponent<RrAvatarDriver>();
            var o = Outfit;
            RrLog.Info("Avatar", "up: the saved choice is the " + (o.wear ? "Rec Room avatar" : "Somnium avatar") + " (" + RrAvatarOutfit.FilePath + ")");
        }

        /// <summary>World unload (RrMain.OnDestroy): Somnium avatars back, rigs gone, the outfit saved, statics cleared.</summary>
        public static void Shutdown()
        {
            try
            {
                RrAvatarHide.RestoreAll();
            }
            catch (Exception e)
            {
                RrLog.Error("Avatar", "restoring the Somnium avatars failed", e);
            }

            if (_saveAt >= 0f && _outfit != null)
            {
                _outfit.Save();
            }

            DestroyRigs();
            Unsubscribe();
            if (_object != null)
            {
                Object.Destroy(_object);
            }

            _object = null;
            _outfit = null;
            _localKey = _previewKey = "";
            _saveAt = -1f;
            _previewWantedAt = -100f;
            _loggedReady = false;
            _errors = 0;
            Changed = null;
            RrAvatarCatalog.Shutdown();
            RrAvatarOutfit.ResetStatics();
        }

        /// <summary>The driver object went away without Shutdown (scene unloaded): give the avatars back at least.</summary>
        internal static void OnDriverDestroyed()
        {
            try
            {
                RrAvatarHide.RestoreAll();
                DestroyRigs();
                Unsubscribe();
            }
            catch (Exception e)
            {
                RrLog.Error("Avatar", "cleanup on driver destroy failed", e);
            }
        }

        private static void DestroyRigs()
        {
            if (_local != null)
            {
                _local.Destroy();
                _local = null;
            }

            if (_preview != null)
            {
                _preview.Destroy();
                _preview = null;
                _previewPlaced = false;
            }

            foreach (var r in Remotes.Values)
            {
                r.Rig.Destroy();
            }

            Remotes.Clear();
        }

        /// <summary>Wear the Rec Room avatar (true) or the Somnium avatar (false). Saved.</summary>
        public static void SetWear(bool wear)
        {
            if (Outfit.wear == wear)
            {
                return;
            }

            Edit(o => o.wear = wear);
            RrLog.Info("Avatar", wear ? "wearing the Rec Room avatar" : "wearing the Somnium avatar");
        }

        /// <summary>Changes the outfit (saved half a second later, sent with the next presence push).</summary>
        public static void Edit(Action<RrAvatarOutfit> change)
        {
            var o = Outfit;
            change(o);
            o.Repair();
            _saveAt = Time.unscaledTime + 0.5f;
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                RrLog.Error("Avatar", "a Changed handler threw", e);
            }
        }

        /// <summary>The Avatar page calls this every frame it shows: the preview stands in front of the player meanwhile.</summary>
        public static void WantPreview()
        {
            _previewWantedAt = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ per frame

        internal static void LateTick()
        {
            try
            {
                Tick(Time.deltaTime);
            }
            catch (Exception e)
            {
                _errors++;
                if (_errors <= 3)
                {
                    RrLog.Error("Avatar", "tick threw (#" + _errors + ")", e);
                }
            }

            // Isolation runs before us (player driver, order 10000); the hide re-applies after it every frame.
            try
            {
                RrAvatarHide.Tick();
            }
            catch (Exception e)
            {
                _errors++;
                if (_errors <= 3)
                {
                    RrLog.Error("Avatar", "hide tick threw (#" + _errors + ")", e);
                }
            }

            Resubscribe();
        }

        private static void Tick(float dt)
        {
            if (!RrAvatarCatalog.EnsureBuilt())
            {
                return;
            }

            if (!_loggedReady)
            {
                _loggedReady = true;
                if (Outfit.wear)
                {
                    RrLog.Info("Avatar", "putting the saved Rec Room avatar back on");
                }
            }

            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            {
                _saveAt = -1f;
                if (Outfit.Save())
                {
                    RrLog.Info("Avatar", "saved: " + Describe(Outfit));
                }
            }

            Transform parent = _object != null ? _object.transform : null;
            TickLocal(parent, dt);
            TickPreview(parent, dt);

            // The outfit goes out in the presence record (only changed ints are pushed, at the net layer's rate).
            RrAvatarWire.Write(RrNet.Core.Own, Outfit);

            TickRemotes(parent, dt);
        }

        private static void TickLocal(Transform parent, float dt)
        {
            var o = Outfit;
            string nick = LocalNick();
            if (!o.wear)
            {
                if (_local != null)
                {
                    _local.Destroy();
                    _local = null;
                    _localKey = "";
                }

                RrAvatarHide.Set(RrPlayer.LocalId, true, false, nick, null);
                return;
            }

            if (_local == null || _local.Destroyed)
            {
                _local = new RrAvatarRig(parent, "(you)");
                _localKey = "";
            }

            string key = Key(o);
            if (key != _localKey)
            {
                _localKey = key;
                _local.Apply(o);
            }

            Vector3 eye;
            Quaternion look;
            LocalView(out eye, out look);
            _local.Pose(eye, look, LocalHand(RrHand.Left), LocalHand(RrHand.Right), dt);
            _local.SetGrip(Curl(RrHand.Left), Curl(RrHand.Right), dt);
            _local.SetVisible(true);
            RrAvatarHide.Set(RrPlayer.LocalId, true, true, nick, () => RrPlayer.HeadAnchor != null ? RrPlayer.HeadAnchor.position : (Vector3?)null);
        }

        private static void TickPreview(Transform parent, float dt)
        {
            // It lingers a few seconds after the page closes: in a headset the open menu dims what is behind it.
            bool want = Time.unscaledTime - _previewWantedAt < PreviewLinger;
            if (!want)
            {
                if (_preview != null)
                {
                    _preview.Destroy();
                    _preview = null;
                    _previewPlaced = false;
                    _previewKey = "";
                }

                return;
            }

            if (_preview == null || _preview.Destroyed)
            {
                _preview = new RrAvatarRig(parent, "(preview)");
                _previewKey = "";
            }

            var o = Outfit;
            string key = Key(o, true);
            if (key != _previewKey)
            {
                _previewKey = key;
                var shown = o.Copy();
                shown.wear = true;
                _preview.Apply(shown);
            }

            // Placed once, ahead and to the right of the player (clear of the desktop menu, 0.9 m wide at 0.8 m), then
            // left there so the player can turn to it; it always faces them, like a mirror that works anywhere. It is
            // placed again when the player walks off (over 3 m).
            Vector3 eye;
            Quaternion look;
            LocalView(out eye, out look);
            Vector3 flatAway = _previewAt - eye;
            flatAway.y = 0f;
            if (!_previewPlaced || flatAway.sqrMagnitude > 9f)
            {
                Vector3 f = look * Vector3.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 1e-4f)
                {
                    f = Vector3.forward;
                }

                f.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, f);
                _previewAt = eye + f * 1.2f + right * 0.85f + Vector3.down * 0.05f;
                _previewPlaced = true;
            }

            _previewAt.y = Mathf.Lerp(_previewAt.y, eye.y - 0.05f, 1f - Mathf.Exp(-dt / 0.3f));
            Vector3 at = _previewAt;
            Vector3 face = eye - at;
            face.y = 0f;
            if (face.sqrMagnitude < 1e-4f)
            {
                face = Vector3.forward;
            }

            _preview.Pose(at, Quaternion.LookRotation(face.normalized, Vector3.up), null, null, dt);
            _preview.SetGrip(0.2f, 0.2f, dt);
            _preview.SetVisible(true);
        }

        private static void TickRemotes(Transform parent, float dt)
        {
            Seen.Clear();
            var all = RrRemotePlayers.All;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null || r.Id < 0)
                {
                    continue;
                }

                Seen.Add(r.Id);
                int[] record = RrNet.PresenceRecord(r.Id);
                bool wearing = RrAvatarWire.Wearing(record) && r.Root != null && r.Head != null;
                RemoteRig rr;
                Remotes.TryGetValue(r.Id, out rr);
                if (!wearing)
                {
                    if (rr != null)
                    {
                        rr.Rig.Destroy();
                        Remotes.Remove(r.Id);
                        RrLog.Info("Avatar", "player " + r.Id + " (" + r.Nick + ") wears their Somnium avatar again");
                    }

                    RrAvatarHide.Set(r.Id, false, false, r.RawNick, null);
                    continue;
                }

                if (rr == null || rr.Rig.Destroyed)
                {
                    rr = new RemoteRig { Rig = new RrAvatarRig(parent, r.Nick), Key = "" };
                    Remotes[r.Id] = rr;
                    RrLog.Info("Avatar", "player " + r.Id + " (" + r.Nick + ") wears a Rec Room avatar");
                }

                string key = RrAvatarWire.Key(record);
                if (key != rr.Key)
                {
                    rr.Key = key;
                    rr.Rig.Apply(RrAvatarWire.Read(record));
                }

                bool show = r.WithMe && !RrIsolation.IsHidden(r.Id);
                rr.Rig.SetVisible(show);
                if (show)
                {
                    rr.Rig.Pose(r.Head.position, r.Head.rotation, RemoteHand(r.LeftHand, RrHandSide.Left), RemoteHand(r.RightHand, RrHandSide.Right), dt);
                    rr.Rig.SetGrip(RemoteCurl(record, true), RemoteCurl(record, false), dt);
                }

                var remote = r;
                RrAvatarHide.Set(r.Id, false, true, r.RawNick, () => remote.Head != null ? remote.Head.position : (Vector3?)null);
            }

            Gone.Clear();
            foreach (var kv in Remotes)
            {
                if (!Seen.Contains(kv.Key))
                {
                    Gone.Add(kv.Key);
                }
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                Remotes[Gone[i]].Rig.Destroy();
                Remotes.Remove(Gone[i]);
                RrAvatarHide.Forget(Gone[i]);
            }
        }

        // ------------------------------------------------------------------ poses

        /// <summary>The local view: VR and first-person play mode = the camera; desktop orbit = the Bridge head anchor.</summary>
        private static void LocalView(out Vector3 eye, out Quaternion look)
        {
            Transform anchor = RrPlayer.HeadAnchor;
            if (RrPlayer.InVr || RrPlayer.InPlayMode || anchor == null)
            {
                eye = RrPlayer.HeadPosition;
                look = RrPlayer.HeadRotation;
                return;
            }

            eye = anchor.position;
            look = anchor.rotation;
        }

        private static Pose? LocalHand(RrHand h)
        {
            if (h == null || !h.Tracked || !RrHand.VrMode || h.IsScreenHand || h.Grip == null)
            {
                return null;
            }

            Pose f = h.RecRoomFrame;
            Quaternion q = h.Grip.rotation;
            return new Pose(h.Grip.position + q * f.position, q * f.rotation);
        }

        /// <summary>A remote VR player's controller anchor (the grip pose) turned into Rec Room's hand frame.</summary>
        private static Pose? RemoteHand(Transform anchor, RrHandSide side)
        {
            if (anchor == null)
            {
                return null;
            }

            Pose f = RrAvatarHand.GripToRecRoomDefault(side);
            Quaternion q = anchor.rotation;
            return new Pose(anchor.position + q * f.position, q * f.rotation);
        }

        private static float Curl(RrHand h)
        {
            if (h == null)
            {
                return 0.2f;
            }

            if (h.Held != null)
            {
                return 1f;
            }

            return RrHand.VrMode && !h.IsScreenHand ? Mathf.Max(0.15f, h.GripValue) : 0.25f;
        }

        /// <summary>Remote fingers: closed while the hand holds something (presence ints 8/9), relaxed otherwise.</summary>
        private static float RemoteCurl(int[] record, bool left)
        {
            if (record == null || !RrPresenceWire.Written(record))
            {
                return 0.25f;
            }

            return RrPresenceWire.HandProp(record, left) >= 0 || RrPresenceWire.HandItem(record, left) > 0 ? 1f : 0.25f;
        }

        private static string LocalNick()
        {
            try
            {
                var local = RrPlayer.Local;
                var props = local != null ? local.Properties : null;
                return props != null ? props.NickName : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Key(RrAvatarOutfit o, bool forceWear = false)
        {
            var sb = new System.Text.StringBuilder(160);
            sb.Append(forceWear || o.wear ? 1 : 0).Append('/').Append(o.skin).Append('/').Append(o.eyes).Append('/').Append(o.mouth);
            for (int i = 0; i < o.items.Length; i++)
            {
                var c = o.items[i];
                sb.Append('/').Append(c.item).Append(':').Append(c.color).Append(':').Append(c.style);
            }

            return sb.ToString();
        }

        public static string Describe(RrAvatarOutfit o)
        {
            if (!o.wear)
            {
                return "Somnium avatar";
            }

            var sb = new System.Text.StringBuilder("Rec Room avatar: skin " + o.skin + ", eyes " + o.eyes + ", mouth " + o.mouth);
            for (int i = 0; i < o.items.Length; i++)
            {
                if (!string.IsNullOrEmpty(o.items[i].item))
                {
                    sb.Append(", ").Append(o.items[i].item);
                    if (o.items[i].color > 0)
                    {
                        sb.Append(" dye ").Append(o.items[i].color);
                    }

                    if (!string.IsNullOrEmpty(o.items[i].style))
                    {
                        sb.Append(" ").Append(o.items[i].style);
                    }
                }
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ cameras

        /// <summary>Re-subscribes every frame so this callback runs after every other (mirrors render from theirs).</summary>
        private static void Resubscribe()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (_object == null)
            {
                _subscribed = false;
                return;
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            _subscribed = true;
        }

        private static void Unsubscribe()
        {
            if (_subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.endCameraRendering -= OnEndCamera;
                _subscribed = false;
            }
        }

        private static readonly Vector3 Far = new Vector3(1e7f, 1e7f, 1e7f);

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            ForCamera(camera.transform.position);
        }

        private static void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            ForCamera(Far);
        }

        private static void ForCamera(Vector3 position)
        {
            try
            {
                if (_local != null)
                {
                    _local.ForCamera(position);
                }

                foreach (var r in Remotes.Values)
                {
                    r.Rig.ForCamera(position);
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Avatar", "camera", "first-person head hiding threw: " + e.Message);
            }
        }

        public static string DescribeState()
        {
            return "avatar: " + (Outfit.wear ? "Rec Room" : "Somnium") + ", " + Remotes.Count + " remote Rec Room avatar(s), " + RrAvatarHide.HiddenRenderers + " Somnium renderer(s) hidden";
        }
    }
}
