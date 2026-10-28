using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;
using RPC.Library.Channels;
using RPC.Library.Dispatch;
using RPC.Library.Proxies;

namespace RPC.Library
{
    /// <summary>
    /// One end of a connection: exposes <typeparamref name="TLocalContract"/> to the other side
    /// and calls <typeparamref name="TRemoteContract"/> on it through <see cref="Remote"/>
    /// </summary>
    public sealed class RpcPeer<TLocalContract, TRemoteContract> : IDisposable
        where TRemoteContract : class
    {
        private readonly RpcChannel channel;
        private readonly ServiceDispatcher dispatcher;

        internal RpcPeer(IRpcConnection connection, RpcOptions options)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            ContractChecks.EnsureSupported(connection.Features, typeof(TLocalContract), typeof(TRemoteContract));

            this.Connection = connection;
            this.Options = options ?? new RpcOptions();
            this.dispatcher = new ServiceDispatcher(typeof(TLocalContract));
            this.channel = new RpcChannel(connection, this.Options, this.dispatcher);
            this.channel.Closed += (sender, reason) => this.Closed?.Invoke(this, reason);
            this.channel.UnhandledError += (sender, error) => this.UnhandledError?.Invoke(this, error);
            this.Remote = RpcProxy<TRemoteContract>.Create(this.channel);
        }

        /// <summary>
        /// Raised once when the connection closes; the argument is the failure, or null for a clean close
        /// </summary>
        public event EventHandler<Exception> Closed;

        /// <summary>
        /// Failures with nobody to report to: a one-way call that threw, a request over a one-way transport, a bad datagram
        /// </summary>
        public event EventHandler<Exception> UnhandledError;

        public IRpcConnection Connection { get; }

        public RpcOptions Options { get; }

        public TransportFeatures Features => this.Connection.Features;

        public bool IsConnected => this.channel.IsOpen;

        /// <summary>
        /// Proxy that sends every call to the other side
        /// </summary>
        public TRemoteContract Remote { get; }

        /// <summary>
        /// Instance the other side's calls run on
        /// </summary>
        public TLocalContract Service => (TLocalContract)this.dispatcher.Service;

        public void Dispose()
        {
            this.channel.Dispose();
        }

        internal void SetService(TLocalContract service)
        {
            this.dispatcher.Service = service;
        }

        internal void Start()
        {
            this.channel.Start();
        }
    }

    public static class RpcPeer
    {
        /// <summary>
        /// Connects to a host and starts serving <paramref name="service"/> to it
        /// </summary>
        public static async Task<RpcPeer<TLocalContract, TRemoteContract>> ConnectAsync<TLocalContract, TRemoteContract>(IRpcTransport transport,
                                                                                                                         EndPoint endPoint,
                                                                                                                         TLocalContract service,
                                                                                                                         RpcOptions options = null,
                                                                                                                         CancellationToken cancellationToken = default)
            where TRemoteContract : class
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));

            IRpcConnection connection = await transport.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);
            try
            {
                RpcPeer<TLocalContract, TRemoteContract> peer = new RpcPeer<TLocalContract, TRemoteContract>(connection, options);
                peer.SetService(service);
                peer.Start();
                return peer;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public static Task<RpcPeer<TLocalContract, TRemoteContract>> ConnectAsync<TLocalContract, TRemoteContract>(IRpcTransport transport,
                                                                                                                   string host,
                                                                                                                   int port,
                                                                                                                   TLocalContract service,
                                                                                                                   RpcOptions options = null,
                                                                                                                   CancellationToken cancellationToken = default)
            where TRemoteContract : class
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            EndPoint endPoint = IPAddress.TryParse(host, out IPAddress address)
                ? new IPEndPoint(address, port)
                : (EndPoint)new DnsEndPoint(host, port);

            return ConnectAsync<TLocalContract, TRemoteContract>(transport, endPoint, service, options, cancellationToken);
        }
    }
}
