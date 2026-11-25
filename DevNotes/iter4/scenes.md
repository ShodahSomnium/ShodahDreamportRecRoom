# iter4 / scenes - progress notes (durability file)
Started 2026-09-24. Task: rrscene parser + RRSK files for every REF2 scene + paintball gameplay layers.
Paths: DN = D:/Dreamport/Rec Room/Rec Room/DevNotes; SP(rrtool) = C:/Users/derii/AppData/Local/Temp/claude/D--Dreamport-Rec-Room-Rec-Room/00e4b7e5-.../scratchpad/rrtool
My scratch: .../1c0947a6-.../scratchpad/rrscene
## Status
- [ ] recon REF2 layout
- [x] recon (min ~25): 43 .unity (6 are ~16 KB empty: CastleGrounds, GrandHall4, JumbotronBoss, StuntLobby, Testing + RunTheBlock has no .unity).
  Mesh refs: m_Mesh guid -> .assets/*.asset.meta (all .asset are class 43 Mesh; m_Name + submesh localAABB in header).
  ~30k refs are Unity builtin (guid 0000000000000000e000000000000000, fileID 10202 Cube/10206 Cyl/10207 Sphere/10208 Capsule/10209 Plane/10210 Quad).
  ~40% of mesh GUIDs have NO .meta -> fall back to GameObject name. Mesh/*.glb are named by mesh NAME (node name), SharpGLTF, POSITION min/max = bounds.
  Materials: only 5 of 1271 material GUIDs resolve (.mat files have no .meta) -> infer names by voting via install mesh->material table.
  MonoBehaviours carry NO fields (stripped); script GUIDs not AssetRipper-MD5 of install scripts (tested) -> label by GO-name keywords.
  PrefabInstance only in MakeItToMidnight (1605) + 1 in RecRoyale.
- [ ] write DN/tools/rrscene/rrscene.cs (inst scan + scene parse + writer)
- [x] (min ~55) tool DN/tools/rrscene built (build.sh; Inst.cs Scene.cs Emit.cs Main.cs Paintball.cs + ../rrtool/Core.cs).
  Work dir = my scratch rrscene/ (guidmap.tsv, inst_renderers.tsv 12.5k rows, inst_materials.tsv, matvotes.tsv 452, scriptlabels.tsv 563, stats/).
  Order: inst -> scan -> write. Full write of 43 scenes = 30 s. DN/refdata/<scene>.rrsk/.md + INDEX.md + _scripts.tsv written.
- [ ] paintball gameplay rrsk (from SP1/tsv/paintballtro__*.tsv)
- [ ] README
- [x] (min ~75) [X] scene names -> install (X) prefabs (43 names; P counts up, e.g. GTact3 P29). Paintball: 7 Paintball_<Map>_gameplay.rrsk (incl. Lobby).
  INDEX.md has scene table + paintball table. README written. DONE.
Known limits: materials mostly inferred by vote (no .mat metas); ~40% mesh GUIDs have no .meta (GO-name fallback);
  #U lines = unknown-size renderers; B includes sky domes/far terrain (GTact3, PortPlunder, RecRally, RecRoyale, SunkenShip);
  MakeItToMidnight = 1605 PrefabInstances whose source prefabs are missing (G prefab_instance); 5 scenes empty.
