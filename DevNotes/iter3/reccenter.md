# iter3 / reccenter (agent notes, durability)
Task: rebuild the Rec Center as Rec Room's FINAL version (see orchestrator task).
Status log:
- start: reading docs (rr_reccenter_cache.md, rr_latest_places.md s3, doors.md, existing code)
- 14:40 docs read. PLAN (room frame = rr_reccenter_cache.md 1.1; origin kiosk centre, entrance at -Z):
  * hall floor: rect X -13.9..11.5 Z -15.6..7.3 + rect X -13.9..-1.5 Z 7.3..13 + convex cap (circle centre (0.76,7.3) R 10.74, arc 0..102 deg, windows 15..56 & 71..97 deg)
  * ceiling 9.0 (beams 8.4..9.0), 2 skylight holes X -3.3..3.48, Z -9.57..-3.45 / 4.42..10.54; slope over balcony X -13.9..-9.32 from 9.0 to 11.9; 3 clerestory windows left wall Y 9.53..10.86
  * balcony Y 3.1 X -13.9..-9.4 Z -15.6..8.7 (+extension to Z 11.2 / landing to 13); front stairs X -9.4..-3.9 Z -15.6..-12.9 (climb -X); back stairs X -12.7..-7.0 Z 11.2..13 (climb -X)
  * gym X -18..-1.8 Z 13.3..25.9 ceil 6.5, opening from hall X -7..-1.8 at Z 13; hoops on X -18 and X -1.8 walls at Z 19.5; SPORTS door (-7.45,0,25.84) yaw 180
  * theatre X 11.8..21.6 Z -10.3..1.8 floor -0.79 via stage opening Z -8.7..-1.8 in right wall; QUESTS door (21.54,-0.79,-4.36) yaw 270; marquee above opening
  * left wing X -25..-14.2: cafe ground Z -7.4..5 (openings in hall left wall Z -4.2..-0.3, 0.3..4.2); upstairs Y 3.1 Z -15.6..-5.9 (opening Z -13.1..-10.9), FEATURED door (-24.94,3.1,-11.93) yaw 90
  * CREATIVE door on balcony wall (-13.84,3.1,5.6) yaw 90; Magic Door under balcony end (-9.39,0,9.6) yaw 90 (Campus Jams spot); Dorm RrPortal at hallway end (-0.65,0,-25.04)
  * hallway X -2.1..0.8 Z -25.1..-15.6 ceil 3.1; spawn in hallway Z -16.4/-17.4 facing +Z; mirror on hallway west wall
  * files: Layout, Shell (+Kiosk, Outdoors new), Geo (+Prism, ColliderOnly), Surfaces, Room, Test, Registry, Tracks; delete Pool/Cue/PoolBall


## !!! USER DECISIONS (orchestrator, ~14:50) - READ DevNotes/iter3/DECISIONS.md NOW
- KEEP the 6 lockers at their measured spots.
- Doors = the real signs at the real spots: SPORTS & PVP, QUESTS, FEATURED ROOMS, MAKERWORLD (RrDoorCategory.MakerWorld), CAMPUS JAMS (RrDoorCategory.CampusJams) + Dorm. NO Creative Campus door. Magic Door near Campus Jams if no known spot.
- 15:20 written: RrRcGeo (Prism, Solid, Ramp, RrRcBatch), Surfaces, Layout (doors/spawns/items), Shell (all architecture; ArcWall for the curve AND the cafe bay: Window_Cafe AABB + bushes show the cafe corner is a curved bay, ellipse centre (-17.6,-3.6) rx 7.4 rz 8.6), Kiosk (logo from RecRoom_Logo_SteamSplash_Tex crop 80,146,356,256, layered transparent quads). NEXT: Outdoors, Room, Test, Registry, Tracks, delete pool files, compile.
- 15:45 all files written (+ Outdoors, Room, Test, Registry text, Tracks without jukebox); pool/cue/poolball deleted. COMPILE OK (0 errors x3, 113 members proven). NEXT: harness run 1 (scenario reccenter_tour.json rewritten).
- 16:05 harness run 1: 36/36 steps ok; 101 prefabs 0 failed; logo texture ok. Fixes after LOOKING: floor planks back to 3 x 0.75 (checkerboard), couches tinted brown (white untinted), board labels yaw 270 (mirrored), fluorescent -> emissive, light wood warmer, quest poster titles via TitleOf + COMING SOON, ping-pong moved to hall back-left (-5.8,6.4)/(-3.4,9.7) out of the entrance view. Compile OK. NEXT: run 2.
- 16:25 harness run 2: 37/37 ok; looked at entrance/kiosk/back-left/pingpong/windows/magic/quests/upstairs/cafe: fixes confirmed (planks, brown couches, readable board, glowing fluorescents, quest titles, open front-right floor). Radio doc updated. Final compile OK (0 errors x3, 113 proven). DONE - returning summary (2 of 4 harness runs used).
