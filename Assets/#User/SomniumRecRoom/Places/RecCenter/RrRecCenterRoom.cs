using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using L = SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter.RrRecCenterLayout;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.RecCenter
{
    /// <summary>
    /// The Rec Center as Rec Room left it (the July 2025 Studio room, measured from the user's download cache:
    /// rr_reccenter_cache.md), at the world origin: the timber-beamed hall with the kiosk and its REC ROOM sign under
    /// two skylights, the balcony, the curved window wall, the stage and theatre, the gym, the cafe wing and the
    /// hallway to the Dorm (<see cref="RrRecCenterShell"/>). The doors are Rec Room's final ones (since Jun 2021):
    /// SPORTS & PVP, QUESTS, CREATIVE CAMPUS and FEATURED ROOMS each open a list of activities, and the Magic Door
    /// picks one (<see cref="RrCategoryDoor"/>, the doors agent's); the Dorm door stays an <see cref="RrPortal"/>.
    /// Rec Room's prefabs furnish it: the reception bell, three radios, two hoops, two ping-pong tables, the lounge,
    /// the cafe, the Friend-O-Tron and vending machine, the community board, the mirror, Puzzles & Art, loose toys.
    /// What the real room never had is gone: pool, poker, the jukebox and the lockers (user decision, iteration 3).
    ///
    /// Iteration 6: when the player's PC still has Rec Room's own July 2025 Rec Center in Rec Room's download cache,
    /// that building is loaded instead of the rebuild (<see cref="RrRecCenterCache"/>, nothing ships with the world):
    /// then our furniture copies are left out (<see cref="RrRecCenterLayout.KeepWithCachedBuilding"/>), our doors,
    /// hoops and props are fitted onto its real floors and walls (<see cref="RrRecCenterFit"/>), our vending
    /// machine and Friend-O-Tron take the places of its two static vending machines, its door signs replace ours, and
    /// its own two ambience sources play. Without the cache, the rebuild below stands as before.
    /// Iteration 12: the cached building's mirror is the one in the theatre's dark-wood frame (<see cref="TheatreMirror"/>);
    /// our hallway mirror stays with the rebuild only.
    /// Iteration 16: players without the cache no longer get the rebuild. They get the same building, built from the
    /// cached scene's measurements shipped with the world (<see cref="BuildFromData"/>, Data/RecCenter_Final.bytes) in
    /// the cached building's own frame, and everything above (furniture rules, fitting, theatre mirror, Magic Door spot)
    /// treats it as Rec Room's building (<see cref="RealBuilding"/>). The rebuild stays only for a world without that data.
    ///
    /// Networking ("fake master", networking doc §5): shared toys carry RrNetBody (LocalPropId 1..16); the radios'
    /// tracks live in the instance's custom ints 0..2, which only the instance authority writes - everyone else asks
    /// with event 65 and repeats it until the ints show it. The bell is event 64, played by every member. Offline the
    /// local player is the authority.
    /// </summary>
    public sealed class RrRecCenterRoom : RrRoom
    {
        public const byte EvBell = 64;
        public const byte EvRadio = 65;

        // Custom instance ints (0..31 = ints 27..58).
        public const int IntRadioKiosk = 0;
        public const int IntRadioCafe = 1;
        public const int IntRadioLounge = 2;
        public const int IntMagic = 31;

        /// <summary>
        /// Marks the custom ints as the Rec Center's (a fresh or reused instance object holds zeros or another room's
        /// values). "RCR2": the final layout; slot 2 was the jukebox before iteration 3, ints 3..21 the pool.
        /// </summary>
        public const int Magic = 0x52435232;

        /// <summary>ReceptionBell minTimeBetweenRings (rr_reccenter.md §2.3).</summary>
        public const float BellCooldown = 0.25f;

        /// <summary>How far in front of a category door a player returning from one of its activities appears.</summary>
        public const float ReturnDistance = 2.2f;

        public static RrRecCenterRoom Current { get; private set; }

        public int Spawned { get; private set; }
        public int Failed { get; private set; }
        public int ToolCount { get; private set; }
        public int NetPropCount { get; private set; }
        public int BellRings { get; private set; }
        public int BottlesDispensed { get; private set; }
        public Transform BellTransform { get; private set; }
        public RrPortal DormDoor { get; private set; }
        public RrDormMirror Mirror { get; private set; }

        /// <summary>The mirror in the theatre's frame of Rec Room's cached building (iteration 12), or null (the rebuild).</summary>
        public RrMirror TheatreMirror { get; private set; }

        /// <summary>Rec Room's own building from the player's download cache, or null (the Dreamport rebuild stands).</summary>
        public RrRecCenterCache Cached { get; private set; }

        /// <summary>
        /// Iteration 16: Rec Room's own building built from the measurements shipped with the world (Data/RecCenter_Final.bytes,
        /// <see cref="BuildFromData"/>), in exactly the cached building's frame; null with the cache or the rebuild.
        /// </summary>
        public Transform DataFrame { get; private set; }

        /// <summary>The scene bank file of the measured building (DevNotes/tools/rcscene, DevNotes/iter16/rooms_same.md).</summary>
        public const string DataFile = "RecCenter_Final";

        /// <summary>Its scene name: the cached scene's, so RrEnvironment's entry for the cached building (sun, sky, fog) applies.</summary>
        public const string DataScene = "RecCenter_071825";

        /// <summary>Rec Room's building stands (cached or measured): our furniture copies go, our things are fitted onto it.</summary>
        public bool RealBuilding
        {
            get { return Cached != null || DataFrame != null; }
        }

        /// <summary>Which building this room built (logs, tests).</summary>
        public string BuildingName
        {
            get { return Cached != null ? "Rec Room's cached building" : DataFrame != null ? "Rec Room's building from the measured data" : "the Dreamport rebuild"; }
        }

        private Transform BuildingFrame
        {
            get { return Cached != null ? Cached.Frame : DataFrame; }
        }

        private readonly Dictionary<string, Material> _dataSurfaces = new Dictionary<string, Material>();

        /// <summary>Our furniture copies left out because the cached building has its own (0 with the rebuild).</summary>
        public int SkippedDuplicates { get; private set; }

        /// <summary>The cached building's own objects hidden under our interactives (vending machines, board, signs).</summary>
        public int HiddenOfTheirs { get; private set; }

        /// <summary>The category doors and the Magic Door, in <see cref="RrRecCenterLayout.Doors"/> order.</summary>
        public readonly List<GameObject> Doors = new List<GameObject>();

        public readonly List<RrHoop> Hoops = new List<RrHoop>();
        public readonly List<RrRecCenterRadio> Radios = new List<RrRecCenterRadio>();
        public readonly List<RrGrabbable> NetProps = new List<RrGrabbable>();

        /// <summary>The first hoop (the scene's painted key is on its wall), or null.</summary>
        public RrHoop Hoop
        {
            get { return Hoops.Count > 0 ? Hoops[0] : null; }
        }

        public int DoorCount
        {
            get { return Doors.Count + (DormDoor != null ? 1 : 0); }
        }

        private readonly List<Object> _owned = new List<Object>();
        private readonly List<GameObject> _bottles = new List<GameObject>();
        private readonly List<AudioSource> _ambience = new List<AudioSource>();
        private RrRcSurfaces _surfaces;
        private RrRecCenterFit _fit;
        private Transform _props;
        private RrInstance _vending;
        private RrInstance _friendotron;
        private TextMeshPro _friendotronText;
        private float _friendotronUntil;
        private float _lastBell = -10f;
        private float _lastVend = -10f;
        private bool _hooked;
        private int _pending;

        public override RrStatus ArrivalStatus
        {
            get { return RrStatus.Playing; }
        }

        /// <summary>True once this instance's custom ints carry the Rec Center's layout.</summary>
        public static bool IntsValid
        {
            get { return RrNet.Instance.Get(IntMagic) == Magic; }
        }

        public override IEnumerator Build()
        {
            Current = this;
            RrRcGeo.Owned = _owned;
            float started = Time.realtimeSinceStartup;
            BuildProgress = 0f;
            // The Dorm door is Rec Room's double activity door; its template (the Charades hull, key 84) also makes
            // the wood floors, paints, brick and carpets resident for the shell.
            var portal = RrPortal.Prepare();
            while (portal.MoveNext())
            {
                BuildProgress = 0.05f;
                yield return portal.Current;
            }

            // Iteration 6: Rec Room's own building from the player's download cache (RrRecCenterCache); without it,
            // the Dreamport rebuild.
            BuildProgress = 0.08f;
            yield return RrRecCenterCache.Load(transform, c => Cached = c);
            if (Cached != null)
            {
                _fit = new RrRecCenterFit(transform, Cached.Frame);
                BuildProgress = 0.45f;
            }
            else
            {
                // Iteration 16: every other player gets the same building, from the measurements shipped with the world
                // (BuildFromData); the old Dreamport rebuild only when that data is missing.
                yield return BuildFromData();
                if (this == null)
                {
                    yield break;
                }

                if (DataFrame != null)
                {
                    _fit = new RrRecCenterFit(transform, DataFrame);
                    BuildProgress = 0.45f;
                }
                else
                {
                    _surfaces = new RrRcSurfaces(_owned);
                    yield return _surfaces.Load();
                    BuildProgress = 0.15f;
                    yield return RrRecCenterShell.Build(transform, _surfaces, p => BuildProgress = 0.15f + p * 0.3f);
                }
            }

            BuildDoors();
            if (_fit != null)
            {
                FitDoors();
            }

            BuildProgress = 0.45f;
            yield return null;

            _props = RrBuild.Group(transform, "Props").transform;
            yield return SpawnAll();
            if (_fit != null)
            {
                FitLoose();
                _fit.Log("the props");
                BuildTheatreMirror();
                HideCachedDuplicates();
                if (Cached != null)
                {
                    yield return HideOurSigns();
                }
            }

            if (Cached == null)
            {
                // Rec Room's light shafts and level effects (iteration 5, RrRecCenterFx); the cached building has its own
                // (skinned shafts and the wind particles, which the measured data leaves out).
                yield return RrRecCenterFx.Build(transform, _owned);
            }

            BuildBoard();
            InitInts();
            Hook(true);
            BuildProgress = 1f;
            RrLog.Info("RecCenter", "built at (" + Origin.x.ToString("0") + ", " + Origin.z.ToString("0") + ") in "
                + (Time.realtimeSinceStartup - started).ToString("0.0") + " s (" + BuildingName + "): "
                + DoorCount + " doors (" + Doors.Count + " category/magic + Dorm), "
                + Spawned + " prefabs (" + Failed + " failed), " + ToolCount + " local tools, " + NetPropCount + " shared props, "
                + Radios.Count + " radios, " + Hoops.Count + " hoops, mirror " + (Mirror != null ? "ready" : TheatreMirror != null ? "ready (theatre)" : "missing"));
        }

        /// <summary>
        /// From the Dorm or the world: in the hallway, facing into the hall (the rec.net picture's view). Back from an
        /// activity: 2.2 m in front of the category door that lists it, facing away from the door.
        /// </summary>
        public override Pose Spawn(int index)
        {
            var prev = RrTravel.Previous;
            int i = Mathf.Abs(index);
            if (prev != RrActivity.None && prev != RrActivity.Dorm && prev != RrActivity.RecCenter)
            {
                var doors = L.Doors;
                for (int d = 0; d < doors.Length; d++)
                {
                    if (doors[d].Magic || !Lists(doors[d].Category, prev))
                    {
                        continue;
                    }

                    var rot = Quaternion.Euler(0f, doors[d].Yaw, 0f);
                    var offset = new Vector3((i % 3 - 1) * 0.7f, 0.05f, ReturnDistance + (i / 3 % 2) * 0.8f);
                    return new Pose(doors[d].Pos + rot * offset, rot);
                }
            }

            var spots = L.HallwaySpawns;
            return new Pose(spots[i % spots.Length] + Vector3.up * 0.05f, Quaternion.identity);
        }

        /// <summary>A fall or a client respawn goes back to the hallway spots (inside the building, facing the hall).</summary>
        public override Pose RescuePose(int index)
        {
            var spots = L.HallwaySpawns;
            var local = spots[Mathf.Abs(index) % spots.Length] + Vector3.up * 0.05f;
            return new Pose(transform.TransformPoint(local), transform.rotation);
        }

        private static bool Lists(RrDoorCategory category, RrActivity activity)
        {
            var list = RrCategoryDoor.Activities(category);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == activity)
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnLocalEnter()
        {
            if (RrTravel.Previous == RrActivity.Dorm && DormDoor != null)
            {
                DormDoor.PlayArrive();
            }

            // Ambience (iteration 5): Rec Room's own two sources in the July 2025 room (cache 1AF, Systems/Audio/Local;
            // the room has no other audio). Neither clip is in the install, so each plays the install's closest one:
            // - AmbianceLightCrowd: 0.25, loop, 2D (pan level 0), the whole room -> the onboarding's people ambience,
            //   2D, at 0.135 (the level Rec Room's OrientationStage1 gives this clip);
            // - CafeCounter (RecCenter_Ambiance_CafeCounter): 0.65, loop, 3D, custom roll-off 9.55 -> 13.08 m, at room
            //   (-19.74, -0.60, -1.82) = the cafe counter -> the coffee pour loop (GUESS clip and volume 0.2), with Rec
            //   Room's distances as a linear roll-off, 1.0 m up.
            // Iteration 3's birds at the windows and the kiosk hum are gone: Rec Room's room had neither.
            StopAmbience();
            if (Cached != null)
            {
                // The cached building brought Rec Room's own two sources with their own clips; they play as the room made them.
                RrLog.Info("RecCenter", "ambience: Rec Room's own AmbianceLightCrowd and CafeCounter sources from the cached building ("
                    + Cached.AudioSources + " sources)");
                RrLog.Info("RecCenter", "entered from " + RrTravel.Previous + " (instance " + InstanceNumber + ", authority " + IsAuthority + ")");
                return;
            }

            Ambient("sfx_onboarding_outdoor_ambiance_local_people02", new Vector3(0f, 3f, 0f), 0.135f, 500f, 0f);
            Ambient("sfx_consumables_coffee_pour_loop_01", new Vector3(-19.74f, 1.0f, -1.82f), 0.2f, 13.08f, 1f, 9.55f);
            RrLog.Info("RecCenter", "ambience: Rec Room's AmbianceLightCrowd (2D 0.25) as sfx_onboarding_outdoor_ambiance_local_people02 (2D 0.135), CafeCounter (0.65, 9.55-13.08 m) at (-19.74, 1.0, -1.82) as sfx_consumables_coffee_pour_loop_01 (0.2, linear)");
            RrLog.Info("RecCenter", "entered from " + RrTravel.Previous + " (instance " + InstanceNumber + ", authority " + IsAuthority + ")");
        }

        public override void OnLocalExit()
        {
            StopAmbience();
        }

        public override void OnAuthorityChanged(bool isAuthority)
        {
            if (isAuthority)
            {
                InitInts();
            }
        }

        private void OnDestroy()
        {
            Hook(false);
            StopAmbience();
            if (Current == this)
            {
                Current = null;
            }

            if (RrRcGeo.Owned == _owned)
            {
                RrRcGeo.Owned = null;
            }

            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Destroy(_owned[i]);
                }
            }

            _owned.Clear();
        }

        private void Update()
        {
            if (_friendotronText != null && _friendotronText.gameObject.activeSelf && Time.time > _friendotronUntil)
            {
                _friendotronText.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ the measured building (iteration 16)

        /// <summary>
        /// Rec Room's own July 2025 building for every player whose PC does not have Rec Room's download cache: the very
        /// scene the cache path loads (RecCenter-RecCenter_071825), measured offline into Data/RecCenter_Final.bytes by
        /// DevNotes/tools/rcscene (names and numbers only: nothing of the cache ships) and built by the scene kit
        /// (<see cref="RrJumbotronArea"/>): the walls, floors, beams, windows, kiosk, balcony, stage, theatre, cafe, gym and
        /// furniture as exact rectangles at Rec Room's measured colours (walls in their own vertex paint), the props the
        /// install has as install meshes, every collider of the scene, its 39 point and spot lights in Rec Room's light
        /// grid, the terrain and trees round it. It stands under a container with the cached building's own pose
        /// (<see cref="RrRecCenterCache.FramePosition"/>, <see cref="RrRecCenterCache.FrameYaw"/>), so the doors, props,
        /// fitting (<see cref="RrRecCenterFit"/>), the theatre mirror and the spawns land exactly as they do for players
        /// with the cache. The objects the cache path hides under ours (the two static vending machines, the community
        /// board) and the building's door signs (their texts are TextMeshPro, ours stay) are left out of the data; their
        /// spots come back as empty named objects (G spot records at the centre of what they drew) for the fitting.
        /// </summary>
        private IEnumerator BuildFromData()
        {
            string raw = RrSceneBank.Text(DataFile);
            if (raw == null)
            {
                RrLog.Warn("RecCenter", "no measured building data (" + DataFile + ".bytes): building the Dreamport rebuild");
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            var frame = new GameObject("Rec Room Building (measured)").transform;
            frame.SetParent(transform, false);
            frame.localPosition = RrRecCenterCache.FramePosition;
            frame.localRotation = Quaternion.Euler(0f, RrRecCenterCache.FrameYaw, 0f);
            var area = new RrJumbotronArea(DataScene, Vector3.zero);
            BuildProgress = 0.12f;
            yield return area.Build(raw, frame, _dataSurfaces);
            if (this == null)
            {
                yield break;
            }

            if (frame == null || area.Kit == null)
            {
                RrLog.Warn("RecCenter", "the measured building did not build: the Dreamport rebuild instead");
                if (frame != null)
                {
                    Destroy(frame.gameObject);
                }

                yield break;
            }

            // The named spots RrRecCenterFit looks the cached building's objects up by ([Door_Sign], [VendingMachine_Static],
            // BulletinBoard ...): empty objects at the centre of what each drew in the scene, with its rotation.
            var spots = new GameObject("Spots").transform;
            spots.SetParent(frame, false);
            int n = 0;
            foreach (var m in area.Markers("spot"))
            {
                var go = new GameObject(m.Name);
                go.transform.SetParent(spots, false);
                go.transform.localPosition = m.Position;
                go.transform.localRotation = m.Rotation;
                n++;
            }

            DataFrame = frame;
            RrLog.Info("RecCenter", "Rec Room's July 2025 building from the measured data (" + DataFile + ".bytes, scene " + DataScene + ") in "
                + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s, in the cached building's frame " + RrRecCenterCache.FramePosition.ToString("F2")
                + " yaw " + RrRecCenterCache.FrameYaw.ToString("0") + ": " + n + " named spots, " + area.Markers("sign").Count + " door-sign texts");
        }

        // ------------------------------------------------------------------ doors

        /// <summary>The category doors and the Magic Door at their measured spots, and the Dorm door in the hallway.</summary>
        private void BuildDoors()
        {
            var parent = RrBuild.Group(transform, "Doors").transform;
            var doors = L.Doors;
            var summary = new List<string>();
            for (int i = 0; i < doors.Length; i++)
            {
                var d = doors[i];
                if (d.Magic && RealBuilding)
                {
                    // Its rebuild spot is an opening into the cafe in Rec Room's building.
                    d = L.MagicWithCachedBuilding;
                }

                GameObject go = null;
                try
                {
                    go = d.Magic ? RrCategoryDoor.CreateMagicDoor(parent, d.Pos, d.Yaw) : RrCategoryDoor.Create(parent, d.Pos, d.Yaw, d.Category);
                }
                catch (Exception e)
                {
                    RrLog.Error("RecCenter", "door " + (d.Magic ? "Magic" : d.Category.ToString()) + " failed", e);
                }

                if (go != null)
                {
                    Doors.Add(go);
                    summary.Add(d.Magic ? RrCategoryDoor.MagicSign : RrCategoryDoor.Sign(d.Category) + " (" + RrCategoryDoor.Activities(d.Category).Count + ")");
                }
            }

            DormDoor = RrPortal.Create(parent, RrActivity.Dorm, L.DormDoor, 0f, "DORM ROOM");
            RrLog.Info("RecCenter", "doors: " + string.Join(", ", summary.ToArray()) + ", DORM ROOM");
        }

        // ------------------------------------------------------------------ prefabs

        private IEnumerator SpawnAll()
        {
            var items = L.Items();
            if (RealBuilding)
            {
                items = ForCachedBuilding(items);
            }

            var assets = RrAssets.Instance;
            int total = items.Count;
            if (assets == null)
            {
                RrLog.Warn("RecCenter", "no RrAssets: the hub has no Rec Room props");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 90f;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                _pending++;
                assets.LoadPrefab(item.Prefab, (go, data) => OnLoaded(item, go, data));
                if (i % 4 == 3)
                {
                    BuildProgress = 0.45f + 0.5f * (i - _pending) / total;
                    yield return null;
                }

                while (_pending > 12 && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            while (_pending > 0 && Time.realtimeSinceStartup < deadline)
            {
                BuildProgress = 0.45f + 0.5f * (total - _pending) / total;
                yield return null;
            }

            if (_pending > 0)
            {
                RrLog.Warn("RecCenter", _pending + " prefab(s) still loading after 90 s; the hub opens without them");
            }
        }

        private void OnLoaded(RrRcItem item, GameObject prefab, Rr.RrPrefabData data)
        {
            _pending--;
            if (this == null || _props == null)
            {
                return;
            }

            if (prefab == null)
            {
                Failed++;
                RrTools.Once("RecCenter", "missing " + item.Prefab, "prefab " + item.Prefab + " not found in the install; skipped");
                return;
            }

            RrSpawnOptions options;
            switch (item.Kind)
            {
                case RrRcKind.Visual:
                    options = RrSpawnOptions.Visual();
                    break;
                case RrRcKind.Tool:
                case RrRcKind.NetTool:
                    options = RrSpawnOptions.Prop();
                    break;
                default:
                    options = RrSpawnOptions.Scenery();
                    break;
            }

            var yaw = Quaternion.Euler(0f, item.Yaw, 0f);
            var rot = item.KeepRootRotation ? yaw * prefab.transform.localRotation : yaw;
            RrInstance inst;
            try
            {
                inst = RrSpawn.SpawnLoaded(prefab, data, _props, item.Pos, rot, options);
            }
            catch (Exception e)
            {
                Failed++;
                RrLog.Error("RecCenter", "spawning " + item.Prefab + " failed", e);
                return;
            }

            if (inst == null)
            {
                Failed++;
                return;
            }

            Spawned++;
            try
            {
                Place(inst, item);
                if (_fit != null)
                {
                    Fit(inst, item);
                }

                Wire(inst, item);
            }
            catch (Exception e)
            {
                RrLog.Error("RecCenter", "setting up " + item.Prefab + " (" + item.Role + ") failed", e);
            }
        }

        /// <summary>Grounds the prefab from its renderer bounds (pivots are often at the mesh centre, rr_reccenter.md §2.3).</summary>
        private void Place(RrInstance inst, RrRcItem item)
        {
            if (item.Place == RrRcPlace.Pivot)
            {
                return;
            }

            var b = inst.WorldBounds();
            if (b.size.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var target = transform.TransformPoint(item.Pos);
            if (item.Place == RrRcPlace.Snap)
            {
                inst.transform.position += Vector3.up * (target.y - b.min.y + (item.Kind == RrRcKind.Scenery ? 0f : 0.004f));
            }
            else
            {
                inst.transform.position += target - b.center;
            }
        }

        // ------------------------------------------------------------------ the cached building (iteration 6)

        private readonly List<GameObject> _hideTheirs = new List<GameObject>();
        private readonly Dictionary<string, Vector3> _centreOn = new Dictionary<string, Vector3>();

        /// <summary>
        /// With Rec Room's own building: our furniture copies go (<see cref="RrRecCenterLayout.KeepWithCachedBuilding"/>),
        /// and the vending machine and the Friend-O-Tron take the places of the building's two static vending machines
        /// (the gym's and the one between the curved windows; theirs are hidden, ours work).
        /// </summary>
        private List<RrRcItem> ForCachedBuilding(List<RrRcItem> items)
        {
            var kept = new List<RrRcItem>(items.Count);
            var used = new HashSet<Transform>();
            foreach (var it in items)
            {
                if (!L.KeepWithCachedBuilding(it))
                {
                    // The hallway mirror is no furniture copy: the building's mirror is the theatre's (BuildTheatreMirror).
                    if (it.Role != "mirror")
                    {
                        SkippedDuplicates++;
                    }

                    continue;
                }

                var item = L.AdjustForCachedBuilding(it);
                if (item.Role == "vending" || item.Role == "friendotron")
                {
                    Vector3 c;
                    var theirs = _fit.Nearest("[VendingMachine_Static]", item.Pos, 2.5f, used, out c);
                    if (theirs != null)
                    {
                        used.Add(theirs);
                        float yaw = _fit.Yaw(theirs);
                        RrLog.Info("RecCenter", "our " + item.Role + " takes the place of the building's " + theirs.name + " at (" + c.x.ToString("0.00") + ", "
                            + c.z.ToString("0.00") + ") yaw " + yaw.ToString("0") + " (planned (" + item.Pos.x.ToString("0.00") + ", " + item.Pos.z.ToString("0.00")
                            + ") yaw " + item.Yaw.ToString("0") + ")");
                        item.Pos = new Vector3(c.x, item.Pos.y, c.z);
                        item.Yaw = yaw;
                        _centreOn[item.Role] = item.Pos;
                        _hideTheirs.Add(theirs.gameObject);
                    }
                }

                kept.Add(item);
            }

            foreach (var go in _hideTheirs)
            {
                go.SetActive(false);
                HiddenOfTheirs++;
            }

            RrLog.Info("RecCenter", "with Rec Room's building: " + kept.Count + " of " + items.Count + " props kept (" + SkippedDuplicates
                + " furniture copies left out: the building has its own couches, chairs, tables, stools, lamps, plants, lockers and punching bag)");
            return kept;
        }

        /// <summary>Stands one spawned prop on the real building (and centres the machines on the ones they replace).</summary>
        private void Fit(RrInstance inst, RrRcItem item)
        {
            Vector3 c;
            if (item.Role != null && _centreOn.TryGetValue(item.Role, out c))
            {
                var b = inst.WorldBounds();
                var bc = transform.InverseTransformPoint(b.center);
                var move = new Vector3(c.x - bc.x, 0f, c.z - bc.z);
                if (move.sqrMagnitude > 1e-4f)
                {
                    inst.transform.position += transform.TransformVector(move);
                    _fit.Moved(item.Role + " centred on theirs", move);
                }
            }

            if (item.Kind == RrRcKind.Tool || item.Kind == RrRcKind.NetTool)
            {
                // Loose props are fitted when everything stands (FitLoose): they may rest on our own props.
                _loose.Add(new LooseFit { Inst = inst, Item = item, Pos = inst.transform.position, Rot = inst.transform.rotation });
                return;
            }

            switch (item.Role)
            {
                case "hoop":
                    // The real gym is 0.5-0.7 m wider than the layout.
                    _fit.Wall(inst.transform, item.Role, 0.05f, item.Pos.y, 1.0f);
                    break;
                case "radio.lounge":
                case "radio.table":
                    // The radio stands on our own table: the two stay together as planned (the wing's upper floor is 3.09
                    // in the scene, the layout's 3.10).
                    break;
                default:
                    // Things on floors may move 0.4 m (the gym and the balcony stand higher than the layout says); things
                    // on counters 0.25 m (the cafe counter is 0.77, not 0.95), else they stay where they are.
                    bool onFloor = Mathf.Abs(item.Pos.y) < 0.05f || Mathf.Abs(item.Pos.y - L.BalconyY) < 0.05f || Mathf.Abs(item.Pos.y - L.TheatreFloor) < 0.05f;
                    _fit.Floor(inst, item, onFloor ? RrRecCenterFit.SceneryTolerance : RrRecCenterFit.CounterTolerance, false);
                    break;
            }
        }

        private struct LooseFit
        {
            public RrInstance Inst;
            public RrRcItem Item;
            public Vector3 Pos;
            public Quaternion Rot;
        }

        private readonly List<LooseFit> _loose = new List<LooseFit>();

        /// <summary>
        /// The loose props onto what is under them once the building and our scenery stand, lowest first (a plate lands
        /// on the plate below it). Each goes back to its planned pose first (a prop spawned early may have woken and
        /// fallen through a table that was not there yet), and the props not fitted yet are left out of the rays.
        /// </summary>
        private void FitLoose()
        {
            _loose.Sort((a, b) => a.Item.Pos.y.CompareTo(b.Item.Pos.y));
            var off = new List<List<Collider>>(_loose.Count);
            foreach (var l in _loose)
            {
                var list = new List<Collider>();
                if (l.Inst != null)
                {
                    foreach (var c in l.Inst.GetComponentsInChildren<Collider>())
                    {
                        if (c.enabled && !c.isTrigger)
                        {
                            c.enabled = false;
                            list.Add(c);
                        }
                    }
                }

                off.Add(list);
            }

            for (int i = 0; i < _loose.Count; i++)
            {
                var l = _loose[i];
                if (l.Inst == null)
                {
                    continue;
                }

                l.Inst.transform.SetPositionAndRotation(l.Pos, l.Rot);
                var body = l.Inst.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                Physics.SyncTransforms();
                _fit.Floor(l.Inst, l.Item, 0f, true);
                foreach (var c in off[i])
                {
                    if (c != null)
                    {
                        c.enabled = true;
                    }
                }

                Physics.SyncTransforms();
                if (body != null)
                {
                    body.Sleep();
                }
            }

            _loose.Clear();
        }


        /// <summary>
        /// Doors onto the real walls and floors; where the building has its own door sign (all but the Magic Door), ours
        /// is hidden so Rec Room's sign, at Rec Room's height, is the one people read.
        /// </summary>
        private void FitDoors()
        {
            var all = AllDoors();
            _signs.Clear();
            foreach (var t in all)
            {
                Vector3 c;
                var at = transform.InverseTransformPoint(t.position);
                var sign = _fit.Nearest("[Door_Sign]", at, 1.6f, null, out c);
                bool magic = t.GetComponent<RrCategoryDoorView>() != null && t.GetComponent<RrCategoryDoorView>().Magic;
                // The Magic Door stands on the entrance wall's painted face (no sign, no collider test: see the layout).
                _fit.Door(t, t.name, sign, !magic);
                if (sign != null)
                {
                    _signs[t] = sign;
                }
            }

            _fit.Log("the doors");
        }

        private readonly Dictionary<Transform, Transform> _signs = new Dictionary<Transform, Transform>();

        private List<Transform> AllDoors()
        {
            var all = new List<Transform>();
            foreach (var d in Doors)
            {
                if (d != null)
                {
                    all.Add(d.transform);
                }
            }

            if (DormDoor != null)
            {
                all.Add(DormDoor.transform);
            }

            return all;
        }

        /// <summary>
        /// Where the building has its own sign for a door (all but the Magic Door), our copy of the sign and our label go,
        /// so Rec Room's sign at Rec Room's height is the one people read. The doors' looks load in the background: this
        /// waits for them (3 s at most).
        /// </summary>
        private IEnumerator HideOurSigns()
        {
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until)
            {
                bool built = true;
                foreach (var d in Doors)
                {
                    var v = d != null ? d.GetComponent<RrCategoryDoorView>() : null;
                    if (v != null && !v.Built)
                    {
                        built = false;
                    }
                }

                if (built)
                {
                    break;
                }

                yield return null;
            }

            int hidden = 0;
            var names = new List<string>();
            foreach (var kv in _signs)
            {
                var t = kv.Key;
                if (t == null)
                {
                    continue;
                }

                bool any = false;
                foreach (var s in t.GetComponentsInChildren<Transform>(true))
                {
                    bool ourSign = s.name == "[Door_Sign]" || (s.name == "Label" && s.parent == t);
                    if (ourSign && s.gameObject.activeSelf)
                    {
                        s.gameObject.SetActive(false);
                        any = true;
                    }
                }

                if (any)
                {
                    hidden++;
                    names.Add(t.name + " under " + kv.Value.name);
                }
            }

            RrLog.Info("RecCenter", "door signs: " + hidden + " of ours hidden under Rec Room's own (" + string.Join(", ", names.ToArray()) + ")");
        }

        /// <summary>The building's own feed board (the rec.net feed is gone) gives way to ours with the Dreamport notes.</summary>
        private void HideCachedDuplicates()
        {
            Vector3 c;
            var board = _fit.Nearest("BulletinBoard", new Vector3(L.HallLeft, 1.4f, 6.4f), 4f, null, out c);
            if (board != null)
            {
                while (board.parent != null && board.parent != BuildingFrame && board.parent.name.StartsWith("BulletinBoard", StringComparison.Ordinal))
                {
                    board = board.parent;
                }

                board.gameObject.SetActive(false);
                HiddenOfTheirs++;
            }

            RrLog.Info("RecCenter", "the building's own objects hidden under ours: " + HiddenOfTheirs + " (" + _hideTheirs.Count + " static vending machines"
                + (board != null ? ", the feed board '" + board.name + "'" : "") + ")");
        }

        private void Wire(RrInstance inst, RrRcItem item)
        {
            if (item.Kind == RrRcKind.Tool || item.Kind == RrRcKind.NetTool)
            {
                var g = RrTools.MakeTool(inst);
                if (g != null)
                {
                    ToolCount++;
                }

                var home = inst.GetComponent<RrRespawnable>();
                if (home != null)
                {
                    home.SetHome();
                }

                if (item.Kind == RrRcKind.NetTool && item.NetId >= 0)
                {
                    var net = inst.gameObject.GetComponent<RrNetBody>() ?? inst.gameObject.AddComponent<RrNetBody>();
                    net.LocalPropId = item.NetId;
                    NetPropCount++;
                    if (g != null)
                    {
                        NetProps.Add(g);
                    }
                }

                var body = inst.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.Sleep();
                }
            }

            switch (item.Role)
            {
                case "bell":
                    BellTransform = inst.transform;
                    Button(inst, "Bell Button", 0.15f, RingBell, null);
                    break;
                case "radio.kiosk":
                    AddRadio(inst, IntRadioKiosk, "Kiosk Radio", RrRecCenterTracks.RadioTheme, 12f, 0.32f);
                    break;
                case "radio.cafe":
                    AddRadio(inst, IntRadioCafe, "Cafe Radio", RrRecCenterTracks.RadioFoodCourt, 9f, 0.3f);
                    break;
                case "radio.lounge":
                    AddRadio(inst, IntRadioLounge, "Lounge Radio", RrRecCenterTracks.RadioLoneShoe, 9f, 0.3f);
                    break;
                case "hoop":
                    RrTools.MakeTool(inst);
                    var hoop = inst.GetComponent<RrHoop>();
                    if (hoop != null)
                    {
                        Hoops.Add(hoop);
                    }

                    break;
                case "pingpong.table":
                    RrRefs.SetTag(inst.gameObject, RrRefs.Table);
                    break;
                case "pingpong.paddle":
                    RrRefs.SetTag(inst.gameObject, RrRefs.Paddle);
                    inst.gameObject.AddComponent<RrRcPaddle>();
                    break;
                case "pingpong.ball":
                    inst.gameObject.AddComponent<RrRcPingPongBall>();
                    break;
                case "vending":
                    _vending = inst;
                    Button(inst, "Vending Button", 0.3f, Vend, "Kiosk_ButtonPressedNext");
                    break;
                case "friendotron":
                    _friendotron = inst;
                    Button(inst, "Friendotron Button", 0.3f, FriendOTron, "Kiosk_ButtonPressedNext");
                    break;
                case "couch":
                    // `Couch_Minimalist_Brown_Instanced_Mat` is recoloured per instance in the Studio scene
                    // (RecRoomStudioInstanceRecolor); uncoloured it renders white (harness). The picture's lounge
                    // pieces are brown (GUESS shade).
                    Tint(inst, CouchBrown);
                    break;
                case "mirror":
                    // Rec Room's glass needs its own reflection script; the Dorm's planar mirror replaces it.
                    foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                    {
                        if (c.isTrigger)
                        {
                            Destroy(c);
                        }
                    }

                    Mirror = inst.gameObject.AddComponent<RrDormMirror>();
                    Mirror.Setup(inst);
                    break;
            }
        }

        /// <summary>
        /// Rec Room's cached building: a mirror in the theatre's empty dark-wood frame (<see cref="RrRecCenterLayout.TheatreMirrorPos"/>),
        /// where Rec Room's room data put its mirror. Placed directly, not fitted: an invisible collider fills the recess
        /// (face z 1.497, 11 cm in front of the brick), so a wall ray would stop short of the glass's place.
        /// </summary>
        private void BuildTheatreMirror()
        {
            TheatreMirror = RrMirror.Create(transform, "Theatre Mirror", L.TheatreMirrorPos, Quaternion.Euler(0f, L.TheatreMirrorYaw, 0f),
                L.TheatreMirrorWidth, L.TheatreMirrorHeight, L.TheatreMirrorRange);
            RrLog.Info("RecCenter", "theatre mirror " + L.TheatreMirrorWidth.ToString("0.000") + " x " + L.TheatreMirrorHeight.ToString("0.000") + " m at "
                + L.TheatreMirrorPos.ToString("F3") + " (" + (TheatreMirror.Available ? "Thad's planar mirror within " + L.TheatreMirrorRange.ToString("0.0") + " m"
                : "stylised glass only: no mirror shader") + "); the hallway mirror stays with the rebuild");
        }

        /// <summary>The brown of the Rec Center couches (instance colour, GUESS from the picture).</summary>
        public static readonly Color CouchBrown = new Color(0.55f, 0.34f, 0.21f);

        private static void Tint(RrInstance inst, Color c)
        {
            var block = new MaterialPropertyBlock();
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                block.SetColor("_InstanceColor", c);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// A click/poke box around a prop (layer 16, no physical contacts), so large props keep their own solid
        /// colliders and tiny ones (the 9 cm bell) are still easy to hit.
        /// </summary>
        private RrPressable Button(RrInstance inst, string name, float minSize, Action onPress, string sound)
        {
            var b = inst.WorldBounds();
            var go = new GameObject(name);
            go.transform.SetParent(_props, false);
            go.transform.position = b.center;
            go.transform.rotation = transform.rotation;
            var box = go.AddComponent<BoxCollider>();
            var size = transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Max(Mathf.Abs(size.x) + 0.04f, minSize), Mathf.Max(Mathf.Abs(size.y) + 0.04f, minSize), Mathf.Max(Mathf.Abs(size.z) + 0.04f, minSize));
            var p = RrPressable.Make(go, onPress);
            p.PressSound = sound;
            p.Cooldown = 0.25f;
            return p;
        }

        private void AddRadio(RrInstance inst, int slot, string name, int def, float maxDistance, float volume)
        {
            var radio = inst.gameObject.AddComponent<RrRecCenterRadio>();
            radio.Setup(slot, name, RrRecCenterTracks.RadioClips, RrRecCenterTracks.RadioTitles, def, maxDistance, volume);
            Radios.Add(radio);
            var button = Button(inst, name + " Button", 0.25f, radio.Press, "Kiosk_ButtonPressedNext");
            button.Cooldown = 0.4f;
        }

        // ------------------------------------------------------------------ board

        /// <summary>
        /// The community board on the blue wall under the balcony, filled with our own notes (Rec Room's board read
        /// the rec.net feed, which is gone). The board faces +X (into the hall), so the labels' +Z points -X (TMP reads from -Z).
        /// </summary>
        private void BuildBoard()
        {
            var face = new Vector3(L.HallLeft + 0.16f, 0f, 6.4f);
            var dark = new Color(0.2f, 0.2f, 0.22f);
            RrRcGeo.Label(_props, "Board Title", "COMMUNITY BOARD", face + new Vector3(0f, 2.15f, 0f), 270f, 0.16f, 2.6f, new Color(0.9f, 0.4f, 0.2f));
            var body = RrRcGeo.Label(_props, "Board Notes",
                "Pick a door to play:\nSPORTS & PVP - in the gym.  QUESTS - through the stage.\nCREATIVE CAMPUS and FEATURED ROOMS - upstairs.\n"
                + "Can't decide? The Magic Door picks for you.\n\nShoot hoops, play ping-pong, grab a coffee in the cafe,\nring the bell and change the radio.\n\nBe excellent to each other.",
                face + new Vector3(0f, 1.4f, 0f), 270f, 0.09f, 2.7f, dark, false);
            body.rectTransform.sizeDelta = new Vector2(2.7f, 1.3f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 0.6f;
            body.fontSizeMax = 2.2f;
        }

        // ------------------------------------------------------------------ interactions

        /// <summary>Press on the reception bell: everyone in the instance hears it (event 64).</summary>
        public void RingBell()
        {
            if (Time.time - _lastBell < BellCooldown)
            {
                return;
            }

            _lastBell = Time.time;
            RrNet.Send(EvBell, new byte[] { 1 });
        }

        private void OnBell(int sender, byte[] body)
        {
            BellRings++;
            var pos = BellTransform != null ? BellTransform.position : transform.TransformPoint(new Vector3(L.Kiosk.x, 1.1f, -2.62f));
            RrSound.Play("ReceptionBell", pos, 0.9f, 1f, 20f);
            if (BellRings == 1)
            {
                RrLog.Info("RecCenter", "reception bell rung by player " + sender);
            }
        }

        /// <summary>The front of a machine-sized prop: its root's +Z (flattened), the prefabs' front (rr_reccenter.md §3.4 H).</summary>
        private static Vector3 Front(RrInstance inst)
        {
            var f = inst.transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        /// <summary>
        /// The vending machine gives a water bottle (Rec Room's ToolPool held 5): working hum, the drop, and a
        /// drinkable `(WaterBottle)` at the tray. Local-only, like Rec Room's own consumables; the oldest of 5 goes.
        /// </summary>
        public void Vend()
        {
            if (_vending == null || Time.time - _lastVend < 2f)
            {
                return;
            }

            _lastVend = Time.time;
            StartCoroutine(VendRoutine());
        }

        private IEnumerator VendRoutine()
        {
            var vb = _vending.WorldBounds();
            var fwd = Front(_vending);
            float half = Mathf.Abs(Vector3.Dot(vb.extents, new Vector3(Mathf.Abs(fwd.x), 0f, Mathf.Abs(fwd.z))));
            var front = new Vector3(vb.center.x, vb.min.y + 0.55f, vb.center.z) + fwd * (half + 0.2f);
            RrSound.Play("VendingMachine_working", front, 0.7f, 1f, 12f);
            yield return new WaitForSeconds(1.2f);
            RrSound.Play("VendingMachine_drop", front, 0.8f, 1f, 12f);
            var yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;
            RrSpawn.Spawn("(WaterBottle)", _props, transform.InverseTransformPoint(front), Quaternion.Euler(0f, yaw, 0f), inst =>
            {
                if (inst == null || this == null)
                {
                    return;
                }

                RrTools.MakeTool(inst, true);
                _bottles.Add(inst.gameObject);
                BottlesDispensed++;
                while (_bottles.Count > 5)
                {
                    if (_bottles[0] != null)
                    {
                        Destroy(_bottles[0]);
                    }

                    _bottles.RemoveAt(0);
                }
            }, RrSpawnOptions.Prop());
        }

        /// <summary>The Friend-O-Tron: shows who is in this Rec Center right now, above its front.</summary>
        public void FriendOTron()
        {
            if (_friendotron == null)
            {
                return;
            }

            if (_friendotronText == null)
            {
                var fb = _friendotron.WorldBounds();
                var fwd = Front(_friendotron);
                var p = new Vector3(fb.center.x, fb.max.y + 0.45f, fb.center.z) + fwd * 0.2f;
                // The label reads from its -Z: its +Z points away from the reader, i.e. against the machine's front.
                var yaw = Quaternion.LookRotation(-fwd, Vector3.up).eulerAngles.y - transform.eulerAngles.y;
                _friendotronText = RrRcGeo.Label(_props, "Friendotron Text", "", transform.InverseTransformPoint(p), yaw, 0.14f, 2.2f, new Color(0.25f, 0.9f, 0.5f));
                _friendotronText.rectTransform.sizeDelta = new Vector2(2.2f, 1.2f);
                _friendotronText.textWrappingMode = TextWrappingModes.Normal;
            }

            var names = new List<string> { RrPlayer.Nick + " (you)" };
            var members = RrNet.MembersOfMyInstance();
            for (int i = 0; i < members.Count && names.Count < 8; i++)
            {
                if (members[i] == RrNet.LocalId)
                {
                    continue;
                }

                var r = RrRemotePlayers.Get(members[i]);
                names.Add(r != null && !string.IsNullOrEmpty(r.Nick) ? r.Nick : "Player " + members[i]);
            }

            _friendotronText.text = "FRIEND-O-TRON\n" + names.Count + " here: " + string.Join(", ", names.ToArray());
            _friendotronText.gameObject.SetActive(true);
            _friendotronUntil = Time.time + 8f;
        }

        // ------------------------------------------------------------------ net

        /// <summary>The authority claims the custom ints for the Rec Center once (radios on their defaults).</summary>
        private void InitInts()
        {
            var s = RrNet.Instance;
            if (!s.CanWrite || s.Get(IntMagic) == Magic)
            {
                return;
            }

            for (int i = 0; i < RrInstanceState.CustomCount; i++)
            {
                s.Set(i, 0);
            }

            s.Set(IntMagic, Magic);
            RrLog.Info("RecCenter", "instance ints initialised (authority " + RrNet.LocalId + ")");
        }

        private void Hook(bool on)
        {
            if (on == _hooked)
            {
                return;
            }

            _hooked = on;
            if (on)
            {
                RrNet.On(EvBell, OnBell);
                RrNet.On(EvRadio, RrRecCenterRadio.OnRequest);
            }
            else
            {
                RrNet.Off(EvBell, OnBell);
                RrNet.Off(EvRadio, RrRecCenterRadio.OnRequest);
            }
        }

        /// <summary>A looping ambient source; spatial 0 = 2D (the whole room), minDistance &gt; 0 = a linear roll-off from there.</summary>
        private void Ambient(string clip, Vector3 local, float volume, float maxDistance, float spatial = 1f, float minDistance = 0f)
        {
            var anchor = new GameObject("Ambience " + clip);
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = local;
            var src = RrSound.Loop(clip, anchor.transform, volume, maxDistance);
            if (src != null)
            {
                src.spatialBlend = Mathf.Clamp01(spatial);
                if (minDistance > 0f)
                {
                    src.minDistance = Mathf.Min(minDistance, maxDistance - 0.5f);
                    src.rolloffMode = AudioRolloffMode.Linear;
                }

                _ambience.Add(src);
            }
        }

        private void StopAmbience()
        {
            for (int i = 0; i < _ambience.Count; i++)
            {
                if (_ambience[i] != null)
                {
                    RrSound.Stop(_ambience[i]);
                }
            }

            _ambience.Clear();
        }
    }
}
