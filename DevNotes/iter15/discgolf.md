# Iter 15 - Disc Golf (basic form)

Status log (append as work progresses; resume from here if interrupted)

## Plan
1. Read Soccer/Bowling/LaserTag (map switch) patterns, rules research (rr_latest_rgames_action 8, rr_sports 5).
2. Data: level9 (Lake) + level10 (Propulsion) gtscene jt=1 -> dg_prep.awk -> isle_thin.pl -> rrsk2bytes --quant 3 ->
   CODE/Data/DiscGolf.bytes, DiscGolf_Propulsion.bytes (names = RrEnvironmentTable keys DiscGolf / DiscGolf_Propulsion).
3. Code Games/DiscGolf/*: registry, room (course choice like LaserTag's map switch), course table, disc (flight model,
   throw, net prop), scorecard.
4. Compile 0 errors.

## Log
- 20:18 notes created. jt=1 conversions -> DN/iter15/discgolf_work/DiscGolf_jt.rrsk (138 MB: 867 k plates, 800 k of
  them tree crowns; 717 k collider plates, 642 k of them the unreachable background hills) and
  DiscGolf_Propulsion_jt.rrsk (40 MB, 380 k plates, 6 k collider plates).
- 20:27 DN/iter15/discgolf_tools/dg_prep.awk: drops the level's discs (R/C mesh Frisbee_LOD0, P (DiscGolfDisc), disc
  capsules, disc-material plates) and (Lake) collider plates outside x -90..120, z -120..140. Then isle_thin.pl
  plates=40000 boxes=10000 (Lake) / 8000 (Propulsion).

## Measured (scene coords; apkscene `find` / `scripts` on level9/level10)
Holes = Systems/Holes/HoleN/HoleNGoal (DiscGolfGoal MB: hole N-1, par) and HoleNTees/[Tee_Marker] (tee = midpoint of
its two ArrowSign_Text posts; ground found at runtime by raycast). Basket base = goal transform (FrisbeeGoal mesh,
colliders: pole 0.05x2.43, tray 0.66x0.60x0.26 at +0.66, top 0.52x0.53x0.06 at +1.31).

LAKE (level9) par 28: tee -> goal (par)
 1 (54.35,80.39) -> (12.08,-0.13,68.85) 3 | 2 (0.09,77.92) -> (-31.16,0.107,93.11) 2 | 3 (-35.27,79.72) -> (-44.38,0.29,18.57) 3
 4 (-54.05,-16.90) -> (-22.24,0.29,-62.42) 4 | 5 (-10.42,-46.67) -> (-27.017,0.37,-35.378) 2 | 6 (-31.55,-12.93) -> (17.85,-0.02,35.43) 4
 7 (37.66,33.54) -> (30.939,0.07,-33.654) 3 | 8 (19.14,-47.26) -> (37.35,6.01,-69.62) 2 | 9 (39.68,-67.73) -> (69.97,2.548,1.34) 5
 Pregame spawns (59-62.6, 1.0-1.1, 77-83); Hole1 practice discs (53.6-54.1, 1.05-1.09, 79-81.6); ScoreboardProjector
 (52.601,0.984,84.705); exit door ActivityDoorSingle_Frame (73.028,1.0927,68.946) q(0,-0.4343,0,0.9007).
 Water = kill_zone KillVolume* boxes (top ~y 0.01): disc hazard, players not KO'd (2019). Bonus panels
 [DiscGolfPowerupGizmo(_2x2)] = trigger_zone Gizmo_Face: H3 (-37.52,5.65,70.65) 4.49 m, H6 (-9.63,3.07,24.27) 4.49 m,
 H9 (91.14,6.66,7.41) 2.06 m (x0.83).
PROPULSION (level10) par 26:
 1 (19.14,18.96) -> (18,0.62,58.52) 3 | 2 (15.85,91.47) -> (15.24,0.62,154.5) 3 | 3 (27.47,151.50) y~16.4 -> (-70.06,0.62,157.2) 2
 4 (-69.58,138.61) -> (-69.67,4.48,86.72) 4 | 5 (-53.30,63.28) -> (-71.66,0.587,15.06) 2 | 6 (-82.29,10.23) -> (-121.46,0.601,8.2) 3
 7 (-125.01,27.18) -> (-124.73,1.5,120) 3 | 8 (-98.52,131.34) y~8.6 -> (-42.142,0.54,79.428) 4 | 9 (-24.48,45.83) -> (-4.56,0.64,22.2) 2
 Pregame spawns (10.1-17.1, 0.617, -10.7/-12.9); practice discs (6.5-18.8, 0.63, -9.96..15.9); fans
 [DiscGolfFanGizmo*], hazards [DiscGolfHazardGizmo*], power-ups [DiscGolfPowerupGizmo] (trigger_zone markers).
- 20:32 thinned: Lake 867 k -> 34.8 k plates (S c5), collider plates 75.5 k -> 9.8 k (S 1.8); Propulsion 380 k -> 35.8 k
  plates (S c2.7), 6.4 k collider plates kept. dg_cover.pl (vertical-ray coverage of 2 m cells) found the Lake only 42 %
  solid even BEFORE thinning: apkscene wrote the level's one Unity terrain (collider over all 256 m) as a 2 m near grid
  with collider only around the pregame lobby + a 4 m far grid (collider=0). dg_terraincol.pl adds the far heights as
  collider-only grids (vis=0, 0.08 m low = the far grid's FarDrop) outside the near grid: Lake 95.5 % (the rest is
  past the terrain's edge), Propulsion 84 -> 100 %.
- 20:37 DATA: CODE/Data/DiscGolf.bytes 969,131 B, DiscGolf_Propulsion.bytes 904,041 B (+ .meta from rrsk2bytes).
  Pipeline (DN/iter15/discgolf_work, scratch-ish; tools in DN/iter15/discgolf_tools):
    gtscene prep apkwork level9 DiscGolf DiscGolf_jt.rrsk jt=1 (level10 DiscGolf_Propulsion likewise)
    awk -v cut=-90,-120,120,140 -f dg_prep.awk (Lake; Propulsion without cut)
    perl isle_thin.pl plates=40000 boxes=10000 (Propulsion boxes=8000)
    perl dg_terraincol.pl; rrsk2bytes.sh --name DiscGolf[_Propulsion] --quant 3
  Next: code.
- 20:55 CODE written (Games/DiscGolf, namespace ...Games.DiscGolf; registry in ...Rooms): RrRegistry.DiscGolf.cs,
  RrDiscGolfCourses.cs (tables), RrDiscGolfLevel.cs (one course via the public RrJumbotronArea + its markers),
  RrDiscGolfDisc.cs (flight model, throws), RrDiscGolfScore.cs (card in custom ints), RrDiscGolfRoom.cs (rules, net,
  course switch, boards, HUD). Compile (build.sh project --editor): 0 errors, 1 warning (Mirror.cs, not ours), editor 0/0.
- 21:00 out-of-bounds: both levels have tall unnamed "Cube" trigger boxes around the course (Lake 24, Propulsion 53);
  checked offline that no tee, basket, bonus panel or lobby spawn is inside one -> treated as out of bounds (GUESS).

## Design (as built)
- One activity, two courses (State.Map: 0 Lake, 1 Propulsion) chosen with the hologram's map CHANGE (LaserTag's
  pattern: GameRequest VoteMap repeated until the int shows it; the new course is built in its own slot - Propulsion
  anchor (520,0,0) - then everyone moves to its lobby and the old one is freed). Lobby = the level's pregame spawns;
  console = the (ScoreboardProjector) nearest them; REC CENTER portal on the level's exit door frame.
- 4 discs ((DiscGolfDisc), RrTools tool, RrNetBody props 0-3, slot colours blue/red/green/yellow via _BaseColor) under
  the room; lobby = free practice with any disc; playing = only your own disc.
- Rules: START (1-4 golfers, join order) -> 5 s countdown at hole 1's tee -> per hole every golfer throws (no turn order,
  GUESS); a throw counts when the disc rests > 2.5 m from where it left the hand; on rest the golfer is teleported
  0.7 m behind the disc facing the basket and the disc is put back in hand (GUESS); basket zone = disc centre within
  0.36 m of the pole and 0.42-1.3 m above the basket foot -> holed (CrowdCheer01, [VFX_DiscGoalRays], GolfClap at or
  under par, the [DiscGolfManager] score word); hazard (water kill boxes, red panels, out-of-bounds Cubes, outside the
  course area, 15 m below the lowest tee/basket) = the throw + 1 penalty, golfer and disc back to the last lie (OutSFX);
  bonus panel flown through = -1 (once per panel per game; DiscGolf Propulsion SFX - Powerup 1); mercy at 7 strokes
  (score 7); when every golfer still here is done -> 4 s -> next tee (whole group teleported); a hole open 360 s gives
  the stragglers the mercy score (ours); after hole 9 lowest total wins (most holes played first) -> RoundOver 5 s
  (YouWin / CrowdCheer01) -> MatchOver 12 s (card stays) -> lobby (discs home).
- Card: custom ints (RrDiscGolfScore: 0 hole, 1 count, 2-5 golfers, 6-9 live hole, 10-17 card 5 bits/hole, 18 winner).
  Golfer -> authority EvStatus 100 (live hole packed, resent every 0.5 s until the int matches); to all EvHoled 101,
  EvHazard 102, EvBonus 103. Roster score = total strokes.
- Screen throw: hold click to charge 5 -> 19.96 m/s over 3 s (ThrowDiscGolfDiscToolBehaviorConfig), release throws
  at the crosshair point +6 deg loft, disc flat, nose +3 deg; Q-hold toss 12 m/s. VR: hand release.
- Flight model (DiscGolfDisc fields, GUESS formulas): spin 45 rad/s * v/20; lift 0.8 * 0.4 (ours) * 0.5 * 1.2 * v^2 *
  0.0707 * (0.15 + 2.5 aoa) along the normal while spin > 6; normal turned to up at 0.6/s; wobble x0.9/step; fans
  15 m/s^2 inside their capsule (face -> capsule centre -> far end). Offline sim (perl, scratchpad discsim.pl):
  effective lift factor 0.32 gives ~28 m at 12 m/s/10 deg, ~55 m at 16, ~95 m at 20 (holes are 20-97 m).
- Boards: TMP card beside the console (lobby) and one at the current tee (on the level's tee projector spot, else
  beside the tee); yellow beam (40 m) over the current basket; head-following HUD line (hole, par, strokes, power bar)
  and a toast for hole / score word / hazard / bonus / winner.
- Lighting: RrEnvironment.SetArea(scene key) as LaserTag (DiscGolf / DiscGolf_Propulsion rows exist).

## Result (basic form)
DATA: Assets/#User/SomniumRecRoom/Data/DiscGolf.bytes (969,131 B), DiscGolf_Propulsion.bytes (904,041 B) + .meta.
CODE: Games/DiscGolf/{RrRegistry.DiscGolf, RrDiscGolfCourses, RrDiscGolfLevel, RrDiscGolfDisc, RrDiscGolfScore,
RrDiscGolfRoom}.cs (+ .meta, folder .meta). Reachable: registry (Sports, 1-4) -> Rec Center SPORTS & PVP door
(RrCategoryDoor already lists DiscGolf) and the watch Play list. Untested in play (no harness per brief).

## Requests for the integrator
1. Run DN/tools/rrscene/bankscene.sh once so RrMain carries Data/DiscGolf.bytes + DiscGolf_Propulsion.bytes (without
   them each course builds plain stand-in greens/baskets and logs "the scene does not carry Data/...").
2. No shared-file edits (RrActivity.DiscGolf, the RegisterDiscGolf hook, the environment rows and the category door
   already existed).

## Open issues / GUESS
- Untested in world. First tour: watch "DiscGolf: Lake built in", "hazard volumes (N out of bounds), F fans, B bonus
  panels" (Lake: ~33 hazards, 0 fans, 3 bonus; Propulsion: 11 hazards + 53 OOB?, ~16 fans, 8 bonus), "disc
  (DiscGolfDisc): 0.30 m", "throw 1 (screen) at .. m/s", "throw counted: .. m", "IN THE BASKET".
- Throw range / lift tuning is offline only (LiftScale 0.4); VR throws may feel strong or weak - tune LiftScale and
  ScreenLoft in-world. Screen curve (maxThrowCurveAngle 30 from mouse motion) not done: screen throws fly straight.
- Auto-teleport to the disc + auto re-give, no turn order, hazard = throw + 1, mercy score 7, OOB Cubes: GUESSES.
- Trees are thinned to coarse leaf blocks (isle_thin S c5 on the Lake); tree trunks keep the level's capsule colliders.
- Lake ground: the 4 m terrain collider strips sit 8 cm under Rec Room's ground (= where the far grid is drawn), so
  baskets outside the lobby's near grid float ~8 cm; holes 1-9 baskets/tees are on the far grid except the lobby.
- Propulsion water (LakeSurfaceDistort, collider) lets discs rest on the water (no hazard) - matches "standing on the
  water" but whether discs were hazarded there is unknown. Moving walls, the "troll fan" behaviour and fan strength
  (15 m/s^2) are GUESS; fans push only discs, not players.
- Co-op Catch mode not built. Disc skins / rewards skipped.
- Spectators joining mid-round just watch (not golfers until the next START).
- 21:05 DN/iter15/discgolf_work keeps only Lake_final.rrsk / Prop_final.rrsk (the exact input of the .bytes), the
  coverage maps, logs and two gtscene renders; the 300 MB of jt/prep/thin intermediates went to the session scratchpad
  (regenerate with the pipeline above: ~30 s convert + ~5 min thin for the Lake). DONE (basic form).
