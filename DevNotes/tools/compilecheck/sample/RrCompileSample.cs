using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Fusion;
using SomniumSpace.Bridge;
using SomniumSpace.Bridge.Player;
using SomniumSpace.Network.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Compile-check sample only (never shipped). Touches every API family a Rec Room world is
    /// expected to use, so a clean build against the server's reference list proves the family
    /// is reachable: AssetBundle loading, URP materials, AudioClip.Create, TMP + UGUI, XRI,
    /// UnityEngine.XR input, the Bridge, Fusion + SomniumSpace.Network, threads, web audio.
    /// </summary>
    public sealed class RrCompileSample : MonoBehaviour, IPointerClickHandler
    {
        private AssetBundle _bundle;
        private Thread _worker;
        private NetworkBridgeData _slot; // the BASE type, as AoM and ETS hold it (the guard has no proof for NetworkBridgeData64 as a type in code)

        private IEnumerator Start()
        {
            // 1. Plain AssetBundle API (UnityEngine.AssetBundleModule is on the server list).
            string path = Path.Combine("E:/SteamLibrary/steamapps/common/RecRoom/RecRoom_Data/StreamingAssets/aa/StandaloneWindows64", "example.bundle");
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
            yield return request;
            _bundle = request.assetBundle;

            if (_bundle != null)
            {
                AssetBundleRequest load = _bundle.LoadAssetAsync<GameObject>("example");
                yield return load;
                var holder = new GameObject("Rr holder (inactive)");
                holder.SetActive(false);
                GameObject copy = load.asset != null ? Instantiate((GameObject)load.asset, holder.transform) : null;

                if (copy != null)
                {
                    foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour == null)
                        {
                            continue; // a missing script shows up as a null entry
                        }
                    }
                }
            }

            // 2. Stock URP shader (the client ships it, so Shader.Find works for it).
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(lit != null ? lit : Shader.Find("Sprites/Default"));
            material.SetColor("_BaseColor", Color.white);
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            Debug.Log("[SomniumRecRoom] Sample: URP asset " + (urp != null ? urp.name : "none") + ", material " + material.shader.name);

            // 3. Sound through AudioClip.Create + SetData (OnAudioFilterRead is never called for world code).
            var samples = new float[4410];
            AudioClip clip = AudioClip.Create("beep", samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            gameObject.AddComponent<AudioSource>().PlayOneShot(clip);

            using (UnityWebRequest web = UnityWebRequestMultimedia.GetAudioClip("file:///C:/x.ogg", AudioType.OGGVORBIS))
            {
                yield return web.SendWebRequest();
            }

            // 4. TMP + UGUI + the XR UI raycaster.
            var canvasObject = new GameObject("Rr Canvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(canvasObject.transform, false);
            label.text = "Rec Room";
            label.font = TMP_Settings.defaultFontAsset;

            // 5. XRI interactable (compiles without Unity.XR.CoreUtils / Unity.InputSystem).
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            XRSimpleInteractable interactable = handle.AddComponent<XRSimpleInteractable>();
            interactable.interactionLayers = InteractionLayerMask.GetMask("Default");
            interactable.activated.AddListener(_ => Debug.Log("[SomniumRecRoom] Sample: activated"));

            // 6. UnityEngine.XR controllers.
            var found = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, found);
            bool present = XRSettings.isDeviceActive;

            // 7. The Bridge.
            // SomniumBridge.HasInstance is PROTECTED (CS0122); every static getter already returns null without a client.
            ISomniumPlayer local = SomniumBridge.PlayersContainer?.LocalPlayer;
            Transform leftHand = local?.References?.Body?.LeftHand;
            string nick = local?.Properties?.NickName;
            local?.Features?.Motion?.DoTeleportToPoint(Vector3.zero, Vector3.zero, null);
            Debug.Log("[SomniumRecRoom] Sample: VR " + present + ", devices " + found.Count + ", hand " + (leftHand != null) + ", nick " + nick);

            // 8. Fusion + SomniumSpace.Network (IsSharedModeMasterClient is proven on the server).
            NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
            bool master = runner != null && runner.IsRunning && runner.IsSharedModeMasterClient;

            if (_slot != null && _slot.Object != null && _slot.Object.HasStateAuthority)
            {
                _slot.IntArray.Set(0, master ? 1 : 0);
            }

            // 9. A background thread for pure work.
            _worker = new Thread(() => { Thread.Sleep(1); }) { IsBackground = true, Name = "Rr sample" };
            _worker.Start();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("[SomniumRecRoom] Sample: clicked");
        }

        private void OnDestroy()
        {
            if (_bundle != null)
            {
                _bundle.Unload(false);
            }
        }
    }
}
