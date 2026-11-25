using System.Collections.Generic;
using SomniumSpace.Worlds.SomniumRecRoom.Net;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using SomniumSpace.Worlds.SomniumRecRoom.Rooms;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Paintball
{
    /// <summary>
    /// Team identity over the other players of this instance. Rec Room dressed paintball players in team outfits;
    /// Somnium avatars must not be touched (ARCHITECTURE.md §1.3), so each member gets a team-coloured marker
    /// floating over the head (rr_shooters.md §11), and Rec Room's KO marker (`[PlayerCombatUI]`'s KO_Skull sprite)
    /// while they are out. Markers exist only for members of the local player's own instance and only while the
    /// local player is in this room; they are our own objects following the client's head transforms.
    /// </summary>
    public sealed class RrPaintballMarkers : MonoBehaviour
    {
        private sealed class Marker
        {
            public Transform Root;
            public Renderer Gem;
            public GameObject Skull;
            public int Team = -2;
        }

        private readonly Dictionary<int, Marker> _markers = new Dictionary<int, Marker>();
        private readonly List<int> _drop = new List<int>();
        private Material _skullMat;
        private bool _skullAsked;
        private float _nextScan;

        private void Update()
        {
            if (Time.time < _nextScan)
            {
                return;
            }

            _nextScan = Time.time + 0.5f;
            var members = RrNet.MembersOfMyInstance();
            _drop.Clear();
            foreach (var kv in _markers)
            {
                if (!members.Contains(kv.Key))
                {
                    _drop.Add(kv.Key);
                }
            }

            for (int i = 0; i < _drop.Count; i++)
            {
                if (_markers[_drop[i]].Root != null)
                {
                    Destroy(_markers[_drop[i]].Root.gameObject);
                }

                _markers.Remove(_drop[i]);
            }

            for (int i = 0; i < members.Count; i++)
            {
                int id = members[i];
                if (id != RrNet.LocalId && !_markers.ContainsKey(id))
                {
                    _markers[id] = Make(id);
                }
            }
        }

        private Marker Make(int id)
        {
            var m = new Marker();
            var root = new GameObject("Marker " + id).transform;
            root.SetParent(transform, false);
            m.Root = root;
            var gem = RrBuild.Primitive(PrimitiveType.Cube, root, "Gem", Vector3.zero, Quaternion.Euler(45f, 0f, 45f), Vector3.one * 0.11f,
                                        RrLook.Emissive(Color.white), false, RrLayers.Default);
            m.Gem = gem.GetComponent<Renderer>();
            var skull = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(skull.GetComponent<Collider>());
            skull.name = "KO";
            skull.transform.SetParent(root, false);
            skull.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            skull.transform.localScale = Vector3.one * 0.38f;
            m.Skull = skull;
            SkullMaterial(skull.GetComponent<Renderer>());
            skull.SetActive(false);
            return m;
        }

        /// <summary>Rec Room's KO_Skull sprite on an unlit, alpha-clipped quad (Sprites/Default does not render in VR).</summary>
        private void SkullMaterial(Renderer r)
        {
            if (_skullMat != null)
            {
                r.sharedMaterial = _skullMat;
                return;
            }

            r.sharedMaterial = RrLook.Unlit(new Color(0.85f, 0.85f, 0.85f));
            if (_skullAsked || RrAssets.Instance == null)
            {
                return;
            }

            _skullAsked = true;
            RrAssets.Instance.Load<Sprite>("KO_Skull", 213, sprite =>
            {
                if (sprite == null || sprite.texture == null)
                {
                    RrLog.WarnOnce("Paintball", "koskull", "KO_Skull sprite not found; out markers are plain");
                    return;
                }

                var mat = new Material(RrLook.Unlit(Color.white));
                var tex = sprite.texture;
                var rect = sprite.textureRect;
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                }

                mat.mainTextureScale = new Vector2(rect.width / tex.width, rect.height / tex.height);
                mat.mainTextureOffset = new Vector2(rect.x / tex.width, rect.y / tex.height);
                if (mat.HasProperty("_AlphaClip"))
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", 0.4f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                }

                _skullMat = mat;
                foreach (var kv in _markers)
                {
                    if (kv.Value.Skull != null)
                    {
                        kv.Value.Skull.GetComponent<Renderer>().sharedMaterial = mat;
                    }
                }
            });
        }

        private void LateUpdate()
        {
            var cam = RrPlayer.Camera;
            foreach (var kv in _markers)
            {
                var m = kv.Value;
                var remote = RrRemotePlayers.Get(kv.Key);
                var p = RrNet.Presence(kv.Key);
                Transform head = remote != null ? (remote.Head != null ? remote.Head : remote.Root) : null;
                bool show = head != null && p.Valid && p.Team >= 0 && p.Team <= 1;
                if (m.Root.gameObject.activeSelf != show)
                {
                    m.Root.gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                m.Root.position = head.position + Vector3.up * (head == remote.Head ? 0.42f : 2.1f);
                if (cam != null)
                {
                    Vector3 d = m.Root.position - cam.transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f)
                    {
                        m.Root.rotation = Quaternion.LookRotation(d);
                    }
                }

                if (m.Team != p.Team)
                {
                    m.Team = p.Team;
                    m.Gem.sharedMaterial = RrLook.Emissive(RrPaintballBuilder.TeamColor(p.Team), 1.6f);
                }

                bool down = p.Status == RrStatus.Down;
                if (m.Skull.activeSelf != down)
                {
                    m.Skull.SetActive(down);
                }

                m.Gem.gameObject.SetActive(!down);
                m.Gem.transform.localRotation = Quaternion.Euler(45f, Time.time * 90f, 45f);
            }
        }

        private void OnDestroy()
        {
            _markers.Clear();
            if (_skullMat != null)
            {
                Destroy(_skullMat);
            }
        }
    }
}
