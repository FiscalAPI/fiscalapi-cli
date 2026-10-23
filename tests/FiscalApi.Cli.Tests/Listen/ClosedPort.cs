using System.Net;
using System.Net.Sockets;

namespace FiscalApi.Cli.Tests.Listen;

internal sealed class ClosedPort : IDisposable
{
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public ClosedPort() => _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

    public int Port => ((IPEndPoint)_socket.LocalEndPoint!).Port;

    public Uri Url(string path = "") => new($"http://127.0.0.1:{Port}/{path}");

    public void Dispose() => _socket.Dispose();
}
