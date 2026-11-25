# Iteration 15 - opt-in Rec Room avatar (2026-10-17)

Status: BUILT, compiles (0 errors, server-like + editor). NOT run in Unity or the client yet (no harness by request).

User ask: "BE Rec Room avatars, with customisations, saved; or just wear your Somnium avatar".
Rules: default = Somnium avatar unchanged. Wearing the RR avatar only hides the Somnium avatar's renderers
(forceRenderingOff, as RrIsolation does), reversibly; never destroy/reparent/retarget/rescale; never touch the client
camera. Leaving the world restores everything.

Tools: rrtool dll in scratchpad `rrtoolbuild/bin/out/rrtool.dll` (index in DevNotes/rrtoolidx). A tiny catalog query
tool (scratchpad `av/catq`, sources = Rr/RrCatalog.cs + deps + Main.cs) loads the world's own catalog cache
(LocalLow/Somnium Space Ltd/Somnium Space VR/SomniumRecRoom/rr-catalog-*.bin) and prints whether a name is an
Addressables container and what loads it. Every piece below was checked to be a container (loadable).

## 1. What the install has (findings)

Three avatar body systems are in the bundles:

| prefix | body | where | notes |
| --- | --- | --- | --- |
| (none) / BB_ | **classic bean**: floating head, floating torso, floating hands, no arms, no legs | head/torso/hands in `f124b399` (the 13 MB UI+avatar bundle) | 137 BB_ outfit items (52 hair, 35 shirts, 15 hats, 7 shoulder/back, 5 facial hair, 4 glasses/eye, 4 wrist, 3 neck, 3+3 ear, 2 belt) |
| MB_ | "modern bean": one skinned body with arms, skeleton + VRIK (FinalIK) | `38066d85` MB_Avatar_Skin_fbx, `1df8e0ef` (PlayerAvatarModernBeanBody) | only torso items exist as MB_ (shirts, wrist, belt, neck, shoulder) |
| FB_ | full body (legs), VRIK | `edf7f2d6` FB_Avatar_Skin_asset | FB_ items incl. legs, shoes |

**Chosen: the classic bean (BB)** - it is the iconic Rec Room shape, every BB item is a single-bone skinned mesh
(Head.Bone / Torso.Bone, or the hand rig), so no IK/skeleton is needed: an item renders = our bone x its bindpose.

Classic parts (all containers in f124b399, loader = their own key):
- `Head_Shapes_fbx` (prefab): Head_LOD0-2, Ears_LOD0-1, Face (all SMR on one bone `Head.Bone`; Head.Bone world = identity
  rotation at y 1.68 in the authoring frame, Head.Bone_end +0.171 up).
- `Torso_Shapes_fbx`: Torso.Geo (+LOD1/2) on `Torso.Bone` (authoring y 1.15, bone end +0.352). Head-torso gap 0.53 m.
- `Hand_fbx`: Hand (+LOD1/2), HandPointing LOD1/2, Hand.Shadow, Hand.Pointing (MeshRenderer); rig Hand.Rig > Hand.Root >
  Finger.1-3, Thumb.1-4. One handedness: the (Player) prefab mirrors the left one (Mesh holder under LeftHand
  p=(-0.02,0,0) r=(-0.5,0.5,-0.5,0.5) s=(1,1,-1); right p=(0.02,0,0) same r, s=(1,1,1)).
- Their prefab materials are a placeholder URP Lit ("Lit@490b8c96"); Rec Room assigns at runtime.
- `Skin_Mat` (bd72059a, container): AG/Avatar, Solid_Red_Tex mask, skin colour in _Red_Col/_Base_Col (default
  0.651,0.443,0.325), Skin_Norm/Skin_Spec tiled x10. Used for head, ears, hands (and the bare torso).
- `Face` material (f124b399): shader AG/AvatarFace; props _LeftEyeTexture/_RightEyeTexture/_MouthTexture and
  _LeftEye/_RightEye/_Mouth = "(Anchor, Scale)" (xy anchor in face UV, zw scale), _MouthRotation, _DepthOffset -0.005,
  plus the AG/Avatar colour set. Classic feature defaults (Eyes_Default / Mouth_Default MonoBehaviours):
  eyes anchor L (0.6,0.65) R (0.3,0.65) scale 0.28; mouth anchor (0.5,0.07) scale 0.5.
- Eye styles: 26 bundles each holding the same texture names (Eye_Default, Eye_Blink, Eye_Happy, ...), 256x256,
  containers. Mouth styles: 26 bundles with Mouth_Smile, Mouth_Talking_Open, ... Only some bundles carry the
  feature MonoBehaviour (names: 4643ee96 = classic Eyes_Default, 63847b9f = FB_Eyes_Default, 58eff8d0 = FB_Eyes_Round,
  295901a0 = FB_Eyes_Liner, 95d031ea = FB_Eyes_Anime_Fem, 15520051 = FB_Eyes_Anime_Neutral; 93338bae = classic
  Mouth_Default, 8fcb685a = FB_Mouth_Default, ca96bd9b = FB_Mouth_Bubbly, 680ae6a3 = FB_Mouth_Nonchalant). The rest are
  referenced by Addressables GUID only, so the editor lists eye/mouth styles by bundle, using the texture itself as
  the thumbnail.
- Face feature menu icons exist (`MenuIcon_Mirror_*` in 880a74d7) but map to features via GUIDs; category icons
  `icon_AvatarCustomization_Hair/Headgear/Torso/Face/Accessories/Facialhair/Body/Gloves`, `icon_AppNav_AvatarCustomization`.
- **No item thumbnails** in the install (Rec Room's store art came from its CDN). Items are listed by name.
- Item materials: AG/Avatar with a colour-mask base map (_BaseMap RGBA mask; _Red_Col/_Green_Col/_Blue_Col dye colours,
  _Base_Col, _Dirt_Col with alpha). Default item material usually = Solid_Red_Tex + greys. 455
  `RecRoom.Avatars.AvatarItemMaterial` "..._Combination" assets name the designed variants; their materials are
  containers named mostly `<Item>_<Variant>_Mat` (55/60 sampled: combination name minus _Combination + _Mat).
- SkinColorVault / HairColorVault have no serialized colours (Rec Room's palettes came from the server), so skin, hair
  and dye palettes are our own.
- AvatarModernBodyConfiguration (f124b399): bean head offset 0.04, steering follow 0.04 s, turn dead zone 70 deg,
  head rotation smoothing 0.1 s - used for the torso follow.
- (Player) prefab (c513202a): Head at (0,0.549,0.016) above the torso origin, hands at (+-0.3,-0.189,0.25); PlayerHand
  frame = +Z fingers, +Y thumb, +X out of the left palm (hands_frames.md) = RrHand.Grip * RrHand.RecRoomFrame.

## 2. Design

- `Player/Avatar/` (namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatar):
  - RrAvatarOutfit: the outfit (wear flag, skin, eyes, mouth, 10 item slots each item+colour+style), JSON save,
    presence packing.
  - RrAvatarCatalog: lists built at runtime from the catalog (no big string tables - the string heap is nearly full):
    BB items per category (sorted ordinal => same index on every client with the same install), eye/mouth bundles,
    variant materials by name prefix.
  - RrAvatarRig: one Rec Room avatar (head, torso, 2 hands, items), built from the parts above, driven by poses.
  - RrAvatars: the system ("Rr Avatars" under WorldRoot, RrAvatarDriver LateUpdate order 10020, after the player driver): local + remote rigs,
    presence read/write, hiding of the Somnium avatar through RrAvatarHide, first-person head hiding per camera.
  - RrAvatarHide: hides one player's Somnium avatar renderers (scene root "Avatar for Player [nick] [" + their cloth
    items) with forceRenderingOff, records only what it flipped, re-applies every frame, restores exactly;
    cooperates with RrIsolation (waits while isolation hides the player).
- Network: presence ints 48-58 (11 ints): 48 header (wear bit, version, skin, eyes, mouth), 49-58 one int per slot
  (item 0-11 bits | colour 12-17 | style 18-25). Records are state, so late joiners see outfits with no event.
- Pose: local = RrPlayer head/camera + RrHand grips (VR) or a rest pose (desktop); remote = RrRemotePlayers Head and
  Left/RightHand (the Bridge's networked anchors; desktop players have none -> rest pose). No new pose stream.
- Save: persistentDataPath/SomniumRecRoom/avatar.json; on start the saved choice is applied automatically.
- Watch: "Avatar" page (id 20) + Home tile.

## Build (done 2026-10-17)

Files (new): `Player/Avatar/` RrAvatarOutfit.cs (outfit, palettes, JSON save, RrAvatarWire), RrAvatarCatalog.cs,
RrAvatarRig.cs, RrAvatarHide.cs, RrAvatars.cs, RrAvatarDriver.cs; `Watch/RrWatchAvatarPage.cs`. Metas written with
DevNotes/tools/tools/metas.sh. Namespace `...Player.Avatars` (NOT `.Avatar`: a child namespace named Avatar would shadow
UnityEngine.Avatar inside the Player namespace - RrAvatarHand.cs:893 uses it).

Additive edits elsewhere:
- Core/RrMain.cs: `Step("avatar", Player.Avatars.RrAvatars.Init)` after grab; `RrAvatars.Shutdown()` in OnDestroy right
  after the isolation's shutdown, in its own try (Somnium avatars come back even if a later step throws).
- Net/RrPresenceWire.cs: doc for ints 48-58 + `AtAvatar = 48`, `AvatarInts = 11` (59-63 still spare).
- Watch/RrWatchPage.cs: `RrWatchPageId.Avatar = 20` (20 to stay clear of other new pages).
- Watch/RrWatchUi.cs: `AddPage(new RrWatchAvatarPage())` after the maker page.
- Watch/RrWatchHomePage.cs: 8th tile "Avatar" (Illustration_ClothingCustomizer) after Maker Pen; tiles 190 -> 170 wide,
  gap 20 -> 14 so eight fit beside Quick Access.
- RrIsolation / RrRemotePlayers: NOT edited (the hide cooperates through RrIsolation.IsHidden and runs after it).

How it works (short):
- Rig = Head_Shapes_fbx + Torso_Shapes_fbx + 2x Hand_fbx spawned with RrSpawn (Visual, layer Default, scope 0 so room
  releases never free them); single-bone SMRs re-bound by bone name to our Head/Torso transforms; culling bounds 1.2 m
  around the bone; LOD0 only. Skin = copy of Skin_Mat with every colour channel = tone; face = copy of "Face"
  (AG/AvatarFace) with eye/mouth textures and the classic anchors; blink every 2.5-6 s with the style's Eye_Blink.
- Items = "(BB_...)" prefabs; dye = material copy with _Red_Col = colour, _Green_Col 72 %, _Blue_Col lighter;
  design = the item's "<Item>_..._Mat" materials found by name in the catalog. Top hides the bare torso.
- Pose: head centre = eye + look*(0,-0.015,-0.085); torso = head + yaw*(0,-0.53,-0.02), yaw follows the view with a
  0.15 s lag, leans 20 % of the head pitch; hands VR = grip * RrHand.RecRoomFrame (local) / anchor *
  RrAvatarHand.GripToRecRoomDefault (remote); rest pose otherwise. Finger curl: grip value / held prop (local),
  presence hand prop/item (remote). Hand mesh holders copy Rec Room's (Player) prefab (left mirrored s z -1).
- First person: in beginCameraRendering a rig's head renderers are hidden for a camera within 0.28 m of the head and
  shown again in endCameraRendering; the callback is re-subscribed every LateUpdate so it runs after Thad's mirror
  (which renders the mirror camera from inside the main camera's callback) -> the mirror sees the head.
- Preview: while the Avatar page shows (and 8 s after), a second rig with the outfit stands 1.2 m ahead and 0.85 m to
  the right (placed once, re-placed when you walk 3 m off), always facing you. Local only.
- Remote: per RrRemotePlayers entry, presence record ints 48-58 -> rig (rebuilt only when the ints change); visible
  only while `WithMe && !RrIsolation.IsHidden`; their Somnium avatar hidden only while their record says wearing.

Controls: Watch > Home > Avatar. Top right: WEAR SOMNIUM AVATAR / WEAR REC ROOM AVATAR. Left: Skin, Eyes, Mouth, Hair,
Hats, Tops, Glasses, Facial Hair, Neck, Ears, Back, Belts, Wrists. Middle: choices (None + items by name; eye/mouth
textures; skin swatches). Right: dye swatches (ORIG = the item's own colours) and the item's designs (Default + Rec
Room's variants). Everything applies at once and is saved 0.5 s later.

Risks / unverified (nothing ran in Unity or the client):
1. AG/AvatarFace anchor semantics are inferred from the property description "(Anchor, Scale)" and the classic
   features' DefaultAnchor/DefaultScale: eyes or mouth could sit wrong/upside down. Face falls back to plain skin if the
   shader is unsupported.
2. Hand orientation (holder rotation) and the left hand's negative-scale mirror copy Rec Room's prefab; a skinned mesh
   under negative scale could render inside-out if Unity does not flip culling for it.
3. Finger curl axis (local X, negative = curl) is a guess from the rest pose.
4. The local Somnium avatar is matched by "Avatar for Player [Bridge nickname] [" with a MasterScene-nearest fallback;
   if neither matches, the Somnium avatar stays drawn (warning after 5 tries in the log, "Avatar:" lines).
5. Item/design indices on the wire are positions in name-sorted lists from each client's own install: identical
   installs agree (Rec Room no longer updates), a different install shows a different item.
6. Desktop players show rest hands (no hand data on the wire for them; no new pose stream was added).
7. In VR the open menu's dim plane darkens the preview; it lingers 8 s after closing the page for that reason.
8. Hats and hair both render (no Rec Room hair-fit under hats): some clipping.

In-world test checklist:
1. Watch > Avatar: the page opens, items list per tab, eye/mouth pictures appear, the preview appears to your right.
2. WEAR REC ROOM AVATAR: your Somnium avatar disappears, the Rec Room avatar follows your head and hands (VR) or stands
   with resting hands (desktop). Look in the Dorm mirror: head, face, hair visible; your own view is not blocked.
3. Change skin, eyes, mouth, hair + dye, hat + design, top: each changes at once on you and the preview.
4. WEAR SOMNIUM AVATAR: the Somnium avatar is back exactly (LODs, outfit, name tag untouched).
5. Leave and re-enter the world: the choice and outfit come back (avatar.json in LocalLow/.../SomniumRecRoom).
6. Two players: B sees A's Rec Room avatar + outfit and not A's Somnium avatar; A goes to another room -> B sees
   neither; A switches back -> B sees A's Somnium avatar again. Late joiner sees outfits at once.
7. Player.log lines start with "[SomniumRecRoom] Avatar:" (catalog counts, hide/restore counts, saves).
