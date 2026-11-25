using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
// enumdump <dll> <TypeNameSuffix>: prints enum members (name = value) and plain fields/properties of matching types.
var (path, want) = (args[0], args[1]);
using var fs = File.OpenRead(path); using var pe = new PEReader(fs); var md = pe.GetMetadataReader();
foreach (var th in md.TypeDefinitions) {
  var t = md.GetTypeDefinition(th); string full = md.GetString(t.Namespace) + "." + md.GetString(t.Name);
  if (!full.EndsWith(want)) continue;
  Console.WriteLine("== " + full);
  foreach (var fh in t.GetFields()) { var f = md.GetFieldDefinition(fh); var c = f.GetDefaultValue();
    string v = c.IsNil ? "" : " = " + string.Join(",", md.GetBlobBytes(md.GetConstant(c).Value));
    Console.WriteLine("  field " + md.GetString(f.Name) + v + "  attrs=" + f.Attributes); }
  foreach (var ph in t.GetProperties()) Console.WriteLine("  prop " + md.GetString(md.GetPropertyDefinition(ph).Name));
}
