using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>The watch's screens (nav tabs and the room details sub-page).</summary>
    public enum RrWatchPageId : byte
    {
        Home = 0,
        Play = 1,
        People = 2,
        ThisRoom = 3,
        Settings = 4,
        Room = 5,

        /// <summary>Player-made rooms (Places/MakerRooms): pick a base room and create one.</summary>
        Create = 6,

        /// <summary>Player-made rooms: my rooms and public rooms, join, delete.</summary>
        Rooms = 7,

        /// <summary>The maker room the player is in: save, privacy, builders, co-owners, rename.</summary>
        MakerRoom = 8,

        /// <summary>Iteration 15: wear the Somnium or a Rec Room avatar, and dress it (Player/Avatar). 20 keeps clear of new pages.</summary>
        Avatar = 20
    }

    /// <summary>
    /// One screen of the watch, drawn on the cream page (1920 x 1080 canvas units, Rec Room's Router page).
    /// Built once, hidden, when the watch starts; <see cref="Show"/> refreshes what can change (who is where,
    /// player counts, volumes) each time the screen comes up, and <see cref="Refresh"/> while it is showing
    /// when the players in the world change.
    /// </summary>
    public abstract class RrWatchPage
    {
        /// <summary>The page area: width 1920, height 1080, origin top-left.</summary>
        public const float Width = 1920f;
        public const float Height = 1080f;

        /// <summary>Left and right margin of page content.</summary>
        public const float Margin = 60f;

        public RrWatchUi Ui;
        public RectTransform Root;

        public abstract RrWatchPageId Id { get; }

        /// <summary>Builds the page's content under <see cref="Root"/> (a 1920 x 1080 rect, top-left origin).</summary>
        public abstract void Build();

        /// <summary>The page is about to be shown.</summary>
        public virtual void Show()
        {
            Refresh();
        }

        /// <summary>World state changed while the page shows (presence, travel).</summary>
        public virtual void Refresh()
        {
        }

        /// <summary>Called every frame while the page shows (cheap checks only).</summary>
        public virtual void Tick()
        {
        }

        /// <summary>The page title in Jost, top-left, as the RRUI screen headers.</summary>
        protected TMPro.TextMeshProUGUI Title(string text, float x = Margin)
        {
            return RrUiKit.Label(Root, "Title", text, x, 34f, 1100f, 80f, 55f, RrUiKit.Ink, TMPro.TextAlignmentOptions.MidlineLeft, false, true);
        }
    }
}
