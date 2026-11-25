using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// What a <see cref="RrGameConsole"/> shows and what its buttons do. Every delegate is read on each refresh
    /// (every 0.5 s, and at once after <see cref="RrGameConsole.Refresh"/>), so a game hands over live getters and
    /// never pushes updates.
    ///
    /// The console never writes game state. START, the mode toggle and the map button call the game's callbacks on
    /// the pressing client only, with the local PlayerId; the game routes them through its own authority path
    /// (<see cref="RrGameRoom.RequestStart"/> or an event to the instance authority) and the next refresh shows the
    /// result. Null delegates take the defaults documented on each field; <see cref="RrGameConsole.ForGame"/> fills
    /// the ones an <see cref="RrGameRoom"/> can answer itself.
    /// </summary>
    public sealed class RrGameConsoleSpec
    {
        /// <summary>The yellow title cell (Rec Room's [GameScoreboardView] TitleText, upper-cased); null: the room's title (ForGame) or "REC ROOM".</summary>
        public string Title;

        /// <summary>Below this many players START is greyed and the status line says "N MORE PLAYERS NEEDED"; 0: the room's minimum (ForGame) or 1.</summary>
        public int MinPlayers;

        /// <summary>0 = no limit (or the room's maximum with ForGame); otherwise the count cell shows "3 / 8".</summary>
        public int MaxPlayers;

        /// <summary>0 = one list (co-op quests, free-for-all); 1-4 = team-coloured rows grouped by team.</summary>
        public int TeamCount = 2;

        /// <summary>The players in this instance; null: <c>RrNet.MembersOfMyInstance()</c> filtered by <c>RrIsolation.IsWithMe</c>.</summary>
        public Func<IReadOnlyList<int>> Members;

        /// <summary>A member's team (-1: none yet, a neutral row); null: <see cref="RrTeams.TeamOf"/>.</summary>
        public Func<int, int> TeamOf;

        /// <summary>A member's display name; null: the presence nick, else "Player id".</summary>
        public Func<int, string> NameOf;

        /// <summary>START pressed by the local player (argument: <c>RrNet.LocalId</c>). Ask the game's authority here.</summary>
        public Action<int> OnStart;

        /// <summary>False greys START (not in the lobby, a quest already running); null: true.</summary>
        public Func<bool> CanStart;

        /// <summary>START stays pressable below <see cref="MinPlayers"/> (Dodgeball's solo practice).</summary>
        public bool StartBelowMin;

        /// <summary>START's label now; null: "START" ("PRACTICE", "IN PROGRESS", "START QUEST"...).</summary>
        public Func<string> StartLabel;

        /// <summary>The status line under the list; null: automatic ("1 MORE PLAYER NEEDED", "2-6 PLAYERS").</summary>
        public Func<string> Status;

        /// <summary>The timer cell ("0:05"); null: the cell stays empty.</summary>
        public Func<string> Timer;

        /// <summary>
        /// Optional game modes: the subtitle cell shows <c>Modes[ModeIndex()]</c> and is Rec Room's ModeToggleButton -
        /// a press calls <see cref="OnMode"/> with the next index (wrapping); <see cref="ModeIndex"/> shows the result.
        /// </summary>
        public string[] Modes;

        public Func<int> ModeIndex;
        public Action<int> OnMode;

        /// <summary>Optional maps: the footer shows "MAP: name" and a CHANGE button, the same pattern as the modes.</summary>
        public string[] Maps;

        public Func<int> MapIndex;
        public Action<int> OnMap;

        /// <summary>The line under the title when there are no modes (the room's subtitle); null: "WAITING FOR PLAYERS".</summary>
        public Func<string> Subtitle;

        /// <summary>Rec Room's own (ScoreboardProjector) from the install carries the panel; false: the panel floats alone.</summary>
        public bool Projector = true;

        /// <summary>How far our aim ray (desktop crosshair, VR right-hand ray) reaches the panel, metres.</summary>
        public float Reach = 8f;
    }
}
