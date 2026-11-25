# Iteration 3 - user decisions made while the build was running (2026-09-24 ~14:50)

The July 2025 cache scene (rr_reccenter_cache.md) changed two earlier answers. The user decided:

1. **Lockers: KEEP them.** The real final room had 6 [Lockers]. Place them where the cache measured them
   (rr_reccenter_cache.md, appendix A). The rest of the removal list stands: the pool table, the poker table and chips,
   and the jukebox are removed.
2. **Doors: use the REAL signs in their REAL places.**
   - SPORTS & PVP, QUESTS, FEATURED ROOMS, MAKERWORLD (upstairs) and CAMPUS JAMS (the "LLM Door"), each at its measured
     spot, plus the Dorm door.
   - Behind FEATURED ROOMS go our party and racing games: 3D Charades, Make It To Midnight, My Little Monsters, Punch Up,
     Rec Rally, Stunt Runner.
   - MAKERWORLD and CAMPUS JAMS open a list that says "Nothing here yet - coming soon."
   - There is NO Creative Campus door in the final room: do not place one.
   - The Magic Door goes where the cache or research puts it. If it has no known spot, put it near the Campus Jams
     door, but not on the Campus Jams spot.

**API change (already made in RrCategoryDoor.cs by the orchestrator):** RrDoorCategory now also has `MakerWorld` and
`CampusJams`. Sign() returns "MAKERWORLD" / "CAMPUS JAMS", Activities() returns an empty list for both, and FeaturedRooms
returns the 6 party/racing games above. The list panel's tabs are now: Sports & PvP, Quests, Featured Rooms, MakerWorld,
Campus Jams. `CreativeCampus` still exists but is unused.
