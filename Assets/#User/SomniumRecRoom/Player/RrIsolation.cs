using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Hides, on this client only, the other players who are not in the same place as the local player, and silences
    /// them. Every room is built at the world origin (one room at a time, the "place" task), so a player in
    /// Paintball stands, as far as Unity knows, in the middle of your Dorm. Rec Room never showed you people from
    /// another room; this makes the Somnium world behave the same way.
    ///
    /// <b>Who is with you</b> (<see cref="IsWithMe"/>): the same activity and instance number in presence
    /// (<see cref="RrNet.Presence"/>, the same fields the watch compares). The Dorm (and any unnetworked room) is
    /// private; the arrival room (activity None) is shared by everyone arriving; an unknown presence, a player whose
    /// status is Loading, and everyone while the local player is Loading count as apart. A change must hold for
    /// <see cref="HideDelay"/> (apart) or <see cref="ShowDelay"/> (together) before anything is switched, so the
    /// presence writes of a trip (Loading, binding, arrived) do not make people flicker. The first decision about a
    /// newly seen player is applied at once, so a stranger is never drawn for half a second at the origin.
    ///
    /// <b>What a remote player is made of</b> (measured from the client source, CLIENT = the Somnium client's
    /// Assets/_SomniumSpace; DevNotes/phase1/isolate.md has the full list). Four places, only one under Body.Root:
    /// <list type="number">
    /// <item>The body prefab <c>Player.XR.Body.Simulator</c> / <c>Player.NonXR.Body.Simulator</c>, which is
    /// <c>Body.Root.parent</c>: the CharacterController on Body.Root (layer 2, enabled, only a collider: the remote
    /// pose is written to transform.position, PlayerTransformNetworkDriver.cs:69,203), the head trigger (layer 15),
    /// the feedback VFX (with an AudioSource), and - as siblings of Body.Root - the name tag
    /// <c>SocialSystem.User.Tag</c> (InfoFeature.cs:67-72, 86) and the quick chat bubble (QuickChatFeature.cs:28-34).</item>
    /// <item>The avatar model <c>Avatar for Player [nick] [longId]</c> (AvatarManager.cs:105), instantiated with no
    /// parent for remote players (AvatarSpawner.cs:30-33): a scene root, with its MAIN/DUMMY/CULLING holders and the
    /// wearables parented to its bones. Quake's Q3Remote walked Body.Root only, which no longer reaches it.</item>
    /// <item>Cloth wearables and held items: scene roots named <c>Simulator Item: [..]</c> (InventoryItemController.cs:73),
    /// skinned to the avatar's bones.</item>
    /// <item>The voice: <c>Voice.Speaker</c> (RemoteVoiceSpeaker + AudioSource) in the NETWORK part
    /// <c>Player.XR/NonXR.Part.Network</c>, a Fusion NetworkObject whose state authority is the player.</item>
    /// </list>
    ///
    /// <b>How they are hidden</b> - always the smallest local switch, always restored exactly:
    /// <list type="bullet">
    /// <item>Renderers: <c>Renderer.forceRenderingOff</c>, not <c>enabled</c>. The client itself writes
    /// <c>enabled</c> (AvatarRootsLODHandler.cs:100 when the avatar LOD setting changes, WearablesRoot.cs:36,
    /// ClothBone.cs:120/128) but never <c>forceRenderingOff</c> (only the Crest ocean does, anywhere in the client),
    /// so the client's own state is never overwritten and nothing it switched meanwhile is undone on restore.</item>
    /// <item>The name tag: its CanvasGroups get alpha 0 and blocksRaycasts false. The client sets the tag Canvas'
    /// <c>enabled</c> on every frustum enter/exit (InfoFeature.cs:272-286, PlayerTagView.cs:243-255), which would
    /// flash the tag each time you look toward the origin, but writes the group alpha only for a quick chat
    /// (PlayerTagView.cs:257-261); an alpha the client writes while hidden becomes the value restored.</item>
    /// <item>Other canvases next to Body.Root (the chat bubble, whose own fade is alpha only,
    /// QuickChatBubbleView.cs:11): <c>Canvas.enabled</c> false.</item>
    /// <item>Colliders (the remote CharacterController, which would otherwise block the local player's capsule and
    /// VR hand boxes - layer 2 hits 2 and 7 - at the shared origin, and the head trigger): <c>enabled</c> false.</item>
    /// <item>The voice (every AudioSource in the player's own NetworkObjects): <c>AudioSource.volume</c> 0, never
    /// <c>mute</c>. <c>mute</c> on the voice is the client's mute and block state: RemoteVoiceSpeaker.SetMuted and
    /// SetBlockValue write it (RemoteVoiceSpeaker.cs:65-79, 96-101), asynchronously after a speaker connects
    /// (RemoteVoiceFeature.cs:66-86, 103-116) and whenever you mute or block someone from the social menu, and the
    /// client reads it back as VoiceSpeaker.IsMuted (VoiceSpeaker.cs:47-55) for its UI. Writing mute ourselves would
    /// hide a block made while the player is hidden (true over true) and undo it on restore. The volume is written
    /// only by VoiceSpeaker.SetVoiceLevel (VoiceSpeaker.cs:143-149: the profile volume slider and the Bridge's
    /// SetVoiceLevel, never per frame), so a volume above 0 seen on a re-scan is the client's new level: it becomes
    /// the value restored and the voice goes back to 0. The voice culling decides "speaking" from Photon's
    /// Speaker.IsPlaying, not from the output (VoiceCullingManager.cs:122), so a silent voice is not culled
    /// differently; the boost gain (RemoteVoiceSpeaker.cs:103-117) multiplies silence. Exact 0 rather than a tiny
    /// marker value, so Unity can virtualise the channel (voices play at priority 16, VoiceSpeaker.cs:26, and would
    /// otherwise keep real channels for people you cannot hear). The one loss: a level set to exactly 0 from the
    /// profile slider while the player is hidden cannot be told from ours, and the earlier level comes back.</item>
    /// <item>Other sounds (the feedback VFX source, items): <c>AudioSource.mute</c> true. Their volume is written on
    /// every clip (VFXAudioClipPlayer.cs:29), their mute only by an admin item mute (LoadedModel.cs:259-271). Only
    /// sources found unmuted are muted and later unmuted, so a sound already muted stays muted.</item>
    /// </list>
    /// Nothing is added, destroyed, re-parented or re-materialled, and the local player's objects are never touched
    /// (a root that contains the local body, camera or our own world is skipped; an avatar is matched by the remote's
    /// raw Bridge nickname only, never the local one and never when two players share it).
    ///
    /// <b>Cost</b>: decisions for all players every <see cref="DecideInterval"/> (a presence read each); at most
    /// <see cref="MaxApplyPerFrame"/> hides/shows and <see cref="MaxMaintainPerFrame"/> re-scans per frame, each
    /// hidden player re-scanned every <see cref="MaintainInterval"/> for renderers/colliders/sounds the client
    /// re-enabled or created (avatar reloads, LOD switches, outfit changes); the scene-root and NetworkObject search
    /// (a "sweep") at most once per frame, only while someone is hidden and only when needed.
    ///
    /// <b>Known limit (not fixable from the world)</b>: the client's AvatarsCullingSystem gives full avatars to the
    /// nearest 8 players within 15 m, by distance alone (AvatarsCullingSystem.cs:24-25, 144-185). Hidden players in
    /// other rooms stand near the shared origin too, so they still take those slots: with more than 8 players about,
    /// a friend 6 m away in your room can get the culled/dummy avatar while hidden strangers 2-5 m away keep full ones.
    /// Moving other players or changing the culling would touch the client, which is not allowed.
    ///
    /// Started lazily from <see cref="RrRemotePlayers"/>' per-frame tick (LateUpdate at order 10000, after the
    /// client), shut down - restoring everything - by <see cref="RrRemotePlayers"/>' reset on world unload.
    /// In the editor and the harness there are no remote players: <see cref="RrIsolationTest"/> registers a fake one
    /// through <see cref="RegisterTestRemote"/> and drives presence with <see cref="SetTestPresence"/>.
    /// </summary>
    public static class RrIsolation
    {
        /// <summary>Apart this long before a player is hidden (a trip writes Loading, binding, arrived within ~1 s).</summary>
        public const float HideDelay = 0.5f;

        /// <summary>Together this long before a player is shown again.</summary>
        public const float ShowDelay = 0.35f;

        public const float DecideInterval = 0.2f;
        public const float MaintainInterval = 0.5f;

        /// <summary>A hide re-uses a sweep this recent; an older one is redone first.</summary>
        public const float SweepFresh = 0.5f;

        /// <summary>While a hidden player's avatar or voice is not found yet (the first few tries).</summary>
        public const float SweepRetry = 2f;

        /// <summary>While anyone is hidden: new cloth items, replaced avatar or network objects.</summary>
        public const float SweepPeriod = 10f;

        public const int SweepRetries = 5;
        public const int MaxApplyPerFrame = 2;
        public const int MaxMaintainPerFrame = 2;

        /// <summary>
        /// Work per frame beyond the first item stops after this (ms). Measured in the editor harness: a cold first sweep
        /// 3.1-4.0 ms and a cold hide of a 126-renderer fake 2.8-4.0 ms, warm hides 0.04-1.2 ms, warm sweeps 0.24-0.33 ms;
        /// so a sweep and the hide that needed it never share a frame. 48 fakes (1440 renderers) hidden by one presence
        /// change: all hidden 0.65 s later, over 25 frames of work, worst frame 0.50 ms; restored in 0.44 s.
        /// </summary>
        public const float FrameBudgetMs = 1.5f;

        /// <summary>AvatarManager.Initialize: "Avatar for Player [" + nick + "] [" + longId + "]".</summary>
        public const string AvatarPrefix = "Avatar for Player [";

        /// <summary>InventoryItemController: "Simulator Item: [type] [bundleId]" for other players' items.</summary>
        public const string ItemPrefix = "Simulator Item:";

        /// <summary>The name tag prefab (SocialSystem.User.Tag.prefab), and its component (excluded from obfuscation).</summary>
        public const string TagPrefix = "SocialSystem.User.Tag";
        public const string TagComponent = "PlayerTagView";

        private struct Place
        {
            public bool Known;
            public RrActivity Activity;
            public int Instance;
            public RrStatus Status;
        }

        private sealed class Entry
        {
            public int Id = -1;
            public string Nick = "?";
            public string RawNick;
            public RrRemotePlayers.Remote Remote;
            public bool Test;
            public int Seen;

            public Transform Root;
            public Transform Body;
            public Transform Avatar;
            public readonly List<Transform> Extras = new List<Transform>();
            public readonly List<AudioSource> Voices = new List<AudioSource>();
            public readonly List<AudioSource> TestVoices = new List<AudioSource>();

            public bool Decided;
            public bool Want = true;
            public float WantSince;
            public bool With = true;
            public bool Visible = true;
            public bool Queued;
            public Place Them;
            public Place Me;
            public float NextMaintain;
            public int AvatarTries;
            public int VoiceTries;
            public bool AvatarUnmatchable;
            public bool LoggedAvatar, LoggedVoice, LoggedNoTag;

            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<Collider> Colliders = new List<Collider>();
            public readonly List<Canvas> Canvases = new List<Canvas>();
            public readonly List<CanvasGroup> Groups = new List<CanvasGroup>();
            public readonly List<float> GroupAlpha = new List<float>();
            public readonly List<bool> GroupBlocks = new List<bool>();
            public readonly List<AudioSource> Muted = new List<AudioSource>();

            /// <summary>Voices set to volume 0, and the volume each goes back to (the client's newest level).</summary>
            public readonly List<AudioSource> Quiet = new List<AudioSource>();
            public readonly List<float> QuietVolume = new List<float>();
            public bool LoggedLevel;
            public readonly HashSet<Object> Touched = new HashSet<Object>();

            public bool NeedsSweep
            {
                get { return !With || !Visible; }
            }
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly Dictionary<RrRemotePlayers.Remote, Entry> ByRemote = new Dictionary<RrRemotePlayers.Remote, Entry>();
        private static readonly List<Entry> Queue = new List<Entry>();
        private static readonly Dictionary<int, Place> TestPresence = new Dictionary<int, Place>();

        private static readonly List<Renderer> ScratchRenderers = new List<Renderer>();
        private static readonly List<Collider> ScratchColliders = new List<Collider>();
        private static readonly List<AudioSource> ScratchAudio = new List<AudioSource>();
        private static readonly List<Canvas> ScratchCanvases = new List<Canvas>();
        private static readonly List<CanvasGroup> ScratchGroups = new List<CanvasGroup>();
        private static readonly List<SkinnedMeshRenderer> ScratchSkinned = new List<SkinnedMeshRenderer>();
        private static readonly List<GameObject> ScratchRoots = new List<GameObject>();
        private static readonly List<Scene> ScratchScenes = new List<Scene>();
        private static readonly List<KeyValuePair<string, Transform>> AvatarRoots = new List<KeyValuePair<string, Transform>>();
        private static readonly List<Transform> ItemRoots = new List<Transform>();

        private static bool _started;
        private static bool _enabled = true;
        private static bool _hasTestHere;
        private static Place _testHere;
        private static string _testLocalNick;
        private static int _stamp;
        private static int _cursor;
        private static int _errors;
        private static int _changeLogs;
        private static int _sweeps;
        private static int _sweepFrame = -1;
        private static float _lastSweep = -100f;
        private static float _sweepMs, _sweepMaxMs, _hideMaxMs, _tickMs, _tickMaxMs;
        private static float _voicePassMs = -1f;
        private static int _voicePassListed, _voicePassValid, _voicePassOwned;
        private static readonly System.Diagnostics.Stopwatch FrameWatch = new System.Diagnostics.Stopwatch();
        private static float _nextDecide;
        private static float _offlineSince = -1f;
        private static Transform _localRoot, _localCamera, _worldRoot;

        /// <summary>A player's applied visibility changed (id, visible): after their objects were hidden or restored.</summary>
        public static event Action<int, bool> OnVisibilityChanged;

        /// <summary>
        /// Master switch (default on). Off shows everyone again at once (the decisions keep running, so
        /// <see cref="IsWithMe"/> still answers); on hides whoever is apart at the next decision pass.
        /// </summary>
        public static bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                RrLog.Info("Isolation", value ? "on: players who are not with you are hidden" : "off: everyone is shown");
                _nextDecide = 0f;
            }
        }

        /// <summary>
        /// Whether a player is in the same place as the local player (the local id: true). For a tracked remote this
        /// is the debounced decision the hiding follows; for any other id (no Bridge player, e.g. offline) the rule is
        /// evaluated directly from presence.
        /// </summary>
        public static bool IsWithMe(int playerId)
        {
            if (!TestPresence.ContainsKey(playerId) && playerId == SafeLocalId())
            {
                return true;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Id == playerId && Entries[i].Decided)
                {
                    return Entries[i].With;
                }
            }

            Place them;
            return Together(MyPlace(), playerId, out them);
        }

        /// <summary>True while this client is hiding the player's objects.</summary>
        public static bool IsHidden(int playerId)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Id == playerId)
                {
                    return !Entries[i].Visible;
                }
            }

            return false;
        }

        public static int HiddenCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (!Entries[i].Visible)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public static string Describe()
        {
            return "isolation " + (_enabled ? "on" : "off") + ": " + Entries.Count + " tracked, " + HiddenCount + " hidden, " + _sweeps + " sweeps (last "
                   + _sweepMs.ToString("0.00") + " ms, max " + _sweepMaxMs.ToString("0.00") + " ms), slowest hide " + _hideMaxMs.ToString("0.00") + " ms, slowest frame "
                   + _tickMaxMs.ToString("0.00") + " ms"
                   + (_voicePassMs >= 0f
                       ? ", last voice pass " + _voicePassListed + " NetworkObject(s), " + _voicePassValid + " valid, " + _voicePassOwned + " owned by hidden players, "
                         + _voicePassMs.ToString("0.00") + " ms (part of the sweep)"
                       : ", no voice pass yet");
        }

        /// <summary>This frame's isolation work (ms), for tests and diagnostics.</summary>
        public static float LastTickMs
        {
            get { return _tickMs; }
        }

        /// <summary>The slowest frame of isolation work since start (ms); <see cref="ResetStats"/> clears it.</summary>
        public static float MaxTickMs
        {
            get { return _tickMaxMs; }
        }

        public static void ResetStats()
        {
            _tickMaxMs = 0f;
            _hideMaxMs = 0f;
            _sweepMaxMs = 0f;
        }

        /// <summary>Test: runs a sweep now (if anyone is hidden) and returns its time in ms.</summary>
        public static float MeasureSweep()
        {
            CacheLocal();
            int before = _sweeps;
            Sweep(Time.unscaledTime);
            return _sweeps > before ? _sweepMs : -1f;
        }

        // ------------------------------------------------------------------ test path (harness, editor)

        /// <summary>Test: the presence a player id reports (overrides RrNet for that id).</summary>
        public static void SetTestPresence(int playerId, RrActivity activity, int instance, RrStatus status)
        {
            TestPresence[playerId] = new Place { Known = true, Activity = activity, Instance = instance, Status = status };
            _nextDecide = 0f;
        }

        /// <summary>Test: the player id has no presence (as a slot not written yet).</summary>
        public static void SetTestPresenceUnknown(int playerId)
        {
            TestPresence[playerId] = new Place { Known = false };
            _nextDecide = 0f;
        }

        public static void ClearTestPresence(int playerId)
        {
            TestPresence.Remove(playerId);
            _nextDecide = 0f;
        }

        /// <summary>Test: where the local player is (overrides the own presence record and RrTravel).</summary>
        public static void SetTestHere(RrActivity activity, int instance, RrStatus status)
        {
            _hasTestHere = true;
            _testHere = new Place { Known = true, Activity = activity, Instance = instance, Status = status };
            _nextDecide = 0f;
        }

        public static void ClearTestHere()
        {
            _hasTestHere = false;
            _nextDecide = 0f;
        }

        /// <summary>Test: the local player's raw nickname (the editor has no Bridge player), for the own-avatar guard.</summary>
        public static void SetTestLocalNick(string nick)
        {
            _testLocalNick = nick;
        }

        /// <summary>
        /// Test: tracks a fake remote player. <paramref name="bodyRoot"/> plays Bridge <c>Body.Root</c> (its parent is
        /// taken as the body prefab, as for a real one); its avatar is found by the normal sweep from
        /// <paramref name="nick"/> ("Avatar for Player [nick] [..]" scene roots); <paramref name="voices"/> stand in for
        /// the network part's Voice.Speaker.
        /// </summary>
        public static void RegisterTestRemote(int playerId, string nick, Transform bodyRoot, params AudioSource[] voices)
        {
            UnregisterTestRemote(playerId);
            EnsureStarted();
            var e = new Entry { Id = playerId, Nick = nick, RawNick = nick, Test = true, Root = bodyRoot };
            if (voices != null)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    if (voices[i] != null)
                    {
                        e.TestVoices.Add(voices[i]);
                    }
                }
            }

            Entries.Add(e);
            _nextDecide = 0f;
            RrLog.Info("Isolation", "test remote " + playerId + " (" + nick + ") registered");
        }

        /// <summary>Test: stops tracking a fake remote, restoring whatever was hidden.</summary>
        public static void UnregisterTestRemote(int playerId)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                if (e.Test && e.Id == playerId)
                {
                    Restore(e);
                    Queue.Remove(e);
                    Entries.RemoveAt(i);
                }
            }
        }

        /// <summary>Test: run a decision pass on the next tick instead of waiting for the interval.</summary>
        public static void DecideSoon()
        {
            _nextDecide = 0f;
        }

        // ------------------------------------------------------------------ lifecycle

        private static void EnsureStarted()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            // One scene-root pass now (boot, behind the loading card) so the first real sweep is not also the first
            // run of this code: cold, it measured 3.9-4.0 ms in the editor harness, warm 0.25-0.33 ms.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CacheLocal();
            CollectRoots();
            int roots = AvatarRoots.Count + ItemRoots.Count;
            AvatarRoots.Clear();
            ItemRoots.Clear();
            RrLog.Info("Isolation", "up: players not in your place are hidden (after " + HideDelay + " s apart, shown after " + ShowDelay
                                    + " s together; renderers forceRenderingOff, tag alpha, colliders, voices volume 0, other sounds muted); warm-up scan of " + ScratchScenes.Count
                                    + " scene(s) " + watch.Elapsed.TotalMilliseconds.ToString("0.00") + " ms, " + roots + " avatar/item root(s)");
        }

        /// <summary>From <see cref="RrRemotePlayers"/>' tick, every frame.</summary>
        internal static void Tick()
        {
            FrameWatch.Reset();
            FrameWatch.Start();
            try
            {
                EnsureStarted();
                float now = Time.unscaledTime;
                CacheLocal();
                if (now >= _nextDecide)
                {
                    _nextDecide = now + DecideInterval;
                    Sync();
                    DecideAll(now);
                    WatchOffline(now);
                }

                ApplyQueued(now);
                MaintainSome(now);
                SweepIfDue(now);
            }
            catch (Exception e)
            {
                _errors++;
                if (_errors <= 3)
                {
                    RrLog.Error("Isolation", "tick threw (#" + _errors + ")", e);
                }
            }

            FrameWatch.Stop();
            _tickMs = (float)FrameWatch.Elapsed.TotalMilliseconds;
            _tickMaxMs = Mathf.Max(_tickMaxMs, _tickMs);
        }

        private static bool OverBudget
        {
            get { return FrameWatch.Elapsed.TotalMilliseconds >= FrameBudgetMs; }
        }

        /// <summary>
        /// Restores everything this client hid and forgets every player (statics survive a world re-entry). Called
        /// from <see cref="RrRemotePlayers"/>' reset (RrPlayer.Shutdown, RrMain.OnDestroy); safe to call twice.
        /// </summary>
        public static void Shutdown()
        {
            int restored = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!Entries[i].Visible || Entries[i].Touched.Count > 0)
                {
                    restored++;
                }

                Restore(Entries[i]);
            }

            if (_started)
            {
                RrLog.Info("Isolation", "shutdown: restored " + restored + " hidden player(s); " + Describe());
            }

            Entries.Clear();
            ByRemote.Clear();
            Queue.Clear();
            TestPresence.Clear();
            AvatarRoots.Clear();
            ItemRoots.Clear();
            ScratchRoots.Clear();
            ScratchScenes.Clear();
            ScratchRenderers.Clear();
            ScratchColliders.Clear();
            ScratchAudio.Clear();
            ScratchCanvases.Clear();
            ScratchGroups.Clear();
            ScratchSkinned.Clear();
            OnVisibilityChanged = null;
            _started = false;
            _enabled = true;
            _hasTestHere = false;
            _testLocalNick = null;
            _stamp = 0;
            _cursor = 0;
            _errors = 0;
            _changeLogs = 0;
            _sweeps = 0;
            _sweepFrame = -1;
            _lastSweep = -100f;
            _sweepMs = _sweepMaxMs = _hideMaxMs = _tickMs = _tickMaxMs = 0f;
            _voicePassMs = -1f;
            _voicePassListed = _voicePassValid = _voicePassOwned = 0;
            _nextDecide = 0f;
            _offlineSince = -1f;
            _localRoot = _localCamera = _worldRoot = null;
        }

        // ------------------------------------------------------------------ decisions

        private static void CacheLocal()
        {
            _localRoot = null;
            _localCamera = null;
            _worldRoot = null;
            try
            {
                _localRoot = RrPlayer.Root;
                Camera cam = RrPlayer.Camera;
                _localCamera = cam != null ? cam.transform : null;
                _worldRoot = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            }
            catch (Exception)
            {
                // The client's player is being replaced: the guards below then refuse nothing, but the decision pass
                // only touches Bridge remote players and test entries.
            }
        }

        /// <summary>Mirrors RrRemotePlayers into entries; players who left are restored (their objects are usually gone).</summary>
        private static void Sync()
        {
            _stamp++;
            var all = RrRemotePlayers.All;
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null)
                {
                    continue;
                }

                Entry e;
                if (!ByRemote.TryGetValue(r, out e))
                {
                    e = new Entry { Remote = r };
                    ByRemote[r] = e;
                    Entries.Add(e);
                }

                e.Seen = _stamp;
                e.Id = r.Id;
                e.Nick = r.Nick;
                if (e.RawNick != r.RawNick)
                {
                    e.RawNick = r.RawNick;
                    e.AvatarUnmatchable = false;
                    e.AvatarTries = 0;
                }

                e.Root = r.Root;
            }

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                var e = Entries[i];
                if (e.Test || e.Seen == _stamp)
                {
                    continue;
                }

                Restore(e);
                Queue.Remove(e);
                Entries.RemoveAt(i);
                if (e.Remote != null)
                {
                    ByRemote.Remove(e.Remote);
                }
            }
        }

        private static void DecideAll(float now)
        {
            Place me = MyPlace();
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                Place them;
                bool want = Together(me, e.Id, out them);
                e.Them = them;
                e.Me = me;
                if (!e.Decided)
                {
                    e.Decided = true;
                    e.Want = want;
                    e.With = want;
                    e.WantSince = now;
                }
                else
                {
                    if (want != e.Want)
                    {
                        e.Want = want;
                        e.WantSince = now;
                    }

                    if (e.With != e.Want && now - e.WantSince >= (e.Want ? ShowDelay : HideDelay))
                    {
                        e.With = e.Want;
                    }
                }

                bool visible = e.With || !_enabled;
                if (visible != e.Visible && !e.Queued)
                {
                    e.Queued = true;
                    Queue.Add(e);
                }
            }
        }

        private static Place MyPlace()
        {
            if (_hasTestHere)
            {
                return _testHere;
            }

            var p = new Place { Known = true };
            try
            {
                RrPresenceView v = RrNet.Presence(RrNet.LocalId);
                if (v.Valid)
                {
                    p.Activity = v.Activity;
                    p.Instance = v.Instance;
                    p.Status = v.Status;
                    return p;
                }

                p.Activity = RrTravel.Current;
                p.Instance = RrTravel.CurrentInstance;
                p.Status = RrTravel.Busy ? RrStatus.Loading : RrStatus.None;
            }
            catch (Exception)
            {
                p.Known = false;
            }

            return p;
        }

        private static bool TheirPlace(int playerId, out Place p)
        {
            if (TestPresence.TryGetValue(playerId, out p))
            {
                return p.Known;
            }

            p = default;
            if (playerId < 0)
            {
                return false;
            }

            try
            {
                RrPresenceView v = RrNet.Presence(playerId);
                if (!v.Valid)
                {
                    return false;
                }

                p.Known = true;
                p.Activity = v.Activity;
                p.Instance = v.Instance;
                p.Status = v.Status;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The rule (see the class notes). <paramref name="them"/> is the other player's place (for the log).</summary>
        private static bool Together(Place me, int playerId, out Place them)
        {
            if (!TheirPlace(playerId, out them))
            {
                return false;
            }

            if (!me.Known || me.Status == RrStatus.Loading || them.Status == RrStatus.Loading || them.Activity != me.Activity)
            {
                return false;
            }

            if (me.Activity == RrActivity.None)
            {
                return true;
            }

            if (!Shared(me.Activity))
            {
                return false;
            }

            return them.Instance == me.Instance;
        }

        /// <summary>The dorm, and any room registered as not networked, is one per player.</summary>
        private static bool Shared(RrActivity activity)
        {
            if (activity == RrActivity.Dorm)
            {
                return false;
            }

            var info = RrRooms.Get(activity);
            return info == null || info.Networked;
        }

        private static string Where(Place p)
        {
            if (!p.Known)
            {
                return "unknown place";
            }

            string s = p.Activity == RrActivity.None ? "the arrival room" : Shared(p.Activity) ? p.Activity + " #" + p.Instance : p.Activity.ToString();
            return p.Status == RrStatus.Loading ? "loading " + s : s;
        }

        private static void WatchOffline(float now)
        {
            bool remotes = RrRemotePlayers.Count > 0;
            bool online;
            try
            {
                online = RrNet.Online;
            }
            catch (Exception)
            {
                online = false;
            }

            if (!remotes || online)
            {
                _offlineSince = -1f;
                return;
            }

            if (_offlineSince < 0f)
            {
                _offlineSince = now;
            }
            else if (now - _offlineSince > 20f)
            {
                RrLog.WarnOnce("Isolation", "offline", RrRemotePlayers.Count + " remote player(s) but the network room has not been up for 20 s: "
                                                      + "their presence is unknown, so they stay hidden");
            }
        }

        private static int SafeLocalId()
        {
            try
            {
                return RrNet.LocalId;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // ------------------------------------------------------------------ applying

        private static void ApplyQueued(float now)
        {
            int done = 0;
            while (Queue.Count > 0 && done < MaxApplyPerFrame && (done == 0 || !OverBudget))
            {
                var e = Queue[0];
                bool visible = e.With || !_enabled;
                if (!visible && visible != e.Visible)
                {
                    // Where the avatar and the voice are: found in one frame, hidden from the next (never both in one frame).
                    if (Time.frameCount == _sweepFrame)
                    {
                        return;
                    }

                    if (now - _lastSweep > SweepFresh)
                    {
                        Sweep(now);
                        return;
                    }
                }

                Queue.RemoveAt(0);
                e.Queued = false;
                if (visible == e.Visible)
                {
                    continue;
                }

                done++;
                if (visible)
                {
                    Show(e);
                }
                else
                {
                    Hide(e, now);
                }
            }
        }

        private static void Hide(Entry e, float now)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Resolve(e);
            Scan(e);
            e.Visible = false;
            e.NextMaintain = now + MaintainInterval;
            float ms = (float)watch.Elapsed.TotalMilliseconds;
            _hideMaxMs = Mathf.Max(_hideMaxMs, ms);
            LogChange(e, false, "; " + e.Renderers.Count + " renderer(s), " + e.Colliders.Count + " collider(s), tag group(s) " + e.Groups.Count + ", canvas(es) "
                                + e.Canvases.Count + ", voice(s) silenced " + e.Quiet.Count + ", sound(s) muted " + e.Muted.Count + ", avatar " + (e.Avatar != null ? "found" : "not found") + ", "
                                + ms.ToString("0.00") + " ms");
            Raise(e.Id, false);
        }

        private static void Show(Entry e)
        {
            int n = e.Renderers.Count;
            Restore(e);
            e.Visible = true;
            LogChange(e, true, "; " + n + " renderer(s) restored");
            Raise(e.Id, true);
        }

        private static void LogChange(Entry e, bool shown, string detail)
        {
            _changeLogs++;
            if (_changeLogs <= 400 || _changeLogs % 50 == 0)
            {
                RrLog.Info("Isolation", "player " + e.Id + " (" + e.Nick + ") " + (shown ? "shown" : "hidden") + ": in " + Where(e.Them) + " (you: " + Where(e.Me) + ")" + detail
                                        + (_changeLogs == 400 ? " (further changes: every 50th)" : ""));
            }
        }

        private static void Raise(int id, bool visible)
        {
            var handler = OnVisibilityChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(id, visible);
            }
            catch (Exception ex)
            {
                RrLog.Error("Isolation", "an OnVisibilityChanged handler threw", ex);
            }
        }

        /// <summary>Round-robin re-scan of hidden players: renderers, colliders and sounds the client re-enabled or made.</summary>
        private static void MaintainSome(float now)
        {
            if (Entries.Count == 0)
            {
                return;
            }

            int done = 0;
            for (int k = 0; k < Entries.Count && done < MaxMaintainPerFrame && !OverBudget; k++)
            {
                _cursor = (_cursor + 1) % Entries.Count;
                var e = Entries[_cursor];
                if (e.Visible || now < e.NextMaintain)
                {
                    continue;
                }

                done++;
                e.NextMaintain = now + MaintainInterval;
                Resolve(e);
                Scan(e);
            }
        }

        /// <summary>Body.Root, the body prefab above it, and the sounds; refused when it would reach local or world objects.</summary>
        private static void Resolve(Entry e)
        {
            if (e.Remote != null)
            {
                e.Root = e.Remote.Root;
            }

            Transform body = e.Root;
            if (body != null && body.parent != null && Safe(body.parent))
            {
                body = body.parent;
            }

            e.Body = body != null && Safe(body) ? body : null;
            if (e.Avatar != null && !Safe(e.Avatar))
            {
                e.Avatar = null;
            }

            if (e.Test)
            {
                e.Voices.Clear();
                e.Voices.AddRange(e.TestVoices);
            }
        }

        /// <summary>Not the local player's (body, camera) and not this world's.</summary>
        private static bool Safe(Transform t)
        {
            if (t == null)
            {
                return false;
            }

            if (_localRoot != null && (_localRoot == t || _localRoot.IsChildOf(t)))
            {
                return false;
            }

            if (_localCamera != null && _localCamera.IsChildOf(t))
            {
                return false;
            }

            if (_worldRoot != null && (_worldRoot == t || t.IsChildOf(_worldRoot) || _worldRoot.IsChildOf(t)))
            {
                return false;
            }

            if (RrMain.Instance != null && RrMain.Instance.transform.IsChildOf(t))
            {
                return false;
            }

            return true;
        }

        /// <summary>Hides what is not hidden yet (idempotent: the first hide and every re-scan).</summary>
        private static void Scan(Entry e)
        {
            ScanTree(e, e.Body);
            ScanTree(e, e.Avatar);
            for (int i = e.Extras.Count - 1; i >= 0; i--)
            {
                if (e.Extras[i] == null)
                {
                    e.Extras.RemoveAt(i);
                    continue;
                }

                ScanTree(e, e.Extras[i]);
            }

            for (int i = 0; i < e.Voices.Count; i++)
            {
                Silence(e, e.Voices[i]);
            }

            ScanCanvases(e);
        }

        private static void ScanTree(Entry e, Transform root)
        {
            if (root == null || !Safe(root))
            {
                return;
            }

            root.GetComponentsInChildren(true, ScratchRenderers);
            for (int i = 0; i < ScratchRenderers.Count; i++)
            {
                Renderer r = ScratchRenderers[i];
                if (r != null && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    if (e.Touched.Add(r))
                    {
                        e.Renderers.Add(r);
                    }
                }
            }

            root.GetComponentsInChildren(true, ScratchColliders);
            for (int i = 0; i < ScratchColliders.Count; i++)
            {
                Collider c = ScratchColliders[i];
                if (c != null && c.enabled)
                {
                    c.enabled = false;
                    if (e.Touched.Add(c))
                    {
                        e.Colliders.Add(c);
                    }
                }
            }

            root.GetComponentsInChildren(true, ScratchAudio);
            for (int i = 0; i < ScratchAudio.Count; i++)
            {
                AudioSource a = ScratchAudio[i];
                if (a != null && (e.Voices.Contains(a) || e.Quiet.Contains(a)))
                {
                    Silence(e, a);
                }
                else
                {
                    Mute(e, a);
                }
            }

            ScratchRenderers.Clear();
            ScratchColliders.Clear();
            ScratchAudio.Clear();
        }

        /// <summary>A sound that is not a voice: mute, recorded only when this client flipped it.</summary>
        private static void Mute(Entry e, AudioSource a)
        {
            if (a != null && !a.mute)
            {
                a.mute = true;
                if (e.Touched.Add(a))
                {
                    e.Muted.Add(a);
                }
            }
        }

        /// <summary>
        /// A voice: volume 0, the client's mute/block flag never touched. The first time, the current volume is recorded;
        /// on a re-scan, a volume above 0 can only be the client's (SetVoiceLevel) and replaces the recorded one.
        /// </summary>
        private static void Silence(Entry e, AudioSource a)
        {
            if (a == null)
            {
                return;
            }

            int i = e.Quiet.IndexOf(a);
            if (i < 0)
            {
                e.Quiet.Add(a);
                e.QuietVolume.Add(a.volume);
                e.Touched.Add(a);
                a.volume = 0f;
                int m = e.Muted.IndexOf(a);
                if (m >= 0)
                {
                    // Muted by an earlier scan before it was known to be a voice (found under the body before the sweep
                    // matched it): that mute was ours (it was unmuted then), so it goes back now and the flag is the client's again.
                    e.Muted.RemoveAt(m);
                    a.mute = false;
                }

                return;
            }

            float v = a.volume;
            if (v > 0f)
            {
                e.QuietVolume[i] = v;
                a.volume = 0f;
                if (!e.LoggedLevel)
                {
                    e.LoggedLevel = true;
                    RrLog.Info("Isolation", "player " + e.Id + " (" + e.Nick + "): voice level set to " + v.ToString("0.00") + " by the client while hidden: silenced again, "
                                            + "that level comes back when they join you");
                }
            }
        }

        /// <summary>The name tag (CanvasGroup alpha) and the other canvases next to Body.Root (Canvas.enabled).</summary>
        private static void ScanCanvases(Entry e)
        {
            Transform body = e.Body;
            if (body == null || body == e.Root)
            {
                return;
            }

            for (int i = 0; i < body.childCount; i++)
            {
                Transform child = body.GetChild(i);
                if (child == e.Root)
                {
                    continue;
                }

                bool tag = child.name.StartsWith(TagPrefix, StringComparison.Ordinal) || child.GetComponent(TagComponent) != null;
                if (tag)
                {
                    child.GetComponentsInChildren(true, ScratchGroups);
                    int groups = ScratchGroups.Count;
                    for (int g = 0; g < ScratchGroups.Count; g++)
                    {
                        HideGroup(e, ScratchGroups[g]);
                    }

                    ScratchGroups.Clear();
                    if (groups > 0)
                    {
                        continue;
                    }

                    if (!e.LoggedNoTag)
                    {
                        e.LoggedNoTag = true;
                        RrLog.Warn("Isolation", "player " + e.Id + "'s name tag '" + child.name + "' has no CanvasGroup: hiding its canvases instead (the client "
                                                + "re-enables them on frustum changes, so the tag may flash until the next re-scan)");
                    }
                }

                child.GetComponentsInChildren(true, ScratchCanvases);
                for (int c = 0; c < ScratchCanvases.Count; c++)
                {
                    Canvas cv = ScratchCanvases[c];
                    if (cv != null && cv.enabled)
                    {
                        cv.enabled = false;
                        if (e.Touched.Add(cv))
                        {
                            e.Canvases.Add(cv);
                        }
                    }
                }

                ScratchCanvases.Clear();
            }
        }

        /// <summary>
        /// alpha 0 and no raycasts; the value restored is the last one seen that was not ours (the client writes the
        /// tag alpha only for a quick chat, so a write while hidden is its newest wish).
        /// </summary>
        private static void HideGroup(Entry e, CanvasGroup g)
        {
            if (g == null)
            {
                return;
            }

            int i = e.Groups.IndexOf(g);
            if (i < 0)
            {
                if (g.alpha <= 0f && !g.blocksRaycasts)
                {
                    return;
                }

                e.Groups.Add(g);
                e.GroupAlpha.Add(g.alpha);
                e.GroupBlocks.Add(g.blocksRaycasts);
                e.Touched.Add(g);
            }
            else
            {
                if (g.alpha > 0f)
                {
                    e.GroupAlpha[i] = g.alpha;
                }

                if (g.blocksRaycasts)
                {
                    e.GroupBlocks[i] = true;
                }
            }

            g.alpha = 0f;
            g.blocksRaycasts = false;
        }

        /// <summary>Puts back exactly what this client changed; objects destroyed meanwhile are skipped.</summary>
        private static void Restore(Entry e)
        {
            try
            {
                for (int i = 0; i < e.Renderers.Count; i++)
                {
                    if (e.Renderers[i] != null)
                    {
                        e.Renderers[i].forceRenderingOff = false;
                    }
                }

                for (int i = 0; i < e.Colliders.Count; i++)
                {
                    if (e.Colliders[i] != null)
                    {
                        e.Colliders[i].enabled = true;
                    }
                }

                for (int i = 0; i < e.Canvases.Count; i++)
                {
                    if (e.Canvases[i] != null)
                    {
                        e.Canvases[i].enabled = true;
                    }
                }

                for (int i = 0; i < e.Groups.Count; i++)
                {
                    if (e.Groups[i] != null)
                    {
                        e.Groups[i].alpha = e.GroupAlpha[i];
                        e.Groups[i].blocksRaycasts = e.GroupBlocks[i];
                    }
                }

                for (int i = 0; i < e.Muted.Count; i++)
                {
                    if (e.Muted[i] != null)
                    {
                        e.Muted[i].mute = false;
                    }
                }

                // Voices: only the volume goes back. A volume above 0 now was set by the client after the last re-scan
                // and is its newest level, so it stays. mute (the client's mute/block) was never written.
                for (int i = 0; i < e.Quiet.Count; i++)
                {
                    AudioSource a = e.Quiet[i];
                    if (a != null && a.volume <= 0f)
                    {
                        a.volume = e.QuietVolume[i];
                    }
                }
            }
            catch (Exception ex)
            {
                RrLog.Error("Isolation", "restoring player " + e.Id + " failed", ex);
            }

            e.Renderers.Clear();
            e.Colliders.Clear();
            e.Canvases.Clear();
            e.Groups.Clear();
            e.GroupAlpha.Clear();
            e.GroupBlocks.Clear();
            e.Muted.Clear();
            e.Quiet.Clear();
            e.QuietVolume.Clear();
            e.LoggedLevel = false;
            e.Touched.Clear();
            e.Visible = true;
        }

        // ------------------------------------------------------------------ finding the objects that live elsewhere

        private static void SweepIfDue(float now)
        {
            if (Time.frameCount == _sweepFrame || OverBudget)
            {
                return;
            }

            bool anyHidden = false, missing = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.Visible)
                {
                    continue;
                }

                anyHidden = true;
                if ((e.Avatar == null && !e.AvatarUnmatchable && e.AvatarTries < SweepRetries) || (!e.Test && e.Voices.Count == 0 && e.VoiceTries < SweepRetries))
                {
                    missing = true;
                }
            }

            if (anyHidden && now - _lastSweep >= (missing ? SweepRetry : SweepPeriod))
            {
                Sweep(now);
            }
        }

        /// <summary>
        /// One pass over the scene roots (avatars, "Simulator Item" roots) and the Fusion NetworkObjects (voices) for
        /// every player who is hidden or about to be. At most once per frame.
        /// </summary>
        private static void Sweep(float now)
        {
            _sweepFrame = Time.frameCount;
            _lastSweep = now;
            bool any = false, anyReal = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].NeedsSweep)
                {
                    any = true;
                    anyReal |= !Entries[i].Test;
                }
            }

            if (!any)
            {
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            CollectRoots();
            MatchAvatars();
            if (anyReal)
            {
                TimedVoicePass();
            }

            MatchItems();
            _sweeps++;
            _sweepMs = (float)watch.Elapsed.TotalMilliseconds;
            _sweepMaxMs = Mathf.Max(_sweepMaxMs, _sweepMs);
            if (_sweeps == 1)
            {
                RrLog.Info("Isolation", "first sweep: " + ScratchScenes.Count + " scene(s), " + AvatarRoots.Count + " avatar root(s), " + ItemRoots.Count + " item root(s), "
                                        + _sweepMs.ToString("0.00") + " ms");
            }
        }

        private static void CollectRoots()
        {
            ScratchScenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                AddScene(SceneManager.GetSceneAt(i));
            }

            if (_localRoot != null)
            {
                AddScene(_localRoot.gameObject.scene);
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Root != null)
                {
                    AddScene(Entries[i].Root.gameObject.scene);
                }
            }

            AvatarRoots.Clear();
            ItemRoots.Clear();
            for (int s = 0; s < ScratchScenes.Count; s++)
            {
                ScratchRoots.Clear();
                ScratchScenes[s].GetRootGameObjects(ScratchRoots);
                for (int i = 0; i < ScratchRoots.Count; i++)
                {
                    GameObject go = ScratchRoots[i];
                    if (go == null)
                    {
                        continue;
                    }

                    string n = go.name;
                    if (n.StartsWith(AvatarPrefix, StringComparison.Ordinal))
                    {
                        int end = n.LastIndexOf("] [", StringComparison.Ordinal);
                        if (end >= AvatarPrefix.Length)
                        {
                            AvatarRoots.Add(new KeyValuePair<string, Transform>(n.Substring(AvatarPrefix.Length, end - AvatarPrefix.Length), go.transform));
                        }
                    }
                    else if (n.StartsWith(ItemPrefix, StringComparison.Ordinal))
                    {
                        ItemRoots.Add(go.transform);
                    }
                }
            }

            ScratchRoots.Clear();
        }

        private static void AddScene(Scene s)
        {
            if (s.IsValid() && s.isLoaded && !ScratchScenes.Contains(s))
            {
                ScratchScenes.Add(s);
            }
        }

        private static string LocalRawNick()
        {
            if (_testLocalNick != null)
            {
                return _testLocalNick;
            }

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

        /// <summary>By the remote's raw Bridge nickname; never the local nickname, never a nickname two players share.</summary>
        private static void MatchAvatars()
        {
            string localNick = LocalRawNick();
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (!e.NeedsSweep)
                {
                    continue;
                }

                e.Avatar = null;
                if (string.IsNullOrEmpty(e.RawNick) || e.RawNick == localNick)
                {
                    NoAvatar(e, string.IsNullOrEmpty(e.RawNick) ? "the Bridge gives no nickname" : "the nickname is the local player's");
                    continue;
                }

                bool shared = false;
                for (int k = 0; k < Entries.Count; k++)
                {
                    if (k != i && Entries[k].RawNick == e.RawNick)
                    {
                        shared = true;
                    }
                }

                Transform found = null;
                int matches = 0;
                for (int k = 0; k < AvatarRoots.Count; k++)
                {
                    if (AvatarRoots[k].Key == e.RawNick)
                    {
                        matches++;
                        found = AvatarRoots[k].Value;
                    }
                }

                if (shared || matches > 1)
                {
                    NoAvatar(e, "two players share the nickname");
                    continue;
                }

                if (found == null || !Safe(found))
                {
                    e.AvatarTries++;
                    if (e.AvatarTries == SweepRetries && !e.LoggedAvatar)
                    {
                        e.LoggedAvatar = true;
                        RrLog.Warn("Isolation", "player " + e.Id + " (" + e.Nick + "): no '" + AvatarPrefix + e.RawNick + "] [..]' root after " + SweepRetries + " sweeps ("
                                                + AvatarRoots.Count + " avatar root(s) seen): their avatar stays drawn");
                    }

                    continue;
                }

                e.Avatar = found;
                e.AvatarTries = 0;
            }
        }

        private static void NoAvatar(Entry e, string why)
        {
            e.AvatarUnmatchable = true;
            if (!e.LoggedAvatar)
            {
                e.LoggedAvatar = true;
                RrLog.Warn("Isolation", "player " + e.Id + " (" + e.Nick + "): the avatar cannot be matched safely (" + why + "): only the body, tag and voice are hidden");
            }
        }

        /// <summary>The network part (Voice.Speaker) is a Fusion NetworkObject whose state authority is the player.</summary>
        /// <summary>
        /// The voice pass, timed on its own: it is the part only a real session runs (the harness has no valid
        /// NetworkObjects), so its cost and counts are kept for <see cref="Describe"/> and logged once.
        /// </summary>
        private static void TimedVoicePass()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool first = _voicePassMs < 0f;
            MatchVoices();
            _voicePassMs = (float)watch.Elapsed.TotalMilliseconds;
            if (first)
            {
                RrLog.Info("Isolation", "first voice pass: " + _voicePassListed + " NetworkObject(s), " + _voicePassValid + " valid, " + _voicePassOwned
                                        + " owned by a hidden player, " + _voicePassMs.ToString("0.00") + " ms");
            }
        }

        /// <summary>Test: runs the voice pass once (as a real session would) and returns its cost in ms.</summary>
        public static float MeasureVoicePass()
        {
            CacheLocal();
            TimedVoicePass();
            return _voicePassMs;
        }

        private static void MatchVoices()
        {
            _voicePassListed = _voicePassValid = _voicePassOwned = 0;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].NeedsSweep && !Entries[i].Test)
                {
                    Entries[i].Voices.Clear();
                }
            }

            Fusion.NetworkObject[] all;
            try
            {
                all = Object.FindObjectsByType<Fusion.NetworkObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
            catch (Exception ex)
            {
                RrLog.WarnOnce("Isolation", "find-networkobjects", "listing NetworkObjects failed: " + ex.Message + " (voices stay audible)");
                return;
            }

            _voicePassListed = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                Fusion.NetworkObject no = all[i];
                if (no == null)
                {
                    continue;
                }

                int owner;
                try
                {
                    if (!no.IsValid)
                    {
                        continue;
                    }

                    owner = no.StateAuthority.PlayerId;
                }
                catch (Exception)
                {
                    continue;
                }

                _voicePassValid++;
                Entry e = NeedingReal(owner);
                if (e == null)
                {
                    continue;
                }

                _voicePassOwned++;

                Transform t = no.transform;
                if (!Safe(t) || OurNetObject(t))
                {
                    continue;
                }

                t.GetComponentsInChildren(true, ScratchAudio);
                for (int k = 0; k < ScratchAudio.Count; k++)
                {
                    if (ScratchAudio[k] != null && !e.Voices.Contains(ScratchAudio[k]))
                    {
                        e.Voices.Add(ScratchAudio[k]);
                    }
                }

                if (ScratchAudio.Count > 0 && !e.LoggedVoice)
                {
                    e.LoggedVoice = true;
                    RrLog.Info("Isolation", "player " + e.Id + " (" + e.Nick + "): " + ScratchAudio.Count + " sound(s) found in network object '" + t.name + "' (first '"
                                            + ScratchAudio[0].name + "')");
                }

                ScratchAudio.Clear();
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (!e.NeedsSweep || e.Test)
                {
                    continue;
                }

                if (e.Voices.Count == 0)
                {
                    e.VoiceTries++;
                    if (e.VoiceTries == SweepRetries)
                    {
                        RrLog.Warn("Isolation", "player " + e.Id + " (" + e.Nick + "): no sound found in their network objects after " + SweepRetries + " sweeps ("
                                                + all.Length + " NetworkObject(s)): their voice may stay audible");
                    }
                }
                else
                {
                    e.VoiceTries = 0;
                }
            }
        }

        /// <summary>This world's own scene network objects ("Rr Net" root: presence slots owned by their players).</summary>
        private static bool OurNetObject(Transform t)
        {
            Transform root = t.root;
            string n = root != null ? root.name : "";
            return n.StartsWith("Rr Net", StringComparison.Ordinal) || n == "Scene Networking" || t.name.StartsWith("Rr Net", StringComparison.Ordinal);
        }

        private static Entry NeedingReal(int playerId)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.Id == playerId && !e.Test && e.NeedsSweep)
                {
                    return e;
                }
            }

            return null;
        }

        /// <summary>A "Simulator Item" root skinned to a hidden avatar's bones (cloth), or owned by a hidden player.</summary>
        private static void MatchItems()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].NeedsSweep)
                {
                    Entries[i].Extras.Clear();
                }
            }

            for (int i = 0; i < ItemRoots.Count; i++)
            {
                Transform item = ItemRoots[i];
                if (item == null || !Safe(item))
                {
                    continue;
                }

                Entry owner = null;
                item.GetComponentsInChildren(true, ScratchSkinned);
                for (int k = 0; k < ScratchSkinned.Count && owner == null; k++)
                {
                    SkinnedMeshRenderer smr = ScratchSkinned[k];
                    if (smr == null)
                    {
                        continue;
                    }

                    Transform bone = smr.rootBone;
                    if (bone == null)
                    {
                        Transform[] bones = smr.bones;
                        bone = bones != null && bones.Length > 0 ? bones[0] : null;
                    }

                    owner = bone != null ? OwnerOfBone(bone) : null;
                }

                ScratchSkinned.Clear();
                if (owner == null)
                {
                    try
                    {
                        Fusion.NetworkObject no = item.GetComponentInChildren<Fusion.NetworkObject>(true);
                        if (no != null && no.IsValid)
                        {
                            owner = NeedingReal(no.StateAuthority.PlayerId);
                        }
                    }
                    catch (Exception)
                    {
                        owner = null;
                    }
                }

                if (owner != null && !owner.Extras.Contains(item))
                {
                    owner.Extras.Add(item);
                }
            }
        }

        private static Entry OwnerOfBone(Transform bone)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.NeedsSweep && e.Avatar != null && bone.IsChildOf(e.Avatar))
                {
                    return e;
                }
            }

            return null;
        }
    }
}
