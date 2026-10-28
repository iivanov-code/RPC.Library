using System;
using System.Buffers;
using System.Threading;

namespace RPC.Library.Abstractions
{
    /// <summary>
    /// One complete message received from a connection. The buffer is rented from
    /// <see cref="ArrayPool{T}.Shared"/> and goes back to the pool on dispose
    /// </summary>
    public sealed class ReceivedMessage : IDisposable
    {
        private byte[] buffer;

        public ReceivedMessage(byte[] rentedBuffer, int length)
        {
            if (rentedBuffer == null) throw new ArgumentNullException(nameof(rentedBuffer));
            if (length < 0 || length > rentedBuffer.Length) throw new ArgumentOutOfRangeException(nameof(length));

            this.buffer = rentedBuffer;
            this.Length = length;
        }

        public int Length { get; }

        public ReadOnlyMemory<byte> Data
        {
            get
            {
                byte[] current = this.buffer;
                if (current == null) throw new ObjectDisposedException(nameof(ReceivedMessage));

                return new ReadOnlyMemory<byte>(current, 0, this.Length);
            }
        }

        public void Dispose()
        {
            byte[] current = Interlocked.Exchange(ref this.buffer, null);
            if (current != null)
            {
                ArrayPool<byte>.Shared.Return(current);
            }
        }
    }
}
