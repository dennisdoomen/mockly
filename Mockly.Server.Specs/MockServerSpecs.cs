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

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Jane");
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
            port = probe.Port;
        }

        // Act
        await using var server = new HttpMock().StartServer(port);

        // Assert
        server.Url.Host.Should().Be("127.0.0.1");
        server.Port.Should().Be(port);
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
    public async Task Reports_unmatched_requests_as_server_errors()
    {
        // Arrange
        var mock = new HttpMock();
        await using var server = mock.StartServer();

        // Act
        var response = await Client.GetAsync(new Uri(server.Url, "/nothing"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
