using System.Threading.Tasks;
using NetworkCommunicator.Models;

namespace NetworkCommunicator.Interfaces
{
    public interface INetworkSender
    {
        Task<int> Send(byte[] messageBuffer, BaseWaitContext context);
    }
}