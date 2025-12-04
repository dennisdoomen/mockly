using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Mockly.Specs;

public class RequestCollectionNotContainRequestSpecs
{
    public class NotContainRequestFor
    {
        [Fact]
        public async Task Succeeds_when_no_matching_request_exists()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/other").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/other");

            // Assert + chaining via And
            mock.Requests.Should()
                .NotContainRequestFor("/api/missing")
                .And
                .NotContainUnexpectedCalls();
        }

        [Fact]
        public async Task Fails_when_a_matching_request_exists_with_clear_diagnostics()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            var act = () => mock.Requests.Should().NotContainRequestFor("/api/t*");

            act.Should().Throw<XunitException>()
                .WithMessage("Did not expect a request for URL pattern \"/api/t*\"*, but found:*GET http://localhost/api/test*");
        }
    }

    public class Chaining
    {
        [Fact]
        public async Task Works_for_contained_request_assertions()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("http://localhost/api/test", new StringContent("{ \"id\": \"1\" }"));

            // Assert: Contain + body assertions chained via .And
            mock.Requests.Should()
                .ContainRequestFor("/api/test")
                .WithBodyMatchingJson("{ \"id\": \"1\" }")
                .And
                .WithBodyHavingProperty("id", "1");
        }
    }
}
