using System;
using System.Buffers;

namespace RPC.Library.Protocol
{
    /// <summary>
    /// Growable <see cref="IBufferWriter{T}"/> backed by <see cref="ArrayPool{T}.Shared"/>
    /// </summary>
    internal sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] buffer;
        private int written;

        public PooledBufferWriter(int initialCapacity = 256)
        {
            if (initialCapacity < 1) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            this.buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        }

        public int WrittenCount => this.written;

        public Memory<byte> WrittenMemory => this.buffer.AsMemory(0, this.written);

        public Span<byte> WrittenSpan => this.buffer.AsSpan(0, this.written);

        public void Advance(int count)
        {
            if (count < 0 || this.written + count > this.buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            this.written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            this.EnsureCapacity(sizeHint);
            return this.buffer.AsMemory(this.written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            this.EnsureCapacity(sizeHint);
            return this.buffer.AsSpan(this.written);
        }

        public void Reset()
        {
            this.written = 0;
        }

        public void Dispose()
        {
            byte[] current = this.buffer;
            this.buffer = null;
            if (current != null)
            {
                ArrayPool<byte>.Shared.Return(current);
            }
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (this.buffer == null) throw new ObjectDisposedException(nameof(PooledBufferWriter));
            if (sizeHint < 1) sizeHint = 1;

            int free = this.buffer.Length - this.written;
            if (free >= sizeHint) return;

            int newSize = Math.Max(this.buffer.Length * 2, this.written + sizeHint);
            byte[] larger = ArrayPool<byte>.Shared.Rent(newSize);
            this.buffer.AsSpan(0, this.written).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(this.buffer);
            this.buffer = larger;
        }
    }
}
