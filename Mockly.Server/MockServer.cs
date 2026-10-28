using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Mockly;

/// <summary>
/// A real HTTP listener, bound to the loopback interface, that serves the requests it receives
/// using the configuration of an <see cref="HttpMock"/>.
/// </summary>
public sealed class MockServer : IAsyncDisposable, IDisposable
{
    private static readonly Uri DefaultBaseAddress = new("https://localhost/");

    private readonly HttpListener listener = new();
    private readonly HttpMessageInvoker invoker;
    private readonly CancellationTokenSource cts = new();
    private readonly Task loop;

    internal MockServer(HttpMock mock, int port)
    {
        invoker = new HttpMessageInvoker(mock.GetMessageHandler());

        // Retry on another free port when none was requested, since a process may grab it in between.
        for (int attempt = 0;; attempt++)
        {
            int candidate = port == 0 ? GetFreePort() : port;
            Url = new Uri($"http://127.0.0.1:{candidate}/");
            listener.Prefixes.Clear();
            listener.Prefixes.Add(Url.ToString());

            try
            {
                listener.Start();
                break;
            }
            catch (HttpListenerException) when (port == 0 && attempt < 5)
            {
                // try another port
            }
        }

        loop = Task.Run(AcceptLoop);
    }

    /// <summary>
    /// Gets the base address of the server, e.g. <c>http://127.0.0.1:50123/</c>.
    /// </summary>
    public Uri Url { get; }

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port => Url.Port;

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

    private async Task Handle(HttpListenerContext context)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(context.Request);
            using HttpResponseMessage response = await invoker.SendAsync(request, cts.Token);
            await WriteResponse(context.Response, response);
        }
        catch (Exception ex)
        {
            // Unmatched requests (and other failures) surface as exceptions from the mock; report them to the caller.
            await TryWriteError(context.Response, ex);
        }
    }

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

    private static void CopyHeaders(HttpListenerResponse target, HttpHeaders headers)
    {
        foreach (var header in headers)
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
                    target.AddHeader(header.Key, value);
                }
            }
        }
    }

    private static async Task TryWriteError(HttpListenerResponse target, Exception ex)
    {
        try
        {
            target.StatusCode = (int)HttpStatusCode.InternalServerError;
            target.ContentType = "text/plain; charset=utf-8";
            byte[] body = Encoding.UTF8.GetBytes(ex.Message);
            target.ContentLength64 = body.Length;
            await target.OutputStream.WriteAsync(body);
            target.Close();
        }
        catch (Exception)
        {
            target.Abort();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
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
