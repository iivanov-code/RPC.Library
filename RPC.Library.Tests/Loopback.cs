// QUIC is only reachable on Windows, Linux and macOS; the tests run on those
#pragma warning disable CA1416
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using RPC.Library.Abstractions;
using RPC.Library.Transports.Quic;
using RPC.Library.Transports.Tcp;

namespace RPC.Library.Tests
{
    /// <summary>
    /// A host and one connected client on the loopback interface over the named transport
    /// </summary>
    public sealed class Loopback : IDisposable
    {
        public static readonly TimeSpan WAIT = TimeSpan.FromSeconds(10);

        private Loopback()
        { }

        public RpcHost<ICalculator, IClientCallback> Host { get; private set; }

        public RpcPeer<IClientCallback, ICalculator> Client { get; private set; }

        public ClientCallback Callback { get; private set; }

        public CalculatorService Service { get; private set; }

        public RpcPeer<ICalculator, IClientCallback> ServerPeer { get; private set; }

        public static IEnumerable<object[]> Transports
        {
            get
            {
                yield return new object[] { "tcp" };

                if (QuicTransport.IsSupported)
                {
                    yield return new object[] { "quic" };
                }
            }
        }

        public static IRpcTransport CreateTransport(string name)
        {
            switch (name)
            {
                case "tcp":
                    return new TcpTransport();

                case "quic":
                    return new QuicTransport(new QuicTransportOptions
                    {
                        ServerCertificate = TestCertificate.Instance,
                        RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true
                    });

                default:
                    throw new ArgumentException($"Unknown transport {name}", nameof(name));
            }
        }

        public static async Task<Loopback> StartAsync(string transportName, RpcOptions options = null)
        {
            Loopback loopback = new Loopback();
            IRpcTransport transport = CreateTransport(transportName);

            TaskCompletionSource<RpcPeer<ICalculator, IClientCallback>> serverPeer = new TaskCompletionSource<RpcPeer<ICalculator, IClientCallback>>(TaskCreationOptions.RunContinuationsAsynchronously);

            loopback.Host = new RpcHost<ICalculator, IClientCallback>(transport, peer =>
            {
                loopback.Service = new CalculatorService(peer);
                return loopback.Service;
            }, options);
            loopback.Host.PeerConnected += (sender, peer) => serverPeer.TrySetResult(peer);

            await loopback.Host.StartAsync(new IPEndPoint(IPAddress.Loopback, 0));

            loopback.Callback = new ClientCallback();
            loopback.Client = await RpcPeer.ConnectAsync<IClientCallback, ICalculator>(transport, loopback.Host.LocalEndPoint, loopback.Callback, options);
            loopback.ServerPeer = await serverPeer.Task.WaitAsync(WAIT);

            return loopback;
        }

        public void Dispose()
        {
            this.Client?.Dispose();
            this.Host?.Dispose();
        }
    }

    internal static class TestCertificate
    {
        private static readonly Lazy<X509Certificate2> instance = new Lazy<X509Certificate2>(Create, LazyThreadSafetyMode.ExecutionAndPublication);

        public static X509Certificate2 Instance => instance.Value;

        private static X509Certificate2 Create()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                SubjectAlternativeNameBuilder alternativeNames = new SubjectAlternativeNameBuilder();
                alternativeNames.AddDnsName("localhost");
                alternativeNames.AddIpAddress(IPAddress.Loopback);
                request.CertificateExtensions.Add(alternativeNames.Build());

                using (X509Certificate2 ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)))
                {
                    // MsQuic on Windows needs a private key it can hand to Schannel; the PFX round trip provides one
                    return new X509Certificate2(ephemeral.Export(X509ContentType.Pfx), (string)null, X509KeyStorageFlags.Exportable);
                }
            }
        }
    }
}
