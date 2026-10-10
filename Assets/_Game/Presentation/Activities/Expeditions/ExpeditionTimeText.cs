using System;
using PocketWilds.Core;

namespace PocketWilds.Presentation
{
    /// <summary>
    /// T42: an expedition's time in plain real time, written into reused char buffers (no allocation): « 3 h 12 », « 4 h »,
    /// « 12 min », « 45 s » (seconds only in the last minute: the first stroll lasts 3 min), inside « De retour dans {time} ».
    /// Minutes and seconds are rounded up, so the label never reads « 0 » while the team is still away. Templates are read
    /// once per bind; <see cref="Key"/> tells the caller whether the shown text would change.
    /// </summary>
    public sealed class ExpeditionTimeText
    {
        private const char NoBreakSpace = (char)0x00A0;
        private const char LineFeed = (char)0x000A;

        private string _hoursMinutes = "{h} h {m}";
        private string _hours = "{h} h";
        private string _minutes = "{n} min";
        private string _seconds = "{n} s";
        private string _backIn = "{time}";

        public void Bind(ILocalization text)
        {
            if (text == null)
                return;
            _hoursMinutes = text.Get(ExpeditionText.TimeHoursMinutes);
            _hours = text.Get(ExpeditionText.TimeHours);
            _minutes = text.Get(ExpeditionText.TimeMinutes);
            _seconds = text.Get(ExpeditionText.TimeSeconds);
            _backIn = text.Get(ExpeditionText.BackIn);
        }

        /// <summary>
        /// What the label shows, as one number: seconds in the last minute, else whole minutes (negative), so a caller
        /// rewrites the label only when this changes (once a second at most, once a minute above a minute).
        /// </summary>
        public static long Key(double seconds)
        {
            long s = (long)Math.Ceiling(Math.Max(0.0, seconds));
            return s < 60 ? s : -((s + 59) / 60);
        }

        /// <summary>« 3 h 12 » into <paramref name="buffer"/> from <paramref name="i"/>; returns the new length.</summary>
        public int WriteTime(char[] buffer, int i, double seconds)
        {
            long s = (long)Math.Ceiling(Math.Max(0.0, seconds));
            if (s < 60)
                return Fill(buffer, i, _seconds, s, 0);
            long minutes = (s + 59) / 60;
            if (minutes < 60)
                return Fill(buffer, i, _minutes, minutes, 0);
            long hours = minutes / 60;
            long rest = minutes % 60;
            return rest == 0 ? Fill(buffer, i, _hours, hours, 0) : Fill(buffer, i, _hoursMinutes, hours, rest);
        }

        /// <summary>« De retour dans 3 h 12 »; with <paramref name="lineBreak"/> the time goes on its own line (the slot card).</summary>
        public int WriteBackIn(char[] buffer, double seconds, bool lineBreak = false)
        {
            string template = _backIn ?? "{time}";
            int at = template.IndexOf("{time}", StringComparison.Ordinal);
            if (at < 0)
                return WriteTime(buffer, 0, seconds);
            int i = 0;
            for (int c = 0; c < at && i < buffer.Length; c++)
                buffer[i++] = template[c];
            if (lineBreak && i > 0)
            {
                while (i > 0 && (buffer[i - 1] == ' ' || buffer[i - 1] == NoBreakSpace))
                    i--;
                if (i < buffer.Length)
                    buffer[i++] = LineFeed;
            }
            i = WriteTime(buffer, i, seconds);
            for (int c = at + 6; c < template.Length && i < buffer.Length; c++)
                buffer[i++] = template[c];
            return i;
        }

        /// <summary>The time alone as a string (the hub card, built when it changes; not per frame).</summary>
        public string TimeString(double seconds)
        {
            var buffer = new char[48];
            return new string(buffer, 0, WriteTime(buffer, 0, seconds));
        }

        public string BackInString(double seconds)
        {
            var buffer = new char[96];
            return new string(buffer, 0, WriteBackIn(buffer, seconds));
        }

        /// <summary>{n} and {h} take the first number, {m} the second on two digits; other text is copied.</summary>
        public static int Fill(char[] buffer, int i, string template, long first, long second)
        {
            if (template == null)
                return i;
            for (int c = 0; c < template.Length && i < buffer.Length; c++)
            {
                char ch = template[c];
                if (ch == '{' && c + 2 < template.Length && template[c + 2] == '}')
                {
                    char token = template[c + 1];
                    if (token == 'n' || token == 'h')
                    {
                        i = Int(buffer, i, first);
                        c += 2;
                        continue;
                    }
                    if (token == 'm')
                    {
                        if (second < 10 && i < buffer.Length)
                            buffer[i++] = '0';
                        i = Int(buffer, i, second);
                        c += 2;
                        continue;
                    }
                }
                buffer[i++] = ch;
            }
            return i;
        }

        public static int Int(char[] buffer, int i, long value)
        {
            if (value < 0)
            {
                if (i < buffer.Length)
                    buffer[i++] = '-';
                value = -value;
            }
            int digits = 1;
            for (long v = value; v >= 10; v /= 10)
                digits++;
            if (i + digits > buffer.Length)
                return i;
            for (int d = i + digits - 1; d >= i; d--)
            {
                buffer[d] = (char)('0' + value % 10);
                value /= 10;
            }
            return i + digits;
        }
    }
}
