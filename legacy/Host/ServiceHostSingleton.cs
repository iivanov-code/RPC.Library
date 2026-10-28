using System.Collections.Generic;
using System.Net.Sockets;
using NetworkCommunicator.Interfaces;
using static NetworkCommunicator.Host.ActivatorExtensions;

namespace NetworkCommunicator.Host
{
    public abstract class ServiceHostSingleton<TClient, THost> : BaseServiceHost<TClient>
           where TClient : INetworkClient
           where THost : IServiceHost
    {
        private static readonly Dictionary<HostKey, THost> instances = new Dictionary<HostKey, THost>();

        internal static THost Instance(ushort port, bool clientStartListening = false)
        {
            var key = new HostKey(port, ProtocolType.Tcp);
            if (!instances.ContainsKey(key))
            {
                THost value = CreateInstance<THost>(clientStartListening);
                value.StartListeningForClients(port);
                instances.Add(key, value);
                return value;
            }
            else
            {
                return instances[key];
            }
        }

        protected ServiceHostSingleton(bool clientStartListening = false)
            : base(clientStartListening)
        { }
    }
}