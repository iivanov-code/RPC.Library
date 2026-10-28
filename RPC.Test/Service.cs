using System;
using System.Threading;
using System.Threading.Tasks;

namespace RPC.Test
{
    internal sealed class Service : IService
    {
        private int counter;

        public Data GetData()
        {
            return new Data { Id = Interlocked.Increment(ref this.counter) };
        }

        public Task<Data> GetDataAsync()
        {
            return Task.FromResult(this.GetData());
        }

        public Data Echo(Data data)
        {
            return data;
        }

        public void Notify(string message)
        {
            Console.WriteLine($"notification: {message}");
        }
    }
}
