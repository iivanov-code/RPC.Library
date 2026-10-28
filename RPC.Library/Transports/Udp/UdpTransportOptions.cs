namespace RPC.Library.Transports.Udp
{
    public class UdpTransportOptions
    {
        /// <summary>
        /// Largest UDP payload over IPv4. Datagrams above the path MTU (about 1400 bytes) are fragmented
        /// and get lost more easily, so lower this on real networks
        /// </summary>
        public const int MAX_DATAGRAM_SIZE = 65507;

        /// <summary>
        /// Largest message the transport accepts; one message is one datagram
        /// </summary>
        public int MaxDatagramSize { get; set; } = MAX_DATAGRAM_SIZE;
    }
}
