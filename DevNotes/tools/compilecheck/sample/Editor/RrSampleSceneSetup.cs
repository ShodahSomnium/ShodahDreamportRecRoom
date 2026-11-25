using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.EditorTools
{
    /// <summary>Compile-check sample of the siblings' editor setup pattern (menu item, SerializedObject wiring, save).</summary>
    internal static class RrSampleSceneSetup
    {
        [MenuItem("Somnium Rec Room/Create sample host", false, 0)]
        private static void Create()
        {
            var host = new GameObject("Rr Sample");
            Undo.RegisterCreatedObjectUndo(host, "Create Rr Sample");
            var sample = host.AddComponent<RrCompileSample>();
            var serialized = new SerializedObject(sample);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(host.scene);
            Selection.activeGameObject = host;
        }
    }
}
