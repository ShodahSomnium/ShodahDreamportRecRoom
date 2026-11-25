# iter11 / layout (Jumbotron layout + floating things)

Owner files: Games/Quests/Jumbotron/RrJumbotronArea.cs, RrJumbotronActs.cs, RrJumbotronRoom.cs,
RrJumbotronHaze.cs, RrJumbotronDoor.cs, RrJumbotronWeaponSpot.cs, RrJumbotronBoss.cs.

## Plan
1. Read iter10 notes (jt10_result, jt_audit, impl_*), current code of owned files.
2. Render each room offline (gtscene render into NSP scratch) vs built layout; check arrival/facing,
   door placement, Filter/ClipRadius/Cut drops, X kit stand-ins, heights, overlaps.
3. Haze: remove floating cyan square (act 5 doorway), magenta box/cone (Foyer ceiling), floating
   shaft/laser pieces.
4. ShowAreas: check visibility of next room from current; keep only current except during teleport.
5. Compile-check.

## Log
- (start) notes created.
- FINDING (act 5 cyan square): kit R record `Cube` (1 m) at (184.37, 2.46, 57.66) with ?SciFi_Blue_Emit_Cargo1_Mat
  (APK shader "Shader Forge/Lighting/EmitBake" = a light-bake helper), 9.7 m straight ahead of the arrival at 2.5 m;
  the haze drew it as a teal emissive box. FIX: GlowKind.Hidden (Concession is its only use).
- FINDING (Foyer magenta box/cone): every light shaft is the install's SKINNED mesh LightArchShaft (a rig of 13
  LightArchBone_* bones: 4 root bones = the lamp aperture, Bot/Top_R/L = the far quad on the stage). The kit draws it
  unskinned (bind pose x 18.76) = a solid box/cone hanging by the lamp. 48 drawn shafts in 11 rooms (+2 projector
  shafts, alpha 0). FIX: kit shafts hidden (grey batches skip them, separate objects / the Reception's install-
  material batch switched off); each rebuilt as a soft additive frustum from the 8 measured corner bones
  (NSP/jt11/shafts.pl reads apkwork/jtdump/level38-48.txt, matched to the R records by position; table in the code).
- ShowAreas: grid builds take 8-25 ms (sliced), so a room activated at the teleport is grid-lit within ~0.2 s;
  Space (act 6, open sky) is 250 m from the Garage slot -> keep only the current act active.
- CHANGE 1 (RrJumbotronHaze.cs, compile 0 errors): SciFi_Blue_Emit_Cargo1_Mat Hidden; LightShaft_Yellow_Mat spec
  (0.76,0.39,0.07,0.18); HideKitShafts + IsShaft (kit shaft objects / install-shaft batches off, grey rebuild skips
  them); Shafts/AddShaft/ShaftMaterial: 48 frustums from ShaftData (scene, material, record pos, 8 corners), 4 sides x
  10 x 4 cells, alpha sin(pi s) x smoothstep(t/0.06) x (1-t)^1.3, white-map clone of the glow material, merged per
  material under the haze group. Log line gains "light shafts N rebuilt ..., M kit shafts hidden".
  Backup: NSP/jt11/RrJumbotronHaze.before_shafts.cs.
- CHANGE 2 (RrJumbotronRoom.cs): ShowAreas keeps only the current act on (was current + next); doc comments updated.
- CHANGE 3 (RrJumbotronHaze.cs): GlowKind.Solid (RrLook.Solid, shared, not owned); stand-ins for the commonest grey
  kit props: Box_Cardboard_Discs_Mat -> install Box_Cardboard_Blank_Mat (0.80,0.47,0.09) [Supply's 44 cubes + 1],
  Dumpster_SciFi_Orange_mat -> install Dumpsters_Recolorable_mat tinted (0.80,0.37,0.08) [6],
  Coiled_Wires_Mat -> solid (0.16,0.16,0.18) [38 cable bundles], PropBox_mat -> solid 0.28 [7].
  Compile: waiting on RrJumbotronHull.cs (other agent mid-edit: GlassFrame/Frame undefined).
- Compile after changes 1-3: 0 errors, 0 warnings (server-like 293 files, API guard 118/118, editor runtime + tools 0).
- Offline checks (NSP/jt11/*.png via gtscene render + X boxes along the rebuilt frustum edges, edges.pl): Foyer magenta
  shaft runs from the ceiling lamp (32.3, 1.4, 50.7) down to the stage/marquee (x 14-20); the two cyan tripod shafts
  aim across at the stage; the Stadium's 14 blue + 4 red + 2 cones fall from the trusses onto the arena.
- CHANGE 4 (RrJumbotronBoss.cs): the boss face after the footage (rr_jumbotron_look 2.11): white "> <" chevron eyes
  (4 bars +-26.6 deg), a cyan LED ticker with dark gaps along the screen foot (dies with the screen), a red LED band
  above each screen; pink = #f95dbc; the point light under the block is cyan-blue #32b5fc (y -3.2, r 18, x1.4).
  Compile 0 errors.
- Final compile (after the haze class summary edit): 0 errors, 0 warnings; API guard 118/118; editor runtime/tools 0.

## Layout review (no change needed)
- Anchors = the Player7SpawnPoint centres in every act (Armory: its 4 PlayerSpawnPoints); arrival yaws match the
  levels (Armory -90, act1 0, act2 -90, act3 +90, act4 +90, act5 -90, act6 180, act7 0, act8 +90, act9 -36).
  Offline arrival renders NSP/jt11/arr_*.png match the iter10 shots (corridors / doors ahead as in the levels).
- Exit doors: every act's [Quest_SciFi_RoomExitDoor] + its [Quest_SciFi_RoomDoorTrigger] pad 2.4-3.0 m in front
  (all within 6 m, so the leave zone sits on the pad); act 9 none (victory).
- Cuts: only act 6 (Space) has a Filter (160 x 90 x 180 m); outside it are only 34 tiny EventRoom_Moon plates at
  y 142 and 10 rockvox dress boxes behind the arrival; 0 kit records. Act 9's drop box holds only the Jumbotron
  shell (and one scaffold dress). No X stand-ins in any rrsk (kit logs: 0 stand-ins). No terrain grids.
- Rooms fit within +-100 m of their slot (Space's cut strip -68..92 x, -144..36 z); 250 m apart, no overlap.
- Kit markers are room-local, area markers scene -> a.L(); consoles, portals, pots, weapons, button, boss consistent.

## Result
Files: Games/Quests/Jumbotron/RrJumbotronHaze.cs, RrJumbotronRoom.cs, RrJumbotronBoss.cs (RrJumbotronArea/Acts/
Door/WeaponSpot unchanged).
1. Flight Deck doorway: the light-cyan square (EmitBake Cube) is gone.
2. Light shafts: the solid magenta cone/box in the Foyer ceiling (and the same bind-pose junk in every room: 48
   shafts) are gone; each shaft is a soft additive cone from its lamp to where its light lands (the Foyer's magenta
   one onto the stage/marquee, the Stadium's blue cones from the trusses).
3. Only the current act is active (no next room floating in Space's sky or through hull gaps).
4. Grey kit props: cardboard boxes (Supply), orange dumpsters, dark cables and prop boxes get install stand-ins.
5. Boss: "> <" eyes, cyan ticker, red LED band, cyan-blue glow under him (after the footage).

## Open issues (not in my files)
- LockIcon_Mat dress quads (10 rooms, hull dress): a flat 2.2 m square hovering 0.23 m over each exit pad; Rec Room
  shows it only while the door is locked. The hull could skip "Quad ... m=LockIcon_Mat" or the door could hide it.
- The shaft fixtures (StageLight / tripod meshes, LockerFountainStageLight_mat static batch) are left out by
  gtscene, so the rebuilt cones start at an empty lamp position.
- Mist (Scifi_AreaFog) volumes are mostly rasterised staircases (e.g. Battlefield1 1961 of 2089 cyan plates are
  under 0.3 m2): blocky haze blobs; a proper fix is in gtscene (measure the fog blobs as boxes / hulls).
- Portal mask direction (v bottom->top) is still a guess; the APK meshes' UVs would settle it (gtscene).
- Rebuilt shafts: brightness is Rec Room's colour x alpha with our own soft profile (sin across, (1-t)^1.3 along);
  tune AddShaft if they read too strong/weak in the headset.
