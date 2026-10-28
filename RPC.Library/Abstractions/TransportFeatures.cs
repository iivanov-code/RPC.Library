using System;

namespace RPC.Library.Abstractions
{
    /// <summary>
    /// What a transport guarantees. The channel adapts its behaviour to these flags
    /// </summary>
    [Flags]
    public enum TransportFeatures
    {
        None = 0,

        /// <summary>
        /// Every sent message arrives
        /// </summary>
        Reliable = 1,

        /// <summary>
        /// Messages arrive in the order they were sent
        /// </summary>
        Ordered = 2,

        /// <summary>
        /// The remote side can answer a request. Without it only one-way (void) methods can be used
        /// </summary>
        RequestResponse = 4,

        /// <summary>
        /// Everything a stream transport such as TCP or QUIC offers
        /// </summary>
        Stream = Reliable | Ordered | RequestResponse
    }
}
