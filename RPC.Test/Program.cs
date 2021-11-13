using System.Diagnostics;
using NetworkCommunicator.Host;
using NetworkCommunicator.Network;

namespace RPC.Test
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            bool isHost = true;
            Data data;

            if (isHost)
            {
                var host = MainServiceHost<IService, Service>.InitMainNode(899, true);
            }
            else
            {
                var networkClient = new ServiceNetworkClient<Service, IService>("127.0.0.1", 899, new Service());

                bool result = networkClient.Connect().GetAwaiter().GetResult();
                networkClient.Listen();

                Stopwatch stopwatch = new Stopwatch();
                List<long> times = new List<long>();
                for (int i = 0; i < 100; i++)
                {
                    stopwatch.Start();
                    data = networkClient.RemoteService.GetData();
                    stopwatch.Stop();
                    times.Add(stopwatch.ElapsedMilliseconds);
                }

                Console.WriteLine("Avg: " + times.Average());
                Console.WriteLine("Min: " + times.Min());
                Console.WriteLine("Max: " + times.Max());
            }

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
