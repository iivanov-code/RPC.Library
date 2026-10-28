namespace RPC.Library.Protocol
{
    public enum FrameType : byte
    {
        /// <summary>
        /// A call that expects a <see cref="Response"/> or an <see cref="Error"/>
        /// </summary>
        Request = 1,

        Response = 2,

        Error = 3,

        /// <summary>
        /// A call that expects nothing back
        /// </summary>
        OneWay = 4
    }
}
