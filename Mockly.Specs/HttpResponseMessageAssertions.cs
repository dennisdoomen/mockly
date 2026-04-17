using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
#if NET8_0_OR_GREATER
#else
using System.Text.Json;
#endif

namespace Mockly.Specs;

internal class HttpResponseMessageAssertions(HttpResponseMessage response)
    : FluentAssertions.Web.HttpResponseMessageAssertions(response, AssertionChain.GetOrCreate())
{
    private readonly HttpResponseMessage response = response;

    public async Task BeEquivalentTo<T>(T expectation)
    {
        string body = await response.Content.ReadAsStringAsync();

#if NET8_0_OR_GREATER
        JsonNode.Parse(body)!.Should().BeEquivalentTo(expectation);
#else
        T actual = (T)JsonSerializer.Deserialize(body, typeof(T));
        actual.Should().BeEquivalentTo(expectation);
#endif
    }
}
