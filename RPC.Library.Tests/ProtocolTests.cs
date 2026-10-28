using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using RPC.Library.Abstractions;
using RPC.Library.Protocol;
using RPC.Library.Serialization;
using RPC.Library.Transports.Tcp;
using Xunit;

namespace RPC.Library.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void FrameHeader_RoundTrips()
        {
            using (PooledBufferWriter writer = new PooledBufferWriter(8))
            {
                FrameHeader.Write(writer, FrameType.Request, 12345, "Add");
                byte[] payload = Encoding.UTF8.GetBytes("[1,2]");
                payload.CopyTo(writer.GetSpan(payload.Length));
                writer.Advance(payload.Length);

                Assert.True(FrameHeader.TryParse(writer.WrittenMemory, out FrameType type, out int id, out string method, out ReadOnlyMemory<byte> parsedPayload));
                Assert.Equal(FrameType.Request, type);
                Assert.Equal(12345, id);
                Assert.Equal("Add", method);
                Assert.Equal(payload, parsedPayload.ToArray());
            }
        }

        [Fact]
        public void FrameHeader_RejectsGarbage()
        {
            Assert.False(FrameHeader.TryParse(new byte[] { 1, 2, 3 }, out _, out _, out _, out _));
            Assert.False(FrameHeader.TryParse(new byte[] { 9, 0, 0, 0, 0, 0, 0 }, out _, out _, out _, out _));
            Assert.False(FrameHeader.TryParse(new byte[] { 1, 0, 0, 0, 0, 50, 0 }, out _, out _, out _, out _));
        }

        [Fact]
        public void PooledBufferWriter_GrowsAndKeepsContent()
        {
            byte[] data = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();

            using (PooledBufferWriter writer = new PooledBufferWriter(16))
            {
                foreach (byte[] chunk in data.Chunk(100))
                {
                    chunk.CopyTo(writer.GetSpan(chunk.Length));
                    writer.Advance(chunk.Length);
                }

                Assert.Equal(data, writer.WrittenSpan.ToArray());
            }
        }

        [Fact]
        public void JsonSerializer_RoundTripsArguments()
        {
            JsonRpcSerializer serializer = new JsonRpcSerializer();
            Type[] types = { typeof(string), typeof(int), typeof(Person), typeof(int[]) };
            object[] arguments = { "text", 7, new Person { Name = "Bo", Age = 3 }, new[] { 1, 2 } };

            using (PooledBufferWriter writer = new PooledBufferWriter())
            {
                serializer.SerializeArguments(writer, arguments, types);
                object[] parsed = serializer.DeserializeArguments(writer.WrittenMemory, types);

                Assert.Equal("text", parsed[0]);
                Assert.Equal(7, parsed[1]);
                Assert.Equal("Bo", ((Person)parsed[2]).Name);
                Assert.Equal(new[] { 1, 2 }, (int[])parsed[3]);
            }

            using (PooledBufferWriter writer = new PooledBufferWriter())
            {
                serializer.SerializeArguments(writer, new object[0], Type.EmptyTypes);

                Assert.Equal(0, writer.WrittenCount);
                Assert.Empty(serializer.DeserializeArguments(writer.WrittenMemory, Type.EmptyTypes));
            }
        }

        [Fact]
        public async Task StreamConnection_KeepsMessagesApart()
        {
            TcpTransport transport = new TcpTransport();
            using (IRpcListener listener = await transport.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                Task<IRpcConnection> accepting = listener.AcceptAsync();
                using (IRpcConnection client = await transport.ConnectAsync(listener.LocalEndPoint))
                using (IRpcConnection server = await accepting)
                {
                    byte[][] messages = { new byte[] { 1 }, new byte[0], Enumerable.Repeat((byte)7, 70000).ToArray() };
                    await Task.WhenAll(messages.Select(m => client.SendAsync(m).AsTask()));

                    int[] received = new int[messages.Length];
                    for (int i = 0; i < messages.Length; i++)
                    {
                        using (ReceivedMessage message = await server.ReceiveAsync())
                        {
                            received[i] = message.Length;
                        }
                    }

                    Assert.Equal(messages.Select(m => m.Length).OrderBy(l => l), received.OrderBy(l => l));

                    client.Dispose();
                    Assert.Null(await server.ReceiveAsync());
                }
            }
        }

        [Fact]
        public async Task StreamConnection_RejectsOversizedMessages()
        {
            TcpTransport transport = new TcpTransport(new TcpTransportOptions { MaxMessageSize = 100 });
            using (IRpcListener listener = await transport.ListenAsync(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                Task<IRpcConnection> accepting = listener.AcceptAsync();
                using (IRpcConnection client = await transport.ConnectAsync(listener.LocalEndPoint))
                using (IRpcConnection server = await accepting)
                {
                    await Assert.ThrowsAsync<RpcMessageTooLargeException>(() => client.SendAsync(new byte[101]).AsTask());
                }
            }
        }
    }
}
