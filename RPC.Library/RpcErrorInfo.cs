using System;

namespace RPC.Library
{
    /// <summary>
    /// Payload of an error frame
    /// </summary>
    public sealed class RpcErrorInfo
    {
        public string Type { get; set; }

        public string Message { get; set; }

        public string Details { get; set; }

        public static RpcErrorInfo From(Exception exception, bool includeDetails)
        {
            return new RpcErrorInfo
            {
                Type = exception.GetType().FullName,
                Message = exception.Message,
                Details = includeDetails ? exception.ToString() : null
            };
        }

        public RpcRemoteException ToException()
        {
            return new RpcRemoteException(this.Type, this.Message, this.Details);
        }
    }
}
