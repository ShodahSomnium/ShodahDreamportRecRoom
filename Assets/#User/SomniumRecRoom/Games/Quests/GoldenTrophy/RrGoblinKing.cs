using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.GoldenTrophy
{
    /// <summary>
    /// The Goblin King (rr_quests.md 1.2 wiki + 4.1 plan; no boss exists in the plain bundles, 2.6): Rec Room's red
    /// barrel knight "(QuestAIController)GoblinKnightSpin" scaled x1.75 (about 6 m) with a procedural gold crown. As a
    /// barrel knight he chases slowly, aims and charges (10 m at 10 m/s, 40 damage) and only the target on his back
    /// takes damage (RrEnemy's charger weak spot). On top, every 4 s he fires three large fire orbs in a 20 degree fan at
    /// the player he is after (r 0.4 m, 7 m/s, 20 damage, 1.5 m splash - GUESS numbers of 4.1), sent as an
    /// <see cref="RrQuestEvents.EnemyVolley"/> so every player's own client judges the hits. Health 3000 x (0.55 +
    /// 0.15 x party) (3.3 boss scaling, GUESS). Each quarter of his health lost calls 2 goblins + 2 imps (from the
    /// third quarter with an elite of each). Setup runs on every client (the body); the brain only on the authority.
    /// </summary>
    public sealed class RrGoblinKing : MonoBehaviour
    {
        public const float Scale = 1.75f;
        public const int BaseHealth = 3000;
        public const float VolleyEvery = 4f;

        private static RrGoblinKing _current;
        private RrEnemy _e;
        private float _nextVolley;
        private bool _setup;

        public void Setup(RrGoldenTrophyRoom room, RrEnemy e, int party)
        {
            if (_setup)
            {
                return;
            }

            _setup = true;
            _e = e;
            _current = this;
            transform.localScale = Vector3.one * Scale;
            float h0 = e.Height;
            e.Height *= Scale;
            e.Radius *= Scale;
            e.MaxHealth = Mathf.RoundToInt(BaseHealth * (0.55f + 0.15f * Mathf.Clamp(party, 1, 4)));
            e.Health = e.MaxHealth;
            e.MoveSpeed = 2f;
            e.ChargeDistance = 10f;
            e.ChargeSpeed = 10f;
            e.Damage = 40;
            e.AggroRange = 60f;
            Crown(h0);
            _nextVolley = Time.time + 3f;
            RrLog.Info("Quest", "the Goblin King rises: " + e.MaxHealth + " HP (party " + party + "), " + e.Height.ToString("0.0") + " m tall");
        }

        private void Crown(float h0)
        {
            var gold = RrQuestKit.Gold ?? RrLook.Solid(RrLook.Yellow);
            var crown = RrBuild.Group(transform, "Crown", new Vector3(0f, h0 * 0.5f, 0f)).transform;
            RrBuild.Cylinder(crown, "Band", Vector3.zero, 0.3f, 0.16f, gold, false);
            for (int i = 0; i < 6; i++)
            {
                var rot = Quaternion.Euler(0f, 60f * i, 0f);
                RrBuild.Box(crown, "Point", rot * new Vector3(0f, 0.16f, 0.27f), rot, new Vector3(0.1f, 0.22f, 0.05f), gold, false, RrLayers.Environment);
            }

            RrBuild.Sphere(crown, "Jewel", new Vector3(0f, 0.05f, 0.31f), 0.05f, RrLook.Emissive(new Color(1f, 0.1f, 0.15f), 1.5f), false);
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        /// <summary>Authority, every frame of the throne room's combat: boss bar, adds, fire orbs.</summary>
        public static void AuthorityStep(RrGoldenTrophyRoom room)
        {
            var k = _current;
            if (k == null || k._e == null)
            {
                return;
            }

            var e = k._e;
            if (!e.Alive)
            {
                room.SetQuestInt(RrQuestInts.BossHealth, 0);
                return;
            }

            float frac = e.Health / (float)Mathf.Max(1, e.MaxHealth);
            room.SetQuestInt(RrQuestInts.BossHealth, Mathf.Clamp(Mathf.CeilToInt(1000f * frac), 1, 1000));
            int phase = Mathf.Clamp(Mathf.FloorToInt((1f - frac) * 4f), 0, 3);
            if (phase > room.QuestInt(RrQuestInts.BossPhase))
            {
                room.SetQuestInt(RrQuestInts.BossPhase, phase);
                Adds(room, phase);
                room.Broadcast("The Goblin King calls for help!");
            }

            if (Time.time >= k._nextVolley && e.Target != RrCombat.NoPlayer && e.State != RrEnemyState.Stunned && e.State != RrEnemyState.Spawning)
            {
                k._nextVolley = Time.time + VolleyEvery;
                Vector3 target = RrQuestEnemies.TargetChest(e);
                Vector3 origin = e.transform.position + Vector3.up * (e.Height * 0.55f) + e.transform.forward * (e.Radius + 0.6f);
                room.Enemies.Volley(e, origin, target, 3, 20f, 7f, 0.4f, 20, 1.5f);
            }
        }

        private static void Adds(RrGoldenTrophyRoom room, int phase)
        {
            var act = room.CurrentAct;
            System.Collections.Generic.List<Vector3> points;
            if (act == null || act.Root == null || !act.Groups.TryGetValue("room", out points) || points.Count == 0)
            {
                return;
            }

            int[] types = phase >= 2
                ? new[] { RrGoldenTrophyRoom.Goblin, RrGoldenTrophyRoom.Imp, RrGoldenTrophyRoom.Goblin, RrGoldenTrophyRoom.Imp, RrGoldenTrophyRoom.EliteGoblin, RrGoldenTrophyRoom.EliteImp }
                : new[] { RrGoldenTrophyRoom.Goblin, RrGoldenTrophyRoom.Imp, RrGoldenTrophyRoom.Goblin, RrGoldenTrophyRoom.Imp };
            for (int i = 0; i < types.Length; i++)
            {
                bool flyer = types[i] == RrGoldenTrophyRoom.Imp || types[i] == RrGoldenTrophyRoom.EliteImp;
                Vector3 p = points[(i + phase * 2) % points.Count] + (flyer ? Vector3.up * 2.5f : Vector3.zero);
                room.Enemies.Spawn(types[i], act.Root.TransformPoint(p), 180f);
            }

            RrLog.Info("Quest", "Goblin King phase " + phase + ": " + types.Length + " adds");
        }
    }
}
