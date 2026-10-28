using System.IO;
using System.Net;
using System.Net.Sockets;

namespace RPC.Library.Transports.Tcp
{
    public sealed class TcpRpcConnection : StreamRpcConnection
    {
        private readonly Socket socket;
        private readonly EndPoint localEndPoint;
        private readonly EndPoint remoteEndPoint;

        internal TcpRpcConnection(Socket socket, Stream stream, int maxMessageSize)
            : base(stream, maxMessageSize)
        {
            this.socket = socket;
            this.localEndPoint = socket.LocalEndPoint;
            this.remoteEndPoint = socket.RemoteEndPoint;
        }

        public override EndPoint LocalEndPoint => this.localEndPoint;

        public override EndPoint RemoteEndPoint => this.remoteEndPoint;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.socket.Dispose();
            }
        }
    }
}
