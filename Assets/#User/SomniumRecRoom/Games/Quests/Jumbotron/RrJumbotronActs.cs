using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron.Scenes;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using W = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestWave.When;
using J = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron.RrJumbotronRoom;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// The Foyer and the ten rooms of The Rise of Jumbotron on Rec Room's own levels, with the fights of the final
    /// version (rr_latest_rgames_story.md 9.1, the wiki walkthrough) placed on each level's own EncountersV2: the enemy
    /// spawn points and regions of each encounter are its spawn group, and its Overlap Volume is the zone that starts it
    /// (names as the level has them). Wave sizes are for 4 players (the framework scales them down for smaller parties).
    /// Each act's anchor is its arrival (the centre of Player7SpawnPoint (1..4), scene coordinates in each method).
    /// </summary>
    public static class RrJumbotronActs
    {
        public delegate IEnumerator Builder(J room, Transform root, Dictionary<string, Material> surfaces, Action<RrQuestAct> done);

        /// <summary>The Armory and acts 1-9, in play order.</summary>
        public static Builder[] All()
        {
            return new Builder[] { Armory, Act1, Act2, Act3, Act4, Act5, Act6, Act7, Act8, Act9 };
        }

        // ------------------------------------------------------------------ helpers

        private static RrQuestAct Finish(J room, RrJumbotronArea a, Transform r, string title, string subtitle)
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
            act.ExitDoor = RrJumbotronDoor.Create(a, r);
            AddPots(room, a, r);
            AddWeapons(room, a, r);
            room.Areas.Add(a);
            return act;
        }

        /// <summary>Rec Room's (SciFatPot) breakables at their poses: 50 credits each (the Supply room's goblin pot too).</summary>
        private static void AddPots(J room, RrJumbotronArea a, Transform parent)
        {
            foreach (var p in a.Pots)
            {
                room.Pots.Add(parent, "(SciFatPot)", a.L(p.Position), p.Rotation.eulerAngles.y, 50);
            }
        }

        private static void AddWeapons(J room, RrJumbotronArea a, Transform parent)
        {
            foreach (var w in a.Weapons)
            {
                string prefab = null;
                if (w.Keys != null)
                {
                    for (int i = 0; i < w.Keys.Length; i++)
                    {
                        if (w.Keys[i] == "prefab")
                        {
                            prefab = w.Values[i];
                        }
                    }
                }

                room.AddSpot(RrJumbotronWeaponSpot.Create(parent, prefab ?? w.Name, a.L(w.Position), w.Rotation));
            }
        }

        private static Vector3 Ahead(RrQuestAct act, float m)
        {
            return act.Entry + Quaternion.Euler(0f, act.EntryYaw, 0f) * new Vector3(0f, 0f, m);
        }

        /// <summary>A ground spawn group from an encounter's points (fallback: 10 m ahead of the arrival).</summary>
        private static void Ground(RrQuestAct act, RrJumbotronArea a, string name, string encounter, string group = null)
        {
            var pts = a.Points(encounter, group);
            act.Group(name, pts.Count > 0 ? pts.ToArray() : new[] { Ahead(act, 10f) });
        }

        /// <summary>
        /// A flyer group: Rec Room's flyer points already float 1.5-3 m up and the director lifts flyers 2.5 m more, so
        /// they are lowered 2 m (bat bots hover at about Rec Room's height).
        /// </summary>
        private static void Fly(RrQuestAct act, RrJumbotronArea a, string name, string encounter, string group = null)
        {
            var pts = a.Points(encounter, group);
            for (int i = 0; i < pts.Count; i++)
            {
                pts[i] += Vector3.down * 2f;
            }

            act.Group(name, pts.Count > 0 ? pts.ToArray() : new[] { Ahead(act, 12f) });
        }

        /// <summary>An Enter wave's zone: the encounter's Overlap Volume (fallback: a 12 m box ahead of the arrival).</summary>
        private static RrQuestWave Zone(RrQuestWave w, RrQuestAct act, RrJumbotronArea a, string encounter, int index = 0)
        {
            Bounds z;
            if (a.Zone(encounter, out z, index))
            {
                return w.In(z.center, z.size);
            }

            RrLog.WarnOnce("Jumbotron", "zone:" + a.Scene + encounter, a.Scene + ": no trigger volume for " + encounter + "; a 12 m zone ahead of the arrival");
            return w.In(Ahead(act, 12f), new Vector3(12f, 6f, 12f));
        }

        private static RrQuestWave Side(RrQuestAct act, RrJumbotronArea a, string encounter, int index = 0)
        {
            Bounds z;
            if (!a.Zone(encounter, out z, index))
            {
                z = new Bounds(Ahead(act, 30f), new Vector3(6f, 5f, 6f));
            }

            return act.SideWave(z.center, z.size);
        }

        // ------------------------------------------------------------------ the Foyer (lobby)

        /// <summary>
        /// The Foyer (level38, Quest_Scifi_Foyer): a sci-fi theatre lobby. The eight Player1..8SpawnPoint stand at
        /// x 39.5-40.6 facing -x (the room's origin is their centre); Rec Room's start hologram [ScoreboardProjector] (1)
        /// stands at (33.19, -6.51, 51.96) facing them (+z of its pose), the (LeaderboardProjector) at (33.95, -6.51, 62.32);
        /// the practice range "StufftoShoot" (standees, water bottles) with a pistol, burst rifle, shotgun and rail gun on
        /// the tables at x 35.3-36.4; the two [ActivityDoor] teleport doors back to the Rec Center behind the spawns
        /// (43.58, -6.51, 53.27 / 55.86, facing -x). Ours: <see cref="RrGameConsole"/> on the hologram's marker (party list
        /// and START QUEST), Rec Center portals where the activity doors are (their Rec Room meshes are left out so the
        /// two do not overlap), the four practice weapons.
        /// </summary>
        public static IEnumerator Foyer(J room, Transform t, Vector3 anchor, List<Pose> spawns, Dictionary<string, Material> surfaces)
        {
            var a = new RrJumbotronArea("Jumbotron_Foyer", anchor);
            foreach (var n in new[] { "ActivityDoor_Left", "ActivityDoor_Right", "ActivityDoor_Emit", "ActivityDoor_Glow", "ActivityDoor_Frame" })
            {
                a.Skip.Add(n);
            }

            yield return a.Build(RrScene_Jumbotron_Foyer.Text(), t, surfaces);
            spawns.Clear();
            spawns.AddRange(a.SpawnPoses());
            var holo = a.Markers("scoreboard");
            if (holo.Count > 0)
            {
                var m = holo[0];
                room.LobbyConsole(t, new Pose(m.Position, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
                RrLog.Info("Jumbotron", "Foyer: start hologram at " + m.Position.ToString("F2") + " yaw " + m.Rotation.eulerAngles.y.ToString("0"));
            }
            else
            {
                room.LobbyStartButton(t, a.L(33.2f, -6.51f, 52f), -90f);
            }

            foreach (var p in a.Markers("portal"))
            {
                RrPortal.Create(t, RrActivity.RecCenter, p.Position, p.Rotation.eulerAngles.y, "REC CENTER");
            }

            AddWeapons(room, a, t);
            AddPots(room, a, t);
        }

        // ------------------------------------------------------------------ the Armory (pre-start)

        /// <summary>
        /// The Armory (level39, Quest_Scifi_Armory; sign "ARMORY"): the dressing room where the run starts. Spawns at
        /// x 68.7 facing -x; the mirror and makeup table on the left wall (z 22.7), the couch and stools, lockers; 4 laser
        /// pistols and 4 burst rifles on the wall at x 61.0 (z 24.4-25.1 and 30.7-31.4); the exit door at x 56.4 with its
        /// trigger pad at x 58.8. No enemies: the door opens as soon as the team is in (the act is clear at once).
        /// </summary>
        private static IEnumerator Armory(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Armory", new Vector3(68.69f, -2.31f, 28.05f));
            yield return a.Build(RrScene_Jumbotron_Armory.Text(), r, s);
            var act = Finish(room, a, r, "THE ARMORY", "Grab a laser pistol or a burst rifle");
            done(act);
        }

        // ------------------------------------------------------------------ act 1

        /// <summary>
        /// Act 1 (level40, Quest_Scifi_Battlefield1; signs "SS Coach", exit "CARGO BAY"): the launch bay. Arrival
        /// (179.6, -3.76, -92.3) facing +z; Encounter1's volume at z -77.7 (the open port), WaveEncounter's at z -70.6 with
        /// spawn points Center / Center Steps / Left / Right, FlankEncounter's high region on the left (163.7, 0.74, -62.6),
        /// Encounter2 by the exit (x 201.3, z -48.0; UpperFlyers). Final: entering the open port spawns 3 bat bots, then 3,
        /// 3 more, 2 high on the left, 4 at the exit.
        /// </summary>
        private static IEnumerator Act1(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Battlefield1", new Vector3(179.63f, -3.76f, -92.33f));
            yield return a.Build(RrScene_Jumbotron_Battlefield1.Text(), r, s);
            var act = Finish(room, a, r, "ACT 1", "The SS Coach");
            Fly(act, a, "port", "Encounter1");
            Fly(act, a, "wave", "WaveEncounter");
            Fly(act, a, "flank", "FlankEncounter");
            Fly(act, a, "exit", "Encounter2");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(J.Bat, 3, "port");
            act.Wave(W.PrevBelow, 1).Add(J.Bat, 3, "wave");
            act.Wave(W.PrevBelow, 1).Add(J.Bat, 3, "wave");
            Zone(act.Wave(W.Enter), act, a, "FlankEncounter").Add(J.Bat, 2, "flank");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(J.Bat, 4, "exit");
            done(act);
        }

        // ------------------------------------------------------------------ act 2

        /// <summary>
        /// Act 2 (level41, Quest_Scifi_Hallway1; sign "CARGO BAY", exit "ENGINEERING"): arrival (-21.0, 1.3, 119.2) facing
        /// -x down a long hall (Encounter1 at x -40, Encounter2 -48, Encounter3 -59), the right-hand side room at z 130 with
        /// the laser shotgun (DeadEndFlyers / DeadEndCrawlers), then the loading bay (Encounter4, x -100, with its W1
        /// flyers, W2 ground points and Exploders regions) and the exit at (-97.7, -4.2, 94.8). Final: 5 tread bots at the
        /// ramp, 5 more behind, 2 tread + 2 bat, 5 mouse bombs; the side room spawns 3 bat bots, then 3 more + mice; 3
        /// consecutive waves at the end. Four explosive barrels stand in the bay.
        /// </summary>
        private static IEnumerator Act2(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Hallway1", new Vector3(-21.0f, 1.3f, 119.15f));
            yield return a.Build(RrScene_Jumbotron_Hallway1.Text(), r, s);
            var act = Finish(room, a, r, "ACT 2", "The Cargo Bay");
            Ground(act, a, "hall", "Encounter1");
            Ground(act, a, "behind", "Encounter2");
            Ground(act, a, "mid", "Encounter3");
            Fly(act, a, "midF", "Encounter3");
            Fly(act, a, "sideF", "DeadEndFlyers");
            Ground(act, a, "sideG", "DeadEndCrawlers");
            Fly(act, a, "bayF", "Encounter4", "flyers");
            Ground(act, a, "bayG", "Encounter4", "ground");
            Ground(act, a, "bayX", "Encounter4", "Exploders");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(J.Tread, 5, "hall");
            var behind = act.Wave(W.PrevBelow, 2).Add(J.Tread, 5, "behind");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(J.Tread, 2, "mid").Add(J.Bat, 2, "midF");
            act.Wave(W.PrevBelow, 1).Add(J.Mouse, 5, "mid");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(J.Tread, 3, "bayG").Add(J.Bat, 2, "bayF");
            act.Wave(W.PrevBelow, 1).Add(J.Mouse, 4, "bayX").Add(J.Tread, 2, "bayG");
            act.Wave(W.PrevBelow, 1).Add(J.Tread, 3, "bayG").Add(J.Bat, 2, "bayF").Add(J.Mouse, 2, "bayX");
            Side(act, a, "DeadEndFlyers").Add(J.Bat, 3, "sideF");
            Side(act, a, "DeadEndCrawlers").Add(J.Bat, 3, "sideF").Add(J.Mouse, 3, "sideG");
            done(act);
        }

        // ------------------------------------------------------------------ act 3

        /// <summary>
        /// Act 3 (level42, Quest_Scifi_Hallway2; signs "SERVERS", exit "BRIDGE"): Engineering. Arrival (17.9, 2.3, 131.0)
        /// facing +x; the server hall runs down ramps (Encounter1 at z 111.6, Encounter2 z 96.5 with its Back Attack
        /// region behind, Encounter3 x 64 with flyers, Encounter4 x 76 attacking from behind) to the final room
        /// (Encounter5, x 75-99, z 66-76: FinaleFlyers, Upper Ground 1/2, Lower Ground; 18 points) and the exit at
        /// (98.0, -1.7, 59.5). 68 server terminals, 12 grenades. Final: tread bots in waves down and up the ramps, bat bots
        /// behind; the last room has a bit of everything.
        /// </summary>
        private static IEnumerator Act3(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Hallway2", new Vector3(17.9f, 2.3f, 131.0f));
            yield return a.Build(RrScene_Jumbotron_Hallway2.Text(), r, s);
            var act = Finish(room, a, r, "ACT 3", "Engineering");
            Ground(act, a, "e1", "Encounter1");
            Ground(act, a, "e2", "Encounter2");
            Fly(act, a, "e2back", "Encounter2", "Back Attack");
            Ground(act, a, "e3", "Encounter3");
            Fly(act, a, "e3F", "Encounter3", "Flyers");
            Fly(act, a, "e4back", "Encounter4");
            Ground(act, a, "back", "BackAttack");
            Ground(act, a, "finG", "Encounter5", "Ground");
            Fly(act, a, "finF", "Encounter5", "Flyers");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(J.Tread, 4, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(J.Tread, 4, "e2").Add(J.Bat, 2, "e2back");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(J.Tread, 3, "e3").Add(J.Bat, 3, "e3F");
            var behind = Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(J.Bat, 2, "e4back").Add(J.Tread, 2, "e3");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(J.Tread, 3, "finG").Add(J.Bat, 2, "finF").Add(J.Mouse, 3, "finG");
            act.Wave(W.PrevBelow, 1).Add(J.EliteBat, 1, "finF").Add(J.Tread, 3, "finG").Add(J.Bat, 2, "finF");
            Side(act, a, "BackAttack").Add(J.Tread, 2, "back");
            done(act);
        }

        // ------------------------------------------------------------------ act 4

        /// <summary>
        /// Act 4 (level43, Quest_Scifi_Reception; signs "BRIDGE", exit "FLIGHT DECK"): arrival (198.5, -1.0, 21.8) facing
        /// +x into the bridge (one WaveEncounter over the whole room, 39 x 45 m: Wave1 with the Knight Spawn Points East /
        /// West = the assassin bots, Wave2, Wave3, Wave5, patrol and ranged points), the east tunnels (x 218 and 231,
        /// z -6 .. -13) and the west tunnel (x 218, z 31-37) with their own flank encounters; exit at (244.0, -3.0, 10.3).
        /// Final: assassin bots (a pink aiming laser, then the shot; the battery on the back is the weak spot), tread, bat
        /// and mouse waves - the mice only if you enter their rooms.
        /// </summary>
        private static IEnumerator Act4(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Reception", new Vector3(198.5f, -1.0f, 21.8f));
            yield return a.Build(RrScene_Jumbotron_Reception.Text(), r, s);
            var act = Finish(room, a, r, "ACT 4", "The Bridge");
            Ground(act, a, "knights", "WaveEncounter", "Knight Spawn Point");
            Ground(act, a, "w2", "WaveEncounter", "Wave2");
            Ground(act, a, "w3", "WaveEncounter", "Wave3");
            Ground(act, a, "w5", "WaveEncounter", "Wave5");
            Fly(act, a, "ranged", "WaveEncounter", "RangedSpawnPoints");
            Ground(act, a, "east1", "EastTunnelFlankEncounter");
            Ground(act, a, "east2", "EastTunnelFlankEncounter (1)");
            Ground(act, a, "west", "WestTunnelFlankEncounter");
            Zone(act.Wave(W.Enter), act, a, "WaveEncounter").Add(J.Assassin, 1, "knights").Add(J.Tread, 3, "w2");
            act.Wave(W.PrevBelow, 1).Add(J.Bat, 3, "ranged").Add(J.Tread, 2, "w3");
            act.Wave(W.PrevBelow, 1).Add(J.Assassin, 1, "knights").Add(J.Tread, 3, "w5");
            act.Wave(W.PrevBelow, 1).Add(J.Assassin, 2, "knights").Add(J.Bat, 2, "ranged");
            Side(act, a, "EastTunnelFlankEncounter").Add(J.Mouse, 3, "east1");
            Side(act, a, "EastTunnelFlankEncounter (1)").Add(J.Mouse, 3, "east2");
            Side(act, a, "WestTunnelFlankEncounter").Add(J.Mouse, 3, "west");
            done(act);
        }

        // ------------------------------------------------------------------ act 5

        /// <summary>
        /// Act 5 (level44, Quest_Scifi_StadiumConcession, room "Departure"; signs "Blast Off!", exit "SPACE"): the Flight
        /// Deck. Arrival (194.1, -0.27, 57.7) facing -x; almost no floor around the parked escape vessel in the middle; the
        /// big green button [QuestEncounterTriggerButton] on its console at (157.0, 1.43, 54.6); the projector screen at
        /// (150.2, 2.46, 44.0); WaveEncounter's spawn points Catwalk / Front / FrontLeft / Right / BackLeft / BackRight /
        /// HallwayLeft / AssassinSpawns; exit (132.6, -0.28, 58.1). Final: the button plays the projector scene (the ship
        /// flying to the Jumbodome) and starts the attack; at the end "Now arriving, all units please depart the escape
        /// vessel". Falling off the deck is a down (Rec Room's kill floor, <see cref="J.KillFloorY"/>).
        /// </summary>
        private static IEnumerator Act5(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_StadiumConcession", new Vector3(194.1f, -0.27f, 57.7f));
            yield return a.Build(RrScene_Jumbotron_StadiumConcession.Text(), r, s);
            var act = Finish(room, a, r, "ACT 5", "The Flight Deck");
            Fly(act, a, "catwalk", "WaveEncounter", "Catwalk");
            Fly(act, a, "front", "WaveEncounter", "Front");
            Ground(act, a, "right", "WaveEncounter", "Right");
            Ground(act, a, "back", "WaveEncounter", "Back");
            Ground(act, a, "hall", "WaveEncounter", "HallwayLeft");
            Ground(act, a, "assassin", "WaveEncounter", "AssassinSpawns");
            // The attack starts at the console: the zone is the deck around the button (8 x 8 m).
            var button = a.Marker("button", "[QuestEncounterTriggerButton]");
            Vector3 b = button != null ? button.Position : Ahead(act, 37f);
            act.Wave(W.Enter).In(b, new Vector3(8f, 6f, 8f)).Add(J.Bat, 4, "catwalk").Add(J.EliteBat, 2, "front");
            act.Wave(W.PrevBelow, 1).Add(J.Tread, 3, "right").Add(J.EliteTread, 1, "back");
            act.Wave(W.PrevBelow, 1).Add(J.Assassin, 1, "assassin").Add(J.Tread, 2, "hall").Add(J.EliteBat, 2, "catwalk");
            if (button != null)
            {
                RrPressable.Create(r, "Launch Button", button.Position + Vector3.up * 0.1f, button.Rotation, new Color(0.2f, 0.95f, 0.3f), 0.45f, room.PressLaunch);
            }

            done(act);
        }

        // ------------------------------------------------------------------ act 6

        /// <summary>
        /// Act 6 (level45, Quest_Scifi_StadiumEntry; signs "JUMBODOME", exit "HANGAR"): Space, outside the Jumbodome. The
        /// level spans 640 m (a backdrop down to x -485): it is cut to 160 x 180 m around the play strip (x -100..-65,
        /// z -146..-30). Arrival (-93.8, -3.5, -34.0) facing -z; Encounter1 (23 points, the patrollers) is waiting as the
        /// team arrives; Attack From Behind at z -62; Encounter2 / 2 (1) / 3 / 3 (2) along the strip; Encounter IntroElites
        /// at the exit (-88.2, -7.5, -141.1). 12 explosive barrels. Final: every sound is muffled; a horde of tread bots
        /// next to an explosive barrel; spawns behind you; an elite tread bot near the end.
        /// </summary>
        private static IEnumerator Act6(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_StadiumEntry", new Vector3(-93.8f, -3.5f, -34.0f))
            {
                Filter = new Bounds(new Vector3(-82f, 0f, -88f), new Vector3(160f, 90f, 180f)),
                // Iteration 10: the light grid over the play strip only (cells about 1.5 x 1.25 x 4.7 m instead of 5.3 x 7.4 x 7.5).
                GridToCore = true
            };
            yield return a.Build(RrScene_Jumbotron_StadiumEntry.Text(), r, s);
            var act = Finish(room, a, r, "ACT 6", "Space");
            Ground(act, a, "horde", "Encounter1");
            Ground(act, a, "behind", "Attack From Behind");
            Ground(act, a, "e2", "Encounter2");
            Ground(act, a, "e21", "Encounter2 (1)");
            Ground(act, a, "e3", "Encounter3");
            Fly(act, a, "e32F", "Encounter3 (2)");
            Ground(act, a, "e32", "Encounter3 (2)");
            Ground(act, a, "elites", "Encounter IntroElites", "Ground");
            Fly(act, a, "elitesF", "Encounter IntroElites", "Flyer");
            act.Wave(W.ActStart).Add(J.Tread, 5, "horde");
            var behind = Zone(act.Wave(W.Enter), act, a, "Attack From Behind").Add(J.Tread, 3, "behind");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(J.Tread, 3, "e2").Add(J.Bat, 2, "e21");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(J.Tread, 3, "e3").Add(J.Bat, 2, "e32F");
            Zone(act.Wave(W.Enter), act, a, "Encounter3 (2)").Add(J.Bat, 2, "e32F").Add(J.EliteBat, 2, "e32F").Add(J.Tread, 2, "e32");
            Zone(act.Wave(W.Enter), act, a, "Encounter IntroElites").Add(J.EliteTread, 1, "elites").Add(J.Tread, 2, "elites").Add(J.Bat, 3, "elitesF");
            done(act);
        }

        // ------------------------------------------------------------------ act 7

        /// <summary>
        /// Act 7 (level46, Quest_Scifi_Garage; exit sign "SUPPLY"): the Hangar, two large rooms. Arrival (138.6, -0.8,
        /// -33.6) facing +z; the first room's Encounter1 (x 109.5, EliteFlyerIntro), Encounter2 and 3 (x 92-95), the second
        /// room's WaveEncounter (x 84-120, z -4..16; Wave1..6, Wave5's KnightSpawn1/2 = assassins, Wave6 flyers) and the
        /// exit at (116.0, -4.7, 22.9). Final: elite bat bots (bursts of 5) with bat bots, then elites of both kinds, their
        /// normal versions and assassin bots; spawns behind you.
        /// </summary>
        private static IEnumerator Act7(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Garage", new Vector3(138.55f, -0.8f, -33.6f));
            yield return a.Build(RrScene_Jumbotron_Garage.Text(), r, s);
            var act = Finish(room, a, r, "ACT 7", "The Hangar");
            Fly(act, a, "e1F", "Encounter1");
            Ground(act, a, "e2", "Encounter2");
            Fly(act, a, "e2F", "Encounter2");
            Ground(act, a, "e3", "Encounter3");
            Fly(act, a, "e3F", "Encounter3");
            Ground(act, a, "w1", "WaveEncounter", "Wave1");
            Ground(act, a, "w2", "WaveEncounter", "Wave2");
            Ground(act, a, "knights", "WaveEncounter", "KnightSpawn");
            Fly(act, a, "w6F", "WaveEncounter", "Wave6");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(J.EliteBat, 2, "e1F").Add(J.Bat, 2, "e1F");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(J.Bat, 3, "e2F").Add(J.Tread, 2, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(J.EliteBat, 2, "e3F").Add(J.Tread, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "WaveEncounter").Add(J.EliteTread, 2, "w1").Add(J.Tread, 2, "w2").Add(J.Bat, 2, "w6F");
            act.Wave(W.PrevBelow, 1).Add(J.Assassin, 1, "knights").Add(J.EliteBat, 2, "w6F").Add(J.Bat, 2, "w6F");
            var behind = act.Wave(W.PrevBelow, 1).Add(J.EliteTread, 2, "e3").Add(J.EliteBat, 2, "e2F").Add(J.Assassin, 1, "knights");
            behind.Toast = "Behind you!";
            done(act);
        }

        // ------------------------------------------------------------------ act 8

        /// <summary>
        /// Act 8 (level47, Quest_Scifi_Cargo1; exit sign "JUMBOTRON"): Supply. Arrival (-8.2, 0.0, 21.4) facing +x with the
        /// extra guns (rail gun, shotgun, pistol, burst rifle within 15 m); Encounter1..3 down the warehouse (z 5 .. -12),
        /// Back Attack, the Assassin patrol (z -22), the side room "Goblin" (17.2, -2.0, -22.2) with Kevin the goblin and
        /// Golden Trophy's (FatPot), and the final room (Encounter4 / 5, x 24-53, z -31..0: assassin patrols, exploders,
        /// flyers) before the exit at (46.3, -2.0, -34.3). 25 explosive barrels. Final: "by far the hardest" final room.
        /// </summary>
        private static IEnumerator Act8(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Cargo1", new Vector3(-8.15f, 0.0f, 21.4f));
            yield return a.Build(RrScene_Jumbotron_Cargo1.Text(), r, s);
            var act = Finish(room, a, r, "ACT 8", "Supply");
            Ground(act, a, "e1", "Encounter1");
            Ground(act, a, "e2", "Encounter2");
            Ground(act, a, "e3", "Encounter3");
            Fly(act, a, "e3F", "Encounter3", "Flyers");
            Ground(act, a, "backG", "Back Attack");
            Fly(act, a, "backF", "Back Attack", "Flyers");
            Ground(act, a, "assassin", "Assassin");
            Ground(act, a, "e4", "Encounter4");
            Fly(act, a, "e4F", "Encounter4", "Flyers");
            Ground(act, a, "e5", "Encounter5", "Ground");
            Ground(act, a, "e5k", "Encounter5", "Assassin");
            Fly(act, a, "e5F", "Encounter5", "Flyers");
            Ground(act, a, "e5x", "Encounter5", "Exploders");
            Ground(act, a, "kevin", "Goblin");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(J.Tread, 3, "e1").Add(J.Bat, 2, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(J.Tread, 3, "e2").Add(J.Mouse, 2, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(J.Bat, 2, "e3F").Add(J.Mouse, 3, "e3").Add(J.EliteBat, 1, "e3F");
            Zone(act.Wave(W.Enter), act, a, "Assassin").Add(J.Assassin, 1, "assassin").Add(J.Tread, 2, "assassin");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(J.EliteTread, 2, "e4").Add(J.EliteBat, 2, "e4F").Add(J.Tread, 2, "e4");
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(J.Assassin, 1, "e5k").Add(J.EliteTread, 2, "e5").Add(J.EliteBat, 2, "e5F").Add(J.Bat, 2, "e5F");
            act.Wave(W.PrevBelow, 1).Add(J.Assassin, 1, "e5k").Add(J.Tread, 2, "e5").Add(J.EliteBat, 2, "e5F").Add(J.Mouse, 3, "e5x");
            Side(act, a, "Back Attack").Add(J.Bat, 2, "backF").Add(J.Mouse, 2, "backG");
            Side(act, a, "Goblin").Add(J.Goblin, 1, "kevin");
            done(act);
        }

        // ------------------------------------------------------------------ act 9

        /// <summary>
        /// Act 9 (level48, Quest_Scifi_Stadium; sign "RE-ENTRY"): the Jumbodome arena. Arrival (103.6, -2.2, -116.2) facing
        /// north-west, inside BossEncounter's volume; Jumbotron hangs over the arena's centre ("RoomGeo/Jumbotron" at
        /// (89.46, 0.76, -96.31), his Enemy_Jumbotron_Mat block over y 8.4-13.6 rebuilt by
        /// <see cref="RrJumbotronBoss"/>), BossSpawnPoint (1) under it; the Reinforcements spawn points Ground
        /// (UpperGroundLeft / UpperGroundRight / NearRightUpper), Flying and Exploders; cover cutouts, railings and a
        /// platform with 2 pistols, 2 rail guns, 2 shotguns and 2 burst rifles. The exit door (89.6, -5.1, -74.5) is Rec
        /// Room's way out after the win; here the victory screen follows.
        /// </summary>
        private static IEnumerator Act9(J room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Jumbotron_Stadium", new Vector3(103.6f, -2.2f, -116.2f));
            a.DropBoxes.Add(new Bounds(new Vector3(89.9f, 11f, -96.4f), new Vector3(14f, 6f, 14.5f)));
            yield return a.Build(RrScene_Jumbotron_Stadium.Text(), r, s);
            var act = Finish(room, a, r, "ACT 9", "Jumbotron");
            act.Boss = true;
            act.ExitDoor = null;
            room.Jumbotron = RrJumbotronBoss.Build(r, a.L(89.9f, 10.9f, -96.4f));
            var bossPoints = a.Markers("enemy_spawner", "BossSpawnPoint");
            act.Group("boss", bossPoints.Count > 0 ? bossPoints[0].Position : a.L(89.46f, 2.89f, -96.31f));
            Ground(act, a, "reinf_ground", "BossEncounter", "Reinforcements/Ground");
            Fly(act, a, "reinf_fly", "BossEncounter", "Reinforcements/Flying");
            Ground(act, a, "reinf_mice", "BossEncounter", "Exploders");
            act.Wave(W.ActStart).Add(J.Boss, 1, "boss");
            act.Wave(W.PrevBelow, 1).Add(J.Tread, 2, "reinf_ground").Add(J.Bat, 2, "reinf_fly");
            done(act);
        }
    }
}
