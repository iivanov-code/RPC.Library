using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Args;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Models;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Network
{
    public abstract class NetworkClient : IDisposable, INetworkClient
    {
        private const int GUID_SIZE = 16;

        private readonly BufferPool<byte> bufferPool;
        private readonly int bufferSize;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IPAddress remoteHostIP;
        private readonly ushort remotePort;
        private readonly ConcurrentDictionary<Guid, BaseWaitContext> responseMessagesDictionary;
        private readonly Socket socket;
        private bool disposedValue;
        public NetworkClient(Socket socket, int bufferSize = 4096)
            : this(bufferSize)
        {
            this.socket = socket;
            var remoteEndpoint = socket.RemoteEndPoint as IPEndPoint;
            remoteHostIP = remoteEndpoint.Address;
            remotePort = (ushort)remoteEndpoint.Port;
        }

        public NetworkClient(string remoteHostIP, ushort remotePort, int bufferSize = 4096)
            : this(bufferSize)
        {
            this.remoteHostIP = IPAddress.Parse(remoteHostIP);
            this.remotePort = remotePort;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        private NetworkClient(int bufferSize)
        {
            this.bufferSize = bufferSize;
            bufferPool = new BufferPool<byte>(bufferSize);
            cancellationTokenSource = new CancellationTokenSource();
            responseMessagesDictionary = new ConcurrentDictionary<Guid, BaseWaitContext>();
        }

        private event EventHandler<MessageEventArgs> messageReceived;
        internal event EventHandler<MessageEventArgs> MessageReceived
        {
            add
            {
                messageReceived += value;
            }
            remove
            {
                messageReceived -= value;
            }
        }

        public ValueTask ConnectAsync(CancellationToken? token = null)
        {
            if (!token.HasValue)
            {
                token = cancellationTokenSource.Token;
            }

            return socket.ConnectAsync(remoteHostIP, remotePort, token.Value);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Task Listen(CancellationToken? token = null)
        {
            if (!token.HasValue)
            {
                token = cancellationTokenSource.Token;
            }

            return Task.Factory.StartNew(() =>
            {
                byte[] buffer = new byte[bufferSize];
                int read = socket.Receive(buffer, SocketFlags.None);
                int messageSize = BitConverter.ToInt32(buffer);

                var key = new Guid(new ReadOnlySpan<byte>(buffer, sizeof(int), GUID_SIZE));

                BaseWaitContext context;

                if (!responseMessagesDictionary.ContainsKey(key))
                {
                    context = responseMessagesDictionary.AddOrUpdate(key, WaitContext.WaitForResponse, (key, value) => value) as BaseWaitContext;
                    context.FireEvent = true;
                }
                else
                {
                    context = responseMessagesDictionary[key];
                }

                context.ResponseMessage.Write(new ReadOnlySpan<byte>(buffer, sizeof(int) + GUID_SIZE, read - (sizeof(int) + GUID_SIZE)));

                if (read < messageSize)
                {
                    while ((read = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None)) > 0)
                    {
                        responseMessagesDictionary[key].ResponseMessage.Write(buffer, 0, read);
                    }
                }

                EndReceive(context);

            }, token.Value, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public Task ListenAsync(CancellationToken? token = null)
        {
            if (!token.HasValue)
            {
                token = cancellationTokenSource.Token;
            }

            return Task.Factory.StartNew(async () =>
             {
                 using IMemoryOwner<byte> memoryOwner = bufferPool.Rent();
                 int read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, token.Value);
                 int messageSize = BitConverter.ToInt32(memoryOwner.Memory.Span);

                 var key = new Guid(memoryOwner.Memory.Span.Slice(sizeof(int), GUID_SIZE));

                 BaseWaitContext context;

                 if (!responseMessagesDictionary.ContainsKey(key))
                 {
                     context = WaitContext.WaitForResponse;
                     _ = responseMessagesDictionary.TryAdd(key, context);
                     context.FireEvent = true;
                 }
                 else
                 {
                     context = responseMessagesDictionary[key];
                 }

                 context.ResponseMessage.Write(memoryOwner.Memory.Span.Slice(sizeof(int) + GUID_SIZE, read - (sizeof(int) + GUID_SIZE)));

                 if (read < messageSize)
                 {
                     while ((read = await socket.ReceiveAsync(memoryOwner.Memory, SocketFlags.None, token.Value)) > 0)
                     {
                         responseMessagesDictionary[key].ResponseMessage.Write(memoryOwner.Memory.Span.Slice(0, read));
                     }
                 }

                 EndReceive(context);

             }, token.Value, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void ListenEvent()
        {
            byte[] buffer = new byte[bufferSize];
            IAsyncResult result = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Receive), buffer);
        }

        public int Send(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            return socket.Send(segments);
        }

        public Task<int> SendAsync(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            return socket.SendAsync(segments, SocketFlags.None);
        }

        public IAsyncResult SendAsyncEvent(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            return socket.BeginSend(segments, SocketFlags.None, new AsyncCallback(AsyncSenderCallback), null);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    socket?.Dispose();
                    responseMessagesDictionary.Clear();
                    bufferPool.Dispose();
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        private void AsyncSenderCallback(IAsyncResult result)
        {
            int sentBytes = socket.EndSend(result);
        }

        private IList<ArraySegment<byte>> BuildMessage(byte[] messageBuffer, BaseWaitContext context)
        {
            byte[] keyBuffer = context.Guid.ToByteArray();

            int packetSize = messageBuffer.Length + keyBuffer.Length + sizeof(int);

            List<ArraySegment<byte>> segments = new List<ArraySegment<byte>>
            {
                BitConverter.GetBytes(packetSize),
                keyBuffer,
                messageBuffer
            };

            if (context.ShouldWait)
            {
                _ = responseMessagesDictionary.TryAdd(context.Guid, context);
            }

            return segments;
        }

        private void EndReceive(BaseWaitContext context)
        {
            if (context.FireEvent)
            {
                messageReceived?.Invoke(this, new MessageEventArgs(context.Guid, context.ResponseMessage.ToArray(), context.ShouldWait, context.WaitHandle));
            }
        }

        private void PostRead(EventWaitContext context)
        {
            if (context.Read < context.MessageSize)
            {
                _ = socket.BeginReceive(context.Buffer, 0, context.Buffer.Length, SocketFlags.None, new AsyncCallback(Receive), context);
            }
            else
            {
                EndReceive(context);
                ListenEvent();
            }
        }

        private void Receive(IAsyncResult result)
        {
            if (result.AsyncState is not BaseWaitContext)
            {
                byte[] buffer = result.AsyncState as byte[];

                int read = socket.EndReceive(result, out SocketError errorCode);
                int packetSize = BitConverter.ToInt32(buffer);
                Guid key = new Guid(new ReadOnlySpan<byte>(buffer, sizeof(int), GUID_SIZE));

                BaseWaitContext context;

                if (this.responseMessagesDictionary.ContainsKey(key))
                {
                    context = this.responseMessagesDictionary[key];
                    context.FireEvent = true;
                    context = this.responseMessagesDictionary.AddOrUpdate(key, context, (key, value) => new EventWaitContext(value));
                }
                else
                {
                    context = this.responseMessagesDictionary.GetOrAdd(key, (k, shouldWait) => new EventWaitContext(k, shouldWait), false);
                }

                int headerSize = sizeof(int) + GUID_SIZE;
                int messageSize = packetSize - headerSize;

                if (messageSize > 0)
                {
                    context.ResponseMessage.Write(buffer, headerSize, messageSize);
                }

                PostRead(context as EventWaitContext);
            }
            else
            {
                EventWaitContext context = result.AsyncState as EventWaitContext;
                int read = socket.EndReceive(result, out SocketError errorCode);
                context.ResponseMessage.Write(context.Buffer, 0, read);
                context.Read += read;
                PostRead(context);
            }
        }
    }
}
