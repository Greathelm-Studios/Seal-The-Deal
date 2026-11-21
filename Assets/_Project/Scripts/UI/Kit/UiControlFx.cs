using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Which colour scheme a control draws with.</summary>
    public enum UiVariant
    {
        /// <summary>Solid cobalt with paper text: the one main action per screen.</summary>
        Primary = 0,
        /// <summary>Paper with a navy keyline.</summary>
        Secondary = 1,
        /// <summary>No plate: a bullet and a label, for the least likely action (Quit to desktop).</summary>
        Quiet = 2,
        /// <summary>A tab in a tab bar; cobalt when it is the open tab.</summary>
        Tab = 3
    }

    /// <summary>
    /// The look and motion every kit control shares: hover lift with closing corner brackets, the press stamp,
    /// the release spring, the confirm tick, the refusal shake with its amber plate, and the disabled dashed
    /// outline. Driven by UiMotion on unscaled time; every value comes from UiTokens. Feel table §2.2.
    /// The references are filled by the kit's prefab builder; a missing one is simply not drawn.
    /// </summary>
    [Serializable]
    public sealed class UiControlFx : IUiAnimated
    {
        [Tooltip("The kit's tokens: colours, sizes and motion. Every control points at the same asset.")]
        public UiTokens Tokens;
        [Tooltip("Which colour scheme this control uses.")]
        public UiVariant Variant = UiVariant.Secondary;

        [Header("Parts (filled by the kit builder)")]
        [Tooltip("The visible body that lifts and stamps. Moving it never moves the clickable area.")]
        public RectTransform Plate;
        [Tooltip("The plate's fill.")]
        public Image Fill;
        [Tooltip("The plate's 1 px keyline.")]
        public Image Outline;
        [Tooltip("The dashed outline drawn instead of the keyline while the control can't be used.")]
        public Image Dashed;
        [Tooltip("The hairline shadow that shows the plate lifting on hover.")]
        public Image Shadow;
        [Tooltip("The main label.")]
        public TMP_Text Label;
        [Tooltip("Extra marks that take the label's colour (bullets, arrows, a check box).")]
        public Graphic[] Inks;
        [Tooltip("The electric underline on quiet buttons and tabs.")]
        public Image Underline;
        [Tooltip("The four corner brackets that close in on hover: top-left, top-right, bottom-left, bottom-right.")]
        public Image[] HoverBrackets;
        [Tooltip("The cyan tick at the left edge on confirm.")]
        public Image ConfirmTick;
        [Tooltip("The amber plate behind a refused control.")]
        public Image RefusalPlate;
        [Tooltip("A settings row's highlight behind the whole row while this control has focus.")]
        public Image RowHighlight;
        [Tooltip("The row highlight's cobalt rule on its left edge.")]
        public Image RowRule;

        [NonSerialized] bool _interactable = true;
        [NonSerialized] bool _hovered;
        [NonSerialized] bool _pressed;
        [NonSerialized] bool _focused;
        [NonSerialized] bool _active;
        [NonSerialized] bool _ready;
        [NonSerialized] Vector2 _plateHome;
        [NonSerialized] Vector2[] _bracketHome;

        [NonSerialized] UiTween _hover;
        [NonSerialized] UiTween _focus;
        [NonSerialized] UiTween _pressDown;
        [NonSerialized] UiSpring _press;
        [NonSerialized] UiSpring _shake;
        [NonSerialized] UiTween _refusal;
        [NonSerialized] UiTween _tick;

        /// <summary>For a tab or a toggle-like button: drawn as the chosen one (cobalt for tabs).</summary>
        public bool Active
        {
            get => _active;
            set { if (_active == value) return; _active = value; Wake(); }
        }

        void Prepare()
        {
            if (_ready)
                return;
            _ready = true;
            if (Plate != null)
                _plateHome = Plate.anchoredPosition;
            if (HoverBrackets != null)
            {
                _bracketHome = new Vector2[HoverBrackets.Length];
                for (int i = 0; i < HoverBrackets.Length; i++)
                    _bracketHome[i] = HoverBrackets[i] != null ? HoverBrackets[i].rectTransform.anchoredPosition : Vector2.zero;
            }
        }

        /// <summary>Sets what the control is doing now; the visuals catch up through the motion driver.</summary>
        public void SetState(bool interactable, bool hovered, bool pressed, bool focused, bool instant)
        {
            Prepare();
            if (Tokens == null)
                return;

            bool changedUse = interactable != _interactable;
            if (hovered != _hovered || changedUse)
                StartFade(ref _hover, hovered && interactable);
            if (focused != _focused || changedUse)
                StartFade(ref _focus, focused && interactable);
            if (pressed != _pressed)
                StartPress(pressed && interactable);

            _interactable = interactable;
            _hovered = hovered;
            _pressed = pressed && interactable;
            _focused = focused;

            if (instant)
                SnapAll();
            else
                Wake();
        }

        void StartFade(ref UiTween tween, bool on)
        {
            tween.Start(tween.Value, on ? 1f : 0f, on ? Tokens.HoverIn : Tokens.HoverOut, on ? Tokens.EaseOut : Tokens.EaseIn);
        }

        void StartPress(bool down)
        {
            if (down)
                _pressDown.Start(_press.Value, 1f, Tokens.PressDown, Tokens.EaseOut);
            else
                _press.Target = 0f;
        }

        void SnapAll()
        {
            _hover.Snap(_hovered && _interactable ? 1f : 0f);
            _focus.Snap(_focused && _interactable ? 1f : 0f);
            _press.Snap(_pressed ? 1f : 0f);
            _pressDown.Snap(_press.Value);
            Apply();
        }

        /// <summary>A keyboard or pad confirm: stamp down and let go, the press a mouse click would give.</summary>
        public void Stamp()
        {
            Prepare();
            if (Tokens == null)
                return;
            _press.Snap(1f);
            _press.Target = 0f;
            _pressDown.Snap(1f);
            Wake();
        }

        /// <summary>The cyan tick at the left edge: this control was confirmed.</summary>
        public void Confirm()
        {
            Prepare();
            if (Tokens == null)
                return;
            _tick.Start(0f, 1f, Tokens.ConfirmTick, Tokens.EaseOut);
            Wake();
        }

        /// <summary>A refused press: amber plate behind, and a decaying sideways shake (a hold, with Reduce motion).</summary>
        public void Refuse()
        {
            Prepare();
            if (Tokens == null)
                return;
            _refusal.Start(1f, 0f, Tokens.HoverOut, Tokens.EaseIn, Tokens.ErrorHold);
            if (!UiMotion.Reduced)
            {
                _shake.Value = Tokens.ErrorShakeDistance;
                _shake.Velocity = 0f;
                _shake.Target = 0f;
            }

            Wake();
        }

        public void Wake()
        {
            if (Application.isPlaying)
                UiMotion.Wake(this);
            else
                Apply();
        }

        public void Sleep() => UiMotion.Sleep(this);

        public bool Tick(float dt)
        {
            if (Tokens == null)
                return false;

            bool moving = _hover.Running | _focus.Running | _refusal.Running | _tick.Running;
            _hover.Step(dt);
            _focus.Step(dt);
            _refusal.Step(dt);
            _tick.Step(dt);

            if (_pressed)
            {
                _pressDown.Step(dt);
                _press.Value = _pressDown.Value;
                _press.Velocity = 0f;
                _press.Target = 1f;
                moving |= _pressDown.Running;
            }
            else
            {
                moving |= _press.Step(dt, Tokens.PressRelease);
            }

            moving |= _shake.Step(dt, Tokens.ErrorShake);
            Apply();
            return moving;
        }

        /// <summary>Draws the current state. Called by Tick, or straight away when not playing.</summary>
        public void Apply()
        {
            Prepare();
            if (Tokens == null)
                return;

            bool reduced = UiMotion.Reduced;
            float hover = _hover.Value;
            float press = Mathf.Clamp01(_press.Value);
            ResolveColours(hover, press, out Color fill, out Color ink, out Color outline);
            ApplyColours(fill, ink, outline);
            ApplyPlate(hover, press, reduced);
            ApplyUnderline(hover, reduced);
            ApplyBrackets(hover, reduced);
            ApplyMarks(hover, reduced);
        }

        void ApplyColours(Color fill, Color ink, Color outline)
        {
            if (Fill != null)
                Fill.color = fill;
            if (Label != null)
                Label.color = ink;
            if (Inks != null)
            {
                foreach (Graphic g in Inks)
                {
                    if (g != null)
                        g.color = ink;
                }
            }

            if (Outline != null)
            {
                Outline.color = outline;
                Outline.enabled = _interactable && outline.a > 0f;
            }

            if (Dashed != null)
            {
                Dashed.enabled = !_interactable;
                Dashed.color = Tokens.Disabled;
            }
        }

        void ApplyPlate(float hover, float press, bool reduced)
        {
            float shift = reduced ? 0f : Tokens.PressShift * press;
            float shake = reduced ? 0f : _shake.Value;
            if (Plate != null)
                Plate.anchoredPosition = _plateHome + new Vector2(shift + shake, -shift);

            if (Shadow == null)
                return;
            float lift = hover * (1f - press);
            float offset = reduced ? Tokens.LiftOffset : Tokens.LiftOffset * lift;
            Shadow.rectTransform.anchoredPosition = _plateHome + new Vector2(offset + shake, -offset);
            Shadow.color = UiTokens.WithAlpha(Tokens.Navy, Tokens.ShadowAlpha * Mathf.Min(1f, lift * 2f));
            Shadow.enabled = _interactable && Variant != UiVariant.Quiet;
        }

        void ApplyUnderline(float hover, bool reduced)
        {
            if (Underline == null)
                return;
            bool openTab = _active && Variant == UiVariant.Tab;
            float shown = _interactable ? Mathf.Max(hover, openTab ? 1f : 0f) : 0f;
            Underline.color = UiTokens.WithAlpha(Tokens.Electric, shown);
            Underline.rectTransform.localScale = new Vector3(reduced || openTab ? 1f : Mathf.Max(0.001f, hover), 1f, 1f);
        }

        void ApplyMarks(float hover, bool reduced)
        {
            if (ConfirmTick != null)
            {
                float t = _tick.Value;
                ConfirmTick.rectTransform.localScale = reduced ? Vector3.one : new Vector3(t, t, 1f);
                ConfirmTick.color = UiTokens.WithAlpha(Tokens.Cyan, t);
            }

            if (RefusalPlate != null)
                RefusalPlate.color = UiTokens.WithAlpha(Tokens.Amber, _refusal.Value);

            float row = Mathf.Max(_focus.Value, hover);
            if (RowHighlight != null)
                RowHighlight.color = UiTokens.WithAlpha(Tokens.HoverTint, row * Tokens.PlateAlpha);
            if (RowRule != null)
            {
                RowRule.color = UiTokens.WithAlpha(Tokens.Cobalt, _focus.Value);
                RowRule.rectTransform.localScale = new Vector3(1f, reduced ? 1f : Mathf.Max(0.001f, _focus.Value), 1f);
            }
        }

        void ApplyBrackets(float hover, bool reduced)
        {
            if (HoverBrackets == null || _bracketHome == null)
                return;
            float gap = reduced ? 0f : Tokens.HoverBracketGap * (1f - hover);
            Color colour = UiTokens.WithAlpha(Tokens.Navy, _interactable ? hover : 0f);
            for (int i = 0; i < HoverBrackets.Length; i++)
            {
                Image b = HoverBrackets[i];
                if (b == null)
                    continue;
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = i < 2 ? 1f : -1f;
                b.rectTransform.anchoredPosition = _bracketHome[i] + new Vector2(sx * gap, sy * gap);
                b.color = colour;
            }
        }

        static readonly Color PrimaryHover = new(0.86f, 0.89f, 0.96f);
        static readonly Color PrimaryPress = new(0.62f, 0.67f, 0.80f);

        void ResolveColours(float hover, float press, out Color fill, out Color ink, out Color outline)
        {
            UiTokens t = Tokens;
            if (!_interactable)
            {
                // The primary's fill is the signature gradient sprite, tinted: faded rather than painted paper.
                fill = Variant == UiVariant.Quiet ? Color.clear : Variant == UiVariant.Primary ? UiTokens.WithAlpha(Color.white, 0.35f) : t.Paper;
                ink = t.Disabled;
                outline = Color.clear;
                return;
            }

            switch (Variant)
            {
                case UiVariant.Primary:
                    // The fill sprite carries the cobalt-to-electric gradient; the tint only deepens it on hover and press.
                    fill = Color.Lerp(Color.Lerp(Color.white, PrimaryHover, hover), PrimaryPress, press * 0.6f);
                    ink = t.Paper;
                    outline = Color.clear;
                    return;
                case UiVariant.Quiet:
                    fill = UiTokens.WithAlpha(Color.Lerp(t.HoverTint, t.Silver, press), Mathf.Max(hover, press) * t.PlateAlpha);
                    ink = t.Navy;
                    outline = Color.clear;
                    return;
                case UiVariant.Tab:
                    if (_active)
                    {
                        fill = Color.Lerp(t.Cobalt, t.CobaltDeep, Mathf.Max(hover, press));
                        ink = t.Paper;
                    }
                    else
                    {
                        fill = UiTokens.WithAlpha(Color.Lerp(t.HoverTint, t.Silver, press), Mathf.Max(hover, press));
                        ink = t.Navy;
                    }

                    outline = Color.clear;
                    return;
                default:
                    fill = Color.Lerp(Color.Lerp(t.Paper, t.HoverTint, hover), t.Silver, press);
                    ink = t.Navy;
                    outline = UiTokens.WithAlpha(t.Navy, Mathf.Lerp(0.16f, 0.4f, hover));   // the bevel and shadow carry the shape
                    return;
            }
        }
    }
}
