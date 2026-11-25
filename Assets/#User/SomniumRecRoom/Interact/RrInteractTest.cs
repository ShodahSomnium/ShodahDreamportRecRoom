using System.Collections;
using System.Collections.Generic;
using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// Test hooks for the play-mode harness (<c>{"call": "SomniumSpace.Worlds.SomniumRecRoom.Interact.RrInteractTest.Setup"}</c>
    /// and so on, in order: Setup, Drink, Break, Eat, Ball, Button, Report). They spawn a root beer bottle, a
    /// basketball, a pizza slice and a hoop on the Arrival floor in front of the harness camera (0, 1.6, -3), make
    /// them tools, then drive them through the same entry points players use (RrGrab.Give, the "use" events,
    /// RrGrab.Release). Each step logs one line "Interact test: ..."; Report logs the verdicts. Nothing here runs
    /// unless called.
    /// </summary>
    public static class RrInteractTest
    {
        public const string Bottle = "(Consumables_RootBeer_Bottle)";
        public const string Basketball = "(Basketball)";
        public const string Pizza = "(Consumables_CheesePizza_Slice)";
        public const string Hoop = "(Basketballhoop)";

        private static readonly Dictionary<string, RrInstance> Spawned = new Dictionary<string, RrInstance>();
        private static readonly List<string> Results = new List<string>();
        private static Transform _mouth;

        private static RrHand Hand
        {
            get
            {
                if (RrHand.Right == null)
                {
                    var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
                    RrHand.Right = RrHand.Create(root, RrHandSide.Right);
                    Log("no right hand from the Player module: made one for the test");
                }

                return RrHand.Right;
            }
        }

        private static void Log(string text)
        {
            RrLog.Info("Interact", "Interact test: " + text);
        }

        private static void Result(string name, bool ok, string detail)
        {
            Results.Add(name + (ok ? " OK" : " FAIL") + " (" + detail + ")");
            Log(name + (ok ? " OK: " : " FAIL: ") + detail);
        }

        private static T Get<T>(string prefab) where T : Component
        {
            RrInstance inst;
            return Spawned.TryGetValue(prefab, out inst) && inst != null ? inst.GetComponent<T>() : null;
        }

        public static IEnumerator Setup()
        {
            Spawned.Clear();
            Results.Clear();
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            if (_mouth == null)
            {
                _mouth = new GameObject("Rr Test Mouth").transform;
                _mouth.SetParent(root, false);
            }

            _mouth.position = new Vector3(0.3f, 1.45f, -1.6f);
            _mouth.rotation = Quaternion.LookRotation(Vector3.forward);
            RrMouth.Override = _mouth;
            var hand = Hand;
            Spawn(Bottle, new Vector3(-0.6f, 0.03f, 0f), 0f, null);
            Spawn(Basketball, new Vector3(0f, 0.2f, 0f), 0f, null);
            Spawn(Pizza, new Vector3(0.6f, 0.05f, 0f), 0f, null);
            Spawn(Hoop, new Vector3(0f, 2.6f, 2.2f), 180f, RrSpawnOptions.Scenery());
            float until = Time.realtimeSinceStartup + 180f;
            while (Spawned.Count < 4 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            var sb = new StringBuilder();
            foreach (var kv in Spawned)
            {
                sb.Append(kv.Key).Append(kv.Value != null ? " " : " missing ");
            }

            Result("setup", Spawned.Count == 4 && hand != null, sb.ToString().Trim() + ", tools made " + RrTools.Made);
            yield return new WaitForSeconds(1f);
        }

        private static void Spawn(string prefab, Vector3 pos, float yaw, RrSpawnOptions options)
        {
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            RrSpawn.Spawn(prefab, root, pos, Quaternion.Euler(0f, yaw, 0f), inst =>
            {
                if (inst != null)
                {
                    RrTools.MakeTool(inst, true);
                }

                Spawned[prefab] = inst;
            }, options);
        }

        public static IEnumerator Drink()
        {
            var g = Get<RrGrabbable>(Bottle);
            var d = Get<RrDrinkable>(Bottle);
            if (g == null || d == null)
            {
                Result("drink", false, "no bottle tool (grabbable " + (g != null) + ", drinkable " + (d != null) + ")");
                yield break;
            }

            RrGrab.Give(g, Hand);
            yield return new WaitForSeconds(0.4f);
            bool held = g.IsHeld;
            g.NotifyUseDown(Hand);
            if (!d.IsPouring)
            {
                d.SetScreenDrink(true);
            }

            yield return new WaitForSeconds(2.6f);
            Result("drink", held && !d.IsCorked && d.Swallows >= 2,
                "held " + held + ", corked " + d.IsCorked + ", pouring " + d.IsPouring + ", drinking " + d.IsDrinking + ", swallows " + d.Swallows +
                ", remaining " + d.Remaining.ToString("0.0") + " s, ToMouth " + g.ToMouth.ToString("0.00"));
        }

        public static IEnumerator Break()
        {
            var g = Get<RrGrabbable>(Bottle);
            var b = Get<RrBreakable>(Bottle);
            var d = Get<RrDrinkable>(Bottle);
            if (g == null || b == null)
            {
                Result("break", false, "no breakable bottle");
                yield break;
            }

            if (d != null)
            {
                d.SetScreenDrink(false);
                g.NotifyUseUp(Hand);
            }

            yield return new WaitForSeconds(0.5f);
            var sound = Get<RrCollisionSound>(Bottle);
            if (!g.IsHeld)
            {
                RrGrab.Give(g, Hand);
                yield return new WaitForSeconds(0.3f);
            }

            // Thrown down hard at the floor, as a player smashing it, from a known spot (the shot looks there).
            RrGrab.Release(g, new Vector3(0f, -7f, 1.5f), Vector3.zero);
            var start = new Vector3(0.3f, 1.2f, -1.3f);
            g.transform.position = start;
            if (g.Body != null)
            {
                g.Body.position = start;
                g.Body.linearVelocity = new Vector3(0f, -7f, 1.5f);
            }
            float until = Time.time + 2f;
            while (!b.IsBroken && Time.time < until)
            {
                yield return null;
            }

            Result("break", b.IsBroken, "broken " + b.IsBroken + ", shatter speed " + b.ShatterSpeed + ", fractured visual " + (b.Fractured != null) +
                ", smash clips " + b.SmashSounds.Count + ", impact sounds " + (sound != null ? sound.Played.ToString() : "none"));
            yield return new WaitForSeconds(0.25f);
        }

        public static IEnumerator Eat()
        {
            var g = Get<RrGrabbable>(Pizza);
            var e = Get<RrEdible>(Pizza);
            if (g == null || e == null)
            {
                Result("eat", false, "no edible slice (grabbable " + (g != null) + ", edible " + (e != null) + ")");
                yield break;
            }

            RrGrab.Give(g, Hand);
            yield return new WaitForSeconds(0.4f);
            int bites = e.Bites;
            for (int i = 0; i < bites - 1; i++)
            {
                g.NotifyUseDown(Hand);
                e.ScreenBite();
                g.NotifyUseUp(Hand);
                // The bite lands about 0.3 s after "use" (the piece travels to the mouth); the interval runs from it.
                yield return new WaitForSeconds(e.MinBiteInterval + 0.5f);
            }

            Result("eat", e.Stage == bites - 1, "bites " + bites + ", taken " + e.Stage + ", left " + e.BitesLeft + ", interval " + e.MinBiteInterval + " s, reach " + e.BiteDistance.ToString("0.00") + " m");
        }

        public static IEnumerator Ball()
        {
            var g = Get<RrGrabbable>(Basketball);
            var ball = Get<RrBall>(Basketball);
            var hoopInst = Spawned.ContainsKey(Hoop) ? Spawned[Hoop] : null;
            var hoop = hoopInst != null ? hoopInst.GetComponent<RrHoop>() : null;
            var pizza = Get<RrGrabbable>(Pizza);
            if (pizza != null && pizza.IsHeld)
            {
                RrGrab.Release(pizza, Vector3.zero, Vector3.zero);
            }

            if (g == null || ball == null || hoop == null)
            {
                Result("ball", false, "grabbable " + (g != null) + ", ball " + (ball != null) + ", hoop " + (hoop != null));
                yield break;
            }

            RrGrab.Give(g, Hand);
            yield return new WaitForSeconds(0.5f);
            var body = g.Body;
            float damping = body != null ? body.linearDamping : 0f;
            if (body != null)
            {
                body.linearDamping = 0f;
            }

            // A ballistic lob that drops through the rim's centre after 1.1 s (semi-implicit Euler drops an extra
            // g dt t / 2, hence T + dt).
            // Launched from a fixed spot 3 m in front of the hoop: the screen hand is wherever the player is, and the
            // Rooms module may already have taken the (fallback) player to the dorm.
            Vector3 from = hoop.RimCenter + new Vector3(0.3f, -0.9f, -3f);
            Vector3 to = hoop.RimCenter + Vector3.up * 0.02f;
            const float T = 1.1f;
            Vector3 v = (to - from) / T - 0.5f * Physics.gravity * (T + Time.fixedDeltaTime);
            RrGrab.Release(g, v, Vector3.zero);
            g.transform.position = from;
            if (body != null)
            {
                body.position = from;
                body.linearVelocity = v;
                body.angularVelocity = Vector3.zero;
            }
            float until = Time.time + 3f;
            while (ball.Scores == 0 && Time.time < until)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            if (body != null)
            {
                body.linearDamping = damping;
            }

            var sound = Get<RrCollisionSound>(Basketball);
            Result("ball", ball.Scores > 0, "scores " + ball.Scores + ", hoop scores " + hoop.Scores + ", bounces " + ball.Bounces + ", rim " + ball.RimHits + ", board " + ball.BoardHits +
                ", impact sounds " + (sound != null ? sound.Played + " of " + sound.MappingCount + " mappings" : "none") + ", launch " + v.magnitude.ToString("0.0") + " m/s");
        }

        public static IEnumerator Button()
        {
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            var button = RrPressable.Create(root, "Rr Test Button", new Vector3(1.2f, 0.9f, -0.5f), Quaternion.Euler(-90f, 0f, 0f), RrLook.Red);
            yield return null;
            yield return new WaitForFixedUpdate();
            Vector3 eye = new Vector3(1.2f, 0.9f, -2.2f);
            bool first = RrPressable.PressAlong(new Ray(eye, Vector3.forward), RrPressable.ScreenReach, "use key");
            bool again = RrPressable.PressAlong(new Ray(eye, Vector3.forward), RrPressable.ScreenReach, "use key");
            yield return new WaitForSeconds(0.4f);
            bool later = button.Press("code");
            Result("button", first && !again && later && button.Presses == 2, "ray press " + first + ", within cooldown " + again + ", after cooldown " + later + ", presses " + button.Presses);
        }

        public static void Report()
        {
            int ok = 0;
            foreach (var r in Results)
            {
                if (r.Contains(" OK "))
                {
                    ok++;
                }
            }

            Log(ok + "/" + Results.Count + " passed: " + string.Join("; ", Results));
            RrMouth.Override = null;
        }
    }
}
