using System;
using System.Buffers;
using System.Collections.Concurrent;
using NetworkCommunicator.Args;

namespace NetworkCommunicator.Utils
{
    internal class BufferPool<T> : MemoryPool<T>
    {
        private ConcurrentDictionary<int, ConcurrentQueue<Memory<T>>> freeBuffers;
        private ConcurrentDictionary<Guid, Memory<T>> requestedBuffers;

        public BufferPool(int maxBufferSize)
        {
            this.maxBufferSize = maxBufferSize;
            freeBuffers = new ConcurrentDictionary<int, ConcurrentQueue<Memory<T>>>();
            requestedBuffers = new ConcurrentDictionary<Guid, Memory<T>>();
        }

        private int maxBufferSize;

        public override int MaxBufferSize
        {
            get
            {
                return maxBufferSize;
            }
        }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            if (minBufferSize <= 0)
            {
                minBufferSize = MaxBufferSize;
            }

            if (freeBuffers.ContainsKey(minBufferSize))
            {
                var buffersType = freeBuffers[minBufferSize];
                Memory<T> buffer = null;

                if (buffersType.TryDequeue(out buffer))
                {
                    var id = Guid.NewGuid();
                    var owner = new MemoryOwner<T>(id, buffer);
                    _ = requestedBuffers.TryAdd(id, buffer);
                    owner.OnMemoryFreed += Owner_OnMemoryFreed;

                    return owner;
                }
                else
                {
                    buffer = new T[minBufferSize];
                    buffersType.Enqueue(buffer);
                    return Rent(minBufferSize);
                }
            }
            else
            {
                _ = freeBuffers.TryAdd(minBufferSize, new ConcurrentQueue<Memory<T>>());
                return Rent(minBufferSize);
            }
        }

        private void Owner_OnMemoryFreed(object sender, MemoryEventArgs e)
        {
            if (requestedBuffers.TryRemove(e.ID, out Memory<T> memory))
            {
                freeBuffers[e.Size].Enqueue(memory);
            }
        }

        protected override void Dispose(bool disposing)
        {
            freeBuffers.Clear();
            requestedBuffers.Clear();
            freeBuffers = null;
            requestedBuffers = null;
        }
    }

    internal class MemoryOwner<T> : IMemoryOwner<T>
    {
        public MemoryOwner(Guid id, Memory<T> buffer)
        {
            this.id = id;
            Memory = buffer;
        }

        private Guid id;
        public Memory<T> Memory { get; private set; }

        private event EventHandler<MemoryEventArgs> onMemoryFreed;

        public event EventHandler<MemoryEventArgs> OnMemoryFreed
        {
            add
            {
                onMemoryFreed += value;
            }
            remove
            {
                onMemoryFreed -= value;
            }
        }

        public void Dispose()
        {
            onMemoryFreed?.Invoke(this, new MemoryEventArgs(id, Memory.Length));
            Memory = null;
        }
    }
}