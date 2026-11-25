using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>A terrain chunk of <see cref="RrSceneKitTerrain"/>: owns its generated mesh (destroyed with it).</summary>
    public sealed class RrSceneKitTerrainChunk : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh != null)
            {
                Destroy(Mesh);
                Mesh = null;
            }
        }
    }
}
