using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SomniumSpace.Worlds.SomniumRecRoom.Watch
{
    /// <summary>
    /// A watch button that behaves like Rec Room's (RRUI MultiGraphicButton + ButtonTheme + AudioPalette):
    ///
    /// - its look per state is one of three kinds, whichever is set: a <see cref="Plate"/> whose alpha fades
    ///   (nav tabs: #602121 at 0 idle, 0.3 hover, 0.4 pressed, 0.5 when the tab is <see cref="Active"/>, 0.6
    ///   active and hovered); a <see cref="Fill"/> colour per state (white, #F1F1F1, #E5E5E5); or a
    ///   <see cref="Gradient"/> pair per state (the orange primary button). Colour fades take 0.1 s, as in the
    ///   theme;
    /// - it pops to 1.03x for 0.05 s when clicked (ButtonAnimationPalette_Default), and can grow on hover
    ///   (room cards, with an orange outline);
    /// - it plays its audio type's hover, down and click pools (<see cref="RrWatchSounds"/>), plus an optional
    ///   "flavour" stinger (the nav tabs' own sounds).
    ///
    /// Selection (keyboard focus) is not used: a clicked button returns to its normal look.
    /// </summary>
    public sealed class RrUiButton : Button
    {
        public RrUiSound Sound = RrUiSound.Default;

        /// <summary>A pool played with the click (nav tab flavour sounds).</summary>
        public string Flavor;

        public Graphic Plate;

        /// <summary>Plate alpha when normal, hovered, pressed, disabled.</summary>
        public Vector4 PlateAlpha = new Vector4(0f, 0.3f, 0.4f, 0f);

        /// <summary>Plate alpha of an active tab: normal, hovered.</summary>
        public Vector2 ActiveAlpha = new Vector2(0.5f, 0.6f);

        public Graphic Fill;

        /// <summary>Fill colours: normal, hovered, pressed.</summary>
        public Color[] FillStates;

        public RrUiGradient Gradient;

        /// <summary>Gradient pairs: normal from/to, hovered from/to, pressed from/to.</summary>
        public Color[] GradientStates;

        /// <summary>Shown while hovered (card focus outline).</summary>
        public Graphic Outline;

        public float HoverScale = 1f;

        /// <summary>The button's text, when it has one (so pages can change it).</summary>
        public TextMeshProUGUI Label;

        private bool _active;
        private SelectionState _state;
        private float _pop;
        private float _scale = 1f;

        /// <summary>A tab that is the current page: its plate stays at half alpha.</summary>
        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value)
                {
                    return;
                }

                _active = value;
                Apply(_state, true);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            navigation = new Navigation { mode = Navigation.Mode.None };
        }

        /// <summary>Re-applies the look after the fields were set (they are set after AddComponent).</summary>
        public void Refresh()
        {
            transition = Transition.None;
            Apply(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            _state = state;
            Apply(state, instant);
        }

        private void Apply(SelectionState state, bool instant)
        {
            bool disabled = state == SelectionState.Disabled;
            bool pressed = state == SelectionState.Pressed;
            bool hover = state == SelectionState.Highlighted;
            float fade = instant ? 0f : 0.1f;
            if (Plate != null)
            {
                float a = disabled ? PlateAlpha.w : pressed ? PlateAlpha.z : hover ? (_active ? ActiveAlpha.y : PlateAlpha.y) : (_active ? ActiveAlpha.x : PlateAlpha.x);
                Plate.CrossFadeColor(new Color(1f, 1f, 1f, a), fade, true, true);
            }

            if (Fill != null && FillStates != null && FillStates.Length >= 3)
            {
                Fill.color = pressed ? FillStates[2] : hover ? FillStates[1] : FillStates[0];
            }

            if (Gradient != null && GradientStates != null && GradientStates.Length >= 6)
            {
                int i = pressed ? 4 : hover ? 2 : 0;
                Gradient.Set(GradientStates[i], GradientStates[i + 1]);
            }

            if (Outline != null)
            {
                Outline.enabled = (hover || pressed) && !disabled;
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            RrWatchSounds.Hover(IsInteractable() ? Sound : RrUiSound.Inactive);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                RrWatchSounds.Down(IsInteractable() ? Sound : RrUiSound.Inactive);
            }
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
            {
                return;
            }

            RrWatchSounds.Click(Sound);
            RrWatchSounds.Play(Flavor);
            _pop = 0.05f;
            base.OnPointerClick(eventData);

            // Keyboard focus would leave the button in its "selected" look.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void Update()
        {
            float target = (_state == SelectionState.Highlighted || _state == SelectionState.Pressed) && IsInteractable() ? HoverScale : 1f;
            if (_pop > 0f)
            {
                _pop -= Time.unscaledDeltaTime;
                target *= 1.03f;
            }

            if (Mathf.Abs(_scale - target) > 0.0001f)
            {
                _scale = Mathf.MoveTowards(_scale, target, Time.unscaledDeltaTime * 1.2f);
                transform.localScale = new Vector3(_scale, _scale, 1f);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _pop = 0f;
            _scale = 1f;
            transform.localScale = Vector3.one;
            if (Outline != null)
            {
                Outline.enabled = false;
            }
        }
    }
}
