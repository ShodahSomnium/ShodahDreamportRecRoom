using System.Text;
using SomniumSpace.Worlds.SomniumRecRoom.Net;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Bowling
{
    /// <summary>
    /// Ten-pin scoring (rr_sports.md §6.2: 10 frames, up to 2 rolls; strike = all 10 on the first roll, bonus the next 2
    /// rolls; spare = all 10 with 2 rolls, bonus the next roll; the 10th frame gives 2 more rolls after a strike and 1 after
    /// a spare; max 300). A bowler's sheet is 21 roll slots - frames 1-9 take slots 2f and 2f+1 (a strike leaves the second
    /// empty), the 10th slots 18-20 - each the pins that roll knocked down, <see cref="Empty"/> when not rolled. In the
    /// instance ints a sheet is 3 ints of 7 slots x 4 bits (research §9.5).
    /// </summary>
    public static class RrBowlingScore
    {
        public const int Frames = 10;
        public const int Slots = 21;
        public const int Empty = 15;
        public const int SlotsPerInt = 7;
        public const int IntsPerSheet = 3;
        public const int FullRack = 0x3FF;

        /// <summary>The packed value of an empty sheet int (7 x 4 bits of 15).</summary>
        public const int EmptyInt = 0x0FFFFFFF;

        public static int SlotOf(int frame, int roll)
        {
            return frame < 9 ? frame * 2 + roll : 18 + roll;
        }

        // ------------------------------------------------------------------ the sheet in the instance ints

        /// <summary>A bowler's 21 slots from the ints starting at <paramref name="at"/>.</summary>
        public static int[] Read(RrInstanceState s, int at)
        {
            var slots = new int[Slots];
            for (int i = 0; i < Slots; i++)
            {
                int word = s.Get(at + i / SlotsPerInt);
                slots[i] = (word >> ((i % SlotsPerInt) * 4)) & 0xF;
            }

            return slots;
        }

        /// <summary>Authority: writes one slot.</summary>
        public static void Write(RrInstanceState s, int at, int slot, int value)
        {
            int index = at + slot / SlotsPerInt;
            int shift = (slot % SlotsPerInt) * 4;
            int word = s.Get(index);
            word = (word & ~(0xF << shift)) | ((value & 0xF) << shift);
            s.Set(index, word);
        }

        public static void Clear(RrInstanceState s, int at)
        {
            for (int i = 0; i < IntsPerSheet; i++)
            {
                s.Set(at + i, EmptyInt);
            }
        }

        // ------------------------------------------------------------------ rules

        /// <summary>
        /// After a roll of <paramref name="knocked"/> pins in (frame, roll) with <paramref name="standingBefore"/> up:
        /// whether the bowler's frame is over, the next roll's index, and whether the next roll gets a full new rack.
        /// </summary>
        public static void Advance(int[] slots, int frame, int roll, int knocked, int standingBefore, out bool frameDone, out int nextRoll, out bool fullRack)
        {
            int before = Count(standingBefore);
            bool cleared = knocked >= before;
            frameDone = false;
            fullRack = false;
            nextRoll = roll + 1;
            if (frame < 9)
            {
                frameDone = roll == 1 || cleared;
            }
            else if (roll == 0)
            {
                fullRack = cleared;
            }
            else if (roll == 1)
            {
                int first = slots[18];
                if (first == 10)
                {
                    fullRack = cleared;
                }
                else if (first + knocked >= 10)
                {
                    fullRack = true;
                }
                else
                {
                    frameDone = true;
                }
            }
            else
            {
                frameDone = true;
            }

            if (frameDone)
            {
                nextRoll = 0;
                fullRack = true;
            }
        }

        /// <summary>The frame a bowler is on (0-9) and its next roll, or frame 10 when the sheet is complete.</summary>
        public static void Position(int[] slots, out int frame, out int roll)
        {
            for (int f = 0; f < Frames; f++)
            {
                int a = slots[SlotOf(f, 0)];
                if (a == Empty)
                {
                    frame = f;
                    roll = 0;
                    return;
                }

                if (f < 9)
                {
                    if (a == 10)
                    {
                        continue;
                    }

                    if (slots[SlotOf(f, 1)] == Empty)
                    {
                        frame = f;
                        roll = 1;
                        return;
                    }

                    continue;
                }

                int b = slots[19];
                if (b == Empty)
                {
                    frame = 9;
                    roll = 1;
                    return;
                }

                bool third = a == 10 || a + b >= 10;
                if (third && slots[20] == Empty)
                {
                    frame = 9;
                    roll = 2;
                    return;
                }
            }

            frame = Frames;
            roll = 0;
        }

        /// <summary>Running totals per frame; -1 for a frame that cannot be scored yet (bonus rolls missing).</summary>
        public static int[] Totals(int[] slots)
        {
            var rolls = new int[Slots];
            var frameStart = new int[Frames];
            int n = 0;
            for (int f = 0; f < Frames; f++)
            {
                frameStart[f] = n;
                int count = f < 9 ? 2 : 3;
                for (int r = 0; r < count; r++)
                {
                    int v = slots[SlotOf(f, r)];
                    if (v == Empty)
                    {
                        continue;
                    }

                    rolls[n++] = v;
                }
            }

            var totals = new int[Frames];
            int sum = 0;
            bool broken = false;
            for (int f = 0; f < Frames; f++)
            {
                int i = frameStart[f];
                int a = slots[SlotOf(f, 0)];
                int value = -1;
                if (!broken && a != Empty)
                {
                    if (f < 9)
                    {
                        int b = slots[SlotOf(f, 1)];
                        if (a == 10)
                        {
                            value = i + 2 < n ? 10 + rolls[i + 1] + rolls[i + 2] : -1;
                        }
                        else if (b != Empty)
                        {
                            value = a + b == 10 ? (i + 2 < n ? 10 + rolls[i + 2] : -1) : a + b;
                        }
                    }
                    else
                    {
                        int b = slots[19], c = slots[20];
                        bool third = b != Empty && (a == 10 || a + b >= 10);
                        if (b != Empty && (!third || c != Empty))
                        {
                            value = a + b + (third ? c : 0);
                        }
                    }
                }

                if (value < 0)
                {
                    broken = true;
                    totals[f] = -1;
                    continue;
                }

                sum += value;
                totals[f] = sum;
            }

            return totals;
        }

        /// <summary>The best total so far (the last scorable frame), 0 before any.</summary>
        public static int Total(int[] slots)
        {
            int best = 0;
            foreach (int t in Totals(slots))
            {
                if (t >= 0)
                {
                    best = t;
                }
            }

            return best;
        }

        /// <summary>A frame's marks as the monitors show them: "X", "7/", "81", "-5", tenth "XX7" / "9/X".</summary>
        public static string Marks(int[] slots, int frame)
        {
            int a = slots[SlotOf(frame, 0)];
            int b = slots[SlotOf(frame, 1)];
            if (frame < 9)
            {
                if (a == Empty)
                {
                    return "  ";
                }

                if (a == 10)
                {
                    return "X ";
                }

                return Digit(a) + (b == Empty ? " " : a + b == 10 ? "/" : Digit(b));
            }

            int c = slots[20];
            var sb = new StringBuilder(3);
            sb.Append(a == Empty ? " " : a == 10 ? "X" : Digit(a));
            sb.Append(b == Empty ? " " : a == 10 ? (b == 10 ? "X" : Digit(b)) : a + b == 10 ? "/" : Digit(b));
            if (c == Empty)
            {
                sb.Append(' ');
            }
            else if (a == 10 && b != 10)
            {
                sb.Append(b + c == 10 ? "/" : Digit(c));
            }
            else
            {
                sb.Append(c == 10 ? "X" : Digit(c));
            }

            return sb.ToString();
        }

        private static string Digit(int v)
        {
            return v == 0 ? "-" : v.ToString();
        }

        public static int Count(int mask)
        {
            int n = 0;
            for (int i = 0; i < 10; i++)
            {
                n += (mask >> i) & 1;
            }

            return n;
        }
    }
}
