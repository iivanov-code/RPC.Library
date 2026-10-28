using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Tcp
{
    public sealed class TcpRpcListener : IRpcListener
    {
        private readonly Socket socket;
        private readonly TcpTransportOptions options;
        private int disposed;

        internal TcpRpcListener(Socket socket, TcpTransportOptions options)
        {
            this.socket = socket;
            this.options = options;
            this.LocalEndPoint = socket.LocalEndPoint;
        }

        public EndPoint LocalEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.Stream;

        public async Task<IRpcConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            Socket accepted;
            try
            {
#if NET5_0_OR_GREATER
                accepted = await this.socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
#else
                accepted = await this.socket.AcceptAsync().ConfigureAwait(false);
#endif
            }
            catch (ObjectDisposedException)
            {
                throw new OperationCanceledException("Listener closed", cancellationToken);
            }
            catch (SocketException ex) when (this.disposed != 0)
            {
                throw new OperationCanceledException("Listener closed", ex, cancellationToken);
            }
            catch (SocketException ex)
            {
                throw new RpcConnectionException("Accept failed", ex);
            }

            try
            {
                accepted.NoDelay = this.options.NoDelay;
                Stream stream = new NetworkStream(accepted, ownsSocket: false);
                if (this.options.ServerStreamWrapper != null)
                {
                    stream = await this.options.ServerStreamWrapper(stream, cancellationToken).ConfigureAwait(false);
                }

                return new TcpRpcConnection(accepted, stream, this.options.MaxMessageSize);
            }
            catch
            {
                accepted.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.socket.Dispose();
        }
    }
}
