using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using W = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestWave.When;
using C = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron.RrCrimsonCauldronRoom;
using K = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron.RrCrimsonCauldronKit;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// The Foyer, the Armory and the nine acts of The Curse of the Crimson Cauldron on Rec Room's own Goblin2 levels, with
    /// the fights of the final version (rr_latest_rgames_story.md 6.1, the wiki walkthrough) placed on each level's own
    /// encounters (Systems/Encounters/&lt;name&gt;: spawn points and spawn / patrol regions are the spawn groups, the
    /// encounter's Triggers/Overlap Volume the zone that starts it; names as the levels have them). Encounters without a
    /// trigger volume are the ones already standing in the room (Rec Room's "in place" enemies): they start with the act.
    /// Wave sizes are for 4 players (the framework scales them down for smaller parties). Each area's anchor is its
    /// arrival (the centre of Player7SpawnPoint (1..4); scene coordinates in each method).
    /// </summary>
    public static class RrCrimsonCauldronActs
    {
        public delegate IEnumerator Builder(C room, Transform root, Dictionary<string, Material> surfaces, Action<RrQuestAct> done);

        /// <summary>The Armory and acts 1-9, in play order.</summary>
        public static Builder[] All()
        {
            return new Builder[] { Armory, Act1, Act2, Act3, Act4, Act5, Act6, Act7, Act8, Act9 };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Builds an area from its data file (<see cref="RrSceneBank"/>). Without the file (the scene does not carry
        /// Data/&lt;scene&gt;.bytes yet) a plain 40 m floor stands in, so the act can still be walked through.
        /// </summary>
        private static IEnumerator Build(RrJumbotronArea a, Transform r, Dictionary<string, Material> s)
        {
            string text = RrSceneBank.Text(a.Scene);
            if (string.IsNullOrEmpty(text))
            {
                RrLog.Warn("CrimsonCauldron", a.Scene + ": no level data in the scene (Data/" + a.Scene + ".bytes); a plain floor stands in");
                RrBuild.Box(r, "Stand-in Floor " + a.Scene, new Vector3(0f, -0.2f, 0f), new Vector3(40f, 0.4f, 40f), RrLook.Solid(new Color(0.33f, 0.3f, 0.27f)));
                yield break;
            }

            yield return a.Build(text, r, s);
        }

        /// <summary>The common part of every act: arrival, area, exit door and pad, lava, pots, weapons, ladders, steam, kill floor.</summary>
        private static RrQuestAct Finish(C room, RrJumbotronArea a, Transform r, string title, string subtitle, bool exit = true)
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
            if (exit)
            {
                GameObject pad;
                act.ExitDoor = K.ExitDoor(a, r, out pad);
                room.AddGatherPlatform(act.ExitDoor, pad);
                if (act.ExitDoor == null)
                {
                    // No exit in the data (a missing file): a door 12 m ahead of the arrival keeps the run going.
                    act.ExitDoor = RrQuestDoor.Create(r, Ahead(act, 12f), act.EntryYaw, 2.05f, 2.15f);
                    RrLog.Warn("CrimsonCauldron", a.Scene + ": no [Quest_Door_Active]; a stand-in exit 12 m ahead of the arrival");
                }
            }

            foreach (var lava in K.Lava(a))
            {
                act.Lava.Add(lava);
            }

            int pots = K.AddPots(room, a, r);
            K.AddWeapons(room, a, r);
            int ladders = K.Ladders(a, r);
            int steam = K.Steam(a, r);
            float? kill = K.KillFloorY(a);
            room.AddArea(a, kill);
            RrLog.Info("CrimsonCauldron", a.Scene + ": " + pots + " pots, " + ladders + " ladders, " + steam + " steam traps, " + act.Lava.Count + " lava boxes"
                + (kill.HasValue ? ", kill floor " + kill.Value.ToString("F1") : ""));
            return act;
        }

        private static Vector3 Ahead(RrQuestAct act, float m)
        {
            return act.Entry + Quaternion.Euler(0f, act.EntryYaw, 0f) * new Vector3(0f, 0f, m);
        }

        /// <summary>
        /// A spawn group from an encounter's points (group: substring of the point's group path; fallback 10 m ahead). A
        /// ground group leaves out the points in the act's water or pit (the Bog's flyer regions lie on the swamp's
        /// surface: a goblin put there would sink at once); a flyer group keeps them (flyers appear 2.5 m up).
        /// </summary>
        private static void G(RrQuestAct act, RrJumbotronArea a, string name, string encounter, string group = null, bool fly = false)
        {
            var pts = a.Points(encounter, group);
            if (!fly && act.Lava.Count > 0)
            {
                var dry = pts.FindAll(p => !Wet(act, p));
                if (dry.Count > 0 && dry.Count < pts.Count)
                {
                    RrLog.Info("CrimsonCauldron", a.Scene + ": group " + name + " leaves " + (pts.Count - dry.Count) + " of " + pts.Count + " points in the water out");
                    pts = dry;
                }
            }

            if (pts.Count == 0)
            {
                RrLog.WarnOnce("CrimsonCauldron", "grp:" + a.Scene + name, a.Scene + ": no spawn points for " + encounter + (group != null ? " / " + group : "") + "; 10 m ahead of the arrival");
                pts.Add(Ahead(act, 10f));
            }

            act.Group(name, pts.ToArray());
        }

        /// <summary>An act-local point over one of the act's lava / water boxes and at most 0.5 m above its surface.</summary>
        private static bool Wet(RrQuestAct act, Vector3 p)
        {
            foreach (var b in act.Lava)
            {
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y <= b.max.y + 0.5f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>An Enter wave's zone: the encounter's index-th Overlap Volume (fallback: a 12 m box ahead of the arrival).</summary>
        private static RrQuestWave Zone(RrQuestWave w, RrQuestAct act, RrJumbotronArea a, string encounter, int index = 0, float grow = 0f)
        {
            Bounds z;
            if (K.Zone(a, encounter, index, out z))
            {
                var size = z.size + new Vector3(grow, grow, grow);
                size.y = Mathf.Max(size.y, 3f);
                return w.In(z.center, size);
            }

            RrLog.WarnOnce("CrimsonCauldron", "zone:" + a.Scene + encounter + index, a.Scene + ": no trigger volume for " + encounter + " (" + index + "); a 12 m zone ahead of the arrival");
            return w.In(Ahead(act, 12f), new Vector3(12f, 6f, 12f));
        }

        // ------------------------------------------------------------------ the Foyer (lobby)

        /// <summary>
        /// The Foyer (level50, Quest_Goblin2_Foyer): the quest theatre's lobby. The eight Player1..8SpawnPoint stand at x
        /// 24.4-25.5 facing -x (the room's origin is their centre); Rec Room's start hologram [ScoreboardProjector] (1)
        /// at (18.08, -7.78, 61.56); the (LeaderboardProjector) at (13.94, 50.16); the practice table with a sword, a
        /// longbow, a wand and four fire potions at x 19.8-21.3; the merch booth (store) on the north side at z 75-78;
        /// the [ActivityDoor] back to the Rec Center behind the spawns at (28.6, -7.78, 64.21), facing -x. Ours:
        /// <see cref="RrGameConsole"/> on the hologram (party list and START QUEST), a Rec Center portal in the activity
        /// door (its Rec Room leaves are left out), the practice weapons on a rack at Rec Room's poses.
        /// </summary>
        public static IEnumerator Foyer(C room, Transform t, List<Pose> spawns, Dictionary<string, Material> surfaces)
        {
            var a = K.Area(C.FoyerScene, C.FoyerAnchor);
            yield return Build(a, t, surfaces);
            spawns.Clear();
            spawns.AddRange(a.SpawnPoses());
            var holo = a.Markers("scoreboard");
            if (holo.Count > 0)
            {
                var m = holo[0];
                room.LobbyConsole(t, new Pose(m.Position, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
            }
            else
            {
                room.LobbyStartButton(t, a.L(18.08f, -7.78f, 61.56f), 24f);
            }

            RrPortal.Create(t, RrActivity.RecCenter, a.L(28.6f, -7.78f, 64.21f), -90f, "REC CENTER");
            K.AddWeapons(room, a, t);
            K.AddPots(room, a, t);
            K.Ladders(a, t);
        }

        // ------------------------------------------------------------------ the Armory (pre-start)

        /// <summary>
        /// The Armory (level51, Quest_Goblin2_Armory): the dressing room where the run starts, 10.7 x 10.5 m. Spawns at x
        /// 46.5 facing -x; Rec Room's weapons on the back wall and tables (4 swords, 2 shields, 2 longbows, 3 wands, a
        /// fire potion) and 11 pots; the exit ([Quest_Door_Active]) at x 38.85, z 50.44, its [DoorTrigger] pad at x 41.3.
        /// No enemies: the door opens as soon as the team is in.
        /// </summary>
        private static IEnumerator Armory(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_Armory", new Vector3(46.56f, -1.685f, 50.40f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "THE ARMORY", "Arm yourselves");
            act.Prelude = true;
            act.ArrivalToast = "Grab your weapons, then gather on the pad by the door!";
            done(act);
        }

        // ------------------------------------------------------------------ act 1

        /// <summary>
        /// Act 1 (level52, Quest_Goblin2_CastleCourtyard): arrival (201.6, -8.46, -84.6) facing +x over the drawbridge.
        /// Encounter1Right (z -96: a spawn and patrol region, a Mortar point up on the wall), Encounter1Left (z -72..-76,
        /// the PillarLeft point), Random Flyers (two regions, volume turned 90 degrees over x 221-260), the Finale by the
        /// exit (GroundTroop x7 at x 249-253, Mortar x2 on the platforms; its volume at x 219-239) and the exit at (256.5,
        /// -9.7, -86.4). Two ladders up to the bomber platforms. Final: right 4 goblins + a bomber on a platform; left a
        /// bomber and 2 shield goblins; a 50/50 pair of flyers (one red, one green); two bombers, and 6 shield goblins
        /// guarding the exit.
        /// </summary>
        private static IEnumerator Act1(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_CastleCourtyard", new Vector3(201.64f, -8.455f, -84.58f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 1", "The Castle Courtyard");
            G(act, a, "right", "Encounter1Right", "Region");
            G(act, a, "rightM", "Encounter1Right", "Mortar");
            G(act, a, "left", "Encounter1Left", "Region");
            G(act, a, "leftM", "Encounter1Left", "Pillar");
            G(act, a, "flyers", "Random Flyers");
            G(act, a, "finG", "Finale", "GroundTroop");
            G(act, a, "finM", "Finale", "Mortar");
            act.Wave(W.ActStart).Add(C.Goblin, 4, "right").Add(C.Bomber, 1, "rightM");
            act.Wave(W.ActStart).Add(C.Bomber, 1, "leftM").Add(C.Shield, 2, "left");
            Zone(act.Wave(W.Enter), act, a, "Random Flyers").Add(C.EliteImp, 1, "flyers").Add(C.Imp, 1, "flyers");
            Zone(act.Wave(W.Enter), act, a, "Finale").Add(C.Bomber, 2, "finM").Add(C.Shield, 6, "finG");
            done(act);
        }

        // ------------------------------------------------------------------ act 2

        /// <summary>
        /// Act 2 (level53, Quest_Goblin2_Forest1): the Forest Path. Arrival (31.2, -2.27, -100.5) facing -x; EntryHallway
        /// (x 20, z -88), the Upper Hallway Flyers (x 10, z -69), the Huntsman's cabin ("Rumble at the Hunstmans Cabin",
        /// x -17, z -74; the double-damage potions), the Goblin Encampment (x -25..-38, z -48..-68, two explosive barrels),
        /// the Ground Pincer, the Attack from Behind (x -10, z -31), Lots of Ground Rushers (x -17, z -29) with the
        /// Annoying Mortar, the Flyer Finale (x 6, z -34) and the exit at (10.2, -10.3, -32.9). Final: 3 goblins, whose
        /// deaths bring 2 flyers and 2 runners; 5 flyers on a branch; the cabin; goblins by the barrels; red goblins
        /// behind; bombers back to back; flyers blocking the path.
        /// </summary>
        private static IEnumerator Act2(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_Forest1", new Vector3(31.23f, -2.269f, -100.50f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 2", "The Forest Path");
            G(act, a, "entry", "EntryHallway");
            G(act, a, "upperF", "Upper Hallway Flyers");
            G(act, a, "cabin", "Rumble at the Hunstmans Cabin");
            G(act, a, "camp", "Goblin Encampment");
            G(act, a, "pincer", "Ground Pincer");
            G(act, a, "behind", "Attack from Behind");
            G(act, a, "rushers", "Lots of Ground Rushers");
            G(act, a, "mortar", "Annoying Mortar");
            G(act, a, "finF", "Flyer Finale");
            Zone(act.Wave(W.Enter), act, a, "EntryHallway").Add(C.Goblin, 3, "entry");
            act.Wave(W.PrevCleared).Add(C.Imp, 2, "entry").Add(C.Goblin, 2, "entry");
            Zone(act.Wave(W.Enter), act, a, "Upper Hallway Flyers").Add(C.Imp, 5, "upperF");
            Zone(act.Wave(W.Enter), act, a, "Rumble at the Hunstmans Cabin").Add(C.Goblin, 3, "cabin").Add(C.EliteGoblin, 1, "cabin");
            Zone(act.Wave(W.Enter), act, a, "Goblin Encampment").Add(C.Goblin, 4, "camp").Add(C.Bomber, 1, "camp");
            Zone(act.Wave(W.Enter), act, a, "Ground Pincer").Add(C.EliteGoblin, 2, "pincer").Add(C.Goblin, 2, "pincer");
            var behind = Zone(act.Wave(W.Enter), act, a, "Attack from Behind").Add(C.EliteGoblin, 2, "behind");
            behind.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), act, a, "Lots of Ground Rushers").Add(C.Goblin, 5, "rushers").Add(C.Bomber, 1, "mortar");
            Zone(act.Wave(W.Enter), act, a, "Flyer Finale").Add(C.Bomber, 2, "mortar").Add(C.Imp, 3, "finF").Add(C.EliteImp, 1, "finF");
            done(act);
        }

        // ------------------------------------------------------------------ act 3

        /// <summary>
        /// Act 3 (level54, Quest_Goblin2_GoblinCamp): the Campsite, one large clearing with the campfire (Fire1..3 at x
        /// -99.4, z 70), two pillars with bombers (Pillar Dude Left / Back Left), three explosive barrels, the
        /// HuntingParty's regions behind (x -114..-126) and the exit at (-90.4, -9.4, 54.7). No trigger volumes: every
        /// fight is already in the camp. Arrival (-126.9, -9.0, 84.7) facing -z. Final: 2 bombers + 3 goblins; 5 more
        /// around the barrels; 3 elites behind + 2 bombers; flyers; a bomber on a pillar with 4 elites; a mixed last wave.
        /// </summary>
        private static IEnumerator Act3(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_GoblinCamp", new Vector3(-126.85f, -9.015f, 84.73f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 3", "The Campsite");
            G(act, a, "pillar", "InitialSpawns", "Pillar");
            G(act, a, "fire", "InitialSpawns", "Fire");
            G(act, a, "camp", "InitialSpawns", "Region");
            G(act, a, "hunt", "HuntingParty");
            act.Wave(W.ActStart).Add(C.Bomber, 2, "pillar").Add(C.Goblin, 3, "fire");
            act.Wave(W.PrevBelow, 1).Add(C.Goblin, 5, "camp");
            var behind = act.Wave(W.PrevBelow, 1).Add(C.EliteGoblin, 3, "hunt").Add(C.Bomber, 2, "pillar");
            behind.Toast = "Behind you!";
            act.Wave(W.PrevBelow, 1).Add(C.Imp, 3, "camp").Add(C.EliteImp, 1, "camp");
            act.Wave(W.PrevBelow, 1).Add(C.Bomber, 1, "pillar").Add(C.EliteGoblin, 4, "hunt");
            act.Wave(W.PrevCleared).Add(C.Goblin, 3, "camp").Add(C.Imp, 2, "camp").Add(C.EliteGoblin, 1, "hunt").Add(C.Bomber, 1, "pillar");
            done(act);
        }

        // ------------------------------------------------------------------ act 4

        /// <summary>
        /// Act 4 (level55, Quest_Goblin2_Forest2): the Deep Forest. Arrival (-225.0, -4.11, -106.9) facing +x into the
        /// Witch Stinger's volume; EntryHallway (z -95), the Big Forest Room Fight (z -69, two points up on the tree
        /// platform), Jerks Go Down Stairs (three volumes, z -47..-63), Throwdown at the Bridge (z -37), the Random
        /// Knight, PoolEntrance (z -19), Rumble in the Showers (z 9, 8 points and the elite patrol), the Back Attack in
        /// the Showers (z 25) and the exit at (-174.8, -11.4, 29.0). Final: imp witches at the spawn, then goblins and 2
        /// more witches; bombers on the stairwell tree platform; spawns behind you on the stairs; shield goblins and 5
        /// flyers; elites by position; bombers on platforms.
        /// </summary>
        private static IEnumerator Act4(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_Forest2", new Vector3(-225.01f, -4.113f, -106.91f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 4", "The Deep Forest");
            G(act, a, "entry", "EntryHallway", "Region");
            G(act, a, "big", "Big Forest Room Fight", "Region");
            G(act, a, "bigSP", "Big Forest Room Fight", "Spawn Points");
            G(act, a, "stairs", "Jerks Go Down Stairs");
            G(act, a, "bridge", "Throwdown at the Bridge");
            G(act, a, "knight", "Random Knight", "Spawn Regions");
            G(act, a, "pool", "PoolEntrance", "Region");
            G(act, a, "poolSP", "PoolEntrance", "Spawn Points");
            G(act, a, "showers", "Rumble in the Showers", "Spawn Points");
            G(act, a, "showersE", "Rumble in the Showers", "Elite");
            G(act, a, "backS", "Back Attack in the Showers");
            Zone(act.Wave(W.Enter), act, a, "Witch Stinger", 0, 2f).Add(C.ImpWitch, 1, "entry");
            Zone(act.Wave(W.Enter), act, a, "EntryHallway").Add(C.Goblin, 3, "entry").Add(C.ImpWitch, 2, "entry");
            Zone(act.Wave(W.Enter), act, a, "Big Forest Room Fight").Add(C.Bomber, 2, "bigSP").Add(C.Goblin, 2, "big");
            var stairs = Zone(act.Wave(W.Enter), act, a, "Jerks Go Down Stairs").Add(C.Goblin, 3, "stairs");
            stairs.Toast = "Behind you!";
            Zone(act.Wave(W.Enter), act, a, "Throwdown at the Bridge").Add(C.Shield, 3, "bridge").Add(C.Imp, 5, "bridge");
            Zone(act.Wave(W.Enter), act, a, "Random Knight").Add(C.KnightGreen, 1, "knight");
            Zone(act.Wave(W.Enter), act, a, "PoolEntrance").Add(C.EliteGoblin, 2, "pool").Add(C.Bomber, 1, "poolSP");
            Zone(act.Wave(W.Enter), act, a, "Rumble in the Showers").Add(C.EliteGoblin, 2, "showersE").Add(C.Goblin, 3, "showers").Add(C.Bomber, 2, "showers");
            var back = Zone(act.Wave(W.Enter), act, a, "Back Attack in the Showers").Add(C.Goblin, 3, "backS").Add(C.Imp, 2, "backS");
            back.Toast = "Behind you!";
            done(act);
        }

        // ------------------------------------------------------------------ act 5

        /// <summary>
        /// Act 5 (level56, Quest_Goblin2_Bog): arrival (-151.3, -30.25, -68.6) facing +x. The swamp water is Rec Room's
        /// three [QuestLavaFloor] boxes (surface about -31.1: a down); 18 [FloatingPalletPlatform] floats (their 1.2 m
        /// collider plates) and a large rock on the right cross it. Encounter0 is waiting (the imp horde; MortarSpawn x2
        /// up at -24), Encounter1's two "witch" points (x -135..-143, z -92), Encounter2 and 3 with a Mortar point each
        /// (z -107), Encounter4 (x -82..-101), the BogMonsterEncounter's small volume on the exit pad (-90.3, -83.6) with
        /// its BossSpawnPoint in the water (-103.8, -31.1, -91.9), and the exit at (-87.9, -30.3, -83.7). Final: a horde
        /// of imps; 2 bombers; flyers; an imp witch, then 2 more; everything including elites and bombers; then "a lowly
        /// bog monster will rise up from its home".
        /// </summary>
        private static IEnumerator Act5(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_Bog", new Vector3(-151.27f, -30.248f, -68.55f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 5", "The Bog");
            // The bog monster rises from the pool of [QuestLavaFloor] (2) (Rec Room's boxes are the goblins' nav zones and
            // the players' down): that pool stays harmless, or the framework's lava would sink the monster as it rises.
            var home = a.Markers("enemy_spawner", "BossSpawnPoint");
            if (home.Count > 0)
            {
                Vector3 h = home[0].Position;
                int n = act.Lava.RemoveAll(b => h.x >= b.min.x && h.x <= b.max.x && h.z >= b.min.z && h.z <= b.max.z);
                RrLog.Info("CrimsonCauldron", a.Scene + ": the bog monster's pool (" + n + " water box) is not a down");
            }

            G(act, a, "e0", "Encounter0", "Region", true);
            G(act, a, "e0M", "Encounter0", "Mortar");
            G(act, a, "e1", "Encounter1", "Region", true);
            G(act, a, "e1W", "Encounter1", "witch", true);
            G(act, a, "e2", "Encounter2", "Region", true);
            G(act, a, "e2M", "Encounter2", "Mortar");
            G(act, a, "e3", "Encounter3", "Region");
            G(act, a, "e3F", "Encounter3", "Region", true);
            G(act, a, "e3M", "Encounter3", "Mortar");
            G(act, a, "e4", "Encounter4");
            G(act, a, "monster", "BogMonsterEncounter", "BossSpawnPoint");
            act.Wave(W.ActStart).Add(C.Imp, 6, "e0");
            act.Wave(W.PrevBelow, 2).Add(C.Bomber, 2, "e0M");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(C.ImpWitch, 1, "e1W").Add(C.Imp, 2, "e1");
            act.Wave(W.PrevCleared).Add(C.ImpWitch, 2, "e1W");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(C.Bomber, 1, "e2M").Add(C.Imp, 3, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(C.EliteGoblin, 2, "e3").Add(C.Bomber, 1, "e3M").Add(C.EliteImp, 1, "e3F").Add(C.Goblin, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(C.Goblin, 3, "e4").Add(C.Shield, 2, "e4").Add(C.EliteImp, 1, "e4");
            Zone(act.Wave(W.Enter), act, a, "BogMonsterEncounter", 0, 3f).Add(C.BogMonster, 1, "monster");
            done(act);
        }

        // ------------------------------------------------------------------ act 6

        /// <summary>
        /// Act 6 (level57, Quest_Goblin2_Mines1): the Mines. Arrival (123.5, 5.26, -109.7) facing -x. Every encounter sits
        /// under Systems/Encounters/New: Enemies around first corner (x 97, z -99..-105; its volume 21.5 x 13 x 28.8 m at
        /// (96.3, 3.3, -109.5)), Throwdown at the top of the stairs (4 points, x 93, z -69..-76), Ground rush from downstairs
        /// (volumes at z -86 / -89), Enemies in place all over the lower level (7 Door Guardians at x 83, the ping-pong
        /// goblins; 2 Knights at x 110) and the ElitesFinale regions (x 97, z -58) with the last volume over the exit
        /// platform (74.5, -5.2, -37.3). Five [Quest_SteamTrap] vents (<see cref="RrCrimsonSteam"/>), five explosive
        /// barrels, two ladders; exit at (75.5, -5.3, -48.0). Final: "the steam kills you"; 4 cannon goblins; 5 flyers by
        /// an explosive barrel; the ping-pong goblins; many shield goblins; a bomber and 2 green barrel knights; 4 elite
        /// goblins behind you at the exit platform.
        /// </summary>
        private static IEnumerator Act6(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_Mines1", new Vector3(123.49f, 5.257f, -109.71f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 6", "The Mines");
            G(act, a, "corner", "New", "first corner");
            G(act, a, "stairs", "New", "top of the stairs/Spawn Regions");
            G(act, a, "stairsSP", "New", "top of the stairs/Spawn Points");
            G(act, a, "rush", "New", "Ground rush");
            G(act, a, "lower", "New", "lower level/Spawn Regions");
            G(act, a, "guards", "New", "Door Guardians");
            G(act, a, "knights", "New", "Knights");
            G(act, a, "elites", "New", "ElitesFinale");
            Zone(act.Wave(W.Enter), act, a, "New", 3).Add(C.Goblin, 3, "corner").Add(C.Bomber, 2, "corner");
            act.Wave(W.PrevBelow, 1).Add(C.Imp, 5, "stairs");
            Zone(act.Wave(W.Enter), act, a, "New", 1).Add(C.Bomber, 2, "stairsSP").Add(C.Goblin, 3, "rush");
            Zone(act.Wave(W.Enter), act, a, "New", 2).Add(C.Shield, 4, "lower").Add(C.Goblin, 2, "guards");
            act.Wave(W.PrevBelow, 1).Add(C.Bomber, 1, "guards").Add(C.KnightGreen, 2, "knights");
            var behind = Zone(act.Wave(W.Enter), act, a, "New", 0).Add(C.EliteGoblin, 4, "elites");
            behind.Toast = "Behind you!";
            done(act);
        }

        // ------------------------------------------------------------------ act 7

        /// <summary>
        /// Act 7 (level58, Quest_Goblin2_BoilerRoom): the Deep Mines. Arrival (-156.8, 1.21, 99.4) facing -z. Factory
        /// Workers in place (x -183, z 90; the SupervisorCrew's 6 points up at y 2), FinaleSetup (the ramp: 6 points at x
        /// -221, z 69; its volume 18.8 x 18 x 71 m), KnightWithFlyerBackup / KnightWithGroundBackup (one volume at (-198,
        /// 0.5, 63), a knight point each), FinaleWaves (x -209..-236, z 20-80) and the exit at (-210.5, -2.1, 50.9). The
        /// boiler pit is a [QuestLavaFloor] (37 x 39 m, surface about -3.7); ten steam vents; twenty explosive barrels.
        /// Final: 6 elite goblins + 1 elite (red) barrel knight at the top of a ramp; two green barrel knights - killing
        /// one brings runners, the other flyers; more flyers + 2 elites by the barrels.
        /// </summary>
        private static IEnumerator Act7(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_BoilerRoom", new Vector3(-156.78f, 1.212f, 99.44f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 7", "The Deep Mines");
            G(act, a, "workers", "Factory Workers in place", "Region");
            G(act, a, "crew", "SupervisorCrew");
            G(act, a, "setup", "FinaleSetup", "Spawn Points");
            G(act, a, "setupR", "FinaleSetup", "Region");
            G(act, a, "kFly", "KnightWithFlyerBackup", "Spawn Point");
            G(act, a, "kFlyR", "KnightWithFlyerBackup", "Region", true);
            G(act, a, "kGround", "KnightWithGroundBackup", "Spawn Point");
            G(act, a, "kGroundR", "KnightWithGroundBackup", "Region");
            G(act, a, "fin", "FinaleWaves", "Region", true);
            G(act, a, "finSP", "FinaleWaves", "Spawn Points");
            Zone(act.Wave(W.Enter), act, a, "Factory Workers in place").Add(C.Goblin, 3, "workers").Add(C.Bomber, 2, "crew");
            Zone(act.Wave(W.Enter), act, a, "FinaleSetup").Add(C.EliteGoblin, 6, "setup").Add(C.KnightRed, 1, "setupR");
            Zone(act.Wave(W.Enter), act, a, "KnightWithFlyerBackup").Add(C.KnightGreen, 1, "kFly");
            act.Wave(W.PrevCleared).Add(C.Imp, 4, "kFlyR");
            act.Wave(W.PrevCleared).Add(C.KnightGreen, 1, "kGround");
            act.Wave(W.PrevCleared).Add(C.Goblin, 4, "kGroundR");
            Zone(act.Wave(W.Enter), act, a, "FinaleWaves").Add(C.Imp, 3, "fin").Add(C.EliteImp, 1, "fin").Add(C.EliteGoblin, 2, "finSP");
            done(act);
        }

        // ------------------------------------------------------------------ act 8

        /// <summary>
        /// Act 8 (level59, Quest_Goblin2_BellTowerStairs): the tower's stairs and ladders, 35 m from the ground floor to
        /// the door at the top. Arrival (-39.7, -20.1, 81.6) facing -z; the Ground Floor Workers (6 points at y -18, the
        /// elite patrol at -15), the Pincer's regions (y -5, z 63) with its volume at (-39.8, -6.3, 66.8), the Creak volume
        /// high up (-46.2, 6.2, 58.5), four ladders (<see cref="RrCrimsonLadder"/>), seven explosive barrels, the exit at
        /// (-37.9, 14.6, 51.6). Final: climb slowly - one player guards the stairs, another clears the imps at the top.
        /// </summary>
        private static IEnumerator Act8(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_BellTowerStairs", new Vector3(-39.72f, -20.104f, 81.61f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 8", "The Bell Tower Stairs");
            G(act, a, "ground", "Ground Floor Workers", "Spawn Points");
            G(act, a, "elite", "Ground Floor Workers", "ElitePatrol");
            G(act, a, "groundR", "Ground Floor Workers", "Region");
            G(act, a, "pincer", "Pincer");
            Bounds creak;
            if (K.Zone(a, "Creak", 0, out creak))
            {
                var c = creak.center;
                act.Group("creak", c, c + new Vector3(0f, 0f, 8f), c - new Vector3(0f, 0f, 8f));
            }
            else
            {
                G(act, a, "creak", "Pincer");
            }

            act.Wave(W.ActStart).Add(C.Goblin, 4, "ground").Add(C.EliteGoblin, 2, "elite");
            act.Wave(W.PrevBelow, 1).Add(C.Imp, 3, "groundR");
            var up = Zone(act.Wave(W.Enter), act, a, "Pincer").Add(C.Goblin, 3, "pincer").Add(C.Imp, 3, "pincer").Add(C.Bomber, 1, "pincer");
            up.Toast = "They're coming up the stairs!";
            Zone(act.Wave(W.Enter), act, a, "Creak").Add(C.Imp, 4, "creak").Add(C.EliteImp, 1, "creak");
            done(act);
        }

        // ------------------------------------------------------------------ act 9

        /// <summary>
        /// Act 9 (level60, Quest_Goblin2_BellTowerArena): the Finale in the belfry. Arrival (167.3, -9.08, -93.1) facing
        /// -z below the arena, two ladders up; the Witch Boss Encounter: the WitchSpawnPoint (142.8, -4.0, -98.7), nine
        /// WitchPoints around (149, -1, -99), the adds' spawn points East / North / West (3 each at y -1.8); the four
        /// GiftBoxSpawnPoints (the rewards) at x 145; Rec Room's PitKillzone below the windows (top about -13.1: a fall
        /// out of the tower is a down). No exit: the victory screen follows the witch.
        /// </summary>
        private static IEnumerator Act9(C room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = K.Area("Crimson_BellTowerArena", new Vector3(167.33f, -9.075f, -93.05f));
            yield return Build(a, r, s);
            var act = Finish(room, a, r, "ACT 9", "The Crimson Witch", false);
            act.Boss = true;
            var spawn = a.Markers("witch_spawn");
            act.Group("witch", spawn.Count > 0 ? spawn[0].Position : a.L(142.8f, -4.0f, -98.7f));
            G(act, a, "east", "Witch Boss Encounter", "East");
            G(act, a, "north", "Witch Boss Encounter", "North");
            G(act, a, "west", "Witch Boss Encounter", "West");
            act.Wave(W.ActStart).Add(C.Witch, 1, "witch");
            foreach (var g in a.Markers("giftbox"))
            {
                room.Chests.Add(RrQuestKit.Chest(r, g.Position, g.Rotation.eulerAngles.y));
            }

            done(act);
        }
    }
}
