using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// The Play board on the back wall (<c>(Dorm_Community_Board)</c>, face 3.05 x 1.81 m). Since 7 Jul 2023 Rec Room's
    /// dorm board showed the Watch Play menu (<c>(PlayCommunityBoard)</c>, bundle 6ef67129 key 3: a top bar and the
    /// room content; rr_latest_places.md §2.2); its UI prefab renders grey without Rec Room's theme scripts, so this is
    /// the same thing made from the watch's parts: a <see cref="RrRoomListPanel"/> board, 2.86 x 1.64 m, titled "Play",
    /// with tabs along the top - "Originals" (every built activity, the Rec Center first, in the watch's order) and the
    /// Rec Center's four category doors (their unbuilt activities marked COMING SOON) - and the watch's room cards.
    /// A card travels there (VR ray/trigger or poke, desktop aim + click, the free cursor through the EventSystem).
    /// </summary>
    public sealed class RrDormBoard : MonoBehaviour
    {
        public const float FaceWidth = 2.86f;
        public const float FaceHeight = 1.64f;

        public RrRoomListPanel Panel { get; private set; }

        /// <summary>The activities on the selected tab (the first tab when built).</summary>
        public List<RrActivity> Tiles
        {
            get { return Panel != null ? Panel.Shown() : new List<RrActivity>(); }
        }

        /// <summary>Cards whose cover picture has arrived.</summary>
        public int ArtLoaded
        {
            get { return Panel != null ? Panel.ArtShown : 0; }
        }

        /// <summary>
        /// Builds the board UI; <paramref name="faceLocal"/> is a point 1 cm in front of the board's face. With
        /// <paramref name="rotationLocal"/> identity the viewer looks +z (a board on the front wall, iteration 3); Y180
        /// turns it for the back wall, where the board hangs since iteration 4 (viewer looking -z).
        /// </summary>
        public static RrDormBoard Build(Transform room, Vector3 faceLocal, Quaternion rotationLocal)
        {
            var go = RrBuild.Group(room, "Play Board", faceLocal, rotationLocal);
            return go.AddComponent<RrDormBoard>();
        }

        /// <summary>
        /// The dorm is the first room built after boot, often before the watch has made its Roboto/Jost fonts; a label
        /// takes its font when it is made, so the board waits for them (20 s at most, then builds with the fallback).
        /// </summary>
        private IEnumerator Start()
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (Watch.RrUiKit.Body == null && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Panel = RrRoomListPanel.CreateBoard(transform, Vector3.zero, Quaternion.Euler(0f, 180f, 0f), FaceWidth, FaceHeight, Tabs());
            var order = PlayOrder();
            RrLog.Info("Dorm", "play board: " + order.Count + " activit" + (order.Count == 1 ? "y" : "ies") + " on Originals, tabs Originals + the Rec Center's 4 categories" +
                (Watch.RrUiKit.Body != null ? "" : " (watch fonts not ready: fallback font)"));
        }

        /// <summary>
        /// The board's first tab: the Rec Center, then every built activity that is not Unlisted in the registry's order (the watch's), not
        /// the dorm. The Rec Center's Featured Rooms door takes the first few of these.
        /// </summary>
        public static List<RrActivity> PlayOrder()
        {
            var list = new List<RrActivity>();
            var rc = RrRooms.Get(RrActivity.RecCenter);
            if (rc != null && rc.Create != null)
            {
                list.Add(RrActivity.RecCenter);
            }

            foreach (var info in RrRooms.All)
            {
                if (info != null && info.Create != null && !info.Unlisted && info.Activity != RrActivity.Dorm && info.Activity != RrActivity.RecCenter)
                {
                    list.Add(info.Activity);
                }
            }

            return list;
        }

        /// <summary>Originals + the four Rec Center categories, all under the title "Play".</summary>
        public static List<RrRoomListPanel.Tab> Tabs()
        {
            var tabs = new List<RrRoomListPanel.Tab>
            {
                new RrRoomListPanel.Tab
                {
                    Label = "Originals",
                    Title = "Play",
                    Subtitle = "Rec Room Originals  ·  or walk through the door to the Rec Center",
                    Items = () => PlayOrder()
                }
            };

            foreach (var t in RrRoomListPanel.CategoryTabs())
            {
                t.Subtitle = t.Title + "  ·  also behind its door in the Rec Center";
                t.Title = "Play";
                tabs.Add(t);
            }

            return tabs;
        }
    }
}
