#nullable enable
using System;
using System.Collections.Generic;

namespace PocketWilds.Simulation
{
    /// <summary>
    /// T48B the tiny strict JSON reader of the season and weekend Remote Config values (no JSON library in the project). Reads
    /// objects, lists, strings and whole numbers only; anything else (fractions, booleans, null, trailing text, duplicate keys)
    /// fails, and a failed read changes nothing. Boot and Remote Config time only, never per frame.
    /// </summary>
    internal sealed class SeasonJson
    {
        private readonly string _text;
        private int _pos;

        public SeasonJson(string text)
        {
            _text = text;

        }

        public bool AtEnd
        {
            get
            {
                SkipSpace();
                return _pos >= _text.Length;
            }
        }

        /// <summary>The next non-space character (without consuming it), or '\0' at the end.</summary>
        public char Peek()
        {
            SkipSpace();
            return _pos < _text.Length ? _text[_pos] : '\0';
        }

        public bool TryConsume(char c)
        {
            if (Peek() != c)
                return false;
            _pos++;
            return true;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;
            if (Peek() != '"')
                return false;
            _pos++;
            var chars = new System.Text.StringBuilder();
            while (_pos < _text.Length)
            {
                char c = _text[_pos++];
                if (c == '"')
                {
                    value = chars.ToString();
                    return true;
                }
                if (c < ' ')
                    return false;
                if (c != '\\')
                {
                    chars.Append(c);
                    continue;
                }
                if (_pos >= _text.Length)
                    return false;
                char e = _text[_pos++];
                switch (e)
                {
                    case '"': chars.Append('"'); break;
                    case '\\': chars.Append('\\'); break;
                    case '/': chars.Append('/'); break;
                    case 'n': chars.Append('\n'); break;
                    case 't': chars.Append('\t'); break;
                    case 'u':
                        if (_pos + 4 > _text.Length || !int.TryParse(_text.Substring(_pos, 4), System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out int code))
                            return false;
                        chars.Append((char)code);
                        _pos += 4;
                        break;
                    default: return false;
                }
            }
            return false;
        }

        /// <summary>A whole number without fraction or exponent, inside the int range.</summary>
        public bool TryReadInt(out int value)
        {
            value = 0;
            SkipSpace();
            int start = _pos;
            if (_pos < _text.Length && _text[_pos] == '-')
                _pos++;
            int digits = 0;
            long acc = 0;
            while (_pos < _text.Length && _text[_pos] >= '0' && _text[_pos] <= '9')
            {
                acc = acc * 10 + (_text[_pos] - '0');
                if (acc > int.MaxValue)
                    return false;
                _pos++;
                digits++;
            }
            if (digits == 0)
            {
                _pos = start;
                return false;
            }
            if (_pos < _text.Length && (_text[_pos] == '.' || _text[_pos] == 'e' || _text[_pos] == 'E'))
                return false;
            value = _text[start] == '-' ? -(int)acc : (int)acc;
            return true;
        }

        /// <summary>An object whose every value is read by <paramref name="readValue"/> (false stops); duplicate keys fail.</summary>
        public bool TryReadObject(Func<string, bool> readValue)
        {
            if (!TryConsume('{'))
                return false;
            if (TryConsume('}'))
                return true;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (!TryReadString(out string key) || !keys.Add(key) || !TryConsume(':') || !readValue(key))
                    return false;
                if (TryConsume(','))
                    continue;
                return TryConsume('}');
            }
        }

        /// <summary>A list whose every item is read by <paramref name="readItem"/> (false stops).</summary>
        public bool TryReadList(Func<bool> readItem)
        {
            if (!TryConsume('['))
                return false;
            if (TryConsume(']'))
                return true;
            while (true)
            {
                if (!readItem())
                    return false;
                if (TryConsume(','))
                    continue;
                return TryConsume(']');
            }
        }

        private void SkipSpace()
        {
            while (_pos < _text.Length && (_text[_pos] == ' ' || _text[_pos] == '\n' || _text[_pos] == '\r' || _text[_pos] == '\t'))
                _pos++;
        }
    }
}
