using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Tcp
{
    /// <summary>
    /// Reliable, ordered, request/response transport over a TCP socket
    /// </summary>
    public sealed class TcpTransport : IRpcTransport
    {
        private readonly TcpTransportOptions options;

        public TcpTransport(TcpTransportOptions options = null)
        {
            this.options = options ?? new TcpTransportOptions();
        }

        public TransportFeatures Features => TransportFeatures.Stream;

        public async Task<IRpcConnection> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            Socket socket = new Socket(AddressFamilyOf(endPoint), SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = this.options.NoDelay
            };

            try
            {
                using (cancellationToken.Register(state => ((Socket)state).Dispose(), socket))
                {
                    try
                    {
                        await socket.ConnectAsync(endPoint).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                    catch (SocketException ex)
                    {
                        throw new RpcConnectionException($"Could not connect to {endPoint}", ex);
                    }
                }

                Stream stream = new NetworkStream(socket, ownsSocket: false);
                if (this.options.ClientStreamWrapper != null)
                {
                    stream = await this.options.ClientStreamWrapper(stream, cancellationToken).ConfigureAwait(false);
                }

                return new TcpRpcConnection(socket, stream, this.options.MaxMessageSize);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        public Task<IRpcListener> ListenAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            Socket socket = new Socket(AddressFamilyOf(endPoint), SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Bind(endPoint);
                socket.Listen(this.options.Backlog);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                throw new RpcConnectionException($"Could not listen on {endPoint}", ex);
            }

            return Task.FromResult<IRpcListener>(new TcpRpcListener(socket, this.options));
        }

        private static AddressFamily AddressFamilyOf(EndPoint endPoint)
        {
            return endPoint is IPEndPoint ip ? ip.AddressFamily : AddressFamily.InterNetwork;
        }
    }
}
