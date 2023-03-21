using System;

namespace NetworkCommunicator.Models
{
    internal class EventWaitContext : BaseWaitContext
    {
        private EventWaitContext(bool shouldWait)
            : base(shouldWait)
        { }

        public EventWaitContext(BaseWaitContext context)
            : base(context.ShouldWait)
        {
            Guid = context.Guid;
            FireEvent = context.FireEvent;
            WaitHandle = context.WaitHandle;
            ResponseMessage = context.ResponseMessage;
        }

        public EventWaitContext(Guid key, bool shouldWait)
            : base(key, shouldWait)
        {
            if (!shouldWait)
            {
                FireEvent = true;
            }
        }

        public byte[] Buffer { get; set; }
        public int MessageSize { get; set; }
        public int Read { get; set; }

        public static EventWaitContext WaitForResponse
        {
            get
            {
                return new EventWaitContext(true);
            }
        }

        public static EventWaitContext NoResponse
        {
            get
            {
                return new EventWaitContext(false);
            }
        }
    }
}