using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player.Avatars
{
    /// <summary>
    /// Hides a player's Somnium avatar while they wear the Rec Room avatar - and only that: renderers of the avatar model
    /// and of the cloth items skinned to it, with <c>Renderer.forceRenderingOff</c>, the switch the client never writes
    /// (RrIsolation's notes). Nothing is destroyed, re-parented, re-materialled, rescaled or re-targeted; the name tag,
    /// voice and colliders stay as they are. The user asked for this on 2026-10-17 as an opt-in (the project rule
    /// "never modify the Somnium avatar" otherwise stands).
    ///
    /// <b>Finding the avatar</b> (client facts measured for RrIsolation, phase1/isolate.md, hands_frames.md 1.1): the
    /// model is a scene root named "Avatar for Player [nick] [longId]" (AvatarManager.cs:105) - the local one in
    /// MasterScene with its Main/Mirror/Teleport copies, remote ones in the active scene with their Main/Dummy/Culling
    /// holders; worn cloth items are scene roots "Original Item: [..]" (the local player's) or "Simulator Item: [..]"
    /// (others') whose skinned meshes use the avatar's bones (InventoryItemController.cs:73). Matched by the Bridge
    /// nickname; when two roots share it, the one whose meshes are nearest the player's head wins. Re-searched once a
    /// second (an avatar or outfit change replaces the model), renderers re-applied every frame.
    ///
    /// <b>Exact restore</b>: only renderers this class switched off are recorded and only those are switched back on.
    /// Cooperation with <see cref="RrIsolation"/> (which hides everything of players in other places with the same
    /// switch and the same "only what I flipped" rule): a renderer isolation already hid is left to it; one it shows
    /// again while the player still wears the Rec Room avatar is hidden again in the same frame (this runs after it);
    /// and when the player stops wearing it while isolation hides them, the restore waits until isolation has shown
    /// them, so a player in another place never flashes into view.
    /// </summary>
    internal static class RrAvatarHide
    {
        public const string AvatarPrefix = "Avatar for Player [";
        public const string RemoteItemPrefix = "Simulator Item:";
        public const string LocalItemPrefix = "Original Item:";
        public const float RescanInterval = 1f;

        private sealed class Target
        {
            public int Id;
            public bool Local;
            public bool Want;
            public string Nick;
            public Func<Vector3?> Near;
            public Transform Avatar;
            public float NextScan;
            public int Misses;
            public bool LoggedMiss;
            public readonly List<Renderer> Cached = new List<Renderer>();
            public readonly List<Renderer> Ours = new List<Renderer>();
            public readonly HashSet<Renderer> OursSet = new HashSet<Renderer>();
        }

        private static readonly Dictionary<int, Target> Targets = new Dictionary<int, Target>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly List<KeyValuePair<string, Transform>> AvatarRoots = new List<KeyValuePair<string, Transform>>();
        private static readonly List<Transform> ItemRoots = new List<Transform>();
        private static readonly List<GameObject> ScratchRoots = new List<GameObject>();
        private static readonly List<Renderer> ScratchRenderers = new List<Renderer>();
        private static readonly List<SkinnedMeshRenderer> ScratchSkinned = new List<SkinnedMeshRenderer>();
        private static int _rootsFrame = -1;
        private const int LocalKey = int.MinValue;

        public static int HiddenRenderers
        {
            get
            {
                int n = 0;
                foreach (var t in Targets.Values)
                {
                    n += t.Ours.Count;
                }

                return n;
            }
        }

        /// <summary>Wants a player's Somnium avatar hidden (true) or back (false). Local: id ignored.</summary>
        public static void Set(int playerId, bool local, bool hide, string nick, Func<Vector3?> near)
        {
            int key = local ? LocalKey : playerId;
            Target t;
            if (!Targets.TryGetValue(key, out t))
            {
                if (!hide)
                {
                    return;
                }

                t = new Target { Id = playerId, Local = local };
                Targets[key] = t;
            }

            if (hide && !t.Want)
            {
                t.NextScan = 0f;
                RrLog.Info("Avatar", (local ? "your" : "player " + playerId + "'s") + " Somnium avatar is hidden while the Rec Room avatar is worn");
            }

            t.Want = hide;
            t.Id = playerId;
            if (nick != t.Nick)
            {
                t.Nick = nick;
                t.NextScan = 0f;
            }

            t.Near = near;
        }

        /// <summary>Every frame after the isolation (LateUpdate order above 10000).</summary>
        public static void Tick()
        {
            float now = Time.unscaledTime;
            Gone.Clear();
            foreach (var kv in Targets)
            {
                var t = kv.Value;
                if (t.Want)
                {
                    // The model was replaced (avatar or outfit change on the client): find the new one at once.
                    if (t.Cached.Count > 0 && t.Avatar == null)
                    {
                        t.NextScan = 0f;
                    }

                    if (now >= t.NextScan)
                    {
                        t.NextScan = now + RescanInterval;
                        Scan(t);
                    }

                    Enforce(t);
                    continue;
                }

                // Not wanted: give back what we hid - unless isolation is hiding this player right now (it will show
                // its own renderers first; ours follow in the frame it does).
                if (!t.Local && RrIsolation.IsHidden(t.Id))
                {
                    continue;
                }

                Restore(t);
                Gone.Add(kv.Key);
            }

            for (int i = 0; i < Gone.Count; i++)
            {
                Targets.Remove(Gone[i]);
            }
        }

        /// <summary>A player left the world: forget them (their objects are being destroyed with them).</summary>
        public static void Forget(int playerId)
        {
            Target t;
            if (Targets.TryGetValue(playerId, out t))
            {
                Restore(t);
                Targets.Remove(playerId);
            }
        }

        private static void Enforce(Target t)
        {
            for (int i = 0; i < t.Cached.Count; i++)
            {
                Renderer r = t.Cached[i];
                if (r != null && !r.forceRenderingOff)
                {
                    r.forceRenderingOff = true;
                    if (t.OursSet.Add(r))
                    {
                        t.Ours.Add(r);
                    }
                }
            }
        }

        private static void Restore(Target t)
        {
            int back = 0;
            for (int i = 0; i < t.Ours.Count; i++)
            {
                Renderer r = t.Ours[i];
                if (r != null)
                {
                    r.forceRenderingOff = false;
                    back++;
                }
            }

            if (t.Ours.Count > 0)
            {
                RrLog.Info("Avatar", (t.Local ? "your" : "player " + t.Id + "'s") + " Somnium avatar is shown again (" + back + " renderer(s) restored)");
            }

            t.Ours.Clear();
            t.OursSet.Clear();
            t.Cached.Clear();
            t.Avatar = null;
        }

        /// <summary>Puts everything back (world shutdown). Isolation restores its own first (RrMain.OnDestroy order).</summary>
        public static void RestoreAll()
        {
            foreach (var t in Targets.Values)
            {
                Restore(t);
            }

            Targets.Clear();
            AvatarRoots.Clear();
            ItemRoots.Clear();
            _rootsFrame = -1;
        }

        // ------------------------------------------------------------------ finding

        private static void CollectRoots()
        {
            if (_rootsFrame == Time.frameCount)
            {
                return;
            }

            _rootsFrame = Time.frameCount;
            AvatarRoots.Clear();
            ItemRoots.Clear();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                ScratchRoots.Clear();
                scene.GetRootGameObjects(ScratchRoots);
                for (int i = 0; i < ScratchRoots.Count; i++)
                {
                    GameObject go = ScratchRoots[i];
                    if (go == null)
                    {
                        continue;
                    }

                    string n = go.name;
                    if (n.StartsWith(AvatarPrefix, StringComparison.Ordinal))
                    {
                        int end = n.LastIndexOf("] [", StringComparison.Ordinal);
                        if (end >= AvatarPrefix.Length)
                        {
                            AvatarRoots.Add(new KeyValuePair<string, Transform>(n.Substring(AvatarPrefix.Length, end - AvatarPrefix.Length), go.transform));
                        }
                    }
                    else if (n.StartsWith(RemoteItemPrefix, StringComparison.Ordinal) || n.StartsWith(LocalItemPrefix, StringComparison.Ordinal))
                    {
                        ItemRoots.Add(go.transform);
                    }
                }
            }

            ScratchRoots.Clear();
        }

        private static void Scan(Target t)
        {
            CollectRoots();
            Transform found = null;
            float best = float.MaxValue;
            int matches = 0;
            Vector3? near = t.Near != null ? t.Near() : null;
            if (!string.IsNullOrEmpty(t.Nick))
            {
                for (int i = 0; i < AvatarRoots.Count; i++)
                {
                    if (AvatarRoots[i].Key != t.Nick || AvatarRoots[i].Value == null || IsOurs(AvatarRoots[i].Value))
                    {
                        continue;
                    }

                    matches++;
                    float d = near.HasValue ? Distance(AvatarRoots[i].Value, near.Value) : 0f;
                    if (found == null || d < best)
                    {
                        best = d;
                        found = AvatarRoots[i].Value;
                    }
                }
            }

            // Two roots under one nickname and nothing to tell them apart by: hide none rather than the wrong one.
            if (matches > 1 && !near.HasValue)
            {
                found = null;
            }

            // The local player's model, when its name does not carry the Bridge nickname: the client spawns it into
            // MasterScene (AvatarSpawner.cs:31, remote ones go to the active scene); take the one whose meshes are within
            // 1 m of the local head.
            if (found == null && t.Local && near.HasValue)
            {
                for (int i = 0; i < AvatarRoots.Count; i++)
                {
                    Transform a = AvatarRoots[i].Value;
                    if (a == null || IsOurs(a) || a.gameObject.scene.name != "MasterScene")
                    {
                        continue;
                    }

                    float d = Distance(a, near.Value);
                    if (d < 1f && d < best)
                    {
                        best = d;
                        found = a;
                    }
                }
            }

            if (found == null)
            {
                t.Misses++;
                if (t.Misses == 5 && !t.LoggedMiss)
                {
                    t.LoggedMiss = true;
                    RrLog.Warn("Avatar", (t.Local ? "your" : "player " + t.Id + "'s") + " Somnium avatar was not found ('" + AvatarPrefix + t.Nick + "] [..]', " +
                                         AvatarRoots.Count + " avatar root(s) seen): it stays drawn under the Rec Room avatar");
                }
            }
            else
            {
                t.Misses = 0;
            }

            t.Avatar = found;
            t.Cached.Clear();
            if (found == null)
            {
                return;
            }

            found.GetComponentsInChildren(true, ScratchRenderers);
            t.Cached.AddRange(ScratchRenderers);
            ScratchRenderers.Clear();

            // Cloth items skinned to this avatar's bones (the local player's "Original Item"s, others' "Simulator Item"s).
            string prefix = t.Local ? LocalItemPrefix : RemoteItemPrefix;
            for (int i = 0; i < ItemRoots.Count; i++)
            {
                Transform item = ItemRoots[i];
                if (item == null || !item.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                item.GetComponentsInChildren(true, ScratchSkinned);
                bool mine = false;
                for (int k = 0; k < ScratchSkinned.Count && !mine; k++)
                {
                    var smr = ScratchSkinned[k];
                    if (smr == null)
                    {
                        continue;
                    }

                    Transform bone = smr.rootBone;
                    if (bone == null)
                    {
                        var bones = smr.bones;
                        bone = bones != null && bones.Length > 0 ? bones[0] : null;
                    }

                    mine = bone != null && bone.IsChildOf(found);
                }

                ScratchSkinned.Clear();
                if (mine)
                {
                    item.GetComponentsInChildren(true, ScratchRenderers);
                    t.Cached.AddRange(ScratchRenderers);
                    ScratchRenderers.Clear();
                }
            }
        }

        /// <summary>Never our own world (a name collision cannot make us hide our own objects).</summary>
        private static bool IsOurs(Transform t)
        {
            var main = RrMain.Instance;
            return main != null && main.WorldRoot != null && t.IsChildOf(main.WorldRoot);
        }

        private static float Distance(Transform avatar, Vector3 head)
        {
            avatar.GetComponentsInChildren(false, ScratchSkinned);
            float best = float.MaxValue;
            for (int i = 0; i < ScratchSkinned.Count; i++)
            {
                var smr = ScratchSkinned[i];
                if (smr == null)
                {
                    continue;
                }

                float d = (smr.bounds.center - head).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                }
            }

            ScratchSkinned.Clear();
            return best == float.MaxValue ? (avatar.position - head).sqrMagnitude + 1e6f : best;
        }
    }
}
