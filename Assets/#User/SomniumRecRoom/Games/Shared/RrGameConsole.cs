using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Watch;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Rec Room's game-start hologram: the <c>(ScoreboardProjector)</c> every game and quest level stands at its
    /// G "scoreboard" marker, projecting the players in the room, the game's title, mode and map, and START.
    ///
    /// The projector is Rec Room's own prefab from the install (bundle 43ea1176..., root #2378492940477835731),
    /// spawned as scenery with its Animator kept: <c>ScoreboardProjectorAnimController</c> has one bool "Open"
    /// (CloseIdle -> Open -> OpenIdle -> Close, 30 fps clips) and a float "Rotate" blend tree (the arm turns up to
    /// UIProjector.maxRotationAngle 60 degrees; left at 0 here). Its UIProjector (a Rec Room script, inert at run time)
    /// says what we redo by hand: it opens when a player is within outerProximityTriggerRadius 20 m, plays
    /// SFX_ScoreboardPopUp_Mono / SFX_ScoreboardPopDown_Mono at volume 0.2 and hums SFX_ScoreboardLoop_Mono at 0.01,
    /// and parents its viewPrefab <c>[GameScoreboardView]</c> (scale 0.0083538) under viewParent
    /// <c>ScoreboardViewRoot</c>, a child of Plane.Jnt, the joint the Open clip grows from scale 0. We parent our own
    /// rebuild of that view (<see cref="RrGameConsolePanel"/>) there the same way, so the hologram opens and closes
    /// with Rec Room's animation. When the projector cannot be spawned or its open pose does not give an upright
    /// panel of a sane size, the panel stands alone at <see cref="FixedCentre"/> and unfolds by itself.
    ///
    /// The marker pose's +Z faces the players: checked against the levels' pregame spawns (Dodgeball's lobby spawns
    /// face the projector, yaw -117.5 degrees at (2.79, 0.36, -11.81); Paddleball's and both Charades projectors
    /// face the middle of the room).
    ///
    /// Input reaches START (and the mode / map buttons) the ways <c>RrRoomListPanel</c> uses: the client's XR ray and
    /// free cursor through a GraphicRaycaster + TrackedDeviceGraphicRaycaster on the canvas; our aim ray
    /// (<see cref="RrInput.AimRay"/>: the crosshair on desktop, the right hand in VR) meeting the canvas plane,
    /// clicked with <see cref="RrInput.UseDown"/>; and a VR fingertip poke. One press seen twice within 0.35 s counts
    /// once. The console never changes game state: it calls the spec's callbacks (<see cref="RrGameConsoleSpec"/>).
    /// </summary>
    public sealed class RrGameConsole : MonoBehaviour
    {
        public const string ProjectorPrefab = "(ScoreboardProjector)";
        public const string ViewRootNode = "ScoreboardViewRoot";
        public const string OpenParameter = "Open";

        /// <summary>[GameScoreboardView]'s own localScale under ScoreboardViewRoot.</summary>
        public const float ViewScale = 0.0083538f;

        public const string PopUpClip = "SFX_ScoreboardPopUp_Mono";
        public const string PopDownClip = "SFX_ScoreboardPopDown_Mono";
        public const string HumClip = "SFX_ScoreboardLoop_Mono";
        public const float PopVolume = 0.2f;
        public const float HumVolume = 0.01f;

        /// <summary>UIProjector.outerProximityTriggerRadius; closing 2 m further out is ours (no flicker at the edge).</summary>
        public const float OpenRadius = 20f;

        public const float CloseRadius = 22f;

        /// <summary>
        /// Where Rec Room's open hologram ends up, measured in the play-mode harness (2026-09-25, the same in the
        /// Dorm, Dodgeball, Paddleball and Charades): once the Open clip has run, ScoreboardViewRoot's lossy scale is
        /// 0.3914 / 0.3918 / 0.3918, so the 1333.33 x 1000 canvas at 0.0083538 is 4.36 x 3.27 m, upright (up 1.00),
        /// reading toward the marker's +Z (1.00), its centre 2.52 m up and 0.54 m in front of the marker: it spans
        /// 0.89 to 4.15 m above the floor. A spot for a console needs that much clear room (<see cref="IsClear"/>).
        /// </summary>
        public static readonly Vector3 ProjectedCentre = new Vector3(0.01f, 2.52f, 0.54f);

        public static readonly Vector2 ProjectedSize = new Vector2(4.36f, 3.27f);

        /// <summary>The panel on its own (no projector): the same place and size as the projected one.</summary>
        public static readonly Vector3 FixedCentre = ProjectedCentre;

        public const float FixedWidth = 4.36f;

        public const float RefreshSeconds = 0.5f;
        public const float PokeFront = 0.03f;
        public const float PokeBack = 0.05f;
        public const float ActionDebounce = 0.35f;

        /// <summary>A projected panel outside these is not trusted (the Open pose went wrong): it stands alone instead.</summary>
        private const float MinWidth = 0.6f;

        private const float MaxWidth = 6f;
        private const float UnfoldSeconds = 0.35f;

        private static readonly List<RrGameConsole> Consoles = new List<RrGameConsole>();

        public static IReadOnlyList<RrGameConsole> All
        {
            get { return Consoles; }
        }

        private enum Mount
        {
            None,
            Projector,
            Fixed
        }

        private RrGameConsoleSpec _spec;
        private RrGameConsolePanel _panel;
        private RrInstance _projector;
        private Animator _animator;
        private Transform _viewRoot;
        private Mount _mount;
        private bool _booted;
        private bool _open;
        private bool _measured;
        private float _openedAt = -1f;
        private float _unfold = -1f;
        private bool _folding;
        private bool _closing;
        private AudioSource _hum;
        private float _refreshAt;
        private float _cameraAt;
        private float _proximityAt;
        private float _lastAction = -10f;
        private GraphicRaycaster _raycaster;
        private TrackedDeviceGraphicRaycaster _tracked;
        private PointerEventData _ped;
        private RrUiButton _hovered;
        private readonly RrUiButton[] _poking = new RrUiButton[2];
        private readonly List<RrUiButton> _targets = new List<RrUiButton>();
        private readonly RrGameConsolePanel.Model _model = new RrGameConsolePanel.Model();
        private readonly List<int> _members = new List<int>();
        private string _mountNote = "";

        public RrGameConsoleSpec Spec
        {
            get { return _spec; }
        }

        /// <summary>The hologram is open: the projector's Open state reached (or the lone panel unfolded) and the panel shown.</summary>
        public bool IsOpen
        {
            get
            {
                if (!_booted || !_open || _panel == null)
                {
                    return false;
                }

                if (_mount == Mount.Projector)
                {
                    return _viewRoot != null && _viewRoot.lossyScale.x > 1e-3f && Time.unscaledTime - _openedAt > 0.2f;
                }

                return _unfold < 0f;
            }
        }

        public int ShownPlayers
        {
            get { return _members.Count; }
        }

        public string StartText
        {
            get { return _model.Start; }
        }

        public bool StartEnabled
        {
            get { return _model.StartEnabled; }
        }

        /// <summary>"projector" (Rec Room's, the panel under ScoreboardViewRoot), "fixed" (the panel alone) or "none" yet.</summary>
        public string MountMode
        {
            get { return _mount == Mount.Projector ? "projector" : _mount == Mount.Fixed ? "fixed" : "none"; }
        }

        // ---- making ----

        /// <summary>
        /// A console at a level's G "scoreboard" marker: <paramref name="localPose"/> is the projector's floor point in
        /// <paramref name="parent"/> space and its +Z faces the players.
        /// </summary>
        public static RrGameConsole Create(Transform parent, Pose localPose, RrGameConsoleSpec spec)
        {
            spec = spec ?? new RrGameConsoleSpec();
            var go = new GameObject("Game Console " + (string.IsNullOrEmpty(spec.Title) ? "" : spec.Title));
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = localPose.position;
            go.transform.localRotation = localPose.rotation;
            var c = go.AddComponent<RrGameConsole>();
            c._spec = spec;
            return c;
        }

        /// <summary>
        /// A console for an <see cref="RrGameRoom"/>: what the spec leaves unset comes from the room - title, minimum and
        /// maximum from its <c>RrRoomInfo</c>, START = <see cref="RrGameRoom.RequestStart"/> (the authority starts the
        /// countdown), pressable in the lobby only, its label and the timer following the phase.
        /// </summary>
        public static RrGameConsole ForGame(RrGameRoom room, Transform parent, Pose localPose, RrGameConsoleSpec spec = null)
        {
            spec = spec ?? new RrGameConsoleSpec();
            var info = room != null ? room.Info : null;
            if (string.IsNullOrEmpty(spec.Title) && info != null)
            {
                spec.Title = info.Title;
            }

            if (spec.MinPlayers <= 0 && info != null)
            {
                spec.MinPlayers = info.MinPlayers;
            }

            if (spec.MaxPlayers <= 0 && info != null)
            {
                spec.MaxPlayers = info.MaxPlayers;
            }

            if (spec.OnStart == null)
            {
                spec.OnStart = id =>
                {
                    if (room != null)
                    {
                        room.RequestStart();
                    }
                };
            }

            if (spec.CanStart == null)
            {
                spec.CanStart = () => room != null && room.Phase == RrPhase.Lobby;
            }

            if (spec.StartLabel == null)
            {
                spec.StartLabel = () => PhaseStartLabel(room);
            }

            if (spec.Timer == null)
            {
                spec.Timer = () => PhaseTimer(room);
            }

            if (spec.Subtitle == null)
            {
                spec.Subtitle = () => PhaseLine(room);
            }

            return Create(parent != null ? parent : room != null ? room.transform : null, localPose, spec);
        }

        /// <summary>START's label for a game's phase.</summary>
        public static string PhaseStartLabel(RrGameRoom room)
        {
            if (room == null)
            {
                return "START";
            }

            switch (room.Phase)
            {
                case RrPhase.Countdown:
                    return "STARTING";
                case RrPhase.Playing:
                case RrPhase.RoundOver:
                    return "IN PROGRESS";
                case RrPhase.MatchOver:
                    return "GAME OVER";
            }

            return "START";
        }

        /// <summary>The timer cell for a game's phase: the countdown or the round's time left, m:ss.</summary>
        public static string PhaseTimer(RrGameRoom room)
        {
            if (room == null || room.Phase == RrPhase.Lobby)
            {
                return "";
            }

            float left = RrNet.Instance != null ? RrNet.Instance.TimeRemaining : 0f;
            if (left <= 0f)
            {
                return "";
            }

            int s = Mathf.CeilToInt(left);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>The subtitle line for a game's phase (when the game has no modes to show there).</summary>
        public static string PhaseLine(RrGameRoom room)
        {
            if (room == null)
            {
                return "";
            }

            switch (room.Phase)
            {
                case RrPhase.Countdown:
                    return "GET READY";
                case RrPhase.Playing:
                    return "GAME ON";
                case RrPhase.RoundOver:
                    return "ROUND OVER";
                case RrPhase.MatchOver:
                    return "GAME OVER";
            }

            return "WAITING FOR PLAYERS";
        }

        // ---- lifecycle ----

        private void OnEnable()
        {
            if (!Consoles.Contains(this))
            {
                Consoles.Add(this);
            }

            RrNet.OnPlayersChanged += OnPlayersChanged;
        }

        private void OnDisable()
        {
            RrNet.OnPlayersChanged -= OnPlayersChanged;
        }

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private void OnPlayersChanged()
        {
            _refreshAt = 0f;
        }

        private IEnumerator Boot()
        {
            yield return RrGameConsolePanel.LoadFonts();
            bool hasModes = _spec.Modes != null && _spec.Modes.Length > 0;
            bool hasMaps = _spec.Maps != null && _spec.Maps.Length > 0;
            _panel = RrGameConsolePanel.Build(transform, hasModes, hasMaps);
            _panel.StartButton.onClick.AddListener(() => Act(() => DoStart("click")));
            _targets.Add(_panel.StartButton);
            if (_panel.ModeButton != null)
            {
                _panel.ModeButton.onClick.AddListener(() => Act(DoMode));
                _targets.Add(_panel.ModeButton);
            }

            if (_panel.MapButton != null)
            {
                _panel.MapButton.onClick.AddListener(() => Act(DoMap));
                _targets.Add(_panel.MapButton);
            }

            var canvasGo = _panel.Rect.gameObject;
            _raycaster = canvasGo.AddComponent<GraphicRaycaster>();
            _tracked = canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            _panel.Canvas.worldCamera = RrPlayer.Camera;
            _ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            canvasGo.SetActive(false);
            RefreshNow();
            yield return null;

            if (_spec.Projector && RrAssets.Instance != null)
            {
                bool done = false;
                RrSpawn.Spawn(ProjectorPrefab, transform, Vector3.zero, Quaternion.identity, inst =>
                {
                    _projector = inst;
                    done = true;
                }, RrSpawnOptions.Scenery());
                float until = Time.realtimeSinceStartup + 20f;
                while (!done && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }
            }

            if (this == null)
            {
                yield break;
            }

            if (_projector != null)
            {
                _animator = _projector.GetComponent<Animator>();
                _viewRoot = _projector.Find(ViewRootNode);
                if (_animator != null)
                {
                    // Culled animators stop updating their transforms: the hologram must open wherever it is seen from.
                    _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }

            string why = null;
            if (_projector == null)
            {
                why = _spec.Projector ? "the (ScoreboardProjector) prefab did not load" : "no projector asked for";
            }
            else if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                why = "the projector has no animator";
            }
            else if (_viewRoot == null)
            {
                why = "the projector has no " + ViewRootNode;
            }
            else if (!HasOpenParameter(_animator))
            {
                why = "the projector's animator has no bool '" + OpenParameter + "'";
            }

            if (why == null)
            {
                MountProjected();
            }
            else
            {
                MountFixed(why);
            }

            canvasGo.SetActive(true);
            _booted = true;
            _proximityAt = 0f;
            RrTools.Once("Console", "made", "game console: Rec Room's " + (_mount == Mount.Projector ? ProjectorPrefab + " carries the panel" : "panel alone (" + _mountNote + ")") +
                                            "; opens within " + OpenRadius + " m");
        }

        private static bool HasOpenParameter(Animator a)
        {
            try
            {
                foreach (var p in a.parameters)
                {
                    if (p.type == AnimatorControllerParameterType.Bool && p.name == OpenParameter)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // an animator without a controller throws in some versions: treated as none
            }

            return false;
        }

        private void MountProjected()
        {
            _mount = Mount.Projector;
            _panel.Rect.SetParent(_viewRoot, false);
            _panel.Rect.localPosition = Vector3.zero;
            _panel.Rect.localRotation = Quaternion.identity;
            _panel.Rect.localScale = Vector3.one * ViewScale;
            _measured = false;
        }

        private void MountFixed(string why)
        {
            _mount = Mount.Fixed;
            _mountNote = why;
            _panel.Rect.SetParent(transform, false);
            _panel.Rect.localPosition = FixedCentre;

            // A UI canvas reads from its -Z side: turned round so it reads from the marker's +Z (the players' side).
            _panel.Rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            float s = FixedWidth / RrGameConsolePanel.Width;
            _panel.Rect.localScale = new Vector3(s, _open ? s : s * 0.01f, s);
            _measured = true;
        }

        private void OnDestroy()
        {
            Consoles.Remove(this);
            RrSound.Stop(_hum);
            _hum = null;
            _hovered = null;
            _poking[0] = _poking[1] = null;
            RrNet.OnPlayersChanged -= OnPlayersChanged;
            if (Consoles.Count == 0)
            {
                RrGameConsolePanel.ReleaseFonts();
            }
        }

        // ---- open / close ----

        private void OpenHologram()
        {
            if (_open)
            {
                return;
            }

            _open = true;
            _openedAt = Time.unscaledTime;
            if (_mount == Mount.Projector && _animator != null)
            {
                _animator.SetBool(OpenParameter, true);
            }
            else
            {
                _unfold = 0f;
                _folding = false;
            }

            RrSound.Play(PopUpClip, transform.position + Vector3.up * 1.5f, PopVolume);
            if (_hum == null)
            {
                _hum = RrSound.Loop(HumClip, transform, HumVolume);
            }

            SetRaycasters(true);
            _refreshAt = 0f;
        }

        private void CloseHologram()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            if (_mount == Mount.Projector && _animator != null)
            {
                _animator.SetBool(OpenParameter, false);
            }
            else
            {
                _unfold = 0f;
                _folding = true;
            }

            RrSound.Play(PopDownClip, transform.position + Vector3.up * 1.5f, PopVolume);
            RrSound.Stop(_hum);
            _hum = null;
            SetHover(null);
            SetRaycasters(false);
        }

        /// <summary>Closes the hologram (Rec Room's close animation and sound) and destroys the console.</summary>
        public void Close()
        {
            if (_closing)
            {
                return;
            }

            _closing = true;
            CloseHologram();
            Destroy(gameObject, _mount == Mount.Projector ? 1.2f : UnfoldSeconds + 0.1f);
        }

        /// <summary>Re-reads the spec now.</summary>
        public void Refresh()
        {
            _refreshAt = 0f;
            if (_panel != null)
            {
                RefreshNow();
            }
        }

        private void SetRaycasters(bool on)
        {
            if (_raycaster != null)
            {
                _raycaster.enabled = on;
            }

            if (_tracked != null)
            {
                _tracked.enabled = on;
            }
        }

        // ---- presses ----

        /// <summary>Presses START as a player would (the harness' hook); false when START is greyed.</summary>
        public bool Press(string how = "test")
        {
            RefreshNow();
            if (!_model.StartEnabled)
            {
                RrLog.Info("Console", "START (" + how + ") on " + Title + ": greyed (" + _model.Start + ", " + _members.Count + " players, " + _model.Status + ")");
                return false;
            }

            DoStart(how);
            return true;
        }

        public void PressMode()
        {
            DoMode();
        }

        public void PressMap()
        {
            DoMap();
        }

        private string Title
        {
            get { return string.IsNullOrEmpty(_spec.Title) ? "the console" : _spec.Title; }
        }

        private void Act(Action action)
        {
            float now = Time.unscaledTime;
            if (now - _lastAction < ActionDebounce || !_open)
            {
                return;
            }

            _lastAction = now;
            action();
        }

        private void DoStart(string how)
        {
            RefreshNow();
            if (!_model.StartEnabled || _spec.OnStart == null)
            {
                return;
            }

            RrLog.Info("Console", "START pressed on " + Title + " (" + how + ", " + _members.Count + " players, label " + _model.Start + ")");
            try
            {
                _spec.OnStart(RrNet.LocalId);
            }
            catch (Exception e)
            {
                RrLog.Error("Console", "the START callback of " + Title + " failed", e);
            }

            _refreshAt = Time.unscaledTime + 0.15f;
        }

        private void DoMode()
        {
            if (_spec.Modes == null || _spec.Modes.Length == 0 || _spec.OnMode == null)
            {
                return;
            }

            int now = _spec.ModeIndex != null ? _spec.ModeIndex() : 0;
            int next = ((now + 1) % _spec.Modes.Length + _spec.Modes.Length) % _spec.Modes.Length;
            RrTools.Once("Console", "mode:" + Title, "mode toggle on " + Title + ": " + Safe(_spec.Modes, now) + " -> " + Safe(_spec.Modes, next));
            try
            {
                _spec.OnMode(next);
            }
            catch (Exception e)
            {
                RrLog.Error("Console", "the mode callback of " + Title + " failed", e);
            }

            _refreshAt = Time.unscaledTime + 0.15f;
        }

        private void DoMap()
        {
            if (_spec.Maps == null || _spec.Maps.Length == 0 || _spec.OnMap == null)
            {
                return;
            }

            int now = _spec.MapIndex != null ? _spec.MapIndex() : 0;
            int next = ((now + 1) % _spec.Maps.Length + _spec.Maps.Length) % _spec.Maps.Length;
            RrTools.Once("Console", "map:" + Title, "map button on " + Title + ": " + Safe(_spec.Maps, now) + " -> " + Safe(_spec.Maps, next));
            try
            {
                _spec.OnMap(next);
            }
            catch (Exception e)
            {
                RrLog.Error("Console", "the map callback of " + Title + " failed", e);
            }

            _refreshAt = Time.unscaledTime + 0.15f;
        }

        private static string Safe(string[] list, int i)
        {
            return list != null && i >= 0 && i < list.Length ? list[i] : "?";
        }

        // ---- every frame ----

        private void Update()
        {
            if (!_booted || _panel == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!_closing && now >= _proximityAt)
            {
                _proximityAt = now + 0.25f;
                Proximity();
            }

            if (_unfold >= 0f)
            {
                _unfold += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_unfold / UnfoldSeconds));
                float k = _folding ? 1f - u : u;
                float s = FixedWidth / RrGameConsolePanel.Width;
                _panel.Rect.localScale = new Vector3(s, s * Mathf.Max(0.01f, k), s);
                if (_unfold >= UnfoldSeconds)
                {
                    _unfold = -1f;
                }
            }

            if (_mount == Mount.Projector && !_measured && _open && now - _openedAt > 1.5f)
            {
                Measure();
            }

            if (now >= _refreshAt)
            {
                _refreshAt = now + RefreshSeconds;
                RefreshNow();
            }

            if (!_open)
            {
                return;
            }

            _cameraAt -= Time.unscaledDeltaTime;
            if (_cameraAt <= 0f)
            {
                _cameraAt = 0.5f;
                var cam = RrPlayer.Camera;
                if (_panel.Canvas.worldCamera != cam)
                {
                    _panel.Canvas.worldCamera = cam;
                }
            }

            UpdatePointer();
        }

        private void Proximity()
        {
            Vector3 feet;
            try
            {
                feet = RrPlayer.FeetPosition;
            }
            catch (Exception e)
            {
                feet = transform.position;
                RrLog.WarnOnce("Console", "feet", "RrPlayer.FeetPosition failed: " + e.Message);
            }

            Vector3 d = feet - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (!_open && dist <= OpenRadius)
            {
                OpenHologram();
            }
            else if (_open && dist > CloseRadius)
            {
                CloseHologram();
            }
        }

        /// <summary>
        /// Once the projector's Open clip has run: where did the panel end up? Logged once (the measured size and pose
        /// of Rec Room's hologram); a panel that is tiny, huge or not upright is re-mounted on its own, and one that
        /// reads from the wrong side is turned round.
        /// </summary>
        private void Measure()
        {
            _measured = true;
            float width, height, upness, facing;
            Vector3 centre;
            Corners(out width, out height, out centre, out upness, out facing);
            Vector3 vs = _viewRoot != null ? _viewRoot.lossyScale : Vector3.zero;
            string before = "projected panel " + width.ToString("0.00") + " x " + height.ToString("0.00") + " m, centre " + transform.InverseTransformPoint(centre).ToString("F2") +
                            " (marker-local), up " + upness.ToString("0.00") + ", reads toward +Z " + facing.ToString("0.00") + ", view root scale " + vs.ToString("F4");
            if (width < MinWidth || width > MaxWidth || height < MinWidth * 0.5f || upness < 0.6f)
            {
                RrLog.Warn("Console", before + " - not usable, the panel stands alone");
                MountFixed("the projected pose was " + width.ToString("0.00") + " m wide, up " + upness.ToString("0.00"));
                _unfold = 0f;
                _folding = false;
                return;
            }

            if (facing < 0f)
            {
                _panel.Rect.localRotation = _panel.Rect.localRotation * Quaternion.Euler(0f, 180f, 0f);
                Corners(out width, out height, out centre, out upness, out facing);
                before += "; turned round: reads toward +Z " + facing.ToString("0.00") + ", up " + upness.ToString("0.00");
            }

            RrTools.Once("Console", "measured", "Rec Room's hologram: " + before + ", opened in " + (Time.unscaledTime - _openedAt).ToString("0.0") + " s");
            string blocker;
            if (!IsClear(transform.parent, new Pose(transform.localPosition, transform.localRotation), out blocker))
            {
                RrLog.Warn("Console", Title + " at " + transform.localPosition.ToString("F2") + ": " + blocker + " (the hologram cuts into the room)");
            }
        }

        // ---- placement ----

        /// <summary>
        /// Whether a console at <paramref name="localPose"/> (in <paramref name="parent"/> space) has room: the open
        /// hologram's box (<see cref="ProjectedSize"/> at <see cref="ProjectedCentre"/>, 10 cm margin each side) and the
        /// projector's base touch no Environment collider (walls, pillars, glass; consoles' own colliders aside).
        /// </summary>
        public static bool IsClear(Transform parent, Pose localPose, out string blocker)
        {
            Physics.SyncTransforms();
            Quaternion rot = parent != null ? parent.rotation * localPose.rotation : localPose.rotation;
            Vector3 marker = parent != null ? parent.TransformPoint(localPose.position) : localPose.position;
            Vector3 centre = marker + rot * ProjectedCentre;
            var half = new Vector3(ProjectedSize.x * 0.5f + 0.1f, ProjectedSize.y * 0.5f, 0.08f);
            if (Blocked(Physics.OverlapBox(centre, half, rot, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore), out blocker))
            {
                blocker = "the hologram meets " + blocker;
                return false;
            }

            // The projector itself: 0.7 m square, from 15 cm above the floor (the floor under it is not a blocker).
            if (Blocked(Physics.OverlapBox(marker + rot * new Vector3(0f, 0.65f, 0f), new Vector3(0.35f, 0.5f, 0.35f), rot, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore), out blocker))
            {
                blocker = "the projector meets " + blocker;
                return false;
            }

            blocker = null;
            return true;
        }

        private static bool Blocked(Collider[] hits, out string name)
        {
            name = null;
            if (hits == null)
            {
                return false;
            }

            foreach (var h in hits)
            {
                if (h == null || h.GetComponentInParent<RrGameConsole>() != null)
                {
                    continue;
                }

                var t = h.transform;
                name = (t.parent != null ? t.parent.name + "/" : "") + t.name;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The first of <paramref name="candidates"/> (floor points in <paramref name="parent"/> space, in the caller's
        /// order of preference) where a console facing the viewer has room (<see cref="IsClear"/>), the viewer's eye
        /// (1.6 m above <paramref name="viewerLocal"/>) sees the whole hologram - centre and START row, both sides - past
        /// walls and posts (<see cref="Posts"/>), nothing solid stands within 2.5 m in front of START, and
        /// <paramref name="accept"/> (optional) agrees. False when none does; <paramref name="note"/> says why each
        /// failed, counted.
        /// </summary>
        public static bool FindSpot(Transform parent, Vector3 viewerLocal, IList<Vector3> candidates, Func<Pose, bool> accept, out Pose pose, out string note)
        {
            pose = default(Pose);
            int walls = 0, hidden = 0, refused = 0;
            string firstWall = null;
            Vector3 eye = (parent != null ? parent.TransformPoint(viewerLocal) : viewerLocal) + Vector3.up * 1.6f;
            var posts = Posts(parent);
            if (candidates != null)
            {
                foreach (var c in candidates)
                {
                    Vector3 to = viewerLocal - c;
                    to.y = 0f;
                    var p = new Pose(c, to.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(to.normalized, Vector3.up) : Quaternion.identity);
                    if (accept != null && !accept(p))
                    {
                        refused++;
                        continue;
                    }

                    string blocker;
                    if (!IsClear(parent, p, out blocker))
                    {
                        walls++;
                        firstWall = firstWall ?? blocker;
                        continue;
                    }

                    // Seen whole: the centre, the START row and both sides (80% out) at both heights, from the
                    // viewer's eye; and nothing solid within 2.5 m in front of START (where a player stands to press).
                    Quaternion rot = parent != null ? parent.rotation * p.rotation : p.rotation;
                    Vector3 marker = parent != null ? parent.TransformPoint(p.position) : p.position;
                    Vector3 centre = marker + rot * ProjectedCentre;
                    Vector3 start = centre - Vector3.up * (ProjectedSize.y * 0.36f);
                    Vector3 side = rot * Vector3.right * (ProjectedSize.x * 0.4f);
                    Vector3 front = rot * Vector3.forward * 2.5f;
                    if (Sightless(eye, centre, posts) || Sightless(eye, start, posts) || Sightless(eye, centre + side, posts) || Sightless(eye, centre - side, posts) ||
                        Sightless(eye, start + side, posts) || Sightless(eye, start - side, posts) || Sightless(start + front, start, posts))
                    {
                        hidden++;
                        continue;
                    }

                    pose = p;
                    note = "spot " + c.ToString("F2") + " (candidate " + (refused + walls + hidden + 1) + " of " + candidates.Count + ", " + posts.Count + " posts; before it " + walls + " met walls" +
                           (firstWall != null ? " (first: " + firstWall + ")" : "") + ", " + hidden + " out of sight, " + refused + " refused)";
                    return true;
                }
            }

            note = "no clear spot (" + posts.Count + " posts): " + walls + " met walls" + (firstWall != null ? " (first: " + firstWall + ")" : "") + ", " + hidden + " out of sight, " + refused + " refused";
            return false;
        }

        /// <summary>A wall (collider) or a post (renderer bounds, <see cref="Posts"/>) between two points.</summary>
        private static bool Sightless(Vector3 from, Vector3 to, List<Bounds> posts)
        {
            float length = (to - from).magnitude;
            var hits = Physics.RaycastAll(from, to - from, length, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider.GetComponentInParent<RrGameConsole>() == null)
                {
                    return true;
                }
            }

            if (posts != null && length > 1e-3f)
            {
                var ray = new Ray(from, (to - from) / length);
                foreach (var b in posts)
                {
                    float d;
                    if (b.IntersectRay(ray, out d) && d < length - 0.05f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The room's posts and pillars as renderer bounds: the Ink Space's hull posts are drawn without colliders
        /// of their own (run 2 stood the Charades hologram behind one that no ray met). A post is a renderer at
        /// least 2.5 m tall and 0.15-1.2 m across both ways (lamp cords are thinner, walls wider).
        /// </summary>
        private static List<Bounds> Posts(Transform parent)
        {
            var list = new List<Bounds>();
            if (parent == null)
            {
                return list;
            }

            foreach (var r in parent.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (r == null || !r.enabled || r.GetComponentInParent<RrGameConsole>() != null)
                {
                    continue;
                }

                var s = r.bounds.size;
                if (s.y >= 2.5f && s.x >= 0.15f && s.z >= 0.15f && s.x <= 1.2f && s.z <= 1.2f)
                {
                    list.Add(r.bounds);
                }
            }

            return list;
        }

        private void Corners(out float width, out float height, out Vector3 centre, out float upness, out float facing)
        {
            var c = new Vector3[4];
            _panel.Rect.GetWorldCorners(c);
            width = (c[3] - c[0]).magnitude;
            height = (c[1] - c[0]).magnitude;
            centre = (c[0] + c[2]) * 0.5f;
            upness = height > 1e-5f ? Vector3.Dot((c[1] - c[0]) / height, Vector3.up) : 0f;
            Vector3 normal = Vector3.Cross(c[1] - c[0], c[3] - c[0]).normalized;

            // Cross(up, right) points out of the side the canvas reads from.
            facing = Vector3.Dot(normal, transform.forward);
        }

        // ---- the model ----

        private void RefreshNow()
        {
            if (_panel == null)
            {
                return;
            }

            try
            {
                Fill(_model);
                _panel.Apply(_model);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Console", "refresh:" + Title, "refreshing " + Title + " failed: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private void Fill(RrGameConsolePanel.Model m)
        {
            var s = _spec;
            _members.Clear();
            var list = s.Members != null ? s.Members() : DefaultMembers();
            if (list != null)
            {
                foreach (int id in list)
                {
                    if (!_members.Contains(id))
                    {
                        _members.Add(id);
                    }
                }
            }

            int n = _members.Count;
            int min = Mathf.Max(1, s.MinPlayers);
            bool can = s.CanStart == null || s.CanStart();
            bool enough = n >= min || s.StartBelowMin;
            m.StartEnabled = can && enough && s.OnStart != null;
            m.Title = (string.IsNullOrEmpty(s.Title) ? "REC ROOM" : s.Title).ToUpperInvariant();
            m.Timer = s.Timer != null ? s.Timer() ?? "" : "";
            m.Start = (s.StartLabel != null ? s.StartLabel() : null) ?? "START";
            m.Count = s.MaxPlayers > 0 ? n + " / " + s.MaxPlayers : n.ToString();
            bool hasModes = s.Modes != null && s.Modes.Length > 0;
            if (hasModes)
            {
                int mi = s.ModeIndex != null ? s.ModeIndex() : 0;
                m.Subtitle = Safe(s.Modes, Mathf.Clamp(mi, 0, s.Modes.Length - 1)).ToUpperInvariant();
                if (_panel.ModeButton != null)
                {
                    _panel.ModeButton.interactable = s.OnMode != null && s.Modes.Length > 1 && can;
                }
            }
            else
            {
                m.Subtitle = ((s.Subtitle != null ? s.Subtitle() : null) ?? "WAITING FOR PLAYERS").ToUpperInvariant();
            }

            if (s.Maps != null && s.Maps.Length > 0)
            {
                int mp = s.MapIndex != null ? s.MapIndex() : 0;
                m.Map = "MAP: " + Safe(s.Maps, Mathf.Clamp(mp, 0, s.Maps.Length - 1)).ToUpperInvariant();
                m.MapButton = s.OnMap != null && s.Maps.Length > 1 && can;
            }

            string status = s.Status != null ? s.Status() : null;
            if (status == null)
            {
                // While a game runs (START greyed by CanStart) the start label says it; "1 more player needed" under a
                // countdown (run 3's Dodgeball practice) would read as a reason it cannot start.
                if (!can)
                {
                    status = "";
                }
                else if (n < min)
                {
                    int more = min - n;
                    status = more + (more == 1 ? " MORE PLAYER NEEDED" : " MORE PLAYERS NEEDED");
                }
                else if (s.MaxPlayers > 0 && n > s.MaxPlayers)
                {
                    status = "ROOM IS FULL";
                }
                else
                {
                    status = m.StartEnabled ? "PRESS START WHEN READY" : "";
                }
            }

            m.Status = status.ToUpperInvariant();

            // Rows: grouped by team once teams are known, one list before (the lobby) or without teams.
            m.Rows.Clear();
            int local = RrNet.LocalId;
            bool anyTeam = false;
            var teams = new int[n];
            for (int i = 0; i < n; i++)
            {
                int t = -1;
                if (s.TeamCount > 0)
                {
                    t = s.TeamOf != null ? s.TeamOf(_members[i]) : RrTeams.TeamOf(_members[i]);
                    if (t >= s.TeamCount)
                    {
                        t = -1;
                    }
                }

                teams[i] = t;
                anyTeam |= t >= 0;
            }

            m.NameHeader = "PLAYER";
            m.SideHeader = anyTeam ? "TEAM" : "";
            if (anyTeam)
            {
                for (int t = -1; t < s.TeamCount; t++)
                {
                    int count = 0;
                    for (int i = 0; i < n; i++)
                    {
                        count += teams[i] == t ? 1 : 0;
                    }

                    if (t < 0 && count == 0)
                    {
                        continue;
                    }

                    string head = t < 0 ? "NO TEAM YET" : RrTeams.Name(t).ToUpperInvariant() + " TEAM";
                    m.Rows.Add(new RrGameConsolePanel.Row { Header = true, Name = head, Side = count.ToString(), Team = t });
                    for (int i = 0; i < n; i++)
                    {
                        if (teams[i] == t)
                        {
                            m.Rows.Add(PlayerRow(_members[i], t, local));
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    m.Rows.Add(PlayerRow(_members[i], -1, local));
                }
            }
        }

        private RrGameConsolePanel.Row PlayerRow(int id, int team, int local)
        {
            string name = null;
            try
            {
                name = _spec.NameOf != null ? _spec.NameOf(id) : null;
            }
            catch (Exception)
            {
                name = null;
            }

            if (string.IsNullOrEmpty(name))
            {
                var p = RrNet.Presence(id);
                name = p.Valid && !string.IsNullOrEmpty(p.Nick) ? p.Nick : id == local ? RrPlayer.Nick : "Player " + id;
            }

            return new RrGameConsolePanel.Row { Name = name, Side = id == local ? "YOU" : "", Team = team, Local = id == local };
        }

        private static IReadOnlyList<int> DefaultMembers()
        {
            var all = RrNet.MembersOfMyInstance();
            var with = new List<int>(all.Count);
            foreach (int id in all)
            {
                if (RrIsolation.IsWithMe(id))
                {
                    with.Add(id);
                }
            }

            return with;
        }

        // ---- pointer (as RrRoomListPanel) ----

        private void UpdatePointer()
        {
            bool vr = RrPlayer.InVr;
            RrUiButton over = null;
            Vector3 hit = Vector3.zero;
            bool onPanel = !RrInput.Blocked && RayOnPanel(RrInput.AimRay, _spec.Reach, out hit);
            if (onPanel)
            {
                over = ButtonAt(hit);
            }

            // A free desktop cursor is the EventSystem's (it hovers and clicks through the GraphicRaycaster); a locked
            // one (play mode, the crosshair) is not seen by it, so we hover for it.
            bool freeCursor = !vr && Cursor.lockState == CursorLockMode.None && Cursor.visible;
            SetHover(!vr && !freeCursor ? over : null);
            if (RrInput.UseDown && over != null)
            {
                Click(over, vr ? "trigger" : "click");
            }

            if (vr)
            {
                Poke(0, RrHandSide.Left);
                Poke(1, RrHandSide.Right);
            }
            else
            {
                _poking[0] = _poking[1] = null;
            }
        }

        private void Poke(int i, RrHandSide side)
        {
            Vector3 tip;
            Vector3 palm;
            RrUiButton b = null;
            var rect = _panel.Rect;
            if (RrWatchModel.TryPokePoints(side, out tip, out palm))
            {
                Vector3 local = rect.InverseTransformPoint(tip);
                float depth = local.z * rect.lossyScale.z;
                if (depth > -PokeFront && depth < PokeBack && InsideCanvas(local))
                {
                    b = ButtonAt(tip);
                }
            }

            if (b != null && b != _poking[i])
            {
                Click(b, "poke");
                var hand = RrHand.Get(side);
                if (hand != null)
                {
                    hand.Haptic(0.3f, 0.05f);
                }
            }

            _poking[i] = b;
        }

        private void Click(RrUiButton b, string how)
        {
            if (b == null || Time.unscaledTime - _lastAction < ActionDebounce)
            {
                return;
            }

            if (!b.IsInteractable())
            {
                RrWatchSounds.Down(RrUiSound.Inactive);
                return;
            }

            RrTools.Once("Console", "input:" + how, "console pressed by " + how + " (" + b.name + ")");
            if (b == _panel.StartButton)
            {
                // The click itself (sound, pop) through the button; the action with the input's name.
                RrWatchSounds.Click(b.Sound);
                _lastAction = Time.unscaledTime;
                DoStart(how);
                return;
            }

            ExecuteEvents.Execute(b.gameObject, _ped, ExecuteEvents.pointerClickHandler);
        }

        private void SetHover(RrUiButton b)
        {
            if (b == _hovered)
            {
                return;
            }

            if (_hovered != null)
            {
                ExecuteEvents.Execute(_hovered.gameObject, _ped, ExecuteEvents.pointerExitHandler);
            }

            _hovered = b;
            if (b != null)
            {
                ExecuteEvents.Execute(b.gameObject, _ped, ExecuteEvents.pointerEnterHandler);
            }
        }

        /// <summary>True when the ray reaches the panel's face within reach, nothing solid in front (our own projector aside).</summary>
        public bool ContainsRay(Ray ray)
        {
            Vector3 hit;
            return _open && RayOnPanel(ray, _spec.Reach, out hit);
        }

        private bool RayOnPanel(Ray ray, float reach, out Vector3 hit)
        {
            hit = Vector3.zero;
            var rect = _panel != null ? _panel.Rect : null;
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return false;
            }

            var plane = new Plane(rect.forward, rect.position);
            float d;
            if (!plane.Raycast(ray, out d) || d < 0f || d > reach)
            {
                return false;
            }

            hit = ray.GetPoint(d);
            if (!InsideCanvas(rect.InverseTransformPoint(hit)))
            {
                return false;
            }

            if (d <= 0.05f)
            {
                return true;
            }

            // A wall between the pointer and the panel hides it (the projector's own colliders do not).
            var hits = Physics.RaycastAll(ray, d - 0.03f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider != null && !h.collider.transform.IsChildOf(transform))
                {
                    return false;
                }
            }

            return true;
        }

        private bool InsideCanvas(Vector3 local)
        {
            var r = _panel.Rect.rect;
            return local.x >= r.xMin && local.x <= r.xMax && local.y >= r.yMin && local.y <= r.yMax;
        }

        private RrUiButton ButtonAt(Vector3 world)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var b = _targets[i];
                if (b == null || !b.isActiveAndEnabled)
                {
                    continue;
                }

                var rt = (RectTransform)b.transform;
                if (rt.rect.Contains((Vector2)rt.InverseTransformPoint(world)))
                {
                    return b;
                }
            }

            return null;
        }

        // ---- test hooks ----

        /// <summary>One line: mount, open state, measured size, rows, START.</summary>
        public string Describe()
        {
            string size = "";
            if (_panel != null && _panel.Rect != null)
            {
                float w, h, up, facing;
                Vector3 centre;
                Corners(out w, out h, out centre, out up, out facing);
                size = w.ToString("0.00") + " x " + h.ToString("0.00") + " m at " + transform.InverseTransformPoint(centre).ToString("F2") + " (world " + centre.ToString("F2") + "), up " + up.ToString("0.00") + ", reads +Z " + facing.ToString("0.00");
            }

            return Title + ": mount " + MountMode + (_mountNote.Length > 0 ? " (" + _mountNote + ")" : "") + ", open " + IsOpen + ", " + size + ", players " + _members.Count +
                   " (rows " + (_panel != null ? _panel.RowsShown : 0) + "), START '" + _model.Start + "' " + (_model.StartEnabled ? "enabled" : "greyed") + ", status '" + _model.Status + "', subtitle '" + _model.Subtitle + "'";
        }

        /// <summary>Test: aims our ray at START from <paramref name="distance"/> in front and says which button it meets.</summary>
        public string TestAim(float distance)
        {
            if (_panel == null || _panel.StartButton == null)
            {
                return Title + ": no panel";
            }

            var rt = (RectTransform)_panel.StartButton.transform;
            Vector3 centre = rt.TransformPoint(rt.rect.center);
            Vector3 toViewer = -_panel.Rect.forward;
            var ray = new Ray(centre + toViewer * distance, -toViewer);
            Vector3 hit;
            var b = RayOnPanel(ray, Mathf.Max(_spec.Reach, distance + 1f), out hit) ? ButtonAt(hit) : null;
            return Title + ": ray from " + distance.ToString("0.0") + " m meets " + (b != null ? b.name : "nothing");
        }
    }
}
