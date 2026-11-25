export const meta = {
  name: 'rr-wave2b',
  description: 'Foundation fixes from wave 2a requests + Laser Tag/Showdown, Rec Royale, Soccer/Disc Golf, Bowling, Stunt Runner',
  phases: [{ title: 'Build' }],
}
const SP = 'C:\\Users\\derii\\AppData\\Local\\Temp\\claude\\D--Dreamport-Rec-Room-Rec-Room\\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\\scratchpad'
const SPU = SP.replace(/\\/g, '/')
const COMMON = `
# Dreamport: Rec Room - wave 2 (places and games)
Rec Room played inside a Somnium Space world (Unity 6000.3 URP; desktop screen mode AND VR; Somnium avatars untouched).
Code: D:\\Dreamport\\Rec Room\\Rec Room\\Assets\\#User\\SomniumRecRoom (asmdef SomniumRecRoom). The Unity project is open in the
user's editor: add/edit files only; never run Unity on it.

READ FIRST (economically - grep headings, read only the sections you need; do not print whole big files):
1. ${SP}\\design\\ARCHITECTURE.md (contract; §11 = registering your room through its RrRegistry partial hook).
2. ${SP}\\design\\wave1_results.md (foundation APIs) and ${SP}\\design\\wave2a_results.md (how Dorm, Rec Center, Paintball,
   Charades, Dodgeball/Paddleball and the quest framework were built - reuse their patterns; Games/Paintball and
   Games/Dodgeball are good references for a team game on RrGameRoom). Then the public APIs you use in Core/, Player/,
   Interact/, Net/, Rooms/ (RrPortal for doors), Watch/, Games/Shared/.
3. The research doc(s) for your area (below): measured Rec Room data and rebuild plans.
4. ${SP}\\research\\somnium_conventions.md §11-13, §24 - skim.

"Done" for your game: registered via its RrRegistry partial hook (title, subtitle, description, category, min/max players,
cover/icon art names per ${SP}\\research\\rr_watch_ui.md, music); built at its origin by Build() (coroutine, yield between
chunks, BuildProgress) from Rec Room's prefabs where the bundles have them (RrSpawnOptions.Scenery() for scenery,
RrTools.MakeTool for props) plus RrBuild/RrLook geometry per the research rebuild plan; plays like Rec Room (rules, timings,
numbers, sounds, music, announcer, effects) via RrGameRoom; exit door to the Rec Center (RrPortal); desktop and VR; offline and
networked (RrNet.Instance custom ints, events type >= 64 with repeat-until-seen requests, RrNetBody props, RrNetActors AI);
things drawn for other players only in the same instance.
Verify in the play-mode harness: scenarios ${SP}\\harness\\scenarios\\<activity>_*.json (boot, {"goto": "<RrActivity name>"},
several shots, static test hooks via {"call": ...}); run "bash ${SPU}/harness/run.sh <scenario-name-without-path>" (one Unity at a
time, it queues; exit 3 = compile error somewhere: wait and retry). LOOK at the PNGs and fix what does not look like Rec Room.
At most 4 runs. NOTE: the harness compiles with Unity's real compiler, which is stricter than the offline check in places
(e.g. it rejected CS0136, a lambda local shadowing an outer local) - avoid shadowing.
Compile: "bash ${SPU}/compilecheck/build.sh project --editor" -> 0 errors, guard proven. Errors in other agents' folders are
theirs (wait ~2 min, re-run). New files: "bash ${SPU}/tools/metas.sh". Edit ONLY your folder(s) (below).
TOKEN ECONOMY: write code files early and keep them compiling; small command outputs; no re-reading; concise result.
Return: files, the player's experience, verification (scenarios, PNGs, what they show), not done, requests, and a README
section in the house style (dated "(2026-09-24)").
`
const SCHEMA = { type: 'object', properties: {
  files: { type: 'array', items: { type: 'string' } }, experience: { type: 'string' }, verified: { type: 'string' },
  notDone: { type: 'array', items: { type: 'string' } }, requests: { type: 'array', items: { type: 'string' } },
  readme: { type: 'string' },
}, required: ['files', 'experience', 'verified', 'notDone', 'requests', 'readme'] }

const TASKS = [
 { key: 'fixes', prompt: `
## Your job: foundation fixes requested by the place/game agents (you own Core/, Interact/, Games/Shared/, Rooms/, Watch/,
Player/, Editor/ for this job; do NOT touch Places/ or other Games/<Name>/ folders - other agents are editing games now, so keep
every existing public member working)
Read the "Requests" and "Not done" lists in ${SP}\\design\\wave2a_results.md, then implement, most valuable first:
1. Interact: RrSeat (sit in couches/armchairs/stools: desktop click or VR trigger-point -> Bridge sit pose if a proven member
   exists, else freeze movement + teleport to the seat and look direction; stand with grab/move) and an API rooms can call on a
   seat node; RrTools.MakeTool option to skip RrRespawnable (respawn: false); RrBall scoring through a Scenery-spawned hoop at
   Scale 0.94 when the ball is launched by setting its velocity (repro RrDormTest.ShootBasket in scenarios/dorm_room.json) - find
   and fix the cause; RrProjectile hitting an RrBreakable breaks it.
2. Games/Shared: RrGameRoom 'protected virtual bool Announces => true' (quests turn off Ready/Gameon/Gameover); RequestStart
   repeat-until-seen; RrScoreboard settable Title after Create and a separate info line; RrLoadout.Replace(prefab, side);
   RrWeapon thrown grenade/proximity mine class per rr_shooters.md (Rec Room 'Grenade': 3 s fuse once armed, 100 splash within
   r 6 m, falloff); bow draw (VR second hand pulls 0-0.6 m, desktop hold 0-1 s scales arrow speed/damage 17-67); RrEnemy static
   OnAttackStarted(RrEnemy, Vector3 aim) event.
3. Rooms/Watch: cover art 'GiftBox_*_Tex|x,y,w,h' names are not found for any game ("Watch: picture not found in Rec Room's
   bundles: GiftBox_Charades_Tex|8,276,380,268") - fix the lookup/loading (the Charades agent suspects loading via container key
   '0' in bundle 56e54a941a1085685fd924215dfa9...); RrPortal door glow ('the door glow's material has no _TintColor: glow off')
   - find DoorGlow_Mat's real colour property after RrMaterials.Fix and restore the hover glow.
4. Player: a smaller/lower screen-mode hold pose for pens and small tools (RrHand.ScreenOffset per tool: let RrGrabbable carry an
   optional screen hold offset) ; expose a read-only 'held tool pose' for remote desktop players if cheap.
5. Editor/scene: make the world scene carry the stock shaders our code creates materials from (URP Lit, Unlit, Particles/Unlit,
   plus any Shader.Find names used in the code - grep for Shader.Find) through RrMain._shaderCarriers: create carrier .mat assets
   in Assets/#User/SomniumRecRoom/Materials/ (hand-written YAML with the URP package shader GUIDs - find them under
   D:\\Dreamport\\Rec Room\\Rec Room\\Library\\PackageCache\\com.unity.render-pipelines.universal*\\Shaders\\*.shader.meta) and wire them into
   the scene through the Editor tool, regenerating the scene with ${SP}\\harness\\make_scene.sh (read ${SP}\\harness\\README.md),
   then check_scene.sh. Keep every existing scene object and value.
6. Harness: run.sh must accept an absolute or scenarios/-relative path as well as a bare name.
7. Compilecheck: make the offline compile catch what Unity's compiler rejects (CS0136 lambda shadowing slipped through): find why
   (langversion, compiler version, /features) and fix ${SP}\\compilecheck\\build.sh.
Run the smoke/dorm scenarios at the end to prove nothing regressed. Report each fix.` },
 { key: 'lasertag', prompt: `
## Your games: Laser Tag and Rec Room Showdown (folders Games/LaserTag/ and Games/Showdown/; hooks RegisterLaserTag,
RegisterShowdown; Category "PvP")
Research: ${SP}\\research\\rr_shooters.md - laser tag (weapons: pistol, burst, shotgun, railgun, orb launcher, beam cannon,
grenade with exact stats; 400 points or 480 s; 10 per player, 2 per bot, 10 per elite bot, friendly-fire penalty; arena rebuild
proposal; sounds/VFX/UI) and Showdown (2022 Wild West cork-gun 3v3, first to 25 hits in 4 minutes, Sarsaparilla Springs; not in
the bundles - rebuild from the Western/props kit you can find + procedural town).
Laser Tag: a neon arena (dark with glowing trim, cover blocks, ramps, two bases), team battle to 400 points / 480 s with Rec
Room's laser guns (at least pistol + one heavier weapon picked from a rack), health 100 with regen per research, respawn,
scoreboard, announcer, music. Bots are optional (if cheap: a few RrEnemy-based laser bots worth 2 points).
Showdown: a small western street/town, 3v3, cork guns (use Rec Room's cork/pistol-like prefab if found, else the paintball pistol
re-skinned with cork projectile visuals), first to 25 hits or 4 minutes.` },
 { key: 'recroyale', prompt: `
## Your game: Rec Royale (folder Games/RecRoyale/; hook RegisterRecRoyale; Category "PvP"; MaxPlayers 16)
Research: ${SP}\\research\\rr_shooters.md - Rec Royale (island, drop from the gondola/glider with its timings, loot tiers and the
decoded RecRoyaleRangedWeaponTaxonomy (damage per rarity, loot weights, magazines, headshot multipliers, legendary names), potions
and ammo packs, shrinking circle, squads), Rec Royale UI/prefabs/sounds (RecRoyale_* clips).
Build: an island that fits within +-700 m of the origin (terrain from meshes/procedural hills, a few landmark areas with
buildings/cover, loot spawn points), the drop (glide down from a start height with the glider sound/wind; desktop and VR
control via RrInput/RrHand), loot pickups (weapons by rarity with Rec Room's rarity colours, ammo, potions/shield), the storm
circle (visible wall, damage outside, stages with timings), last player/squad standing wins, spectate after death, Rec Room
sounds/music/UI. Keep it performant (few draw calls: combine procedural terrain meshes; limit props).` },
 { key: 'soccergolf', prompt: `
## Your games: Soccer and Disc Golf (folders Games/Soccer/ and Games/DiscGolf/; hooks RegisterSoccer, RegisterDiscGolf; Category "Sports")
Research: ${SP}\\research\\rr_sports.md - soccer (ball key 00, soccer shield key 28, 180 s match, walk speed 6, teleport 8 m,
Rec Room soccer uses hands/shields; goals from the sandbox goals; arena plan) and disc golf (disc key 9d, basket key c, 9 holes,
mercy 7 strokes, stroke counts after 2.5 m, score labels "Birdie -1" etc., disc flight settings lift/spin/stability/curve,
screen-mode throw 5-20 m/s; Lake course trees/rocks; Propulsion gizmos).
Soccer: a walled pitch with two goals, teams, the Rec Room ball (RrNetBody with touch-steal), shields/hands to push the ball,
kick-off, goal celebration VO/sounds, 180 s match. Disc Golf: the Lake course (9 holes with tees, baskets, trees, water, rocks
from the bundles), stroke counting per player, disc flight model per research, scorecard, turn/hole flow with up to 4 players.` },
 { key: 'bowling', prompt: `
## Your game: Bowling (folder Games/Bowling/; hook RegisterBowling; Category "Sports"; up to 6 bowlers)
Research: ${SP}\\research\\rr_sports.md - bowling (ball key a5a, pin key a2a, lanes "Lone Shoe Lanes": one scoring lane plus two
practice lanes, regulation pin spots, machine cycle sweeper + ball return, jukebox/stool/pool set placements (room >= ~34 m),
screen-mode throw 4.5-9 m/s, sounds Bowling_*, music Bowling_MusicGameplay), and somnium_networking.md §5.5.5 (pins simulated
by the bowler's client, PinStream event at 10 Hz during a roll, standing mask in instance ints).
Build the bowling alley room (lanes with gutters, approach, ball return and rack, pin deck, sweeper animation, scoring monitors
above the lanes, seating, jukebox), 10-frame scoring with strikes/spares (Rec Room's MegaStrike sound), turn order for up to 6,
pin physics tuned so strikes happen, ball pickup from the return.` },
 { key: 'stuntrunner', prompt: `
## Your game: Stunt Runner (folder Games/StuntRunner/; hook RegisterStuntRunner; Category "Party"; up to 4 players)
Research: ${SP}\\research\\rr_party.md - Stunt Runner (the full sound-stage kit with keys: floor, ceiling, walls 98 x 114 x 35 m,
start gate with 4 lanes and race lights, finish gate, checkpoint arch, KO Cola cans (x1.45 speed for 2 s), bounce pads, rope
nets, crash pads, jumbotron; announcer/crowd/music clips; Rec Room movement constants: slide, wall-run, climb, clamber - the six
courses are not in the bundles, design one or two), ${SP}\\research\\somnium_player.md §6 (locomotion control options for
wall-run/slide/boost through the Bridge: SetMovementSpeed/SetJump/SetGravity; desktop play mode).
Build the sound stage with the kit, one or two obstacle courses (start gate with lights countdown, platforms, ramps, bounce pads,
rope nets, KO Cola boosts, checkpoints with respawn at the last one when falling, finish gate), race flow for up to 4 with times
and placings on the jumbotron, SR_Announcer lines and crowd, music. Use Bridge locomotion multipliers for boosts/bounce (no
avatar modification).` },
]
phase('Build')
const results = await Promise.all(TASKS.map(t => agent(COMMON + t.prompt, { label: t.key, phase: 'Build', schema: SCHEMA })))
return results.map((r, i) => ({ key: TASKS[i].key, result: r }))
