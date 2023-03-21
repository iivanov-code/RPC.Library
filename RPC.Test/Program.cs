using System.Diagnostics;
using NetworkCommunicator.Host;
using NetworkCommunicator.Interfaces;
using NetworkCommunicator.Network;

namespace RPC.Test
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            bool isHost = true;

            if (isHost)
            {
                ServerHost();
            }
            else
            {
                ClientBenchmark();
            }

            Console.ReadLine();
        }

        private static void ServerListener()
        {
            INetworkClient networkServer = new ServiceNetworkClient<Service, IService>(new Service());
            bool result = networkServer.AcceptConnection(899).GetAwaiter().GetResult();
            Console.WriteLine($"Listening on: {networkServer.LocalEndpoint}");
            networkServer.Listen();
        }

        private static void ServerHost()
        {
            var host = MainServiceHost<Service, IService>.InitMainNode(899, true);
            Console.WriteLine($"Listening on: {host.LocalEndpoint}");
        }

        private static void ClientBenchmark()
        {
            Data data;
            IServiceNetworkClient<Service, IService> networkClient = new ServiceNetworkClient<Service, IService>("127.0.0.1", 899, new Service());

            bool result = networkClient.Connect().GetAwaiter().GetResult();
            networkClient.Listen();

            Console.WriteLine($"Connected to: {networkClient.RemoteHostIP}:{networkClient.RemotePort}");

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
    }
}