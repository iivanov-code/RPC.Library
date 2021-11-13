using System;
using System.Net;
using System.Threading.Tasks;

namespace NetworkCommunicator.Interfaces
{
    public interface INetworkClient : INetworkSender, IDisposable
    {
        Task<bool> AcceptConnection(ushort port);
        Task<bool> Connect();
        Task Listen();
    }
}