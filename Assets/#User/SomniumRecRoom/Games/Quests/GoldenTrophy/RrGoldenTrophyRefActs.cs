using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy.Scenes;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using W = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestWave.When;
using G = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy.RrGoldenTrophyRoom;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// Quest for the Golden Trophy's eleven rooms as Rec Room had them at the shutdown, from its own levels in the Android
    /// build (2026-03-23; levels 19-29, build order = play order): the lobby Quest_Foyer (the 2020 theatre with the merch
    /// store, the 2025-12 RRO mod board and the start hologram), the dressing room Quest_Armory (the pre-start room: 4
    /// swords, 2 shields and 2 bows on the back wall, the mirror, the platform by the door), and the acts Quest_Hallway1
    /// (1), Hallway2 (2, the crossbow), Hallway3 (3, the boulder), DeadIsland (4, the lava room), Hallway4 (5, the split
    /// path), Library (6), Hallway5 (7, the maze), Battlefield (8, half lava) and Cafeteria (9, the Goblin King). The
    /// REF2 Studio ports of the lobby and acts 1-6 are the same levels in the same frame; they add their enemy spawn
    /// points and level effects. Acts 7-9 have no reference spawn points: their groups are sampled on the level's own
    /// floors inside the rooms named in each method.
    ///
    /// Waves follow the final walkthrough (research rr_latest_rgames_story.md 5.1) and are sized so the whole quest lands
    /// near Rec Room's "slightly over 180k" best (kill gold 450-5,500).
    /// </summary>
    public static class RrGoldenTrophyRefActs
    {
        // ------------------------------------------------------------------ helpers

        /// <summary>Enter-zone helper: scene-space box (x0..x1, z0..z1) around floor height y, area-local.</summary>
        private static RrQuestWave Zone(RrQuestWave w, RrGoldenTrophyArea a, float x0, float z0, float x1, float z1, float y)
        {
            return w.In(a.L((x0 + x1) * 0.5f, y + 1.5f, (z0 + z1) * 0.5f), new Vector3(Mathf.Abs(x1 - x0), 5f, Mathf.Abs(z1 - z0)));
        }

        /// <summary>A spawn group; an empty one falls back to the act's other spawners (never onto the team).</summary>
        private static void Group(RrQuestAct act, RrGoldenTrophyArea a, string name, Vector3[] points)
        {
            if (points.Length == 0)
            {
                var all = new List<Vector3>();
                foreach (var m in a.Spawners())
                {
                    all.Add(m.Position);
                }

                if (all.Count == 0)
                {
                    var exit = a.Extra("[DoorTrigger]");
                    all.Add(exit != null ? exit.Position + Vector3.up * 0.05f : act.Entry + Quaternion.Euler(0f, act.EntryYaw, 0f) * new Vector3(0f, 0f, 12f));
                }

                RrLog.Warn("Quest", a.Scene + ": spawn group \"" + name + "\" found no floor; using " + all.Count + " other point(s)");
                points = all.ToArray();
            }

            act.Group(name, points);
        }

        /// <summary>Spawners set down onto the floor below them (the REF2 markers stand 0.1-0.3 m up, some on props).</summary>
        private static Vector3[] Ground(RrGoldenTrophyArea a, Vector3[] local)
        {
            return a.Ground(local);
        }

        /// <summary>The common part of every act: entry, exit door, area, lava, pots, weapons, the in-act hologram.</summary>
        private static RrQuestAct Finish(G room, RrGoldenTrophyArea a, Transform r, string title, string subtitle, bool exitDoor = true)
        {
            var act = new RrQuestAct { Title = title, Subtitle = subtitle, Root = r, Origin = r.localPosition };
            Vector3 at;
            float yaw;
            if (a.Arrival(out at, out yaw))
            {
                act.Entry = at;
                act.EntryYaw = yaw;
            }

            act.Area = a.Area();
            if (exitDoor)
            {
                GameObject platform;
                act.ExitDoor = a.ExitDoor(r, out platform);
                room.AddGatherPlatform(act.ExitDoor, platform);
            }

            foreach (var lava in a.Lava())
            {
                act.Lava.Add(lava);
            }

            a.AddPots(room.Pots, r);
            a.AddWeapons(room, r);
            room.ActHologram(a, r, subtitle);
            return act;
        }

        // ------------------------------------------------------------------ lobby: Quest_Foyer

        /// <summary>
        /// The lobby, Rec Room's Quest_Foyer (level 19): a theatre. The audience floor is at -6.51; the eight player spawns
        /// (x 114.7-115.8) face the stage (-x); the stage (x 76-90) stands 1 m up behind the marquee ("Quest for the
        /// Golden Trophy", x 89.6); the merch store is upstairs at the north wall. Rec Room's pieces used for our
        /// controls: the start hologram [ScoreboardProjector] (1) at (108.35, 42.72) - our <see cref="RrGameConsole"/>
        /// with the party and START; the RRO mod board [QuestModInteractions] at (106.6, 48.9), facing the spawns (-150
        /// degrees); the practice weapons (2 swords, 2 bows) on their tables and the two practice standees; the exit
        /// [ActivityDoor] at x 118.9 - our Rec Center door.
        /// </summary>
        public static IEnumerator Lobby(G room, Transform t, List<Pose> spawns, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Foyer", new Vector3(115.25f, -6.5087f, 45.84f));
            yield return a.Build(hull, RrScene_GoldenTrophy_Foyer.Text(), t);
            spawns.Clear();
            foreach (var m in a.Markers("spawn"))
            {
                if (m.Name.StartsWith("Player") && m.Name.EndsWith("SpawnPoint"))
                {
                    spawns.Add(new Pose(m.Position + Vector3.up * 0.05f, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
                }
            }

            var holo = a.Markers("scoreboard");
            if (holo.Count > 0)
            {
                room.LobbyConsole(t, holo[0].Pose);
            }
            else
            {
                room.LobbyStart(t, a.L(108.35f, -6.51f, 42.72f), -90f);
            }

            room.ModBoard(t, a.L(106.58f, -6.5087f, 48.85f), -150f);
            RrBuild.Sign(t, "Marquee", "QUEST FOR THE GOLDEN TROPHY", a.L(89.2f, -0.18f, 46f), Quaternion.Euler(0f, -90f, 0f), 3.2f, new Color(1f, 0.82f, 0.36f), true, 7f);
            RrBuild.Sign(t, "Marquee Dept", "THEATER DEPT.", a.L(89.3f, 1.47f, 45.86f), Quaternion.Euler(0f, -90f, 0f), 2f, new Color(1f, 0.9f, 0.7f), true, 4.6f);
            foreach (var m in a.Markers("standee"))
            {
                string prefab = m.Get("prefab");
                if (!string.IsNullOrEmpty(prefab) && m.Get("active") != "0")
                {
                    RrSpawn.Spawn(prefab, t, m.Position, m.Rotation, null, RrSpawnOptions.Scenery());
                }
            }

            RrPortal.Create(t, RrActivity.RecCenter, a.L(118.6f, -6.5087f, 45.76f), -90f, "REC CENTER");
            a.AddPots(room.Pots, t);
            a.AddWeapons(room, t);
        }

        // ------------------------------------------------------------------ the dressing room: Quest_Armory

        /// <summary>
        /// The pre-start room, Rec Room's Quest_Armory (level 20; the final "dressing room"): 8.8 x 10.2 m, green and grey
        /// painted brick. The team arrives at the four PlayerSpawnPoints (x 27.9, facing the back wall, -x); the back wall
        /// (x 20.2) holds the Default Weapons on Rec Room's own hooks - 4 (QuestSword), 2 (QuestShield), 2 (Longbow) - and
        /// the door ([Quest_Door_Active], z 0.24) between them; the [DoorTrigger] platform is 2.5 m in front of it. The
        /// make-up table with its mirror stands on the left wall, a couch and stools opposite, 11 pots around.
        /// </summary>
        public static IEnumerator Armory(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Armory", new Vector3(27.906f, -1.6851f, 0.33f));
            yield return a.Build(hull, RrScene_GoldenTrophy_Armory.Text(), r);
            var act = Finish(room, a, r, "THE DRESSING ROOM", "The Dressing Room");
            act.Prelude = true;
            act.ArrivalToast = "Grab your weapons, then gather on the platform by the door!";
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 1: Quest_Hallway1

        /// <summary>
        /// Act 1, Quest_Hallway1: the locker hall at -0.79 (x 25-56, z 23-36), a corridor north that drops 3.5 m down a
        /// ramp (z 44-61) into the goblin room at -4.29 (x 15.6-38, z 61-68) whose west wall holds the exit (x 15.6, z
        /// 64.2). Final: sword goblins only, in small groups, several waves in the final room.
        /// </summary>
        public static IEnumerator Act1(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Hallway1", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Hallway1.Text(), r);
            var act = Finish(room, a, r, "ACT 1", "The Hallway");
            Group(act, a, "hall", Ground(a, a.SpawnersIn(30f, 25f, 45f, 38f)));
            Group(act, a, "ramp", Ground(a, a.SpawnersIn(22f, 45f, 36f, 58.5f)));
            Group(act, a, "room", Ground(a, a.SpawnersIn(15f, 59f, 38f, 69f)));
            act.Wave(W.ActStart).Add(G.Goblin, 2, "hall");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 2, "hall");
            Zone(act.Wave(W.Enter), a, 22.7f, 40f, 35.3f, 50f, -1.5f).Add(G.Goblin, 3, "ramp");
            Zone(act.Wave(W.Enter), a, 15.6f, 58f, 37.6f, 68.2f, -4.29f).Add(G.Goblin, 4, "room");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 4, "room");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 5, "room");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 2: Quest_Hallway2

        /// <summary>
        /// Act 2, Quest_Hallway2: the team starts in the lower hall at -6.26 (x 98-121, z -50..-44) with the side room to the
        /// north (x 90-97, z -50..-33: 4 large pots, a crossbow and a sword); a ramp climbs west into the big room at 0.2
        /// (x 40-90, z -70..-43) with its balconies (imps at 2.2-3.7) and the exit in the south wall (x 53.6, z -68.3).
        /// Final: imps arrive (arrows stun them); waves spawn behind you.
        /// </summary>
        public static IEnumerator Act2(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Hallway2", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Hallway2.Text(), r);
            var act = Finish(room, a, r, "ACT 2", "The Crossbow Hall");
            Group(act, a, "hall", Ground(a, a.SpawnersIn(86f, -54f, 104f, -43f)));
            Group(act, a, "behind", a.FloorPoints(110f, -50f, 121f, -44f, -6.6f, -5.9f, 3));
            Group(act, a, "ramp", Ground(a, a.SpawnersIn(68f, -50f, 73f, -44f)));
            Group(act, a, "room", Ground(a, a.SpawnersIn(40f, -68f, 63f, -50f, -1f, 1f)));
            Group(act, a, "high", a.SpawnersIn(40f, -70f, 63f, -43f, 1.5f, 5f));
            act.Wave(W.ActStart).Add(G.Imp, 2, "hall").Add(G.Goblin, 2, "hall");
            var behind = act.Wave(W.PrevBelow, 1).Add(G.Goblin, 3, "behind").Add(G.Imp, 1, "behind");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), a, 76f, -52f, 88f, -44f, -3f).Add(G.Imp, 2, "ramp").Add(G.Goblin, 3, "ramp");
            Zone(act.Wave(W.Enter), a, 40.4f, -69.8f, 75f, -42.8f, 0.2f).Add(G.Goblin, 3, "room").Add(G.Imp, 3, "high");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 3, "room").Add(G.Imp, 2, "high");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 3: Quest_Hallway3

        /// <summary>
        /// Act 3, Quest_Hallway3: the team starts on the platform at 4.22 in the east room next to Rec Room's [Quest_Boulder]
        /// (62.05, 4.91, -1.84). Measured on the level's colliders along z -1.8: the platform is flat to x 60, the ramp
        /// falls to -0.25 at x 48, the goblins' gallery lies at -0.3 (x 41-47, six EnemySpawnPoints); the east room's floor
        /// is at -4.7 (z 7-18: "imps spawn in the rooms behind") and the pit with the exit at -9.28 (x 39-49, z -15..-6).
        /// Our boulder rolls down that lane when the lever on the platform is pulled.
        /// </summary>
        public static IEnumerator Act3(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Hallway3", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Hallway3.Text(), r);
            var act = Finish(room, a, r, "ACT 3", "The Boulder");
            Group(act, a, "lane", Ground(a, a.SpawnersIn(40f, -5f, 48f, 6f)));
            Group(act, a, "behind", a.FloorPoints(57f, 6f, 66f, 18f, -5f, -4.4f, 4));
            Group(act, a, "pit", a.FloorPoints(39.5f, -14f, 48.5f, -6f, -9.5f, -9f, 3));
            act.Wave(W.ActStart).Add(G.Goblin, 6, "lane");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 3, "lane").Add(G.Imp, 2, "behind");
            Zone(act.Wave(W.Enter), a, 56.6f, 4f, 66.5f, 19f, -4.7f).Add(G.Imp, 2, "behind").Add(G.Goblin, 3, "behind");
            Zone(act.Wave(W.Enter), a, 39.1f, -14.4f, 49f, -6f, -9.28f).Add(G.Goblin, 4, "pit").Add(G.Imp, 2, "pit");
            float R = RrGoldenTrophyBoulder.Radius;
            var start = a.Extra("[Quest_Boulder]");
            var from = start != null ? new Vector3(start.ScenePosition.x, 4.22f + R, start.ScenePosition.z) : new Vector3(62.05f, 4.22f + R, -1.84f);
            var path = new[] { a.L(from), a.L(60.2f, 4.22f + R, -1.8f), a.L(48f, -0.25f + R, -1.8f), a.L(41f, -0.3f + R, -1.8f) };
            room.Boulder = RrGoldenTrophyBoulder.Create(room, r, path);
            var lever = a.L(64.3f, 4.22f, 3.2f);
            float h = a.FloorAt(lever);
            if (!float.IsNaN(h))
            {
                lever.y = h;
            }

            RrBuild.Box(r, "Lever Post", lever + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f), RrQuestKit.WoodDark);
            RrPressable.Create(r, "Boulder Lever", lever + Vector3.up * 1.03f, Quaternion.identity, new Color(0.95f, 0.25f, 0.2f), 0.4f,
                () => room.Ask(RrQuestEvents.ReqHazard, (byte)G.HazardBoulder, 0));
            RrBuild.Sign(r, "Lever Sign", "PUSH THE BOULDER!", lever + new Vector3(0f, 2.3f, 0f), Quaternion.Euler(0f, 90f, 0f), 2.2f, new Color(1f, 0.85f, 0.4f), true, 4f);
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 4: Quest_DeadIsland

        /// <summary>
        /// Act 4, Quest_DeadIsland: the lava room. Rec Room's [QuestLavaFloor] is 47.8 x 3.0 x 33.1 m at (155.4, -5.78,
        /// 57.6) (its top, -4.28, is the lava; the level's Lava_Mat glows over it). The team crosses from the entry room
        /// (x 169-195, -0.27) through an arch onto bridges down to the sunken centre at -2.27 and up to the exit vestibule
        /// (x 132-138, z 55-61, door at x 132.46); north and south bridges end in the side tunnels (EnemySpawnPoints at x
        /// 155, z 33-43 and 72-82). Final: barrel knights (charge) arrive; imps over the lava fall in when hit; goblins
        /// from every side but the exit; each side tunnel spawns 2 extra goblins when entered.
        /// </summary>
        public static IEnumerator Act4(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_DeadIsland", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_DeadIsland.Text(), r);
            var act = Finish(room, a, r, "ACT 4", "The Lava Room");
            Group(act, a, "center", a.FloorPoints(151.5f, 54.5f, 158.5f, 61f, -2.5f, -2f, 3));
            Group(act, a, "north", Ground(a, a.SpawnersIn(152f, 71f, 158f, 83f)));
            Group(act, a, "south", Ground(a, a.SpawnersIn(152f, 32f, 158f, 44f)));
            Group(act, a, "east", Ground(a, a.SpawnersIn(160f, 56f, 167f, 59f)));
            Group(act, a, "west", a.FloorPoints(133f, 55f, 141f, 60.5f, -0.6f, 0.2f, 3));
            Group(act, a, "lava", a.SpawnersIn(139f, 45f, 151f, 70f));
            act.Wave(W.ActStart).Add(G.KnightGreen, 1, "center").Add(G.Goblin, 2, "west");
            act.Wave(W.PrevBelow, 1).Add(G.Imp, 3, "lava").Add(G.Goblin, 2, "east");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 2, "north").Add(G.Goblin, 2, "south").Add(G.Imp, 2, "lava");
            act.Wave(W.PrevCleared).Add(G.KnightGreen, 2, "west").Add(G.Goblin, 3, "center");
            act.Wave(W.PrevBelow, 1).Add(G.KnightGreen, 1, "center").Add(G.Imp, 3, "lava").Add(G.Goblin, 2, "west");
            act.SideWave(a.L(155f, 1f, 78f), new Vector3(6f, 5f, 9f)).Add(G.Goblin, 2, "north");
            act.SideWave(a.L(155f, 1f, 37.5f), new Vector3(6f, 5f, 9f)).Add(G.Goblin, 2, "south");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 5: Quest_Hallway4

        /// <summary>
        /// Act 5, Quest_Hallway4: the split path. The team starts in the east hall (x 163-173, -4.78, facing west); a
        /// north-south corridor (x 152-157) splits into the north room at -7.28 (x 122-151, z 0-28) and the south gallery
        /// at -1.3 (x 114-152, z -33..-12); both meet in the west hall at -6.8 (x 86-118) with the exit at x 86.6. Final:
        /// elite goblins (not stunned by arrows) and elite imps (5-fireball spread) arrive; at some point 2 melee goblins
        /// spawn behind you; high platforms, walls and pillars give cover.
        /// </summary>
        public static IEnumerator Act5(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Hallway4", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Hallway4.Text(), r);
            var act = Finish(room, a, r, "ACT 5", "The Split Path");
            Group(act, a, "north", a.FloorPoints(122f, 0f, 151f, 28f, -7.6f, -6.9f, 5));
            Group(act, a, "south", Ground(a, a.SpawnersIn(114f, -30f, 152f, -15f)));
            Group(act, a, "behind", a.FloorPoints(163f, -11f, 174f, -5f, -5.2f, -4.4f, 2));
            Group(act, a, "west", a.FloorPoints(86.5f, -10f, 118f, -1f, -7.2f, -6.4f, 5));
            act.Wave(W.ActStart).Add(G.EliteGoblin, 1, "north").Add(G.Goblin, 2, "north").Add(G.EliteImp, 1, "north")
                .Add(G.EliteGoblin, 1, "south").Add(G.Goblin, 2, "south").Add(G.EliteImp, 1, "south");
            var behind = act.Wave(W.PrevBelow, 2).Add(G.Goblin, 2, "behind");
            behind.Toast = "Behind you!";
            act.Wave(W.PrevBelow, 1).Add(G.EliteGoblin, 2, "north").Add(G.Imp, 2, "south").Add(G.Goblin, 2, "south");
            Zone(act.Wave(W.Enter), a, 86.3f, -35.5f, 118f, 12.5f, -6.8f).Add(G.EliteGoblin, 2, "west").Add(G.EliteImp, 2, "west").Add(G.Goblin, 2, "west");
            act.Wave(W.PrevBelow, 1).Add(G.EliteGoblin, 2, "west").Add(G.EliteImp, 2, "west").Add(G.Imp, 2, "west");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 6: Quest_Library

        /// <summary>
        /// Act 6, Quest_Library (the 2019 "Golden Trophy Library" fix): the team starts in the north reading room at -1.13
        /// (x -42..-34, facing south) above the gallery; the main floor is at -3.13 among Rec Room's bookshelves (their
        /// colliders are the level's: players can stand on them); the raised platform with a ramp in the south-west carries
        /// GroundEnemySpawnPoint1..4; the exit wing runs west to x -58.7. Final: waves from nearly every side including
        /// behind; the elite barrel knight (spins) first appears here; a left tunnel spawns 2 elite goblins when entered.
        /// </summary>
        public static IEnumerator Act6(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Library", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Library.Text(), r);
            var act = Finish(room, a, r, "ACT 6", "The Library");
            Group(act, a, "platform", Ground(a, a.SpawnersIn(-46f, -3f, -38f, 5f)));
            Group(act, a, "gallery", Ground(a, a.SpawnersIn(-23f, 16f, -17f, 19f)));
            Group(act, a, "behind", Ground(a, a.SpawnersIn(-40f, 20f, -35f, 25f)));
            Group(act, a, "main", a.FloorPoints(-48f, -12f, -8f, 14f, -3.4f, -2.8f, 6));
            Group(act, a, "west", a.FloorPoints(-59f, 5f, -48f, 11f, -3.4f, -2.6f, 2));
            act.Wave(W.ActStart).Add(G.Goblin, 3, "platform").Add(G.Imp, 2, "main");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 2, "gallery").Add(G.EliteImp, 1, "main").Add(G.EliteGoblin, 1, "platform");
            var behind = act.Wave(W.PrevBelow, 1).Add(G.Goblin, 2, "behind").Add(G.EliteGoblin, 1, "behind");
            behind.Toast = "Behind you!";
            act.Wave(W.PrevCleared).Add(G.KnightRed, 1, "main").Add(G.EliteGoblin, 2, "platform").Add(G.Imp, 2, "main");
            act.Wave(W.PrevBelow, 1).Add(G.KnightGreen, 1, "gallery").Add(G.EliteImp, 2, "main").Add(G.Goblin, 2, "west");
            act.Wave(W.PrevBelow, 1).Add(G.KnightRed, 1, "main").Add(G.EliteGoblin, 2, "west").Add(G.EliteImp, 1, "main");
            act.SideWave(a.L(-54f, -1.6f, 8f), new Vector3(10f, 5f, 6f)).Add(G.EliteGoblin, 2, "west");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 7: Quest_Hallway5

        /// <summary>
        /// Act 7, Quest_Hallway5 (no reference spawn points; groups sampled on its floors at 0): the team starts in the
        /// north-east room (x 261-269, z 0-18, facing south); a long hall runs west (x 218-269, z -5..0) past a side room
        /// (x 242-260, z -16..-6); the small maze of crates and walls lies a floor lower (-3.3, x 198-213, z -19..-8), and the path ends in the
        /// larger south-west room (x 183-197, z -32..-8) with the exit (x 190.6, z -32.8). Final: an easier act with a
        /// barrel knight inside the maze, spawns ahead and behind, and a larger room with elite imps and goblins at the end.
        /// </summary>
        public static IEnumerator Act7(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Hallway5", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Hallway5.Text(), r);
            var act = Finish(room, a, r, "ACT 7", "The Maze");
            Group(act, a, "hall", a.FloorPoints(220f, -5f, 257f, 0.5f, -1f, 1f, 4));
            Group(act, a, "side", a.FloorPoints(242f, -16f, 260f, -6.5f, -1f, 1f, 3));
            Group(act, a, "maze", a.FloorPoints(198f, -19f, 213f, -8f, -3.6f, -2.8f, 5));
            Group(act, a, "behind", a.FloorPoints(258f, -5f, 269f, 6f, -1f, 1f, 3));
            Group(act, a, "end", a.FloorPoints(183f, -31f, 197f, -9f, -1.5f, 1.5f, 6));
            act.Wave(W.ActStart).Add(G.Goblin, 3, "hall").Add(G.Imp, 1, "side");
            act.Wave(W.PrevBelow, 1).Add(G.KnightGreen, 1, "maze").Add(G.Goblin, 2, "maze");
            var behind = Zone(act.Wave(W.Enter), a, 198f, -19f, 213f, -8f, -3.3f).Add(G.Goblin, 2, "behind").Add(G.Imp, 2, "maze");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), a, 183f, -31f, 197f, -9f, 0f).Add(G.EliteImp, 2, "end").Add(G.EliteGoblin, 2, "end");
            act.Wave(W.PrevBelow, 1).Add(G.EliteGoblin, 1, "end").Add(G.Goblin, 2, "end").Add(G.EliteImp, 1, "end");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 8: Quest_Battlefield

        /// <summary>
        /// Act 8, Quest_Battlefield (no reference spawn points): the team arrives up on the spawn ramp (x 26-34, 2.2, facing
        /// west); the hall below (x -13..18, z -62..-32, wood at 0.7) is half lava: Rec Room's [QuestLavaFloor] 11.6 x 18.9
        /// m at (-7.8, -52.8) under the south-west quarter, with wooden bridges and octagonal platforms; tunnels open north
        /// and south; the exit is in the west wall (x -19.4, z -46.8). Final (wiki): a trigger at the top of the spawn
        /// ramp; waves 1 elite + 4 goblins; 3 elites + 2 goblins; 2 elite flyers + 2 flyers (over the lava or in the back
        /// corner); 5 elite goblins; 2 elite barrel knights; 2 barrel knights + 1 elite flyer; a final wave of everything.
        /// </summary>
        public static IEnumerator Act8(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Battlefield", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Battlefield.Text(), r);
            var act = Finish(room, a, r, "ACT 8", "The Battlefield");
            Group(act, a, "hall", a.FloorPoints(-1f, -60f, 16f, -33f, 0.2f, 2f, 6));
            Group(act, a, "wood", a.FloorPoints(6f, -58f, 16f, -35f, 0.2f, 2f, 4));
            Group(act, a, "lava", a.FloorPoints(-13f, -61f, -2.5f, -44f, -0.6f, 0.4f, 4));
            Group(act, a, "back", a.FloorPoints(-13f, -42f, -1f, -32f, 0.2f, 2f, 3));
            Group(act, a, "tunnels", a.FloorPoints(-8.5f, -33f, -4.5f, -21f, 0.3f, 1.1f, 2));
            Zone(act.Wave(W.Enter), a, 17f, -51f, 27f, -43f, 2.2f).Add(G.EliteGoblin, 1, "hall").Add(G.Goblin, 4, "hall");
            act.Wave(W.PrevBelow, 1).Add(G.EliteGoblin, 3, "wood").Add(G.Goblin, 2, "hall");
            act.Wave(W.PrevBelow, 1).Add(G.EliteImp, 2, "lava").Add(G.Imp, 2, "back");
            act.Wave(W.PrevBelow, 1).Add(G.EliteGoblin, 5, "hall");
            act.Wave(W.PrevCleared).Add(G.KnightRed, 2, "hall");
            act.Wave(W.PrevBelow, 1).Add(G.KnightGreen, 2, "wood").Add(G.EliteImp, 1, "lava");
            act.Wave(W.PrevBelow, 1).Add(G.Goblin, 2, "tunnels").Add(G.EliteGoblin, 2, "hall").Add(G.Imp, 1, "back").Add(G.EliteImp, 1, "lava").Add(G.KnightGreen, 1, "wood");
            acts.Add(act);
        }

        // ------------------------------------------------------------------ act 9: Quest_Cafeteria

        /// <summary>
        /// Act 9, Quest_Cafeteria: the Goblin King's feast hall (x 129-165, z -65..-41, floor -5.29) with its feast tables,
        /// the half-round stage at the west end where Rec Room's "Boss Spawn Point (1)" stands (135.97, -4.43, -51.66,
        /// facing east), the raised platform with the bows and crossbows (Rec Room's Default Weapons: 3 crossbows, a bow,
        /// 2 shields, 4 swords) and the team's entry in the east (x 170). The Golden Trophy (Rec Room's GoldenBucket at
        /// (142.35, 0.17, -51.73)) drops to the floor below it when the king falls; a chest per player opens around it.
        /// </summary>
        public static IEnumerator Act9(G room, Transform r, List<RrQuestAct> acts, RrGoldenTrophyHull hull)
        {
            var a = new RrGoldenTrophyArea("GoldenTrophy_Cafeteria", Vector3.zero);
            yield return a.Build(hull, RrScene_GoldenTrophy_Cafeteria.Text(), r);
            var act = Finish(room, a, r, "ACT 9", "The Goblin King", false);
            act.Boss = true;
            var boss = a.Extra("Boss Spawn Point");
            var throne = boss != null ? a.Ground(boss.Position)[0] : a.Pts(136f, -4.4f, -51.7f)[0];
            act.Group("throne", throne);
            Group(act, a, "room", a.FloorPoints(143f, -63f, 163f, -43f, -5.6f, -4.9f, 6));
            act.Wave(W.ActStart).Add(G.King, 1, "throne");
            var trophy = a.Pts(142.35f, -5f, -51.73f)[0];
            room.TrophySpot = RrBuild.Group(r, "Trophy Spot", trophy + Vector3.up * 0.1f).transform;
            for (int i = 0; i < 4; i++)
            {
                var c = a.Pts(145.5f, -5f, -56.2f + 3f * i)[0];
                room.Chests.Add(RrQuestKit.Chest(r, c, -90f));
            }

            RrPortal.Create(r, RrActivity.RecCenter, a.L(142.5f, -5.29f, -35.8f), 180f, "REC CENTER");
            acts.Add(act);
        }
    }
}
