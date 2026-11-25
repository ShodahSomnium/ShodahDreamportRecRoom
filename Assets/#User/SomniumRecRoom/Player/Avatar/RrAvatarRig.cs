using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>
    /// One classic Rec Room avatar - floating head, floating torso, floating hands, no arms or legs - built from the
    /// player's own install and posed every frame from a head pose and two hand poses (DevNotes/iter15/avatar.md).
    ///
    /// <b>Parts</b> (all Addressables containers in the install): Head_Shapes_fbx (head, ears and the face card),
    /// Torso_Shapes_fbx (the bare torso, shown when no top is worn), Hand_fbx twice (the left one mirrored the way
    /// Rec Room's (Player) prefab mirrors it: holder p (-0.02,0,0), r (-0.5,0.5,-0.5,0.5), s (1,1,-1)). Their prefab
    /// materials are placeholders, so the skin is a copy of Rec Room's Skin_Mat (AG/Avatar) in the chosen tone and the
    /// face is a copy of its "Face" material (AG/AvatarFace) with the chosen eye and mouth textures at the classic
    /// anchors (eyes L (0.6,0.65) R (0.3,0.65) scale 0.28, mouth (0.5,0.07) scale 0.5).
    ///
    /// <b>Items</b> are Rec Room's "(BB_...)" outfit prefabs: single-bone skinned meshes on "Head.Bone" or
    /// "Torso.Bone" (wrist items on the hand rig). Every bean part and item is authored with its bone at the part's
    /// centre and a bind pose that only turns Z-up into Y-up, so binding all of them to our own Head and Torso
    /// transforms (bones re-mapped by name) puts every item where Rec Room puts it. Dye = a copy of the item's
    /// material with its mask colours (_Red/_Green/_Blue/_Base_Col) set from the palette; a design = one of the item's
    /// own "..._Mat" materials. Only LOD0 is kept (LOD groups removed: avatars are few and LOD0 is light).
    ///
    /// Everything here is our own object under "Rr Avatars"; nothing touches the Somnium avatar. Layer Default, no
    /// colliders, so it is drawn by the player's camera and by mirrors and is never hit by rays.
    /// </summary>
    internal sealed class RrAvatarRig
    {
        /// <summary>Head centre relative to the eyes, in the head's frame (eyes sit 1.4 cm above and ~10 cm in front of the centre).</summary>
        public static readonly Vector3 EyeToHead = new Vector3(0f, -0.015f, -0.085f);

        /// <summary>Head bone to torso bone, in the torso's yaw frame (1.68 - 1.15 in Rec Room's authoring frame).</summary>
        public static readonly Vector3 HeadToTorso = new Vector3(0f, -0.53f, -0.02f);

        private static readonly Vector3 HolderLeftPos = new Vector3(-0.02f, 0f, 0f);
        private static readonly Vector3 HolderRightPos = new Vector3(0.02f, 0f, 0f);
        private static readonly Quaternion HolderRot = new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f);

        private static readonly Vector4 LeftEyeAnchor = new Vector4(0.6f, 0.65f, 0.28f, 0.28f);
        private static readonly Vector4 RightEyeAnchor = new Vector4(0.3f, 0.65f, 0.28f, 0.28f);
        private static readonly Vector4 MouthAnchor = new Vector4(0.5f, 0.07f, 0.5f, 0.5f);

        public readonly GameObject Root;
        public readonly Transform Head;
        public readonly Transform Torso;
        public readonly Transform HandL;
        public readonly Transform HandR;
        private readonly Transform _holderL;
        private readonly Transform _holderR;
        public readonly string Who;

        private RrAvatarOutfit _outfit = new RrAvatarOutfit();
        private readonly string[] _slotKeys = new string[RrAvatarOutfit.SlotCount];
        private readonly GameObject[] _items = new GameObject[RrAvatarOutfit.SlotCount];
        private readonly int[] _itemTokens = new int[RrAvatarOutfit.SlotCount];
        private readonly List<Material> _owned = new List<Material>();
        private readonly List<Material>[] _itemMats = new List<Material>[RrAvatarOutfit.SlotCount];

        /// <summary>Each item's own materials as spawned, so a dye or design can always go back to them.</summary>
        private readonly Dictionary<Renderer, Material[]>[] _originals = new Dictionary<Renderer, Material[]>[RrAvatarOutfit.SlotCount];

        private Material _skin;
        private Material _face;
        private Texture _eye, _blink, _mouth;
        private int _eyeStyle = -1, _mouthStyle = -1;
        private GameObject _headPart, _torsoPart, _handPartL, _handPartR;
        private readonly List<Renderer> _torsoRenderers = new List<Renderer>();
        private readonly Dictionary<string, Transform> _rightBones = new Dictionary<string, Transform>();
        private readonly Transform[] _fingersL = new Transform[3], _fingersR = new Transform[3];
        private readonly Quaternion[] _fingerBaseL = new Quaternion[3], _fingerBaseR = new Quaternion[3];
        private int _pendingCore;
        private bool _destroyed;
        private bool _coreReady;
        private float _torsoYaw;
        private bool _hasYaw;
        private float _curlL, _curlR;
        private float _nextBlink;
        private float _blinkUntil;
        private bool _hiddenForCamera;
        private readonly List<Renderer> _headRenderers = new List<Renderer>();
        private bool _headRenderersDirty = true;

        public RrAvatarRig(Transform parent, string who)
        {
            Who = who;
            Root = new GameObject("Rr Avatar " + who);
            Root.transform.SetParent(parent, false);
            Head = new GameObject("Head").transform;
            Head.SetParent(Root.transform, false);
            Torso = new GameObject("Torso").transform;
            Torso.SetParent(Root.transform, false);
            HandL = new GameObject("Hand L").transform;
            HandL.SetParent(Root.transform, false);
            HandR = new GameObject("Hand R").transform;
            HandR.SetParent(Root.transform, false);
            _holderL = new GameObject("Mesh").transform;
            _holderL.SetParent(HandL, false);
            _holderL.localPosition = HolderLeftPos;
            _holderL.localRotation = HolderRot;
            _holderL.localScale = new Vector3(1f, 1f, -1f);
            _holderR = new GameObject("Mesh").transform;
            _holderR.SetParent(HandR, false);
            _holderR.localPosition = HolderRightPos;
            _holderR.localRotation = HolderRot;
            _nextBlink = Time.time + UnityEngine.Random.Range(2f, 5f);
            BuildCore();
        }

        public bool Destroyed
        {
            get { return _destroyed; }
        }

        public bool CoreReady
        {
            get { return _coreReady; }
        }

        public RrAvatarOutfit Outfit
        {
            get { return _outfit; }
        }

        public void SetVisible(bool visible)
        {
            if (!_destroyed && Root.activeSelf != visible)
            {
                Root.SetActive(visible);
            }
        }

        public bool Visible
        {
            get { return !_destroyed && Root.activeSelf; }
        }

        // ------------------------------------------------------------------ loading

        /// <summary>Loads under the shared scope (0): an avatar outlives the room it was dressed in.</summary>
        private static void Shared(Action load)
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                return;
            }

            int was = assets.Scope;
            assets.Scope = 0;
            try
            {
                load();
            }
            finally
            {
                assets.Scope = was;
            }
        }

        /// <summary>The entry with this name and class, preferring a bundle whose file starts with the hint.</summary>
        internal static bool FindEntry(string name, int classId, string bundleHint, out RrEntry entry)
        {
            entry = default(RrEntry);
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                return false;
            }

            var all = assets.Catalog.Find(name, classId);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].LoaderBundle >= 0 && (bundleHint == null || assets.Catalog.Bundles[all[i].Bundle].File.StartsWith(bundleHint, StringComparison.Ordinal)))
                {
                    entry = all[i];
                    return true;
                }
            }

            return assets.TryFind(name, classId, out entry);
        }

        private void Spawn(string prefab, Transform parent, Action<GameObject> done)
        {
            var options = new RrSpawnOptions
            {
                Layer = RrLayers.Default, StripColliders = true, StripRigidbodies = true, StripAudioSources = true, StripLights = true, StripParticles = true
            };
            Shared(() => RrSpawn.Spawn(prefab, parent, Vector3.zero, Quaternion.identity, inst =>
            {
                if (inst == null)
                {
                    RrLog.WarnOnce("Avatar", "spawn:" + prefab, "could not load " + prefab + " from Rec Room's bundles");
                    done(null);
                    return;
                }

                if (_destroyed)
                {
                    Object.Destroy(inst.gameObject);
                    done(null);
                    return;
                }

                Prepare(inst.gameObject);
                done(inst.gameObject);
            }, options));
        }

        /// <summary>LOD0 only, no LOD groups or animators, no shadow card or pointing hand.</summary>
        private static void Prepare(GameObject go)
        {
            foreach (var lod in go.GetComponentsInChildren<LODGroup>(true))
            {
                Object.Destroy(lod);
            }

            foreach (var a in go.GetComponentsInChildren<Animator>(true))
            {
                Object.Destroy(a);
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.IndexOf("_LOD1", StringComparison.Ordinal) >= 0 || n.IndexOf("_LOD2", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("Shadow", StringComparison.Ordinal) >= 0 || n.IndexOf("Pointing", StringComparison.Ordinal) >= 0)
                {
                    r.gameObject.SetActive(false);
                    continue;
                }

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null)
                {
                    smr.updateWhenOffscreen = false;
                }
            }
        }

        private void BuildCore()
        {
            _pendingCore = 6;
            Shared(() =>
            {
                RrEntry e;
                if (FindEntry("Skin_Mat", 21, "bd72059a", out e))
                {
                    RrAssets.Instance.Load<Material>(e, m =>
                    {
                        if (!_destroyed)
                        {
                            _skin = Own(m != null ? new Material(RrMaterials.Fix(m)) : FallbackLit());
                            if (_skin != null)
                            {
                                _skin.name = "Rr Avatar Skin";
                            }

                            ApplySkin();
                        }

                        CoreDone();
                    });
                }
                else
                {
                    _skin = Own(FallbackLit());
                    CoreDone();
                }

                if (FindEntry("Face", 21, "f124b399", out e))
                {
                    RrAssets.Instance.Load<Material>(e, m =>
                    {
                        if (!_destroyed && m != null)
                        {
                            _face = Own(new Material(RrMaterials.Fix(m)));
                            _face.name = "Rr Avatar Face";
                            ApplySkin();
                            ApplyFace();
                        }

                        CoreDone();
                    });
                }
                else
                {
                    CoreDone();
                }
            });

            Spawn("Head_Shapes_fbx", Head, go =>
            {
                _headPart = go;
                if (go != null)
                {
                    Bind(go, false);
                    _headRenderersDirty = true;
                }

                ApplySkin();
                CoreDone();
            });
            Spawn("Torso_Shapes_fbx", Torso, go =>
            {
                _torsoPart = go;
                if (go != null)
                {
                    Bind(go, false);
                    go.GetComponentsInChildren(false, _torsoRenderers);
                }

                ApplySkin();
                UpdateTorsoShown();
                CoreDone();
            });
            Spawn("Hand_fbx", _holderL, go =>
            {
                _handPartL = go;
                if (go != null)
                {
                    FindFingers(go.transform, _fingersL, _fingerBaseL);
                }

                ApplySkin();
                CoreDone();
            });
            Spawn("Hand_fbx", _holderR, go =>
            {
                _handPartR = go;
                if (go != null)
                {
                    FindFingers(go.transform, _fingersR, _fingerBaseR);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (!_rightBones.ContainsKey(t.name))
                        {
                            _rightBones[t.name] = t;
                        }
                    }
                }

                ApplySkin();
                CoreDone();
            });
        }

        private void CoreDone()
        {
            _pendingCore--;
            if (_pendingCore > 0 || _destroyed)
            {
                return;
            }

            _coreReady = true;

            // Items wait for the hands (wrist items bind to the right hand's rig).
            ApplyItems(true);
        }

        private Material Own(Material m)
        {
            if (m != null)
            {
                _owned.Add(m);
            }

            return m;
        }

        private static Material FallbackLit()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            return lit != null ? new Material(lit) : null;
        }

        private static void FindFingers(Transform root, Transform[] fingers, Quaternion[] bases)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int k = t.name == "Hand.Finger.1" ? 0 : t.name == "Hand.Finger.2" ? 1 : t.name == "Hand.Finger.3" ? 2 : -1;
                if (k >= 0)
                {
                    fingers[k] = t;
                    bases[k] = t.localRotation;
                }
            }
        }

        /// <summary>Re-points a part's or item's skinned meshes at our bones (by bone name).</summary>
        private void Bind(GameObject go, bool rightHand)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                bool changed = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    var m = Map(bones[i], rightHand);
                    if (m != bones[i])
                    {
                        bones[i] = m;
                        changed = true;
                    }
                }

                if (changed)
                {
                    smr.bones = bones;
                }

                var root = Map(smr.rootBone, rightHand);
                if (root == null && bones.Length > 0)
                {
                    root = bones[0];
                }

                if (root != smr.rootBone)
                {
                    smr.rootBone = root;
                }

                // Culling bounds in the root bone's space: every bean part and item lies within ~0.4 m of its bone, so
                // a generous box around the bone never culls a visible piece (the authored bounds may be relative to
                // the prefab's own renderer transform, which no longer means anything here).
                smr.localBounds = new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f));
            }
        }

        private Transform Map(Transform bone, bool rightHand)
        {
            if (bone == null)
            {
                return null;
            }

            string n = bone.name;
            if (n == "Head.Bone")
            {
                return Head;
            }

            if (n == "Torso.Bone")
            {
                return Torso;
            }

            Transform t;
            if (rightHand && _rightBones.TryGetValue(n, out t))
            {
                return t;
            }

            return bone;
        }

        // ------------------------------------------------------------------ outfit

        /// <summary>Dresses the avatar: skin and face at once, items as they load (only changed slots reload).</summary>
        public void Apply(RrAvatarOutfit outfit)
        {
            if (_destroyed || outfit == null)
            {
                return;
            }

            _outfit = outfit.Copy();
            ApplySkin();
            ApplyFace();
            if (_coreReady)
            {
                ApplyItems(false);
            }
        }

        private void ApplySkin()
        {
            if (_destroyed)
            {
                return;
            }

            Color c = RrAvatarOutfit.Skin(_outfit.skin);
            if (_skin != null)
            {
                SetAll(_skin, c);
            }

            if (_face != null)
            {
                SetAll(_face, c);
                if (_skin != null && _skin.HasProperty("_BaseMap") && _face.HasProperty("_BaseMap") && _face.GetTexture("_BaseMap") == null)
                {
                    _face.SetTexture("_BaseMap", _skin.GetTexture("_BaseMap"));
                }
            }

            SkinParts(_headPart);
            SkinParts(_torsoPart);
            SkinParts(_handPartL);
            SkinParts(_handPartR);
        }

        private void SkinParts(GameObject part)
        {
            if (part == null || _skin == null)
            {
                return;
            }

            foreach (var r in part.GetComponentsInChildren<Renderer>(true))
            {
                bool face = r.gameObject.name == "Face";
                var want = face ? _face : _skin;
                if (want == null)
                {
                    if (face)
                    {
                        r.enabled = false;
                    }

                    continue;
                }

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != want)
                    {
                        mats[i] = want;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }

                if (face && !r.enabled)
                {
                    r.enabled = true;
                }
            }
        }

        /// <summary>Every AG/Avatar colour channel (and URP's base colour) to one colour: the mask then cannot matter.</summary>
        private static void SetAll(Material m, Color c)
        {
            c.a = 1f;
            if (m.HasProperty("_Base_Col"))
            {
                m.SetColor("_Base_Col", c);
            }

            if (m.HasProperty("_Red_Col"))
            {
                m.SetColor("_Red_Col", c);
            }

            if (m.HasProperty("_Green_Col"))
            {
                m.SetColor("_Green_Col", c);
            }

            if (m.HasProperty("_Blue_Col"))
            {
                m.SetColor("_Blue_Col", c);
            }

            if (m.HasProperty("_Dirt_Col"))
            {
                m.SetColor("_Dirt_Col", new Color(c.r, c.g, c.b, 0f));
            }

            if (m.HasProperty("_BaseColor") && !m.HasProperty("_Red_Col"))
            {
                m.SetColor("_BaseColor", c);
            }
        }

        /// <summary>A dye on an item: the main (red) channel in the colour, trims a shade darker, details lighter.</summary>
        private static void Dye(Material m, Color c)
        {
            if (m.HasProperty("_Red_Col"))
            {
                m.SetColor("_Red_Col", c);
                if (m.HasProperty("_Green_Col"))
                {
                    m.SetColor("_Green_Col", new Color(c.r * 0.72f, c.g * 0.72f, c.b * 0.72f, 1f));
                }

                if (m.HasProperty("_Blue_Col"))
                {
                    m.SetColor("_Blue_Col", Color.Lerp(c, Color.white, 0.35f));
                }

                if (m.HasProperty("_Base_Col"))
                {
                    m.SetColor("_Base_Col", c);
                }
            }
            else if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", c);
            }
        }

        private void ApplyFace()
        {
            if (_destroyed || _face == null || !RrAvatarCatalog.Ready)
            {
                return;
            }

            _face.SetVector("_LeftEye", LeftEyeAnchor);
            _face.SetVector("_RightEye", RightEyeAnchor);
            _face.SetVector("_Mouth", MouthAnchor);
            var eyes = RrAvatarCatalog.Eyes;
            var mouths = RrAvatarCatalog.Mouths;
            int ei = eyes.Count > 0 ? Mathf.Clamp(_outfit.eyes, 0, eyes.Count - 1) : -1;
            int mi = mouths.Count > 0 ? Mathf.Clamp(_outfit.mouth, 0, mouths.Count - 1) : -1;
            if (ei >= 0 && ei != _eyeStyle)
            {
                _eyeStyle = ei;
                var style = eyes[ei];
                Shared(() =>
                {
                    RrAssets.Instance.Load<Texture2D>(style.Eye, t =>
                    {
                        if (_destroyed || _eyeStyle != ei || t == null)
                        {
                            return;
                        }

                        _eye = t;
                        SetEyes(_eye);
                    });
                    if (style.Blink.Name != null)
                    {
                        RrAssets.Instance.Load<Texture2D>(style.Blink, t =>
                        {
                            if (!_destroyed && _eyeStyle == ei)
                            {
                                _blink = t;
                            }
                        });
                    }
                    else
                    {
                        _blink = null;
                    }
                });
            }

            if (mi >= 0 && mi != _mouthStyle)
            {
                _mouthStyle = mi;
                var style = mouths[mi];
                Shared(() => RrAssets.Instance.Load<Texture2D>(style.Eye, t =>
                {
                    if (_destroyed || _mouthStyle != mi || t == null)
                    {
                        return;
                    }

                    _mouth = t;
                    if (_face != null)
                    {
                        _face.SetTexture("_MouthTexture", t);
                    }
                }));
            }
        }

        private void SetEyes(Texture t)
        {
            if (_face != null && t != null)
            {
                _face.SetTexture("_LeftEyeTexture", t);
                _face.SetTexture("_RightEyeTexture", t);
            }
        }

        private static string SlotKey(RrAvatarItemChoice c)
        {
            return string.IsNullOrEmpty(c.item) ? "" : c.item + "|" + c.color + "|" + c.style;
        }

        private void ApplyItems(bool force)
        {
            if (!RrAvatarCatalog.Ready)
            {
                return;
            }

            for (int i = 0; i < RrAvatarOutfit.SlotCount; i++)
            {
                var choice = _outfit.items[i];
                string key = SlotKey(choice);
                if (!force && key == _slotKeys[i])
                {
                    continue;
                }

                // Same item, other colour or design: only the materials change.
                string oldItem = _slotKeys[i] != null && _slotKeys[i].Length > 0 ? _slotKeys[i].Substring(0, _slotKeys[i].IndexOf('|')) : "";
                _slotKeys[i] = key;
                if (!force && _items[i] != null && oldItem == choice.item && key.Length > 0)
                {
                    DressItem(i, _items[i], choice, ++_itemTokens[i]);
                    continue;
                }

                ClearItem(i);
                if (key.Length == 0)
                {
                    continue;
                }

                var item = RrAvatarCatalog.Find((RrAvatarSlot)i, choice.item);
                if (item == null)
                {
                    continue;
                }

                int token = ++_itemTokens[i];
                int slot = i;
                Transform parent = ParentFor((RrAvatarSlot)i);
                Spawn(item.Prefab, parent, go =>
                {
                    if (go == null)
                    {
                        return;
                    }

                    if (_destroyed || token != _itemTokens[slot])
                    {
                        Object.Destroy(go);
                        return;
                    }

                    bool right = slot == (int)RrAvatarSlot.Wrist;
                    Bind(go, right);
                    _items[slot] = go;
                    if (slot == (int)RrAvatarSlot.Top)
                    {
                        UpdateTorsoShown();
                    }

                    _headRenderersDirty = true;
                    DressItem(slot, go, _outfit.items[slot], token);
                });
            }

            UpdateTorsoShown();
        }

        private Transform ParentFor(RrAvatarSlot slot)
        {
            switch (slot)
            {
                case RrAvatarSlot.Top:
                case RrAvatarSlot.Neck:
                case RrAvatarSlot.Back:
                case RrAvatarSlot.Belt:
                    return Torso;
                case RrAvatarSlot.Wrist:
                    return _holderR;
                default:
                    return Head;
            }
        }

        private void ClearItem(int slot)
        {
            _itemTokens[slot]++;
            if (_items[slot] != null)
            {
                Object.Destroy(_items[slot]);
                _items[slot] = null;
            }

            FreeItemMats(slot);
            _originals[slot] = null;
            _headRenderersDirty = true;
            if (slot == (int)RrAvatarSlot.Top)
            {
                UpdateTorsoShown();
            }
        }

        private void FreeItemMats(int slot)
        {
            var list = _itemMats[slot];
            if (list == null)
            {
                return;
            }

            foreach (var m in list)
            {
                if (m != null)
                {
                    _owned.Remove(m);
                    Object.Destroy(m);
                }
            }

            list.Clear();
        }

        /// <summary>The item's materials for its colour and design (async when a design material must load).</summary>
        private void DressItem(int slot, GameObject go, RrAvatarItemChoice choice, int token)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            // The prefab's own materials, remembered on first dress so a later "original colours" can go back.
            var originals = _originals[slot];
            if (originals == null)
            {
                originals = new Dictionary<Renderer, Material[]>();
                _originals[slot] = originals;
            }

            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null && !originals.ContainsKey(renderers[r]))
                {
                    originals[renderers[r]] = renderers[r].sharedMaterials;
                }
            }

            Action<Material> finish = design =>
            {
                if (_destroyed || go == null || token != _itemTokens[slot])
                {
                    return;
                }

                FreeItemMats(slot);
                if (_itemMats[slot] == null)
                {
                    _itemMats[slot] = new List<Material>();
                }

                var copies = new Dictionary<Material, Material>();
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null)
                    {
                        continue;
                    }

                    var mats = originals[renderers[r]];
                    var now = new Material[mats.Length];
                    for (int k = 0; k < mats.Length; k++)
                    {
                        Material m = design != null ? design : mats[k];
                        if (m != null && choice.color > 0)
                        {
                            Material copy;
                            if (!copies.TryGetValue(m, out copy))
                            {
                                copy = new Material(m) { name = m.name + " (dyed)" };
                                Dye(copy, RrAvatarOutfit.Dye(choice.color));
                                copies[m] = copy;
                                _itemMats[slot].Add(copy);
                                _owned.Add(copy);
                            }

                            m = copy;
                        }

                        now[k] = m;
                    }

                    renderers[r].sharedMaterials = now;
                }
            };

            if (string.IsNullOrEmpty(choice.style))
            {
                finish(null);
                return;
            }

            RrEntry e;
            if (!FindEntry(choice.style, 21, null, out e))
            {
                finish(null);
                return;
            }

            Shared(() => RrAssets.Instance.Load<Material>(e, m => finish(m != null ? RrMaterials.Fix(m) : null)));
        }

        private void UpdateTorsoShown()
        {
            bool top = _items[(int)RrAvatarSlot.Top] != null;
            for (int i = 0; i < _torsoRenderers.Count; i++)
            {
                if (_torsoRenderers[i] != null)
                {
                    _torsoRenderers[i].enabled = !top;
                }
            }
        }

        // ------------------------------------------------------------------ pose

        /// <summary>
        /// Poses the avatar. <paramref name="eye"/> / <paramref name="look"/> = the player's view (camera, or the Bridge
        /// head anchor); hands in Rec Room's hand frame (+Z fingers, +Y thumb, +X out of the left palm), null = rest
        /// beside the torso (desktop players, untracked hands). The torso hangs under the head and turns toward the
        /// view's heading with a short lag (Rec Room's steering follow is 0.04-0.2 s).
        /// </summary>
        public void Pose(Vector3 eye, Quaternion look, Pose? left, Pose? right, float dt)
        {
            if (_destroyed)
            {
                return;
            }

            Vector3 fwd = look * Vector3.forward;
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            float yaw = flat.sqrMagnitude > 1e-6f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : look.eulerAngles.y;
            if (!_hasYaw || dt <= 0f)
            {
                _torsoYaw = yaw;
                _hasYaw = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-dt / 0.15f);
                _torsoYaw += Mathf.DeltaAngle(_torsoYaw, yaw) * k;
            }

            Quaternion yawRot = Quaternion.Euler(0f, _torsoYaw, 0f);
            Vector3 headPos = eye + look * EyeToHead;
            Head.SetPositionAndRotation(headPos, look);

            // A little of the head's pitch leans the torso, as Rec Room's bean does.
            float pitch = Mathf.DeltaAngle(0f, look.eulerAngles.x);
            Quaternion torsoRot = yawRot * Quaternion.Euler(Mathf.Clamp(pitch, -40f, 60f) * 0.2f, 0f, 0f);
            Vector3 torsoPos = headPos + yawRot * HeadToTorso;
            Torso.SetPositionAndRotation(torsoPos, torsoRot);

            Pose rest;
            if (left.HasValue)
            {
                HandL.SetPositionAndRotation(left.Value.position, left.Value.rotation);
            }
            else
            {
                rest = RestHand(torsoPos, yawRot, true);
                HandL.SetPositionAndRotation(rest.position, rest.rotation);
            }

            if (right.HasValue)
            {
                HandR.SetPositionAndRotation(right.Value.position, right.Value.rotation);
            }
            else
            {
                rest = RestHand(torsoPos, yawRot, false);
                HandR.SetPositionAndRotation(rest.position, rest.rotation);
            }

            Blink();
        }

        /// <summary>A hand hanging beside the torso, palm in, fingers down and a little forward.</summary>
        public static Pose RestHand(Vector3 torso, Quaternion yaw, bool left)
        {
            return new Pose(torso + yaw * new Vector3(left ? -0.24f : 0.24f, -0.12f, 0.1f), yaw * Quaternion.Euler(65f, left ? 8f : -8f, 0f));
        }

        /// <summary>Finger curl 0 (open) .. 1 (fist), smoothed.</summary>
        public void SetGrip(float left, float right, float dt)
        {
            float k = dt > 0f ? 1f - Mathf.Exp(-dt / 0.06f) : 1f;
            _curlL = Mathf.Lerp(_curlL, Mathf.Clamp01(left), k);
            _curlR = Mathf.Lerp(_curlR, Mathf.Clamp01(right), k);
            Curl(_fingersL, _fingerBaseL, _curlL);
            Curl(_fingersR, _fingerBaseR, _curlR);
        }

        private static readonly float[] CurlAngles = { 65f, 80f, 55f };

        private static void Curl(Transform[] fingers, Quaternion[] bases, float curl)
        {
            for (int i = 0; i < fingers.Length; i++)
            {
                if (fingers[i] != null)
                {
                    fingers[i].localRotation = bases[i] * Quaternion.Euler(-CurlAngles[i] * curl, 0f, 0f);
                }
            }
        }

        private void Blink()
        {
            if (_face == null || _eye == null)
            {
                return;
            }

            float now = Time.time;
            if (_blinkUntil > 0f && now >= _blinkUntil)
            {
                _blinkUntil = 0f;
                SetEyes(_eye);
            }

            if (now >= _nextBlink)
            {
                _nextBlink = now + UnityEngine.Random.Range(2.5f, 6f);
                if (_blink != null)
                {
                    _blinkUntil = now + 0.12f;
                    SetEyes(_blink);
                }
            }
        }

        // ------------------------------------------------------------------ first person

        /// <summary>
        /// Hides the head (and what is worn on it) from a camera inside it - the player's own view - and shows it to any
        /// other camera (mirrors). Only our own renderers.
        /// </summary>
        public void ForCamera(Vector3 cameraPosition)
        {
            if (_destroyed || !Root.activeInHierarchy)
            {
                return;
            }

            bool hide = (cameraPosition - Head.position).sqrMagnitude < 0.28f * 0.28f;
            if (hide == _hiddenForCamera && !_headRenderersDirty)
            {
                return;
            }

            if (_headRenderersDirty)
            {
                _headRenderersDirty = false;
                Head.GetComponentsInChildren(true, _headRenderers);
            }

            _hiddenForCamera = hide;
            for (int i = 0; i < _headRenderers.Count; i++)
            {
                if (_headRenderers[i] != null)
                {
                    _headRenderers[i].forceRenderingOff = hide;
                }
            }
        }

        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            for (int i = 0; i < _itemTokens.Length; i++)
            {
                _itemTokens[i]++;
            }

            if (Root != null)
            {
                Object.Destroy(Root);
            }

            foreach (var m in _owned)
            {
                if (m != null)
                {
                    Object.Destroy(m);
                }
            }

            _owned.Clear();
        }
    }
}
