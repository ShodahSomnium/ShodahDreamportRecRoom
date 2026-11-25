using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.LostSkulls
{
    /// <summary>
    /// The brig of act 1 (rr_latest_rgames_story.md 8.1: "the players start locked in a jail cell ... Break out"): Rec
    /// Room's [BreakableLockEncounter] in the Galley level, a jail-bar door (1.37 x 2.12 m, hinged at its pivot) with a
    /// padlock on the inside. The level's bars and door collider are left out (RrLostSkullsActs.Galley's drop box) and
    /// rebuilt here as our own door; the padlock is a button (poke it, point and pull the trigger, or click it). A press
    /// asks the authority (hazard <see cref="RrLostSkullsRoom.HazardLock"/>), which sets the act's cell bit; every client
    /// then swings the door open (100 degrees outward, 0.8 s) and the lock breaks off.
    /// </summary>
    public sealed class RrLostSkullsCell : MonoBehaviour
    {
        public const float Width = 1.37f, Height = 2.12f;
        public const string BreakClip = "Quest_Sword_ImpactEnvironment01";

        private Transform _door;
        private GameObject _lock;
        private float _k;
        private bool _open;

        /// <summary>The cell door at its hinge (act-local, the level's door yaw) and the padlock (act-local).</summary>
        public static RrLostSkullsCell Create(RrLostSkullsRoom room, Transform parent, Vector3 hinge, float yaw, Vector3 lockAt)
        {
            var root = RrBuild.Group(parent, "Brig Door", hinge, Quaternion.Euler(0f, yaw, 0f));
            var c = root.AddComponent<RrLostSkullsCell>();
            c.BuildDoor(root.transform);
            // The padlock faces into the cell (the door's +z).
            Vector3 inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var lk = RrBuild.Group(parent, "Brig Lock", lockAt, Quaternion.FromToRotation(Vector3.up, inward));
            var brass = RrLook.Solid(new Color(0.62f, 0.46f, 0.18f));
            RrBuild.Box(lk.transform, "Padlock", new Vector3(0f, -0.04f, 0f), new Vector3(0.13f, 0.06f, 0.15f), brass, false);
            RrPressable.Create(lk.transform, "Break the lock", Vector3.zero, Quaternion.identity, new Color(0.78f, 0.6f, 0.25f), 0.2f,
                () => room.PressHazard(RrLostSkullsRoom.HazardLock, 0));
            c._lock = lk;
            RrLog.Info("LostSkulls", "brig door at " + hinge.ToString("F2") + " yaw " + yaw.ToString("0") + ", lock at " + lockAt.ToString("F2"));
            return c;
        }

        private void BuildDoor(Transform root)
        {
            var leaf = RrBuild.Group(root, "Bars").transform;
            _door = leaf;
            var iron = RrLook.Solid(new Color(0.16f, 0.15f, 0.14f));
            // Frame rails top and bottom, 7 upright bars, one cross bar (the level's JailBars look, measured sizes).
            RrBuild.Box(leaf, "Top Rail", new Vector3(Width * 0.5f, Height - 0.05f, 0f), new Vector3(Width, 0.07f, 0.05f), iron, false);
            RrBuild.Box(leaf, "Bottom Rail", new Vector3(Width * 0.5f, 0.08f, 0f), new Vector3(Width, 0.07f, 0.05f), iron, false);
            RrBuild.Box(leaf, "Cross Rail", new Vector3(Width * 0.5f, Height * 0.55f, 0f), new Vector3(Width, 0.05f, 0.04f), iron, false);
            for (int i = 0; i < 7; i++)
            {
                float x = 0.06f + i * (Width - 0.12f) / 6f;
                RrBuild.Box(leaf, "Bar " + i, new Vector3(x, Height * 0.5f, 0f), new Vector3(0.035f, Height, 0.035f), iron, false);
            }

            var col = leaf.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(Width * 0.5f, Height * 0.5f, 0f);
            col.size = new Vector3(Width, Height, 0.08f);
            leaf.gameObject.layer = RrLayers.Environment;
        }

        /// <summary>Every client, every frame: open while the act's cell bit is set (a new run closes it again).</summary>
        public void Show(bool open)
        {
            if (open != _open)
            {
                _open = open;
                if (open)
                {
                    RrSound.Play(BreakClip, _lock != null ? _lock.transform.position : transform.position, 0.9f, 0.8f);
                    RrFx.Spawn("(Quest_Pirate1_Weapon_Sparks_VFX)", _lock != null ? _lock.transform.position : transform.position, Quaternion.identity);
                }
                else
                {
                    _k = 0f;
                    Apply();
                }

                if (_lock != null)
                {
                    _lock.SetActive(!open);
                }
            }

            if (open && _k < 1f)
            {
                _k = Mathf.MoveTowards(_k, 1f, Time.deltaTime / 0.8f);
                Apply();
            }
        }

        private void Apply()
        {
            if (_door != null)
            {
                _door.localRotation = Quaternion.Euler(0f, 100f * Mathf.SmoothStep(0f, 1f, _k), 0f);
            }
        }
    }
}
