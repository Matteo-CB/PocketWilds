using TMPro;

namespace PocketWilds.UI
{
    /// <summary>
    /// Small integers and "a/b" counters written into a reused char buffer (no string per bind).
    /// Counts in the Codex stay below a billion; larger values use BigValueText.
    /// </summary>
    public sealed class UiNumberText
    {
        private readonly char[] _buffer = new char[32];

        public void SetCount(TMP_Text label, int value, char prefix = default)
        {
            int length = 0;
            if (prefix != default(char))
                _buffer[length++] = prefix;
            length = Write(value, length);
            label.SetText(_buffer, 0, length);
        }

        public void SetRatio(TMP_Text label, int value, int total)
        {
            int length = Write(value, 0);
            _buffer[length++] = '/';
            length = Write(total, length);
            label.SetText(_buffer, 0, length);
        }

        /// <summary>Formats into the internal buffer and returns the new length (exposed for tests).</summary>
        public int Write(int value, int start)
        {
            if (value < 0)
            {
                _buffer[start++] = '-';
                value = -value;
            }
            int digits = 1;
            for (int v = value; v >= 10; v /= 10)
                digits++;
            int end = start + digits;
            for (int i = end - 1; i >= start; i--)
            {
                _buffer[i] = (char)('0' + value % 10);
                value /= 10;
            }
            return end;
        }

        public char[] Buffer => _buffer;
    }
}
