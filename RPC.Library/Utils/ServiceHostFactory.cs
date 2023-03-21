using NetworkCommunicator.Host;
using NetworkCommunicator.Interfaces;

namespace NetworkCommunicator.Utils
{
    public static class ServiceHostFactory
    {
        public static IServiceHost CreateHostInstance<TServiceImplementation, TServiceContract>(ushort port, bool clientStartListening = false)
               where TServiceContract : class
               where TServiceImplementation : class, TServiceContract, new()
        {
            return MainServiceHost<TServiceImplementation, TServiceContract>.InitMainNode(port, clientStartListening);
        }
    }
}