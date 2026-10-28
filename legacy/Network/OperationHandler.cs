using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Models;
using NetworkCommunicator.Utils;

namespace NetworkCommunicator.Network
{
    internal class OperationProxyHandler
    {
        private INetworkClient client;

        public OperationProxyHandler(INetworkClient client)
        {
            this.client = client;
        }

        public async Task<object> HandleInvocation(MethodInfo targetMethod, object[] args, ConcurrentDictionary<Guid, BaseMessage> responseMessages)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            string message = null;

            if (parameters.Length == 1)
            {
                message = Serialize(parameters[0].ParameterType, args[0]);
            }

            byte[] buffer = MessagePipeline.Wrap(message, targetMethod.Name);

            if (targetMethod.ReturnType != null)
            {
                BaseWaitContext context = WaitContext.WaitForResponse;
                _ = responseMessages.TryAdd(context.Guid, null);
                int sent = await client.Send(buffer, context);
                bool result = context.WaitHandle.WaitOne();

                return Deserialize(targetMethod.ReturnType, responseMessages[context.Guid].Data);
            }
            else
            {
                BaseWaitContext context = WaitContext.NoResponse;
                int sent = await client.Send(buffer, context);
                return null;
            }
        }

        private object Deserialize(Type t, string json)
        {
            return typeof(NetworkUtils)
                  .GetMethod(nameof(NetworkUtils.Deserialize), BindingFlags.Public | BindingFlags.Static)
                  .MakeGenericMethod(t)
                  .Invoke(null, new object[] { json });
        }

        private string Serialize(Type t, object obj)
        {
            return typeof(NetworkUtils)
                   .GetMethod(nameof(NetworkUtils.Serialize), BindingFlags.Public | BindingFlags.Static)
                   .MakeGenericMethod(t)
                   .Invoke(null, new object[] { obj }) as string;
        }
    }
}