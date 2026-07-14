using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using System.Net.Http.Json;
#endif
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Mockly.Specs;

public class HttpMockSpecs
{
    public class BasicUsage
    {
        [Fact]
        public async Task Can_create_basic_mock_for_get_request()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            mock.ForGet().WithPath("api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Assert
            var response = await mock.GetClient().GetAsync("https://localhost/api/test");
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Ignores_preceding_slashes_in_the_path()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Assert
            var response = await mock.GetClient().GetAsync("https://localhost/api/test");
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task The_path_must_match_exactly()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api").RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://localhost/api/test");

            // Assert
            await act
                .Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("Unexpected request to*GET https://localhost/api/test*");
        }

        [Fact]
        public async Task A_request_with_query_only_matches_a_mock_that_specifies_a_query()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/search?q=something with spaces");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("Unexpected request to:*GET https://localhost/api/search?q=something with spaces*");
        }

        [Fact]
        public async Task Can_match_any_query()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithAnyQuery()
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=something with spaces");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Supports_spaces_in_the_query()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQuery("?q=something with spaces")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=something with spaces");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task The_query_does_not_require_a_question_mark()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQuery("q=something with spaces")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=something with spaces");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_match_path_with_pipe_character()
        {
            // Arrange
            var mock = new HttpMock();
            var key = $"{Guid.NewGuid()}|{Guid.NewGuid()}";

            mock.ForDelete()
                .WithPath($"IncomeRelations/{key}")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var client = mock.GetClient();
            var response = await client.DeleteAsync($"https://localhost/IncomeRelations/{key}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_match_query_with_pipe_character()
        {
            // Arrange
            var mock = new HttpMock();
            var filter = "status=active|pending";

            mock.ForGet()
                .WithPath("api/items")
                .WithQuery($"filter={filter}")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var client = mock.GetClient();
            var response = await client.GetAsync($"https://localhost/api/items?filter={filter}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_mock_get_request_with_json_response()
        {
            // Arrange
            var mock = new HttpMock();
            var testData = new
            {
                Id = 123,
                Name = "Test"
            };

            mock.ForGet().WithPath("/api/data").RespondsWithJsonContent(testData);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await response.Should().BeEquivalentTo(new
            {
                Id = 123,
                Name = "Test"
            });
        }

        [Fact]
        public async Task Can_mock_get_request_with_json_response_and_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var testData = new
            {
                Id = 123,
                Name = "Test"
            };

            mock.ForGet().WithPath("/api/data").RespondsWithJsonContent(HttpStatusCode.Found, testData);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            await response.Should().BeEquivalentTo(new
            {
                Id = 123,
                Name = "Test"
            });
        }

        [Fact]
        public async Task Can_mock_post_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/create")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/create", new StringContent("test"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Can_mock_patch_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPatch()
                .WithPath("/api/update")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), "https://localhost/api/update")
            {
                Content = new StringContent("test")
            };

            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_mock_put_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPut()
                .WithPath("/api/update")
                .RespondsWithStatus(HttpStatusCode.Accepted);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.PutAsync("https://localhost/api/update", new StringContent("payload"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task Can_mock_delete_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForDelete()
                .WithPath("/api/delete")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.DeleteAsync("https://localhost/api/delete");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    public class WildcardSupport
    {
        [Fact]
        public async Task Supports_wildcard_in_path()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .ForHttps()
                .WithPath("/api/users/*")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response1 = await client.GetAsync("https://localhost/api/users/123");
            var response2 = await client.GetAsync("https://localhost/api/users/456");

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Supports_wildcard_in_query()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQuery("?q=*")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class CustomMatching
    {
        [Fact]
        public async Task Supports_a_predicate_for_custom_matching()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/test")
                .With(req => req.Headers.Contains("X-Custom-Header"))
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Add("X-Custom-Header", "value");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_inspect_the_request_body()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .With(req => req.Body!.Contains("something"))
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/test",
                new StringContent("a body with something in it"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Supports_async_custom_matcher_that_matches()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/async")
                .With(async req =>
                {
                    await Task.Delay(1);
                    return req.Uri!.Query == "?q=test";
                })
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/async?q=test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Supports_async_custom_matcher_that_does_not_match()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/async")
                .With(async req =>
                {
                    await Task.Delay(1);
                    return req.Uri!.Query == "?q=expected";
                })
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/async?q=other");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("Unexpected request to*GET https://localhost/api/async?q=other*");
        }
    }

    public class HeaderMatching
    {
        [Fact]
        public async Task Matches_when_the_header_is_present()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("X-Api-Key")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", "secret");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Reports_the_header_requirement_when_the_header_is_missing()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("X-Api-Key")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/test");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*header \"X-Api-Key\" is present*");
        }

        [Fact]
        public void Throws_when_the_header_name_is_null()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            Action act = () => mock.ForGet().WithHeader(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("name");
        }

        [Fact]
        public void Throws_when_the_header_name_or_value_pattern_is_null()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            Action act = () => mock.ForGet().WithHeader("X-Correlation-Id", null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("valuePattern");
        }

        [Fact]
        public async Task Matches_when_the_header_value_satisfies_the_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("X-Correlation-Id", "abc-*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Add("X-Correlation-Id", "abc-123");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Reports_the_value_requirement_when_the_header_value_does_not_match()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("X-Correlation-Id", "abc-*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Add("X-Correlation-Id", "xyz-123");

            // Act
            var act = () => client.GetAsync("https://localhost/api/test");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*header \"X-Correlation-Id\" matches \"abc-*\"*");
        }

        [Fact]
        public async Task Reports_the_value_requirement_when_the_header_is_missing()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("X-Correlation-Id", "abc-*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/test");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*header \"X-Correlation-Id\" matches \"abc-*\"*");
        }

        [Fact]
        public async Task Matches_when_any_value_of_a_multi_valued_header_satisfies_the_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithHeader("Accept", "application/json")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Add("Accept", "text/plain");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_bearer_token_by_default()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithBearerToken()
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "any-token");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_bearer_token_against_a_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithBearerToken("eyJ*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJ");

            // Act
            var response = await client.GetAsync("https://localhost/api/test");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Reports_the_bearer_token_requirement_when_the_scheme_is_not_bearer()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithBearerToken()
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNz");

            // Act
            var act = () => client.GetAsync("https://localhost/api/test");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*bearer token matches \"*\"*");
        }

        [Fact]
        public void Throws_when_the_bearer_token_pattern_is_null()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            Action act = () => mock.ForGet().WithBearerToken(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("tokenPattern");
        }

        [Fact]
        public async Task Reports_the_bearer_token_requirement_when_the_authorization_header_is_missing()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet()
                .WithPath("/api/test")
                .WithBearerToken()
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/test");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*bearer token matches \"*\"*");
        }

        [Fact]
        public async Task Matches_the_content_type_media_type_ignoring_parameters()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost()
                .WithPath("/api/test")
                .WithContentType("application/json")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync("https://localhost/api/test", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public void Throws_when_the_content_type_pattern_is_null()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            Action act = () => mock.ForPost().WithContentType(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("mediaTypePattern");
        }

        [Fact]
        public async Task Reports_the_content_type_requirement_when_the_media_type_does_not_match()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost()
                .WithPath("/api/test")
                .WithContentType("application/json")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();
            var content = new StringContent("plain", Encoding.UTF8, "text/plain");

            // Act
            var act = () => client.PostAsync("https://localhost/api/test", content);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*content type matches \"application/json\"*");
        }

        [Fact]
        public async Task Reports_the_content_type_requirement_when_the_content_type_is_missing()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost()
                .WithPath("/api/test")
                .WithContentType("application/json")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var act = () => client.PostAsync("https://localhost/api/test", null);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*content type matches \"application/json\"*");
        }
    }

    public class AdvancedMatching
    {
        [Fact]
        public async Task Can_match_body_against_a_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBody("*something*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/test",
                new StringContent("a body with something in it"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_match_a_multiline_body_against_a_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBody("*condition attribute=\"statecode\" operator=\"eq\" value=\"0\"*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/test",
                new StringContent(
                    """
                    <xml>
                    <condition attribute="statecode" operator="eq" value="0"/>
                    </xml>
                    """));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Will_report_the_wildcard()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBody("*something*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var action = async () => await client.PostAsync("https://localhost/api/mismatch",
                new StringContent("a body with something in it"));

            // Assert
            await action.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*where body matches wildcard pattern*something*");
        }

        [Fact]
        public async Task Can_match_a_multipart_batch_body_against_a_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/$batch")
                .WithBody("*fnv_managerportfoliorules*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            var multipartContent = new MultipartContent("mixed", "batch_" + Guid.NewGuid());
            multipartContent.Add(new StringContent("some fnv_managerportfoliorules query payload"));

            // Act
            var response = await client.PostAsync("https://localhost/$batch", multipartContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_match_the_body_against_a_json_string_ignoring_layout()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/json")
                .WithBodyMatchingJson("{\"name\": \"John\", \"age\": 30}")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/json", new StringContent(
                """
                {
                "name" : "John",
                "age" : 30
                }

                """));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Will_report_the_expected_json()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/json")
                .WithBodyMatchingJson("{\"name\": \"John\", \"age\": 30}")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var action = async () => await client.PostAsync("https://localhost/api/wrongroute", new StringContent(""));

            // Assert
            await action.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*body matches JSON {\"name\": \"John\", \"age\": 30}*");
        }

        [Fact]
        public async Task Throws_for_a_body_that_cannot_be_parsed_as_json()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/json")
                .WithBodyMatchingJson("{\"name\": \"John\", \"age\": 30}")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var act = () => client.PostAsync("https://localhost/api/json", new StringContent(
                """
                {
                "name" : "John",
                "age" : 30

                """));

            // Assert
            await act.Should().ThrowAsync<RequestMatchingException>().WithMessage(
                "Could not parse the request body as JSON*");
        }

        [Fact]
        public async Task Can_match_the_body_against_a_serialized_object()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/json")
                .WithBody(new { name = "John", age = 30 })
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/json", new StringContent(
                """
                {
                "name" : "John",
                "age" : 30
                }

                """));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Will_report_the_expected_json_for_serialized_object()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/json")
                .WithBody(new { name = "John", age = 30 })
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var action = async () => await client.PostAsync("https://localhost/api/json",
                new StringContent("{\"name\": \"Jane\", \"age\": 25}"));

            // Assert
            await action.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*body matches JSON*\"name\"*\"John\"*");
        }

        [Fact]
        public async Task Can_match_body_against_a_regex_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBodyMatchingRegex(".*something.*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/test",
                new StringContent("a body with something in it"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Will_report_the_regex_for_mismatching_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBodyMatchingRegex(".*something.*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var action = async () => await client.PostAsync("https://localhost/api/wrongroute",
                new StringContent("a body with something in it"));

            // Assert
            await action.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*body matches regex .*something.**");
        }

        [Fact]
        public async Task Can_prevent_the_matcher_from_prefetching_the_body()
        {
            // Arrange
            var mock = new HttpMock();
            mock.PrefetchBody = false;

            RequestInfo request = null;

            mock.ForPost()
                .WithPath("/api/test")
                .With(req =>
                {
                    request = req;
                    return true;
                })
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test",
                new StringContent("a body with something in it"));

            // Assert
            request.Should().NotBeNull();
            request.Body.Should().BeNull();
            request.RawBody.Should().BeNull();
        }

        [Fact]
        public async Task Can_use_multiple_matches_combined()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/test")
                .WithBody("*something*")
                .WithBodyMatchingRegex(".*else.*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var action = async () => await client.PostAsync("https://localhost/api/test",
                new StringContent("a body with something in it"));

            // Assert
            await action.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*body matches wildcard pattern \"*something*\" or body matches regex .*else.*");
        }
    }

    public class WhenCollectingRequests
    {
        [Fact]
        public async Task Can_collect_requests_in_collection()
        {
            // Arrange
            var mock = new HttpMock();
            var requests = new RequestCollection();

            mock.ForPatch()
                .WithPath("/api/update")
                .CollectingRequestsIn(requests)
                .RespondsWithStatus(HttpStatusCode.NoContent);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.SendAsync(new HttpRequestMessage(new HttpMethod("PATCH"), "https://localhost/api/update")
            {
                Content = new StringContent("test")
            });

            // Assert
            requests.HasUnexpectedRequests.Should().BeFalse();
            requests.IsEmpty.Should().BeFalse();
            requests.Count.Should().Be(1);
            requests.Should().ContainSingle().Which.ToString().Should().Be("PATCH https://localhost/api/update");
        }

        [Fact]
        public async Task Exposes_the_textual_body_of_a_captured_request()
        {
            // Arrange
            var mock = new HttpMock();
            var requests = new RequestCollection();

            mock.ForPost()
                .WithPath("/api/update")
                .CollectingRequestsIn(requests)
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/update",
                new StringContent("hello", Encoding.UTF8, "text/plain"));

            // Assert
            CapturedRequest capturedRequest = requests.Should().ContainSingle().Which;
            capturedRequest.Body.Should().Be("hello");
            capturedRequest.RawBody.Should().Equal("hello"u8.ToArray());
        }

        [Fact]
        public async Task Exposes_the_raw_bytes_of_a_binary_captured_request()
        {
            // Arrange
            var mock = new HttpMock();
            var requests = new RequestCollection();

            mock.ForPost()
                .WithPath("/api/update")
                .CollectingRequestsIn(requests)
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/update", new ByteArrayContent([1, 2, 3])
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/octet-stream")
                }
            });

            // Assert
            CapturedRequest capturedRequest = requests.Should().ContainSingle().Which;
            capturedRequest.Body.Should().BeNull();
            capturedRequest.RawBody.Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task Tracks_unexpected_requests()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPatch()
                .WithPath("/api/update")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            try
            {
                // Execute a GET request instead of a PATCH request
                await client.SendAsync(new HttpRequestMessage(new HttpMethod("GET"), "https://localhost/api/update"));
            }
            catch (Exception)
            {
                // We don't care
            }

            // Assert
            mock.Requests.HasUnexpectedRequests.Should().BeTrue();
            mock.Requests.Should().ContainSingle().Which.WasExpected.Should().BeFalse();
        }

        [Fact]
        public async Task All_requests_are_captured_globally()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet().ForHttp().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            mock.ForGet().ForHttps().WithPath("/api/test2").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");
            await client.GetAsync("https://localhost/api/test2");

            // Assert
            mock.Requests.Should().BeEquivalentTo([
                new
                {
                    Timestamp = Value.ThatMatches<DateTime>(t => t > DateTime.MinValue),
                    Host = "localhost",
                    Path = "/api/test",
                    Query = "",
                    Method = HttpMethod.Get,
                    Scheme = "http",
                    Uri = new Uri("http://localhost/api/test"),
                    WasExpected = true,
                },
                new
                {
                    Timestamp = Value.ThatMatches<DateTime>(t => t > DateTime.MinValue),
                    Host = "localhost",
                    Path = "/api/test2",
                    Query = "",
                    Method = HttpMethod.Get,
                    Scheme = "https",
                    Uri = new Uri("https://localhost/api/test2"),
                    WasExpected = true,
                }
            ]);
        }
    }

    public class WhenFailingOnUnexpectedCalls
    {
        [Fact]
        public async Task Throws_exception_when_unexpected_request_and_fail_on_unexpected_is_true()
        {
            // Arrange
            var mock = new HttpMock();
            mock.FailOnUnexpectedCalls = true;

            mock.ForGet().WithPath("/api/expected").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/unexpected");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Does_not_throw_when_fail_on_unexpected_is_false()
        {
            // Arrange
            var mock = new HttpMock();
            mock.FailOnUnexpectedCalls = false;

            mock.ForGet().WithPath("/api/expected").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/unexpected");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Reports_the_closest_matching_mock_when_failing()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForGet().ForHttps().WithPath("/fnv_collectiveschemes")
                .WithoutQuery()
                .RespondsWithStatus(HttpStatusCode.NotModified);

            mock
                .ForPost().ForHttps().WithPath("/fnv_collectiveschemes")
                .WithoutQuery()
                .RespondsWithStatus(HttpStatusCode.Accepted);

            mock
                .ForGet().WithPath("/fnv_collectiveschemes(123*)")
                .RespondsWithStatus(HttpStatusCode.OK);

            mock
                .ForGet().WithPath("/fnv_collectiveschemes(123*)")
                .With(request => request.Uri?.Query == "?$count=1")
                .RespondsWithStatus(HttpStatusCode.OK);

            mock
                .ForGet().WithPath("/fnv_collectiveschemes(456)")
                .RespondsWithStatus(HttpStatusCode.OK);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () => httpClient.GetAsync("https://localhost/fnv_collectiveschemes(111)");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage(
                    """
                    Unexpected request to:
                      GET https://localhost/fnv_collectiveschemes(111) with body of 0 bytes

                    Note that you can further inspect the executed requests through the HttpMock.Requests property.

                    Closest matching mock:
                      GET https://localhost:443/fnv_collectiveschemes(123*)

                    Registered mocks:
                     - GET https://localhost:443/fnv_collectiveschemes
                     - POST https://localhost:443/fnv_collectiveschemes
                     - GET https://localhost:443/fnv_collectiveschemes(123*)
                     - GET https://localhost:443/fnv_collectiveschemes(123*) where request => request.Uri?.Query == "?$count=1"
                     - GET https://localhost:443/fnv_collectiveschemes(456)
                    """);
        }

        [Fact]
        public async Task Only_reports_closest_matching_mock_when_there_are_multiple_mocks()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForGet().ForHttps().WithPath("/fnv_collectiveschemes")
                .WithoutQuery()
                .RespondsWithStatus(HttpStatusCode.NotModified);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () => httpClient.GetAsync("https://localhost/fnv_collectiveschemes(111)");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("""
                             Unexpected request to:
                               GET https://localhost/fnv_collectiveschemes(111) with body of 0 bytes

                             Note that you can further inspect the executed requests through the HttpMock.Requests property.

                             Registered mocks:
                              - GET https://localhost:443/fnv_collectiveschemes
                             """);
        }

        [Fact]
        public async Task Annotates_mocks_without_query_pattern_when_actual_request_has_query_string()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForGet().ForHttps().ForAnyHost().WithPath("/api/contacts*")
                .RespondsWithStatus(HttpStatusCode.OK);

            mock
                .ForGet().ForHttps().ForAnyHost().WithPath("/api/contacts*")
                .WithQuery("?expand=true")
                .RespondsWithStatus(HttpStatusCode.OK);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () => httpClient.GetAsync("https://localhost/api/contacts/123?unknownParam=true");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage(
                    """
                    Unexpected request to:
                      GET https://localhost/api/contacts/123?unknownParam=true with body of 0 bytes

                    Note that you can further inspect the executed requests through the HttpMock.Requests property.

                    Closest matching mock:
                      GET https://*/api/contacts*?expand=true

                    Registered mocks:
                     - GET https://*/api/contacts* (without query string)
                     - GET https://*/api/contacts*?expand=true
                    """);
        }

        [Fact]
        public async Task Does_not_annotate_mocks_when_actual_request_has_no_query_string()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForGet().ForHttps().ForAnyHost().WithPath("/api/contacts/expected")
                .RespondsWithStatus(HttpStatusCode.OK);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () => httpClient.GetAsync("https://localhost/api/contacts/123");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage(
                    """
                    Unexpected request to:
                      GET https://localhost/api/contacts/123 with body of 0 bytes

                    Note that you can further inspect the executed requests through the HttpMock.Requests property.

                    Registered mocks:
                     - GET https://*/api/contacts/expected
                    """);
        }

        [Fact]
        public async Task Includes_the_body_if_it_is_textual()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForPost().ForHttps().WithPath("/fnv_collectiveschemes")
                .WithoutQuery()
                .RespondsWithStatus(HttpStatusCode.Accepted);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () =>
                httpClient.PostAsync("https://localhost/fnv_collectiveschemes(111)", new StringContent("Some string content"));

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage(
                    """
                    Unexpected request to:
                      POST https://localhost/fnv_collectiveschemes(111) with body of 19 bytes

                    Note that you can further inspect the executed requests through the HttpMock.Requests property.

                    Registered mocks:
                     - POST https://localhost:443/fnv_collectiveschemes

                    Body (text/plain):
                      "Some string content"
                    """);
        }

        [Fact]
        public async Task Omits_the_body_if_it_is_not_textual()
        {
            // Arrange
            var mock = new HttpMock();

            mock
                .ForPost().ForHttps().WithPath("/fnv_collectiveschemes")
                .WithoutQuery()
                .RespondsWithStatus(HttpStatusCode.Accepted);

            HttpClient httpClient = mock.GetClient();

            // Act
            var act = () =>
                httpClient.PostAsync("https://localhost/fnv_collectiveschemes(111)", new ByteArrayContent([1, 2, 3])
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/octet-stream")
                    }
                });

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage(
                    """
                    Unexpected request to:
                      POST https://localhost/fnv_collectiveschemes(111) with body of 3 bytes

                    Note that you can further inspect the executed requests through the HttpMock.Requests property.

                    Registered mocks:
                     - POST https://localhost:443/fnv_collectiveschemes

                    Body (application/octet-stream):
                      (binary content)
                    """);
        }
    }

    public class WhenContinuingBuilding
    {
        [Fact]
        public async Task Can_continue_building_on_previous_builder()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .ForHttps().ForHost("somehost")
                .WithPath("/api/test")
                .WithQuery("?q=test")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Continue building using the same host and path
            mock.ForGet()
                .ForAnyHost()
                .WithQuery("?q=test2")
                .RespondsWithStatus(HttpStatusCode.NotModified);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response1 = await client.GetAsync("https://somehost/api/test?q=test");
            var response2 = await client.GetAsync("https://someotherhost/api/test?q=test2");

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.NotModified);
        }

        [Fact]
        public async Task Custom_matchers_are_never_reused()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .ForHttps().ForHost("somehost")
                .WithPath("/api/test")
                .With(request => request.Uri!.Query == "?q=test")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Continue building using the same host and path
            mock.ForGet().WithPath("/api/test")
                .WithQuery("?q=test2")
                .RespondsWithStatus(HttpStatusCode.NotModified);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response1 = await client.GetAsync("https://somehost/api/test?q=test");
            var response2 = await client.GetAsync("https://somehost/api/test?q=test2");

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.NotModified);
        }

        [Fact]
        public void Can_clear_configured_mocks()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Act
            mock.Clear();

            // No assertion needed - just verify it doesn't throw
        }

        [Fact]
        public async Task Reset_clears_previous_builder_state()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .ForHttps().ForHost("somehost")
                .WithPath("/api/test")
                .WithQuery("?q=test")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Reset should prevent reusing the previous builder's host and scheme
            mock.Reset();

            mock.ForGet()
                .WithPath("/api/test")
                .WithQuery("?q=test")
                .RespondsWithStatus(HttpStatusCode.NotModified);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var httpsResponse = await client.GetAsync("https://somehost/api/test?q=test");
            var httpResponse = await client.GetAsync("https://localhost/api/test?q=test");

            // Assert
            httpsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            httpResponse.StatusCode.Should().Be(HttpStatusCode.NotModified);
        }
    }

    public class WhenUsingDifferentResponseTypes
    {
        [Fact]
        public async Task Can_respond_with_raw_string()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/text")
                .RespondsWithContent("Hello, World!");

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/text");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Hello, World!");
        }

        [Fact]
        public async Task Can_respond_with_raw_string_and_status_code()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/api/text")
                .RespondsWithContent(HttpStatusCode.Ambiguous, "Hello, World!");

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.PostAsync("https://localhost/api/text", new StringContent("something"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Ambiguous);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Hello, World!");
        }

        [Fact]
        public async Task Can_respond_with_raw_string_status_code_and_content_type()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/text")
                .RespondsWithContent(HttpStatusCode.Ambiguous, "Hello, World!", "text/json");

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/text");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Ambiguous);

            var content = await response.Content.ReadAsStringAsync();
            content.Should().Be("Hello, World!");
        }

        [Fact]
        public async Task Can_respond_with_empty_content()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/empty")
                .RespondsWithEmptyContent();

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/empty");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_use_custom_responder()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/custom")
                .RespondsWith(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("Custom response")
                });

            // Build step removed;
            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/custom");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            content.Should().Be("Custom response");
        }

        [Fact]
        public async Task A_custom_response_can_throw_an_exception()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/custom")
                .RespondsWith((Func<RequestInfo, HttpResponseMessage>)(_ => throw new InvalidOperationException()));

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("https://localhost/api/custom");

            var request = mock.Requests.Single(r =>
                r.Uri is not null && r.Uri.ToString().StartsWith("https://localhost/api", StringComparison.OrdinalIgnoreCase));

            request.Response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            request.Response.ReasonPhrase.Should().Contain("InvalidOperationException");
        }

        [Fact]
        public async Task Can_respond_with_odata_result_envelope_with_values()
        {
            // Arrange
            var mock = new HttpMock();

            var items = new[]
            {
                new
                {
                    Id = 1,
                    Name = "A"
                },
                new
                {
                    Id = 2,
                    Name = "B"
                }
            };

            mock.ForGet()
                .WithPath("/odata/items")
                .RespondsWithODataResult(items);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/odata/items");

            // Assert
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 1,
                        Name = "A"
                    },
                    new
                    {
                        Id = 2,
                        Name = "B"
                    }
                }
            });
        }

        [Fact]
        public async Task Can_respond_with_odata_result_envelope_with_empty_values()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/odata/empty")
                .RespondsWithODataResult(Array.Empty<object>());

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/odata/empty");

            // Assert
            await response.Should().BeEquivalentTo(new
            {
                value = Array.Empty<object>()
            });
        }

        [Fact]
        public async Task Can_respond_with_odata_result_envelope_including_context()
        {
            // Arrange
            var mock = new HttpMock();

            var items = new[]
            {
                new
                {
                    Id = 1
                }
            };

            const string context = "https://localhost/$metadata#Items";

            mock.ForGet()
                .WithPath("/odata/ctx")
                .RespondsWithODataResult(HttpStatusCode.OK, items, context);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/odata/ctx");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("\"@odata.context\":\"" + context + "\"");
        }

        [Fact]
        public async Task Can_respond_with_a_single_element_that_will_be_wrapped_in_an_odata_envelope()
        {
            // Arrange
            var mock = new HttpMock();

            var item = new
            {
                Id = 1
            };

            mock.ForGet()
                .WithPath("/odata/ctx")
                .RespondsWithODataResult(HttpStatusCode.Found, item);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/odata/ctx");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Found);
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 1
                    }
                }
            });
        }
    }

    public class WhenUsingAsyncResponders
    {
        [Fact]
        public async Task An_async_responder_is_awaited()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/async")
                .RespondsWith(async _ =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent("async body")
                    };
                });

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/async");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            content.Should().Be("async body");
        }

        [Fact]
        public async Task An_async_responder_with_a_cancellation_token_is_awaited()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/async-ct")
                .RespondsWith(async (_, _) =>
                {
                    await Task.Yield();
                    return new HttpResponseMessage(HttpStatusCode.Created);
                });

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/async-ct");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task The_cancellation_token_is_passed_to_the_async_responder()
        {
            // Arrange
            var mock = new HttpMock();
            CancellationToken observedToken = default;

            mock.ForGet()
                .WithPath("/api/observe")
                .RespondsWith((_, ct) =>
                {
                    observedToken = ct;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var client = mock.GetClient();

            // Act
            await client.GetAsync("https://localhost/api/observe");

            // Assert
            observedToken.CanBeCanceled.Should().BeTrue();
        }

        [Fact]
        public async Task A_cancelled_token_is_observed_by_the_async_responder()
        {
            // Arrange
            var mock = new HttpMock();
            using var cts = new CancellationTokenSource();
            var wasCancelled = false;

            mock.ForGet()
                .WithPath("/api/cancel")
                .RespondsWith(async (_, ct) =>
                {
#if NET8_0_OR_GREATER
                    await cts.CancelAsync();
#else
                    cts.Cancel();
#endif
                    try
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        wasCancelled = true;
                        throw;
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var client = mock.GetClient();

            // Act
            Func<Task> act = () => client.GetAsync("https://localhost/api/cancel", cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
            wasCancelled.Should().BeTrue();
        }

        [Fact]
        public async Task An_async_responder_works_with_invocation_limits()
        {
            // Arrange
            var mock = new HttpMock();
            var invocations = 0;

            mock.ForGet()
                .WithPath("/api/limited")
                .RespondsWith(_ =>
                {
                    Interlocked.Increment(ref invocations);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                })
                .Once();

            var client = mock.GetClient();

            // Act
            await client.GetAsync("https://localhost/api/limited");

            // Assert
            invocations.Should().Be(1);
            mock.AllMocksInvoked.Should().BeTrue();
        }

        [Fact]
        public async Task An_async_responder_collects_requests()
        {
            // Arrange
            var mock = new HttpMock();
            var collected = new RequestCollection();

            mock.ForPost()
                .WithPath("/api/collect")
                .CollectingRequestsIn(collected)
                .RespondsWith(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/collect", new StringContent("payload"));

            // Assert
            collected.Should().ContainSingle();
        }

        [Fact]
        public async Task An_exception_from_an_async_responder_results_in_an_internal_server_error()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/async-throw")
                .RespondsWith(_ => Task.FromException<HttpResponseMessage>(new InvalidOperationException()));

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/async-throw");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            response.ReasonPhrase.Should().Contain("InvalidOperationException");
        }

        [Fact]
        public async Task An_existing_synchronous_responder_still_works()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/sync")
                .RespondsWith(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("sync body")
                });

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/sync");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            content.Should().Be("sync body");
        }

        [Fact]
        public async Task An_async_responder_receives_request_info()
        {
            // Arrange
            var mock = new HttpMock();
            RequestInfo capturedInfo = null;

            mock.ForPost()
                .WithPath("/api/info")
                .RespondsWith(async request =>
                {
                    capturedInfo = request;
                    await Task.Yield();
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/info", new StringContent("hello", Encoding.UTF8, "text/plain"));

            // Assert
            capturedInfo.Should().NotBeNull();
            capturedInfo.Method.Should().Be(HttpMethod.Post);
            capturedInfo.Uri.Should().NotBeNull();
            capturedInfo.Uri!.AbsolutePath.Should().Be("/api/info");
            capturedInfo.Body.Should().Be("hello");
            capturedInfo.RawBody.Should().Equal("hello"u8.ToArray());
        }

        [Fact]
        public async Task An_async_responder_receives_binary_request_body_as_raw_bytes()
        {
            // Arrange
            var mock = new HttpMock();
            RequestInfo capturedInfo = null;

            mock.ForPost()
                .WithPath("/api/binary-info")
                .RespondsWith(async request =>
                {
                    capturedInfo = request;
                    await Task.Yield();
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/binary-info", new ByteArrayContent([1, 2, 3])
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/octet-stream")
                }
            });

            // Assert
            capturedInfo.Should().NotBeNull();
            capturedInfo.Body.Should().BeNull();
            capturedInfo.RawBody.Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task An_async_responder_works_with_multiple_invocations()
        {
            // Arrange
            var mock = new HttpMock();
            var counter = 0;

            mock.ForGet()
                .WithPath("/api/multi")
                .RespondsWith(_ =>
                {
                    Interlocked.Increment(ref counter);
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

            var client = mock.GetClient();

            // Act
            await client.GetAsync("https://localhost/api/multi");
            await client.GetAsync("https://localhost/api/multi");
            await client.GetAsync("https://localhost/api/multi");

            // Assert
            counter.Should().Be(3);
        }

        [Fact]
        public void Null_async_responder_throws_argument_null_exception()
        {
            // Arrange
            var mock = new HttpMock();
            var builder = mock.ForGet().WithPath("/api/null");

            // Act
            Action act = () => builder.RespondsWith((Func<RequestInfo, Task<HttpResponseMessage>>)null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Null_async_responder_with_cancellation_token_throws_argument_null_exception()
        {
            // Arrange
            var mock = new HttpMock();
            var builder = mock.ForGet().WithPath("/api/null-ct");

            // Act
            Action act = () => builder.RespondsWith((Func<RequestInfo, CancellationToken, Task<HttpResponseMessage>>)null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }
    }

    public class WhenLimitingInvocations
    {
        [Fact]
        public async Task Once_allows_only_a_single_call()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/once").RespondsWithStatus(HttpStatusCode.OK).Once();

            var client = mock.GetClient();

            // Act + Assert first call succeeds
            var response1 = await client.GetAsync("https://localhost/api/once");
            response1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Second call should be unexpected (no matching non-exhausted mock)
            Func<Task> act = async () => await client.GetAsync("https://localhost/api/once");
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Times_three_requires_three_calls_for_AllMocksInvoked()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/times").RespondsWithStatus(HttpStatusCode.OK).Times(3);

            var client = mock.GetClient();

            // Act: perform 2 out of 3 required calls
            await client.GetAsync("https://localhost/api/times");
            await client.GetAsync("https://localhost/api/times");

            // Assert: not all mocks invoked (needs 3)
            Action assertion = () => mock.AllMocksInvoked.Should().BeTrue();
            assertion.Should().Throw<XunitException>();

            // Perform the third call
            await client.GetAsync("https://localhost/api/times");

            // Now all mocks have been invoked the required number of times
            mock.AllMocksInvoked.Should().BeTrue();
        }

        [Fact]
        public void Cannot_check_a_call_is_invoked_zero_times()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ForGet().WithPath("/api/times").RespondsWithStatus(HttpStatusCode.OK).Times(0);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*to less than 1*");
        }
    }

    public class WhenUsingUrlShortcuts
    {
        [Fact]
        public async Task ForGet_with_full_https_url_and_query_wildcards_matches()
        {
            var mock = new HttpMock();

            mock.ForGet("https://api.example.com/users/*?q=*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var response = await mock.GetClient().GetAsync("https://api.example.com/users/123?q=abc");

            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Can_provide_a_port_number()
        {
            var mock = new HttpMock();

            mock.ForGet("https://api.example.com:7777/users/*?q=*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var response = await mock.GetClient().GetAsync("https://api.example.com:7777/users/123?q=abc");

            response.Should().Be200Ok();
        }

        [Fact]
        public async Task ForGet_with_wildcard_host_and_path_matches()
        {
            var mock = new HttpMock();

            mock.ForGet("http://*.example.com:80/*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var response = await mock.GetClient().GetAsync("http://shop.example.com/path/to/resource");

            response.Should().Be200Ok();
        }

        [Fact]
        public async Task ForPut_with_full_https_url_and_query_wildcards_matches()
        {
            var mock = new HttpMock();

            mock.ForPut("https://api.example.com:443/users/*?q=*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var response = await mock.GetClient().PutAsync("https://api.example.com/users/42?q=term", new StringContent(""));

            response.Should().Be200Ok();
        }

        [Fact]
        public async Task ForPatch_with_wildcard_host_and_path_matches()
        {
            var mock = new HttpMock();

            mock.ForPatch("http://*.example.com/*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();
            var response = await client.SendAsync(
                new HttpRequestMessage(new HttpMethod("PATCH"), "http://admin.example.com/users/42")
                {
                    Content = new StringContent("{}")
                });

            response.Should().Be200Ok();
        }

        [Fact]
        public async Task ForDelete_without_query_in_pattern_does_not_match_request_with_query()
        {
            var mock = new HttpMock();

            mock.ForDelete("https://localhost/api/items/*")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            Func<Task> act = () => mock.GetClient().DeleteAsync("https://localhost/api/items/123?force=true");

            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("Unexpected request to*DELETE https://localhost/api/items/123?force=true*");
        }

        [Fact]
        public async Task ForPost_without_query_in_pattern_does_not_match_request_with_query()
        {
            var mock = new HttpMock();

            mock.ForPost("https://localhost/api/items")
                .RespondsWithStatus(HttpStatusCode.Created);

            Func<Task> act = () => mock.GetClient().PostAsync("https://localhost/api/items?x=1", new StringContent(""));

            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("Unexpected request to*POST https://localhost/api/items?x=1*");
        }
    }

    public class WhenCreatingHttpClientFactory
    {
        [Fact]
        public async Task GetClientFactory_creates_clients_using_the_mock_handler()
        {
            var mock = new HttpMock();
            mock.ForGet().WithPath("/ping").RespondsWithStatus(HttpStatusCode.OK);

            var factory = mock.GetClientFactory();
            var client = factory.CreateClient("any");

            var response = await client.GetAsync("https://localhost/ping");

            response.Should().Be200Ok();
        }
    }

    public class WhenGettingMessageHandler
    {
        [Fact]
        public async Task Can_return_a_message_handler_that_intercepts_requests()
        {
            var mock = new HttpMock();
            mock.ForGet().WithPath("/ping").RespondsWithStatus(HttpStatusCode.OK);

            var handler = mock.GetMessageHandler();

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost/")
            };

            var response = await client.GetAsync("/ping");

            response.Should().Be200Ok();
        }
    }

    public class RespondingWithHttpContent
    {
        [Fact]
        public async Task Can_respond_with_http_content_using_default_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var content = new StringContent("Test content");

            mock.ForGet()
                .WithPath("/api/test")
                .RespondsWith(content);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/test");
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            responseContent.Should().Be("Test content");
        }

        [Fact]
        public async Task Can_respond_with_http_content_using_custom_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var content = new StringContent("Test content");

            mock.ForGet()
                .WithPath("/api/test")
                .RespondsWith(HttpStatusCode.Accepted, content);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/test");
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            responseContent.Should().Be("Test content");
        }

        [Fact]
        public async Task Can_respond_with_multipart_content()
        {
            // Arrange
            var mock = new HttpMock();

            var inner = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[{\"Count\":42}],\"@Microsoft.Dynamics.CRM.morerecords\":false}")
            };

            var multipart = new MultipartContent("mixed", $"batchresponse_{Guid.NewGuid()}");
            multipart.Add(new HttpMessageContent(inner));

            mock.ForGet()
                .WithPath("/api/batch")
                .RespondsWith(HttpStatusCode.OK, multipart);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/batch");
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Should().BeOfType<MultipartContent>();
            responseContent.Should().Contain("Count");
            responseContent.Should().Contain("42");
        }

        [Fact]
        public async Task Demonstrates_simplification_from_issue()
        {
            // This test demonstrates the simplification requested in the issue
            // Before: Had to use RespondsWith(_ => new HttpResponseMessage(...) { Content = ... })
            // After: Can directly use RespondsWith(statusCode, content)

            // Arrange
            var mock = new HttpMock();
            const int count = 5;

            // Create complex nested content as shown in the issue
            var inner = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{\"value\":[{{\"Count\":{count}}}],\"@Microsoft.Dynamics.CRM.morerecords\":false}}",
                    Encoding.UTF8,
                    "application/json")
            };

            var multipart = new MultipartContent("mixed", $"batchresponse_{Guid.NewGuid()}");
            multipart.Add(new HttpMessageContent(inner));

            // Simplified API - no need for lambda
            mock.ForGet()
                .WithPath("/api/dynamics/batch")
                .RespondsWith(HttpStatusCode.OK, multipart);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/dynamics/batch");
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Should().BeOfType<MultipartContent>();
            responseContent.Should().Contain($"\"Count\":{count}");
            responseContent.Should().Contain("@Microsoft.Dynamics.CRM.morerecords");
        }

        [Fact]
        public async Task Can_respond_with_byte_array_content()
        {
            // Arrange
            var mock = new HttpMock();
            byte[] bytes = "Hello"u8.ToArray(); // "Hello" in ASCII
            var content = new ByteArrayContent(bytes);

            mock.ForGet()
                .WithPath("/api/binary")
                .RespondsWith(content);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/binary");
            var responseBytes = await response.Content.ReadAsByteArrayAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            responseBytes.Should().Equal(bytes);
        }

        [Fact]
        public async Task Can_respond_with_stream_content()
        {
            // Arrange
            var mock = new HttpMock();
            var stream = new MemoryStream([1, 2, 3, 4, 5]);
            var content = new StreamContent(stream);

            mock.ForGet()
                .WithPath("/api/stream")
                .RespondsWith(HttpStatusCode.Created, content);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/stream");
            var responseBytes = await response.Content.ReadAsByteArrayAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            responseBytes.Should().Equal(1, 2, 3, 4, 5);
        }
    }

    public class ResponseBuilderUsage
    {
        // Simple test data builder implementation
        private class UserBuilder : IResponseBuilder<object>
        {
            private int id = 1;
            private string name = "Default";

            public UserBuilder WithId(int id)
            {
                this.id = id;
                return this;
            }

            public UserBuilder WithName(string name)
            {
                this.name = name;
                return this;
            }

            public object Build()
            {
                return new
                {
                    Id = id,
                    Name = name
                };
            }
        }

        [Fact]
        public async Task Can_use_response_builder_with_json_content()
        {
            // Arrange
            var mock = new HttpMock();
            var userBuilder = new UserBuilder()
                .WithId(123)
                .WithName("John Doe");

            mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(userBuilder);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/user");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await response.Should().BeEquivalentTo(new
            {
                Id = 123,
                Name = "John Doe"
            });
        }

        [Fact]
        public async Task Can_use_response_builder_with_json_content_and_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var userBuilder = new UserBuilder()
                .WithId(456)
                .WithName("Jane Smith");

            mock.ForGet().WithPath("/api/user").RespondsWithJsonContent(HttpStatusCode.Created, userBuilder);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/user");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            await response.Should().BeEquivalentTo(new
            {
                Id = 456,
                Name = "Jane Smith"
            });
        }

        [Fact]
        public async Task Can_use_response_builder_with_odata_result_single_item()
        {
            // Arrange
            var mock = new HttpMock();
            var userBuilder = new UserBuilder()
                .WithId(789)
                .WithName("Bob Johnson");

            mock.ForGet().WithPath("/odata/user").RespondsWithODataResult(userBuilder);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/odata/user");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 789,
                        Name = "Bob Johnson"
                    }
                }
            });
        }

        [Fact]
        public async Task Can_use_response_builder_with_odata_result_and_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var userBuilder = new UserBuilder()
                .WithId(999)
                .WithName("Alice Brown");

            mock.ForGet().WithPath("/odata/user").RespondsWithODataResult(HttpStatusCode.Accepted, userBuilder);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/odata/user");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 999,
                        Name = "Alice Brown"
                    }
                }
            });
        }

        [Fact]
        public async Task Can_use_response_builders_with_odata_result_collection()
        {
            // Arrange
            var mock = new HttpMock();
            var builders = new[]
            {
                new UserBuilder().WithId(1).WithName("User 1"),
                new UserBuilder().WithId(2).WithName("User 2"),
                new UserBuilder().WithId(3).WithName("User 3")
            };

            mock.ForGet().WithPath("/odata/users").RespondsWithODataResult(builders);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/odata/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 1,
                        Name = "User 1"
                    },
                    new
                    {
                        Id = 2,
                        Name = "User 2"
                    },
                    new
                    {
                        Id = 3,
                        Name = "User 3"
                    }
                }
            });
        }

        [Fact]
        public async Task Can_use_response_builders_with_odata_result_collection_and_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            var builders = new[]
            {
                new UserBuilder().WithId(10).WithName("Admin"),
                new UserBuilder().WithId(20).WithName("Manager")
            };

            mock.ForGet().WithPath("/odata/staff").RespondsWithODataResult(HttpStatusCode.PartialContent, builders);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/odata/staff");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 10,
                        Name = "Admin"
                    },
                    new
                    {
                        Id = 20,
                        Name = "Manager"
                    }
                }
            });
        }

        [Fact]
        public async Task Can_use_response_builders_with_odata_result_and_context()
        {
            // Arrange
            var mock = new HttpMock();
            var builders = new[]
            {
                new UserBuilder().WithId(100).WithName("Context User")
            };

            const string context = "https://localhost/$metadata#Users";

            mock.ForGet().WithPath("/odata/users").RespondsWithODataResult(HttpStatusCode.OK, builders, context);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/odata/users");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("\"@odata.context\":\"" + context + "\"");
            await response.Should().BeEquivalentTo(new
            {
                value = new[]
                {
                    new
                    {
                        Id = 100,
                        Name = "Context User"
                    }
                }
            });
        }
    }

    public class WhenUsingCustomJsonSerializerOptions
    {
        [Fact]
        public async Task Can_use_custom_options_for_json_response_content()
        {
            // Arrange
            var mock = new HttpMock();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            mock.ForGet()
                .WithPath("/api/data")
                .Using(options)
                .RespondsWithJsonContent(new { UserId = 42, UserName = "Alice" });

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("\"userId\"").And.Contain("\"userName\"");
            body.Should().NotContain("\"UserId\"").And.NotContain("\"UserName\"");
        }

        [Fact]
        public async Task Can_use_custom_options_to_match_request_body_as_object()
        {
            // Arrange
            var mock = new HttpMock();

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            mock.ForPost()
                .WithPath("/api/data")
                .Using(options)
                .WithBody(new { UserId = 42, UserName = "Alice" })
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act: Send camelCase JSON (matching what camelCase options would serialize to)
            var response = await client.PostAsync("https://localhost/api/data",
                new StringContent("{\"userId\":42,\"userName\":\"Alice\"}"));

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    /// <summary>
    /// Tests for concurrency issues.
    /// </summary>
    /// <remarks>
    /// Only really proves that parallelism didn't work sometimes.
    /// But doesn't necessarily prove that everything is thread safe now.
    /// This test had to be run ~10 times without thread safety fixes before a race condition occurred.
    /// </remarks>
    public class WhenInMultiThreadedContext
    {
#if NET6_0_OR_GREATER
        [Fact]
        public async Task Can_handle_parallel_scenario()
        {
            // Arrange
            var mock = new HttpMock();

            var testData = new TestData
            {
                Id = 123,
                Name = "Test"
            };

            mock.ForGet().WithPath("/api/data").RespondsWithJsonContent(testData);

            // Act
            ConcurrentBag<TestData> responses = [];

            var options = new ParallelOptions
            {
                CancellationToken = new CancellationToken(canceled: false),
                MaxDegreeOfParallelism = 50,
            };

            var client = mock.GetClient();

            await Parallel.ForAsync(0, 1000, options, async (_, token) =>
            {
                var response = await client.GetFromJsonAsync<TestData>("https://localhost/api/data", token);
                responses.Add(response);
            });

            // Assert
            responses.Count.Should().Be(1000);
            responses.Should().AllBeEquivalentTo(testData);
        }

        private class TestData
        {
            public int Id { get; init; }

            public string Name { get; init; }
        }
#endif
    }

    public class ResponseHeaders
    {
        [Fact]
        public async Task Adds_a_standard_response_header()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("api/users")
                .RespondsWithStatus(HttpStatusCode.Created)
                .WithHeader("Location", "/api/users/123");

            // Act
            var response = await mock.GetClient().PostAsync("https://localhost/api/users", new StringContent(""));

            // Assert
            response.Headers.Location.Should().Be(new Uri("/api/users/123", UriKind.Relative));
        }

        [Fact]
        public async Task Routes_a_content_header_to_the_content_headers()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithContent("hello")
                .WithHeader("Content-Type", "application/xml");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        }

        [Fact]
        public async Task Creates_empty_content_when_a_content_header_is_set_without_content()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Content.Should().NotBeNull();
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        }

        [Fact]
        public async Task Adds_a_multi_value_response_header()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("X-Custom", "one", "two");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Headers.GetValues("X-Custom").Should().BeEquivalentTo("one", "two");
        }

        [Fact]
        public async Task Adds_a_non_standard_header_using_try_add_without_validation()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("X-Rate-Limit", "100");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Headers.GetValues("X-Rate-Limit").Should().ContainSingle().Which.Should().Be("100");
        }

        [Fact]
        public async Task Applies_the_header_on_every_invocation()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("ETag", "\"v1\"");

            // Act
            var first = await mock.GetClient().GetAsync("https://localhost/api/data");
            var second = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            first.Headers.ETag!.Tag.Should().Be("\"v1\"");
            second.Headers.ETag!.Tag.Should().Be("\"v1\"");
        }

        [Fact]
        public async Task Supports_chaining_multiple_headers()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("ETag", "\"v1\"")
                .WithHeader("Cache-Control", "no-cache");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Headers.ETag!.Tag.Should().Be("\"v1\"");
            response.Headers.CacheControl!.NoCache.Should().BeTrue();
        }

        [Fact]
        public async Task Applies_headers_to_every_response_in_a_sequence()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.Accepted)
                .ThenRespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("X-Sequence", "yes");

            // Act
            var first = await mock.GetClient().GetAsync("https://localhost/api/data");
            var second = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            first.Headers.GetValues("X-Sequence").Should().ContainSingle().Which.Should().Be("yes");
            second.Headers.GetValues("X-Sequence").Should().ContainSingle().Which.Should().Be("yes");
        }

        [Fact]
        public async Task The_last_configured_value_wins_when_a_header_is_set_twice()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("X-Custom", "first")
                .WithHeader("X-Custom", "second");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Headers.GetValues("X-Custom").Should().ContainSingle().Which.Should().Be("second");
        }

        [Fact]
        public void Rejects_an_empty_header_name_at_configuration_time()
        {
            // Arrange
            var mock = new HttpMock();
            var builder = mock.ForGet().WithPath("api/data").RespondsWithStatus(HttpStatusCode.OK);

            // Act
            Action act = () => builder.WithHeader(" ", "value");

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task Is_not_affected_by_mutating_the_values_array_after_configuration()
        {
            // Arrange
            var mock = new HttpMock();
            var values = new[] { "original" };
            mock.ForGet().WithPath("api/data")
                .RespondsWithStatus(HttpStatusCode.OK)
                .WithHeader("X-Custom", values);

            // Act
            values[0] = "mutated";
            var response = await mock.GetClient().GetAsync("https://localhost/api/data");

            // Assert
            response.Headers.GetValues("X-Custom").Should().ContainSingle().Which.Should().Be("original");
        }
    }

    public class GenericAndAdditionalVerbs
    {
        [Fact]
        public async Task Can_mock_head_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForHead()
                .WithPath("/api/resource")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head, "https://localhost/api/resource");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_mock_head_request_using_url_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForHead("https://localhost/api/resource")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head, "https://localhost/api/resource");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_mock_options_request()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForOptions()
                .WithPath("/api/resource")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/resource");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_mock_options_request_using_url_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForOptions("https://localhost/api/resource")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/resource");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_mock_request_using_generic_for_with_standard_method()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(HttpMethod.Get)
                .WithPath("/api/data")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/data");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_mock_request_using_generic_for_with_custom_method()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(new HttpMethod("PROPFIND"))
                .WithPath("/api/dav")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), "https://localhost/api/dav");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Generic_for_does_not_match_a_different_method()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(new HttpMethod("PROPFIND"))
                .WithPath("/api/dav")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/dav");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Can_mock_request_using_generic_for_with_url_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(HttpMethod.Options, "https://localhost/api/resource")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Options, "https://localhost/api/resource");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task New_verbs_reuse_scheme_and_host_from_previous_builder()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .ForHttps().ForHost("somehost")
                .WithPath("/api/test")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Reuse the previous builder's scheme and host for an OPTIONS mock
            mock.ForOptions()
                .WithPath("/api/test")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var client = mock.GetClient();

            // Act
            var getResponse = await client.GetAsync("https://somehost/api/test");
            var optionsRequest = new HttpRequestMessage(HttpMethod.Options, "https://somehost/api/test");
            var optionsResponse = await client.SendAsync(optionsRequest);

            // Assert
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            optionsResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Generic_for_with_schemeless_url_pattern_matches_any_scheme()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(HttpMethod.Get, "localhost/api/data")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/data");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Generic_for_with_host_only_url_pattern_matches()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(HttpMethod.Head, "https://localhost")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var request = new HttpRequestMessage(HttpMethod.Head, "https://localhost/");
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Generic_for_with_host_and_query_url_pattern_matches()
        {
            // Arrange
            var mock = new HttpMock();

            mock.For(HttpMethod.Get, "https://localhost?q=1")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/?q=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class SequencedResponses
    {
        [Fact]
        public async Task The_sequence_advances_with_each_call()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.ServiceUnavailable)
                .ThenRespondsWithStatus(HttpStatusCode.ServiceUnavailable)
                .ThenRespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");
            var third = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            second.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            third.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task The_last_response_repeats_once_the_sequence_is_exhausted()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.InternalServerError)
                .ThenRespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");
            var third = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            third.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task A_single_response_keeps_repeating()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource").RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Sequenced_json_responses_produce_different_bodies()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithJsonContent(new { Page = 1 })
                .ThenRespondsWithJsonContent(new { Page = 2 });

            var client = mock.GetClient();

            // Act
            var firstBody = await (await client.GetAsync("https://localhost/resource")).Content.ReadAsStringAsync();
            var secondBody = await (await client.GetAsync("https://localhost/resource")).Content.ReadAsStringAsync();

            // Assert
            firstBody.Should().Contain("\"Page\":1");
            secondBody.Should().Contain("\"Page\":2");
        }

        [Fact]
        public async Task A_sequence_can_be_combined_with_collecting_requests()
        {
            // Arrange
            var mock = new HttpMock();
            var requests = new RequestCollection();
            mock.ForGet().WithPath("/resource")
                .CollectingRequestsIn(requests)
                .RespondsWithStatus(HttpStatusCode.ServiceUnavailable)
                .ThenRespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            requests.Should().HaveCount(2);
            requests.ElementAt(0).Response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            requests.ElementAt(1).Response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task A_sequence_combined_with_Times_stops_matching_after_the_limit()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.ServiceUnavailable)
                .ThenRespondsWithStatus(HttpStatusCode.OK)
                .Times(2);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");
            var third = await client.GetAsync("https://localhost/resource");
            Func<Task> fourth = () => client.GetAsync("https://localhost/resource");

            // Assert
            // Times(2) applies to the last appended response (OK), so the sequence is:
            // 1× ServiceUnavailable (promoted to count=1 when ThenXXX was called), then 2× OK, then exhausted.
            first.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            third.StatusCode.Should().Be(HttpStatusCode.OK);
            await fourth.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task A_custom_responder_can_be_appended_to_the_sequence()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.Accepted)
                .ThenRespondsWith(_ => new HttpResponseMessage(HttpStatusCode.Created));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.Accepted);
            second.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public void The_responder_property_still_exposes_the_first_response()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.BadGateway)
                .ThenRespondsWithStatus(HttpStatusCode.OK);

            // Act
            var uninvoked = mock.GetUninvokedMocks().First();
            var request = new RequestInfo(new HttpRequestMessage(HttpMethod.Get, "https://localhost/resource"), rawBody: null);
            var firstResponse = uninvoked.Responder(request);

            // Assert
            firstResponse.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        }

        [Fact]
        public async Task Then_with_http_content_serves_that_content_as_the_next_response()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWith(new StringContent("hello"));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Be("hello");
        }

        [Fact]
        public async Task Then_with_status_and_http_content_serves_that_status_and_content()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWith(HttpStatusCode.Created, new StringContent("created"));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.Created);
            (await second.Content.ReadAsStringAsync()).Should().Be("created");
        }

        [Fact]
        public async Task An_appended_json_response_using_a_builder_produces_the_serialized_body()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithJsonContent(new ItemBuilder().WithId(42));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"Id\":42");
        }

        [Fact]
        public async Task An_appended_json_response_using_a_builder_can_use_a_custom_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithJsonContent(HttpStatusCode.Accepted, new ItemBuilder().WithId(7));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"Id\":7");
        }

        [Fact]
        public async Task An_appended_odata_response_with_a_single_object_wraps_it_in_an_envelope()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(new { Id = 1 });

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"value\"");
        }

        [Fact]
        public async Task An_appended_odata_response_with_a_collection_wraps_it_in_an_envelope()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult([new { Id = 1 }, new { Id = 2 }]);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"value\"");
        }

        [Fact]
        public async Task An_appended_odata_response_can_include_the_odata_context()
        {
            // Arrange
            const string context = "https://localhost/$metadata#Items";
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(HttpStatusCode.OK, [new { Id = 1 }], context);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await second.Content.ReadAsStringAsync()).Should().Contain("@odata.context").And.Contain(context);
        }

        [Fact]
        public async Task An_appended_odata_response_using_a_single_builder_wraps_it_in_an_envelope()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(new ItemBuilder().WithId(99));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"Id\":99");
        }

        [Fact]
        public async Task An_appended_odata_response_using_a_builder_collection_wraps_them_in_an_envelope()
        {
            // Arrange
            var builders = new[]
            {
                new ItemBuilder().WithId(10),
                new ItemBuilder().WithId(20)
            };
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(HttpStatusCode.OK, builders);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await second.Content.ReadAsStringAsync();
            body.Should().Contain("\"Id\":10").And.Contain("\"Id\":20");
        }

        [Fact]
        public async Task An_appended_odata_response_using_a_builder_collection_can_include_the_odata_context()
        {
            // Arrange
            const string context = "https://localhost/$metadata#Items";
            var builders = new[] { new ItemBuilder().WithId(5) };
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(HttpStatusCode.OK, builders, context);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await second.Content.ReadAsStringAsync()).Should().Contain("@odata.context").And.Contain(context);
        }

        [Fact]
        public async Task An_appended_odata_response_can_use_a_custom_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(HttpStatusCode.Accepted, new { Id = 3 });

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task An_appended_odata_response_using_a_builder_can_use_a_custom_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(HttpStatusCode.Created, new ItemBuilder().WithId(11));

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task An_appended_odata_response_using_enumerable_builders_wraps_them_in_an_envelope()
        {
            // Arrange
            IEnumerable<ItemBuilder> builders = [new ItemBuilder().WithId(1), new ItemBuilder().WithId(2)];
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithODataResult(builders);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"value\"");
        }

        [Fact]
        public async Task An_appended_string_content_response_serves_that_body()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithContent("{\"ok\":true}");

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            (await second.Content.ReadAsStringAsync()).Should().Contain("\"ok\"");
        }

        [Fact]
        public async Task An_appended_string_content_response_can_use_a_custom_status_and_content_type()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.NotFound)
                .ThenRespondsWithContent(HttpStatusCode.Accepted, "<root/>", "application/xml");

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
            second.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        }

        [Fact]
        public async Task An_appended_empty_content_response_uses_no_content_status_by_default()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.OK)
                .ThenRespondsWithEmptyContent();

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task An_appended_empty_content_response_can_use_a_custom_status_code()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/resource")
                .RespondsWithStatus(HttpStatusCode.OK)
                .ThenRespondsWithEmptyContent(HttpStatusCode.Accepted);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/resource");
            var second = await client.GetAsync("https://localhost/resource");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        private class ItemBuilder : IResponseBuilder<object>
        {
            private int id;

            public ItemBuilder WithId(int value)
            {
                id = value;
                return this;
            }

            public object Build() => new { Id = id };
        }
    }

#nullable enable
    public class WhenRespondingWithProblemDetails
    {
        [Fact]
        public async Task Uses_the_problem_json_content_type()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/users/999")
                .RespondsWithProblemDetails(HttpStatusCode.NotFound);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/users/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        }

        [Fact]
        public async Task Defaults_the_title_to_the_reason_phrase_and_includes_the_status()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/users/999")
                .RespondsWithProblemDetails(HttpStatusCode.NotFound);

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/users/999");

            // Assert
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            document.RootElement.GetProperty("title").GetString().Should().Be("Not Found");
            document.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        }

        [Fact]
        public async Task Uses_the_supplied_title_detail_type_and_instance()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/users/999")
                .RespondsWithProblemDetails(
                    HttpStatusCode.NotFound,
                    title: "User not found",
                    detail: "No user exists with id 999",
                    type: "https://example.com/problems/not-found",
                    instance: "/api/users/999");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/users/999");

            // Assert
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            root.GetProperty("title").GetString().Should().Be("User not found");
            root.GetProperty("detail").GetString().Should().Be("No user exists with id 999");
            root.GetProperty("type").GetString().Should().Be("https://example.com/problems/not-found");
            root.GetProperty("instance").GetString().Should().Be("/api/users/999");
        }

        [Fact]
        public async Task Serializes_extension_members()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/users/999")
                .RespondsWithProblemDetails(
                    HttpStatusCode.BadRequest,
                    extensions: new Dictionary<string, object?>
                    {
                        ["traceId"] = "00-abc-def-01",
                        ["errors"] = new[] { "name is required" }
                    });

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/users/999");

            // Assert
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            root.GetProperty("traceId").GetString().Should().Be("00-abc-def-01");
            root.GetProperty("errors")[0].GetString().Should().Be("name is required");
        }

        [Fact]
        public async Task Works_with_times()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/users/999")
                .RespondsWithProblemDetails(HttpStatusCode.NotFound, title: "User not found")
                .Times(2);

            var client = mock.GetClient();

            // Act
            var first = await client.GetAsync("https://localhost/api/users/999");
            var second = await client.GetAsync("https://localhost/api/users/999");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.NotFound);
            second.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            mock.AllMocksInvoked.Should().BeTrue();
        }
    }
#nullable restore

    public class InvocationTracking
    {
        [Fact]
        public async Task Configured_mock_exposes_its_invocation_count_after_calls()
        {
            // Arrange
            var mock = new HttpMock();
            var responseBuilder = mock.ForGet().WithPath("api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Act
            await mock.GetClient().GetAsync("https://localhost/api/test");
            await mock.GetClient().GetAsync("https://localhost/api/test");

            // Assert
            responseBuilder.RequestMock.InvocationCount.Should().Be(2);
        }

        [Fact]
        public void Configured_mock_exposes_its_invocation_limit()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var responseBuilder = mock.ForGet().WithPath("api/test").RespondsWithStatus(HttpStatusCode.OK).Twice();

            // Assert
            responseBuilder.RequestMock.MaxInvocations.Should().Be(2);
        }
    }

    public class RespondingWithBinaryPayloads
    {
        [Fact]
        public async Task Can_respond_with_a_file_inferring_the_content_type_from_the_extension()
        {
            // Arrange
            byte[] payload = { 1, 2, 3, 4, 5 };
            string path = Path.ChangeExtension(Path.GetTempFileName(), ".pdf");
            File.WriteAllBytes(path, payload);

            try
            {
                var mock = new HttpMock();
                mock.ForGet().WithPath("/api/file").RespondsWithFile(path);

                // Act
                using var response = await mock.GetClient().GetAsync("https://localhost/api/file");
                byte[] downloaded = await response.Content.ReadAsByteArrayAsync();

                // Assert
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                downloaded.Should().Equal(payload);
                response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Can_respond_with_a_file_using_an_explicit_content_type()
        {
            // Arrange
            byte[] payload = { 10, 20, 30 };
            string path = Path.ChangeExtension(Path.GetTempFileName(), ".unknownext");
            File.WriteAllBytes(path, payload);

            try
            {
                var mock = new HttpMock();
                mock.ForGet().WithPath("/api/file").RespondsWithFile(path, "image/png");

                // Act
                using var response = await mock.GetClient().GetAsync("https://localhost/api/file");
                byte[] downloaded = await response.Content.ReadAsByteArrayAsync();

                // Assert
                downloaded.Should().Equal(payload);
                response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Can_respond_with_an_unknown_extension_using_the_default_content_type()
        {
            // Arrange
            byte[] payload = { 7, 7, 7 };
            string path = Path.ChangeExtension(Path.GetTempFileName(), ".weirdext");
            File.WriteAllBytes(path, payload);

            try
            {
                var mock = new HttpMock();
                mock.ForGet().WithPath("/api/file").RespondsWithFile(path);

                // Act
                using var response = await mock.GetClient().GetAsync("https://localhost/api/file");

                // Assert
                response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Opens_the_file_freshly_for_every_invocation()
        {
            // Arrange
            byte[] payload = { 42, 43, 44, 45 };
            string path = Path.ChangeExtension(Path.GetTempFileName(), ".bin");
            File.WriteAllBytes(path, payload);

            try
            {
                var mock = new HttpMock();
                mock.ForGet().WithPath("/api/file").RespondsWithFile(path);
                var client = mock.GetClient();

                // Act
                using var firstResponse = await client.GetAsync("https://localhost/api/file");
                byte[] first = await firstResponse.Content.ReadAsByteArrayAsync();

                using var secondResponse = await client.GetAsync("https://localhost/api/file");
                byte[] second = await secondResponse.Content.ReadAsByteArrayAsync();

                // Assert
                first.Should().Equal(payload);
                second.Should().Equal(payload);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task Can_respond_with_raw_bytes()
        {
            // Arrange
            byte[] payload = { 100, 101, 102 };
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/bytes").RespondsWithBytes(payload, "application/octet-stream");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/bytes");
            byte[] downloaded = await response.Content.ReadAsByteArrayAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            downloaded.Should().Equal(payload);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        }

        [Fact]
        public async Task Buffered_bytes_can_be_served_for_multiple_invocations()
        {
            // Arrange
            byte[] payload = { 5, 6, 7, 8 };
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/bytes").RespondsWithBytes(payload, "application/octet-stream");
            var client = mock.GetClient();

            // Act
            byte[] first = await (await client.GetAsync("https://localhost/api/bytes")).Content.ReadAsByteArrayAsync();
            byte[] second = await (await client.GetAsync("https://localhost/api/bytes")).Content.ReadAsByteArrayAsync();

            // Assert
            first.Should().Equal(payload);
            second.Should().Equal(payload);
        }

        [Fact]
        public async Task Can_respond_with_a_stream()
        {
            // Arrange
            byte[] payload = { 200, 201, 202 };
            var stream = new MemoryStream(payload);
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/stream").RespondsWithStream(stream, "application/pdf");

            // Act
            var response = await mock.GetClient().GetAsync("https://localhost/api/stream");
            byte[] downloaded = await response.Content.ReadAsByteArrayAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            downloaded.Should().Equal(payload);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        }

        [Fact]
        public async Task A_stream_response_can_only_be_consumed_once()
        {
            // Arrange
            byte[] payload = { 1, 2, 3 };
            var stream = new ForwardOnlyStream(payload);
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/stream").RespondsWithStream(stream, "application/octet-stream");
            var client = mock.GetClient();

            // Act
            using var firstResponse = await client.GetAsync(
                "https://localhost/api/stream", HttpCompletionOption.ResponseHeadersRead);
            using var firstBody = await firstResponse.Content.ReadAsStreamAsync();
            using var firstBuffer = new MemoryStream();
            await firstBody.CopyToAsync(firstBuffer);

            using var secondResponse = await client.GetAsync(
                "https://localhost/api/stream", HttpCompletionOption.ResponseHeadersRead);
            using var secondBody = await secondResponse.Content.ReadAsStreamAsync();
            using var secondBuffer = new MemoryStream();
            await secondBody.CopyToAsync(secondBuffer);

            // Assert
            firstBuffer.ToArray().Should().Equal(payload);
            secondBuffer.ToArray().Should().BeEmpty();
        }

        private sealed class ForwardOnlyStream : Stream
        {
            private readonly MemoryStream inner;

            public ForwardOnlyStream(byte[] data)
            {
                inner = new MemoryStream(data);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }

    public class QueryParameterMatching
    {
        [Fact]
        public async Task Matches_a_query_parameter_regardless_of_order()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "mockly")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?page=2&q=mockly");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_query_parameter_when_unrelated_parameters_are_present()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "mockly")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=mockly&sort=desc&page=3");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_query_parameter_value_using_a_wildcard()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "moc*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=mockly");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_query_parameter_by_presence_only()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=anything&page=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Does_not_match_when_the_query_parameter_is_absent()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/search?page=1");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*query parameter \"q\" is present*");
        }

        [Fact]
        public async Task Does_not_match_when_the_query_parameter_value_differs()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "mockly")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var act = () => client.GetAsync("https://localhost/api/search?q=other");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*query parameter \"q\" matches \"mockly\"*");
        }

        [Fact]
        public async Task Matches_a_query_parameter_value_in_its_entirety()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "mockly")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act: a value that merely contains the pattern must not match
            var act = () => client.GetAsync("https://localhost/api/search?q=mockly-extended");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*query parameter \"q\" matches \"mockly\"*");
        }

        [Fact]
        public async Task Can_combine_multiple_query_parameters()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQueryParam("q", "mockly")
                .WithQueryParam("page", "2")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?page=2&q=mockly");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_combine_a_query_parameter_with_a_full_query_pattern()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet()
                .WithPath("/api/search")
                .WithQuery("?q=mockly&page=2")
                .WithQueryParam("page", "2")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            // Act
            var response = await client.GetAsync("https://localhost/api/search?q=mockly&page=2");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class FormFieldMatching
    {
        [Fact]
        public async Task Matches_a_form_field_regardless_of_order()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/oauth/token")
                .WithFormField("grant_type", "client_credentials")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("client_id", "abc"),
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
            ]);

            // Act
            var response = await client.PostAsync("https://localhost/oauth/token", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Matches_a_form_field_value_using_a_wildcard()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/oauth/token")
                .WithFormField("scope", "read*")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("scope", "read write"),
            ]);

            // Act
            var response = await client.PostAsync("https://localhost/oauth/token", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Does_not_match_when_the_form_field_value_differs()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForPost()
                .WithPath("/oauth/token")
                .WithFormField("grant_type", "client_credentials")
                .RespondsWithStatus(HttpStatusCode.OK);

            var client = mock.GetClient();

            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "password"),
            ]);

            // Act
            var act = () => client.PostAsync("https://localhost/oauth/token", content);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>()
                .WithMessage("*form field \"grant_type\" matches \"client_credentials\"*");
        }
    }
}

