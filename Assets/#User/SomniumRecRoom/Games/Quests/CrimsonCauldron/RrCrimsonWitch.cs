using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.CrimsonCauldron
{
    /// <summary>
    /// The Crimson Witch, the Finale's boss in the bell tower (rr_latest_rgames_story.md 6.1 act 9; rr_quests.md 1.3 /
    /// 4.2). No witch exists in the plain bundles, so she is Rec Room's red imp "(QuestAIController)GoblinEliteFlyingRanged"
    /// scaled x2.2 with a procedural pointed hat. SIMPLIFIED (basic form): in Rec Room she cannot be hurt directly and the
    /// team bats her purple orb back to her (3 returns in the first phase, 4 in each later one, with more and more
    /// enemies); here she takes weapon hits, her health bar is split into the same three phases (no adds, then 2
    /// goblins + 2 imps, then 3 goblins + 2 elite imps + 1 shield goblin, from the level's own East / North / West spawn
    /// points of the "Witch Boss Encounter"), and every 5 s she throws Rec Room's slow purple orb (r 0.45 m, 5 m/s, 30
    /// damage, 1.5 m splash; GUESS numbers of 4.2) on top of the imp's own fireball spread. Health 4500 x (0.55 + 0.15 x
    /// party) (the 3.3 boss scaling, GUESS). Setup runs on every client (the body); the brain only on the authority.
    /// </summary>
    public sealed class RrCrimsonWitch : MonoBehaviour
    {
        public const float Scale = 2.2f;
        public const int BaseHealth = 4500;
        public const float OrbEvery = 5f;

        /// <summary>Health fractions where phases 2 and 3 start.</summary>
        public static readonly float[] PhaseAt = { 0.7f, 0.35f };

        private static RrCrimsonWitch _current;
        private RrEnemy _e;
        private float _nextOrb;
        private bool _setup;

        public void Setup(RrEnemy e, int party)
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
            e.AggroRange = 70f;
            e.Hover = 4f;
            e.Damage = 25;
            Hat(h0);
            _nextOrb = Time.time + 4f;
            RrLog.Info("CrimsonCauldron", "the Crimson Witch appears: " + e.MaxHealth + " HP (party " + party + "), " + e.Height.ToString("0.0") + " m");
        }

        /// <summary>A black pointed hat with a crimson band (stacked cylinders), on the imp's head.</summary>
        private void Hat(float h0)
        {
            var black = RrLook.Solid(new Color(0.06f, 0.05f, 0.07f));
            var band = RrLook.Emissive(new Color(0.75f, 0.05f, 0.12f), 0.8f);
            var hat = RrBuild.Group(transform, "Witch Hat", new Vector3(0f, h0 * 0.62f, 0f)).transform;
            RrBuild.Cylinder(hat, "Brim", Vector3.zero, 0.32f, 0.02f, black, false);
            RrBuild.Cylinder(hat, "Band", new Vector3(0f, 0.04f, 0f), 0.16f, 0.06f, band, false);
            float y = 0.07f;
            float r = 0.15f;
            for (int i = 0; i < 5; i++)
            {
                RrBuild.Cylinder(hat, "Cone " + i, new Vector3(0.012f * i, y + 0.045f, 0f), r, 0.09f, black, false);
                y += 0.09f;
                r *= 0.72f;
            }
        }

        private void OnDestroy()
        {
            if (_current == this)
            {
                _current = null;
            }
        }

        /// <summary>Room shutdown: no witch outlives her room.</summary>
        public static void Clear()
        {
            _current = null;
        }

        /// <summary>Authority, every frame of the Finale's combat: the boss bar, the phases' adds, the purple orbs.</summary>
        public static void AuthorityStep(RrCrimsonCauldronRoom room)
        {
            var w = _current;
            if (w == null || w._e == null)
            {
                return;
            }

            var e = w._e;
            if (!e.Alive)
            {
                room.SetQuestInt(RrQuestInts.BossHealth, 0);
                return;
            }

            float frac = e.Health / (float)Mathf.Max(1, e.MaxHealth);
            room.SetQuestInt(RrQuestInts.BossHealth, Mathf.Clamp(Mathf.CeilToInt(1000f * frac), 1, 1000));
            int phase = frac <= PhaseAt[1] ? 2 : frac <= PhaseAt[0] ? 1 : 0;
            if (phase > room.QuestInt(RrQuestInts.BossPhase))
            {
                room.SetQuestInt(RrQuestInts.BossPhase, phase);
                Adds(room, phase);
                room.Broadcast(phase == 1 ? "The Crimson Witch calls her goblins!" : "The Crimson Witch summons everything she has!");
            }

            if (Time.time >= w._nextOrb && e.Target != RrCombat.NoPlayer && e.State != RrEnemyState.Stunned && e.State != RrEnemyState.Spawning)
            {
                w._nextOrb = Time.time + OrbEvery;
                Vector3 target = RrQuestEnemies.TargetChest(e);
                Vector3 origin = e.transform.position + Vector3.up * (e.Height * 0.5f) + e.transform.forward * (e.Radius + 0.6f);
                room.Enemies.Volley(e, origin, target, 1, 0f, 5f, 0.45f, 30, 1.5f);
            }
        }

        private static void Adds(RrCrimsonCauldronRoom room, int phase)
        {
            var act = room.CurrentAct;
            if (act == null || act.Root == null)
            {
                return;
            }

            var points = new List<Vector3>();
            foreach (var g in new[] { "east", "north", "west" })
            {
                List<Vector3> list;
                if (act.Groups.TryGetValue(g, out list))
                {
                    points.AddRange(list);
                }
            }

            if (points.Count == 0)
            {
                points.Add(act.Entry + Vector3.forward * 8f);
            }

            int[] types = phase >= 2
                ? new[] { RrCrimsonCauldronRoom.Goblin, RrCrimsonCauldronRoom.Goblin, RrCrimsonCauldronRoom.Goblin, RrCrimsonCauldronRoom.EliteImp, RrCrimsonCauldronRoom.EliteImp, RrCrimsonCauldronRoom.Shield }
                : new[] { RrCrimsonCauldronRoom.Goblin, RrCrimsonCauldronRoom.Imp, RrCrimsonCauldronRoom.Goblin, RrCrimsonCauldronRoom.Imp };
            for (int i = 0; i < types.Length; i++)
            {
                bool flyer = types[i] == RrCrimsonCauldronRoom.Imp || types[i] == RrCrimsonCauldronRoom.EliteImp;
                Vector3 p = points[(i * 3 + phase) % points.Count] + (flyer ? Vector3.up * 2.5f : Vector3.zero);
                room.Enemies.Spawn(types[i], act.Root.TransformPoint(p), 180f);
            }

            RrLog.Info("CrimsonCauldron", "Crimson Witch phase " + (phase + 1) + ": " + types.Length + " adds");
        }
    }
}
