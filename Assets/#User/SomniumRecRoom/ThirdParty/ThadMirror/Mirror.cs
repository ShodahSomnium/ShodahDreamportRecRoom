using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

#if UNITY_EDITOR
using UnityEditor;
#endif

// Thad's mirror (the user's package; shader and materials in Assets/Thad/Mirrors), unchanged except that it lives in the
// world's assembly and namespace: the Somnium upload compiles only the world's code, under SomniumSpace.Worlds.<World>.
namespace SomniumSpace.Worlds.SomniumRecRoom.ThadMirror
{
    public enum MirrorAntiAliasing
    {
        Off = 1,
        x2 = 2,
        x4 = 4,
        x8 = 8
    }

    public enum MirrorBackground
    {
        DontClear = CameraClearFlags.Depth, // always clear at least depth
        SolidColor = CameraClearFlags.SolidColor,
        Skybox = CameraClearFlags.Skybox
    }

    [DefaultExecutionOrder(1)]
    public class Mirror : MonoBehaviour
    {
        [Header("Mirror Settings")]
        [SerializeField] private LayerMask _CullingMask = ~0;
        [SerializeField] private bool _UseOcclusionCulling = false;
        [SerializeField, Range(0.0f, 1.0f)] private float _ResolutionScale = 1.0f;
        [Tooltip("Number of anti aliasing samples depends on graphics quality settings and is only limited by this property.")]
        public MirrorAntiAliasing MaxAntiAliasing = MirrorAntiAliasing.Off;
        [SerializeField] private MirrorBackground _Background = MirrorBackground.Skybox;
        [SerializeField] private Color _BackgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        [Header("Advanced Settings")]
        [Tooltip("Manually assign a camera only if you want to have more control over the camera parameters.")]
        [SerializeField] private Camera _MirrorCamera = null;
        protected Renderer _MirrorRenderer = null;
        protected Material _MirrorMaterial = null;
        protected static bool _IsRendering = false;
        protected RenderTexture _ReflectionTexture;
        protected Vector4 _ReflectionSize = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
        protected int _ReflectionSizeId;
        protected bool _IsActive = false;
        public bool IsActive
        {
            get => _IsActive;
            private set
            {
                if (_IsActive != value)
                {
                    _IsActive = value;
                    if (_IsActive)
                    {
                        RenderPipelineManager.beginCameraRendering += _BeginRendering;
                    }
                    else
                    {
                        RenderPipelineManager.beginCameraRendering -= _BeginRendering;
                    }
                }
            }
        }

        public LayerMask CullingMask
        {
            get => _CullingMask;
            set
            {
                _CullingMask = value;
                if (_MirrorCamera != null)
                {
                    _MirrorCamera.cullingMask = _CullingMask & ~(1 << this.gameObject.layer);
                }
            }
        }

        public bool UseOcclusionCulling
        {
            get => _UseOcclusionCulling;
            set
            {
                _UseOcclusionCulling = value;
                if (_MirrorCamera != null)
                {
                    _MirrorCamera.useOcclusionCulling = value;
                }
            }
        }

        public MirrorBackground Background
        {
            get => _Background;
            set
            {
                _Background = value;
                if (_MirrorCamera != null)
                {
                    _MirrorCamera.clearFlags = (CameraClearFlags)_Background;
                }
            }
        }

        public Color BackgroundColor
        {
            get => _BackgroundColor;
            set
            {
                _BackgroundColor = value;
                if (_MirrorCamera != null)
                {
                    _MirrorCamera.backgroundColor = value;
                }
            }
        }

        public float ResolutionScale
        {
            get => _ResolutionScale;
            set
            {
                if (_ResolutionScale != value & _ReflectionTexture != null)
                {
                    _ReflectionTexture.Release();
                }
                _ResolutionScale = value;
            }
        }

        public Camera MirrorCamera => _MirrorCamera;

        private void Awake()
        {
            _MirrorRenderer = GetComponent<Renderer>();
            _MirrorMaterial = _MirrorRenderer.material;
            if (_MirrorCamera == null)
            {
                GameObject camObject = new GameObject("MirrorCamera");
                camObject.transform.parent = this.transform;
                _MirrorCamera = camObject.AddComponent<Camera>();
            }
            _ReflectionTexture = _MirrorCamera.targetTexture;
            if (_ReflectionTexture == null)
            {
                _ReflectionTexture = _CreateTexture();
                _MirrorCamera.targetTexture = _ReflectionTexture;
            }
            _ReflectionSizeId = Shader.PropertyToID("_ReflectionSize");
            _MirrorMaterial.SetVector(_ReflectionSizeId, _ReflectionSize);
            _MirrorMaterial.SetTexture("_ReflectionTex", _ReflectionTexture);

            _MirrorCamera.enabled = false;
            // update camera properties
            CullingMask = _CullingMask;
            Background = _Background;
            BackgroundColor = _BackgroundColor;
            UseOcclusionCulling = _UseOcclusionCulling;
        }

        private void OnDestroy()
        {
            if (_ReflectionTexture != null)
            {
                Destroy(_ReflectionTexture);
            }
            IsActive = false;
        }

        private void OnDisable()
        {
            if (_ReflectionTexture != null)
            {
                _ReflectionTexture.Release();
            }
            IsActive = false;
        }

        private void OnBecameVisible()
        {
            IsActive = true;
        }

        private void OnBecameInvisible()
        {
            IsActive = false;
        }

        protected void _BeginRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null | camera == _MirrorCamera | _IsRendering
                || !_IsVisibleToCamera(camera))
            {
                return;
            }
            _IsRendering = true;

            Vector3 eyePos;
            Quaternion eyeRot;
            Rect viewportRect;

            bool renderBothEyes = false;
            int width;
            int height;

            if (camera.targetTexture != null)
            {
                width = camera.targetTexture.width;
                height = camera.targetTexture.height;
            }
            else
            {
                if (XRSettings.enabled)
                {
                    width = XRSettings.eyeTextureWidth;
                    height = XRSettings.eyeTextureHeight;
                    renderBothEyes = true;
                }
                else
                {
                    width = camera.pixelWidth;
                    height = camera.pixelHeight;
                }
            }
            width = (int)(width * _ResolutionScale);
            height = (int)(height * _ResolutionScale);

            if (renderBothEyes)
            {
                _AdjustTexture(width, height * 2);
                viewportRect = new Rect(0.0f, 0.0f, (float)width / (float)_ReflectionTexture.width, (float)height / (float)_ReflectionTexture.height);

                InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                Vector3 centerPos;
                Quaternion centerRot;
                if (device.isValid
                    && device.TryGetFeatureValue(CommonUsages.centerEyePosition, out centerPos)
                    && device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out centerRot))
                {
                    Quaternion eye2CamRotation = camera.transform.rotation * Quaternion.Inverse(centerRot);
                    device = InputDevices.GetDeviceAtXRNode(XRNode.LeftEye);
                    if (device.isValid
                        && device.TryGetFeatureValue(CommonUsages.leftEyePosition, out eyePos)
                        && device.TryGetFeatureValue(CommonUsages.leftEyeRotation, out eyeRot))
                    {
                        viewportRect.y = 0.0f;
                        eyePos = (eye2CamRotation * (eyePos - centerPos)) + camera.transform.position;
                        eyeRot = eye2CamRotation * eyeRot;
                        _RenderCamera(context, eyePos, eyeRot, viewportRect, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), camera);
                    }
                    device = InputDevices.GetDeviceAtXRNode(XRNode.RightEye);
                    if (device.isValid
                        && device.TryGetFeatureValue(CommonUsages.rightEyePosition, out eyePos)
                        && device.TryGetFeatureValue(CommonUsages.rightEyeRotation, out eyeRot))
                    {
                        viewportRect.y = viewportRect.height;
                        eyePos = (eye2CamRotation * (eyePos - centerPos)) + camera.transform.position;
                        eyeRot = eye2CamRotation * eyeRot;
                        _RenderCamera(context, eyePos, eyeRot, viewportRect, camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), camera);
                    }
                }
            }
            else
            {
                _AdjustTexture(width, height);
                viewportRect = new Rect(0.0f, 0.0f, (float)width / (float)_ReflectionTexture.width, (float)height / (float)_ReflectionTexture.height);

                eyePos = camera.transform.position;
                eyeRot = camera.transform.rotation;
                _RenderCamera(context, eyePos, eyeRot, viewportRect, camera.projectionMatrix, camera);
            }

            _IsRendering = false;
        }

        protected bool _IsVisibleToCamera(Camera cam)
        {
            // layer check
            if ((cam.cullingMask & (1 << this.gameObject.layer)) == 0)
            {
                return false;
            }
            // frustum check
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, _MirrorRenderer.bounds);
        }

        protected void _RenderCamera(ScriptableRenderContext context, Vector3 eyePos, Quaternion eyeRot, Rect viewportRect, Matrix4x4 projectionMatrix, Camera povCamera)
        {
            if (_MirrorCamera == null)
            {
                return;
            }
            _MirrorCamera.rect = viewportRect;
            if (_ReflectionSize.x != viewportRect.width || _ReflectionSize.y != viewportRect.height)
            {
                _ReflectionSize.x = viewportRect.width;
                _ReflectionSize.y = viewportRect.height;
                _MirrorMaterial.SetVector(_ReflectionSizeId, _ReflectionSize);
            }

            _MirrorCamera.fieldOfView = povCamera.fieldOfView;
            _MirrorCamera.nearClipPlane = povCamera.nearClipPlane; // effectivly only affects culling
            _MirrorCamera.farClipPlane = povCamera.farClipPlane; // TODO: better use spherical far clipping and make it parameterizable

            //projectionMatrix[0, 0] = -projectionMatrix[0, 0]; // commented out, no flip horizontally -> done in shader instead
            projectionMatrix[0, 2] = -projectionMatrix[0, 2]; // flip horizontal sensor shift

            _MirrorCamera.projectionMatrix = projectionMatrix;

            Vector3 mirrorPos = this.transform.position;
            Vector3 mirror2eye = eyePos - mirrorPos;
            Vector3 normal = this.transform.forward;

            // Flip normal if it faces the same direction as camera view (to clip behind mirror)
            if (Vector3.Dot(normal, mirror2eye) < 0f)
            {
                normal = -normal;
            }

            Vector3 cameraPos = mirrorPos + Vector3.Reflect(mirror2eye, normal);

            Vector3 forward = eyeRot * Vector3.forward;
            Vector3 up = eyeRot * Vector3.up;
            Vector3 reflectedForward = Vector3.Reflect(forward, normal);
            Vector3 reflectedUp = Vector3.Reflect(up, normal);
            Quaternion cameraRot = Quaternion.LookRotation(reflectedForward, reflectedUp);

            _MirrorCamera.transform.SetPositionAndRotation(cameraPos, cameraRot);

            // Define the plane: ax + by + cz + d = 0 → (normal, -dot(normal, pos))
            Vector4 planeWorld = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, mirrorPos));

            // Convert plane to camera space
            Matrix4x4 cameraToWorld = _MirrorCamera.cameraToWorldMatrix;
            Vector4 planeCameraSpace = cameraToWorld.transpose * planeWorld;

            // Adjust the projection matrix with the custom clip plane
            _MirrorCamera.projectionMatrix = _MirrorCamera.CalculateObliqueMatrix(planeCameraSpace);

            //GL.invertCulling = true; // not needed without horizontal flip
            UniversalRenderPipeline.RenderSingleCamera(context, _MirrorCamera);
            // proper way of doing above line, but this would cause exceptions:
            //var request = new UniversalRenderPipeline.SingleCameraRequest();
            //RenderPipeline.SubmitRenderRequest(_MirrorCamera, request);
            //GL.invertCulling = false;
        }

        protected int _GetMSAASamples()
        {
            return QualitySettings.antiAliasing > 0 ? Mathf.Min(QualitySettings.antiAliasing, (int)MaxAntiAliasing) : 1;
        }

        private RenderTexture _CreateTexture()
        {
            // create a small dummy that will be resized on demand
            RenderTextureDescriptor desc = new RenderTextureDescriptor(64, 64, RenderTextureFormat.DefaultHDR, 24);
            desc.msaaSamples = _GetMSAASamples();
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            var texture = new RenderTexture(desc);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _MirrorCamera.targetTexture = texture;
            return texture;
        }

        protected void _AdjustTexture(int width, int height)
        {
            int aa = _GetMSAASamples();
            if (!_ReflectionTexture.IsCreated())
            {
                _ReflectionTexture.width = width;
                _ReflectionTexture.height = height;
                _ReflectionTexture.antiAliasing = aa;
            }
            else if (width > _ReflectionTexture.width | height > _ReflectionTexture.height | _ReflectionTexture.antiAliasing != aa)
            {
                _ReflectionTexture.Release();
                _ReflectionTexture.width = Mathf.Max(_ReflectionTexture.width, width);
                _ReflectionTexture.height = Mathf.Max(_ReflectionTexture.height, height);
                _ReflectionTexture.antiAliasing = aa;
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Mirror))]
    [CanEditMultipleObjects]
    public class MirrorEditor : Editor
    {
        private static readonly GUIContent mirrorCameraLabel = new GUIContent("Mirror Camera (auto created if not assigned)");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty occlusionCullingProp = serializedObject.FindProperty("_UseOcclusionCulling");
            SerializedProperty backgroundProp = serializedObject.FindProperty("_Background");
            SerializedProperty backgroundColorProp = serializedObject.FindProperty("_BackgroundColor");
            SerializedProperty mirrorCameraProp = serializedObject.FindProperty("_MirrorCamera");

            // properties
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                // Skip background color prop if background is not set to solid color
                if (prop.name == backgroundColorProp.name && backgroundProp.intValue != (int)MirrorBackground.SolidColor)
                {
                    //continue; not needed, only "else" branches left
                }
                else if (prop.name == mirrorCameraProp.name)
                {
                    EditorGUILayout.PropertyField(prop, mirrorCameraLabel);
                }
                else // all other properties
                {
                    EditorGUILayout.PropertyField(prop, true);
                }
            }

            bool showLayerWarning = false;
            bool showRendererError = false;

            foreach (var t in targets)
            {
                Mirror mirror = t as Mirror;
                if (mirror == null) continue;

                // forward changes to mirror camera
                // culling mask is always set, because GUI.changed doesn't seem to work for layer masks
                mirror.CullingMask = mirror.CullingMask;
                if (GUI.changed)
                {
                    mirror.Background = (MirrorBackground)backgroundProp.intValue;
                    mirror.BackgroundColor = backgroundColorProp.colorValue;
                    mirror.UseOcclusionCulling = occlusionCullingProp.boolValue;
                }

                showLayerWarning |= ((1 << mirror.gameObject.layer) & mirror.CullingMask.value) != 0;
                showRendererError |= mirror.GetComponent<Renderer>() == null;
            }

            if (showLayerWarning)
            {
                EditorGUILayout.HelpBox(
                    "Warning: The layer that the mirror is on will be removed from the culling mask during runtime.",
                    MessageType.Warning
                );
            }

            if (showRendererError)
            {
                EditorGUILayout.HelpBox(
                    "Error: No Renderer component found on Mirror.",
                    MessageType.Error
                );
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

}
