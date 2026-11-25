using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games
{
    /// <summary>
    /// Test hooks for the play-mode harness ("call" steps; no arguments, run as coroutines). <see cref="Setup"/>
    /// builds a wall 8 m ahead of the camera, hands the paintball rifle to the local (screen-mode) hand, spawns one
    /// sword goblin (passive) and fires at the wall and the goblin. <see cref="Unleash"/> lets the goblin hunt the
    /// player, then shoots it until it dies (poof, coins). Both log their measurements under "CombatTest".
    /// Nothing here runs unless a harness scenario calls it.
    /// </summary>
    public static class RrCombatTest
    {
        public const string Rifle = "(PaintballAssaultRifle)";
        public const string Goblin = "(QuestAIController)GoblinBasicGroundMelee2020";

        private static RrWeapon _weapon;
        private static RrEnemy _goblin;

        public static IEnumerator Setup()
        {
            // The world boots into the dorm (a teleport ~3 s in): let that finish, then bring the player to the test floor.
            float wait = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - wait < 15f && (Rooms.RrTravel.Busy || Rooms.RrTravel.ArrivedAt < 0f))
            {
                yield return null;
            }

            RrPlayer.Teleport(new Vector3(0f, 0f, -3f), 0f);
            yield return Wait(0.3f);
            RrLog.Info("CombatTest", "player at " + RrPlayer.FeetPosition + " (room " + Rooms.RrTravel.Current + ")");
            RrCombat.Ensure();
            var root = RrMain.Instance != null ? RrMain.Instance.WorldRoot : null;
            RrHealth.Ensure().Begin(100, 3f, 2f);
            RrBuild.Box(root, "Test Wall", new Vector3(0f, 1.75f, 5f), new Vector3(10f, 3.5f, 0.3f), RrLook.Solid(new Color(0.86f, 0.86f, 0.83f)));
            RrBuild.Box(root, "Test Crate", new Vector3(-2.5f, 0.5f, 1.5f), new Vector3(1f, 1f, 1f), RrLook.Solid(new Color(0.55f, 0.4f, 0.25f)));

            bool given = false;
            RrLoadout.Give(Rifle, RrHandSide.Right, c =>
            {
                _weapon = c as RrWeapon;
                given = true;
            });
            float t0 = Time.realtimeSinceStartup;
            while (!given && Time.realtimeSinceStartup - t0 < 60f)
            {
                yield return null;
            }

            RrLog.Info("CombatTest", "rifle: " + (_weapon != null ? _weapon.Spec.Describe() : "none") + " after " + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s, held by "
                + (_weapon != null && _weapon.Hand != null ? "RrHand " + _weapon.Hand.Side : _weapon != null && _weapon.ScreenHeld ? "the screen hand" : "nothing")
                + "; camera " + (RrPlayer.Camera != null ? RrPlayer.Camera.transform.position.ToString("F2") : "none")
                + ", gun " + (_weapon != null ? _weapon.transform.position.ToString("F2") : "-")
                + ", barrel " + (_weapon != null && _weapon.Barrel != null ? _weapon.Barrel.position.ToString("F2") + " fwd " + _weapon.Barrel.forward.ToString("F2") : "-")
                + ", grip " + (_weapon != null && _weapon.Spec != null ? _weapon.Spec.GripPosition.ToString("F3") : "-"));

            bool ready = false;
            _goblin = RrEnemy.Spawn(Goblin, root, new Vector3(1.8f, 0f, 2f), 200f, e => ready = true);
            _goblin.Passive = true;
            t0 = Time.realtimeSinceStartup;
            while (!ready && Time.realtimeSinceStartup - t0 < 60f)
            {
                yield return null;
            }

            // The rig (and its clips) load after the spawner body.
            t0 = Time.realtimeSinceStartup;
            while (_goblin != null && !_goblin.HasRig && Time.realtimeSinceStartup - t0 < 8f)
            {
                yield return null;
            }

            if (_goblin != null)
            {
                RrLog.Info("CombatTest", "goblin ready: " + _goblin.MaxHealth + " HP, " + _goblin.MoveSpeed + " m/s, height " + _goblin.Height.ToString("0.00") + ", rig " + _goblin.HasRig + ", state " + _goblin.State);
            }

            if (_weapon == null || !_weapon.Ready)
            {
                RrLog.Warn("CombatTest", "no weapon to fire");
                yield break;
            }

            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(-3f + i * 1.1f, 0.9f + 0.45f * (i % 3), 4.84f);
                _weapon.FireToward(p);
                yield return Wait(0.2f);
            }

            int before = _goblin != null ? _goblin.Health : 0;
            for (int i = 0; i < 3 && _goblin != null; i++)
            {
                _weapon.FireToward(_goblin.Center + Vector3.up * (0.1f * i));
                yield return Wait(0.2f);
            }

            yield return Wait(0.6f);
            RrLog.Info("CombatTest", "fired: " + RrProjectile.Launched + " projectiles launched, " + RrProjectile.ActiveCount + " in flight, ammo " + _weapon.Ammo
                + ", goblin health " + before + " -> " + (_goblin != null ? _goblin.Health : -1) + ", local health " + RrHealth.Local.Health);
        }

        public static IEnumerator Unleash()
        {
            if (_goblin == null)
            {
                RrLog.Warn("CombatTest", "no goblin to unleash");
                yield break;
            }

            var start = _goblin.transform.position;
            _goblin.Passive = false;
            float t0 = Time.time;
            int hp = RrHealth.Local != null ? RrHealth.Local.Health : -1;
            while (_goblin != null && Time.time - t0 < 6f && (RrHealth.Local == null || RrHealth.Local.Health == hp))
            {
                yield return null;
            }

            if (_goblin != null)
            {
                RrLog.Info("CombatTest", "goblin hunted for " + (Time.time - t0).ToString("0.0") + " s: moved " + Vector3.Distance(start, _goblin.transform.position).ToString("0.00") + " m, state " + _goblin.State
                    + ", target " + _goblin.Target + ", local health " + hp + " -> " + (RrHealth.Local != null ? RrHealth.Local.Health : -1));
            }

            _goblin.Passive = true;
            RrPlayer.Teleport(new Vector3(0f, 0f, -6f), 0f);
            yield return Wait(0.3f);
            int shots = 0;
            while (_goblin != null && _goblin.Alive && shots < 40 && _weapon != null)
            {
                if (_weapon.FireToward(_goblin.Center))
                {
                    shots++;
                }

                yield return Wait(0.15f);
            }

            bool dead = _goblin == null || !_goblin.Alive;
            RrLog.Info("CombatTest", "goblin " + (dead ? "killed" : "still alive") + " after " + shots + " more shots");
            yield return Wait(1.2f);
            _goblin = null;
            _weapon = null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
            }
        }
    }
}
