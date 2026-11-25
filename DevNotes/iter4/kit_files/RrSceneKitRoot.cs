using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// On the root of a scene built by <see cref="RrSceneKit"/>: owns what the kit created that is not a scene
    /// object (merged meshes, stand-in meshes) and destroys it with the room, and puts back any render setting the
    /// scene changed (ambient, skybox) when it goes. Install assets (meshes, materials) are not destroyed here:
    /// RrAssets owns them and frees them with the room's scope.
    /// </summary>
    public sealed class RrSceneKitRoot : MonoBehaviour
    {
        [NonSerialized] public RrSceneKitResult Result;

        private readonly List<Object> _owned = new List<Object>();

        private bool _ambientSaved;
        private AmbientMode _ambientMode;
        private Color _ambientLight;
        private Color _ambientSky;
        private Color _ambientEquator;
        private Color _ambientGround;
        private float _ambientIntensity;

        private bool _skySaved;
        private Material _sky;

        public int Owned
        {
            get { return _owned.Count; }
        }

        public void Own(Object o)
        {
            if (o != null)
            {
                _owned.Add(o);
            }
        }

        /// <summary>The scene's ambient (flat colour, or trilight sky/equator/ground) until this root is destroyed.</summary>
        public void ApplyAmbient(Color sky, Color equator, Color ground, float intensity, bool trilight)
        {
            if (!_ambientSaved)
            {
                _ambientSaved = true;
                _ambientMode = RenderSettings.ambientMode;
                _ambientLight = RenderSettings.ambientLight;
                _ambientSky = RenderSettings.ambientSkyColor;
                _ambientEquator = RenderSettings.ambientEquatorColor;
                _ambientGround = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
            }

            if (trilight)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = sky;
            }

            RenderSettings.ambientIntensity = intensity > 0f ? intensity : 1f;
        }

        public void ApplySky(Material m)
        {
            if (m == null)
            {
                return;
            }

            if (!_skySaved)
            {
                _skySaved = true;
                _sky = RenderSettings.skybox;
            }

            RenderSettings.skybox = m;
        }

        private void OnDestroy()
        {
            if (_ambientSaved)
            {
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientLight = _ambientLight;
                RenderSettings.ambientSkyColor = _ambientSky;
                RenderSettings.ambientEquatorColor = _ambientEquator;
                RenderSettings.ambientGroundColor = _ambientGround;
                RenderSettings.ambientIntensity = _ambientIntensity;
            }

            if (_skySaved)
            {
                RenderSettings.skybox = _sky;
            }

            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Destroy(_owned[i]);
                }
            }

            _owned.Clear();
            RrSceneKit.Forget(this);
        }
    }
}
