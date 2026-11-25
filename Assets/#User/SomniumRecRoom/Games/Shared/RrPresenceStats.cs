using System;
using System.Reflection;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Writes the local player's health, kills, deaths and life status into their presence slot, so scoreboards and
    /// health bars over other players can read them (RrPresenceView.Health/Kills/Deaths, status Down/Spectating;
    /// presence int 18 in somnium_networking.md §5.4).
    ///
    /// The Net module's API has no setter for these yet (wave 1: requested as RrNet.SetCombat(health, kills,
    /// deaths) and RrNet.SetStatus(RrStatus)). Until it does, this adapter looks the setters up by name once and
    /// calls them when they exist; otherwise the values stay local (<see cref="Health"/> etc.), which is all an
    /// offline game needs. Writes happen on change only.
    /// </summary>
    public static class RrPresenceStats
    {
        public static int Health = -1;
        public static int Kills;
        public static int Deaths;
        public static RrStatus Status = RrStatus.None;

        private static bool _looked;
        private static MethodInfo _setCombat;
        private static MethodInfo _setHealth;
        private static MethodInfo _setKills;
        private static MethodInfo _setDeaths;
        private static MethodInfo _setStatus;
        private static int _sentHealth = int.MinValue;
        private static int _sentKills = int.MinValue;
        private static int _sentDeaths = int.MinValue;
        private static RrStatus _sentStatus = (RrStatus)255;

        /// <summary>True once the Net module exposes a setter this adapter found.</summary>
        public static bool Networked
        {
            get
            {
                Look();
                return _setCombat != null || _setHealth != null || _setStatus != null;
            }
        }

        public static void Publish(int health, int kills, int deaths, RrStatus status)
        {
            Health = health;
            Kills = kills;
            Deaths = deaths;
            Status = status;
            Look();
            try
            {
                if (health != _sentHealth || kills != _sentKills || deaths != _sentDeaths)
                {
                    _sentHealth = health;
                    _sentKills = kills;
                    _sentDeaths = deaths;
                    if (_setCombat != null)
                    {
                        _setCombat.Invoke(null, new object[] { health, kills, deaths });
                    }
                    else
                    {
                        Call(_setHealth, health);
                        Call(_setKills, kills);
                        Call(_setDeaths, deaths);
                    }
                }

                if (status != _sentStatus)
                {
                    _sentStatus = status;
                    if (_setStatus != null)
                    {
                        _setStatus.Invoke(null, new object[] { status });
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Health", "presence", "writing health to presence failed: " + e.Message + " (kept local)");
            }
        }

        private static void Call(MethodInfo m, int v)
        {
            if (m != null)
            {
                m.Invoke(null, new object[] { v });
            }
        }

        private static void Look()
        {
            if (_looked)
            {
                return;
            }

            _looked = true;
            var t = typeof(RrNet);
            const BindingFlags F = BindingFlags.Public | BindingFlags.Static;
            _setCombat = t.GetMethod("SetCombat", F, null, new[] { typeof(int), typeof(int), typeof(int) }, null);
            _setHealth = t.GetMethod("SetHealth", F, null, new[] { typeof(int) }, null);
            _setKills = t.GetMethod("SetKills", F, null, new[] { typeof(int) }, null);
            _setDeaths = t.GetMethod("SetDeaths", F, null, new[] { typeof(int) }, null);
            _setStatus = t.GetMethod("SetStatus", F, null, new[] { typeof(RrStatus) }, null);
            RrLog.Info("Health", _setCombat != null || _setHealth != null
                ? "health/kills/deaths go to the presence slot through RrNet"
                : "RrNet has no health setter yet: health, kills and deaths stay local");
        }

        internal static void ResetStatics()
        {
            Health = -1;
            Kills = 0;
            Deaths = 0;
            Status = RrStatus.None;
            _looked = false;
            _setCombat = null;
            _setHealth = null;
            _setKills = null;
            _setDeaths = null;
            _setStatus = null;
            _sentHealth = int.MinValue;
            _sentKills = int.MinValue;
            _sentDeaths = int.MinValue;
            _sentStatus = (RrStatus)255;
        }
    }
}
