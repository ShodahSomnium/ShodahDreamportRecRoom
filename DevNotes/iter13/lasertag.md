# iter13 lasertag - Laser Tag basic form

## Plan
1. Read: Paintball room/registry/maps, Jumbotron registry, RrSceneBank, rrsk2bytes, gtscene jt=1, research rr_latest_rgames_action.md (laser tag), refdata apk INDEX.
2. Convert Hangar (level76) + Cyberjunk City (level77) with gtscene jt=1 -> rrsk -> CODE/Data/LaserTag_Hangar.bytes, LaserTag_CyberjunkCity.bytes.
3. Games/LaserTag/: RrLaserTagRegistry.cs (RegisterLaserTag), room (compose/subclass Paintball), maps, weapons (install laser guns), music Arena_*.
4. Compile check 0 errors.
5. Notes + shared_requests.

## Log
- 14:15 read Paintball room (sealed; compose), RrGameRoom, RrJumbotronArea/Hull/Look (public; reuse RrJumbotronArea to build jt=1 plates + kit + lighting registration).
- 14:15 gtscene prep jt=1: refdata/apk/lasertag/LaserTag_Hangar.rrsk (48.8k plates, 6.5 MB), LaserTag_CyberjunkCity.rrsk (167k plates, 18 MB). gzip 0.9 / 2.7 MB raw -> need rrsk2bytes size check (<=3 MB total).
  cmd: bash DN/tools/gtscene/run.sh prep NSP/apkwork level76 LaserTag_Hangar DN/refdata/apk/lasertag/LaserTag_Hangar.rrsk jt=1 stats=NSP/lt13/Hangar.tsv (level77 CyberjunkCity likewise)
- 14:25 Data: rrsk2bytes --quant 2 (1 cm, like Golden Trophy) -> CODE/Data/LaserTag_Hangar.bytes, LaserTag_CyberjunkCity.bytes (~3.1 MB total; lossless was 3.47 MB; BarrelTrashPipes_Mat curved props = 22% of Cyberjunk).
- 14:35 design (files in CODE/Games/LaserTag):
  RrLaserTagRegistry.cs (hook), RrLaserTagMaps.cs (2 maps: data names, slots, music, power weapon GUESS),
  RrLaserTagLevel.cs (wraps Jumbotron's RrJumbotronArea: kit + jt=1 plates + lighting; markers -> team spawns
  Match Start / Random, weapon spots, projectors, exit door -> portal + arrival), RrLaserTagRoom.cs (RrGameRoom:
  lobby inside each level; chosen map built at slot A/B, switch = build other slot, move lobby players, free old;
  countdown 10 s, 480 s / 400 pts, 10 per enemy kill, -10 self-destruct, 100 HP, respawn 5 s (GUESS), 3 s invincible,
  pistol at spawn, level pickups, console with MAP), RrLaserTagPickup.cs (networked pickup EvPickup 64),
  RrLaserTagGrenade.cs (Arena_Grenade throwable: 3 s fuse, 100 dmg 6 m, opponents + self, EvBlast 65). No bots (open).
- 16:30 RESUMED (attempt 2): no code yet; Data/LaserTag_*.bytes exist (3.08 MB). Re-read RrGameRoom + RrPaintballRoom; next: RrJumbotronArea, racks/weapon spots, console, then write code.
- 16:xx measured from the data: Hangar lobby = x 33-51, floor top y 1.75, console = G scoreboard (31.78,1.75,-2.9) yaw 79 (+Z to lobby);
  CJ console (-53.52,6.25,-53.21) yaw 274. Spawns team=blue/red, path "Match Start" x4 / "Random*" 24-26 per team. weapon_pickup
  set=Weapons: Hangar 8 grenades, 2 rail, 4 auto, 2 shotgun; CJ 12 grenades, 3 rail, 3 auto, 6 shotgun. NO power weapon marker
  -> GUESS spots: Hangar orb launcher on the centre-pit pad (0.49,-0.19,0.04) box 1.23 m; CJ beam gun at (-10.46,2.07,-57.82).
  Hangar lobby has level audio A FoodCourt (kit plays it) -> no lobby music there; CJ lobby Arena_CyberJunk_Explore03b.
  Slots: Hangar anchor (0,0,0), CJ anchor (-300,0,0) (local x 217..345); the old level is freed after lobby players move.
- writing code now: RrLaserTagRegistry/Maps/Level/Room/Pickup/Grenade.cs in Games/LaserTag.
- 16:42 written: RrLaserTagMaps.cs, RrLaserTagLevel.cs, RrLaserTagPickup.cs, RrLaserTagGrenade.cs. Next: RrLaserTagRoom.cs, RrLaserTagRegistry.cs, compile.
- 16:47 written RrLaserTagRoom.cs + RrLaserTagRegistry.cs + metas; compiling
- 16:50 compile: 0 errors in a copy without Interact/MakerPen (makerpen job's in-progress file RrMakerPenTool missing in the live tree -> 8 errors there, not mine); base boards face the spawns' mean forward

## Result (attempt 2)
Files (all new, Games/LaserTag/ + .meta, folder meta; Data/ from attempt 1):
- RrLaserTagRegistry.cs  RegisterLaserTag: "Laser Tag", PvP, 1-8 players, T-shirt art (same crop as RrWatchArt), Music null
  (Hangar lobby = the level's own A FoodCourt loop; CJ lobby music from the room), Create -> RrLaserTagRoom.
- RrLaserTagMaps.cs      names, scenes (= data names = RrEnvironment keys), anchors (Hangar 0, CJ (-300,0,0)), music, power weapons + GUESS spots.
- RrLaserTagLevel.cs     one map via Jumbotron's RrJumbotronArea (kit + jt=1 hull + dress + lights + floors + RegisterWholeRoom);
  reads spawn (team=blue/red; path "Match..." -> MatchStart, else Respawns), weapon_pickup set=Weapons (Arena_*), scoreboard
  marker -> console pose; 8 lobby arrivals 3.5-5 m in front of the console, floor-snapped (raycast Environment layer);
  fallback 24 m floor if the data file is not in the scene (RrSceneBank.Text null) or has no spawns.
- RrLaserTagRoom.cs      RrGameRoom: 400 pts / 480 s, +10 per tag, -10 self-destruct (own grenade, fell out; clamped at 0 GUESS),
  100 HP (GUESS), respawn 5 s (GUESS) farthest-from-opponents, 3 s invincibility, pistol at spawn/respawn, no friendly fire
  (RrCombat.FriendlyFire=false + Accepts), own-grenade self damage, countdown 10 s teleports to Match Start (rank per team),
  join-in-progress -> smaller team + respawn point, fall/out-of-bounds -> out, console (TEAM BATTLE, MAP: HANGAR/CYBER JUNK
  CITY, START; "LOADING ..." status while building), scoreboards (lobby + each base wall), REC CENTER portal on the lobby wall
  opposite the console, map switch = build new slot -> move lobby players -> free old; RrEnvironment.SetArea(level scene).
  Events: EvPickup 64 (map, spot), EvBlast 65 (room-local dm); ReqMap 16 GameRequest.
- RrLaserTagPickup.cs    networked field pickup (visual prefab + press box + VR grip), 15 s respawn, power weapon 60 s (GUESS).
- RrLaserTagGrenade.cs   (Arena_Grenade) arm on trigger / on release, 3 s fuse, 100 dmg r 6 m falloff 0.71->0.2, opponents +
  thrower, (Arena_Grenade_Explosion_VFX); sounds = paint grenade clips (GUESS, Arena grenade clips undocumented).
Compile: 0 errors for my files (copy of the tree without Interact/MakerPen, which another job had mid-write).
Open: no bots (Rec Room fills Laser Tag with tread/bat bots, 2 pts / elite 10), no team radio, no per-player tag counts,
  no "+10" floating notices / Arena HUD, dual wield, power weapon spots are guesses, travel BuildTimeout 90 s vs CJ build time
  (167k plates + 1712 renderers) unmeasured.
Harness (integrator): {"goto":"LaserTag","timeout":300} {"expect_log":"Laser Tag built","timeout":60}
  {"call":"SomniumSpace.Worlds.SomniumRecRoom.Games.LaserTag.RrLaserTagRoom.TestConsoleStart"} {"wait":14}
  {"call":"...RrLaserTagRoom.TestPickupAndBlast"} {"call":"...RrLaserTagRoom.TestEndSoon"} {"wait":22}
  {"call":"...RrLaserTagRoom.TestCyberJunk"} {"expect_log":"map switch: now on Cyber Junk City","timeout":120}
- 16:51 console START greyed while the chosen map builds (CanStart); FULL project compile (--editor): 0 errors, API guard all proven. DONE.
