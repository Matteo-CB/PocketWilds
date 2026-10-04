using PocketWilds.Core;
using TMPro;
using UnityEngine;

namespace PocketWilds.UI
{
    /// <summary>
    /// Writes a BigValue into a TMP label without allocating. Only touches the label when the
    /// formatted text actually changes, so a counter never rebuilds its canvas for nothing.
    /// </summary>
    public sealed class BigValueText
    {
        private readonly TMP_Text _label;
        private readonly char[] _buffer = new char[BigValueFormatter.MaxLength];
        private readonly char[] _shown = new char[BigValueFormatter.MaxLength];
        private int _shownLength = -1;

        public BigValueText(TMP_Text label)
        {
            _label = label;
        }

        public NumberFormatSettings Format { get; set; } = NumberFormatSettings.Invariant;

        /// <summary>Returns true when the label changed.</summary>
        public bool Set(BigValue value)
        {
            int length = BigValueFormatter.Format(value, _buffer, 0, Format);
            if (length == _shownLength && Same(length))
                return false;

            System.Array.Copy(_buffer, _shown, length);
            _shownLength = length;
            _label.SetText(_buffer, 0, length);
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
}
