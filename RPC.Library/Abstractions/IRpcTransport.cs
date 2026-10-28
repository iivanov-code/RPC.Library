using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Library.Abstractions
{
    /// <summary>
    /// Creates connections and listeners for one underlying protocol (TCP, QUIC, UDP, ...)
    /// </summary>
    public interface IRpcTransport
    {
        TransportFeatures Features { get; }

        Task<IRpcConnection> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default);

        Task<IRpcListener> ListenAsync(EndPoint endPoint, CancellationToken cancellationToken = default);
    }
}
