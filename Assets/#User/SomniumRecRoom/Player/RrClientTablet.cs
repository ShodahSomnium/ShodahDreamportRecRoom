using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomniumSpace.Worlds.SomniumRecRoom.Player
{
    /// <summary>
    /// Whether the Somnium client's tablet is open, so gameplay input pauses while the player uses it (conventions §24
    /// item 27; Quake pauses on Tab). Found and read the way ETS does it (<c>EtsTabletLink.Discover/ReadTabletOpen</c>,
    /// proven in the client): the local tablet root is the transform carrying a <c>TabletControl</c> component (matched
    /// by type name - no client reference), else the first one named "Tablet.XR(" / "Tablet.NonXR("; remote players'
    /// tablets ("Simulator") and this world's own objects are skipped. Open = the root Animator's bool "IsOpen"
    /// (<c>TabletTransitionControl.cs:46/57</c>), else its "GrabCollider" child being active, else unknown (never
    /// reported open, so a detection failure cannot block the game for good).
    ///
    /// Cost: the search walks MasterScene first, then the other loaded scenes except this world's, at most 1,200
    /// transforms a frame (the ETS walk did a whole pass in one frame), and only tests components on objects whose name
    /// starts with "Tablet". A pass starts every 2 s for the first minute and every 10 s after, only while not found and
    /// only with a client (never in the editor). Found, a frame costs one Animator bool.
    /// </summary>
    internal static class RrClientTablet
    {
        private const int WalkBudget = 1200;
        private static readonly int IsOpenHash = Animator.StringToHash("IsOpen");

        internal static bool IsOpen { get; private set; }

        internal static bool Found
        {
            get { return _root != null; }
        }

        internal static string Name
        {
            get { return _root != null ? _root.name : null; }
        }

        private static Transform _root;
        private static Animator _animator;
        private static bool _animatorHasIsOpen;
        private static GameObject _grabCollider;
        private static float _startedAt = -1f;
        private static float _nextPassAt;
        private static float _recheckAt;
        private static bool _walking;
        private static int _walked;
        private static int _passes;
        private static Transform _named;
        private static int _transitions;
        private static bool _reportedMiss;
        private static readonly Stack<Transform> Pending = new Stack<Transform>();
        private static readonly List<GameObject> Roots = new List<GameObject>();
        private static readonly List<MonoBehaviour> Buffer = new List<MonoBehaviour>();

        /// <summary>From the player driver's LateUpdate, only when the client is there.</summary>
        internal static void Tick(Transform skip)
        {
            float now = Time.unscaledTime;
            if (_startedAt < 0f)
            {
                _startedAt = now;
            }

            if (_root != null)
            {
                if (now >= _recheckAt)
                {
                    _recheckAt = now + 1f;
                    if (!_root.gameObject.scene.isLoaded)
                    {
                        Lose("its scene unloaded");
                        return;
                    }
                }

                bool open = ReadOpen();
                if (open != IsOpen)
                {
                    IsOpen = open;
                    if (_transitions < 6)
                    {
                        _transitions++;
                        RrLog.Info("Tablet", open ? "client tablet opened - gameplay input paused" : "client tablet closed");
                    }
                }

                return;
            }

            if (IsOpen)
            {
                IsOpen = false;
            }

            if (!_walking)
            {
                if (now < _nextPassAt)
                {
                    return;
                }

                StartPass(skip);
            }

            Walk(skip);
        }

        private static bool ReadOpen()
        {
            try
            {
                if (_animator != null && _animatorHasIsOpen && _animator.isActiveAndEnabled)
                {
                    return _animator.GetBool(IsOpenHash);
                }

                if (_grabCollider != null)
                {
                    return _grabCollider.activeSelf;
                }
            }
            catch (Exception e)
            {
                RrLog.WarnOnce("Tablet", "read", "reading the tablet state failed: " + e.Message + " (logged once)");
            }

            return false;
        }

        private static void StartPass(Transform skip)
        {
            _walking = true;
            _walked = 0;
            _named = null;
            _passes++;
            Pending.Clear();
            Scene own = skip != null ? skip.gameObject.scene : default(Scene);
            Scene master = SceneManager.GetSceneByName("MasterScene");

            // Pushed last = walked first: MasterScene goes on top.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded || s == master || (own.IsValid() && s == own))
                {
                    continue;
                }

                PushRoots(s);
            }

            if (master.IsValid() && master.isLoaded)
            {
                PushRoots(master);
            }
        }

        private static void PushRoots(Scene s)
        {
            Roots.Clear();
            s.GetRootGameObjects(Roots);
            for (int i = Roots.Count - 1; i >= 0; i--)
            {
                if (Roots[i] != null)
                {
                    Pending.Push(Roots[i].transform);
                }
            }

            Roots.Clear();
        }

        private static void Walk(Transform skip)
        {
            int budget = WalkBudget;
            while (Pending.Count > 0 && budget-- > 0)
            {
                Transform t = Pending.Pop();
                if (t == null || t == skip)
                {
                    continue;
                }

                _walked++;
                string name = t.name;
                if (name.IndexOf("Simulator", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                if (name.StartsWith("Tablet", StringComparison.Ordinal))
                {
                    if (HasType(t, "TabletControl"))
                    {
                        Adopt(t, "type TabletControl");
                        return;
                    }

                    if (_named == null && (name.StartsWith("Tablet.XR(", StringComparison.Ordinal) || name.StartsWith("Tablet.NonXR(", StringComparison.Ordinal)))
                    {
                        _named = t;
                    }
                }

                for (int i = t.childCount - 1; i >= 0; i--)
                {
                    Pending.Push(t.GetChild(i));
                }
            }

            if (Pending.Count > 0)
            {
                return;
            }

            _walking = false;
            if (_named != null)
            {
                Adopt(_named, "name");
                return;
            }

            float now = Time.unscaledTime;
            _nextPassAt = now + (now - _startedAt < 60f ? 2f : 10f);
            if (!_reportedMiss && now - _startedAt > 20f)
            {
                _reportedMiss = true;
                RrLog.Warn("Tablet", "no client tablet found yet (" + _passes + " passes, last walked " + _walked +
                                     " transforms) - its open state cannot pause gameplay; still looking every few seconds");
            }
        }

        private static void Adopt(Transform root, string how)
        {
            _walking = false;
            Pending.Clear();
            _root = root;
            _animator = root.GetComponent<Animator>();
            _animatorHasIsOpen = false;
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    _animatorHasIsOpen |= p.type == AnimatorControllerParameterType.Bool && p.name == "IsOpen";
                }
            }

            Transform grab = FindChild(root, "GrabCollider", 2);
            _grabCollider = grab != null ? grab.gameObject : null;
            _recheckAt = Time.unscaledTime + 1f;
            IsOpen = ReadOpen();
            RrLog.Info("Tablet", "client tablet '" + root.name + "' in scene '" + root.gameObject.scene.name + "' (by " + how + ", pass " + _passes +
                                 "); open state from " + (_animatorHasIsOpen ? "Animator IsOpen" : _grabCollider != null ? "GrabCollider" : "nothing (never reported open)") +
                                 ", open now: " + IsOpen);
        }

        private static void Lose(string why)
        {
            RrLog.Info("Tablet", "client tablet lost (" + why + ") - searching again");
            _root = null;
            _animator = null;
            _grabCollider = null;
            IsOpen = false;
            _nextPassAt = 0f;
        }

        private static bool HasType(Transform t, string typeName)
        {
            t.GetComponents(Buffer);
            bool found = false;
            for (int i = 0; i < Buffer.Count; i++)
            {
                MonoBehaviour mb = Buffer[i];
                if (mb != null && mb.GetType().Name == typeName)
                {
                    found = true;
                    break;
                }
            }

            Buffer.Clear();
            return found;
        }

        private static Transform FindChild(Transform root, string name, int depth)
        {
            if (depth < 0)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.name == name)
                {
                    return c;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name, depth - 1);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        internal static void Reset()
        {
            _root = null;
            _animator = null;
            _animatorHasIsOpen = false;
            _grabCollider = null;
            IsOpen = false;
            _startedAt = -1f;
            _nextPassAt = 0f;
            _recheckAt = 0f;
            _walking = false;
            _walked = 0;
            _passes = 0;
            _named = null;
            _transitions = 0;
            _reportedMiss = false;
            Pending.Clear();
            Roots.Clear();
            Buffer.Clear();
        }
    }
}
