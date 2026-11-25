using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The Rec Center's light shafts and level effects (iteration 5), measured from the July 2025 room in the user's
    /// download cache (HTTPCache 1AF, <c>DevNotes/research/rr_reccenter_cache.md</c>; measurements only, nothing of the
    /// cache is loaded).
    ///
    /// <b>Light shafts.</b> The scene has six <c>[RecCenter_LightShafts]</c>: one skinned mesh
    /// (<c>Skylight_LightShafts</c>, 16 vertices: four inward-facing side quads of a 5.93 x 6.47 m prism, 7.685 m
    /// long, vertex colour 0.97 white at the source fading to black at the far end, u 0.009 -> 0.989 along the length)
    /// whose four bottom corners are bones (<c>Rig/&lt;corner&gt;/&lt;corner&gt;_bot</c>), posed in the scene to slant
    /// the prism: two hang under the skylights and end 3.65 m down, two come in through the curved windows and two
    /// through the gym's back windows, down to the floor. The mesh is not in the install, so the shafts are rebuilt
    /// here from the measured corner positions below. Their material, <c>LightShaftLocker_Mat</c>, is URP's own
    /// Particles/Unlit, so it is remade with its values (<see cref="ShaftMat"/>) on the install's GradientHorizontal
    /// (taken from the Dorm's <c>DormRoom_LightShaft_Gold_Mat</c>; that material, AG/Unlit-Additive-Soft, drew nothing
    /// visible here in harness run 1 and is only the fallback). All six are one mesh and one draw call, no collider,
    /// no shadow.
    ///
    /// <b>Effects.</b> The scene's only particle system is <c>[Wind_Lines_VFX]</c> outside the gym's back windows; the
    /// install has no such prefab, so its wind stand-in <c>[Sandbox_Wind_Wavy_VFX]</c> goes there through the scene
    /// kit (an inline RRSK placement), which also handles VR culling.
    /// </summary>
    public static class RrRecCenterFx
    {
        public const string ShaftMaterial = "DormRoom_LightShaft_Gold_Mat";

        /// <summary>LightShaftLocker_Mat's base colour x alpha (additive).</summary>
        public static readonly Color ShaftTint = new Color(0.157f, 0.137f, 0.050f, 1f);

        // Room frame (rr_reccenter_cache.md 1.1: X = z + 2.05, Y = y - 0.09, Z = -x - 7.23), per shaft the four source
        // corners (the rig's BackLeft, BackRight, FrontLeft, FrontRight bones) then the four far corners (their _bot
        // bones), from cacheidx/world_1AF.tsv.
        private static readonly float[] Corners =
        {
            // 1: under the front skylight, 3.65 m down towards the entrance
            -3.13f, 8.68f, -3.50f, 3.34f, 8.68f, -3.50f, -3.13f, 8.68f, -9.43f, 3.34f, 8.68f, -9.43f,
            -4.45f, 5.03f, -9.10f, 1.70f, 5.03f, -9.10f, -4.65f, 5.03f, -15.79f, 1.76f, 5.03f, -15.81f,
            // 2: under the back skylight
            -3.08f, 8.68f, 10.53f, 3.39f, 8.68f, 10.53f, -3.08f, 8.68f, 4.60f, 3.39f, 8.68f, 4.60f,
            -4.54f, 5.03f, 2.83f, 0.77f, 5.03f, 2.82f, -4.22f, 5.03f, -1.71f, 1.72f, 5.03f, -1.64f,
            // 3-4: through the curved windows (sill 0.7, top 5.6) onto the hall floor
            -0.45f, 0.71f, 18.16f, 3.88f, 0.71f, 17.28f, -0.36f, 5.66f, 18.09f, 4.02f, 5.59f, 17.30f,
            -0.86f, 0.00f, 16.03f, 3.65f, 0.00f, 15.43f, -2.29f, 0.00f, 9.85f, 2.19f, 0.00f, 8.92f,
            6.48f, 0.70f, 16.12f, 11.37f, 0.72f, 10.40f, 6.48f, 5.69f, 16.05f, 11.42f, 5.62f, 10.42f,
            6.24f, 0.00f, 14.74f, 11.08f, 0.00f, 9.15f, 4.58f, 0.00f, 7.68f, 9.50f, 0.00f, 2.24f,
            // 5-6: through the gym's back windows onto the gym floor (the scene's is 0.25 higher; ours is at 0)
            -16.71f, 1.00f, 25.86f, -8.92f, 1.01f, 25.79f, -16.72f, 5.06f, 25.79f, -8.87f, 5.00f, 25.81f,
            -16.85f, 0.00f, 24.35f, -9.44f, 0.00f, 24.33f, -17.53f, 0.00f, 19.04f, -10.57f, 0.00f, 19.04f,
            -5.98f, 0.90f, 25.61f, -3.29f, 0.91f, 25.53f, -5.98f, 4.97f, 25.53f, -3.25f, 4.90f, 25.55f,
            -6.22f, 0.00f, 24.41f, -3.70f, 0.05f, 23.84f, -7.49f, 0.00f, 19.07f, -5.05f, 0.12f, 18.88f,
        };

        // The Rec Center's one particle effect, as a scene-kit placement: [Wind_Lines_VFX] at room (-14.81, 2.89, 26.64),
        // yaw 160, outside the gym's back wall (z 25.9).
        private const string FxScene =
            "# RRSK 1 RecCenterFx\n" +
            "F [Sandbox_Wind_Wavy_VFX] -14.81 2.89 26.64 0 0.9848 0 0.1736 kind=wind match=kind src=[Wind_Lines_VFX]\n";

        public static int Shafts
        {
            get { return Corners.Length / 24; }
        }

        /// <summary>Builds the shafts (mesh and material owned by the room) and the effects under the room.</summary>
        public static IEnumerator Build(Transform room, List<Object> owned)
        {
            Material source = null;
            bool done = false;
            if (RrAssets.Instance != null)
            {
                RrAssets.Instance.Load<Material>(ShaftMaterial, 21, m =>
                {
                    source = m;
                    done = true;
                });
            }
            else
            {
                done = true;
            }

            float deadline = Time.realtimeSinceStartup + 20f;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (room == null)
            {
                yield break;
            }

            string how;
            var mat = ShaftMat(source, out how);
            if (mat != null)
            {
                owned.Add(mat);
                var mesh = ShaftMesh();
                owned.Add(mesh);
                var go = new GameObject("Light Shafts");
                go.transform.SetParent(room, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            RrLog.Info("RecCenter", "fx: " + (mat != null ? Shafts + " light shafts from the cache scene's rig (" + how + ", tint #" + ColorUtility.ToHtmlStringRGB(ShaftTint) + ")" : "light shafts left out (" + ShaftMaterial + " did not load)"));
            yield return RrSceneKit.Build(FxScene, room, new RrSceneKitOptions { RootName = "Rr Scene RecCenterFx" }, r => { });
        }

        /// <summary>
        /// Rec Room's LightShaftLocker_Mat (cache 1AF, mats_1AF.tsv row 48) is URP's own Particles/Unlit: transparent,
        /// _BaseColor (2, 1.76, 0.64, 0.078), colour mode add (_COLORADDSUBDIFF_ON, _BaseColorAddSubDiff (1, 0, 0, 0)),
        /// base map GradientHorizontal, soft particles and camera fading on. The same shader is in the world, so the
        /// material is remade with those values and the install's GradientHorizontal (the texture of the Dorm's
        /// light-shaft material); additive blending (GUESS: the row does not list the blend), and soft particles and
        /// camera fading left off (they need the camera depth texture, which the client's settings decide).
        /// The Dorm's AG/Unlit-Additive-Soft material is the fallback when URP's shader cannot be found.
        /// </summary>
        private static Material ShaftMat(Material source, out string how)
        {
            how = null;
            if (source == null)
            {
                return null;
            }

            Texture ramp = null;
            foreach (var p in new[] { "_MainTex", "_BaseMap" })
            {
                if (ramp == null && source.HasProperty(p))
                {
                    ramp = source.GetTexture(p);
                }
            }

            var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp != null)
            {
                var m = new Material(urp) { name = "RecCenter_LightShafts (LightShaftLocker_Mat values)" };
                m.SetTexture("_BaseMap", ramp);
                m.SetColor("_BaseColor", new Color(2f, 1.75957f, 0.63943f, 0.0784314f));
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.EnableKeyword("_COLORADDSUBDIFF_ON");
                m.SetVector("_BaseColorAddSubDiff", new Vector4(1f, 0f, 0f, 0f));
                m.renderQueue = 3000;
                how = "URP Particles/Unlit with LightShaftLocker_Mat's colour, ramp " + (ramp != null ? ramp.name : "none");
                return m;
            }

            var fixedMat = RrMaterials.Fix(source);
            var f = new Material(fixedMat != null ? fixedMat : source) { name = "RecCenter_LightShafts (from " + source.name + ")" };
            foreach (var p in new[] { "_TintColor", "_Color", "_BaseColor" })
            {
                if (f.HasProperty(p))
                {
                    var old = f.GetColor(p);
                    f.SetColor(p, new Color(ShaftTint.r, ShaftTint.g, ShaftTint.b, old.a));
                    how = ShaftMaterial + " " + f.shader.name + " " + p;
                    break;
                }
            }

            how = how ?? ShaftMaterial + " " + f.shader.name + " (no tint property)";
            return f;
        }

        // Four side quads per shaft, inward-facing like Rec Room's (seen from outside, the far sides show through),
        // colour 0.97 white at the source to black at the far end, u along the length, v round the prism.
        private static Mesh ShaftMesh()
        {
            int n = Shafts;
            var v = new List<Vector3>(n * 16);
            var c = new List<Color>(n * 16);
            var uv = new List<Vector2>(n * 16);
            var t = new List<int>(n * 24);
            // faces as (top a, top b) corner indices: back (BL-BR), right (BR-FR), front (FR-FL), left (FL-BL)
            int[] fa = { 0, 1, 3, 2 };
            int[] fb = { 1, 3, 2, 0 };
            var top = new Color(0.97f, 0.97f, 0.97f, 1f);
            for (int s = 0; s < n; s++)
            {
                var p = new Vector3[8];
                var centre = Vector3.zero;
                for (int k = 0; k < 8; k++)
                {
                    int o = s * 24 + k * 3;
                    p[k] = new Vector3(Corners[o], Corners[o + 1], Corners[o + 2]);
                    centre += p[k] / 8f;
                }

                for (int f = 0; f < 4; f++)
                {
                    int i0 = v.Count;
                    Vector3 a = p[fa[f]], b = p[fb[f]], b2 = p[fb[f] + 4], a2 = p[fa[f] + 4];
                    v.Add(a);
                    v.Add(b);
                    v.Add(b2);
                    v.Add(a2);
                    c.Add(top);
                    c.Add(top);
                    c.Add(Color.black);
                    c.Add(Color.black);
                    float v0 = f * 0.25f, v1 = v0 + 0.25f;
                    uv.Add(new Vector2(0.009f, v0));
                    uv.Add(new Vector2(0.009f, v1));
                    uv.Add(new Vector2(0.989f, v1));
                    uv.Add(new Vector2(0.989f, v0));
                    // Unity's front face is clockwise seen from the front: make the face's normal point into the prism.
                    var n0 = Vector3.Cross(b - a, b2 - a);
                    var inward = centre - (a + b + a2 + b2) * 0.25f;
                    if (Vector3.Dot(n0, inward) >= 0f)
                    {
                        t.Add(i0);
                        t.Add(i0 + 1);
                        t.Add(i0 + 2);
                        t.Add(i0);
                        t.Add(i0 + 2);
                        t.Add(i0 + 3);
                    }
                    else
                    {
                        t.Add(i0);
                        t.Add(i0 + 2);
                        t.Add(i0 + 1);
                        t.Add(i0);
                        t.Add(i0 + 3);
                        t.Add(i0 + 2);
                    }
                }
            }

            var mesh = new Mesh { name = "RecCenter_LightShafts (rebuilt)" };
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>For the harness: the shafts' corner count and first source corner, as text.</summary>
        public static string Describe()
        {
            return Shafts + " shafts, first source corner (" + Corners[0].ToString("0.00", CultureInfo.InvariantCulture) + ", " + Corners[1].ToString("0.00", CultureInfo.InvariantCulture) + ", " +
                   Corners[2].ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }
    }
}
