using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace Mockly.Specs;

public class AssertionSpecs
{
    public class HttpMockSpecs
    {
        [Fact]
        public async Task Can_assert_all_mocks_have_been_invoked()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet().WithPath("/api/test1").RespondsWithStatus(HttpStatusCode.OK);
            mock.ForGet().WithPath("/api/test2").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test1");
            await client.GetAsync("http://localhost/api/test2");

            // Assert
            mock.Should().HaveAllRequestsCalled();
        }

        [Fact]
        public async Task Will_throw_when_not_all_mocks_have_been_invoked()
        {
            // Arrange
            var mock = new HttpMock();

            mock.ForGet().WithPath("/api/test1").RespondsWithStatus(HttpStatusCode.OK);
            mock.ForGet().WithPath("/api/test2").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            await client.GetAsync("http://localhost/api/test1");

            // Act
            var act = () => mock.Should().HaveAllRequestsCalled();

            // Assert
            act.Should().Throw<XunitException>().WithMessage("*but 1 mock(s) were not invoked*");
        }
    }

    public class RequestCollectionSpecs
    {
        [Fact]
        public async Task Can_assert_request_collection_is_not_empty()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            mock.Requests.Should().NotBeEmpty();
        }

        [Fact]
        public void Will_throw_when_request_collection_is_empty()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.Requests.Should().NotBeEmpty();

            // Assert
            act.Should().Throw<XunitException>().WithMessage("*empty*");
        }

        [Fact]
        public async Task Can_assert_no_unexpected_calls()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            mock.Requests.Should().NotContainUnexpectedCalls();
        }

        [Fact]
        public async Task Will_throw_when_unexpected_calls_are_present()
        {
            // Arrange
            var mock = new HttpMock
            {
                FailOnUnexpectedCalls = false
            };

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/unexpected");
            var act = () => mock.Requests.Should().NotContainUnexpectedCalls();

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("no unexpected requests should exist, but found 1 unexpected request(s):*");
        }
    }

    public class CapturedRequestAssertions
    {
        [Fact]
        public async Task Can_assert_captured_request_is_expected()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");

            // Assert
            var request = mock.Requests.First();
            request.Should().BeExpected();
        }

        [Fact]
        public async Task Will_throw_when_captured_request_is_not_expected()
        {
            // Arrange
            var mock = new HttpMock
            {
                FailOnUnexpectedCalls = false
            };

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/unexpected");
            var request = mock.Requests.First();
            var act = () => request.Should().BeExpected();

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("request should be expected, but it was unexpected");
        }

        [Fact]
        public async Task Can_assert_captured_request_is_unexpected()
        {
            // Arrange
            var mock = new HttpMock();
            mock.FailOnUnexpectedCalls = false;

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/unexpected");

            // Assert
            var request = mock.Requests.First();
            request.Should().BeUnexpected();
        }

        [Fact]
        public async Task Will_throw_when_captured_request_is_expected_but_asserted_unexpected()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForGet().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.OK);

            // Build step removed;
            var client = mock.GetClient();

            // Act
            await client.GetAsync("http://localhost/api/test");
            var request = mock.Requests.First();
            var act = () => request.Should().BeUnexpected();

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("request should be unexpected, but it was expected");
        }
    }
}
