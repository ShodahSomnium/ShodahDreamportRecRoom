using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Lists every member (method, field, property accessor) an assembly references on types from the
// Fusion.* assemblies, as "Type::Member" lines. Used to diff new world code against code that already
// passed the Somnium server compile (whose Fusion is older than the local copies).
static class RefDump
{
    static int Main(string[] args)
    {
        var all = new SortedSet<string>(StringComparer.Ordinal);

        foreach (string path in args)
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            MetadataReader md = pe.GetMetadataReader();

            foreach (MemberReferenceHandle h in md.MemberReferences)
            {
                MemberReference mr = md.GetMemberReference(h);
                string type = TypeName(md, mr.Parent, out string asm);

                if (asm == null || !(asm.StartsWith("Fusion", StringComparison.Ordinal) || asm.StartsWith("SomniumSpace", StringComparison.Ordinal))) continue;
                all.Add(asm + " | " + type + "::" + md.GetString(mr.Name));
            }

            // Types used at all (e.g. casts, generic args) - resolution failures show as missing types.
            foreach (TypeReferenceHandle th in md.TypeReferences)
            {
                TypeReference tr = md.GetTypeReference(th);
                string asm = ScopeAsm(md, tr.ResolutionScope);
                if (asm == null || !(asm.StartsWith("Fusion", StringComparison.Ordinal) || asm.StartsWith("SomniumSpace", StringComparison.Ordinal))) continue;
                all.Add(asm + " | " + Full(md, tr) + " (type)");
            }
        }

        foreach (string s in all) Console.WriteLine(s);
        return 0;
    }

    static string TypeName(MetadataReader md, EntityHandle parent, out string asm)
    {
        asm = null;

        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                TypeReference tr = md.GetTypeReference((TypeReferenceHandle)parent);
                asm = ScopeAsm(md, tr.ResolutionScope);
                return Full(md, tr);
            case HandleKind.TypeSpecification:
                TypeSpecification ts = md.GetTypeSpecification((TypeSpecificationHandle)parent);
                BlobReader br = md.GetBlobReader(ts.Signature);
                // GENERICINST: 0x15, then CLASS/VALUETYPE, then a TypeDefOrRef coded index.
                byte kind = br.ReadByte();
                if (kind == 0x15)
                {
                    br.ReadByte();
                    EntityHandle inner = br.ReadTypeHandle();
                    return TypeName(md, inner, out asm) + "<>";
                }
                return "typespec";
            default:
                return parent.Kind.ToString();
        }
    }

    static string ScopeAsm(MetadataReader md, EntityHandle scope)
    {
        if (scope.Kind == HandleKind.AssemblyReference)
            return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
        if (scope.Kind == HandleKind.TypeReference)
            return ScopeAsm(md, md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope);
        return null;
    }

    static string Full(MetadataReader md, TypeReference tr)
    {
        string ns = md.GetString(tr.Namespace), n = md.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return Full(md, md.GetTypeReference((TypeReferenceHandle)tr.ResolutionScope)) + "+" + n;
        return ns.Length > 0 ? ns + "." + n : n;
    }
}
