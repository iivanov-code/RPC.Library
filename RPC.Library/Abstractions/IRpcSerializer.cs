using System;
using System.Buffers;

namespace RPC.Library.Abstractions
{
    /// <summary>
    /// Turns values into payload bytes and back. Implementations must be thread safe
    /// </summary>
    public interface IRpcSerializer
    {
        void Serialize(IBufferWriter<byte> writer, object value, Type type);

        object Deserialize(ReadOnlyMemory<byte> data, Type type);

        /// <summary>
        /// Writes the argument list of one call
        /// </summary>
        void SerializeArguments(IBufferWriter<byte> writer, object[] arguments, Type[] types);

        /// <summary>
        /// Reads the argument list of one call; the result has one entry per type
        /// </summary>
        object[] DeserializeArguments(ReadOnlyMemory<byte> data, Type[] types);
    }
}
