using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Network;

namespace NetworkCommunicator.Utils
{
    public static class NetworkChannelBuilder
    {
        public static INetworkClient CreateClient<TCallbackService, TRemoteService>(string host, ushort port)
            where TCallbackService : class, new()
            where TRemoteService : class
        {
            return new ServiceNetworkClient<TCallbackService, TRemoteService>(host, port, new TCallbackService());
        }

        public static INetworkClient CreateClient<TCallbackService, TRemoteService>(string host, ushort port, TCallbackService service)
            where TCallbackService : class
            where TRemoteService : class
        {
            return new ServiceNetworkClient<TCallbackService, TRemoteService>(host, port, service);
        }
    }
}
