#if NET8_0_OR_GREATER
// System.Net.Quic is flagged as a preview API by the analyzer; nothing from it leaks into this library's public surface
#pragma warning disable CA2252
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Quic
{
    /// <summary>
    /// Reliable, ordered, request/response transport over QUIC (UDP with TLS 1.3 and no head-of-line
    /// blocking between connections). Needs the MsQuic library, which ships with Windows 11 and
    /// recent Windows Server; check <see cref="IsSupported"/> first
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public sealed class QuicTransport : IRpcTransport
    {
        private const byte STREAM_PREAMBLE = 0x52;

        private readonly QuicTransportOptions options;

        public QuicTransport(QuicTransportOptions options = null)
        {
            this.options = options ?? new QuicTransportOptions();
            if (string.IsNullOrEmpty(this.options.ApplicationProtocol))
                throw new ArgumentException("ApplicationProtocol must be set", nameof(options));
        }

        public static bool IsSupported => QuicConnection.IsSupported && QuicListener.IsSupported;

        public TransportFeatures Features => TransportFeatures.Stream;

        public async Task<IRpcConnection> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            EnsureSupported();

            QuicClientConnectionOptions connectionOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = endPoint,
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol(this.options.ApplicationProtocol) },
                    TargetHost = this.options.TargetHost ?? (endPoint as DnsEndPoint)?.Host ?? "localhost",
                    RemoteCertificateValidationCallback = this.options.RemoteCertificateValidationCallback
                }
            };

            QuicConnection connection;
            try
            {
                connection = await QuicConnection.ConnectAsync(connectionOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (QuicException ex)
            {
                throw new RpcConnectionException($"Could not connect to {endPoint}", ex);
            }

            try
            {
                QuicStream stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);

                // the peer only learns about the stream once bytes flow on it
                await stream.WriteAsync(new byte[] { STREAM_PREAMBLE }, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                return new QuicRpcConnection(connection, stream, this.options.MaxMessageSize);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<IRpcListener> ListenAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (!(endPoint is IPEndPoint ipEndPoint)) throw new ArgumentException("QUIC can only listen on an IP end point", nameof(endPoint));
            if (this.options.ServerCertificate == null) throw new InvalidOperationException("QuicTransportOptions.ServerCertificate is required to listen");
            EnsureSupported();

            List<SslApplicationProtocol> protocols = new List<SslApplicationProtocol> { new SslApplicationProtocol(this.options.ApplicationProtocol) };

            QuicListenerOptions listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = ipEndPoint,
                ApplicationProtocols = protocols,
                ConnectionOptionsCallback = (connection, info, token) => ValueTask.FromResult(new QuicServerConnectionOptions
                {
                    DefaultStreamErrorCode = 0,
                    DefaultCloseErrorCode = 0,
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        ApplicationProtocols = protocols,
                        ServerCertificate = this.options.ServerCertificate
                    }
                })
            };

            try
            {
                QuicListener listener = await QuicListener.ListenAsync(listenerOptions, cancellationToken).ConfigureAwait(false);
                return new QuicRpcListener(listener, this.options);
            }
            catch (QuicException ex)
            {
                throw new RpcConnectionException($"Could not listen on {endPoint}", ex);
            }
        }

        internal static async Task ReadPreambleAsync(QuicStream stream, CancellationToken cancellationToken)
        {
            byte[] preamble = new byte[1];
            int read = await stream.ReadAsync(preamble, cancellationToken).ConfigureAwait(false);
            if (read != 1 || preamble[0] != STREAM_PREAMBLE)
                throw new RpcProtocolException("QUIC stream did not start with the expected preamble");
        }

        private static void EnsureSupported()
        {
            if (!IsSupported)
                throw new PlatformNotSupportedException("QUIC is not available: System.Net.Quic needs MsQuic (Windows 11 / Server 2022 or libmsquic on Linux)");
        }
    }
}
#endif
