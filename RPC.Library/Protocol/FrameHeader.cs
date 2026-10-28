using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace RPC.Library.Protocol
{
    /// <summary>
    /// Layout of every message: [byte type][int32 correlationId][uint16 methodLength][method utf8][payload]
    /// </summary>
    internal static class FrameHeader
    {
        public const int FIXED_SIZE = sizeof(byte) + sizeof(int) + sizeof(ushort);

        public static void Write(IBufferWriter<byte> writer, FrameType type, int correlationId, string method)
        {
            int methodLength = string.IsNullOrEmpty(method) ? 0 : Encoding.UTF8.GetByteCount(method);
            if (methodLength > ushort.MaxValue)
                throw new ArgumentException($"Method name is longer than {ushort.MaxValue} bytes", nameof(method));

            int headerSize = FIXED_SIZE + methodLength;
            Span<byte> span = writer.GetSpan(headerSize);

            span[0] = (byte)type;
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(1), correlationId);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(5), (ushort)methodLength);

            if (methodLength > 0)
            {
                Encoding.UTF8.GetBytes(method.AsSpan(), span.Slice(FIXED_SIZE));
            }

            writer.Advance(headerSize);
        }

        public static bool TryParse(ReadOnlyMemory<byte> message,
                                    out FrameType type,
                                    out int correlationId,
                                    out string method,
                                    out ReadOnlyMemory<byte> payload)
        {
            type = default;
            correlationId = 0;
            method = null;
            payload = default;

            ReadOnlySpan<byte> span = message.Span;
            if (span.Length < FIXED_SIZE) return false;

            byte rawType = span[0];
            if (rawType < (byte)FrameType.Request || rawType > (byte)FrameType.OneWay) return false;

            int methodLength = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(5));
            if (span.Length < FIXED_SIZE + methodLength) return false;

            type = (FrameType)rawType;
            correlationId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(1));
            method = methodLength == 0 ? string.Empty : Encoding.UTF8.GetString(span.Slice(FIXED_SIZE, methodLength));
            payload = message.Slice(FIXED_SIZE + methodLength);

            return true;
        }
    }
}
