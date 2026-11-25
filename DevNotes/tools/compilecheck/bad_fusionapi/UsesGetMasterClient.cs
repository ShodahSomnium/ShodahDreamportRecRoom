using Fusion;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    public sealed class UsesGetMasterClient : MonoBehaviour
    {
        // Compiles against the local Fusion 2.0.12, but the upload server's Fusion is older and has no
        // GetMasterClient (ETS upload 2026-09-23 failed with CS1061). The guard must print it.
        private void Update()
        {
            NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
            if (runner != null) Debug.Log(runner.GetMasterClient());
        }
    }
}
