# iter13 integrate (wave 1)

## Plan
1. Read builder notes; apply shared requests (RrGameEvents header comment, README sections, RrQuestRoom LavaToast, checkpoints (maybe), RrEnvironment KeyFor, RrEnvironmentTable regen (maybe)).
2. Reachability: Rec Center category doors / room list panels, watch room list: Crimson, Isle, Crescendo under QUESTS; LaserTag, Soccer, Bowling under SPORTS & PVP.
3. bankscene.sh once; compile check 0 errors.
4. One harness run "wave1_tour"; check summary + log; fix clear breakage (max one more run).
5. Copy shots to DevNotes/iter13/screens; write wave1_result.md.

## Log
- start (no previous integrate.md)
- added .meta files for Games/Soccer, Games/Bowling (folders + 10 scripts)
- compile check before edits: 0 errors
- RrGameEvents header: Maker Pen ids 20-23 noted
- RrQuestRoom: virtual LavaToast (+ Crimson override "Swallowed by the bog!" in the Bog act)
- Core/RrEnvironmentTable.cs regenerated with envcs (69 -> 91 scenes; only additions: 11 Crimson_*, 11 Crescendo_*); backup NSP/int13/RrEnvironmentTable.cs.bak; #US heap was 10.66 MB / 16 MiB before
- Core/RrEnvironment.KeyFor: CrimsonCauldron -> Crimson_Foyer, Crescendo -> Crescendo_Foyer
- reachability: already done - RrCategoryDoor SportsList/QuestList list all six; watch Play page lists RrRooms.All; all six registries have Create -> nothing to change
- Isle checkpoint request NOT applied (needs same-act re-entry on clients; not small) -> open issue
- bankscene.sh run
- RrVersion 13; compile 0 errors after shared edits + bankscene
- harness: RrScenarioRun shot frame "player" added (backup NSP/int13/RrScenarioRun.before.cs)
- 18:17 harness wave1_tour started (scene mode; user opened the Rec Room project in Unity at about this time, after bankscene)
- README: Places row, iteration 13 row, iteration 13 checklist, iteration 13 section (backup NSP/int13/README.before.md)
- run1 partial: maker pen Dorm OK (draw, wire exact, erase+undo); Jumbotron foyer/acts 5/8/9 shots OK; Crimson: foyer, console START, armory, act1 clear, bog, witch, KillBoss -> VICTORY (27800 gold, C), chests open
- run1: Isle act 8 NOT CLEAR - Ghost Beard's 3rd hit never came (FP3 45.6 deg off both cannons from the muzzle; arc 45). FIX: RrLostSkullsCannon.Arc 45 -> 55 (order preserved: FP1/FP2 70-75 deg off the other cannon)
- run1: Crescendo: foyer, START, act1 fight CLEAR + exit, Grand Hall, Concert Hall: Dracula 3 mirror hits -> VICTORY (18545 silver, rank S+)
- run1: Laser Tag: Hangar built 1.4 s, console START -> countdown -> Blue base spawn, pickup + grenade (self-destruct -10, clamped 0), TestEndSoon -> match over (tie), map switch -> Cyber Junk City built 4.7 s (166,722 plates), Hangar freed; README slot fixed to (300,0,0)
- run1 DONE: 0 step failures of 149, 490 s. Errors: 172x "CollisionMeshData couldn't be created ... Quest_Rock01" (Crimson build; RrSceneKit mesh collider on a non-readable install mesh). Warnings: Crescendo racks try scene objects "[Quest_Dracula1_Plate] (N)", "[Quest_Dracula1_MirrorCannon]", "[Quest_Pirate1_Torch] (1)", "Crossbow Trap (1)".
- FIX: Core/RrSceneKit.cs ColliderPhase: non-readable mesh -> box from its bounds (WarnOnce). FIX: Crescendo Weapons() only "(Name)" prefabs.
- Soccer: START (practice) -> kick-off -> test shot -> goal for Blue. Bowling: START -> roll 6.8 m/s -> 10 down STRIKE, pinsetter reset, frame 2. Maker pen in Rec Center OK.
- 18:29 run2 wave1_fix started
- run2 DONE (wave1_fix, 32 steps, 0 failures): 0 CollisionMeshData errors (Quest_Rock01/04 now boxes); Ghost Beard hit 3/3 (cannon 0 at FP3) -> Isle VICTORY 15000 gold S; Crescendo 11 racks, no "could not spawn" warnings. Return to the lobby after a quest victory not observed (harness matched an earlier line); Laser Tag's MatchOver -> Lobby is proven (TestMap needs Lobby).
- shots copied to DevNotes/iter13/screens (run1 w1_*, run2 w1fix_*)
- wave1_result.md written
