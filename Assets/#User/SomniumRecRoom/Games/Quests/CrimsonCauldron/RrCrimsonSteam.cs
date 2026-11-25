using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// A Goblin2 steam trap ([Quest_SteamTrap] in the Mines and the Deep Mines; wiki: "the steam kills you"). Rec Room's
    /// trap vents into its OverlapVolume (1.2 x 2.51 x 2.28 m, the volume's world scale); its timing is not in the level
    /// data, so the jet runs a GUESS cycle: 2 s of steam every 6 s, each trap 1.3 s after the previous one so a gap
    /// always opens. While it vents, a local player whose body is in the volume is down (a one-hit down, as a goblin's
    /// hit). The cycle runs on the shared network clock, so every client sees the same jets; each client judges only its
    /// own player (as the framework's lava). The jet is drawn as a pale, flickering column.
    /// </summary>
    public sealed class RrCrimsonSteam : MonoBehaviour
    {
        public const float Period = 6f;
        public const float OnSeconds = 2f;
        public const float Stagger = 1.3f;

        private Vector3 _half;
        private float _offset;
        private Transform _jet;
        private bool _on;
        private float _guard;

        public static RrCrimsonSteam Create(Transform parent, Vector3 local, Quaternion rotation, Vector3 size, int index)
        {
            var go = RrBuild.Group(parent, "Steam Trap " + index, local, rotation);
            var s = go.AddComponent<RrCrimsonSteam>();
            s._half = size * 0.5f;
            s._offset = index * Stagger;
            var mat = RrLook.Glass(new Color(0.92f, 0.94f, 0.97f, 0.35f));
            var jet = RrBuild.Box(go.transform, "Jet", Vector3.zero, Quaternion.identity, size, mat, false);
            var r = jet.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s._jet = jet.transform;
            jet.SetActive(false);
            return s;
        }

        /// <summary>Whether the jet vents at network time <paramref name="t"/>.</summary>
        public bool VentsAt(double t)
        {
            double k = (t + _offset) % Period;
            return k >= 0 && k < OnSeconds;
        }

        private void Update()
        {
            bool on = VentsAt(RrNet.Now);
            if (on != _on)
            {
                _on = on;
                _jet.gameObject.SetActive(on);
            }

            if (!on)
            {
                return;
            }

            float f = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, _offset);
            _jet.localScale = new Vector3(f, 1f, f);
            var q = RrQuestRoom.Current;
            var h = RrHealth.Local;
            if (q == null || q.Phase != RrPhase.Playing || h == null || !h.InCombat || h.IsOut || h.Invincible || Time.time < _guard)
            {
                return;
            }

            Vector3 p = transform.InverseTransformPoint(RrPlayer.FeetPosition + Vector3.up * 0.9f);
            if (Mathf.Abs(p.x) <= _half.x + 0.2f && Mathf.Abs(p.y) <= _half.y + 0.2f && Mathf.Abs(p.z) <= _half.z + 0.2f)
            {
                _guard = Time.time + 2f;
                h.GoOut(RrCombat.NoPlayer, RrCombat.EnemyTool, RrPlayer.FeetPosition);
                q.Hud.Toast("The steam got you!", 2f);
                RrLog.Info("CrimsonCauldron", "local player caught by the steam at " + transform.position.ToString("F1"));
            }
        }
    }
}
