using System;
using System.Threading.Tasks;

namespace RPC.Library.Tests
{
    public class Person
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }

    public interface ICalculator
    {
        int Add(int a, int b);

        Task<int> AddAsync(int a, int b);

        Task DelayAsync(int milliseconds);

        ValueTask<int> DoubleAsync(int value);

        string Ping();

        string Concat(string text, int number, bool flag);

        string Describe(Person person);

        byte[] Blob(int size);

        void Fire(string message);

        int Throws();

        Task<int> ThrowsAsync();

        Task<string> UpperViaCallbackAsync(string text);
    }

    public interface IClientCallback
    {
        string Upper(string text);

        void Notify(string message);
    }

    public sealed class CalculatorService : ICalculator
    {
        private readonly RpcPeer<ICalculator, IClientCallback> peer;
        private readonly TaskCompletionSource<string> fired = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        public CalculatorService(RpcPeer<ICalculator, IClientCallback> peer)
        {
            this.peer = peer;
        }

        public Task<string> Fired => this.fired.Task;

        public int Add(int a, int b)
        {
            return a + b;
        }

        public async Task<int> AddAsync(int a, int b)
        {
            await Task.Yield();
            return a + b;
        }

        public Task DelayAsync(int milliseconds)
        {
            return Task.Delay(milliseconds);
        }

        public ValueTask<int> DoubleAsync(int value)
        {
            return new ValueTask<int>(value * 2);
        }

        public string Ping()
        {
            return "pong";
        }

        public string Concat(string text, int number, bool flag)
        {
            return $"{text}-{number}-{flag}";
        }

        public string Describe(Person person)
        {
            return $"{person.Name} is {person.Age}";
        }

        public byte[] Blob(int size)
        {
            byte[] blob = new byte[size];
            for (int i = 0; i < size; i++)
            {
                blob[i] = (byte)(i % 251);
            }

            return blob;
        }

        public void Fire(string message)
        {
            this.fired.TrySetResult(message);
        }

        public int Throws()
        {
            throw new InvalidOperationException("boom");
        }

        public Task<int> ThrowsAsync()
        {
            throw new InvalidOperationException("async boom");
        }

        public Task<string> UpperViaCallbackAsync(string text)
        {
            return Task.Run(() => this.peer.Remote.Upper(text));
        }
    }

    public sealed class ClientCallback : IClientCallback
    {
        private readonly TaskCompletionSource<string> notified = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Notified => this.notified.Task;

        public string Upper(string text)
        {
            return text.ToUpperInvariant();
        }

        public void Notify(string message)
        {
            this.notified.TrySetResult(message);
        }
    }

    public interface INotifications
    {
        void Log(string line);
    }

    public sealed class NotificationSink : INotifications
    {
        private readonly TaskCompletionSource<string> logged = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Logged => this.logged.Task;

        public void Log(string line)
        {
            this.logged.TrySetResult(line);
        }
    }
}
