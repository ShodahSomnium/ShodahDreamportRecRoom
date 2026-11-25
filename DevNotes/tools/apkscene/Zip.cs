// apkscene: read-only access to the user's Android asset packs (plain zip, entries STORED). Nothing is extracted to disk.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

sealed class ZipStore : IDisposable {
  readonly FileStream fs;
  public readonly Dictionary<string, (long off, long size, int method)> E = new(StringComparer.Ordinal);
  public ZipStore(string path) {
    fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
    long len = fs.Length; int tail = (int)Math.Min(len, 65536 + 22);
    var t = ReadAt(len - tail, tail); int eocd = -1;
    for (int i = tail - 22; i >= 0; i--) if (BitConverter.ToUInt32(t, i) == 0x06054b50) { eocd = i; break; }
    if (eocd < 0) throw new Exception("zip: no EOCD in " + path);
    int n = BitConverter.ToUInt16(t, eocd + 10); long cdSize = BitConverter.ToUInt32(t, eocd + 12), cdOff = BitConverter.ToUInt32(t, eocd + 16);
    var cd = ReadAt(cdOff, (int)cdSize); int p = 0;
    for (int i = 0; i < n; i++) {
      if (BitConverter.ToUInt32(cd, p) != 0x02014b50) throw new Exception("zip: bad CD entry " + i);
      int method = BitConverter.ToUInt16(cd, p + 10);
      long csz = BitConverter.ToUInt32(cd, p + 20);
      int nl = BitConverter.ToUInt16(cd, p + 28), el = BitConverter.ToUInt16(cd, p + 30), cl = BitConverter.ToUInt16(cd, p + 32);
      long lho = BitConverter.ToUInt32(cd, p + 42);
      string name = Encoding.UTF8.GetString(cd, p + 46, nl);
      E[name] = (lho, csz, method);
      p += 46 + nl + el + cl;
    }
  }
  byte[] ReadAt(long off, int n) { var b = new byte[n]; lock (fs) { fs.Position = off; int r = 0; while (r < n) { int k = fs.Read(b, r, n - r); if (k <= 0) throw new EndOfStreamException(); r += k; } } return b; }
  public byte[] Read(string name) {
    var e = E[name]; if (e.method != 0) throw new Exception("zip: entry not stored: " + name);
    var h = ReadAt(e.off, 30); int nl = BitConverter.ToUInt16(h, 26), el = BitConverter.ToUInt16(h, 28);
    return ReadAt(e.off + 30 + nl + el, (int)e.size);
  }
  public void Dispose() => fs.Dispose();
}

/// The Android player's Data folder (assets/bin/Data in UnityDataAssetPack.apk). Joins levelN.split0..k in order.
sealed class DataDir {
  public readonly ZipStore Z; const string Pre = "assets/bin/Data/";
  public readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string, int> splits = new(StringComparer.OrdinalIgnoreCase);
  readonly string plain; readonly Dictionary<string, int> psplits = new(StringComparer.OrdinalIgnoreCase); readonly HashSet<string> pfiles = new(StringComparer.OrdinalIgnoreCase);
  /// <param name="plainDir">optional folder with Data files taken from the base APK (globalgamemanagers*, level0, Resources/*)</param>
  public DataDir(string apk, string plainDir = null) {
    Z = new ZipStore(apk);
    if (plainDir != null && Directory.Exists(plainDir)) { plain = plainDir; foreach (var p in Directory.GetFiles(plainDir)) { var f = Path.GetFileName(p); int si = f.LastIndexOf(".split", StringComparison.Ordinal); if (si > 0 && int.TryParse(f.Substring(si + 6), out int part)) { var b = f.Substring(0, si); psplits[b] = Math.Max(psplits.GetValueOrDefault(b, -1), part); pfiles.Add(b); } else pfiles.Add(f); } foreach (var f in pfiles) Files.Add(f); }
    foreach (var k in Z.E.Keys) {
      if (!k.StartsWith(Pre)) continue; var f = k.Substring(Pre.Length);
      int si = f.LastIndexOf(".split", StringComparison.Ordinal);
      if (si > 0 && int.TryParse(f.Substring(si + 6), out int part)) { var b = f.Substring(0, si); splits[b] = Math.Max(splits.GetValueOrDefault(b, -1), part); Files.Add(b); }
      else Files.Add(f);
    }
  }
  public bool Has(string f) => Files.Contains(f);
  public byte[] Get(string f) {
    if (pfiles.Contains(f)) { if (psplits.TryGetValue(f, out int pl)) { var ms = new MemoryStream(); for (int i = 0; i <= pl; i++) { var bb = File.ReadAllBytes(Path.Combine(plain, f + ".split" + i)); ms.Write(bb, 0, bb.Length); } return ms.ToArray(); } return File.ReadAllBytes(Path.Combine(plain, f)); }
    if (splits.TryGetValue(f, out int last)) {
      var parts = new List<byte[]>(); for (int i = 0; i <= last; i++) parts.Add(Z.Read(Pre + f + ".split" + i));
      var r = new byte[parts.Sum(x => (long)x.Length)]; int o = 0; foreach (var p in parts) { Buffer.BlockCopy(p, 0, r, o, p.Length); o += p.Length; }
      return r;
    }
    return Z.Read(Pre + f);
  }
}
