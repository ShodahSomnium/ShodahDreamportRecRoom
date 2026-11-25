using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Rec Room's activity door (rr_reccenter.md §2.1.2, §4.1): the official double door `[ActivityDoor]` - the
    /// Charades hull's "Rec Center" exit, the only complete copy in the bundles - cloned once as a template, then
    /// one copy per door with its own sign text, frame colour, hover animation and glow. Walking into the doorway,
    /// clicking it (desktop), pointing and pulling the trigger or touching the leaves (VR) calls
    /// <see cref="RrTravel.GoTo"/> for <see cref="Target"/>.
    ///
    /// Rec Room drove the door with an Animator whose parameters are hashed; its two clips are constant poses
    /// (leaves 0 / +21.44 and 0 / -22.27 degrees, glow alpha 0 / 0.834, 0.25 s transitions), so the Animator is
    /// disabled and the same values are animated here. The leaves swing towards +Z, the side the player stands on.
    /// The door's pivot is the floor centre of the doorway; place it with local +Z pointing into the room.
    ///
    /// Presence is polled, not sensed with a trigger: the avatar's CharacterController is on layer 2 and would not
    /// meet a trigger on our layers. The solid door (layer 23) stops a walking player about a capsule radius in
    /// front of the leaves, so the doorway band reaches 0.6 m out (the research's 0.35 m assumed an open doorway).
    ///
    /// When the bundles cannot give the door (no install, the hull missing) a procedural door with the same
    /// size, hinges and sign is built instead, so rooms never have to care. A door whose activity is not
    /// registered (or <see cref="Locked"/>) does not open and says "COMING SOON" for a moment when used, like Rec
    /// Room's lockedScreenSpaceMessage.
    /// </summary>
    public sealed class RrPortal : MonoBehaviour
    {
        /// <summary>The prefab holding the official double door (B43, container key 84).</summary>
        public const string HullPrefab = "(Charades_InkSpace_MainHull_Sandbox)Variant";
        public const string DoorNode = "[ActivityDoor]";

        /// <summary>Loading this single door makes the recolourable frame material resident (key 2ac).</summary>
        public const string RecolorPrefab = "(Sandbox_RoomDoor)";
        public const string RecolorMaterial = "ActivityDoor_Recolorable_Mat";

        /// <summary>The recolourable atlas' frame texel is grey 0.76: base colour = wanted colour / 0.76 (GUESS, §3.3).</summary>
        public const float FrameTexel = 0.76f;

        /// <summary>Hover sound: none in the data; a soft kiosk blip at -15 dB (§4.5, GUESS).</summary>
        public const string HoverClip = "Kiosk_StateChange01";
        public const float HoverVolume = 0.18f;

        public const float OpenSeconds = 0.25f;
        public const float LeftOpenYaw = 21.44f;
        public const float RightOpenYaw = -22.27f;
        public const float GlowOpenAlpha = 0.834f;

        /// <summary>Hover zone in front of the door (door-local z 0..3, |x| &lt; 1.6).</summary>
        public const float HoverDepth = 3f;
        public const float HoverHalfWidth = 1.6f;

        /// <summary>Doorway band that counts as walking in (door-local |x| &lt; 0.9, z &lt; 0.6).</summary>
        public const float WalkHalfWidth = 0.9f;
        public const float WalkDepth = 0.6f;

        /// <summary>Farthest a click or trigger ray reaches a door.</summary>
        public const float ClickReach = 15f;

        /// <summary>Uses are ignored this long after arriving anywhere (you spawn 1.8 m in front of a door).</summary>
        public const float ArrivalDebounce = 1.5f;

        public const float NoticeSeconds = 1.2f;

        /// <summary>The solid door (root BoxCollider of the Rec Room prefab).</summary>
        public static readonly Vector3 SolidCenter = new Vector3(0f, 1.07228f, 0.04454f);
        public static readonly Vector3 SolidSize = new Vector3(2.04518f, 2.14456f, 0.196021f);

        private static readonly Color GlowTint = new Color(1f, 0.972f, 0.816f, 0f);
        private static readonly Color NoticeColor = new Color(0.992f, 0.510f, 0.302f);
        private static readonly Vector3 LeftHinge = new Vector3(0.906f, 1.008f, 0.094f);
        private static readonly Vector3 RightHinge = new Vector3(-0.905f, 1.028f, 0.094f);

        /// <summary>Where the door goes.</summary>
        public RrActivity Target;

        /// <summary>Instance to join (-1: the fullest joinable one, like Rec Room).</summary>
        public int Instance = -1;

        /// <summary>Sign text (null: the activity's title, upper case).</summary>
        public string Label;

        /// <summary>A locked door does not open; using it shows <see cref="LockedMessage"/>.</summary>
        public bool Locked;
        public string LockedMessage = "COMING SOON";

        /// <summary>False: using the door only raises <see cref="OnUsed"/> (the room decides what happens).</summary>
        public bool Travels = true;

        /// <summary>The door was used (before travelling). Handlers are guarded.</summary>
        public event Action<RrPortal> OnUsed;

        private static readonly List<RrPortal> Live = new List<RrPortal>();
        private static readonly Dictionary<int, Material> FrameMaterials = new Dictionary<int, Material>();
        private static readonly int TintId = Shader.PropertyToID("_TintColor");

        private enum Stage
        {
            None,
            Loading,
            Ready,
            Failed
        }

        private static Stage _stage;
        private static bool _recolorLoading;
        private static int _generation;
        private static float _loadDeadline;
        private static GameObject _templateHolder;
        private static GameObject _template;
        private static Material _recolorSource;
        private static bool _loggedProcedural;
        private static bool _loggedNoRecolor;
        private static int _rayFrame = -1;
        private static RrPortal _pointed;
        private static int _feetFrame = -1;
        private static Vector3 _feet;

        private Color? _frame;
        private bool _built;
        private bool _procedural;
        private Transform _left;
        private Transform _right;
        private Quaternion _leftBase = Quaternion.identity;
        private Quaternion _rightBase = Quaternion.identity;
        private Renderer _glow;
        private MaterialPropertyBlock _block;
        private Light _light;
        private TextMeshPro _sign;
        private TextMeshPro _notice;
        private float _open;
        private float _applied = -1f;
        private float _usedAt = -100f;
        private float _noticeUntil;
        private float _pulseUntil;
        private float _lastHoverSound = -100f;
        private bool _hovered;
        private bool _inDoorway;
        private bool _touching;
        private bool _going;

        /// <summary>Every door that exists now (all rooms; normally only the current room's).</summary>
        public static IReadOnlyList<RrPortal> All
        {
            get { return Live; }
        }

        /// <summary>The Rec Room door template is loaded (false: not yet, or doors are procedural).</summary>
        public static bool TemplateReady
        {
            get { return _stage == Stage.Ready; }
        }

        /// <summary>The door's look is built (template copy or procedural).</summary>
        public bool Built
        {
            get { return _built; }
        }

        /// <summary>This door was built procedurally (the Rec Room door could not be loaded).</summary>
        public bool Procedural
        {
            get { return _procedural; }
        }

        /// <summary>Open (hovered, pointed at or in use) rather than closed.</summary>
        public bool IsOpen
        {
            get { return _open > 0.5f; }
        }

        /// <summary>The sign's text as shown.</summary>
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(Label))
                {
                    return Label;
                }

                var info = RrRooms.Get(Target);
                string title = info != null && !string.IsNullOrEmpty(info.Title) ? info.Title : Target.ToString();
                return title.ToUpperInvariant();
            }
        }

        /// <summary>
        /// A door to <paramref name="target"/> at a local position and yaw under a room (its +Z, after the yaw,
        /// must point into the room). Frame colour null: by the activity's group (§3.3); stock blue for PvP/Sports.
        /// Its look is built as soon as the template is loaded - call <see cref="Prepare"/> in the room's Build
        /// first to have it there before the fade lifts.
        /// </summary>
        public static RrPortal Create(Transform parent, RrActivity target, Vector3 localPos, float yaw, string label = null, Color? frame = null)
        {
            var go = new GameObject("Door " + target);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var portal = go.AddComponent<RrPortal>();
            portal.Target = target;
            portal.Label = label;
            portal._frame = frame ?? FrameColorFor(target);
            return portal;
        }

        /// <summary>The first door leading to <paramref name="target"/> (null: none).</summary>
        public static RrPortal For(RrActivity target)
        {
            for (int i = 0; i < Live.Count; i++)
            {
                if (Live[i] != null && Live[i].Target == target && Live[i].isActiveAndEnabled)
                {
                    return Live[i];
                }
            }

            return null;
        }

        /// <summary>Rec Room's door groups (§3.3): Quests rust, Party purple, Racing green, the dorm warm grey; PvP and Sports keep the stock blue (null).</summary>
        public static Color? FrameColorFor(RrActivity activity)
        {
            switch (activity)
            {
                case RrActivity.GoldenTrophy:
                case RrActivity.CrimsonCauldron:
                case RrActivity.Jumbotron:
                case RrActivity.LostSkulls:
                case RrActivity.Crescendo:
                    return new Color(0.54f, 0.30f, 0.19f);
                case RrActivity.Charades:
                case RrActivity.Midnight:
                case RrActivity.LittleMonsters:
                case RrActivity.PunchUp:
                    return new Color(0.42f, 0.26f, 0.58f);
                case RrActivity.StuntRunner:
                case RrActivity.RecRally:
                    return new Color(0.18f, 0.48f, 0.28f);
                case RrActivity.Dorm:
                    return new Color(0.45f, 0.41f, 0.37f);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Loads the door template (and the recolourable frame material) once; rooms yield on it in Build. Ends
        /// when both are loaded or failed, 60 s at most. Safe to call any number of times.
        /// </summary>
        public static IEnumerator Prepare()
        {
            if (_stage == Stage.None)
            {
                Load();
            }

            while ((_stage == Stage.Loading || _recolorLoading) && Time.realtimeSinceStartup < _loadDeadline)
            {
                yield return null;
            }

            if (_stage == Stage.Loading || _recolorLoading)
            {
                // A load that never answered: doors go procedural / stock blue instead of making every room wait.
                RrLog.Warn("Portal", "the door template did not load within 60 s: " + (_stage == Stage.Loading ? "procedural doors" : "stock frame colours"));
                if (_stage == Stage.Loading)
                {
                    _stage = Stage.Failed;
                }

                _recolorLoading = false;
            }
        }

        public static void Shutdown()
        {
            _generation++;
            _stage = Stage.None;
            _recolorLoading = false;
            if (_templateHolder != null)
            {
                Object.Destroy(_templateHolder);
            }

            _templateHolder = null;
            _template = null;
            _recolorSource = null;
            foreach (var m in FrameMaterials.Values)
            {
                if (m != null)
                {
                    Object.Destroy(m);
                }
            }

            FrameMaterials.Clear();
            Live.Clear();
            _loggedProcedural = false;
            _loggedNoRecolor = false;
            _rayFrame = -1;
            _pointed = null;
            _feetFrame = -1;
        }

        /// <summary>World pose in front of the door facing into the room (arrivals through this door, §4.3).</summary>
        public Pose FrontPose(float distance = 1.8f, float lateral = 0f)
        {
            var t = transform;
            return new Pose(t.position + t.forward * distance + t.right * lateral, t.rotation);
        }

        /// <summary>The door opens and closes again over a second (someone just came through it).</summary>
        public void PlayArrive()
        {
            _pulseUntil = Time.realtimeSinceStartup + 0.5f;
        }

        /// <summary>Uses the door as the player would (how: for the log).</summary>
        public void Use(string how = "script")
        {
            float now = Time.realtimeSinceStartup;
            if (now - _usedAt < 1f)
            {
                return;
            }

            _usedAt = now;
            var info = RrRooms.Get(Target);
            if (Locked || info == null || info.Create == null)
            {
                ShowNotice(LockedMessage);
                RrSound.Play(HoverClip, transform.position + Vector3.up * 1.2f, HoverVolume * 1.4f, 0.8f, 12f);
                RrLog.Info("Portal", "door to " + Target + " is locked (" + (Locked ? "locked" : "not registered") + "), " + how);
                return;
            }

            var handlers = OnUsed;
            if (handlers != null)
            {
                foreach (Action<RrPortal> h in handlers.GetInvocationList())
                {
                    try
                    {
                        h(this);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Portal", "an OnUsed handler of the door to " + Target + " failed", e);
                    }
                }
            }

            if (!Travels || RrTravel.Busy)
            {
                return;
            }

            _going = true;
            RrLog.Info("Portal", "door to " + Target + (Instance >= 0 ? " #" + Instance : "") + " used (" + how + ")");
            RrTravel.GoTo(Target, Instance);
        }

        // ---- lifetime ----

        private void Awake()
        {
            Live.Add(this);
            _block = new MaterialPropertyBlock();

            // The solid door and the click box exist at once; the look follows when the template is loaded.
            var solid = new GameObject("Solid");
            solid.layer = RrLayers.Environment;
            solid.transform.SetParent(transform, false);
            var box = solid.AddComponent<BoxCollider>();
            box.center = SolidCenter;
            box.size = SolidSize;

            var click = new GameObject("Click");
            click.layer = RrLayers.Click;
            click.transform.SetParent(transform, false);
            var clickBox = click.AddComponent<BoxCollider>();
            clickBox.center = new Vector3(0f, 1.07f, 0.15f);
            clickBox.size = new Vector3(2.05f, 2.14f, 0.3f);
            clickBox.excludeLayers = ~0;
        }

        private void Start()
        {
            StartCoroutine(BuildLook());
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (_pointed == this)
            {
                _pointed = null;
            }
        }

        private IEnumerator BuildLook()
        {
            yield return Prepare();
            try
            {
                if (_stage == Stage.Ready && _template != null)
                {
                    FromTemplate();
                }
                else
                {
                    MakeProcedural();
                }
            }
            catch (Exception e)
            {
                RrLog.Error("Portal", "building the door to " + Target + " failed; using the procedural door", e);
                ClearLook();
                try
                {
                    MakeProcedural();
                }
                catch (Exception e2)
                {
                    RrLog.Error("Portal", "the procedural door to " + Target + " failed too", e2);
                }
            }

            try
            {
                _notice = RrBuild.Sign(transform, "Notice", "", new Vector3(0f, 3.1f, 0.12f), Quaternion.Euler(0f, 180f, 0f), 1.5f, NoticeColor, true, 3f);
                _notice.gameObject.SetActive(false);
                _light = RrBuild.PointLight(transform, "Hover Light", new Vector3(0f, 1.4f, 0.9f), new Color(1f, 0.86f, 0.66f), 3.5f, 0f);
                _light.enabled = false;
            }
            catch (Exception e)
            {
                RrLog.Error("Portal", "the door to " + Target + ": notice/light failed", e);
            }

            _built = true;
            _applied = -1f;
            Apply(_open);
        }

        // ---- the template ----

        private static void Load()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                _stage = Stage.Failed;
                RrLog.Warn("Portal", "no Rec Room assets: activity doors are procedural");
                return;
            }

            _stage = Stage.Loading;
            _recolorLoading = true;
            _loadDeadline = Time.realtimeSinceStartup + 60f;
            int generation = _generation;
            float t0 = Time.realtimeSinceStartup;
            assets.LoadPrefab(HullPrefab, (asset, data) =>
            {
                if (generation != _generation)
                {
                    return;
                }

                try
                {
                    MakeTemplate(asset, t0);
                }
                catch (Exception e)
                {
                    _stage = Stage.Failed;
                    RrLog.Error("Portal", "making the door template failed; activity doors are procedural", e);
                }
            });
            assets.LoadPrefab(RecolorPrefab, (asset, data) =>
            {
                if (generation != _generation)
                {
                    return;
                }

                _recolorLoading = false;
                _recolorSource = asset != null ? FindMaterial(asset, RecolorMaterial) : null;
                if (_recolorSource != null)
                {
                    _recolorSource = RrMaterials.Fix(_recolorSource) ?? _recolorSource;
                }

                RrLog.Info("Portal", _recolorSource != null ? "recolourable frame material " + RecolorMaterial + " (" + _recolorSource.shader.name + ")" : "no " + RecolorMaterial + " in " + RecolorPrefab + ": every frame keeps the stock blue");
            });
        }

        private static void MakeTemplate(GameObject hull, float t0)
        {
            var door = hull != null ? FindDeep(hull.transform, DoorNode) : null;
            if (door == null)
            {
                _stage = Stage.Failed;
                RrLog.Warn("Portal", hull == null ? "prefab " + HullPrefab + " not loaded: activity doors are procedural" : DoorNode + " not found in " + HullPrefab + ": activity doors are procedural");
                return;
            }

            _templateHolder = new GameObject("Rr Door Template");
            _templateHolder.SetActive(false);
            _templateHolder.transform.SetParent(RrTravelDriver.Ensure().transform, false);
            _template = RrClone.Clone(door.gameObject, _templateHolder.transform);
            _template.name = "Rec Room Door";
            _template.transform.localPosition = Vector3.zero;
            _template.transform.localRotation = Quaternion.identity;
            foreach (var animator in _template.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            // Every door gets its own solid and click boxes; the prefab's colliders would double them.
            foreach (var c in _template.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            RrMaterials.FixTree(_template);
            RrLayers.Set(_template, RrLayers.Environment);
            int renderers = _template.GetComponentsInChildren<Renderer>(true).Length;
            bool leaves = FindDeep(_template.transform, "ActivityDoor_Left") != null && FindDeep(_template.transform, "ActivityDoor_Right") != null;
            _stage = Stage.Ready;
            RrLog.Info("Portal", "door template from " + HullPrefab + "/" + DoorNode + ": " + renderers + " renderers" + (leaves ? "" : ", leaves not found (no hover swing)") + ", " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }

        private static Material FindMaterial(GameObject root, string name)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && m.name == name)
                    {
                        return m;
                    }
                }
            }

            return null;
        }

        private static Material FrameMaterial(Color wanted)
        {
            if (_recolorSource == null)
            {
                if (!_loggedNoRecolor)
                {
                    _loggedNoRecolor = true;
                    RrLog.Info("Portal", "frame colours unavailable (no " + RecolorMaterial + "): stock blue frames");
                }

                return null;
            }

            int key = (Mathf.RoundToInt(wanted.r * 255f) << 16) | (Mathf.RoundToInt(wanted.g * 255f) << 8) | Mathf.RoundToInt(wanted.b * 255f);
            Material m;
            if (FrameMaterials.TryGetValue(key, out m) && m != null)
            {
                return m;
            }

            m = new Material(_recolorSource) { name = RecolorMaterial + " " + key.ToString("x6") };
            var baseColor = new Color(Mathf.Min(1.3f, wanted.r / FrameTexel), Mathf.Min(1.3f, wanted.g / FrameTexel), Mathf.Min(1.3f, wanted.b / FrameTexel), 1f);
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", baseColor);
            }

            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", baseColor);
            }

            FrameMaterials[key] = m;
            return m;
        }

        // ---- building one door ----

        private void FromTemplate()
        {
            var look = Instantiate(_template, transform, false);
            look.name = "Look";
            look.transform.localPosition = Vector3.zero;
            look.transform.localRotation = Quaternion.identity;
            var root = look.transform;
            _left = FindDeep(root, "ActivityDoor_Left");
            _right = FindDeep(root, "ActivityDoor_Right");
            var glow = FindDeep(root, "ActivityDoor_Glow");
            _glow = glow != null ? glow.GetComponent<Renderer>() : null;
            if (_glow != null && (_glow.sharedMaterial == null || !_glow.sharedMaterial.HasProperty(TintId)))
            {
                // A glow without its additive shader would draw as a solid slab.
                _glow.enabled = false;
                _glow = null;
                RrLog.WarnOnce("Portal", "glow", "the door glow's material has no _TintColor: glow off");
            }

            if (_frame.HasValue)
            {
                var frameNode = FindDeep(root, "ActivityDoor_Frame");
                var frameRenderer = frameNode != null ? frameNode.GetComponent<Renderer>() : null;
                var m = frameRenderer != null ? FrameMaterial(_frame.Value) : null;
                if (m != null)
                {
                    frameRenderer.sharedMaterial = m;

                    // LOD1 is one mesh with the leaves: tinting it would tint the wood, so keep LOD0 at every
                    // distance (the whole door is under 1000 vertices).
                    var visualRoot = root.Find("VisualRoot");
                    var lods = visualRoot != null ? visualRoot.GetComponent<LODGroup>() : null;
                    if (lods != null)
                    {
                        lods.enabled = false;
                    }

                    var lod1 = FindDeep(root, "ActivityDoor_LOD1");
                    var lod1Renderer = lod1 != null ? lod1.GetComponent<Renderer>() : null;
                    if (lod1Renderer != null)
                    {
                        lod1Renderer.enabled = false;
                    }
                }
            }

            // The sign's TextMeshPro is a script and is not copied: a label of ours takes its place and pose.
            var signRoot = FindDeep(root, "[Door_Sign]");
            var textNode = signRoot != null ? FindDeep(signRoot, "Text") : null;
            if (textNode != null)
            {
                var old = textNode.GetComponent<Renderer>();
                if (old != null)
                {
                    old.enabled = false;
                }

                var go = new GameObject("Label");
                go.layer = RrLayers.Environment;
                go.transform.SetParent(textNode, false);
                _sign = go.AddComponent<TextMeshPro>();
                RrText.Style(_sign, true);
                _sign.rectTransform.sizeDelta = new Vector2(44f, 11f);
                _sign.enableAutoSizing = true;
                _sign.fontSizeMin = 50f;
                _sign.fontSizeMax = 100f;
                _sign.fontSize = 100f;
                _sign.color = Color.black;
                _sign.alignment = TextAlignmentOptions.Center;
                _sign.textWrappingMode = TextWrappingModes.Normal;
                _sign.text = DisplayLabel;
            }
            else
            {
                MakeLabel(new Vector3(0f, 2.625f, 0.04f));
            }

            Hinges();
        }

        private void MakeProcedural()
        {
            _procedural = true;
            if (!_loggedProcedural)
            {
                _loggedProcedural = true;
                RrLog.Info("Portal", "activity doors are procedural (Rec Room's door is not available)");
            }

            var look = RrBuild.Group(transform, "Look (procedural)").transform;
            var frameMat = RrLook.Solid(_frame ?? new Color(0.19f, 0.40f, 0.55f));
            var wood = RrLook.Solid(RrLook.Wood);
            RrBuild.Box(look, "Frame L", new Vector3(-0.965f, 1.072f, 0.045f), new Vector3(0.115f, 2.144f, 0.196f), frameMat, false);
            RrBuild.Box(look, "Frame R", new Vector3(0.965f, 1.072f, 0.045f), new Vector3(0.115f, 2.144f, 0.196f), frameMat, false);
            RrBuild.Box(look, "Frame Top", new Vector3(0f, 2.086f, 0.045f), new Vector3(2.045f, 0.116f, 0.196f), frameMat, false);
            RrBuild.Box(look, "Backlight", new Vector3(0f, 1.03f, 0.0f), new Vector3(1.84f, 2.0f, 0.02f), RrLook.Emissive(new Color(1f, 0.985f, 0.9f), 1.2f), false);
            var left = RrBuild.Group(look, "ActivityDoor_Left", LeftHinge).transform;
            RrBuild.Box(left, "Leaf", new Vector3(-0.45f, 0.02f, 0f), new Vector3(0.9f, 1.99f, 0.06f), wood, false);
            var right = RrBuild.Group(look, "ActivityDoor_Right", RightHinge).transform;
            RrBuild.Box(right, "Leaf", new Vector3(0.45f, 0f, 0f), new Vector3(0.9f, 1.99f, 0.06f), wood, false);
            RrBuild.Box(look, "Sign", new Vector3(0f, 2.625f, 0f), new Vector3(1.07f, 0.27f, 0.07f), RrLook.Emissive(new Color(0.991f, 0.897f, 0.808f), 0.4f), false);
            RrLayers.Set(look.gameObject, RrLayers.Environment);
            _left = left;
            _right = right;
            MakeLabel(new Vector3(0f, 2.625f, 0.04f));
            Hinges();
        }

        private void MakeLabel(Vector3 localPos)
        {
            _sign = RrBuild.Sign(transform, "Label", DisplayLabel, localPos, Quaternion.Euler(0f, 180f, 0f), 2f, Color.black, true, 1.02f);
            _sign.rectTransform.sizeDelta = new Vector2(1.02f, 0.26f);
            _sign.enableAutoSizing = true;
            _sign.fontSizeMin = 0.9f;
            _sign.fontSizeMax = 2.2f;
        }

        private void Hinges()
        {
            _leftBase = _left != null ? _left.localRotation : Quaternion.identity;
            _rightBase = _right != null ? _right.localRotation : Quaternion.identity;
        }

        private void ClearLook()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Look") || child.name == "Label")
                {
                    Destroy(child.gameObject);
                }
            }

            _left = null;
            _right = null;
            _glow = null;
            _sign = null;
        }

        // ---- every frame ----

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            Vector3 local = transform.InverseTransformPoint(Feet());
            bool level = Mathf.Abs(local.y) < 2f;
            bool near = level && local.z > -0.3f && local.z < HoverDepth && Mathf.Abs(local.x) < HoverHalfWidth;
            bool doorway = level && local.z > -0.3f && local.z < WalkDepth && Mathf.Abs(local.x) < WalkHalfWidth;
            bool pointed = Pointed() == this;
            bool touching = RrPlayer.InVr && HandTouching();
            bool locked = Locked || RrRooms.Get(Target) == null;
            bool hover = (near || pointed) && !locked;

            bool ready = !RrTravel.Busy && !RrLoadingScreen.Showing && now - RrTravel.ArrivedAt >= ArrivalDebounce;
            if (ready)
            {
                if (doorway && !_inDoorway)
                {
                    Use("walked in");
                }
                else if (pointed && RrInput.UseDown && !RrInput.PointerOverUi)
                {
                    Use(RrPlayer.InVr ? "trigger" : "click");
                }
                else if (touching && !_touching)
                {
                    Use("touched");
                }
            }

            _inDoorway = doorway;
            _touching = touching;

            if (hover && !_hovered && now - _lastHoverSound > 0.6f && ready)
            {
                _lastHoverSound = now;
                RrSound.Play(HoverClip, transform.position + Vector3.up * 1.2f, HoverVolume, 1f, 12f);
            }

            _hovered = hover;
            if (_going && !RrTravel.Busy && now - _usedAt > 2f)
            {
                _going = false;
            }

            bool open = hover || _going || now < _pulseUntil;
            _open = Mathf.MoveTowards(_open, open ? 1f : 0f, Time.unscaledDeltaTime / OpenSeconds);
            Apply(_open);

            if (_notice != null && _notice.gameObject.activeSelf && now >= _noticeUntil)
            {
                _notice.gameObject.SetActive(false);
            }
        }

        private void Apply(float t)
        {
            if (Mathf.Approximately(t, _applied))
            {
                return;
            }

            _applied = t;
            float s = t * t * (3f - 2f * t);
            if (_left != null)
            {
                _left.localRotation = _leftBase * Quaternion.Euler(0f, LeftOpenYaw * s, 0f);
            }

            if (_right != null)
            {
                _right.localRotation = _rightBase * Quaternion.Euler(0f, RightOpenYaw * s, 0f);
            }

            if (_glow != null)
            {
                bool on = s > 0.003f;
                _glow.enabled = on;
                if (on)
                {
                    _glow.GetPropertyBlock(_block);
                    _block.SetColor(TintId, new Color(GlowTint.r, GlowTint.g, GlowTint.b, GlowOpenAlpha * s));
                    _glow.SetPropertyBlock(_block);
                }
            }

            if (_light != null)
            {
                _light.enabled = s > 0.01f;
                _light.intensity = 0.9f * s;
            }
        }

        private void ShowNotice(string text)
        {
            if (_notice == null)
            {
                return;
            }

            _notice.text = text;
            _notice.gameObject.SetActive(true);
            _noticeUntil = Time.realtimeSinceStartup + NoticeSeconds;
        }

        private bool HandTouching()
        {
            return Touches(RrHand.Left) || Touches(RrHand.Right);
        }

        private bool Touches(RrHand hand)
        {
            if (hand == null || !hand.Tracked)
            {
                return false;
            }

            Vector3 p = transform.InverseTransformPoint(hand.Position);
            return Mathf.Abs(p.x) < 1f && p.y > 0.2f && p.y < 2.1f && p.z > -0.05f && p.z < 0.35f;
        }

        /// <summary>The player's feet, read once a frame for every door.</summary>
        private static Vector3 Feet()
        {
            if (_feetFrame != Time.frameCount)
            {
                _feetFrame = Time.frameCount;
                try
                {
                    _feet = RrPlayer.FeetPosition;
                }
                catch (Exception e)
                {
                    _feet = new Vector3(0f, -10000f, 0f);
                    RrLog.WarnOnce("Portal", "feet", "RrPlayer.FeetPosition failed: " + e.Message);
                }
            }

            return _feet;
        }

        /// <summary>The door under the aim ray (one raycast a frame for every door; walls in front block it).</summary>
        private static RrPortal Pointed()
        {
            if (_rayFrame == Time.frameCount)
            {
                return _pointed;
            }

            _rayFrame = Time.frameCount;
            _pointed = null;
            try
            {
                RaycastHit hit;
                if (Physics.Raycast(RrInput.AimRay, out hit, ClickReach, (1 << RrLayers.Click) | (1 << RrLayers.Environment), QueryTriggerInteraction.Ignore)
                    && hit.collider.gameObject.layer == RrLayers.Click)
                {
                    _pointed = hit.collider.GetComponentInParent<RrPortal>();
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Portal", "ray", "the door aim ray failed: " + e.Message);
            }

            return _pointed;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
