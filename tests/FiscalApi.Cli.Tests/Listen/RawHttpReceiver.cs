using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FiscalApi.Cli.Tests.Listen;

internal sealed class RawHttpReceiver : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly string _statusLine;
    private int _requests;

    public RawHttpReceiver(string statusLine)
    {
        _statusLine = statusLine;
        _listener.Start();
        _ = AcceptAsync();
    }

    public Uri Url => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/webhooks");

    public int Requests => Volatile.Read(ref _requests);

    public void Dispose() => _listener.Stop();

    private async Task AcceptAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
            {
                return;
            }

            _ = RespondAsync(client);
        }
    }

    private async Task RespondAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            await ReadRequestAsync(stream);
            Interlocked.Increment(ref _requests);
            var response = Encoding.ASCII.GetBytes($"{_statusLine}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
        }
    }

    private static async Task ReadRequestAsync(NetworkStream stream)
    {
        var received = new List<byte>();
        var buffer = new byte[4096];
        int headerEnd;
        while ((headerEnd = IndexOf(received, "\r\n\r\n"u8)) < 0)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                return;
            }

            received.AddRange(buffer.AsSpan(0, read));
        }

        var headers = Encoding.ASCII.GetString(received.ToArray(), 0, headerEnd);
        var bodyLength = headers.Split("\r\n")
            .Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            .Select(line => int.Parse(line["Content-Length:".Length..].Trim()))
            .FirstOrDefault();
        var chunked = headers.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase);
        while (chunked ? IndexOf(received, "\r\n0\r\n\r\n"u8) < 0 : received.Count < headerEnd + 4 + bodyLength)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                return;
            }

            received.AddRange(buffer.AsSpan(0, read));
        }
    }

    private static int IndexOf(List<byte> bytes, ReadOnlySpan<byte> value) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytes).IndexOf(value);
}
