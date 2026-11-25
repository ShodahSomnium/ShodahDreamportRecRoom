// shdis: research only. Reads one Shader object from a PC install bundle, decompresses its program blob and
// disassembles the DXBC programs (d3dcompiler_47 D3DDisassemble) into text files for reading.
// usage: shdis <bundle> <pathId> <outDir> [maxPrograms]
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ID3DBlob { [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] UIntPtr GetBufferSize(); }

static class Shdis {
  [DllImport("d3dcompiler_47.dll")] static extern int D3DDisassemble(IntPtr src, UIntPtr size, uint flags, [MarshalAs(UnmanagedType.LPStr)] string comments, out ID3DBlob dis);

  static int Main(string[] a) {
    Common.Load(@"E:\SteamLibrary\steamapps\common\RecRoom\UnityPlayer.dll");
    string dir = @"E:\SteamLibrary\steamapps\common\RecRoom\RecRoom_Data\StreamingAssets\aa\StandaloneWindows64";
    var (data, nodes) = Bundle.Open(Path.Combine(dir, a[0] + ".bundle"));
    SFile f = null;
    foreach (var n in nodes) { if (n.name.EndsWith(".resS") || n.name.EndsWith(".resource")) continue; f = Ser.Parse(n.name, data, (int)n.off); break; }
    var o = f.Objects.First(x => x.PathId == long.Parse(a[1]));
    var d = (Dictionary<string, object>)Ser.Read(f, o);
    string outDir = a[2]; Directory.CreateDirectory(outDir);
    if (a.Length > 3 && a[3] == "seg") { Seg.Dump(d, outDir); return 0; }
    if (a.Length > 3 && a[3] == "names") { Names.Run(d); return 0; }
    if (a.Length > 3 && a[3] == "has") { Has.Run(d, a.Skip(4).ToArray()); return 0; }
    if (a.Length > 3 && a[3] == "str") { Seg.Strings(a[4], Convert.ToInt64(a[5], 16), Convert.ToInt64(a[6], 16)); return 0; }
    int max = 100000;
    Console.WriteLine("keys: " + string.Join(",", d.Keys));
    var blob = d["compressedBlob"] as byte[];
    var plats = (List<object>)d["platforms"];
    Console.WriteLine("platforms " + string.Join(",", plats) + " blob " + blob.Length);
    var offs = (List<object>)d["offsets"]; var cl = (List<object>)d["compressedLengths"]; var dl = (List<object>)d["decompressedLengths"];
    int progs = 0;
    for (int p = 0; p < plats.Count; p++) {
      var po = Flat(offs[p]); var pc = Flat(cl[p]); var pd = Flat(dl[p]);
      Console.WriteLine($"platform {plats[p]}: {po.Count} segments");
      for (int s = 0; s < po.Count; s++) {
        var dec = new byte[pd[s]]; Lz4Dec.Decode(blob, (int)po[s], (int)pc[s], dec);
        // DXBC containers inside the segment
        for (int i = 0; i + 32 < dec.Length; i++) {
          if (dec[i] != 'D' || dec[i + 1] != 'X' || dec[i + 2] != 'B' || dec[i + 3] != 'C') continue;
          int size = BitConverter.ToInt32(dec, i + 24); if (size <= 0 || i + size > dec.Length) continue;
          if (progs >= max) break;
          var h = GCHandle.Alloc(dec, GCHandleType.Pinned);
          try {
            int hr = D3DDisassemble(h.AddrOfPinnedObject() + i, (UIntPtr)size, 0, null, out var db);
            if (hr == 0 && db != null) {
              var txt = Marshal.PtrToStringAnsi(db.GetBufferPointer(), (int)db.GetBufferSize());
              File.WriteAllText(Path.Combine(outDir, $"p{plats[p]}_s{s}_{i:X}.asm"), txt);
              progs++;
            }
          } finally { h.Free(); }
          i += size - 1;
        }
      }
    }
    Console.WriteLine(progs + " programs disassembled");
    return 0;
  }
  public static List<long> Flat(object o) {
    var res = new List<long>();
    void W(object x) { if (x is List<object> l) foreach (var y in l) W(y); else if (x is Dictionary<string, object> dd) foreach (var v in dd.Values) W(v); else res.Add(Convert.ToInt64(x)); }
    W(o); return res;
  }
}
