using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Moving the local player between places the way Rec Room does it (rr_reccenter.md §4.2): the view fades
    /// to black in 0.15 s, the loading card says where you are going, the old room is left and destroyed, the
    /// instance is bound (somnium_networking.md §5.4: the instance object k carries the instance's state), the
    /// new room is built at the world origin behind the card, the player is teleported to its spawn, presence
    /// says Lobby or Playing, the room's own Rec Room post-processing fades in (<see cref="RrPostFx"/>) and its own
    /// lighting is applied (<see cref="RrEnvironment"/>: key light and shadows, ambient, fog, sky; restored on leaving), the
    /// room's music starts and the card fades out into the room.
    ///
    /// Only one room exists at a time for this client, and every room stands at the world origin
    /// (<see cref="RrPlacement"/>). The old room is destroyed - with what shared systems made for it outside its
    /// root, and the assets only it used - before the new one is built, so two rooms never overlap and their
    /// assets are never alive together; meanwhile the player waits on the arrival room's floor at the origin (the
    /// scene's own "Arrival" slab, shown and solid only while there is no room), because a player left standing
    /// in a room that is being destroyed falls. See RrTravel.Origin.cs for the arrival room, the teardown, the
    /// memory release and the rescue of a player who fell out of a room or was respawned by the client.
    ///
    /// Failures degrade instead of stranding the player: a room that throws or takes longer than 90 s to build
    /// is destroyed and the trip goes to the dorm instead; a dorm that fails is replaced by the placeholder
    /// dorm; if even that fails the player stays in the arrival room. Every call into room code is guarded.
    /// </summary>
    public static partial class RrTravel
    {
        /// <summary>A room's Build gets this long (realtime) before it is abandoned.</summary>
        public const float BuildTimeout = 90f;

        /// <summary>The client's teleport answers within two frames; this is only a guard against a lost callback.</summary>
        public const float TeleportTimeout = 6f;

        /// <summary>Begin waits this long in the arrival room for the network before going home offline.</summary>
        public const float BeginWait = 15f;

        /// <summary>
        /// Below the room's lowest floor by this much, the player is put back (a fall of 10 m takes 1.4 s; the
        /// client's own anti-fall waits for y = -100).
        /// </summary>
        public const float FallDepth = 10f;

        /// <summary>
        /// Shortest time the card stays up (Rec Room: 5 s, minTotalDisplayDuration). A local trip needs far less,
        /// but a card that only flashes looks broken (rr_reccenter.md §4.2: 2.5 s, 1 s between the dorm and the Rec Center).
        /// </summary>
        public const float MinCardSeconds = 2.5f;
        public const float MinCardSocialSeconds = 1f;

        /// <summary>Rec Room's whoosh (0.30 s), played if the bundles can give it (not a container: GUESS it loads).</summary>
        public const string TeleportClip = "GenericTeleport01";

        /// <summary>
        /// The arrival sting for activities: the start of the Rec Room theme (b203e635:9, 60.6 s). Rec Room's own
        /// ActivityTitleJingle is in the Startup bundle but nothing references it, so it cannot be loaded.
        /// </summary>
        public const string ArrivalStingClip = "RecRoomTheme2";
        public const float ArrivalStingSeconds = 3.5f;

        public static RrActivity Current { get; private set; }

        public static RrRoom CurrentRoom { get; private set; }

        /// <summary>True while a trip is under way (fading, building, teleporting).</summary>
        public static bool Busy { get; private set; }

        /// <summary>Instance number of the current room (0 for the dorm and offline).</summary>
        public static int CurrentInstance { get; private set; }

        /// <summary>Where the player came from (rooms use it, e.g. the Rec Center spawns you at that activity's door).</summary>
        public static RrActivity Previous { get; private set; }

        /// <summary>Where the trip under way is going (None when not travelling).</summary>
        public static RrActivity Destination { get; private set; }

        /// <summary>Realtime (Time.realtimeSinceStartup) of the last arrival; doors ignore uses for a moment after it.</summary>
        public static float ArrivedAt { get; private set; } = -100f;

        /// <summary>Play the Rec Room theme's opening when arriving in an activity.</summary>
        public static bool ArrivalSting = true;

        public static event Action<RrActivity> OnArrived;

        /// <summary>A trip starts (the argument is the destination); raised before the fade.</summary>
        public static event Action<RrActivity> OnLeaving;

        private static bool _begun;
        private static bool _waitingToBegin;
        private static bool _subscribed;
        private static bool _hasQueued;
        private static RrActivity _queued;
        private static int _queuedInstance;
        private static bool _pendingMembers;
        private static bool _pendingAuthority;
        private static bool _pendingAuthorityValue;
        private static int _trip;
        private static int _spawnIndex;
        private static int _footingAttempts;
        private static float _footingWaitUntil;
        private static bool _footingGaveUp;
        private static RrActivity _lastBoundActivity;
        private static int _lastBoundInstance = -1;
        private static RrActivity _arrived;

        /// <summary>Go to a place; instanceNumber -1 picks one (joins the fullest joinable instance, like Rec Room).</summary>
        public static void GoTo(RrActivity activity, int instanceNumber = -1)
        {
            if (activity == RrActivity.None)
            {
                RrLog.Warn("Travel", "GoTo(None) ignored");
                return;
            }

            var driver = RrTravelDriver.Ensure();
            if (Busy)
            {
                _hasQueued = true;
                _queued = activity;
                _queuedInstance = instanceNumber;
                RrLog.Info("Travel", "going to " + activity + " after the trip to " + Destination);
                return;
            }

            if (CurrentRoom != null && activity == Current && (instanceNumber < 0 || instanceNumber == CurrentInstance))
            {
                RrLog.Info("Travel", "already in " + activity);
                return;
            }

            _waitingToBegin = false;
            Busy = true;
            Destination = activity;
            driver.StartCoroutine(RunTrip(activity, instanceNumber));
        }

        /// <summary>Back to the player's own dorm.</summary>
        public static void GoHome()
        {
            GoTo(RrActivity.Dorm);
        }

        /// <summary>
        /// Called by RrMain once everything is started. The player stays in the arrival room, with the status on
        /// the scene's label, until the network has joined the room and claimed a presence slot (the record that
        /// tells everyone where this player is) or 15 s have passed (offline), then goes home. In the editor, where no runner will ever
        /// come unless SceneNetworking was told to make one, a second is enough. A GoTo in the meantime wins.
        /// </summary>
        public static void Begin()
        {
            if (_begun)
            {
                return;
            }

            _begun = true;
            RememberSceneRoots();
            Subscribe();
            var driver = RrTravelDriver.Ensure();
            RrLoadingScreen.Ensure();
            _waitingToBegin = true;
            driver.StartCoroutine(BeginRoutine());
        }

        private static IEnumerator BeginRoutine()
        {
            float start = Time.realtimeSinceStartup;
            string why = null;
            RrLog.Info("Travel", "in the arrival room, waiting for the network (up to " + BeginWait + " s)");
            SetStatus("Joining the room...");
            while (_waitingToBegin)
            {
                float waited = Time.realtimeSinceStartup - start;
                if (RrNet.Online && RrNet.SlotIndex >= 0)
                {
                    why = "network ready, presence slot " + RrNet.SlotIndex + " after " + waited.ToString("0.0") + " s";
                    break;
                }

                if (waited >= BeginWait)
                {
                    why = RrNet.Online ? "online but no presence slot after " + BeginWait + " s" : "no network after " + BeginWait + " s: offline";
                    break;
                }

                if (Application.isEditor && SceneNetworking.NetworkRunnerRef == null && waited >= 1f)
                {
                    why = "editor without a network runner: offline";
                    break;
                }

                yield return null;
            }

            SetStatus("");
            if (!_waitingToBegin)
            {
                yield break;
            }

            _waitingToBegin = false;
            if (StayInArrival)
            {
                RrLog.Info("Travel", "staying in the arrival room (StayInArrival; " + why + ")");
                yield break;
            }

            RrLog.Info("Travel", "leaving the arrival room (" + why + ")");
            GoHome();
        }

        public static void Shutdown()
        {
            Unsubscribe();
            OnArrived = null;
            OnLeaving = null;
            Current = RrActivity.None;
            CurrentRoom = null;
            Busy = false;
            CurrentInstance = 0;
            Previous = RrActivity.None;
            Destination = RrActivity.None;
            ArrivalSting = true;
            _begun = false;
            _waitingToBegin = false;
            _hasQueued = false;
            _pendingMembers = false;
            _pendingAuthority = false;
            _trip = 0;
            _spawnIndex = 0;
            _footingAttempts = 0;
            _footingWaitUntil = 0f;
            _footingGaveUp = false;
            _lastBoundActivity = RrActivity.None;
            _lastBoundInstance = -1;
            _arrived = RrActivity.None;
            _join = null;
            _joinLooked = false;
            ArrivedAt = -100f;
            ResetOrigin();
            RrPortal.Shutdown();
            RrCoverArt.Shutdown();

            // The post Volume goes with the world: the client's own post-processing is back exactly as it was.
            RrPostFx.Shutdown();

            // Rec Room's lighting goes with the world: the world scene's sun, ambient, fog and sky are back as saved.
            RrEnvironment.Shutdown();
        }

        // ---- the trip ----

        private static IEnumerator RunTrip(RrActivity activity, int instanceNumber)
        {
            var outcome = new RrTravelDriver.Outcome();
            yield return RrTravelDriver.Guard(Trip(activity, instanceNumber), outcome);
            if (!outcome.Finished)
            {
                RrLog.Error("Travel", "the trip to " + activity + " broke off", outcome.Error);
                yield return Recover();
            }

            Busy = false;
            Destination = RrActivity.None;
            if (outcome.Finished)
            {
                RaiseArrived(_arrived);
                FlushPending();
            }

            if (_hasQueued)
            {
                _hasQueued = false;
                GoTo(_queued, _queuedInstance);
            }
        }

        private static IEnumerator Trip(RrActivity target, int instanceNumber)
        {
            int trip = ++_trip;
            float started = Time.realtimeSinceStartup;
            var info = RrRooms.Get(target);
            RrLog.Info("Travel", "trip " + trip + ": " + (Current == RrActivity.None ? "arrival room" : Current.ToString()) + " -> " + target + (instanceNumber >= 0 ? " #" + instanceNumber : "") + (info == null ? " (not registered)" : ""));
            var screen = RrLoadingScreen.Ensure();
            screen.ProgressSource = null;

            // From here until the next room is created, loads are the card's, the sounds' and the watch's: shared.
            SetAssetScope(0);
            PlayTeleportSound();
            Safe(() => RrPlayer.SetMovement(0f, false), "freezing the player");
            Safe(() => RrNet.SetLocation(target, Math.Max(0, instanceNumber), RrStatus.Loading), "presence (loading)");
            RaiseLeaving(target);
            yield return screen.Cover(info, target);
            screen.SetProgress(0.05f);

            // Leave the old room: the arrival floor comes back under the player, the room's OnLocalExit runs, what
            // it left outside its root is cleared, it is destroyed, and what only it used is freed.
            Previous = Current;
            SetArrival(true, "trip " + trip + " to " + target);
            if (CurrentRoom != null)
            {
                var old = CurrentRoom;
                var oldActivity = Current;
                int oldScope = old.AssetScope;
                Safe(old.OnLocalExit, oldActivity + ".OnLocalExit");
                Safe(RrPostFx.Leave, "post-processing (leaving " + oldActivity + ")");
                Safe(RrEnvironment.Leave, "lighting (leaving " + oldActivity + ")");
                ReleaseHeld(old);
                yield return Teleport(RrPlacement.ArrivalSpot, 0f);
                CurrentRoom = null;
                Current = RrActivity.None;
                ClearShared(oldActivity);
                if (old != null)
                {
                    Object.Destroy(old.gameObject);
                }

                // Destroy takes effect at the end of the frame: the census and the memory release come after it.
                yield return null;
                RrLog.Info("Travel", "left " + oldActivity);
                yield return FreeRoom(oldActivity, oldScope);
                Safe(() => Census("after leaving " + oldActivity + " (visit " + VisitOf(oldActivity) + ")"), "census");
            }

            Safe(() => RrMusic.Stop(0.3f), "music stop");
            screen.SetProgress(0.1f);

            // Build, falling back to the dorm, then to the placeholder dorm, then to the arrival room.
            var result = new BuildResult();
            var plan = new List<KeyValuePair<RrActivity, RrRoomInfo>> { new KeyValuePair<RrActivity, RrRoomInfo>(target, info) };
            if (target != RrActivity.Dorm)
            {
                plan.Add(new KeyValuePair<RrActivity, RrRoomInfo>(RrActivity.Dorm, RrRooms.Get(RrActivity.Dorm)));
            }

            plan.Add(new KeyValuePair<RrActivity, RrRoomInfo>(RrActivity.Dorm, RrPlaceholderRoom.InfoFor(RrActivity.Dorm)));
            for (int i = 0; i < plan.Count && result.Room == null; i++)
            {
                var step = plan[i];
                if (step.Value == null || (i > 0 && step.Value == plan[i - 1].Value))
                {
                    continue;
                }

                if (i > 0)
                {
                    string failed = plan[i - 1].Value != null ? plan[i - 1].Value.Title : plan[i - 1].Key.ToString();
                    screen.SetMessage("Could not load " + failed + " - going to your dorm");
                    screen.SetDestination(step.Value, step.Key);
                    RrLog.Warn("Travel", "falling back to " + (step.Value.Create == null ? "?" : step.Value.Title) + " (" + result.Error + ")");
                }

                result = new BuildResult();
                yield return BuildRoom(step.Value, step.Key, i == 0 ? instanceNumber : -1, result, screen);
            }

            var room = result.Room;
            screen.ProgressSource = null;
            screen.SetProgress(0.92f);

            // Arrive: teleport to the spawn, then everything that says "I am here".
            if (room != null)
            {
                if (float.IsNaN(room.LowestFloor))
                {
                    try
                    {
                        room.LowestFloor = MeasureLowestFloor(room);
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Travel", "measuring " + room.Activity + "'s lowest floor failed; using its origin", e);
                        room.LowestFloor = room.Origin.y;
                    }
                }

                _spawnIndex = SpawnIndex();
                Pose pose;
                try
                {
                    pose = room.WorldSpawn(_spawnIndex);
                }
                catch (Exception e)
                {
                    RrLog.Error("Travel", room.Activity + ".Spawn(" + _spawnIndex + ") failed; using the origin", e);
                    pose = new Pose(room.Origin + Vector3.up * 0.05f, Quaternion.identity);
                }

                yield return Teleport(pose.position, pose.rotation.eulerAngles.y);

                // The player stands in the room now: the arrival floor would cut through it.
                SetArrival(false, "in " + room.Activity);
            }
            else
            {
                RrLog.Error("Travel", "nothing could be built: staying in the arrival room");
                SetAssetScope(0);
                yield return Teleport(RrPlacement.ArrivalSpot, 0f);
            }

            Safe(() => RrPlayer.SetMovement(1f, true), "restoring movement");
            CurrentRoom = room;
            Current = room != null ? room.Activity : RrActivity.None;
            CurrentInstance = room != null ? room.InstanceNumber : 0;
            ResetFooting();
            string roomMusic = null;
            if (room != null)
            {
                var status = RrStatus.Lobby;
                Safe(() => status = room.ArrivalStatus, room.Activity + ".ArrivalStatus");
                Safe(() => RrNet.SetLocation(room.Activity, room.InstanceNumber, status), "presence (arrived)");

                // Rec Room's post-processing for the room, still under the card; before OnLocalEnter so a room can pick an area.
                Safe(() => RrPostFx.Enter(room.Activity), "post-processing (" + room.Activity + ")");
                Safe(() => RrEnvironment.Enter(room.Activity), "lighting (" + room.Activity + ")");
                Safe(room.OnLocalEnter, room.Activity + ".OnLocalEnter");
                roomMusic = RrMusic.Current;
                if (roomMusic == null && room.Info != null && !string.IsNullOrEmpty(room.Info.Music))
                {
                    roomMusic = room.Info.Music;
                    Safe(() => RrMusic.Play(roomMusic), "room music");
                }
            }
            else
            {
                Safe(() => RrNet.SetLocation(RrActivity.None, 0, RrStatus.None), "presence (arrival room)");
                Safe(RrPostFx.Leave, "post-processing (arrival room)");
                Safe(RrEnvironment.Leave, "lighting (arrival room)");
            }

            _arrived = Current;
            Safe(() => Census(room != null ? "at " + room.Activity + " (visit " + NextVisit(room.Activity) + ")" : "in the arrival room"), "census");
            float minCard = room != null && IsSocial(room.Info) ? MinCardSocialSeconds : MinCardSeconds;
            while (Time.realtimeSinceStartup - started < minCard)
            {
                yield return null;
            }

            yield return screen.Uncover();
            RrLog.Info("Travel", "arrived in " + (room != null ? room.Activity + (room.Info != null && room.Info.Networked ? " #" + room.InstanceNumber : "") + " at " + Fmt(room.Origin) + ", spawn " + _spawnIndex : "the arrival room") + " after " + (Time.realtimeSinceStartup - started).ToString("0.00") + " s");
            if (room != null && ArrivalSting && !IsSocial(room.Info) && RrTravelDriver.Instance != null)
            {
                RrTravelDriver.Instance.StartCoroutine(Sting(room, roomMusic, trip));
            }
        }

        private sealed class BuildResult
        {
            public RrRoom Room;
            public string Error;
        }

        /// <summary>Binds the instance, makes the room at its origin and runs its Build under a deadline.</summary>
        private static IEnumerator BuildRoom(RrRoomInfo info, RrActivity activity, int requested, BuildResult result, RrLoadingScreen screen)
        {
            if (info == null || info.Create == null)
            {
                result.Error = activity + " is not registered";
                yield break;
            }

            int number = 0;
            Vector3 origin = Vector3.zero;
            string where = "";
            yield return Bind(info, activity, requested, (n, o, w) =>
            {
                number = n;
                origin = o;
                where = w;
            });

            // Everything asked for from here on - the room's build and, later, its visit - is tagged with its scope.
            int scope = RrAssets.Instance != null ? RrAssets.Instance.NewScope() : 0;
            SetAssetScope(scope);
            RrRoom room = null;
            try
            {
                room = Create(info, activity, number, origin);
            }
            catch (Exception e)
            {
                RrLog.Error("Travel", "creating " + activity + " failed", e);
            }

            if (room == null)
            {
                result.Error = activity + ": Create made no room";
                SetAssetScope(0);
                yield return null;
                yield return FreeRoom(activity, scope);
                yield break;
            }

            room.AssetScope = scope;

            RrLog.Info("Travel", "building " + activity + (info.Networked ? " #" + number : "") + " at " + Fmt(origin) + " (" + where + ")");
            float t0 = Time.realtimeSinceStartup;
            screen.ProgressSource = () => BuildProgressOf(room, t0);
            var outcome = new RrTravelDriver.Outcome();
            IEnumerator routine = null;
            try
            {
                routine = room.Build();
            }
            catch (Exception e)
            {
                outcome.Error = e;
            }

            if (routine != null)
            {
                yield return RrTravelDriver.Guard(routine, outcome, t0 + BuildTimeout, () => room == null);
            }
            else if (outcome.Error == null)
            {
                outcome.Finished = true;
            }

            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            if (outcome.Finished && room != null)
            {
                room.Built = true;
                room.BuildProgress = 1f;
                result.Room = room;
                RrLog.Info("Travel", "built " + activity + " in " + ms.ToString("0") + " ms");
                yield break;
            }

            result.Error = outcome.TimedOut ? "the build took longer than " + BuildTimeout + " s" : "the build threw " + (outcome.Error != null ? outcome.Error.GetType().Name + ": " + outcome.Error.Message : "?");
            RrLog.Error("Travel", "building " + activity + " failed after " + ms.ToString("0") + " ms: " + result.Error, outcome.Error);
            SetAssetScope(0);
            if (room != null)
            {
                ClearShared(activity);
                Object.Destroy(room.gameObject);
            }

            yield return null;
            yield return FreeRoom(activity, scope);
        }

        private static void SetAssetScope(int scope)
        {
            if (RrAssets.Instance != null)
            {
                RrAssets.Instance.Scope = scope;
            }
        }

        /// <summary>A room's own progress (BuildProgress) mapped onto the bar, else a curve that slows as it fills.</summary>
        private static float BuildProgressOf(RrRoom room, float t0)
        {
            if (room != null && room.BuildProgress >= 0f)
            {
                return 0.12f + 0.78f * Mathf.Clamp01(room.BuildProgress);
            }

            float t = Time.realtimeSinceStartup - t0;
            return 0.12f + 0.78f * (1f - Mathf.Exp(-t / 4f));
        }

        private static RrRoom Create(RrRoomInfo info, RrActivity activity, int number, Vector3 origin)
        {
            var go = new GameObject("Room " + activity + (info.Networked ? " #" + number : ""));
            if (RrMain.Instance != null && RrMain.Instance.WorldRoot != null)
            {
                go.transform.SetParent(RrMain.Instance.WorldRoot, false);
            }

            go.transform.position = origin;
            go.transform.rotation = Quaternion.identity;
            RrRoom room;
            try
            {
                room = info.Create(go);
            }
            catch
            {
                Object.Destroy(go);
                throw;
            }

            if (room == null)
            {
                Object.Destroy(go);
                return null;
            }

            room.Activity = activity;
            room.InstanceNumber = number;
            room.Info = info;
            room.Built = false;
            return room;
        }

        /// <summary>
        /// Instance binding. Every room stands at the world origin (RrPlacement.DormOrigin / InstanceOrigin return
        /// it); what the binding decides is the instance: its number and the instance object k that carries its
        /// state, events and leases. The dorm (and any unnetworked room) needs none.
        ///
        /// The Net module's RrNet.JoinInstance(activity, requested), when it exists, picks the fullest joinable
        /// instance (instanceNumber -1) and binds an instance object k by the election of networking §5.4. It is
        /// found by reflection (<see cref="JoinMethod"/>) so this module compiles against a Net module that does not
        /// have it yet. Without it this adapter writes the location itself. Either way it then waits up to 5 s for
        /// RrNet.InstanceObjectIndex to name a (new) object, and otherwise places the room at the offline object
        /// k = activity - 1.
        /// </summary>
        private static IEnumerator Bind(RrRoomInfo info, RrActivity activity, int requested, Action<int, Vector3, string> done)
        {
            if (!info.Networked)
            {
                int slot = RrPlacement.LocalDormSlot();
                done(0, RrPlacement.DormOrigin(slot), "dorm slot " + slot + (RrNet.SlotIndex < 0 ? " (no presence slot)" : ""));
                yield break;
            }

            int number = requested >= 0 ? requested : 0;
            int before = RrNet.InstanceObjectIndex;
            bool rejoin = activity == _lastBoundActivity && number == _lastBoundInstance;
            var join = RrNet.Online ? JoinMethod() : null;
            bool joined = false;
            if (join != null)
            {
                object answer = null;
                bool invoked = false;
                try
                {
                    var parameters = join.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = activity;
                    args[1] = requested;
                    for (int i = 2; i < args.Length; i++)
                    {
                        args[i] = parameters[i].DefaultValue;
                    }

                    answer = join.Invoke(null, args);
                    invoked = true;
                }
                catch (Exception e)
                {
                    RrLog.Error("Travel", "RrNet.JoinInstance(" + activity + ", " + requested + ") threw; binding by presence instead", e is TargetInvocationException && e.InnerException != null ? e.InnerException : e);
                }

                if (invoked)
                {
                    var routine = answer as IEnumerator;
                    if (routine != null)
                    {
                        var outcome = new RrTravelDriver.Outcome();
                        yield return RrTravelDriver.Guard(routine, outcome, Time.realtimeSinceStartup + 10f);
                        if (!outcome.Finished)
                        {
                            RrLog.Warn("Travel", "RrNet.JoinInstance for " + activity + (outcome.TimedOut ? " took longer than 10 s" : " failed: " + (outcome.Error != null ? outcome.Error.Message : "?")));
                        }
                    }
                    else if (answer is int)
                    {
                        number = Math.Max(0, (int)answer);
                    }

                    joined = true;
                    RrPresenceView me = default(RrPresenceView);
                    Safe(() => me = RrNet.Presence(RrNet.LocalId), "presence (joined)");
                    if (me.Valid && me.Activity == activity && me.Instance >= 0)
                    {
                        number = me.Instance;
                    }
                }
            }

            if (!joined)
            {
                Safe(() => RrNet.SetLocation(activity, number, RrStatus.Loading), "presence (binding)");
            }

            int k = -1;
            if (RrNet.Online)
            {
                float until = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < until)
                {
                    int index = RrNet.InstanceObjectIndex;
                    if (index >= 0 && (index != before || before < 0 || rejoin))
                    {
                        k = index;
                        break;
                    }

                    yield return null;
                }

                if (k < 0)
                {
                    RrLog.Warn("Travel", "no instance object bound for " + activity + " #" + number + " within 5 s: playing it on its own at the offline place");
                }
            }

            string where = k >= 0 ? "instance object " + k : "offline instance object " + RrPlacement.OfflineInstanceObject(activity);
            if (k < 0)
            {
                k = RrPlacement.OfflineInstanceObject(activity);
            }

            _lastBoundActivity = activity;
            _lastBoundInstance = number;
            done(number, RrPlacement.InstanceOrigin(k), where);
        }

        private static MethodInfo _join;
        private static bool _joinLooked;

        /// <summary>
        /// RrNet.JoinInstance(RrActivity, int[, optional...]) if the Net module has it (looked up once, logged once).
        /// It may return an IEnumerator (run to the end, 10 s at most), the instance number (int) or nothing.
        /// </summary>
        private static MethodInfo JoinMethod()
        {
            if (_joinLooked)
            {
                return _join;
            }

            _joinLooked = true;
            try
            {
                foreach (var m in typeof(RrNet).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "JoinInstance")
                    {
                        continue;
                    }

                    var p = m.GetParameters();
                    bool fits = p.Length >= 2 && p[0].ParameterType == typeof(RrActivity) && p[1].ParameterType == typeof(int);
                    for (int i = 2; fits && i < p.Length; i++)
                    {
                        fits = p[i].IsOptional;
                    }

                    if (fits)
                    {
                        _join = m;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                RrLog.Warn("Travel", "looking for RrNet.JoinInstance failed: " + e.Message);
            }

            RrLog.Info("Travel", _join != null ? "instances are joined through RrNet.JoinInstance (returns " + _join.ReturnType.Name + ")" : "RrNet has no JoinInstance(RrActivity, int): instances are bound by presence and InstanceObjectIndex");
            return _join;
        }

        /// <summary>Spread arrivals over the room's spawns: the number of others already in the instance.</summary>
        private static int SpawnIndex()
        {
            try
            {
                var members = RrNet.MembersOfMyInstance();
                if (members == null)
                {
                    return 0;
                }

                int n = members.Count;
                if (members.Contains(RrNet.LocalId))
                {
                    n--;
                }

                return Mathf.Max(0, n);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Travel", "members", "MembersOfMyInstance failed: " + e.Message);
                return 0;
            }
        }

        private static IEnumerator Teleport(Vector3 feet, float yaw)
        {
            bool done = false;
            try
            {
                RrPlayer.Teleport(feet, yaw, () => done = true);
            }
            catch (Exception e)
            {
                RrLog.Error("Travel", "teleport to " + Fmt(feet) + " failed", e);
                yield break;
            }

            float until = Time.realtimeSinceStartup + TeleportTimeout;
            while (!done && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            if (!done)
            {
                RrLog.WarnOnce("Travel", "teleport-timeout", "the teleport did not report back within " + TeleportTimeout + " s; going on");
            }
        }

        /// <summary>After a broken trip: movement back, the card down, and the arrival room if no room is left.</summary>
        private static IEnumerator Recover()
        {
            Safe(() => RrPlayer.SetMovement(1f, true), "restoring movement");
            if (CurrentRoom == null)
            {
                Current = RrActivity.None;
                SetAssetScope(0);
                Safe(RrPostFx.Leave, "post-processing (a broken trip)");
                Safe(RrEnvironment.Leave, "lighting (a broken trip)");
                SetArrival(true, "a broken trip");
                yield return Teleport(RrPlacement.ArrivalSpot, 0f);
            }

            var screen = RrLoadingScreen.Instance;
            if (screen != null)
            {
                screen.ProgressSource = null;
                yield return screen.Uncover();
            }
        }

        /// <summary>The first seconds of the Rec Room theme as the activity appears, then the room's own music.</summary>
        private static IEnumerator Sting(RrRoom room, string roomMusic, int trip)
        {
            Safe(() => RrMusic.Play(ArrivalStingClip, 0.5f, false), "arrival sting");
            float until = Time.realtimeSinceStartup + ArrivalStingSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (_trip != trip || CurrentRoom != room)
                {
                    yield break;
                }

                yield return null;
            }

            if (RrMusic.Current != ArrivalStingClip)
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(roomMusic))
            {
                Safe(() => RrMusic.Play(roomMusic), "room music");
            }
            else
            {
                Safe(() => RrMusic.Stop(0.6f), "music stop");
            }
        }

        private static void PlayTeleportSound()
        {
            var assets = RrAssets.Instance;
            if (assets == null)
            {
                return;
            }

            // On the 2D interface source: the pooled 3D sources are stopped when the room is left, mid-whoosh.
            var clip = assets.Resident<AudioClip>(TeleportClip);
            if (clip != null)
            {
                Safe(() => RrSound.PlayUi(clip, 0.8f), "teleport sound");
                return;
            }

            Rr.RrEntry e;
            if (assets.TryFind(TeleportClip, 83, out e) && e.LoaderBundle >= 0)
            {
                Safe(() => RrSound.PlayUi(TeleportClip, 0.8f), "teleport sound");
            }
        }

        private static bool IsSocial(RrRoomInfo info)
        {
            return info != null && info.Category == "Social";
        }

        // ---- presence and authority, handed to the current room ----

        private static void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            RrNet.OnPlayersChanged += HandlePlayersChanged;
            RrNet.OnAuthorityChanged += HandleAuthorityChanged;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            RrNet.OnPlayersChanged -= HandlePlayersChanged;
            RrNet.OnAuthorityChanged -= HandleAuthorityChanged;
        }

        private static void HandlePlayersChanged()
        {
            var room = CurrentRoom;
            if (room == null || Busy || !room.Built)
            {
                _pendingMembers = true;
                return;
            }

            CallRoom(room, r => r.OnMembersChanged(), "OnMembersChanged");
        }

        private static void HandleAuthorityChanged(bool isAuthority)
        {
            var room = CurrentRoom;
            if (room == null || Busy || !room.Built)
            {
                _pendingAuthority = true;
                _pendingAuthorityValue = isAuthority;
                return;
            }

            CallRoom(room, r => r.OnAuthorityChanged(isAuthority), "OnAuthorityChanged");
        }

        /// <summary>Changes that came in during a trip reach the new room once it is entered.</summary>
        private static void FlushPending()
        {
            var room = CurrentRoom;
            bool members = _pendingMembers;
            bool authority = _pendingAuthority;
            bool value = _pendingAuthorityValue;
            _pendingMembers = false;
            _pendingAuthority = false;
            if (room == null || !room.Built)
            {
                return;
            }

            if (authority)
            {
                CallRoom(room, r => r.OnAuthorityChanged(value), "OnAuthorityChanged");
            }

            if (members)
            {
                CallRoom(room, r => r.OnMembersChanged(), "OnMembersChanged");
            }
        }

        private static void CallRoom(RrRoom room, Action<RrRoom> call, string what)
        {
            try
            {
                call(room);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Travel", what + ":" + room.Activity, room.Activity + "." + what + " threw " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ---- footing ----

        private static void ResetFooting()
        {
            _footingAttempts = 0;
            _footingWaitUntil = Time.unscaledTime + 2f;
            _footingGaveUp = false;
        }

        /// <summary>
        /// Twice a second (from the driver): a player whose feet are more than <see cref="FallDepth"/> below the
        /// room's lowest floor (<see cref="RrRoom.LowestFloor"/>; the arrival floor when there is no room) fell out
        /// of it and goes back to the room's rescue pose. Where they stand sideways is not looked at: every room is
        /// at the world origin and some of a room's areas are hundreds of metres from it. The client's respawn to
        /// the scene spawn is caught every frame by <see cref="WatchRespawn"/>. After three returns that change
        /// nothing - the teleport cannot move this player, as in the editor without the client - it stops trying
        /// until the next trip.
        /// </summary>
        internal static void CheckFooting()
        {
            if (Busy || !RrMain.Ready || _footingGaveUp || Time.unscaledTime < _footingWaitUntil)
            {
                return;
            }

            var room = CurrentRoom;
            if (room != null && !room.Built)
            {
                return;
            }

            Vector3 feet = RrPlayer.FeetPosition;
            float floor = room == null ? RrPlacement.ArrivalOrigin.y : float.IsNaN(room.LowestFloor) ? room.Origin.y : room.LowestFloor;
            if (feet.y >= floor - FallDepth)
            {
                _footingAttempts = 0;
                return;
            }

            Rescue(room, "fell out of " + (room != null ? room.Activity.ToString() : "the arrival room") + " (feet " + Fmt(feet) + ", lowest floor y " + floor.ToString("0.0") + ")");
        }

        // ---- helpers ----

        private static void RaiseArrived(RrActivity activity)
        {
            ArrivedAt = Time.realtimeSinceStartup;
            var handlers = OnArrived;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<RrActivity> h in handlers.GetInvocationList())
            {
                try
                {
                    h(activity);
                }
                catch (Exception e)
                {
                    RrLog.Error("Travel", "OnArrived handler failed", e);
                }
            }
        }

        private static void RaiseLeaving(RrActivity destination)
        {
            var handlers = OnLeaving;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<RrActivity> h in handlers.GetInvocationList())
            {
                try
                {
                    h(destination);
                }
                catch (Exception e)
                {
                    RrLog.Error("Travel", "OnLeaving handler failed", e);
                }
            }
        }

        private static void Safe(Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                RrLog.Error("Travel", what + " failed", e);
            }
        }

        private static void SetStatus(string text)
        {
            if (RrMain.Instance != null)
            {
                RrMain.Instance.SetStatus(text);
            }
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("0.#") + ", " + v.y.ToString("0.#") + ", " + v.z.ToString("0.#") + ")";
        }
    }
}
