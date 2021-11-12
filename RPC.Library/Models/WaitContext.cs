using System;

namespace NetworkCommunicator.Models
{
    internal class WaitContext : BaseWaitContext
    {
        private WaitContext(bool shouldWait)
            : base(shouldWait)
        { }

        internal WaitContext(Guid key, bool shouldWait)
            : base(key, shouldWait)
        { }

        public static WaitContext WaitForResponse
        {
            get
            {
                return new WaitContext(true);
            }
        }

        public static BaseWaitContext NoResponse
        {
            get
            {
                return new WaitContext(false);
            }
        }

    }
}
