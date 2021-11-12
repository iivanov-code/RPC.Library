using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using NetworkCommunicator.Args;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Models;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Network
{
    public class ServiceNetworkClient<TCallbackService, TRemoteService> : NetworkClient, IServiceNetworkClient<TCallbackService, TRemoteService>
        where TCallbackService : class
        where TRemoteService : class
    {
        private ConcurrentDictionary<Guid, BaseMessage> responseMessages;
        internal ServiceNetworkClient(Socket socket, TCallbackService service, int bufferSize = 4096)
            : base(socket, bufferSize)
        {
            this.Service = service;
            this.responseMessages = new ConcurrentDictionary<Guid, BaseMessage>();
            MessageReceived += ServiceNetworkClient_MessageReceived;
            RemoteService = DynamicProxy.Create<TRemoteService>(new OperationProxyHandler(this), responseMessages);
        }

        public ServiceNetworkClient(string remoteHostIP, ushort remotePort, TCallbackService service, int bufferSize = 4096)
            : base(remoteHostIP, remotePort, bufferSize)
        {
            this.Service = service;
            this.responseMessages = new ConcurrentDictionary<Guid, BaseMessage>();
            MessageReceived += ServiceNetworkClient_MessageReceived;
            RemoteService = DynamicProxy.Create<TRemoteService>(new OperationProxyHandler(this), responseMessages);
        }

        private void ServiceNetworkClient_MessageReceived(object sender, MessageEventArgs e)
        {
            BaseMessage message = MessagePipeline.Unwrap(e.MessageBuffer);

            if (e.IsResponse)
            {
                responseMessages[e.Guid] = message;
                _ = e.WaitHandle.Set();
            }
            else
            {
                ExecuteEndpointMethod(e.Guid, message);
            }
        }

        public TRemoteService RemoteService { get; private set; }

        public TCallbackService Service { get; private set; }


        private void ExecuteEndpointMethod(Guid key, BaseMessage message)
        {
            MethodInfo method = typeof(TCallbackService).GetMethod(message.MethodName, BindingFlags.Public | BindingFlags.Instance);
            ParameterInfo[] parameterInfos = method.GetParameters();

            object[] parameters = null;

            if (parameterInfos.Length == 1)
            {
                parameters = new object[] { Deserialize(parameterInfos[0].ParameterType, message.Data) };
            }
            else if (parameterInfos.Length > 1)
            {
                throw new NotSupportedException("");
            }

            if (method.ReturnType != null)
            {
                object result = method.Invoke(this.Service, parameters);
                SendResult(result, message.MethodName, key);
            }
            else if (method.ReturnType == typeof(Task))
            {
                (method.Invoke(this.Service, parameters) as Task).Wait();
            }
            else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                object result = (method.Invoke(this.Service, parameters) as Task<object>).Result;
                SendResult(result, message.MethodName, key);
            }
        }

        private void SendResult(object result, string methodName, Guid key)
        {
            string json = Serialize(result);
            byte[] messageBuffer = MessagePipeline.Wrap(json, methodName);
            _ = this.Send(messageBuffer, new WaitContext(key, false));
        }

        private string Serialize(object data)
        {
            return typeof(NetworkUtils)
                      .GetMethod(nameof(NetworkUtils.Serialize), BindingFlags.Static | BindingFlags.Public)
                      .MakeGenericMethod(data.GetType())
                      .Invoke(null, new object[] { data }) as string;
        }

        private object Deserialize(Type t, string json)
        {
            return typeof(NetworkUtils)
                      .GetMethod(nameof(NetworkUtils.Deserialize), BindingFlags.Static | BindingFlags.Public)
                      .MakeGenericMethod(t)
                      .Invoke(null, new object[] { json });
        }
    }
}
