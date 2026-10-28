using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;
using RPC.Library.Dispatch;
using RPC.Library.Protocol;

namespace RPC.Library.Channels
{
    /// <summary>
    /// Runs the protocol over one connection: correlates requests with responses,
    /// applies timeouts, propagates remote errors and dispatches incoming calls
    /// </summary>
    internal sealed class RpcChannel : IDisposable
    {
        private readonly IRpcConnection connection;
        private readonly IRpcSerializer serializer;
        private readonly RpcOptions options;
        private readonly ServiceDispatcher dispatcher;
        private readonly ConcurrentDictionary<int, PendingCall> pendingCalls;
        private readonly CancellationTokenSource closeSource;

        private int nextCorrelationId;
        private int closed;
        private Task receiveLoop;

        public RpcChannel(IRpcConnection connection, RpcOptions options, ServiceDispatcher dispatcher)
        {
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.serializer = options.Serializer ?? throw new ArgumentException("Serializer must be set", nameof(options));
            this.pendingCalls = new ConcurrentDictionary<int, PendingCall>();
            this.closeSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Raised once, when the connection closes. The argument is the failure, or null for a clean close
        /// </summary>
        public event EventHandler<Exception> Closed;

        /// <summary>
        /// Raised for failures that have no caller to report to: a failed one-way call,
        /// a request over a one-way transport, a bad datagram
        /// </summary>
        public event EventHandler<Exception> UnhandledError;

        public bool IsOpen => this.closed == 0;

        public TransportFeatures Features => this.connection.Features;

        public bool SupportsRequests => (this.connection.Features & TransportFeatures.RequestResponse) != 0;

        public void Start()
        {
            if (this.receiveLoop != null) throw new InvalidOperationException("Channel already started");

            this.receiveLoop = Task.Run(this.ReceiveLoopAsync);
        }

        public async Task<object> InvokeAsync(string method, object[] arguments, Type[] argumentTypes, Type returnType, CancellationToken cancellationToken)
        {
            this.ThrowIfClosed();
            if (!this.SupportsRequests)
                throw new RpcTransportException($"Cannot call '{method}': the transport is one-way and cannot carry a response. Only void methods can be used over it");

            int correlationId = Interlocked.Increment(ref this.nextCorrelationId);
            PendingCall pending = new PendingCall(method, returnType);
            this.pendingCalls[correlationId] = pending;

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.closeSource.Token))
            {
                if (this.options.CallTimeout > TimeSpan.Zero)
                {
                    timeout.CancelAfter(this.options.CallTimeout);
                }

                using (timeout.Token.Register(() => this.AbandonCall(correlationId, cancellationToken)))
                {
                    try
                    {
                        await this.SendFrameAsync(FrameType.Request,
                                                  correlationId,
                                                  method,
                                                  writer => this.serializer.SerializeArguments(writer, arguments, argumentTypes),
                                                  timeout.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        this.pendingCalls.TryRemove(correlationId, out _);
                        throw;
                    }

                    return await pending.Completion.Task.ConfigureAwait(false);
                }
            }
        }

        public Task NotifyAsync(string method, object[] arguments, Type[] argumentTypes, CancellationToken cancellationToken)
        {
            this.ThrowIfClosed();

            return this.SendFrameAsync(FrameType.OneWay,
                                       0,
                                       method,
                                       writer => this.serializer.SerializeArguments(writer, arguments, argumentTypes),
                                       cancellationToken);
        }

        public void Dispose()
        {
            this.Close(null);
        }

        private void AbandonCall(int correlationId, CancellationToken callerToken)
        {
            if (!this.pendingCalls.TryRemove(correlationId, out PendingCall pending)) return;

            if (callerToken.IsCancellationRequested)
            {
                pending.Completion.TrySetCanceled(callerToken);
            }
            else if (this.closeSource.IsCancellationRequested)
            {
                pending.Completion.TrySetException(new RpcConnectionException("Connection closed while waiting for the response"));
            }
            else
            {
                pending.Completion.TrySetException(new RpcTimeoutException(pending.Method, this.options.CallTimeout));
            }
        }

        private async Task SendFrameAsync(FrameType type, int correlationId, string method, Action<IBufferWriter<byte>> writePayload, CancellationToken cancellationToken)
        {
            using (PooledBufferWriter writer = new PooledBufferWriter())
            {
                FrameHeader.Write(writer, type, correlationId, method);
                writePayload?.Invoke(writer);

                if (writer.WrittenCount > this.connection.MaxMessageSize)
                    throw new RpcMessageTooLargeException(writer.WrittenCount, this.connection.MaxMessageSize);

                try
                {
                    await this.connection.SendAsync(writer.WrittenMemory, cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException ex)
                {
                    throw new RpcConnectionException("Connection is closed", ex);
                }
            }
        }

        private async Task ReceiveLoopAsync()
        {
            Exception failure = null;

            try
            {
                while (true)
                {
                    ReceivedMessage message = await this.connection.ReceiveAsync(this.closeSource.Token).ConfigureAwait(false);
                    if (message == null) break;

                    this.ProcessMessage(message);
                }
            }
            catch (OperationCanceledException) when (this.closeSource.IsCancellationRequested)
            { }
            catch (ObjectDisposedException) when (this.closeSource.IsCancellationRequested)
            { }
            catch (Exception ex)
            {
                failure = ex;
            }

            this.Close(failure);
        }

        private void ProcessMessage(ReceivedMessage message)
        {
            if (!FrameHeader.TryParse(message.Data, out FrameType type, out int correlationId, out string method, out ReadOnlyMemory<byte> payload))
            {
                message.Dispose();

                RpcProtocolException error = new RpcProtocolException($"Received {message.Length} bytes that are not a valid frame");
                if ((this.connection.Features & TransportFeatures.Reliable) != 0)
                {
                    // a corrupt stream cannot be resynchronised
                    throw error;
                }

                // a bad datagram is dropped
                this.UnhandledError?.Invoke(this, error);
                return;
            }

            switch (type)
            {
                case FrameType.Response:
                    this.CompleteCall(correlationId, payload, message);
                    break;

                case FrameType.Error:
                    this.FailCall(correlationId, payload, message);
                    break;

                case FrameType.Request:
                    if (!this.SupportsRequests)
                    {
                        message.Dispose();
                        this.UnhandledError?.Invoke(this, new RpcTransportException($"Dropped a request for '{method}': the transport is one-way and cannot carry a response"));
                        break;
                    }

                    _ = Task.Run(() => this.HandleCallAsync(type, correlationId, method, payload, message));
                    break;

                case FrameType.OneWay:
                    _ = Task.Run(() => this.HandleCallAsync(type, correlationId, method, payload, message));
                    break;
            }
        }

        private void CompleteCall(int correlationId, ReadOnlyMemory<byte> payload, ReceivedMessage message)
        {
            using (message)
            {
                if (!this.pendingCalls.TryRemove(correlationId, out PendingCall pending)) return;

                try
                {
                    object result = pending.ReturnType == typeof(void) || payload.IsEmpty
                        ? null
                        : this.serializer.Deserialize(payload, pending.ReturnType);

                    pending.Completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    pending.Completion.TrySetException(new RpcException($"Could not read the response of '{pending.Method}'", ex));
                }
            }
        }

        private void FailCall(int correlationId, ReadOnlyMemory<byte> payload, ReceivedMessage message)
        {
            using (message)
            {
                if (!this.pendingCalls.TryRemove(correlationId, out PendingCall pending)) return;

                Exception error;
                try
                {
                    RpcErrorInfo info = (RpcErrorInfo)this.serializer.Deserialize(payload, typeof(RpcErrorInfo));
                    error = info.ToException();
                }
                catch (Exception ex)
                {
                    error = new RpcException($"Call to '{pending.Method}' failed and the error could not be read", ex);
                }

                pending.Completion.TrySetException(error);
            }
        }

        private async Task HandleCallAsync(FrameType type, int correlationId, string method, ReadOnlyMemory<byte> payload, ReceivedMessage message)
        {
            RpcMethod target = null;

            try
            {
                object[] arguments;
                try
                {
                    target = this.dispatcher.Resolve(method);
                    arguments = this.serializer.DeserializeArguments(payload, target.ParameterTypes);
                }
                finally
                {
                    message.Dispose();
                }

                object result = await this.dispatcher.InvokeAsync(target, arguments).ConfigureAwait(false);

                if (type == FrameType.Request)
                {
                    Type resultType = target.ReturnType;
                    await this.SendFrameAsync(FrameType.Response,
                                              correlationId,
                                              null,
                                              writer =>
                                              {
                                                  if (resultType != typeof(void))
                                                  {
                                                      this.serializer.Serialize(writer, result, resultType);
                                                  }
                                              },
                                              this.closeSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (type == FrameType.Request && this.IsOpen)
                {
                    await this.TrySendErrorAsync(correlationId, ex).ConfigureAwait(false);
                }
                else
                {
                    this.UnhandledError?.Invoke(this, ex);
                }
            }
        }

        private async Task TrySendErrorAsync(int correlationId, Exception error)
        {
            try
            {
                RpcErrorInfo info = RpcErrorInfo.From(error, this.options.IncludeExceptionDetails);
                await this.SendFrameAsync(FrameType.Error,
                                          correlationId,
                                          null,
                                          writer => this.serializer.Serialize(writer, info, typeof(RpcErrorInfo)),
                                          this.closeSource.Token).ConfigureAwait(false);
            }
            catch (Exception sendError)
            {
                this.UnhandledError?.Invoke(this, new RpcException("Could not report a failed call to the caller", sendError));
            }
        }

        private void Close(Exception reason)
        {
            if (Interlocked.Exchange(ref this.closed, 1) != 0) return;

            this.closeSource.Cancel();
            this.connection.Dispose();

            foreach (int correlationId in this.pendingCalls.Keys)
            {
                if (this.pendingCalls.TryRemove(correlationId, out PendingCall pending))
                {
                    pending.Completion.TrySetException(new RpcConnectionException("Connection closed while waiting for the response", reason));
                }
            }

            // closeSource is deliberately not disposed: its token is still read by calls that race with the close
            this.Closed?.Invoke(this, reason);
        }

        private void ThrowIfClosed()
        {
            if (!this.IsOpen) throw new RpcConnectionException("Connection is closed");
        }

        private sealed class PendingCall
        {
            public PendingCall(string method, Type returnType)
            {
                this.Method = method;
                this.ReturnType = returnType;
                this.Completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public string Method { get; }

            public Type ReturnType { get; }

            public TaskCompletionSource<object> Completion { get; }
        }
    }
}
