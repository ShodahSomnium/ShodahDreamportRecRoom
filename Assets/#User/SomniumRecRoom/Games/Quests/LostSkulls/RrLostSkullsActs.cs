using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using W = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.RrQuestWave.When;
using I = SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls.RrLostSkullsRoom;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// The Foyer and the eight acts of Isle of Lost Skulls on Rec Room's own levels 62-70, with the fights of the final
    /// version (rr_latest_rgames_story.md 8.1, the wiki walkthrough) placed on each level's own "Encounters": the enemy
    /// spawn points and regions of each encounter are its spawn group, and its Overlap Volume is the zone that starts it
    /// (DevNotes/iter13/isle_tools/isle_markers.pl; names as the level has them). Wave sizes are for 3 players (the
    /// framework scales them down for smaller parties); gold totals come out near the wiki's per-act points. Each act's
    /// anchor is its arrival (the centre of Player7SpawnPoint (1..4), scene coordinates in each method).
    /// </summary>
    public static class RrLostSkullsActs
    {
        public delegate IEnumerator Builder(I room, Transform root, Dictionary<string, Material> surfaces, Action<RrQuestAct> done);

        /// <summary>Acts 1-8, in play order.</summary>
        public static Builder[] All()
        {
            return new Builder[] { Galley, Banquet, HighSeas, PortPlunder, GibbetJungle, Caves, SunkenShip, GhostBeard };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Builds a level from its data file (Data/Isle_&lt;Room&gt;.bytes); a missing file leaves the root empty.</summary>
        private static IEnumerator BuildArea(RrJumbotronArea a, Transform r, Dictionary<string, Material> s)
        {
            string raw = RrSceneBank.Text(a.Scene);
            if (string.IsNullOrEmpty(raw))
            {
                RrLog.Warn("LostSkulls", a.Scene + ": the scene does not carry Data/" + a.Scene + ".bytes (bankscene.sh), the room stays empty");
                yield break;
            }

            yield return a.Build(raw, r, s);
        }

        private static RrQuestAct Finish(I room, RrJumbotronArea a, Transform r, string title, string subtitle)
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
            act.ExitDoor = RrLostSkullsExit.Create(a, r);
            AddWeapons(room, a, r);
            RrLostSkullsLadder.AddAll(a, r);
            room.Areas.Add(a);
            return act;
        }

        /// <summary>Rec Room's found weapons where the level lays them (cutlasses, stools, bottles, flintlocks...).</summary>
        private static void AddWeapons(I room, RrJumbotronArea a, Transform parent)
        {
            int n = 0;
            foreach (var m in a.Markers("weapon_pickup"))
            {
                string prefab = m.Get("prefab") ?? m.Name;
                if (prefab.StartsWith("(Quest_Pirate1_") || prefab.StartsWith("(Quest_Dracula1_"))
                {
                    room.AddSpot(RrJumbotronWeaponSpot.Create(parent, prefab, m.Position, m.Rotation));
                    n++;
                }
            }

            RrLog.Info("LostSkulls", a.Scene + ": " + n + " found weapons");
        }

        private static Vector3 Ahead(RrQuestAct act, float m)
        {
            return act.Entry + Quaternion.Euler(0f, act.EntryYaw, 0f) * new Vector3(0f, 0f, m);
        }

        /// <summary>A spawn group from an encounter's points (group: a substring of the point's group path; fallback 10 m ahead).</summary>
        private static void Group(RrQuestAct act, RrJumbotronArea a, string name, string encounter, string group = null)
        {
            var pts = a.Points(encounter, group);
            if (pts.Count == 0 && group != null)
            {
                pts = a.Points(encounter);
            }

            if (pts.Count == 0)
            {
                RrLog.WarnOnce("LostSkulls", "grp:" + a.Scene + encounter, a.Scene + ": no spawn points for " + encounter + "; 10 m ahead of the arrival");
            }

            act.Group(name, pts.Count > 0 ? pts.ToArray() : new[] { Ahead(act, 10f) });
        }

        /// <summary>An Enter wave's zone: the encounter's Overlap Volume (fallback: a 12 m box ahead of the arrival).</summary>
        private static RrQuestWave Zone(RrQuestWave w, RrQuestAct act, RrJumbotronArea a, string encounter, int index = 0)
        {
            Bounds z;
            if (a.Zone(encounter, out z, index))
            {
                return w.In(z.center, z.size);
            }

            RrLog.WarnOnce("LostSkulls", "zone:" + a.Scene + encounter, a.Scene + ": no trigger volume for " + encounter + "; a 12 m zone ahead of the arrival");
            return w.In(Ahead(act, 12f), new Vector3(12f, 6f, 12f));
        }

        /// <summary>
        /// A mid-level gate's jail-bar door ([Quest_Pirate1_MidLevelGate] LockableDoor/DoorPivot/Door, world scale 1.125:
        /// its box collider sits at (0.675, 1.05, 0.01) x 1.125 from the pivot, 1.37 x 2.12 m): the bars and the collider are
        /// left out, so the gate stands open (the basic form has no gate encounters). Pivot and yaw from the level.
        /// </summary>
        private static void OpenGate(RrJumbotronArea a, float px, float py, float pz, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 c = new Vector3(px, py, pz) + rot * new Vector3(0.759f, 1.182f, 0f);
            // The axis-aligned box around the turned door (half 0.8 m along the bars' width, 0.15 m across).
            Vector3 w = rot * Vector3.right * 0.8f, d = rot * Vector3.forward * 0.15f;
            float hx = Mathf.Abs(w.x) + Mathf.Abs(d.x), hz = Mathf.Abs(w.z) + Mathf.Abs(d.z);
            a.DropBoxes.Add(new Bounds(c, new Vector3(hx * 2f + 0.2f, 2.5f, hz * 2f + 0.2f)));
        }

        // ------------------------------------------------------------------ the Foyer (lobby)

        /// <summary>
        /// The Foyer (level62, Quest_Pirate1_Foyer): the pier lobby. Player1..3SpawnPoint at z 158.9 facing +z (the room's
        /// origin is their centre); Rec Room's start hologram [ScoreboardProjector] (1) at (-68.29, -6.47, 148.62); the
        /// Swash and Buckle storefront [PirateStorefront] behind the spawns; skeleton standees and a table of practice
        /// weapons ("StufftoShoot"); the [ActivityDoor] back to the Rec Center at (-56.81, -6.47, 151.80). Ours: the
        /// console on the hologram's marker, a Rec Center portal on the door (its Rec Room meshes are left out), the
        /// practice weapons. No dressing room: START takes the team straight into the brig.
        /// </summary>
        public static IEnumerator Foyer(I room, Transform t, Vector3 anchor, List<Pose> spawns, Dictionary<string, Material> surfaces)
        {
            var a = new RrJumbotronArea(I.FoyerScene, anchor);
            foreach (var n in new[] { "ActivityDoor_Left", "ActivityDoor_Right", "ActivityDoor_Emit", "ActivityDoor_Glow", "ActivityDoor_Frame" })
            {
                a.Skip.Add(n);
            }

            yield return BuildArea(a, t, surfaces);
            spawns.Clear();
            spawns.AddRange(a.SpawnPoses());
            var holo = a.Markers("scoreboard");
            if (holo.Count > 0)
            {
                var m = holo[0];
                room.LobbyConsole(t, new Pose(m.Position, Quaternion.Euler(0f, m.Rotation.eulerAngles.y, 0f)));
                RrLog.Info("LostSkulls", "Foyer: start hologram at " + m.Position.ToString("F2") + " yaw " + m.Rotation.eulerAngles.y.ToString("0"));
            }
            else
            {
                room.LobbyStartButton(t, a.L(-68.29f, -6.47f, 148.62f), 180f);
            }

            foreach (var p in a.Markers("portal", "[ActivityDoor]"))
            {
                RrPortal.Create(t, RrActivity.RecCenter, p.Position, p.Rotation.eulerAngles.y, "REC CENTER");
            }

            AddWeapons(room, a, t);
        }

        // ------------------------------------------------------------------ act 1

        /// <summary>
        /// Act 1, the Galley (level63, Quest_Pirate1_Hallway1): arrival (145.53, -3.44, 49.81) inside the brig; its door
        /// [BreakableLockEncounter] at x 149.55 with the lock at (149.48, -2.48, 47.98) (<see cref="RrLostSkullsCell"/>).
        /// Encounters 1-6 run east along the ship's hold to the exit [Quest_Door_Active] at x 250.5; the mid-level gate
        /// (204.6, 55.4) stands open. Final: swashbucklers in waves (14,000 gold).
        /// </summary>
        private static IEnumerator Galley(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_Hallway1", new Vector3(145.53f, -3.44f, 49.81f));
            a.DropBoxes.Add(new Bounds(new Vector3(149.545f, -2.43f, 47.48f), new Vector3(0.34f, 2.25f, 1.5f)));
            OpenGate(a, 205.1701f, -1.5189f, 54.8838f, -135f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 1", "The Galley");
            room.Cell = RrLostSkullsCell.Create(room, r, a.L(149.5483f, -3.4991f, 46.8037f), -90f, a.L(149.4751f, -2.4784f, 47.9841f));
            Group(act, a, "e1", "Encounter1", "Spawn");
            Group(act, a, "e2", "Encounter2");
            Group(act, a, "e3", "Encounter3", "Spawn");
            Group(act, a, "e4", "Encounter4");
            Group(act, a, "e5", "Encounter5");
            Group(act, a, "e6", "Encounter6", "Spawn");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(I.Swash, 3, "e1");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 3, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Swash, 3, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Swash, 3, "e3");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(I.Swash, 2, "e4");
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(I.Swash, 2, "e5");
            Zone(act.Wave(W.Enter), act, a, "Encounter6").Add(I.Swash, 3, "e6");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 3, "e6");
            done(act);
        }

        // ------------------------------------------------------------------ act 2

        /// <summary>
        /// Act 2, the Pirate Banquet (level64, Quest_Pirate1_Hallway2): arrival (27.57, -5.99, 64.54); Encounters 1-5 and
        /// the LiftShaft up through the banquet hall to the Finale's two floors (x 120-125). Final: swashbucklers, the
        /// first jug skeleton and 2 bottle throwers, 2 more throwers, then 3 on the beams, a jug on the stairs; the last
        /// fight is the Bar Fight, everything at once (22,000 gold).
        /// </summary>
        private static IEnumerator Banquet(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_Hallway2", new Vector3(27.57f, -5.99f, 64.54f));
            OpenGate(a, 104.3749f, -1.5189f, 60.5752f, -90f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 2", "The Pirate Banquet");
            Group(act, a, "e1", "Encounter1");
            Group(act, a, "e2", "Encounter2", "Spawn");
            Group(act, a, "e3", "Encounter3");
            Group(act, a, "e4", "Encounter4");
            Group(act, a, "e5", "Encounter5");
            Group(act, a, "finale", "Finale", "Spawn");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(I.Swash, 3, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Swash, 3, "e2").Add(I.Jug, 1, "e2").Add(I.Bottle, 2, "e2");
            act.Wave(W.PrevBelow, 1).Add(I.Bottle, 2, "e2");
            act.Wave(W.PrevBelow, 1).Add(I.Bottle, 3, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Jug, 1, "e3").Add(I.Swash, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(I.Swash, 3, "e4");
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(I.Swash, 3, "e5").Add(I.Bottle, 2, "e5");
            // The Bar Fight: everything at once, on the floor and the high platforms.
            act.Wave(W.PrevCleared).Add(I.Swash, 4, "finale").Add(I.Bottle, 3, "finale").Add(I.Jug, 2, "finale");
            act.Wave(W.PrevBelow, 2).Add(I.Swash, 4, "finale").Add(I.Bottle, 2, "finale");
            done(act);
        }

        // ------------------------------------------------------------------ act 3

        /// <summary>
        /// Act 3, the High Seas (level65, Quest_Pirate1_ShipDeck): arrival (-161.71, 5.57, -23.80) on the team's ship;
        /// its two cannons (-156.58, 5.57, -23.94) and (-145.08, 6.09, -23.95) face the other ship 15 m south, whose
        /// cannons (-146.76 / -135.46, z -38.56) the cannoneers man. The boarding bridge stands raised at (-143.09, 5.70,
        /// -39.16) with the target [Paintball_Target] on it (<see cref="RrLostSkullsBridge"/>): a cannon hit drops it onto
        /// BridgeLands (-143.10, 6.86, -26.53). The curtain gate (-130.9) stands open; the exit [Quest_Door_Active] at
        /// x -106.1. Final: swashbucklers and a bottle thrower on the mainsail, a jug and a thrower at the wheel, 2
        /// cannoneers, 3 captains on the other ship; after the bridge, waves of swashbucklers, captains and a jug (15,000).
        /// </summary>
        private static IEnumerator HighSeas(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_ShipDeck", new Vector3(-161.71f, 5.57f, -23.80f));
            a.DropBoxes.Add(new Bounds(new Vector3(-143.05f, 12.3f, -36.8f), new Vector3(2.8f, 14.6f, 6.2f)));
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 3", "The High Seas");
            room.Bridge = RrLostSkullsBridge.Create(r, a.L(-143.09f, 5.70f, -39.16f), a.L(-143.10f, 6.86f, -26.53f), a.L(-142.98f, 9.50f, -37.78f));
            int k = 0;
            foreach (var m in a.Markers("cannon"))
            {
                // The team's ship (z > -30): the enemy ship's two cannons are the cannoneers' posts.
                if (m.ScenePosition.z > -30f)
                {
                    room.Cannons.Add(RrLostSkullsCannon.Create(room, r, I.HighSeasAct, k++, m.Position, m.Rotation.eulerAngles.y));
                }
            }

            Group(act, a, "e1", "Encounter1");
            Group(act, a, "e2", "Encounter2", "Spawn");
            Group(act, a, "e3", "Encounter3");
            act.Group("cannoneers", a.L(-146.76f, 5.28f, -38.57f) + new Vector3(0f, 0f, -1.2f), a.L(-135.46f, 5.87f, -38.55f) + new Vector3(0f, 0f, -1.2f));
            act.Wave(W.ActStart).Add(I.Swash, 3, "e1").Add(I.Bottle, 1, "e1");
            act.Wave(W.PrevBelow, 1).Add(I.Jug, 1, "e1").Add(I.Bottle, 1, "e1");
            act.Wave(W.PrevBelow, 1).Add(I.Cannoneer, 2, "cannoneers").Add(I.Captain, 3, "e2");
            // Boarding: the other ship's deck, reachable over the dropped bridge.
            act.Wave(W.Enter).In(a.L(-142f, 6f, -42f), new Vector3(26f, 8f, 12f)).Add(I.Swash, 3, "e3").Add(I.Captain, 2, "e3");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 3, "e3").Add(I.Jug, 1, "e3");
            done(act);
        }

        // ------------------------------------------------------------------ act 4

        /// <summary>
        /// Act 4, Port Plunder (level66, Quest_Pirate1_Beach1): arrival (-64.58, -4.56, -259.85) where the team lands;
        /// Encounters 1-4 along the harbour, Encounter5 at the mid-level gate (-36.6, -264.4, open), then the
        /// MegaMarketRumble in the market streets (flintlocks in the towers, melees in the streets, a jugger in the centre
        /// tower) before the exit at x 2.0. Final: captains, swashbucklers, jugs on high platforms; the gate triggers 5
        /// captains, then 4 more (27,500 gold). Flintlocks lie around from here on.
        /// </summary>
        private static IEnumerator PortPlunder(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_Beach1", new Vector3(-64.58f, -4.56f, -259.85f));
            OpenGate(a, -36.5547f, -3.6929f, -265.1647f, -90f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 4", "Port Plunder");
            Group(act, a, "e1", "Encounter1", "Spawn");
            Group(act, a, "e2", "Encounter2");
            Group(act, a, "e3", "Encounter3");
            Group(act, a, "e4", "Encounter4", "Spawn");
            Group(act, a, "e5", "Encounter5");
            Group(act, a, "towers", "MegaMarketRumble", "FlintLocksInTowers/Spawn");
            Group(act, a, "streets", "MegaMarketRumble", "MeleesInStreets/Spawn");
            Group(act, a, "jugger", "MegaMarketRumble", "Jugger");
            act.Wave(W.ActStart).Add(I.Captain, 1, "e1").Add(I.Swash, 3, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Swash, 3, "e2").Add(I.Captain, 1, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(I.Swash, 3, "e4").Add(I.Jug, 1, "e4").Add(I.Captain, 1, "e4");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Captain, 2, "e3").Add(I.Swash, 3, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(I.Captain, 5, "e5");
            act.Wave(W.PrevBelow, 1).Add(I.Captain, 4, "e5");
            Zone(act.Wave(W.Enter), act, a, "MegaMarketRumble").Add(I.Captain, 3, "towers").Add(I.Swash, 4, "streets").Add(I.Jug, 1, "jugger");
            act.Wave(W.PrevBelow, 2).Add(I.Swash, 4, "streets").Add(I.Captain, 2, "towers");
            done(act);
        }

        // ------------------------------------------------------------------ act 5

        /// <summary>
        /// Act 5, Gibbet Jungle (level67, Quest_Pirate1_Beach2): arrival (-178.93, 13.90, 110.53); Encounters 1-8 through
        /// the jungle ruins (the mid-level gate at (-143.6, 74.5) open) to the exit at (-107.0, 16.4, 85.5). Final: the
        /// first Gibbeted Skeleton (it throws bombs), bottle throwers on the side platforms, captains behind,
        /// swashbucklers and a jug in the corners, throwers on a bridge, 2 more gibbets (23,500 gold).
        /// </summary>
        private static IEnumerator GibbetJungle(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_Beach2", new Vector3(-178.93f, 13.90f, 110.53f));
            OpenGate(a, -143.5817f, 14.1628f, 73.6896f, -90f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 5", "Gibbet Jungle");
            for (int i = 1; i <= 8; i++)
            {
                Group(act, a, "e" + i, "Encounter" + i);
            }

            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(I.Gibbet, 1, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Bottle, 2, "e2").Add(I.Captain, 2, "e2").Add(I.Swash, 2, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Swash, 3, "e3").Add(I.Jug, 1, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(I.Bottle, 2, "e4");
            Zone(act.Wave(W.Enter), act, a, "Encounter6").Add(I.Bottle, 2, "e6").Add(I.Captain, 1, "e6");
            Zone(act.Wave(W.Enter), act, a, "Encounter5").Add(I.Gibbet, 1, "e5").Add(I.Swash, 3, "e5");
            Zone(act.Wave(W.Enter), act, a, "Encounter7").Add(I.Gibbet, 1, "e7").Add(I.Swash, 2, "e7");
            Zone(act.Wave(W.Enter), act, a, "Encounter8").Add(I.Swash, 3, "e8").Add(I.Captain, 2, "e8");
            done(act);
        }

        // ------------------------------------------------------------------ act 6

        /// <summary>
        /// Act 6, the Caves (level68, Quest_Pirate1_Caves1): arrival (-55.73, -2.38, -59.64); Encounters 1-3 in the
        /// first cavern (two mid-level gates, open), the BridgeThrowers over the holed bridge (49.5, 9.6, -57.1), the exit
        /// at (85.8, 16.5, -44.0). Final: dark (one torch; lit here), a gibbet, bottle skeletons, the bridge with holes;
        /// blunderbusses only here (7,500 gold).
        /// </summary>
        private static IEnumerator Caves(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_Caves1", new Vector3(-55.73f, -2.38f, -59.64f));
            OpenGate(a, 13.0884f, 13.6574f, -57.8683f, -90f);
            OpenGate(a, -14.2216f, 9.5993f, -33.8605f, -90f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 6", "The Caves");
            Group(act, a, "e1", "Encounter1");
            Group(act, a, "e2", "Encounter2");
            Group(act, a, "e3", "Encounter3");
            Group(act, a, "bridge", "BridgeThrowers");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(I.Swash, 3, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Swash, 3, "e2").Add(I.Bottle, 1, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Gibbet, 1, "e3").Add(I.Bottle, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "BridgeThrowers").Add(I.Bottle, 3, "bridge");
            done(act);
        }

        // ------------------------------------------------------------------ act 7

        /// <summary>
        /// Act 7, the Sunken Ship (level69, Quest_Pirate1_SunkenShip): arrival (181.64, -10.83, -267.77); Encounters 1-2
        /// down the underwater tunnel, 3 by the sunken ship, 4 on the platforms, the mid-level gate (246.3, -274.3, open)
        /// and the exit at x 247.1. Final: swashbucklers underwater, mines, a jugger on a platform, the missing puzzle
        /// pieces (not in the basic form) (11,500 gold).
        /// </summary>
        private static IEnumerator SunkenShip(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_SunkenShip", new Vector3(181.64f, -10.83f, -267.77f));
            OpenGate(a, 246.3155f, -12.3587f, -275.0151f, -90f);
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 7", "The Sunken Ship");
            Group(act, a, "e1", "Encounter1");
            Group(act, a, "e2", "Encounter2");
            Group(act, a, "e3", "Encounter3");
            Group(act, a, "e4", "Encounter4");
            Zone(act.Wave(W.Enter), act, a, "Encounter1").Add(I.Swash, 4, "e1");
            Zone(act.Wave(W.Enter), act, a, "Encounter2").Add(I.Swash, 3, "e2");
            Zone(act.Wave(W.Enter), act, a, "Encounter3").Add(I.Jug, 1, "e3").Add(I.Swash, 2, "e3");
            Zone(act.Wave(W.Enter), act, a, "Encounter4").Add(I.Swash, 3, "e4").Add(I.Captain, 2, "e4");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 3, "e4");
            done(act);
        }

        // ------------------------------------------------------------------ act 8

        /// <summary>
        /// Act 8, Ghost Beard's Gauntlet (level70, Quest_Pirate1_BossArena): arrival (-172.15, -4.06, 161.17) on the dock;
        /// the ship to clear and board lies at x -140 ("Enemies Defend Ship": Deck, Platform, Upper Back), its two cannons
        /// (-141.86, -4.76, 165.90) and (-143.61, -4.23, 153.40) face west over the water, where Ghost Beard floats
        /// ([GhostBeardEncounter]: Initial Spawn (-156.0, -2.5, 159.8), Fight Positions 1-3, the RetreatPoint 15 m up).
        /// Final: clear the ship, board it and Ghost Beard appears; only cannons damage him, 3 hits (rear cannon, then
        /// front, then either); swashbucklers come over the bridges; he fires cannon shots (34,500 gold).
        /// </summary>
        private static IEnumerator GhostBeard(I room, Transform r, Dictionary<string, Material> s, Action<RrQuestAct> done)
        {
            var a = new RrJumbotronArea("Isle_BossArena", new Vector3(-172.15f, -4.06f, 161.17f));
            yield return BuildArea(a, r, s);
            var act = Finish(room, a, r, "ACT 8", "Ghost Beard's Gauntlet");
            act.Boss = true;
            // The last act: its [Quest_Door_Active] is the way out after the credits in Rec Room, not an act exit.
            if (act.ExitDoor != null)
            {
                UnityEngine.Object.Destroy(act.ExitDoor.gameObject);
            }

            act.ExitDoor = null;
            int k = 0;
            foreach (var m in a.Markers("cannon"))
            {
                room.Cannons.Add(RrLostSkullsCannon.Create(room, r, I.BossAct, k++, m.Position, m.Rotation.eulerAngles.y));
            }

            var spots = new[]
            {
                PointOr(a, "Fight Position 1", a.L(-160.48f, -2.50f, 169.13f)),
                PointOr(a, "Fight Position 2", a.L(-156.13f, -2.32f, 148.10f)),
                PointOr(a, "Fight Position 3", a.L(-158.96f, -2.78f, 158.95f))
            };
            Vector3 start = PointOr(a, "Initial Spawn", a.L(-156.0f, -2.5f, 159.8f));
            Vector3 retreat = PointOr(a, "RetreatPoint", a.L(-159.11f, 13.05f, 159.80f));
            room.GhostBeard = RrGhostBeard.Build(r, start, spots, retreat);
            act.Group("boss", start);
            Group(act, a, "defend", "Enemies Defend Ship", "Spawn");
            Group(act, a, "boarders", "[GhostBeardEncounter]", "Spawn");
            act.Wave(W.ActStart).Add(I.Captain, 3, "defend").Add(I.Swash, 2, "defend");
            Zone(act.Wave(W.Enter), act, a, "Player triggers ship").Add(I.Boss, 1, "boss").Add(I.Swash, 3, "boarders");
            act.Wave(W.PrevBelow, 1).Add(I.Swash, 3, "boarders");
            done(act);
        }

        /// <summary>A named enemy spawn point of the level (room-local), or the fallback.</summary>
        private static Vector3 PointOr(RrJumbotronArea a, string name, Vector3 fallback)
        {
            var m = a.Marker("enemy_spawner", name);
            return m != null ? m.Position : fallback;
        }
    }
}
