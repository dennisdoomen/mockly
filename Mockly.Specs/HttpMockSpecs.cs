using System;
using System.Net;
using System.Net.Http;
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
                      GET https://localhost/fnv_collectiveschemes(111)

                    Closest matching mock:
                      GET https://*/fnv_collectiveschemes(123*)

                    Registered mocks:
                     - GET https://*/fnv_collectiveschemes
                     - POST https://*/fnv_collectiveschemes
                     - GET https://*/fnv_collectiveschemes(123*)
                     - GET https://*/fnv_collectiveschemes(123*) (1 custom matcher(s)) where (request => request.Uri?.Query == "?$count=1")
                     - GET https://*/fnv_collectiveschemes(456)
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
                               GET https://localhost/fnv_collectiveschemes(111)

                             Registered mocks:
                              - GET https://*/fnv_collectiveschemes
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
                .RespondsWith(_ => throw new InvalidOperationException());

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("https://localhost/api/custom");

            var request = mock.Requests.Should().ContainRequestFor("https://localhost/api*").Subject;

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
            Action assertion = () => mock.Should().HaveAllRequestsCalled();
            assertion.Should().Throw<XunitException>();

            // Perform the third call
            await client.GetAsync("https://localhost/api/times");

            // Now all mocks have been invoked the required number of times
            mock.Should().HaveAllRequestsCalled();
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
}
