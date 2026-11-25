using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// A gameplay marker of a built scene (an RRSK "G" record, or a non-looping audio source as kind "audio"):
    /// spawn points, checkpoints, enemy spawners, doors, levers, trigger zones, weapon racks, flag bases... The
    /// kit builds nothing for them; game code reads them from <see cref="RrSceneKitResult.Markers"/>.
    /// </summary>
    public sealed class RrSceneMarker
    {
        public string Kind;
        public string Name;

        /// <summary>Pose in the parent the scene was built under (the kit's offset applied).</summary>
        public Vector3 Position;

        public Quaternion Rotation;

        /// <summary>Pose as stored in the scene (its own world space).</summary>
        public Vector3 ScenePosition;

        public Quaternion SceneRotation;

        /// <summary>key=value pairs of the record (empty when it had none).</summary>
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        public string Get(string key, string fallback = null)
        {
            string v;
            return key != null && Values.TryGetValue(key, out v) ? v : fallback;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            float f;
            string v = Get(key);
            return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            int i;
            string v = Get(key);
            return v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : fallback;
        }

        public bool Has(string key)
        {
            return key != null && Values.ContainsKey(key);
        }

        public Pose Pose
        {
            get { return new Pose(Position, Rotation); }
        }

        public override string ToString()
        {
            return Kind + " " + Name + " @" + Position.ToString("F2");
        }
    }
}
