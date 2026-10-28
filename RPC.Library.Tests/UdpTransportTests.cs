using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RPC.Library.Transports.Udp;
using Xunit;

namespace RPC.Library.Tests
{
    public class UdpTransportTests
    {
        private static readonly TimeSpan WAIT = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task OneWayCall_ReachesTheHost()
        {
            UdpTransport transport = new UdpTransport();
            NotificationSink sink = new NotificationSink();

            using (RpcHost<INotifications, NoService> host = new RpcHost<INotifications, NoService>(transport, sink))
            {
                await host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

                using (RpcPeer<NoService, INotifications> client = await RpcPeer.ConnectAsync<NoService, INotifications>(transport, host.LocalEndPoint, null))
                {
                    client.Remote.Log("fire and forget");

                    Assert.Equal("fire and forget", await sink.Logged.WaitAsync(WAIT));
                }
            }
        }

        [Fact]
        public async Task Host_CanNotifyTheClientBack()
        {
            UdpTransport transport = new UdpTransport();
            NotificationSink serverSink = new NotificationSink();
            NotificationSink clientSink = new NotificationSink();

            using (RpcHost<INotifications, INotifications> host = new RpcHost<INotifications, INotifications>(transport, serverSink))
            {
                TaskCompletionSource<RpcPeer<INotifications, INotifications>> connected = new TaskCompletionSource<RpcPeer<INotifications, INotifications>>(TaskCreationOptions.RunContinuationsAsynchronously);
                host.PeerConnected += (sender, peer) => connected.TrySetResult(peer);
                await host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

                using (RpcPeer<INotifications, INotifications> client = await RpcPeer.ConnectAsync<INotifications, INotifications>(transport, host.LocalEndPoint, clientSink))
                {
                    // the host only learns about a sender from its first datagram
                    client.Remote.Log("hello");
                    Assert.Equal("hello", await serverSink.Logged.WaitAsync(WAIT));

                    RpcPeer<INotifications, INotifications> serverPeer = await connected.Task.WaitAsync(WAIT);
                    serverPeer.Remote.Log("back");

                    Assert.Equal("back", await clientSink.Logged.WaitAsync(WAIT));
                }
            }
        }

        [Fact]
        public void Host_RejectsContractsThatNeedResponses()
        {
            RpcTransportException error = Assert.Throws<RpcTransportException>(
                () => new RpcHost<ICalculator, NoService>(new UdpTransport(), peer => null));

            Assert.Contains("ICalculator.Add", error.Message);
            Assert.DoesNotContain("ICalculator.Fire", error.Message);
        }

        [Fact]
        public async Task Client_RejectsContractsThatNeedResponses()
        {
            UdpTransport transport = new UdpTransport();

            using (RpcHost<INotifications, NoService> host = new RpcHost<INotifications, NoService>(transport, new NotificationSink()))
            {
                await host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

                RpcTransportException error = await Assert.ThrowsAsync<RpcTransportException>(
                    () => RpcPeer.ConnectAsync<NoService, ICalculator>(transport, host.LocalEndPoint, null));

                Assert.Contains("ICalculator.AddAsync", error.Message);
            }
        }

        [Fact]
        public async Task MessageLargerThanADatagram_IsRejectedBeforeSending()
        {
            UdpTransport transport = new UdpTransport(new UdpTransportOptions { MaxDatagramSize = 512 });

            using (RpcHost<INotifications, NoService> host = new RpcHost<INotifications, NoService>(transport, new NotificationSink()))
            {
                await host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

                using (RpcPeer<NoService, INotifications> client = await RpcPeer.ConnectAsync<NoService, INotifications>(transport, host.LocalEndPoint, null))
                {
                    RpcMessageTooLargeException error = Assert.Throws<RpcMessageTooLargeException>(() => client.Remote.Log(new string('x', 1000)));

                    Assert.Equal(512, error.MaxSize);
                    Assert.True(client.IsConnected);
                }
            }
        }

        [Fact]
        public async Task GarbageDatagram_IsDroppedAndReported()
        {
            UdpTransport transport = new UdpTransport();
            NotificationSink sink = new NotificationSink();

            using (RpcHost<INotifications, NoService> host = new RpcHost<INotifications, NoService>(transport, sink))
            {
                TaskCompletionSource<Exception> reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                host.PeerConnected += (sender, peer) => peer.UnhandledError += (s, error) => reported.TrySetResult(error);
                await host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

                using (Socket raw = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    raw.Connect(host.LocalEndPoint);
                    raw.Send(new byte[] { 0xFF, 0xFF, 0xFF });

                    Assert.IsType<RpcProtocolException>(await reported.Task.WaitAsync(WAIT));

                    // the same sender can still deliver valid frames afterwards
                    using (RpcPeer<NoService, INotifications> client = await RpcPeer.ConnectAsync<NoService, INotifications>(transport, host.LocalEndPoint, null))
                    {
                        client.Remote.Log("still alive");
                        Assert.Equal("still alive", await sink.Logged.WaitAsync(WAIT));
                    }
                }
            }
        }

        [Fact]
        public void Transport_DeclaresNoFeatures()
        {
            Assert.Equal(Abstractions.TransportFeatures.None, new UdpTransport().Features);
        }
    }
}
