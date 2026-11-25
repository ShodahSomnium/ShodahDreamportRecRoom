using System;
using System.Collections;
using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Rooms
{
    /// <summary>How a place appears in the watch menu and how it is made.</summary>
    public sealed class RrRoomInfo
    {
        public RrActivity Activity;
        public string Title;
        public string Subtitle;
        public string Description;
        public int MinPlayers = 1;
        public int MaxPlayers = 8;

        /// <summary>Rec Room sprite/texture names for the watch's cards (see rr_watch_ui.md).</summary>
        public string CoverSprite;
        public string IconSprite;

        /// <summary>Rec Room clip name for the room's music while in it (null: none).</summary>
        public string Music;

        /// <summary>Menu grouping: "Social", "PvP", "Quests", "Sports", "Party".</summary>
        public string Category = "Games";

        /// <summary>Shared with other players (false: the dorm, one per player).</summary>
        public bool Networked = true;

        /// <summary>
        /// Not offered as an activity card: kept out of the watch's Play and Home pages, the Dorm's Play board, the Rec
        /// Center's door lists and the Magic Door's picks (e.g. the player-made rooms, which are many rooms under one
        /// activity and are reached through the watch's Rooms page and the MAKERWORLD / CAMPUS JAMS doors). Travel to it
        /// still works.
        /// </summary>
        public bool Unlisted;

        /// <summary>Adds the room's component to the (empty, positioned) root and returns it.</summary>
        public Func<GameObject, RrRoom> Create;
    }

    /// <summary>The registry of every place, filled once at start by <see cref="RrRegistry.RegisterAll"/>.</summary>
    public static class RrRooms
    {
        private static readonly Dictionary<RrActivity, RrRoomInfo> Infos = new Dictionary<RrActivity, RrRoomInfo>();
        private static readonly List<RrRoomInfo> Ordered = new List<RrRoomInfo>();

        public static void Register(RrRoomInfo info)
        {
            if (info == null || info.Activity == RrActivity.None)
            {
                return;
            }

            if (Infos.ContainsKey(info.Activity))
            {
                Ordered.Remove(Infos[info.Activity]);
            }

            Infos[info.Activity] = info;
            Ordered.Add(info);
        }

        public static RrRoomInfo Get(RrActivity activity)
        {
            RrRoomInfo info;
            return Infos.TryGetValue(activity, out info) ? info : null;
        }

        public static IReadOnlyList<RrRoomInfo> All
        {
            get { return Ordered; }
        }

        /// <summary>
        /// An activity a list may offer as a card: registered with a room to build and not <see cref="RrRoomInfo.Unlisted"/>.
        /// Unregistered activities (games not in the Dreamport) are never pickable.
        /// </summary>
        public static bool Listed(RrActivity activity)
        {
            var info = Get(activity);
            return info != null && info.Create != null && !info.Unlisted;
        }

        public static void Clear()
        {
            Infos.Clear();
            Ordered.Clear();
        }
    }

    /// <summary>
    /// A place the player can be: the dorm, the Rec Center, or one of the games. The room's root sits at the
    /// room's origin - the world origin, for every room (<see cref="RrPlacement"/>): only the current room exists -
    /// and everything the room builds must be under it, so leaving destroys the lot. Anything a room puts outside
    /// its root (the world root, the scene root, a hand) outlives it and shows up in the next room at the same
    /// place; the shared systems that must do so (effects, sounds, decals, shots in flight) are cleared by travel. Build runs as a coroutine behind the travel fade; it should yield every
    /// few milliseconds of work so the headset keeps its frame rate.
    ///
    /// Games subclass this (usually through Games.RrGameRoom, which adds the match flow). Authority is the
    /// "fake master" of the instance: the member with the lowest PlayerId, elected by <see cref="RrNet"/>;
    /// offline it is always this client.
    /// </summary>
    public abstract class RrRoom : MonoBehaviour
    {
        [NonSerialized] public RrActivity Activity;
        [NonSerialized] public int InstanceNumber;
        [NonSerialized] public RrRoomInfo Info;

        /// <summary>World position of the room's (0, 0, 0).</summary>
        public Vector3 Origin
        {
            get { return transform.position; }
        }

        /// <summary>True once Build has finished.</summary>
        [NonSerialized] public bool Built;

        /// <summary>
        /// How far Build has got (0..1) for the loading card's bar; a room that never sets it (-1) gets a curve
        /// that slows as it fills. Travel sets it to 1 when Build returns.
        /// </summary>
        [NonSerialized] public float BuildProgress = -1f;

        /// <summary>
        /// The presence status the player has on arrival (somnium_networking.md §5.1): Lobby by default; a game
        /// joined mid-match may say Spectating, a social room Playing.
        /// </summary>
        public virtual RrStatus ArrivalStatus
        {
            get { return RrStatus.Lobby; }
        }

        /// <summary>This client runs the instance (timers, scores, AI, spawns).</summary>
        public bool IsAuthority
        {
            get { return Info == null || !Info.Networked || RrNet.IsInstanceAuthority; }
        }

        /// <summary>Builds the room under this transform (local coordinates). Spread the work over frames.</summary>
        public abstract IEnumerator Build();

        /// <summary>Where the local player appears (local position and facing yaw). Default: the origin, facing +z.</summary>
        public virtual Pose Spawn(int index)
        {
            return new Pose(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
        }

        /// <summary>The local player has arrived (after the teleport, as the fade lifts).</summary>
        public virtual void OnLocalEnter()
        {
        }

        /// <summary>The local player is leaving (before the room is destroyed).</summary>
        public virtual void OnLocalExit()
        {
        }

        /// <summary>Someone joined or left this instance, changed team or status.</summary>
        public virtual void OnMembersChanged()
        {
        }

        /// <summary>This client became (true) or stopped being (false) the instance's authority.</summary>
        public virtual void OnAuthorityChanged(bool isAuthority)
        {
        }

        /// <summary>World-space pose of a spawn point.</summary>
        public Pose WorldSpawn(int index)
        {
            var p = Spawn(index);
            return new Pose(transform.TransformPoint(p.position), transform.rotation * p.rotation);
        }

        /// <summary>
        /// World y of the room's lowest floor: the bottom of its lowest solid collider on the Environment layer,
        /// measured by travel once Build is done (NaN before). A player whose feet are 10 m below it has fallen
        /// out of the room and is put back (<see cref="RrTravel"/>). A room can set it itself after its build.
        /// </summary>
        [NonSerialized] public float LowestFloor = float.NaN;

        /// <summary>The RrAssets scope its loads were tagged with (0: none); released when the room is left.</summary>
        [NonSerialized] internal int AssetScope;

        /// <summary>
        /// Where a player who fell out of the room or was respawned by the client goes back to (world pose).
        /// Default: <see cref="WorldSpawn"/>. Travel prefers a game's own respawn point while its match runs.
        /// </summary>
        public virtual Pose RescuePose(int index)
        {
            return WorldSpawn(index);
        }
    }
}
