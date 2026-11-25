using System;
using System.Collections;
using SomniumSpace.Bridge;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Bridge.Player.Components;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// The private-testing gate. While the Dreamport is shown only to the team, the world loads Rec Room only for the
    /// Somnium accounts listed in <see cref="Whitelist"/>. Everyone else stays on the plain arrival platform with a
    /// "private for now" note. For them the world does not read the Rec Room install, and no room, watch, network
    /// slot or game starts. Privacy works both ways through the isolation (<see cref="RrVisitorDriver"/>): a visitor
    /// knows nobody's presence, so every other player is hidden and silent on the visitor's screen; and a visitor
    /// claims no presence slot, so the testers' isolation hides and silences the visitor.
    ///
    /// The check reads the local player's <c>Properties.NickName</c> (the username on the name tag) and
    /// <c>Properties.Id</c> from the Bridge. Both are proven members that RrPlayer and RrRemotePlayers already use. The
    /// comparison ignores letter case and surrounding spaces. The Bridge fills the local player a moment after the
    /// world starts, so the check waits for it, with a warning in the log after <see cref="NameTimeout"/> seconds.
    /// Nothing loads while it waits, so the gate fails closed. The editor (no client, used by the harness) is always
    /// let in. Player.log gets one line
    /// with the verdict, the username and the id, so an id can be copied into the list as well.
    /// </summary>
    public static class RrAccess
    {
        /// <summary>
        /// Who may play. Add Somnium usernames (as on the name tag; any letter case) or Somnium user ids (Player.log shows
        /// each player's at start: "Access: ... (id ...)"). Leave the list empty to open the world to everyone.
        /// </summary>
        public static readonly string[] Whitelist =
        {
            "Shodah",
            "denevraut",
            "Wildy",
            "Ninquiet",
        };

        /// <summary>After this many seconds without a username from the client, the log says the check is still waiting.</summary>
        public const float NameTimeout = 30f;

        /// <summary>What a visitor who is not on the list sees on the status label.</summary>
        public const string PrivateMessage = "This world is private for now.";

        public enum Verdict
        {
            Pending,
            Allowed,
            Denied,
        }

        public static Verdict State { get; private set; }

        /// <summary>The local player's username and Somnium id as the client reported them (null before, or in the editor).</summary>
        public static string Nick { get; private set; }

        public static string UserId { get; private set; }

        public static bool Allowed
        {
            get { return State == Verdict.Allowed; }
        }

        /// <summary>Decides <see cref="State"/>. Waits for the username and runs before anything from Rec Room is read.</summary>
        public static IEnumerator Check()
        {
            State = Verdict.Pending;
            if (Whitelist.Length == 0)
            {
                State = Verdict.Allowed;
                RrLog.Info("Access", "the whitelist is empty: the world is open to everyone");
                yield break;
            }

            if (Application.isEditor)
            {
                State = Verdict.Allowed;
                RrLog.Info("Access", "editor: the whitelist (" + Whitelist.Length + " name(s)) is not applied");
                yield break;
            }

            // Nothing loads until the username is known, however long the client takes: a slow client only delays the
            // start, and nobody gets in without a name.
            float warnAt = Time.realtimeSinceStartup + NameTimeout;
            bool warned = false;
            while (!ReadLocal())
            {
                if (!warned && Time.realtimeSinceStartup >= warnAt)
                {
                    warned = true;
                    RrLog.Warn("Access", "the client has not reported the local username after " + NameTimeout.ToString("0") + " s: still waiting, nothing is loaded meanwhile");
                }

                yield return null;
            }

            string who = Nick + " (id " + (string.IsNullOrEmpty(UserId) ? "unknown" : UserId) + ")";
            State = Matches(Nick, UserId) ? Verdict.Allowed : Verdict.Denied;
            RrLog.Info("Access", State == Verdict.Allowed
                ? who + " is on the whitelist: loading Rec Room"
                : who + " is not on the whitelist: Rec Room is not loaded, other players are hidden");
        }

        /// <summary>True when the username or the id is on <see cref="Whitelist"/> (letter case and spaces ignored).</summary>
        public static bool Matches(string nick, string id)
        {
            foreach (string entry in Whitelist)
            {
                string e = entry != null ? entry.Trim() : "";
                if (e.Length == 0)
                {
                    continue;
                }

                if ((!string.IsNullOrEmpty(nick) && string.Equals(e, nick.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(id) && string.Equals(e, id.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Reset()
        {
            State = Verdict.Pending;
            Nick = null;
            UserId = null;
        }

        private static bool ReadLocal()
        {
            try
            {
                ISomniumPlayersContainer c = SomniumBridge.PlayersContainer;
                ISomniumPlayer local = c != null ? c.LocalPlayer : null;
                ISomniumPlayerProperties p = local != null ? local.Properties : null;
                if (p == null)
                {
                    return false;
                }

                Nick = p.NickName;
                UserId = p.Id;
                return !string.IsNullOrEmpty(Nick);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
