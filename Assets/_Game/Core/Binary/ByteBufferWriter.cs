#nullable enable
using System;
using System.Text;

namespace PocketWilds.Core
{
    /// <summary>
    /// Growable little-endian binary writer over a reusable byte array.
    /// Call <see cref="Reset"/> to reuse the same memory; the buffer only grows.
    /// </summary>
    public sealed class ByteBufferWriter
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private byte[] _buffer;
        private int _length;

        public ByteBufferWriter(int initialCapacity = 4096)
        {
            _buffer = new byte[Math.Max(16, initialCapacity)];
        }

        public int Length => _length;
        public byte[] Buffer => _buffer;
        public ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _length);

        public void Reset() => _length = 0;

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
        }

        public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

        public void WriteUInt32(uint value)
        {
            Ensure(4);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
            _buffer[_length++] = (byte)(value >> 16);
            _buffer[_length++] = (byte)(value >> 24);
        }

        public void WriteInt64(long value)
        {
            ulong v = unchecked((ulong)value);
            WriteUInt32((uint)v);
            WriteUInt32((uint)(v >> 32));
        }

        public void WriteDouble(double value) => WriteInt64(BitConverter.DoubleToInt64Bits(value));

        public void WriteBigValue(BigValue value)
        {
            WriteDouble(value.Mantissa);
            WriteInt32(value.Exponent10);
        }

        public void WriteContentId(ContentId id) => WriteUInt32(id.Value);

        /// <summary>UTF-8 with int32 byte-length prefix. Null is written as -1.</summary>
        public void WriteString(string? value)
        {
            if (value == null)
            {
                WriteInt32(-1);
                return;
            }

            int byteCount = Utf8.GetByteCount(value);
            WriteInt32(byteCount);
            Ensure(byteCount);
            Utf8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += byteCount;
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            Ensure(bytes.Length);
            bytes.CopyTo(new Span<byte>(_buffer, _length, bytes.Length));
            _length += bytes.Length;
        }

        /// <summary>Reserves 4 bytes and returns their position, for lengths patched later.</summary>
        public int ReserveInt32()
        {
            int position = _length;
            WriteInt32(0);
            return position;
        }

        public void PatchInt32(int position, int value)
        {
            if (position < 0 || position + 4 > _length)
                throw new ArgumentOutOfRangeException(nameof(position));
            uint v = unchecked((uint)value);
            _buffer[position] = (byte)v;
            _buffer[position + 1] = (byte)(v >> 8);
            _buffer[position + 2] = (byte)(v >> 16);
            _buffer[position + 3] = (byte)(v >> 24);
        }

        public void PatchUInt32(int position, uint value) => PatchInt32(position, unchecked((int)value));

        private void Ensure(int extra)
        {
            int required = _length + extra;
            if (required <= _buffer.Length)
                return;

            int newSize = _buffer.Length * 2;
            while (newSize < required)
                newSize *= 2;
            Array.Resize(ref _buffer, newSize);
        }
    }
}
