using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Udp
{
    /// <summary>
    /// Client side of the UDP transport: a socket bound to one remote end point.
    /// One-way only: messages may be lost, duplicated or reordered, and nothing can be answered
    /// </summary>
    public sealed class UdpClientRpcConnection : IRpcConnection
    {
        private readonly Socket socket;
        private readonly int maxMessageSize;
        private int disposed;

        internal UdpClientRpcConnection(Socket socket, int maxMessageSize)
        {
            this.socket = socket;
            this.maxMessageSize = maxMessageSize;
            this.LocalEndPoint = socket.LocalEndPoint;
            this.RemoteEndPoint = socket.RemoteEndPoint;
        }

        public EndPoint LocalEndPoint { get; }

        public EndPoint RemoteEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.None;

        public int MaxMessageSize => this.maxMessageSize;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
        {
            if (message.Length > this.maxMessageSize) throw new RpcMessageTooLargeException(message.Length, this.maxMessageSize);
            if (this.disposed != 0) throw new RpcConnectionException("Connection is closed");

            try
            {
                await this.socket.SendAsync(message, SocketFlags.None, cancellationToken).ConfigureAwait(false);
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
            if (this.disposed != 0) return null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(this.maxMessageSize);
            try
            {
                int read = await this.socket.ReceiveAsync(buffer.AsMemory(0, this.maxMessageSize), SocketFlags.None, cancellationToken).ConfigureAwait(false);
                ReceivedMessage message = new ReceivedMessage(buffer, read);
                buffer = null;
                return message;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            catch (SocketException ex)
            {
                if (this.disposed != 0) return null;
                throw new RpcConnectionException("Could not receive a datagram", ex);
            }
            finally
            {
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.socket.Dispose();
        }
    }
}
