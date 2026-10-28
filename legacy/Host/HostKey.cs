using System.Net.Sockets;

namespace NetworkCommunicator.Host
{
    public class HostKey
    {
        public HostKey(ushort port, ProtocolType protocol)
        {
            Port = port;
            Protocol = protocol;
        }

        public readonly ushort Port;
        public readonly ProtocolType Protocol;

        public override int GetHashCode()
        {
            return Port.GetHashCode() ^ Protocol.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            HostKey other = (HostKey)obj;
            return Port == other.Port && Protocol == other.Protocol;
        }
    }
}