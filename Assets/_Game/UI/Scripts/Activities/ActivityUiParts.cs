using PocketWilds.Core;
using PocketWilds.Presentation;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// T37: allocation-free writers for the activity screens' sentences (ActivityText placeholders): {n} and {h} take the
    /// first number, {total}, {price} and {m} the second ({m} on two digits, « 1 h 05 »), {goal} a string. Other braces
    /// are copied as they are.
    /// </summary>
    public static class ActivityChars
    {
        public static int Fill(char[] buffer, int i, string template, long first, long second = 0, string goal = null)
        {
            if (template == null)
                return i;
            int length = template.Length;
            for (int c = 0; c < length && i < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{')
                {
                    int end = template.IndexOf('}', c + 1);
                    if (end > c && end - c <= 8)
                    {
                        if (Is(template, c + 1, end, "n") || Is(template, c + 1, end, "h"))
                        {
                            i = UiChars.Int(buffer, i, first);
                            c = end;
                            continue;
                        }
                        if (Is(template, c + 1, end, "total") || Is(template, c + 1, end, "price"))
                        {
                            i = UiChars.Int(buffer, i, second);
                            c = end;
                            continue;
                        }
                        if (Is(template, c + 1, end, "m"))
                        {
                            if (second < 10)
                                i = UiChars.Char(buffer, i, '0');
                            i = UiChars.Int(buffer, i, second);
                            c = end;
                            continue;
                        }
                        if (Is(template, c + 1, end, "goal"))
                        {
                            i = UiChars.Str(buffer, i, goal);
                            c = end;
                            continue;
                        }
                    }
                }
                buffer[i++] = ch;
            }
            return i;
        }

        private static bool Is(string s, int start, int end, string token)
        {
            if (end - start != token.Length)
                return false;
            for (int k = 0; k < token.Length; k++)
            {
                if (s[start + k] != token[k])
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The time to the next free ticket in plain real time (doc Mini-jeux section 2): « Prochain billet dans 12 min »,
    /// « dans 2 h », « dans 1 h 05 », or « Tous tes billets sont prêts » when the reserve is full. Minutes only, never
    /// seconds: the label changes at most once a minute, nothing ticks. Templates read once per bind.
    /// </summary>
    public sealed class TicketTimeText
    {
        private string _minutes;
        private string _hours;
        private string _hoursMinutes;
        private string _full;

        public void Bind(ILocalization text)
        {
            if (text == null)
                return;
            _minutes = text.Get(ActivityText.TicketsNextMinutes);
            _hours = text.Get(ActivityText.TicketsNextHours);
            _hoursMinutes = text.Get(ActivityText.TicketsNextHoursMinutes);
            _full = text.Get(ActivityText.TicketsFull);
        }

        /// <summary>Writes the line for <paramref name="minutesToNext"/> (0 or less: the reserve is full).</summary>
        public void Write(UiLabel label, int minutesToNext)
        {
            if (label == null)
                return;
            var buffer = label.Buffer;
            if (minutesToNext <= 0)
            {
                label.Commit(UiChars.Str(buffer, 0, _full));
                return;
            }
            int hours = minutesToNext / 60;
            int minutes = minutesToNext % 60;
            int length;
            if (hours == 0)
                length = ActivityChars.Fill(buffer, 0, _minutes, minutes);
            else if (minutes == 0)
                length = ActivityChars.Fill(buffer, 0, _hours, hours);
            else
                length = ActivityChars.Fill(buffer, 0, _hoursMinutes, hours, minutes);
            label.Commit(length);
        }
    }

    /// <summary>The activity screens' curves (doc 05 Sensations sheet 20), in seconds.</summary>
    public static class ActivityCurves
    {
        public const float StarSeconds = 0.35f;
        public const float FlipSeconds = 0.25f;

        /// <summary>A result star: 0 to 1.20 in 160 ms (strong ease out), back to 1.00 at 350 ms.</summary>
        public static float StarPop(float t)
        {
            if (t <= 0f)
                return 0f;
            if (t < 0.16f)
            {
                float k = 1f - t / 0.16f;
                return 1.2f * (1f - k * k * k);
            }
            if (t < StarSeconds)
                return Mathf.Lerp(1.2f, 1f, UiCurves.Smooth((t - 0.16f) / (StarSeconds - 0.16f)));
            return 1f;
        }

        /// <summary>A soft pop (the record ribbon, a mark reached): 0.6 to 1.08 in 180 ms, 1.00 at 320 ms.</summary>
        public static float SoftPop(float t)
        {
            if (t <= 0f)
                return 0.6f;
            if (t < 0.18f)
                return Mathf.Lerp(0.6f, 1.08f, UiCurves.EaseOut(t / 0.18f));
            if (t < 0.32f)
                return Mathf.Lerp(1.08f, 1f, UiCurves.Smooth((t - 0.18f) / 0.14f));
            return 1f;
        }

        /// <summary>A small bump of something already shown (a score that grows): 1.00 to 1.07 and back in 180 ms.</summary>
        public static float Bump(float t)
        {
            if (t <= 0f || t >= 0.18f)
                return 1f;
            return 1f + 0.07f * Mathf.Sin(t / 0.18f * Mathf.PI);
        }
    }
}
