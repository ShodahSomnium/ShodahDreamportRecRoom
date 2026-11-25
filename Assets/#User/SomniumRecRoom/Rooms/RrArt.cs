using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>A picture from Rec Room's bundles, ready to draw: the texture and the part of it to show (<see cref="RrCoverArt"/>).</summary>
    public sealed class RrArt
    {
        /// <summary>The sprite, when the art is one (UGUI Images draw it directly, atlas packing included).</summary>
        public Sprite Sprite;

        public Texture Texture;

        /// <summary>Normalised rectangle of <see cref="Texture"/> to show (whole texture: 0, 0, 1, 1).</summary>
        public Rect Uv;

        /// <summary>Width / height of the shown part, in pixels.</summary>
        public float Aspect;
    }
}
