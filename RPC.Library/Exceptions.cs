using System;

namespace RPC.Library
{
    public class RpcException : Exception
    {
        public RpcException(string message)
            : base(message)
        { }

        public RpcException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }

    /// <summary>
    /// The remote method threw; carries the remote exception type and message
    /// </summary>
    public sealed class RpcRemoteException : RpcException
    {
        public RpcRemoteException(string remoteType, string message, string remoteDetails)
            : base(message)
        {
            this.RemoteType = remoteType;
            this.RemoteDetails = remoteDetails;
        }

        public string RemoteType { get; }

        /// <summary>
        /// Full remote exception text, only when the remote side enabled <see cref="RpcOptions.IncludeExceptionDetails"/>
        /// </summary>
        public string RemoteDetails { get; }

        public override string ToString()
        {
            return this.RemoteDetails ?? $"{this.RemoteType}: {this.Message}{Environment.NewLine}{base.ToString()}";
        }
    }

    public sealed class RpcTimeoutException : RpcException
    {
        public RpcTimeoutException(string method, TimeSpan timeout)
            : base($"Call to '{method}' did not complete within {timeout}")
        {
            this.Method = method;
            this.Timeout = timeout;
        }

        public string Method { get; }

        public TimeSpan Timeout { get; }
    }

    /// <summary>
    /// The connection is closed or failed
    /// </summary>
    public sealed class RpcConnectionException : RpcException
    {
        public RpcConnectionException(string message)
            : base(message)
        { }

        public RpcConnectionException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }

    /// <summary>
    /// The transport cannot do what was asked, for example a request over a one-way transport
    /// </summary>
    public sealed class RpcTransportException : RpcException
    {
        public RpcTransportException(string message)
            : base(message)
        { }
    }

    public sealed class RpcMessageTooLargeException : RpcException
    {
        public RpcMessageTooLargeException(int size, int maxSize)
            : base($"Message of {size} bytes exceeds the transport limit of {maxSize} bytes")
        {
            this.Size = size;
            this.MaxSize = maxSize;
        }

        public int Size { get; }

        public int MaxSize { get; }
    }

    /// <summary>
    /// The remote side sent bytes that are not a valid frame
    /// </summary>
    public sealed class RpcProtocolException : RpcException
    {
        public RpcProtocolException(string message)
            : base(message)
        { }
    }
}
