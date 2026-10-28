using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Mockly;

/// <summary>
/// A real HTTP listener, bound to the loopback interface, that serves the requests it receives
/// using the configuration of an <see cref="HttpMock"/>.
/// </summary>
public sealed class MockServer : IAsyncDisposable
{
    /// <summary>
    /// The number of free ports to try before giving up when no specific port was requested.
    /// </summary>
    private const int MaxPortAttempts = 5;

    /// <summary>
    /// The base address under which incoming requests are presented to the mock, the same as <see cref="HttpMock.GetClient"/> uses.
    /// </summary>
    private static readonly Uri DefaultBaseAddress = new("https://localhost/");

    /// <summary>
    /// The listener that accepts the incoming connections.
    /// </summary>
    private readonly HttpListener listener;

    /// <summary>
    /// Sends the incoming requests through the message handler of the mock.
    /// </summary>
    private readonly HttpMessageInvoker invoker;

    /// <summary>
    /// Signals the accept loop and the pending requests to stop when the server is disposed.
    /// </summary>
    private readonly CancellationTokenSource cts = new();

    /// <summary>
    /// The background task that accepts incoming requests until the server is disposed.
    /// </summary>
    private readonly Task loop;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockServer"/> class and starts listening.
    /// </summary>
    /// <param name="mock">The mock whose configuration is used to serve the requests.</param>
    /// <param name="port">The port to listen on, or 0 to pick a free port.</param>
    internal MockServer(HttpMock mock, int port)
    {
        (listener, Url) = StartListener(port);
        invoker = new HttpMessageInvoker(mock.GetMessageHandler());
        loop = Task.Run(AcceptLoop);
    }

    /// <summary>
    /// Gets the base address of the server, e.g. <c>http://127.0.0.1:50123/</c>.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// Starts a new listener on the loopback interface.
    /// </summary>
    /// <remarks>
    /// When no port was requested, it retries on another free port, since a process may grab the port in between.
    /// A failed <see cref="HttpListener.Start"/> leaves the listener unusable, so every attempt uses a new one.
    /// </remarks>
    private static (HttpListener Listener, Uri Url) StartListener(int port)
    {
        for (int attempt = 1;; attempt++)
        {
            var url = new Uri($"http://127.0.0.1:{(port == 0 ? GetFreePort() : port)}/");

            var listener = new HttpListener();
            listener.Prefixes.Add(url.ToString());

            try
            {
                listener.Start();
                return (listener, url);
            }
            catch (HttpListenerException)
            {
                listener.Close();

                if (port != 0 || attempt >= MaxPortAttempts)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Asks the operating system for a port on the loopback interface that is free at this moment.
    /// </summary>
    private static int GetFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        try
        {
            return ((IPEndPoint)tcp.LocalEndpoint).Port;
        }
        finally
        {
            tcp.Stop();
        }
    }

    /// <summary>
    /// Accepts incoming requests and handles each of them in the background, until the listener is closed.
    /// </summary>
    private async Task AcceptLoop()
    {
        while (!cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => Handle(context));
        }
    }

    /// <summary>
    /// Passes a single request to the mock and writes its response back to the caller.
    /// </summary>
    /// <remarks>
    /// An unmatched request results in a <c>404 Not Found</c>, the same status the mock returns when
    /// <see cref="HttpMock.FailOnUnexpectedCalls"/> is <c>false</c>. Any other exception, such as a failure simulated by
    /// <c>ThrowsException</c> or <c>TimesOut</c>, aborts the connection, so the caller sees a network-level failure.
    /// </remarks>
    private async Task Handle(HttpListenerContext context)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(context.Request);
            using HttpResponseMessage response = await invoker.SendAsync(request, cts.Token);
            await WriteResponse(context.Response, response);
        }
        catch (UnexpectedRequestException ex)
        {
            await TryWriteNotFound(context.Response, ex.Message);
        }
        catch (Exception)
        {
            context.Response.Abort();
        }
    }

    /// <summary>
    /// Converts the request received by the listener into an <see cref="HttpRequestMessage"/>, including its body and headers.
    /// </summary>
    private static HttpRequestMessage BuildRequest(HttpListenerRequest source)
    {
        // Present the request under the same base address as HttpMock.GetClient(), so mocks match identically in both modes.
        var uri = new Uri(DefaultBaseAddress, source.Url!.PathAndQuery);
        var request = new HttpRequestMessage(new HttpMethod(source.HttpMethod), uri);

        if (source.HasEntityBody)
        {
            request.Content = new StreamContent(source.InputStream);
        }

        foreach (string? name in source.Headers.AllKeys)
        {
            if (name is null)
            {
                continue;
            }

            string[] values = source.Headers.GetValues(name) ?? [];
            if (!request.Headers.TryAddWithoutValidation(name, values) && request.Content is not null)
            {
                request.Content.Headers.TryAddWithoutValidation(name, values);
            }
        }

        return request;
    }

    /// <summary>
    /// Writes the status, headers and body of the mocked response to the listener response.
    /// </summary>
    private static async Task WriteResponse(HttpListenerResponse target, HttpResponseMessage response)
    {
        target.StatusCode = (int)response.StatusCode;
        if (response.ReasonPhrase is not null)
        {
            target.StatusDescription = response.ReasonPhrase;
        }

        CopyHeaders(target, response.Headers);

        CopyHeaders(target, response.Content.Headers);
        byte[] body = await response.Content.ReadAsByteArrayAsync();
        target.ContentLength64 = body.Length;
        await target.OutputStream.WriteAsync(body);
        target.Close();
    }

    /// <summary>
    /// Copies the headers to the listener response, except the framing headers that the listener sets itself.
    /// </summary>
    private static void CopyHeaders(HttpListenerResponse target, HttpHeaders headers)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
        {
            if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                target.ContentType = string.Join(", ", header.Value);
            }
            else
            {
                foreach (string value in header.Value)
                {
                    target.AppendHeader(header.Key, value);
                }
            }
        }
    }

    /// <summary>
    /// Writes a <c>404 Not Found</c> response that explains why no mock matched, or aborts the connection if that fails.
    /// </summary>
    private static async Task TryWriteNotFound(HttpListenerResponse target, string message)
    {
        try
        {
            target.StatusCode = (int)HttpStatusCode.NotFound;
            target.StatusDescription = "No matching mock found for this request";
            target.ContentType = "text/plain; charset=utf-8";
            byte[] body = Encoding.UTF8.GetBytes(message);
            target.ContentLength64 = body.Length;
            await target.OutputStream.WriteAsync(body);
            target.Close();
        }
        catch (Exception)
        {
            target.Abort();
        }
    }

    /// <summary>
    /// Stops listening, waits for the accept loop to finish and releases the resources of the server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!cts.IsCancellationRequested)
        {
            await cts.CancelAsync();
            listener.Close();
            await loop;
            invoker.Dispose();
            cts.Dispose();
        }
    }
}
