using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Models;

namespace RPC.Library.Listeners
{
    internal class EventListener : BaseListener
    {
        public EventListener(ref Socket socket, int bufferSize, CancellationToken? token = null)
            : base(ref socket, bufferSize, token)
        { }

        private int sentBytes;

        public override Task<int> Send(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            IAsyncResult result = socket.BeginSend(segments, SocketFlags.None, new AsyncCallback(SentCallback), null);

            if (result.IsCompleted || result.AsyncWaitHandle.WaitOne())
            {
                return Task.FromResult(sentBytes);
            }
            else
            {
                return Task.FromResult(0);
            }
        }

        private void SentCallback(IAsyncResult result)
        {
            sentBytes = socket.EndSend(result);
            result.AsyncWaitHandle.Close();
        }

        public override Task Listen()
        {
            byte[] buffer = new byte[bufferSize];
            IAsyncResult result = socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(Receive), buffer);
            return Task.CompletedTask;
        }

        public override Task<bool> Connect(IPAddress remoteHostIp, ushort remotePort)
        {
            IAsyncResult result = socket.BeginConnect(remoteHostIp, remotePort, new AsyncCallback(ConnectedCallback), null);
            bool wait = result.IsCompleted || result.AsyncWaitHandle.WaitOne();

            return Task.FromResult(wait);
        }

        private void ConnectedCallback(IAsyncResult result)
        {
            socket.EndConnect(result);
            result.AsyncWaitHandle.Close();
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
                Listen();
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

                if (this.ResponseMessagesDictionary.ContainsKey(key))
                {
                    context = this.ResponseMessagesDictionary[key];
                    context.FireEvent = true;
                    context = this.ResponseMessagesDictionary.AddOrUpdate(key, context, (key, value) => new EventWaitContext(value));
                }
                else
                {
                    context = this.ResponseMessagesDictionary.GetOrAdd(key, (k, shouldWait) => new EventWaitContext(k, shouldWait), false);
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

        public override Task<bool> AcceptConnection(ushort port)
        {
            if (Bind(port))
            {
                IAsyncResult result = socket.BeginAccept(new AsyncCallback(AcceptedCallback), null);
                bool wait = result.IsCompleted || result.AsyncWaitHandle.WaitOne();
                return Task.FromResult(wait);
            }

            return Task.FromResult(false);
        }

        private void AcceptedCallback(IAsyncResult result)
        {
            socket = socket.EndAccept(result);
            result.AsyncWaitHandle.Close();
        }
    }
}
