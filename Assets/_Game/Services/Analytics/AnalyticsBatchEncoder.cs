#nullable enable
using System;
using System.Text;

namespace PocketWilds.Services.Analytics
{
    /// <summary>What a batch header says about this install, once (never in each event).</summary>
    public sealed class AnalyticsDeviceInfo
    {
        public string AnalyticsId = string.Empty;
        public string AppVersion = string.Empty;
        public string BuildType = string.Empty;
        public string Platform = string.Empty;
        public string DeviceModel = string.Empty;
        public string OperatingSystem = string.Empty;
        public int SystemMemoryMb;
    }

    /// <summary>
    /// Encodes a batch as compact UTF-8 JSON into a reused buffer: header (escaped once), then each event as a
    /// positional array following <see cref="AnalyticsSchema"/>. Numbers are written digit by digit: encoding
    /// allocates nothing. About 40 to 70 bytes per event.
    /// </summary>
    public sealed class AnalyticsBatchEncoder
    {
        private const double MaxWhole = 9e18;
        private static readonly long[] Pow10 = { 1, 10, 100, 1000, 10000, 100000, 1000000 };

        private readonly byte[] _headerStart;
        private byte[] _buffer = new byte[16 * 1024];
        private int _length;

        public AnalyticsBatchEncoder(AnalyticsDeviceInfo device)
        {
            var header = new StringBuilder(256);
            header.Append("{\"schema\":").Append(AnalyticsSchema.Version);
            Field(header, "aid", device.AnalyticsId);
            Field(header, "app", device.AppVersion);
            Field(header, "build", device.BuildType);
            Field(header, "platform", device.Platform);
            Field(header, "device", device.DeviceModel);
            Field(header, "os", device.OperatingSystem);
            header.Append(",\"ram\":").Append(device.SystemMemoryMb);
            _headerStart = Encoding.UTF8.GetBytes(header.ToString());
        }

        public byte[] Buffer => _buffer;
        public int Length => _length;

        /// <summary>Encodes count events read from a ring buffer starting at index start.</summary>
        public void Encode(int batchIndex, int dropped, AnalyticsEvent[] ring, int start, int count)
        {
            _length = 0;
            Raw(_headerStart);
            Ascii(",\"batch\":");
            Long(batchIndex);
            Ascii(",\"dropped\":");
            Long(dropped);
            Ascii(",\"events\":[");
            for (int n = 0; n < count; n++)
            {
                ref readonly var e = ref ring[(start + n) % ring.Length];
                if (n > 0)
                    Byte((byte)',');
                Byte((byte)'[');
                Long((int)e.Id);
                Byte((byte)',');
                Long(e.UnixMs);
                Byte((byte)',');
                Long(e.Session);
                var layout = AnalyticsSchema.Layout(e.Id);
                for (int f = 0; f < layout.Length; f++)
                {
                    Byte((byte)',');
                    var field = layout[f];
                    switch (field.Kind)
                    {
                        case 'I':
                            Long(field.Slot switch { 0 => e.I0, 1 => e.I1, 2 => e.I2, _ => e.I3 });
                            break;
                        case 'U':
                            Long(field.Slot switch { 0 => e.U0, 1 => e.U1, _ => e.U2 });
                            break;
                        default:
                            Fixed(field.Slot switch { 0 => e.D0, 1 => e.D1, _ => e.D2 }, field.Decimals);
                            break;
                    }
                }
                Byte((byte)']');
            }
            Ascii("]}");
        }

        private static void Field(StringBuilder header, string name, string value)
        {
            header.Append(",\"").Append(name).Append("\":\"");
            foreach (char c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\')
                    header.Append('\\').Append(c);
                else if (c < ' ')
                    header.Append(' ');
                else
                    header.Append(c);
            }
            header.Append('"');
        }

        private void Ensure(int extra)
        {
            if (_length + extra <= _buffer.Length)
                return;
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + extra));
        }

        private void Byte(byte b)
        {
            Ensure(1);
            _buffer[_length++] = b;
        }

        private void Raw(byte[] bytes)
        {
            Ensure(bytes.Length);
            System.Buffer.BlockCopy(bytes, 0, _buffer, _length, bytes.Length);
            _length += bytes.Length;
        }

        private void Ascii(string text)
        {
            Ensure(text.Length);
            for (int i = 0; i < text.Length; i++)
                _buffer[_length++] = (byte)text[i];
        }

        private void Long(long value)
        {
            Ensure(21);
            if (value < 0)
            {
                _buffer[_length++] = (byte)'-';
                if (value == long.MinValue)
                    value = long.MaxValue;
                else
                    value = -value;
            }
            int digits = 1;
            for (long v = value; v >= 10; v /= 10)
                digits++;
            int end = _length + digits;
            for (int i = end - 1; i >= _length; i--)
            {
                _buffer[i] = (byte)('0' + value % 10);
                value /= 10;
            }
            _length = end;
        }

        /// <summary>Fixed decimals, trailing zeros trimmed ("16.7", "3", "0.125"). Non-finite values write 0.</summary>
        private void Fixed(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                Byte((byte)'0');
                return;
            }
            decimals = Math.Min(decimals, Pow10.Length - 1);
            long scale = Pow10[decimals];
            double scaled = Math.Round(Math.Abs(value) * scale, MidpointRounding.AwayFromZero);
            if (scaled > 9e15)
            {
                // Too big for fixed decimals: whole number only, clamped to what a long holds (never reached by
                // mantissas, seconds or Gems; protects the payload from garbage).
                Long((long)Math.Max(-MaxWhole, Math.Min(MaxWhole, Math.Round(value))));
                return;
            }
            long units = (long)scaled;
            if (value < 0 && units > 0)
                Byte((byte)'-');
            Long(units / scale);
            long fraction = units % scale;
            if (fraction == 0)
                return;
            int width = decimals;
            while (fraction % 10 == 0)
            {
                fraction /= 10;
                width--;
            }
            Ensure(width + 1);
            _buffer[_length++] = (byte)'.';
            for (int i = width - 1; i >= 0; i--)
            {
                _buffer[_length + i] = (byte)('0' + fraction % 10);
                fraction /= 10;
            }
            _length += width;
        }
    }
}
