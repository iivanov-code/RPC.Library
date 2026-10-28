using System;
using RPC.Library.Abstractions;
using RPC.Library.Serialization;

namespace RPC.Library
{
    public class RpcOptions
    {
        /// <summary>
        /// How long a request may wait for its response. Zero or negative disables the timeout
        /// </summary>
        public TimeSpan CallTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public IRpcSerializer Serializer { get; set; } = JsonRpcSerializer.Default;

        /// <summary>
        /// When true the full exception text of a failed remote call is sent back to the caller
        /// </summary>
        public bool IncludeExceptionDetails { get; set; }
    }
}
