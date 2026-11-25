using System;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact.MakerPen;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.MakerRooms
{
    /// <summary>
    /// The objects' visuals. Every object is a container "Maker &lt;id&gt; &lt;kind&gt;" under the room's Objects group (at the
    /// room origin, identity), posed exactly by its Position / Rotation / Scale (a light keeps scale 1: its Scale holds range,
    /// intensity and spot angle) and marked with <see cref="RrMakerObjectMark"/>; what it draws is rebuilt under it when the
    /// look changes, so <see cref="Find"/> stays the same object for the build tools.
    ///
    /// Props are install prefabs (RrSpawn), shapes the install's ExtrusionShape_* meshes re-centred to a unit size (Unity
    /// primitives or small generated meshes when one cannot load), ink the Maker Pen's tube, lights a Unity light plus the
    /// room's Rec Room light grid, text an RrBuild sign. Physics: Decoration on the Grabbable layer (the avatar walks
    /// through, rays hit it), everything else on Environment (solid).
    /// </summary>
    public sealed partial class RrMakerRoom
    {
        /// <summary>Shapes the install has (ExtrusionShape_&lt;name&gt;, bundle 1df8e0ef).</summary>
        public static readonly string[] InstallShapes = { "Cube", "Sphere", "Cylinder", "Pyramid", "Wedge", "Cone", "Hemisphere", "Donut", "Pipe", "HalfPipe", "Diamond", "Mound" };

        private readonly List<string> _missingAssets = new List<string>();
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<string, Mesh> _shapeMeshes = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, List<Action<Mesh>>> _shapeWaiting = new Dictionary<string, List<Action<Mesh>>>();
        private readonly Dictionary<int, RrLightGrid.Source> _lightSources = new Dictionary<int, RrLightGrid.Source>();
        private float _lightsAt = -1f;
        private GameObject _gridHost;
        private bool _bulbsShown = true;
        private float _bulbCheckAt;

        private static int LayerOf(RrMakerObject o)
        {
            return o.Physics == RrMakerPhysics.Decoration ? RrLayers.Grabbable : RrLayers.Environment;
        }

        private static Color ColorOf(RrMakerObject o)
        {
            Color32 c = o.Color;
            return new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
        }

        // ------------------------------------------------------------------ building one object

        private void BuildVisual(Entry e)
        {
            e.Serial++;
            bool hadLights = e.HasLights;
            if (e.Go == null)
            {
                var go = new GameObject();
                go.transform.SetParent(_objects != null ? _objects : transform, false);
                var mark = go.AddComponent<RrMakerObjectMark>();
                mark.Id = e.O.Id;
                mark.Room = this;
                e.Go = go;
            }
            else
            {
                var t = e.Go.transform;
                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    var child = t.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }

            if (e.OwnMesh != null)
            {
                _owned.Remove(e.OwnMesh);
                Destroy(e.OwnMesh);
                e.OwnMesh = null;
            }

            e.Bulb = null;
            e.Light = null;
            e.HasLights = false;
            e.Lerp = false;
            e.Go.name = "Maker " + e.O.Id.ToString("x8") + " " + e.O.Kind + (string.IsNullOrEmpty(e.O.Asset) ? "" : " " + e.O.Asset);
            e.Go.layer = LayerOf(e.O);
            ApplyTransform(e);
            try
            {
                switch (e.O.Kind)
                {
                    case RrMakerKind.Prop:
                        BuildProp(e);
                        break;
                    case RrMakerKind.Shape:
                        BuildShape(e);
                        break;
                    case RrMakerKind.Ink:
                        BuildInk(e);
                        break;
                    case RrMakerKind.Light:
                        BuildLight(e);
                        break;
                    case RrMakerKind.Text:
                        BuildText(e);
                        break;
                    default:
                        Placeholder(e, "kind " + (int)e.O.Kind);
                        break;
                }
            }
            catch (Exception ex)
            {
                RrLog.Error("MakerRooms", "building object " + e.O.Id.ToString("x8") + " (" + e.O.Kind + " " + e.O.Asset + ") failed", ex);
                Placeholder(e, e.O.Asset);
            }

            if (hadLights || e.HasLights)
            {
                LightsChanged();
            }
        }

        /// <summary>The container's pose from the data (a light keeps scale 1).</summary>
        private void ApplyTransform(Entry e)
        {
            if (e.Go == null)
            {
                return;
            }

            var t = e.Go.transform;
            t.localPosition = e.O.Position;
            t.localRotation = e.O.Rotation;
            t.localScale = e.O.Kind == RrMakerKind.Light ? Vector3.one : e.O.Scale;
            if (e.HasLights)
            {
                LightsChanged();
            }
        }

        private void Remove(Entry e)
        {
            _entries.Remove(e.O.Id);
            e.Serial++;
            if (e.OwnMesh != null)
            {
                _owned.Remove(e.OwnMesh);
                Destroy(e.OwnMesh);
                e.OwnMesh = null;
            }

            if (e.Go != null)
            {
                e.Go.SetActive(false);
                Destroy(e.Go);
                e.Go = null;
            }

            if (e.HasLights)
            {
                LightsChanged();
            }
        }

        /// <summary>A grey box standing in for something that could not be made (so it can still be selected and deleted).</summary>
        private void Placeholder(Entry e, string what)
        {
            if (e.Go == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(what) && !_missingAssets.Contains(what) && _missingAssets.Count < 20)
            {
                _missingAssets.Add(what);
            }

            var box = RrBuild.Box(e.Go.transform, "Missing " + what, Vector3.zero, Vector3.one * 0.4f, RrLook.Solid(RrLook.Grey), true, LayerOf(e.O));
            box.transform.localScale = Vector3.one * 0.4f;
        }

        // ------------------------------------------------------------------ props

        private void BuildProp(Entry e)
        {
            string asset = e.O.Asset;
            if (string.IsNullOrEmpty(asset))
            {
                Placeholder(e, "(no prefab)");
                return;
            }

            int serial = e.Serial;
            var options = e.O.Physics == RrMakerPhysics.Decoration
                ? new RrSpawnOptions { Layer = RrLayers.Grabbable, Kinematic = true, StripAudioSources = true }
                : new RrSpawnOptions { Layer = RrLayers.Environment, Kinematic = true, StripAudioSources = true };
            _pending++;
            RrSpawn.Spawn(asset, e.Go.transform, Vector3.zero, Quaternion.identity, inst =>
            {
                _pending--;
                Entry cur;
                if (this == null || e.Go == null || e.Serial != serial || !_entries.TryGetValue(e.O.Id, out cur) || cur != e)
                {
                    if (inst != null)
                    {
                        Destroy(inst.gameObject);
                    }

                    return;
                }

                if (inst == null)
                {
                    RrLog.WarnOnce("MakerRooms", "prop:" + asset, "prop " + asset + " could not be spawned from the install: a grey box stands in");
                    Placeholder(e, asset);
                    return;
                }

                try
                {
                    FitProp(e, inst);
                }
                catch (Exception ex)
                {
                    RrLog.Warn("MakerRooms", "fitting " + asset + ": " + ex.GetType().Name + " " + ex.Message);
                }

                RrMakerEdits.RaiseChanged(e.O.Id);
            }, options);
        }

        private void FitProp(Entry e, RrInstance inst)
        {
            // A Maker Pen kit piece stored in its original room's coordinates (the Dorm kit): snapped to the kit's frame,
            // relative to the template pose the object was made at.
            Pose kit;
            if (RrMakerTemplates.KitPose(inst.PrefabName, out kit))
            {
                Transform child = null;
                var root = inst.transform;
                for (int i = 0; i < root.childCount; i++)
                {
                    var c = root.GetChild(i);
                    if (c.name == "PropContainer" || c.GetComponentInChildren<Renderer>(true) == null)
                    {
                        continue;
                    }

                    child = c;
                    break;
                }

                if (child != null && Mathf.Abs(child.localScale.x - 1f) < 0.01f)
                {
                    var snapRot = Quaternion.Inverse(child.localRotation);
                    var snapPos = -(snapRot * child.localPosition);
                    var inv = Quaternion.Inverse(kit.rotation);
                    root.localPosition = inv * (snapPos - kit.position);
                    root.localRotation = inv * snapRot;
                }
            }

            // Maker-pen surfaces take the object's colour; Rec Room's own materials stay as they are.
            var solid = RrLook.Solid(ColorOf(e.O));
            bool shafts = inst.PrefabName == "(Dorm_Light_Shafts)";
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                bool editorOnly = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null)
                    {
                        continue;
                    }

                    string n = m.name + "|" + (m.shader != null ? m.shader.name : "");
                    if (n.IndexOf("Holographic", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("InteractionVolume", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("DoNotRender", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("InvisibleOutline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        editorOnly = true;
                        break;
                    }

                    if (n.IndexOf("MakerPen", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mats[i] = solid;
                        changed = true;
                    }
                }

                if (editorOnly)
                {
                    r.enabled = false;
                    continue;
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.TwoSided;
                }

                if (shafts)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }

            // Triggers (padding, selection volumes) would block the pen's and the pointer's rays: solid colliders only.
            bool solidCollider = false;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
            {
                // The Dorm's light shafts are pure visuals (the Dorm spawns them without colliders): nothing to hit there.
                if (c.isTrigger || shafts)
                {
                    Destroy(c);
                }
                else
                {
                    solidCollider = true;
                }
            }

            // Rec Room's own room doors in a venue (the Ink Space hull's) need its activity logic: hidden.
            var door = inst.Find("[ActivityDoor]");
            if (door != null)
            {
                door.gameObject.SetActive(false);
            }

            foreach (var l in inst.GetComponentsInChildren<Light>(true))
            {
                l.shadows = LightShadows.None;
                e.HasLights = true;
            }

            if (e.HasLights)
            {
                LightsChanged();
            }

            // Something to point at: props without a collider of their own get a pick box (small ones only: a big box
            // round the Dorm's light shafts would catch every ray in the room).
            if (!solidCollider)
            {
                var b = LocalBounds(e.Go.transform, inst.gameObject);
                if (b.size != Vector3.zero && b.size.x < 3f && b.size.y < 3f && b.size.z < 3f)
                {
                    var pick = new GameObject("Pick");
                    pick.layer = LayerOf(e.O);
                    pick.transform.SetParent(e.Go.transform, false);
                    var box = pick.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = Vector3.Max(b.size, Vector3.one * 0.05f);
                }
            }
        }

        /// <summary>Renderer bounds in <paramref name="space"/>'s local frame (zero when there are none).</summary>
        private static Bounds LocalBounds(Transform space, GameObject go)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                var wb = r.bounds;
                var c = wb.center;
                var ex = wb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var p = space.InverseTransformPoint(c + new Vector3((i & 1) == 0 ? -ex.x : ex.x, (i & 2) == 0 ? -ex.y : ex.y, (i & 4) == 0 ? -ex.z : ex.z));
                    if (!any)
                    {
                        b = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(p);
                    }
                }
            }

            return any ? b : new Bounds(Vector3.zero, Vector3.zero);
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>The install name of a shape ("Cube" -> "ExtrusionShape_Cube"), or null when the install has none by that name.</summary>
        public static string InstallMeshOf(string shape)
        {
            foreach (var s in InstallShapes)
            {
                if (string.Equals(s, shape, StringComparison.OrdinalIgnoreCase))
                {
                    return "ExtrusionShape_" + s;
                }
            }

            return null;
        }

        private void BuildShape(Entry e)
        {
            var holder = new GameObject("Shape");
            holder.layer = LayerOf(e.O);
            holder.transform.SetParent(e.Go.transform, false);
            var material = RrLook.Solid(ColorOf(e.O));

            string shape = e.O.Asset ?? "";
            bool round = shape.Equals("Sphere", StringComparison.OrdinalIgnoreCase) || shape.Equals("Mound", StringComparison.OrdinalIgnoreCase) ||
                         shape.Equals("Hemisphere", StringComparison.OrdinalIgnoreCase);
            var col = new GameObject("Collider");
            col.layer = LayerOf(e.O);
            col.transform.SetParent(e.Go.transform, false);
            if (shape.Equals("Sphere", StringComparison.OrdinalIgnoreCase))
            {
                col.AddComponent<SphereCollider>().radius = 0.5f;
            }
            else
            {
                col.AddComponent<BoxCollider>().size = round ? new Vector3(1f, 1f, 1f) : Vector3.one;
            }

            string meshName = InstallMeshOf(shape);
            if (meshName == null || RrAssets.Instance == null)
            {
                FallbackShape(e, holder.transform, shape, material);
                return;
            }

            int serial = e.Serial;
            _pending++;
            ShapeMesh(meshName, mesh =>
            {
                _pending--;
                if (this == null || holder == null || e.Serial != serial)
                {
                    return;
                }

                if (mesh == null)
                {
                    FallbackShape(e, holder.transform, shape, material);
                    return;
                }

                var mf = holder.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = holder.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = ShadowCastingMode.On;
                FitUnit(holder.transform, mesh.bounds);
            });
        }

        /// <summary>Scales and moves a mesh holder so the mesh's bounds become a unit box centred on the parent's origin.</summary>
        private static void FitUnit(Transform holder, Bounds b)
        {
            var size = new Vector3(Mathf.Max(1e-4f, b.size.x), Mathf.Max(1e-4f, b.size.y), Mathf.Max(1e-4f, b.size.z));
            var scale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            holder.localScale = scale;
            holder.localPosition = -Vector3.Scale(b.center, scale);
            holder.localRotation = Quaternion.identity;
        }

        private void ShapeMesh(string name, Action<Mesh> done)
        {
            Mesh m;
            if (_shapeMeshes.TryGetValue(name, out m))
            {
                done(m);
                return;
            }

            List<Action<Mesh>> waiting;
            if (_shapeWaiting.TryGetValue(name, out waiting))
            {
                waiting.Add(done);
                return;
            }

            waiting = new List<Action<Mesh>> { done };
            _shapeWaiting[name] = waiting;
            RrAssets.Instance.Load<Mesh>(name, 43, mesh =>
            {
                if (mesh == null)
                {
                    RrLog.WarnOnce("MakerRooms", "shape:" + name, "shape mesh " + name + " did not load from the install: a stand-in shape is drawn");
                }

                _shapeMeshes[name] = mesh;
                List<Action<Mesh>> list;
                if (_shapeWaiting.TryGetValue(name, out list))
                {
                    _shapeWaiting.Remove(name);
                    foreach (var cb in list)
                    {
                        try
                        {
                            cb(mesh);
                        }
                        catch (Exception e)
                        {
                            RrLog.Warn("MakerRooms", "a shape callback failed: " + e.Message);
                        }
                    }
                }
            });
        }

        /// <summary>A unit shape of our own when the install's cannot be had: Unity primitives, or a small generated mesh.</summary>
        private void FallbackShape(Entry e, Transform holder, string shape, Material material)
        {
            string s = (shape ?? "").ToLowerInvariant();
            if (s == "sphere" || s == "hemisphere" || s == "mound")
            {
                RrBuild.Primitive(PrimitiveType.Sphere, holder, "Sphere", Vector3.zero, Quaternion.identity, Vector3.one, material, false, holder.gameObject.layer);
                return;
            }

            if (s == "cylinder" || s == "pipe" || s == "donut" || s == "halfpipe")
            {
                RrBuild.Primitive(PrimitiveType.Cylinder, holder, "Cylinder", Vector3.zero, Quaternion.identity, new Vector3(1f, 0.5f, 1f), material, false, holder.gameObject.layer);
                return;
            }

            Mesh mesh = null;
            if (s == "wedge")
            {
                mesh = WedgeMesh();
            }
            else if (s == "pyramid" || s == "diamond")
            {
                mesh = PyramidMesh(4);
            }
            else if (s == "cone")
            {
                mesh = PyramidMesh(20);
            }

            if (mesh == null)
            {
                RrBuild.Primitive(PrimitiveType.Cube, holder, "Cube", Vector3.zero, Quaternion.identity, Vector3.one, material, false, holder.gameObject.layer);
                return;
            }

            e.OwnMesh = mesh;
            _owned.Add(mesh);
            holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            holder.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>A unit wedge centred on the origin: a box whose top slopes from the back (y +0.5) to the front edge (y -0.5).</summary>
        private static Mesh WedgeMesh()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 a = new Vector3(-0.5f, -0.5f, -0.5f), b = new Vector3(0.5f, -0.5f, -0.5f);
            Vector3 c = new Vector3(0.5f, -0.5f, 0.5f), d = new Vector3(-0.5f, -0.5f, 0.5f);
            Vector3 e = new Vector3(0.5f, 0.5f, 0.5f), f = new Vector3(-0.5f, 0.5f, 0.5f);
            Quad(v, t, a, d, c, b);          // bottom
            Quad(v, t, d, f, e, c);          // back
            Quad(v, t, a, b, e, f);          // slope
            Tri(v, t, a, f, d);              // left
            Tri(v, t, b, c, e);              // right
            return Finish("Maker Wedge", v, t);
        }

        /// <summary>A unit pyramid (4 sides) or cone (n sides) centred on the origin, apex up.</summary>
        private static Mesh PyramidMesh(int sides)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            var apex = new Vector3(0f, 0.5f, 0f);
            var centre = new Vector3(0f, -0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + (sides == 4 ? 0.5f : 0f)) * Mathf.PI * 2f / sides;
                float a1 = (i + 1 + (sides == 4 ? 0.5f : 0f)) * Mathf.PI * 2f / sides;
                float r = sides == 4 ? 0.7071068f : 0.5f;
                var p0 = new Vector3(Mathf.Cos(a0) * r, -0.5f, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, -0.5f, Mathf.Sin(a1) * r);
                Tri(v, t, p0, apex, p1);
                Tri(v, t, p0, p1, centre);
            }

            return Finish(sides == 4 ? "Maker Pyramid" : "Maker Cone", v, t);
        }

        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(v, t, a, b, c);
            Tri(v, t, a, c, d);
        }

        private static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count;
            v.Add(a);
            v.Add(b);
            v.Add(c);
            t.Add(i);
            t.Add(i + 1);
            t.Add(i + 2);
        }

        private static Mesh Finish(string name, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ ink

        private void BuildInk(Entry e)
        {
            var pts = e.O.Points;
            if (pts == null || pts.Count == 0)
            {
                Placeholder(e, "(empty ink)");
                return;
            }

            float radius = Mathf.Max(0.002f, e.O.Width * 0.5f);
            var radii = new List<float>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                radii.Add(radius);
            }

            var mesh = new Mesh { name = "Maker Ink " + e.O.Id.ToString("x8") };
            RrMakerPenTube.Build(mesh, new List<Vector3>(pts), radii);
            e.OwnMesh = mesh;
            _owned.Add(mesh);
            var go = new GameObject("Ink");
            go.layer = LayerOf(e.O);
            go.transform.SetParent(e.Go.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RrLook.Solid(ColorOf(e.O));
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = Vector3.Max(mesh.bounds.size, Vector3.one * Mathf.Max(0.02f, radius * 2f));
        }

        // ------------------------------------------------------------------ lights

        private void BuildLight(Entry e)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(e.Go.transform, false);
            var l = go.AddComponent<Light>();
            bool spot = string.Equals(e.O.Asset, "Spot", StringComparison.OrdinalIgnoreCase);
            l.type = spot ? LightType.Spot : LightType.Point;
            l.color = ColorOf(e.O);
            l.range = e.O.Scale.x > 0.01f ? e.O.Scale.x : 6f;
            l.intensity = e.O.Scale.y > 0.001f ? e.O.Scale.y : 1f;
            if (spot)
            {
                l.spotAngle = Mathf.Clamp(e.O.Scale.z > 0.5f ? e.O.Scale.z : 60f, 1f, 179f);
            }

            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            e.Light = l;
            e.HasLights = true;

            // The gadget: a small glowing bulb (a cone for a spot) builders can point at; hidden from visitors (Rec Room's
            // "Hide gadgets").
            var bulb = RrBuild.Sphere(e.Go.transform, "Bulb", Vector3.zero, 0.07f, RrLook.Emissive(ColorOf(e.O), 2f), true, RrLayers.Grabbable);
            if (spot)
            {
                RrBuild.Box(bulb.transform, "Aim", new Vector3(0f, 0f, 1.6f), new Vector3(0.25f, 0.25f, 1.6f), RrLook.Emissive(ColorOf(e.O), 1f), false, RrLayers.Grabbable);
            }

            foreach (var r in bulb.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            e.Bulb = bulb;
            bulb.SetActive(_bulbsShown);
        }

        /// <summary>Lights moved, came or went: the room's light grid is rebuilt a second after the last change.</summary>
        private void LightsChanged()
        {
            _lightsAt = Time.realtimeSinceStartup + 1f;
        }

        private void TickLights()
        {
            if (_lightsAt > 0f && Time.realtimeSinceStartup >= _lightsAt && Built && !Rooms.RrTravel.Busy)
            {
                _lightsAt = -1f;
                RebuildGrid();
            }

            if (Time.realtimeSinceStartup >= _bulbCheckAt)
            {
                _bulbCheckAt = Time.realtimeSinceStartup + 0.5f;
                bool show = CanEdit;
                if (show != _bulbsShown)
                {
                    _bulbsShown = show;
                    foreach (var e in _entries.Values)
                    {
                        if (e.Bulb != null)
                        {
                            e.Bulb.SetActive(show);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rec Room's light grid for the room's Rec Room shaders (the maker-pen shapes and walls, the install props): the
        /// base's lights (a kit scene's, the Dorm rig) and every light of the objects, at their current poses.
        /// </summary>
        private void RebuildGrid()
        {
            if (_gridHost != null)
            {
                Destroy(_gridHost);
                _gridHost = null;
            }

            var sources = new List<RrLightGrid.Source>();
            if (_base != null)
            {
                sources.AddRange(_base.Sources);
            }

            if (_objects != null)
            {
                foreach (var l in _objects.GetComponentsInChildren<Light>(false))
                {
                    if (!l.enabled || (l.type != LightType.Point && l.type != LightType.Spot))
                    {
                        continue;
                    }

                    int key = l.GetInstanceID();
                    RrLightGrid.Source s;
                    if (!_lightSources.TryGetValue(key, out s))
                    {
                        // The first time: Rec Room's values as the light has them, then its URP twin is set up once.
                        s = RrLightGrid.FromLight(l, transform);
                        s.Unity = l;
                        RrLightGrid.ConfigureUnity(l, s);
                        _lightSources[key] = s;
                    }

                    s.Position = transform.InverseTransformPoint(l.transform.position);
                    s.Rotation = Quaternion.Inverse(transform.rotation) * l.transform.rotation;
                    s.Unity = l;
                    sources.Add(s);
                }
            }

            if (sources.Count == 0)
            {
                return;
            }

            _gridHost = new GameObject("Maker Light Grid");
            _gridHost.transform.SetParent(transform, false);
            var grid = RrLightGrid.Attach(_gridHost, transform, "MakerRoom " + (Template != null ? Template.Key : "?"), default(Bounds), sources);
            RrLog.Info("MakerRooms", "light grid: " + sources.Count + " light(s)" + (grid == null ? " - not attached" : ""));
        }

        // ------------------------------------------------------------------ text

        private void BuildText(Entry e)
        {
            var tmp = RrBuild.Sign(e.Go.transform, "Text", e.O.Text ?? "", Vector3.zero, Quaternion.identity, 1f, ColorOf(e.O), true, 4f);
            tmp.gameObject.layer = LayerOf(e.O);
            var col = new GameObject("Collider");
            col.layer = LayerOf(e.O);
            col.transform.SetParent(e.Go.transform, false);
            var box = col.AddComponent<BoxCollider>();
            box.size = new Vector3(4f, 0.6f, 0.05f);
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Remote drags glide to their target (updates arrive at ~5 Hz).</summary>
        private void TickObjects()
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            foreach (var e in _entries.Values)
            {
                if (!e.Lerp || e.Go == null)
                {
                    continue;
                }

                var t = e.Go.transform;
                t.localPosition = Vector3.Lerp(t.localPosition, e.ToPos, k);
                t.localRotation = Quaternion.Slerp(t.localRotation, e.ToRot, k);
                t.localScale = Vector3.Lerp(t.localScale, e.ToScale, k);
                if ((t.localPosition - e.ToPos).sqrMagnitude < 1e-8f && Quaternion.Angle(t.localRotation, e.ToRot) < 0.05f)
                {
                    e.Lerp = false;
                    ApplyTransform(e);
                }
            }

            TickLights();
        }

        private void DestroyOwned()
        {
            foreach (var o in _owned)
            {
                if (o != null)
                {
                    Destroy(o);
                }
            }

            _owned.Clear();
            _shapeMeshes.Clear();
            _shapeWaiting.Clear();
            _lightSources.Clear();
        }
    }
}
