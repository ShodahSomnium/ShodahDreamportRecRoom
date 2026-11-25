using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// Test and diagnostic entry points for the play-mode harness (scenario "call" steps). Nothing in the world
    /// calls these; they exist so the watch can be opened and photographed without a Somnium client, a headset or
    /// a keyboard.
    /// </summary>
    public static class RrWatchTest
    {
        private static readonly string[] Probe =
        {
            "83:sfx_rrui_watch_ui_open_paper_01", "83:sfx_rrui_watch_ui_open_stereo_01", "83:comped_sfx_rrui_watch_ui_open_stereo_01",
            "83:sfx_rrui_watch_ui_close_paper_01", "83:sfx_rrui_watch_open_cloth_01", "83:sfx_rrui_watch_ui_transition_open_01",
            "83:sfx_rrui_watch_ui_select_click_01", "83:sfx_rrui_watch_ui_button_hover_general_01", "83:Watch2_open",
            "43:Wrist_Watch", "21:Watch_Wrist_mat", "21:Watch_Wrist_Hand_mat", "128:Roboto-Regular", "128:Jost-500-Medium",
            "213:icon_Home", "213:Illustration_Menu_RecCenter", "213:Illustration_Friends", "213:icon_ProfilePic",
            "28:GiftBox_Paintball_Tex", "28:Consumable_Polaroid_Dracula_Tex(2048)", "28:Weapon_BonkyHammer_Skin_BonkyFace_Tex",
            "28:Standee_CactusTumbleweed_Tex", "213:SpawnableToolTex_BOWLING_PIN", "213:MenuIcon_Sprites_2_Gloves"
        };

        /// <summary>Logs, for the assets the watch uses, which container loads each one and how big its bundle is.</summary>
        public static void LogAssetLoaders()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                RrLog.Warn("Watch", "diagnostic: no RrAssets");
                return;
            }

            foreach (var p in Probe)
            {
                int colon = p.IndexOf(':');
                int cls = int.Parse(p.Substring(0, colon));
                string name = p.Substring(colon + 1);
                RrEntry e;
                if (!assets.TryFind(name, cls, out e))
                {
                    RrLog.Info("Watch", "diagnostic: " + name + " NOT FOUND");
                    continue;
                }

                var cat = assets.Catalog;
                string loader = e.LoaderBundle < 0 ? "none" : cat.Bundles[e.LoaderBundle].File + " key " + e.LoaderKey + " (" + (cat.Bundles[e.LoaderBundle].Size / 1024) + " KB)";
                RrLog.Info("Watch", "diagnostic: " + name + " in " + cat.Bundles[e.Bundle].File.Substring(0, 8) + ", loader " + loader);
            }
        }

        /// <summary>Where the test poses put the menu: 0.8 m in front of the harness camera at (0, 1.6, -3), facing it.</summary>
        public static readonly Vector3 MenuTestPosition = new Vector3(0f, 1.6f, -2.2f);

        /// <summary>Where <see cref="ShowWatch"/> puts the wrist watch, face toward -Z (a camera at z -3).</summary>
        public static readonly Vector3 WatchTestPosition = new Vector3(0.6f, 1.5f, -2.75f);

        private static IEnumerator WaitBuilt()
        {
            float until = Time.realtimeSinceStartup + 90f;
            while (!RrWatch.Ready && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (!RrWatch.Ready)
            {
                RrLog.Warn("Watch", "test: the menu was not built within 90 s");
            }
        }

        private static IEnumerator Settle(float seconds = 0.8f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static IEnumerator OpenWith(bool vr)
        {
            yield return WaitBuilt();
            if (RrWatch.IsOpen && RrWatch.ForceVrLayout != vr)
            {
                RrWatch.Close();
                yield return Settle(0.6f);
            }

            RrWatch.ForceVrLayout = vr;
            RrWatchUi.TestPose = new Pose(MenuTestPosition, Quaternion.identity);
            RrWatch.Show(RrWatchPageId.Home);
            yield return Settle();
            var ui = RrWatch.Ui;
            RrLog.Info("Watch", "test: menu open=" + RrWatch.IsOpen + ", layout " + (ui != null && ui.VrLayout ? "VR" : "desktop") + ", page " + (ui != null ? ui.Current.ToString() : "-") + ", input blocked by '" + RrInput.BlockedBy + "'");
        }

        /// <summary>Opens the menu with the screen (desktop) layout at the test pose, on Home.</summary>
        public static IEnumerator OpenDesktop()
        {
            return OpenWith(false);
        }

        /// <summary>Opens the menu with the headset layout (150-tall bar, detach button, dimmed world) at the test pose.</summary>
        public static IEnumerator OpenVr()
        {
            return OpenWith(true);
        }

        private static IEnumerator Page(RrWatchPageId id)
        {
            yield return WaitBuilt();
            if (RrWatch.Ui != null)
            {
                RrWatch.Ui.ShowPage(id);
            }

            yield return Settle();
            RrLog.Info("Watch", "test: page " + (RrWatch.Ui != null ? RrWatch.Ui.Current.ToString() : "-"));
        }

        public static IEnumerator ShowPlay()
        {
            return Page(RrWatchPageId.Play);
        }

        public static IEnumerator ShowPeople()
        {
            return Page(RrWatchPageId.People);
        }

        public static IEnumerator ShowThisRoom()
        {
            return Page(RrWatchPageId.ThisRoom);
        }

        public static IEnumerator ShowSettings()
        {
            return Page(RrWatchPageId.Settings);
        }

        /// <summary>The room details page of Paintball (as if its card was clicked).</summary>
        public static IEnumerator ShowRoomPaintball()
        {
            yield return WaitBuilt();
            if (RrWatch.Ui != null)
            {
                RrWatch.Ui.ShowRoom(Rooms.RrActivity.Paintball);
            }

            yield return Settle();
        }

        /// <summary>Back (as Backspace).</summary>
        public static IEnumerator Back()
        {
            if (RrWatch.Ui != null)
            {
                RrWatch.Ui.Back();
            }

            yield return Settle();
            RrLog.Info("Watch", "test: back to " + (RrWatch.Ui != null ? RrWatch.Ui.Current.ToString() : "-"));
        }

        /// <summary>Closes the menu and clears the test switches.</summary>
        public static IEnumerator CloseMenu()
        {
            RrWatch.Close();
            yield return Settle(0.6f);
            RrWatch.ForceVrLayout = false;
            RrWatchUi.TestPose = null;
            RrLog.Info("Watch", "test: menu open=" + RrWatch.IsOpen + ", input blocked by '" + RrInput.BlockedBy + "'");
        }

        /// <summary>Shows the wrist watch at <see cref="WatchTestPosition"/> with the dwell ring 60% full.</summary>
        public static IEnumerator ShowWatch()
        {
            RrWatchModel.TestPose = new Pose(WatchTestPosition, Quaternion.LookRotation(Vector3.back, Vector3.up));
            yield return Settle(1.5f);
        }

        public static void HideWatch()
        {
            RrWatchModel.TestPose = null;
        }

        /// <summary>Loads the heavier watch assets one by one and logs the time and the bundles each one opened.</summary>
        public static System.Collections.IEnumerator LogAssetLoadTimes()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                yield break;
            }

            string[] names = { "83:sfx_rrui_watch_ui_open_paper_01", "43:Wrist_Watch", "21:Watch_Wrist_mat", "213:icon_Home", "213:Illustration_Menu_RecCenter", "28:GiftBox_Paintball_Tex", "28:Consumable_Polaroid_Dracula_Tex(2048)" };
            foreach (var p in names)
            {
                int colon = p.IndexOf(':');
                int cls = int.Parse(p.Substring(0, colon));
                string name = p.Substring(colon + 1);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                UnityEngine.Object got = null;
                yield return assets.LoadRoutine<UnityEngine.Object>(name, cls, o => got = o);
                RrLog.Info("Watch", "diagnostic load " + name + ": " + (got != null ? got.GetType().Name : "null") + " in " + sw.ElapsedMilliseconds + " ms; " + assets.Describe());
            }
        }
    }
}
