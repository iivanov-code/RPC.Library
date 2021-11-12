using System;
using System.Threading;

namespace NetworkCommunicator.Args
{
    internal class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(Guid guid, byte[] messageBuffer, bool isResponse, ManualResetEvent waitHandle)
        {
            this.Guid = guid;
            MessageBuffer = messageBuffer;
            this.IsResponse = isResponse;
            this.WaitHandle = waitHandle;
        }

        public readonly Guid Guid;
        public readonly byte[] MessageBuffer;
        public readonly bool IsResponse;
        public readonly ManualResetEvent WaitHandle;
    }
}
