using System.Net;

namespace Mockly;

/// <summary>
/// Exposes an <see cref="HttpMock"/> over a real socket.
/// </summary>
public static class HttpMockServerExtensions
{
    /// <summary>
    /// Starts a loopback-only HTTP server that serves requests using the configuration of this mock.
    /// Matching, capturing and assertions behave exactly as with <see cref="HttpMock.GetClient"/>: requests are matched
    /// as if sent to <c>https://localhost/</c>, so configure mocks by path, e.g. <c>ForGet("/api/users")</c>.
    /// </summary>
    /// <param name="mock">The mock whose configuration the server uses.</param>
    /// <param name="port">The port to listen on. The default of 0 picks a free port.</param>
    /// <returns>The running server. Dispose it to stop listening.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mock"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not a valid port number.</exception>
    public static MockServer StartServer(this HttpMock mock, int port = 0)
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, IPEndPoint.MaxPort);

        return new MockServer(mock, port);
    }
}
