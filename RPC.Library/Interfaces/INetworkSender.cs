using System;
using System.Threading.Tasks;
using NetworkCommunicator.Models;

namespace NetworkCommunicator.Interfaces
{
    public interface INetworkSender
    {
        int Send(byte[] messageBuffer, BaseWaitContext context);
        Task<int> SendAsync(byte[] messageBuffer, BaseWaitContext context);
        IAsyncResult SendAsyncEvent(byte[] messageBuffer, BaseWaitContext context);
    }
}
