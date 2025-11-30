using System.Net.Http;

#if NET6_0_OR_GREATER
#else
using System.Text.Json;
#endif

namespace Mockly.Specs;

internal static class HttpResponseMessageExtensions
{
    public static HttpResponseMessageAssertions Should(this HttpResponseMessage response)
    {
        return new HttpResponseMessageAssertions(response);
    }
}
