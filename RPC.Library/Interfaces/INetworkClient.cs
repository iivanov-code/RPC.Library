using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkCommunicator.Interfaces
{
    public interface INetworkClient : INetworkSender, IDisposable
    {
        ValueTask ConnectAsync(CancellationToken? token = null);
        Task Listen(CancellationToken? token = null);
        Task ListenAsync(CancellationToken? token = null);
        void ListenEvent();
    }
}