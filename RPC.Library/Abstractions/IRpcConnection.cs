using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Library.Abstractions
{
    /// <summary>
    /// A message oriented link to one remote peer: one call sends one whole message,
    /// one call receives one whole message. Framing is the transport's business
    /// </summary>
    public interface IRpcConnection : IDisposable
    {
        EndPoint LocalEndPoint { get; }

        EndPoint RemoteEndPoint { get; }

        TransportFeatures Features { get; }

        /// <summary>
        /// Largest message the transport can carry in one piece
        /// </summary>
        int MaxMessageSize { get; }

        /// <summary>
        /// Sends one complete message. Safe to call concurrently
        /// </summary>
        ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Receives the next complete message, or null once the connection is closed
        /// </summary>
        ValueTask<ReceivedMessage> ReceiveAsync(CancellationToken cancellationToken = default);
    }
}
