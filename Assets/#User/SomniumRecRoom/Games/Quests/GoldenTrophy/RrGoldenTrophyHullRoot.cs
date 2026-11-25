using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// Owner of what <see cref="RrGoldenTrophyHull"/> makes at run time (the merged hull and dressing meshes and the
    /// surface materials made from Rec Room's install materials): destroys them with the area, so nothing outlives a
    /// world visit (ARCHITECTURE hard rule 5).
    /// </summary>
    public sealed class RrGoldenTrophyHullRoot : MonoBehaviour
    {
        public readonly List<Object> Owned = new List<Object>();

        private void OnDestroy()
        {
            foreach (var o in Owned)
            {
                if (o != null)
                {
                    Destroy(o);
                }
            }

            Owned.Clear();
        }
    }
}
