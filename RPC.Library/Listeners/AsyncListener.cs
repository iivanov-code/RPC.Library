using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Models;

namespace RPC.Library.Listeners
{
    internal class AsyncListener : BaseListener
    {
        public AsyncListener(ref Socket socket, int bufferSize, CancellationToken? token = null)
            : base(ref socket, bufferSize, token)
        { }

        public override Task<int> Send(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            return socket.SendAsync(segments, SocketFlags.None);
        }

        public override Task<bool> Connect(IPAddress remoteHostIp, ushort remotePort)
        {
            return socket.ConnectAsync(remoteHostIp, remotePort, Token)
                .AsTask()
                .ContinueWith(t =>
                {
                    return !t.IsFaulted;
                });
        }

        public override async Task Listen()
        {
            while (!Token.IsCancellationRequested)
            {
                using IMemoryOwner<byte> memoryOwner = bufferPool.Rent();
                int read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, Token);
                int packetSize = BitConverter.ToInt32(memoryOwner.Memory.Span);

                var key = new Guid(memoryOwner.Memory.Span.Slice(sizeof(int), GUID_SIZE));

                BaseWaitContext context;

                if (this.ResponseMessagesDictionary.ContainsKey(key))
                {
                    context = this.ResponseMessagesDictionary[key];
                    context.FireEvent = true;
                    context.ShouldWait = true;
                }
                else
                {
                    context = this.ResponseMessagesDictionary.GetOrAdd(key, (k, shouldWait) => new WaitContext(k, shouldWait), false);
                    context.FireEvent = true;
                    context.ShouldWait = false;
                }

                int headerSize = sizeof(int) + GUID_SIZE;
                int messageSize = packetSize - headerSize;

                if (messageSize > 0)
                {
                    context.ResponseMessage.Write(memoryOwner.Memory.Span.Slice(headerSize, messageSize));
                }

                if (read < packetSize)
                {
                    while ((read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, Token)) > 0)
                    {
                        ResponseMessagesDictionary[key].ResponseMessage.Write(memoryOwner.Memory.Span.Slice(0, read));
                    }
                }

                EndReceive(context);
            }
        }

        public override async Task<bool> AcceptConnection(ushort port)
        {
            if (Bind(port))
            {
                socket = await socket.AcceptAsync(Token);
            }

            return false;
        }

        public override async Task<bool> Disconnect()
        {
            await socket.DisconnectAsync(true, Token);
            return true;
        }
    }
}
