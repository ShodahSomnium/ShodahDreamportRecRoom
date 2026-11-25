# rplaces progress (research FINAL Dorm Room + Rec Center)
Started 2026-09-24. Deliverable: DN/research/rr_latest_places.md. Research only, no CODE edits.

## Milestones
- [ ] 0. Read ARCHITECTURE s1, rr_reccenter s3, rr_dorm_core headings
- [ ] 1. Wiki JSON + steamnews search
- [ ] 2. Web (fandom API, reddit, wayback)
- [ ] 3. rrtool idx search (RecCenter/Dorm/hub/lobby/window textures+prefabs)
- [ ] 4. RrExport.cs + export_art.json + run (max 4 runs), look at PNGs
- [ ] 5. Write doc sections (Dorm a-d, Rec Center a-d)

## Notes
- M0 done (read ARCH s1, rr_reccenter s0-1).
- M1 steamnews (PowerShell extract -> rplaces scratch steamnews_hits.txt in session 1c09... scratchpad/rplaces):
  * CORRECTION: "New layout, fresh paint, some nice big windows" is the 2018-08-28 "^RecCenter edition" (gid
    2444779293805621016, gif i.imgur.com/I9ztiiE.gif), NOT Sep 2020. Wiki Rec_Center history agrees: "August 28, 2018
    Rec Center receives new redesign to the current version". 2017-06-08 = big room with many doors.
  * Later Rec Center changes: 2019-04-23 lighting/render overhaul; 2019-10-15 kiosk collision, community board;
    2019-11-21 revised merch booth layout, basket makeable on screens; 2020-01 donuts/plates flicker; 2020-09-09 cakes in
    "Rec Center Cafe"; 2021-06-02 doors -> room search menu; 2021-06-25 "Facilities has removed a few things from the
    Rec Center to make room for some changes"; 2021-07-09 Magic Door in Rec Center; 2021-09-22 Friend-O-Tron fix;
    2022-02-02 "RecCenter storefront" (RR+ seasons); 2023-01-25 NFL collab in RecCenter; 2023-01-31 coffee-making
    ambient sound in Rec Center; 2024-04-02 "Removed slideshow from Rec Center, hoping to use that space for something
    different"; 2025-08-12 gift packages spawn in dorm/Rec Center; 2025-09-23 dorm door defaults to ^RecCenter
    (Watch>Settings>Experience for Roomiverse); Grow/Shrink potions "in the Supplies store or the Rec Center".
  * Dorm: wiki Dorm_Room: 2023-07-07 Rec Center door -> "Room-i-verse" door, community board -> Play board;
    Sep 2025 Rec Center door re-added; contents list (mirror, loft bed+desk+radio, Play board, Code of Conduct + Privacy/
    Help buttons, 2 posters Block/Mute + Friends, hoop over dustbin, ladder to 2nd floor, armchair, plants, art supplies
    + canvas 2nd floor). Custom Dorms (2024-08 My Dorms experiment).
- M2 (web, partial): fandom MediaWiki API works with curl (UA header); raw files in session 1c09.../scratchpad/rplaces/w_*.txt.
  Timeline page: 2018-04-27 "New Dorm Room released", 2018-08-28 "New Rec Center released", 2021-09-07 Friend-O-Tron,
  2025-07-28 "Added new rendering engine", 2025-09-22 "Re-added Rec Center door". NO later Rec Center/Dorm rebuild listed.
  Dorm_Room page edited 2023-10-20 "not accurate to the present dorm room" + 2024-10-19 "updated screenshots" -> its
  contents list = the late (2023-2024) default dorm. Weekly Rotation Archive 2025: "Sci-Fi Rec Center" (Apr 2025),
  "Rec Center Booth & Pop-Up Shops", "Medieval Fest Begins!" (Aug 2025) = seasonal dressing of the SAME hall.
  Challenges: new-player dorm challenges (water bottle in dorm, marker + white board in the LOFT, basketball in dorm);
  Rec Center challenges: score a basket, Friend-O-Tron gift. Wiki images exist but are binaries: not downloaded (rule).
- M2b KEY FINDINGS (web archive + local logs):
  * rooms.rec.net (Wayback 2026-05-27 bulk?id=1&id=170126): ^RecCenter RoomId 170126, CreatedAt 2018-08-28,
    PublishedAt 2020-10-08, **BecameRRStudioRoomAt 2025-03-04T19:41:39Z**, MaxPlayers 12, UgcSubVersion 251,
    desc "A social hub to meet and mingle with friends new and old.", image changed (78aup... 2024 -> 50xrbh... 2026).
  * User's own Rec Room log (LocalLow/Against Gravity/Rec Room/Player-prev.log, 2025-07-26): "^RecCenter in scene
    Maker Room", LoadScene(MAKER_ROOM_2) = "ugcr2" + AssetBundle_Download 5.7 s + STUDIO_ASSET_BUNDLE_PROCESSED
    => the final Rec Center is a Rooms-2/RRS room whose geometry came from a CDN-downloaded Studio bundle.
    Dorm: LoadScene(DORM_ROOM) = "dormroom2" (install level file, encrypted) = the 2018 dorm (kit matches).
  * HTTPCache (418 MB, 2022 files, big .data files dated 2025-07-26/08-07) probably holds that Studio bundle.
    NOT opened (outside the approved sources) -> open question for the user.
  * Class of '87 Reunion (22-30 Jan 2026): TEMPORARY recreated classic Rec Center (modified ^LockerRoomArchive by
    @happyvr), rec.net/creator/p/class-of-87-reunion (Wayback 20260323). Regular ^RecCenter still existed.
  * YouTube (text metadata only): 2025-08-25 "YOU Didn't Notice The NEW Rec Room Rec Center UPDATE"; 2025-09-30 "The
    REC CENTER DOOR IS BACK!"; 2024-10-14 "This Is GONE From The Rec Center Now..."; 2023-03-13 "Rec Center Changes";
    2022-06-23 NBA skin stripped. Content of the videos not accessible as text.
- M4 export: wrote SP/unityharness/RRHarness/Assets/RrHarnessTests/RrExport.cs (+.meta d4b2f7b1...) and
  SP/harness/scenarios/export_art.json. Run 1 (exit 0): 50 written, 0 missing -> SP/harness/out/export/*.png.
  Next: LOOK at the PNGs, write doc.
- M3 bundle evidence: (PlayCommunityBoard) 6ef67129 key 3 #7018542257515072498 (= the 2023+ dorm Play board UI),
  (PlayHighlight_CategorySelect)CommunityBoardVariant key 0 (Nov 2024 "categories page on the bulletin board in the dorm");
  NUX notebook posters Nux_Stop_Gesture2 (6a69c4f2 key 0), Nux_Shake_hands2 (3c03b87a key a), Nux_Fist_Bump2 (1a733ad6
  key 6), Nux_Open_Watch2 (a754c58d key 4) + PSVR variants; Sandbox_PopUpShop_1..10_Storefront (2025 "Rec Center Booth &
  Pop-Up Shops"); (Sandbox_MagicDoor) (2021); Friend-O-Tron bbd; doors (Sandbox_RoomDoorV2) #-5656950724883909510.
  Pool table was in ^Bowling (2020 "Pool Shark"), NOT the Rec Center. 2018-09-14: "screens upstairs in the Rec Center"
  (slideshow) => the 2018 hall has an UPSTAIRS. Jan 2019 (Social Dorm ed.): info kiosk in the middle, full ping-pong
  tables, 2nd basketball hoop, moved Merch Booth / Puzzles & Art / Mirror / Community Board / Dart Board; "projector
  room" with Cheer + Go to Room buttons.
- Looked at PNGs: campus map (vaulted-roof hall + lower wings + clerestory windows), HomeButton/Menu illustrations,
  CustomLoadingScreenDefault (warm interior, big dark-framed windows), NUX posters, bunk bed purple bedding.
- NEXT: write doc (sections Dorm a-d, Rec Center a-d, bundle table, questions).
- Copied all export PNGs + crops/contact sheets to SP/research/img/places/ (durable copy; harness out/ may be wiped).
- Writing doc now (part 1: summary, sources, Dorm).
- Doc part 1 written (sections 0, 1, 2 Dorm a-d). Next: section 3 Rec Center a-d, 4 PNG table, 5 bundle evidence, 6 questions.
- Doc complete (sections 0-7). Next: verify facts (magic door key, hoop root height), final check.
- DONE 2026-09-24: DN/research/rr_latest_places.md complete (0 summary, 1 sources, 2 Dorm a-d, 3 Rec Center a-d,
  4 export tool, 5 bundle evidence, 6 questions, 7 builder notes). Fixed hoop root height (rim + 0.335).
  Files touched: DN/phase1/rplaces.md, DN/research/rr_latest_places.md, SP harness RrExport.cs(+meta),
  SP/harness/scenarios/export_art.json, SP/research/img/places/* (PNG copies). CODE untouched. Harness runs used: 1 of 4.
