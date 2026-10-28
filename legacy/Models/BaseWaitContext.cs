using System;
using System.IO;
using System.Threading;

namespace NetworkCommunicator.Models
{
    public abstract class BaseWaitContext
    {
        internal BaseWaitContext(Guid key, bool shouldWait)
        {
            this.Guid = key;

            ShouldWait = shouldWait;

            if (shouldWait)
            {
                WaitHandle = new ManualResetEvent(false);
            }

            ResponseMessage = new MemoryStream();
        }

        protected BaseWaitContext(bool shouldWait)
        {
            Guid = Guid.NewGuid();

            ShouldWait = shouldWait;

            if (shouldWait)
            {
                WaitHandle = new ManualResetEvent(false);
            }

            ResponseMessage = new MemoryStream();
        }

        public bool FireEvent { get; set; }
        public Guid Guid { get; set; }
        public bool ShouldWait { get; set; }
        public MemoryStream ResponseMessage { get; set; }
        public ManualResetEvent WaitHandle { get; set; }
    }
}