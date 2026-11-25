using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// The Net module's offline behaviour checked inside Unity (play-mode harness: <c>{"call":
    /// "SomniumSpace.Worlds.SomniumRecRoom.Net.RrNetSelfTest.Run"}</c>): location and binding (k = activity - 1),
    /// authority, instance state and its timer, event loop-back, JoinInstance, actor blocks and a prop body without a
    /// network. Logs "Net self-test: all N checks passed" or the failures. Never run by the world itself; it restores
    /// the location it found.
    /// </summary>
    public static class RrNetSelfTest
    {
        private static int _checks, _fails;

        public static IEnumerator Run()
        {
            _checks = 0;
            _fails = 0;
            var core = RrNet.Core;
            var before = (RrActivity)core.Activity;
            int beforeNumber = core.Number;
            var beforeStatus = (RrStatus)core.Status;

            Check(!RrNet.Online, "offline in the harness");
            RrNet.SetLocation(RrActivity.Paintball, 0, RrStatus.Lobby);
            yield return null;
            Check(RrNet.InstanceObjectIndex == (int)RrActivity.Paintball - 1, "offline instance object = activity - 1 (" + RrNet.InstanceObjectIndex + ")");
            Check(RrNet.IsInstanceAuthority, "authority offline");
            List<int> members = RrNet.MembersOfMyInstance();
            Check(members.Count == 1 && members[0] == RrNet.LocalId, "members = the local player");
            Check(RrNet.Presence(RrNet.LocalId).Activity == RrActivity.Paintball && RrNet.Presence(RrNet.LocalId).Status == RrStatus.Lobby, "own presence view");

            RrInstanceState s = RrNet.Instance;
            s.SetPhase(RrPhase.Playing, 5f);
            s.Round = 3;
            s.Set(3, 42);
            s.SetTeamScore(1, 9);
            s.SetPlayerScore(RrNet.LocalId, 7);
            Check(s.Phase == RrPhase.Playing && s.Round == 3 && s.Get(3) == 42 && s.TeamScore(1) == 9 && s.PlayerScore(RrNet.LocalId) == 7, "instance state writes");
            Check(s.TimeRemaining > 4.5f && s.TimeRemaining <= 5f, "timer counts down (" + s.TimeRemaining.ToString("0.00") + " s)");

            int got = 0, sender = -1, length = -1;
            Action<int, byte[]> handler = (from, body) =>
            {
                got++;
                sender = from;
                length = body.Length;
            };
            RrNet.On(201, handler);
            RrNet.Send(201, new byte[] { 1, 2, 3 });
            RrNet.Send(201, new byte[] { 4 }, true);
            yield return null;
            yield return null;
            Check(got == 2 && sender == RrNet.LocalId, "events loop back (" + got + " of 2, last " + length + " bytes)");
            RrNet.Off(201, handler);
            RrNet.Send(201, new byte[] { 5 });
            yield return null;
            yield return null;
            Check(got == 2, "Off unsubscribes");

            int joined = -2;
            RrNet.JoinInstance(RrActivity.Dodgeball, -1, n => joined = n);
            yield return null;
            Check(joined == 0 && RrNet.InstanceObjectIndex == (int)RrActivity.Dodgeball - 1, "JoinInstance offline -> #" + joined + ", object " + RrNet.InstanceObjectIndex);
            Check(s.Phase == RrPhase.Lobby && s.Round == 0, "a new instance starts with a clean state");

            RrNetActors block = RrNetActors.Get(250);
            Vector3 at = new Vector3(1f, 2f, 3f) + RrPlacement.RoomOrigin;
            block.Put(0, new RrActorState { Position = at, Yaw = 90f, Type = 2, Health = 100, Flags = RrActorWire.FlagUsed | RrActorWire.FlagAlive });
            block.SetBit(100, true);
            RrActorState a = block.Actor(0);
            Check(!block.Networked && (a.Position - at).magnitude < 0.02f && Mathf.Abs(Mathf.DeltaAngle(a.Yaw, 90f)) < 0.1f && a.Alive, "actor block local put/get");
            Check(block.GetBit(100) && !block.GetBit(101), "actor bitset");
            RrNetActors.Release(250);

            var go = new GameObject("Rr Net Self Test Prop");
            go.AddComponent<Rigidbody>().useGravity = false;
            RrNetBody prop = go.AddComponent<RrNetBody>();
            prop.LocalPropId = 250;
            yield return null;
            int throwBefore = prop.ThrowId;
            prop.LocalGrab(RrHandSide.Right, Vector3.zero, Quaternion.identity);
            prop.LocalRelease();
            Check(!prop.Networked && prop.LocallyControlled && prop.HolderId == -1 && prop.ThrowId == throwBefore + 1, "prop body without a network is local physics");
            UnityEngine.Object.Destroy(go);

            RrNet.SetLocation(before, beforeNumber, beforeStatus);
            yield return null;
            string result = _fails == 0 ? "all " + _checks + " checks passed" : _fails + " of " + _checks + " checks failed";
            RrLog.Info("Net", "Net self-test: " + result);
        }

        private static void Check(bool ok, string what)
        {
            _checks++;

            if (!ok)
            {
                _fails++;
                RrLog.Warn("Net", "self-test FAILED: " + what);
            }
        }
    }
}
