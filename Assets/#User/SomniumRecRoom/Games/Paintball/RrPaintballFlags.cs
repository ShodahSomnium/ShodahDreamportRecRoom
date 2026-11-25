using System;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;
using B = SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball.RrPaintballBuilder;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Capture the Flag, as Rec Room's PaintballManager plays it (rr_shooters.md §1.1, §5, §7.1):
    /// grab the enemy flag and bring it to your own stand while your own flag is home; a carrier who is hit drops
    /// the flag where they fell; a dropped flag stays until someone touches it - a defender sends it home at once,
    /// an attacker picks it up; 3 captures win (or the lead when the 600 s run out).
    ///
    /// State lives in the instance's activity ints, written only by the instance authority ("fake master"), per
    /// flag f (0 Blue, 1 Red) at 6 f: state (0 home, 1 carried, 2 dropped), holder PlayerId + 1, and the dropped
    /// position in room-local millimetres. Players ask with <see cref="RrPaintballRoom.EvFlag"/> (take, return,
    /// capture), repeated every 0.3 s while the condition holds and the ints do not show it yet, so a request lost in
    /// an authority hand-over is simply sent again. Every client draws the flags from the ints and plays the VO on
    /// the transitions it sees ("Blue flag taken", "Red flag dropped", "Blue flag returned", "Redteamscores").
    ///
    /// Touch: walking into a flag (feet within 1.1 m) works in screen mode and VR; in VR closing a hand on the staff
    /// also takes it. The local carrier sees the flag over the left shoulder (screen) or in the left hand (VR);
    /// everyone else sees it in the carrier's left hand, or on their back when the client gives no hand transforms.
    /// </summary>
    public sealed class RrPaintballFlags : MonoBehaviour
    {
        public const int Home = 0, Carried = 1, Dropped = 2;
        public const byte Take = 1, Return = 2, Capture = 3;
        public const int CapturesToWin = 3;
        private const int Stride = 6;
        private const float TouchRadius = 1.1f;
        private const float CaptureRadius = 1.6f;

        private RrPaintballRoom _room;
        private readonly Transform[] _flags = new Transform[2];
        private readonly Transform[] _stands = new Transform[2];
        private readonly Vector3[] _flagFoot = { new Vector3(0f, -0.983f, 0f), new Vector3(0f, -0.983f, 0f) };
        private readonly int[] _seenState = { -1, -1 };
        private readonly int[] _seenHolder = { -1, -1 };
        private float _nextAsk;
        private float _nextVo;
        private int _standsMap = -1;
        private object _standsLevel;

        /// <summary>
        /// Rec Room's carry: (FlagTool)'s LeftHandPickupTransform (43ea1176 #-3300879768199749876), the flag's pose in
        /// Rec Room's hand frame - root 0.79 m up the thumb side, pole tipped 10 deg forward. The first build turned the
        /// grip by Euler(-70, 0, 0) instead, which put the flag's +Y out of the little-finger side: upside down.
        /// </summary>
        private static readonly Vector3 CarryPosition = new Vector3(0f, 0.788913f, 0.136643f);
        private static readonly Quaternion CarryRotation = new Quaternion(0.0866689f, 0f, 0f, 0.996237f);

        internal static RrPaintballFlags Create(RrPaintballRoom room, RrPaintballBuilder b)
        {
            var go = new GameObject("Flags");
            go.transform.SetParent(room.transform, false);
            var f = go.AddComponent<RrPaintballFlags>();
            f._room = room;
            for (int t = 0; t < 2; t++)
            {
                int team = t;
                b.Prop("(FlagTool)", go.transform, new Vector3(0f, -200f, 0f), 0f, 1f, false, RrSpawnOptions.Visual(), inst =>
                {
                    f._flags[team] = inst.transform;
                    var foot = inst.Find("Goal Attach Point");
                    if (foot != null)
                    {
                        f._flagFoot[team] = inst.transform.InverseTransformPoint(foot.position);
                    }

                    int n = B.Tint(inst.gameObject, B.TeamColor(team), "Flag", "Team", "Glow");
                    RrLog.Info("Paintball", "flag " + team + " ready (" + n + " team-coloured materials, foot " + f._flagFoot[team] + ")");
                });
                b.Prop("(FlagGoal)", go.transform, new Vector3(0f, -200f, 0f), 0f, 1f, false, RrSpawnOptions.Visual(), inst =>
                {
                    f._stands[team] = inst.transform;
                    B.Tint(inst.gameObject, B.TeamColor(team), "Team", "Glow", "Goal");
                });
            }

            return f;
        }

        private RrInstanceState S
        {
            get { return RrNet.Instance; }
        }

        public int StateOf(int flag)
        {
            return S.Get(flag * Stride);
        }

        public int HolderOf(int flag)
        {
            return StateOf(flag) == Carried ? S.Get(flag * Stride + 1) - 1 : RrCombat.NoPlayer;
        }

        /// <summary>The flag (team) the player carries, or -1.</summary>
        public int CarriedBy(int playerId)
        {
            for (int f = 0; f < 2; f++)
            {
                if (StateOf(f) == Carried && HolderOf(f) == playerId)
                {
                    return f;
                }
            }

            return -1;
        }

        private Vector3 DropPoint(int flag)
        {
            int i = flag * Stride;
            return _room.transform.TransformPoint(new Vector3(S.Get(i + 2), S.Get(i + 3), S.Get(i + 4)) * 0.001f);
        }

        public Vector3 StandPoint(int flag)
        {
            return _room.transform.TransformPoint(_room.ActiveMap.FlagStands[flag]);
        }

        /// <summary>World position of the flag's foot now (home, dropped, or the carrier).</summary>
        public Vector3 FlagPoint(int flag)
        {
            return _flags[flag] != null ? _flags[flag].TransformPoint(_flagFoot[flag]) : StandPoint(flag);
        }

        // ------------------------------------------------------------------ authority

        public void AuthorityReset()
        {
            for (int f = 0; f < 2; f++)
            {
                SetFlag(f, Home, RrCombat.NoPlayer, Vector3.zero);
            }
        }

        private void SetFlag(int f, int state, int holder, Vector3 roomLocal)
        {
            int i = f * Stride;
            S.Set(i, state);
            S.Set(i + 1, holder + 1);
            S.Set(i + 2, Mathf.RoundToInt(roomLocal.x * 1000f));
            S.Set(i + 3, Mathf.RoundToInt(roomLocal.y * 1000f));
            S.Set(i + 4, Mathf.RoundToInt(roomLocal.z * 1000f));
        }

        /// <summary>Authority: a flag request from a member.</summary>
        public void AuthorityRequest(int sender, byte kind, int flag)
        {
            if (!_room.IsAuthority || _room.Phase != RrPhase.Playing || _room.Mode != RrPaintballRoom.ModeCtf || flag < 0 || flag > 1)
            {
                return;
            }

            int team = RrCombat.TeamOf(sender);
            if (team < 0 || IsOut(sender))
            {
                return;
            }

            int state = StateOf(flag);
            switch (kind)
            {
                case Take:
                    if (flag != team && state != Carried)
                    {
                        SetFlag(flag, Carried, sender, Vector3.zero);
                        RrLog.Info("Paintball", RrTeams.Name(flag) + " flag taken by player " + sender);
                    }

                    break;
                case Return:
                    if (flag == team && state == Dropped)
                    {
                        SetFlag(flag, Home, RrCombat.NoPlayer, Vector3.zero);
                        RrLog.Info("Paintball", RrTeams.Name(flag) + " flag returned by player " + sender);
                    }

                    break;
                case Capture:
                    if (flag != team && state == Carried && HolderOf(flag) == sender && StateOf(team) == Home)
                    {
                        SetFlag(flag, Home, RrCombat.NoPlayer, Vector3.zero);
                        RrLog.Info("Paintball", RrTeams.Name(team) + " captured the " + RrTeams.Name(flag) + " flag (player " + sender + ")");
                        _room.AuthorityCapture(team);
                    }

                    break;
            }
        }

        /// <summary>Authority: a carrier went out (or left) - the flag drops where they were.</summary>
        public void AuthorityDrop(int playerId, Vector3 worldPoint)
        {
            int f = CarriedBy(playerId);
            if (f < 0 || !_room.IsAuthority)
            {
                return;
            }

            Vector3 p = worldPoint;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 30f, 1 << RrLayers.Environment, QueryTriggerInteraction.Ignore))
            {
                p = hit.point;
            }

            SetFlag(f, Dropped, RrCombat.NoPlayer, _room.transform.InverseTransformPoint(p));
            RrLog.Info("Paintball", RrTeams.Name(f) + " flag dropped by player " + playerId + " at " + p);
        }

        /// <summary>Authority, every frame while playing: carriers that left or are out lose the flag.</summary>
        public void AuthorityStep()
        {
            for (int f = 0; f < 2; f++)
            {
                if (StateOf(f) != Carried)
                {
                    continue;
                }

                int holder = HolderOf(f);
                bool present = holder == RrNet.LocalId || RrNet.MembersOfMyInstance().Contains(holder);
                if (!present)
                {
                    var r = RrRemotePlayers.Get(holder);
                    if (r != null && r.Root != null)
                    {
                        AuthorityDrop(holder, r.Root.position);
                    }
                    else
                    {
                        SetFlag(f, Home, RrCombat.NoPlayer, Vector3.zero);
                    }
                }
                else if (IsOut(holder))
                {
                    AuthorityDrop(holder, PlayerFeet(holder));
                }
            }
        }

        private static bool IsOut(int playerId)
        {
            if (playerId == RrNet.LocalId)
            {
                return RrHealth.Local != null && RrHealth.Local.IsOut;
            }

            var p = RrNet.Presence(playerId);
            return p.Valid && p.Status == RrStatus.Down;
        }

        private static Vector3 PlayerFeet(int playerId)
        {
            if (playerId == RrNet.LocalId)
            {
                return RrPlayer.FeetPosition;
            }

            var r = RrRemotePlayers.Get(playerId);
            return r != null && r.Root != null ? r.Root.position : RrPlayer.FeetPosition;
        }

        // ------------------------------------------------------------------ every client

        private void Update()
        {
            if (_room == null || !_room.Built)
            {
                return;
            }

            bool ctf = _room.Mode == RrPaintballRoom.ModeCtf && _room.Phase != RrPhase.Lobby && _room.MapReady;
            PlaceStands(ctf);
            Announce(ctf && _room.Phase == RrPhase.Playing);
            if (ctf && _room.Phase == RrPhase.Playing)
            {
                LocalTouches();
            }
        }

        private void LateUpdate()
        {
            bool ctf = _room != null && _room.Built && _room.Mode == RrPaintballRoom.ModeCtf && _room.Phase != RrPhase.Lobby && _room.MapReady;
            for (int f = 0; f < 2; f++)
            {
                if (_flags[f] == null)
                {
                    continue;
                }

                _flags[f].gameObject.SetActive(ctf);
                if (ctf)
                {
                    PlaceFlag(f);
                }
            }
        }

        private void PlaceStands(bool ctf)
        {
            int map = ctf ? _room.MapIndex : -1;
            object level = ctf ? _room.MapLevel : null;
            if (map == _standsMap && ReferenceEquals(level, _standsLevel))
            {
                return;
            }

            _standsMap = map;
            _standsLevel = level;
            for (int t = 0; t < 2; t++)
            {
                if (_stands[t] == null)
                {
                    _standsMap = -2;
                    continue;
                }

                _stands[t].gameObject.SetActive(ctf);
                if (ctf)
                {
                    _stands[t].position = StandPoint(t);
                    _stands[t].rotation = _room.transform.rotation * _room.ActiveMap.FlagRotations[t];
                }
            }
        }

        private void PlaceFlag(int f)
        {
            var t = _flags[f];
            int state = StateOf(f);
            Vector3 foot;
            Quaternion rot = _room.transform.rotation;
            if (state == Carried)
            {
                int holder = HolderOf(f);
                if (holder == RrNet.LocalId)
                {
                    PlaceLocalCarry(t);
                    return;
                }

                var r = RrRemotePlayers.Get(holder);
                if (r != null && r.LeftHand != null)
                {
                    // Their Bridge hand is their controller anchor (= the grip pose): the same carry on their drawn hand.
                    Pose g = RrAvatarHand.GripToRecRoomDefault(RrHandSide.Left);
                    Quaternion hand = r.LeftHand.rotation * g.rotation;
                    t.SetPositionAndRotation(r.LeftHand.position + r.LeftHand.rotation * g.position + hand * CarryPosition, hand * CarryRotation);
                    return;
                }

                if (r != null && r.Root != null)
                {
                    rot = Quaternion.Euler(0f, r.Root.eulerAngles.y, 0f);
                    t.SetPositionAndRotation(r.Root.position + rot * new Vector3(-0.25f, 1.25f, -0.25f), rot * Quaternion.Euler(-12f, 0f, 10f));
                    return;
                }

                foot = StandPoint(f);
            }
            else if (state == Dropped)
            {
                foot = DropPoint(f);
            }
            else
            {
                // Home: exactly where the level keeps its (FlagTool) on the stand.
                var map = _room.ActiveMap;
                if (!float.IsNaN(map.FlagHomes[f].x))
                {
                    t.SetPositionAndRotation(_room.transform.TransformPoint(map.FlagHomes[f]), _room.transform.rotation * map.FlagHomeRotations[f]);
                    return;
                }

                foot = StandPoint(f) + Vector3.up * 0.3f;
            }

            t.SetPositionAndRotation(foot - rot * _flagFoot[f], rot);
        }

        private void PlaceLocalCarry(Transform t)
        {
            var left = RrHand.Left;
            if (RrPlayer.InVr && left != null && left.Grip != null)
            {
                Pose carry = Interact.RrGrab.WorldHoldPose(left, CarryPosition, CarryRotation);
                t.SetPositionAndRotation(carry.position, carry.rotation);
                return;
            }

            // Screen mode: over the left shoulder, lower left of the view, tilted back so the cloth shows.
            var cam = RrPlayer.Camera;
            if (cam != null)
            {
                var c = cam.transform;
                t.SetPositionAndRotation(c.position + c.right * -0.42f + c.up * -0.55f + c.forward * 0.55f,
                                         Quaternion.LookRotation(c.forward, c.up) * Quaternion.Euler(-25f, 0f, 18f));
                return;
            }

            var rot = Quaternion.Euler(0f, RrPlayer.Yaw, 0f);
            t.SetPositionAndRotation(RrPlayer.FeetPosition + rot * new Vector3(-0.25f, 1.25f, -0.25f), rot);
        }

        /// <summary>Local player: touch flags, ask to take / return / capture (repeated until the ints show it).</summary>
        private void LocalTouches()
        {
            if (RrHealth.Local != null && RrHealth.Local.IsOut)
            {
                return;
            }

            int me = RrNet.LocalId;
            int team = RrTeams.LocalTeam;
            if (team < 0 || team > 1 || Time.time < _nextAsk)
            {
                return;
            }

            int enemy = 1 - team;
            Vector3 feet = RrPlayer.FeetPosition;
            int enemyState = StateOf(enemy);
            if (enemyState != Carried && Touching(enemy, feet))
            {
                Ask(Take, enemy);
                return;
            }

            if (StateOf(team) == Dropped && Touching(team, feet))
            {
                Ask(Return, team);
                return;
            }

            if (enemyState == Carried && HolderOf(enemy) == me && StateOf(team) == Home && Flat(feet - StandPoint(team)) < CaptureRadius)
            {
                Ask(Capture, enemy);
            }
        }

        private bool Touching(int flag, Vector3 feet)
        {
            Vector3 p = FlagPoint(flag);
            if (Flat(feet - p) < TouchRadius && Mathf.Abs(feet.y - p.y) < 2.5f)
            {
                return true;
            }

            return Grabs(RrHand.Left, p) || Grabs(RrHand.Right, p);
        }

        private static bool Grabs(RrHand hand, Vector3 foot)
        {
            if (hand == null || !RrPlayer.InVr || !hand.GripHeld || hand.Grip == null)
            {
                return false;
            }

            Vector3 g = hand.Grip.position;
            return Flat(g - foot) < 0.35f && g.y > foot.y && g.y < foot.y + 2.3f;
        }

        private static float Flat(Vector3 d)
        {
            d.y = 0f;
            return d.magnitude;
        }

        private void Ask(byte kind, int flag)
        {
            _nextAsk = Time.time + 0.3f;
            RrNet.Send(RrPaintballRoom.EvFlag, new[] { kind, (byte)flag }, true);
        }

        /// <summary>Plays Rec Room's CTF VO on the transitions this client sees (PaintballManager fields, §7.1).</summary>
        private void Announce(bool live)
        {
            for (int f = 0; f < 2; f++)
            {
                int state = StateOf(f);
                int holder = HolderOf(f);
                int was = _seenState[f];
                bool changed = state != was || (state == Carried && holder != _seenHolder[f]);
                _seenState[f] = state;
                _seenHolder[f] = holder;
                if (!changed || !live || was < 0)
                {
                    continue;
                }

                string team = f == 0 ? "Blue" : "Red";
                if (state == Carried)
                {
                    Vo("FlagTaken", team + " flag taken");
                    if (holder == RrNet.LocalId)
                    {
                        var hand = RrHand.Left ?? RrHand.Right;
                        if (hand != null)
                        {
                            hand.Haptic(0.5f, 0.15f);
                        }
                    }
                }
                else if (state == Dropped)
                {
                    Vo(null, team + " flag dropped");
                }
                else if (was == Dropped)
                {
                    Vo("FlagReturn01", team + " flag returned");
                }
                else if (was == Carried)
                {
                    Vo("ScoreSFX", RrAnnouncer.TeamScores(1 - f));
                }
            }
        }

        private void Vo(string sfx, string line)
        {
            if (!string.IsNullOrEmpty(sfx))
            {
                RrSound.PlayUi(sfx, 0.7f);
            }

            // audioFXMinInterval 1.5 s: queue the line behind the previous one.
            float at = Mathf.Max(Time.time + 0.35f, _nextVo);
            _nextVo = at + 1.5f;
            _room.SayAt(line, at);
        }

        /// <summary>Forget the seen states (a new visit or match) so no stale VO plays.</summary>
        public void ResetSeen()
        {
            _seenState[0] = _seenState[1] = -1;
            _seenHolder[0] = _seenHolder[1] = -1;
            _standsMap = -1;
        }

        public bool Ready
        {
            get { return _flags[0] != null && _flags[1] != null; }
        }

        public Transform FlagTransform(int flag)
        {
            return flag >= 0 && flag < 2 ? _flags[flag] : null;
        }

        private void OnDestroy()
        {
            Array.Clear(_flags, 0, 2);
        }
    }
}
