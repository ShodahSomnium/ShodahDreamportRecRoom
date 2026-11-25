using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.EditorTools
{
    /// <summary>
    /// Re-bakes the world scene's network objects for Photon Fusion 2.1 (2026-10-20 migration from 2.0.12).
    ///
    /// Fusion 2.1 bakes NetworkObject flags with version V2 and a new prefab format; objects baked by 2.0 carry V1.
    /// Fusion re-bakes a scene whenever it is saved, so this opens "Rec Room Scene", runs Fusion's own
    /// "Bake Scene Objects", saves the scene and rebuilds the prefab table (Fusion_2.1_Migration_Guide §7), then
    /// reports how many of the 135 objects carry V2 and AllowStateAuthorityOverride. Also callable in batch mode:
    /// Unity.exe -batchmode -projectPath ... -executeMethod SomniumSpace.Worlds.SomniumRecRoom.EditorTools.RrFusionRebake.Run -quit
    /// </summary>
    public static class RrFusionRebake
    {
        public const string ScenePath = "Assets/Rec Room Scene.unity";

        [MenuItem("Somnium Rec Room/Fusion 2.1: re-bake network objects", false, 40)]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool baked = EditorApplication.ExecuteMenuItem("Tools/Fusion/Scene/Bake Scene Objects");
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            bool table = EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table");
            AssetDatabase.SaveAssets();

            int total = 0;
            int v2 = 0;
            int overrideFlag = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var no in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    total++;
                    if ((no.Flags & NetworkObjectFlags.V2) != 0)
                    {
                        v2++;
                    }

                    if ((no.Flags & NetworkObjectFlags.AllowStateAuthorityOverride) != 0)
                    {
                        overrideFlag++;
                    }
                }
            }

            Debug.Log("[SomniumRecRoom] Fusion 2.1 rebake: bake menu " + baked + ", saved " + saved + ", prefab table " + table
                + "; NetworkObjects " + total + ", V2 " + v2 + ", AllowStateAuthorityOverride " + overrideFlag);
        }
    }
}
