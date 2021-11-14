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
                while (!Token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[bufferSize];
                    int read = 0;
                    try
                    {
                        read = socket.Receive(buffer, SocketFlags.None);
                    }
                    catch (SocketException ex)
                    {
                        return;
                    }

                    int packetSize = BitConverter.ToInt32(buffer);

                    var key = new Guid(new Span<byte>(buffer, sizeof(int), GUID_SIZE));

                    BaseWaitContext context;

                    if (this.ResponseMessagesDictionary.ContainsKey(key))
                    {
                        context = this.ResponseMessagesDictionary[key];
                        context.ShouldWait = true;
                        context.FireEvent = true;
                    }
                    else
                    {
                        context = this.ResponseMessagesDictionary.GetOrAdd(key, (k, shouldWait) => new WaitContext(k, shouldWait), true);
                        context.FireEvent = true;
                        context.ShouldWait = false;
                    }

                    int headerSize = sizeof(int) + GUID_SIZE;
                    int messageSize = packetSize - headerSize;

                    if (messageSize > 0)
                    {
                        context.ResponseMessage.Write(buffer, headerSize, messageSize);
                    }

                    if (read < packetSize)
                    {
                        while ((read = socket.Receive(buffer, SocketFlags.None)) > 0)
                        {
                            ResponseMessagesDictionary[key].ResponseMessage.Write(buffer, 0, read);
                        }
                    }

                    EndReceive(context);
                }
            }, Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public override Task<bool> Disconnect()
        {
            try
            {
                socket.Disconnect(true);
                return Task.FromResult(true);
            }
            catch (Exception)
            {
                return Task.FromResult(false);
            }
        }
    }
}
