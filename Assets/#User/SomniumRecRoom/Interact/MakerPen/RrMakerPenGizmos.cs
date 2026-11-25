using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen
{
    /// <summary>
    /// Measuring maker-room objects (whatever the room runtime built for them): the world bounds of their
    /// renderers, and the tight box in a given rotation frame (the outline, shape placement, cloning offsets).
    /// Particle, line and trail renderers are ignored; meshes are measured from their own bounds so a rotated
    /// object still gets a tight box.
    /// </summary>
    public static class RrMakerPenMeasure
    {
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly Vector3[] Corners = new Vector3[8];

        private static bool Skip(Renderer r)
        {
            return r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer;
        }

        /// <summary>The union of the object's renderer bounds; false when it has none yet.</summary>
        public static bool WorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default(Bounds);
            if (go == null)
            {
                return false;
            }

            bool any = false;
            Renderers.Clear();
            go.GetComponentsInChildren(false, Renderers);
            foreach (var r in Renderers)
            {
                if (Skip(r))
                {
                    continue;
                }

                var b = r.bounds;
                if (b.size.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = b;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            Renderers.Clear();
            return any;
        }

        /// <summary>
        /// The object's tight box in the frame (<paramref name="origin"/>, <paramref name="rotation"/>): its centre in
        /// the world and its size along the frame's axes. Meshes use their mesh bounds, other renderers their world
        /// bounds. False when it has no renderers yet.
        /// </summary>
        public static bool Oriented(GameObject go, Vector3 origin, Quaternion rotation, out Vector3 centre, out Vector3 size)
        {
            centre = origin;
            size = Vector3.zero;
            if (go == null)
            {
                return false;
            }

            var inv = Quaternion.Inverse(rotation);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            bool any = false;
            Renderers.Clear();
            go.GetComponentsInChildren(false, Renderers);
            foreach (var r in Renderers)
            {
                if (Skip(r))
                {
                    continue;
                }

                Bounds local;
                Transform t = r.transform;
                bool meshSpace = true;
                var mf = r is MeshRenderer ? r.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null)
                {
                    local = mf.sharedMesh.bounds;
                }
                else if (r is SkinnedMeshRenderer)
                {
                    local = ((SkinnedMeshRenderer)r).localBounds;
                    var root = ((SkinnedMeshRenderer)r).rootBone;
                    if (root != null)
                    {
                        t = root;
                    }
                }
                else
                {
                    local = r.bounds;
                    meshSpace = false;
                }

                if (local.size.sqrMagnitude < 1e-10f)
                {
                    continue;
                }

                Vector3 c = local.center, e = local.extents;
                for (int i = 0; i < 8; i++)
                {
                    var p = new Vector3(c.x + ((i & 1) != 0 ? e.x : -e.x), c.y + ((i & 2) != 0 ? e.y : -e.y), c.z + ((i & 4) != 0 ? e.z : -e.z));
                    Corners[i] = meshSpace ? t.TransformPoint(p) : p;
                }

                for (int i = 0; i < 8; i++)
                {
                    var q = inv * (Corners[i] - origin);
                    min = Vector3.Min(min, q);
                    max = Vector3.Max(max, q);
                }

                any = true;
            }

            Renderers.Clear();
            if (!any)
            {
                return false;
            }

            centre = origin + rotation * ((min + max) * 0.5f);
            size = max - min;
            return true;
        }

        /// <summary>The object has a collider (pointing finds it by physics; otherwise by its bounds).</summary>
        public static bool HasCollider(GameObject go)
        {
            return go != null && go.GetComponentInChildren<Collider>() != null;
        }
    }

    /// <summary>
    /// The build tools' local visuals (nobody else sees them): the pointer laser from the pen tip, the outline box
    /// around the object under the tip or ray, and the see-through box of a shape being drawn. All live under the
    /// room (as the pen's palette and cursor do) and are unlit lines / glass, so they read in any lighting.
    /// </summary>
    public sealed class RrMakerPenGizmos
    {
        public const float LaserWidth = 0.004f;
        public const float OutlineWidth = 0.006f;

        private readonly Transform _home;
        private LineRenderer _laser;
        private LineRenderer _outline;
        private GameObject _preview;
        private Renderer _previewRenderer;
        private GameObject _cursor;
        private Renderer _cursorRenderer;
        private int _laserLook = -1, _outlineLook = -1, _previewLook = -1, _cursorLook = -1;
        private readonly Vector3[] _box = new Vector3[16];

        public RrMakerPenGizmos(Transform home)
        {
            _home = home;
        }

        private LineRenderer Line(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_home, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.shadowCastingMode = ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.numCapVertices = 2;
            l.numCornerVertices = 1;
            l.alignment = LineAlignment.View;
            return l;
        }

        private static int Look(Color c)
        {
            return (Mathf.RoundToInt(c.r * 255f) << 16) ^ (Mathf.RoundToInt(c.g * 255f) << 8) ^ Mathf.RoundToInt(c.b * 255f);
        }

        /// <summary>The pointer from the pen tip (VR): to the hit point, or its full reach.</summary>
        public void Laser(bool on, Vector3 from, Vector3 to, Color colour)
        {
            if (!on)
            {
                if (_laser != null && _laser.gameObject.activeSelf)
                {
                    _laser.gameObject.SetActive(false);
                }

                return;
            }

            if (_laser == null)
            {
                _laser = Line("Maker Pen Laser");
            }

            if (!_laser.gameObject.activeSelf)
            {
                _laser.gameObject.SetActive(true);
            }

            int look = Look(colour);
            if (look != _laserLook)
            {
                _laserLook = look;
                _laser.sharedMaterial = RrLook.Unlit(colour);
            }

            _laser.positionCount = 2;
            _laser.SetPosition(0, from);
            _laser.SetPosition(1, to);
            _laser.startWidth = LaserWidth;
            _laser.endWidth = LaserWidth * 0.5f;
        }

        /// <summary>An outline box (centre, rotation, size) in the mode's colour; <paramref name="width"/> 0 = default.</summary>
        public void Outline(bool on, Vector3 centre, Quaternion rotation, Vector3 size, Color colour, float width = 0f)
        {
            if (!on)
            {
                if (_outline != null && _outline.gameObject.activeSelf)
                {
                    _outline.gameObject.SetActive(false);
                }

                return;
            }

            if (_outline == null)
            {
                _outline = Line("Maker Pen Outline");
                _outline.loop = false;
            }

            if (!_outline.gameObject.activeSelf)
            {
                _outline.gameObject.SetActive(true);
            }

            int look = Look(colour);
            if (look != _outlineLook)
            {
                _outlineLook = look;
                _outline.sharedMaterial = RrLook.Unlit(colour);
            }

            var e = size * 0.5f + Vector3.one * 0.01f;
            // the 8 corners: bottom loop 0-3, top loop 4-7; one path over all 12 edges (some twice)
            Vector3 c0 = centre + rotation * new Vector3(-e.x, -e.y, -e.z);
            Vector3 c1 = centre + rotation * new Vector3(e.x, -e.y, -e.z);
            Vector3 c2 = centre + rotation * new Vector3(e.x, -e.y, e.z);
            Vector3 c3 = centre + rotation * new Vector3(-e.x, -e.y, e.z);
            Vector3 c4 = centre + rotation * new Vector3(-e.x, e.y, -e.z);
            Vector3 c5 = centre + rotation * new Vector3(e.x, e.y, -e.z);
            Vector3 c6 = centre + rotation * new Vector3(e.x, e.y, e.z);
            Vector3 c7 = centre + rotation * new Vector3(-e.x, e.y, e.z);
            _box[0] = c0;
            _box[1] = c1;
            _box[2] = c2;
            _box[3] = c3;
            _box[4] = c0;
            _box[5] = c4;
            _box[6] = c5;
            _box[7] = c1;
            _box[8] = c5;
            _box[9] = c6;
            _box[10] = c2;
            _box[11] = c6;
            _box[12] = c7;
            _box[13] = c3;
            _box[14] = c7;
            _box[15] = c4;
            _outline.positionCount = _box.Length;
            _outline.SetPositions(_box);
            float w = width > 0f ? width : OutlineWidth;
            _outline.startWidth = w;
            _outline.endWidth = w;
        }

        /// <summary>The see-through box of a shape being drawn (Rec Room shows the shape growing; this is its box).</summary>
        public void Preview(bool on, Vector3 centre, Quaternion rotation, Vector3 size, Color colour)
        {
            if (!on)
            {
                if (_preview != null && _preview.activeSelf)
                {
                    _preview.SetActive(false);
                }

                return;
            }

            if (_preview == null)
            {
                _preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _preview.name = "Maker Pen Shape Preview";
                Object.Destroy(_preview.GetComponent<Collider>());
                _preview.transform.SetParent(_home, false);
                _previewRenderer = _preview.GetComponent<Renderer>();
                _previewRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _previewRenderer.receiveShadows = false;
            }

            if (!_preview.activeSelf)
            {
                _preview.SetActive(true);
            }

            var glass = new Color(colour.r, colour.g, colour.b, 0.45f);
            int look = Look(colour);
            if (look != _previewLook)
            {
                _previewLook = look;
                _previewRenderer.sharedMaterial = RrLook.Glass(glass);
            }

            _preview.transform.SetPositionAndRotation(centre, rotation);
            _preview.transform.localScale = new Vector3(Mathf.Max(0.005f, size.x), Mathf.Max(0.005f, size.y), Mathf.Max(0.005f, size.z));
        }

        /// <summary>A small ball where the pen creates (the tip, or the crosshair's spot), in the current colour.</summary>
        public void Cursor(bool on, Vector3 point, float radius, Color colour)
        {
            if (!on)
            {
                if (_cursor != null && _cursor.activeSelf)
                {
                    _cursor.SetActive(false);
                }

                return;
            }

            if (_cursor == null)
            {
                _cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _cursor.name = "Maker Pen Build Cursor";
                Object.Destroy(_cursor.GetComponent<Collider>());
                _cursor.transform.SetParent(_home, false);
                _cursorRenderer = _cursor.GetComponent<Renderer>();
                _cursorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                _cursorRenderer.receiveShadows = false;
            }

            if (!_cursor.activeSelf)
            {
                _cursor.SetActive(true);
            }

            int look = Look(colour);
            if (look != _cursorLook)
            {
                _cursorLook = look;
                _cursorRenderer.sharedMaterial = RrLook.Emissive(colour, 0.6f);
            }

            _cursor.transform.position = point;
            _cursor.transform.localScale = Vector3.one * Mathf.Max(0.008f, radius * 2f);
        }

        public void HideAll()
        {
            Cursor(false, Vector3.zero, 0f, Color.white);
            Laser(false, Vector3.zero, Vector3.zero, Color.white);
            Outline(false, Vector3.zero, Quaternion.identity, Vector3.zero, Color.white);
            Preview(false, Vector3.zero, Quaternion.identity, Vector3.zero, Color.white);
        }

        public void Destroy()
        {
            if (_cursor != null)
            {
                Object.Destroy(_cursor);
            }

            _cursor = null;
            if (_laser != null)
            {
                Object.Destroy(_laser.gameObject);
            }

            if (_outline != null)
            {
                Object.Destroy(_outline.gameObject);
            }

            if (_preview != null)
            {
                Object.Destroy(_preview);
            }

            _laser = null;
            _outline = null;
            _preview = null;
        }
    }
}
