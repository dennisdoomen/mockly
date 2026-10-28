using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Mockly.Server.Specs;

public class MockServerSpecs
{
    private static readonly HttpClient Client = new();

    [Fact]
    public async Task Serves_configured_response_over_a_real_socket()
    {
        // Arrange
        var mock = new HttpMock();
        mock.ForGet("/api/users").RespondsWithJsonContent(new { Name = "Jane" });

        await using var server = mock.StartServer();

        // Act
        var response = await Client.GetAsync(new Uri(server.Url, "/api/users"));
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Jane");
        mock.AllMocksInvoked.Should().BeTrue();
        mock.Requests.Count.Should().Be(1);
    }

    [Fact]
    public async Task Binds_to_loopback_on_the_requested_port()
    {
        // Arrange
        int port;
        await using (var probe = new HttpMock().StartServer())
        {
            port = probe.Url.Port;
        }

        // Act
        await using var server = new HttpMock().StartServer(port);

        // Assert
        server.Url.Host.Should().Be("127.0.0.1");
        server.Url.Port.Should().Be(port);
    }

    [Fact]
    public async Task Captures_request_body_sent_over_the_socket()
    {
        // Arrange
        var mock = new HttpMock();
        mock.ForPost("/orders").RespondsWithStatus(HttpStatusCode.Created);

        await using var server = mock.StartServer();

        // Act
        var response = await Client.PostAsync(new Uri(server.Url, "/orders"), new StringContent("hello", Encoding.UTF8, "text/plain"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        mock.Requests.Single().Body.Should().Be("hello");
    }

    [Fact]
    public async Task Passes_request_headers_to_the_mock()
    {
        // Arrange
        var mock = new HttpMock();
        mock.ForGet("/secure").WithHeader("X-Api-Key", "secret").RespondsWithStatus(HttpStatusCode.OK);

        await using var server = mock.StartServer();

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server.Url, "/secure"));
        request.Headers.Add("X-Api-Key", "secret");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mock.Requests.Single().Headers.GetValues("X-Api-Key").Should().Equal("secret");
    }

    [Fact]
    public async Task Returns_response_headers_to_the_client()
    {
        // Arrange
        var mock = new HttpMock();
        mock.ForPost("/users")
            .RespondsWithStatus(HttpStatusCode.Created)
            .WithHeader("Location", "/users/123")
            .WithHeader("X-Custom", "first", "second");

        await using var server = mock.StartServer();

        // Act
        var response = await Client.PostAsync(new Uri(server.Url, "/users"), null);

        // Assert
        response.Headers.Location.Should().Be(new Uri("/users/123", UriKind.Relative));
        response.Headers.GetValues("X-Custom").Should().Equal("first", "second");
    }

    [Fact]
    public async Task Reports_unmatched_requests_as_not_found()
    {
        // Arrange
        var mock = new HttpMock();
        await using var server = mock.StartServer();

        // Act
        var response = await Client.GetAsync(new Uri(server.Url, "/nothing"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Aborts_the_connection_for_a_simulated_failure()
    {
        // Arrange
        var mock = new HttpMock();
        mock.ForGet("/slow").TimesOut();

        await using var server = mock.StartServer();

        // Act
        Func<Task> act = () => Client.GetAsync(new Uri(server.Url, "/slow"));

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        mock.Requests.Should().ContainSingle();
    }
}
