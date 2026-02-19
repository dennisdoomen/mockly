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
            await client.GetAsync("https://localhost/api/test1");
            await client.GetAsync("https://localhost/api/test2");

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

            await client.GetAsync("https://localhost/api/test1");

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
            await client.GetAsync("https://localhost/api/test");

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
            await client.GetAsync("https://localhost/api/test");

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
            await client.GetAsync("https://localhost/api/unexpected");
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
            await client.GetAsync("https://localhost/api/test");

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
            await client.GetAsync("https://localhost/api/unexpected");
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
            await client.GetAsync("https://localhost/api/unexpected");

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
            await client.GetAsync("https://localhost/api/test");
            var request = mock.Requests.First();
            var act = () => request.Should().BeUnexpected();

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("request should be unexpected, but it was expected");
        }
    }

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
            await client.GetAsync("https://localhost/api/test");

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
            await client.GetAsync("https://localhost/api/test");

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
            await client.GetAsync("https://localhost/api/test");

            // Assert
            mock.Requests.Should().ContainRequestFor(new Uri("https://localhost/*/test"));
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
            await client.GetAsync("https://localhost/api/other");

            var act = () => mock.Requests.Should().ContainRequestFor("/api/missing");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("Expected*/api/missing**among:*GET https://localhost/api/other*");
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
            await client.PostAsync("https://localhost/api/test", new StringContent("hello world"));

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBody("*world");
        }

        [Fact]
        public async Task Can_match_multiple_requests()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("hello world"));
            await client.PostAsync("https://localhost/api/test", new StringContent("hallo wereld"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBody("*wereld*");
            mock.Requests.Should().ContainRequest().WithBody("*world*");
        }

        [Fact]
        public async Task Fails_when_body_does_not_match_wildcard_pattern()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("hello world"));

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
            await client.PostAsync("https://localhost/api/test", new StringContent("{\n  \"id\":1, \"name\":\"x\"\n}"));

            // Assert
            mock.Requests.Should().ContainRequest()
                .WithBodyMatchingJson("{ \"id\": 1, \"name\": \"x\" }");
        }

        [Fact]
        public async Task Can_match_against_multiple_requests()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{\n  \"id\":1, \"name\":\"x\"\n}"));
            await client.PostAsync("https://localhost/api/test", new StringContent("{\n  \"id\":2, \"name\":\"y\"\n}"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyMatchingJson("{ \"id\": 2, \"name\": \"y\" }");
            mock.Requests.Should().ContainRequest().WithBodyMatchingJson("{ \"id\": 1, \"name\": \"x\" }");
        }

        [Fact]
        public async Task Fails_when_body_json_does_not_match()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":2 }"));
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
            await client.PostAsync("https://localhost/api/test", new StringContent("not-json"));
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
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":2, \"name\":\"y\" }"));
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":1, \"name\":\"x\" }"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyEquivalentTo(new
            {
                id = 1,
                name = "x"
            });

            mock.Requests.Should().ContainRequest().WithBodyEquivalentTo(new
            {
                id = 2,
                name = "y"
            });
        }

        [Fact]
        public async Task Fails_when_body_is_not_equivalent_to_anonymous_object()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":1, \"name\":\"x\" }"));

            var expected = new
            {
                id = 2,
                name = "y"
            };

            var act = () => mock.Requests.Should().ContainRequest()
                .WithBodyEquivalentTo(expected);

            // Assert
            act.Should().Throw<XunitException>().WithMessage(
                """
                Expected request #1 (POST https://localhost/api/test) to have a body equivalent to the expectation, but it did not:
                - Expected property actual.id to be 2, but found 1.
                - Expected property actual.name to be "y", but "x" differs near "x" (index 0).*
                """);
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
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"2\", \"name\":\"y\" }"));
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"1\", \"name\":\"x\" }"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "1",
                    ["name"] = "x"
                });

            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "2",
                    ["name"] = "y"
                });
        }

        [Fact]
        public async Task Matches_body_having_properties_of_dictionary_when_request_has_properties_with_null_values()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"2\", \"name\":null }"));
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"1\", \"name\":\"x\" }"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "1",
                    ["name"] = "x"
                });

            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "2",
                    ["name"] = null
                });
        }

        [Fact]
        public async Task Fails_when_body_does_not_have_expected_properties()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"1\" }"));

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
        public async Task Fails_when_none_of_the_requests_have_the_expected_property_and_value()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":1 }"));
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":2 }"));

            var act = () => mock.Requests.Should().ContainRequest().WithBodyHavingProperty("id", "3");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("Expected at least one request body to contain property \"id\" with value \"3\", but none did");
        }

        [Fact]
        public async Task Fails_when_body_does_not_have_the_expected_property_and_value()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":1 }"));

            var act = () => mock.Requests.Should().ContainRequest().WithBodyHavingProperty("id", "3");

            // Assert
            act.Should().Throw<XunitException>()
                .WithMessage("Expected the request body to contain property \"id\" with value \"3\", but it did not:*");
        }

        [Fact]
        public async Task Matches_body_having_property_when_request_has_properties_with_null_values()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":3, \"name\":null}"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingProperty("id", "3");
        }

        [Fact]
        public async Task Fails_when_body_is_not_json_object()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("not-json"));

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

        [Fact]
        public async Task Can_handle_different_json_types()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost("https://localhost/api/test").RespondsWithStatus(HttpStatusCode.Accepted);

            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent(
                """
                {
                  "fnv_name" : "Parent company pension plan working scope",
                  "fnv_collectivescheme@odata.bind" : "/fnv_collectiveschemes(3588777a-b78e-4716-95f7-99952c49b4cb)",
                  "fnv_grouping" : 118680000,
                  "fnv_businesssubgroup@odata.bind" : "/fnv_businesssubgroups(9f76a4a0-72d8-48a7-aaff-ca11da572130)",
                  "fnv_businessgrouping@odata.bind" : "/accounts(4d1ae18c-5568-4cc4-be94-a83275dc8992)",
                  "fnv_inheritedof@odata.bind" : "/fnv_workingscopes(af0e9547-3902-470e-9bdb-b858da7eef38)",
                  "fnv_origin" : 118680001,
                  "fnv_inherit" : false
                }
                """));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>
                {
                    { "fnv_name", "Parent company pension plan working scope" },
                    { "fnv_collectivescheme@odata.bind", "/fnv_collectiveschemes(3588777a-b78e-4716-95f7-99952c49b4cb)" },
                    { "fnv_grouping", "118680000" },
                    { "fnv_businessgrouping@odata.bind", "/accounts(4d1ae18c-5568-4cc4-be94-a83275dc8992)" },
                    { "fnv_businesssubgroup@odata.bind", "/fnv_businesssubgroups(9f76a4a0-72d8-48a7-aaff-ca11da572130)" },
                    { "fnv_inheritedof@odata.bind", "/fnv_workingscopes(af0e9547-3902-470e-9bdb-b858da7eef38)" },
                    { "fnv_origin", "118680001" },
                    { "fnv_inherit", "False" }
                });
        }

        [Fact]
        public async Task Ignores_extra_properties_in_body()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test",
                new StringContent("{ \"id\":\"1\", \"name\":\"x\", \"extra\":\"value\" }"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesOf(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "1",
                    ["name"] = "x"
                });
        }
    }

    public class WithBodyHavingPropertiesEqualTo
    {
        [Fact]
        public async Task Matches_body_having_properties_equal_to_dictionary()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"1\", \"name\":\"x\" }"));

            // Assert
            mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesEqualTo(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "1",
                    ["name"] = "x"
                });
        }

        [Fact]
        public async Task Fails_when_body_has_extra_properties()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test",
                new StringContent("{ \"id\":\"1\", \"name\":\"x\", \"extra\":\"value\" }"));

            var act = () => mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesEqualTo(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "1",
                    ["name"] = "x"
                });

            // Assert
            act.Should().Throw<XunitException>();
        }

        [Fact]
        public async Task Fails_when_body_has_mismatched_property_values()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ForPost().WithPath("/api/test").RespondsWithStatus(HttpStatusCode.Created);
            var client = mock.GetClient();

            // Act
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\":\"1\" }"));

            var act = () => mock.Requests.Should().ContainRequest().WithBodyHavingPropertiesEqualTo(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["id"] = "2"
                });

            // Assert
            act.Should().Throw<XunitException>();
        }
    }

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
            await client.GetAsync("https://localhost/api/other");

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
            await client.GetAsync("https://localhost/api/test");

            // Assert
            var act = () => mock.Requests.Should().NotContainRequestFor("/api/t*");

            act.Should().Throw<XunitException>()
                .WithMessage("Did not expect a request for URL pattern \"/api/t*\"*, but found:*GET https://localhost/api/test*");
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
            await client.PostAsync("https://localhost/api/test", new StringContent("{ \"id\": \"1\" }"));

            // Assert: Contain + body assertions chained via .And
            mock.Requests.Should()
                .ContainRequestFor("/api/test")
                .WithBodyMatchingJson("{ \"id\": \"1\" }")
                .And
                .WithBodyHavingProperty("id", "1");
        }
    }
}
