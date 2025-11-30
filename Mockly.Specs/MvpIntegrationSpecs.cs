using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mockly.Specs;

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
            var renamedCollectiveScheme = new CollectiveScheme
            {
                Id = 123,
                Name = "Test Scheme"
            };

            var existingWorkingScope = new WorkingScope
            {
                Id = 456,
                CollectiveSchemeId = 123
            };

            var patches = new RequestCollection();

            var mock = new HttpMock
            {
                FailOnUnexpectedCalls = true // default is true
            };

            mock
                .ForGet().WithPath($"/fnv_collectiveschemes({renamedCollectiveScheme.Id}*)") // Supports wildcards
                .RespondsWithJsonContent(renamedCollectiveScheme);

            mock
                .ForGet().WithPath("/fnv_workingscopes")
                .WithQuery($"?$filter=_fnv_collectivescheme_value eq {renamedCollectiveScheme.Id}*") // Supports wildcards
                .RespondsWithJsonContent(new[]
                {
                    existingWorkingScope
                }); // Serializes the object to JSON using System.Text.Json

            mock
                .ForGet().WithPath("/fnv_workingscopes")
                .WithoutQuery()
                .With(request => request.Uri?.Query.Contains("special") ?? false) // predicate on the HttpRequestMessage
                .RespondsWithStatus(HttpStatusCode.Accepted);

            mock
                .ForPatch().WithPath($"/fnv_workingscopes({existingWorkingScope.Id})")
                .CollectingRequestIn(patches) // Collects the requests specific for this mocked HTTP call
                .RespondsWithStatus(HttpStatusCode.NoContent);

            HttpClient httpClient = mock.GetClient();

            // Act - Make various requests
            var getCollectiveSchemeResponse =
                await httpClient.GetAsync($"http://localhost/fnv_collectiveschemes({renamedCollectiveScheme.Id})");

            var getCollectiveSchemeContent = await getCollectiveSchemeResponse.Content.ReadAsStringAsync();

            var getWorkingScopesResponse =
                await httpClient.GetAsync(
                    $"http://localhost/fnv_workingscopes?$filter=_fnv_collectivescheme_value eq {renamedCollectiveScheme.Id}");

            var getSpecialWorkingScopesResponse = await httpClient.GetAsync("http://localhost/fnv_workingscopes?special=true");

            var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"),
                $"http://localhost/fnv_workingscopes({existingWorkingScope.Id})")
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
            mock.Should().HaveAllRequestsCalled();

            var allRequests = mock.Requests;
            allRequests.Should().NotBeEmpty();
            allRequests.Should().NotContainUnexpectedCalls();

            var patchRequest1 = patches.First();
            (patchRequest1 != null).Should().BeTrue();
            patchRequest1.WasExpected.Should().BeTrue();
            patchRequest1.Should().BeExpected();

            // Continue using the previous mock
            mock.ForPost().WithPath("/api/new").RespondsWithStatus(HttpStatusCode.Created);

            // Verify we can use the same mock with the additional configuration
            var client2 = mock.GetClient();
            var postResponse = await client2.PostAsync("http://localhost/api/new", new StringContent("test"));
            postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }
}
