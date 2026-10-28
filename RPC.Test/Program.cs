using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using RPC.Library;
using RPC.Library.Abstractions;
using RPC.Library.Transports.Tcp;

namespace RPC.Test
{
    /// <summary>
    /// Usage: RPC.Test server [port]  |  RPC.Test client [host] [port]
    /// </summary>
    internal static class Program
    {
        private const int DEFAULT_PORT = 8990;
        private const int CALLS = 10_000;

        public static async Task Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "server";
            IRpcTransport transport = new TcpTransport();

            if (mode == "server")
            {
                int port = args.Length > 1 ? int.Parse(args[1]) : DEFAULT_PORT;
                await RunServerAsync(transport, port);
            }
            else
            {
                string host = args.Length > 1 ? args[1] : "127.0.0.1";
                int port = args.Length > 2 ? int.Parse(args[2]) : DEFAULT_PORT;
                await RunClientAsync(transport, host, port);
            }
        }

        private static async Task RunServerAsync(IRpcTransport transport, int port)
        {
            using (RpcHost<IService, NoService> host = new RpcHost<IService, NoService>(transport, new Service()))
            {
                host.PeerConnected += (sender, peer) => Console.WriteLine($"connected: {peer.Connection.RemoteEndPoint}");
                host.PeerDisconnected += (sender, peer) => Console.WriteLine($"disconnected: {peer.Connection.RemoteEndPoint}");

                await host.StartAsync(new IPEndPoint(IPAddress.Any, port));
                Console.WriteLine($"Listening on {host.LocalEndPoint}. Press Enter to stop.");
                Console.ReadLine();
            }
        }

        private static async Task RunClientAsync(IRpcTransport transport, string host, int port)
        {
            using (RpcPeer<NoService, IService> client = await RpcPeer.ConnectAsync<NoService, IService>(transport, host, port, null!))
            {
                Console.WriteLine($"Connected to {client.Connection.RemoteEndPoint}");

                // warm up
                for (int i = 0; i < 100; i++)
                {
                    client.Remote.GetData();
                }

                client.Remote.Notify("benchmark starting");

                double[] micros = new double[CALLS];
                Stopwatch stopwatch = new Stopwatch();
                for (int i = 0; i < CALLS; i++)
                {
                    stopwatch.Restart();
                    client.Remote.GetData();
                    stopwatch.Stop();
                    micros[i] = stopwatch.Elapsed.TotalMilliseconds * 1000;
                }

                Array.Sort(micros);
                Console.WriteLine($"Sync GetData x{CALLS}: avg {micros.Average():F1} us, median {micros[CALLS / 2]:F1} us, p99 {micros[(int)(CALLS * 0.99)]:F1} us, max {micros[CALLS - 1]:F1} us");

                stopwatch.Restart();
                Data[] results = await Task.WhenAll(Enumerable.Range(0, CALLS).Select(i => client.Remote.GetDataAsync()));
                stopwatch.Stop();
                Console.WriteLine($"Async GetDataAsync x{CALLS} in flight together: {stopwatch.Elapsed.TotalMilliseconds:F0} ms, {CALLS / stopwatch.Elapsed.TotalSeconds:F0} calls/s, {results.Select(r => r.Id).Distinct().Count()} distinct ids");

                Data echoed = client.Remote.Echo(new Data { Id = 42 });
                Console.WriteLine($"Echo: {echoed.Id}");
            }
        }
    }
}
