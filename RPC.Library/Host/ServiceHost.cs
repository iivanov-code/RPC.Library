using System;
using System.Net.Sockets;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Network;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Host
{
    public sealed class MainServiceHost<TServiceImplementation, TServiceContract> : ServiceHostSingleton<INetworkClient, MainServiceHost<TServiceImplementation, TServiceContract>>
       where TServiceContract : class
       where TServiceImplementation : class
    {
        private readonly TServiceImplementation singletonServiceImplementation;

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
            ServiceNetworkClient<TServiceImplementation, TServiceContract> client;

            if (singletonServiceImplementation == null)
            {
                client = new ServiceNetworkClient<TServiceImplementation, TServiceContract>(socket, Activator.CreateInstance<TServiceImplementation>());
            }
            else
            {
                client = new ServiceNetworkClient<TServiceImplementation, TServiceContract>(socket, singletonServiceImplementation);
            }

            this.clients.Add(client);
            return client;
        }

        public MainServiceHost(TServiceImplementation serviceInstance, bool clientStartListening = false)
            : base(clientStartListening)
        {
            this.singletonServiceImplementation = serviceInstance;
        }

        public MainServiceHost(bool clientStartListening = false)
            : base(clientStartListening)
        {
        }
    }
}