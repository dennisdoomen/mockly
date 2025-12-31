using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Mockly.Common;
#if NET472
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Represents a configured HTTP request mock.
/// </summary>
public class RequestMock
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.OrdinalIgnoreCase);

    private bool hostPatternNormalized;

    public HttpMethod Method { get; init; } = HttpMethod.Get;

    public string? PathPattern { get; init; }

    public string? QueryPattern { get; init; }

    // REFACTOR: Should not be nullable in the future and replaced with an enum
    public string? Scheme { get; init; }

    public string? HostPattern { get; set; }

    public IEnumerable<Matcher> CustomMatchers { get; internal init; } = [];

    public Func<RequestInfo, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage();

    public RequestCollection? RequestCollection { get; init; } = new();

    /// <summary>
    /// Gets a value determining how many times this mock has been invoked.
    /// </summary>
    public int InvocationCount { get; private set; }

    /// <summary>
    /// Gets the maximum number of times this mock can be invoked.
    /// If <c>null</c>, the mock can be invoked unlimited times.
    /// </summary>
    public uint? MaxInvocations { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this mock has been exhausted, i.e.
    /// it has been invoked at least <see cref="MaxInvocations"/> times when that limit is set.
    /// </summary>
    internal bool IsExhausted => MaxInvocations is not null && InvocationCount >= MaxInvocations;

    /// <summary>
    /// Checks if this mock matches the given request.
    /// </summary>
    public async Task<bool> Matches(RequestInfo request)
    {
        // Check HTTP method
        if (!request.Method.Equals(Method))
        {
            return false;
        }

        // Check scheme if specified
        if (Scheme != null && request.Uri != null)
        {
            if (!string.Equals(request.Uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check host pattern if specified
        if (HostPattern != null && request.Uri != null)
        {
            NormalizeHostPattern();
            var host = request.Uri.Host + ":" + request.Uri.Port;
            if (!MatchesPattern(host, HostPattern))
            {
                return false;
            }
        }

        // Check path pattern if specified
        if (PathPattern != null)
        {
            var path = WebUtility.UrlDecode(request.Uri?.AbsolutePath ?? string.Empty);
            if (!MatchesPattern(path.TrimStart('/'), PathPattern.TrimStart('/')))
            {
                return false;
            }
        }

        // Check query pattern if specified
        string query = WebUtility.UrlDecode(request.Uri?.Query ?? string.Empty);
        if (QueryPattern != null)
        {
            if (!MatchesPattern(query, QueryPattern))
            {
                return false;
            }
        }
        else if (query.Length > 0 && !CustomMatchers.Any())
        {
            // No query specified and no custom matchers configured, so this can never be match
            return false;
        }
        else
        {
            // No query specified and no pattern configured, so this is a match
        }

        // Check custom matcher if specified
        if (CustomMatchers.Any())
        {
            foreach (Matcher matcher in CustomMatchers)
            {
                if (!await matcher.IsMatch(request))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Normalizes the host pattern by appending the default port if missing
    /// based on the scheme (443 for HTTPS, 80 for HTTP), unless the pattern is a wildcard.
    /// </summary>
    private void NormalizeHostPattern()
    {
        if (!hostPatternNormalized && HostPattern is not null && HostPattern != "*")
        {
            string[] segments = HostPattern.Split(':');
            if (segments.Length == 1)
            {
                HostPattern += Scheme!.Equals("https", StringComparison.OrdinalIgnoreCase) ? ":443" : ":80";
            }
        }

        hostPatternNormalized = true;
    }

    /// <summary>
    /// Calculates a score representing how closely this mock matches the given request.
    /// </summary>
    internal async Task<int> GetMatchScoreAsync(RequestInfo request)
    {
        var score = 0;

        // HTTP method must match first; otherwise the score remains zero.
        if (request.Method.Equals(Method))
        {
            score += 5;
        }

        // Check scheme if specified
        if (Scheme != null && request.Uri != null)
        {
            if (string.Equals(request.Uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
            {
                score += 5;
            }
        }
        else
        {
            score += 5;
        }

        // Check host pattern if specified
        if (HostPattern != null && request.Uri != null)
        {
            if (MatchesPattern(request.Uri.Host, HostPattern))
            {
                score += 5;
            }
        }
        else
        {
            score += 5;
        }

        // Check path pattern if specified
        var path = request.Uri?.AbsolutePath.TrimStart('/') ?? string.Empty;
        if (PathPattern != null)
        {
            string trimmedPattern = PathPattern.TrimStart('/');
            if (MatchesPattern(path, trimmedPattern))
            {
                score += path.Length;
            }
            else
            {
                // Calculate the number of characters that match
                score += trimmedPattern.CountOrderedOverlap(path);
            }
        }
        else
        {
            score += path.Length;
        }

        // Check query pattern if specified
        string query = WebUtility.UrlDecode(request.Uri?.Query ?? string.Empty);
        if (QueryPattern != null)
        {
            if (MatchesPattern(query, QueryPattern))
            {
                score += query.Length;
            }
            else
            {
                // Calculate the number of characters that match
                score += QueryPattern.CountOrderedOverlap(query);
            }
        }

        // Check custom matchers if any
        if (CustomMatchers.Any())
        {
            foreach (Matcher matcher in CustomMatchers)
            {
                if (await matcher.IsMatch(request))
                {
                    score += 5;
                }
            }
        }

        return score;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        // Convert wildcard pattern to regex and cache it
#if NETFRAMEWORK || NETSTANDARD2_0
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
    public CapturedRequest TrackRequest(RequestInfo request)
    {
        InvocationCount++;

        CapturedRequest capturedRequest = new(request)
        {
            Mock = this,
            WasExpected = true,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            capturedRequest.Response = Responder(request);
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            capturedRequest.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = $"{e.GetType().Name}:{e.Message}"
            };
        }

        RequestCollection?.Add(capturedRequest);

        return capturedRequest;
    }

    /// <summary>
    /// Builds a detailed textual representation of this mock, including its route
    /// and any configured custom matchers.
    /// </summary>
    /// <remarks>
    /// This method is intended for diagnostics and exception messages only and is
    /// not part of the public API.
    /// </remarks>
    internal string ToDetailedString()
    {
        var route = ToString();

        if (!CustomMatchers.Any())
        {
            return route;
        }

        var matcherDescriptions = string.Join(" or ", CustomMatchers.Select(m => "(" + m + ")"));
        return $"{route} where {matcherDescriptions}";
    }

    /// <summary>
    /// Returns a string describing the exact route pattern this mock will match.
    /// Wildcards (*) indicate unspecified components.
    /// Example: GET https://api.*.com/users/*?*
    /// </summary>
    public override string ToString()
    {
        string method = Method.Method ?? "*";
        string scheme = Scheme ?? "http(s)";
        string host = HostPattern ?? "*";
        string path = PathPattern ?? "*";

        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            path = "/" + path;
        }

        string? query;
        if (QueryPattern == null)
        {
            query = "";
        }
        else
        {
            query = QueryPattern.StartsWith("?", StringComparison.Ordinal) ? QueryPattern : "?" + QueryPattern;
        }

        string route = $"{method} {scheme}://{host}{path}{query}";
        if (CustomMatchers.Any())
        {
            route += $" ({CustomMatchers.Count()} custom matcher(s))";
        }

        return route;
    }
}
