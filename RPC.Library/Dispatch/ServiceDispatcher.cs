using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace RPC.Library.Dispatch
{
    /// <summary>
    /// Runs incoming calls against the local service instance
    /// </summary>
    internal sealed class ServiceDispatcher
    {
        private readonly Type contract;
        private readonly Dictionary<string, RpcMethod> methods;
        private object service;

        public ServiceDispatcher(Type contract)
        {
            this.contract = contract ?? throw new ArgumentNullException(nameof(contract));
            this.methods = new Dictionary<string, RpcMethod>(StringComparer.Ordinal);

            foreach (MethodInfo method in ContractReflector.GetMethods(contract))
            {
                this.methods.Add(method.Name, new RpcMethod(contract, method));
            }
        }

        public Type Contract => this.contract;

        public IReadOnlyCollection<RpcMethod> Methods => this.methods.Values;

        /// <summary>
        /// The instance calls run on. May stay null only for a contract without methods
        /// </summary>
        public object Service
        {
            get
            {
                return this.service;
            }
            set
            {
                if (value == null && this.methods.Count > 0)
                    throw new ArgumentNullException(nameof(value), $"A service instance is required for {this.contract.Name}");
                if (value != null && !this.contract.IsInstanceOfType(value))
                    throw new ArgumentException($"Service of type {value.GetType().Name} does not implement {this.contract.Name}", nameof(value));

                this.service = value;
            }
        }

        public RpcMethod Resolve(string name)
        {
            if (name != null && this.methods.TryGetValue(name, out RpcMethod method)) return method;

            throw new RpcException($"Method '{name}' is not part of the {this.contract.Name} contract");
        }

        public Task<object> InvokeAsync(RpcMethod method, object[] arguments)
        {
            object instance = this.service;
            if (instance == null) throw new InvalidOperationException($"No service instance is set for {this.contract.Name}");

            return method.InvokeAsync(instance, arguments);
        }
    }
}
