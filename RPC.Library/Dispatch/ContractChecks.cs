using System;
using System.Collections.Generic;
using System.Reflection;
using RPC.Library.Abstractions;

namespace RPC.Library.Dispatch
{
    internal static class ContractChecks
    {
        /// <summary>
        /// A transport without request/response (UDP) can only carry void methods, in both directions
        /// </summary>
        public static void EnsureSupported(TransportFeatures features, Type localContract, Type remoteContract)
        {
            if ((features & TransportFeatures.RequestResponse) != 0) return;

            List<string> offending = new List<string>();
            CollectMethodsNeedingResponse(localContract, offending);
            CollectMethodsNeedingResponse(remoteContract, offending);

            if (offending.Count > 0)
            {
                throw new RpcTransportException("The transport is one-way (no request/response), so only void methods can be used, but these need a response: "
                                                + string.Join(", ", offending));
            }
        }

        private static void CollectMethodsNeedingResponse(Type contract, List<string> offending)
        {
            if (contract == null) return;

            foreach (MethodInfo method in ContractReflector.GetMethods(contract))
            {
                if (ContractReflector.NeedsResponse(ContractReflector.Analyze(method, out _)))
                {
                    offending.Add($"{contract.Name}.{method.Name}");
                }
            }
        }
    }
}
