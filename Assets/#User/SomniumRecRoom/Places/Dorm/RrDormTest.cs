using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// Play-mode harness hooks for the dorm (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm.RrDormTest.X"}</c>).
    /// Each logs one "DormTest:" line with its outcome. They need the dorm to be the current room (goto Dorm first).
    /// </summary>
    public static class RrDormTest
    {
        /// <summary>One line: what was built, what is missing, mirror/radio/board state.</summary>
        public static void Report()
        {
            var room = RrDormRoom.Current;
            if (room == null)
            {
                RrLog.Warn("DormTest", "report: no dorm");
                return;
            }

            RrLog.Info("DormTest", "report: props " + room.Props.Count + ", missing [" + string.Join(", ", room.Missing) + "]" +
                ", door " + (room.Door != null && room.Door.Built) +
                ", mirror " + (room.Mirror != null ? (room.Mirror.Reflecting ? "reflecting, " + room.Mirror.FramesRendered + " frames" : "stylised") : "none") +
                ", radio " + (room.Radio != null ? (room.Radio.Playing ?? "silent") : "none") +
                ", board " + (room.Board != null ? room.Board.Tiles.Count + " tiles, " + room.Board.ArtLoaded + " art" : "none") +
                ", ladder " + (room.Ladder != null) + ", hoops " + RrHoop.All.Count +
                ", posters " + room.PostersShown + " notebook / " + room.PostersFallback + " fallback" +
                ", marker set " + (room.Named(RrDormRoom.MarkerSet) != null) + ", whiteboard " + (room.Named(RrDormRoom.Whiteboard) != null) +
                ", welcome mat " + (room.Named("(SandboxWelcomeMat)") != null) + ", dresser " + (room.Named("(Dorm_Dresser)") != null) +
                ", armchairs " + Count(room, "(Sandbox_CouchChair)") + ", help \"" + room.HelpText + "\"");
        }

        private static int Count(RrDormRoom room, string prefab)
        {
            int n = 0;
            foreach (var inst in room.GetComponentsInChildren<RrInstance>(true))
            {
                if (inst.PrefabName == prefab)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Shows the Play board's "Sports & PvP" tab (index 1) and reports its cards.</summary>
        public static IEnumerator BoardSports()
        {
            var room = RrDormRoom.Current;
            var panel = room != null && room.Board != null ? room.Board.Panel : null;
            if (panel == null)
            {
                RrLog.Warn("DormTest", "board: no panel");
                yield break;
            }

            panel.SelectTab(1);
            yield return new WaitForSeconds(1.2f);
            RrLog.Info("DormTest", "board tab " + panel.SelectedTab + ": " + panel.CardCount + " cards, " + panel.PickableCount + " built, art " + panel.ArtShown +
                "; " + panel.TestAim(Rooms.RrActivity.Paintball, 2f));
        }

        /// <summary>Presses the help panel's "Help" button (its note shows for 12 s).</summary>
        public static void PressHelp()
        {
            var room = RrDormRoom.Current;
            if (room == null)
            {
                RrLog.Warn("DormTest", "help: no dorm");
                return;
            }

            room.ShowHelp(0);
            RrLog.Info("DormTest", "help: \"" + room.HelpText + "\"");
        }

        /// <summary>Stands the player 1.6 m in front of the mirror (front wall), facing it (+z), so the reflection renders.</summary>
        public static IEnumerator StandAtMirror()
        {
            var room = RrDormRoom.Current;
            if (room == null)
            {
                yield break;
            }

            bool done = false;
            var t = room.transform;
            RrPlayer.Teleport(t.TransformPoint(RrDormRoom.MirrorPosition.x, 0.05f, RrDormRoom.MirrorPosition.z - 1.6f), t.eulerAngles.y, () => done = true);
            float until = Time.realtimeSinceStartup + 3f;
            while (!done && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            RrLog.Info("DormTest", "at mirror: " + (room.Mirror != null && room.Mirror.Reflecting ? "reflecting (" + room.Mirror.FramesRendered + " frames)" : "not reflecting"));
        }

        /// <summary>Lobs the basketball into the hoop from 2.9 m away (the -x side, clear of the bunk bed) and waits for a basket.</summary>
        public static IEnumerator ShootBasket()
        {
            var room = RrDormRoom.Current;
            var inst = room != null ? room.Named("(Basketball)") : null;
            var ball = inst != null ? inst.GetComponent<RrBall>() : null;
            var hoop = RrHoop.All.Count > 0 ? RrHoop.All[0] : null;
            var body = inst != null ? inst.GetComponent<Rigidbody>() : null;
            if (ball == null || hoop == null || body == null)
            {
                RrLog.Warn("DormTest", "basket: ball " + (ball != null) + ", hoop " + (hoop != null) + ", body " + (body != null));
                yield break;
            }

            int before = ball.Scores;
            // Aim the ball's collider centre (not the prefab pivot) through the rim centre.
            var sphere = inst.GetComponentInChildren<SphereCollider>();
            Vector3 offset = sphere != null ? sphere.bounds.center - body.position : Vector3.zero;
            Vector3 from = hoop.RimCenter + room.transform.TransformDirection(new Vector3(-2.2f, 0.2f, 1.0f)) - offset;
            Vector3 to = hoop.RimCenter + Vector3.up * 0.02f - offset;
            RrLog.Info("DormTest", "basket: collider offset " + offset.ToString("F3") + ", rim " + room.transform.InverseTransformPoint(hoop.RimCenter).ToString("F3"));
            const float T = 1.25f;  // a high lob: the ball drops into the rim at ~70 degrees
            float damping = body.linearDamping;
            body.linearDamping = 0f;
            body.isKinematic = false;
            body.position = from;
            ball.transform.position = from;
            body.linearVelocity = (to - from) / T - 0.5f * Physics.gravity * (T + Time.fixedDeltaTime);
            body.angularVelocity = Vector3.zero;
            float until = Time.time + 5f;  // the score trigger fires as the ball leaves the net: ~3.5 s after the throw
            float lastY = float.MaxValue;
            string crossing = "never crossed the rim plane";
            while (ball.Scores == before && Time.time < until)
            {
                Vector3 c = body.position + offset;
                if (lastY > hoop.RimCenter.y && c.y <= hoop.RimCenter.y && crossing.StartsWith("never"))
                {
                    Vector3 d = c - hoop.RimCenter;
                    crossing = "crossed the rim plane " + new Vector2(d.x, d.z).magnitude.ToString("0.000") + " m from its centre";
                }

                lastY = c.y;
                yield return null;
            }

            RrLog.Info("DormTest", "basket: ball " + crossing);

            body.linearDamping = damping;
            RrLog.Info("DormTest", "basket: " + (ball.Scores > before ? "SCORED" : "missed") + " (ball scores " + ball.Scores +
                ", rim hits " + ball.RimHits + ", board hits " + ball.BoardHits + ", bounces " + ball.Bounces + ")");
        }

        /// <summary>Uses the ladder (up from the floor, down from the loft) and reports where the player's feet are.</summary>
        public static IEnumerator ClimbLadder()
        {
            var room = RrDormRoom.Current;
            if (room == null || room.Ladder == null)
            {
                RrLog.Warn("DormTest", "ladder: none");
                yield break;
            }

            room.Ladder.Climb("test");
            yield return new WaitForSeconds(1.2f);
            RrLog.Info("DormTest", "ladder: feet at room y " + room.transform.InverseTransformPoint(RrPlayer.FeetPosition).y.ToString("0.00") + " after " + room.Ladder.Climbs + " climb(s)");
        }

        /// <summary>Presses "next" on the radio.</summary>
        public static IEnumerator RadioNext()
        {
            var room = RrDormRoom.Current;
            if (room == null || room.Radio == null)
            {
                RrLog.Warn("DormTest", "radio: none");
                yield break;
            }

            room.Radio.Next();
            yield return new WaitForSeconds(2f);
            RrLog.Info("DormTest", "radio: " + (room.Radio.Playing ?? "silent") + " (track " + room.Radio.Index + ")");
        }

        // ------------------------------------------------------------------ layout check (iteration 4)

        private struct Item
        {
            public string Name;
            public Bounds B;
            public bool Mounted;
            public bool Hanging;
        }

        private const float Tol = 0.01f;

        /// <summary>
        /// Checks the placed layout in the room frame (renderer bounds) and logs one Warn line per problem plus a summary:
        /// <list type="bullet">
        /// <item>walls: inside the interior (x -4.94..4.77, z -2.96..2.96; z -2.72..2.72 where the loft walls stand,
        /// x below -2.57), out of the loft slab (y 2.64..2.75 for x below -2.63) and out of the colliders of the pillar,
        /// the loft beam, railing and shelves (1 cm tolerance) and of the wall stripe (1 mm);</item>
        /// <item>the ladder gap: the climbing space (the ladder's bounds widened 0.35 m along x, floor to 3.5 m) holds
        /// nothing else;</item>
        /// <item>floating: every standing object's lowest point within 3 cm of the floor, the loft floor, the desk top or
        /// the coffee table's top (wall-mounted and hanging pieces excepted), and nothing lies under the desk or table;</item>
        /// <item>pairs: two objects' bounds overlapping by more than 3 cm on every axis (items on the desk or table
        /// excepted; overlaps with a plant are counted apart - a crown's bounds are much larger than its leaves).</item>
        /// </list>
        /// Run it a few seconds after arriving, when the props have settled.
        /// </summary>
        public static void Layout()
        {
            var room = RrDormRoom.Current;
            if (room == null)
            {
                RrLog.Warn("DormTest", "layout: no dorm");
                return;
            }

            var t = room.transform;
            var items = new List<Item>();
            var solids = new List<KeyValuePair<string, Bounds>>();
            foreach (var inst in room.GetComponentsInChildren<RrInstance>(true))
            {
                string n = inst.PrefabName;
                if (RrDormRoom.IsArchitecture(n))
                {
                    // The wall stripe's six boxes are 6 mm thick bands 8-22 mm off the walls (y 1.43..1.85).
                    if (n == "(Dorm_Loft_Shelves)" || n == "(Dorm_Loft_Railing)" || n == "(Dorm_Loft_Beam)" || n == "(Dorm_Wall_Pillar)" || n == "(Dorm_Wall_Stripe)")
                    {
                        foreach (var c in inst.GetComponentsInChildren<Collider>())
                        {
                            if (c.enabled && !c.isTrigger)
                            {
                                solids.Add(new KeyValuePair<string, Bounds>(n, Local(t, c.bounds)));
                            }
                        }
                    }

                    continue;
                }

                var wb = inst.WorldBounds();
                if (wb.size == Vector3.zero)
                {
                    continue;
                }

                var lb = Local(t, wb);
                if (n == "(Dorm_Ladder)")
                {
                    // Its mesh is authored leaning and stood up by a child rotation, so its renderer bounds are 1.8 m
                    // deep; the rails themselves are 0.06 m thick, 0.48 m apart and 2.81 m tall (rr_dorm_core §2.3).
                    var p = RrDormRoom.LadderPosition;
                    lb = new Bounds(new Vector3(p.x, 1.405f, p.z), new Vector3(0.06f, 2.81f, 0.54f));
                }
                else if (n.Contains("Houseplant"))
                {
                    lb = Crown(inst, lb);
                }

                items.Add(new Item
                {
                    Name = n,
                    B = lb,
                    Mounted = n == "(Dorm_FreeMirror)" || n == "(Dorm_Campus_Map)" || n == "(Sandbox_CodeOfConduct_Poster)" || n == "(Dorm_Community_Board)" ||
                              n == "(Sandbox_MuteGesture_Poster)" || n == "(Sandbox_ShakeHands_Poster)",
                    Hanging = n == "(Dorm_Fluorescent_Light)" || n == "(Basketballhoop)"
                });
            }

            // Our own pieces: the door (its closed solid box - open, the leaves swing 0.43 m into the room over its middle
            // 1.8 m), the two panels and the notebook posters.
            var solidBox = room.Door != null ? room.Door.transform.Find("Solid") : null;
            var doorCollider = solidBox != null ? solidBox.GetComponent<BoxCollider>() : null;
            if (doorCollider != null)
            {
                items.Add(new Item { Name = "door", B = Local(t, doorCollider.bounds), Mounted = true });
            }
            else
            {
                AddOwn(items, t, room.Door != null ? room.Door.gameObject : null, "door");
            }

            AddOwn(items, t, Child(t, "Mirror Panel"), "mirror panel");
            AddOwn(items, t, Child(t, "Help Panel"), "help panel");
            AddOwn(items, t, Child(t, "Poster " + RrDormRoom.StopGesturePoster), "poster stop");
            AddOwn(items, t, Child(t, "Poster " + RrDormRoom.ShakeHandsPoster), "poster hands");

            Bounds bed = default(Bounds), table = default(Bounds), ladder = default(Bounds);
            bool hasBed = false, hasTable = false, hasLadder = false;
            foreach (var it in items)
            {
                if (it.Name == "(Dorm_Bunk_Bed)")
                {
                    bed = it.B;
                    hasBed = true;
                }
                else if (it.Name == "(Sandbox_Table_Short)")
                {
                    table = it.B;
                    hasTable = true;
                }
                else if (it.Name == "(Dorm_Ladder)")
                {
                    ladder = it.B;
                    hasLadder = true;
                }
            }

            float tableTop = hasTable ? table.max.y : -1f;
            var deskMin = new Vector2(RrDormRoom.BedPosition.x - 1.245f, RrDormRoom.BedPosition.z - 0.71f);
            var deskMax = new Vector2(RrDormRoom.BedPosition.x + 1.245f, RrDormRoom.BedPosition.z + 0.33f);
            var climb = hasLadder ? new Bounds(ladder.center + Vector3.up * (1.75f - ladder.center.y), new Vector3(ladder.size.x + 0.7f, 3.5f, ladder.size.z)) : default(Bounds);

            int walls = 0, gap = 0, floating = 0, overlaps = 0, crowns = 0, onDesk = 0;
            var crownList = new List<string>();
            foreach (var it in items)
            {
                var b = it.B;
                bool loftSide = b.min.x < -2.57f;
                float zLimit = loftSide ? 2.72f : 2.96f;
                if (b.min.x < -4.94f - Tol || b.max.x > 4.77f + Tol || b.min.z < -zLimit - Tol || b.max.z > zLimit + Tol)
                {
                    walls++;
                    RrLog.Warn("DormTest", "layout: " + it.Name + " reaches into a wall " + Show(b));
                }

                if (b.min.x < -2.63f && b.max.y > RrDormRoom.LoftUndersideY + Tol && b.min.y < RrDormRoom.LoftFloorY - Tol)
                {
                    walls++;
                    RrLog.Warn("DormTest", "layout: " + it.Name + " cuts the loft floor " + Show(b));
                }

                foreach (var s in solids)
                {
                    // The stripe is checked to the millimetre (it drew over the Code of Conduct 2 mm behind it); the
                    // door's frame covers it by construction (its solid box is 6.5 mm off the wall, the stripe 8 mm).
                    bool stripe = s.Key == "(Dorm_Wall_Stripe)";
                    if (stripe && it.Name == "door")
                    {
                        continue;
                    }

                    if (Overlap(b, s.Value, stripe ? 0.001f : Tol))
                    {
                        walls++;
                        RrLog.Warn("DormTest", "layout: " + it.Name + " overlaps " + s.Key + " " + Show(b) + " vs " + Show(s.Value));
                    }
                }

                if (hasLadder && it.Name != "(Dorm_Ladder)" && Overlap(b, climb, Tol))
                {
                    gap++;
                    RrLog.Warn("DormTest", "layout: " + it.Name + " stands in the ladder's climbing space " + Show(b));
                }

                if (!it.Mounted && !it.Hanging && it.Name != "door" && it.Name != "(Dorm_Bunk_Bed)")
                {
                    float y = b.min.y;
                    bool inDesk = hasBed && b.center.x > deskMin.x && b.center.x < deskMax.x && b.center.z > deskMin.y && b.center.z < deskMax.y;
                    bool inTable = hasTable && b.center.x > table.min.x && b.center.x < table.max.x && b.center.z > table.min.z && b.center.z < table.max.z &&
                                   it.Name != "(Sandbox_Table_Short)";
                    bool supported = Mathf.Abs(y) < 0.03f || (b.center.x < -2.63f && Mathf.Abs(y - RrDormRoom.LoftFloorY) < 0.03f) ||
                                     (inDesk && Mathf.Abs(y - RrDormRoom.DeskTopY) < 0.03f) || (inTable && Mathf.Abs(y - tableTop) < 0.03f);
                    if (inDesk && Mathf.Abs(y - RrDormRoom.DeskTopY) < 0.03f)
                    {
                        onDesk++;
                    }

                    if (!supported)
                    {
                        floating++;
                        RrLog.Warn("DormTest", "layout: " + it.Name + " floats (lowest point y " + y.ToString("0.000") + ") " + Show(b));
                    }
                    else if ((inDesk || inTable) && y < 0.03f)
                    {
                        floating++;
                        RrLog.Warn("DormTest", "layout: " + it.Name + " lies on the floor under the " + (inDesk ? "desk" : "table") + " " + Show(b));
                    }
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                {
                    var a = items[i];
                    var c = items[j];
                    if (!Overlap(a.B, c.B, 0.03f) || Rests(a, c, RrDormRoom.DeskTopY, "(Dorm_Bunk_Bed)") || Rests(a, c, tableTop, "(Sandbox_Table_Short)") ||
                        Rests(a, c, 0f, "(Dorm_Rug)"))
                    {
                        continue;
                    }

                    if (a.Name.Contains("Houseplant") || c.Name.Contains("Houseplant"))
                    {
                        crowns++;
                        crownList.Add(a.Name + " x " + c.Name + " " + Depth(a.B, c.B));
                        continue;
                    }

                    overlaps++;
                    RrLog.Warn("DormTest", "layout: " + a.Name + " overlaps " + c.Name + " by " + Depth(a.B, c.B) + ": " + Show(a.B) + " vs " + Show(c.B));
                }
            }

            RrLog.Info("DormTest", "layout: " + items.Count + " objects checked against " + solids.Count + " loft, pillar and stripe colliders; walls " + walls + ", ladder gap " + gap +
                ", floating " + floating + ", overlaps " + overlaps + ", " + onDesk + " on the desk" + (crowns > 0 ? "; plant crowns touching: " + string.Join("; ", crownList) : ""));
            foreach (var it in items)
            {
                if (it.Name == "door" || it.Name == "(Dorm_Bunk_Bed)" || it.Name == "(Dorm_Ladder)" || it.Name == RrDormRoom.Whiteboard || it.Name == "(Sandbox_CouchChair)" ||
                    it.Name == "(Sandbox_Table_Short)" || it.Name == "(Dorm_Community_Board)" || it.Name == "(Dorm_FreeMirror)" || it.Name == "(Sandbox_Houseplant_Dracaena)")
                {
                    RrLog.Info("DormTest", "layout: " + it.Name + " " + Show(it.B));
                }
            }
        }

        /// <summary>
        /// A plant's crown as a round footprint: renderer bounds of a turned plant are its mesh box's corners (the
        /// dracaena's 1.09 x 1.11 m box at 147 degrees reads 1.51 m wide), but leaves fan out in a circle, so the radius is
        /// the unturned half-width (Renderer.localBounds x/z times the scale) around the bounds' centre.
        /// </summary>
        private static Bounds Crown(RrInstance inst, Bounds turned)
        {
            float r = 0f;
            foreach (var rend in inst.GetComponentsInChildren<Renderer>())
            {
                if (!rend.enabled || rend is ParticleSystemRenderer)
                {
                    continue;
                }

                var e = rend.localBounds.extents;
                var s = rend.transform.lossyScale;
                r = Mathf.Max(r, Mathf.Max(e.x * Mathf.Abs(s.x), e.z * Mathf.Abs(s.z)));
            }

            if (r <= 0f)
            {
                return turned;
            }

            return new Bounds(turned.center, new Vector3(2f * r, turned.size.y, 2f * r));
        }

        /// <summary>True when one of the pair is <paramref name="support"/> and the other rests on its surface at <paramref name="top"/>.</summary>
        private static bool Rests(Item a, Item b, float top, string support)
        {
            return (a.Name == support && b.B.min.y > top - 0.03f) || (b.Name == support && a.B.min.y > top - 0.03f);
        }

        private static void AddOwn(List<Item> items, Transform room, GameObject go, string name)
        {
            if (go == null)
            {
                return;
            }

            bool any = false;
            var b = default(Bounds);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (any)
            {
                items.Add(new Item { Name = name, B = Local(room, b), Mounted = true });
            }
        }

        private static GameObject Child(Transform room, string name)
        {
            var c = room.Find(name);
            return c != null ? c.gameObject : null;
        }

        /// <summary>A world AABB as an AABB in the room's frame (its 8 corners).</summary>
        private static Bounds Local(Transform room, Bounds w)
        {
            var b = new Bounds(room.InverseTransformPoint(w.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                b.Encapsulate(room.InverseTransformPoint(new Vector3((i & 1) != 0 ? w.max.x : w.min.x, (i & 2) != 0 ? w.max.y : w.min.y, (i & 4) != 0 ? w.max.z : w.min.z)));
            }

            return b;
        }

        private static bool Overlap(Bounds a, Bounds b, float tolerance)
        {
            return a.max.x - tolerance > b.min.x && b.max.x - tolerance > a.min.x &&
                   a.max.y - tolerance > b.min.y && b.max.y - tolerance > a.min.y &&
                   a.max.z - tolerance > b.min.z && b.max.z - tolerance > a.min.z;
        }

        private static string Depth(Bounds a, Bounds b)
        {
            float dx = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float dy = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
            float dz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            return "(" + dx.ToString("0.00") + ", " + dy.ToString("0.00") + ", " + dz.ToString("0.00") + ")";
        }

        private static string Show(Bounds b)
        {
            return "x " + b.min.x.ToString("0.00") + ".." + b.max.x.ToString("0.00") + " y " + b.min.y.ToString("0.00") + ".." + b.max.y.ToString("0.00") +
                   " z " + b.min.z.ToString("0.00") + ".." + b.max.z.ToString("0.00");
        }
    }
}
