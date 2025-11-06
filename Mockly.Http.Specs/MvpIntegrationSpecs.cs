using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mockly.Http.Specs;

/// <summary>
/// Integration test demonstrating the MVP usage as shown in the issue.
/// </summary>
public class MvpIntegrationSpecs
{
    public class ExampleFromIssue
    {
        private class CollectiveScheme
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class WorkingScope
        {
            public int Id { get; set; }
            public int CollectiveSchemeId { get; set; }
        }

        [Fact]
        public async Task Demonstrates_mvp_functionality_from_issue()
        {
            // Arrange - Create test data
            var renamedCollectiveScheme = new CollectiveScheme { Id = 123, Name = "Test Scheme" };
            var existingWorkingScope = new WorkingScope { Id = 456, CollectiveSchemeId = 123 };
            var patches = new RequestCollection();
            
            var builder = new HttpMockBuilder();

            // Configure it to fail on unexpected calls
            builder.FailOnUnexpectedCalls = false; // default is true

            builder
                .ForGet().ForPath($"/fnv_collectiveschemes({renamedCollectiveScheme.Id}*)") // Supports wildcards
                .RespondsWithJsonContent(renamedCollectiveScheme);

            builder
                .ForGet().ForPath($"/fnv_workingscopes")
                .ForQuery($"?$filter=_fnv_collectivescheme_value eq {renamedCollectiveScheme.Id}*") // Supports wildcards
                .RespondsWithJsonContent(new[] { existingWorkingScope }); // Serializes the object to JSON using System.Text.Json

            builder
                .ForGet().ForPath($"/fnv_workingscopes")
                .For(request => request.RequestUri?.Query.Contains("special") ?? false) // predicate on the HttpRequestMessage
                .RespondsWithStatus(HttpStatusCode.Accepted);

            builder
                .ForPatch().ForPath($"/fnv_workingscopes({existingWorkingScope.Id})")
                .CollectingRequestIn(patches) // Collects the requests specific for this mocked HTTP call
                .RespondsWithStatus(HttpStatusCode.NoContent);

            var httpMock = builder.Build();
            HttpClient httpClient = httpMock.Build();
            
            // Act - Make various requests
            var getCollectiveSchemeResponse = await httpClient.GetAsync($"http://localhost/fnv_collectiveschemes({renamedCollectiveScheme.Id})");
            var getCollectiveSchemeContent = await getCollectiveSchemeResponse.Content.ReadAsStringAsync();
            
            var getWorkingScopesResponse = await httpClient.GetAsync($"http://localhost/fnv_workingscopes?$filter=_fnv_collectivescheme_value eq {renamedCollectiveScheme.Id}");
            
            var getSpecialWorkingScopesResponse = await httpClient.GetAsync($"http://localhost/fnv_workingscopes?special=true");
            
            var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), $"http://localhost/fnv_workingscopes({existingWorkingScope.Id})")
            {
                Content = new StringContent("{}")
            };
            var patchResponse = await httpClient.SendAsync(patchRequest);

            // Assert - Verify responses
            getCollectiveSchemeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            getCollectiveSchemeContent.Should().Contain("Test Scheme");
            
            getWorkingScopesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            getSpecialWorkingScopesResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
            
            patchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Assert - Verify mock invocations
            httpMock.Should().HaveAllRequestsCalled();
            
            var allRequests = httpMock.Requests;
            allRequests.Should().NotBeEmpty();
            allRequests.Should().NotContainUnexpectedCalls();

            var patchRequest1 = patches.First();
            (patchRequest1 != null).Should().BeTrue();
            patchRequest1.WasExpected.Should().BeTrue();
            patchRequest1.Should().BeExpected();

            // Continue using the previous builder
            builder.ForPost().ForPath("/api/new").RespondsWithStatus(HttpStatusCode.Created);
            
            // Verify we can rebuild with the additional mock
            var httpMock2 = builder.Build();
            var client2 = httpMock2.Build();
            var postResponse = await client2.PostAsync("http://localhost/api/new", new StringContent("test"));
            postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            
            // Cleanup
            httpMock.Dispose();
            httpMock2.Dispose();
        }
    }
}
