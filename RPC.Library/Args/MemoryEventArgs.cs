using System;

namespace NetworkCommunicator.Args
{
    internal class MemoryEventArgs : EventArgs
    {
        public MemoryEventArgs(Guid id, int size)
        {
            ID = id;
            Size = size;
        }

        public readonly Guid ID;
        public readonly int Size;
    }
}