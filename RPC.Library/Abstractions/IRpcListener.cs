using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Library.Abstractions
{
    public interface IRpcListener : IDisposable
    {
        EndPoint LocalEndPoint { get; }

        TransportFeatures Features { get; }

        /// <summary>
        /// Waits for the next remote peer and returns a connection to it
        /// </summary>
        Task<IRpcConnection> AcceptAsync(CancellationToken cancellationToken = default);
    }
}
