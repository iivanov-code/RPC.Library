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

        protected NetworkClient(string remoteHostIP, ushort remotePort, int bufferSize = 4096)
            : this(null, bufferSize)
        {
            this.RemoteHostIP = IPAddress.Parse(remoteHostIP);
            this.RemotePort = remotePort;
        }

        protected NetworkClient(Socket socket, int bufferSize = 4096, ListenerTypes type = ListenerTypes.Event)
        {
            if (socket == null)
            {
                this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }

            switch (type)
            {
                case ListenerTypes.Basic:
                    this.listener = new Listener(ref this.socket, bufferSize);
                    break;

                case ListenerTypes.Async:
                    this.listener = new AsyncListener(ref this.socket, bufferSize);
                    break;

                case ListenerTypes.Event:
                    this.listener = new EventListener(ref this.socket, bufferSize);
                    break;
            }
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

        public IPEndPoint LocalEndpoint
        {
            get
            {
                return socket.LocalEndPoint as IPEndPoint;
            }
        }

        public IPAddress RemoteHostIP
        {
            get
            {
                if (remoteHostIp == null)
                {
                    remoteHostIp = (socket.RemoteEndPoint as IPEndPoint)?.Address;
                }

                return remoteHostIp;
            }
            protected internal set
            {
                remoteHostIp = value;
            }
        }

        public ushort RemotePort
        {
            get
            {
                if (remotePort == 0)
                {
                    remotePort = (ushort)(socket.RemoteEndPoint as IPEndPoint)?.Port;
                }

                return remotePort;
            }
            protected internal set
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

        public Task<bool> Disconnect()
        {
            return listener.Disconnect();
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