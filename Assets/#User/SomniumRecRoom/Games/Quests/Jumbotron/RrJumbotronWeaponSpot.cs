using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// One of Rec Room's laser weapons where the level lays it: on the Armory's wall pegs, on a crate, a console or the
    /// floor ((Quest_SciFi_Pistol) / _AutomaticGun / _Shotgun / _RailGun weapon_pickup markers, 1-8 per room). The
    /// weapon is the install's prefab given our behaviour exactly as a quest rack does (<see cref="RrQuestRack.Arm"/>:
    /// RrWeapon from its RangedWeapon data - magazines 5 / 22 / 3 / 3, rr_latest_rgames_story.md 9.2). It rests still
    /// (kinematic) at Rec Room's pose until someone takes it, and a new one pops in 3 s later (Rec Room's weapons
    /// respawn at their spawners) with Rec Room's spawn puff. Unlike <see cref="RrQuestRack"/> there is no wooden
    /// stand: the level's own table, shelf or wall is what it rests on. Weapons are local to each client, as on racks.
    /// </summary>
    public sealed class RrJumbotronWeaponSpot : MonoBehaviour
    {
        public float RefillSeconds = 3f;
        public string Prefab;

        private RrInstance _current;
        private bool _loading;
        private float _refillAt = -1f;
        private bool _pop;
        private readonly List<GameObject> _taken = new List<GameObject>();

        public static RrJumbotronWeaponSpot Create(Transform parent, string prefab, Vector3 local, Quaternion rotation)
        {
            var go = RrBuild.Group(parent, "Weapon " + prefab, local, rotation);
            var s = go.AddComponent<RrJumbotronWeaponSpot>();
            s.Prefab = prefab;
            return s;
        }

        private void Start()
        {
            Fill();
        }

        /// <summary>A new run: the weapons lying around go and the spot is filled again.</summary>
        public void ResetSpot()
        {
            foreach (var go in _taken)
            {
                if (go != null)
                {
                    var g = go.GetComponent<RrGrabbable>();
                    if (g == null || !g.IsHeld)
                    {
                        Destroy(go);
                    }
                }
            }

            _taken.Clear();
            if (_current == null && !_loading)
            {
                Fill();
            }
        }

        private void Update()
        {
            if (_current != null)
            {
                var g = _current.GetComponent<RrGrabbable>();
                if ((g != null && g.IsHeld) || (_current.transform.position - transform.position).sqrMagnitude > 0.36f)
                {
                    Taken();
                }
            }
            else if (!_loading && _refillAt > 0f && Time.time >= _refillAt)
            {
                _refillAt = -1f;
                _pop = true;
                Fill();
            }
        }

        private void Taken()
        {
            var inst = _current;
            _current = null;
            _refillAt = Time.time + RefillSeconds;
            _taken.Add(inst.gameObject);
            var g = inst.GetComponent<RrGrabbable>();
            if (g != null)
            {
                g.OnReleased += (hand, velocity) => StartCoroutine(Loosen(inst));
            }
            else
            {
                StartCoroutine(Loosen(inst));
            }

            // At most 3 old weapons per spot lie around (the oldest goes).
            if (_taken.Count > 3)
            {
                var old = _taken[0];
                _taken.RemoveAt(0);
                var og = old != null ? old.GetComponent<RrGrabbable>() : null;
                if (old != null && (og == null || !og.IsHeld))
                {
                    Destroy(old);
                }
            }
        }

        private IEnumerator Loosen(RrInstance inst)
        {
            yield return null;
            if (inst == null)
            {
                yield break;
            }

            var g = inst.GetComponent<RrGrabbable>();
            if (g != null && g.IsHeld)
            {
                yield break;
            }

            var rb = inst.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.WakeUp();
            }
        }

        private void Fill()
        {
            _loading = true;
            RrSpawn.Spawn(Prefab, transform, Vector3.zero, Quaternion.identity, inst =>
            {
                _loading = false;
                if (inst == null)
                {
                    RrLog.WarnOnce("Jumbotron", "spot:" + Prefab, "could not spawn " + Prefab + " for a weapon spot");
                    return;
                }

                if (this == null)
                {
                    Destroy(inst.gameObject);
                    return;
                }

                RrQuestRack.Arm(inst, Prefab);
                var rb = inst.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = inst.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 1f;
                }

                rb.isKinematic = true;
                _current = inst;
                if (_pop)
                {
                    _pop = false;
                    Vector3 at = inst.transform.position;
                    RrFx.Spawn("[VFX_ObjectSpawn]", at, Quaternion.identity);
                    RrSound.Play(RrAnimateInOut.SpawnSounds[Random.Range(0, RrAnimateInOut.SpawnSounds.Length)], at, 0.7f);
                }
            }, RrSpawnOptions.Prop());
        }
    }
}
