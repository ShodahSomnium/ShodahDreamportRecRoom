using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// Stand-ins for the two social rooms, so travel works before the Places modules exist and as the last
    /// fallback of a failed trip (<see cref="RrTravel"/>): a plain dorm with a door to the Rec Center, and a Rec
    /// Center hall with a door for every activity at the places the Rec Center plan gives them
    /// (rr_reccenter.md §3.3; doors of activities nobody has registered stay locked). They are registered by
    /// <see cref="RrRegistry"/>'s RegisterInfrastructure only while the real room is missing.
    /// </summary>
    public sealed class RrPlaceholderRoom : RrRoom
    {
        /// <summary>Dorm: inner size 7 x 7 m, door on the south wall.</summary>
        public const float DormHalf = 3.5f;

        /// <summary>Rec Center hall: inner x -20..20, z -18..18 (the plan's hall; the lobby is left out).</summary>
        public const float HallHalfX = 20f;
        public const float HallHalfZ = 18f;
        public const float WallHeight = 3f;
        public const float HallHeight = 8f;

        private static RrRoomInfo _dorm;
        private static RrRoomInfo _recCenter;

        /// <summary>A door of the Rec Center plan: target, door root (x, z) and yaw (+Z into the hall).</summary>
        private struct Door
        {
            public RrActivity Target;
            public float X;
            public float Z;
            public float Yaw;
            public string Sign;

            public Door(RrActivity target, float x, float z, float yaw, string sign)
            {
                Target = target;
                X = x;
                Z = z;
                Yaw = yaw;
                Sign = sign;
            }
        }

        /// <summary>rr_reccenter.md §3.3 (the Dorm door moves from the lobby to the hall's south wall).</summary>
        private static readonly Door[] HallDoors =
        {
            new Door(RrActivity.Paintball, -16f, 17.94f, 180f, "PAINTBALL"),
            new Door(RrActivity.LaserTag, -12f, 17.94f, 180f, "LASER TAG"),
            new Door(RrActivity.RecRoyale, -8f, 17.94f, 180f, "REC ROYALE"),
            new Door(RrActivity.Showdown, -4f, 17.94f, 180f, "SHOWDOWN"),
            new Door(RrActivity.Dodgeball, 0f, 17.94f, 180f, "DODGEBALL"),
            new Door(RrActivity.Paddleball, 4f, 17.94f, 180f, "PADDLEBALL"),
            new Door(RrActivity.Soccer, 8f, 17.94f, 180f, "SOCCER"),
            new Door(RrActivity.DiscGolf, 12f, 17.94f, 180f, "DISC GOLF"),
            new Door(RrActivity.Bowling, 16f, 17.94f, 180f, "BOWLING"),
            new Door(RrActivity.GoldenTrophy, 19.94f, -2f, 270f, "GOLDEN TROPHY"),
            new Door(RrActivity.CrimsonCauldron, 19.94f, 2f, 270f, "CRIMSON CAULDRON"),
            new Door(RrActivity.Jumbotron, 19.94f, 6f, 270f, "RISE OF JUMBOTRON"),
            new Door(RrActivity.LostSkulls, 19.94f, 10f, 270f, "ISLE OF LOST SKULLS"),
            new Door(RrActivity.Crescendo, 19.94f, 14f, 270f, "CRESCENDO OF THE BLOOD MOON"),
            new Door(RrActivity.Charades, -19.94f, 14f, 90f, "3D CHARADES"),
            new Door(RrActivity.Midnight, -19.94f, 10f, 90f, "MAKE IT TO MIDNIGHT"),
            new Door(RrActivity.LittleMonsters, -19.94f, 6f, 90f, "MY LITTLE MONSTERS"),
            new Door(RrActivity.PunchUp, -19.94f, 2f, 90f, "PUNCH UP"),
            new Door(RrActivity.StuntRunner, -19.94f, -2f, 90f, "STUNT RUNNER"),
            new Door(RrActivity.RecRally, -19.94f, -6f, 90f, "REC RALLY"),
            new Door(RrActivity.Dorm, 0f, -17.94f, 0f, "DORM")
        };

        private readonly List<RrPortal> _doors = new List<RrPortal>();

        /// <summary>The placeholder's registry entry (Dorm or RecCenter; null for anything else). Always the same object.</summary>
        public static RrRoomInfo InfoFor(RrActivity activity)
        {
            if (activity == RrActivity.Dorm)
            {
                if (_dorm == null)
                {
                    _dorm = new RrRoomInfo
                    {
                        Activity = RrActivity.Dorm,
                        Title = "Dorm Room",
                        Subtitle = "Your own room",
                        Description = "Your private room. (Placeholder: the real dorm is not built yet.)",
                        MinPlayers = 1,
                        MaxPlayers = 1,
                        CoverSprite = "illustration_HomeButton_DormRoom",
                        Category = "Social",
                        Networked = false,
                        Create = root => root.AddComponent<RrPlaceholderRoom>()
                    };
                }

                return _dorm;
            }

            if (activity == RrActivity.RecCenter)
            {
                if (_recCenter == null)
                {
                    _recCenter = new RrRoomInfo
                    {
                        Activity = RrActivity.RecCenter,
                        Title = "Rec Center",
                        Subtitle = "Meet up between games",
                        Description = "The hub with a door to every activity. (Placeholder: the real Rec Center is not built yet.)",
                        MinPlayers = 1,
                        MaxPlayers = 40,
                        CoverSprite = "illustration_HomeButton_RecCenter",
                        Category = "Social",
                        Networked = true,
                        Create = root => root.AddComponent<RrPlaceholderRoom>()
                    };
                }

                return _recCenter;
            }

            return null;
        }

        /// <summary>The Rec Center placeholder is shared: people there are "Playing" (hanging out), as in a social room.</summary>
        public override RrStatus ArrivalStatus
        {
            get { return RrStatus.Playing; }
        }

        public override IEnumerator Build()
        {
            BuildProgress = 0f;
            var portalLoad = RrPortal.Prepare();
            while (portalLoad.MoveNext())
            {
                BuildProgress = 0.1f;
                yield return portalLoad.Current;
            }

            BuildProgress = 0.4f;
            if (Activity == RrActivity.Dorm)
            {
                BuildDorm();
            }
            else
            {
                yield return BuildHall();
            }

            BuildProgress = 1f;
            RrLog.Info("Rooms", "placeholder " + Activity + " built at (" + Origin.x.ToString("0") + ", " + Origin.z.ToString("0") + ") with " + _doors.Count + " door(s)" + (RrPortal.TemplateReady ? "" : " (procedural doors)"));
        }

        public override Pose Spawn(int index)
        {
            // Arriving from an activity: in front of its door (§4.3), staggered for simultaneous arrivals.
            float lateral = (index % 3 - 1) * 0.6f;
            var from = Activity == RrActivity.RecCenter ? DoorTo(RrTravel.Previous) : null;
            if (from == null)
            {
                from = DoorTo(Activity == RrActivity.Dorm ? RrActivity.RecCenter : RrActivity.Dorm);
            }

            if (from != null)
            {
                var world = from.FrontPose(1.8f, lateral);
                return new Pose(transform.InverseTransformPoint(world.position) + Vector3.up * 0.05f, Quaternion.Inverse(transform.rotation) * world.rotation);
            }

            return new Pose(new Vector3(lateral, 0.05f, 0f), Quaternion.identity);
        }

        public override void OnLocalEnter()
        {
            var door = Activity == RrActivity.RecCenter ? DoorTo(RrTravel.Previous) : null;
            if (door != null)
            {
                door.PlayArrive();
            }
        }

        private RrPortal DoorTo(RrActivity target)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                if (_doors[i] != null && _doors[i].Target == target)
                {
                    return _doors[i];
                }
            }

            return null;
        }

        private void BuildDorm()
        {
            var t = transform;
            var floor = RrLook.Solid(RrLook.Wood);
            var walls = RrLook.Solid(RrLook.Cream);
            RrBuild.RoomShell(t, "Shell", Vector3.zero, new Vector3(DormHalf * 2f + 0.3f, WallHeight, DormHalf * 2f + 0.3f), floor, walls, null);
            RrBuild.Box(t, "Rug", new Vector3(0f, 0.005f, 0.5f), new Vector3(3f, 0.01f, 2.2f), RrLook.Solid(RrLook.Teal), false);
            RrBuild.Box(t, "Bed", new Vector3(-2.4f, 0.25f, 2f), new Vector3(1.1f, 0.5f, 2.2f), RrLook.Solid(RrLook.Blue));
            RrBuild.Sign(t, "Title", "DORM ROOM", new Vector3(0f, 2.3f, DormHalf - 0.02f), Quaternion.identity, 4f, RrLook.DarkGrey, true, 5f);
            RrBuild.Sign(t, "Note", "placeholder - the real dorm is not built yet", new Vector3(0f, 1.8f, DormHalf - 0.02f), Quaternion.identity, 1.6f, RrLook.Grey, false, 5f);
            RrBuild.PointLight(t, "Light", new Vector3(0f, 2.6f, 0f), new Color(1f, 0.93f, 0.82f), 9f, 1.2f);
            _doors.Add(RrPortal.Create(t, RrActivity.RecCenter, new Vector3(0f, 0f, -DormHalf + 0.06f), 0f, "REC CENTER"));
        }

        private IEnumerator BuildHall()
        {
            var t = transform;
            var floor = RrLook.Solid(new Color(0.78f, 0.62f, 0.44f));
            var walls = RrLook.Solid(RrLook.White);
            RrBuild.RoomShell(t, "Shell", Vector3.zero, new Vector3(HallHalfX * 2f + 0.3f, HallHeight, HallHalfZ * 2f + 0.3f), floor, walls, null);
            RrBuild.Box(t, "Kiosk", new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 1.1f, 2.4f), RrLook.Solid(RrLook.Orange));
            RrBuild.Sign(t, "Title", "REC CENTER", new Vector3(0f, 4.4f, -HallHalfZ + 0.02f), Quaternion.Euler(0f, 180f, 0f), 8f, RrLook.Orange, true, 16f);
            RrBuild.Sign(t, "Note", "placeholder - the real Rec Center is not built yet", new Vector3(0f, 3.4f, -HallHalfZ + 0.02f), Quaternion.Euler(0f, 180f, 0f), 2.4f, RrLook.Grey, false, 16f);
            BuildProgress = 0.5f;
            yield return null;
            for (int i = 0; i < HallDoors.Length; i++)
            {
                var d = HallDoors[i];
                _doors.Add(RrPortal.Create(t, d.Target, new Vector3(d.X, 0f, d.Z), d.Yaw, d.Sign));
                if (i % 7 == 6)
                {
                    BuildProgress = 0.5f + 0.5f * i / HallDoors.Length;
                    yield return null;
                }
            }
        }
    }
}
