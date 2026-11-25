# progress notes (rr-quests run 2)
- Big bundle B=43ea1176ca25c0333547813ec3298808 holds QuestManagers, QuestAIController spawners, quest weapons, loot, GameRuleWrapper circuits.
- GameRuleWrapper (container) -> gameRulePrefab QuestManager:
  Golden Trophy (QuestForTheGoldenTrophy_GameRuleWrapperCircuit)Variant key? pathId -9060994791089726935 -> Quest_Goblin1_Manager go 3389189270401179563 campaign 0 score Gold
  Crimson Cauldron -4135721262361396085 (key 935) -> Quest_Goblin2_Manager go -409550925792103234 campaign 2 Gold
  Isle of Lost Skulls -4868050408680623212 -> Quest_Pirate1_Manager go 6402587617209858979 campaign 3 (+LootManager currency 100 gold, +QuestGearManager)
  Rise of Jumbotron -6314749316754083315 -> Quest_SciFi1_Manager go 23278338029046748 campaign 1 Credits
  Blood Moon 5791420566573996470 (key 4e) -> Quest_Dracula1_Manager go 8923214061001421911 campaign 4 Silver (+LootManager currency 101, +GearManager)
- Enemy full models/animators NOT in bundles. Available: static *_Spawner meshes (43ea1176) and 3 skinned player-transformation rigs (d10447fc: Goblin_Ground, Pirate skeleton melee, SciFi_Enemy_Ground) w/ idle+walk clips.
- q/ai/*.txt = QuestAIController spawner dumps: QuestAiConfig_EnemyCommon maxHealth/movementSpeed, type enum.
- sections 2.0-2.9 written to rr_quests.md
- section 1 written
- section 3 framework written
- section 4 level plans written
- sections 0 and 5 written; doc complete
