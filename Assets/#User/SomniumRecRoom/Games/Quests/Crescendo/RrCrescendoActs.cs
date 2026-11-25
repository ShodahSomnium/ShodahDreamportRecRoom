using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using W = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestWave.When;
using C = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo.RrCrescendoRoom;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Crescendo
{
    /// <summary>
    /// The Foyer and the 13 acts of Crescendo of the Blood Moon on Rec Room's own levels, with the fights of the final
    /// version (rr_latest_rgames_story.md 7.1, the wiki's act order and walkthrough) placed on each level's own encounters
    /// (Systems/Encounters, RequiredEncounters: their spawn points and regions are the spawn groups, their Overlap Volume
    /// the zone that starts them; names as the level has them, DN/iter13/crescendo_tools/cbm_markers.pl). Rec Room's wave
    /// contents live in circuits the build cannot read: the counts below follow the wiki's descriptions and the level's own
    /// spawn names (BoneThrower, RangedBat, Swoop Bat, Musket, Wolf...), for a full party of 3 (the framework scales them).
    /// Each level's anchor is its arrival (the centre of its PlayerSpawnPoints, scene coordinates in each method).
    /// </summary>
    public static class RrCrescendoActs
    {
        public delegate IEnumerator Builder(C room, C.Level level, Dictionary<string, Material> surfaces, Action<RrQuestAct> done);

        /// <summary>The 13 acts in play order (the Grand Hall's four visits share one level).</summary>
        public static Builder[] All()
        {
            return new Builder[] { Township, Carriage, CastleGrounds, Hall1, Gallery, Library, Hall2, Park, Ballroom, Hall3, Dungeon, Hall4, Concert };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Builds a level's area from its data file (Data/&lt;scene&gt;.bytes), then its breakables, weapons and gates.</summary>
        private static IEnumerator Build(C room, C.Level level, string scene, Vector3 anchor, Dictionary<string, Material> s, bool bigSky = false)
        {
            var a = new RrJumbotronArea(scene, anchor) { GridToCore = bigSky };
            level.Area = a;
            string raw = RrSceneBank.Text(scene);
            if (raw == null || level.Root == null)
            {
                RrLog.Warn("Crescendo", scene + ": no scene data, the level is left empty");
                yield break;
            }

            yield return a.Build(raw, level.Root, s);
            Collectables(room, level, 20);
            Weapons(room, level);
        }

        /// <summary>An act on a built level: arrival, area, title.</summary>
        private static RrQuestAct NewAct(C.Level level, string title, string subtitle)
        {
            var r = level.Root;
            var act = new RrQuestAct { Title = title, Subtitle = subtitle, Root = r, Origin = r.localPosition };
            var a = level.Area;
            Vector3 at;
            float yaw;
            if (a != null && a.Arrival(out at, out yaw))
            {
                act.Entry = at;
                act.EntryYaw = yaw;
            }

            act.Area = a != null ? a.Area() : new Bounds(Vector3.zero, new Vector3(60f, 20f, 60f));
            return act;
        }

        /// <summary>
        /// Rec Room's silver breakables at their poses (rr_quests.md 2.4: the candelabra 3, small candle 2, wall sconce 2,
        /// jack-o-lantern 3): at most cap per level, spread evenly over the level's list (the load stays moderate).
        /// </summary>
        private static void Collectables(C room, C.Level l, int cap)
        {
            var list = new List<RrSceneMarker>();
            foreach (var m in l.Area.Markers("weapon_pickup"))
            {
                string p = m.Get("prefab") ?? m.Name;
                if (p.StartsWith("(Quest_Dracula1_Collectable", StringComparison.Ordinal))
                {
                    list.Add(m);
                }
            }

            int step = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)Mathf.Max(1, cap)));
            for (int i = 0; i < list.Count; i += step)
            {
                var m = list[i];
                string p = m.Get("prefab") ?? m.Name;
                int silver = p.Contains("Large") || p.Contains("Pumpkin") ? 3 : 2;
                room.Pots.Add(l.Root, p, m.Position, m.Rotation.eulerAngles.y, silver);
            }
        }

        /// <summary>
        /// The level's weapons where it lays them (shovels, pitchforks and whips in hay piles and racks, blunderbusses, the
        /// Lower Sanctum's rum jugs and bottles), plus one weapon in each of up to three of its silver chests (Rec Room's
        /// chests hold the flintlock and the crossbows: rotated through the level list), refilled 3 s after they are taken.
        /// </summary>
        private static void Weapons(C room, C.Level l)
        {
            var prefabs = new List<string>();
            var at = new List<Vector3>();
            var rot = new List<Quaternion>();
            foreach (var m in l.Area.Markers("weapon_pickup"))
            {
                string p = m.Get("prefab") ?? m.Name;
                if (p.StartsWith("(Quest_Dracula1_Collectable", StringComparison.Ordinal))
                {
                    continue;
                }

                // Only the install's spawnable "(Name)" prefabs: the level's "[Quest_Dracula1_Plate] (9)",
                // "[Quest_Dracula1_MirrorCannon]", "[Quest_Pirate1_Torch] (1)" and "Crossbow Trap (1)" are scene objects
                // with no prefab in the bundles (iteration 13 tour: each logged "could not spawn ... for a rack").
                if (!p.StartsWith("(", StringComparison.Ordinal))
                {
                    continue;
                }

                prefabs.Add(p);
                at.Add(m.Position + Vector3.up * 0.05f);
                rot.Add(m.Rotation);
            }

            var chest = new[] { C.Flintlock, C.Crossbow, C.HunterCrossbow, C.BlunderBuss };
            int n = 0;
            foreach (var m in l.Area.Markers("chest"))
            {
                if (n >= 3)
                {
                    break;
                }

                prefabs.Add(chest[(n + room.Levels.Count) % chest.Length]);
                at.Add(m.Position + Vector3.up * 0.75f);
                rot.Add(Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f));
                n++;
            }

            if (prefabs.Count > 0)
            {
                room.AddRack(RrQuestRack.AtPoses(l.Root, "Weapons " + l.Area.Scene, prefabs.ToArray(), at.ToArray(), rot.ToArray()));
            }
        }

        /// <summary>A mid-level gate by the level's name, opening once `after` waves of the act have started and been beaten.</summary>
        private static void Gate(C.Level l, string name, int after)
        {
            var door = l.Area.Marker("gate_door", name);
            if (door == null)
            {
                RrLog.WarnOnce("Crescendo", "gate:" + l.Area.Scene + name, l.Area.Scene + ": no gate " + name);
                return;
            }

            var g = RrCrescendoGate.Create(l.Root, door, l.Area.Marker("gate_block", name), l.Area.Anchor);
            if (g != null)
            {
                l.Gates.Add(g);
                l.GateAfter.Add(after);
            }
        }

        /// <summary>A trigger volume as an act-local axis-aligned box (the level's volumes are rotated boxes; size= is their full size).</summary>
        private static bool TryZone(RrJumbotronArea a, string enc, out Bounds zone, int index = 0)
        {
            zone = default(Bounds);
            int n = 0;
            foreach (var m in a.Markers("enc_trigger"))
            {
                if (m.Name != enc || n++ < index)
                {
                    continue;
                }

                Vector3 size;
                if (!RrCrescendoGate.TryVec(m.Get("size"), out size))
                {
                    size = new Vector3(8f, 6f, 8f);
                }

                Vector3 e = size * 0.5f;
                var r = m.Rotation;
                Vector3 ax = r * new Vector3(e.x, 0f, 0f), ay = r * new Vector3(0f, e.y, 0f), az = r * new Vector3(0f, 0f, e.z);
                var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
                zone = new Bounds(m.Position, ext * 2f);
                return true;
            }

            return false;
        }

        private static Vector3 Ahead(RrQuestAct act, float m)
        {
            return act.Entry + Quaternion.Euler(0f, act.EntryYaw, 0f) * new Vector3(0f, 0f, m);
        }

        /// <summary>An Enter wave in an encounter's Overlap Volume (fallback: around its spawn points, else 12 m ahead).</summary>
        private static RrQuestWave Enter(RrQuestAct act, RrJumbotronArea a, string enc, int index = 0)
        {
            var w = act.Wave(W.Enter);
            Bounds z;
            if (TryZone(a, enc, out z, index))
            {
                return w.In(z.center, z.size);
            }

            var pts = a.Points(enc);
            if (pts.Count > 0)
            {
                z = Around(pts);
                RrLog.Info("Crescendo", a.Scene + ": " + enc + " has no trigger volume; its zone is around its " + pts.Count + " spawn points");
                return w.In(z.center, z.size);
            }

            RrLog.WarnOnce("Crescendo", "zone:" + a.Scene + enc, a.Scene + ": nothing for " + enc + "; a 12 m zone ahead of the arrival");
            return w.In(Ahead(act, 12f), new Vector3(12f, 6f, 12f));
        }

        /// <summary>The box around points, 6 m more on each side and 4 m up and down.</summary>
        private static Bounds Around(List<Vector3> pts)
        {
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts)
            {
                b.Encapsulate(p);
            }

            b.Expand(new Vector3(12f, 8f, 12f));
            return b;
        }

        /// <summary>
        /// A spawn group from an encounter's points whose group path or name contains filter (all of the encounter when
        /// none match; else ahead of the arrival). Flyers' points are lowered 1 m (the director lifts flyers 2.5 m).
        /// </summary>
        private static string G(RrQuestAct act, RrJumbotronArea a, string name, string enc, string filter = null, bool fly = false)
        {
            var pts = a.Points(enc, filter);
            if (pts.Count == 0 && filter != null)
            {
                pts = a.Points(enc);
            }

            if (pts.Count == 0)
            {
                RrLog.WarnOnce("Crescendo", "group:" + a.Scene + enc + filter, a.Scene + ": no spawn points for " + enc + " / " + filter);
                pts.Add(Ahead(act, 12f));
            }

            if (fly)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    pts[i] += Vector3.down;
                }
            }

            act.Group(name, pts.ToArray());
            return name;
        }

        /// <summary>
        /// The exit on a gather pad (Rec Room's [DoorTrigger]: players stand on it to leave): the framework's door on the
        /// pad, its medieval parts removed but its glow kept, and a zone of the pad's size from 1.5 m under it to 8 m over
        /// it (some pads sit on stage pieces that rise out of the floor). padPath: a part of the pad's level path.
        /// </summary>
        private static RrQuestDoor PadExit(C.Level l, string padPath)
        {
            RrSceneMarker pad = null;
            foreach (var m in l.Area.Markers("door_trigger"))
            {
                string p = m.Get("path") ?? string.Empty;
                if (p.IndexOf(padPath, StringComparison.OrdinalIgnoreCase) >= 0 || m.Name == padPath)
                {
                    pad = m;
                    break;
                }
            }

            if (pad == null)
            {
                RrLog.Warn("Crescendo", l.Area.Scene + ": no exit pad " + padPath);
                return null;
            }

            Vector3 size;
            if (!RrCrescendoGate.TryVec(pad.Get("size"), out size))
            {
                size = new Vector3(4f, 0.2f, 3f);
            }

            var door = Door(l.Root, pad.Position, pad.Rotation.eulerAngles.y);
            door.Zone = new Bounds(new Vector3(0f, 3.25f, 0f), new Vector3(size.x + 1.2f, 9.5f, size.z + 1.2f));
            return door;
        }

        /// <summary>The exit on a pad (<see cref="PadExit"/>), or, when the level has none, an 8 m zone at the arrival (the act can always be left).</summary>
        private static RrQuestDoor Exit(C.Level l, RrQuestAct act, string padPath)
        {
            return PadExit(l, padPath) ?? ZoneExit(l, new Bounds(act.Entry + Vector3.up * 2f, new Vector3(8f, 8f, 8f)));
        }

        /// <summary>The exit as a zone (an act-local box): the carriage, the Outro volume.</summary>
        private static RrQuestDoor ZoneExit(C.Level l, Bounds zone)
        {
            var door = Door(l.Root, zone.center, 0f);
            door.Zone = new Bounds(Vector3.zero, zone.size);
            return door;
        }

        private static RrQuestDoor Door(Transform parent, Vector3 at, float yaw)
        {
            var door = RrQuestDoor.Create(parent, at, yaw, 3.4f, 3f);
            for (int i = door.transform.childCount - 1; i >= 0; i--)
            {
                var c = door.transform.GetChild(i);
                if (c.name != "Glow")
                {
                    UnityEngine.Object.Destroy(c.gameObject);
                }
            }

            return door;
        }

        // ------------------------------------------------------------------ the Foyer (lobby)

        /// <summary>
        /// The Foyer (level79, Quest_Dracula1_Foyer): the quest's theatre foyer. Player1..8SpawnPoint at x -162.7..-163.8
        /// facing -x (the room origin is their centre); the start hologram [ScoreboardProjector] (1) at (-171.36, -0.62,
        /// 250.99), the Rec Center [ActivityDoor] behind the spawns (-159.62, -0.62, 254.80, facing -x), the gift shop
        /// [DraculaStorefront] (not built: no silver to spend here), hay piles with shovels, a pitchfork and three whips,
        /// jack-o-lanterns and candles. Ours: <see cref="RrGameConsole"/> on the hologram's marker, a Rec Center portal where
        /// the activity door is (its Rec Room meshes are left out so the two do not overlap), the practice weapons.
        /// </summary>
        public static IEnumerator Foyer(C room, Transform t, List<Pose> spawns, Dictionary<string, Material> surfaces)
        {
            var level = new C.Level { Root = t };
            var a = new RrJumbotronArea(C.FoyerScene, new Vector3(-163.23f, -0.62f, 254.87f));
            foreach (var n in new[] { "ActivityDoor_Left", "ActivityDoor_Right", "ActivityDoor_Emit", "ActivityDoor_Glow", "ActivityDoor_Frame" })
            {
                a.Skip.Add(n);
            }

            level.Area = a;
            string raw = RrSceneBank.Text(C.FoyerScene);
            if (raw != null)
            {
                yield return a.Build(raw, t, surfaces);
            }

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
                room.LobbyStartButton(t, a.L(-171.36f, -0.62f, 250.99f), -90f);
            }

            foreach (var p in a.Markers("portal"))
            {
                RrPortal.Create(t, RrActivity.RecCenter, p.Position, p.Rotation.eulerAngles.y, "REC CENTER");
            }

            if (a.Kit != null)
            {
                Collectables(room, level, 20);
                Weapons(room, level);
            }
        }

        // ------------------------------------------------------------------ act 1: Burning Village

        /// <summary>
        /// Act 1 (level80, Quest_Dracula1_Township): Burning Village. The team spawns in a house at the west end
        /// (Player7SpawnPoint (1..4), x 122-126). E1 "Pumpkin Patch Rumble" (no trigger: at once) - Rec Room's 3 waves of
        /// Pumpkin Spicers - then the gate [Quest_Dracula1_MidLevelGate] (1) at z -40 into E3 "Midtown Madness" (bone throwers
        /// on the balconies, ranged and swooping bats, melee pincers), the gate [Quest_Dracula1_MidLevelGate] at z -66 into
        /// E4 "Weapon Shop Alley" (the chests, whips and blunderbusses; a maid bursts out of the shop), E5 "Town Square"
        /// (bats and pumpkin heads over the hill), and the Outro: the carriage at (191, 6.4, -80) takes the team away.
        /// </summary>
        private static IEnumerator Township(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Township", new Vector3(123.81f, 9.56f, -85.49f), s);
            var a = l.Area;
            var act = NewAct(l, "BURNING VILLAGE", "Act 1");
            const string e1 = "E1: Pumpkin Patch Rumble", e3 = "E3: Midtown Madness", e4 = "E4: Weapon Shop Alley", e5 = "E5: Town Square";
            G(act, a, "e1", e1, "Spawn Points");
            G(act, a, "e1bats", e1, "Bats", true);
            G(act, a, "e3maid", e3, "Bone");
            G(act, a, "e3ranged", e3, "Ranged", true);
            G(act, a, "e3swoop", e3, "Swoop", true);
            G(act, a, "e3melee", e3, "Melee");
            G(act, a, "e4", e4);
            G(act, a, "e5maid", e5, "BoneThrower");
            G(act, a, "e5melee", e5, "Melee");
            G(act, a, "e5bats", e5, "Bats", true);
            act.Wave(W.ActStart).Add(C.Spicer, 3, "e1");
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 3, "e1");
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 3, "e1").Add(C.FireBat, 2, "e1bats");
            Enter(act, a, e3).Add(C.Maid, 2, "e3maid").Add(C.FireBat, 2, "e3ranged").Add(C.Spicer, 2, "e3melee");
            act.Wave(W.PrevBelow, 1).Add(C.DiveBat, 2, "e3swoop").Add(C.Spicer, 3, "e3melee").Add(C.Maid, 1, "e3maid");
            var shop = Enter(act, a, e4).Add(C.Maid, 1, "e4").Add(C.Spicer, 3, "e4");
            shop.Toast = "A maid bursts out of the shop!";
            Enter(act, a, e5).Add(C.DiveBat, 3, "e5bats").Add(C.Spicer, 3, "e5melee").Add(C.Maid, 2, "e5maid");
            act.Wave(W.PrevBelow, 1).Add(C.DiveBat, 2, "e5bats").Add(C.Spicer, 3, "e5melee");
            Gate(l, "[Quest_Dracula1_MidLevelGate] (1)", 3);
            Gate(l, "[Quest_Dracula1_MidLevelGate]", 5);
            Bounds outro;
            act.ExitDoor = TryZone(a, "Outro", out outro) ? ZoneExit(l, outro) : Exit(l, act, "Outro");
            act.ArrivalToast = "Grab a shovel or a pitchfork from the hay";
            done(act);
        }

        // ------------------------------------------------------------------ act 2: Forest Road

        /// <summary>
        /// Act 2 (level81, Quest_Dracula1_CarriageRide): Forest Road, the carriage ride. The team stands on the carriage
        /// (PlayerSpawnPoint at (220.0, -5.8, 143.5)); Rec Room pans the forest past it (PanningTreesLeft) while
        /// EnemyWaves spawn from its six regions around the carriage (Front, Side and Back, left and right): swooping and
        /// fire bats, and the Barrel Dogs are introduced ("easily the hardest level in solo mode"). Here the carriage
        /// stands still. When the waves are beaten the carriage crashes at the castle: the team goes straight on.
        /// </summary>
        private static IEnumerator Carriage(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_CarriageRide", new Vector3(220.02f, -5.82f, 143.53f), s);
            var a = l.Area;
            var act = NewAct(l, "FOREST ROAD", "Act 2");
            const string ew = "EnemyWaves";
            G(act, a, "leftF", ew, "Left", true);
            G(act, a, "rightF", ew, "Right", true);
            G(act, a, "front", ew, "Front");
            G(act, a, "side", ew, "Side");
            G(act, a, "backF", ew, "Back", true);
            act.Wave(W.ActStart).Add(C.DiveBat, 2, "leftF").Add(C.DiveBat, 2, "rightF");
            act.Wave(W.PrevBelow, 1).Add(C.Dog, 2, "front").Add(C.FireBat, 2, "backF");
            act.Wave(W.PrevBelow, 1).Add(C.Dog, 2, "side").Add(C.DiveBat, 3, "leftF");
            act.Wave(W.PrevBelow, 1).Add(C.Dog, 3, "front").Add(C.DiveBat, 2, "rightF").Add(C.FireBat, 2, "backF");
            act.ExitDoor = ZoneExit(l, new Bounds(act.Entry + Vector3.up * 2f, new Vector3(40f, 14f, 40f)));
            done(act);
        }

        // ------------------------------------------------------------------ act 3: Castle Grounds

        /// <summary>
        /// Act 3 (level82, Quest_Dracula1_CastleEntry): Castle Grounds, after the carriage crash (the wrecked carriages by
        /// the spawn at (355.8, -241.0, -224.2)). Marksmen waiting on the stairs and rocks (Encounter1: InPlaceMuskets,
        /// PatrolOnStairs), the battles under, on and over the bridge (Encounter4-a/b/c, with maids and Barrel Dogs), the
        /// finale before the drawbridge (Encounter5-a: muskets on the gate and the stairs; 5-b the drawbridge deck) and the
        /// door into the castle ([DoorTrigger] (1) at (336.3, -231.7, -312.4); Rec Room's whip tears it down).
        /// </summary>
        private static IEnumerator CastleGrounds(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_CastleEntry", new Vector3(355.84f, -240.95f, -224.22f), s, true);
            var a = l.Area;
            var act = NewAct(l, "CASTLE GROUNDS", "Act 3");
            const string m1 = "Encounter1-aInPlaceMuskets", st = "Encounter1-aPatrolOnStairs", ub = "Encounter4-aUnderBridgeBattle", br = "Encounter4-bBridgeBattle",
                op = "Encounter4-cOverPassBattle", fin = "Encounter5-aFinaleSetup", db = "Encounter5-bDrawBridge";
            G(act, a, "m1", m1);
            G(act, a, "stM", st, "ExplicitSpawn");
            G(act, a, "stS", st, "Spawn Regions");
            G(act, a, "ubMouth", ub, "Bridge");
            G(act, a, "ubW2", ub, "wave2");
            G(act, a, "brM", br, "Spawn Points");
            G(act, a, "brW3", br, "wave3");
            G(act, a, "opM", op, "Spawn Points");
            G(act, a, "opMelee", op, "Melees");
            G(act, a, "finM", fin, "Musket");
            G(act, a, "finStairs", fin, "Stair");
            G(act, a, "finUp", fin, "Up");
            G(act, a, "deckF", db, "Deck", true);
            act.Wave(W.ActStart).Add(C.Marksman, 2, "m1");
            Enter(act, a, st).Add(C.Marksman, 2, "stM").Add(C.Spicer, 2, "stS");
            Enter(act, a, ub).Add(C.Spicer, 3, "ubMouth").Add(C.Dog, 2, "ubW2");
            Enter(act, a, br).Add(C.Marksman, 2, "brM").Add(C.Maid, 2, "brW3").Add(C.Dog, 1, "ubW2");
            Enter(act, a, op).Add(C.Marksman, 2, "opM").Add(C.Spicer, 3, "opMelee");
            Enter(act, a, fin).Add(C.Marksman, 3, "finM").Add(C.Spicer, 3, "finStairs").Add(C.Maid, 2, "finUp");
            act.Wave(W.PrevBelow, 1).Add(C.Dog, 3, "finStairs").Add(C.DiveBat, 2, "deckF");
            act.ExitDoor = Exit(l, act, "Doors/ToCastle");
            done(act);
        }

        // ------------------------------------------------------------------ acts 4, 7, 10, 12: the Grand Hall

        private static IEnumerator Hall1(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            return Hall(room, l, s, done, 1, "Doors/Library", "The next door: 2nd floor, right");
        }

        private static IEnumerator Hall2(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            return Hall(room, l, s, done, 2, "Doors/Park", "The garden door: 1st floor, right");
        }

        private static IEnumerator Hall3(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            return Hall(room, l, s, done, 3, "Doors/Dungeon", "The Lower Sanctum: far rear, 1st floor");
        }

        private static IEnumerator Hall4(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            return Hall(room, l, s, done, 4, "Doors/Dracula", "The Concert Hall: 2nd floor, back room");
        }

        /// <summary>
        /// The Grand Hall (level83, Quest_Dracula1_CastleMainFoyer), visit 1-4: the hub between the levels (built on the first
        /// visit, the same level for all four). Spawn (-192.65, -182.66, -93.63) facing the hall; 17 chests, candles and
        /// candelabras (the silver breakables), the organ benches on the balconies. No fights: arriving banks the silver
        /// and makes the hall the checkpoint (<see cref="RrCrescendoRoom"/>), then the visit's own door opens - the four
        /// [DoorTrigger] pads under Systems/Doors: Library (2nd floor right, to the Gallery), Park (1st floor right), Dungeon
        /// (1st floor far rear) and Dracula (2nd floor back room, to the Concert Hall).
        /// </summary>
        private static IEnumerator Hall(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done, int visit, string pad, string toast)
        {
            if (l.Area == null)
            {
                yield return Build(room, l, "Crescendo_CastleMainFoyer", new Vector3(-192.65f, -182.66f, -93.63f), s, true);
            }

            var act = NewAct(l, "GRAND HALL", visit == 1 ? "The castle's hub" : "Visit " + visit);
            act.ExitDoor = Exit(l, act, pad);
            act.ArrivalToast = toast;
            done(act);
        }

        // ------------------------------------------------------------------ act 5: Gallery

        /// <summary>
        /// Act 5 (level84, Quest_Dracula1_Gallery): the Gallery. Spawn (75.5, 4.5, 169.7); pumpkin heads follow the team down
        /// the hall (Encounter1-FollowMelees, at once), an ambush in the stair room (Encounter3), the band room below
        /// (Encounter4: maids on the rows, a musket on the podium), the gate [Quest_Dracula1_MidLevelGate] at (91.8, -0.4,
        /// 140.2) and the mini-boss room (Encounter5-Ritual): the <b>Maid Warlord</b> rises from her coffin (her fiery bones
        /// burst; adds from the tunnels). Exit: the [DoorTrigger] (1) stage at (92.0, 1.7, 160.6).
        /// </summary>
        private static IEnumerator Gallery(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Gallery", new Vector3(75.50f, 4.52f, 169.72f), s);
            var a = l.Area;
            var act = NewAct(l, "GALLERY", "Act 5");
            const string fm = "Encounter1-FollowMelees", st = "Encounter3-StairEncounter", band = "Encounter4-Bandroom", rit = "Encounter5-Ritual";
            G(act, a, "fm", fm);
            G(act, a, "stM", st, "Musket");
            G(act, a, "stS", st, "Room");
            G(act, a, "bandBack", band, "BackRow");
            G(act, a, "bandFront", band, "FrontRow");
            G(act, a, "bandHall", band, "hallway");
            G(act, a, "podium", band, "Podium");
            G(act, a, "mb", rit, "MinibossSpawn");
            G(act, a, "coffin", rit, "Coffin");
            G(act, a, "ritHall", rit, "HallwaySpawn");
            G(act, a, "behind", rit, "Behind");
            act.Wave(W.ActStart).Add(C.Spicer, 3, "fm");
            Enter(act, a, st).Add(C.Marksman, 1, "stM").Add(C.Spicer, 3, "stS");
            Enter(act, a, band).Add(C.Maid, 2, "bandBack").Add(C.Spicer, 3, "bandFront").Add(C.Marksman, 1, "podium");
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 2, "bandHall").Add(C.Maid, 1, "bandBack");
            var boss = Enter(act, a, rit).Add(C.MaidWarlord, 1, "mb").Add(C.Spicer, 2, "coffin");
            boss.Toast = "The Maid Warlord!";
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 2, "ritHall").Add(C.Maid, 1, "behind");
            Gate(l, "[Quest_Dracula1_MidLevelGate]", 4);
            act.ExitDoor = Exit(l, act, "ExitDoor");
            done(act);
        }

        // ------------------------------------------------------------------ act 6: Chamber of Knowledge

        /// <summary>
        /// Act 6 (level85, Quest_Dracula1_Library): the Chamber of Knowledge. Spawn (-203.1, 0.0, 207.0); the reception
        /// (1-Reception) and its gate [..MidLevelGate] RECEPTION, the hallway patrols (3), the library arena (4: muskets at the
        /// windows, pumpkin heads from the entrances, bats from the top), the gate [..MidLevelGate] (1) at (-161.3, -3.7,
        /// 144.6), the maze (5) and its adds (6) where the <b>Mayor Marksman</b> fires his 5 quick shots from the platform
        /// with 2 pumpkin heads, 2 Barrel Dogs and 2 bats. Rec Room's push-in books and whip walls are not built (the gates
        /// open with the waves). Exit: the EndDoor pad (-134.2, -4.5, 145.9), back to the Grand Hall.
        /// </summary>
        private static IEnumerator Library(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Library", new Vector3(-203.11f, 0.01f, 207.00f), s);
            var a = l.Area;
            var act = NewAct(l, "CHAMBER OF KNOWLEDGE", "Act 6");
            const string rec = "1-Reception", hall = "3-LibraryHallwayPatrols", arena = "4-LibraryArena", maze = "5-Maze", adds = "6-MazeAdds (1)";
            G(act, a, "rec", rec);
            G(act, a, "hall", hall);
            G(act, a, "win", arena, "Window");
            G(act, a, "low", arena, "Entrace");
            G(act, a, "topF", arena, "TopSpawn", true);
            G(act, a, "maze", maze);
            G(act, a, "adds", adds, "ArenaSpawn");
            G(act, a, "addsB", adds, "Behind");
            Enter(act, a, rec).Add(C.Spicer, 3, "rec").Add(C.Maid, 1, "rec");
            Enter(act, a, hall).Add(C.Spicer, 2, "hall").Add(C.Marksman, 1, "hall");
            Enter(act, a, arena).Add(C.Marksman, 2, "win").Add(C.Spicer, 3, "low").Add(C.FireBat, 2, "topF");
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 2, "low").Add(C.Dog, 2, "low").Add(C.FireBat, 2, "topF");
            Enter(act, a, maze).Add(C.Spicer, 3, "maze").Add(C.Dog, 1, "maze");
            var boss = Enter(act, a, adds).Add(C.MayorMarksman, 1, "adds").Add(C.Spicer, 2, "adds").Add(C.Dog, 2, "addsB");
            boss.Toast = "The Mayor Marksman!";
            act.Wave(W.PrevBelow, 1).Add(C.FireBat, 2, "adds").Add(C.Spicer, 2, "addsB");
            Gate(l, "[Quest_Dracula1_MidLevelGate] RECEPTION", 1);
            Gate(l, "[Quest_Dracula1_MidLevelGate] (1)", 4);
            act.ExitDoor = Exit(l, act, "Doors/EndDoor");
            done(act);
        }

        // ------------------------------------------------------------------ act 8: Garden of Sorrows

        /// <summary>
        /// Act 8 (level86, Quest_Dracula1_Park, the graveyard garden): the Garden of Sorrows. Spawn (-18.4, -80.8, -459.9);
        /// skeletons rise from the headstones (Encounter1-HeadstoneReveal), muskets on the stairs, the pond: waves on both
        /// banks and the island (Encounter2-DefendIsland, "every enemy except barrel dogs"), the gate [..MidLevelGate] at
        /// (25.0, -79.7, -428.8), the gazebo (Encounter3) and the graveyard (Encounter5): <b>President Pumpkin Head</b>
        /// with minions rising from the gravestones. Exit: the ExitDoor pad (-15.2, -75.8, -363.7).
        /// </summary>
        private static IEnumerator Park(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Park", new Vector3(-18.42f, -80.80f, -459.92f), s, true);
            var a = l.Area;
            var act = NewAct(l, "GARDEN OF SORROWS", "Act 8");
            const string hs = "Encounter1-HeadstoneReveal", ms = "Encounter1-MusketOnStairs", di = "Encounter2-DefendIsland", gz = "Encounter3-aApproachGazebo", gy = "Encounter5-GraveyardBattle";
            G(act, a, "graves", hs, "Gravestone");
            G(act, a, "mstairs", ms);
            G(act, a, "island", di, "Ranged");
            G(act, a, "islandOn", di, "OnIsland");
            G(act, a, "bridges", di, "Bridge");
            G(act, a, "bridgesF", di, "Bridge", true);
            G(act, a, "steps", gz, "Step");
            G(act, a, "gazebo", gz, "Gazebo");
            G(act, a, "hill", gz, "OverTheHill");
            G(act, a, "behind", gz, "Behind");
            G(act, a, "treeF", gz, "UnderTree", true);
            G(act, a, "mb", gy, "MiniBoss");
            G(act, a, "gs", gy, "Gravespawn");
            G(act, a, "gsR", gy, "Spawn Region");
            Enter(act, a, hs).Add(C.Spicer, 3, "graves");
            Enter(act, a, ms).Add(C.Marksman, 2, "mstairs");
            Enter(act, a, di).Add(C.Marksman, 2, "island").Add(C.Maid, 1, "islandOn").Add(C.Spicer, 3, "bridges").Add(C.FireBat, 2, "bridgesF");
            act.Wave(W.PrevBelow, 1).Add(C.DiveBat, 3, "bridgesF").Add(C.Spicer, 2, "bridges");
            Enter(act, a, gz).Add(C.Spicer, 3, "steps").Add(C.Maid, 2, "gazebo").Add(C.Marksman, 1, "hill");
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 3, "behind").Add(C.FireBat, 2, "treeF");
            var boss = Enter(act, a, gy).Add(C.PumpkinHead, 1, "mb").Add(C.Spicer, 4, "gs");
            boss.Toast = "President Pumpkin Head!";
            act.Wave(W.PrevBelow, 1).Add(C.Spicer, 3, "gsR").Add(C.Maid, 1, "gsR").Add(C.FireBat, 2, "gsR");
            Gate(l, "[Quest_Dracula1_MidLevelGate]", 4);
            act.ExitDoor = Exit(l, act, "Doors/ExitDoor");
            done(act);
        }

        // ------------------------------------------------------------------ act 9: Inverted Ballroom

        /// <summary>
        /// Act 9 (level87, Quest_Dracula1_Ballroom): the Inverted Ballroom, the room hung upside down. Spawn (22.2, -0.5,
        /// 187.6); pumpkin heads and muskets in the lobby (Encounter1-MeleePatrol), the stair fight with the dogs
        /// (Encounter2-StairFight), the gate [..MidLevelGate] at (13.3, 0.7, 144.6) and the ballroom itself
        /// (Encounter2-Ballroom): maids and riflemen on the tables, then bats, pumpkin heads and a maid, then the
        /// <b>Baron Fire Bat</b> (purple; a horizontal line of 5 fireballs: jump over it). Music CBM_Ballroom. Exit: the
        /// [DoorTrigger] (1) pad at the ballroom's far end (-16.2, 2.0, 144.6).
        /// </summary>
        private static IEnumerator Ballroom(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Ballroom", new Vector3(22.21f, -0.50f, 187.58f), s);
            var a = l.Area;
            var act = NewAct(l, "INVERTED BALLROOM", "Act 9");
            const string mp = "Encounter1-MeleePatrol (1)", sf = "Encounter2-StairFight", ball = "Encounter2-Ballroom";
            G(act, a, "lobby", mp, "Lobby");
            G(act, a, "muskets", mp, "Musket");
            G(act, a, "wolf", sf, "Wolf");
            G(act, a, "sfHall", sf, "Spawn");
            G(act, a, "tables", ball, "Table");
            G(act, a, "topF", ball, "TopSpawn", true);
            G(act, a, "back", ball, "Back");
            G(act, a, "scaffold", ball, "Scaffolding");
            Enter(act, a, mp).Add(C.Spicer, 3, "lobby").Add(C.Marksman, 2, "muskets");
            Enter(act, a, sf).Add(C.Dog, 2, "wolf").Add(C.Spicer, 3, "sfHall");
            Enter(act, a, ball).Add(C.Maid, 2, "tables").Add(C.Marksman, 2, "tables");
            act.Wave(W.PrevBelow, 1).Add(C.FireBat, 3, "topF").Add(C.Spicer, 3, "back").Add(C.Maid, 1, "scaffold");
            var boss = act.Wave(W.PrevBelow, 1).Add(C.BaronBat, 1, "topF").Add(C.FireBat, 2, "topF");
            boss.Toast = "Baron Fire Bat! Jump over his fireballs";
            Gate(l, "[Quest_Dracula1_MidLevelGate]", 2);
            act.ExitDoor = Exit(l, act, "[DoorTrigger] (1)");
            done(act);
        }

        // ------------------------------------------------------------------ act 11: Lower Sanctum

        /// <summary>
        /// Act 11 (level88, Quest_Dracula1_Dungeon): the Lower Sanctum, torch-lit. Spawn (-50.6, 16.5, 205.0) above the
        /// trap corridors: the Dart Traps volumes shoot arrows that down a player (<see cref="RrCrescendoTraps"/>; Rec
        /// Room's swinging axes and fire cannons are not built), the gate [..MidLevelGate] at the trap exit (open from the
        /// start), the props storage room (pumpkin heads and red bats), the stairs (2 maids and a musket), the gate
        /// [..MidLevelGate] (1) into the arena and the <b>King Barrel Dog</b> (Miniboss-Wolf, no adds), the gate
        /// [..MidLevelGate] (3) and the Story State Increment volume that ends the act (-56.9, -5.6, 185.7).
        /// </summary>
        private static IEnumerator Dungeon(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_Dungeon", new Vector3(-50.56f, 16.47f, 205.04f), s);
            var a = l.Area;
            var act = NewAct(l, "LOWER SANCTUM", "Act 11");
            const string ps = "PropsStorageRoom", st = "Stairs", wolf = "Miniboss-Wolf";
            G(act, a, "storage", ps);
            G(act, a, "storageF", ps, null, true);
            G(act, a, "maids", st, "Bone");
            G(act, a, "musket", st, "Musket");
            G(act, a, "wolf", wolf);
            Enter(act, a, ps).Add(C.Spicer, 3, "storage").Add(C.FireBat, 2, "storageF");
            Enter(act, a, st).Add(C.Maid, 2, "maids").Add(C.Marksman, 1, "musket").Add(C.DiveBat, 2, "storageF");
            var boss = Enter(act, a, wolf).Add(C.KingDog, 1, "wolf");
            boss.Toast = "The King Barrel Dog!";
            Gate(l, "[Quest_Dracula1_MidLevelGate]", 0);
            Gate(l, "[Quest_Dracula1_MidLevelGate] (1)", 2);
            Gate(l, "[Quest_Dracula1_MidLevelGate] (3)", 3);
            Bounds end;
            act.ExitDoor = TryZone(a, "Story State Increment", out end) ? ZoneExit(l, end) : Exit(l, act, "Story State Increment");
            var traps = new List<Bounds>();
            Bounds z;
            for (int i = 0; TryZone(a, "Dart Traps", out z, i); i++)
            {
                traps.Add(z);
            }

            room.Traps = RrCrescendoTraps.Create(l.Root, traps);
            done(act);
        }

        // ------------------------------------------------------------------ act 13: Concert Hall

        /// <summary>
        /// Act 13 (level89, Quest_Dracula1_BossRoom): the Concert Hall. The team arrives at the back (-146.0, -3.9, 250.6)
        /// facing the stage; Dracula at his organ on the stage (DraculaBossFight/SpawnPoint (-110.0, -6.2, 250.2)), the
        /// balconies left and right (Balcony Left / Right, the safe spots; the top-left one has a mirror), the three light
        /// mirrors [Quest_Dracula1_MirrorCannon] around the hall, the side doors where his minions come in (Door Left /
        /// Right) and the decoy bats' regions (DecoyBatAdds). The fight is <see cref="RrCrescendoDracula"/>.
        /// </summary>
        private static IEnumerator Concert(C room, C.Level l, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            yield return Build(room, l, "Crescendo_BossRoom", new Vector3(-146.03f, -3.90f, 250.57f), s);
            var a = l.Area;
            var act = NewAct(l, "CONCERT HALL", "Act 13");
            act.Boss = true;
            const string fight = "DraculaBossFight", decoy = "DecoyBatAdds";
            var organ = a.Points(fight, "/SpawnPoint");
            act.Group("boss", organ.Count > 0 ? organ[0] : a.L(-110.05f, -6.23f, 250.23f));
            act.Wave(W.ActStart).Add(C.Dracula, 1, "boss");
            var stands = new List<Vector3>();
            stands.Add(organ.Count > 0 ? organ[0] : a.L(-110.05f, -6.23f, 250.23f));
            stands.AddRange(a.Points(fight, "Stage Front"));
            stands.AddRange(a.Points(fight, "Balcony"));
            var mirrors = new List<Pose>();
            foreach (var m in a.Markers("mirror"))
            {
                mirrors.Add(new Pose(m.Position, m.Rotation));
            }

            var doors = a.Points(fight, "Door");
            var bats = a.Points(decoy);
            room.DraculaFight = RrCrescendoDracula.Create(l.Root, stands, mirrors, doors, bats, a.Points(fight, "Balcony Right"));
            act.ExitDoor = null;
            act.ArrivalToast = "Aim the light mirrors at Dracula - three hits end him";
            done(act);
        }
    }
}
