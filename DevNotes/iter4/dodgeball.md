# iter4 dodgeball - Dodgeball's final rules
Status: DONE 2026-09-24 (compile clean, harness 31/31, PNGs checked). Owner files: CODE/Games/Dodgeball/*, harness scenarios/dodgeball_gym.json.

## Facts found (research done)
- Unity PhysicsMaterialCombine enum (compiled check vs 6000.3 PhysicsModule): Average 0, Multiply 1, Minimum 2, Maximum 3.
  (rr_reference_coverage 2.4's "1 Minimum, 2 Multiply" is PhysX's order - wrong for Unity YAML.)
- Install (final build) DodgeballBall_phys (ART #-6279935958218146749, rrtool dump): 0.6/0.6/0.6, friction Maximum(3), bounce Minimum(2).
  REF2 DodgeballBall_phys: same numbers but bounceCombine 1 = Multiply (Studio copy). Install wins (it is what loads).
- REF2 DodgeballCourt_phys (sharedassets11 = encrypted level, not loadable): 0.6/0.6/0.6, Average/Average.
- OUR floor/walls had NO physics material (Unity default 0.6/0.6/bounce 0) -> ball bounce = Minimum(0.6, 0) = 0:
  our balls did not bounce. Fix: DodgeballCourt_phys copy on every gym collider -> Minimum(0.6, 0.6) = 0.6.
- MinPlayers 2 already in the registry; RrGameRoom auto-starts at 2 -> RrTeams.Balance gives 1 v 1. Solo START = practice.
- Screen throw paths: LMB (RrDodgeball.OnUseUp -> Room.ScreenThrowVelocity, was charge-scaled) and Q long-press
  (RrGrab.Throw with grab.ScreenThrow = 14 m/s, not ours). Both 14 m/s after the change.

## Plan / progress
1. [x] CatchOutsThrower = false, remove teammate return (FirstOutTeammate, _outOrder, verdict 'back' field).
2. [x] ScreenThrowVelocity flat 14 m/s (no held param).
3. [x] Barriers: 0.5 m (GUESS) at z = +-8.9, x +-5.0; lobby spawns moved z -9.3 -> -9.8; ball housekeeping keeps balls
       between back line and barrier.
4. [x] Registry text (no catch-outs, 1 v 1, practice).
5. [x] Court physics material + ball collider check/log.
6. [x] (Bounce, Catch in RrDodgeballTest; scenario updated) Test hooks: Catch (hit claim + catch of the same throw -> nobody out), Bounce (drop 1.5 m, rebound height).
7. [x] compile, harness run (max 2).
- code edits done; first compile blocked by another agent's RrQuestDirector.cs error (WaveIntervalScale); waiting
- compile try 2: 5 errors, all in Quests (another agent mid-edit: RrQuestRoom members); none in Dodgeball; retry loop running
- compile CLEAN (0 errors x3, all 113 members proven) at 17:14; harness run 1 starting
- harness run 1: 31/31 OK; bounce e 0.63; catch nobody out; hit -> Blue 1-0. Next: look at PNGs
- PNGs checked (overview, barrier_blue, barrier_red, door, knocked, roundover): barriers read as dark team-colour
  kerbs with white caps and round end posts across each end; the door/START area sits behind the blue one; practice,
  knock and round-over boards unchanged. No second run needed (no code change after run 1).
## Not done (noted)
- Mods (Low Gravity, Dodge-Bombs, Disco Dodgeball, Mod Board): not in this task; all GUESS in detail.
- Barrier size/spot: GUESS (0.5 m, 0.9 m behind the back line, 10 m wide) - no picture or size published.
