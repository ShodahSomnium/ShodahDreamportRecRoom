# rgames_action progress (research only, no CODE edits)
Task: final (pre-shutdown, 1 June 2026) versions of Paintball, Laser Tag, Rec Royale, Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling.
Deliverable: DN/research/rr_latest_rgames_action.md

## Status
- [ ] started 2026-09-24: read existing research headings
- [x] Steam news split into SP/research/rga/news/<date>_<gid>.txt (564 items, index SP/research/rga/news_index.tsv; perl SP/research/rga/split.pl)
- [x] wiki pages fetched (curl, rec-room.fandom.com/api.php parse wikitext) into SP/research/rga/wiki/*.txt: activity pages + News_Archive_2021..2025 (no 2026 archive page exists)
- Key finds so far: Paintball lobby rework 2025-10-14, Merry Midway 2025-10-28, mods (Shotguns/Hammers/Fiesta) 2025-12-01, RRO Mod Tickets + mod board, battle passes (Clash of Clowns, Heartbreaker 2026-02), sprays, match summary. Dodgeball Jan 2025 rule change ("count outs only on the player that was hit, no more catch-outs"), 2026-01-22 mods Low Gravity + Dodge-Bombs + mod board + barriers behind each side, 2026-02-10 Disco Dodgeball mod. Laser Tag Jan 2023 layout adjustments + lobby buckets. Paddleball 2025-09 ball freeze fix.
- [x] Showdown dev blog pt 1-3 fetched from Wayback (SP/research/rga/blog/showdown*.txt): 6-shot revolver (not 5), Deputies vs Outlaws/Bandits, 4 min, 25, HUD with team hits+timer, music speeds up near the end, no dual wield, KO skull, respawn with fresh revolver, theatre lobby with piano.
- [x] rec wiki: Rec Royale needs 14 (solos) / 18 (squads) players to START (not in rr_shooters).
- [x] bundle: 77ea7f3e key 6 = (Page_BattlePass_PostGameSummary) = the 2025 match summary (Match Complete!, Match XP, Team Bonus, XP Boost, Daily Punch Card +500 XP, Continue). 85515de1 (RROModification_Paintball_Ticket) "RRO Mod Ticket". No Merry Midway music/level in plain bundles; hammer is (key 51d).
- recroom.com is a parking page now; Wayback CDX shows Steam has every ship note from 2025-09 on.
- NEXT: write Paintball section, then Laser Tag, Rec Royale, Showdown, Dodgeball, Paddleball, Soccer, Disc Golf, Bowling.
- [x] Paintball section written (section 1)
- [x] Laser Tag section written (section 2)
- [x] Rec Royale section written (section 3)
- [x] Showdown section written (section 4). NEXT: Dodgeball (built) - Jan 2025 rule change, 2026 mods, barriers; then Paddleball, Soccer, Disc Golf, Bowling
- [x] Dodgeball section written (section 5): build must set CatchOutsThrower=false, screen speed independent of charge, barriers, mods
- [x] Paddleball section written (section 6)
- [x] Soccer, Disc Golf, Bowling sections written (7-9). NEXT: summary section 0, cross-check research corrections, final return
- [x] Section 0 summary inserted; doc status COMPLETE (472+ lines). DONE 2026-09-24.
## Result
Doc: DN/research/rr_latest_rgames_action.md. Files touched: only that doc + this progress file (+ scratch in SP/research/rga). No CODE edits.
Build-relevant findings: Dodgeball CatchOutsThrower must be false (2025-01 no catch-outs) + screen throw speed independent of charge (2021-04) + barriers (2026-01) + mods; Paintball: 5 maps missing, Drive-In needs thrower+mines, grenade splash hits teammates, head+torso hit zones, mods, summary page, no auto-start in RR; Paddleball 1v1 VR-only in RR; Rec Royale needs 14/18 players to start.
