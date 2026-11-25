using System;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Level data kept out of the assembly: the generated <c>RrScene_*.cs</c> strings live in .NET's user-string heap
    /// (16 MB = ~8.3M characters for the whole world; 5.1M were used by iteration 11), so rooms added from iteration 13 on
    /// ship their RRSK as gzip files instead: <c>Data/&lt;Name&gt;.bytes</c> (DevNotes/tools/rrscene/rrsk2bytes.sh, the same
    /// lossless compaction as rrsk2cs.sh), carried into the upload by RrMain's scene data list (filled from the folder by
    /// DevNotes/tools/rrscene/bankscene.sh). Placement data only (names and numbers), as the generated classes.
    /// </summary>
    public static class RrSceneBank
    {
        /// <summary>Whether the scene carries the data file for this RRSK name.</summary>
        public static bool Has(string name)
        {
            return Find(name) != null;
        }

        /// <summary>
        /// The RRSK for <see cref="RrSceneKit.Build"/> / <see cref="RrSceneData"/>, as an RRSZ1 payload (inflated on the
        /// kit's worker), or null when the scene does not carry it.
        /// </summary>
        public static string Text(string name)
        {
            var asset = Find(name);
            if (asset == null)
            {
                RrLog.WarnOnce("SceneBank", "missing:" + name, "no scene data \"" + name + "\" in RrMain's scene data list (run DevNotes/tools/rrscene/bankscene.sh)");
                return null;
            }

            var bytes = asset.bytes;
            return bytes == null || bytes.Length == 0 ? null : RrSceneKitData.CompressedPrefix + Convert.ToBase64String(bytes);
        }

        private static TextAsset Find(string name)
        {
            var main = RrMain.Instance;
            if (main == null || main.SceneData == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var a in main.SceneData)
            {
                if (a != null && a.name == name)
                {
                    return a;
                }
            }

            return null;
        }
    }
}
