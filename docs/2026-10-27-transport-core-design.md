# Transport core design

Date: 2026-10-27

## Goal

Replace the transport core of RPC.Library so that calls are reliable (no lost
or merged frames, no interleaved writes, no hanging calls), fast (binary
framing, pooled buffers, no reflection per call) and work both synchronously
and asynchronously, with the underlying transport (TCP, QUIC, UDP)
interchangeable behind one abstraction.

## Layers

```
RpcHost<TLocal, TRemote> / RpcPeer<TLocal, TRemote>   public facade
    RpcProxy<TRemote>      DispatchProxy, turns interface calls into frames
    ServiceDispatcher      compiled delegates, runs incoming calls on TLocal
    RpcChannel             correlation ids, timeouts, error frames, receive loop
        IRpcSerializer     JsonRpcSerializer (System.Text.Json, UTF-8 bytes)
        IRpcConnection     one complete message in, one complete message out
            TcpTransport   NetworkStream (optionally wrapped in SslStream)
            QuicTransport  one bidirectional QuicStream per connection (net8.0)
            UdpTransport   one datagram per message, one-way only
```

## Transport abstraction

- `IRpcTransport`: `Features`, `ConnectAsync(EndPoint)`, `ListenAsync(EndPoint)`.
- `IRpcListener`: `AcceptAsync()` returns an `IRpcConnection` per remote peer.
  For UDP the listener demultiplexes datagrams by sender and hands out one
  connection per new sender.
- `IRpcConnection`: `SendAsync(ReadOnlyMemory<byte>)` sends one whole message,
  `ReceiveAsync()` returns the next whole message (or `null` when closed),
  plus `Features` and `MaxMessageSize`.
- `TransportFeatures` flags: `Reliable`, `Ordered`, `RequestResponse`.
  TCP and QUIC have all three; UDP has none.

Stream transports share `StreamRpcConnection`, which does the length-prefix
framing (`int32 length` + body) over any `Stream` and serialises writes with a
lock, so a `Stream` wrapper such as `SslStream` is enough to add TLS.

## Frame

```
[byte type]            Request = 1, Response = 2, Error = 3, OneWay = 4
[int32 correlationId]  0 for OneWay
[uint16 methodLength][method utf8]   empty for Response / Error
[payload]              serializer output: argument array, result, or error info
```

## Call semantics

- `void` method: `OneWay` frame, returns once the frame is written, no result and
  no remote exception.
- `T` method: `Request` frame, blocks on the response (`GetAwaiter().GetResult()`).
- `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>`: `Request` frame, awaits the
  response natively.
- A remote exception comes back as an `Error` frame and surfaces as
  `RpcRemoteException` (remote type name and message, stack trace only when
  `RpcOptions.IncludeExceptionDetails` is on).
- Every request has a timeout (`RpcOptions.CallTimeout`, default 30 s) and fails
  with `RpcTimeoutException`; when the connection closes every pending call fails
  with `RpcConnectionException`.
- Overloads are not supported: method names must be unique in a contract.
  Any number of parameters is supported (serialised as an array).

## One-way transports (UDP)

- `RpcPeer` and `RpcHost` check both contracts when the transport lacks
  `RequestResponse`: any non-`void` method makes construction fail with
  `RpcTransportException` naming the offending methods.
- The channel refuses to send a `Request` frame, and drops an incoming
  `Request` frame with an `UnhandledError` event, over such a transport.
- Every message is checked against `MaxMessageSize` before it is sent
  (`RpcMessageTooLargeException`); for UDP that is one datagram.
- Delivery is best effort; nothing is retransmitted.

## QUIC

`System.Net.Quic` is available from .NET 7, so `QuicTransport` is compiled for
the `net8.0` target only. One bidirectional stream per connection carries the
frames; the client writes one preamble byte so the server sees the stream
immediately. TLS 1.3 is built in: the server needs a certificate, the client a
target host and optionally a validation callback.

## Testing

`RPC.Library.Tests` (xunit, net8.0) runs the same loopback suite over TCP and,
where supported, QUIC: sync and async calls, multiple parameters, complex
types, large messages, concurrency, remote exceptions, timeouts, callbacks from
server to client, and disconnects. UDP tests cover one-way notifications in
both directions, the contract check, and the datagram size check.
