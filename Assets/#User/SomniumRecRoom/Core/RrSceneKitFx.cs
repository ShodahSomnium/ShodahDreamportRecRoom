using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Rec Room's level effects of one scene (the "F" records, iteration 5): places them once the room around them is
    /// built, and keeps only the nearest ones running for VR.
    ///
    /// <b>Why placement waits.</b> The kit builds first, and the room's own code then builds what the reference scenes
    /// lack (the Golden Trophy acts' walls and floors are rebuilt by RrGoldenTrophyShell after the kit returns). Rec
    /// Room's wall torches hang on walls that are not always rebuilt in the same place: in iteration 4 the area code
    /// found a rebuilt wall within 1.2 m for only 8 of 80 torches. A flame that floated where Rec Room's wall was would
    /// look wrong, so a wall-mounted effect (mount=wall) is placed only when a ray from 1 m in front of the flame,
    /// cast along the torch's back (back=), hits a wall the room draws (a collider with its own renderer: the
    /// rebuilt shell, not Rec Room's invisible collision boxes) within 1.2 m behind Rec Room's flame; it then
    /// goes onto that wall at Rec Room's own 0.29 m flame offset (off=). Iteration 8: the quests now stand in Rec Room's own
    /// Android rooms, drawn as plates over the level's own collision boxes, so a collision face exactly where Rec Room's
    /// wall was (off= behind the flame, 12 cm tolerance) also counts as the wall. The component therefore places its effects on
    /// the first frame it is active after travel has finished building (<c>Rooms.RrTravel.Busy</c> false), or 30 s
    /// after the build at the latest. An act that stays inactive places nothing until it is entered.
    ///
    /// <b>Templates.</b> Each install effect (a prefab root, or a child of one, part=) is made once by the kit as a
    /// clean copy (<see cref="RrSpawn"/>: no colliders, rigidbodies, lights or sounds of its own) and cloned per record.
    /// A stand-in chosen by kind (fit=kind) takes the scene system's own numbers: start size, lifetime, speed and
    /// rate as ratios on every system of the effect, max particles, and the emitter shape (a torch's 0.057 m circle,
    /// act 4's 13.9 m lava disc), with scaling set to Local so the numbers are in metres.
    ///
    /// <b>VR.</b> At most <see cref="RrSceneKitOptions.MaxEffects"/> effects of the scene are active and at most
    /// <see cref="RrSceneKitOptions.MaxEffectSounds"/> of their loops play, the ones nearest the local head, checked
    /// every 0.5 s; each system keeps at most <see cref="RrSceneKitOptions.MaxEffectParticles"/> particles. Loops use
    /// a linear roll-off (a logarithmic one never reaches silence at its max distance, and 14 torches would add up).
    /// </summary>
    public sealed class RrSceneKitFx : MonoBehaviour
    {
        internal RrSceneKitResult Result;
        internal RrSceneData Data;
        internal RrSceneKitOptions Options;
        internal readonly List<int> Pending = new List<int>();
        internal readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();

        /// <summary>Each template's rotation inside its prefab (keeprot=1 records keep it under the scene pose).</summary>
        internal readonly Dictionary<string, Quaternion> TemplateRotations = new Dictionary<string, Quaternion>();
        internal float BuiltAt;

        private sealed class Live
        {
            public GameObject Go;
            public AudioSource Sound;
            public Transform SoundAnchor;
            public bool On = true;
            public bool SoundOn = true;
            public float D2;
        }

        private readonly List<Live> _live = new List<Live>();
        private readonly List<Live> _order = new List<Live>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private int _state; // 0 waiting, 1 placing, 2 placed
        private int _framesSinceBuild;
        private float _nextCull;

        /// <summary>Effects placed / left out (no wall) / without a template / sounds started.</summary>
        public int Placed, NoWall, Missing, Sounds, Moved;

        /// <summary>Placed wall effects: the flame's world position and the wall's normal (into the room), for harness shots.</summary>
        public readonly List<Ray> OnWalls = new List<Ray>();

        public static string Key(string prefab, string part)
        {
            return string.IsNullOrEmpty(part) ? prefab : prefab + "|" + part;
        }

        private void Update()
        {
            if (_state == 0)
            {
                _framesSinceBuild++;
                bool busy = Rooms.RrTravel.Busy;
                if (_framesSinceBuild > 2 && (!busy || Time.realtimeSinceStartup - BuiltAt > 30f))
                {
                    _state = 1;
                    StartCoroutine(Place());
                }

                return;
            }

            if (_state == 2 && Time.unscaledTime >= _nextCull)
            {
                _nextCull = Time.unscaledTime + 0.5f;
                Cull();
            }
        }

        private void OnEnable()
        {
            // Re-entering an act: everything that was running comes back, then the next cull trims it.
            _nextCull = 0f;
            foreach (var l in _live)
            {
                l.On = false;
                l.SoundOn = false;
            }
        }

        private IEnumerator Place()
        {
            var sw = Stopwatch.StartNew();
            Physics.SyncTransforms();
            var kinds = new Dictionary<string, int>();
            var sounds = new StringBuilder();
            int soundsListed = 0;
            int max = Mathf.Max(0, Options.MaxEffectsBuilt);
            foreach (int index in Pending)
            {
                if (sw.Elapsed.TotalMilliseconds > Options.FrameBudgetMs)
                {
                    yield return null;
                    sw.Reset();
                    sw.Start();
                    Physics.SyncTransforms();
                }

                if (Placed >= max)
                {
                    break;
                }

                var e = Data.Effects[index];
                GameObject template;
                string key = Key(e.Prefab, e.Get("part"));
                if (!Templates.TryGetValue(key, out template) || template == null)
                {
                    Missing++;
                    continue;
                }

                if (e.Get("card") == "1" && !Options.FlameCards)
                {
                    continue;
                }

                Vector3 local = e.Position;
                if (e.Get("mount") == "wall" && !OnWall(e, ref local))
                {
                    NoWall++;
                    continue;
                }

                if (e.Get("kind") == "lava")
                {
                    // Act 4's [VFX_Lava] sits 7 cm under the lava top (scene y -4.47 against the pool at -4.40); Rec Room's own
                    // 2 m particles cleared it, the install bubbles (0.3 m camera-facing sprites) would not.
                    local.y += 0.15f;
                }

                var go = Object.Instantiate(template, transform, false);
                go.name = (e.Get("src") ?? template.name) + " (" + template.name + ")";
                go.transform.localPosition = local;
                Quaternion own;
                go.transform.localRotation = e.Get("keeprot") == "1" && TemplateRotations.TryGetValue(key, out own) ? e.Rotation * own : e.Rotation;
                bool fit = e.Get("fit") == "kind";
                go.transform.localScale = fit ? Vector3.one : Vector3.one * (e.Scale > 0f ? e.Scale : 1f);
                Configure(go, e, fit);
                FitHeight(go, e.Number("fit.height", -1f));
                go.SetActive(true);
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Play(false);
                }

                var live = new Live { Go = go };
                string clip = e.Get("snd");
                if (!string.IsNullOrEmpty(clip) && !Options.SkipAudio)
                {
                    var anchor = new GameObject("Sound " + clip).transform;
                    anchor.SetParent(transform, false);
                    anchor.localPosition = local;
                    float maxD = Mathf.Max(2f, e.Number("snd.max", 20f));
                    var source = RrSound.Loop(clip, anchor, e.Number("snd.vol", 0.3f) * Options.AudioVolumeScale, maxD);
                    if (source != null)
                    {
                        source.minDistance = Mathf.Clamp(e.Number("snd.min", 1f), 0.1f, maxD - 0.5f);
                        source.rolloffMode = AudioRolloffMode.Linear;
                        live.Sound = source;
                        live.SoundAnchor = anchor;
                        Sounds++;
                        if (soundsListed++ < 6)
                        {
                            var w = anchor.position;
                            sounds.Append(soundsListed > 1 ? ", " : "").Append(clip).Append(" at (").Append(w.x.ToString("0.0", CultureInfo.InvariantCulture)).Append(", ")
                                  .Append(w.y.ToString("0.0", CultureInfo.InvariantCulture)).Append(", ").Append(w.z.ToString("0.0", CultureInfo.InvariantCulture)).Append(')');
                        }
                    }
                }

                _live.Add(live);
                Placed++;
                int n;
                kinds.TryGetValue(key, out n);
                kinds[key] = n + 1;
            }

            _state = 2;
            Cull();
            if (Result != null)
            {
                Result.EffectsPlaced = Placed;
                Result.EffectsNoWall = NoWall;
                Result.EffectSounds = Sounds;
            }

            var sb = new StringBuilder();
            sb.Append(Result != null ? Result.Scene : name).Append(": effects placed ").Append(Placed).Append(" of ").Append(Pending.Count);
            if (Moved > 0 || NoWall > 0)
            {
                sb.Append(" (wall mounts: ").Append(Moved).Append(" on a rebuilt wall, ").Append(NoWall).Append(" left out with no wall within 1.2 m)");
            }

            if (Missing > 0)
            {
                sb.Append(", ").Append(Missing).Append(" without an install template");
            }

            foreach (var kv in kinds)
            {
                sb.Append("; ").Append(kv.Key).Append(" x").Append(kv.Value);
            }

            sb.Append("; sounds ").Append(Sounds);
            if (Sounds > 0)
            {
                sb.Append(": ").Append(sounds).Append(Sounds > soundsListed ? ", ..." : "");
            }

            sb.Append("; nearest ").Append(Options.MaxEffects).Append(" effects / ").Append(Options.MaxEffectSounds).Append(" sounds run");
            RrLog.Info(RrSceneKit.Area, sb.ToString());
        }

        // A wall-mounted effect: find the wall behind it (environment colliders), else leave it out.
        private bool OnWall(RrSceneData.EffectRecord e, ref Vector3 local)
        {
            var back = e.Vector("back", Vector3.zero);
            back.y = 0f;
            if (back.sqrMagnitude < 0.25f)
            {
                return false;
            }

            back.Normalize();
            var t = transform;
            Vector3 flame = t.TransformPoint(e.Position);
            Vector3 dir = t.TransformDirection(back).normalized;
            float off = Mathf.Clamp(e.Number("off", 0.29f), 0f, 1f);
            // From 1 m into the room, along the torch's back, to 1.2 m behind Rec Room's flame. Only a wall the room
            // draws counts: the kit builds Rec Room's own collision boxes too (collider-only objects), and where the
            // room's shell was not rebuilt on the same line those stand invisible (harness run 1: every lobby flame
            // "found a wall", six of them in the open floor round the spawn). A shell wall is a box with its renderer
            // on the collider's object.
            int n = Physics.RaycastNonAlloc(flame - dir, dir, Hits, 2.2f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore);
            int best = -1, exact = -1;
            for (int i = 0; i < n; i++)
            {
                var r = Hits[i].collider != null ? Hits[i].collider.GetComponent<MeshRenderer>() : null;
                if (r != null && r.enabled && (best < 0 || Hits[i].distance < Hits[best].distance))
                {
                    best = i;
                }

                // Iteration 8: the quest areas are now Rec Room's own Android rooms, whose walls are drawn as separate
                // plates over the level's own collision boxes (collider-only objects). A collision face exactly where Rec
                // Room's wall was (the flame "off" metres in front of it, 12 cm tolerance) is that wall.
                if (Hits[i].collider != null && Mathf.Abs(Hits[i].distance - (1f + off)) < 0.12f && (exact < 0 || Hits[i].distance < Hits[exact].distance))
                {
                    exact = i;
                }
            }

            if (best < 0)
            {
                best = exact;
            }

            if (best < 0)
            {
                return false;
            }

            var hit = Hits[best];
            Vector3 at = hit.point - dir * off;
            at.y = flame.y;
            OnWalls.Add(new Ray(at, hit.normal));
            local = t.InverseTransformPoint(at);
            Moved++;
            return true;
        }

        // fit.height=: scale the copy so its drawn meshes (not its particles) are that tall in the world (the candle's
        // flame card on a wall torch), measured from the mesh bounds through the transforms.
        private static void FitHeight(GameObject go, float height)
        {
            if (height <= 0f)
            {
                return;
            }

            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    continue;
                }

                // Mesh bounds through the transform (Renderer.bounds of an inactive object is empty).
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var w = r.transform.TransformPoint(c);
                    if (!any)
                    {
                        b = new Bounds(w, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(w);
                    }
                }
            }

            if (any && b.size.y > 1e-4f)
            {
                go.transform.localScale *= height / b.size.y;
            }
        }

        // Loop, particle caps and, for a stand-in, the scene system's own numbers (see the class summary).
        private void Configure(GameObject go, RrSceneData.EffectRecord e, bool fit)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
            {
                return;
            }

            var first = go.GetComponent<ParticleSystem>() ?? systems[0];
            var fm = first.main;
            float Ratio(float want, float have)
            {
                return want > 0f && have > 1e-4f ? want / have : 1f;
            }

            float size = fit ? Ratio(e.Number("ps.size", -1f), fm.startSize3D ? fm.startSizeXMultiplier : fm.startSize.constantMax) : 1f;
            float life = fit ? Ratio(e.Number("ps.life", -1f), fm.startLifetime.constantMax) : 1f;
            float rate = fit ? Ratio(e.Number("ps.rate", -1f), first.emission.rateOverTime.constantMax) : 1f;
            float speedHave = fm.startSpeed.constantMax;
            float speedWant = fit ? e.Number("ps.speed", -1f) : -1f;
            bool loop = e.Get("loop") == "1";
            int cap = Mathf.Max(8, Options.MaxEffectParticles);
            foreach (var ps in systems)
            {
                var m = ps.main;
                if (loop)
                {
                    m.loop = true;
                }

                if (fit)
                {
                    m.scalingMode = ParticleSystemScalingMode.Local;
                    if (m.startSize3D)
                    {
                        m.startSizeXMultiplier *= size;
                        m.startSizeYMultiplier *= size;
                        m.startSizeZMultiplier *= size;
                    }
                    else
                    {
                        m.startSizeMultiplier *= size;
                    }

                    m.startLifetimeMultiplier *= life;
                    if (speedWant >= 0f)
                    {
                        if (speedHave > 1e-3f)
                        {
                            m.startSpeedMultiplier *= speedWant / speedHave;
                        }
                        else if (ps == first)
                        {
                            m.startSpeed = speedWant;
                        }
                    }

                    var em = ps.emission;
                    em.rateOverTimeMultiplier *= rate;
                    if (ps == first)
                    {
                        float wantMax = e.Number("ps.max", -1f);
                        if (wantMax > 0f)
                        {
                            m.maxParticles = Mathf.RoundToInt(wantMax);
                        }
                    }
                    else if (rate > 1f)
                    {
                        m.maxParticles = Mathf.RoundToInt(m.maxParticles * rate);
                    }
                }

                m.maxParticles = Mathf.Min(m.maxParticles, cap);
            }

            if (!fit)
            {
                return;
            }

            // The scene's emitter shape goes on every system that emits from a shape of its own: the lava stand-in
            // (VFX_Lava_Bubbles01) is a root that draws nothing plus bubble systems, which must spread over the 13.9 m disc
            // too; a sub-emitter keeps emitting where its parent's particles are.
            int shape = Mathf.RoundToInt(e.Number("ps.shape", -1f));
            if (shape >= 0)
            {
                float radius = e.Number("ps.radius", -1f);
                var box = e.Vector("ps.box", Vector3.zero);
                foreach (var ps in systems)
                {
                    var sh = ps.shape;
                    if (ps != first && !sh.enabled)
                    {
                        continue;
                    }

                    sh.enabled = true;
                    sh.shapeType = (ParticleSystemShapeType)shape;
                    if (radius > 0f)
                    {
                        sh.radius = radius;
                    }

                    if (box.sqrMagnitude > 0f)
                    {
                        sh.scale = box;
                    }
                }
            }
        }

        // The nearest effects and loops to the local head run; the rest wait (count caps, not distance: a harness
        // camera far from the room must still see it).
        private void Cull()
        {
            if (_live.Count == 0)
            {
                return;
            }

            Vector3 head = Player.RrPlayer.HeadPosition;
            _order.Clear();
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var l = _live[i];
                if (l.Go == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                l.D2 = (l.Go.transform.position - head).sqrMagnitude;
                _order.Add(l);
            }

            _order.Sort((a, b) => a.D2.CompareTo(b.D2));
            int fx = 0, snd = 0;
            foreach (var l in _order)
            {
                bool on = fx++ < Options.MaxEffects;
                if (on != l.On)
                {
                    l.Go.SetActive(on);
                    l.On = on;
                    if (on)
                    {
                        foreach (var ps in l.Go.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ps.Play(false);
                        }
                    }
                }

                if (l.Sound == null)
                {
                    continue;
                }

                // A clip that finished loading after a cull starts itself (RrSound.Loop), so both ways are checked each time.
                bool play = snd++ < Options.MaxEffectSounds;
                if (play)
                {
                    if (l.Sound.clip != null && !l.Sound.isPlaying)
                    {
                        l.Sound.Play();
                    }
                }
                else if (l.Sound.isPlaying)
                {
                    l.Sound.Stop();
                }

                l.SoundOn = play;
            }
        }
    }
}
