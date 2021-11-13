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

        public override Task Listen()
        {
            return Task.Factory.StartNew(async () =>
            {
                using IMemoryOwner<byte> memoryOwner = bufferPool.Rent();
                int read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, Token);
                int messageSize = BitConverter.ToInt32(memoryOwner.Memory.Span);

                var key = new Guid(memoryOwner.Memory.Span.Slice(sizeof(int), GUID_SIZE));

                BaseWaitContext context;

                if (!ResponseMessagesDictionary.ContainsKey(key))
                {
                    context = WaitContext.WaitForResponse;
                    _ = ResponseMessagesDictionary.TryAdd(key, context);
                    context.FireEvent = true;
                }
                else
                {
                    context = ResponseMessagesDictionary[key];
                }

                context.ResponseMessage.Write(memoryOwner.Memory.Span.Slice(sizeof(int) + GUID_SIZE, read - (sizeof(int) + GUID_SIZE)));

                if (read < messageSize)
                {
                    while ((read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, Token)) > 0)
                    {
                        ResponseMessagesDictionary[key].ResponseMessage.Write(memoryOwner.Memory.Span.Slice(0, read));
                    }
                }

                EndReceive(context);

            }, Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public override async Task<bool> AcceptConnection(ushort port)
        {
            if (Bind(port))
            {
                socket = await socket.AcceptAsync(Token);
            }

            return false;
        }
    }
}
