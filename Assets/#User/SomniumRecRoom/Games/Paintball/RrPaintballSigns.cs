using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// The 2025 Paintball lobby's lettering and lounge (iteration 8: "blank orange sign boards"), at the level's own
    /// spots (level113, scene frame; room-local = scene - <see cref="RrPaintballMaps.LobbyAnchor"/>):
    /// - the four lit door signs ((Door_Sign) Door_Sign_LOD0, drawn by the kit from the install) get the level's own
    ///   TextMeshPro texts, read from the Android level's TMP components (m_text, black): "Paintball" over the
    ///   exit door, "Customize your look!" twice at the skins shop, "Try 'em out!" over the test wall;
    /// - the orange sign boards (Signs_PaintballLobby_Mat, an atlas the install lacks, drawn orange by
    ///   <see cref="RrPaintballDress"/>) get cream lettering: "PRACTICE ARENA" on the arena's gateway arch and
    ///   "PAINTBALL" on the big board over the entrance (both in the 2025 footage, DevNotes/research/
    ///   rr_paintball_footage.md), and by what stands under them (GUESS wording, the atlas is not readable):
    ///   "CHOOSE YOUR SKINS" over the skins shop (the footage's sign), "SPRAYS" over the level's
    ///   [PaintballStorefront_Sprays], "TARGET PRACTICE" over PaintballTargetPractice, "LEADERBOARD" on the
    ///   leaderboard hut; the 4 m band over the lockers stays plain;
    /// - the lounge rug (Rug_Quest, 2.38 x 3.80 m, its Banner_Mat not in the install): red with an orange border and
    ///   a cream roundel ("a red rug with an orange border and the Rec Room bucket logo");
    /// - the lounge armchair: the level gives it ?Couch_Plush_Purple_Mat, which the install lacks (the kit falls
    ///   back to the mesh's own Couch_Plush_Blue_Mat): a purple copy of that material ("purple couches");
    /// - the shooting gallery's male target: the install's PracticeMale_Cutout_LOD0 in place of the Bonky standee the
    ///   install lacks.
    /// Text is on both faces of each board; the one against a wall is hidden.
    /// </summary>
    internal static class RrPaintballSigns
    {
        private struct Board
        {
            public Vector3 C;
            public Quaternion Q;
            public Vector3 E;
            public string Text;

            public Board(float x, float y, float z, float qx, float qy, float qz, float qw, float ex, float ey, float ez, string text)
            {
                C = new Vector3(x, y, z);
                Q = new Quaternion(qx, qy, qz, qw);
                E = new Vector3(ex, ey, ez);
                Text = text;
            }
        }

        /// <summary>The sign boards (G dress struct m=?Signs_PaintballLobby[_Additional]_Mat: centre, rotation, half extents).</summary>
        private static readonly Board[] Boards =
        {
            new Board(-45.9069f, 0.119f, 66.3531f, 0f, -0.7071f, 0f, 0.7071f, 1.4505f, 0.6331f, 0.0492f, "PRACTICE ARENA"),
            new Board(-52.4829f, -0.236f, 56.9155f, 0f, -1f, 0f, 0f, 2.5703f, 0.912f, 0.0871f, "PAINTBALL"),
            new Board(-61.2968f, -1.3063f, 60.3224f, 0f, -1f, 0f, 0f, 1.6586f, 0.2124f, 0.024f, "CHOOSE YOUR SKINS"),
            new Board(-56.1866f, -1.5512f, 79.6744f, 0f, 0f, 0f, 1f, 1.4387f, 0.2452f, 0.0515f, "SPRAYS"),
            new Board(-66.2312f, -1.7075f, 65.6638f, 0f, -0.7071f, 0f, 0.7071f, 1.1789f, 0.1887f, 0.0225f, "TARGET PRACTICE"),
            new Board(-46.5867f, -2.1932f, 60.4244f, 0.6469f, -0.6469f, -0.2856f, 0.2856f, 0.2284f, 0.0355f, 1.6641f, "LEADERBOARD")
        };

        /// <summary>The level's Door_Sign_LOD0 renderers (pose, uniform scale) and their TMP texts.</summary>
        private static readonly Board[] DoorSigns =
        {
            new Board(-52.4918f, -2.9018f, 57.3263f, 0f, -0.7121f, 0f, 0.7021f, 0.7934f, 0f, 0f, "Paintball"),
            new Board(-56.3338f, -2.5523f, 58.0768f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, "Customize your look!"),
            new Board(-56.6688f, -2.8394f, 73.3998f, 0f, 1f, 0f, 0f, 1.3889f, 0f, 0f, "Customize your look!"),
            new Board(-66.1942f, -2.073f, 71.6905f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, "Try 'em out!")
        };

        /// <summary>Door_Sign_LOD0's bounds at scale 1 (install mesh): centre (0, 0.0029, 0), half 0.0344 x 0.1729 x 0.6775.</summary>
        private static readonly Vector3 DoorSignHalf = new Vector3(0.0344f, 0.1729f, 0.6775f);

        private static readonly Vector3 RugScene = new Vector3(-55.6395f, -5.2336f, 77.447f);
        private static readonly Quaternion RugRot = new Quaternion(-0.4507f, -0.5449f, -0.5449f, 0.4507f);
        private static readonly Vector2 RugHalf = new Vector2(1.1889f, 1.8985f);

        /// <summary>G standee Standee_Bonky_fbx (TargetPracticeTargetMaleStandee): the target root, feet on the ground.</summary>
        private static readonly Vector3 BonkyScene = new Vector3(-69.983f, -5.3f, 67.438f);
        private static readonly Quaternion BonkyRot = new Quaternion(0f, 0.7699f, 0f, 0.6382f);

        private static readonly Color Cream = new Color(0.98f, 0.93f, 0.80f);
        private static readonly Color Ink = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color CouchPurple = new Color(0.46f, 0.24f, 0.66f);

        public static void Build(Transform root, RrPaintballLevel level)
        {
            var g = RrBuild.Group(root, "Signs").transform;
            int labels = 0;
            foreach (var b in Boards)
            {
                labels += BoardText(g, b.Text, RrPaintballLobby.Local(b.C), b.Q, b.E, Cream, 0.86f, 0.62f);
            }

            foreach (var d in DoorSigns)
            {
                float s = d.E.x;
                var c = RrPaintballLobby.Local(d.C) + d.Q * new Vector3(0f, 0.0029f * s, 0f);
                labels += BoardText(g, d.Text, c, d.Q, DoorSignHalf * s, Ink, 0.88f, 0.62f);
            }

            Rug(root);
            MaleStandee(root);
            int tinted = TintCouches(level);
            RrLog.Info("Paintball", "lobby signs: " + labels + " labels on " + Boards.Length + " boards and " + DoorSigns.Length + " door signs (the level's own texts: Paintball, " +
                                    "Customize your look! x2, Try 'em out!), lounge rug, " + tinted + " couch material(s) purple");
        }

        /// <summary>Lettering on both big faces of a board: centred, auto-sized to the face, readable from each side.</summary>
        private static int BoardText(Transform g, string text, Vector3 c, Quaternion q, Vector3 e, Color ink, float fillW, float fillH)
        {
            Vector3[] ax = { q * Vector3.right, q * Vector3.up, q * Vector3.forward };
            float[] ex = { e.x, e.y, e.z };
            int thin = 0;
            for (int i = 1; i < 3; i++)
            {
                if (ex[i] < ex[thin])
                {
                    thin = i;
                }
            }

            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            // The in-plane axis nearest world up is the text's up; the other one runs along the lettering.
            int upI = Mathf.Abs(ax[a].y) >= Mathf.Abs(ax[b].y) ? a : b;
            if (Mathf.Abs(ax[a].y) < 0.3f && Mathf.Abs(ax[b].y) < 0.3f)
            {
                upI = ex[a] <= ex[b] ? a : b;
            }

            int alongI = upI == a ? b : a;
            Vector3 up = ax[upI].y < 0f ? -ax[upI] : ax[upI];
            int made = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 n = ax[thin] * side;
                var go = new GameObject("Sign " + text + (side > 0 ? " A" : " B"));
                go.transform.SetParent(g, false);
                go.transform.localPosition = c + n * (ex[thin] + 0.008f);
                go.transform.localRotation = Quaternion.LookRotation(-n, up);
                var tmp = go.AddComponent<TextMeshPro>();
                RrText.Style(tmp, true);
                tmp.text = text;
                tmp.color = ink;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.05f;
                tmp.fontSizeMax = 80f;
                tmp.rectTransform.sizeDelta = new Vector2(ex[alongI] * 2f * fillW, ex[upI] * 2f * fillH);
                made++;
            }

            return made;
        }

        /// <summary>The lounge rug on the level's Rug_Quest: an orange border, the red field and a cream roundel.</summary>
        private static void Rug(Transform root)
        {
            var c = RrPaintballLobby.Local(RugScene);
            Vector3 n = RugRot * Vector3.forward;
            if (n.y < 0f)
            {
                n = -n;
            }

            var g = RrBuild.Group(root, "Lounge Rug").transform;
            RrBuild.Box(g, "Rug Border", c + n * 0.008f, RugRot, new Vector3(RugHalf.x * 2f, RugHalf.y * 2f, 0.012f), RrLook.Solid(new Color(0.93f, 0.50f, 0.15f)), false);
            RrBuild.Box(g, "Rug Field", c + n * 0.012f, RugRot, new Vector3(RugHalf.x * 2f - 0.34f, RugHalf.y * 2f - 0.34f, 0.012f), RrLook.Solid(new Color(0.72f, 0.14f, 0.13f)), false);
            RrBuild.Cylinder(g, "Rug Roundel", c + n * 0.02f, 0.52f, 0.008f, RrLook.Solid(Cream), false);
            RrBuild.Cylinder(g, "Rug Roundel Ring", c + n * 0.023f, 0.40f, 0.008f, RrLook.Solid(new Color(0.72f, 0.14f, 0.13f)), false);
            RrBuild.Cylinder(g, "Rug Roundel Dot", c + n * 0.026f, 0.28f, 0.008f, RrLook.Solid(Cream), false);
        }

        /// <summary>
        /// The gallery's male target (PaintballTargetPractice/Targets/TargetPracticeTargetMaleStandee) is Rec Room's
        /// Standee_Bonky, whose mesh and texture the install lacks: the install's own male practice cutout
        /// (PracticeMale_Cutout_LOD0, Standee_Male01_mat; the female one beside it is PracticeFemale_Cutout_LOD0) stands
        /// at its pose, feet on the ground as the female cutout's are.
        /// </summary>
        private static void MaleStandee(Transform root)
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                return;
            }

            var at = RrPaintballLobby.Local(BonkyScene);
            assets.Load<Mesh>("PracticeMale_Cutout_LOD0", 43, mesh =>
            {
                if (mesh == null || root == null)
                {
                    return;
                }

                assets.Load<Material>("Standee_Male01_mat", 21, cutoutMat =>
                {
                    if (root == null)
                    {
                        return;
                    }

                    var go = new GameObject("Gallery Standee (PracticeMale_Cutout)");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = at;
                    go.transform.localRotation = BonkyRot;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = cutoutMat != null ? RrMaterials.Fix(cutoutMat) : RrLook.Solid(new Color(0.85f, 0.75f, 0.6f));
                    RrLog.Info("Paintball", "gallery: the install's male practice cutout stands at the Bonky target " + at.ToString("F1"));
                });
            });
        }

        /// <summary>The level's couch materials the install lacks (?Couch_Plush_Purple_Mat) stand in as Couch_Plush_Blue_Mat: recoloured.</summary>
        private static int TintCouches(RrPaintballLevel level)
        {
            if (level == null || level.Kit == null || level.Kit.Root == null)
            {
                return 0;
            }

            var copies = new Dictionary<Material, Material>();
            foreach (var r in level.Kit.Root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.StartsWith("Couch_Plush_Blue"))
                    {
                        continue;
                    }

                    Material purple;
                    if (!copies.TryGetValue(m, out purple))
                    {
                        purple = new Material(m) { name = "Couch_Plush_Purple (Rr)" };
                        if (purple.HasProperty("_Color"))
                        {
                            purple.SetColor("_Color", CouchPurple);
                        }

                        if (purple.HasProperty("_BaseColor"))
                        {
                            purple.SetColor("_BaseColor", CouchPurple);
                        }

                        copies[m] = purple;
                    }

                    mats[i] = purple;
                    changed = true;
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }

            return copies.Count;
        }
    }
}
