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
    public static MockServer StartServer(this HttpMock mock, int port = 0)
    {
        if (mock is null)
        {
            throw new ArgumentNullException(nameof(mock));
        }

        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        return new MockServer(mock, port);
    }
}
