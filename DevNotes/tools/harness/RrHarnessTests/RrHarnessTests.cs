using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RrHarness
{
    /// <summary>
    /// The play-mode harness: one test, RrScenario, that boots the world and runs the scenario named by the
    /// environment variable RR_SCENARIO (default scratchpad/harness/scenarios/smoke.json), writing PNGs and
    /// summary.txt to RR_OUT (default scratchpad/harness/out/&lt;scenario&gt;). See RrScenarioRun for the step list and
    /// scratchpad/harness/README.md for how to run it (run.sh).
    ///
    /// The test passes whenever the world boots, whatever the steps do (their failures are in the summary); it
    /// fails when RrMain.Ready is not reached (the summary then says why). Logged errors do not fail it
    /// (LogAssert.ignoreFailingMessages): they are collected into the summary instead.
    /// </summary>
    public sealed class RrHarnessTests
    {
        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator RrScenario()
        {
            LogAssert.ignoreFailingMessages = true;
            var run = new RrScenarioRun();
            var e = run.Execute();

            while (true)
            {
                object current;

                try
                {
                    if (!e.MoveNext())
                    {
                        break;
                    }

                    current = e.Current;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[RrHarness] run crashed: " + ex);
                    run.BootFailure = run.BootFailure ?? "harness crashed: " + ex.Message;
                    break;
                }

                yield return current;
                run.Frame(Time.unscaledDeltaTime);
            }

            var down = run.TearDown();

            while (down.MoveNext())
            {
                yield return down.Current;
            }

            if (run.BootFailure != null)
            {
                Assert.Fail(run.BootFailure + "\nsummary: " + System.IO.Path.Combine(run.OutDir ?? "", "summary.txt"));
            }
        }
    }
}
