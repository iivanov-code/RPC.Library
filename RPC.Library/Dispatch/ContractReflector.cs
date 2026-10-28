using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RPC.Library.Dispatch
{
    /// <summary>
    /// Shared rules for reading a contract: which methods take part and how each one is called
    /// </summary>
    internal static class ContractReflector
    {
        public static IReadOnlyList<MethodInfo> GetMethods(Type contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));

            IEnumerable<MethodInfo> methods;
            if (contract.IsInterface)
            {
                methods = contract.GetMethods().Concat(contract.GetInterfaces().SelectMany(i => i.GetMethods()));
            }
            else
            {
                methods = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            }

            List<MethodInfo> result = methods.Where(m => !m.IsSpecialName).ToList();

            foreach (IGrouping<string, MethodInfo> group in result.GroupBy(m => m.Name))
            {
                if (group.Count() > 1)
                    throw new NotSupportedException($"{contract.Name}.{group.Key} is overloaded; method names in a contract must be unique");
            }

            foreach (MethodInfo method in result)
            {
                if (method.IsGenericMethodDefinition)
                    throw new NotSupportedException($"{contract.Name}.{method.Name} is generic, which is not supported");

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    if (parameter.ParameterType.IsByRef)
                        throw new NotSupportedException($"{contract.Name}.{method.Name} has a ref or out parameter, which is not supported");
                }
            }

            return result;
        }

        /// <summary>
        /// Classifies the method and unwraps its result type (typeof(void) when nothing comes back)
        /// </summary>
        public static RpcMethodKind Analyze(MethodInfo method, out Type resultType)
        {
            Type returnType = method.ReturnType;

            if (returnType == typeof(void))
            {
                resultType = typeof(void);
                return RpcMethodKind.OneWay;
            }

            if (returnType == typeof(Task))
            {
                resultType = typeof(void);
                return RpcMethodKind.Task;
            }

            if (returnType == typeof(ValueTask))
            {
                resultType = typeof(void);
                return RpcMethodKind.ValueTask;
            }

            if (returnType.IsGenericType)
            {
                Type definition = returnType.GetGenericTypeDefinition();
                if (definition == typeof(Task<>))
                {
                    resultType = returnType.GetGenericArguments()[0];
                    return RpcMethodKind.TaskOfResult;
                }

                if (definition == typeof(ValueTask<>))
                {
                    resultType = returnType.GetGenericArguments()[0];
                    return RpcMethodKind.ValueTaskOfResult;
                }
            }

            resultType = returnType;
            return RpcMethodKind.Synchronous;
        }

        public static bool NeedsResponse(RpcMethodKind kind)
        {
            return kind != RpcMethodKind.OneWay;
        }
    }
}
