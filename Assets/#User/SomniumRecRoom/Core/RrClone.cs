using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// Builds a copy of a Rec Room prefab from Unity's own components only, so the copy carries none of Rec
    /// Room's script components.
    ///
    /// Why: instantiating a Rec Room prefab logs one "The referenced script ... is missing!" warning per Rec Room
    /// component - 190 to 290 per prop in the play-mode harness (2026-09-24), 573 for three props - and a room
    /// spawns dozens of props. In the client each warning is a Player.log line with a stack trace, and
    /// the lot arrives in the frame of the spawn. The components cannot be removed at run time (the call that
    /// strips missing scripts is editor-only), so they are simply not copied: a GameObject per node with the same
    /// name, pose, layer and active state, and copies of the meshes, skinned meshes (bones remapped), colliders,
    /// rigidbody, LOD groups, lights, audio sources and animators. Nodes whose behaviour Unity cannot copy
    /// through its API - particle systems, trail and line renderers - are instantiated as they are, with their
    /// subtree, in the same place; only their own few script components warn.
    ///
    /// Child order is the prefab's, so a node's child-index path (<see cref="Rr.RrNodeData.IndexPath"/>) finds it
    /// in the copy as it would in an instance.
    /// </summary>
    public static class RrClone
    {
        public static int ClonedNodes;
        public static int InstantiatedSubtrees;

        public static GameObject Clone(GameObject source, Transform parent)
        {
            var map = new Dictionary<Transform, Transform>();
            var lods = new List<KeyValuePair<LODGroup, Transform>>();
            var skins = new List<KeyValuePair<SkinnedMeshRenderer, Transform>>();
            var root = Node(source.transform, parent, map, lods, skins);

            foreach (var kv in skins)
            {
                var s = kv.Key;
                var n = kv.Value.gameObject.AddComponent<SkinnedMeshRenderer>();
                n.sharedMesh = s.sharedMesh;
                n.sharedMaterials = s.sharedMaterials;
                var bones = s.bones;
                var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform b;
                    mapped[i] = bones[i] != null && map.TryGetValue(bones[i], out b) ? b : null;
                }

                n.bones = mapped;
                Transform rb;
                n.rootBone = s.rootBone != null && map.TryGetValue(s.rootBone, out rb) ? rb : null;
                n.localBounds = s.localBounds;
                n.updateWhenOffscreen = s.updateWhenOffscreen;
                n.quality = s.quality;
                n.shadowCastingMode = s.shadowCastingMode;
                n.receiveShadows = s.receiveShadows;
                if (s.sharedMesh != null)
                {
                    for (int i = 0; i < s.sharedMesh.blendShapeCount; i++)
                    {
                        n.SetBlendShapeWeight(i, s.GetBlendShapeWeight(i));
                    }
                }

                n.enabled = s.enabled;
            }

            foreach (var kv in lods)
            {
                var s = kv.Key;
                var n = kv.Value.gameObject.AddComponent<LODGroup>();
                var src = s.GetLODs();
                var dst = new LOD[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var rs = new List<Renderer>();
                    foreach (var r in src[i].renderers)
                    {
                        Transform t;
                        if (r != null && map.TryGetValue(r.transform, out t))
                        {
                            var copy = t.GetComponent(r.GetType()) as Renderer;
                            if (copy != null)
                            {
                                rs.Add(copy);
                            }
                        }
                    }

                    dst[i] = new LOD(src[i].screenRelativeTransitionHeight, rs.ToArray()) { fadeTransitionWidth = src[i].fadeTransitionWidth };
                }

                n.fadeMode = s.fadeMode;
                n.animateCrossFading = s.animateCrossFading;
                n.localReferencePoint = s.localReferencePoint;
                n.size = s.size;
                n.SetLODs(dst);
                n.enabled = s.enabled;
            }

            return root.gameObject;
        }

        private static bool NeedsInstantiate(Transform s)
        {
            return s.GetComponent<ParticleSystem>() != null || s.GetComponent<TrailRenderer>() != null || s.GetComponent<LineRenderer>() != null
                || s.GetComponent<Cloth>() != null || s is RectTransform;
        }

        private static Transform Node(Transform s, Transform parent, Dictionary<Transform, Transform> map, List<KeyValuePair<LODGroup, Transform>> lods, List<KeyValuePair<SkinnedMeshRenderer, Transform>> skins)
        {
            if (parent != null && NeedsInstantiate(s))
            {
                var copy = Object.Instantiate(s.gameObject, parent, false);
                copy.name = s.name;
                InstantiatedSubtrees++;
                MapSubtree(s, copy.transform, map);
                return copy.transform;
            }

            var go = new GameObject(s.name);
            go.layer = s.gameObject.layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = s.localPosition;
            t.localRotation = s.localRotation;
            t.localScale = s.localScale;
            map[s] = t;
            ClonedNodes++;

            var mf = s.GetComponent<MeshFilter>();
            var mr = s.GetComponent<MeshRenderer>();
            if (mf != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            }

            if (mr != null)
            {
                var n = go.AddComponent<MeshRenderer>();
                n.sharedMaterials = mr.sharedMaterials;
                n.shadowCastingMode = mr.shadowCastingMode;
                n.receiveShadows = mr.receiveShadows;
                n.enabled = mr.enabled;
            }

            var smr = s.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                skins.Add(new KeyValuePair<SkinnedMeshRenderer, Transform>(smr, t));
            }

            var lod = s.GetComponent<LODGroup>();
            if (lod != null)
            {
                lods.Add(new KeyValuePair<LODGroup, Transform>(lod, t));
            }

            foreach (var c in s.GetComponents<Collider>())
            {
                CopyCollider(c, go);
            }

            var rb = s.GetComponent<Rigidbody>();
            if (rb != null)
            {
                var n = go.AddComponent<Rigidbody>();
                n.mass = rb.mass;
                n.linearDamping = rb.linearDamping;
                n.angularDamping = rb.angularDamping;
                n.useGravity = rb.useGravity;
                n.isKinematic = rb.isKinematic;
                n.interpolation = rb.interpolation;
                n.collisionDetectionMode = rb.collisionDetectionMode;
                n.constraints = rb.constraints;
                n.maxAngularVelocity = rb.maxAngularVelocity;
            }

            var light = s.GetComponent<Light>();
            if (light != null)
            {
                var n = go.AddComponent<Light>();
                n.type = light.type;
                n.color = light.color;
                n.intensity = light.intensity;
                n.range = light.range;
                n.spotAngle = light.spotAngle;
                n.innerSpotAngle = light.innerSpotAngle;
                n.shadows = LightShadows.None;
                n.cookie = light.cookie;
                n.enabled = light.enabled;
            }

            var audio = s.GetComponent<AudioSource>();
            if (audio != null)
            {
                var n = go.AddComponent<AudioSource>();
                n.clip = audio.clip;
                n.volume = audio.volume;
                n.pitch = audio.pitch;
                n.loop = audio.loop;
                n.playOnAwake = audio.playOnAwake;
                n.spatialBlend = audio.spatialBlend;
                n.minDistance = audio.minDistance;
                n.maxDistance = audio.maxDistance;
                n.rolloffMode = audio.rolloffMode;
                n.dopplerLevel = 0f;
                n.priority = audio.priority;
                n.enabled = audio.enabled;
            }

            var animator = s.GetComponent<Animator>();
            if (animator != null)
            {
                var n = go.AddComponent<Animator>();
                n.runtimeAnimatorController = animator.runtimeAnimatorController;
                n.avatar = animator.avatar;
                n.applyRootMotion = false;
                n.cullingMode = animator.cullingMode;
                n.updateMode = animator.updateMode;
                n.enabled = animator.enabled;
            }

            for (int i = 0; i < s.childCount; i++)
            {
                Node(s.GetChild(i), t, map, lods, skins);
            }

            go.SetActive(s.gameObject.activeSelf);
            return t;
        }

        private static void MapSubtree(Transform source, Transform copy, Dictionary<Transform, Transform> map)
        {
            map[source] = copy;
            int n = Mathf.Min(source.childCount, copy.childCount);
            for (int i = 0; i < n; i++)
            {
                MapSubtree(source.GetChild(i), copy.GetChild(i), map);
            }
        }

        private static void CopyCollider(Collider c, GameObject go)
        {
            Collider n = null;
            var bc = c as BoxCollider;
            var sc = c as SphereCollider;
            var cc = c as CapsuleCollider;
            var mc = c as MeshCollider;
            if (bc != null)
            {
                var x = go.AddComponent<BoxCollider>();
                x.center = bc.center;
                x.size = bc.size;
                n = x;
            }
            else if (sc != null)
            {
                var x = go.AddComponent<SphereCollider>();
                x.center = sc.center;
                x.radius = sc.radius;
                n = x;
            }
            else if (cc != null)
            {
                var x = go.AddComponent<CapsuleCollider>();
                x.center = cc.center;
                x.radius = cc.radius;
                x.height = cc.height;
                x.direction = cc.direction;
                n = x;
            }
            else if (mc != null && mc.sharedMesh != null)
            {
                var x = go.AddComponent<MeshCollider>();
                x.convex = mc.convex;
                x.sharedMesh = mc.sharedMesh;
                n = x;
            }

            if (n != null)
            {
                n.isTrigger = c.isTrigger;
                n.sharedMaterial = c.sharedMaterial;
                n.enabled = c.enabled;
            }
        }
    }
}
