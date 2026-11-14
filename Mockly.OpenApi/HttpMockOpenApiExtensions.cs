using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.OpenApi;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly;

/// <summary>
/// Lets an <see cref="HttpMock"/> respond to and police requests using an OpenAPI (Swagger) document.
/// </summary>
public static class HttpMockOpenApiExtensions
{
    private static readonly ConditionalWeakTable<HttpMock, List<OpenApiContract>> Contracts = new();

    /// <summary>
    /// Responds to every request that maps onto an operation of the OpenAPI document at <paramref name="pathOrUrl"/> with
    /// the operation's success response, using the examples from the document or values generated from its schemas,
    /// and rejects every request that violates the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Explicitly configured mocks, such as <c>ForGet("/pets/1")</c>, always take precedence over the generated
    /// responses, regardless of the order in which they were configured. Supports OpenAPI 3.0 and 3.1 as well as
    /// Swagger 2.0 documents in JSON format. Request paths are matched both as-is and relative to the paths of the
    /// <c>servers</c> (or Swagger 2.0 <c>basePath</c>) declared by the document. Can be called multiple times to
    /// combine several documents.
    /// </para>
    /// <para>
    /// Like any other unexpected call, a request that belongs to the API but violates the document is captured as
    /// unexpected in <see cref="HttpMock.Requests"/>. If <see cref="HttpMock.FailOnUnexpectedCalls"/> is <c>true</c>
    /// (the default), it throws a <see cref="RequestContractViolationException"/> that lists every violation;
    /// otherwise it returns <c>400 Bad Request</c>. This includes requests handled by explicitly configured mocks.
    /// A request violates the document if it calls an undocumented operation, omits a required parameter or body,
    /// or sends a parameter or JSON body that violates the schema of the operation. Requests that are not under the
    /// base path of any document, and don't match any of its paths, are not validated.
    /// </para>
    /// </remarks>
    /// <param name="mock">The mock to configure.</param>
    /// <param name="pathOrUrl">
    /// The path to the JSON OpenAPI document, or an absolute <c>http</c> or <c>https</c> URL to download it from,
    /// such as <c>https://staging.example.com/swagger/v1/swagger.json</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="mock"/> or <paramref name="pathOrUrl"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">The document is not in JSON format.</exception>
    /// <exception cref="InvalidOperationException">
    /// The document could not be downloaded or is not a valid OpenAPI document.
    /// </exception>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
        Justification = "Accepts a file path as well as a URL; the Uri overload covers the URL-only case.")]
    public static HttpMock UsingOpenApi(this HttpMock mock, string pathOrUrl)
    {
        if (mock is null)
        {
            throw new ArgumentNullException(nameof(mock));
        }

        if (pathOrUrl is null)
        {
            throw new ArgumentNullException(nameof(pathOrUrl));
        }

        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out Uri? url) && IsHttp(url))
        {
            return mock.UsingOpenApi(url);
        }

        return Register(mock, OpenApiContract.Load(pathOrUrl));
    }

    /// <summary>
    /// Downloads the OpenAPI document from <paramref name="url"/>, for instance from a real (test) environment of
    /// the API, and responds to every request that maps onto one of its operations like
    /// <see cref="UsingOpenApi(HttpMock, string)"/> does.
    /// </summary>
    /// <remarks>
    /// The document is downloaded once, when this method is called.
    /// </remarks>
    /// <param name="mock">The mock to configure.</param>
    /// <param name="url">The absolute <c>http</c> or <c>https</c> URL of the JSON OpenAPI document.</param>
    /// <param name="httpClient">
    /// The client to download the document with, for instance to add authentication headers. If <c>null</c>, a new
    /// client is used. Don't pass the client of the mock itself.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="mock"/> or <paramref name="url"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="url"/> is not an absolute HTTP(S) URL.</exception>
    /// <exception cref="HttpRequestException">The server could not be reached.</exception>
    /// <exception cref="NotSupportedException">The document is not in JSON format.</exception>
    /// <exception cref="InvalidOperationException">
    /// The server did not return a success status code, or the document is not a valid OpenAPI document.
    /// </exception>
    public static HttpMock UsingOpenApi(this HttpMock mock, Uri url, HttpClient? httpClient = null)
    {
        if (mock is null)
        {
            throw new ArgumentNullException(nameof(mock));
        }

        if (url is null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (!url.IsAbsoluteUri || !IsHttp(url))
        {
            throw new ArgumentException($"The URL {url} must be an absolute http or https URL.", nameof(url));
        }

        return Register(mock, OpenApiContract.Download(url, httpClient));
    }

    private static bool IsHttp(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;
    }

    private static HttpMock Register(HttpMock mock, OpenApiContract contract)
    {
        if (!Contracts.TryGetValue(mock, out List<OpenApiContract>? contracts))
        {
            contracts = [];
            Contracts.Add(mock, contracts);

            // A single validator checks the request against all documents, so it is valid if any of them allows it.
            mock.AddRequestValidator(request => ContractValidator.Validate(request, contracts));
        }

        contracts.Add(contract);

        mock.AddFallbackResponder(request =>
        {
            OpenApiOperation? operation = contract.FindPath(request.Uri)?.FindOperation(request.Method);
            return operation is null ? null : ResponseGenerator.CreateResponse(operation);
        });

        return mock;
    }
}
