using System.Collections;
using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// Play-mode harness hooks for the Rec Center ({"call": "SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter.RrRecCenterTest.X"}).
    /// Each acts as the local player would (press, throw) and logs one "RecCenter test:" line with the result.
    /// </summary>
    public static class RrRecCenterTest
    {
        private static RrRecCenterRoom Room
        {
            get
            {
                var r = RrRecCenterRoom.Current;
                if (r == null)
                {
                    RrLog.Warn("RecCenter", "test: no Rec Center built");
                }

                return r;
            }
        }

        /// <summary>One line with the hub's counts and states.</summary>
        public static void Report()
        {
            var r = Room;
            if (r == null)
            {
                return;
            }

            var radios = "";
            for (int i = 0; i < r.Radios.Count; i++)
            {
                radios += (i > 0 ? ", " : "") + r.Radios[i].DisplayName + "=" + r.Radios[i].PlayingTitle + " (changes " + r.Radios[i].Changes + ")";
            }

            var doors = "";
            for (int i = 0; i < r.Doors.Count; i++)
            {
                var d = r.Doors[i];
                doors += (i > 0 ? ", " : "") + (d != null ? d.name + " @" + r.transform.InverseTransformPoint(d.transform.position).ToString("F1") : "(gone)");
            }

            var hoops = "";
            for (int i = 0; i < r.Hoops.Count; i++)
            {
                hoops += (i > 0 ? ", " : "") + (r.Hoops[i] != null ? r.Hoops[i].Scores.ToString() : "-");
            }

            RrLog.Info("RecCenter", "test: report: doors " + r.DoorCount + " [" + doors + "], prefabs " + r.Spawned + " (" + r.Failed + " failed), tools "
                + r.ToolCount + ", shared props " + r.NetPropCount + ", bell rings " + r.BellRings + ", bottles " + r.BottlesDispensed
                + ", radios [" + radios + "], hoop scores [" + hoops + "], mirror " + (r.Mirror != null ? "ready" : r.TheatreMirror != null ? "theatre (" + (r.TheatreMirror.Available ? "planar" : "stylised") + ")" : "missing")
                + ", building " + (r.RealBuilding ? r.BuildingName + " (" + r.SkippedDuplicates + " furniture copies left out, " + r.HiddenOfTheirs + " of theirs hidden)" : r.BuildingName));
        }

        public static void RingBell()
        {
            var r = Room;
            if (r != null)
            {
                r.RingBell();
                RrLog.Info("RecCenter", "test: bell rings " + r.BellRings);
            }
        }

        /// <summary>Presses every radio once and the kiosk radio twice (next track, then the next).</summary>
        public static void PressRadios()
        {
            var r = Room;
            if (r == null)
            {
                return;
            }

            for (int i = 0; i < r.Radios.Count; i++)
            {
                var radio = r.Radios[i];
                radio.Press();
                if (radio.Slot == RrRecCenterRoom.IntRadioKiosk)
                {
                    radio.Press();
                }
            }

            RrLog.Info("RecCenter", "test: radios pressed");
        }

        /// <summary>Drops a basketball through each hoop in turn and waits up to 3 s for each score.</summary>
        public static IEnumerator ShootHoop()
        {
            var r = Room;
            if (r == null || r.Hoops.Count == 0)
            {
                RrLog.Warn("RecCenter", "test: no hoop");
                yield break;
            }

            RrBall ball = null;
            for (int i = 0; i < r.NetProps.Count && ball == null; i++)
            {
                if (r.NetProps[i] != null)
                {
                    ball = r.NetProps[i].GetComponent<RrBall>();
                }
            }

            if (ball == null)
            {
                RrLog.Warn("RecCenter", "test: no basketball");
                yield break;
            }

            var body = ball.GetComponent<Rigidbody>();
            for (int h = 0; h < r.Hoops.Count; h++)
            {
                var hoop = r.Hoops[h];
                int before = hoop.Scores;
                body.isKinematic = false;
                body.position = hoop.RimCenter + Vector3.up * 0.5f;
                ball.transform.position = body.position;
                body.linearVelocity = Vector3.down * 1.5f;
                body.angularVelocity = Vector3.zero;
                float until = Time.realtimeSinceStartup + 3f;
                while (hoop.Scores == before && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }

                RrLog.Info("RecCenter", "test: hoop " + (h + 1) + " " + (hoop.Scores > before ? "scored" : "missed") + " (scores " + hoop.Scores + ")");
            }
        }

        public static IEnumerator Vend()
        {
            var r = Room;
            if (r == null)
            {
                yield break;
            }

            r.Vend();
            yield return new WaitForSeconds(2.5f);
            RrLog.Info("RecCenter", "test: vending dispensed " + r.BottlesDispensed);
        }

        public static void FriendOTron()
        {
            var r = Room;
            if (r != null)
            {
                r.FriendOTron();
                RrLog.Info("RecCenter", "test: Friend-O-Tron shown");
            }
        }
    }
}
