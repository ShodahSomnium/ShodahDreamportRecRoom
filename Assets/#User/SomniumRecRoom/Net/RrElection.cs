using System.Collections.Generic;

namespace SomniumSpace.Worlds.SomniumRecRoom.Net
{
    /// <summary>
    /// Who runs an instance ("fake master", somnium_networking.md §5.4). Pure: every client computes it from the same
    /// presence slots and the same instance header, so all of them reach the same answer without talking.
    ///
    /// Elected: the member with the lowest Fusion PlayerId among members that are not Loading (Loading members only
    /// when nobody else is there). Fusion's ids grow with the join order, so that is the longest-present member and a
    /// newcomer never displaces anyone (ETS EtsSlotRules.ActingMaster).
    ///
    /// Acting: once the instance is bound, incumbency wins - the member named in the header (int 3) keeps running it
    /// while its heartbeat (the header's seq, moved at least once a second) is alive, even when a lower id comes back
    /// (the doc's "an old authority that sees another writer named stands down"). A named authority that left the
    /// instance or the room hands over to the elected member at once. A header silent for StaleSeconds has lost its
    /// runner: the other members take over in PlayerId order, one every TakeoverStep seconds (ETS
    /// EtsTrafficAuthority: 3 s + 1.5 s per candidate below), and the first to write becomes the incumbent.
    /// </summary>
    public static class RrElection
    {
        /// <summary>A header whose seq has not moved for this long has lost its runner, seconds.</summary>
        public const double StaleSeconds = 3.0;

        /// <summary>Extra wait per candidate ahead in the takeover queue, seconds.</summary>
        public const double TakeoverStep = 1.5;

        /// <summary>The lowest PlayerId not Loading, else the lowest Loading one; -1 without members.</summary>
        /// <param name="members">PlayerIds, any order.</param>
        /// <param name="loading">Per member: its status is Loading.</param>
        public static int Elect(IReadOnlyList<int> members, IReadOnlyList<bool> loading)
        {
            int best = -1;
            int bestLoading = -1;

            for (int i = 0; i < members.Count; i++)
            {
                int id = members[i];

                if (id < 0)
                {
                    continue;
                }

                if (loading != null && i < loading.Count && loading[i])
                {
                    if (bestLoading < 0 || id < bestLoading)
                    {
                        bestLoading = id;
                    }
                }
                else if (best < 0 || id < best)
                {
                    best = id;
                }
            }

            return best >= 0 ? best : bestLoading;
        }

        /// <summary>
        /// The member that runs a bound instance, as seen by localId.
        /// </summary>
        /// <param name="members">The instance's members, ascending PlayerIds.</param>
        /// <param name="elected">From <see cref="Elect"/>.</param>
        /// <param name="bound">The instance has an instance object.</param>
        /// <param name="headerAuthority">The PlayerId the instance object's int 3 names (-1 none).</param>
        /// <param name="silentSeconds">Since the instance object's seq last moved.</param>
        public static int Acting(int localId, IReadOnlyList<int> members, int elected, bool bound, int headerAuthority, double silentSeconds)
        {
            if (!bound)
            {
                return elected;
            }

            int incumbent = headerAuthority >= 0 && RrWire.Contains(members, headerAuthority) ? headerAuthority : elected;

            if (silentSeconds < StaleSeconds || incumbent < 0)
            {
                return incumbent;
            }

            if (localId == incumbent || !RrWire.Contains(members, localId))
            {
                return incumbent;
            }

            int ahead = 0;

            for (int i = 0; i < members.Count; i++)
            {
                int id = members[i];

                if (id == incumbent)
                {
                    continue;
                }

                if (id == localId)
                {
                    break;
                }

                ahead++;
            }

            return silentSeconds >= StaleSeconds + TakeoverStep * ahead ? localId : incumbent;
        }
    }
}
