using System;
using System.Collections.Generic;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Bridge.Player.Components;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// The other players in the world as the Bridge shows them (somnium_player.md §8): identity and the tracked body
    /// transforms (read only - never modified), refreshed once a second and on join. Games hang their own objects (hit
    /// volumes, name tags, held props) off these transforms; the avatar is never changed.
    ///
    /// - <see cref="Remote.Id"/> = <c>Properties.PhotonId</c>, the Fusion <c>PlayerRef.PlayerId</c> our networking keys
    ///   use (-1 while the player's network object is not up yet); <see cref="Remote.Nick"/> = <c>NickName</c>.
    /// - Body: <c>Root</c> (the remote body's CharacterController object), <c>Head</c> (HeadAnchor: networked for
    ///   headset players, a fixed point above the root for desktop players), <c>LeftHand</c>/<c>RightHand</c> (the
    ///   networked controller anchors - <b>null for desktop players</b>). Test with <c>!= null</c>, never <c>??</c>.
    /// - Joins arrive through <c>PlayersContainer.OnPlayerAdded</c> (the list is refreshed on the next LateUpdate, when the
    ///   body usually exists; the 1 Hz pass catches it otherwise). Leaves are found by the 1 Hz pass: only proven Bridge
    ///   members are used and <c>OnPlayerRemoved</c> is not among them. Between passes a leaver's transforms turn null
    ///   (destroyed) - consumers already skip a null <see cref="Remote.Root"/>.
    /// - <see cref="Remote"/> objects are kept per player (same instance across refreshes), so a game may keep them.
    /// - Everyone in the world is listed, wherever they are. Every room is built at the world origin, so a player in
    ///   another room stands inside yours: <see cref="RrIsolation"/> (ticked from here every frame, shut down from
    ///   <see cref="Reset"/>) hides them locally and mutes them, and <see cref="Remote.WithMe"/> tells a game whether a
    ///   player is in the local player's place. Anything a game draws or tests for another player (hit volumes, tags,
    ///   held props, zone checks, enemy targets) should skip players who are not <see cref="Remote.WithMe"/>.
    /// Offline and in the editor the list is empty.
    /// </summary>
    public static class RrRemotePlayers
    {
        public sealed class Remote
        {
            public int Id;
            public string Nick;
            /// <summary>Somnium user id (<c>Properties.Id</c>), stable across sessions.</summary>
            public string UserId;
            public Transform Root;
            public Transform Head;
            public Transform LeftHand;
            public Transform RightHand;

            /// <summary>The Bridge player behind it (for Bridge calls a game may need).</summary>
            public ISomniumPlayer Player;

            /// <summary>A headset player (the Bridge gives tracked hands).</summary>
            public bool Vr
            {
                get { return LeftHand != null || RightHand != null; }
            }

            /// <summary>In the local player's place (same activity and instance, not loading): <see cref="RrIsolation.IsWithMe"/>.</summary>
            public bool WithMe
            {
                get { return RrIsolation.IsWithMe(Id); }
            }

            /// <summary>The Bridge nickname as given (null/empty kept): the client names the avatar root after it.</summary>
            internal string RawNick;

            internal bool Seen;
        }

        public const float RefreshInterval = 1f;

        private static readonly List<Remote> List = new List<Remote>();
        private static ISomniumPlayersContainer _subscribed;
        private static bool _dirty;
        private static float _nextRefresh;
        private static int _joinLogs;
        private static bool _loggedError;
        private static int _tickErrors;

        public static IReadOnlyList<Remote> All
        {
            get { return List; }
        }

        public static int Count
        {
            get { return List.Count; }
        }

        /// <summary>Someone joined or left (after the list changed).</summary>
        public static event Action OnChanged;

        public static Remote Get(int playerId)
        {
            if (playerId < 0)
            {
                return null;
            }

            for (int i = 0; i < List.Count; i++)
            {
                if (List[i].Id == playerId)
                {
                    return List[i];
                }
            }

            return null;
        }

        /// <summary>
        /// From the player driver's LateUpdate: the list (on join and once a second), then the isolation. The list step
        /// has its own try, so a failure there (a Bridge read that starts throwing) can never stop the isolation: a
        /// frozen isolation would keep hidden players invisible and silent after they join you, and leave players who
        /// went elsewhere standing at the origin. RrIsolation.Tick catches its own errors.
        /// </summary>
        internal static void Tick()
        {
            try
            {
                TickList();
            }
            catch (Exception e)
            {
                _tickErrors++;
                if (_tickErrors <= 3)
                {
                    RrLog.Error("Remote", "the player list tick threw (" + _tickErrors + (_tickErrors == 3 ? ", later ones not logged" : "") + "); the isolation still runs", e);
                }
            }

            RrIsolation.Tick();
        }

        private static void TickList()
        {
            ISomniumPlayersContainer c = RrPlayer.Container;
            if (!ReferenceEquals(c, _subscribed))
            {
                Unsubscribe();
                if (c != null)
                {
                    try
                    {
                        c.OnPlayerAdded += OnAdded;
                        _subscribed = c;
                    }
                    catch (Exception e)
                    {
                        Warn("subscribing to OnPlayerAdded failed: " + e.Message);
                    }
                }

                _dirty = true;
            }

            float now = Time.unscaledTime;
            if (!_dirty && now < _nextRefresh)
            {
                return;
            }

            _dirty = false;
            _nextRefresh = now + RefreshInterval;
            Refresh(c);
        }

        private static void OnAdded(ISomniumPlayer p)
        {
            _dirty = true;
        }

        private static void Refresh(ISomniumPlayersContainer c)
        {
            for (int i = 0; i < List.Count; i++)
            {
                List[i].Seen = false;
            }

            bool changed = false;
            if (c != null)
            {
                List<ISomniumPlayer> players = null;
                ISomniumPlayer local = null;
                try
                {
                    players = c.AllPlayers;
                    local = c.LocalPlayer;
                }
                catch (Exception e)
                {
                    Warn("reading AllPlayers failed: " + e.Message);
                }

                if (players != null)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        ISomniumPlayer p = players[i];
                        if (p == null || ReferenceEquals(p, local))
                        {
                            continue;
                        }

                        changed |= Update(p);
                    }
                }
            }

            for (int i = List.Count - 1; i >= 0; i--)
            {
                Remote r = List[i];
                if (r.Seen)
                {
                    continue;
                }

                List.RemoveAt(i);
                changed = true;
                Log("left: " + Describe(r));
            }

            if (changed)
            {
                try
                {
                    OnChanged?.Invoke();
                }
                catch (Exception e)
                {
                    RrLog.Error("Remote", "an OnChanged handler threw", e);
                }
            }
        }

        /// <summary>Reads one Bridge player into its entry; true when it is new.</summary>
        private static bool Update(ISomniumPlayer p)
        {
            int id = -1;
            string nick = null;
            string userId = null;
            Transform root = null, head = null, left = null, right = null;
            try
            {
                ISomniumPlayerProperties props = p.Properties;
                if (props != null)
                {
                    if (props.IsLocal)
                    {
                        return false;
                    }

                    id = props.PhotonId;
                    nick = props.NickName;
                    userId = props.Id;
                }

                ISomniumPlayerReferences refs = p.References;
                ISomniumPlayerBody body = refs != null ? refs.Body : null;
                if (body != null)
                {
                    root = body.Root;
                    head = body.Head;
                    left = body.LeftHand;
                    right = body.RightHand;
                }
            }
            catch (Exception e)
            {
                // A player object mid-change: keep its entry as it was this pass; one being torn down drops out of
                // AllPlayers by itself.
                Warn("reading a player failed: " + e.Message);
                for (int i = 0; i < List.Count; i++)
                {
                    if (ReferenceEquals(List[i].Player, p))
                    {
                        List[i].Seen = true;
                    }
                }

                return false;
            }

            Remote r = null;
            for (int i = 0; i < List.Count; i++)
            {
                Remote x = List[i];
                if (ReferenceEquals(x.Player, p) || (id >= 0 && x.Id == id && !x.Seen))
                {
                    r = x;
                    break;
                }
            }

            bool isNew = r == null;
            if (isNew)
            {
                r = new Remote();
                List.Add(r);
            }

            r.Player = p;
            r.Id = id;
            r.Nick = !string.IsNullOrEmpty(nick) ? nick : "Player " + (id >= 0 ? id.ToString() : "?");
            r.RawNick = nick;
            r.UserId = userId;
            r.Root = root != null ? root : null;
            r.Head = head != null ? head : null;
            r.LeftHand = left != null ? left : null;
            r.RightHand = right != null ? right : null;
            r.Seen = true;
            if (isNew)
            {
                Log("joined: " + Describe(r));
            }

            return isNew;
        }

        private static string Describe(Remote r)
        {
            return "'" + r.Nick + "' (id " + r.Id + ", " + (r.Root == null ? "no body yet" : r.Vr ? "VR" : "desktop") + ")";
        }

        private static void Log(string text)
        {
            if (_joinLogs < 40)
            {
                _joinLogs++;
                RrLog.Info("Remote", text + "; " + List.Count + " remote player(s)");
            }
        }

        private static void Warn(string text)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                RrLog.Warn("Remote", text + " (later failures not logged)");
            }
        }

        private static void Unsubscribe()
        {
            if (_subscribed == null)
            {
                return;
            }

            try
            {
                _subscribed.OnPlayerAdded -= OnAdded;
            }
            catch (Exception)
            {
                // The client's container is gone with the world.
            }

            _subscribed = null;
        }

        /// <summary>Statics survive a world re-entry: cleared by RrPlayer.Shutdown. The isolation first puts back what it hid.</summary>
        internal static void Reset()
        {
            try
            {
                RrIsolation.Shutdown();
            }
            catch (Exception e)
            {
                RrLog.Error("Remote", "isolation shutdown threw", e);
            }

            Unsubscribe();
            List.Clear();
            OnChanged = null;
            _dirty = false;
            _nextRefresh = 0f;
            _joinLogs = 0;
            _loggedError = false;
            _tickErrors = 0;
        }
    }
}
