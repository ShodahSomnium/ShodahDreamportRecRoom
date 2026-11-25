using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// The world's log lines, all prefixed "[SomniumRecRoom] Area: ..." so a Player.log from the client can be
    /// read for this world alone. Warnings of the same kind are reported once (with a count on the heartbeat),
    /// because a missing asset asked for every frame would otherwise bury everything else.
    /// </summary>
    public static class RrLog
    {
        public const string Prefix = "[SomniumRecRoom] ";

        private static readonly Dictionary<string, int> Once = new Dictionary<string, int>();

        public static void Info(string area, string text)
        {
            Debug.Log(Prefix + area + ": " + text);
        }

        public static void Warn(string area, string text)
        {
            Debug.LogWarning(Prefix + area + ": " + text);
        }

        public static void Error(string area, string text)
        {
            Debug.LogError(Prefix + area + ": " + text);
        }

        public static void Error(string area, string text, Exception e)
        {
            Debug.LogError(Prefix + area + ": " + text + " - " + (e == null ? "" : e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace));
        }

        /// <summary>Logs a warning the first time a key is seen; later calls only count.</summary>
        public static void WarnOnce(string area, string key, string text)
        {
            int n;
            Once.TryGetValue(area + "|" + key, out n);
            Once[area + "|" + key] = n + 1;
            if (n == 0)
            {
                Warn(area, text);
            }
        }

        public static int RepeatCount
        {
            get
            {
                int total = 0;
                foreach (var kv in Once)
                {
                    total += kv.Value - 1;
                }

                return total;
            }
        }

        /// <summary>Statics survive a world re-entry: cleared by RrMain.OnDestroy.</summary>
        public static void Reset()
        {
            Once.Clear();
        }
    }
}
