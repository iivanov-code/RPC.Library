using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Interfaces;

namespace NetworkCommunicator.Host
{
    public abstract class BaseServiceHost<TClient> : IDisposable, IServiceHost where TClient : INetworkClient
    {
        protected readonly List<INetworkClient> clients;

        private readonly bool clientStartListening;

        private readonly CancellationTokenSource tokenSource;

        private bool disposedValue;

        private Task listeningTask;

        protected BaseServiceHost(bool clientStartListening = false)
        {
            this.clientStartListening = clientStartListening;
            clients = new List<INetworkClient>();
            this.tokenSource = new CancellationTokenSource();
            Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public IPEndPoint LocalEndpoint
        {
            get
            {
                return Listener.LocalEndPoint as IPEndPoint;
            }
        }

        public IReadOnlyList<INetworkClient> NetworkClients
        {
            get
            {
                return clients;
            }
        }

        protected Socket Listener { get; private set; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task StartListeningForClients(ushort port)
        {
            if (listeningTask == null || !listeningTask.IsCompleted)
            {
                Listener.Bind(new IPEndPoint(IPAddress.Any, port));
                Listener.Listen(100);

                this.listeningTask = Task.Factory.StartNew(() =>
                {
                    while (!this.tokenSource.IsCancellationRequested)
                    {
                        CreateClientFromSocket(Listener.Accept());
                    }
                }, this.tokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            return this.listeningTask;
        }

        public void StopListening()
        {
            this.tokenSource.Cancel();
            this.Listener.Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var client in this.clients)
                    {
                        client.Dispose();
                    }

                    this.tokenSource.Dispose();
                }
                disposedValue = true;
            }
        }

        protected abstract TClient OnNewClientAdded(Socket socket);

        private void CreateClientFromSocket(Socket socket)
        {
            TClient client = OnNewClientAdded(socket);
            if (this.clientStartListening)
            {
                client.Listen();
            }

            clients.Add(client);
        }
    }
}
