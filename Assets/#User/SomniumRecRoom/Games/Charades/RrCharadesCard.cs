using SomniumSpace.Worlds.SomniumRecRoom.Player;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Charades
{
    /// <summary>
    /// The secret word card. Rec Room's <c>[CharadesCard]</c> has no container key (rr_party.md §1.3), so it is
    /// rebuilt at its measured size: a 0.197 x 0.147 x 0.008 m golden-orange card (<c>Paint_GoldenOrange_Mat</c>)
    /// with a white face and orange bold italic text. Only the performer's client writes the word on it; everyone
    /// else's copy says Rec Room's <c>hiddenWordString</c>, "No Peeking!".
    ///
    /// Two uses: the podium card on the stage (a big copy, <see cref="Build"/> with a scale), and the performer's
    /// own hand card, which <see cref="Follow"/> keeps in view: in VR above the left hand facing the eyes, in
    /// screen mode at the lower left of the view (where Rec Room's screen mode shows a held card). It runs after
    /// the Player module's LateUpdate so it never lags the camera.
    /// </summary>
    [DefaultExecutionOrder(10100)]
    public sealed class RrCharadesCard : MonoBehaviour
    {
        public const string Hidden = "No Peeking!";
        public static readonly Color GoldenOrange = new Color(1f, 0.62f, 0.12f);
        public static readonly Vector3 Size = new Vector3(0.197f, 0.147f, 0.008f);

        public TextMeshPro Text;
        public TextMeshPro Header;

        /// <summary>Follow the local player's hand or view (the performer's own card).</summary>
        public bool Follow;

        /// <summary>Builds a card facing its local -z (read from the -z side), scaled uniformly.</summary>
        public static RrCharadesCard Build(Transform parent, string name, float scale)
        {
            var root = RrBuild.Group(parent, name).transform;
            root.localScale = Vector3.one * scale;
            RrBuild.Box(root, "Card", Vector3.zero, Size, RrLook.Solid(GoldenOrange), false);
            RrBuild.Box(root, "Face", new Vector3(0f, -0.006f, -Size.z * 0.5f - 0.0005f), new Vector3(Size.x - 0.02f, Size.y - 0.034f, 0.001f), RrLook.Solid(RrLook.White), false);
            var card = root.gameObject.AddComponent<RrCharadesCard>();
            card.Header = RrBuild.Sign(root, "Header", "3D CHARADES", new Vector3(0f, Size.y * 0.5f - 0.009f, -Size.z * 0.5f - 0.001f), Quaternion.identity, 0.12f, Color.white, true, Size.x);
            card.Text = RrBuild.Sign(root, "Word", Hidden, new Vector3(0f, -0.006f, -Size.z * 0.5f - 0.0012f), Quaternion.identity, 0.35f, GoldenOrange, true, Size.x - 0.03f);
            card.Text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            card.Text.enableAutoSizing = true;
            card.Text.fontSizeMin = 0.08f;
            card.Text.fontSizeMax = 0.35f;
            card.Text.rectTransform.sizeDelta = new Vector2(Size.x - 0.03f, Size.y - 0.04f);
            card.Text.alignment = TextAlignmentOptions.Center;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return card;
        }

        public void SetWord(string word)
        {
            if (Text != null && Text.text != word)
            {
                Text.text = word;
            }
        }

        private void LateUpdate()
        {
            if (!Follow)
            {
                return;
            }

            var cam = RrPlayer.Camera;
            Vector3 head = cam != null ? cam.transform.position : RrPlayer.HeadPosition;
            var left = RrHand.Left;
            if (RrPlayer.InVr && left != null && left.Tracked)
            {
                Vector3 pos = left.Position + Vector3.up * 0.1f;
                Vector3 look = pos - head;
                if (look.sqrMagnitude < 1e-4f)
                {
                    look = Vector3.forward;
                }

                transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look, Vector3.up));
                return;
            }

            if (cam == null)
            {
                return;
            }

            var rot = cam.transform.rotation;
            transform.SetPositionAndRotation(head + rot * new Vector3(-0.2f, -0.13f, 0.45f), rot * Quaternion.Euler(0f, 12f, 0f));
        }
    }
}
