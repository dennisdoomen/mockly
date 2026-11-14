using System.Runtime.CompilerServices;
using Microsoft.OpenApi;

namespace Mockly;

/// <summary>
/// Lets an <see cref="HttpMock"/> respond to and police requests using an OpenAPI (Swagger) document.
/// </summary>
public static class HttpMockOpenApiExtensions
{
    private static readonly ConditionalWeakTable<HttpMock, OpenApiSupport> Supports = new();

    /// <summary>
    /// Responds to every request that maps onto an operation of the OpenAPI document at <paramref name="path"/> with
    /// the operation's success response, using the examples from the document or values generated from its schemas.
    /// </summary>
    /// <remarks>
    /// Explicitly configured mocks, such as <c>ForGet("/pets/1")</c>, always take precedence over the generated
    /// responses, regardless of the order in which they were configured. Supports OpenAPI 3.0 and 3.1 as well as
    /// Swagger 2.0 documents in JSON format. Request paths are matched both as-is and relative to the paths of the
    /// <c>servers</c> (or Swagger 2.0 <c>basePath</c>) declared by the document. Can be called multiple times to
    /// combine several documents.
    /// </remarks>
    /// <param name="mock">The mock to configure.</param>
    /// <param name="path">The path to the JSON OpenAPI document.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mock"/> or <paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">The document is not in JSON format.</exception>
    /// <exception cref="InvalidOperationException">The document is not a valid OpenAPI document.</exception>
    public static HttpMock UsingOpenApi(this HttpMock mock, string path)
    {
        if (mock is null)
        {
            throw new ArgumentNullException(nameof(mock));
        }

        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        OpenApiContract contract = OpenApiContract.Load(path);
        GetSupport(mock).Contracts.Add(contract);

        mock.AddFallbackResponder(request =>
        {
            OpenApiOperation? operation = contract.FindPath(request.Uri)?.FindOperation(request.Method);
            return operation is null ? null : ResponseGenerator.CreateResponse(operation);
        });

        return mock;
    }

    /// <summary>
    /// Fails every request of the system under test that calls an operation that doesn't exist in the OpenAPI
    /// document(s) registered through <see cref="UsingOpenApi"/>, omits a required parameter or body, or sends a
    /// parameter or JSON body that violates the schema of that operation.
    /// </summary>
    /// <remarks>
    /// The validation applies to all requests, including those handled by explicitly configured mocks. A violating
    /// request is captured as unexpected in <see cref="HttpMock.Requests"/> and causes the call of the system under
    /// test to throw a <see cref="RequestContractViolationException"/> that lists every violation.
    /// </remarks>
    /// <param name="mock">The mock to configure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mock"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No OpenAPI document was registered through <see cref="UsingOpenApi"/>.
    /// </exception>
    public static HttpMock ValidateRequestsAgainstSpec(this HttpMock mock)
    {
        if (mock is null)
        {
            throw new ArgumentNullException(nameof(mock));
        }

        OpenApiSupport support = GetSupport(mock);
        if (support.Contracts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Call {nameof(UsingOpenApi)} to register an OpenAPI document before validating requests against it.");
        }

        if (!support.IsValidating)
        {
            support.IsValidating = true;
            mock.AddRequestValidator(request => ContractValidator.Validate(request, support.Contracts));
        }

        return mock;
    }

    private static OpenApiSupport GetSupport(HttpMock mock)
    {
        return Supports.GetValue(mock, _ => new OpenApiSupport());
    }

    private sealed class OpenApiSupport
    {
        public List<OpenApiContract> Contracts { get; } = new();

        public bool IsValidating { get; set; }
    }
}
