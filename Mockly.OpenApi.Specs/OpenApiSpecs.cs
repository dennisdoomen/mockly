using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
#if NET472_OR_GREATER
using System.Net.Http;
#endif

namespace Mockly.OpenApi.Specs;

public static class OpenApiSpecs
{
    public class GeneratedResponses
    {
        [Fact]
        public async Task Responds_with_the_example_from_the_document()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://api.example.com/v1/pets?limit=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement[0].GetProperty("name").GetString().Should().Be("Rex");
            mock.Requests.Should().ContainSingle().Which.WasExpected.Should().BeTrue();
        }

        [Fact]
        public async Task Generates_a_schema_conforming_body_when_the_document_has_no_example()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://api.example.com/v1/pets/42");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement pet = body.RootElement;
            pet.GetProperty("id").GetInt64().Should().Be(0);
            pet.GetProperty("name").GetString().Should().Be("string");
            pet.GetProperty("status").GetString().Should().Be("available");
            pet.GetProperty("birthDate").GetString().Should().Be("2025-01-01");
            Guid.TryParse(pet.GetProperty("chipId").GetString(), out _).Should().BeTrue();
            pet.GetProperty("tags").GetArrayLength().Should().Be(1);
            pet.GetProperty("weight").GetDecimal().Should().Be(0.5m);
        }

        [Fact]
        public async Task Limits_the_depth_of_recursive_schemas()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://api.example.com/v1/pets/42");

            // Assert
            string body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("\"parent\":{");
        }

        [Fact]
        public async Task Responds_with_the_lowest_success_status_code()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://api.example.com/v1/pets",
                new StringContent("""{ "name": "Rex" }""", Encoding.UTF8, "application/json"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Responds_without_content_when_the_response_declares_none()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var response = await client.DeleteAsync("https://api.example.com/v1/pets/42");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        }

        [Fact]
        public async Task Explicit_mocks_take_precedence_regardless_of_registration_order()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            mock.ForGet("https://api.example.com/v1/pets/42").RespondsWithJsonContent(new { id = 42, name = "Explicit" });
            HttpClient client = mock.GetClient();

            // Act
            string body = await client.GetStringAsync("https://api.example.com/v1/pets/42");

            // Assert
            body.Should().Contain("Explicit");
            mock.AllMocksInvoked.Should().BeTrue();
        }

        [Fact]
        public async Task Matches_paths_with_and_without_the_base_path_of_the_server()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            var withBasePath = await client.GetAsync("/v1/pets/mine");
            var withoutBasePath = await client.GetAsync("/pets/mine");

            // Assert
            withBasePath.StatusCode.Should().Be(HttpStatusCode.OK);
            withoutBasePath.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Prefers_a_concrete_path_over_a_templated_one()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            string body = await client.GetStringAsync("/pets/mine");

            // Assert
            body.Should().Contain("Mine");
        }

        [Fact]
        public async Task Requests_outside_the_document_are_still_unexpected()
        {
            // Arrange
            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore));
            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("/owners");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Supports_swagger_2_documents()
        {
            // Arrange
            const string document =
                """
                {
                  "swagger": "2.0",
                  "info": { "title": "Orders", "version": "1.0" },
                  "host": "orders.example.com",
                  "basePath": "/api",
                  "paths": {
                    "/orders/{id}": {
                      "get": {
                        "produces": [ "application/json" ],
                        "parameters": [ { "name": "id", "in": "path", "required": true, "type": "string" } ],
                        "responses": { "200": { "description": "Order", "schema": { "$ref": "#/definitions/Order" } } }
                      }
                    }
                  },
                  "definitions": {
                    "Order": {
                      "type": "object",
                      "properties": { "total": { "type": "number", "minimum": 10 }, "paid": { "type": "boolean" } }
                    }
                  }
                }
                """;

            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(document));
            HttpClient client = mock.GetClient();

            // Act
            string body = await client.GetStringAsync("https://orders.example.com/api/orders/A1");

            // Assert
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("total").GetDecimal().Should().Be(10);
            json.RootElement.GetProperty("paid").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Supports_openapi_3_1_documents()
        {
            // Arrange
            const string document =
                """
                {
                  "openapi": "3.1.0",
                  "info": { "title": "Users", "version": "1.0" },
                  "paths": {
                    "/users/{id}": {
                      "get": {
                        "parameters": [ { "name": "id", "in": "path", "required": true, "schema": { "type": "string" } } ],
                        "responses": {
                          "200": {
                            "description": "User",
                            "content": {
                              "application/json": {
                                "schema": {
                                  "type": "object",
                                  "required": [ "email", "manager" ],
                                  "properties": {
                                    "email": { "type": "string", "format": "email" },
                                    "manager": { "type": [ "string", "null" ], "examples": [ "jane" ] }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """;

            var mock = new HttpMock().UsingOpenApi(OpenApiDocuments.WriteToFile(document));
            HttpClient client = mock.GetClient();

            // Act
            string body = await client.GetStringAsync("/users/1");

            // Assert
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("email").GetString().Should().Be("user@example.com");
            json.RootElement.GetProperty("manager").GetString().Should().Be("jane");
        }

        [Fact]
        public void Rejects_a_document_that_is_not_json()
        {
            // Arrange
            string path = OpenApiDocuments.WriteToFile("openapi: 3.0.0");

            // Act
            Action act = () => new HttpMock().UsingOpenApi(path);

            // Assert
            act.Should().Throw<NotSupportedException>().WithMessage("*Only JSON*");
        }

        [Fact]
        public void Rejects_a_document_that_is_not_a_valid_openapi_document()
        {
            // Arrange
            string path = OpenApiDocuments.WriteToFile("""{ "openapi": "3.0.0" }""");

            // Act
            Action act = () => new HttpMock().UsingOpenApi(path);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*could not be loaded*");
        }

        [Fact]
        public async Task Downloads_the_document_from_a_url()
        {
            // Arrange
            var environment = new HttpMock();
            environment.ForGet("https://staging.example.com/swagger/v1/swagger.json")
                .RespondsWithContent(HttpStatusCode.OK, OpenApiDocuments.PetStore, "application/json");

            var mock = new HttpMock().UsingOpenApi(
                new Uri("https://staging.example.com/swagger/v1/swagger.json"), environment.GetClient());

            HttpClient client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://api.example.com/v1/pets?limit=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement[0].GetProperty("name").GetString().Should().Be("Rex");
        }

        [Fact]
        public void Rejects_a_url_that_does_not_return_the_document()
        {
            // Arrange
            var environment = new HttpMock();
            environment.ForGet("https://staging.example.com/swagger/v1/swagger.json")
                .RespondsWithStatus(HttpStatusCode.NotFound);

            // Act
            Action act = () => new HttpMock().UsingOpenApi(
                new Uri("https://staging.example.com/swagger/v1/swagger.json"), environment.GetClient());

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*could not be downloaded*404*");
        }

        [Fact]
        public void Treats_an_http_string_as_a_url_to_download_from()
        {
            // Act
            Action act = () => new HttpMock().UsingOpenApi("http://127.0.0.1:1/swagger.json");

            // Assert
            act.Should().Throw<HttpRequestException>();
        }

        [Fact]
        public void Rejects_a_url_that_is_not_http()
        {
            // Act
            Action act = () => new HttpMock().UsingOpenApi(new Uri("ftp://staging.example.com/swagger.json"));

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*http or https*");
        }
    }

    public class RequestValidation
    {
        [Fact]
        public async Task Accepts_a_request_that_complies_with_the_document()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/v1/pets");
            request.Content = new StringContent(
                """{ "name": "Rex", "status": "sold", "nickname": null, "tags": [ "good" ] }""",
                Encoding.UTF8, "application/json");

            request.Headers.Add("X-Tenant", "acme");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Rejects_a_path_the_document_does_not_define()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("https://api.example.com/v1/owners");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*GET https://api.example.com/v1/owners*No path in the OpenAPI document matches*");
        }

        [Fact]
        public async Task Rejects_a_method_the_path_does_not_support()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.PutAsync("/v1/pets/1", new StringContent("{}"));

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*Path /pets/{petId} does not support PUT, only GET, DELETE*");
        }

        [Fact]
        public async Task Rejects_a_missing_required_query_parameter()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("/v1/pets");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*'listPets' (GET /pets)*Required query parameter 'limit' is missing*");
        }

        [Fact]
        public async Task Rejects_query_parameters_that_violate_their_schema()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("/v1/pets?limit=500&status=lost");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*query parameter 'limit' must be >= 1 and <= 100, but found 500*" +
                    "query parameter 'status' must be one of \"available\", \"sold\", but found \"lost\"*");
        }

        [Fact]
        public async Task Rejects_a_path_parameter_of_the_wrong_type()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("/v1/pets/rex");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*path parameter 'petId' must be an integer, but found a string \"rex\"*");
        }

        [Fact]
        public async Task Rejects_a_missing_required_header()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => client.PostAsync("/v1/pets",
                new StringContent("""{ "name": "Rex" }""", Encoding.UTF8, "application/json"));

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*Required header parameter 'X-Tenant' is missing*");
        }

        [Fact]
        public async Task Rejects_a_body_that_violates_the_schema()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => PostPet(client, """{ "petName": "Rex", "status": 3, "tags": [ "a", "b", "c" ] }""");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("""
                    *$ is missing required property 'name'*
                    *$ contains property 'petName' which the schema does not define*
                    *$.status must be a string, but found an integer 3*
                    *$.tags must have at most 2 items, but has 3*
                    """);
        }

        [Fact]
        public async Task Rejects_a_body_that_is_not_valid_json()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => PostPet(client, "{ name: ");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*Request body is not valid JSON*");
        }

        [Fact]
        public async Task Rejects_a_missing_required_body()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pets");
            request.Headers.Add("X-Tenant", "acme");

            // Act
            Func<Task> act = () => client.SendAsync(request);

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*Required request body is missing*");
        }

        [Fact]
        public async Task Rejects_a_content_type_the_operation_does_not_accept()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => PostPet(client, "name=Rex", "application/x-www-form-urlencoded");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*Content type 'application/x-www-form-urlencoded' is not accepted, expected one of application/json*");
        }

        [Fact]
        public async Task Also_validates_requests_handled_by_explicit_mocks()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            mock.ForPost("/v1/pets").RespondsWithStatus(HttpStatusCode.Created);
            HttpClient client = mock.GetClient();

            // Act
            Func<Task> act = () => PostPet(client, """{ "name": "" }""");

            // Assert
            await act.Should().ThrowAsync<RequestContractViolationException>()
                .WithMessage("*$.name must be at least 1 characters long*");

            mock.AllMocksInvoked.Should().BeFalse();
        }

        [Fact]
        public async Task Captures_a_violating_request_as_unexpected()
        {
            // Arrange
            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            try
            {
                await client.GetAsync("/v1/owners");
            }
            catch (RequestContractViolationException)
            {
                // Expected
            }

            // Assert
            mock.Requests.Should().ContainSingle().Which.WasExpected.Should().BeFalse();
        }

        [Fact]
        public async Task Accepts_a_request_that_matches_any_of_several_documents()
        {
            // Arrange
            const string owners =
                """
                {
                  "openapi": "3.0.3",
                  "info": { "title": "Owners", "version": "1.0" },
                  "paths": { "/owners": { "get": { "responses": { "200": { "description": "Owners" } } } } }
                }
                """;

            var mock = new HttpMock()
                .UsingOpenApi(OpenApiDocuments.WriteToFile(OpenApiDocuments.PetStore))
                .UsingOpenApi(OpenApiDocuments.WriteToFile(owners))
                .ValidateRequestsAgainstSpec();

            HttpClient client = mock.GetClient();

            // Act
            var response = await client.GetAsync("/owners");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public void Requires_a_document_to_validate_against()
        {
            // Act
            Action act = () => new HttpMock().ValidateRequestsAgainstSpec();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*UsingOpenApi*");
        }

        private static async Task<HttpResponseMessage> PostPet(HttpClient client, string body,
            string contentType = "application/json")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/pets");
            request.Content = new StringContent(body, Encoding.UTF8, contentType);

            request.Headers.Add("X-Tenant", "acme");
            return await client.SendAsync(request);
        }
    }
}
