using System;
using System.Reflection;
using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.EditorTools
{
    /// <summary>
    /// Puts the world's network objects into the open scene, as somnium_networking.md §5.2 lists them, in the shape
    /// of the ETS and AoM scenes (EtsNetSceneSetup, EtsTrafficSceneSetup):
    ///
    ///   Rr Net
    ///     Scene Networking         SceneNetworking (_autoTransferObjectsAuthority 0: our own rules adopt orphans, §3.3)
    ///     Rr Net Events            NetworkObject + NetworkBridgeEvents   batched RPCs (§5.7)
    ///     Rr Net Player 0..49      NetworkObject + NetworkBridgeData64   presence slot per player (§5.4)
    ///     Rr Net Instance 0..23    NetworkObject + NetworkBridgeData64   instance state (§5.6)
    ///     Rr Net Actors 0..11      NetworkObject + NetworkBridgeData64   AI actors / bitsets (§5.8)
    ///     Rr Net Prop 0..47        NetworkObject + NetworkBridgeData12   synced physics props (§5.5)
    ///
    /// 135 NetworkObjects. Flags SceneNetworking.NETWORK_OBJECT_DEFAULT_FLAGS (V1 | AllowStateAuthorityOverride =
    /// 524289) and Object Interest Global (1, set through the serialized property: the field is internal to Fusion).
    /// Objects are found at run time by name, so the names are the contract.
    ///
    /// Fusion bakes SortKey and NetworkedBehaviours when a scene is saved, from a sceneSaving hook that its
    /// NetworkObjectPostprocessor registers in a static constructor - which a fresh batch-mode editor may never
    /// have run. So the bake is also called directly (by reflection: Fusion.Unity.Editor is not a reference of
    /// ours) before the save. Idempotent like the ETS tools: complete objects are left as they are, incomplete
    /// ones only get what they lack, and the scene is saved only when something changed.
    /// </summary>
    public static class RrNetSceneSetup
    {
        public const string RootName = "Rr Net";
        public const string SceneNetworkingName = "Scene Networking";
        public const string EventsName = "Rr Net Events";
        public const string PlayerPrefix = "Rr Net Player ";
        public const string InstancePrefix = "Rr Net Instance ";
        public const string ActorsPrefix = "Rr Net Actors ";
        public const string PropPrefix = "Rr Net Prop ";
        public const int Players = 50;
        public const int Instances = 24;
        public const int Actors = 12;
        public const int Props = 48;
        public const int ExpectedNetworkObjects = 1 + Players + Instances + Actors + Props;

        private const string Log = "[SomniumRecRoom] NetSceneSetup: ";

        [MenuItem("Somnium Rec Room/Add network objects", false, 20)]
        public static void AddNetworkObjects()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool changed = false;
            GameObject parent = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == RootName)
                {
                    parent = root;
                    break;
                }
            }

            if (parent == null)
            {
                parent = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(parent, "Create " + RootName);
                changed = true;
            }

            changed |= Child(parent, SceneNetworkingName, go =>
            {
                bool c = false;
                SceneNetworking networking = go.GetComponent<SceneNetworking>();

                if (networking == null)
                {
                    networking = go.AddComponent<SceneNetworking>();
                    c = true;
                }

                var so = new SerializedObject(networking);
                c |= SetBool(so, "_autoTransferObjectsAuthority", false);
                c |= SetBool(so, "_editorConnectPhotonNetwork", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                return c;
            });

            changed |= Child(parent, EventsName, go => Networked<NetworkBridgeEvents>(go));
            int made = 0;
            made += Block<NetworkBridgeData64>(parent, PlayerPrefix, Players, ref changed);
            made += Block<NetworkBridgeData64>(parent, InstancePrefix, Instances, ref changed);
            made += Block<NetworkBridgeData64>(parent, ActorsPrefix, Actors, ref changed);
            made += Block<NetworkBridgeData12>(parent, PropPrefix, Props, ref changed);

            int count = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log(Log + count + " NetworkObjects in the scene (" + ExpectedNetworkObjects + " expected); " + made + " slot objects made or completed now.");

            if (count != ExpectedNetworkObjects)
            {
                Debug.LogWarning(Log + "NetworkObject count " + count + " differs from the " + ExpectedNetworkObjects + " of somnium_networking.md §5.2: extra or missing objects?");
            }

            if (!changed)
            {
                Debug.Log(Log + "all in the scene already; nothing changed.");
                return;
            }

            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning(Log + "the scene has never been saved: save it (Fusion bakes the objects on save).");
                return;
            }

            Bake(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Log + "network objects are in " + scene.path + " and it has been saved (Fusion baked them on save).");
        }

        private static int Block<T>(GameObject parent, string prefix, int n, ref bool changed) where T : Component
        {
            int made = 0;

            for (int i = 0; i < n; i++)
            {
                if (Child(parent, prefix + i, go => Networked<T>(go)))
                {
                    changed = true;
                    made++;
                }
            }

            return made;
        }

        /// <summary>Fusion.Editor.NetworkObjectPostprocessor.BakeScene(scene), found by name.</summary>
        private static void Bake(Scene scene)
        {
            Type baker = null;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                baker = assembly.GetType("Fusion.Editor.NetworkObjectPostprocessor", false);

                if (baker != null)
                {
                    break;
                }
            }

            MethodInfo bake = baker != null ? baker.GetMethod("BakeScene", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Scene) }, null) : null;

            if (bake == null)
            {
                Debug.LogWarning(Log + "Fusion's NetworkObjectPostprocessor.BakeScene not found: relying on the save hook.");
                return;
            }

            try
            {
                bake.Invoke(null, new object[] { scene });
            }
            catch (Exception e)
            {
                Debug.LogWarning(Log + "Fusion bake failed: " + (e.InnerException ?? e).Message);
            }
        }

        /// <summary>
        /// A NetworkObject (flags 524289, interest Global) and the data component T, whichever are missing; an
        /// existing NetworkObject whose flags or interest differ is corrected. True if anything changed.
        /// </summary>
        private static bool Networked<T>(GameObject go) where T : Component
        {
            bool changed = false;

            // Unity's null test, not ??: a missing component is a fake null in the editor that ?? keeps.
            NetworkObject networkObject = go.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                networkObject = go.AddComponent<NetworkObject>();
                changed = true;
            }

            // Fusion 2.1 bakes the flag version (V2) itself when the scene is saved; assigning a fixed value would undo
            // that (Fusion_2.1_Migration_Guide §6), so only the override flag is added and the rest is kept.
            if ((networkObject.Flags & NetworkObjectFlags.AllowStateAuthorityOverride) == 0)
            {
                networkObject.Flags |= NetworkObjectFlags.AllowStateAuthorityOverride;
                changed = true;
            }

            var serialized = new SerializedObject(networkObject);
            SerializedProperty interest = serialized.FindProperty("ObjectInterest");

            if (interest == null)
            {
                Debug.LogWarning(Log + "NetworkObject has no ObjectInterest property; set Object Interest to Global by hand on " + go.name + ".");
            }
            else if (interest.intValue != 1)
            {
                interest.intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (go.GetComponent<T>() == null)
            {
                go.AddComponent<T>();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(go);
            }

            return changed;
        }

        private static bool SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);

            if (p == null)
            {
                Debug.LogWarning(Log + so.targetObject.GetType().Name + " has no field " + name + ".");
                return false;
            }

            if (p.boolValue == value)
            {
                return false;
            }

            p.boolValue = value;
            return true;
        }

        /// <summary>The named child of parent, made if missing, then set up. True if it was made or setUp changed it.</summary>
        private static bool Child(GameObject parent, string name, Func<GameObject, bool> setUp)
        {
            Transform found = parent.transform.Find(name);
            bool made = found == null;
            GameObject go = made ? new GameObject(name) : found.gameObject;

            if (made)
            {
                go.transform.SetParent(parent.transform, false);
            }

            bool changed = setUp(go) | made;

            if (changed)
            {
                EditorUtility.SetDirty(go);
            }

            return changed;
        }
    }
}
