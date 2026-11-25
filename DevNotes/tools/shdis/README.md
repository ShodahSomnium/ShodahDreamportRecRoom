# shdis: how Rec Room's installed shaders compute their lighting (research only, iteration 9)

Reads one Shader object from a PC install bundle (rrtool's reader), LZ4-decompresses its program blob and disassembles
every DXBC program with Windows' own d3dcompiler_47.dll (D3DDisassemble) into text files. Nothing is loaded into Unity or
shipped; the output stays in a scratch folder.

    bash build.sh
    D="C:/Program Files/Unity/Hub/Editor/6000.3.5f2/Editor/Data/NetCoreRuntime/dotnet.exe"
    "$D" out/shdis.dll <bundle> <shaderPathId> <outDir>              # every program -> <outDir>/p<platform>_s<segment>_<offset>.asm
    "$D" out/shdis.dll <bundle> <shaderPathId> <outDir> has <name>.. # how often each name occurs in the decompressed blob
    "$D" out/shdis.dll <bundle> <shaderPathId> <outDir> seg          # the decompressed segments (parameter names are in them)

AG/Standard-Opaque = bundle f124b399caea956fb2ae85b8413c07d2, path id -8827874758124330193 (1,120 programs).
Findings (the world-space light grid `_Voxels`, Rec Room's falloff 1 - d^2/r^2, specular in the colour's alpha):
DevNotes/iter9/light9.md.
