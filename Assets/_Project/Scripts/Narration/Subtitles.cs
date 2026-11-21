using System;
using UnityEngine;

namespace CloseTheDeal.Narration
{
    /// <summary>One spoken line, for a subtitle and, later, a voice clip.</summary>
    public readonly struct SubtitleLine
    {
        /// <summary>Who says it, for the subtitle's label. "Secretary" for her.</summary>
        public readonly string Speaker;
        /// <summary>The finished text, every {token} already filled in.</summary>
        public readonly string Text;
        /// <summary>How long it stays up, in seconds.</summary>
        public readonly float Seconds;
        /// <summary>Higher interrupts lower. See SubtitlePriority.</summary>
        public readonly int Priority;
        /// <summary>What prompted it (a moment name such as "Steal"), so a voice can pick a clip.</summary>
        public readonly string Moment;

        public SubtitleLine(string speaker, string text, float seconds, int priority, string moment)
        {
            Speaker = speaker;
            Text = text;
            Seconds = seconds;
            Priority = priority;
            Moment = moment;
        }
    }

    /// <summary>Single local subtitle stream shared by match and reception narration. Design: docs/systems/NARRATION.md.</summary>
    public static class Subtitles
    {
        /// <summary>A line starts. A new one replaces whatever is showing.</summary>
        public static event Action<SubtitleLine> Shown;

        /// <summary>The line on screen now, if its time isn't up.</summary>
        public static bool TryGetCurrent(out SubtitleLine line)
        {
            line = _current;
            return _hasCurrent && Time.unscaledTime < _until;
        }

        static SubtitleLine _current;
        static bool _hasCurrent;
        static float _until;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shown = null;
            _hasCurrent = false;
            _until = 0f;
        }

        /// <summary>Shows a line unless a higher-priority one is still up. Returns whether it was shown.</summary>
        public static bool Show(SubtitleLine line)
        {
            if (string.IsNullOrEmpty(line.Text))
                return false;
            if (TryGetCurrent(out SubtitleLine current) && current.Priority > line.Priority)
                return false;

            _current = line;
            _hasCurrent = true;
            _until = Time.unscaledTime + line.Seconds;
            Shown?.Invoke(line);
            return true;
        }

        /// <summary>How long a line should stay up: seconds per word, never under the floor.</summary>
        public static float SecondsFor(string text, float secondsPerWord, float floor)
        {
            int words = 1;
            for (int i = 0; i < text.Length; i++)
                words += text[i] == ' ' ? 1 : 0;
            return Mathf.Max(floor, words * secondsPerWord);
        }
    }
}
