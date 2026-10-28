using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Library.Transports.Tcp
{
    public class TcpTransportOptions
    {
        public const int DEFAULT_MAX_MESSAGE_SIZE = 16 * 1024 * 1024;

        /// <summary>
        /// Disables Nagle's algorithm so small frames leave immediately
        /// </summary>
        public bool NoDelay { get; set; } = true;

        public int Backlog { get; set; } = 128;

        public int MaxMessageSize { get; set; } = DEFAULT_MAX_MESSAGE_SIZE;

        /// <summary>
        /// Wraps the raw client stream, for example in an authenticated <see cref="System.Net.Security.SslStream"/>
        /// </summary>
        public Func<Stream, CancellationToken, Task<Stream>> ClientStreamWrapper { get; set; }

        /// <summary>
        /// Wraps the raw stream of every accepted connection, for example in an authenticated <see cref="System.Net.Security.SslStream"/>
        /// </summary>
        public Func<Stream, CancellationToken, Task<Stream>> ServerStreamWrapper { get; set; }
    }
}
