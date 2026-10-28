#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System;
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Quic
{
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public sealed class QuicRpcListener : IRpcListener
    {
        private readonly QuicListener listener;
        private readonly QuicTransportOptions options;
        private int disposed;

        internal QuicRpcListener(QuicListener listener, QuicTransportOptions options)
        {
            this.listener = listener;
            this.options = options;
            this.LocalEndPoint = listener.LocalEndPoint;
        }

        public EndPoint LocalEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.Stream;

        public async Task<IRpcConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            QuicConnection connection;
            try
            {
                connection = await this.listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                throw new OperationCanceledException("Listener closed", cancellationToken);
            }
            catch (QuicException ex) when (this.disposed != 0)
            {
                throw new OperationCanceledException("Listener closed", ex, cancellationToken);
            }
            catch (QuicException ex)
            {
                throw new RpcConnectionException("Accept failed", ex);
            }

            try
            {
                QuicStream stream = await connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
                await QuicTransport.ReadPreambleAsync(stream, cancellationToken).ConfigureAwait(false);

                return new QuicRpcConnection(connection, stream, this.options.MaxMessageSize);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.listener.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
#endif
