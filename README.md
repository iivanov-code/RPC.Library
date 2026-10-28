# RPC.Library

Small, fast remote procedure calls for .NET over interchangeable transports.
A contract is a plain C# interface; the client gets a proxy, the server runs an
implementation, and both sides can call each other.

- **Transports**: TCP (optionally TLS through `SslStream`), QUIC (net8.0, TLS 1.3
  built in) and UDP for one-way notifications. All share one abstraction, so
  switching is one line.
- **Sync and async**: `T`, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` and
  `void` (one-way) methods, any number of parameters, complex types.
- **Reliable**: length-prefixed binary framing, serialised writes, correlation
  ids, per-call timeouts, remote exceptions delivered to the caller, pending
  calls failed when the connection drops.
- **Fast**: pooled buffers, UTF-8 JSON straight into the frame, compiled
  delegates instead of reflection per call. Serialization is pluggable
  (`IRpcSerializer`).

Targets `net8.0`, `net6.0` and `netstandard2.1`. No dependencies on net6.0/net8.0.

## Contract

```csharp
public interface ICalculator
{
    int Add(int a, int b);                 // request, caller blocks for the answer
    Task<int> AddAsync(int a, int b);      // request, awaited
    Task SaveAsync(Document document);     // request without a result
    void Log(string line);                 // one-way: no answer, no remote exception
}

public interface IClientCallback
{
    void Progress(int percent);
}
```

Method names must be unique within a contract (no overloads); `ref`/`out`
parameters and generic methods are not supported.

## Server

```csharp
IRpcTransport transport = new TcpTransport();

// one service instance per connected peer; the peer lets the service call the client back
using RpcHost<ICalculator, IClientCallback> host = new RpcHost<ICalculator, IClientCallback>(
    transport,
    peer => new CalculatorService(peer));

host.PeerConnected += (sender, peer) => Console.WriteLine($"connected {peer.Connection.RemoteEndPoint}");
await host.StartAsync(new IPEndPoint(IPAddress.Any, 8990));
```

`new RpcHost<ICalculator, IClientCallback>(transport, sharedInstance)` serves one
thread-safe instance to every peer. Use `NoService` as the remote contract when
the server never calls the client.

## Client

```csharp
using RpcPeer<IClientCallback, ICalculator> client =
    await RpcPeer.ConnectAsync<IClientCallback, ICalculator>(transport, "127.0.0.1", 8990, new ClientCallback());

int sum = client.Remote.Add(2, 3);
int sumAsync = await client.Remote.AddAsync(2, 3);
client.Remote.Log("done");                 // returns as soon as the frame is written

client.Closed += (sender, reason) => Console.WriteLine($"closed: {reason?.Message ?? "clean"}");
```

A client that exposes nothing passes `null` as its service:
`RpcPeer.ConnectAsync<NoService, ICalculator>(transport, host, port, null)`.

## Errors and timeouts

| Situation | Caller sees |
| --- | --- |
| remote method throws | `RpcRemoteException` with `RemoteType` and `Message` (full text when `RpcOptions.IncludeExceptionDetails` is on) |
| no response within `RpcOptions.CallTimeout` (default 30 s) | `RpcTimeoutException`; the connection stays open |
| connection closed while waiting | `RpcConnectionException` |
| message larger than the transport allows | `RpcMessageTooLargeException`, nothing is sent |
| request over a one-way transport | `RpcTransportException` |

Failures nobody is waiting for (a one-way method that threw, a bad datagram)
are raised on `RpcPeer.UnhandledError`.

## Transports

```csharp
new TcpTransport(new TcpTransportOptions { MaxMessageSize = 16 * 1024 * 1024 });

new QuicTransport(new QuicTransportOptions            // net8.0, needs MsQuic (Windows 11 / libmsquic)
{
    ServerCertificate = certificate,                  // server side
    TargetHost = "example.com"                        // client side
});

new UdpTransport(new UdpTransportOptions { MaxDatagramSize = 1400 });   // one-way only
```

TLS over TCP is a stream wrapper:

```csharp
TcpTransportOptions options = new TcpTransportOptions
{
    ServerStreamWrapper = async (stream, token) =>
    {
        SslStream ssl = new SslStream(stream);
        await ssl.AuthenticateAsServerAsync(certificate);
        return ssl;
    },
    ClientStreamWrapper = async (stream, token) =>
    {
        SslStream ssl = new SslStream(stream);
        await ssl.AuthenticateAsClientAsync("example.com");
        return ssl;
    }
};
```

### One-way UDP

UDP declares `TransportFeatures.None`: no delivery guarantee, no ordering and no
responses. `RpcHost` and `RpcPeer` therefore refuse any contract that contains
a non-`void` method and name the offending methods in the exception. Every
message must fit in one datagram (`MaxDatagramSize`, 65507 by default; use about
1400 outside the loopback interface). The host creates one peer per sender, so
the server can send notifications back to a client after its first datagram.

### Writing a transport

Implement `IRpcTransport`, `IRpcListener` and `IRpcConnection` from
`RPC.Library.Abstractions`. A connection sends and receives whole messages;
derive from `StreamRpcConnection` to get the length-prefix framing and write
serialisation for free when the underlying protocol is a stream.

## Wire format

```
[int32 length]        (stream transports only; a UDP datagram is one message)
[byte type]           Request = 1, Response = 2, Error = 3, OneWay = 4
[int32 correlationId]
[uint16 methodLength][method utf8]
[payload]             JSON array of arguments, JSON result, or error info
```

## Benchmark

`RPC.Test server` then `RPC.Test client` runs 10 000 synchronous calls and
10 000 concurrent asynchronous calls over TCP on the loopback interface and
prints latency percentiles and throughput.

## Tests

`dotnet test` runs the loopback suite over TCP and, when MsQuic is available,
QUIC, plus the UDP one-way and framing tests.
