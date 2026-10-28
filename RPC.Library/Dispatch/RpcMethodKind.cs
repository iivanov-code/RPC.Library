namespace RPC.Library.Dispatch
{
    internal enum RpcMethodKind
    {
        /// <summary>
        /// void: sent as a one-way frame, nothing comes back
        /// </summary>
        OneWay,

        /// <summary>
        /// T: request, the caller blocks for the response
        /// </summary>
        Synchronous,

        Task,

        TaskOfResult,

        ValueTask,

        ValueTaskOfResult
    }
}
