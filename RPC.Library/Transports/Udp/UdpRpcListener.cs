using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Udp
{
    /// <summary>
    /// Receives every datagram on one socket and sorts it by sender: the first datagram
    /// from a new sender creates a connection that <see cref="AcceptAsync"/> hands out
    /// </summary>
    public sealed class UdpRpcListener : IRpcListener
    {
        private readonly Socket socket;
        private readonly int maxMessageSize;
        private readonly ConcurrentDictionary<EndPoint, UdpServerRpcConnection> connections;
        private readonly ConcurrentQueue<UdpServerRpcConnection> pendingAccepts;
        private readonly SemaphoreSlim acceptAvailable;
        private readonly Task receiveLoop;
        private int disposed;

        internal UdpRpcListener(Socket socket, int maxMessageSize)
        {
            this.socket = socket;
            this.maxMessageSize = maxMessageSize;
            this.LocalEndPoint = socket.LocalEndPoint;
            this.connections = new ConcurrentDictionary<EndPoint, UdpServerRpcConnection>();
            this.pendingAccepts = new ConcurrentQueue<UdpServerRpcConnection>();
            this.acceptAvailable = new SemaphoreSlim(0);
            this.receiveLoop = Task.Run(this.ReceiveLoopAsync);
        }

        public EndPoint LocalEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.None;

        /// <summary>
        /// Raised when a datagram could not be received; the loop keeps running
        /// </summary>
        public event EventHandler<Exception> ReceiveFailed;

        public async Task<IRpcConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (this.disposed != 0) throw new OperationCanceledException("Listener closed", cancellationToken);

                try
                {
                    await this.acceptAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    throw new OperationCanceledException("Listener closed", cancellationToken);
                }

                if (this.pendingAccepts.TryDequeue(out UdpServerRpcConnection connection)) return connection;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.socket.Dispose();

            foreach (UdpServerRpcConnection connection in this.connections.Values)
            {
                connection.Dispose();
            }

            this.acceptAvailable.Release();
        }

        internal void Forget(UdpServerRpcConnection connection)
        {
            this.connections.TryRemove(connection.RemoteEndPoint, out _);
        }

        private async Task ReceiveLoopAsync()
        {
            EndPoint anyEndPoint = this.socket.AddressFamily == AddressFamily.InterNetworkV6
                ? new IPEndPoint(IPAddress.IPv6Any, 0)
                : new IPEndPoint(IPAddress.Any, 0);

            while (this.disposed == 0)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(this.maxMessageSize);
                SocketReceiveFromResult result;

                try
                {
                    result = await this.socket.ReceiveFromAsync(new ArraySegment<byte>(buffer, 0, this.maxMessageSize), SocketFlags.None, anyEndPoint).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    return;
                }
                catch (SocketException ex)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    if (this.disposed != 0) return;

                    // e.g. ICMP port unreachable surfaced as ConnectionReset on Windows
                    this.ReceiveFailed?.Invoke(this, ex);
                    continue;
                }

                UdpServerRpcConnection connection = this.connections.GetOrAdd(result.RemoteEndPoint, this.CreateConnection);
                connection.Deliver(new ReceivedMessage(buffer, result.ReceivedBytes));
            }
        }

        private UdpServerRpcConnection CreateConnection(EndPoint remoteEndPoint)
        {
            UdpServerRpcConnection connection = new UdpServerRpcConnection(this, this.socket, remoteEndPoint, this.maxMessageSize);
            this.pendingAccepts.Enqueue(connection);
            this.acceptAvailable.Release();
            return connection;
        }
    }
}
