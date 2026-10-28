using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;

namespace RPC.Library.Transports
{
    /// <summary>
    /// Message framing over any duplex <see cref="Stream"/>: [int32 length][message].
    /// Writes are serialised so concurrent senders never interleave. Used by TCP and QUIC
    /// </summary>
    public abstract class StreamRpcConnection : IRpcConnection
    {
        private const int LENGTH_PREFIX_SIZE = sizeof(int);

        private readonly Stream stream;
        private readonly SemaphoreSlim writeLock;
        private readonly int maxMessageSize;
        private int disposed;

        protected StreamRpcConnection(Stream stream, int maxMessageSize)
        {
            if (maxMessageSize < 1) throw new ArgumentOutOfRangeException(nameof(maxMessageSize));

            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.maxMessageSize = maxMessageSize;
            this.writeLock = new SemaphoreSlim(1, 1);
        }

        public abstract EndPoint LocalEndPoint { get; }

        public abstract EndPoint RemoteEndPoint { get; }

        public TransportFeatures Features => TransportFeatures.Stream;

        public int MaxMessageSize => this.maxMessageSize;

        public bool IsDisposed => this.disposed != 0;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
        {
            if (message.Length > this.maxMessageSize) throw new RpcMessageTooLargeException(message.Length, this.maxMessageSize);
            this.ThrowIfDisposed();

            // one buffer, one write: length prefix and body leave together
            int frameSize = LENGTH_PREFIX_SIZE + message.Length;
            byte[] frame = ArrayPool<byte>.Shared.Rent(frameSize);
            try
            {
                BinaryPrimitives.WriteInt32LittleEndian(frame, message.Length);
                message.CopyTo(frame.AsMemory(LENGTH_PREFIX_SIZE));

                await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await this.stream.WriteAsync(frame.AsMemory(0, frameSize), cancellationToken).ConfigureAwait(false);
                    await this.stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.writeLock.Release();
                }
            }
            catch (IOException ex)
            {
                throw new RpcConnectionException("Could not write to the connection", ex);
            }
            catch (ObjectDisposedException ex)
            {
                throw new RpcConnectionException("Connection is closed", ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(frame);
            }
        }

        public async ValueTask<ReceivedMessage> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            if (this.IsDisposed) return null;

            byte[] prefix = ArrayPool<byte>.Shared.Rent(LENGTH_PREFIX_SIZE);
            try
            {
                int read = await this.ReadExactlyAsync(prefix, LENGTH_PREFIX_SIZE, cancellationToken).ConfigureAwait(false);
                if (read == 0) return null;
                if (read < LENGTH_PREFIX_SIZE) throw new RpcConnectionException("Connection closed in the middle of a frame");

                int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
                if (length < 0 || length > this.maxMessageSize)
                    throw new RpcProtocolException($"Frame length {length} is outside 0..{this.maxMessageSize}");

                byte[] body = ArrayPool<byte>.Shared.Rent(Math.Max(length, 1));
                try
                {
                    if (length > 0 && await this.ReadExactlyAsync(body, length, cancellationToken).ConfigureAwait(false) < length)
                        throw new RpcConnectionException("Connection closed in the middle of a frame");
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(body);
                    throw;
                }

                return new ReceivedMessage(body, length);
            }
            catch (IOException ex)
            {
                if (this.IsDisposed) return null;
                throw new RpcConnectionException("Could not read from the connection", ex);
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(prefix);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;

            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.stream.Dispose();
                this.writeLock.Dispose();
            }
        }

        /// <summary>
        /// Reads until count bytes are in the buffer. Returns fewer only when the stream ends first
        /// </summary>
        private async ValueTask<int> ReadExactlyAsync(byte[] buffer, int count, CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < count)
            {
                int read = await this.stream.ReadAsync(buffer.AsMemory(total, count - total), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;

                total += read;
            }

            return total;
        }

        private void ThrowIfDisposed()
        {
            if (this.IsDisposed) throw new RpcConnectionException("Connection is closed");
        }
    }
}
