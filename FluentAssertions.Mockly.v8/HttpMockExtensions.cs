using FluentAssertions.Execution;

namespace Mockly.FluentAssertions;

/// <summary>
/// Provides FluentAssertions extension methods for <see cref="HttpMock"/>.
/// </summary>
public static class HttpMockExtensions
{
    /// <summary>
    /// Returns an <see cref="HttpMockAssertions"/> object that can be used to assert on the <see cref="HttpMock"/>.
    /// </summary>
    public static HttpMockAssertions Should(this HttpMock subject)
    {
        return new HttpMockAssertions(subject, AssertionChain.GetOrCreate());
    }
}
