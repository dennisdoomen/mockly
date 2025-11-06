using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Mockly.Http;

/// <summary>
/// Represents a configured HTTP request mock.
/// </summary>
internal class RequestMock
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.OrdinalIgnoreCase);

    public HttpMethod Method { get; set; } = HttpMethod.Get;

    public string? PathPattern { get; set; }

    public string? QueryPattern { get; set; }

    public Func<HttpRequestMessage, bool>? CustomMatcher { get; set; }

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage();

    public RequestCollection? RequestCollection { get; set; }

    public int InvocationCount { get; set; }

    /// <summary>
    /// Checks if this mock matches the given request.
    /// </summary>
    public bool Matches(HttpRequestMessage request)
    {
        // Check HTTP method
        if (!request.Method.Equals(Method))
        {
            return false;
        }

        // Check path pattern if specified
        if (PathPattern != null)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (!MatchesPattern(path, PathPattern))
            {
                return false;
            }
        }

        // Check query pattern if specified
        if (QueryPattern != null)
        {
            var query = request.RequestUri?.Query ?? string.Empty;
            if (!MatchesPattern(query, QueryPattern))
            {
                return false;
            }
        }

        // Check custom matcher if specified
        if (CustomMatcher != null)
        {
            return CustomMatcher(request);
        }

        return true;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        // Convert wildcard pattern to regex and cache it
#if NET47 || NETSTANDARD2_0
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
#else
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
#endif
        var regex = RegexCache.GetOrAdd(regexPattern, p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        return regex.IsMatch(value);
    }

    /// <summary>
    /// Handles the request and returns a response.
    /// </summary>
    public HttpResponseMessage HandleRequest(HttpRequestMessage request)
    {
        InvocationCount++;
        var response = Responder(request);

        if (RequestCollection != null)
        {
            RequestCollection.Add(new CapturedRequest
            {
                Request = request,
                Response = response,
                Mock = this,
                WasExpected = true,
                Timestamp = DateTime.UtcNow
            });
        }

        return response;
    }
}
