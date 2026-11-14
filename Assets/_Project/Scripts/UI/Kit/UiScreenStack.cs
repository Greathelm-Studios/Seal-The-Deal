using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The kit's screens, top one in charge. Push opens a screen over the current one (a full screen slides the
    /// old one out; a dialog leaves it showing behind a veil), Pop goes back one, Replace swaps the top. Every change
    /// animates (never a cut) and plays its sound; Esc / the pad's B is passed to the top screen. Input is never
    /// locked while a screen animates. Design: docs/systems/UI_KIT.md "Screens".
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Screen Stack")]
    public sealed class UiScreenStack : MonoBehaviour
    {
        readonly List<UiScreen> _stack = new(8);

        /// <summary>Raised after the stack changes (push, pop, replace, clear).</summary>
        public event Action Changed;

        public UiScreen Top => _stack.Count > 0 ? _stack[^1] : null;

        public int Count => _stack.Count;

        /// <summary>Opens a screen on top. A full screen sends the one below out; a dialog covers it.</summary>
        public void Push(UiScreen screen)
        {
            if (screen == null || _stack.Contains(screen))
                return;

            UiScreen below = Top;
            if (below != null)
            {
                if (screen.IsDialog)
                    below.Cover();
                else
                    below.Hide(false);
            }

            _stack.Add(screen);
            screen.Stack = this;
            screen.Show(false);
            UiSound.Play(below == null ? UiSoundEvent.None : screen.IsDialog ? UiSoundEvent.PanelOpen : UiSoundEvent.Transition);
            Changed?.Invoke();
        }

        /// <summary>
        /// Closes the top screen and brings back the one below, where the player left it. Plays Back (a screen) or
        /// PanelClose (a dialog) unless the caller already played its own answer this frame.
        /// </summary>
        public void Pop(bool playSound = true)
        {
            if (_stack.Count == 0)
                return;

            UiScreen top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Hide(true);
            UiScreen below = Top;
            if (below != null)
            {
                if (top.IsDialog)
                    below.Uncover();
                else
                    below.Show(true);
            }

            if (playSound)
                UiSound.Play(top.IsDialog ? UiSoundEvent.PanelClose : UiSoundEvent.Back);
            Changed?.Invoke();
        }

        /// <summary>
        /// Swaps the top screen for another of the same kind (screen for screen, dialog for dialog) with one transition.
        /// Swapping between kinds goes back one and opens the new one instead.
        /// </summary>
        public void Replace(UiScreen screen)
        {
            if (screen == null)
                return;
            if (_stack.Count == 0)
            {
                Push(screen);
                return;
            }

            UiScreen top = _stack[^1];
            if (top == screen)
                return;
            if (top.IsDialog != screen.IsDialog)
            {
                Pop(false);
                Push(screen);
                return;
            }

            _stack[^1] = screen;
            top.Hide(false);
            screen.Stack = this;
            screen.Show(false);
            UiSound.Play(UiSoundEvent.Transition);
            Changed?.Invoke();
        }

        /// <summary>Closes every screen with its hide motion (the game took over: hosting, joining, playing).</summary>
        public void Clear()
        {
            if (_stack.Count == 0)
                return;
            for (int i = _stack.Count - 1; i >= 0; i--)
                _stack[i].Hide(true);
            _stack.Clear();
            Changed?.Invoke();
        }

        /// <summary>Esc / B: the top screen decides (most go back; the title asks before quitting).</summary>
        public void Cancel()
        {
            UiScreen top = Top;
            if (top != null)
                top.OnCancel();
        }
    }
}
