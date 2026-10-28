using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports.Udp
{
    /// <summary>
    /// One-way transport: one datagram per message, no delivery guarantee, no responses.
    /// Only contracts made of void methods can be used over it; peers and hosts verify that
    /// </summary>
    public sealed class UdpTransport : IRpcTransport
    {
        private readonly UdpTransportOptions options;

        public UdpTransport(UdpTransportOptions options = null)
        {
            this.options = options ?? new UdpTransportOptions();
            if (this.options.MaxDatagramSize < 1 || this.options.MaxDatagramSize > UdpTransportOptions.MAX_DATAGRAM_SIZE)
                throw new ArgumentOutOfRangeException(nameof(options), $"MaxDatagramSize must be within 1..{UdpTransportOptions.MAX_DATAGRAM_SIZE}");
        }

        public TransportFeatures Features => TransportFeatures.None;

        public Task<IRpcConnection> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            Socket socket = CreateSocket(AddressFamilyOf(endPoint));
            try
            {
                // UDP connect only fixes the default destination and filters incoming datagrams; nothing goes on the wire
                socket.Connect(endPoint);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                throw new RpcConnectionException($"Could not bind the UDP socket to {endPoint}", ex);
            }

            return Task.FromResult<IRpcConnection>(new UdpClientRpcConnection(socket, this.options.MaxDatagramSize));
        }

        public Task<IRpcListener> ListenAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));

            Socket socket = CreateSocket(AddressFamilyOf(endPoint));
            try
            {
                socket.Bind(endPoint);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                throw new RpcConnectionException($"Could not bind to {endPoint}", ex);
            }

            return Task.FromResult<IRpcListener>(new UdpRpcListener(socket, this.options.MaxDatagramSize));
        }

        private static Socket CreateSocket(AddressFamily addressFamily)
        {
            Socket socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // SIO_UDP_CONNRESET: stop Windows from failing receives after an ICMP "port unreachable"
                const int SIO_UDP_CONNRESET = -1744830452;
                try
                {
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                }
                catch (SocketException)
                { }
            }

            return socket;
        }

        private static AddressFamily AddressFamilyOf(EndPoint endPoint)
        {
            return endPoint is IPEndPoint ip ? ip.AddressFamily : AddressFamily.InterNetwork;
        }
    }
}
