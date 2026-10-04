#nullable enable
using System;
using PocketWilds.Core;

namespace PocketWilds.Services.Save
{
    public readonly struct SaveHeader
    {
        public readonly ushort FormatVersion;
        public readonly int SchemaVersion;
        public readonly long Revision;
        public readonly int PayloadLength;
        public readonly uint PayloadCrc;

        public SaveHeader(ushort formatVersion, int schemaVersion, long revision, int payloadLength, uint payloadCrc)
        {
            FormatVersion = formatVersion;
            SchemaVersion = schemaVersion;
            Revision = revision;
            PayloadLength = payloadLength;
            PayloadCrc = payloadCrc;
        }
    }

    /// <summary>
    /// File layout (little endian):
    /// [magic u32 "PWS1"][format u16][reserved u16][schema i32][revision i64][payloadLength i32][payloadCrc32 u32]
    /// then payload = sequence of sections [id u16][version u16][length i32][bytes].
    /// The container format is independent from the game schema version.
    /// </summary>
    public static class SaveFileFormat
    {
        public const uint Magic = 0x31535750; // "PWS1"
        public const ushort FormatVersion = 1;
        public const int HeaderSize = 28;
        public const int MaxPayloadBytes = 256 * 1024 * 1024;

        public const int PayloadLengthOffset = 20;
        public const int PayloadCrcOffset = 24;

        public static void WriteHeaderPlaceholder(ByteBufferWriter writer, int schemaVersion, long revision)
        {
            writer.WriteUInt32(Magic);
            writer.WriteUInt16(FormatVersion);
            writer.WriteUInt16(0);
            writer.WriteInt32(schemaVersion);
            writer.WriteInt64(revision);
            writer.WriteInt32(0); // payload length, patched by FinishPayload
            writer.WriteUInt32(0); // payload crc, patched by FinishPayload
        }

        public static void FinishPayload(ByteBufferWriter writer)
        {
            int payloadLength = writer.Length - HeaderSize;
            uint crc = Crc32.Compute(new ReadOnlySpan<byte>(writer.Buffer, HeaderSize, payloadLength));
            writer.PatchInt32(PayloadLengthOffset, payloadLength);
            writer.PatchUInt32(PayloadCrcOffset, crc);
        }

        /// <summary>Checks magic, sizes and payload checksum. Never throws on bad data.</summary>
        public static bool TryValidate(byte[] data, int length, out SaveHeader header, out string error)
        {
            header = default;
            if (data == null || length < HeaderSize || length > data.Length)
            {
                error = "File too short.";
                return false;
            }

            try
            {
                var reader = new ByteBufferReader(data, 0, HeaderSize);
                uint magic = reader.ReadUInt32();
                ushort format = reader.ReadUInt16();
                reader.ReadUInt16();
                int schema = reader.ReadInt32();
                long revision = reader.ReadInt64();
                int payloadLength = reader.ReadInt32();
                uint crc = reader.ReadUInt32();

                if (magic != Magic)
                {
                    error = "Not a save file.";
                    return false;
                }
                if (format > FormatVersion)
                {
                    error = "Save container format " + format + " is newer than this build.";
                    return false;
                }
                if (payloadLength < 0 || payloadLength > MaxPayloadBytes || HeaderSize + payloadLength != length)
                {
                    error = "Payload length mismatch.";
                    return false;
                }

                uint actual = Crc32.Compute(new ReadOnlySpan<byte>(data, HeaderSize, payloadLength));
                if (actual != crc)
                {
                    error = "Checksum mismatch.";
                    return false;
                }

                header = new SaveHeader(format, schema, revision, payloadLength, crc);
                error = string.Empty;
                return true;
            }
            catch (BinaryFormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
