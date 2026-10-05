using PocketWilds.Core;
using TMPro;

namespace PocketWilds.UI
{
    /// <summary>
    /// Formats a BigValue with an optional fixed prefix/suffix ("+", "/s") into reusable char buffers and
    /// tells whether the visible text changed. Pure and allocation free; no UI dependency.
    /// </summary>
    public sealed class BigValueTextBuffer
    {
        private readonly char[] _prefix;
        private readonly char[] _suffix;
        private readonly char[] _buffer;
        private readonly char[] _shown;
        private int _shownLength = -1;

        public BigValueTextBuffer(string prefix = null, string suffix = null)
        {
            _prefix = string.IsNullOrEmpty(prefix) ? System.Array.Empty<char>() : prefix.ToCharArray();
            _suffix = string.IsNullOrEmpty(suffix) ? System.Array.Empty<char>() : suffix.ToCharArray();
            int capacity = _prefix.Length + BigValueFormatter.MaxLength + _suffix.Length;
            _buffer = new char[capacity];
            _shown = new char[capacity];
        }

        public NumberFormatSettings Format { get; set; } = NumberFormatSettings.Invariant;
        public char[] Characters => _shown;
        public int Length => _shownLength < 0 ? 0 : _shownLength;

        /// <summary>Returns true when the formatted text differs from the last one.</summary>
        public bool Update(BigValue value)
        {
            System.Array.Copy(_prefix, _buffer, _prefix.Length);
            int length = _prefix.Length + BigValueFormatter.Format(value, _buffer, _prefix.Length, Format);
            System.Array.Copy(_suffix, 0, _buffer, length, _suffix.Length);
            length += _suffix.Length;
            if (length == _shownLength && Same(length))
                return false;
            System.Array.Copy(_buffer, _shown, length);
            _shownLength = length;
            return true;
        }

        private bool Same(int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (_buffer[i] != _shown[i])
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Writes a BigValue into a TMP label, touching the label only when the formatted text changes so a
    /// counter never rebuilds its canvas for nothing. Zero allocation in players; in the Editor only,
    /// TMP mirrors the text into a string for its inspector (TMP_Text.SetCharArray).
    /// </summary>
    public sealed class BigValueText
    {
        private readonly TMP_Text _label;
        private readonly BigValueTextBuffer _text;

        public BigValueText(TMP_Text label, string prefix = null, string suffix = null)
        {
            _label = label;
            _text = new BigValueTextBuffer(prefix, suffix);
        }

        public NumberFormatSettings Format
        {
            get => _text.Format;
            set => _text.Format = value;
        }

        /// <summary>Returns true when the label changed.</summary>
        public bool Set(BigValue value)
        {
            if (!_text.Update(value))
                return false;
            _label.SetText(_text.Characters, 0, _text.Length);
            return true;
        }
    }
}
