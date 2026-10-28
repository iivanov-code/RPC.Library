using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace RPC.Library.Dispatch
{
    /// <summary>
    /// One callable method of a local contract with a compiled invoker, built once per contract
    /// </summary>
    internal sealed class RpcMethod
    {
        private readonly Func<object, object[], object> invoker;
        private readonly Func<Task, object> resultGetter;

        public RpcMethod(Type contract, MethodInfo method)
        {
            this.Name = method.Name;
            this.ParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            this.Kind = ContractReflector.Analyze(method, out Type resultType);
            this.ReturnType = resultType;
            this.invoker = BuildInvoker(contract, method, this.Kind);
            this.resultGetter = this.Kind == RpcMethodKind.TaskOfResult || this.Kind == RpcMethodKind.ValueTaskOfResult
                ? BuildResultGetter(resultType)
                : null;
        }

        public string Name { get; }

        public Type[] ParameterTypes { get; }

        /// <summary>
        /// Type of the value sent back; typeof(void) when nothing is
        /// </summary>
        public Type ReturnType { get; }

        public RpcMethodKind Kind { get; }

        public async Task<object> InvokeAsync(object service, object[] arguments)
        {
            object raw = this.invoker(service, arguments);

            switch (this.Kind)
            {
                case RpcMethodKind.Task:
                case RpcMethodKind.ValueTask:
                    await ((Task)raw).ConfigureAwait(false);
                    return null;

                case RpcMethodKind.TaskOfResult:
                case RpcMethodKind.ValueTaskOfResult:
                    Task task = (Task)raw;
                    await task.ConfigureAwait(false);
                    return this.resultGetter(task);

                default:
                    return raw;
            }
        }

        /// <summary>
        /// (object service, object[] args) => (object)((TContract)service).Method((T0)args[0], ...)
        /// ValueTask results are converted with AsTask so the caller only deals with Task
        /// </summary>
        private static Func<object, object[], object> BuildInvoker(Type contract, MethodInfo method, RpcMethodKind kind)
        {
            ParameterExpression service = Expression.Parameter(typeof(object), "service");
            ParameterExpression arguments = Expression.Parameter(typeof(object[]), "arguments");

            ParameterInfo[] parameters = method.GetParameters();
            Expression[] typedArguments = new Expression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                typedArguments[i] = Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(i)), parameters[i].ParameterType);
            }

            Expression call = Expression.Call(Expression.Convert(service, contract), method, typedArguments);

            Expression body;
            switch (kind)
            {
                case RpcMethodKind.OneWay:
                    body = Expression.Block(call, Expression.Constant(null, typeof(object)));
                    break;

                case RpcMethodKind.ValueTask:
                case RpcMethodKind.ValueTaskOfResult:
                    body = Expression.Convert(Expression.Call(call, method.ReturnType.GetMethod("AsTask", Type.EmptyTypes)), typeof(object));
                    break;

                default:
                    body = Expression.Convert(call, typeof(object));
                    break;
            }

            return Expression.Lambda<Func<object, object[], object>>(body, service, arguments).Compile();
        }

        /// <summary>
        /// (Task task) => (object)((Task&lt;T&gt;)task).Result
        /// </summary>
        private static Func<Task, object> BuildResultGetter(Type resultType)
        {
            ParameterExpression task = Expression.Parameter(typeof(Task), "task");
            Type taskType = typeof(Task<>).MakeGenericType(resultType);
            Expression result = Expression.Property(Expression.Convert(task, taskType), "Result");

            return Expression.Lambda<Func<Task, object>>(Expression.Convert(result, typeof(object)), task).Compile();
        }
    }
}
