using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// A node of a type tree: one field of a serialized class, with its children.
    /// Meta flag 0x4000 means the reader aligns to four bytes after the field.
    /// </summary>
    public sealed class RrTypeNode
    {
        public string Type;
        public string Name;
        public int Level;
        public int ByteSize;
        public int MetaFlag;
        public readonly List<RrTypeNode> Children = new List<RrTypeNode>();

        public bool AlignAfter
        {
            get { return (MetaFlag & 0x4000) != 0; }
        }
    }

    /// <summary>One entry of the file's type table.</summary>
    public sealed class RrType
    {
        public int ClassId;
        public short ScriptTypeIndex;
        public RrTypeNode Root;
    }

    /// <summary>One object of the file: its path id, where its bytes are, and its class.</summary>
    public struct RrObjectInfo
    {
        public long PathId;
        public long Start;
        public uint Size;
        public int TypeIndex;
        public int ClassId;
    }

    /// <summary>A reference to an object: m_FileID 0 is this file, n is external n-1.</summary>
    public struct RrPPtr
    {
        public int FileId;
        public long PathId;

        public bool IsNull
        {
            get { return PathId == 0; }
        }

        public override string ToString()
        {
            return "PPtr(" + FileId + "," + PathId + ")";
        }
    }

    /// <summary>
    /// A Unity serialized file (format 22, Unity 6) as found inside Rec Room's bundles: the type
    /// table with full type trees, the object table and the externals.
    ///
    /// Header (big-endian): u32 0, u32 0, u32 version (22), u32 0, u8 endianness, 3 reserved,
    /// u32 metadata size, i64 file size, i64 data offset, i64 reserved. Then, in the file's own
    /// byte order: the Unity version (Rec Room strips it to "0.0.0"), i32 platform (19,
    /// StandaloneWindows64), bool type trees (always true here), the types, the objects (i64
    /// path id, i64 start, u32 size, i32 type index; each aligned to four), the script types,
    /// the externals (empty string, 16-byte GUID, i32 type, path), the reference types, and a
    /// user string.
    ///
    /// Type tree nodes are 32 bytes: u16 version, u8 level, u8 flags, u32 type-name offset, u32
    /// field-name offset, i32 byte size, i32 index, i32 meta flag, u64 ref-type hash. An offset
    /// with the top bit set points into Unity's common string table (<see cref="RrCommonStrings"/>),
    /// otherwise into the type's own string buffer.
    ///
    /// Reading an object's fields with its type tree gives names and values for every class,
    /// including Rec Room's own MonoBehaviours whose code is not here - which is how this world
    /// learns a bottle's drink interval or a gun's fire rate without the game's assemblies.
    /// </summary>
    public sealed class RrSerializedFile
    {
        public readonly string Name;
        public readonly byte[] Data;
        public readonly int DataOffset;
        public readonly bool BigEndian;
        public readonly List<RrType> Types = new List<RrType>();
        public readonly List<RrObjectInfo> Objects = new List<RrObjectInfo>();
        public readonly List<string> Externals = new List<string>();
        private Dictionary<long, int> _byPathId;

        public RrSerializedFile(string name, byte[] data)
        {
            Name = name;
            Data = data;
            var r = new RrReader(data, 0, true);
            r.U32();
            r.U32();
            uint version = r.U32();
            r.U32();
            if (version < 22)
            {
                throw new InvalidDataException("serialized file version " + version + " is older than this reader");
            }

            BigEndian = r.U8() != 0;
            r.Position += 3;
            r.U32();
            r.I64();
            long dataOffset = r.I64();
            r.I64();
            DataOffset = (int)dataOffset;
            r.BigEndian = BigEndian;

            r.CString();
            r.I32();
            bool typeTrees = r.Bool();
            int typeCount = r.I32();
            for (int i = 0; i < typeCount; i++)
            {
                Types.Add(ReadType(r, typeTrees, false));
            }

            int objectCount = r.I32();
            for (int i = 0; i < objectCount; i++)
            {
                r.Align(4);
                var o = new RrObjectInfo { PathId = r.I64(), Start = r.I64(), Size = r.U32(), TypeIndex = r.I32() };
                o.ClassId = Types[o.TypeIndex].ClassId;
                Objects.Add(o);
            }

            int scriptCount = r.I32();
            for (int i = 0; i < scriptCount; i++)
            {
                r.I32();
                r.Align(4);
                r.I64();
            }

            int externalCount = r.I32();
            for (int i = 0; i < externalCount; i++)
            {
                r.CString();
                r.Position += 16;
                r.I32();
                Externals.Add(r.CString());
            }
        }

        private static RrType ReadType(RrReader r, bool typeTrees, bool isRef)
        {
            var t = new RrType { ClassId = r.I32() };
            r.Bool();
            t.ScriptTypeIndex = r.I16();
            if (isRef || t.ClassId == 114)
            {
                r.Position += 16;
            }

            r.Position += 16;
            if (!typeTrees)
            {
                return t;
            }

            int nodeCount = r.I32();
            int stringBytes = r.I32();
            int nodesAt = r.Position;
            r.Position += nodeCount * 32;
            var strings = r.Take(stringBytes);
            int after = r.Position;
            r.Position = nodesAt;
            var stack = new List<RrTypeNode>();
            for (int i = 0; i < nodeCount; i++)
            {
                r.U16();
                int level = r.U8();
                r.U8();
                uint typeOffset = r.U32();
                uint nameOffset = r.U32();
                var node = new RrTypeNode
                {
                    Level = level,
                    Type = String(strings, typeOffset),
                    Name = String(strings, nameOffset),
                    ByteSize = r.I32()
                };
                r.I32();
                node.MetaFlag = r.I32();
                r.I64();
                if (level == 0)
                {
                    t.Root = node;
                    stack.Clear();
                    stack.Add(node);
                }
                else
                {
                    while (stack.Count > level)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    stack[stack.Count - 1].Children.Add(node);
                    stack.Add(node);
                }
            }

            r.Position = after;
            int dependencies = r.I32();
            r.Position += dependencies * 4;
            return t;
        }

        private static string String(byte[] buffer, uint offset)
        {
            if ((offset & 0x80000000u) != 0)
            {
                return RrCommonStrings.At(offset & 0x7fffffffu);
            }

            int end = (int)offset;
            while (end < buffer.Length && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(buffer, (int)offset, end - (int)offset);
        }

        public int IndexOf(long pathId)
        {
            if (_byPathId == null)
            {
                var map = new Dictionary<long, int>(Objects.Count);
                for (int i = 0; i < Objects.Count; i++)
                {
                    map[Objects[i].PathId] = i;
                }

                _byPathId = map;
            }

            int index;
            return _byPathId.TryGetValue(pathId, out index) ? index : -1;
        }

        public bool TryGet(long pathId, out RrObjectInfo info)
        {
            int i = IndexOf(pathId);
            if (i < 0)
            {
                info = default(RrObjectInfo);
                return false;
            }

            info = Objects[i];
            return true;
        }

        /// <summary>A reader positioned at the start of an object's bytes.</summary>
        public RrReader At(RrObjectInfo o)
        {
            return new RrReader(Data, DataOffset + (int)o.Start, BigEndian);
        }

        /// <summary>
        /// Reads an object through its type tree into plain values: Dictionary&lt;string, object&gt;
        /// for classes and structs, List&lt;object&gt; for arrays, byte[] for byte arrays and
        /// TypelessData, RrPPtr for references, and CLR primitives and strings for the rest.
        /// </summary>
        public Dictionary<string, object> Read(RrObjectInfo o)
        {
            var root = Types[o.TypeIndex].Root;
            if (root == null)
            {
                throw new InvalidDataException("no type tree for class " + o.ClassId);
            }

            var r = At(o);
            return ReadNode(r, root) as Dictionary<string, object>;
        }

        public static object ReadNode(RrReader r, RrTypeNode n)
        {
            object v;
            switch (n.Type)
            {
                case "SInt8": v = (sbyte)r.U8(); break;
                case "UInt8":
                case "char": v = r.U8(); break;
                case "bool": v = r.Bool(); break;
                case "SInt16":
                case "short": v = r.I16(); break;
                case "UInt16":
                case "unsigned short": v = r.U16(); break;
                case "SInt32":
                case "int":
                case "Type*": v = r.I32(); break;
                case "UInt32":
                case "unsigned int": v = r.U32(); break;
                case "SInt64":
                case "long long": v = r.I64(); break;
                case "UInt64":
                case "unsigned long long":
                case "FileSize": v = (ulong)r.I64(); break;
                case "float": v = r.F32(); break;
                case "double": v = r.F64(); break;
                case "string":
                    return r.AlignedString();
                case "TypelessData":
                {
                    int length = r.I32();
                    v = r.Take(length);
                    break;
                }

                default:
                    if (n.Type.StartsWith("PPtr<", StringComparison.Ordinal) && n.Children.Count == 2)
                    {
                        v = new RrPPtr { FileId = r.I32(), PathId = r.I64() };
                    }
                    else if (n.Type == "Array")
                    {
                        v = ReadArray(r, n);
                    }
                    else if (n.Children.Count == 1 && n.Children[0].Type == "Array")
                    {
                        v = ReadArray(r, n.Children[0]);
                    }
                    else
                    {
                        var d = new Dictionary<string, object>(n.Children.Count);
                        foreach (var k in n.Children)
                        {
                            d[k.Name] = ReadNode(r, k);
                        }

                        v = d;
                    }

                    break;
            }

            if (n.AlignAfter)
            {
                r.Align(4);
            }

            return v;
        }

        private static object ReadArray(RrReader r, RrTypeNode array)
        {
            int count = r.I32();
            if (count < 0 || count > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("bad array count " + count);
            }

            var element = array.Children[1];
            object result;
            if (element.Type == "UInt8" || element.Type == "char")
            {
                result = r.Take(count);
            }
            else
            {
                var list = new List<object>(Math.Min(count, 4096));
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadNode(r, element));
                }

                result = list;
            }

            if (array.AlignAfter)
            {
                r.Align(4);
            }

            return result;
        }

        // ---- fast readers for the handful of built-in classes read in bulk ----

        public static RrPPtr ReadPPtr(RrReader r)
        {
            return new RrPPtr { FileId = r.I32(), PathId = r.I64() };
        }

        /// <summary>GameObject: components, layer, name, tag, active.</summary>
        public void ReadGameObject(RrObjectInfo o, List<long> components, out int layer, out string name, out bool active)
        {
            var r = At(o);
            int n = r.I32();
            for (int i = 0; i < n; i++)
            {
                var p = ReadPPtr(r);
                if (components != null)
                {
                    components.Add(p.PathId);
                }
            }

            layer = r.I32();
            name = r.AlignedString();
            r.U16();
            active = r.Bool();
        }

        /// <summary>Transform (and the Transform part of RectTransform): owner, local pose, children, parent.</summary>
        public void ReadTransform(RrObjectInfo o, out long gameObject, float[] rotation, float[] position, float[] scale, List<long> children, out long parent)
        {
            var r = At(o);
            gameObject = ReadPPtr(r).PathId;
            for (int i = 0; i < 4; i++)
            {
                float f = r.F32();
                if (rotation != null)
                {
                    rotation[i] = f;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float f = r.F32();
                if (position != null)
                {
                    position[i] = f;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                float f = r.F32();
                if (scale != null)
                {
                    scale[i] = f;
                }
            }

            int n = r.I32();
            for (int i = 0; i < n; i++)
            {
                var p = ReadPPtr(r);
                if (children != null)
                {
                    children.Add(p.PathId);
                }
            }

            parent = ReadPPtr(r).PathId;
        }

        /// <summary>MonoBehaviour header: owner, enabled, script reference, name.</summary>
        public void ReadMonoBehaviourHead(RrObjectInfo o, out long gameObject, out bool enabled, out RrPPtr script, out string name)
        {
            var r = At(o);
            gameObject = ReadPPtr(r).PathId;
            enabled = r.Bool();
            r.Align(4);
            script = ReadPPtr(r);
            name = r.AlignedString();
        }

        /// <summary>Every named asset class starts with m_Name.</summary>
        public string ReadName(RrObjectInfo o)
        {
            return At(o).AlignedString();
        }
    }
}
