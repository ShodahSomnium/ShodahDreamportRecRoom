using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Play-mode harness hooks for the category doors (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter.RrDoorsTest.X"}</c>,
    /// scenario doors_test.json, "arrival": true). <see cref="Spawn"/> puts the four category doors and the Magic Door
    /// in a row on the Arrival floor, 2.2 m apart at z 4 facing -z (towards the harness camera at (0, 1.6, -3)), on a
    /// brick-red test wall 0.06 m behind them, as a room would place them. Each hook logs one "DoorsTest:" line.
    /// Nothing here runs unless called.
    /// </summary>
    public static class RrDoorsTest
    {
        public const float Z = 4f;
        public const float Spacing = 2.2f;

        private static readonly List<RrCategoryDoorView> Doors = new List<RrCategoryDoorView>();
        private static GameObject _root;

        private static void Log(string text)
        {
            RrLog.Info("Doors", "DoorsTest: " + text);
        }

        private static RrCategoryDoorView Door(int i)
        {
            return i >= 0 && i < Doors.Count ? Doors[i] : null;
        }

        /// <summary>Door i's floor centre (0..3 the categories in enum order, 4 the Magic Door).</summary>
        public static Vector3 DoorPoint(int i)
        {
            return new Vector3((i - 2) * Spacing, 0f, Z);
        }

        public static IEnumerator Spawn()
        {
            Doors.Clear();
            if (_root != null)
            {
                Object.Destroy(_root);
            }

            var parent = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _root = new GameObject("Rr Doors Test");
            _root.transform.SetParent(parent, false);
            var t = _root.transform;
            RrBuild.Box(t, "Brick Wall", new Vector3(0f, 1.7f, Z + 0.06f + 0.1f), new Vector3(11.6f, 3.4f, 0.2f), RrLook.Solid(new Color(0.60f, 0.24f, 0.19f)));
            for (int i = 0; i < 4; i++)
            {
                var go = RrCategoryDoor.Create(t, DoorPoint(i), 180f, (RrDoorCategory)i);
                Doors.Add(go.GetComponent<RrCategoryDoorView>());
            }

            var magic = RrCategoryDoor.CreateMagicDoor(t, DoorPoint(4), 180f);
            Doors.Add(magic.GetComponent<RrCategoryDoorView>());

            float until = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < until && Doors.Exists(x => x != null && !x.Built))
            {
                yield return null;
            }

            // The watch's fonts (the list's Roboto/Jost) arrive with the watch.
            until = Time.realtimeSinceStartup + 30f;
            while (Watch.RrUiKit.Body == null && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            var sb = new StringBuilder();
            foreach (var d in Doors)
            {
                sb.Append(d.Label).Append(d.Built ? (d.Procedural ? " procedural" : " rec-room") : " NOT BUILT").Append("; ");
            }

            Log("spawned " + Doors.Count + " doors: " + sb + "fonts " + (Watch.RrUiKit.Body != null ? Watch.RrUiKit.Body.name : "none"));
        }

        /// <summary>Stands the player 1.8 m in front of the Sports & PvP door, facing it (it swings ajar and glows).</summary>
        public static IEnumerator StandAtSports()
        {
            bool done = false;
            Vector3 p = DoorPoint(0) + new Vector3(0f, 0.05f, -1.8f);
            RrPlayer.Teleport(p, 0f, () => done = true);
            float until = Time.realtimeSinceStartup + 3f;
            while (!done && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            Log("player at " + RrPlayer.FeetPosition.ToString("F2") + " (teleport " + (done ? "done" : "timed out") + ")");
        }

        /// <summary>Uses the Sports & PvP door (as a click would) and waits for the list's cards.</summary>
        public static IEnumerator OpenSports()
        {
            var d = Door(0);
            if (d == null)
            {
                Log("open: no door");
                yield break;
            }

            d.Use("test");
            yield return new WaitForSeconds(1.5f);
            var panel = RrRoomListPanel.Current;
            Log("open: " + (panel != null ? "list open, tab " + panel.SelectedTab + ", " + panel.CardCount + " cards, " + panel.PickableCount + " built, art " + panel.ArtShown + ", shown [" + string.Join(", ", panel.Shown()) + "]" : "NO LIST"));
        }

        /// <summary>Hovers the Paintball card and checks the list's own ray and poke hit-tests against it.</summary>
        public static void HoverPaintball()
        {
            var panel = RrRoomListPanel.Current;
            if (panel == null)
            {
                Log("hover: no list");
                return;
            }

            panel.TestHover(RrActivity.Paintball);
            Log("aim: " + panel.TestAim(RrActivity.Paintball, 1.5f) + "; " + panel.TestAim(RrActivity.LaserTag, 1.5f));
        }

        /// <summary>Picks a COMING SOON card (Laser Tag): must not travel.</summary>
        public static void PickComingSoon()
        {
            var panel = RrRoomListPanel.Current;
            bool travelled = panel != null && panel.Pick(RrActivity.LaserTag, "test");
            Log("coming soon pick: " + (travelled ? "TRAVELLED (wrong)" : "no travel (right)") + ", busy " + RrTravel.Busy);
        }

        /// <summary>Shows the Featured Rooms tab.</summary>
        public static IEnumerator ShowFeatured()
        {
            var panel = RrRoomListPanel.Current;
            if (panel == null)
            {
                Log("featured: no list");
                yield break;
            }

            panel.SelectTab((int)RrDoorCategory.FeaturedRooms);
            yield return new WaitForSeconds(1.5f);
            Log("featured: " + panel.CardCount + " cards, " + panel.PickableCount + " built, shown [" + string.Join(", ", panel.Shown()) + "]");
        }

        /// <summary>Closes the list (as the Close button does) and waits for its hide animation.</summary>
        public static IEnumerator CloseList()
        {
            var panel = RrRoomListPanel.Current;
            if (panel != null)
            {
                panel.Close("test");
            }

            yield return new WaitForSeconds(0.6f);
            Log("closed: current " + (RrRoomListPanel.Current != null ? "still open" : "none"));
        }

        /// <summary>Rolls the Magic Door without travelling and reports the pick after the roll.</summary>
        public static IEnumerator RollMagic()
        {
            var d = Door(4);
            if (d == null)
            {
                Log("magic: no door");
                yield break;
            }

            d.RollOnly = true;
            d.Use("test");
            yield return new WaitForSeconds(2.2f);
            Log("magic: rolled " + d.LastRoll + " of [" + string.Join(", ", RrCategoryDoor.MagicCandidates()) + "], rolling " + d.Rolling);
        }

        /// <summary>Removes the test doors and wall (before a scenario travels on).</summary>
        public static void Clear()
        {
            var panel = RrRoomListPanel.Current;
            if (panel != null)
            {
                panel.Close("test clear");
            }

            if (_root != null)
            {
                Object.Destroy(_root);
            }

            _root = null;
            Doors.Clear();
            Log("cleared");
        }

        public static void Report()
        {
            var sb = new StringBuilder();
            foreach (var d in Doors)
            {
                if (d != null)
                {
                    sb.Append(d.Label).Append(": uses ").Append(d.Uses).Append("; ");
                }
            }

            foreach (RrDoorCategory c in System.Enum.GetValues(typeof(RrDoorCategory)))
            {
                int built = 0;
                var list = RrCategoryDoor.Activities(c);
                foreach (var a in list)
                {
                    if (RrCategoryDoor.IsBuilt(a))
                    {
                        built++;
                    }
                }

                sb.Append(RrCategoryDoor.Sign(c)).Append(" ").Append(built).Append("/").Append(list.Count).Append(" built; ");
            }

            Log("report: " + sb);
        }
    }
}
