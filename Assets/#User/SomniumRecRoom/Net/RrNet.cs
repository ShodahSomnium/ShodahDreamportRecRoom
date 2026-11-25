using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>What a player's presence slot says about them.</summary>
    public struct RrPresenceView
    {
        public int PlayerId;
        public bool Valid;
        public RrActivity Activity;
        public int Instance;
        public RrStatus Status;
        public int Team;
        public bool InVr;
        public bool NoGame;
        public int Health;
        public int Kills;
        public int Deaths;
        public string Nick;

        /// <summary>The instance object the player's slot names (-1 none).</summary>
        public int InstanceObject;

        /// <summary>Ammo bucket (0-255) from presence int 18.</summary>
        public int Ammo;
    }

    /// <summary>
    /// The world's view of the Fusion room through the scene's NetworkBridge objects (ARCHITECTURE.md §7,
    /// somnium_networking.md §5): presence slots, a per-instance elected authority ("fake master"), the instance
    /// state, batched events and synced props. The decisions live in the pure <see cref="RrNetCore"/> (tested
    /// offline by SP\nettest against a simulated room); this class is its adapter: it finds the scene objects by
    /// name, reads their ints into the core's mirrors, carries out the core's authority requests and writes (only
    /// changed ints, only with authority), and wires the Bridge RPCs.
    ///
    /// Offline (editor, no runner, or the scene lacks the network objects) everything is local: the local player is
    /// PlayerId 0, present alone, authority of whatever it plays, bound to instance object activity - 1, and its
    /// events loop back.
    /// </summary>
    public static partial class RrNet
    {
        private static RrNetCore _core = new RrNetCore();
        private static RrNetDriver _driver;
        private static readonly List<int> OfflinePresent = new List<int> { 0 };
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static RrActivity _activity;
        private static int _presenceEpoch;
        private static bool _stepSoon, _membersDirty;

        /// <summary>The pure core (the test harness and diagnostics read it; game code uses the API below).</summary>
        public static RrNetCore Core
        {
            get { return _core; }
        }

        /// <summary>The room is joined and the scene's network objects are found (SceneNetworking.IsNetworkReady plus the objects).</summary>
        public static bool Online
        {
            get { return SceneNetworking.IsNetworkReady && _slotsFound; }
        }

        /// <summary>The local player's Fusion PlayerId while the runner runs (as RrPlayer.LocalId), 0 offline.</summary>
        public static int LocalId
        {
            get
            {
                try
                {
                    var runner = SceneNetworking.NetworkRunnerRef;

                    if (runner != null && runner.IsRunning)
                    {
                        int id = runner.LocalPlayer.PlayerId;

                        if (id >= 0)
                        {
                            return id;
                        }
                    }
                }
                catch (Exception)
                {
                    // a runner shutting down: offline
                }

                return 0;
            }
        }

        /// <summary>PlayerIds present in the Fusion room (the local one included), ascending.</summary>
        public static IReadOnlyList<int> Present
        {
            get
            {
                if (Online)
                {
                    return _core.Present;
                }

                OfflinePresent[0] = LocalId;
                return OfflinePresent;
            }
        }

        /// <summary>Someone joined or left the room, or a presence slot's location, status or team changed.</summary>
        public static event Action OnPlayersChanged;

        /// <summary>The local client started (true) or stopped (false) running its instance.</summary>
        public static event Action<bool> OnAuthorityChanged;

        /// <summary>The time base of the instance state's timers (Time.unscaledTimeAsDouble).</summary>
        public static double Now
        {
            get { return Time.unscaledTimeAsDouble; }
        }

        // ------------------------------------------------------------------ presence (writes into the own slot)

        /// <summary>Where the local player is: written into their presence slot. A networked activity also starts binding its instance.</summary>
        public static void SetLocation(RrActivity activity, int instanceNumber, RrStatus status)
        {
            int number = Mathf.Clamp(instanceNumber, 0, 255);

            if ((int)activity != _core.Activity || number != _core.Number)
            {
                _presenceEpoch = (_presenceEpoch + 1) & 0xFF;
                _core.Own[RrPresenceWire.AtSpawn] = RrWire.With(_core.Own[RrPresenceWire.AtSpawn], 24, 8, _presenceEpoch);

                if (activity != _activity)
                {
                    _core.Team = -1;
                }
            }

            _activity = activity;
            _core.Activity = (int)activity;
            _core.Number = number;
            _core.Status = (int)status;
            _core.Networked = IsNetworked(activity);
            Changed();
        }

        public static void SetStatus(RrStatus status)
        {
            _core.Status = (int)status;
            Changed();
        }

        /// <summary>Team 0..14, -1 none.</summary>
        public static void SetTeam(int team)
        {
            _core.Team = team < 0 || team >= RrPresenceWire.NoTeam ? -1 : team;
            Changed();
        }

        /// <summary>Health, kills and deaths (0..255 each) into presence int 18.</summary>
        public static void SetCombat(int health, int kills, int deaths)
        {
            int v = _core.Own[RrPresenceWire.AtCombat];
            v = RrWire.With(v, 0, 8, Mathf.Clamp(health, 0, 255));
            v = RrWire.With(v, 16, 8, Mathf.Clamp(kills, 0, 255));
            v = RrWire.With(v, 24, 8, Mathf.Clamp(deaths, 0, 255));
            _core.Own[RrPresenceWire.AtCombat] = v;
        }

        public static void SetHealth(int health)
        {
            _core.Own[RrPresenceWire.AtCombat] = RrWire.With(_core.Own[RrPresenceWire.AtCombat], 0, 8, Mathf.Clamp(health, 0, 255));
        }

        public static void SetAmmo(int ammo)
        {
            _core.Own[RrPresenceWire.AtCombat] = RrWire.With(_core.Own[RrPresenceWire.AtCombat], 8, 8, Mathf.Clamp(ammo, 0, 255));
        }

        public static void SetKills(int kills)
        {
            _core.Own[RrPresenceWire.AtCombat] = RrWire.With(_core.Own[RrPresenceWire.AtCombat], 16, 8, Mathf.Clamp(kills, 0, 255));
        }

        public static void SetDeaths(int deaths)
        {
            _core.Own[RrPresenceWire.AtCombat] = RrWire.With(_core.Own[RrPresenceWire.AtCombat], 24, 8, Mathf.Clamp(deaths, 0, 255));
        }

        /// <summary>Activity-specific per-player int: index 0 = presence int 19, 1..16 = ints 32..47.</summary>
        public static void SetPlayerInt(int index, int value)
        {
            int at = RrPresenceWire.ActivityIntAt(index);

            if (at >= 0)
            {
                _core.Own[at] = value;
            }
        }

        /// <summary>A player's activity-specific int (see <see cref="SetPlayerInt"/>), 0 when unknown.</summary>
        public static int PlayerInt(int playerId, int index)
        {
            int at = RrPresenceWire.ActivityIntAt(index);
            int[] r = _core.RecordOf(playerId);
            return at >= 0 && r != null ? r[at] : 0;
        }

        /// <summary>Spawn index (0-255) and role/loadout (0-255) in presence int 5.</summary>
        public static void SetSpawn(int spawnIndex, int role)
        {
            int v = _core.Own[RrPresenceWire.AtSpawn];
            v = RrWire.With(v, 8, 8, Mathf.Clamp(spawnIndex, 0, 255));
            v = RrWire.With(v, 16, 8, Mathf.Clamp(role, 0, 255));
            _core.Own[RrPresenceWire.AtSpawn] = v;
        }

        /// <summary>A visitor without Rec Room: listed, never elected, never a member (§5.11).</summary>
        public static void SetNoGame(bool noGame)
        {
            _core.Own[RrPresenceWire.AtOwner] = RrWire.WithBit(_core.Own[RrPresenceWire.AtOwner], RrPresenceWire.FlagNoGame, noGame);
            Changed();
        }

        /// <summary>The watch/menu is open (flag 26) - others may show it.</summary>
        public static void SetMenuOpen(bool open)
        {
            _core.Own[RrPresenceWire.AtOwner] = RrWire.WithBit(_core.Own[RrPresenceWire.AtOwner], RrPresenceWire.FlagMenu, open);
        }

        /// <summary>What a hand holds for others to draw: item id (0-4095) and item state (0-4095); the held prop is set by RrNetBody.</summary>
        public static void SetHand(RrHandSide side, int item, int itemState)
        {
            int at = RrPresenceWire.HandAt(side == RrHandSide.Left);
            int v = _core.Own[at];
            v = RrWire.With(v, 0, 12, item);
            v = RrWire.With(v, 12, 12, itemState);
            _core.Own[at] = v;
        }

        /// <summary>RrNetBody: the local hand holds prop object p (-1 none) at this offset from the hand.</summary>
        internal static void SetHeldProp(bool left, int propObject, Vector3 gripPosition, Quaternion gripRotation)
        {
            int at = RrPresenceWire.HandAt(left);
            _core.Own[at] = RrWire.With(_core.Own[at], 24, 8, propObject + 1);

            if (propObject >= 0)
            {
                RrPresenceWire.PutGrip(_core.Own, left, gripPosition, gripRotation);
            }
            else
            {
                RrPresenceWire.ClearGrip(_core.Own, left);
            }
        }

        public static RrPresenceView Presence(int playerId)
        {
            var view = new RrPresenceView { PlayerId = playerId, Team = -1, InstanceObject = -1 };
            int[] r = Online ? _core.RecordOf(playerId) : playerId == LocalId ? _core.Own : null;

            if (r == null)
            {
                return view;
            }

            bool local = r == _core.Own;
            view.Valid = local || RrPresenceWire.Written(r);

            if (!view.Valid)
            {
                return view;
            }

            view.Activity = (RrActivity)RrPresenceWire.Activity(r);
            view.Instance = RrPresenceWire.Number(r);
            view.Status = (RrStatus)RrPresenceWire.Status(r);
            view.Team = RrPresenceWire.Team(r);
            view.InstanceObject = local ? InstanceObjectIndex : RrPresenceWire.InstanceObject(r);
            view.InVr = RrWire.Bit(r[RrPresenceWire.AtOwner], RrPresenceWire.FlagVr);
            view.NoGame = RrWire.Bit(r[RrPresenceWire.AtOwner], RrPresenceWire.FlagNoGame);
            view.Health = RrPresenceWire.Health(r);
            view.Ammo = RrPresenceWire.Ammo(r);
            view.Kills = RrPresenceWire.Kills(r);
            view.Deaths = RrPresenceWire.Deaths(r);
            view.Nick = NickOf(playerId, local);
            return view;
        }

        /// <summary>The raw presence record of a player (the own record for the local one), null when none. Read only.</summary>
        public static int[] PresenceRecord(int playerId)
        {
            return Online ? _core.RecordOf(playerId) : playerId == LocalId ? _core.Own : null;
        }

        /// <summary>PlayerIds in the local player's current instance (the local one included), ascending.</summary>
        public static List<int> MembersOfMyInstance()
        {
            if (!Online || _core.Key < 0)
            {
                return new List<int> { LocalId };
            }

            return new List<int>(_core.Members);
        }

        /// <summary>This client runs the local player's current instance (true offline and in unnetworked rooms).</summary>
        public static bool IsInstanceAuthority
        {
            get { return !Online || _core.IsAuthority; }
        }

        /// <summary>The PlayerId running the current instance (the local one offline).</summary>
        public static int InstanceAuthorityId
        {
            get { return Online ? _core.Acting : LocalId; }
        }

        /// <summary>Which of the scene's instance objects the current instance is bound to (-1 none): it carries the instance's state, events and leases (rooms all stand at the world origin).</summary>
        public static int InstanceObjectIndex
        {
            get
            {
                if (Online)
                {
                    return _core.Bound;
                }

                return _core.Networked && _core.Activity > 0 && _core.Status != RrPresenceWire.StatusNone
                    ? Mathf.Clamp(_core.Activity - 1, 0, RrInstanceWire.Objects - 1)
                    : -1;
            }
        }

        /// <summary>The generation of the bound instance object (events and leases carry it), 0 offline.</summary>
        public static int InstanceGeneration
        {
            get { return Online && _core.Bound >= 0 ? _core.BoundGeneration : 0; }
        }

        /// <summary>The local player's presence slot index (-1 none): the record that says where this player is.</summary>
        public static int SlotIndex
        {
            get { return Online ? _core.SlotIndex : -1; }
        }

        // ------------------------------------------------------------------ instances

        private static Action<int> _joinDone;
        private static int _joinNumber;
        private static int _joinKey = -1;
        private static double _joinUntil;

        /// <summary>A join waits this long for an instance object before playing solo at the offline place, s.</summary>
        public const double JoinWait = 8.0;

        /// <summary>
        /// Enters an instance of an activity: number -1 picks the fullest joinable instance below the activity's
        /// MaxPlayers (Rec Room fills rooms), else the lowest unused number. Writes the location (status Loading) and
        /// calls done(number) once the instance is bound (<see cref="InstanceObjectIndex"/> then names its state object), at
        /// once offline or for an unnetworked activity, or after <see cref="JoinWait"/> without a free instance object
        /// (InstanceObjectIndex -1: play it solo). The work starts at once; the returned routine only waits for the
        /// end (RrTravel runs it; ignoring it is fine).
        /// </summary>
        public static IEnumerator JoinInstance(RrActivity activity, int number, Action<int> done = null)
        {
            if (number < 0)
            {
                number = ChooseInstanceNumber(activity);
            }

            if (_joinKey >= 0)
            {
                Info("join " + _activity + " #" + _joinNumber + " superseded by " + activity + " #" + number);
            }

            SetLocation(activity, number, RrStatus.Loading);
            _joinDone = done;
            _joinNumber = number;
            _joinKey = RrInstanceWire.Key((int)activity, number);
            _joinUntil = Now + JoinWait;
            int serial = ++_joinSerial;

            if (!Online || !_core.Networked || _driver == null)
            {
                FinishJoin("offline or unnetworked");
            }

            return WaitJoin(serial);
        }

        private static int _joinSerial;

        private static IEnumerator WaitJoin(int serial)
        {
            while (_joinKey >= 0 && _joinSerial == serial)
            {
                yield return null;
            }
        }

        /// <summary>Leaves the current instance (back to the arrival room: activity None).</summary>
        public static void LeaveInstance()
        {
            _joinDone = null;
            _joinKey = -1;
            SetLocation(RrActivity.None, 0, RrStatus.None);
        }

        /// <summary>The instance number a join with number -1 picks (see <see cref="JoinInstance"/>).</summary>
        public static int ChooseInstanceNumber(RrActivity activity)
        {
            if (!Online)
            {
                return 0;
            }

            var info = RrRooms.Get(activity);
            int cap = info != null && info.MaxPlayers > 0 ? info.MaxPlayers : int.MaxValue;
            var counts = new int[256];
            var locked = new bool[256];

            for (int s = 0; s < RrPresenceWire.Slots; s++)
            {
                int[] r = _core.Slot[s];

                if (!_core.SlotValid[s] || !RrPresenceWire.Written(r) || RrPresenceWire.OwnerOf(r) == _core.LocalId ||
                    !RrPresenceWire.Listed(r, _core.Present) || RrPresenceWire.Flag(r, RrPresenceWire.FlagNoGame) ||
                    RrPresenceWire.Activity(r) != (int)activity || RrPresenceWire.Status(r) == RrPresenceWire.StatusNone)
                {
                    continue;
                }

                counts[RrPresenceWire.Number(r)]++;
            }

            for (int k = 0; k < RrInstanceWire.Objects; k++)
            {
                int[] r = _core.Inst[k];
                int key = RrInstanceWire.KeyOf(r);

                if (key >= 0 && RrInstanceWire.KeyActivity(key) == (int)activity && !RrInstanceWire.Joinable(r))
                {
                    locked[RrInstanceWire.KeyNumber(key)] = true;
                }
            }

            int best = -1;

            for (int n = 0; n < RrPresenceWire.SoloNumber; n++)
            {
                if (counts[n] > 0 && counts[n] < cap && !locked[n] && (best < 0 || counts[n] > counts[best]))
                {
                    best = n;
                }
            }

            if (best >= 0)
            {
                return best;
            }

            for (int n = 0; n < RrPresenceWire.SoloNumber; n++)
            {
                if (counts[n] == 0 && !locked[n])
                {
                    return n;
                }
            }

            return RrPresenceWire.SoloNumber;
        }

        private static void CheckJoin()
        {
            if (_joinKey < 0)
            {
                return;
            }

            if (_core.Key != _joinKey)
            {
                FinishJoin("location changed");
                return;
            }

            if (!Online || !_core.Networked)
            {
                FinishJoin("offline");
            }
            else if (_core.Bound >= 0)
            {
                FinishJoin("bound to instance object " + _core.Bound);
            }
            else if (Now >= _joinUntil)
            {
                RrLog.Warn("Net", "no instance object for " + _activity + " #" + _joinNumber + " within " + JoinWait + " s: playing it solo");
                FinishJoin("timed out");
            }
        }

        private static void FinishJoin(string why)
        {
            Action<int> done = _joinDone;
            int number = _joinNumber;
            _joinDone = null;
            _joinKey = -1;
            Info("joined " + _activity + " #" + number + ": " + why);

            if (done == null)
            {
                return;
            }

            try
            {
                done(number);
            }
            catch (Exception e)
            {
                RrLog.Error("Net", "join callback failed", e);
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public static void Init()
        {
            Shutdown();
            _core = new RrNetCore();
            _core.LocalId = LocalId;
            Transform parent = RrMain.Instance != null ? (RrMain.Instance.WorldRoot != null ? RrMain.Instance.WorldRoot : RrMain.Instance.transform) : null;
            var go = new GameObject("Rr Net Driver");

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            _driver = go.AddComponent<RrNetDriver>();
            SceneNetworking.OnOtherPlayerJoined += OnRoomPlayer;
            SceneNetworking.OnOtherPlayerLeft += OnRoomPlayer;
            SceneNetworking.OnLocalPlayerJoined += OnLocalJoined;
            _stepSoon = true;
            RrLog.Info("Net", "started (" + (SceneNetworking.IsNetworkReady ? "room joined" : "waiting for the room; offline until then") + ")");
        }

        public static void Shutdown()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnRoomPlayer;
            SceneNetworking.OnOtherPlayerLeft -= OnRoomPlayer;
            SceneNetworking.OnLocalPlayerJoined -= OnLocalJoined;
            Unhook();

            if (_driver != null)
            {
                RrNetDriver d = _driver;
                _driver = null;

                if (d.gameObject != null)
                {
                    UnityEngine.Object.Destroy(d.gameObject);
                }
            }

            OnPlayersChanged = null;
            OnAuthorityChanged = null;
            ClearEvents();
            RrNetBody.ClearAll();
            RrNetActors.ClearAll();
            _joinDone = null;
            _joinKey = -1;
            _activity = RrActivity.None;
            _wasAuthority = true;
            _wasOnline = false;
            Logged.Clear();
            _core = new RrNetCore();
        }

        internal static void DriverDestroyed(RrNetDriver driver)
        {
            if (_driver == driver)
            {
                _driver = null;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static bool IsNetworked(RrActivity activity)
        {
            if (activity == RrActivity.None || activity == RrActivity.Dorm)
            {
                return false;
            }

            var info = RrRooms.Get(activity);
            return info == null || info.Networked;
        }

        private static void Changed()
        {
            _stepSoon = true;
            _membersDirty = true;

            // The own record says it at once (Presence(LocalId) this frame); the core confirms the object at its step.
            int[] own = _core.Own;
            bool sameInstance = RrPresenceWire.Activity(own) == _core.Activity && RrPresenceWire.Number(own) == _core.Number;
            RrPresenceWire.SetWhere(own, _core.Activity, _core.Number, _core.Status, _core.Team, sameInstance ? RrPresenceWire.InstanceObject(own) : -1);

            if (_driver == null)
            {
                _core.LocalId = LocalId;
                _core.Step(Now);
            }
        }

        private static string NickOf(int playerId, bool local)
        {
            try
            {
                if (local)
                {
                    return RrPlayer.Nick;
                }

                var remote = RrRemotePlayers.Get(playerId);
                return remote != null ? remote.Nick : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One log line per distinct fact.</summary>
        internal static void Info(string text)
        {
            if (text != null && Logged.Add(text))
            {
                RrLog.Info("Net", text);
            }
        }
    }
}
