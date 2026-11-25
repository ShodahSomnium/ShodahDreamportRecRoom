using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests
{
    /// <summary>
    /// Down and revive (rr_quests.md 2.5 measured, 3.7): one hit downs the local player (Rec Room's final rule,
    /// <see cref="RrQuestDef.PlayerHealth"/> 1; <see cref="RrHealth"/> status Down: slowed, cannot fight, enemies
    /// ignore them). A teammate within
    /// <see cref="RrQuestDef.ReviveRange"/> (2.5 m, CombatRevivePerHandScreenPlayerBehaviorConfig) revives them by
    /// holding: VR - a hand with grip held on the downed player's revive marker (Rec Room's handshake / high five);
    /// desktop - look at the marker and hold E (the grab key). The reviver must stay within 1 m
    /// (MaxReviveMoveDistance) while <c>Quest_Revive</c> plays; then a <see cref="RrQuestEvents.Revive"/> event
    /// brings the victim back (one hit downs again) with 3 s of invincibility (GUESS), the only protection the
    /// one-hit rule leaves, and <c>RecRoyale_Revive_Revived01</c>. Rec Room's revive is a high five (WIKI).
    /// The bleed-out is ours, not Rec Room's (no source has one; rr_latest_rgames_story.md 5.3 item 11): nobody comes
    /// within 30 s and the player gets up at the act's entry (the team paid the death penalty when they went down).
    /// Downed teammates carry our own marker (a red ring and a glowing orb above their feet) - the Somnium avatar
    /// itself is never touched.
    /// </summary>
    public sealed class RrQuestRevive : MonoBehaviour
    {
        private RrQuestRoom _room;
        private float _downAt = -1f;
        private int _target = -1;
        private float _progress;
        private Vector3 _start;
        private AudioSource _loop;
        private bool _grabHeld;
        private int _sendTo = -1;
        private float _sendUntil, _nextSend;
        private readonly Dictionary<int, GameObject> _markers = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, float> _countedAt = new Dictionary<int, float>();
        private readonly List<int> _drop = new List<int>();

        public static RrQuestRevive Create(RrQuestRoom room)
        {
            var go = new GameObject("Rr Quest Revive");
            go.transform.SetParent(room.transform, false);
            var r = go.AddComponent<RrQuestRevive>();
            r._room = room;
            return r;
        }

        private void OnEnable()
        {
            RrNet.On(RrQuestEvents.Revive, OnRevive);
        }

        private void OnDisable()
        {
            RrNet.Off(RrQuestEvents.Revive, OnRevive);
            StopLoop();
        }

        /// <summary>Every frame from the room while a run is on (playing false: clean up).</summary>
        public void Step(bool playing)
        {
            var def = _room.Def;
            var h = RrHealth.Local;
            if (!playing || def == null)
            {
                _downAt = -1f;
                Cancel();
                ClearMarkers();
                return;
            }

            if (_grabHeld && RrInput.GrabUp)
            {
                _grabHeld = false;
            }

            if (RrInput.GrabDown)
            {
                _grabHeld = true;
            }

            LocalDown(def, h);
            Markers();
            Reviving(def, h);
            Resend();
        }

        private void LocalDown(RrQuestDef def, RrHealth h)
        {
            if (h == null || !h.IsOut)
            {
                _downAt = -1f;
                return;
            }

            if (_downAt < 0f)
            {
                _downAt = Time.time;
                _room.Hud.Toast("YOU'RE DOWN!", 2f);
            }

            float left = def.BleedOutSeconds - (Time.time - _downAt);
            if (left <= 0f)
            {
                _downAt = -1f;
                h.Respawn(true);
                _room.Hud.Toast("Back on your feet at the act entry", 2.5f);
                RrLog.Info("Quest", "bled out (ours, " + def.BleedOutSeconds + " s): up again at the entry of act " + (_room.ActIndex + 1));
                return;
            }

            _room.Hud.Prompt("Down! A teammate can revive you  (" + Mathf.CeilToInt(left) + ")", 1f - left / def.BleedOutSeconds);
        }

        // ------------------------------------------------------------------ markers

        private void Markers()
        {
            var members = RrNet.MembersOfMyInstance();
            int me = RrNet.LocalId;
            foreach (var id in members)
            {
                if (id == me)
                {
                    continue;
                }

                var r = RrRemotePlayers.Get(id);
                bool down = r != null && r.Root != null && RrNet.Presence(id).Status == RrStatus.Down;
                GameObject m;
                _markers.TryGetValue(id, out m);
                if (down)
                {
                    if (m == null)
                    {
                        m = MakeMarker(id);
                        _markers[id] = m;
                    }

                    m.transform.position = r.Root.position;
                    var orb = m.transform.GetChild(1);
                    orb.localPosition = new Vector3(0f, 1.1f + 0.06f * Mathf.Sin(Time.time * 3f), 0f);
                }
                else if (m != null)
                {
                    Destroy(m);
                    _markers.Remove(id);
                }
            }

            _drop.Clear();
            foreach (var kv in _markers)
            {
                if (!members.Contains(kv.Key))
                {
                    _drop.Add(kv.Key);
                }
            }

            foreach (var id in _drop)
            {
                if (_markers[id] != null)
                {
                    Destroy(_markers[id]);
                }

                _markers.Remove(id);
            }
        }

        private GameObject MakeMarker(int id)
        {
            var go = new GameObject("Downed " + id);
            go.transform.SetParent(transform, false);
            RrBuild.Cylinder(go.transform, "Ring", new Vector3(0f, 0.02f, 0f), 0.6f, 0.03f, RrLook.Emissive(new Color(1f, 0.2f, 0.15f), 1.5f), false);
            RrBuild.Sphere(go.transform, "Orb", new Vector3(0f, 1.1f, 0f), 0.16f, RrLook.Emissive(new Color(1f, 0.35f, 0.25f), 2.5f), false);
            RrBuild.Sign(go.transform, "Label", "REVIVE", new Vector3(0f, 1.45f, 0f), Quaternion.identity, 2.2f, new Color(1f, 0.5f, 0.4f), true, 2f);
            return go;
        }

        private void ClearMarkers()
        {
            foreach (var kv in _markers)
            {
                if (kv.Value != null)
                {
                    Destroy(kv.Value);
                }
            }

            _markers.Clear();
        }

        // ------------------------------------------------------------------ reviving a teammate

        private void Reviving(RrQuestDef def, RrHealth h)
        {
            if (h == null || h.IsOut || !h.InCombat)
            {
                Cancel();
                return;
            }

            Vector3 feet = RrPlayer.FeetPosition;
            int best = -1;
            float bestD = def.ReviveRange;
            foreach (var kv in _markers)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                float d = Vector3.Distance(new Vector3(feet.x, 0f, feet.z), new Vector3(kv.Value.transform.position.x, 0f, kv.Value.transform.position.z));
                if (d <= bestD)
                {
                    bestD = d;
                    best = kv.Key;
                }
            }

            if (best < 0)
            {
                Cancel();
                return;
            }

            var orb = _markers[best].transform.GetChild(1).position;
            string nick = RrNet.Presence(best).Nick;
            if (string.IsNullOrEmpty(nick))
            {
                nick = "your teammate";
            }

            bool vr = RrPlayer.InVr;
            bool holding = vr ? HandOn(orb) : _grabHeld && Looking(orb);
            if (!holding)
            {
                if (_target >= 0)
                {
                    Cancel();
                }

                _room.Hud.Prompt(vr ? "Grip " + nick + "'s marker to revive" : "Hold E to revive " + nick);
                return;
            }

            if (_target != best)
            {
                Cancel();
                _target = best;
                _start = feet;
                _progress = 0f;
                _loop = RrSound.Loop("Quest_Revive", _markers[best].transform, 0.9f, 15f);
            }

            if (Vector3.Distance(feet, _start) > def.ReviveStay)
            {
                Cancel();
                _room.Hud.Toast("Stay close to revive", 1.5f);
                return;
            }

            _progress += Time.deltaTime / (vr ? def.ReviveSecondsVr : def.ReviveSecondsDesktop);
            _room.Hud.Prompt("Reviving " + nick + "...", _progress);
            if (_progress >= 1f)
            {
                int victim = _target;
                Cancel();
                RrSound.Play("RecRoyale_Revive_Revived01", orb, 1f, 1f);
                _sendTo = victim;
                _sendUntil = Time.time + 3f;
                _nextSend = 0f;
                Resend();
                RrLog.Info("Quest", "revived player " + victim);
            }
        }

        private static bool HandOn(Vector3 orb)
        {
            return Held(RrHand.Left, orb) || Held(RrHand.Right, orb);
        }

        private static bool Held(RrHand hand, Vector3 orb)
        {
            if (hand == null || !hand.GripHeld || hand.Held != null)
            {
                return false;
            }

            var p = hand.Grip != null ? hand.Grip.position : hand.transform.position;
            return Vector3.Distance(p, orb) < 0.35f;
        }

        private static bool Looking(Vector3 orb)
        {
            var ray = RrInput.AimRay;
            Vector3 to = orb - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along < 0f)
            {
                return false;
            }

            return Vector3.Distance(ray.origin + ray.direction * along, orb) < 0.6f;
        }

        private void Cancel()
        {
            _target = -1;
            _progress = 0f;
            StopLoop();
        }

        private void StopLoop()
        {
            if (_loop != null)
            {
                RrSound.Stop(_loop);
                _loop = null;
            }
        }

        /// <summary>Repeats the Revive event until the victim's presence is no longer Down (requests can be lost).</summary>
        private void Resend()
        {
            if (_sendTo < 0)
            {
                return;
            }

            if (Time.time > _sendUntil || RrNet.Presence(_sendTo).Status != RrStatus.Down)
            {
                _sendTo = -1;
                return;
            }

            if (Time.time >= _nextSend)
            {
                _nextSend = Time.time + 0.4f;
                var b = new byte[2];
                int i = 0;
                RrQuestEvents.U16(b, ref i, _sendTo);
                RrNet.Send(RrQuestEvents.Revive, b);
            }
        }

        private void OnRevive(int sender, byte[] body)
        {
            int i = 0;
            int victim = RrQuestEvents.U16(body, ref i);
            float last;
            if (_room.IsAuthority && (!_countedAt.TryGetValue(victim, out last) || Time.time - last > 4f))
            {
                _countedAt[victim] = Time.time;
                _room.SetQuestInt(RrQuestInts.Revives, _room.QuestInt(RrQuestInts.Revives) + 1);
            }

            var h = RrHealth.Local;
            if (victim != RrNet.LocalId || h == null || !h.IsOut)
            {
                return;
            }

            var def = _room.Def;
            h.Revive(def != null ? def.ReviveHealth : 1, def != null ? def.ReviveInvincible : 3f);
            _downAt = -1f;
            RrSound.PlayUi("RecRoyale_Revive_Revived01", 0.9f);
            string nick = RrNet.Presence(sender).Nick;
            _room.Hud.Toast("Revived" + (string.IsNullOrEmpty(nick) ? "!" : " by " + nick + "!"), 2f);
            RrLog.Info("Quest", "local player revived by " + sender);
        }
    }
}
