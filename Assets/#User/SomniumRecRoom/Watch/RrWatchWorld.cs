using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>One player as the watch lists them.</summary>
    public sealed class RrWatchPerson
    {
        public int Id;
        public string Nick;
        public RrActivity Activity;
        public int Instance;
        public RrStatus Status;
        public int Team;
        public bool InVr;
        public bool IsLocal;

        /// <summary>In the local player's current instance.</summary>
        public bool WithMe;
    }

    /// <summary>
    /// What the watch shows about the world, read from the other modules: who is in the world and where
    /// (<see cref="RrNet.Presence"/>: activity, instance, status per presence slot), where the local player is
    /// (<see cref="RrTravel"/>), how many are in each activity. Offline (editor, before the network room is
    /// joined) the presence list is empty and the local player is listed alone, where travel says they are.
    ///
    /// Polled when a page is shown and every 1.5 s while it stays open - there are at most 50 slots, so a
    /// refresh is a few microseconds and no event is needed for location changes that do not change the
    /// player list.
    /// </summary>
    public static class RrWatchWorld
    {
        private static readonly List<RrWatchPerson> People = new List<RrWatchPerson>();
        private static readonly Stack<RrWatchPerson> Spare = new Stack<RrWatchPerson>();

        public static RrActivity Here
        {
            get { return RrTravel.Current; }
        }

        public static int HereInstance
        {
            get { return RrTravel.CurrentRoom != null ? RrTravel.CurrentRoom.InstanceNumber : 0; }
        }

        /// <summary>Everyone in the world, the local player first. The list is reused: copy what you keep.</summary>
        public static List<RrWatchPerson> Everyone()
        {
            foreach (var p in People)
            {
                Spare.Push(p);
            }

            People.Clear();
            int local = RrNet.LocalId;
            var present = RrNet.Present;
            bool sawLocal = false;
            if (present != null)
            {
                for (int i = 0; i < present.Count; i++)
                {
                    int id = present[i];
                    var view = RrNet.Presence(id);
                    bool isLocal = id == local;
                    if (!view.Valid && !isLocal)
                    {
                        continue;
                    }

                    var p = Take();
                    p.Id = id;
                    p.IsLocal = isLocal;
                    p.Nick = !string.IsNullOrEmpty(view.Nick) ? view.Nick : isLocal ? RrPlayer.Nick : "Player " + id;
                    p.Activity = isLocal ? Here : view.Activity;
                    p.Instance = isLocal ? HereInstance : view.Instance;
                    p.Status = view.Status;
                    p.Team = view.Team;
                    p.InVr = isLocal ? RrPlayer.InVr : view.InVr;
                    if (isLocal)
                    {
                        sawLocal = true;
                        People.Insert(0, p);
                    }
                    else
                    {
                        People.Add(p);
                    }
                }
            }

            if (!sawLocal)
            {
                var me = Take();
                me.Id = local;
                me.IsLocal = true;
                me.Nick = RrPlayer.Nick;
                me.Activity = Here;
                me.Instance = HereInstance;
                me.Status = RrStatus.None;
                me.Team = 0;
                me.InVr = RrPlayer.InVr;
                People.Insert(0, me);
            }

            var here = Here;
            int hereInstance = HereInstance;
            foreach (var p in People)
            {
                // One answer for the whole world: the isolation decides who is here (the dorm is private, arrivals
                // are together, loading players are apart).
                p.WithMe = p.IsLocal || RrIsolation.IsWithMe(p.Id);
            }

            return People;
        }

        private static RrWatchPerson Take()
        {
            return Spare.Count > 0 ? Spare.Pop() : new RrWatchPerson();
        }

        /// <summary>How many players are in an activity (all instances), the local player included.</summary>
        public static int CountIn(RrActivity activity, List<RrWatchPerson> everyone)
        {
            int n = 0;
            foreach (var p in everyone)
            {
                if (p.Activity == activity)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Whether the watch may offer to join this player where they are.</summary>
        public static bool CanJoin(RrWatchPerson p)
        {
            if (p.IsLocal || p.WithMe || p.Activity == RrActivity.None || p.Activity == RrActivity.Dorm)
            {
                return false;
            }

            var info = RrRooms.Get(p.Activity);
            return info == null || info.Networked;
        }

        /// <summary>"Playing Paintball", "In their Dorm Room", "Loading...", in Rec Room's wording.</summary>
        public static string Where(RrWatchPerson p)
        {
            if (p.Activity == RrActivity.None)
            {
                return p.IsLocal ? "Arriving" : "Arriving in Dreamport";
            }

            if (p.Activity == RrActivity.Dorm)
            {
                return p.IsLocal ? "In your Dorm Room" : "In their Dorm Room";
            }

            string title = RrWatchArt.TitleOf(p.Activity);
            if (p.Status == RrStatus.Loading)
            {
                return "Going to " + title;
            }

            string verb = p.Activity == RrActivity.RecCenter ? "In " : p.Status == RrStatus.Spectating ? "Watching " : "Playing ";
            string instance = p.Instance > 0 ? "  ·  #" + p.Instance : "";
            return verb + title + instance;
        }

        public static void Shutdown()
        {
            People.Clear();
            Spare.Clear();
        }
    }
}
