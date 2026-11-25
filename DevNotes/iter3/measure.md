# iter3 / measure - progress notes
Task: measure the real final Rec Center from the user's HTTPCache (measurements only). Deliverable: DevNotes/research/rr_reccenter_cache.md
Cache index goes to C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/1c0947a6-bfa3-41e9-ae24-3394d8a31490/scratchpad/cacheidx

## Status
- [ ] classify cache files
- [ ] find Rec Center bundle
- [ ] rrtool override + rebuild (separate output)
- [ ] dump transforms
- [ ] write doc

## Findings so far (13:30)
- Cache: 1011 .data files: 344 UnityFS (Unity 2021.3.29f1, LZ4HC + some LZMA blocks), 482 JPEG, 115 PNG, 2 JSON, 68 other (mostly protobuf-like 0x08.. = room/invention data; left alone).
- REC CENTER = 1AF.data (60.1 MB, 26 Jul 2025 21:13:25 GMT): scene bundle "Assets/s/Assets/Rooms/RecCenter/RecCenter-RecCenter_071825.unity"; 44 deps 183..1AE (same second). Scene readable (roots: InfoKiosk, LightShafts, Props, Light_Meshes, Environment, LLM Door, VendingMachine_Static, BulletinBoard...).
- 35C = MakeItToMidnight-Home (not ours).
- Tool: copy of rrtool in MY scratchpad rrtool_cache/ (env RR_BUNDLE_DIR, RR_IDX_DIR; *.data; LZMA; "bundle#k" selects k-th serialized file: 1AF#1 = scene). Index: my scratchpad cacheidx/.
- 13:45 world dump done: SP(mine)/cacheidx/world_1AF.tsv (2,571 GameObjects, world TRS, mesh/material names, world AABBs; static-batched AABBs from submeshes), mats_1AF.tsv (95 materials).
- Door signs (TMP text): SPORTS & PVP (-32.8,2.9,-9.5 src, west end wall), QUESTS (-2.9,2.1,19.5 src), Featured Rooms (4.7,6.1,-26.9 upstairs), MakerWorld (-12.8,5.9,-15.7 upstairs), DORM ROOM (17.9,2.6,-2.7 = entrance, east end), CAMPUS JAMS (-16.3,2.6,-11.3 = "LLM Door"), State of the Art (inactive).
- Room data save = 182.data (protobuf, 671 KB, plain): door objects [ActivityDoor_RRS]/[ActivityDoor_Menu_RRS]/[LoungeDoor_RRS], menus "Quests","Sports & PVP","Featured Rooms","Campus Jams" -> Open Room Selection Menu; room names listed. Strings in cacheidx/strings_182.txt.
- Frame plan: source entrance at +x looking -x. Room frame: X = z_s + 2.05, Z = -x_s - 7.23, Y = y_s - floor; yaw_room = yaw_s + 90. Origin = kiosk centre (src -7.23, -2.05).
- 14:00 room-frame tables: cacheidx/placed.tsv (347 top-level prefab instances), render.tsv (1,141 renderers), lights.tsv (42), match.tsv (install name matches). Script cacheidx/roomframe.pl. NOW WRITING the doc.
- Found: 6 [Lockers] ARE in the Jul 2025 scene (report only; user decision stands). Doors/hoops/ping-pong/Friend-O-Tron/radios/mirror are NOT in the scene (room-data objects in 182.data); door positions from the 7 Door_Sign texts.
- 14:20 DONE. Doc written: DevNotes/research/rr_reccenter_cache.md (sections 0-8 + appendices A placed objects, B lights, C files). Tool sources saved to DevNotes/tools/rrtool_cache/ (+README). Not done: decode 182.data (doors' room lists, hoop/ping-pong/Friend-O-Tron/radio/mirror positions), wall vertex colours.
