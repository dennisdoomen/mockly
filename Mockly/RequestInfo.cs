using System.Net.Http;
using System.Net.Http.Headers;

namespace Mockly;

public class RequestInfo(HttpRequestMessage request, string? body)
{
    public Uri? Uri => request.RequestUri;

    public string? Body { get; } = body;

    public HttpRequestHeaders Headers
    {
        get => request.Headers;
    }

    public HttpMethod Method
    {
        get => request.Method;
        set => request.Method = value;
    }

#if !NET8_0_OR_GREATER
    public IDictionary<string, object?> Properties
    {
        get => request.Properties;
    }
#endif

#if NET8_0_OR_GREATER
    public HttpRequestOptions Options
    {
        get => request.Options;
    }

    public HttpVersionPolicy VersionPolicy
    {
        get => request.VersionPolicy;
        set => request.VersionPolicy = value;
    }
#endif

    public Version Version
    {
        get => request.Version;
        set => request.Version = value;
    }
}
