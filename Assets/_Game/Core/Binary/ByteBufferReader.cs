#nullable enable
using System;
using System.Text;

namespace PocketWilds.Core
{
    public sealed class BinaryFormatException : Exception
    {
        public BinaryFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>Bounds-checked little-endian reader. Used at load time only.</summary>
    public struct ByteBufferReader
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private readonly byte[] _buffer;
        private readonly int _end;
        private int _position;

        public ByteBufferReader(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            _buffer = buffer;
            _position = offset;
            _end = offset + count;
        }

        public int Position => _position;
        public int Remaining => _end - _position;
        public bool IsAtEnd => _position >= _end;

        public byte ReadByte()
        {
            Require(1);
            return _buffer[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUInt16()
        {
            Require(2);
            int v = _buffer[_position] | (_buffer[_position + 1] << 8);
            _position += 2;
            return (ushort)v;
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public uint ReadUInt32()
        {
            Require(4);
            uint v = (uint)(_buffer[_position]
                            | (_buffer[_position + 1] << 8)
                            | (_buffer[_position + 2] << 16)
                            | (_buffer[_position + 3] << 24));
            _position += 4;
            return v;
        }

        public long ReadInt64()
        {
            ulong low = ReadUInt32();
            ulong high = ReadUInt32();
            return unchecked((long)(low | (high << 32)));
        }

        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

        /// <summary>Reads and re-normalizes, so corrupted values can never produce NaN or out-of-range exponents.</summary>
        public BigValue ReadBigValue()
        {
            double mantissa = ReadDouble();
            int exponent = ReadInt32();
            return BigValue.FromMantissaExponent(mantissa, exponent);
        }

        public ContentId ReadContentId() => new ContentId(ReadUInt32());

        public string? ReadString()
        {
            int length = ReadInt32();
            if (length == -1)
                return null;
            if (length < 0)
                throw new BinaryFormatException("Negative string length.");
            Require(length);
            string value = Utf8.GetString(_buffer, _position, length);
            _position += length;
            return value;
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            Require(count);
            var span = new ReadOnlySpan<byte>(_buffer, _position, count);
            _position += count;
            return span;
        }

        public void Skip(int count)
        {
            Require(count);
            _position += count;
        }

        private void Require(int count)
        {
            if (count < 0 || _position + count > _end)
                throw new BinaryFormatException("Unexpected end of data.");
        }
    }
}
