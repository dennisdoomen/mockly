using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Mockly.Specs;

public class RequestCollectionContainRequestSpecs
{
    public class ContainRequest
    {
        [Fact]
        public async Task Can_ensure_a_request_was_captured()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            mock.Requests.Should().ContainRequest();
        }

        [Fact]
        public void Fails_when_no_requests_are_captured()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.Requests.Should().ContainRequest();

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("Expected at least one request to have been captured, but none were found*");
        }
    }

    public class ContainRequestFor
    {
        [Fact]
        public async Task Finds_request_for_relative_path()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            mock.Requests.Should().ContainRequestFor("/api/t*t");
        }

        [Fact]
        public async Task Finds_request_for_absolute_uri()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            mock.Requests.Should().ContainRequestFor(new Uri("http://localhost/*/test"));
        }

        [Fact]
        public void Fails_when_no_requests_are_captured_at_all()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.Requests.Should().ContainRequestFor("/missing");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("*Expected*/missing*at all*");
        }

        [Fact]
        public async Task Fails_when_the_expected_request_is_missing()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/other").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/other");

            var act = () => mock.Requests.Should().ContainRequestFor("/api/missing");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("Expected*/api/missing**among:*GET http://localhost/api/other*");
        }
    }

    public class WithBody
    {
        [Fact]
        public async Task Matches_body_with_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("hello world"));

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBody("*world");
        }

        [Fact]
        public async Task Fails_when_body_does_not_match_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("hello world"));

            var act = () => mock.Requests.Should().ContainRequest()
                .WithBody("abc?");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("*expected request body to match wildcard pattern*");
        }
    }

    public class WithBodyMatchingJson
    {
        [Fact]
        public async Task Matches_body_equivalent_json()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{\n  \"id\":1, \"name\":\"x\"\n}"));

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBodyMatchingJson("{ \"id\": 1, \"name\": \"x\" }");
        }

        [Fact]
        public async Task Fails_when_body_json_does_not_match()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\":2 }"));
            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyMatchingJson("{ \"id\": 1 }");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("*to be JSON-equivalent*");
        }

        [Fact]
        public async Task Fails_when_request_body_is_not_valid_json()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("not-json"));
            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyMatchingJson("{ \"id\": 1 }");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("*request body is not valid JSON*");
        }
    }

    public class WithBodyEquivalentTo
    {
        [Fact]
        public async Task Matches_body_equivalent_to_anonymous_object()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\":1, \"name\":\"x\" }"));

            var expected = new
            {
                id = 1,
                name = "x"
            };

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBodyEquivalentTo(expected);
        }

        [Fact]
        public async Task Fails_when_body_is_not_equivalent_to_anonymous_object()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\":1, \"name\":\"x\" }"));

            var expected = new
            {
                id = 2,
                name = "y"
            };

            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyEquivalentTo(expected);

            // Assert
            act.Should().Throw<XunitException>();
        }
    }

    public class WithBodyHavingPropertiesOf
    {
        [Fact]
        public async Task Matches_body_having_properties_of_dictionary()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\":\"1\", \"name\":\"x\" }"));

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = "1",
                ["name"] = "x"
            };

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBodyHavingPropertiesOf(expected);
        }

        [Fact]
        public async Task Fails_when_body_does_not_have_expected_properties()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\":\"1\" }"));

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = "2"
            };

            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyHavingPropertiesOf(expected);

            // Assert
            act.Should().Throw<XunitException>();
        }

        [Fact]
        public async Task Fails_when_body_is_not_json_object()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("not-json"));

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["id"] = "1"
            };

            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyHavingPropertiesOf(expected);

            // Assert
            act.Should().Throw<JsonException>()
                .WithMessage("*'not-json' is an invalid JSON literal*");
        }
    }
}
