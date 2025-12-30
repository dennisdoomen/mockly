using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mockly.Specs;

public class UrlEncodingTests
{
    [Fact]
    public async Task WithPath_should_handle_url_encoded_characters()
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
    public async Task WithQuery_should_handle_url_encoded_characters()
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
}
