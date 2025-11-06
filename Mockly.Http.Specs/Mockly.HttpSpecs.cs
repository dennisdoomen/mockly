using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mockly.Http.Specs;

public class MocklyHttpSpecs
{
    public class WhenBuildingMocks
    {
        [Fact]
        public void Can_create_basic_mock_for_get_request()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            // Act
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Assert
            client.Should().NotBeNull();
        }

        [Fact]
        public async Task Can_mock_get_request_with_json_response()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            var testData = new { Id = 123, Name = "Test" };
            
            builder.ForGet()
                .ForPath("/api/data")
                .RespondsWithJsonContent(testData);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/data");
            var content = await response.Content.ReadAsStringAsync();
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("123");
            content.Should().Contain("Test");
        }

        [Fact]
        public async Task Can_mock_post_request()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForPost()
                .ForPath("/api/create")
                .RespondsWithStatus(HttpStatusCode.Created);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.PostAsync("http://localhost/api/create", new StringContent("test"));
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Can_mock_patch_request()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForPatch()
                .ForPath("/api/update")
                .RespondsWithStatus(HttpStatusCode.NoContent);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), "http://localhost/api/update")
            {
                Content = new StringContent("test")
            };
            var response = await client.SendAsync(request);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    public class WhenUsingWildcards
    {
        [Fact]
        public async Task Supports_wildcard_in_path()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .ForPath("/api/users/*")
                .RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response1 = await client.GetAsync("http://localhost/api/users/123");
            var response2 = await client.GetAsync("http://localhost/api/users/456");
            
            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Supports_wildcard_in_query()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .ForPath("/api/search")
                .ForQuery("?q=*")
                .RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/search?q=test");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class WhenUsingCustomMatchers
    {
        [Fact]
        public async Task Supports_custom_matcher_predicate()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .For(req => req.Headers.Contains("X-Custom-Header"))
                .RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            client.DefaultRequestHeaders.Add("X-Custom-Header", "value");
            
            // Act
            var response = await client.GetAsync("http://localhost/api/test");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    public class WhenCollectingRequests
    {
        [Fact]
        public async Task Can_collect_requests_in_collection()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            var collection = new RequestCollection();
            
            builder.ForPatch()
                .ForPath("/api/update")
                .CollectingRequestIn(collection)
                .RespondsWithStatus(HttpStatusCode.NoContent);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), "http://localhost/api/update")
            {
                Content = new StringContent("test")
            };
            await client.SendAsync(request);
            
            // Assert
            collection.Count.Should().Be(1);
            var capturedRequest = collection.First();
            (capturedRequest != null).Should().BeTrue();
            capturedRequest.WasExpected.Should().BeTrue();
        }

        [Fact]
        public async Task All_requests_are_captured_globally()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/test");
            await client.GetAsync("http://localhost/api/test");
            
            // Assert
            httpMock.Requests.Should().NotBeEmpty();
            httpMock.Requests.Count.Should().Be(2);
        }
    }

    public class WhenFailingOnUnexpectedCalls
    {
        [Fact]
        public async Task Throws_exception_when_unexpected_request_and_fail_on_unexpected_is_true()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.FailOnUnexpectedCalls = true;
            
            builder.ForGet().ForPath("/api/expected").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act & Assert
            await Assert.ThrowsAsync<UnexpectedRequestException>(async () =>
            {
                await client.GetAsync("http://localhost/api/unexpected");
            });
        }

        [Fact]
        public async Task Does_not_throw_when_fail_on_unexpected_is_false()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.FailOnUnexpectedCalls = false;
            
            builder.ForGet().ForPath("/api/expected").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/unexpected");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    public class WhenUsingAssertions
    {
        [Fact]
        public async Task Can_assert_all_mocks_have_been_invoked()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet().ForPath("/api/test1").RespondsWithStatus(HttpStatusCode.OK);
            builder.ForGet().ForPath("/api/test2").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/test1");
            await client.GetAsync("http://localhost/api/test2");
            
            // Assert
            httpMock.Should().HaveAllRequestsCalled();
        }

        [Fact]
        public async Task Can_assert_request_collection_not_empty()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/test");
            
            // Assert
            httpMock.Requests.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Can_assert_no_unexpected_calls()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/test");
            
            // Assert
            httpMock.Requests.Should().NotContainUnexpectedCalls();
        }

        [Fact]
        public async Task Can_assert_captured_request_is_expected()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/test");
            
            // Assert
            var request = httpMock.Requests.First();
            request.Should().BeExpected();
        }

        [Fact]
        public async Task Can_assert_captured_request_is_unexpected()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.FailOnUnexpectedCalls = false;
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            await client.GetAsync("http://localhost/api/unexpected");
            
            // Assert
            var request = httpMock.Requests.First();
            request.Should().BeUnexpected();
        }
    }

    public class WhenContinuingBuilding
    {
        [Fact]
        public async Task Can_continue_building_on_previous_builder()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet().ForPath("/api/test1").RespondsWithStatus(HttpStatusCode.OK);
            
            // Continue building
            builder.ForPost().ForPath("/api/test2").RespondsWithStatus(HttpStatusCode.Created);
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response1 = await client.GetAsync("http://localhost/api/test1");
            var response2 = await client.PostAsync("http://localhost/api/test2", new StringContent("test"));
            
            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public void Can_clear_configured_mocks()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            builder.ForGet().ForPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            
            // Act
            builder.Clear();
            
            // No assertion needed - just verify it doesn't throw
        }
    }

    public class WhenUsingDifferentResponseTypes
    {
        [Fact]
        public async Task Can_respond_with_raw_string()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .ForPath("/api/text")
                .RespondsWithContent("Hello, World!");
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/text");
            var content = await response.Content.ReadAsStringAsync();
            
            // Assert
            content.Should().Be("Hello, World!");
        }

        [Fact]
        public async Task Can_respond_with_empty_content()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .ForPath("/api/empty")
                .RespondsWithEmptyContent();
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/empty");
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Can_use_custom_responder()
        {
            // Arrange
            var builder = new HttpMockBuilder();
            
            builder.ForGet()
                .ForPath("/api/custom")
                .RespondsWith(req => new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Content = new StringContent("Custom response")
                });
            
            var httpMock = builder.Build();
            var client = httpMock.Build();
            
            // Act
            var response = await client.GetAsync("http://localhost/api/custom");
            var content = await response.Content.ReadAsStringAsync();
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            content.Should().Be("Custom response");
        }
    }
}
