using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Procedural pieces of the hub: boxes whose UVs are in metres / tile size, so Rec Room's tiled surface
    /// textures (planks, paint grain, carpet, brick) keep their real-world scale on walls of any size - Unity's cube
    /// primitive would stretch one texture repeat over a 25 m wall. Also convex prisms (the curved far end's floor
    /// and ceiling, the round kiosk rug) and <see cref="RrRcBatch"/>, which merges many small boxes of one surface
    /// (window mullions, beams, rails) into a single renderer. Every mesh made here is unique to the room and is
    /// added to the room's owned list, which destroys them with the room.
    /// </summary>
    internal static class RrRcGeo
    {
        public static List<Object> Owned;

        /// <summary>A box from min to max corners (room-local), with a BoxCollider on layer 23 when asked.</summary>
        public static GameObject Slab(Transform parent, string name, Vector3 min, Vector3 max, RrRcSurface s, bool collider = true)
        {
            var center = (min + max) * 0.5f;
            var size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z));
            return Box(parent, name, center, Quaternion.identity, size, s, collider);
        }

        /// <summary>A box by centre, rotation and size.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size, RrRcSurface s, bool collider = true)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            var v = new List<Vector3>(24);
            var n = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var t = new List<int>(36);
            // Axis-aligned boxes take room-local UVs so flush neighbours continue the planks; rotated ones use their own.
            var anchor = rotation == Quaternion.identity ? center : Vector3.zero;
            AppendBox(v, n, uv, t, Vector3.zero, Quaternion.identity, size, Tile(s), anchor);
            go.AddComponent<MeshFilter>().sharedMesh = MakeMesh("Rr Rc Box", v, n, uv, t);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = s.Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (collider)
            {
                go.AddComponent<BoxCollider>().size = size;
            }

            return go;
        }

        /// <summary>
        /// An invisible solid (layer 23): stair ramps under visible steps, railing guards, window panes' backstops.
        /// The Somnium controller climbs a smooth ramp more reliably than 17 cm step boxes (rr_latest_places.md §3.3.1).
        /// </summary>
        public static GameObject Solid(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>
        /// A ramp collider from a (bottom, on the slope's surface) to b (top), width w, 0.2 m thick under the surface.
        /// </summary>
        public static GameObject Ramp(Transform parent, string name, Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            var center = (a + b) * 0.5f - rot * Vector3.up * 0.1f;
            return Solid(parent, name, center, rot, new Vector3(width, 0.2f, d.magnitude));
        }

        /// <summary>
        /// A vertical prism over a convex polygon in the XZ plane (room-local metres), from y0 to y1: floors and
        /// ceilings of the curved end, round rugs. Top and bottom carry X/Z metre UVs anchored in room space, so a
        /// prism floor continues the planks of the box floors next to it. Optional MeshCollider (layer 23).
        /// </summary>
        public static GameObject Prism(Transform parent, string name, IList<Vector2> poly, float y0, float y1, RrRcSurface s, bool collider = true)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            var tile = Tile(s);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int count = poly.Count;
            var centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                centroid += poly[i];
            }

            centroid /= Mathf.Max(1, count);
            for (int face = 0; face < 2; face++)
            {
                float y = face == 0 ? y1 : y0;
                var normal = face == 0 ? Vector3.up : Vector3.down;
                int b = v.Count;
                for (int i = 0; i < count; i++)
                {
                    v.Add(new Vector3(poly[i].x, y, poly[i].y));
                    n.Add(normal);
                    uv.Add(new Vector2(poly[i].x / tile.x, poly[i].y / tile.y));
                }

                for (int i = 1; i < count - 1; i++)
                {
                    Tri(t, v, b, b + i, b + i + 1, normal);
                }
            }

            for (int i = 0; i < count; i++)
            {
                var p0 = poly[i];
                var p1 = poly[(i + 1) % count];
                var edge = p1 - p0;
                float len = edge.magnitude;
                if (len < 1e-4f)
                {
                    continue;
                }

                var outward = new Vector3(edge.y, 0f, -edge.x) / len;
                var mid = (p0 + p1) * 0.5f - centroid;
                if (outward.x * mid.x + outward.z * mid.y < 0f)
                {
                    outward = -outward;
                }

                int b = v.Count;
                float along = p0.x * edge.x / len + p0.y * edge.y / len;
                v.Add(new Vector3(p0.x, y0, p0.y));
                v.Add(new Vector3(p1.x, y0, p1.y));
                v.Add(new Vector3(p1.x, y1, p1.y));
                v.Add(new Vector3(p0.x, y1, p0.y));
                uv.Add(new Vector2(along / tile.x, y0 / tile.y));
                uv.Add(new Vector2((along + len) / tile.x, y0 / tile.y));
                uv.Add(new Vector2((along + len) / tile.x, y1 / tile.y));
                uv.Add(new Vector2(along / tile.x, y1 / tile.y));
                for (int k = 0; k < 4; k++)
                {
                    n.Add(outward);
                }

                Tri(t, v, b, b + 1, b + 2, outward);
                Tri(t, v, b, b + 2, b + 3, outward);
            }

            var mesh = MakeMesh("Rr Rc Prism", v, n, uv, t);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = s.Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            return go;
        }

        /// <summary>Points on a circle in the XZ plane: angle 0 = +X, increasing toward +Z (degrees), both ends included.</summary>
        public static List<Vector2> Arc(Vector2 center, float radius, float fromDeg, float toDeg, int segments)
        {
            var l = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)segments) * Mathf.Deg2Rad;
                l.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            return l;
        }

        /// <summary>A flat quad facing its local -Z (so it reads from the side +Z points away from), UV 0..1.</summary>
        public static GameObject Quad(Transform parent, string name, Vector3 center, Quaternion rotation, Vector2 size, Material m)
        {
            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            var mesh = new Mesh { name = "Rr Rc Quad" };
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            mesh.vertices = new[] { new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f), new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            Own(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// A world label in Rec Room's font. Local +Z of the label points away from the reader (TMP reads from -Z),
        /// so a label on a wall uses the rotation whose +Z points into the wall.
        /// </summary>
        public static TextMeshPro Label(Transform parent, string name, string text, Vector3 pos, float yaw, float height, float width, Color color, bool title = true)
        {
            var tmp = RrBuild.Sign(parent, name, text, pos, Quaternion.Euler(0f, yaw, 0f), height * 10f, color, title, width);
            tmp.rectTransform.sizeDelta = new Vector2(width, height * 1.6f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = height * 3f;
            tmp.fontSizeMax = height * 10f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>A panel (box 0.03 m deep) against a wall with a label on it; yaw as for <see cref="Label"/>.</summary>
        public static void Panel(Transform parent, string name, string text, Vector3 center, float yaw, Vector2 size, RrRcSurface panel, Color textColor)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box(parent, name + " Panel", center, rot, new Vector3(size.x, size.y, 0.03f), panel, false);
            var front = center - rot * Vector3.forward * 0.02f;
            Label(parent, name + " Text", text, front, yaw, size.y * 0.62f, size.x * 0.94f, textColor);
        }

        public static void Own(Object o)
        {
            if (Owned != null && o != null)
            {
                Owned.Add(o);
            }
        }

        internal static Vector2 Tile(RrRcSurface s)
        {
            float tu = s.Tile > 0.01f ? s.Tile : 1f;
            return new Vector2(tu, s.TileV > 0.01f ? s.TileV : tu);
        }

        internal static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            Own(mesh);
            return mesh;
        }

        /// <summary>A triangle whose front face (clockwise, Unity) faces <paramref name="want"/>.</summary>
        private static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 want)
        {
            var cross = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(cross, want) >= 0f)
            {
                t.Add(a);
                t.Add(b);
                t.Add(c);
            }
            else
            {
                t.Add(a);
                t.Add(c);
                t.Add(b);
            }
        }

        /// <summary>
        /// Appends a box (centre, rotation, size in the mesh's space) with metre UVs: an axis-aligned box's UVs are
        /// anchored at <paramref name="anchor"/> + its local corner, so flush pieces continue the texture.
        /// </summary>
        internal static void AppendBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 center, Quaternion rot, Vector3 size, Vector2 tile, Vector3 anchor)
        {
            var h = size * 0.5f;
            // +X, -X, +Y, -Y, +Z, -Z: each face spans two axes (u, v) in metres.
            Face(v, n, uv, t, Vector3.right, Vector3.forward, Vector3.up, h.x, h.z, h.y, tile, anchor, center, rot);
            Face(v, n, uv, t, Vector3.left, Vector3.back, Vector3.up, h.x, h.z, h.y, tile, anchor, center, rot);
            Face(v, n, uv, t, Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z, tile, anchor, center, rot);
            Face(v, n, uv, t, Vector3.down, Vector3.right, Vector3.back, h.y, h.x, h.z, tile, anchor, center, rot);
            Face(v, n, uv, t, Vector3.forward, Vector3.left, Vector3.up, h.z, h.x, h.y, tile, anchor, center, rot);
            Face(v, n, uv, t, Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y, tile, anchor, center, rot);
        }

        private static void Face(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 normal, Vector3 uAxis, Vector3 vAxis, float depth, float hu, float hv, Vector2 tile, Vector3 anchor, Vector3 center, Quaternion rot)
        {
            var c = normal * depth;
            int b = v.Count;
            for (int k = 0; k < 4; k++)
            {
                float su = k == 0 || k == 3 ? -1f : 1f;
                float sv = k < 2 ? -1f : 1f;
                var local = c + uAxis * (su * hu) + vAxis * (sv * hv);
                v.Add(center + rot * local);
                n.Add(rot * normal);
                // Metre UVs anchored in room space: a wall or floor split into pieces shows no seams.
                var p = anchor + local;
                uv.Add(new Vector2(Vector3.Dot(p, uAxis) / tile.x, Vector3.Dot(p, vAxis) / tile.y));
            }

            // Winding: Unity front faces are clockwise seen from outside; with (u x v) == normal that is 0-1-2.
            if (Vector3.Dot(Vector3.Cross(uAxis, vAxis), normal) < 0f)
            {
                t.Add(b);
                t.Add(b + 2);
                t.Add(b + 1);
                t.Add(b);
                t.Add(b + 3);
                t.Add(b + 2);
            }
            else
            {
                t.Add(b);
                t.Add(b + 1);
                t.Add(b + 2);
                t.Add(b);
                t.Add(b + 2);
                t.Add(b + 3);
            }
        }
    }

    /// <summary>
    /// Many boxes of one surface merged into one mesh and one renderer (window frames, beams, rails, bulbs):
    /// the curved window wall alone is ~100 frame pieces, which as separate objects would cost a draw call each
    /// in VR. Boxes are in the parent's space; axis-aligned ones keep room-space UVs like <see cref="RrRcGeo.Box"/>.
    /// Colliders are optional per box (separate collider-only objects, so the merged renderer stays one).
    /// </summary>
    internal sealed class RrRcBatch
    {
        private readonly RrRcSurface _s;
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<int> _t = new List<int>();
        private readonly List<KeyValuePair<Pose, Vector3>> _solids = new List<KeyValuePair<Pose, Vector3>>();

        public RrRcBatch(RrRcSurface s)
        {
            _s = s;
        }

        public int Count { get; private set; }

        public void Box(Vector3 center, Quaternion rot, Vector3 size, bool collider = false)
        {
            var anchor = rot == Quaternion.identity ? center : Vector3.zero;
            RrRcGeo.AppendBox(_v, _n, _uv, _t, center, rot, size, RrRcGeo.Tile(_s), anchor);
            if (collider)
            {
                _solids.Add(new KeyValuePair<Pose, Vector3>(new Pose(center, rot), size));
            }

            Count++;
        }

        public void Slab(Vector3 min, Vector3 max, bool collider = false)
        {
            Box((min + max) * 0.5f, Quaternion.identity, new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), Mathf.Abs(max.z - min.z)), collider);
        }

        /// <summary>Makes the merged renderer (and the collider-only boxes) under <paramref name="parent"/>; null if empty.</summary>
        public GameObject Build(Transform parent, string name)
        {
            if (Count == 0)
            {
                return null;
            }

            var go = new GameObject(name);
            go.layer = RrLayers.Environment;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = RrRcGeo.MakeMesh("Rr Rc Batch " + name, _v, _n, _uv, _t);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _s.Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < _solids.Count; i++)
            {
                RrRcGeo.Solid(go.transform, name + " Solid", _solids[i].Key.position, _solids[i].Key.rotation, _solids[i].Value);
            }

            return go;
        }
    }
}
