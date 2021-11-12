using NetworkCommunicator.Host;
using NetworkCommunicator.Interfaces;

namespace NetworkCommunicator.Utils
{
    public static class ServiceHostFactory
    {
        public static IServiceHost CreateHostInstance<TServiceContract, TServiceImplementation>(ushort port, bool clientStartListening = false)
               where TServiceContract : class
               where TServiceImplementation : class, TServiceContract, new()
        {
            return MainServiceHost<TServiceContract, TServiceImplementation>.InitMainNode(port, clientStartListening);
        }
    }
}
