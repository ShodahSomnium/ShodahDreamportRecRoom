using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Play-mode harness hooks for <see cref="RrIsolation"/> (scenario <c>isolation_basic.json</c>). The harness has
    /// no Somnium client and so no remote players; this builds a fake one shaped like the client's (measured from
    /// the client prefabs, see RrIsolation): a body prefab root with Body.Root (CharacterController, head sphere,
    /// the layer-15 head trigger, a disabled IK sphere, a disabled debug cube, a feedback AudioSource, a sound the
    /// user muted), the name tag <c>SocialSystem.User.Tag(Clone)</c> (world-space Canvas + CanvasGroup) and a chat
    /// bubble next to it; the avatar as a separate scene root <c>Avatar for Player [nick] [id]</c> (colliders, an
    /// inactive culling holder, 120 small parts for timing); a cloth item root <c>Simulator Item: [..]</c> skinned to
    /// the avatar's chest; and a voice AudioSource. A second fake uses the LOCAL nickname, next to a fake local avatar
    /// root with that name, which must never be touched.
    ///
    /// Presence is flipped with <see cref="RrIsolation.SetTestPresence"/> and the local place with
    /// <see cref="RrIsolation.SetTestHere"/>, so the real decision rule, hysteresis, sweep (scene roots by name), hide,
    /// re-scan and restore paths run. Every check compares a full snapshot of the fake's state (per renderer: enabled
    /// and forceRenderingOff; per collider, canvas: enabled; per group: alpha, blocksRaycasts; per sound: mute and volume)
    /// and logs "IsolationTest: PASS ..." or "IsolationTest: FAIL ..." with the differences.
    ///
    /// What only two real clients can prove: that the Bridge nickname matches the client's avatar root name, that
    /// the voice's NetworkObject reports the speaker as state authority (and that volume 0 silences them), that the
    /// client does not re-show the tag some other way, and how long a sweep takes with 50 players.
    /// </summary>
    public static class RrIsolationTest
    {
        public const int FakeId = 901;
        public const int TwinId = 902;
        public const string FakeNick = "IsoTest Remote";
        public const string LocalNick = "IsoTest Local";
        public static readonly Vector3 At = new Vector3(0f, 100f, 5f);

        private static readonly List<GameObject> Made = new List<GameObject>();
        private static readonly List<string> Events = new List<string>();
        private static Dictionary<Object, string> _before;
        private static Transform _body, _root, _avatar, _item, _localAvatar, _twinBody;
        private static CharacterController _cc;
        private static CanvasGroup _tagGroup, _bubbleGroup;
        private static Canvas _tagCanvas, _bubbleCanvas;
        private static AudioSource _voice, _feedback, _userMuted;
        private static Renderer _lodRenderer, _newRenderer;
        private static int _fails, _passes;

        // ------------------------------------------------------------------ steps

        /// <summary>Builds both fakes, registers them, puts everyone in the Rec Center #1 (together).</summary>
        public static IEnumerator Build()
        {
            Clean();
            _fails = _passes = 0;
            Events.Clear();
            RrIsolation.SetTestLocalNick(LocalNick);
            RrIsolation.SetTestHere(RrActivity.RecCenter, 1, RrStatus.Lobby);
            RrIsolation.SetTestPresence(FakeId, RrActivity.RecCenter, 1, RrStatus.Lobby);
            RrIsolation.SetTestPresence(TwinId, RrActivity.RecCenter, 1, RrStatus.Lobby);
            RrIsolation.OnVisibilityChanged -= OnVisibility;
            RrIsolation.OnVisibilityChanged += OnVisibility;

            MakeFake();
            RrIsolation.RegisterTestRemote(FakeId, FakeNick, _root, _voice);
            RrIsolation.RegisterTestRemote(TwinId, LocalNick, _twinBody.GetChild(0));
            yield return Wait(1f);

            _before = Snapshot();
            Check("together untouched", !RrIsolation.IsHidden(FakeId) && RrIsolation.IsWithMe(FakeId) && Events.Count == 0 && CountForced() == 0,
                "hidden " + RrIsolation.IsHidden(FakeId) + ", with " + RrIsolation.IsWithMe(FakeId) + ", events " + Events.Count + ", forced " + CountForced());
            RrLog.Info("Isolation", "IsolationTest: built, " + _before.Count + " tracked components; " + RrIsolation.Describe());
        }

        /// <summary>The fake goes to Paintball #2, the twin to its Dorm.</summary>
        public static void Apart()
        {
            RrIsolation.SetTestPresence(FakeId, RrActivity.Paintball, 2, RrStatus.Playing);
            RrIsolation.SetTestPresence(TwinId, RrActivity.Dorm, 0, RrStatus.Lobby);
        }

        public static void CheckHidden()
        {
            var now = Snapshot();
            var bad = new List<string>();
            foreach (var kv in _before)
            {
                if (kv.Key == null)
                {
                    continue;
                }

                string expect = ExpectHidden(kv.Key, kv.Value);
                string got;
                now.TryGetValue(kv.Key, out got);
                if (got != expect)
                {
                    bad.Add(Label(kv.Key) + ": " + got + " (expected " + expect + ")");
                }
            }

            bool events = Events.Contains(FakeId + " hidden") && Events.Contains(TwinId + " hidden");
            Check("hide", bad.Count == 0 && RrIsolation.IsHidden(FakeId) && !RrIsolation.IsWithMe(FakeId) && RrIsolation.IsHidden(TwinId) && events,
                bad.Count + " difference(s) " + Join(bad) + "; hidden " + RrIsolation.IsHidden(FakeId) + "/" + RrIsolation.IsHidden(TwinId) + ", events [" + string.Join(", ", Events) + "]");
            bool same = Same(_localAvatar, now);
            Check("local avatar untouched", same, "the root named after the local nickname " + (same ? "is unchanged" : "changed"));
        }

        /// <summary>
        /// Plays the client while the fake is hidden: an LOD switch, a new outfit part, a vehicle exit, a quick chat, an
        /// unmute, and on the voice a block (RemoteVoiceSpeaker.SetBlockValue: mute true) and a new level from the profile
        /// slider (SetVoiceLevel: volume 0.6). The block must survive the restore and the new level must be the one restored.
        /// </summary>
        public static void Meddle()
        {
            _lodRenderer.enabled = false;
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(part.GetComponent<Collider>());
            part.name = "New Outfit Part";
            part.transform.SetParent(_avatar.Find("MainRoot/Hips/Spine/Chest"), false);
            part.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            part.transform.localScale = Vector3.one * 0.25f;
            _newRenderer = part.GetComponent<Renderer>();
            _newRenderer.sharedMaterial = RrLook.Solid(RrLook.Pink);
            _cc.enabled = true;
            _tagGroup.alpha = 0.5f;
            _userMuted.mute = false;
            _bubbleCanvas.enabled = true;
            _voice.mute = true;
            _voice.volume = 0.6f;
            RrLog.Info("Isolation", "IsolationTest: meddled (LOD renderer off, new renderer, CC on, tag alpha 0.5, user unmute, bubble canvas on, voice blocked, voice level 0.6)");
        }

        public static void CheckStillHidden()
        {
            bool ok = _newRenderer != null && _newRenderer.forceRenderingOff && !_cc.enabled && _tagGroup.alpha == 0f && !_tagGroup.blocksRaycasts && _userMuted.mute
                      && !_bubbleCanvas.enabled && !_lodRenderer.enabled && _lodRenderer.forceRenderingOff && _voice.volume == 0f && _voice.mute;
            Check("re-scan", ok, "new renderer forced " + (_newRenderer != null && _newRenderer.forceRenderingOff) + ", CC " + _cc.enabled + ", tag alpha " + _tagGroup.alpha
                                  + ", user-muted source mute " + _userMuted.mute + ", bubble canvas " + _bubbleCanvas.enabled + ", LOD renderer enabled " + _lodRenderer.enabled
                                  + ", voice volume " + _voice.volume.ToString("0.00") + " mute " + _voice.mute);
        }

        /// <summary>Both come back to the Rec Center #1 (the fake as Playing: the status does not matter).</summary>
        public static void Together()
        {
            RrIsolation.SetTestPresence(FakeId, RrActivity.RecCenter, 1, RrStatus.Playing);
            RrIsolation.SetTestPresence(TwinId, RrActivity.RecCenter, 1, RrStatus.Lobby);
        }

        public static void CheckRestored()
        {
            var bad = CompareRestored();
            Check("restore exact", bad.Count == 0 && !RrIsolation.IsHidden(FakeId) && !RrIsolation.IsHidden(TwinId) && RrIsolation.IsWithMe(FakeId)
                                   && Events.Contains(FakeId + " shown") && Events.Contains(TwinId + " shown"),
                bad.Count + " difference(s) " + Join(bad) + "; hidden " + RrIsolation.IsHidden(FakeId) + "/" + RrIsolation.IsHidden(TwinId) + ", events ["
                + string.Join(", ", Events) + "]");
        }

        /// <summary>
        /// 48 more fakes (10-part bodies, 20-part avatars) hidden by one presence change, as a trip hides a full world:
        /// all must be hidden within 2 s with the isolation's own work per frame kept small, then all restored exactly.
        /// </summary>
        public static IEnumerator Stress()
        {
            const int count = 48;
            var roots = new List<GameObject>();
            for (int k = 0; k < count; k++)
            {
                int id = 1000 + k;
                var at = At + new Vector3(-6f + (k % 8) * 1.7f, 4f + (k / 8) * 2.2f, 6f);
                var body = Root("Player.XR.Body.Simulator (stress " + k + ")", at);
                var root = Child(body.transform, "Root", Vector3.zero);
                root.AddComponent<CapsuleCollider>();
                for (int i = 0; i < 10; i++)
                {
                    var p = Primitive(PrimitiveType.Cube, root.transform, "Part " + i, new Vector3(0f, 0.15f * i, 0f), 0.1f, RrLook.Grey);
                    Object.Destroy(p.GetComponent<Collider>());
                }

                var avatar = Root(RrIsolation.AvatarPrefix + "IsoStress " + k + "] [" + id + "]", at);
                for (int i = 0; i < 20; i++)
                {
                    var p = Primitive(PrimitiveType.Cube, avatar.transform, "Mesh " + i, new Vector3(0.2f, 0.08f * i, 0f), 0.08f, RrLook.Purple);
                    Object.Destroy(p.GetComponent<Collider>());
                }

                roots.Add(body);
                roots.Add(avatar);
                RrIsolation.SetTestPresence(id, RrActivity.RecCenter, 1, RrStatus.Lobby);
                RrIsolation.RegisterTestRemote(id, "IsoStress " + k, root.transform);
            }

            yield return Wait(0.8f);
            var before = new Dictionary<Object, string>();
            SnapshotInto(roots, before);
            RrIsolation.ResetStats();
            for (int k = 0; k < count; k++)
            {
                RrIsolation.SetTestPresence(1000 + k, RrActivity.Paintball, 1, RrStatus.Playing);
            }

            float start = Time.unscaledTime;
            int frames = 0, workFrames = 0;
            float worst = 0f;
            while (Time.unscaledTime - start < 3f)
            {
                yield return null;
                frames++;
                worst = Mathf.Max(worst, RrIsolation.LastTickMs);
                if (RrIsolation.LastTickMs > 0.3f)
                {
                    workFrames++;
                }

                int hidden = 0;
                for (int k = 0; k < count; k++)
                {
                    if (RrIsolation.IsHidden(1000 + k))
                    {
                        hidden++;
                    }
                }

                if (hidden == count)
                {
                    break;
                }
            }

            float took = Time.unscaledTime - start;
            int forced = 0, total = 0;
            foreach (var go in roots)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    total++;
                    forced += r.forceRenderingOff ? 1 : 0;
                }
            }

            float warmSweep = RrIsolation.MeasureSweep();

            // The voice pass only a real session runs (FindObjectsByType<NetworkObject>, IsValid, StateAuthority): here
            // it lists the harness scene's NetworkObjects, none valid (no runner), so it proves the pass runs and costs
            // this much for the listing; the per-player matching needs two clients.
            float voicePass = RrIsolation.MeasureVoicePass();
            RrLog.Info("Isolation", "IsolationTest: voice pass " + voicePass.ToString("0.00") + " ms; " + RrIsolation.Describe());
            Check("stress hide", took < 2f && forced == total && worst < 10f, count + " players, " + total + " renderers: all hidden after " + took.ToString("0.00") + " s over " + frames
                                                               + " frames (" + workFrames + " with > 0.3 ms of isolation work), worst isolation frame " + worst.ToString("0.00")
                                                               + " ms; " + RrIsolation.Describe() + "; warm sweep " + warmSweep.ToString("0.00") + " ms");
            for (int k = 0; k < count; k++)
            {
                RrIsolation.SetTestPresence(1000 + k, RrActivity.RecCenter, 1, RrStatus.Lobby);
            }

            start = Time.unscaledTime;
            while (Time.unscaledTime - start < 3f && RrIsolation.HiddenCount > 0)
            {
                yield return null;
            }

            took = Time.unscaledTime - start;
            var after = new Dictionary<Object, string>();
            SnapshotInto(roots, after);
            int diffs = 0;
            foreach (var kv in before)
            {
                string got;
                after.TryGetValue(kv.Key, out got);
                if (got != kv.Value)
                {
                    diffs++;
                }
            }

            Check("stress restore", diffs == 0 && RrIsolation.HiddenCount == 0, "all shown after " + took.ToString("0.00") + " s, " + diffs + " difference(s) in "
                                                                                 + before.Count + " components");
            for (int k = 0; k < count; k++)
            {
                RrIsolation.UnregisterTestRemote(1000 + k);
                RrIsolation.ClearTestPresence(1000 + k);
            }

            foreach (var go in roots)
            {
                Made.Remove(go);
                Object.Destroy(go);
            }
        }

        /// <summary>The decision rule on ids without objects, then hysteresis and the dorm rule on the fake.</summary>
        public static IEnumerator Rules()
        {
            const int probe = 950;
            Rule(RrActivity.Dorm, 0, RrStatus.Lobby, RrActivity.Dorm, 0, RrStatus.Lobby, false, "dorm is private");
            Rule(RrActivity.None, 0, RrStatus.None, RrActivity.None, 0, RrStatus.None, true, "arrival room is shared");
            Rule(RrActivity.RecCenter, 1, RrStatus.Lobby, RrActivity.RecCenter, 1, RrStatus.Loading, false, "they are loading");
            Rule(RrActivity.RecCenter, 1, RrStatus.Loading, RrActivity.RecCenter, 1, RrStatus.Lobby, false, "you are loading");
            Rule(RrActivity.RecCenter, 1, RrStatus.Lobby, RrActivity.RecCenter, 2, RrStatus.Lobby, false, "other instance");
            Rule(RrActivity.Paintball, 3, RrStatus.Playing, RrActivity.Paintball, 3, RrStatus.Down, true, "same instance");
            RrIsolation.SetTestPresenceUnknown(probe);
            Check("rule unknown presence", !RrIsolation.IsWithMe(probe), "with " + RrIsolation.IsWithMe(probe));
            RrIsolation.ClearTestPresence(probe);

            // Hysteresis: apart for 0.25 s (< HideDelay) must not hide.
            RrIsolation.SetTestHere(RrActivity.RecCenter, 1, RrStatus.Lobby);
            RrIsolation.SetTestPresence(FakeId, RrActivity.RecCenter, 1, RrStatus.Lobby);
            yield return Wait(0.6f);
            int before = Events.Count;
            bool everHidden = false;
            RrIsolation.SetTestPresence(FakeId, RrActivity.Paintball, 2, RrStatus.Loading);
            float until = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < until)
            {
                everHidden |= RrIsolation.IsHidden(FakeId);
                yield return null;
            }

            RrIsolation.SetTestPresence(FakeId, RrActivity.RecCenter, 1, RrStatus.Lobby);
            until = Time.unscaledTime + 1f;
            while (Time.unscaledTime < until)
            {
                everHidden |= RrIsolation.IsHidden(FakeId);
                yield return null;
            }

            Check("hysteresis (0.25 s apart)", !everHidden && Events.Count == before, "ever hidden " + everHidden + ", events " + (Events.Count - before));

            // Both in "the same" dorm: still apart.
            RrIsolation.SetTestHere(RrActivity.Dorm, 0, RrStatus.Lobby);
            RrIsolation.SetTestPresence(FakeId, RrActivity.Dorm, 0, RrStatus.Lobby);
            yield return Wait(1f);
            Check("dorm hides", RrIsolation.IsHidden(FakeId), "hidden " + RrIsolation.IsHidden(FakeId));
            RrIsolation.SetTestHere(RrActivity.RecCenter, 1, RrStatus.Lobby);
            RrIsolation.SetTestPresence(FakeId, RrActivity.RecCenter, 1, RrStatus.Lobby);
            yield return Wait(1f);
            var bad = CompareRestored();
            Check("dorm restore exact", !RrIsolation.IsHidden(FakeId) && bad.Count == 0, bad.Count + " difference(s) " + Join(bad));
        }

        /// <summary>Hidden, then the world unloads (RrIsolation.Shutdown, as RrRemotePlayers.Reset calls it): all back.</summary>
        public static IEnumerator ShutdownRestores()
        {
            RrIsolation.SetTestPresence(FakeId, RrActivity.Paintball, 2, RrStatus.Playing);
            yield return Wait(1f);
            bool hidden = RrIsolation.IsHidden(FakeId);
            RrIsolation.Shutdown();
            var bad = CompareRestored();
            Check("shutdown restores", hidden && bad.Count == 0 && RrIsolation.HiddenCount == 0, "was hidden " + hidden + ", " + bad.Count + " difference(s) " + Join(bad));
        }

        /// <summary>Summary line, then the fakes go and the overrides are cleared.</summary>
        public static void Report()
        {
            RrLog.Info("Isolation", "IsolationTest: " + (_fails == 0 ? "PASS all" : "FAIL " + _fails + " check(s)") + " (" + _passes + " passed); " + RrIsolation.Describe());
            RrIsolation.OnVisibilityChanged -= OnVisibility;
            RrIsolation.UnregisterTestRemote(FakeId);
            RrIsolation.UnregisterTestRemote(TwinId);
            RrIsolation.ClearTestPresence(FakeId);
            RrIsolation.ClearTestPresence(TwinId);
            RrIsolation.ClearTestHere();
            RrIsolation.SetTestLocalNick(null);
            Clean();
        }

        // ------------------------------------------------------------------ the fake

        private static void MakeFake()
        {
            // Body prefab root with Body.Root under it (Player.XR.Body.Simulator).
            _body = Root("Player.XR.Body.Simulator (IsoTest)", At).transform;
            _root = Child(_body, "Root", Vector3.zero).transform;
            _cc = _root.gameObject.AddComponent<CharacterController>();
            _cc.height = 1.35f;
            _cc.radius = 0.15f;
            _cc.center = new Vector3(0f, 0.675f, 0f);
            var head = Primitive(PrimitiveType.Sphere, _root, "Head", new Vector3(0f, 1.6f, 0f), 0.25f, RrLook.Cream);
            var trigger = Child(head.transform, "XR.Simulator.PlayerTrigger", Vector3.zero);
            trigger.layer = 15;
            var sphere = trigger.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.18f;
            var ik = Child(_root, "LeftHandVRIKPoint", new Vector3(-0.3f, 1.1f, 0.1f));
            ik.layer = 30;
            ik.AddComponent<SphereCollider>().enabled = false;
            var debugCube = Primitive(PrimitiveType.Cube, _root, "Cube", new Vector3(0.3f, 1.1f, 0.1f), 0.1f, RrLook.Red);
            debugCube.GetComponent<Renderer>().enabled = false;
            Object.Destroy(debugCube.GetComponent<Collider>());
            _feedback = Child(_root, "AvatarFeedbackVFX", Vector3.up).AddComponent<AudioSource>();
            _feedback.playOnAwake = false;
            _userMuted = Child(_root, "Emote Sound", Vector3.up).AddComponent<AudioSource>();
            _userMuted.playOnAwake = false;
            _userMuted.mute = true;

            // Name tag and chat bubble: siblings of Body.Root.
            var tag = Child(_body, "SocialSystem.User.Tag(Clone)", new Vector3(0f, 2.15f, 0f));
            _tagCanvas = UiCanvas(Child(tag.transform, "UICorner", Vector3.zero).transform, "Canvas", new Vector2(120f, 30f), RrLook.TeamBlue, out _tagGroup);
            var bubble = Child(_body, "QuickChat.Bubble(Clone)", new Vector3(0.55f, 2.45f, 0f));
            _bubbleCanvas = UiCanvas(bubble.transform, "Root", new Vector2(60f, 30f), RrLook.White, out _bubbleGroup);

            // Avatar: its own scene root, named as AvatarManager.Initialize names it.
            _avatar = Root(RrIsolation.AvatarPrefix + FakeNick + "] [" + FakeId + "]", At).transform;
            var main = Child(_avatar, "MainRoot", Vector3.zero).transform;
            var hips = Child(main, "Hips", new Vector3(0f, 0.95f, 0f)).transform;
            var spine = Child(hips, "Spine", new Vector3(0f, 0.2f, 0f)).transform;
            var chest = Child(spine, "Chest", new Vector3(0f, 0.25f, 0f)).transform;
            Primitive(PrimitiveType.Capsule, hips, "Body", new Vector3(0f, 0.1f, 0f), 0.55f, RrLook.Blue);
            _lodRenderer = Primitive(PrimitiveType.Cube, chest, "LOD1 Arms", new Vector3(0f, 0.05f, 0f), 0.3f, RrLook.Teal).GetComponent<Renderer>();
            _lodRenderer.transform.localScale = new Vector3(1.1f, 0.12f, 0.12f);
            var culling = Child(_avatar, "CullingRoot", Vector3.zero);
            Primitive(PrimitiveType.Cube, culling.transform, "Impostor", new Vector3(0f, 1f, 0f), 0.5f, RrLook.Grey);
            culling.SetActive(false);
            var parts = Child(main, "Parts", Vector3.zero).transform;
            for (int i = 0; i < 120; i++)
            {
                float a = i * 0.21f;
                var p = Primitive(PrimitiveType.Cube, parts, "Part " + i, new Vector3(Mathf.Cos(a) * 0.35f, 0.4f + i * 0.01f, Mathf.Sin(a) * 0.35f), 0.04f, RrLook.Purple);
                Object.Destroy(p.GetComponent<Collider>());
            }

            // Cloth item: a scene root skinned to the avatar's chest.
            _item = Root(RrIsolation.ItemPrefix + " [Cloth] [iso]", At + new Vector3(0f, 1.45f, -0.2f)).transform;
            var smr = _item.gameObject.AddComponent<SkinnedMeshRenderer>();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            smr.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            smr.rootBone = chest;
            smr.sharedMaterial = RrLook.Solid(RrLook.Yellow);
            _item.localScale = new Vector3(0.45f, 0.3f, 0.1f);

            // The voice (the network part's Voice.Speaker).
            _voice = Root("Voice.Speaker (IsoTest)", At + Vector3.up * 1.6f).AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.volume = 0.8f;

            // The twin: a remote with the LOCAL nickname, and a local avatar root it must not take.
            _twinBody = Root("Player.NonXR.Body.Simulator (IsoTest twin)", At + new Vector3(-1.6f, 0f, 0f)).transform;
            var twinRoot = Child(_twinBody, "NonXR.Body", Vector3.zero).transform;
            Primitive(PrimitiveType.Cube, twinRoot, "Twin", new Vector3(0f, 0.6f, 0f), 0.5f, RrLook.Orange);
            _localAvatar = Root(RrIsolation.AvatarPrefix + LocalNick + "] [1]", At + new Vector3(1.6f, 0f, 0f)).transform;
            Primitive(PrimitiveType.Cube, _localAvatar, "Local Avatar", new Vector3(0f, 0.6f, 0f), 0.5f, RrLook.Green);
        }

        private static GameObject Root(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            Made.Add(go);
            return go;
        }

        private static GameObject Child(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go;
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 local, float size, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = RrLook.Solid(c);
            return go;
        }

        private static Canvas UiCanvas(Transform parent, string name, Vector2 size, Color c, out CanvasGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.01f;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            group = go.AddComponent<CanvasGroup>();
            var img = new GameObject("Image", typeof(RectTransform));
            img.transform.SetParent(go.transform, false);
            var irt = (RectTransform)img.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            img.AddComponent<Image>().color = c;
            return canvas;
        }

        private static void Clean()
        {
            for (int i = 0; i < Made.Count; i++)
            {
                if (Made[i] != null)
                {
                    Object.Destroy(Made[i]);
                }
            }

            Made.Clear();
        }

        // ------------------------------------------------------------------ checks

        private static void OnVisibility(int id, bool visible)
        {
            Events.Add(id + (visible ? " shown" : " hidden"));
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        private static void Rule(RrActivity myA, int myI, RrStatus myS, RrActivity a, int n, RrStatus s, bool expect, string what)
        {
            const int probe = 950;
            RrIsolation.SetTestHere(myA, myI, myS);
            RrIsolation.SetTestPresence(probe, a, n, s);
            bool with = RrIsolation.IsWithMe(probe);
            RrIsolation.ClearTestPresence(probe);
            Check("rule " + what, with == expect, "you " + myA + " #" + myI + " " + myS + ", them " + a + " #" + n + " " + s + ": with " + with + " (expected " + expect + ")");
        }

        private static void Check(string what, bool ok, string detail)
        {
            if (ok)
            {
                _passes++;
                RrLog.Info("Isolation", "IsolationTest: PASS " + what + " (" + detail + ")");
            }
            else
            {
                _fails++;
                RrLog.Warn("Isolation", "IsolationTest: FAIL " + what + ": " + detail);
            }
        }

        /// <summary>Every renderer/collider/canvas/group/sound under the fake's roots and the local avatar root.</summary>
        private static Dictionary<Object, string> Snapshot()
        {
            var d = new Dictionary<Object, string>();
            SnapshotInto(Made, d);
            return d;
        }

        private static void SnapshotInto(List<GameObject> roots, Dictionary<Object, string> d)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                foreach (var r in roots[i].GetComponentsInChildren<Renderer>(true))
                {
                    d[r] = "renderer enabled " + r.enabled + " forced " + r.forceRenderingOff;
                }

                foreach (var c in roots[i].GetComponentsInChildren<Collider>(true))
                {
                    d[c] = "collider enabled " + c.enabled;
                }

                foreach (var c in roots[i].GetComponentsInChildren<Canvas>(true))
                {
                    d[c] = "canvas enabled " + c.enabled;
                }

                foreach (var g in roots[i].GetComponentsInChildren<CanvasGroup>(true))
                {
                    d[g] = "group alpha " + g.alpha.ToString("0.00") + " blocks " + g.blocksRaycasts;
                }

                foreach (var a in roots[i].GetComponentsInChildren<AudioSource>(true))
                {
                    d[a] = "sound mute " + a.mute + " volume " + a.volume.ToString("0.00");
                }
            }
        }

        /// <summary>What a component of the fake must read while hidden, from what it read before.</summary>
        private static string ExpectHidden(Object o, string before)
        {
            if (IsUnder(o, _localAvatar))
            {
                return before;
            }

            if (o is Renderer r)
            {
                return "renderer enabled " + r.enabled + " forced True";
            }

            if (o is Collider)
            {
                return "collider enabled False";
            }

            if (o == _bubbleCanvas)
            {
                return "canvas enabled False";
            }

            if (o == _tagGroup)
            {
                return "group alpha 0.00 blocks False";
            }

            if (o == _voice)
            {
                // The voice: volume 0, its mute (the client's mute/block) untouched.
                return before.Substring(0, before.IndexOf(" volume ", System.StringComparison.Ordinal)) + " volume 0.00";
            }

            if (o is AudioSource)
            {
                // Other sounds: muted, volume untouched.
                return "sound mute True" + before.Substring(before.IndexOf(" volume ", System.StringComparison.Ordinal));
            }

            return before;
        }

        /// <summary>Differences from the snapshot before hiding, allowing exactly what the "client" changed meanwhile.</summary>
        private static List<string> CompareRestored()
        {
            var now = Snapshot();
            var bad = new List<string>();
            foreach (var kv in now)
            {
                string expect;
                if (kv.Key == _lodRenderer)
                {
                    expect = "renderer enabled False forced False";
                }
                else if (kv.Key == _newRenderer)
                {
                    expect = "renderer enabled True forced False";
                }
                else if (kv.Key == _tagGroup)
                {
                    expect = "group alpha 0.50 blocks True";
                }
                else if (kv.Key == _userMuted)
                {
                    expect = "sound mute False volume 1.00";
                }
                else if (kv.Key == _voice)
                {
                    // Blocked while hidden (must stay blocked), set to 0.6 while hidden (must come back at 0.6).
                    expect = "sound mute True volume 0.60";
                }
                else if (!_before.TryGetValue(kv.Key, out expect))
                {
                    expect = "(not in the first snapshot)";
                }

                if (kv.Value != expect)
                {
                    bad.Add(Label(kv.Key) + ": " + kv.Value + " (expected " + expect + ")");
                }
            }

            return bad;
        }

        private static bool Same(Transform root, Dictionary<Object, string> now)
        {
            foreach (var kv in _before)
            {
                if (kv.Key != null && IsUnder(kv.Key, root))
                {
                    string got;
                    now.TryGetValue(kv.Key, out got);
                    if (got != kv.Value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int CountForced()
        {
            int n = 0;
            for (int i = 0; i < Made.Count; i++)
            {
                if (Made[i] == null)
                {
                    continue;
                }

                foreach (var r in Made[i].GetComponentsInChildren<Renderer>(true))
                {
                    if (r.forceRenderingOff)
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        private static bool IsUnder(Object o, Transform root)
        {
            var c = o as Component;
            return c != null && root != null && c.transform.IsChildOf(root);
        }

        private static string Label(Object o)
        {
            var c = o as Component;
            return c != null ? c.gameObject.name + "/" + c.GetType().Name : "?";
        }

        private static string Join(List<string> items)
        {
            if (items.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder("[");
            for (int i = 0; i < items.Count && i < 8; i++)
            {
                sb.Append(i > 0 ? "; " : "").Append(items[i]);
            }

            if (items.Count > 8)
            {
                sb.Append("; ...");
            }

            return sb.Append("]").ToString();
        }
    }
}
