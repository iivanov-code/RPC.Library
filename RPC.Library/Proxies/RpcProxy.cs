using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Channels;
using RPC.Library.Dispatch;

namespace RPC.Library.Proxies
{
    /// <summary>
    /// Turns calls on a contract interface into frames on a channel.
    /// Public only because <see cref="DispatchProxy"/> requires it; use <see cref="RpcPeer{TLocalContract, TRemoteContract}.Remote"/>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RpcProxy<TContract> : DispatchProxy
        where TContract : class
    {
        private static readonly MethodInfo CAST_TASK = typeof(RpcProxy<TContract>).GetMethod(nameof(CastTaskAsync), BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly Dictionary<MethodInfo, ProxyMethod> METHODS = BuildMethods();

        private RpcChannel channel;

        internal static TContract Create(RpcChannel channel)
        {
            if (!typeof(TContract).IsInterface)
                throw new NotSupportedException($"{typeof(TContract).Name} must be an interface to be called remotely");

            TContract proxy = Create<TContract, RpcProxy<TContract>>();
            ((RpcProxy<TContract>)(object)proxy).channel = channel ?? throw new ArgumentNullException(nameof(channel));
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!METHODS.TryGetValue(targetMethod, out ProxyMethod method))
                throw new NotSupportedException($"{targetMethod.Name} is not a remotely callable method of {typeof(TContract).Name}");

            switch (method.Kind)
            {
                case RpcMethodKind.OneWay:
                    this.channel.NotifyAsync(method.Name, args, method.ParameterTypes, CancellationToken.None).GetAwaiter().GetResult();
                    return null;

                case RpcMethodKind.Synchronous:
                    return this.channel.InvokeAsync(method.Name, args, method.ParameterTypes, method.ReturnType, CancellationToken.None).GetAwaiter().GetResult();

                case RpcMethodKind.Task:
                    return this.channel.InvokeAsync(method.Name, args, method.ParameterTypes, typeof(void), CancellationToken.None);

                default:
                    return method.Convert(this.channel.InvokeAsync(method.Name, args, method.ParameterTypes, method.ReturnType, CancellationToken.None));
            }
        }

        private static async Task<TResult> CastTaskAsync<TResult>(Task<object> task)
        {
            return (TResult)await task.ConfigureAwait(false);
        }

        private static Dictionary<MethodInfo, ProxyMethod> BuildMethods()
        {
            Dictionary<MethodInfo, ProxyMethod> methods = new Dictionary<MethodInfo, ProxyMethod>();
            if (!typeof(TContract).IsInterface) return methods;

            foreach (MethodInfo method in ContractReflector.GetMethods(typeof(TContract)))
            {
                methods[method] = new ProxyMethod(method);
            }

            return methods;
        }

        private sealed class ProxyMethod
        {
            public ProxyMethod(MethodInfo method)
            {
                this.Name = method.Name;
                this.ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                this.Kind = ContractReflector.Analyze(method, out Type returnType);
                this.ReturnType = returnType;
                this.Convert = BuildConverter(this.Kind, returnType);
            }

            public string Name { get; }

            public Type[] ParameterTypes { get; }

            public Type ReturnType { get; }

            public RpcMethodKind Kind { get; }

            /// <summary>
            /// Turns the channel's Task&lt;object&gt; into the exact return type the interface declares
            /// </summary>
            public Func<Task<object>, object> Convert { get; }

            private static Func<Task<object>, object> BuildConverter(RpcMethodKind kind, Type returnType)
            {
                ParameterExpression task = Expression.Parameter(typeof(Task<object>), "task");
                Expression body;

                switch (kind)
                {
                    case RpcMethodKind.TaskOfResult:
                        body = Expression.Call(CAST_TASK.MakeGenericMethod(returnType), task);
                        break;

                    case RpcMethodKind.ValueTask:
                        body = Expression.New(typeof(ValueTask).GetConstructor(new[] { typeof(Task) }), Expression.Convert(task, typeof(Task)));
                        break;

                    case RpcMethodKind.ValueTaskOfResult:
                        Type valueTaskType = typeof(ValueTask<>).MakeGenericType(returnType);
                        Type taskType = typeof(Task<>).MakeGenericType(returnType);
                        body = Expression.New(valueTaskType.GetConstructor(new[] { taskType }), Expression.Call(CAST_TASK.MakeGenericMethod(returnType), task));
                        break;

                    default:
                        return null;
                }

                return Expression.Lambda<Func<Task<object>, object>>(Expression.Convert(body, typeof(object)), task).Compile();
            }
        }
    }
}
