#if NET8_0_OR_GREATER
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace RPC.Library.Transports.Quic
{
    public class QuicTransportOptions
    {
        public const int DEFAULT_MAX_MESSAGE_SIZE = 16 * 1024 * 1024;

        /// <summary>
        /// Application protocol negotiated during the TLS handshake; both sides must agree
        /// </summary>
        public string ApplicationProtocol { get; set; } = "rpc.library/1";

        public int MaxMessageSize { get; set; } = DEFAULT_MAX_MESSAGE_SIZE;

        /// <summary>
        /// Required to listen. QUIC always runs TLS 1.3
        /// </summary>
        public X509Certificate2 ServerCertificate { get; set; }

        /// <summary>
        /// Host name presented by the client; defaults to the DNS end point's host or "localhost"
        /// </summary>
        public string TargetHost { get; set; }

        /// <summary>
        /// Client side validation of the server certificate; null uses the system trust store
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; }
    }
}
#endif
