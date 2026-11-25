#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace Thad.Mirrors
{
    public class StandardMirrorShaderGUI : ShaderGUI
    {
        public enum Background
        {
            Default = 0,
            Probe = 1,
            ProbeForceBox = 2,
            ProbeCustomBounds = 3
        }

        public enum BlendMode
        {
            Opaque = 0,
            Cutout = 1,
            Transparent = 2,
            Custom = 3
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            DrawProperty(materialEditor, properties, "_Color");

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Background Blending", EditorStyles.boldLabel);

            MaterialProperty backgroundProp = FindProperty("_Background", properties);
            materialEditor.ShaderProperty(backgroundProp, "Blend Transparent Pixels");
            Background background = (Background)backgroundProp.floatValue;

            if (background != Background.Default)
            {
                DrawProperty(materialEditor, properties, "_Roughness");
                if (background == Background.ProbeCustomBounds)
                {
                    DrawVector3(properties, "_CustomProbeBoundsMin");
                    DrawVector3(properties, "_CustomProbeBoundsMax");
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

            DrawProperty(materialEditor, properties, "_Cull");

            foreach (Material mat in materialEditor.targets)
            {
                EditorGUI.BeginChangeCheck();
                MaterialProperty blendModeProp = FindProperty("_BlendMode", properties);
                materialEditor.ShaderProperty(blendModeProp, "Render Type");
                bool blendModeChanged = EditorGUI.EndChangeCheck();
                BlendMode blendMode = (BlendMode)blendModeProp.floatValue;

                MaterialProperty zWriteProp = FindProperty("_ZWrite", properties);
                MaterialProperty zTestProp = FindProperty("_ZTest", properties);
                MaterialProperty srcBlendProp = FindProperty("_SrcBlend", properties);
                MaterialProperty dstBlendProp = FindProperty("_DstBlend", properties);
                MaterialProperty srcBlendAlphaProp = FindProperty("_SrcBlendAlpha", properties);
                MaterialProperty dstBlendAlphaProp = FindProperty("_DstBlendAlpha", properties);
                MaterialProperty alphaClipProp = FindProperty("_AlphaClip", properties);
                MaterialProperty cutoffProp = FindProperty("_Cutoff", properties);
                MaterialProperty alphaToMaskProp = FindProperty("_AlphaToMask", properties);

                if (blendModeChanged)
                {
                    switch (blendMode)
                    {
                        case BlendMode.Opaque:
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                            zWriteProp.floatValue = 1.0f;
                            zTestProp.floatValue = (float)UnityEngine.Rendering.CompareFunction.LessEqual;
                            srcBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.One;
                            dstBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.Zero;
                            srcBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.One;
                            dstBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.Zero;
                            alphaToMaskProp.floatValue = 0.0f;
                            alphaClipProp.floatValue = 0.0f;
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.DisableKeyword("_ALPHATOMASK_ON");
                            break;
                        case BlendMode.Cutout:
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                            zWriteProp.floatValue = 1.0f;
                            zTestProp.floatValue = (float)UnityEngine.Rendering.CompareFunction.LessEqual;
                            srcBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.One;
                            dstBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.Zero;
                            srcBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.One;
                            dstBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.Zero;
                            alphaClipProp.floatValue = 1.0f;
                            alphaToMaskProp.floatValue = 0.0f;
                            cutoffProp.floatValue = 0.5f;
                            mat.EnableKeyword("_ALPHATEST_ON");
                            mat.DisableKeyword("_ALPHATOMASK_ON");
                            break;
                        case BlendMode.Transparent:
                            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                            zWriteProp.floatValue = 0.0f;
                            zTestProp.floatValue = (float)UnityEngine.Rendering.CompareFunction.LessEqual;
                            srcBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.SrcAlpha;
                            dstBlendProp.floatValue = (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
                            srcBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.OneMinusDstAlpha;
                            dstBlendAlphaProp.floatValue = (float)UnityEngine.Rendering.BlendMode.One;
                            alphaToMaskProp.floatValue = 0.0f;
                            alphaClipProp.floatValue = 0.0f;
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.DisableKeyword("_ALPHATOMASK_ON");
                            break;
                        case BlendMode.Custom:
                            break;
                    }
                }

                if (blendMode == BlendMode.Custom)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Custom Render Settings", EditorStyles.boldLabel);
                    materialEditor.ShaderProperty(zWriteProp, zWriteProp.displayName);
                    materialEditor.ShaderProperty(zTestProp, zTestProp.displayName);
                    materialEditor.ShaderProperty(srcBlendProp, srcBlendProp.displayName);
                    materialEditor.ShaderProperty(dstBlendProp, dstBlendProp.displayName);
                    materialEditor.ShaderProperty(srcBlendAlphaProp, srcBlendAlphaProp.displayName);
                    materialEditor.ShaderProperty(dstBlendAlphaProp, dstBlendAlphaProp.displayName);
                    EditorGUI.BeginChangeCheck();
                    materialEditor.ShaderProperty(alphaClipProp, alphaClipProp.displayName);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (alphaClipProp.floatValue == 0.0f)
                        {
                            alphaToMaskProp.floatValue = 0.0f;
                        }
                    }
                    if (alphaClipProp.floatValue > 0.0f)
                    {
                        EditorGUI.BeginChangeCheck();
                        materialEditor.ShaderProperty(alphaToMaskProp, alphaToMaskProp.displayName);
                        if (EditorGUI.EndChangeCheck())
                        {
                            if (alphaToMaskProp.floatValue > 0.0f)
                            {
                                alphaClipProp.floatValue = 1.0f;
                                cutoffProp.floatValue = 0.5f;
                            }
                        }
                        if (alphaToMaskProp.floatValue == 0.0f)
                        {
                            materialEditor.ShaderProperty(cutoffProp, cutoffProp.displayName);
                        }
                    }
                }

                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                int customQueue = EditorGUILayout.IntField("Render Queue", mat.renderQueue);
                if (EditorGUI.EndChangeCheck())
                {
                    mat.renderQueue = customQueue;
                }
            }

        }

        public static void SetRenderQueue(MaterialEditor materialEditor, int renderQueue)
        {
            foreach (Material mat in materialEditor.targets)
            {
                mat.renderQueue = renderQueue;
            }
        }

        public static void DrawProperty(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty prop = FindProperty(propertyName, properties);
            if (prop == null)
            {
                return;
            }

            materialEditor.ShaderProperty(prop, prop.displayName);
        }

        public static void DrawVector3(MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty prop = FindProperty(propertyName, properties, false);
            if (prop == null)
            {
                return;
            }

            Vector4 vec = prop.vectorValue;
            Vector3 newVec3 = EditorGUILayout.Vector3Field(prop.displayName, new Vector3(vec.x, vec.y, vec.z));
            prop.vectorValue = new Vector4(newVec3.x, newVec3.y, newVec3.z, vec.w);
        }
    }

}

#endif // UNITY_EDITOR
