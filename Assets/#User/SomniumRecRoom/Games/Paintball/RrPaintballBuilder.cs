using System;
using System.Collections;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Build helper shared by the lobby and the maps: Rec Room props through <see cref="RrSpawn"/> (asynchronous; the
    /// builder counts what is still loading so Build can wait for it), grounded on their renderers' lowest point
    /// (the Sandbox variants' pivots are not at their feet), plus the paintball palette for procedural pieces.
    /// </summary>
    internal sealed class RrPaintballBuilder
    {
        public readonly Transform Room;
        public int Pending;
        public int Spawned;
        public int Failed;
        public int Requested;

        public RrPaintballBuilder(Transform room)
        {
            Room = room;
        }

        // ---- palette (Rec Room's flat, saturated look; rr_shooters.md §3.1 team colours) ----
        public static readonly Color Grass = new Color(0.40f, 0.64f, 0.29f);
        public static readonly Color DarkGrass = new Color(0.30f, 0.52f, 0.24f);
        public static readonly Color Dirt = new Color(0.58f, 0.44f, 0.30f);
        public static readonly Color RiverBed = new Color(0.45f, 0.40f, 0.32f);
        public static readonly Color WaterBlue = new Color(0.28f, 0.58f, 0.80f);
        public static readonly Color Plank = new Color(0.72f, 0.52f, 0.32f);
        public static readonly Color DarkPlank = new Color(0.50f, 0.34f, 0.20f);
        public static readonly Color Asphalt = new Color(0.20f, 0.20f, 0.22f);
        public static readonly Color Gravel = new Color(0.55f, 0.50f, 0.45f);
        public static readonly Color Stripe = new Color(0.95f, 0.93f, 0.85f);
        public static readonly Color Stucco = new Color(0.93f, 0.86f, 0.72f);
        public static readonly Color Mint = new Color(0.55f, 0.85f, 0.75f);
        public static readonly Color Steel = new Color(0.45f, 0.47f, 0.50f);
        public static readonly Color Blue = new Color(0.22f, 0.56f, 0.84f);
        public static readonly Color Red = new Color(1f, 0.45f, 0.25f);

        public static Color TeamColor(int team)
        {
            return team == 1 ? Red : Blue;
        }

        public static Material M(Color c)
        {
            return RrLook.Solid(c);
        }

        /// <summary>
        /// Spawns a Rec Room prop (scenery by default: environment layer, kinematic) at a local position of parent,
        /// yawed, optionally scaled, and grounded so its lowest renderer point sits at the given local height.
        /// </summary>
        public void Prop(string prefab, Transform parent, Vector3 localPos, float yaw, float scale = 1f, bool ground = true,
                         RrSpawnOptions options = null, Action<RrInstance> then = null)
        {
            Pending++;
            Requested++;
            var o = options ?? RrSpawnOptions.Scenery();
            if (Mathf.Abs(scale - 1f) > 1e-4f)
            {
                o.Scale = scale;
            }

            RrSpawn.Spawn(prefab, parent, localPos, Quaternion.Euler(0f, yaw, 0f), inst =>
            {
                Pending--;
                if (inst == null)
                {
                    Failed++;
                    RrLog.WarnOnce("Paintball", "miss:" + prefab, "prop " + prefab + " could not be spawned; left out");
                    return;
                }

                if (parent == null)
                {
                    UnityEngine.Object.Destroy(inst.gameObject);
                    return;
                }

                Spawned++;
                if (ground)
                {
                    var b = inst.WorldBounds();
                    if (b.size.sqrMagnitude > 1e-6f)
                    {
                        float y = parent.TransformPoint(localPos).y;
                        inst.transform.position += Vector3.up * (y - b.min.y);
                    }
                }

                if (then != null)
                {
                    try
                    {
                        then(inst);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Paintball", "prop setup failed for " + prefab, e);
                    }
                }
            }, o);
        }

        /// <summary>Waits until every requested prop has arrived (or the timeout), reporting 0..1.</summary>
        public IEnumerator WaitAll(float timeout, Action<float> progress)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (Pending > 0 && Time.realtimeSinceStartup < until)
            {
                if (progress != null)
                {
                    progress(Requested > 0 ? 1f - Pending / (float)Requested : 1f);
                }

                yield return null;
            }

            if (Pending > 0)
            {
                RrLog.Warn("Paintball", Pending + " props still loading after " + timeout + " s; building on");
            }
        }

        // ---- procedural pieces ----

        public static GameObject Box(Transform p, string name, Vector3 center, Vector3 size, Color c, bool collider = true)
        {
            return RrBuild.Box(p, name, center, size, M(c), collider);
        }

        public static GameObject Box(Transform p, string name, Vector3 center, float yaw, Vector3 size, Color c, bool collider = true)
        {
            return RrBuild.Box(p, name, center, Quaternion.Euler(0f, yaw, 0f), size, M(c), collider);
        }

        /// <summary>A flat decal-like strip lying on the ground (no collider).</summary>
        public static GameObject Strip(Transform p, string name, Vector3 center, Vector2 size, Color c, float yaw = 0f, float lift = 0.012f)
        {
            return RrBuild.Box(p, name, center + Vector3.up * lift, Quaternion.Euler(0f, yaw, 0f), new Vector3(size.x, 0.01f, size.y), M(c), false);
        }

        /// <summary>An invisible wall that keeps players in the playfield (environment layer, no renderer).</summary>
        public static void Fence(Transform p, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.localPosition = center;
            go.layer = RrLayers.Environment;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Four invisible walls around a rectangle (half sizes hx, hz around c).</summary>
        public static void Bounds(Transform p, Vector3 c, float hx, float hz, float height = 6f)
        {
            Fence(p, "Bound N", c + new Vector3(0f, height * 0.5f, hz + 0.5f), new Vector3(hx * 2f + 2f, height, 1f));
            Fence(p, "Bound S", c + new Vector3(0f, height * 0.5f, -hz - 0.5f), new Vector3(hx * 2f + 2f, height, 1f));
            Fence(p, "Bound E", c + new Vector3(hx + 0.5f, height * 0.5f, 0f), new Vector3(1f, height, hz * 2f + 2f));
            Fence(p, "Bound W", c + new Vector3(-hx - 0.5f, height * 0.5f, 0f), new Vector3(1f, height, hz * 2f + 2f));
        }

        /// <summary>Tints every material of a spawned prop whose name contains one of the keys (team-coloured parts).</summary>
        public static int Tint(GameObject root, Color c, params string[] keys)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !Matches(m.name, keys))
                    {
                        continue;
                    }

                    SetColor(m, c);
                    changed = true;
                    n++;
                }

                if (changed)
                {
                    r.materials = mats;
                }
            }

            return n;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, m.GetColor("_BaseColor").a));
            }

            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", new Color(c.r, c.g, c.b, m.GetColor("_Color").a));
            }

            if (m.HasProperty("_TintColor"))
            {
                m.SetColor("_TintColor", new Color(c.r, c.g, c.b, m.GetColor("_TintColor").a));
            }

            if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION"))
            {
                m.SetColor("_EmissionColor", c * 1.5f);
            }
        }

        private static bool Matches(string name, string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (name.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
