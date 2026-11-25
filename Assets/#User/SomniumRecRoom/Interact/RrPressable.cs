using System;
using System.Collections;
using SomniumSpace.Worlds.SomniumRecRoom.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SomniumSpace.Worlds.SomniumRecRoom.Interact
{
    /// <summary>
    /// A world button (Rec Room's push buttons: the Charades button, the Stunt Runner finish, game-board and
    /// activity buttons). Pressed four ways, all ending in <see cref="Press"/>:
    /// desktop - aiming at it within <see cref="ScreenReach"/> and pressing "use" (RrGrab calls
    /// <see cref="PressAlong"/>), or the Somnium client's own click, which reaches colliders on layer 16 through its
    /// PhysicsRaycaster as a UGUI pointer click (somnium_conventions.md §11.3); VR - poking it with an empty hand,
    /// or pointing at it and pulling the trigger (RrGrab's trigger ray, 5 m). A client click and our own ray can
    /// report the same press, so presses closer than <see cref="Cooldown"/> count once.
    ///
    /// The collider sits on layer 16 (Click) and excludes every layer, so it pushes nothing (the ETS handle recipe).
    /// The press plays Rec Room's Charades_ButtonPress (0.27 s) and pushes the cap in by <see cref="Travel"/>.
    /// </summary>
    public sealed class RrPressable : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Desktop reach of "use" on a button, metres from the camera.</summary>
        public const float ScreenReach = 3f;

        /// <summary>VR poke: a hand's grip point within this many metres of the button's collider.</summary>
        public const float PokeRadius = 0.05f;

        public const string DefaultSound = "Charades_ButtonPress";

        private static readonly RrPressable[] Touching = new RrPressable[2];
        private static readonly Collider[] Buffer = new Collider[8];

        public string PressSound = DefaultSound;
        public float Volume = 0.8f;
        public float Cooldown = 0.3f;
        public bool Interactable = true;

        /// <summary>The part that moves in when pressed (optional), along its parent's -Y.</summary>
        public Transform Cap;
        public float Travel = 0.012f;

        /// <summary>Pressed (any way).</summary>
        public event Action OnPressed;

        /// <summary>Pressed, with how: "click", "use key", "trigger ray", "poke", "code".</summary>
        public event Action<string> OnPressedBy;

        /// <summary>Any button pressed (button, how).</summary>
        public static event Action<RrPressable, string> OnAnyPressed;

        public int Presses { get; private set; }

        private float _last = -10f;
        private Vector3 _capRest;
        private bool _capKnown;
        private Coroutine _push;

        /// <summary>Makes a GameObject with colliders a button: colliders to layer 16, excluding every layer.</summary>
        public static RrPressable Make(GameObject go, Action onPressed = null)
        {
            if (go == null)
            {
                return null;
            }

            var colliders = go.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                colliders = new Collider[] { go.AddComponent<BoxCollider>() };
            }

            foreach (var c in colliders)
            {
                c.gameObject.layer = RrLayers.Click;
                c.excludeLayers = ~0;
            }

            var p = go.GetComponent<RrPressable>();
            if (p == null)
            {
                p = go.AddComponent<RrPressable>();
            }

            if (onPressed != null)
            {
                p.OnPressed += onPressed;
            }

            return p;
        }

        /// <summary>A procedural round push button (cap on a base, Rec Room's 0.30 m on 0.35 m), facing its local +Y.</summary>
        public static RrPressable Create(Transform parent, string name, Vector3 localPos, Quaternion localRot, Color cap, float diameter = 0.3f, Action onPressed = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = localRot;
            float baseD = diameter * 1.17f;
            var baseGo = Disc(root.transform, "Base", baseD, 0.04f, 0.02f, RrLook.Solid(RrLook.DarkGrey));
            var capGo = Disc(root.transform, "Cap", diameter, 0.035f, 0.055f, RrLook.Solid(cap));
            UnityEngine.Object.Destroy(baseGo.GetComponent<Collider>());
            UnityEngine.Object.Destroy(capGo.GetComponent<Collider>());
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.04f, 0f);
            box.size = new Vector3(baseD, 0.08f, baseD);
            var p = Make(root, onPressed);
            p.Cap = capGo.transform;
            return p;
        }

        private static GameObject Disc(Transform parent, string name, float diameter, float height, float y, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Press("click");
        }

        /// <summary>Presses it now; false when disabled or within the cooldown.</summary>
        public bool Press(string how = "code")
        {
            if (!Interactable || !isActiveAndEnabled || Time.time - _last < Cooldown)
            {
                return false;
            }

            _last = Time.time;
            Presses++;
            if (!string.IsNullOrEmpty(PressSound))
            {
                RrSound.Play(PressSound, transform.position, Volume);
            }

            if (Cap != null)
            {
                if (_push != null)
                {
                    StopCoroutine(_push);
                }

                _push = StartCoroutine(Push());
            }

            RrTools.Once("Button", "first", "first button press: " + name + " (" + how + ")");
            if (OnPressed != null)
            {
                OnPressed();
            }

            if (OnPressedBy != null)
            {
                OnPressedBy(how);
            }

            if (OnAnyPressed != null)
            {
                OnAnyPressed(this, how);
            }

            return true;
        }

        private IEnumerator Push()
        {
            if (!_capKnown)
            {
                _capRest = Cap.localPosition;
                _capKnown = true;
            }

            Vector3 down = _capRest - Vector3.up * (Travel / Mathf.Max(0.0001f, Cap.parent != null ? Cap.parent.lossyScale.y : 1f));
            Cap.localPosition = down;
            yield return new WaitForSeconds(0.15f);
            float t = 0f;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                Cap.localPosition = Vector3.Lerp(down, _capRest, t / 0.1f);
                yield return null;
            }

            Cap.localPosition = _capRest;
            _push = null;
        }

        private void OnDisable()
        {
            if (_capKnown && Cap != null)
            {
                Cap.localPosition = _capRest;
            }

            _push = null;
        }

        /// <summary>
        /// Presses the first button along a ray within <paramref name="reach"/>, unless something solid is in front
        /// of it. Returns true when a button took the press.
        /// </summary>
        public static bool PressAlong(Ray ray, float reach, string how)
        {
            float max = reach;
            RaycastHit hit;
            int solid = RrLayers.WorldMask & ~(1 << RrLayers.Click);
            if (Physics.Raycast(ray, out hit, reach, solid, QueryTriggerInteraction.Ignore))
            {
                max = hit.distance + 0.02f;
            }

            if (!Physics.Raycast(ray, out hit, max, 1 << RrLayers.Click, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            var p = hit.collider.GetComponentInParent<RrPressable>();
            return p != null && p.Press(how);
        }

        /// <summary>VR poke: an empty hand entering a button presses it once (called by RrGrab every frame).</summary>
        internal static void Tick(bool vr)
        {
            if (!vr)
            {
                Touching[0] = Touching[1] = null;
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                var hand = i == 0 ? RrHand.Left : RrHand.Right;
                if (hand == null || !hand.isActiveAndEnabled || RrGrab.HeldBy(hand) != null)
                {
                    Touching[i] = null;
                    continue;
                }

                Vector3 p = hand.Grip != null ? hand.Grip.position : hand.transform.position;
                int n = Physics.OverlapSphereNonAlloc(p, PokeRadius, Buffer, 1 << RrLayers.Click, QueryTriggerInteraction.Collide);
                RrPressable found = null;
                for (int k = 0; k < n && found == null; k++)
                {
                    found = Buffer[k] != null ? Buffer[k].GetComponentInParent<RrPressable>() : null;
                }

                if (found != null && found != Touching[i] && found.Press("poke"))
                {
                    hand.Haptic(0.3f, 0.05f);
                }

                Touching[i] = found;
            }
        }

        internal static void Shutdown()
        {
            Touching[0] = Touching[1] = null;
            Array.Clear(Buffer, 0, Buffer.Length);
            OnAnyPressed = null;
        }
    }
}
