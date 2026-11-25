using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// Play-mode harness hooks for 3D Charades (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.Games.Charades.RrCharadesTest.StartCoop"}</c>).
    /// They drive the real code paths: the start button, the authority's turn flow, the ink (a drawing made through
    /// the performer API, then a copy decoded from the wire format as another client would receive it), and the
    /// CORRECT button. Every hook logs "CharadesTest: ..." lines with the room's state.
    /// </summary>
    public static class RrCharadesTest
    {
        private static RrCharadesRoom Room()
        {
            var room = RrCharadesRoom.Current;
            if (room == null)
            {
                room = Object.FindFirstObjectByType<RrCharadesRoom>();
            }

            if (room == null)
            {
                RrLog.Warn("Charades", "CharadesTest: no Charades room");
            }

            return room;
        }

        /// <summary>Presses CO-OP CLASSIC on the podium, skips the countdown and the intermission.</summary>
        public static IEnumerator StartCoop()
        {
            return Start(RrCharadesRoom.ReqStartCoop, "Start Coop");
        }

        public static IEnumerator StartLightning()
        {
            return Start(RrCharadesRoom.ReqStartLightning, "Start Lightning");
        }

        private static IEnumerator Start(byte kind, string button)
        {
            var room = Room();
            if (room == null)
            {
                yield break;
            }

            if (!Press(room, button))
            {
                room.Request(kind);
            }

            float until = Time.realtimeSinceStartup + 3f;
            while (room.Phase == RrPhase.Lobby && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            RrLog.Info("Charades", "CharadesTest: after start: " + room.Describe());
            until = Time.realtimeSinceStartup + 8f;
            while (!room.DrawingNow && Time.realtimeSinceStartup < until)
            {
                room.TestSkipWaits();
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            RrLog.Info("Charades", "CharadesTest: drawing: " + room.Describe());
        }

        /// <summary>Draws a house and a sun through the performer API, then a decoded copy of the house (as a remote client).</summary>
        public static IEnumerator Draw()
        {
            var room = Room();
            if (room == null || room.Ink == null)
            {
                yield break;
            }

            var ink = room.Ink;
            if (!ink.LocalPerformer)
            {
                RrLog.Warn("Charades", "CharadesTest: the local player is not the performer: " + room.Describe());
                yield break;
            }

            var c = ink.transform;
            var fwd = Quaternion.Euler(0f, RrCharadesRoom.PerformerYaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, RrCharadesRoom.PerformerYaw, 0f) * Vector3.right;
            Vector3 centre = c.position + fwd * 1.1f + Vector3.up * 1.2f;

            // house: square walls and a roof (from the audience's view), orange
            var house = new List<Vector3>
            {
                centre + right * -0.35f + Vector3.up * -0.35f, centre + right * 0.35f + Vector3.up * -0.35f,
                centre + right * 0.35f + Vector3.up * 0.25f, centre + Vector3.up * 0.65f,
                centre + right * -0.35f + Vector3.up * 0.25f, centre + right * -0.35f + Vector3.up * -0.35f
            };
            yield return Stroke(ink, 3, house, 0.015f);

            // sun: a yellow circle up and to one side, with a thin-to-thick radius
            var sun = new List<Vector3>();
            Vector3 sc = centre + right * 0.9f + Vector3.up * 0.55f + fwd * -0.2f;
            for (int i = 0; i <= 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                sun.Add(sc + right * Mathf.Cos(a) * 0.22f + Vector3.up * Mathf.Sin(a) * 0.22f);
            }

            yield return Stroke(ink, 4, sun, 0.02f);

            // a 3D spiral going toward the audience (blue) so the depth shows
            var spiral = new List<Vector3>();
            Vector3 sp = centre + right * -0.95f;
            for (int i = 0; i <= 60; i++)
            {
                float a = i * 0.35f;
                spiral.Add(sp + right * Mathf.Cos(a) * 0.18f + Vector3.up * (Mathf.Sin(a) * 0.18f + i * 0.004f) + fwd * (i * 0.012f));
            }

            yield return Stroke(ink, 7, spiral, 0.01f);
            yield return new WaitForSeconds(0.3f);
            RrLog.Info("Charades", "CharadesTest: drew 3 strokes: " + room.Describe());

            // the wire round trip: encode the house as the performer sends it, decode it as a receiver would (green, 0.5 m higher)
            var local = new List<Vector3>();
            var radii = new List<float>();
            foreach (var p in house)
            {
                local.Add(c.InverseTransformPoint(p + Vector3.up * 1.4f));
                radii.Add(0.012f);
            }

            var bytes = RrCharadesInk.EncodePoints(ink.Key, 900, 5, 0, local, radii, local.Count);
            ink.ApplyPoints(bytes);
            RrLog.Info("Charades", "CharadesTest: decoded a " + bytes.Length + "-byte ink event into stroke 900: " + ink.StrokeCount + " strokes, " + ink.PointCount + " points");
        }

        private static IEnumerator Stroke(RrCharadesInk ink, byte colour, List<Vector3> corners, float radius)
        {
            ink.BeginStroke(colour, corners[0], radius);
            for (int i = 1; i < corners.Count; i++)
            {
                var a = corners[i - 1];
                var b = corners[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.02f));
                for (int s = 1; s <= steps; s++)
                {
                    ink.AddPoint(Vector3.Lerp(a, b, s / (float)steps), radius);
                }

                yield return null;
            }

            ink.EndStroke();
        }

        /// <summary>Presses CORRECT on the podium (as the performer) and reports the score and the next turn.</summary>
        public static IEnumerator Correct()
        {
            var room = Room();
            if (room == null)
            {
                yield break;
            }

            int before = RrNet.Instance.TeamScore(0);
            string word = room.Word;
            if (!Press(room, "Correct"))
            {
                room.Request(RrCharadesRoom.ReqCorrect);
            }

            yield return new WaitForSeconds(0.6f);
            RrLog.Info("Charades", "CharadesTest: correct \"" + word + "\": score " + before + " -> " + RrNet.Instance.TeamScore(0) + "; " + room.Describe());
        }

        /// <summary>Presses SKIP (a new word for the same performer).</summary>
        public static IEnumerator Skip()
        {
            var room = Room();
            if (room == null)
            {
                yield break;
            }

            float until = Time.realtimeSinceStartup + 6f;
            while (!room.DrawingNow && Time.realtimeSinceStartup < until)
            {
                room.TestSkipWaits();
                yield return null;
            }

            string word = room.Word;
            if (!Press(room, "Skip"))
            {
                room.Request(RrCharadesRoom.ReqSkip);
            }

            yield return new WaitForSeconds(0.4f);
            RrLog.Info("Charades", "CharadesTest: skip \"" + word + "\" -> \"" + room.Word + "\"; " + room.Describe());
        }

        public static void Report()
        {
            var room = Room();
            if (room != null)
            {
                RrLog.Info("Charades", "CharadesTest: " + room.Describe());
            }
        }

        private static bool Press(RrCharadesRoom room, string name)
        {
            foreach (var p in room.GetComponentsInChildren<RrPressable>(false))
            {
                if (p.name == name)
                {
                    return p.Press("test");
                }
            }

            return false;
        }
    }
}
