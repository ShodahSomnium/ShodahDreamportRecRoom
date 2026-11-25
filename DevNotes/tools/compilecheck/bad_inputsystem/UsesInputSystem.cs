using UnityEngine;
using UnityEngine.InputSystem; // NOT on the server list: must fail with CS0246 in the server-like build

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    public sealed class UsesInputSystem : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Debug.Log("space");
        }
    }
}
