using System;
using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// One client's networking brain, pure C# (no Unity or Fusion calls) so SP\nettest runs the very same code against a
    /// simulated room: presence slot claim and writes, instance binding, election and hand-over, instance state
    /// replication with a local-countdown timer, and the leases of props and actor blocks (somnium_networking.md
    /// §5.4-§5.8). The adapter (<see cref="RrNet"/> in the world, the fake room in the test) fills the room view -
    /// every object's ints as read, whether it is valid, whether this client holds its state authority, who does -
    /// calls <see cref="Step"/>, then carries out <see cref="Asks"/> (RequestStateAuthority) and <see cref="Writes"/>
    /// (only changed ints, only with authority). Everything it decides comes from ints every client reads alike.
    /// </summary>
    public sealed class RrNetCore
    {
        public const int KindSlot = 0;
        public const int KindInstance = 1;
        public const int KindProp = 2;
        public const int KindBlock = 3;

        /// <summary>After the room comes up, wait this long for the join snapshot before claiming anything (§5.6 snapshot race), s.</summary>
        public const double SnapshotWait = 1.0;

        /// <summary>The instance authority rewrites the header (seq + timer) at least this often: its heartbeat, s.</summary>
        public const double HeartbeatEvery = 1.0;

        /// <summary>Slot housekeeping (writing free the slots of players who left) runs this often, s.</summary>
        public const double HousekeepEvery = 0.25;

        /// <summary>Ask again for an object this client should hold but does not, s.</summary>
        public const double AskEvery = 1.0;

        /// <summary>An elected authority that cannot bind an instance object (all 24 busy) plays solo after this long, s.</summary>
        public const double SoloAfter = 10.0;

        public const int MaxLocalIds = 256;

        public struct Ask
        {
            public int Kind;
            public int Index;
        }

        public struct Write
        {
            public int Kind;
            public int Index;
            public int[] Record;
        }

        // ------------------------------------------------------------------ the room as this client sees it

        public readonly int[][] Slot = Make(RrPresenceWire.Slots, RrPresenceWire.Ints);
        public readonly bool[] SlotValid = new bool[RrPresenceWire.Slots];
        public readonly bool[] SlotAuth = new bool[RrPresenceWire.Slots];

        public readonly int[][] Inst = Make(RrInstanceWire.Objects, RrInstanceWire.Ints);
        public readonly bool[] InstValid = new bool[RrInstanceWire.Objects];
        public readonly bool[] InstAuth = new bool[RrInstanceWire.Objects];

        public readonly int[][] Prop = Make(RrPropWire.Objects, RrPropWire.Ints);
        public readonly bool[] PropValid = new bool[RrPropWire.Objects];
        public readonly bool[] PropAuth = new bool[RrPropWire.Objects];

        /// <summary>Per prop object: the PlayerId holding its Fusion state authority (-1 none).</summary>
        public readonly int[] PropHolder = Fill(RrPropWire.Objects, -1);

        public readonly int[][] Block = Make(RrActorWire.Objects, RrActorWire.Ints);
        public readonly bool[] BlockValid = new bool[RrActorWire.Objects];
        public readonly bool[] BlockAuth = new bool[RrActorWire.Objects];

        /// <summary>PlayerIds in the Fusion room, the local one included.</summary>
        public readonly List<int> Present = new List<int>();

        public int LocalId = -1;

        /// <summary>The room is joined and the scene objects are found.</summary>
        public bool Up;

        // ------------------------------------------------------------------ local intent (set by the owner)

        /// <summary>The own presence record, composed here and by the setters of RrNet; pushed while the slot is held.</summary>
        public readonly int[] Own = new int[RrPresenceWire.Ints];

        public int Activity;
        public int Number;
        public int Status;
        public int Team = -1;

        /// <summary>The current activity is played over the network (false: dorm, arrival, unnetworked rooms).</summary>
        public bool Networked;

        /// <summary>Local prop ids / actor block ids the current instance uses (leased by its authority).</summary>
        public readonly List<int> NeedProps = new List<int>();
        public readonly List<int> NeedBlocks = new List<int>();

        // ------------------------------------------------------------------ results

        /// <summary>The instance state: authoritative here while <see cref="IsAuthority"/>, else a copy of the bound object.</summary>
        public readonly int[] State = new int[RrInstanceWire.Ints];

        /// <summary>When the current phase's timer ends, on Step's clock (0 = no timer).</summary>
        public double TimerEnd;

        public double Now { get; private set; }

        public int Key { get; private set; } = -1;
        public int Bound { get; private set; } = -1;
        public int BoundGeneration { get; private set; }
        public int Elected { get; private set; } = -1;
        public int Acting { get; private set; } = -1;
        public bool IsAuthority { get; private set; } = true;

        /// <summary>Members of the local player's instance (participants of its key), ascending, the local one included.</summary>
        public readonly List<int> Members = new List<int>();

        /// <summary>Local prop id -> prop object p leased to the current instance (-1 none).</summary>
        public readonly int[] PropOf = Fill(MaxLocalIds, -1);

        /// <summary>Local block id -> actor object a leased to the current instance (-1 none).</summary>
        public readonly int[] BlockOf = Fill(MaxLocalIds, -1);

        public readonly List<Ask> Asks = new List<Ask>();
        public readonly List<Write> Writes = new List<Write>();

        /// <summary>Things worth one log line each, collected this step.</summary>
        public readonly List<string> Notes = new List<string>();

        /// <summary>Local prop ids whose lease this client wrote this step (their pose must follow at once).</summary>
        public readonly List<int> LeasedProps = new List<int>();

        // ------------------------------------------------------------------ private

        private readonly RrSlotClaim _slotClaim = new RrSlotClaim("presence slot");
        private readonly RrSlotClaim _bindClaim = new RrSlotClaim("instance object");
        private readonly bool[] _free = new bool[RrPresenceWire.Slots];
        private readonly int[] _owner = new int[RrPresenceWire.Slots];
        private readonly bool[] _named = new bool[RrInstanceWire.Objects];
        private readonly int[] _instSeq = new int[RrInstanceWire.Objects];
        private readonly double[] _instSeqAt = new double[RrInstanceWire.Objects];
        private readonly bool[] _instSeen = new bool[RrInstanceWire.Objects];
        private readonly int[] _instGen = new int[RrInstanceWire.Objects];
        private readonly bool[] _instLive = new bool[RrInstanceWire.Objects];
        private readonly bool[] _propFree = new bool[RrPropWire.Objects];
        private readonly bool[] _propMatch = new bool[RrPropWire.Objects];
        private readonly bool[] _blockFree = new bool[RrActorWire.Objects];
        private readonly bool[] _blockMatch = new bool[RrActorWire.Objects];
        private readonly double[] _propAskAt = new double[RrPropWire.Objects];
        private readonly double[] _blockAskAt = new double[RrActorWire.Objects];
        private readonly Dictionary<int, RrLease> _propLeases = new Dictionary<int, RrLease>();
        private readonly Dictionary<int, RrLease> _blockLeases = new Dictionary<int, RrLease>();
        private readonly List<int> _memberIds = new List<int>();
        private readonly List<bool> _memberLoading = new List<bool>();
        private readonly List<long> _sort = new List<long>();
        private readonly List<bool> _loading = new List<bool>();
        private double _upSince = -1.0, _houseAt, _beatAt, _instAskAt, _keySince, _timerSent = -1.0;
        private int _stateSeq = int.MinValue, _stateTimer = int.MinValue;

        public RrNetCore()
        {
            RrPresenceWire.SetWhere(Own, 0, 0, RrPresenceWire.StatusNone, -1, -1);
        }

        /// <summary>The presence slot this client owns (-1 while none is confirmed).</summary>
        public int SlotIndex
        {
            get { return _slotClaim.Current == RrSlotClaim.Stage.Owned ? _slotClaim.Slot : -1; }
        }

        /// <summary>The slot being claimed or held (-1 none): its record is this client's own.</summary>
        public int ClaimedSlot
        {
            get { return _slotClaim.Holding ? _slotClaim.Slot : -1; }
        }

        public double Silent(int k)
        {
            return k >= 0 && k < _instSeqAt.Length && _instSeen[k] ? Now - _instSeqAt[k] : 0.0;
        }

        public static int Stamp(double now)
        {
            return (int)(long)Math.Round(now * 1000.0);
        }

        // ------------------------------------------------------------------ step

        public void Step(double now)
        {
            Now = now;
            Asks.Clear();
            Writes.Clear();
            LeasedProps.Clear();

            if (!Up || LocalId < 0)
            {
                StepOffline(now);
                return;
            }

            if (_upSince < 0.0)
            {
                _upSince = now;
            }

            bool settled = now - _upSince >= SnapshotWait;
            TrackInstances(now);
            StepSlot(now, settled);
            StepInstance(now, settled);
            StepLeases(now, settled);
        }

        private void StepOffline(double now)
        {
            _upSince = -1.0;
            var input = new RrSlotClaim.Inputs { Now = now, LocalId = LocalId, Allowed = false };
            _slotClaim.Step(in input, SlotValid, SlotAuth, _free, _owner, Present);
            Note(_slotClaim.TakeNote());
            _bindClaim.Reset();
            UpdateKey(now);
            Bound = -1;
            Elected = LocalId;
            Acting = LocalId;
            IsAuthority = true;
            Members.Clear();
            Members.Add(Math.Max(0, LocalId));
            ClearMap(PropOf);
            ClearMap(BlockOf);
            RrPresenceWire.SetWhere(Own, Activity, Number, Status, Team, -1);
        }

        private void TrackInstances(double now)
        {
            for (int k = 0; k < RrInstanceWire.Objects; k++)
            {
                int seq = InstValid[k] && RrInstanceWire.Written(Inst[k]) ? Inst[k][RrInstanceWire.AtSeq] : 0;

                if (!_instSeen[k] || seq != _instSeq[k])
                {
                    _instSeen[k] = true;
                    _instSeq[k] = seq;
                    _instSeqAt[k] = now;
                }

                _named[k] = false;
            }

            int mine = ClaimedSlot;

            for (int s = 0; s < RrPresenceWire.Slots; s++)
            {
                int[] r = Slot[s];

                if (s == mine || !SlotValid[s] || !RrPresenceWire.Listed(r, Present) || RrPresenceWire.OwnerOf(r) == LocalId)
                {
                    continue;
                }

                int k = RrPresenceWire.InstanceObject(r);

                if (k >= 0 && k < RrInstanceWire.Objects && RrPresenceWire.Status(r) != RrPresenceWire.StatusNone)
                {
                    _named[k] = true;
                }
            }
        }

        // ------------------------------------------------------------------ presence slot

        private void StepSlot(double now, bool settled)
        {
            for (int i = 0; i < RrPresenceWire.Slots; i++)
            {
                _free[i] = SlotValid[i] && RrPresenceWire.FreeFor(Slot[i], LocalId, Present);
                _owner[i] = RrPresenceWire.OwnerOf(Slot[i]);
            }

            var input = new RrSlotClaim.Inputs
            {
                Now = now,
                LocalId = LocalId,
                Allowed = settled,
                Count = RrPresenceWire.Slots,
                Start = LocalId % RrPresenceWire.Slots
            };

            _slotClaim.Step(in input, SlotValid, SlotAuth, _free, _owner, Present);
            Note(_slotClaim.TakeNote());

            if (_slotClaim.AskAuthority >= 0)
            {
                Asks.Add(new Ask { Kind = KindSlot, Index = _slotClaim.AskAuthority });
            }

            int s = _slotClaim.Slot;

            if (_slotClaim.WriteClaim && s >= 0)
            {
                Own[RrPresenceWire.AtSeq] = Math.Max(Own[RrPresenceWire.AtSeq], Slot[s][RrPresenceWire.AtSeq]);
                RrPresenceWire.StampClaim(Own, LocalId, RrPresenceWire.GenerationOf(Slot[s]) + 1, Stamp(now));
            }

            RrPresenceWire.SetWhere(Own, Activity, Number, Status, Team, Bound);
            Own[RrPresenceWire.AtSpawn] = RrWire.With(Own[RrPresenceWire.AtSpawn], 0, 8, Bound >= 0 ? BoundGeneration : 0);

            if (_slotClaim.Holding && s >= 0 && SlotAuth[s])
            {
                PushOwn(s, now, _slotClaim.WriteClaim);
            }

            if (now >= _houseAt)
            {
                _houseAt = now + HousekeepEvery;

                for (int i = 0; i < RrPresenceWire.Slots; i++)
                {
                    bool claimedHere = i == s && _slotClaim.Holding;

                    if (RrPresenceWire.ToFree(Slot[i], SlotAuth[i], claimedHere, LocalId, Present))
                    {
                        int gone = RrPresenceWire.OwnerOf(Slot[i]);
                        var record = (int[])Slot[i].Clone();
                        RrPresenceWire.WriteFree(record, Stamp(now));
                        Writes.Add(new Write { Kind = KindSlot, Index = i, Record = record });
                        Note("presence slot " + i + " written free (player " + gone + (gone == LocalId ? ", a leftover of this client" : ", gone") + ")");
                    }
                }
            }
        }

        private void PushOwn(int s, double now, bool force)
        {
            int[] net = Slot[s];
            bool header = false;
            bool any = force;

            for (int i = RrPresenceWire.AtOwner; i < RrPresenceWire.Ints; i++)
            {
                if (Own[i] != net[i])
                {
                    any = true;
                    header |= i < RrPresenceWire.HeaderEnd;
                }
            }

            if (!any)
            {
                return;
            }

            if (header && !force)
            {
                Own[RrPresenceWire.AtSeq] = Math.Max(Own[RrPresenceWire.AtSeq], net[RrPresenceWire.AtSeq]) + 1;
                Own[RrPresenceWire.AtStamp] = Stamp(now);
            }

            Writes.Add(new Write { Kind = KindSlot, Index = s, Record = Own });
        }

        // ------------------------------------------------------------------ instance: members, binding, election

        private void UpdateKey(double now)
        {
            int key = Networked && Activity > 0 && Status != RrPresenceWire.StatusNone ? RrInstanceWire.Key(Activity, Number) : -1;

            if (key == Key)
            {
                return;
            }

            Key = key;
            _keySince = now;
            _bindClaim.Reset();
            Bound = -1;
            BoundGeneration = 0;
            Array.Clear(State, 0, State.Length);
            TimerEnd = 0.0;
            _timerSent = -1.0;
            _stateSeq = int.MinValue;
            _stateTimer = int.MinValue;
            ClearMap(PropOf);
            ClearMap(BlockOf);
            _propLeases.Clear();
            _blockLeases.Clear();
        }

        private void StepInstance(double now, bool settled)
        {
            UpdateKey(now);
            CollectMembers();
            Elected = Key >= 0 ? RrElection.Elect(Members, _loading) : LocalId;

            int bound = Key >= 0 ? FindBound(Key) : -1;

            if (bound != Bound)
            {
                if (bound >= 0)
                {
                    Note("instance " + RrInstanceWire.KeyActivity(Key) + "#" + RrInstanceWire.KeyNumber(Key) + " bound to instance object " + bound +
                         " (generation " + RrInstanceWire.GenerationOf(Inst[bound]) + ", authority player " + RrInstanceWire.AuthorityOf(Inst[bound]) + ")");
                }
                else if (Bound >= 0)
                {
                    Note("instance object " + Bound + " no longer carries this instance");
                }

                Bound = bound;
                _stateSeq = int.MinValue;
            }

            if (Bound >= 0)
            {
                BoundGeneration = RrInstanceWire.GenerationOf(Inst[Bound]);
                _bindClaim.Reset();
            }
            else if (Key >= 0 && Elected == LocalId && settled)
            {
                StepBind(now);
            }
            else
            {
                _bindClaim.Reset();
            }

            int header = Bound >= 0 ? RrInstanceWire.AuthorityOf(Inst[Bound]) : -1;
            Acting = Key < 0 ? LocalId : RrElection.Acting(LocalId, Members, Elected, Bound >= 0, header, Silent(Bound));
            bool authority = Key < 0 || (Bound >= 0 ? Acting == LocalId : Elected == LocalId || now - _keySince >= SoloAfter);

            if (authority != IsAuthority)
            {
                IsAuthority = authority;

                if (Key >= 0)
                {
                    Note(authority
                        ? "this client now runs the instance" + (Bound >= 0 && header >= 0 && header != LocalId ? " (taking over from player " + header + ")" : "")
                        : "player " + Acting + " runs the instance");
                }

                _instAskAt = now;
            }

            if (Bound < 0)
            {
                return;
            }

            if (!IsAuthority)
            {
                ReadState(now);
                return;
            }

            if (!InstAuth[Bound])
            {
                if (now >= _instAskAt)
                {
                    Asks.Add(new Ask { Kind = KindInstance, Index = Bound });
                    _instAskAt = now + AskEvery;
                }

                return;
            }

            PushState(now);
        }

        private void CollectMembers()
        {
            Members.Clear();
            _loading.Clear();
            _sort.Clear();

            if (Key < 0)
            {
                Members.Add(LocalId);
                _loading.Add(false);
                return;
            }

            int mine = ClaimedSlot;

            for (int s = 0; s < RrPresenceWire.Slots; s++)
            {
                int[] r = Slot[s];

                if (s == mine || !SlotValid[s] || !RrPresenceWire.Written(r))
                {
                    continue;
                }

                int owner = RrPresenceWire.OwnerOf(r);

                if (owner == LocalId || !RrPresenceWire.Participant(r, Present, Activity, Number, -1))
                {
                    continue;
                }

                AddSorted(owner, RrPresenceWire.Status(r) == RrPresenceWire.StatusLoading);
            }

            if (!RrPresenceWire.Flag(Own, RrPresenceWire.FlagNoGame))
            {
                AddSorted(LocalId, Status == RrPresenceWire.StatusLoading);
            }

            _sort.Sort();

            for (int i = 0; i < _sort.Count; i++)
            {
                long v = _sort[i];
                int id = (int)(v >> 1);

                if (Members.Count > 0 && Members[Members.Count - 1] == id)
                {
                    continue;
                }

                Members.Add(id);
                _loading.Add((v & 1) != 0);
            }
        }

        private void AddSorted(int id, bool loading)
        {
            _sort.Add(((long)id << 1) | (loading ? 1L : 0L));
        }

        /// <summary>
        /// The instance object carrying this key: the one whose authority is the elected member, else the one whose
        /// authority is a live participant, else the one most participants name. A header nobody else names that has
        /// been silent FreeAfterSilence is a leftover of an earlier game with the same key: not bound (it is rebound
        /// fresh). The local client's own binding counts while it runs the instance.
        /// </summary>
        private int FindBound(int key)
        {
            int best = -1;
            int bestScore = 0;

            for (int k = 0; k < RrInstanceWire.Objects; k++)
            {
                int[] r = Inst[k];

                if (!InstValid[k] || RrInstanceWire.KeyOf(r) != key)
                {
                    continue;
                }

                int authority = RrInstanceWire.AuthorityOf(r);
                double silent = Silent(k);
                int named = CountNaming(k);
                bool mine = authority == LocalId && (Bound == k || _bindClaim.Slot == k);

                if (!mine && named == 0 && Bound != k && silent >= RrInstanceWire.FreeAfterSilence)
                {
                    continue;
                }

                // Continuity: the object this client already plays in (the last member left after its authority).
                int score = named + (Bound == k ? 2 : 0);

                if (mine)
                {
                    score += 8;
                }
                else if (authority >= 0 && RrWire.Contains(Members, authority))
                {
                    score += silent < RrElection.StaleSeconds ? 6 : 4;
                }

                if (authority >= 0 && authority == Elected)
                {
                    score += 16;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = k;
                }
            }

            return best;
        }

        private int CountNaming(int k)
        {
            int n = 0;
            int mine = ClaimedSlot;

            for (int s = 0; s < RrPresenceWire.Slots; s++)
            {
                int[] r = Slot[s];

                if (s != mine && SlotValid[s] && RrPresenceWire.OwnerOf(r) != LocalId && RrPresenceWire.InstanceObject(r) == k &&
                    RrPresenceWire.Participant(r, Present, Activity, Number, k))
                {
                    n++;
                }
            }

            return n;
        }

        private void StepBind(double now)
        {
            for (int k = 0; k < RrInstanceWire.Objects; k++)
            {
                _free[k] = InstValid[k] && RrInstanceWire.FreeForBind(Inst[k], _named[k], LocalId, Present, Silent(k));
                _owner[k] = RrInstanceWire.AuthorityOf(Inst[k]);
            }

            var input = new RrSlotClaim.Inputs
            {
                Now = now,
                LocalId = LocalId,
                Allowed = true,
                Count = RrInstanceWire.Objects,
                Start = RrInstanceWire.StartIndex(Key, RrInstanceWire.Objects)
            };

            _bindClaim.Step(in input, InstValid, InstAuth, _free, _owner, Present);
            Note(_bindClaim.TakeNote());

            if (_bindClaim.AskAuthority >= 0)
            {
                Asks.Add(new Ask { Kind = KindInstance, Index = _bindClaim.AskAuthority });
            }

            int b = _bindClaim.Slot;

            if (!_bindClaim.WriteClaim || b < 0)
            {
                return;
            }

            // A fresh binding keeps whatever the game already set up locally while it waited (ints 4..63).
            var record = (int[])Inst[b].Clone();
            RrInstanceWire.WriteBind(record, Key, LocalId);
            record[RrInstanceWire.AtAuthority] = (State[RrInstanceWire.AtAuthority] & unchecked((int)0xFFFF0000)) | RrWire.Id16(LocalId);

            for (int i = RrInstanceWire.AtTimer; i < RrInstanceWire.Ints; i++)
            {
                record[i] = State[i];
            }

            record[RrInstanceWire.AtTimer] = RemainingMs(now);
            Array.Copy(record, State, State.Length);
            _timerSent = TimerEnd;
            _beatAt = now + HeartbeatEvery;
            Writes.Add(new Write { Kind = KindInstance, Index = b, Record = State });
        }

        // ------------------------------------------------------------------ instance state

        private int RemainingMs(double now)
        {
            return TimerEnd > now ? Math.Max(1, (int)Math.Round((TimerEnd - now) * 1000.0)) : 0;
        }

        private void ReadState(double now)
        {
            int[] r = Inst[Bound];
            int seq = r[RrInstanceWire.AtSeq];
            int timer = r[RrInstanceWire.AtTimer];

            if (seq != _stateSeq || timer != _stateTimer)
            {
                _stateSeq = seq;
                _stateTimer = timer;
                TimerEnd = timer > 0 ? now + timer / 1000.0 : 0.0;
            }

            Array.Copy(r, State, State.Length);
        }

        private void PushState(double now)
        {
            int[] net = Inst[Bound];

            if (!RrInstanceWire.Written(State) || RrInstanceWire.KeyOf(State) != Key)
            {
                // Became the authority of an object bound by someone else: continue from what it says.
                Array.Copy(net, State, State.Length);
            }

            State[RrInstanceWire.AtAuthority] = (State[RrInstanceWire.AtAuthority] & unchecked((int)0xFFFF0000)) | RrWire.Id16(LocalId);
            bool changed = false;

            for (int i = 0; i < RrInstanceWire.Ints; i++)
            {
                if (i != RrInstanceWire.AtSeq && i != RrInstanceWire.AtTimer && State[i] != net[i])
                {
                    changed = true;
                    break;
                }
            }

            if (!changed && TimerEnd == _timerSent && now < _beatAt)
            {
                return;
            }

            State[RrInstanceWire.AtSeq] = Math.Max(State[RrInstanceWire.AtSeq], net[RrInstanceWire.AtSeq]) + 1;
            State[RrInstanceWire.AtTimer] = RemainingMs(now);
            _timerSent = TimerEnd;
            _beatAt = now + HeartbeatEvery;
            Writes.Add(new Write { Kind = KindInstance, Index = Bound, Record = State });
        }

        /// <summary>Authority: sets the phase's timer (seconds from now, 0 = none).</summary>
        public void SetTimer(double now, double seconds)
        {
            TimerEnd = seconds > 0.0 ? now + seconds : 0.0;
        }

        // ------------------------------------------------------------------ leases (props, actor blocks)

        private void StepLeases(double now, bool settled)
        {
            if (Bound < 0)
            {
                ClearMap(PropOf);
                ClearMap(BlockOf);
                return;
            }

            for (int k = 0; k < RrInstanceWire.Objects; k++)
            {
                int[] r = Inst[k];
                bool written = InstValid[k] && RrInstanceWire.Written(r);
                int authority = written ? RrInstanceWire.AuthorityOf(r) : -1;
                _instGen[k] = written ? RrInstanceWire.GenerationOf(r) : -1;
                _instLive[k] = written && (_named[k] || (authority >= 0 && RrWire.Contains(Present, authority) && Silent(k) < RrInstanceWire.FreeAfterSilence));
            }

            _instLive[Bound] = true;
            MapProps();
            MapBlocks();

            if (!IsAuthority || !settled)
            {
                _propLeases.Clear();
                _blockLeases.Clear();
                return;
            }

            LeaseProps(now);
            LeaseBlocks(now);
            int mask = 0;

            for (int i = 0; i < NeedBlocks.Count; i++)
            {
                int a = BlockOf[NeedBlocks[i] & 0xFF];

                if (a >= 0)
                {
                    mask |= 1 << a;
                }
            }

            int props = 0;

            for (int i = 0; i < NeedProps.Count; i++)
            {
                props += PropOf[NeedProps[i] & 0xFF] >= 0 ? 1 : 0;
            }

            State[RrInstanceWire.AtActorMask] = mask;
            State[RrInstanceWire.AtPropCount] = RrWire.With(State[RrInstanceWire.AtPropCount], 0, 16, props);
        }

        private void MapProps()
        {
            ClearMap(PropOf);

            for (int i = 0; i < NeedProps.Count; i++)
            {
                int id = NeedProps[i] & 0xFF;

                for (int p = 0; p < RrPropWire.Objects; p++)
                {
                    if (PropValid[p] && RrPropWire.LeaseMatches(Prop[p], Bound, BoundGeneration, id))
                    {
                        PropOf[id] = p;
                        break;
                    }
                }
            }
        }

        private void MapBlocks()
        {
            ClearMap(BlockOf);

            for (int i = 0; i < NeedBlocks.Count; i++)
            {
                int id = NeedBlocks[i] & 0xFF;

                for (int a = 0; a < RrActorWire.Objects; a++)
                {
                    if (BlockValid[a] && RrActorWire.LeaseMatches(Block[a], Bound, BoundGeneration, id))
                    {
                        BlockOf[id] = a;
                        break;
                    }
                }
            }
        }

        private void LeaseProps(double now)
        {
            for (int i = 0; i < NeedProps.Count; i++)
            {
                int id = NeedProps[i] & 0xFF;
                int p = PropOf[id];

                if (p >= 0)
                {
                    _propLeases.Remove(id);

                    // Orphans: nobody holds the object, or a player who is not in this instance does.
                    int holder = PropHolder[p];

                    if (!PropAuth[p] && (holder < 0 || !RrWire.Contains(Members, holder)) && now >= _propAskAt[p])
                    {
                        Asks.Add(new Ask { Kind = KindProp, Index = p });
                        _propAskAt[p] = now + AskEvery;
                    }

                    continue;
                }

                RrLease lease;

                if (!_propLeases.TryGetValue(id, out lease))
                {
                    lease = new RrLease();
                    _propLeases.Add(id, lease);
                }

                for (int q = 0; q < RrPropWire.Objects; q++)
                {
                    _propFree[q] = PropValid[q] && RrPropWire.FreeForLease(Prop[q], _instGen, _instLive) && !LeasingElsewhere(_propLeases, id, q);
                    _propMatch[q] = PropValid[q] && RrPropWire.LeaseMatches(Prop[q], Bound, BoundGeneration, id);
                }

                lease.Step(now, Bound * 2 + id, RrPropWire.Objects, PropValid, PropAuth, _propFree, _propMatch);
                Note(lease.TakeNote() is string note ? "prop " + id + ": " + note : null);

                if (lease.AskAuthority >= 0)
                {
                    Asks.Add(new Ask { Kind = KindProp, Index = lease.AskAuthority });
                }

                if (lease.WriteLease && lease.Object >= 0)
                {
                    int q = lease.Object;
                    var record = (int[])Prop[q].Clone();
                    record[RrPropWire.AtLease] = RrPropWire.PackLease(Bound, BoundGeneration, id);
                    record[RrPropWire.AtState] = RrPropWire.PackState(RrPropWire.StateResting, false, RrPropWire.Epoch(record) + 1, LocalId);
                    record[RrPropWire.AtSeq] = record[RrPropWire.AtSeq] + 1;
                    record[RrPropWire.AtStamp] = Stamp(now);
                    record[RrPropWire.AtKind] = 0;
                    RrPropWire.SetThrowId(record, 0);
                    Writes.Add(new Write { Kind = KindProp, Index = q, Record = record });
                    LeasedProps.Add(id);
                }
            }
        }

        private void LeaseBlocks(double now)
        {
            for (int i = 0; i < NeedBlocks.Count; i++)
            {
                int id = NeedBlocks[i] & 0xFF;
                int a = BlockOf[id];

                if (a >= 0)
                {
                    _blockLeases.Remove(id);

                    if (!BlockAuth[a] && now >= _blockAskAt[a])
                    {
                        Asks.Add(new Ask { Kind = KindBlock, Index = a });
                        _blockAskAt[a] = now + AskEvery;
                    }

                    continue;
                }

                RrLease lease;

                if (!_blockLeases.TryGetValue(id, out lease))
                {
                    lease = new RrLease();
                    _blockLeases.Add(id, lease);
                }

                for (int q = 0; q < RrActorWire.Objects; q++)
                {
                    _blockFree[q] = BlockValid[q] && RrActorWire.FreeForLease(Block[q], _instGen, _instLive) && !LeasingElsewhere(_blockLeases, id, q);
                    _blockMatch[q] = BlockValid[q] && RrActorWire.LeaseMatches(Block[q], Bound, BoundGeneration, id);
                }

                lease.Step(now, Bound + id, RrActorWire.Objects, BlockValid, BlockAuth, _blockFree, _blockMatch);
                Note(lease.TakeNote() is string note ? "actor block " + id + ": " + note : null);

                if (lease.AskAuthority >= 0)
                {
                    Asks.Add(new Ask { Kind = KindBlock, Index = lease.AskAuthority });
                }

                if (lease.WriteLease && lease.Object >= 0)
                {
                    var record = (int[])Block[lease.Object].Clone();
                    RrActorWire.WriteLease(record, id, LocalId, Bound, BoundGeneration, Stamp(now));
                    Writes.Add(new Write { Kind = KindBlock, Index = lease.Object, Record = record });
                }
            }
        }

        private static bool LeasingElsewhere(Dictionary<int, RrLease> leases, int id, int q)
        {
            foreach (KeyValuePair<int, RrLease> pair in leases)
            {
                if (pair.Key != id && pair.Value.Object == q)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>The presence slot whose record names this player (the local one: the claimed slot), -1 none.</summary>
        public int SlotOf(int playerId)
        {
            if (playerId == LocalId && ClaimedSlot >= 0)
            {
                return ClaimedSlot;
            }

            for (int s = 0; s < RrPresenceWire.Slots; s++)
            {
                if (SlotValid[s] && RrPresenceWire.OwnerOf(Slot[s]) == playerId && playerId != LocalId)
                {
                    return s;
                }
            }

            return -1;
        }

        /// <summary>The record describing a player: the own record for the local one, else its slot's ints (null none).</summary>
        public int[] RecordOf(int playerId)
        {
            if (playerId == LocalId || (!Up && playerId == 0))
            {
                return Own;
            }

            int s = SlotOf(playerId);
            return s >= 0 && RrPresenceWire.Listed(Slot[s], Present) ? Slot[s] : null;
        }

        // ------------------------------------------------------------------ helpers

        private void Note(string text)
        {
            if (text != null)
            {
                Notes.Add(text);
            }
        }

        private static void ClearMap(int[] map)
        {
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = -1;
            }
        }

        private static int[][] Make(int count, int ints)
        {
            var arrays = new int[count][];

            for (int i = 0; i < count; i++)
            {
                arrays[i] = new int[ints];
            }

            return arrays;
        }

        private static int[] Fill(int count, int value)
        {
            var a = new int[count];

            for (int i = 0; i < count; i++)
            {
                a[i] = value;
            }

            return a;
        }
    }
}
