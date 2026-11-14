using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// An OpenAPI document that has been parsed and indexed so that incoming requests can be mapped onto its operations.
/// </summary>
internal sealed class OpenApiContract
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly List<string> basePaths;
    private readonly List<PathTemplate> pathTemplates;

    private OpenApiContract(string source, OpenApiDocument document)
    {
        Source = source;
        basePaths = GetBasePaths(document);
        pathTemplates = document.Paths
            .Select(path => new PathTemplate(path.Key, path.Value))
            .OrderByDescending(template => template.LiteralLength)
            .ToList();
    }

    /// <summary>
    /// Gets the file the contract was loaded from, for diagnostic purposes.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Loads and parses the OpenAPI 3.x or Swagger 2.0 JSON document at <paramref name="path"/>.
    /// </summary>
    public static OpenApiContract Load(string path)
    {
        return Parse(File.ReadAllText(path), path);
    }

    /// <summary>
    /// Downloads and parses the OpenAPI 3.x or Swagger 2.0 JSON document at <paramref name="url"/>.
    /// </summary>
    /// <param name="url">The absolute HTTP(S) URL of the document.</param>
    /// <param name="httpClient">The client to download with, or <c>null</c> to use a new one.</param>
    public static OpenApiContract Download(Uri url, HttpClient? httpClient)
    {
        using HttpClient? ownedClient = httpClient is null ? new HttpClient() : null;
        HttpClient client = httpClient ?? ownedClient!;

        // The mock is configured synchronously, so block on the download. ConfigureAwait(false) avoids
        // deadlocks in environments with a synchronization context.
        using HttpResponseMessage response = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"The OpenAPI document {url} could not be downloaded: the server responded with " +
                $"{(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        string content = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        return Parse(content, url.ToString());
    }

    private static OpenApiContract Parse(string content, string source)
    {
        if (!content.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"The OpenAPI document {source} is not a JSON document. Only JSON documents are supported.");
        }

        ReadResult result = OpenApiDocument.Parse(content, "json", new OpenApiReaderSettings());

        IList<OpenApiError> errors = result.Diagnostic?.Errors ?? [];
        if (result.Document is null || errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"The OpenAPI document {source} could not be loaded:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "  - " + error)));
        }

        return new OpenApiContract(source, result.Document);
    }

    /// <summary>
    /// Finds the path of the document that matches the path of <paramref name="requestUri"/>, either directly or
    /// relative to one of the base paths declared by the document's servers.
    /// </summary>
    /// <returns>The matching path and the values of its path parameters, or <c>null</c> if no path matches.</returns>
    public PathMatch? FindPath(Uri? requestUri)
    {
        string requestPath = requestUri?.AbsolutePath ?? "/";
        if (requestPath.Length > 1)
        {
            requestPath = requestPath.TrimEnd('/');
        }

        foreach (string candidate in GetCandidatePaths(requestPath))
        {
            foreach (PathTemplate template in pathTemplates)
            {
                if (template.TryMatch(candidate, out Dictionary<string, string>? parameters))
                {
                    return new PathMatch(template.Template, template.PathItem, parameters);
                }
            }
        }

        return null;
    }

    private IEnumerable<string> GetCandidatePaths(string requestPath)
    {
        foreach (string basePath in basePaths)
        {
            if (requestPath.Equals(basePath, StringComparison.Ordinal))
            {
                yield return "/";
            }
            else if (requestPath.StartsWith(basePath + "/", StringComparison.Ordinal))
            {
                yield return requestPath.Substring(basePath.Length);
            }
            else
            {
                // This base path does not apply to the request
            }
        }

        yield return requestPath;
    }

    /// <summary>
    /// Extracts the path portion of every server URL (e.g. <c>/api/v3</c> from <c>https://host/api/v3</c>),
    /// substituting server variables with their default values. For Swagger 2.0 documents, the parser converts
    /// <c>host</c> and <c>basePath</c> into a server.
    /// </summary>
    private static List<string> GetBasePaths(OpenApiDocument document)
    {
        var result = new List<string>();

        foreach (OpenApiServer server in document.Servers ?? [])
        {
            string url = Regex.Replace(server.Url ?? string.Empty, @"\{(?<name>[^}]+)}", match =>
                server.Variables?.TryGetValue(match.Groups["name"].Value, out OpenApiServerVariable? variable) == true
                    ? variable.Default ?? string.Empty
                    : match.Value, RegexOptions.ExplicitCapture, MatchTimeout);

            // Strip the scheme and authority, including scheme-relative ones like //host/api, which is what
            // the parser produces for a Swagger 2.0 document without schemes.
            int authorityStart = url.IndexOf("//", StringComparison.Ordinal);
            if (authorityStart >= 0)
            {
                string afterScheme = url.Substring(authorityStart + 2);
                int pathStart = afterScheme.IndexOf("/", StringComparison.Ordinal);
                url = pathStart >= 0 ? afterScheme.Substring(pathStart) : string.Empty;
            }

            string basePath = url.TrimEnd('/');
            if (basePath.Length > 0 && basePath.IndexOf("{", StringComparison.Ordinal) < 0 && !result.Contains(basePath))
            {
                result.Add(basePath);
            }
        }

        return result.OrderByDescending(path => path.Length).ToList();
    }

    /// <summary>
    /// A path of the document such as <c>/pets/{petId}</c>, compiled into a regular expression.
    /// </summary>
    private sealed class PathTemplate
    {
        private readonly Regex regex;
        private readonly List<string> parameterNames = new();

        public PathTemplate(string template, IOpenApiPathItem pathItem)
        {
            Template = template;
            PathItem = pathItem;

            // Spec-derived literals are escaped and placeholders become [^/]+, so the pattern cannot backtrack
            // catastrophically.
            string pattern = Regex.Replace(Regex.Escape(template.TrimEnd('/')), @"\\\{(?<name>[^}]+)}", match =>
            {
                parameterNames.Add(Regex.Unescape(match.Groups["name"].Value));
                return $"(?<p{parameterNames.Count - 1}>[^/]+)";
            }, RegexOptions.ExplicitCapture, MatchTimeout);

            regex = new Regex("^" + (pattern.Length == 0 ? "/" : pattern) + "$",
                RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, MatchTimeout);

            LiteralLength = Regex.Replace(template, @"\{[^}]+}", string.Empty, RegexOptions.None, MatchTimeout).Length;
        }

        public string Template { get; }

        public IOpenApiPathItem PathItem { get; }

        /// <summary>
        /// Gets the number of non-placeholder characters, so that concrete paths like <c>/pets/mine</c> are tried
        /// before templated ones like <c>/pets/{petId}</c>, as the OpenAPI specification requires.
        /// </summary>
        public int LiteralLength { get; }

        public bool TryMatch(string path, [NotNullWhen(true)] out Dictionary<string, string>? parameters)
        {
            Match match = regex.Match(path);
            if (!match.Success)
            {
                parameters = null;
                return false;
            }

            parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < parameterNames.Count; index++)
            {
                parameters[parameterNames[index]] = Uri.UnescapeDataString(match.Groups[$"p{index}"].Value);
            }

            return true;
        }
    }
}

/// <summary>
/// The result of mapping a request path onto a path of an OpenAPI document.
/// </summary>
internal sealed class PathMatch(string template, IOpenApiPathItem pathItem, Dictionary<string, string> pathParameters)
{
    public string Template { get; } = template;

    public IOpenApiPathItem PathItem { get; } = pathItem;

    public IReadOnlyDictionary<string, string> PathParameters { get; } = pathParameters;

    public OpenApiOperation? FindOperation(HttpMethod method)
    {
        if (PathItem.Operations is null)
        {
            return null;
        }

        return PathItem.Operations
            .Where(operation => string.Equals(operation.Key.Method, method.Method, StringComparison.OrdinalIgnoreCase))
            .Select(operation => operation.Value)
            .FirstOrDefault();
    }
}
