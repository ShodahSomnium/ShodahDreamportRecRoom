# rrtool — research tool over Rec Room's Addressables bundles

Rec Room (Unity 6000.0.27f1, IL2CPP) ships two kinds of data in
`E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data`:

* `level*`, `sharedassets*.assets`, `resources.assets`, `globalgamemanagers*`: **encrypted** (custom `..RR..RR`
  header). We do NOT touch these and must not try to decrypt them. The official rooms' level geometry lives here.
* `StreamingAssets/aa/StandaloneWindows64/*.bundle` (2,787 files, 1.6 GB): **plain UnityFS bundles**, LZ4,
  serialized file v22, **type trees present**. Props, tools, weapons, enemies, UI (watch), sprites, fonts,
  1,178 AudioClips, 1,471 particle systems, 185 shaders, Maker Pen kit pieces (incl. a Dorm kit), configs.
  All MonoBehaviour data is readable through the type trees (field names + values), and references resolve.

Binary: `C:\Users\derii\AppData\Local\Temp\claude\D--Dreamport-Rec-Room-Rec-Room\00e4b7e5-d1a8-4847-ac44-3e6fa88a3591\scratchpad\rrtool\bin\out\rrtool.exe`
(call it `RR` below). Index files (TSV, grep-able) are in `...\scratchpad\rrtool\idx\`:

| file | columns |
| --- | --- |
| `objects.tsv` | bundle, pathId, class (`GameObject*` = root GameObject i.e. a prefab root), name, components (GameObjects: comma list of component class / MonoBehaviour script names) |
| `mbscripts.tsv` | bundle, pathId, gameObjectPathId, script full name (namespace.Class), m_Name |
| `containers.tsv` | bundle, addressable container key (mostly short internal ids), pathId, class, name |
| `scripts.tsv` | bundle, pathId, assembly, namespace, class (the MonoScript objects) |
| `deps.tsv` | bundle, comma list of bundles it depends on |
| `cabs.tsv` | CAB file name, bundle |

Commands (bundle = file name without `.bundle`; pathIds are signed 64-bit, negative ones fine):

```
RR find <regex> [ClassPrefix]        # search objects.tsv by name, e.g. RR find "^basketball" GameObject
RR tree <bundle> <goPathId> [depth] [--fields] [--all]
                                     # hierarchy with transforms (local pos/rot/scale), layers, components;
                                     # --fields prints every MonoBehaviour's serialized fields as JSON with
                                     # references resolved to "Class:Name@bundle8#pathId"; --all also dumps
                                     # built-in components (colliders, rigidbody, renderers, audio sources, particles)
RR dump <bundle> <pathId> [depth]    # any object as JSON (materials, audio clips, meshes' header, particle systems...)
RR roots <bundle>                    # root GameObjects (prefabs) in a bundle
RR script <regex>                    # every MonoBehaviour instance whose script name matches
RR containers <regex>                # search containers.tsv
RR resolve <bundle> <fileId> <pathId># follow a raw PPtr from a bundle's context
RR deps <bundle>                     # dependency bundles
```

Tips
* Grep the TSVs directly for bulk questions (e.g. `grep -i "paintball" idx/objects.tsv | grep "GameObject\*"`).
* Output lines can be long; pipe through `cut -c1-2000` or `head`.
* Prefab roots named in parentheses, e.g. `(Sandbox_RootBeerBottle)`, are Rec Room's spawnable tools/objects.
* A MonoBehaviour's `audioClip` fields resolve to `AudioClip:<name>@<bundle8>#<pathId>`; `@x` means another bundle
  whose name starts with x (grep cabs/objects for the full name).
* Example: `RR tree 85515de19d04f3c4dfe704e6f02046e3 -3649557515530645676 3 --fields` shows the root beer bottle
  (DrinkableBottle: drinkInterval 0.68 s, lifeSpan 8 s, uncork/fizz/swallow clips; BreakableObject shatterSpeed 2 m/s...).
