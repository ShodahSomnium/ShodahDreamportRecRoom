using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Procedural building blocks for the rooms this world rebuilds (Rec Room's level files are encrypted and
    /// stay untouched, so shells, arenas and courses are built here to the real rooms' layouts and dressed with
    /// Rec Room's own props). Every collider made here is on <see cref="RrLayers.Environment"/> unless asked
    /// otherwise: the avatar stands on it in desktop and VR, the VR ray hits it, props bounce off it.
    ///
    /// Positions and sizes are local to the parent (a room's root, placed at the room's origin).
    /// </summary>
    public static class RrBuild
    {
        /// <summary>An opening cut into a wall: distance along the wall from its start, width, bottom and top.</summary>
        public struct Hole
        {
            public float Along;
            public float Width;
            public float Bottom;
            public float Top;

            public Hole(float along, float width, float bottom, float top)
            {
                Along = along;
                Width = width;
                Bottom = bottom;
                Top = top;
            }

            /// <summary>A doorway from the floor.</summary>
            public static Hole Door(float along, float width = 1.6f, float height = 2.6f)
            {
                return new Hole(along, width, 0f, height);
            }
        }

        public static GameObject Group(Transform parent, string name, Vector3 localPos = default(Vector3), Quaternion? localRot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            return go;
        }

        /// <summary>A box by centre and size (axis-aligned in the parent), with a box collider.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = true, int layer = RrLayers.Environment)
        {
            return Primitive(PrimitiveType.Cube, parent, name, center, Quaternion.identity, size, material, collider, layer);
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size, Material material, bool collider = true, int layer = RrLayers.Environment)
        {
            return Primitive(PrimitiveType.Cube, parent, name, center, rotation, size, material, collider, layer);
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material material, bool collider = true, int layer = RrLayers.Environment)
        {
            // Unity's cylinder is 2 m tall and 1 m across.
            return Primitive(PrimitiveType.Cylinder, parent, name, center, Quaternion.identity, new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, collider, layer);
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 center, float radius, Material material, bool collider = true, int layer = RrLayers.Environment)
        {
            return Primitive(PrimitiveType.Sphere, parent, name, center, Quaternion.identity, Vector3.one * radius * 2f, material, collider, layer);
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 scale, Material material, bool collider, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (material != null)
            {
                r.sharedMaterial = material;
            }

            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null)
                {
                    Object.Destroy(c);
                }
            }

            return go;
        }

        /// <summary>A floor slab whose top surface is at y (default thickness 0.2 m).</summary>
        public static GameObject Floor(Transform parent, string name, Vector3 center, Vector2 size, Material material, float thickness = 0.2f)
        {
            return Box(parent, name, new Vector3(center.x, center.y - thickness * 0.5f, center.z), new Vector3(size.x, thickness, size.y), material);
        }

        /// <summary>
        /// A straight wall from a to b (their y is the wall's bottom), with openings. Built as boxes around the
        /// holes so every piece keeps a simple box collider.
        /// </summary>
        public static GameObject Wall(Transform parent, string name, Vector3 a, Vector3 b, float height, float thickness, Material material, params Hole[] holes)
        {
            var root = Group(parent, name);
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float length = flat.magnitude;
            if (length < 0.01f)
            {
                return root;
            }

            root.transform.localPosition = a;
            root.transform.localRotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);

            // Local frame: x along the wall (0..length), y up, z through the wall.
            var sorted = new List<Hole>(holes ?? new Hole[0]);
            sorted.Sort((h1, h2) => h1.Along.CompareTo(h2.Along));
            float x = 0f;
            int n = 0;
            foreach (var h in sorted)
            {
                float h0 = Mathf.Clamp(h.Along, 0f, length);
                float h1 = Mathf.Clamp(h.Along + h.Width, 0f, length);
                if (h0 > x + 0.001f)
                {
                    Segment(root.transform, name + " " + n++, x, h0, 0f, height, thickness, material);
                }

                if (h.Bottom > 0.001f)
                {
                    Segment(root.transform, name + " " + n++, h0, h1, 0f, h.Bottom, thickness, material);
                }

                if (h.Top < height - 0.001f)
                {
                    Segment(root.transform, name + " " + n++, h0, h1, h.Top, height, thickness, material);
                }

                x = Mathf.Max(x, h1);
            }

            if (x < length - 0.001f)
            {
                Segment(root.transform, name + " " + n, x, length, 0f, height, thickness, material);
            }

            return root;
        }

        private static void Segment(Transform wall, string name, float x0, float x1, float y0, float y1, float thickness, Material material)
        {
            Box(wall, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f), new Vector3(x1 - x0, y1 - y0, thickness), material);
        }

        /// <summary>A closed rectangular room shell: floor, four walls, optional ceiling. Returns the root.</summary>
        public static GameObject RoomShell(Transform parent, string name, Vector3 center, Vector3 size, Material floor, Material walls, Material ceiling, float wallThickness = 0.3f,
            Hole[] north = null, Hole[] south = null, Hole[] east = null, Hole[] west = null)
        {
            var root = Group(parent, name, center);
            var t = root.transform;
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;
            Floor(t, "Floor", Vector3.zero, new Vector2(size.x + wallThickness * 2f, size.z + wallThickness * 2f), floor);
            Wall(t, "Wall N", new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), size.y, wallThickness, walls, north ?? new Hole[0]);
            Wall(t, "Wall S", new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, -hz), size.y, wallThickness, walls, south ?? new Hole[0]);
            Wall(t, "Wall E", new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz), size.y, wallThickness, walls, east ?? new Hole[0]);
            Wall(t, "Wall W", new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), size.y, wallThickness, walls, west ?? new Hole[0]);
            if (ceiling != null)
            {
                Box(t, "Ceiling", new Vector3(0f, size.y + 0.1f, 0f), new Vector3(size.x + wallThickness * 2f, 0.2f, size.z + wallThickness * 2f), ceiling);
            }

            return root;
        }

        /// <summary>A ramp from a (bottom) to b (top), width w, as a rotated slab.</summary>
        public static GameObject Ramp(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material, float thickness = 0.2f)
        {
            Vector3 d = b - a;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            var center = (a + b) * 0.5f - rot * Vector3.up * (thickness * 0.5f);
            return Box(parent, name, center, rot, new Vector3(width, thickness, d.magnitude), material);
        }

        /// <summary>Stairs from a (bottom front) climbing to b (top back).</summary>
        public static GameObject Stairs(Transform parent, string name, Vector3 a, Vector3 b, float width, int steps, Material material)
        {
            var root = Group(parent, name);
            Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float rise = (b.y - a.y) / steps;
            float run = flat.magnitude / steps;
            var rot = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (int i = 0; i < steps; i++)
            {
                Vector3 c = a + flat.normalized * (run * (i + 0.5f)) + Vector3.up * (rise * (i + 1) * 0.5f);
                Box(root.transform, name + " " + i, c, rot, new Vector3(width, rise * (i + 1), run), material);
            }

            return root;
        }

        /// <summary>A world-space label in Rec Room's font (signs, scoreboards, door titles).</summary>
        public static TextMeshPro Sign(Transform parent, string name, string text, Vector3 localPos, Quaternion localRot, float fontSize, Color color, bool title = true, float width = 4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var tmp = go.AddComponent<TextMeshPro>();
            RrText.Style(tmp, title);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(width, fontSize * 0.25f + 0.5f);
            return tmp;
        }

        /// <summary>A light suitable for Rec Room's soft, bright rooms.</summary>
        public static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float range, float intensity = 1.2f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
