using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// Unity's common string table for type trees. A type-tree string offset with the top bit set
    /// is an offset into this table, where each string is followed by a zero byte. The table is
    /// part of the engine, not of any file; this copy was read from the UnityPlayer.dll that
    /// ships with Rec Room (6000.0.27f1): 110 strings, from "AABB" to "RenderingLayerMask".
    /// Offsets are computed here the same way, so the table only has to be kept in order.
    /// </summary>
    public static class RrCommonStrings
    {
        private static readonly string[] Table =
        {
            "AABB", "AnimationClip", "AnimationCurve", "AnimationState", "Array", "Base", "BitField", "bitset", "bool",
            "char", "ColorRGBA", "Component", "data", "deque", "double", "dynamic_array", "FastPropertyName", "first",
            "float", "Font", "GameObject", "Generic Mono", "GradientNEW", "GUID", "GUIStyle", "int", "list", "long long",
            "map", "Matrix4x4f", "MdFour", "MonoBehaviour", "MonoScript", "m_ByteSize", "m_Curve",
            "m_EditorClassIdentifier", "m_EditorHideFlags", "m_Enabled", "m_ExtensionPtr", "m_GameObject", "m_Index",
            "m_IsArray", "m_IsStatic", "m_MetaFlag", "m_Name", "m_ObjectHideFlags", "m_PrefabInternal",
            "m_PrefabParentObject", "m_Script", "m_StaticEditorFlags", "m_Type", "m_Version", "Object", "pair",
            "PPtr<Component>", "PPtr<GameObject>", "PPtr<Material>", "PPtr<MonoBehaviour>", "PPtr<MonoScript>",
            "PPtr<Object>", "PPtr<Prefab>", "PPtr<Sprite>", "PPtr<TextAsset>", "PPtr<Texture>", "PPtr<Texture2D>",
            "PPtr<Transform>", "Prefab", "Quaternionf", "Rectf", "RectInt", "RectOffset", "second", "set", "short",
            "size", "SInt16", "SInt32", "SInt64", "SInt8", "staticvector", "string", "TextAsset", "TextMesh", "Texture",
            "Texture2D", "Transform", "TypelessData", "UInt16", "UInt32", "UInt64", "UInt8", "unsigned int",
            "unsigned long long", "unsigned short", "vector", "Vector2f", "Vector3f", "Vector4f",
            "m_ScriptingClassIdentifier", "Gradient", "Type*", "int2_storage", "int3_storage", "BoundsInt",
            "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "FileSize", "Hash128",
            "RenderingLayerMask"
        };

        private static Dictionary<uint, string> _byOffset;

        public static string At(uint offset)
        {
            if (_byOffset == null)
            {
                var map = new Dictionary<uint, string>(Table.Length);
                uint o = 0;
                foreach (var s in Table)
                {
                    map[o] = s;
                    o += (uint)s.Length + 1;
                }

                _byOffset = map;
            }

            string found;
            return _byOffset.TryGetValue(offset, out found) ? found : "?common" + offset;
        }
    }
}
