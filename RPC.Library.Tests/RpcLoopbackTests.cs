using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace RPC.Library.Tests
{
    public class RpcLoopbackTests
    {
        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task SynchronousCall_ReturnsTheResult(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal(5, loopback.Client.Remote.Add(2, 3));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task AsyncCall_ReturnsTheResult(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal(7, await loopback.Client.Remote.AddAsync(3, 4));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task TaskWithoutResult_CompletesAfterTheRemoteSide(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                await loopback.Client.Remote.DelayAsync(20);
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task ValueTaskCall_ReturnsTheResult(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal(42, await loopback.Client.Remote.DoubleAsync(21));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task CallWithoutArguments_Works(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal("pong", loopback.Client.Remote.Ping());
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task MultipleParameters_ArriveInOrder(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal("a-1-True", loopback.Client.Remote.Concat("a", 1, true));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task ComplexParameter_IsSerialized(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal("Ann is 30", loopback.Client.Remote.Describe(new Person { Name = "Ann", Age = 30 }));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task LargeMessage_IsReassembled(string transport)
        {
            const int SIZE = 2_000_000;

            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                byte[] blob = loopback.Client.Remote.Blob(SIZE);

                Assert.Equal(SIZE, blob.Length);
                Assert.True(Enumerable.Range(0, SIZE).All(i => blob[i] == (byte)(i % 251)));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task ConcurrentCalls_GetTheirOwnResponses(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                int[] results = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => loopback.Client.Remote.AddAsync(i, i)));

                Assert.Equal(Enumerable.Range(0, 200).Select(i => 2 * i), results);
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task RemoteException_SurfacesAsRpcRemoteException(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                RpcRemoteException sync = Assert.Throws<RpcRemoteException>(() => loopback.Client.Remote.Throws());
                Assert.Equal("boom", sync.Message);
                Assert.Equal(typeof(InvalidOperationException).FullName, sync.RemoteType);
                Assert.Null(sync.RemoteDetails);

                RpcRemoteException async = await Assert.ThrowsAsync<RpcRemoteException>(() => loopback.Client.Remote.ThrowsAsync());
                Assert.Equal("async boom", async.Message);

                // the connection survives a failed call
                Assert.Equal(5, loopback.Client.Remote.Add(2, 3));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task RemoteException_CarriesDetailsWhenEnabled(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport, new RpcOptions { IncludeExceptionDetails = true }))
            {
                RpcRemoteException error = Assert.Throws<RpcRemoteException>(() => loopback.Client.Remote.Throws());

                Assert.Contains(nameof(CalculatorService.Throws), error.RemoteDetails);
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task OneWayCall_IsDeliveredWithoutWaiting(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                loopback.Client.Remote.Fire("hello");

                Assert.Equal("hello", await loopback.Service.Fired.WaitAsync(Loopback.WAIT));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task Timeout_FailsTheCallButKeepsTheConnection(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport, new RpcOptions { CallTimeout = TimeSpan.FromMilliseconds(200) }))
            {
                await Assert.ThrowsAsync<RpcTimeoutException>(() => loopback.Client.Remote.DelayAsync(5000));

                Assert.True(loopback.Client.IsConnected);
                Assert.Equal(5, loopback.Client.Remote.Add(2, 3));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task Server_CanCallTheClientBack(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                Assert.Equal("ABC", await loopback.Client.Remote.UpperViaCallbackAsync("abc"));

                loopback.ServerPeer.Remote.Notify("ping");
                Assert.Equal("ping", await loopback.Callback.Notified.WaitAsync(Loopback.WAIT));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task HostStop_ClosesTheClient(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                TaskCompletionSource<bool> closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                loopback.Client.Closed += (sender, reason) => closed.TrySetResult(true);

                Task<int> pending = loopback.Client.Remote.AddAsync(1, 1);
                Assert.Equal(2, await pending);

                Task delayed = loopback.Client.Remote.DelayAsync(5000);
                await loopback.Host.StopAsync();

                await closed.Task.WaitAsync(Loopback.WAIT);
                await Assert.ThrowsAsync<RpcConnectionException>(() => delayed);
                Assert.False(loopback.Client.IsConnected);
                Assert.Throws<RpcConnectionException>(() => loopback.Client.Remote.Add(1, 1));
            }
        }

        [Theory]
        [MemberData(nameof(Loopback.Transports), MemberType = typeof(Loopback))]
        public async Task ClientDispose_RemovesThePeerFromTheHost(string transport)
        {
            using (Loopback loopback = await Loopback.StartAsync(transport))
            {
                TaskCompletionSource<bool> disconnected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                loopback.Host.PeerDisconnected += (sender, peer) => disconnected.TrySetResult(true);
                Assert.Single(loopback.Host.Peers);

                loopback.Client.Dispose();

                await disconnected.Task.WaitAsync(Loopback.WAIT);
                Assert.Empty(loopback.Host.Peers);
            }
        }
    }
}
