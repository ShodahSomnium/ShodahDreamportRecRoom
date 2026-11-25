# rcdress: the built-in Rec Center's dressing, measured (2026-11-23)

Reads the user's Android build in place through ../apkscene's reader (DATA ONLY; nothing installed, executed or copied).
Writes numbers only: colours per layout plate, piece boxes, renderer poses. No pixels, UVs or vertices leave the tool.

Build: `bash build.sh`. Run: `APK_DIR=... RRTOOL_IDX=... bash run.sh <cmd> <work> level7 ...` (work = DevNotes/apkwork).
Always run with `< /dev/null` from scripts.

| command | what |
| --- | --- |
| `platecol <work> level7 <layout.rrsk> <out.tsv> <mat,...>` | per "G plate" of the listed materials: the mean colour the level shows on it (its own hull triangles in the plate's plane, sampled through their UVs on the material's main texture) |
| `meshcol <work> level7 <goRe> <mat>` | a level object's colours grouped by face direction |
| `pieces <work> level7 <goRe> [n]` | a level mesh split into connected pieces: local box, area, colour (`RC_VIEW=1`: side/top occupancy of the largest piece) |
| `rends <work> level7` | every renderer: enabled, active, LOD1, in the PC install, mesh, materials, pose, path |

Outputs used by the world:
- `out/data/rc_platecol.tsv` -> `node gen_dressdata.js out/data/rc_platecol.tsv <world>/Places/RecCenter/RrRecCenterDressData.cs`
- `out/data/rc_rends.tsv` (curtain renderers, chair poses, posters), the piece numbers in RrRecCenterDress / RrRecCenterPosters.
- `node preview_rewrite.js <layout> <RrRecCenterDressData.cs> out/data/rc_preview.rrsk` + `../gtscene/run.sh render` -> out/render/*_{orig,preview}.png

2026-11-23 (cache layout): the world's Rec Center now builds from the July 2025 cache layout
(`refdata/apk/jt/RecCenter_Cache_poly.rrsk`, installed as `Layouts/RecCenter_Builtin.bytes`; the Android layout is kept as
`refdata/apk/jt/RecCenter_Builtin_android.bytes`). `remap_cache.js` carries the Android palettes over to its `G plate` /
`G poly` records (coplanar cover by area, else nearest coplanar within 12 cm, else nearest similar-facing within 25 cm;
rules for the cafe table tops, back counter and counter tops):
`node remap_cache.js ../../refdata/apk/jt/RecCenter_Builtin_android.bytes out/data/RrRecCenterDressData_android.cs ../../refdata/apk/jt/RecCenter_Cache_poly.rrsk out/data/RrRecCenterDressData_cache.cs out/data/rc_cache_remap.tsv`
then copy `out/data/RrRecCenterDressData_cache.cs` to `<world>/Places/RecCenter/RrRecCenterDressData.cs`.
