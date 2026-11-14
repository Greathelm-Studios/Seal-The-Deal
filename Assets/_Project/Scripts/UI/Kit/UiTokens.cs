using System;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>A type role: which font, how big, how wide. One per kind of text (title, button label, caption...).</summary>
    [Serializable]
    public struct UiTypeRole
    {
        [Tooltip("The font this text uses. Until the shipping faces are licensed and imported this is TextMeshPro's bundled Liberation Sans.")]
        public TMP_FontAsset Font;

        [Tooltip("Text size in pixels at 1920 x 1080. The menu scales with the screen, so this is the size on a 1080p monitor.")]
        public float Size;

        [Tooltip("Extra space between letters, in hundredths of the text size. 0 = the font's own spacing; 22 = the title's wide spread.")]
        public float Tracking;

        [Tooltip("Bold, italic or capitals for this role. Leave plain unless the role needs it.")]
        public FontStyles Style;

        [Tooltip("Optional look for this role's letters (thinner or heavier strokes) from a TextMeshPro material made from the same font. Empty = the font's own.")]
        public Material Material;

        [Tooltip("The shipping face this role is meant to use, for whoever imports the fonts. Shown here only; the game reads the Font field.")]
        public string TargetFace;
    }

    /// <summary>A spring for motion that must be interruptible: damping ratio and response time, as in the feel table.</summary>
    [Serializable]
    public struct UiSpringTuning
    {
        [Tooltip("1 = settles without overshooting (every move in the kit). Below 1 it wobbles past its target: only the refusal shake uses that.")]
        [Range(0.05f, 2f)] public float Damping;

        [Tooltip("Roughly how long the move takes, in seconds. At damping 1 it has settled after about 0.93 x this.")]
        [Range(0.02f, 1f)] public float Response;

        public UiSpringTuning(float damping, float response)
        {
            Damping = damping;
            Response = response;
        }
    }

    /// <summary>
    /// Every colour, size, type role, spacing and motion value the UI kit uses, in one asset so a designer
    /// tunes the whole menu from one Inspector. Designer-owned: the game only reads it (AGENTS.md §4).
    /// Colours are the art bible's seven swatches plus Amber; motion is the feel table in
    /// ArtSource/UI/research/feel_wii_ds.md §2.2. Guide: docs/systems/UI_KIT.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/UI Tokens", fileName = "UiTokens")]
    public sealed class UiTokens : ScriptableObject
    {
        // ---- Colour ----------------------------------------------------------------------------

        [Header("Colour: the bible's swatches")]
        [Tooltip("Paper White: every light field and plate. Navy text on it reads 15:1.")]
        public Color Paper = Hex(0xF3F4F4);
        [Tooltip("Silver Grey: quiet frames and the pressed face of light buttons. Never a text colour on paper.")]
        public Color Silver = Hex(0xC9CED3);
        [Tooltip("Deep Navy: all type and keylines. The only text colour on light surfaces.")]
        public Color Navy = Hex(0x111B38);
        [Tooltip("Corporate Cobalt: the one primary button per screen and the active tab. Paper text on it reads 6.6:1.")]
        public Color Cobalt = Hex(0x244FB9);
        [Tooltip("The primary button's hover and pressed fill: a deeper cobalt so the press reads as pushed in.")]
        public Color CobaltDeep = Hex(0x1B3C8F);
        [Tooltip("Electric Blue: rules, hover underlines and the focus ring. A mark colour, never text on paper.")]
        public Color Electric = Hex(0x2879EF);
        [Tooltip("Cyan: 'you' marks and the confirm tick. Always with a navy keyline on light surfaces, never as text.")]
        public Color Cyan = Hex(0x20D4E6);
        [Tooltip("Near Black: one hairline or bullet as an accent, and the crosshair's outline. Never a panel fill.")]
        public Color Black = Hex(0x101116);
        [Tooltip("Amber: the rival firm and refusals only. Navy text on a solid amber plate; amber is never text itself.")]
        public Color Amber = Hex(0xFF9E1B);

        [Header("Colour: text inks (opaque on purpose)")]
        [Tooltip("Secondary text on paper. Opaque, not navy at 78%: see-through text renders lighter in this project's Linear colour space than in the mockups.")]
        public Color Ink2 = Hex(0x434B61);
        [Tooltip("Microtype on paper, the lightest text allowed (about 5.7:1). Opaque for the same reason as Ink 2.")]
        public Color Ink3 = Hex(0x596074);
        [Tooltip("Disabled labels on paper. Always paired with a dashed outline and a reason in words, never colour alone.")]
        public Color Disabled = Hex(0x8D929F);
        [Tooltip("Hairline rules and the background grid on paper. Decoration only.")]
        public Color Hair = Hex(0xBDC0C7);
        [Tooltip("The faint background grid on paper fields. Lighter than the hairline so it never competes with text.")]
        public Color Grid = Hex(0xE3E5E8);
        [Tooltip("Hover tint on light buttons: between paper and silver.")]
        public Color HoverTint = Hex(0xE4E7EA);

        [Header("Glass and plates")]
        [Tooltip("How solid a white plate is over the world (HUD chips, row highlights). 0.90 keeps navy text above 13:1 over the brightest sky.")]
        [Range(0f, 1f)] public float PlateAlpha = 0.90f;
        [Tooltip("How solid a frosted menu pane is when the real glass material is missing. 0.86 keeps navy text above 13:1 on every backdrop.")]
        [Range(0f, 1f)] public float FrostedAlpha = 0.86f;
        [Tooltip("Clear glass frames: see-through and never carrying text.")]
        [Range(0f, 1f)] public float ClearAlpha = 0.40f;
        [Tooltip("The paper veil behind a dialog, so the screen behind steps back. Paper, never a dark dim.")]
        [Range(0f, 1f)] public float VeilAlpha = 0.60f;
        [Tooltip("The soft hairline shadow under a lifted plate, as a fraction of navy.")]
        [Range(0f, 1f)] public float ShadowAlpha = 0.22f;

        // ---- Type ------------------------------------------------------------------------------

        [Header("Type roles (sizes at 1920 x 1080)")]
        [Tooltip("The game's name on the title screen: one large, thin, very wide line.")]
        public UiTypeRole Title = new() { Size = 64f, Tracking = 18f, TargetFace = "Saira Expanded Light" };
        [Tooltip("Screen headings such as SETTINGS: capitals, tracked wide.")]
        public UiTypeRole Heading = new() { Size = 40f, Tracking = 15f, TargetFace = "Saira Expanded Light" };
        [Tooltip("Button labels, menu items and settings row names.")]
        public UiTypeRole Item = new() { Size = 20f, TargetFace = "Archivo SemiExpanded Medium" };
        [Tooltip("Status lines, descriptions and dialog bodies.")]
        public UiTypeRole Body = new() { Size = 18f, TargetFace = "Archivo SemiExpanded" };
        [Tooltip("Field labels and key-hint captions: the smallest size a player must read to act.")]
        public UiTypeRole Label = new() { Size = 16f, TargetFace = "Archivo SemiExpanded Medium" };
        [Tooltip("Section headers such as § 1 ACCESSIBILITY: small capitals tracked wide.")]
        public UiTypeRole Section = new() { Size = 16f, Tracking = 18f, TargetFace = "IBM Plex Mono" };
        [Tooltip("Kit captions and values in number fields.")]
        public UiTypeRole Caption = new() { Size = 13f, Tracking = 10f, TargetFace = "IBM Plex Mono" };
        [Tooltip("Legal microtype in corners and footers: decorative, never needed to act.")]
        public UiTypeRole Micro = new() { Size = 11f, Tracking = 8f, TargetFace = "IBM Plex Mono Light" };

        // ---- Space -----------------------------------------------------------------------------

        [Header("Spacing and sizes (pixels at 1920 x 1080)")]
        [Tooltip("The small gap: between a bullet and its text, inside a key cap.")]
        public float SpaceS = 12f;
        [Tooltip("The standard gap between stacked controls.")]
        public float SpaceM = 16f;
        [Tooltip("The gap between groups.")]
        public float SpaceL = 24f;
        [Tooltip("Page margins and the gap between columns.")]
        public float SpaceXL = 48f;
        [Tooltip("Height of the primary button.")]
        public float PrimaryHeight = 64f;
        [Tooltip("Height of secondary buttons and tabs.")]
        public float SecondaryHeight = 56f;
        [Tooltip("Height of one settings row.")]
        public float RowHeight = 64f;
        [Tooltip("Corner radius of every plate and button. The survey look is square: 0.")]
        public float Radius = 0f;
        [Tooltip("How far a control's clickable area reaches past its plate, so near misses still count. Keep gaps between controls at least twice this.")]
        public float HitPadding = 8f;

        // ---- Motion ----------------------------------------------------------------------------

        [Header("Motion: hover and press (feel table §2.2)")]
        [Tooltip("Seconds for a hovered button's corner brackets to close and its plate to lift.")]
        [Range(0f, 0.5f)] public float HoverIn = 0.10f;
        [Tooltip("Seconds for the hover to undo when the pointer leaves.")]
        [Range(0f, 0.5f)] public float HoverOut = 0.07f;
        [Tooltip("How far apart the hover brackets start before closing onto the plate, in pixels.")]
        public float HoverBracketGap = 6f;
        [Tooltip("How far a hovered plate lifts: the offset of its hairline shadow, in pixels.")]
        public float LiftOffset = 2f;
        [Tooltip("Seconds for a press to stamp the plate down. Fires on button down, before the action.")]
        [Range(0f, 0.3f)] public float PressDown = 0.04f;
        [Tooltip("How far a pressed plate shifts right and down, in pixels.")]
        public float PressShift = 2f;
        [Tooltip("The spring that lets go of a pressed plate. Interruptible by the next press.")]
        public UiSpringTuning PressRelease = new(1f, 0.16f);
        [Tooltip("Seconds for the cyan confirm tick to appear at a button's left edge.")]
        [Range(0f, 0.5f)] public float ConfirmTick = 0.12f;

        [Header("Motion: focus")]
        [Tooltip("The spring the focus brackets and chevron travel on when focus moves. Selection itself changes on the input frame; only the marks catch up.")]
        public UiSpringTuning FocusTravel = new(1f, 0.12f);
        [Tooltip("How far outside the focused control the focus ring sits, in pixels.")]
        public float FocusRingGap = 3f;
        [Tooltip("Seconds with no input before the focus brackets start to breathe.")]
        public float BreatheDelay = 3f;
        [Tooltip("One breath of the focus brackets, in seconds.")]
        public float BreathePeriod = 2.4f;
        [Tooltip("How far the brackets breathe out, in pixels.")]
        public float BreatheDistance = 1f;
        [Tooltip("How far the brackets nudge when focus is pushed against the end of a list, in pixels.")]
        public float EdgeBump = 4f;

        [Header("Motion: refusal")]
        [Tooltip("The wobbly spring of the refusal shake: the only motion in the kit allowed to overshoot.")]
        public UiSpringTuning ErrorShake = new(0.25f, 0.08f);
        [Tooltip("How far a refused control jumps sideways at the start of its shake, in pixels.")]
        public float ErrorShakeDistance = 6f;
        [Tooltip("Seconds the amber refusal plate stays behind a refused control.")]
        public float ErrorHold = 0.24f;

        [Header("Motion: panels and screens")]
        [Tooltip("Seconds for a panel or dialog to slide and fade in.")]
        [Range(0f, 0.6f)] public float PanelIn = 0.20f;
        [Tooltip("Seconds for a panel or dialog to slide and fade out. About 70% of the way in.")]
        [Range(0f, 0.6f)] public float PanelOut = 0.14f;
        [Tooltip("How far a panel slides as it enters, in pixels.")]
        public float PanelSlideIn = 24f;
        [Tooltip("How far a panel slides as it leaves, in pixels.")]
        public float PanelSlideOut = 16f;
        [Tooltip("Delay between one line of a screen and the next as they reveal, in seconds.")]
        [Range(0f, 0.1f)] public float RevealStagger = 0.016f;
        [Tooltip("At most this many lines reveal one after another; the rest come in with the last.")]
        public int RevealMaxLines = 6;
        [Tooltip("Seconds for one revealed line to fade and rise into place.")]
        [Range(0f, 0.5f)] public float RevealDuration = 0.16f;
        [Tooltip("How far each revealed line rises into place, in pixels.")]
        public float RevealRise = 8f;
        [Tooltip("Seconds for the cross-fade that replaces every slide and zoom when Reduce motion is on.")]
        [Range(0f, 0.5f)] public float ReducedFade = 0.16f;

        [Header("Motion: easing curves (cubic-bezier control points)")]
        [Tooltip("OUT: fast start, gentle stop. Things arriving.")]
        public Vector4 EaseOut = new(0.2f, 0f, 0f, 1f);
        [Tooltip("OUT-EXPO: a stronger arrival, for panels.")]
        public Vector4 EaseOutExpo = new(0.16f, 1f, 0.3f, 1f);
        [Tooltip("IN: gentle start, fast finish. Things leaving.")]
        public Vector4 EaseIn = new(0.4f, 0f, 1f, 1f);
        [Tooltip("IN-OUT: for moves that both start and stop on screen.")]
        public Vector4 EaseInOut = new(0.65f, 0f, 0.35f, 1f);

        [Header("Input")]
        [Tooltip("Seconds a direction is held before focus starts repeating. Keys and pad alike.")]
        [Range(0.1f, 1f)] public float RepeatDelay = 0.4f;
        [Tooltip("Seconds between repeated focus steps while a direction stays held.")]
        [Range(0.03f, 0.5f)] public float RepeatInterval = 0.1f;
        [Tooltip("Play the quiet tick when the mouse moves onto a control. The feel table marks this a proposal: switch it off if it tires.")]
        public bool HoverSound = true;

        static Color Hex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// <summary>A colour with this alpha. For the plate, frosted and veil alphas.</summary>
        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
