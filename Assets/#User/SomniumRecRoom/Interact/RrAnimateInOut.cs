using System;
using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's AnimateInOut (993 prefabs): the pop with which a prop appears and disappears. The data only holds
    /// the delays (animInStartOffset / animOutStartOffset 0.2 s on 842) and, on 5 prefabs, an effect
    /// ([VFX_ObjectSpawn]); the "rez" look itself is Rec Room code. Here, after the delay, the visual root grows from
    /// nothing to full size in 0.35 s with an overshoot (ease-out back) and one of sfx_ui_prop_spawn_01-04 plays;
    /// going out is the reverse. The visual root is boundsRendererRoot, else the child holding the renderers
    /// (VisualRoot), so the rigidbody's own colliders keep their size.
    /// </summary>
    public sealed class RrAnimateInOut : MonoBehaviour
    {
        public static readonly string[] SpawnSounds = { "sfx_ui_prop_spawn_01", "sfx_ui_prop_spawn_02", "sfx_ui_prop_spawn_03", "sfx_ui_prop_spawn_04" };

        public float InDelay = 0.2f;
        public float OutDelay = 0.2f;
        public float Duration = 0.35f;
        public bool SupportsIn = true;
        public bool SupportsOut = true;
        public Transform Visual;
        public RrTarget Effect;
        public float SoundVolume = 0.5f;

        public bool IsAnimating { get; private set; }

        private Vector3 _scale = Vector3.one;
        private Coroutine _running;

        public void Setup(RrInstance inst, RrConfig c)
        {
            InDelay = c.Float("animInStartOffset", 0.2f);
            OutDelay = c.Float("animOutStartOffset", 0.2f);
            SupportsIn = c.Bool("SupportsAnimateIn", true);
            SupportsOut = c.Bool("SupportsAnimateOut", true);
            Effect = c.Ref("effectPrefab");
            Visual = RrTools.NodeOf(inst, c, "boundsRendererRoot");
            if (Visual == null)
            {
                Visual = VisualRootOf(transform);
            }

            _scale = Visual.localScale;
        }

        private static Transform VisualRootOf(Transform root)
        {
            var named = root.Find("VisualRoot");
            if (named != null)
            {
                return named;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.GetComponentInChildren<Renderer>(true) != null && child.GetComponent<Collider>() == null)
                {
                    return child;
                }
            }

            return root;
        }

        /// <summary>Grows in (after the delay).</summary>
        public void AnimateIn(Action done = null)
        {
            if (!SupportsIn || !isActiveAndEnabled || Visual == null)
            {
                if (done != null)
                {
                    done();
                }

                return;
            }

            Run(true, done);
        }

        /// <summary>Shrinks away (after the delay), then calls <paramref name="done"/>.</summary>
        public void AnimateOut(Action done = null)
        {
            if (!SupportsOut || !isActiveAndEnabled || Visual == null)
            {
                if (done != null)
                {
                    done();
                }

                return;
            }

            Run(false, done);
        }

        private void Run(bool into, Action done)
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                Visual.localScale = _scale;
            }

            _running = StartCoroutine(Animate(into, done));
        }

        private IEnumerator Animate(bool into, Action done)
        {
            IsAnimating = true;
            if (into)
            {
                Visual.localScale = _scale * 0.001f;
            }

            float delay = into ? InDelay : OutDelay;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            RrSound.Play(SpawnSounds[UnityEngine.Random.Range(0, SpawnSounds.Length)], transform.position, SoundVolume);
            if (!Effect.IsNull)
            {
                RrTools.SpawnFx(Effect, "[VFX_ObjectSpawn]", transform.position, Quaternion.identity);
            }

            float t = 0f;
            while (t < Duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Duration);
                float s = into ? EaseOutBack(k) : 1f - EaseInBack(k);
                Visual.localScale = _scale * Mathf.Max(0.001f, s);
                yield return null;
            }

            Visual.localScale = into ? _scale : _scale * 0.001f;
            IsAnimating = false;
            _running = null;
            if (done != null)
            {
                done();
            }
        }

        /// <summary>Full size now (after an interrupted animation or a respawn done without one).</summary>
        public void Restore()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            IsAnimating = false;
            if (Visual != null)
            {
                Visual.localScale = _scale;
            }
        }

        private void OnDisable()
        {
            Restore();
        }

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static float EaseInBack(float k)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * k * k * k - c1 * k * k;
        }
    }
}
