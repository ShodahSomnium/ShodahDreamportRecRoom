using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// A Rec Room mirror (the Dorm's and the Rec Center hallway's <c>(Dorm_FreeMirror)</c>, the Jumbotron Armory's dressing
    /// mirror): Thad's planar mirror (<c>ThirdParty/ThadMirror/Mirror.cs</c>, shader "Thad/Mirrors/StandardMirror" from
    /// <c>Assets/Thad/Mirrors</c>, the user's package) - a per-eye reflection, correct in stereo, rendered only while the
    /// glass is seen. Its material comes from RrMain's shader carriers ("Default Mirror"), set up as Thad's "High Quality
    /// Mirror" prefab: layer 4, skybox background, the prefab's reflected layers.
    ///
    /// Rec Room's mirrors reflect only when you are in front of them; so does this one: within <see cref="Range"/> m in front
    /// of the glass and <see cref="Enabled"/> (the Dorm's MIRROR ON / OFF), else a stylised glass (pale blue-grey with a
    /// highlight band) at no cost. Without the shader (a scene with no carrier) it stays the stylised glass.
    /// The glass faces +z of this transform. The Somnium avatar is untouched: it appears in the reflection as the client
    /// draws it.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    public sealed class RrMirror : MonoBehaviour
    {
        public const string ShaderName = "Thad/Mirrors/StandardMirror";

        /// <summary>The mirror glass's layer (Unity's built-in "Water", as Thad's prefabs; the mirror leaves it out of its reflection).</summary>
        public const int GlassLayer = 4;

        /// <summary>Thad's "High Quality Mirror" culling mask: every layer but 3, 4, 5 (UI), 7, 21, 29 and 31.</summary>
        public const int ReflectedLayers = 1608515399;

        public float Range = 6.5f;

        /// <summary>The mirror's switch (off = stylised glass only).</summary>
        public bool Enabled = true;

        public bool Reflecting { get; private set; }

        /// <summary>Frames the reflection was on and the glass in view (diagnostics).</summary>
        public int FramesLive { get; private set; }

        /// <summary>Thad's mirror is in place (its shader was found).</summary>
        public bool Available
        {
            get { return _mirror != null; }
        }

        private GameObject _live;
        private GameObject _still;
        private Renderer _liveRenderer;
        private ThadMirror.Mirror _mirror;
        private Material _liveMaterial;
        private Material _stillMaterial;
        private Texture2D _stylised;
        private static bool _loggedMissing;

        /// <summary>A mirror of width x height m under parent, its glass facing +z of localRot.</summary>
        public static RrMirror Create(Transform parent, string name, Vector3 localPos, Quaternion localRot, float width, float height, float range = 6.5f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var m = go.AddComponent<RrMirror>();
            m.Range = range;
            m.Build(width, height);
            return m;
        }

        private void Build(float width, float height)
        {
            // Unity's quad shows its -z side: turned 180 degrees it faces +z of this transform.
            _stylised = StylisedTexture();
            _stillMaterial = new Material(RrLook.Unlit(Color.white)) { name = "Rr Mirror (stylised)" };
            _stillMaterial.SetTexture("_BaseMap", _stylised);
            _stillMaterial.SetColor("_BaseColor", Color.white);
            _still = Quad("Mirror Glass (stylised)", width, height, _stillMaterial);

            var template = Template();
            if (template == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    RrLog.Warn("Mirror", "no \"" + ShaderName + "\" material among RrMain's shader carriers: mirrors stay stylised glass");
                }

                return;
            }

            _liveMaterial = new Material(template) { name = "Rr Mirror (Thad)" };
            _live = Quad("Mirror Glass (reflection)", width, height, _liveMaterial);
            _live.layer = GlassLayer;
            _liveRenderer = _live.GetComponent<Renderer>();
            // Thad's Mirror reads its renderer and material in Awake (now), so both are in place first.
            _mirror = _live.AddComponent<ThadMirror.Mirror>();
            _mirror.CullingMask = ReflectedLayers;
            _mirror.Background = ThadMirror.MirrorBackground.Skybox;
            _live.SetActive(false);
        }

        private GameObject Quad(string name, float width, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>The "Default Mirror" material the scene carries into the upload (Shader.Find fails for the world's own shaders).</summary>
        private static Material Template()
        {
            var main = RrMain.Instance;
            if (main == null || main.ShaderCarriers == null)
            {
                return null;
            }

            foreach (var m in main.ShaderCarriers)
            {
                if (m != null && m.shader != null && m.shader.name == ShaderName && m.shader.isSupported)
                {
                    return m;
                }
            }

            return null;
        }

        private void LateUpdate()
        {
            var cam = RrPlayer.Camera;
            bool want = false;
            if (Enabled && _mirror != null && cam != null)
            {
                Vector3 eye = cam.transform.position;
                Vector3 p = transform.position;
                float d = Vector3.Dot(eye - p, transform.forward);
                want = d > 0.05f && d < Range && (eye - p).sqrMagnitude < Range * Range * 1.5f;
            }

            if (want != Reflecting)
            {
                Reflecting = want;
                if (_live != null)
                {
                    _live.SetActive(want);
                }

                _still.SetActive(!want);
            }

            if (Reflecting && _liveRenderer != null && _liveRenderer.isVisible)
            {
                FramesLive++;
            }
        }

        /// <summary>A small vertical gradient with a diagonal highlight band: reads as glass from across the room.</summary>
        private static Texture2D StylisedTexture()
        {
            const int w = 32, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Rr Mirror Stylised", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            var top = new Color(0.83f, 0.90f, 0.95f);
            var bottom = new Color(0.62f, 0.70f, 0.78f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = Color.Lerp(bottom, top, y / (float)(h - 1));
                    float band = Mathf.Abs((x / (float)w) - (y / (float)h) * 0.9f - 0.05f);
                    if (band < 0.08f)
                    {
                        c = Color.Lerp(c, Color.white, 0.45f * (1f - band / 0.08f));
                    }

                    px[y * w + x] = c;
                }
            }

            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        private void OnDestroy()
        {
            // Thad's Mirror frees its own render texture; its renderer's material copy (made in its Awake) is ours to free.
            if (_liveRenderer != null && _liveRenderer.sharedMaterial != null && _liveRenderer.sharedMaterial != _liveMaterial)
            {
                Destroy(_liveRenderer.sharedMaterial);
            }

            if (_liveMaterial != null)
            {
                Destroy(_liveMaterial);
            }

            if (_stillMaterial != null)
            {
                Destroy(_stillMaterial);
            }

            if (_stylised != null)
            {
                Destroy(_stylised);
            }
        }

        /// <summary>RrMain.OnDestroy: the one-time log flag.</summary>
        public static void Shutdown()
        {
            _loggedMissing = false;
        }
    }
}
