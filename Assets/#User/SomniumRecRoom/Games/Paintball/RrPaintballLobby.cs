using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using B = SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.RrPaintballBuilder;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Our controls in Rec Room's 2025 Paintball lobby (level113, paintball_lobby.unity; the level itself is built by
    /// <see cref="RrPaintballLevel"/>): each goes where Rec Room had its equivalent, measured from the level
    /// (DevNotes/apk/paintball.md, "Lobby anchors"):
    /// - START: Rec Room's own START button on its pedestal by the tyre stacks at the Practice Arena gate (scene
    ///   (-44.38, -4.08, 64.29)); the level's button mesh stays, our press volume sits on it;
    /// - the match console (the seven maps, Capture the Flag / Team Battle, Blue / Red) beside it, facing the plaza;
    /// - the scoreboard on the lobby's leaderboard projector (scene (-46.41, -5.67, 60.10));
    /// - the Rec Center door in the level's ActivityDoorSingle frame (scene (-52.94, -5.38, 57.28));
    /// - the "Try 'em out!" test wall: each weapon hung on it can be taken to practise (the level's pickup volumes);
    /// - the shooting gallery: its START button (scene (-67.62, -4.27, 65.94)) runs a 30 s round on the gallery's
    ///   standees and bullseyes, NOW / BEST on the counter, with the gallery pistol on the counter;
    /// - the radio (the level's own (Radio) on the picnic table) plays the chosen map's lobby station.
    /// Room-local = scene - <see cref="RrPaintballMaps.LobbyAnchor"/> (the pregame spawns' centre is the origin).
    /// </summary>
    internal static class RrPaintballLobby
    {
        /// <summary>The level's own exit door meshes: our RrPortal (Rec Room's activity door) stands in their frame.</summary>
        public static readonly HashSet<string> SkipNames = new HashSet<string> { "ActivityDoorSingle_Frame", "ActivityDoor_Emit", "ActivityDoor_Glow" };

        public static readonly Vector3 StartScene = new Vector3(-44.382f, -4.0831f, 64.2874f);
        public static readonly Vector3 GalleryStartScene = new Vector3(-67.6182f, -4.2702f, 65.9424f);
        public static readonly Vector3 DoorScene = new Vector3(-52.9409f, -5.3755f, 57.2845f);
        public static readonly Vector3 LeaderboardScene = new Vector3(-46.407f, -5.674f, 60.102f);

        /// <summary>
        /// The level's second projector, [LeaderboardProjector] Paintball_Lobby_Variant (inactive in the 2026 build) on
        /// the plaza 6 m east of the locker building's plaza door, its +Z (yaw -90.3) facing that door: Rec Room's
        /// game-start hologram (the players, teams, map, mode and START) stands here.
        /// </summary>
        public static readonly Vector3 HologramScene = new Vector3(-46.25f, -5.124f, 71.15f);

        public static readonly Quaternion HologramRot = new Quaternion(0f, 0.7088f, 0f, -0.7054f);
        public static readonly Vector3 GalleryGunScene = new Vector3(-67.712f, -4.385f, 66.5053f);
        public static readonly Quaternion GalleryGunRot = new Quaternion(0.1538f, 0.1814f, 0.6283f, 0.7407f);

        /// <summary>The test wall's pickup volumes (scene centre, size, prefab).</summary>
        private static readonly Vector3[] WallAt =
        {
            new Vector3(-66.1659f, -3.6226f, 72.7109f), new Vector3(-66.1525f, -4.2506f, 72.6684f), new Vector3(-66.1625f, -4.2149f, 70.7342f),
            new Vector3(-66.1314f, -3.0253f, 72.5954f), new Vector3(-66.0637f, -3.6675f, 70.749f), new Vector3(-66.0608f, -3.755f, 71.6895f),
            new Vector3(-66.0729f, -3.0246f, 70.7916f)
        };

        private static readonly Vector3[] WallSize =
        {
            new Vector3(0.31f, 0.30f, 0.47f), new Vector3(0.39f, 0.46f, 0.97f), new Vector3(0.28f, 0.31f, 0.85f), new Vector3(0.42f, 0.39f, 1.31f),
            new Vector3(0.43f, 0.43f, 0.74f), new Vector3(0.36f, 0.77f, 0.73f), new Vector3(0.51f, 0.48f, 1.09f)
        };

        private static readonly string[] WallPrefab =
        {
            RrPaintballRoom.Pistol, RrPaintballRoom.Rifle, RrPaintballRoom.Shotgun, RrPaintballRoom.Sniper, RrPaintballRoom.Launcher,
            RrPaintballRoom.Shield, RrPaintballRoom.Thrower
        };

        /// <summary>The gallery's targets: its three standees and six bullseyes (scene).</summary>
        public static readonly Vector3[] GalleryTargets =
        {
            new Vector3(-69.983f, -5.3f, 67.438f), new Vector3(-69.998f, -5.3f, 63.453f), new Vector3(-71.5802f, -5.3333f, 65.8193f),
            new Vector3(-78.197f, -1.927f, 64.52f), new Vector3(-78.197f, -1.927f, 66.396f), new Vector3(-78.26f, -3.653f, 65.6f),
            new Vector3(-78.17f, -3.667f, 63.62f), new Vector3(-73.776f, -2.9326f, 67.287f), new Vector3(-71.24f, -3.8779f, 64.221f)
        };

        public static Vector3 Local(Vector3 scene)
        {
            return scene - RrPaintballMaps.LobbyAnchor;
        }

        public static void Build(B b, RrPaintballRoom room, RrPaintballLevel level)
        {
            var root = RrBuild.Group(room.transform, "Lobby").transform;

            // START on Rec Room's own button (the level draws the button; our press volume covers it).
            var start = new GameObject("START");
            start.transform.SetParent(root, false);
            start.transform.localPosition = Local(StartScene) + new Vector3(0f, 0.05f, 0f);
            start.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.35f, 0.5f);
            RrPressable.Make(start, room.PressStart);
            // The level's own text on this button is "Challenge" (its [Paintball_DuelController]/StartButton TMP): the
            // Practice Arena duel's button. The 1 v 1 duel is not built, so it starts the match like the hologram's START.
            // One label, read from the plaza (TMP draws both sides: a second, back-to-back label garbles both).
            RrBuild.Sign(root, "START Label", "CHALLENGE", Local(StartScene) + new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 135f, 0f), 1.8f, RrLook.Yellow, true, 1.6f);

            // Beside the Practice Arena gate (not in it), facing the plaza and the picnic tables.
            Console(root, room, new Vector3(13.2f, 0.03f, -13.8f), 135f);

            // Rec Room's game-start hologram on the level's second projector: who is in the lobby, their teams, the
            // map and mode (both switchable on it, as on the board) and START (2 players).
            Hologram(root, room);

            // The level's lettering, the lounge rug and the purple armchair.
            RrPaintballSigns.Build(root, level);

            // The scoreboard on the lobby's leaderboard projector, facing the way the projector does (yaw -42).
            var lb = Local(LeaderboardScene);
            RrScoreboard.Create(root, lb + new Vector3(0f, 2.6f, 0f), Quaternion.Euler(0f, -42f + 180f, 0f), "LAST MATCH", 2, 3f);

            // The Rec Center door in the level's own exit-door frame (its +z points into the building).
            RrPortal.Create(root, RrActivity.RecCenter, Local(DoorScene), 0f, "REC CENTER");

            // The "Try 'em out!" wall: take any weapon hanging on it to practise.
            for (int i = 0; i < WallAt.Length; i++)
            {
                var rack = RrPaintballRack.CreateSpot(room, b, root, Local(WallAt[i]), Quaternion.identity, WallPrefab[i], false, WallSize[i]);
                rack.name = "Try Out " + RrPaintballRoom.GunName(WallPrefab[i]);
            }

            // The shooting gallery: its pistol on the counter, its START and the NOW / BEST sign.
            RrPaintballRack.CreateSpot(room, b, root, Local(GalleryGunScene), GalleryGunRot, RrPaintballRoom.Pistol, false);
            RrPaintballGallery.Create(room, root);
            RrLog.Info("Paintball", "lobby controls: START at " + Local(StartScene).ToString("F1") + ", console, scoreboard, Rec Center door at " + Local(DoorScene).ToString("F1") +
                                    ", " + WallAt.Length + " try-out weapons, shooting gallery at " + Local(GalleryStartScene).ToString("F1") + " (" + GalleryTargets.Length + " targets)");
        }

        /// <summary>
        /// The match board: a standing board (players on its -z side, where the signs face) with the next match on
        /// top, the seven maps in a row and the mode and team buttons below; the chosen map and mode light yellow.
        /// </summary>
        private static void Console(Transform root, RrPaintballRoom room, Vector3 at, float yaw)
        {
            var g = RrBuild.Group(root, "Console", at, Quaternion.Euler(0f, yaw, 0f)).transform;
            B.Box(g, "Board", new Vector3(0f, 1.6f, 0.06f), new Vector3(4.8f, 2.0f, 0.1f), RrLook.DarkGrey);
            B.Box(g, "Board Trim", new Vector3(0f, 2.63f, 0.06f), new Vector3(4.9f, 0.08f, 0.14f), RrLook.Orange, false);
            B.Box(g, "Post L", new Vector3(-2.3f, 1.3f, 0.14f), new Vector3(0.12f, 2.6f, 0.12f), B.DarkPlank);
            B.Box(g, "Post R", new Vector3(2.3f, 1.3f, 0.14f), new Vector3(0.12f, 2.6f, 0.12f), B.DarkPlank);
            RrBuild.Sign(g, "Title", "PAINTBALL", new Vector3(0f, 2.42f, -0.01f), Quaternion.identity, 3.2f, RrLook.Orange, true, 4.5f);
            room.LobbyNext = RrBuild.Sign(g, "Next", "", new Vector3(0f, 2.18f, -0.01f), Quaternion.identity, 2.0f, Color.white, true, 4.6f);
            room.LobbyStatus = RrBuild.Sign(g, "Status", "", new Vector3(0f, 1.96f, -0.01f), Quaternion.identity, 1.8f, new Color(1f, 0.9f, 0.4f), true, 4.6f);
            for (int i = 0; i < RrPaintballMaps.Count; i++)
            {
                int map = i;
                float x = -1.98f + i * 0.66f;
                room.MapLabels.Add(Button(g, RrPaintballMaps.Short[i], new Vector3(x, 1.68f, -0.03f), MapColor(i), () => room.VoteMap(map), 0.22f, 0.66f));
            }

            room.ModeLabels.Add(Button(g, "CAPTURE THE FLAG", new Vector3(-1.6f, 1.1f, -0.03f), RrLook.Yellow, () => room.VoteMode(RrPaintballRoom.ModeCtf), 0.26f, 1.3f));
            room.ModeLabels.Add(Button(g, "TEAM BATTLE", new Vector3(-0.45f, 1.1f, -0.03f), RrLook.Purple, () => room.VoteMode(RrPaintballRoom.ModeTeamBattle), 0.26f, 1.1f));
            Button(g, "JOIN BLUE", new Vector3(0.75f, 1.1f, -0.03f), B.Blue, () => room.JoinTeam(0), 0.26f, 1f);
            Button(g, "JOIN RED", new Vector3(1.8f, 1.1f, -0.03f), B.Red, () => room.JoinTeam(1), 0.26f, 1f);
            RrBuild.Sign(g, "Start Hint", "START: ON THE HOLOGRAM BY THE LOCKER ROOM DOOR", new Vector3(0f, 0.72f, -0.01f), Quaternion.identity, 1.3f, RrLook.Yellow, true, 4.4f);
            RrBuild.PointLight(g, "Console Light", new Vector3(0f, 2.8f, -1.4f), new Color(1f, 0.92f, 0.8f), 7f, 0.9f);
        }

        /// <summary>The console hologram (Games/Shared/RrGameConsole, the install's (ScoreboardProjector)) at the level's second projector.</summary>
        private static void Hologram(Transform root, RrPaintballRoom room)
        {
            var modes = new[] { RrPaintballRoom.ModeName(RrPaintballRoom.ModeCtf).ToUpperInvariant(), RrPaintballRoom.ModeName(RrPaintballRoom.ModeTeamBattle).ToUpperInvariant() };
            var maps = new string[RrPaintballMaps.Count];
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i] = RrPaintballMaps.Names[i].ToUpperInvariant();
            }

            var spec = new RrGameConsoleSpec
            {
                Title = "PAINTBALL",
                MinPlayers = RrPaintballRoom.MinPlayers,
                TeamCount = 2,
                Modes = modes,
                ModeIndex = () => room.Mode,
                OnMode = m => room.VoteMode(m),
                Maps = maps,
                MapIndex = () => room.MapIndex,
                OnMap = m => room.VoteMap(m),
                OnStart = id => room.PressStart()
            };
            room.Hologram = RrGameConsole.ForGame(room, root, new Pose(Local(HologramScene), HologramRot), spec);
            RrLog.Info("Paintball", "game-start hologram at the level's second projector " + Local(HologramScene).ToString("F1") + " (scene " + HologramScene.ToString("F2") +
                                    "), facing the locker building's plaza door; START needs " + RrPaintballRoom.MinPlayers + " players");
        }


        private static Color MapColor(int i)
        {
            switch (i)
            {
                case RrPaintballMaps.River: return RrLook.Green;
                case RrPaintballMaps.Homestead: return RrLook.Wood;
                case RrPaintballMaps.Quarry: return RrLook.Grey;
                case RrPaintballMaps.Clearcut: return new Color(0.25f, 0.5f, 0.3f);
                case RrPaintballMaps.Spillway: return RrLook.Blue;
                case RrPaintballMaps.DriveIn: return RrLook.Orange;
                default: return RrLook.Pink;
            }
        }

        /// <summary>A round button on the board facing the players, its label just under it.</summary>
        private static TMPro.TextMeshPro Button(Transform g, string label, Vector3 pos, Color cap, System.Action onPressed, float size, float width)
        {
            RrPressable.Create(g, "Button " + label, pos, Quaternion.Euler(-90f, 0f, 0f), cap, size, onPressed);
            return RrBuild.Sign(g, "Label " + label, label, pos + new Vector3(0f, -0.2f, 0.02f), Quaternion.identity, 1f, Color.white, true, width);
        }

    }
}
