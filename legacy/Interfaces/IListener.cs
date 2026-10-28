using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetworkCommunicator.Args;
using NetworkCommunicator.Models;

namespace RPC.Library.Listeners
{
    internal interface IListener : IDisposable
    {
        ConcurrentDictionary<Guid, BaseWaitContext> ResponseMessagesDictionary { get; }
        CancellationToken Token { get; }

        event EventHandler<MessageEventArgs> MessageReceived;

        Task<bool> AcceptConnection(ushort port);

        Task<bool> Connect(IPAddress remoteHostIp, ushort remotePort);

        Task Listen();

        Task<int> Send(byte[] messageBuffer, BaseWaitContext context);

        Task<bool> Disconnect();
    }
}