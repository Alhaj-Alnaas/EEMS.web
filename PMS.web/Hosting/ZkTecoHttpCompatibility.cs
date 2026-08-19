using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace PMS.web.Hosting
{
    /// <summary>
    /// ZKTeco ADMS firmware often sends HTTP/1.1 without a Host header.
    /// Kestrel rejects that with 400 before any MVC/Blazor middleware runs.
    /// This connection adapter injects a Host header when missing.
    /// </summary>
    public static class ZkTecoHttpCompatibility
    {
        public static ListenOptions UseZkTecoHttpCompatibility(this ListenOptions listenOptions)
        {
            listenOptions.Use(next => async connection =>
            {
                var logger = connection.Features.Get<IServiceProvider>()
                    ?.GetService<ILoggerFactory>()
                    ?.CreateLogger("ZkTeco.HttpFix");

                var hostValue = BuildHostValue(connection.LocalEndPoint);
                var remote = Describe(connection.RemoteEndPoint);
                logger?.LogWarning("CONNECTION OPEN from {Remote}", remote);

                var original = connection.Transport;
                await using var adapter = new HostHeaderInjectingDuplexPipe(original, hostValue, remote, logger);
                connection.Transport = adapter;
                try
                {
                    await next(connection);
                }
                finally
                {
                    connection.Transport = original;
                }
            });

            return listenOptions;
        }

        private static string BuildHostValue(EndPoint? local)
        {
            if (local is IPEndPoint ip)
            {
                var addr = ip.Address.IsIPv4MappedToIPv6 ? ip.Address.MapToIPv4() : ip.Address;
                // 0.0.0.0 / :: are not valid Host values — use localhost with the real port.
                if (IPAddress.Any.Equals(addr) || IPAddress.IPv6Any.Equals(addr))
                    return $"localhost:{ip.Port}";

                return addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                    ? $"[{addr}]:{ip.Port}"
                    : $"{addr}:{ip.Port}";
            }

            return "localhost:5260";
        }

        private static string Describe(EndPoint? remote)
        {
            if (remote is not IPEndPoint ip) return remote?.ToString() ?? "unknown";
            var addr = ip.Address.IsIPv4MappedToIPv6 ? ip.Address.MapToIPv4() : ip.Address;
            return $"{addr}:{ip.Port}";
        }
    }

    file sealed class HostHeaderInjectingDuplexPipe : IDuplexPipe, IAsyncDisposable
    {
        private readonly IDuplexPipe _inner;
        private readonly HostHeaderInjectingPipeReader _input;

        public HostHeaderInjectingDuplexPipe(IDuplexPipe inner, string hostValue, string remote, ILogger? logger)
        {
            _inner = inner;
            _input = new HostHeaderInjectingPipeReader(inner.Input, hostValue, remote, logger);
            Output = inner.Output;
        }

        public PipeReader Input => _input;
        public PipeWriter Output { get; }

        public ValueTask DisposeAsync() => _input.DisposeAsync();
    }

    file sealed class HostHeaderInjectingPipeReader : PipeReader, IAsyncDisposable
    {
        private readonly PipeReader _inner;
        private readonly string _hostValue;
        private readonly string _remote;
        private readonly ILogger? _logger;
        private readonly Pipe _pipe = new();
        private bool _rewritten;
        private bool _completed;

        public HostHeaderInjectingPipeReader(PipeReader inner, string hostValue, string remote, ILogger? logger)
        {
            _inner = inner;
            _hostValue = hostValue;
            _remote = remote;
            _logger = logger;
        }

        public override void AdvanceTo(SequencePosition consumed) => _pipe.Reader.AdvanceTo(consumed);

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) =>
            _pipe.Reader.AdvanceTo(consumed, examined);

        public override void CancelPendingRead()
        {
            _inner.CancelPendingRead();
            _pipe.Reader.CancelPendingRead();
        }

        public override void Complete(Exception? exception = null)
        {
            _completed = true;
            _inner.Complete(exception);
            _pipe.Writer.Complete(exception);
            _pipe.Reader.Complete(exception);
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (!_rewritten)
            {
                _rewritten = true;
                await RewriteFirstRequestAsync(cancellationToken);
            }

            return await _pipe.Reader.ReadAsync(cancellationToken);
        }

        public override bool TryRead(out ReadResult result) => _pipe.Reader.TryRead(out result);

        public ValueTask DisposeAsync()
        {
            if (!_completed)
                Complete();
            return ValueTask.CompletedTask;
        }

        private async Task RewriteFirstRequestAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    var result = await _inner.ReadAsync(ct);
                    var buffer = result.Buffer;

                    if (!buffer.IsEmpty && buffer.FirstSpan.Length > 0 && buffer.FirstSpan[0] == 0x16)
                    {
                        _logger?.LogWarning(
                            "TLS handshake received on plain-HTTP endpoint from {Remote}. " +
                            "The client is using HTTPS; disable HTTPS/SSL on the device or serve HTTPS.",
                            _remote);

                        foreach (var segment in buffer)
                            await _pipe.Writer.WriteAsync(segment, ct);
                        _inner.AdvanceTo(buffer.End);
                        _ = PumpRemainingAsync(ct);
                        return;
                    }

                    if (TryGetHeadersEnd(buffer, out var headersEnd))
                    {
                        var headers = buffer.Slice(0, headersEnd);
                        var rest = buffer.Slice(headersEnd);
                        var fixedHeaders = EnsureHostHeader(headers);

                        await _pipe.Writer.WriteAsync(fixedHeaders, ct);
                        if (!rest.IsEmpty)
                        {
                            foreach (var segment in rest)
                                await _pipe.Writer.WriteAsync(segment, ct);
                        }

                        _inner.AdvanceTo(buffer.End);

                        // Pump remaining bytes in background.
                        _ = PumpRemainingAsync(ct);
                        return;
                    }

                    if (result.IsCompleted)
                    {
                        if (!buffer.IsEmpty)
                        {
                            foreach (var segment in buffer)
                                await _pipe.Writer.WriteAsync(segment, ct);
                        }

                        _inner.AdvanceTo(buffer.End);
                        await _pipe.Writer.CompleteAsync();
                        return;
                    }

                    // Need more data for a complete header block.
                    _inner.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "ZKTeco HTTP Host-header fix failed; passing stream through as-is is no longer possible");
                await _pipe.Writer.CompleteAsync(ex);
            }
        }

        private async Task PumpRemainingAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    var result = await _inner.ReadAsync(ct);
                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        foreach (var segment in buffer)
                            await _pipe.Writer.WriteAsync(segment, ct);
                    }

                    _inner.AdvanceTo(buffer.End);
                    if (result.IsCompleted)
                        break;
                }

                await _pipe.Writer.CompleteAsync();
            }
            catch (Exception ex)
            {
                await _pipe.Writer.CompleteAsync(ex);
            }
        }

        private byte[] EnsureHostHeader(ReadOnlySequence<byte> headers)
        {
            // Normalize to a contiguous string for a small header block.
            var text = Encoding.ASCII.GetString(headers.ToArray());
            if (text.Contains("\nHost:", StringComparison.OrdinalIgnoreCase)
                || text.Contains("\rHost:", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.ASCII.GetBytes(text);
            }

            // Only rewrite obvious HTTP requests.
            if (!(text.StartsWith("GET ", StringComparison.OrdinalIgnoreCase)
                  || text.StartsWith("POST ", StringComparison.OrdinalIgnoreCase)
                  || text.StartsWith("PUT ", StringComparison.OrdinalIgnoreCase)
                  || text.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase)))
            {
                return Encoding.ASCII.GetBytes(text);
            }

            var hostLine = $"Host: {_hostValue}\r\n";
            string fixedText;
            var idx = text.IndexOf("\r\n", StringComparison.Ordinal);
            if (idx >= 0)
            {
                fixedText = text.Insert(idx + 2, hostLine);
            }
            else
            {
                var idxN = text.IndexOf('\n');
                fixedText = idxN >= 0
                    ? text.Insert(idxN + 1, hostLine)
                    : hostLine + text;
            }

            _logger?.LogWarning(
                "Injected missing Host header ({Host}) for ZKTeco-compatible request: {FirstLine}",
                _hostValue,
                FirstLine(text));

            return Encoding.ASCII.GetBytes(fixedText);
        }

        private static string FirstLine(string text)
        {
            var end = text.IndexOfAny(['\r', '\n']);
            return end < 0 ? text : text[..end];
        }

        private static bool TryGetHeadersEnd(ReadOnlySequence<byte> buffer, out SequencePosition end)
        {
            // Header blocks from ZK devices are tiny; scan for blank line.
            if (buffer.Length > 16_384)
            {
                end = default;
                return false;
            }

            var bytes = buffer.ToArray();
            for (var i = 0; i < bytes.Length - 1; i++)
            {
                if (i + 3 < bytes.Length
                    && bytes[i] == (byte)'\r'
                    && bytes[i + 1] == (byte)'\n'
                    && bytes[i + 2] == (byte)'\r'
                    && bytes[i + 3] == (byte)'\n')
                {
                    end = buffer.GetPosition(i + 4);
                    return true;
                }

                if (bytes[i] == (byte)'\n' && bytes[i + 1] == (byte)'\n')
                {
                    end = buffer.GetPosition(i + 2);
                    return true;
                }
            }

            end = default;
            return false;
        }
    }
}
