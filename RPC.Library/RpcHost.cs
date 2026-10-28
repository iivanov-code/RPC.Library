using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;
using RPC.Library.Dispatch;

namespace RPC.Library
{
    /// <summary>
    /// Accepts peers on one transport and serves <typeparamref name="TLocalContract"/> to each of them
    /// </summary>
    public sealed class RpcHost<TLocalContract, TRemoteContract> : IDisposable
        where TRemoteContract : class
    {
        private readonly IRpcTransport transport;
        private readonly Func<RpcPeer<TLocalContract, TRemoteContract>, TLocalContract> serviceFactory;
        private readonly RpcOptions options;
        private readonly ConcurrentDictionary<RpcPeer<TLocalContract, TRemoteContract>, byte> peers;
        private readonly CancellationTokenSource stopSource;
        private IRpcListener listener;
        private Task acceptLoop;
        private int disposed;

        /// <summary>
        /// Creates a service instance per peer; the factory receives the peer so the service can call it back
        /// </summary>
        public RpcHost(IRpcTransport transport, Func<RpcPeer<TLocalContract, TRemoteContract>, TLocalContract> serviceFactory, RpcOptions options = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
            this.options = options ?? new RpcOptions();
            this.peers = new ConcurrentDictionary<RpcPeer<TLocalContract, TRemoteContract>, byte>();
            this.stopSource = new CancellationTokenSource();

            ContractChecks.EnsureSupported(transport.Features, typeof(TLocalContract), typeof(TRemoteContract));
        }

        /// <summary>
        /// Serves one shared instance to every peer; it must be thread safe
        /// </summary>
        public RpcHost(IRpcTransport transport, TLocalContract service, RpcOptions options = null)
            : this(transport, peer => service, options)
        { }

        public event EventHandler<RpcPeer<TLocalContract, TRemoteContract>> PeerConnected;

        public event EventHandler<RpcPeer<TLocalContract, TRemoteContract>> PeerDisconnected;

        /// <summary>
        /// Raised when a peer could not be accepted or set up; the host keeps accepting
        /// </summary>
        public event EventHandler<Exception> AcceptFailed;

        public EndPoint LocalEndPoint => this.listener?.LocalEndPoint;

        public bool IsListening => this.listener != null && !this.stopSource.IsCancellationRequested;

        public IReadOnlyCollection<RpcPeer<TLocalContract, TRemoteContract>> Peers => this.peers.Keys.ToList();

        public async Task StartAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (endPoint == null) throw new ArgumentNullException(nameof(endPoint));
            if (this.listener != null) throw new InvalidOperationException("Host already started");
            if (this.disposed != 0) throw new ObjectDisposedException(nameof(RpcHost<TLocalContract, TRemoteContract>));

            this.listener = await this.transport.ListenAsync(endPoint, cancellationToken).ConfigureAwait(false);
            this.acceptLoop = Task.Run(this.AcceptLoopAsync);
        }

        public Task StartAsync(int port, CancellationToken cancellationToken = default)
        {
            return this.StartAsync(new IPEndPoint(IPAddress.Any, port), cancellationToken);
        }

        /// <summary>
        /// Stops accepting and closes every peer
        /// </summary>
        public async Task StopAsync()
        {
            this.stopSource.Cancel();
            this.listener?.Dispose();

            if (this.acceptLoop != null)
            {
                await this.acceptLoop.ConfigureAwait(false);
            }

            foreach (RpcPeer<TLocalContract, TRemoteContract> peer in this.peers.Keys)
            {
                peer.Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.StopAsync().GetAwaiter().GetResult();
            this.stopSource.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!this.stopSource.IsCancellationRequested)
            {
                IRpcConnection connection;
                try
                {
                    connection = await this.listener.AcceptAsync(this.stopSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (this.stopSource.IsCancellationRequested) break;

                    this.AcceptFailed?.Invoke(this, ex);
                    continue;
                }

                this.AddPeer(connection);
            }
        }

        private void AddPeer(IRpcConnection connection)
        {
            RpcPeer<TLocalContract, TRemoteContract> peer = null;
            try
            {
                peer = new RpcPeer<TLocalContract, TRemoteContract>(connection, this.options);
                peer.SetService(this.serviceFactory(peer));

                this.peers[peer] = 0;
                peer.Closed += (sender, reason) =>
                {
                    this.peers.TryRemove(peer, out _);
                    this.PeerDisconnected?.Invoke(this, peer);
                };

                peer.Start();
                this.PeerConnected?.Invoke(this, peer);
            }
            catch (Exception ex)
            {
                if (peer != null)
                {
                    this.peers.TryRemove(peer, out _);
                    peer.Dispose();
                }
                else
                {
                    connection.Dispose();
                }

                this.AcceptFailed?.Invoke(this, ex);
            }
        }
    }
}
