using System;
using System.Collections.Generic;
using CloseTheDeal.UI.Title;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Manages active UiScreen stack, input routing, and covered screen transitions.</summary>
    [AddComponentMenu("Close the Deal/UI/Screen Stack")]
    public sealed class UiScreenStack : MonoBehaviour
    {
        readonly List<UiScreen> _stack = new(8);
        readonly List<UiScreen> _closing = new(8);

        /// <summary>Raised after the stack changes (push, pop, replace, clear).</summary>
        public event Action Changed;

        public UiScreen Top => _stack.Count > 0 ? _stack[^1] : null;

        public int Count => _stack.Count;

        /// <summary>True while a change waits for lane 8's transition to cover the view: further changes are ignored.</summary>
        public bool Busy { get; private set; }

        /// <summary>Off: every change runs at once with the screens' own motion (capture tools that step motion by hand).</summary>
        public static bool UseTransitions = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => UseTransitions = true;

        int _generation;
        bool _underCover;
        Action _pending;
        float _pendingSince;
        float _pendingFor;
        readonly List<UiScreen> _settle = new(8);

        /// <summary>Defers change until transition covers the screen, then settles screens; returns false if un-covered.</summary>
        public bool RunCovered(UiTransitionKind? kind, Action change)
        {
            if (kind == null || _underCover || !UseTransitions || !Application.isPlaying)
                return false;

            Busy = true;
            _pending = change;
            _pendingSince = Time.unscaledTime;
            _pendingFor = UiTransition.Duration(kind.Value) + 0.5f;
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            int generation = _generation;
            UiTransition.Play(kind.Value, () =>
            {
                if (generation == _generation)
                    RunPending();
            });
            return true;
        }

        void RunPending()
        {
            Action change = _pending;
            _pending = null;
            Busy = false;
            if (change == null)
                return;
            _underCover = true;
            try
            {
                change();
            }
            finally
            {
                _underCover = false;
            }

            GetComponentsInChildren(false, _settle); // every screen still showing or still hiding
            foreach (UiScreen screen in _settle)
                screen.Settle();
            _settle.Clear();
            if (Top != null)
                Top.Refocus();
        }

        // Watchdog: if a covered transition overlay was destroyed mid-flight, flush pending change so Busy cannot stick.
        void Update()
        {
            if (Busy && !UiTransition.IsPlaying && Time.unscaledTime - _pendingSince > _pendingFor)
            {
                Debug.LogWarning("[UI] A screen change waited for a transition that never covered the view; it runs now.", this);
                RunPending();
            }
        }

        /// <summary>Opens a screen on top. A full screen sends the one below out; a dialog covers it.</summary>
        public void Push(UiScreen screen)
        {
            if (screen == null || _stack.Contains(screen) || Busy)
                return;

            UiScreen below = Top;
            if (below != null && RunCovered(screen.Through, () => PushNow(screen)))
                return; // lane 8's overlay plays its own sound as it starts (lane 9's set)
            if (below != null)
                UiSound.Play(screen.OpenSound);
            PushNow(screen);
        }

        void PushNow(UiScreen screen)
        {
            if (_stack.Contains(screen))
                return;
            UiScreen below = Top;
            float wait = 0f;
            if (below != null)
            {
                if (screen.IsDialog)
                    below.Cover();
                else
                {
                    below.Hide(false);
                    wait = below.ClearTime;
                }
            }

            _stack.Add(screen);
            screen.Stack = this;
            screen.Show(false, wait);
            Changed?.Invoke();
        }

        /// <summary>Closes the top screen and restores the screen below it.</summary>
        public void Pop(bool playSound = true)
        {
            if (_stack.Count == 0 || Busy)
                return;

            UiScreen top = _stack[^1];
            UiScreen under = _stack.Count > 1 ? _stack[^2] : null;
            if (playSound)
                UiSound.Play(top.IsDialog ? UiSoundEvent.PanelClose : UiSoundEvent.Back);
            if (under != null && RunCovered(top.Through, () => PopNow(top)))
                return;
            PopNow(top);
        }

        void PopNow(UiScreen top)
        {
            if (Top != top)
                return;
            _stack.RemoveAt(_stack.Count - 1);
            UiScreen below = Top;
            top.Hide(true, below == null);
            if (below != null)
            {
                if (top.IsDialog)
                    below.Uncover();
                else
                    below.Show(true, top.ClearTime);
            }

            Changed?.Invoke();
        }

        /// <summary>Swaps the top screen for another of the same kind in one transition.</summary>
        public void Replace(UiScreen screen)
        {
            if (screen == null || Busy)
                return;
            if (_stack.Count == 0)
            {
                Push(screen);
                return;
            }

            UiScreen top = _stack[^1];
            if (top == screen)
                return;
            if (RunCovered(screen.Through ?? top.Through, () => ReplaceNow(top, screen)))
                return; // the overlay's own sound answers
            UiSound.Play(screen.IsDialog ? screen.OpenSound : UiSoundEvent.Transition);
            ReplaceNow(top, screen);
        }

        void ReplaceNow(UiScreen top, UiScreen screen)
        {
            if (Top != top)
                return;
            if (top.IsDialog != screen.IsDialog)
            {
                PopNow(top);
                PushNow(screen);
                return;
            }

            _stack[^1] = screen;
            top.Hide(false);
            screen.Stack = this;
            screen.Show(false, top.ClearTime);
            Changed?.Invoke();
        }

        /// <summary>Closes every screen with its hide motion (the game took over: hosting, joining, playing).</summary>
        public void Clear()
        {
            _generation++;
            Busy = false;
            _pending = null;
            if (_stack.Count == 0)
                return;
            var closing = _closing;
            closing.Clear();
            closing.AddRange(_stack);
            _stack.Clear(); // Emptied first: a screen reacting to its hide (a dialog it owns) finds nothing to pop.
            for (int i = closing.Count - 1; i >= 0; i--)
                closing[i].Hide(true, true);
            closing.Clear();
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
