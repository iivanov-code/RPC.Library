using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using NetworkCommunicator.Models;
using NetworkCommunicator.Network;

namespace NetworkCommunicator.Utils
{
    internal static class DynamicProxy
    {
        public static TService Create<TService>(OperationProxyHandler handler, ConcurrentDictionary<Guid, BaseMessage> responseMessages)
            where TService : class
        {
            return DynamicProxy<TService>.Create(handler, responseMessages);
        }
    }

    internal class DynamicProxy<TProxy> : DispatchProxy
        where TProxy : class
    {
        private OperationProxyHandler handler;
        private ConcurrentDictionary<Guid, BaseMessage> responseMessages;


        public static TProxy Create(OperationProxyHandler handler, ConcurrentDictionary<Guid, BaseMessage> responseMessages)
        {
            TProxy proxy = Create<TProxy, DynamicProxy<TProxy>>();

            (proxy as DynamicProxy<TProxy>).SetParameters(handler, responseMessages);

            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.ReturnType == typeof(Task))
            {
                return handler.HandleInvocation(targetMethod, args, responseMessages);
            }
            else
            {
                return handler.HandleInvocation(targetMethod, args, responseMessages).Result;
            }
        }

        protected void SetParameters(OperationProxyHandler handler, ConcurrentDictionary<Guid, BaseMessage> responseMessages)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            this.responseMessages = responseMessages;
            this.handler = handler;
        }
    }
}
