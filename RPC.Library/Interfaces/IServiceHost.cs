using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace NetworkCommunicator.Interfaces
{
    public interface IServiceHost : IDisposable
    {
        IPEndPoint LocalEndpoint { get; }
        IReadOnlyList<INetworkClient> NetworkClients { get; }

        Task StartListeningForClients(ushort port);

        void StopListening();
    }
}