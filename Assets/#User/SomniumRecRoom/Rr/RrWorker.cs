using System;
using System.Collections.Generic;
using System.Threading;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rr
{
    /// <summary>
    /// One background thread for pure work - reading bundles' serialized files, building the
    /// catalog, walking references - so the frame never waits on it. Results come back through
    /// <see cref="Drain"/> on the main thread, where anything Unity is done.
    ///
    /// The same pattern as the Euro Truck world's EtsWorker: jobs must be pure (no UnityEngine
    /// object, no cache the main thread writes). If the thread cannot be started, jobs run on the
    /// main thread instead, one per Drain call - the same work in the same order, only on the frame.
    /// </summary>
    public sealed class RrWorker : IDisposable
    {
        private sealed class Job
        {
            public Func<object> Work;
            public Action<object, Exception> Done;
            public object Result;
            public Exception Failure;
        }

        private readonly object _gate = new object();
        private readonly Queue<Job> _todo = new Queue<Job>();
        private readonly Queue<Job> _done = new Queue<Job>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _thread;
        private volatile bool _stop;

        public bool Threaded { get; private set; }

        public string Failure { get; private set; }

        public int Waiting
        {
            get
            {
                lock (_gate)
                {
                    return _todo.Count + _done.Count;
                }
            }
        }

        public RrWorker(string name)
        {
            try
            {
                _thread = new Thread(Run) { Name = name, IsBackground = true, Priority = ThreadPriority.BelowNormal };
                _thread.Start();
                Threaded = true;
            }
            catch (Exception e)
            {
                _thread = null;
                Threaded = false;
                Failure = e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>Queues pure work; done runs later on the main thread, from Drain, with the result or the exception.</summary>
        public void Post(Func<object> work, Action<object, Exception> done)
        {
            var job = new Job { Work = work, Done = done };
            lock (_gate)
            {
                _todo.Enqueue(job);
            }

            if (Threaded)
            {
                _wake.Set();
            }
        }

        /// <summary>Delivers finished jobs on the calling (main) thread. Without a thread, runs one job first.</summary>
        public void Drain(int max = 16)
        {
            if (!Threaded)
            {
                Job job = null;
                lock (_gate)
                {
                    if (_todo.Count > 0)
                    {
                        job = _todo.Dequeue();
                    }
                }

                if (job != null)
                {
                    Execute(job);
                    lock (_gate)
                    {
                        _done.Enqueue(job);
                    }
                }
            }

            for (int i = 0; i < max; i++)
            {
                Job job;
                lock (_gate)
                {
                    if (_done.Count == 0)
                    {
                        return;
                    }

                    job = _done.Dequeue();
                }

                if (job.Done != null)
                {
                    job.Done(job.Result, job.Failure);
                }
            }
        }

        private static void Execute(Job job)
        {
            try
            {
                job.Result = job.Work();
            }
            catch (Exception e)
            {
                job.Failure = e;
            }
        }

        private void Run()
        {
            while (!_stop)
            {
                Job job = null;
                lock (_gate)
                {
                    if (_todo.Count > 0)
                    {
                        job = _todo.Dequeue();
                    }
                }

                if (job == null)
                {
                    _wake.WaitOne(250);
                    continue;
                }

                Execute(job);
                lock (_gate)
                {
                    _done.Enqueue(job);
                }
            }
        }

        public void Dispose()
        {
            _stop = true;
            if (Threaded)
            {
                _wake.Set();
            }
        }
    }
}
