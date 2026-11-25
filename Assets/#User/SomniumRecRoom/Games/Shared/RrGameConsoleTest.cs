using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Play-mode harness hooks for <see cref="RrGameConsole"/>
    /// (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.RrGameConsoleTest.Arrival"}</c>).
    /// </summary>
    public static class RrGameConsoleTest
    {
        /// <summary>Players the Arrival console shows besides the local one (ids, names, teams), so rows and teams are seen.</summary>
        private static readonly int[] FakeIds = { 77, 78, 79 };

        private static readonly string[] FakeNames = { "Coach", "PaintballPro", "DodgeQueen" };
        private static int _arrivalStarts;
        private static int _arrivalMode;
        private static int _arrivalMap;
        private static bool _teams;
        private static RrGameConsole _showcase;

        /// <summary>
        /// A showcase console at (0, 0, 3) of the current room (the world root when there is none), facing -Z: four
        /// players, two modes, two maps, a START that only counts. Waits until it is open (up to 8 s) and logs what it
        /// shows. (The harness' Arrival floor is covered by the Dorm within seconds of the boot - every room stands at
        /// the world origin - so the showcase runs in a game room with space for the 4.36 m hologram.)
        /// </summary>
        public static IEnumerator Arrival()
        {
            var room = Rooms.RrTravel.CurrentRoom;
            var parent = room != null ? room.transform : RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            _arrivalStarts = 0;
            _arrivalMode = 0;
            _arrivalMap = 0;
            _teams = false;
            var spec = new RrGameConsoleSpec
            {
                Title = "Paintball",
                MinPlayers = 2,
                MaxPlayers = 8,
                TeamCount = 2,
                Members = () =>
                {
                    var list = new List<int> { RrNet.LocalId };
                    list.AddRange(FakeIds);
                    return list;
                },
                TeamOf = id => _teams ? (id == RrNet.LocalId || id == 78 ? 0 : 1) : -1,
                NameOf = id =>
                {
                    int i = System.Array.IndexOf(FakeIds, id);
                    return i >= 0 ? FakeNames[i] : null;
                },
                OnStart = id =>
                {
                    _arrivalStarts++;
                    _teams = true;
                    RrLog.Info("Console", "ConsoleTest: arrival START callback " + _arrivalStarts + " by " + id);
                },
                Modes = new[] { "Capture the Flag", "Team Battle" },
                ModeIndex = () => _arrivalMode,
                OnMode = i => _arrivalMode = i,
                Maps = new[] { "Spillway", "River" },
                MapIndex = () => _arrivalMap,
                OnMap = i => _arrivalMap = i,
                Timer = () => "0:15",
            };
            var console = RrGameConsole.Create(parent, new Pose(new Vector3(0f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f)), spec);
            _showcase = console;
            yield return WaitOpen(console, 8f);
            RrLog.Info("Console", "ConsoleTest: arrival " + (console != null ? console.Describe() : "gone"));
        }

        private static IEnumerator WaitOpen(RrGameConsole console, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (console != null && !console.IsOpen && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            // The open clip keeps growing the panel a little after IsOpen; the measure runs 1.5 s after opening.
            float settle = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < settle)
            {
                yield return null;
            }
        }

        /// <summary>Waits until every console is open (up to 8 s), then logs each one.</summary>
        public static IEnumerator Report()
        {
            float until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until)
            {
                bool all = RrGameConsole.All.Count > 0;
                foreach (var c in RrGameConsole.All)
                {
                    all &= c != null && c.IsOpen;
                }

                if (all)
                {
                    break;
                }

                yield return null;
            }

            yield return new WaitForSeconds(2f);
            RrLog.Info("Console", "ConsoleTest: " + RrGameConsole.All.Count + " console(s)");
            foreach (var c in RrGameConsole.All)
            {
                if (c != null)
                {
                    RrLog.Info("Console", "ConsoleTest: report " + c.Describe() + "; " + c.TestAim(2.5f));
                }
            }
        }

        /// <summary>Presses START on every console (as a player: through the same path as a click) and logs the result.</summary>
        public static IEnumerator PressStart()
        {
            var list = new List<RrGameConsole>(RrGameConsole.All);
            foreach (var c in list)
            {
                if (c == null)
                {
                    continue;
                }

                bool ok = c.Press("harness");
                RrLog.Info("Console", "ConsoleTest: press START -> " + (ok ? "pressed" : "greyed") + "; " + c.Describe());
            }

            yield return new WaitForSeconds(1.2f);
            foreach (var c in list)
            {
                if (c != null)
                {
                    c.Refresh();
                    RrLog.Info("Console", "ConsoleTest: after START " + c.Describe());
                }
            }
        }

        /// <summary>Presses the mode toggle and the map button on every console.</summary>
        public static IEnumerator PressModeAndMap()
        {
            foreach (var c in RrGameConsole.All)
            {
                if (c != null)
                {
                    c.PressMode();
                    c.PressMap();
                    c.Refresh();
                    RrLog.Info("Console", "ConsoleTest: after mode/map " + c.Describe());
                }
            }

            yield return null;
        }

        /// <summary>Closes the showcase console (Rec Room's close animation), leaving the room's own.</summary>
        public static IEnumerator CloseArrival()
        {
            if (_showcase != null)
            {
                _showcase.Close();
                _showcase = null;
            }

            yield return new WaitForSeconds(1.5f);
            RrLog.Info("Console", "ConsoleTest: after close, " + RrGameConsole.All.Count + " console(s)");
        }
    }
}
