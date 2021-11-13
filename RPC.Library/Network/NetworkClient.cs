using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkCommunicator.Args;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Models;
using RPC.Library.Listeners;

namespace NetworkCommunicator.Network
{
    public abstract class NetworkClient : IDisposable, INetworkClient
    {
        private Socket socket;
        private bool disposedValue;
        private IListener listener;
        private IPAddress remoteHostIp;
        private ushort remotePort;

        public NetworkClient(Socket socket, int bufferSize = 4096)
        {
            this.socket = socket;
            this.listener = new Listener(ref this.socket, bufferSize);
        }

        public NetworkClient(string remoteHostIP, ushort remotePort, int bufferSize = 4096)
        {
            this.RemoteHostIP = IPAddress.Parse(remoteHostIP);
            this.RemotePort = remotePort;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.listener = new Listener(ref socket, bufferSize);
        }

        internal event EventHandler<MessageEventArgs> MessageReceived
        {
            add
            {
                listener.MessageReceived += value;
            }
            remove
            {
                listener.MessageReceived -= value;
            }
        }

        protected internal IPAddress RemoteHostIP
        {
            get
            {
                if (remoteHostIp == null)
                {
                    remoteHostIp = (socket.RemoteEndPoint as IPEndPoint)?.Address;
                }

                return remoteHostIp;
            }
            set
            {
                remoteHostIp = value;
            }
        }

        protected internal ushort RemotePort
        {
            get
            {
                if (remotePort == 0)
                {
                    remotePort = (ushort)(socket.RemoteEndPoint as IPEndPoint)?.Port;
                }

                return remotePort;
            }
            set
            {
                remotePort = value;
            }
        }

        public Task<bool> Connect()
        {
            return listener.Connect(RemoteHostIP, RemotePort);
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        public Task Listen()
        {
            return listener.Listen();
        }

        public Task<bool> AcceptConnection(ushort port)
        {
            return listener.AcceptConnection(port);
        }

        public Task<int> Send(byte[] messageBuffer, BaseWaitContext context)
        {
            return listener.Send(messageBuffer, context);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    socket?.Dispose();
                    listener?.Dispose();
                }

                disposedValue = true;
            }
        }
    }
}
