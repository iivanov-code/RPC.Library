using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Models;

namespace RPC.Library.Listeners
{
    internal class Listener : BaseListener
    {
        public Listener(ref Socket socket, int bufferSize, CancellationToken? token = null)
            : base(ref socket, bufferSize, token)
        { }

        public override Task<bool> AcceptConnection(ushort port)
        {
            if (Bind(port))
            {
                try
                {
                    socket = socket.Accept();
                    return Task.FromResult(true);
                }
                catch (Exception)
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(false);
        }

        public override Task<int> Send(byte[] messageBuffer, BaseWaitContext context)
        {
            var segments = BuildMessage(messageBuffer, context);

            int sent = socket.Send(segments);

            return Task.FromResult(sent);
        }

        public override Task<bool> Connect(IPAddress remoteHostIP, ushort remotePort)
        {
            try
            {
                socket.Connect(remoteHostIP, remotePort);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }

        public override Task Listen()
        {
            return Task.Factory.StartNew(() =>
            {
                byte[] buffer = new byte[bufferSize];
                int read = socket.Receive(buffer, SocketFlags.None);
                int messageSize = BitConverter.ToInt32(buffer);

                var key = new Guid(new ReadOnlySpan<byte>(buffer, sizeof(int), GUID_SIZE));

                BaseWaitContext context;

                if (!ResponseMessagesDictionary.ContainsKey(key))
                {
                    context = ResponseMessagesDictionary.AddOrUpdate(key, WaitContext.WaitForResponse, (key, value) => value);
                    context.FireEvent = true;
                }
                else
                {
                    context = ResponseMessagesDictionary[key];
                }

                context.ResponseMessage.Write(new ReadOnlySpan<byte>(buffer, sizeof(int) + GUID_SIZE, read - (sizeof(int) + GUID_SIZE)));

                if (read < messageSize)
                {
                    while ((read = socket.Receive(buffer, 0, buffer.Length, SocketFlags.None)) > 0)
                    {
                        ResponseMessagesDictionary[key].ResponseMessage.Write(buffer, 0, read);
                    }
                }

                EndReceive(context);

            }, Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
