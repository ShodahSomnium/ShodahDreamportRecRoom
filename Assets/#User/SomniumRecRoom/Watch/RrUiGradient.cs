using UnityEngine;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// A two-colour linear gradient over any UI graphic, standing in for Rec Room's RRUI.UIGradient (a script
    /// that is missing at runtime, so its colours would otherwise be lost). Rec Room's watch uses it on the orange
    /// navigation bar (#FF5F2F from 30% of the way to #FF7D31), the home tiles (white to #CAC8C6 at -45 degrees),
    /// the primary buttons (#F76B1C to #F63E16) and the shadow above the navigation bar (black 50% to clear).
    ///
    /// It tints the vertices, so it multiplies with the graphic's own colour and works on rounded (sliced)
    /// images: their corner vertices sit on the rectangle's edges and the colour is linear in position, so the
    /// interpolation between vertices is exact. Angle 0 runs left to right, 90 bottom to top.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class RrUiGradient : BaseMeshEffect
    {
        public Color From = Color.white;
        public Color To = Color.white;

        /// <summary>Direction in degrees: 0 left to right, 90 bottom to top, -90 top to bottom.</summary>
        public float Angle;

        /// <summary>Where along the direction (0..1) From ends and To is reached (Rec Room's gradient keys).</summary>
        public float KeyStart;
        public float KeyEnd = 1f;

        public void Set(Color from, Color to)
        {
            if (from == From && to == To)
            {
                return;
            }

            From = from;
            To = to;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
            {
                return;
            }

            float rad = Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var r = graphic.rectTransform.rect;
            float a = Vector2.Dot(new Vector2(r.xMin, r.yMin), dir);
            float b = Vector2.Dot(new Vector2(r.xMax, r.yMax), dir);
            float c = Vector2.Dot(new Vector2(r.xMin, r.yMax), dir);
            float d = Vector2.Dot(new Vector2(r.xMax, r.yMin), dir);
            float min = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
            float span = Mathf.Max(0.0001f, Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d)) - min);
            float keySpan = Mathf.Max(0.0001f, KeyEnd - KeyStart);
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = (Vector2.Dot(v.position, dir) - min) / span;
                float k = Mathf.Clamp01((t - KeyStart) / keySpan);
                Color tint = Color.Lerp(From, To, k);
                Color baseColor = v.color;
                v.color = baseColor * tint;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
