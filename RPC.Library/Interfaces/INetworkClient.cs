using System;
using System.Net;
using System.Threading.Tasks;

namespace NetworkCommunicator.Interfaces
{
    public interface INetworkClient : INetworkSender, IDisposable
    {
        IPEndPoint LocalEndpoint { get; }
        IPAddress RemoteHostIP { get; }
        ushort RemotePort { get; }

        Task<bool> AcceptConnection(ushort port);
        Task<bool> Connect();
        Task Listen();
    }
}