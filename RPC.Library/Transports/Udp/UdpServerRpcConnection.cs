using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Udp
{
    /// <summary>
    /// Server side view of one remote sender. The listener feeds it the datagrams that
    /// came from that sender; replies go out through the shared listening socket
    /// </summary>
    public sealed class UdpServerRpcConnection : IRpcConnection
    {
        private readonly UdpRpcListener listener;
        private readonly Socket socket;
        private readonly ConcurrentQueue<ReceivedMessage> inbox;
        private readonly SemaphoreSlim available;
        private int disposed;

        internal UdpServerRpcConnection(UdpRpcListener listener, Socket socket, EndPoint remoteEndPoint, int maxMessageSize)
        {
            this.listener = listener;
            this.socket = socket;
            this.RemoteEndPoint = remoteEndPoint;
            this.LocalEndPoint = socket.LocalEndPoint;
            this.MaxMessageSize = maxMessageSize;
            this.inbox = new ConcurrentQueue<ReceivedMessage>();
            this.available = new SemaphoreSlim(0);
        }

        public EndPoint LocalEndPoint { get; }

        public EndPoint RemoteEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.None;

        public int MaxMessageSize { get; }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
        {
            if (message.Length > this.MaxMessageSize) throw new RpcMessageTooLargeException(message.Length, this.MaxMessageSize);
            if (this.disposed != 0) throw new RpcConnectionException("Connection is closed");

            if (!MemoryMarshal.TryGetArray(message, out ArraySegment<byte> segment))
            {
                segment = new ArraySegment<byte>(message.ToArray());
            }

            try
            {
                await this.socket.SendToAsync(segment, SocketFlags.None, this.RemoteEndPoint).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                throw new RpcConnectionException("Could not send the datagram", ex);
            }
            catch (ObjectDisposedException ex)
            {
                throw new RpcConnectionException("Connection is closed", ex);
            }
        }

        public async ValueTask<ReceivedMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (this.inbox.TryDequeue(out ReceivedMessage message)) return message;
                if (this.disposed != 0) return null;

                try
                {
                    await this.available.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            }
        }

        internal void Deliver(ReceivedMessage message)
        {
            if (this.disposed != 0)
            {
                message.Dispose();
                return;
            }

            this.inbox.Enqueue(message);
            this.available.Release();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.listener.Forget(this);

            while (this.inbox.TryDequeue(out ReceivedMessage message))
            {
                message.Dispose();
            }

            // wake a pending receive so it observes the close
            this.available.Release();
        }
    }
}
