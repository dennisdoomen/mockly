using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Mockly.Common;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Represents a configured HTTP request mock.
/// </summary>
public class RequestMock
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object hostNormalizationLock = new();
    private readonly object respondersLock = new();

    private readonly List<(Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> Responder, uint? Count)> responders
        = [(static (_, _) => Task.FromResult(new HttpResponseMessage()), null)];

    private readonly List<Action<HttpResponseMessage>> responseMutators = [];
    private int invocationCount;

    private bool hostPatternNormalized;

    /// <summary>
    /// Gets the HTTP method this mock matches against.
    /// </summary>
    public HttpMethod Method { get; init; } = HttpMethod.Get;

    /// <summary>
    /// Gets the path pattern this mock matches against, supporting wildcards (<c>*</c>).
    /// When <c>null</c>, any path is accepted.
    /// </summary>
    public string? PathPattern { get; init; }

    /// <summary>
    /// Gets the query string pattern this mock matches against, supporting wildcards (<c>*</c>).
    /// When <c>null</c>, requests without a query string are accepted (unless custom matchers are configured).
    /// </summary>
    public string? QueryPattern { get; init; }

    // REFACTOR: Should not be nullable in the future and replaced with an enum

    /// <summary>
    /// Gets the URI scheme this mock matches against (e.g. <c>"http"</c> or <c>"https"</c>).
    /// When <c>null</c>, both schemes are accepted.
    /// </summary>
    public string? Scheme { get; init; }

    /// <summary>
    /// Gets or sets the host pattern this mock matches against, supporting wildcards (<c>*</c>).
    /// When <c>null</c>, any host is accepted.
    /// </summary>
    public string? HostPattern { get; set; }

    /// <summary>
    /// Gets the custom matchers that are evaluated in addition to the standard route criteria.
    /// All matchers must return <c>true</c> for the mock to match a request.
    /// </summary>
    public IEnumerable<Matcher> CustomMatchers { get; internal init; } = [];

    /// <summary>
    /// Gets or sets the responder used to produce a response for a matched request.
    /// </summary>
    /// <remarks>
    /// This is the first (or only) responder in the configured sequence. Additional responders can be appended
    /// through the fluent <c>Then*</c> methods on <see cref="SequencedResponseBuilder"/>, in which case this
    /// property continues to represent the response returned for the first invocation.
    /// </remarks>
    public Func<RequestInfo, HttpResponseMessage> Responder
    {
        get
        {
            lock (respondersLock)
            {
                Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> asyncFn = responders[0].Responder;
                return request => asyncFn(request, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        // ReSharper disable once PropertyCanBeMadeInitOnly.Global
        internal set
        {
            Func<RequestInfo, HttpResponseMessage> captured = value;
            lock (respondersLock)
            {
                responders[0] = ((req, _) => Task.FromResult(captured(req)), responders[0].Count);
            }
        }
    }

    /// <summary>
    /// Gets the collection that receives every <see cref="CapturedRequest"/> handled by this mock.
    /// When <c>null</c>, captured requests are not stored.
    /// </summary>
    public RequestCollection? RequestCollection { get; init; } = [];

    /// <summary>
    /// Gets a value determining how many times this mock has been invoked.
    /// </summary>
    public int InvocationCount => Volatile.Read(ref invocationCount);

    /// <summary>
    /// Gets the maximum number of times this mock can be invoked.
    /// If <c>null</c>, the mock can be invoked unlimited times.
    /// </summary>
    public uint? MaxInvocations
    {
        get
        {
            lock (respondersLock)
            {
                if (responders[^1].Count is null)
                {
                    return null;
                }

                uint total = 0;

                foreach ((_, uint? count) in responders)
                {
                    total += count!.Value;
                }

                return total;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this mock has been exhausted, i.e.
    /// it has been invoked at least <see cref="MaxInvocations"/> times when that limit is set.
    /// </summary>
    internal bool IsExhausted => MaxInvocations is not null && Volatile.Read(ref invocationCount) >= MaxInvocations;

    /// <summary>
    /// Checks if this mock matches the given request.
    /// </summary>
    public async Task<bool> Matches(RequestInfo request)
    {
        NormalizeHostPatternOnce();

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
    private void NormalizeHostPatternOnce()
    {
        lock (hostNormalizationLock)
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

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> matches <paramref name="pattern"/>,
    /// where <c>*</c> in the pattern acts as a wildcard. Matching is case-insensitive.
    /// Compiled regexes are cached to avoid repeated compilation.
    /// </summary>
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
    /// Appends a responder to the configured sequence of responses.
    /// </summary>
    /// <remarks>
    /// Consecutive matching requests are served by consecutive responders. Once the sequence is exhausted,
    /// the last responder is repeated for every subsequent request.
    /// When a new responder is appended, the previously-last entry's unlimited slot is promoted to a
    /// single-use count so the sequence advances after one invocation by default.
    /// </remarks>
    internal void AppendResponder(Func<RequestInfo, HttpResponseMessage> responder)
    {
        Func<RequestInfo, HttpResponseMessage> captured = responder;

        lock (respondersLock)
        {
            ConvertLastResponderToSingleUse();
            responders.Add(((req, _) => Task.FromResult(captured(req)), null));
        }
    }

    /// <summary>
    /// Appends an asynchronous responder to the configured sequence of responses.
    /// </summary>
    internal void AppendAsyncResponder(Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        lock (respondersLock)
        {
            ConvertLastResponderToSingleUse();
            responders.Add((responder, null));
        }
    }

    /// <summary>
    /// Sets the count for the most recently configured response in the sequence.
    /// </summary>
    /// <remarks>
    /// When this response's count is exhausted the sequence advances to the next entry. If this is the last
    /// entry in the sequence the mock becomes exhausted and stops matching further requests.
    /// </remarks>
    internal void SetCurrentResponseCount(uint count)
    {
        lock (respondersLock)
        {
            (Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> responder, _) = responders[^1];
            responders[^1] = (responder, count);
        }
    }

    /// <summary>
    /// Replaces the first (initial) responder with an asynchronous function.
    /// Used by the async <c>RespondsWith</c> overloads to set the primary async responder.
    /// </summary>
    internal void SetFirstAsyncResponder(Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        lock (respondersLock)
        {
            responders[0] = (responder, responders[0].Count);
        }
    }

    /// <summary>
    /// Converts the current last responder from an unlimited response to a single-use response.
    /// </summary>
    /// <remarks>
    /// Appended responders are unlimited by default. When a new responder is appended, the previously last
    /// responder must become single-use so the sequence can advance to the new responder after one invocation.
    /// </remarks>
    private void ConvertLastResponderToSingleUse()
    {
        (Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> lastResponder, uint? lastCount) = responders[^1];

        if (lastCount is null)
        {
            responders[^1] = (lastResponder, Count: 1);
        }
    }

    /// <summary>
    /// Registers a post-processing callback that is applied to every <see cref="HttpResponseMessage"/>
    /// produced by this mock, after the responder generates it.
    /// </summary>
    /// <remarks>
    /// Multiple mutators can be registered; they are applied in registration order.
    /// Mutators are called by builder classes (e.g. <c>RequestMockResponseBuilder</c>) when the user
    /// chains response-modification methods such as <c>WithHeader</c>, keeping post-processing concerns
    /// separate from the responder sequence.
    /// </remarks>
    /// <param name="responseMutator">
    /// A delegate that receives the response and can modify it in-place, for example by adding headers
    /// or changing the status code.
    /// </param>
    internal void AppendResponseMutator(Action<HttpResponseMessage> responseMutator)
    {
        lock (respondersLock)
        {
            responseMutators.Add(responseMutator);
        }
    }

    /// <summary>
    /// Handles the request asynchronously and returns a response, awaiting the configured responder and
    /// flowing the supplied <paramref name="cancellationToken"/> into it.
    /// </summary>
    /// <remarks>
    /// Both synchronous and asynchronous responders converge on this single asynchronous execution path.
    /// </remarks>
    internal async Task<CapturedRequest> TrackRequestAsync(RequestInfo request, CancellationToken cancellationToken)
    {
        int invocationIndex = Interlocked.Increment(ref invocationCount) - 1;

        CapturedRequest capturedRequest = new(request)
        {
            Mock = this,
            WasExpected = true,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            capturedRequest.Response = ApplyResponseMutators(await InvokeResponderAsync(request, invocationIndex, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
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
    /// Invokes the responder for the given invocation index, awaiting asynchronous responders and
    /// flowing the supplied <paramref name="cancellationToken"/> into them.
    /// </summary>
    private Task<HttpResponseMessage> InvokeResponderAsync(RequestInfo request, int invocationIndex, CancellationToken cancellationToken)
    {
        return GetResponderForInvocation(invocationIndex)(request, cancellationToken);
    }

    /// <summary>
    /// Returns the responder that should handle the given zero-based <paramref name="invocationIndex"/>
    /// by walking the sequence and subtracting each entry's count until the correct slot is found.
    /// Falls back to the last responder once the sequence is exhausted.
    /// </summary>
    private Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> GetResponderForInvocation(int invocationIndex)
    {
        lock (respondersLock)
        {
            long remaining = invocationIndex;

            foreach ((Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>> responder, uint? count) in responders)
            {
                if (count is null)
                {
                    return responder;
                }

                if (remaining < count.Value)
                {
                    return responder;
                }

                remaining -= count.Value;
            }

            return responders[^1].Responder;
        }
    }

    /// <summary>
    /// Applies all registered response mutators to <paramref name="response"/> in registration order
    /// and returns the (modified) response.
    /// </summary>
    private HttpResponseMessage ApplyResponseMutators(HttpResponseMessage response)
    {
        Action<HttpResponseMessage>[] mutators;
        lock (respondersLock)
        {
            mutators = [.. responseMutators];
        }

        foreach (Action<HttpResponseMessage> mutator in mutators)
        {
            mutator(response);
        }

        return response;
    }

    /// <summary>
    /// Builds a detailed textual representation of this mock, including its route
    /// and any configured custom matchers.
    /// </summary>
    /// <remarks>
    /// This method is intended for diagnostics and exception messages only and is
    /// not part of the public API.
    /// </remarks>
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

        var route = $"{method} {scheme}://{host}{path}{query}";

        if (!CustomMatchers.Any())
        {
            return route;
        }

        var matcherDescriptions = string.Join(" or ", CustomMatchers);
        return $"{route} where {matcherDescriptions}";
    }
}
