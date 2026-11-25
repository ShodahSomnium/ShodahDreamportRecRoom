using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// The dorm mirror (<c>(Dorm_FreeMirror)</c>: painted frame 2.12 x 2.29 m, glass quad <c>MirrorProp</c> 2.04 x 2.21 m),
    /// also the Rec Center hallway's. Rec Room's <c>Mirror</c> script renders the reflection with its own camera and the
    /// <c>AG/Mirror</c> shader, both inert here, so the glass is replaced by an <see cref="RrMirror"/> (Thad's per-eye
    /// planar mirror within <see cref="Range"/> m in front, a stylised glass otherwise or when switched off), 5 mm in
    /// front of the original glass, in our white painted frame.
    /// </summary>
    public sealed class RrDormMirror : MonoBehaviour
    {
        public const float GlassWidth = 2.04f;
        public const float GlassHeight = 2.21f;
        public float Range = 6.5f;

        private RrMirror _mirror;

        /// <summary>The mirror panel's on/off switch (off = stylised glass only).</summary>
        public bool Enabled
        {
            get { return _mirror == null || _mirror.Enabled; }
            set
            {
                if (_mirror != null)
                {
                    _mirror.Enabled = value;
                }
            }
        }

        public bool Reflecting
        {
            get { return _mirror != null && _mirror.Reflecting; }
        }

        public int FramesRendered
        {
            get { return _mirror != null ? _mirror.FramesLive : 0; }
        }

        public void Setup(RrInstance inst)
        {
            // Hide Rec Room's glass (AG/Mirror needs their reflection script).
            var prop = inst.Find("MirrorProp");
            if (prop != null)
            {
                foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = false;
                }
            }

            // The glass faces +z of the mirror root (the room side).
            _mirror = RrMirror.Create(transform, "Dreamport Mirror", new Vector3(0f, 0f, 0.005f), Quaternion.identity, GlassWidth, GlassHeight, Range);

            // Rec Room's painted frame (Wood_VertColor_Painted_Horizontal_Emit_Mat) did not show in the harness renders:
            // a white painted frame of ours around the glass, 8 cm wide, like the dorm's window frames.
            var frame = RrLook.Solid(RrDormRoom.FrameColor);
            const float b = 0.08f, depth = 0.05f;
            float ow = GlassWidth + 2f * b, oh = GlassHeight + 2f * b;
            RrBuild.Box(transform, "Frame Top", new Vector3(0f, (GlassHeight + b) * 0.5f, 0.01f), new Vector3(ow, b, depth), frame, false);
            RrBuild.Box(transform, "Frame Bottom", new Vector3(0f, -(GlassHeight + b) * 0.5f, 0.01f), new Vector3(ow, b, depth), frame, false);
            RrBuild.Box(transform, "Frame Left", new Vector3(-(GlassWidth + b) * 0.5f, 0f, 0.01f), new Vector3(b, oh, depth), frame, false);
            RrBuild.Box(transform, "Frame Right", new Vector3((GlassWidth + b) * 0.5f, 0f, 0.01f), new Vector3(b, oh, depth), frame, false);
            RrLog.Info("Dorm", "mirror ready (" + (_mirror.Available ? "Thad's planar mirror" : "stylised glass only: no mirror shader") + " within " + Range.ToString("0.0") + " m)");
        }
    }
}
