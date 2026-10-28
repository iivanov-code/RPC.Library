#if NET8_0_OR_GREATER
#pragma warning disable CA2252
using System.Net;
using System.Net.Quic;
using System.Runtime.Versioning;

namespace RPC.Library.Transports.Quic
{
    /// <summary>
    /// One QUIC connection carrying one bidirectional stream of frames
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public sealed class QuicRpcConnection : StreamRpcConnection
    {
        private readonly QuicConnection connection;

        internal QuicRpcConnection(QuicConnection connection, QuicStream stream, int maxMessageSize)
            : base(stream, maxMessageSize)
        {
            this.connection = connection;
            this.LocalEndPoint = connection.LocalEndPoint;
            this.RemoteEndPoint = connection.RemoteEndPoint;
        }

        public override EndPoint LocalEndPoint { get; }

        public override EndPoint RemoteEndPoint { get; }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
#endif
