using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The part of travel that follows from every room standing at the world origin (<see cref="RrPlacement"/>):
    ///
    /// <b>The arrival room</b> is the scene's "Arrival" slab (20 m square, top at y = 0) at the origin too, so it
    /// would cut through every room: its renderers and colliders are switched off once the player is in a room and
    /// back on whenever there is none (between two rooms, behind the loading card, and after a failed trip). They
    /// are our own scene objects, found by name among the roots of the world's scene.
    ///
    /// <b>Leaving a room</b> removes all of it before the next is built. Most of a room is under its root, but a
    /// few shared systems keep what they make under the world root and would show it in the next room at the
    /// same place: effects (RrFx), pooled one-shot sounds (RrSound), paint splats and impact effects (RrImpacts),
    /// shots in flight (RrProjectile), given tools (RrLoadout) and whatever the hands hold (parented to our grip,
    /// not the room). Those are cleared or put back under the room before it is destroyed; a frame later a census
    /// counts what is left under the world root outside any room and logs it.
    ///
    /// <b>Memory</b>: RrAssets forgets what only the old room asked for, Resources.UnloadUnusedAssets frees it,
    /// and the bundles only that room opened are closed when that is safe (<see cref="RrAssets.Release"/>) - all
    /// behind the card, where a long main-thread step is allowed. Memory is logged before and after
    /// (Profiler.GetTotalAllocatedMemoryLong, the managed heap).
    ///
    /// <b>Rescue</b>. The old "away from my room" check (feet more than 1 km from the room's origin) means
    /// nothing now. Two things are watched instead, and only our own teleports are trusted: a fall - feet 10 m
    /// below the room's lowest floor (the bottom of its lowest solid Environment collider, measured after the
    /// build), checked twice a second, which beats the client's own anti-fall at y = -100 (about 4.5 s of falling
    /// from a floor at 0; after 3 of those the client turns on free-fly) - and the client's respawn, which puts
    /// the body at the scene spawn (0, 0.9, 0) after such a fall or when the player presses Respawn: a jump of
    /// more than 3 m in one frame onto that point, with no teleport of ours in the last 2 s, sampled every frame.
    /// Either one sends the player to the room's rescue pose (the match's own respawn point while a match runs,
    /// else the room's spawn). Legitimate moves are never fought: horizontal distance is not looked at (Paintball's
    /// maps are 450 m from the lobby, quest acts up to 800 m) and every move of ours goes through RrPlayer.Teleport.
    /// </summary>
    public static partial class RrTravel
    {
        /// <summary>The scene object that is the arrival room's floor.</summary>
        public const string ArrivalObjectName = "Arrival";

        /// <summary>A jump of at least this much in one frame, landing on the scene spawn, is the client's respawn.</summary>
        public const float RespawnJump = 3f;

        /// <summary>How close to the scene spawn (horizontally; y within 0.35 m of 0.9) that landing must be (VR adds the head's offset).</summary>
        public const float RespawnRadius = 2f;

        /// <summary>Our own teleport within this many seconds explains any jump.</summary>
        public const float OwnTeleportGrace = 2f;

        /// <summary>Never trust a measured lowest floor below this: the rescue must fire before the client's -100.</summary>
        public const float LowestRescueFloor = -80f;

        /// <summary>
        /// Stay in the arrival room at start instead of going home (the play-mode harness sets it for scenarios
        /// that test things on the Arrival floor). Cleared by Shutdown.
        /// </summary>
        public static bool StayInArrival;

        private static GameObject _arrival;
        private static bool _arrivalLooked;
        private static bool _arrivalShown = true;
        private static readonly List<Renderer> ArrivalRenderers = new List<Renderer>();
        private static readonly List<Collider> ArrivalColliders = new List<Collider>();
        private static int _arrivalLogs;
        private static Vector3 _lastFeet;
        private static bool _haveLastFeet;
        private static int _rescues;

        // ---- the arrival room ----

        /// <summary>True while the arrival floor is shown and solid.</summary>
        public static bool ArrivalShown
        {
            get { return _arrivalShown; }
        }

        /// <summary>Shows (and makes solid) or hides the scene's Arrival floor and everything under it.</summary>
        internal static void SetArrival(bool shown, string why)
        {
            FindArrival();
            if (_arrival == null || shown == _arrivalShown)
            {
                return;
            }

            _arrivalShown = shown;
            foreach (var r in ArrivalRenderers)
            {
                if (r != null)
                {
                    r.enabled = shown;
                }
            }

            foreach (var c in ArrivalColliders)
            {
                if (c != null)
                {
                    c.enabled = shown;
                }
            }

            if (_arrivalLogs < 6)
            {
                _arrivalLogs++;
                RrLog.Info("Travel", "arrival room " + (shown ? "shown, colliders on" : "hidden, colliders off") + " (" + why + ")");
            }
        }

        private static void FindArrival()
        {
            // Looked for once: the floor is part of the saved scene and exists before any script runs.
            if (_arrivalLooked)
            {
                return;
            }

            _arrivalLooked = true;
            _arrival = null;
            ArrivalRenderers.Clear();
            ArrivalColliders.Clear();
            var main = RrMain.Instance;
            if (main != null)
            {
                _arrival = FindRoot(main.gameObject.scene, ArrivalObjectName);
            }

            if (_arrival == null)
            {
                _arrival = FindRoot(SceneManager.GetActiveScene(), ArrivalObjectName);
            }

            if (_arrival == null)
            {
                RrLog.Warn("Travel", "no \"" + ArrivalObjectName + "\" object at the root of the world's scene: the arrival room cannot be hidden in rooms");
                return;
            }

            // Only what the scene has switched on is ours to switch.
            foreach (var r in _arrival.GetComponentsInChildren<Renderer>(true))
            {
                if (r.enabled)
                {
                    ArrivalRenderers.Add(r);
                }
            }

            foreach (var c in _arrival.GetComponentsInChildren<Collider>(true))
            {
                if (c.enabled)
                {
                    ArrivalColliders.Add(c);
                }
            }

            RrLog.Info("Travel", "arrival room: \"" + ArrivalObjectName + "\" at " + Fmt(_arrival.transform.position) + " with " + ArrivalRenderers.Count + " renderers and " + ArrivalColliders.Count + " colliders");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (var go in scene.GetRootGameObjects())
            {
                if (go != null && go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        // ---- leaving a room ----

        /// <summary>Whatever the hands hold goes back to the room it came from (it is parented to our grip, outside the room).</summary>
        private static void ReleaseHeld(RrRoom old)
        {
            foreach (var hand in new[] { RrHand.Left, RrHand.Right })
            {
                if (hand == null)
                {
                    continue;
                }

                Interact.RrGrabbable item = null;
                Safe(() => item = Interact.RrGrab.HeldBy(hand), "held item");
                if (item == null)
                {
                    continue;
                }

                string name = item.name;
                Safe(item.ForceRelease, "releasing " + name);
                if (item != null && old != null && !item.transform.IsChildOf(old.transform))
                {
                    item.transform.SetParent(old.transform, true);
                }

                RrLog.Info("Travel", "released " + name + " from the " + hand.Side + " hand: it stays with " + (old != null ? old.Activity.ToString() : "the room"));
            }
        }

        /// <summary>Clears what shared systems made for the room outside its root (see the class summary).</summary>
        private static void ClearShared(RrActivity left)
        {
            int fx = 0;
            int sounds = 0;
            int decals = 0;
            Safe(() => fx = RrFx.Clear(), "clearing effects");
            Safe(() => sounds = RrSound.StopWorld(), "stopping sounds");
            Safe(() => decals = Games.RrImpacts.DecalCount, "counting decals");
            Safe(Games.RrImpacts.Clear, "clearing impacts");
            Safe(Games.RrProjectile.ClearAll, "clearing projectiles");
            Safe(Games.RrLoadout.Clear, "clearing the loadout");
            RrLog.Info("Travel", "cleared what " + left + " left outside its root: " + fx + " effects, " + sounds + " sounds, " + decals + " splats, shots in flight and given tools");
        }

        /// <summary>Frees what only the left room used (RrAssets scopes) and logs memory before and after.</summary>
        private static IEnumerator FreeRoom(RrActivity left, int scope)
        {
            var assets = RrAssets.Instance;
            if (assets == null || scope <= 0)
            {
                yield break;
            }

            long before = Profiler.GetTotalAllocatedMemoryLong();
            long managedBefore = GC.GetTotalMemory(false);
            float t0 = Time.realtimeSinceStartup;
            string report = "no report";
            var outcome = new RrTravelDriver.Outcome();
            yield return RrTravelDriver.Guard(assets.Release(scope, r => report = r), outcome, Time.realtimeSinceStartup + 20f);
            if (!outcome.Finished)
            {
                RrLog.Warn("Travel", "freeing " + left + "'s assets " + (outcome.TimedOut ? "took longer than 20 s" : "failed: " + (outcome.Error != null ? outcome.Error.Message : "?")));
            }

            long after = Profiler.GetTotalAllocatedMemoryLong();
            RrLog.Info("Travel", "freed " + left + " in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms: memory " + Mb(before) + " -> " + Mb(after) + " MB, managed " + Mb(managedBefore) + " -> " + Mb(GC.GetTotalMemory(false)) + " MB; " + report);
        }

        /// <summary>
        /// One line that proves where things are: the room and its origin, the feet, what is under the world root
        /// (in the room, outside it, active renderers outside it that are not the player's, the watch's or the
        /// card's), how many RrRoom components exist, the scene roots and memory.
        /// </summary>
        internal static string Census(string when)
        {
            var sb = new StringBuilder();
            sb.Append("census ").Append(when).Append(": ");
            var room = CurrentRoom;
            Transform root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            int world = root != null ? root.GetComponentsInChildren<Transform>(true).Length : 0;
            int inRoom = room != null ? room.GetComponentsInChildren<Transform>(true).Length : 0;
            int stray = 0;
            string firstStray = null;
            if (root != null)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || (room != null && r.transform.IsChildOf(room.transform)) || IsPersistent(r.transform, root))
                    {
                        continue;
                    }

                    stray++;
                    if (firstStray == null)
                    {
                        firstStray = PathOf(r.transform, root);
                    }
                }
            }

            int rooms = Object.FindObjectsByType<RrRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int expected = room != null ? 1 : 0;
            string extraRoots = ExtraSceneRoots();
            if (rooms == expected && stray == 0 && extraRoots.Length == 0)
            {
                sb.Append(room != null ? "1 room, nothing outside it" : "no room, nothing of it left");
            }
            else
            {
                sb.Append(room != null ? "OUTSIDE THE ROOM: " : "LEFT BEHIND: ").Append(rooms).Append(" RrRoom components (expected ").Append(expected).Append("), ")
                  .Append(stray).Append(" active renderers under the world root outside a room").Append(firstStray != null ? " (first: " + firstStray + ")" : "")
                  .Append(extraRoots.Length > 0 ? ", new scene roots: " + extraRoots : "");
            }

            sb.Append("; room ").Append(room != null ? room.Activity + " at " + Fmt(room.Origin) : "none (arrival room)");
            sb.Append(", feet ").Append(Fmt(RrPlayer.FeetPosition));
            sb.Append(", arrival ").Append(_arrivalShown ? "shown" : "hidden");
            sb.Append(", world root ").Append(world).Append(" objects (").Append(inRoom).Append(" in the room, ").Append(world - inRoom).Append(" outside)");
            var ours = RrMain.Instance != null ? RrMain.Instance.gameObject.scene : SceneManager.GetActiveScene();
            var active = SceneManager.GetActiveScene();
            sb.Append(", scene roots ").Append(ours.IsValid() ? ours.rootCount : -1);
            if (active.IsValid() && active != ours)
            {
                sb.Append(" (active scene ").Append(active.rootCount).Append(")");
            }

            sb.Append(", memory ").Append(Mb(Profiler.GetTotalAllocatedMemoryLong())).Append(" MB (managed ").Append(Mb(GC.GetTotalMemory(false))).Append(" MB)");
            if (RrAssets.Instance != null)
            {
                sb.Append(", ").Append(RrAssets.Instance.Describe());
            }

            string line = sb.ToString();
            RrLog.Info("Travel", line);
            return line;
        }

        // Scene roots at start (the arrival room, before any room): anything added later is listed by the census.
        private static readonly HashSet<int> BaseRoots = new HashSet<int>();

        /// <summary>Remembers the scene roots as they are now (called by Begin, in the arrival room).</summary>
        internal static void RememberSceneRoots()
        {
            BaseRoots.Clear();
            foreach (var scene in CensusScenes())
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    BaseRoots.Add(go.GetInstanceID());
                }
            }
        }

        private static string ExtraSceneRoots()
        {
            if (BaseRoots.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            int n = 0;
            var ours = RrMain.Instance != null ? RrMain.Instance.gameObject.scene : SceneManager.GetActiveScene();
            foreach (var scene in CensusScenes())
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (BaseRoots.Contains(go.GetInstanceID()) || go.name == "Rr spawn holder")
                    {
                        continue;
                    }

                    // In the client's own active scene only what is recognisably ours counts (its avatars come and go).
                    if (scene != ours && go.GetComponent<RrInstance>() == null && !go.name.StartsWith("Rr ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (++n <= 6)
                    {
                        sb.Append(n > 1 ? " | " : "").Append(go.name).Append(go.activeInHierarchy ? "" : " (inactive)");
                    }
                }
            }

            return n > 6 ? sb.Append(" and ").Append(n - 6).Append(" more").ToString() : sb.ToString();
        }

        private static List<Scene> CensusScenes()
        {
            var list = new List<Scene>();
            var ours = RrMain.Instance != null ? RrMain.Instance.gameObject.scene : SceneManager.GetActiveScene();
            if (ours.IsValid() && ours.isLoaded)
            {
                list.Add(ours);
            }

            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && active != ours)
            {
                list.Add(active);
            }

            return list;
        }

        // The world-level things that are ours and legitimately visible in every room.
        private static readonly string[] PersistentRoots = { "Rr Player", "Rr Watch", "Rr Loading Screen", "Rr Screen Hand" };

        private static bool IsPersistent(Transform t, Transform worldRoot)
        {
            Transform top = t;
            while (top.parent != null && top.parent != worldRoot)
            {
                top = top.parent;
            }

            string n = top.name;
            for (int i = 0; i < PersistentRoots.Length; i++)
            {
                if (n == PersistentRoots[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static string PathOf(Transform t, Transform stop)
        {
            string path = t.name;
            for (var p = t.parent; p != null && p != stop; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// The room's lowest floor (world y): the bottom of its lowest enabled, solid collider on the Environment
        /// layer; the room's origin when it has none. Clamped to <see cref="LowestRescueFloor"/>.
        /// </summary>
        private static float MeasureLowestFloor(RrRoom room)
        {
            Physics.SyncTransforms();
            float lowest = float.PositiveInfinity;
            int n = 0;
            foreach (var c in room.GetComponentsInChildren<Collider>(false))
            {
                if (c == null || !c.enabled || c.isTrigger || c.gameObject.layer != RrLayers.Environment)
                {
                    continue;
                }

                n++;
                float y = c.bounds.min.y;
                if (y < lowest)
                {
                    lowest = y;
                }
            }

            float floor = n > 0 ? Mathf.Max(lowest, LowestRescueFloor) : room.Origin.y;
            RrLog.Info("Travel", room.Activity + ": lowest floor y = " + floor.ToString("0.0") + " (" + n + " environment colliders); a player 10 m below it is put back");
            return floor;
        }

        // ---- rescue ----

        /// <summary>
        /// Every frame (from the driver): a jump onto the scene spawn that none of our teleports explains is the
        /// client's respawn (after a fall below -100, or the Respawn button) - back to the room.
        /// </summary>
        internal static void WatchRespawn()
        {
            var room = CurrentRoom;
            if (Busy || room == null || !room.Built || !RrMain.Ready)
            {
                _haveLastFeet = false;
                return;
            }

            Vector3 feet = RrPlayer.FeetPosition;
            if (_haveLastFeet)
            {
                Vector3 step = feet - _lastFeet;
                float now = Time.realtimeSinceStartup;
                bool ours = now - RrPlayer.LastTeleportAt < OwnTeleportGrace || now - RrPlayer.LastTeleportDoneAt < 1f;
                if (!ours && step.sqrMagnitude > RespawnJump * RespawnJump && AtSceneSpawn(feet))
                {
                    Rescue(room, "the client respawned the player (a " + step.magnitude.ToString("0.0") + " m jump from " + Fmt(_lastFeet) + " to the scene spawn " + Fmt(feet) + ")");
                    _haveLastFeet = false;
                    return;
                }
            }

            _lastFeet = feet;
            _haveLastFeet = true;
        }

        private static bool AtSceneSpawn(Vector3 feet)
        {
            Vector3 s = RrPlacement.SceneSpawn;
            float dx = feet.x - s.x;
            float dz = feet.z - s.z;
            return dx * dx + dz * dz <= RespawnRadius * RespawnRadius && Mathf.Abs(feet.y - s.y) <= 0.35f;
        }

        /// <summary>Where the rescue puts the player: a running match's respawn point, else the room's rescue pose.</summary>
        private static Pose RescuePoseFor(RrRoom room)
        {
            try
            {
                var health = Games.RrHealth.Local;
                if (health != null && health.InCombat)
                {
                    return health.RespawnPose();
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Travel", "rescue:health", "the match's respawn point failed: " + e.Message);
            }

            try
            {
                return room.RescuePose(_spawnIndex);
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Travel", "spawn:" + room.Activity, room.Activity + ".RescuePose threw " + e.Message);
                return new Pose(room.Origin + Vector3.up * 0.05f, Quaternion.identity);
            }
        }

        private static void Rescue(RrRoom room, string why)
        {
            if (_footingGaveUp)
            {
                return;
            }

            if (_footingAttempts >= 3)
            {
                _footingGaveUp = true;
                RrLog.Warn("Travel", "3 rescues in a row changed nothing (" + why + "); not trying again until the next trip");
                return;
            }

            _footingAttempts++;
            _footingWaitUntil = Time.unscaledTime + 3f;
            _rescues++;
            Pose pose = room != null ? RescuePoseFor(room) : new Pose(RrPlacement.ArrivalSpot, Quaternion.identity);
            RrLog.Info("Travel", "rescue " + _rescues + ": " + why + " -> " + (room != null ? room.Activity + " " : "arrival room ") + Fmt(pose.position));
            Safe(() => RrPlayer.Teleport(pose.position, pose.rotation.eulerAngles.y), "rescue teleport");
        }

        // Arrivals per activity, so each visit's census line can be told apart in the log.
        private static readonly Dictionary<RrActivity, int> Visits = new Dictionary<RrActivity, int>();

        private static int NextVisit(RrActivity activity)
        {
            int n;
            Visits.TryGetValue(activity, out n);
            Visits[activity] = ++n;
            return n;
        }

        private static int VisitOf(RrActivity activity)
        {
            int n;
            return Visits.TryGetValue(activity, out n) ? n : 0;
        }

        private static void ResetOrigin()
        {
            Visits.Clear();
            StayInArrival = false;
            _arrival = null;
            _arrivalLooked = false;
            _arrivalShown = true;
            ArrivalRenderers.Clear();
            ArrivalColliders.Clear();
            _arrivalLogs = 0;
            _haveLastFeet = false;
            _rescues = 0;
        }

        private static string Mb(long bytes)
        {
            return (bytes / (1024.0 * 1024.0)).ToString("0.0");
        }
    }
}
