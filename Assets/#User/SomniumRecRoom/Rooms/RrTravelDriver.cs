using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>
    /// The MonoBehaviour behind <see cref="RrTravel"/>: it runs the trips and the room builds as coroutines and
    /// watches the local player's footing each frame. It lives under the world root, so it goes with the world.
    ///
    /// Room code is stepped here rather than handed to Unity: <see cref="Guard"/> walks nested enumerators itself
    /// and wraps every step in a try/catch, so an exception anywhere in a room's Build (or in a trip) ends that
    /// routine with a report instead of silently killing the whole chain and leaving the player behind a black
    /// screen. Unity's own yield instructions (WaitForSeconds, async operations, coroutines) still go to Unity.
    /// </summary>
    public sealed class RrTravelDriver : MonoBehaviour
    {
        /// <summary>How a guarded routine ended.</summary>
        public sealed class Outcome
        {
            public bool Finished;
            public bool TimedOut;
            public Exception Error;

            public bool Failed
            {
                get { return !Finished; }
            }
        }

        public static RrTravelDriver Instance { get; private set; }

        private float _nextCheck;

        public static RrTravelDriver Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("Rr Travel");
            if (RrMain.Instance != null && RrMain.Instance.WorldRoot != null)
            {
                go.transform.SetParent(RrMain.Instance.WorldRoot, false);
            }

            return go.AddComponent<RrTravelDriver>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Steps a routine and every enumerator it yields, catching exceptions at each step. Stops (TimedOut)
        /// once realtime passes the deadline (0: none) or when cancel returns true.
        /// </summary>
        public static IEnumerator Guard(IEnumerator root, Outcome outcome, float deadline = 0f, Func<bool> cancel = null)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if ((deadline > 0f && Time.realtimeSinceStartup > deadline) || (cancel != null && cancel()))
                {
                    outcome.TimedOut = true;
                    yield break;
                }

                var top = stack.Peek();
                bool more;
                object current = null;
                try
                {
                    more = top.MoveNext();
                    if (more)
                    {
                        current = top.Current;
                    }
                }
                catch (Exception e)
                {
                    outcome.Error = e;
                    yield break;
                }

                if (!more)
                {
                    stack.Pop();
                    continue;
                }

                var nested = current as IEnumerator;
                if (nested != null)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current;
            }

            outcome.Finished = true;
        }

        private void Update()
        {
            // The client's respawn is a one-frame jump: watched every frame (a feet position and a few compares).
            RrTravel.WatchRespawn();

            // Footing: twice a second is plenty (a fall of 10 m takes 1.4 s, the client's anti-fall waits ~4.5 s).
            if (Time.unscaledTime < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.unscaledTime + 0.5f;
            RrTravel.CheckFooting();
        }
    }
}
