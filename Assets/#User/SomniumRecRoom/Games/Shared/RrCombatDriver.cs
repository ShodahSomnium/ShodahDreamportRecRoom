using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// The one per-world object of the combat kit ("Rr Combat" under the world root): steps every projectile after
    /// the frame's movement (execution order 100, so hit volumes and enemies have moved), ages decals, and clears
    /// the kit's statics when the world goes away (the world root is destroyed by RrMain.OnDestroy).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RrCombatDriver : MonoBehaviour
    {
        private void Update()
        {
            RrProjectile.StepAll(Time.deltaTime);
            RrImpacts.Tick();
        }

        private void OnDestroy()
        {
            RrCombat.Shutdown();
        }
    }
}
