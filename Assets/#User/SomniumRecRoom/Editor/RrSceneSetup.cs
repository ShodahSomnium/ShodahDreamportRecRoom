using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom.EditorTools
{
    /// <summary>
    /// Makes the world's scene, "Assets/Rec Room Scene.unity", in one menu item, the way EtsSceneSetup builds the
    /// Euro Truck hosts - objects created by name, fields wired through SerializedObject:
    ///
    ///   Uploader            WorldSDKConfig + WorldConfig (conventions §8.2): scripting on with our asmdef, spawn
    ///                       (0, 0.9, 0) facing +Z, respawn below y = -100 at the spawn point. The world slot is left
    ///                       at 0 / "new world": the user picks it in the uploader.
    ///   Directional Light   warm white sun with soft shadows and URP's additional data; RenderSettings.sun
    ///   Arrival             the 20 m spawn floor at y = 0, collider on layer 23 (Environment: the avatar stands on
    ///                       it and the VR ray hits it). It exists before any script runs, so the player lands on
    ///                       something while the catalog is read (about 20 s the first time).
    ///   Status Canvas       screen label in LiberationSans SDF - the font ships with the world through this
    ///                       reference - wired to RrMain._status
    ///   Rec Room            RrMain, which boots everything else at run time
    ///   Rr Net              SceneNetworking and the 135 network objects (RrNetSceneSetup)
    ///
    /// Render settings: fog OFF, bright tri-colour ambient. The scene is saved with fog off, so the fog variants of
    /// any shader shipped in the world bundle are stripped (conventions §13); Rec Room's own shaders come from the
    /// player's bundles and are unaffected. The ambient is applied here and by the harness; at run time the client
    /// may make another scene active, so rooms that care re-apply it through the Bridge.
    ///
    /// Idempotent: an object that exists is kept as it is (the uploader's world slot and name are never reset),
    /// except that the scripting fields the upload depends on are corrected if they drifted, and the render
    /// settings are re-applied. The builders marked "runtime-safe" use only runtime APIs, so the play-mode
    /// harness calls them through reflection to stand in the same light and floor as the scene.
    /// </summary>
    public static class RrSceneSetup
    {
        public const string ScenePath = "Assets/Rec Room Scene.unity";
        public const string WorldName = "Dreamport: Rec Room";
        public const string WorldDescription = "Rec Room, played inside Somnium Space: the dorm, the Rec Center and the classic games, built from your own Rec Room install.";
        public const string AssemblyName = "SomniumRecRoom";
        public const string AsmdefPath = "Assets/#User/SomniumRecRoom/SomniumRecRoom.asmdef";
        public const string LiberationSansGuid = "8f586378b4e144a9851e7b34d9b748ee";

        public static readonly Vector3 SpawnPoint = new Vector3(0f, 0.9f, 0f);
        public const float SpawnYaw = 0f;
        public const float RespawnHeight = -100f;
        public const float ArrivalSize = 20f;
        public const float ArrivalThickness = 0.5f;

        // The look: Rec Room is bright and soft - a warm sun, strong sky fill, light bounce from the floor.
        public static readonly Color SunColor = new Color(1f, 0.957f, 0.894f);
        public const float SunIntensity = 1.3f;
        public const float SunShadowStrength = 0.7f;
        public static readonly Vector3 SunAngles = new Vector3(50f, -30f, 0f);
        public static readonly Color AmbientSky = new Color(0.78f, 0.86f, 1f);
        public static readonly Color AmbientEquator = new Color(0.80f, 0.80f, 0.78f);
        public static readonly Color AmbientGround = new Color(0.52f, 0.48f, 0.44f);
        public const float AmbientIntensity = 1f;

        private const string Log = "[SomniumRecRoom] SceneSetup: ";

        [MenuItem("Somnium Rec Room/Create world scene", false, 0)]
        public static void CreateWorldScene()
        {
            Scene scene = OpenOrCreateScene();

            if (!scene.IsValid())
            {
                return;
            }

            GameObject uploader = Uploader(scene);
            Light sun = Sun(scene, true);
            Arrival(scene, true);
            TMP_Text label = StatusLabel(scene, true);
            RecRoom(scene, label);
            ApplyRenderSettings(sun);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Log + "world scene saved at " + ScenePath + " (uploader " + (uploader != null ? "ok" : "MISSING") + ").");

            RrNetSceneSetup.AddNetworkObjects();

            if (!Application.isBatchMode)
            {
                Selection.activeGameObject = uploader;
            }
        }

        /// <summary>
        /// Batch entry point: Unity.exe -batchmode -projectPath P -executeMethod
        /// SomniumSpace.Worlds.SomniumRecRoom.EditorTools.RrSceneSetup.CreateWorldSceneBatch -quit
        /// </summary>
        public static void CreateWorldSceneBatch()
        {
            try
            {
                CreateWorldScene();
                Debug.Log(Log + "batch done");
            }
            catch (Exception e)
            {
                Debug.LogError(Log + "batch failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ---- scene ----

        private static Scene OpenOrCreateScene()
        {
            Scene active = SceneManager.GetActiveScene();

            if (active.path == ScenePath)
            {
                return active;
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log(Log + "cancelled: the open scene was not saved.");
                return default(Scene);
            }

            if (File.Exists(ScenePath))
            {
                Debug.Log(Log + "opening " + ScenePath);
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(Log + "created " + ScenePath);
            return scene;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            if (!scene.IsValid())
            {
                return GameObject.Find(name);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        // ---- Uploader (SDK types found by name: SomniumSpace.SDK/Bundles are not our asmdef's references) ----

        private static GameObject Uploader(Scene scene)
        {
            Type sdkType = FindType("SomniumSpace.SDK.Configs.WorldSDKConfig");
            Type configType = FindType("SomniumSpace.Bundles.Configs.WorldConfig");

            if (sdkType == null || configType == null)
            {
                Debug.LogError(Log + "the Somnium SDK types are missing (WorldSDKConfig " + (sdkType != null) + ", WorldConfig " + (configType != null) + "): is the SDK imported?");
                return null;
            }

            GameObject go = FindRoot(scene, "Uploader");
            bool made = go == null;

            if (made)
            {
                go = new GameObject("Uploader");
                Undo.RegisterCreatedObjectUndo(go, "Create Uploader");
            }

            Component config = go.GetComponent(configType);
            bool newConfig = config == null;

            if (newConfig)
            {
                config = go.AddComponent(configType);
            }

            Component sdk = go.GetComponent(sdkType);
            bool newSdk = sdk == null;

            if (newSdk)
            {
                sdk = go.AddComponent(sdkType);
            }

            if (newConfig)
            {
                var c = new SerializedObject(config);
                SetVector3(c, "SpawnPointPos", SpawnPoint);
                SetNumber(c, "SpawnPointDirection", SpawnYaw);
                SetNumber(c, "RespawnHeightY", RespawnHeight);
                SetNumber(c, "RespawningBehavior", 0f);
                c.ApplyModifiedPropertiesWithoutUndo();
            }

            var s = new SerializedObject(sdk);

            if (newSdk)
            {
                SetString(s, "AssetName", WorldName);
                SetString(s, "AssetDescription", WorldDescription);
                SetNumber(s, "DoCleanBuild", 1f);
                SetNumber(s, "WorldIdForUpload", 0f);
                SetNumber(s, "DoReplaceExistingWorld", 0f);
                SetNumber(s, "OpenScriptingSettings", 1f);
            }

            // The fields the upload depends on: corrected whenever they drift.
            SetObject(s, "Config", config);
            SetNumber(s, "EnableScripting", 1f);
            SetString(s, "ScriptingAssembly", AssemblyName);
            Object asmdef = AssetDatabase.LoadAssetAtPath<Object>(AsmdefPath);

            if (asmdef == null)
            {
                Debug.LogError(Log + "asmdef not found at " + AsmdefPath);
            }
            else
            {
                SetObject(s, "AsmdefReference", asmdef);
            }

            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
            Debug.Log(Log + "Uploader " + (made ? "created" : "kept") + ": " + Describe(s, "AssetName", "WorldIdForUpload", "DoReplaceExistingWorld", "EnableScripting", "ScriptingAssembly", "AsmdefReference") +
                      "; WorldConfig " + Describe(new SerializedObject(config), "SpawnPointPos", "SpawnPointDirection", "RespawnHeightY", "RespawningBehavior"));
            return go;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(fullName, false);

                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        private static void SetString(SerializedObject so, string name, string value)
        {
            SerializedProperty p = Prop(so, name);

            if (p != null && p.propertyType == SerializedPropertyType.String)
            {
                p.stringValue = value;
            }
        }

        private static void SetNumber(SerializedObject so, string name, float value)
        {
            SerializedProperty p = Prop(so, name);

            if (p == null)
            {
                return;
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    p.boolValue = value != 0f;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    p.intValue = (int)value;
                    break;
                case SerializedPropertyType.Float:
                    p.floatValue = value;
                    break;
                default:
                    Debug.LogWarning(Log + so.targetObject.GetType().Name + "." + name + " is a " + p.propertyType + ", not a number: left as it is.");
                    break;
            }
        }

        private static void SetVector3(SerializedObject so, string name, Vector3 value)
        {
            SerializedProperty p = Prop(so, name);

            if (p != null && p.propertyType == SerializedPropertyType.Vector3)
            {
                p.vector3Value = value;
            }
        }

        private static void SetObject(SerializedObject so, string name, Object value)
        {
            SerializedProperty p = Prop(so, name);

            if (p == null)
            {
                return;
            }

            p.objectReferenceValue = value;

            if (p.objectReferenceValue != value)
            {
                Debug.LogWarning(Log + so.targetObject.GetType().Name + "." + name + " did not accept a " + (value != null ? value.GetType().Name : "null") + ": set it by hand.");
            }
        }

        private static SerializedProperty Prop(SerializedObject so, string name)
        {
            SerializedProperty p = so.FindProperty(name);

            if (p == null)
            {
                Debug.LogWarning(Log + so.targetObject.GetType().Name + " has no field '" + name + "' (SDK changed?): set it by hand.");
            }

            return p;
        }

        private static string Describe(SerializedObject so, params string[] names)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (string n in names)
            {
                SerializedProperty p = so.FindProperty(n);
                string v;

                if (p == null)
                {
                    v = "(missing)";
                }
                else
                {
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.String: v = "'" + p.stringValue + "'"; break;
                        case SerializedPropertyType.Boolean: v = p.boolValue ? "1" : "0"; break;
                        case SerializedPropertyType.Integer: v = p.intValue.ToString(); break;
                        case SerializedPropertyType.Enum: v = p.intValue.ToString(); break;
                        case SerializedPropertyType.Float: v = p.floatValue.ToString("0.###"); break;
                        case SerializedPropertyType.Vector3: v = p.vector3Value.ToString("0.###"); break;
                        case SerializedPropertyType.ObjectReference: v = p.objectReferenceValue != null ? p.objectReferenceValue.name + " (" + p.objectReferenceValue.GetType().Name + ")" : "null"; break;
                        default: v = p.propertyType.ToString(); break;
                    }
                }

                parts.Add(n + "=" + v);
            }

            return string.Join(", ", parts.ToArray());
        }

        // ---- runtime-safe builders (also called by the play-mode harness through reflection) ----

        /// <summary>The sun: "Directional Light", warm white, soft shadows, URP additional data. Kept if it exists.</summary>
        public static Light Sun(Scene scene, bool undo)
        {
            GameObject go = FindRoot(scene, "Directional Light");

            if (go != null && go.GetComponent<Light>() != null)
            {
                return go.GetComponent<Light>();
            }

            if (go == null)
            {
                go = new GameObject("Directional Light");
            }

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = SunColor;
            light.intensity = SunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = SunShadowStrength;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            go.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(SunAngles));

            if (go.GetComponent<UniversalAdditionalLightData>() == null)
            {
                go.AddComponent<UniversalAdditionalLightData>();
            }

            if (undo && !Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create Directional Light");
            }

            return light;
        }

        /// <summary>"Arrival": the 20 m square spawn floor, top face at y = 0, box collider on layer 23. Kept if it exists.</summary>
        public static GameObject Arrival(Scene scene, bool undo)
        {
            GameObject go = FindRoot(scene, "Arrival");

            if (go != null)
            {
                return go;
            }

            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Arrival";
            go.transform.position = new Vector3(0f, -ArrivalThickness * 0.5f, 0f);
            go.transform.localScale = new Vector3(ArrivalSize, ArrivalThickness, ArrivalSize);
            go.layer = RrLayers.Environment;

            var renderer = go.GetComponent<MeshRenderer>();
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;

            if (renderer != null && pipeline != null && pipeline.defaultMaterial != null)
            {
                renderer.sharedMaterial = pipeline.defaultMaterial;
            }

            if (undo && !Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(go, "Create Arrival");
            }

            return go;
        }

        /// <summary>"Status Canvas" with the "Status" label (ETS layout: overlay, sorting 100, 1920x1080, 20 pt, top-left, 32 px).</summary>
        public static TMP_Text StatusLabel(Scene scene, bool undo)
        {
            GameObject canvasObject = FindRoot(scene, "Status Canvas");

            if (canvasObject != null)
            {
                var existing = canvasObject.GetComponentInChildren<TMP_Text>(true);

                if (existing != null)
                {
                    return existing;
                }
            }
            else
            {
                canvasObject = new GameObject("Status Canvas");
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            var textObject = new GameObject("Status");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = LiberationSans();

            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = 20f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.text = WorldName + "\nStarting...";
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32f, 32f);
            rect.offsetMax = new Vector2(-32f, -32f);

            if (undo && !Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Status Canvas");
            }

            return text;
        }

        /// <summary>LiberationSans SDF from TMP Essentials, by GUID (the font the world ships through the label).</summary>
        public static TMP_FontAsset LiberationSans()
        {
            string path = AssetDatabase.GUIDToAssetPath(LiberationSansGuid);
            TMP_FontAsset font = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font == null)
            {
                Debug.LogWarning(Log + "LiberationSans SDF (guid " + LiberationSansGuid + ") not found: import TMP Essentials.");
            }

            return font;
        }

        /// <summary>Fog off, bright tri-colour ambient, the sun as RenderSettings.sun (active scene).</summary>
        public static void ApplyRenderSettings(Light sun)
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = AmbientIntensity;

            if (sun != null)
            {
                RenderSettings.sun = sun;
            }
        }

        /// <summary>The harness's environment: sun, arrival floor and render settings in the active scene.</summary>
        public static void BuildEnvironment()
        {
            Scene scene = SceneManager.GetActiveScene();
            Light sun = Sun(scene, false);
            Arrival(scene, false);
            ApplyRenderSettings(sun);
        }

        // ---- the world's own object ----

        private static void RecRoom(Scene scene, TMP_Text label)
        {
            GameObject go = FindRoot(scene, "Rec Room");
            bool made = go == null;

            if (made)
            {
                go = new GameObject("Rec Room");
                Undo.RegisterCreatedObjectUndo(go, "Create Rec Room");
            }

            RrMain main = go.GetComponent<RrMain>();

            if (main == null)
            {
                main = go.AddComponent<RrMain>();
            }

            var so = new SerializedObject(main);
            SerializedProperty status = so.FindProperty("_status");

            if (status != null && status.objectReferenceValue == null)
            {
                status.objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(go);
            Debug.Log(Log + "Rec Room " + (made ? "created" : "kept") + "; status label " + (status != null && status.objectReferenceValue != null ? "wired" : "NOT wired"));
        }
    }
}
