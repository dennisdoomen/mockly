using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Mockly.Specs;

public class CurlImportSpecs
{
    public class BasicRequests
    {
        [Fact]
        public async Task Imports_a_simple_get_request()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Imports_a_request_without_the_curl_executable_prefix()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("https://api.example.com/users")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Preserves_the_query_string()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl 'https://api.example.com/search?q=mockly&page=2'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/search?q=mockly&page=2");

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task A_request_without_the_expected_query_string_is_not_matched()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl 'https://api.example.com/search?q=mockly'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://api.example.com/search");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Honors_the_scheme_from_the_url()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl http://api.example.com/users")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Defaults_to_http_when_the_url_has_no_scheme()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl api.example.com/users")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Matches_a_url_encoded_query_value_against_the_decoded_request_query()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl 'https://api.example.com/search?q=hello%20world'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/search?q=hello%20world");

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Treats_a_literal_asterisk_in_the_path_as_a_literal_character()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl 'https://api.example.com/users/*'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://api.example.com/users/1");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }
    }

    public class Methods
    {
        [Fact]
        public async Task Uses_the_explicit_method_from_the_dash_x_option()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -X DELETE https://api.example.com/users/1")
                .RespondsWithStatus(HttpStatusCode.NoContent);

            // Act
            var response = await mock.GetClient().DeleteAsync("https://api.example.com/users/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Normalizes_a_lowercase_method_name()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -X post https://api.example.com/users -d 'name=mockly'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("name=mockly");
            var response = await mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Defaults_to_post_when_a_body_is_present_without_an_explicit_method()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users --data 'name=mockly'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("name=mockly");
            var response = await mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    public class RequestHeaders
    {
        [Fact]
        public async Task Matches_a_single_request_header()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -H 'Accept: application/json'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("Accept", "application/json");
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Matches_multiple_request_headers()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl(
                    "curl https://api.example.com/users -H 'Accept: application/json' -H 'Authorization: Bearer abc123'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Authorization", "Bearer abc123");
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task A_request_missing_a_required_header_is_not_matched()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -H 'Authorization: Bearer abc123'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var act = () => mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Matches_a_multi_token_header_by_its_original_separator()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -A 'Mozilla/5.0 Foo/1.0'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 Foo/1.0");
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Requires_a_header_to_be_absent_when_it_was_suppressed_with_a_trailing_colon()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -H 'Accept:'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task A_request_with_the_suppressed_header_present_is_not_matched()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -H 'Accept:'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("Accept", "application/json");
            var act = () => mock.GetClient().SendAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Requires_an_explicitly_empty_header_value_when_using_the_trailing_semicolon_syntax()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl https://api.example.com/users -H 'X-Empty;'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("X-Empty", string.Empty);
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }
    }

    public class Bodies
    {
        [Fact]
        public async Task Matches_a_json_body_ignoring_whitespace()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl(
                    "curl -X POST https://api.example.com/users -H 'Content-Type: application/json' " +
                    "--data-raw '{\"name\":\"mockly\"}'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("{\n  \"name\": \"mockly\"\n}", Encoding.UTF8, "application/json");
            var response = await mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task A_request_with_a_different_json_body_is_not_matched()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl(
                    "curl -X POST https://api.example.com/users -H 'Content-Type: application/json' " +
                    "--data-raw '{\"name\":\"mockly\"}'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("{\"name\":\"other\"}", Encoding.UTF8, "application/json");
            var act = () => mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Matches_a_form_encoded_body()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -X POST https://api.example.com/login -d 'user=admin&password=secret'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var content = new StringContent("user=admin&password=secret");
            var response = await mock.GetClient().PostAsync("https://api.example.com/login", content);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task A_non_json_body_is_matched_exactly_and_not_as_a_substring()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -X POST https://api.example.com/login -d 'user=admin'")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var content = new StringContent("user=admin&extra=1");
            var act = () => mock.GetClient().PostAsync("https://api.example.com/login", content);

            // Assert
            await act.Should().ThrowAsync<UnexpectedRequestException>();
        }

        [Fact]
        public async Task Encodes_a_data_urlencode_value_the_way_curl_sends_it()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -X POST https://api.example.com/users --data-urlencode 'name=John Doe'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("name=John+Doe");
            var response = await mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public void Rejects_a_data_urlencode_file_reference()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("curl -X POST https://api.example.com/users --data-urlencode '@file.txt'");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*file reference*");
        }
    }

    public class ShellSyntax
    {
        [Fact]
        public async Task Supports_backslash_line_continuations()
        {
            // Arrange
            var mock = new HttpMock();
            var curl =
                "curl 'https://api.example.com/users' \\\n" +
                "  -H 'Accept: application/json'";

            mock.ImportFromCurl(curl).RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("Accept", "application/json");
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Supports_double_quoted_arguments()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl \"https://api.example.com/users\" -H \"Accept: application/json\"")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/users");
            request.Headers.Add("Accept", "application/json");
            var response = await mock.GetClient().SendAsync(request);

            // Assert
            response.Should().Be200Ok();
        }

        [Fact]
        public async Task Supports_a_backslash_escaped_apostrophe_outside_quotes()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl(
                    "curl -X POST https://api.example.com/users -d 'name=It'\\''s a test'")
                .RespondsWithStatus(HttpStatusCode.Created);

            // Act
            var content = new StringContent("name=It's a test");
            var response = await mock.GetClient().PostAsync("https://api.example.com/users", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    public class OptionParsing
    {
        [Fact]
        public async Task Ignores_options_with_values_so_they_are_not_treated_as_the_url()
        {
            // Arrange
            var mock = new HttpMock();
            mock.ImportFromCurl("curl -u user:pass https://api.example.com/users")
                .RespondsWithStatus(HttpStatusCode.OK);

            // Act
            var response = await mock.GetClient().GetAsync("https://api.example.com/users");

            // Assert
            response.Should().Be200Ok();
        }
    }

    public class MalformedInput
    {
        [Fact]
        public void A_null_command_throws()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void An_empty_command_throws_a_clear_exception()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("   ");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*empty*");
        }

        [Fact]
        public void A_command_without_a_url_throws_a_clear_exception()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("curl -X GET");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*does not contain a URL*");
        }

        [Fact]
        public void An_option_missing_its_value_throws_a_clear_exception()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("curl https://api.example.com/users -H");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*missing a value*");
        }

        [Fact]
        public void An_unterminated_quote_throws_a_clear_exception()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("curl 'https://api.example.com/users");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*unterminated*");
        }

        [Fact]
        public void A_malformed_header_throws_a_clear_exception()
        {
            // Arrange
            var mock = new HttpMock();

            // Act
            var act = () => mock.ImportFromCurl("curl https://api.example.com/users -H 'NotAHeader'");

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*Name: Value*");
        }
    }
}
