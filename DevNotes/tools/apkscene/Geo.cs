// apkscene: small vector / quaternion helpers and RRSK number formatting.
using System;
using System.Globalization;

struct V3 {
  public double x, y, z;
  public V3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
  public static V3 operator +(V3 a, V3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
  public static V3 operator -(V3 a, V3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
  public static V3 operator *(V3 a, double s) => new(a.x * s, a.y * s, a.z * s);
  public V3 Mul(V3 b) => new(x * b.x, y * b.y, z * b.z);
  public V3 Abs() => new(Math.Abs(x), Math.Abs(y), Math.Abs(z));
  public double Max() => Math.Max(x, Math.Max(y, z));
  public double Len() => Math.Sqrt(x * x + y * y + z * z);
  public static V3 Min(V3 a, V3 b) => new(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
  public static V3 Max(V3 a, V3 b) => new(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
  public override string ToString() => $"{Y.F(x)} {Y.F(y)} {Y.F(z)}";
}
struct Q {
  public double x, y, z, w;
  public Q(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
  public static readonly Q I = new(0, 0, 0, 1);
  public static Q operator *(Q a, Q b) => new(
    a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
    a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
    a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
    a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
  public V3 Rot(V3 v) {
    double tx = 2 * (y * v.z - z * v.y), ty = 2 * (z * v.x - x * v.z), tz = 2 * (x * v.y - y * v.x);
    return new V3(v.x + w * tx + (y * tz - z * ty), v.y + w * ty + (z * tx - x * tz), v.z + w * tz + (x * ty - y * tx));
  }
  public Q Norm() { double n = Math.Sqrt(x * x + y * y + z * z + w * w); return n < 1e-12 ? I : new Q(x / n, y / n, z / n, w / n); }
  public override string ToString() => $"{Y.F(x)} {Y.F(y)} {Y.F(z)} {Y.F(w)}";
}
struct TRS { public V3 p; public Q r; public V3 s; public static readonly TRS I = new() { r = Q.I, s = new V3(1, 1, 1) }; }

static class Y {
  public static string F(double v) { if (double.IsNaN(v) || double.IsInfinity(v)) v = 0; var r = Math.Round(v, 4); if (r == 0) r = 0; return r.ToString("0.####", CultureInfo.InvariantCulture); }
  public static string F3(double v) { var r = Math.Round(v, 3); if (r == 0) r = 0; return r.ToString("0.###", CultureInfo.InvariantCulture); }
  public static string Enc(string s) => (s ?? "").Replace("%", "%25").Replace(" ", "%20").Replace("\t", "%09").Replace("\n", "%0A").Replace("\r", "");
}
