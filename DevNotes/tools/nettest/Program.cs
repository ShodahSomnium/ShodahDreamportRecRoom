using System;
using System.Collections.Generic;
using System.Linq;
using SomniumSpace.Worlds.SomniumRecRoom.Net;

// Offline test of the Net module's pure layer (RrNetCore + claims, election, leases, grab claim) against a simulated
// Fusion shared-mode room: last authority request wins, writes without authority are dropped, every message is
// delayed, state changes reach the others after a delay, a client that loses authority is resynced to the server.
namespace NetTest
{
    internal sealed class Msg
    {
        public double At;
        public long Seq;
        public Action Do;
    }

    internal sealed class Client
    {
        public int Id;
        public Room Room;
        public bool Frozen;
        public RrNetCore Core = new RrNetCore();
        public RrGrabClaim Grab = new RrGrabClaim();
        public List<string> Notes = new List<string>();

        public int[][] View(int kind)
        {
            return kind == 0 ? Core.Slot : kind == 1 ? Core.Inst : kind == 2 ? Core.Prop : Core.Block;
        }

        public bool[] Auth(int kind)
        {
            return kind == 0 ? Core.SlotAuth : kind == 1 ? Core.InstAuth : kind == 2 ? Core.PropAuth : Core.BlockAuth;
        }

        public bool[] Valid(int kind)
        {
            return kind == 0 ? Core.SlotValid : kind == 1 ? Core.InstValid : kind == 2 ? Core.PropValid : Core.BlockValid;
        }

        public void Step(double now)
        {
            Core.LocalId = Id;
            Core.Up = true;
            Core.Step(now);

            foreach (RrNetCore.Ask a in Core.Asks)
            {
                Room.Request(this, a.Kind, a.Index);
            }

            foreach (RrNetCore.Write w in Core.Writes)
            {
                Write(w.Kind, w.Index, w.Record);
            }

            foreach (string n in Core.Notes)
            {
                Notes.Add(now.ToString("0.00") + " p" + Id + ": " + n);
            }

            Core.Notes.Clear();
            StepGrab(now);
        }

        public bool Write(int kind, int index, int[] record)
        {
            if (!Auth(kind)[index])
            {
                return false;
            }

            Array.Copy(record, View(kind)[index], record.Length);
            Room.Write(this, kind, index, (int[])record.Clone());
            return true;
        }

        // The RrNetBody grab logic, reduced to what the race needs.
        public int GrabObject = -1;

        public void StepGrab(double now)
        {
            if (GrabObject < 0)
            {
                return;
            }

            int p = GrabObject;
            Grab.Step(now, Core.PropAuth[p], Core.Prop[p], Id, Core.Present);

            if (Grab.Ask && !Core.PropAuth[p])
            {
                Room.Request(this, RrNetCore.KindProp, p);
            }

            if (Grab.WriteHeld)
            {
                var r = (int[])Core.Prop[p].Clone();
                r[RrPropWire.AtState] = RrPropWire.PackState(RrPropWire.StateHeld, false, RrPropWire.Epoch(r), Id);
                r[RrPropWire.AtSeq]++;
                Write(RrNetCore.KindProp, p, r);
            }

            if (Grab.Drop)
            {
                Notes.Add(now.ToString("0.00") + " p" + Id + ": grab lost - " + Grab.Why);
                GrabObject = -1;
            }
        }
    }

    internal sealed class Room
    {
        public double Now;
        public double Delay = 0.06;
        public readonly SortedDictionary<int, Client> Clients = new SortedDictionary<int, Client>();
        public readonly int[][][] Ints = new int[4][][];
        public readonly int[][] Holder = new int[4][];
        public int Accepted, Dropped, Requests;
        private readonly List<Msg> _queue = new List<Msg>();
        private long _seq;
        private double _masterAt = 1.0;
        private static readonly int[] Counts = { RrPresenceWire.Slots, RrInstanceWire.Objects, RrPropWire.Objects, RrActorWire.Objects };
        private static readonly int[] Sizes = { RrPresenceWire.Ints, RrInstanceWire.Ints, RrPropWire.Ints, RrActorWire.Ints };

        public Room()
        {
            for (int kind = 0; kind < 4; kind++)
            {
                Ints[kind] = new int[Counts[kind]][];
                Holder[kind] = new int[Counts[kind]];

                for (int i = 0; i < Counts[kind]; i++)
                {
                    Ints[kind][i] = new int[Sizes[kind]];
                    Holder[kind][i] = -1;
                }
            }
        }

        private void Later(double delay, Action a)
        {
            _queue.Add(new Msg { At = Now + delay, Seq = _seq++, Do = a });
        }

        public void Run(double seconds, double step = 0.05)
        {
            double end = Now + seconds;

            while (Now < end - 1e-9)
            {
                Now += step;

                while (true)
                {
                    var due = _queue.Where(m => m.At <= Now + 1e-9).OrderBy(m => m.At).ThenBy(m => m.Seq).ToList();

                    if (due.Count == 0)
                    {
                        break;
                    }

                    foreach (Msg m in due)
                    {
                        _queue.Remove(m);
                        m.Do();
                    }
                }

                // SceneNetworking.SlowLoop: once a second the shared-mode master (here the longest-present player)
                // requests every object nobody holds (ReassignNullObjectsAuthority).
                if (Now >= _masterAt && Clients.Count > 0)
                {
                    _masterAt = Now + 1.0;
                    Client master = Clients.Values.First();

                    for (int kind = 0; kind < 4; kind++)
                    {
                        for (int i = 0; i < Counts[kind]; i++)
                        {
                            if (Holder[kind][i] < 0)
                            {
                                Request(master, kind, i);
                            }
                        }
                    }
                }

                foreach (Client c in Clients.Values.ToList())
                {
                    if (!c.Frozen)
                    {
                        c.Step(Now);
                    }
                }
            }
        }

        public Client Join(int id)
        {
            var c = new Client { Id = id, Room = this };
            Clients.Add(id, c);
            c.Core.Present.Add(id);

            Later(Delay, () =>
            {
                for (int kind = 0; kind < 4; kind++)
                {
                    for (int i = 0; i < Counts[kind]; i++)
                    {
                        c.Valid(kind)[i] = true;
                        Array.Copy(Ints[kind][i], c.View(kind)[i], Sizes[kind]);

                        if (kind == 2)
                        {
                            c.Core.PropHolder[i] = Holder[kind][i];
                        }
                    }
                }

                c.Core.Present.Clear();
                c.Core.Present.AddRange(Clients.Keys);
            });

            foreach (Client other in Clients.Values)
            {
                if (other != c)
                {
                    Client o = other;
                    Later(Delay, () =>
                    {
                        if (!o.Core.Present.Contains(id))
                        {
                            o.Core.Present.Add(id);
                            o.Core.Present.Sort();
                        }
                    });
                }
            }

            return c;
        }

        public void Leave(int id)
        {
            Clients.Remove(id);

            for (int kind = 0; kind < 4; kind++)
            {
                for (int i = 0; i < Counts[kind]; i++)
                {
                    if (Holder[kind][i] == id)
                    {
                        Holder[kind][i] = -1;
                        BroadcastHolder(kind, i);
                    }
                }
            }

            foreach (Client other in Clients.Values)
            {
                Client o = other;
                Later(Delay, () => o.Core.Present.Remove(id));
            }
        }

        public void Request(Client c, int kind, int i)
        {
            Requests++;
            Later(Delay, () =>
            {
                if (!Clients.ContainsKey(c.Id))
                {
                    return;
                }

                Holder[kind][i] = c.Id;
                BroadcastHolder(kind, i);
            });
        }

        private void BroadcastHolder(int kind, int i)
        {
            int holder = Holder[kind][i];

            foreach (Client other in Clients.Values)
            {
                Client o = other;
                Later(Delay, () =>
                {
                    bool had = o.Auth(kind)[i];
                    bool has = holder == o.Id;
                    o.Auth(kind)[i] = has;

                    if (kind == 2)
                    {
                        o.Core.PropHolder[i] = holder;
                    }

                    if (had && !has)
                    {
                        Array.Copy(Ints[kind][i], o.View(kind)[i], Sizes[kind]);
                    }
                });
            }
        }

        public void Write(Client c, int kind, int i, int[] record)
        {
            Later(Delay, () =>
            {
                if (Holder[kind][i] != c.Id)
                {
                    Dropped++;
                    return;
                }

                Accepted++;
                Array.Copy(record, Ints[kind][i], Sizes[kind]);
                var snap = (int[])record.Clone();

                foreach (Client other in Clients.Values)
                {
                    if (other == c)
                    {
                        continue;
                    }

                    Client o = other;
                    Later(Delay, () =>
                    {
                        if (!o.Auth(kind)[i])
                        {
                            Array.Copy(snap, o.View(kind)[i], Sizes[kind]);
                        }
                    });
                }
            });
        }
    }

    internal static class Program
    {
        private static int _fails, _checks;
        private const int Paintball = 13, Dodgeball = 7;

        private static void Check(bool ok, string what)
        {
            _checks++;

            if (!ok)
            {
                _fails++;
            }

            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        }

        private static void Go(Client c, int activity, int number, int status)
        {
            c.Core.Activity = activity;
            c.Core.Number = number;
            c.Core.Status = status;
            c.Core.Networked = true;
        }

        private static void Dump(IEnumerable<Client> clients, int max = 6)
        {
            foreach (Client c in clients)
            {
                foreach (string n in c.Notes.Take(max))
                {
                    Console.WriteLine("        " + n);
                }

                c.Notes.Clear();
            }
        }

        private static int Main()
        {
            var room = new Room();

            // 1. three clients join a few hundred ms apart and claim presence slots
            Console.WriteLine("1. three clients join");
            Client a = room.Join(1);
            room.Run(0.3);
            Client b = room.Join(2);
            room.Run(0.3);
            Client c = room.Join(3);
            room.Run(5.0);
            var slots = new[] { a, b, c }.Select(x => x.Core.SlotIndex).ToArray();
            Check(slots.All(s => s >= 0) && slots.Distinct().Count() == 3, "distinct owned slots " + string.Join(",", slots));
            Check(new[] { a, b, c }.All(x => Enumerable.Range(0, 50).Count(s => RrPresenceWire.Listed(x.Core.Slot[s], x.Core.Present)) == 3),
                "every client lists 3 slots");
            Dump(new[] { a }, 2);

            // 2. simultaneous claims: three ids that all start their search at slot 5
            Console.WriteLine("2. simultaneous claims (ids 105, 155, 205 all start at slot 5)");
            var sim = new[] { room.Join(105), room.Join(155), room.Join(205) };
            room.Run(10.0);
            var simSlots = sim.Select(x => x.Core.SlotIndex).ToArray();
            var allSlots = room.Clients.Values.Select(x => x.Core.SlotIndex).ToArray();
            Check(simSlots.All(s => s >= 0) && allSlots.Distinct().Count() == allSlots.Length, "all 6 own distinct slots " + string.Join(",", allSlots));
            Check(room.Clients.Values.All(x => Enumerable.Range(0, 50).Count(s => RrPresenceWire.Listed(x.Core.Slot[s], x.Core.Present)) == 6),
                "no duplicate or missing listings (6 each)");
            Dump(sim, 4);

            foreach (Client x in sim)
            {
                room.Leave(x.Id);
            }

            room.Run(2.0);
            Check(Enumerable.Range(0, 50).Count(s => RrPresenceWire.Written(room.Ints[0][s]) && RrPresenceWire.OwnerOf(room.Ints[0][s]) >= 0 &&
                                                     new[] { 105, 155, 205 }.Contains(RrPresenceWire.OwnerOf(room.Ints[0][s]))) == 0,
                "leavers' slots written free by housekeeping");

            // 3. three members enter Paintball #0: one binding, the lowest id runs it
            Console.WriteLine("3. instance binding and election");
            foreach (Client x in new[] { a, b, c })
            {
                Go(x, Paintball, 0, RrPresenceWire.StatusLoading);
            }

            room.Run(3.0);

            foreach (Client x in new[] { a, b, c })
            {
                x.Core.Status = RrPresenceWire.StatusLobby;
            }

            room.Run(2.0);
            var bound = new[] { a, b, c }.Select(x => x.Core.Bound).ToArray();
            Check(bound.All(k => k >= 0) && bound.Distinct().Count() == 1, "all bound to the same instance object " + string.Join(",", bound));
            Check(a.Core.IsAuthority && !b.Core.IsAuthority && !c.Core.IsAuthority, "player 1 (lowest id) is the only authority");
            Check(new[] { a, b, c }.All(x => x.Core.Members.SequenceEqual(new[] { 1, 2, 3 })), "members agree: 1,2,3");
            int k0 = bound[0];

            // the authority runs a round: phase Playing, round 2, scores 3:5, 60 s timer
            a.Core.State[RrInstanceWire.AtAuthority] = RrWire.With(RrWire.With(a.Core.State[RrInstanceWire.AtAuthority], 16, 8, 2), 24, 8, 2);
            RrInstanceWire.SetTeamScore(a.Core.State, 0, 3);
            RrInstanceWire.SetTeamScore(a.Core.State, 1, 5);
            a.Core.SetTimer(room.Now, 60.0);
            double timerEnd = room.Now + 60.0;
            room.Run(1.5);
            Check(RrInstanceWire.Round(b.Core.State) == 2 && RrInstanceWire.TeamScore(c.Core.State, 1) == 5, "members read round 2 and score 5");
            Check(Math.Abs(b.Core.TimerEnd - timerEnd) < 0.3 && Math.Abs(c.Core.TimerEnd - timerEnd) < 0.3,
                "members' local timer end within 0.3 s (" + (b.Core.TimerEnd - timerEnd).ToString("0.000") + " s)");
            int writesBefore = room.Accepted;
            room.Run(5.0);
            double perSecond = (room.Accepted - writesBefore) / 5.0;
            Console.WriteLine("        idle traffic: " + perSecond.ToString("0.0") + " accepted writes/s (heartbeat)");
            Dump(new[] { a, b, c }, 3);

            // 4. the authority leaves mid-round
            Console.WriteLine("4. authority leaves mid-round");
            double leftAt = room.Now;
            room.Leave(1);
            double tookAt = -1;

            for (int i = 0; i < 60 && tookAt < 0; i++)
            {
                room.Run(0.05);

                if (RrInstanceWire.AuthorityOf(room.Ints[1][k0]) == 2)
                {
                    tookAt = room.Now;
                }
            }

            room.Run(2.0);
            Check(tookAt > 0, "player 2 took over in " + ((tookAt - leftAt) * 1000).ToString("0") + " ms");
            Check(b.Core.IsAuthority && !c.Core.IsAuthority && c.Core.Acting == 2, "2 runs it, 3 agrees");
            Check(b.Core.Bound == k0 && c.Core.Bound == k0, "same instance object " + k0);
            Check(RrInstanceWire.Round(c.Core.State) == 2 && RrInstanceWire.TeamScore(c.Core.State, 0) == 3 && RrInstanceWire.TeamScore(c.Core.State, 1) == 5,
                "round and scores survived");
            Check(Math.Abs(c.Core.TimerEnd - timerEnd) < 0.5, "timer continues (" + (c.Core.TimerEnd - timerEnd).ToString("0.000") + " s off)");
            Dump(new[] { b }, 4);

            // 5. a late joiner
            Console.WriteLine("5. late joiner");
            Client d = room.Join(4);
            room.Run(2.0);
            Go(d, Paintball, 0, RrPresenceWire.StatusLoading);
            room.Run(1.0);
            d.Core.Status = RrPresenceWire.StatusLobby;
            room.Run(2.0);
            Check(d.Core.Bound == k0 && !d.Core.IsAuthority && d.Core.Acting == 2, "joins object " + k0 + ", authority stays 2");
            Check(RrInstanceWire.Round(d.Core.State) == 2 && RrInstanceWire.TeamScore(d.Core.State, 1) == 5 && Math.Abs(d.Core.TimerEnd - timerEnd) < 0.5,
                "reads round, score and timer (" + (d.Core.TimerEnd - timerEnd).ToString("0.000") + " s off)");
            Check(new[] { b, c, d }.All(x => x.Core.Members.SequenceEqual(new[] { 2, 3, 4 })), "members agree: 2,3,4");

            // 6. a hung authority (present but silent): the next member takes over after 3 s, the old one stands down
            Console.WriteLine("6. hung authority");
            b.Frozen = true;
            double hungAt = room.Now;
            double tookHung = -1;

            for (int i = 0; i < 120 && tookHung < 0; i++)
            {
                room.Run(0.05);

                if (RrInstanceWire.AuthorityOf(room.Ints[1][k0]) == 3)
                {
                    tookHung = room.Now;
                }
            }

            Check(tookHung > 0, "player 3 took over after " + (tookHung - hungAt).ToString("0.00") + " s of silence");
            b.Frozen = false;
            room.Run(3.0);
            Check(!b.Core.IsAuthority && c.Core.IsAuthority && d.Core.Acting == 3 && RrInstanceWire.AuthorityOf(room.Ints[1][k0]) == 3,
                "2 comes back and stands down; 3 keeps it");
            Dump(new[] { b, c }, 3);

            // 7. binding race: two new instances whose keys start at the same object
            Console.WriteLine("7. instance binding race");
            int keyA = RrInstanceWire.Key(Dodgeball, 0);
            int start = RrInstanceWire.StartIndex(keyA, 24);
            int other = -1;

            for (int n = 1; n < 255 && other < 0; n++)
            {
                if (RrInstanceWire.StartIndex(RrInstanceWire.Key(Dodgeball, n), 24) == start)
                {
                    other = n;
                }
            }

            Client e = room.Join(10);
            Client f = room.Join(11);
            room.Run(2.0);
            Go(e, Dodgeball, 0, RrPresenceWire.StatusLobby);
            Go(f, Dodgeball, other, RrPresenceWire.StatusLobby);
            room.Run(8.0);
            Check(e.Core.Bound >= 0 && f.Core.Bound >= 0 && e.Core.Bound != f.Core.Bound && e.Core.Bound != k0 && f.Core.Bound != k0,
                "Dodgeball #0 -> " + e.Core.Bound + ", Dodgeball #" + other + " -> " + f.Core.Bound + " (both started at " + start + ")");
            Check(e.Core.IsAuthority && f.Core.IsAuthority, "each runs its own");
            Check(RrInstanceWire.KeyOf(room.Ints[1][k0]) == RrInstanceWire.Key(Paintball, 0), "the Paintball binding is untouched");
            Dump(new[] { e, f }, 4);

            // 8. prop grab race: the authority leases prop 0, then 3 and 4 grab it at the same moment
            Console.WriteLine("8. prop lease and grab race");
            foreach (Client x in new[] { b, c, d })
            {
                x.Core.NeedProps.Add(0);
            }

            room.Run(3.0);
            int p = c.Core.PropOf[0];
            Check(p >= 0 && b.Core.PropOf[0] == p && d.Core.PropOf[0] == p, "prop 0 leased to object " + p + ", all agree");
            int acceptedBefore = room.Accepted;
            c.GrabObject = p;
            d.GrabObject = p;
            c.Grab.Begin(room.Now);
            d.Grab.Begin(room.Now);
            room.Run(4.0);
            int holders = new[] { c, d }.Count(x => x.Grab.Current == RrGrabClaim.Stage.Holding);
            int holder = c.Grab.Current == RrGrabClaim.Stage.Holding ? 3 : d.Grab.Current == RrGrabClaim.Stage.Holding ? 4 : -1;
            Check(holders == 1, "exactly one holder: player " + holder);
            Check(new[] { b, c, d }.All(x => RrPropWire.State(x.Core.Prop[p]) == RrPropWire.StateHeld && RrPropWire.Writer(x.Core.Prop[p]) == holder),
                "every view says Held by " + holder);
            Check(room.Holder[2][p] == holder, "the holder keeps the prop's state authority (no orphan adoption by the instance authority)");
            Dump(new[] { c, d }, 3);

            // 9. the holder leaves: the instance authority adopts the orphan
            Console.WriteLine("9. holder leaves");
            room.Leave(holder);
            room.Run(3.0);
            var rest = room.Clients.Values.Where(x => x.Id >= 2 && x.Id <= 4).ToList();
            int acting = rest[0].Core.Acting;
            Check(rest.All(x => x.Core.Acting == acting) && room.Holder[2][p] == acting,
                "orphaned prop adopted by the instance authority (prop holder " + room.Holder[2][p] + ", authority " + acting + ")");

            // 10. a two-player instance whose authority leaves: the last member keeps the object and the state
            Console.WriteLine("10. last member left alone");
            Client g = room.Join(12);
            room.Run(2.0);
            Go(g, Dodgeball, 0, RrPresenceWire.StatusLobby);
            room.Run(2.0);
            int kd = e.Core.Bound;
            e.Core.State[RrInstanceWire.AtAuthority] = RrWire.With(e.Core.State[RrInstanceWire.AtAuthority], 24, 8, 4);
            room.Run(1.0);
            Check(g.Core.Bound == kd && e.Core.IsAuthority && RrInstanceWire.Round(g.Core.State) == 4, "12 joined 10's Dodgeball #0 (object " + kd + ", round 4)");
            room.Leave(10);
            room.Run(3.0);
            Check(g.Core.Bound == kd && g.Core.IsAuthority && RrInstanceWire.AuthorityOf(room.Ints[1][kd]) == 12 && RrInstanceWire.Round(g.Core.State) == 4,
                "12 alone keeps object " + kd + " and round 4 (bound " + g.Core.Bound + ")");
            Dump(new[] { g }, 4);

            Console.WriteLine("writes accepted " + room.Accepted + ", dropped (no authority) " + room.Dropped + ", authority requests " + room.Requests);
            Console.WriteLine(_fails == 0 ? "ALL " + _checks + " CHECKS PASSED" : _fails + " OF " + _checks + " CHECKS FAILED");
            return _fails == 0 ? 0 : 1;
        }
    }
}
