using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Rec Room's BreakableObject (92 prefabs): bottles, glasses, lattes, potions, plates shatter when they hit
    /// something at <see cref="ShatterSpeed"/> m/s or more (2 on every instance; quest props shatter on any impact).
    /// The intact visual (<c>wholeObject</c>) is hidden and the pre-cut one (<c>fracturedObject</c>: a skinned mesh
    /// on a rig of chunk bones, each with a box collider) appears; the chunks get rigidbodies and fly apart with part
    /// of the impact velocity; a random smash clip (root beer: Quest_Pirate1_BottleShatter01/03/05, 05 listed three
    /// times, v0.5 pv0.2) and the smash particle ((Quest_Pirate1_Weapon_Sparks_VFX) on 31 of 92) play at the contact.
    /// After <c>brokenLifetime</c> (2 s consumables, 5 s quest props) the pieces go: Rec Room destroys the object;
    /// here an object with an <see cref="RrRespawnable"/> is mended and sent home instead (a dorm keeps its bottles),
    /// unless <see cref="DestroyWhenBroken"/>.
    ///
    /// Contacts with the Somnium hand boxes (layer 7) never break it: they sit where the hand releases a throw.
    /// </summary>
    public sealed class RrBreakable : MonoBehaviour
    {
        public float ShatterSpeed = 2f;
        public bool ShatterOnAnyImpact;
        public float BrokenLifetime = 2f;

        /// <summary>Share of the pre-impact velocity the chunks keep (GUESS; the config's advanced portion is 0.2).</summary>
        public float ChunkVelocityShare = 0.45f;

        public GameObject Whole;
        public GameObject Fractured;
        public List<RrClipInfo> SmashSounds = new List<RrClipInfo>();
        public RrTarget SmashFx;
        public string SmashFxName;

        /// <summary>Destroy after breaking even when respawnable (single-use props handed out by games).</summary>
        public bool DestroyWhenBroken;

        public bool IsBroken { get; private set; }

        /// <summary>Fired when it shatters (contact point).</summary>
        public event Action<Vector3> OnBroken;

        /// <summary>Fired when it is whole again (mended after <see cref="BrokenLifetime"/>).</summary>
        public event Action OnMended;

        private readonly List<Transform> _chunks = new List<Transform>();
        private readonly List<Vector3> _chunkPos = new List<Vector3>();
        private readonly List<Quaternion> _chunkRot = new List<Quaternion>();
        private readonly List<Rigidbody> _added = new List<Rigidbody>();
        private Rigidbody _body;
        private RrGrabbable _grabbable;
        private Vector3 _lastVelocity;
        private float _armedAt;
        private bool _wasKinematic;

        public void Setup(RrInstance inst, RrConfig c)
        {
            ShatterSpeed = c.Float("shatterSpeed", 2f);
            ShatterOnAnyImpact = c.Bool("shatterOnAnyImpact");
            BrokenLifetime = c.Float("brokenLifetime", 2f);
            var w = RrTools.NodeOf(inst, c, "wholeObject");
            var f = RrTools.NodeOf(inst, c, "fracturedObject");
            Whole = w != null ? w.gameObject : null;
            Fractured = f != null && f != transform ? f.gameObject : null;
            SmashSounds = c.Clips("smashSounds");
            SmashFx = c.Ref("smashParticlePrefab");
            if (!SmashFx.IsNull && !SmashFx.IsInPrefab)
            {
                // The one smash effect seen on consumables and quest props; popcorn has its own (not by name here).
                SmashFxName = "(Quest_Pirate1_Weapon_Sparks_VFX)";
            }

            CollectChunks();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _grabbable = GetComponent<RrGrabbable>();
            _armedAt = Time.time + 0.5f;
        }

        private void CollectChunks()
        {
            _chunks.Clear();
            _chunkPos.Clear();
            _chunkRot.Clear();
            if (Fractured == null)
            {
                return;
            }

            foreach (var c in Fractured.GetComponentsInChildren<Collider>(true))
            {
                var t = c.transform;
                if (!_chunks.Contains(t))
                {
                    _chunks.Add(t);
                    _chunkPos.Add(t.localPosition);
                    _chunkRot.Add(t.localRotation);
                }
            }
        }

        private void FixedUpdate()
        {
            if (_body != null && !_body.isKinematic)
            {
                _lastVelocity = _body.linearVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsBroken || Time.time < _armedAt || collision.collider == null || collision.collider.gameObject.layer == 7)
            {
                return;
            }

            if (_grabbable != null && _grabbable.IsHeld)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed >= ShatterSpeed || (ShatterOnAnyImpact && speed > 0.5f))
            {
                Vector3 p = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
                RrTools.Once("Breakable", "first", "first shatter: " + name + " at " + speed.ToString("0.0") + " m/s (shatterSpeed " + ShatterSpeed + ")");
                Break(p, _lastVelocity);
            }
        }

        /// <summary>Shatters now (also the test hook and what games call for a hit by a weapon).</summary>
        public void Break(Vector3 point, Vector3 velocity)
        {
            if (IsBroken)
            {
                return;
            }

            IsBroken = true;
            if (_grabbable != null)
            {
                _grabbable.ForceRelease();
                _grabbable.CanGrab = false;
            }

            if (_body != null)
            {
                _wasKinematic = _body.isKinematic;
                if (!_body.isKinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }

                _body.isKinematic = true;
            }

            if (Whole != null)
            {
                Whole.SetActive(false);
            }

            if (Fractured != null)
            {
                Fractured.SetActive(true);
                float mass = _body != null ? _body.mass : 1f;
                int n = Mathf.Max(1, _chunks.Count);
                for (int i = 0; i < _chunks.Count; i++)
                {
                    var t = _chunks[i];
                    if (t == null)
                    {
                        continue;
                    }

                    var col = t.GetComponent<Collider>();
                    if (col != null)
                    {
                        col.enabled = true;
                        col.isTrigger = false;
                        col.excludeLayers = 1 << 7;
                    }

                    var rb = t.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = t.gameObject.AddComponent<Rigidbody>();
                        _added.Add(rb);
                    }

                    rb.isKinematic = false;
                    rb.mass = Mathf.Max(0.02f, mass / n);
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    Vector3 away = (t.position - point).normalized;
                    rb.linearVelocity = velocity * ChunkVelocityShare + away * UnityEngine.Random.Range(0.4f, 1.2f) + Vector3.up * 0.5f;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;
                }
            }
            else if (Whole == null)
            {
                // No fractured visual: hide the renderers so the object reads as gone.
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    r.enabled = false;
                }
            }

            RrTools.PlayAny(SmashSounds, point, 1f, 40f);
            RrTools.SpawnFx(SmashFx, SmashFxName, point, Quaternion.identity);
            var drink = GetComponent<RrDrinkable>();
            if (drink != null)
            {
                drink.StopPouring();
            }

            if (OnBroken != null)
            {
                OnBroken(point);
            }

            StartCoroutine(AfterBreak());
        }

        private IEnumerator AfterBreak()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, BrokenLifetime));
            var respawn = GetComponent<RrRespawnable>();
            if (DestroyWhenBroken || respawn == null)
            {
                Destroy(gameObject);
                yield break;
            }

            // Home first (which mends it), then the pop in: no whole bottle flashing where it broke.
            respawn.Respawn(false);
            Mend();
        }

        /// <summary>Whole again: chunks back on the rig, rigidbodies removed, the object grabbable.</summary>
        public void Mend()
        {
            if (!IsBroken)
            {
                return;
            }

            for (int i = 0; i < _added.Count; i++)
            {
                if (_added[i] != null)
                {
                    Destroy(_added[i]);
                }
            }

            _added.Clear();
            for (int i = 0; i < _chunks.Count; i++)
            {
                if (_chunks[i] != null)
                {
                    _chunks[i].localPosition = _chunkPos[i];
                    _chunks[i].localRotation = _chunkRot[i];
                }
            }

            if (Fractured != null)
            {
                Fractured.SetActive(false);
            }

            if (Whole != null)
            {
                Whole.SetActive(true);
            }
            else if (Fractured == null)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    r.enabled = true;
                }
            }

            if (_body != null)
            {
                _body.isKinematic = _wasKinematic;
            }

            if (_grabbable != null)
            {
                _grabbable.CanGrab = true;
                _grabbable.RefreshLayers();
            }

            IsBroken = false;
            _armedAt = Time.time + 0.5f;
            if (OnMended != null)
            {
                OnMended();
            }
        }
    }
}
