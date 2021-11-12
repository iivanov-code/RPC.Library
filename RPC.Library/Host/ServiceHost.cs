using System.Net.Sockets;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Network;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Host
{
    public sealed class MainServiceHost<TServiceContract, TServiceImplementation> : ServiceHostSingleton<INetworkClient, MainServiceHost<TServiceContract, TServiceImplementation>>
       where TServiceContract : class
       where TServiceImplementation : class, TServiceContract, new()
    {
        public static IServiceHost InitMainNode(ushort port, bool clientStartListening = false)
        {
            while (!NetworkUtils.IsPortAvailable(port))
            {
                port = NetworkUtils.GetRandomPort();
            }

            var mainHost = Instance(port, clientStartListening);

            return mainHost;
        }

        protected override INetworkClient OnNewClientAdded(Socket socket)
        {
            var client = new ServiceNetworkClient<TServiceImplementation, TServiceContract>(socket, new TServiceImplementation());
            this.clients.Add(client);
            return client;
        }

        private MainServiceHost(bool clientStartListening = false)
            : base(clientStartListening)
        { }
    }
}
