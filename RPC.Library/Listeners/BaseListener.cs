using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Args;
using NetworkCommunicator.Models;
using NetworkCommunicator.Utils;

namespace RPC.Library.Listeners
{
    internal abstract class BaseListener : IListener
    {
        protected const int GUID_SIZE = 16;

        protected readonly BufferPool<byte> bufferPool;
        protected readonly int bufferSize;
        protected readonly CancellationTokenSource cancellationTokenSource;
        protected Socket socket;

        protected BaseListener(ref Socket socket, int bufferSize, CancellationToken? token = null)
        {
            this.bufferSize = bufferSize;
            this.socket = socket;

            if (token.HasValue)
            {
                this.Token = token.Value;
            }

            this.ResponseMessagesDictionary = new ConcurrentDictionary<Guid, BaseWaitContext>();
            this.cancellationTokenSource = new CancellationTokenSource();
            this.bufferPool = new BufferPool<byte>(this.bufferSize);
        }

        public event EventHandler<MessageEventArgs> MessageReceived
        {
            add
            {
                messageReceived += value;
            }
            remove
            {
                messageReceived -= value;
            }
        }

        private event EventHandler<MessageEventArgs> messageReceived;

        public ConcurrentDictionary<Guid, BaseWaitContext> ResponseMessagesDictionary { get; private set; }

        private CancellationToken? token;
        private bool disposedValue;

        public CancellationToken Token
        {
            get
            {
                if (!token.HasValue)
                {
                    token = cancellationTokenSource.Token;
                }
                return token.Value;
            }
            private set
            {
                token = value;
            }
        }

        public abstract Task Listen();
        public abstract Task<bool> Connect(IPAddress remoteHostIp, ushort remotePort);
        public abstract Task<int> Send(byte[] messageBuffer, BaseWaitContext context);
        public abstract Task<bool> AcceptConnection(ushort port);

        protected void EndReceive(BaseWaitContext context)
        {
            if (context.FireEvent)
            {
                messageReceived?.Invoke(this, new MessageEventArgs(context.Guid, context.ResponseMessage.ToArray(), context.ShouldWait, context.WaitHandle));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.ResponseMessagesDictionary = null;
                    this.cancellationTokenSource.Dispose();
                    this.bufferPool.Dispose();
                }

                disposedValue = true;
            }
        }

        protected IList<ArraySegment<byte>> BuildMessage(byte[] messageBuffer, BaseWaitContext context)
        {
            byte[] keyBuffer = context.Guid.ToByteArray();

            int packetSize = messageBuffer.Length + keyBuffer.Length + sizeof(int);

            List<ArraySegment<byte>> segments = new List<ArraySegment<byte>>
            {
                BitConverter.GetBytes(packetSize),
                keyBuffer,
                messageBuffer
            };

            if (context.ShouldWait)
            {
                _ = ResponseMessagesDictionary.TryAdd(context.Guid, context);
            }

            return segments;
        }

        protected bool Bind(ushort port)
        {
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
