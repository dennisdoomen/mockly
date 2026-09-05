using System.Net;
using System.Text;

namespace Mockly;

/// <summary>
/// Represents the relevant parts of a parsed <c>curl</c> command.
/// </summary>
internal sealed class CurlRequest
{
    public string? Method { get; set; }

    public string? Url { get; set; }

    /// <summary>
    /// Headers to apply. A <c>null</c> value means the header must be excluded (curl's <c>Name:</c> syntax),
    /// while a non-null value (including an empty string, curl's <c>Name;</c> syntax) means the header must
    /// be present with that value.
    /// </summary>
    public IList<KeyValuePair<string, string?>> Headers { get; } = new List<KeyValuePair<string, string?>>();

    public string? Body { get; set; }
}

/// <summary>
/// Splits a <c>curl</c> command line into individual tokens, honoring single quotes, double quotes,
/// unquoted backslash escapes and shell line-continuation characters (<c>\</c>, <c>^</c> and <c>`</c>).
/// </summary>
internal sealed class CurlTokenizer(string text)
{
    private readonly List<string> tokens = new();
    private readonly StringBuilder current = new();
    private bool tokenStarted;
    private int position;

    public IReadOnlyList<string> Tokenize()
    {
        while (position < text.Length)
        {
            char c = text[position];

            if (c == '\'')
            {
                ReadSingleQuoted();
            }
            else if (c == '"')
            {
                ReadDoubleQuoted();
            }
            else if (c is ' ' or '\t' or '\r' or '\n')
            {
                FlushToken();
                position++;
            }
            else if (IsLineContinuation(c))
            {
                position = SkipLineBreak(position + 1);
            }
            else if (c == '\\')
            {
                AppendUnquotedEscape();
            }
            else
            {
                current.Append(c);
                tokenStarted = true;
                position++;
            }
        }

        FlushToken();
        return tokens;
    }

    /// <summary>
    /// Handles a backslash outside quotes. Per POSIX shell rules, it escapes the following character
    /// (removing any special meaning it would otherwise have), which is how tools such as browser
    /// DevTools embed an apostrophe in a copied <c>curl</c> command (e.g. <c>'\''</c>).
    /// </summary>
    private void AppendUnquotedEscape()
    {
        tokenStarted = true;
        position++;

        if (position < text.Length)
        {
            current.Append(text[position]);
            position++;
        }
        else
        {
            current.Append('\\');
        }
    }

    private bool IsLineContinuation(char c)
    {
        return c is '\\' or '^' or '`' &&
            position + 1 < text.Length &&
            (text[position + 1] == '\n' || text[position + 1] == '\r');
    }

    private int SkipLineBreak(int index)
    {
        if (index < text.Length && text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
        {
            return index + 2;
        }

        return index + 1;
    }

    private void FlushToken()
    {
        if (tokenStarted)
        {
            tokens.Add(current.ToString());
            current.Clear();
            tokenStarted = false;
        }
    }

    private void ReadSingleQuoted()
    {
        tokenStarted = true;
        position++;

        while (position < text.Length)
        {
            char c = text[position];
            if (c == '\'')
            {
                position++;
                return;
            }

            current.Append(c);
            position++;
        }

        throw new ArgumentException("The cURL command contains an unterminated single quote.");
    }

    private void ReadDoubleQuoted()
    {
        tokenStarted = true;
        position++;

        while (position < text.Length)
        {
            char c = text[position];
            if (c == '"')
            {
                position++;
                return;
            }

            if (c == '\\' && TryAppendDoubleQuoteEscape())
            {
                continue;
            }

            current.Append(c);
            position++;
        }

        throw new ArgumentException("The cURL command contains an unterminated double quote.");
    }

    private bool TryAppendDoubleQuoteEscape()
    {
        if (position + 1 >= text.Length)
        {
            return false;
        }

        char next = text[position + 1];
        if (next is '\n' or '\r')
        {
            position = SkipLineBreak(position + 1);
            return true;
        }

        if (next is '"' or '\\' or '$' or '`')
        {
            current.Append(next);
            position += 2;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Parses a tokenized <c>curl</c> command into a <see cref="CurlRequest"/>.
/// </summary>
internal sealed class CurlCommandParser
{
    private static readonly HashSet<string> ValueTakingOptions = new(StringComparer.Ordinal)
    {
        "-o", "--output", "-u", "--user", "-U", "--proxy-user", "-x", "--proxy", "-T", "--upload-file",
        "--connect-timeout", "-m", "--max-time", "--retry", "--retry-delay", "--retry-max-time",
        "--resolve", "--cacert", "--capath", "--cert", "-E", "--key", "--limit-rate", "--max-redirs",
        "-C", "--continue-at", "-w", "--write-out", "--oauth2-bearer", "-F", "--form", "--cert-type",
        "--key-type", "--proxy-header", "--interface", "--dns-servers", "--noproxy", "--range", "-r"
    };

    private readonly IReadOnlyList<string> tokens;
    private readonly CurlRequest request = new();
    private readonly List<string> dataParts = new();
    private int position;

    private CurlCommandParser(IReadOnlyList<string> tokens)
    {
        this.tokens = tokens;
    }

    public static CurlRequest Parse(string curlCommand)
    {
        if (string.IsNullOrWhiteSpace(curlCommand))
        {
            throw new ArgumentException("The cURL command must not be empty.", nameof(curlCommand));
        }

        IReadOnlyList<string> parsedTokens = new CurlTokenizer(curlCommand).Tokenize();
        return new CurlCommandParser(parsedTokens).ParseTokens();
    }

    private CurlRequest ParseTokens()
    {
        SkipExecutableName();

        while (position < tokens.Count)
        {
            string token = tokens[position];
            if (IsOption(token))
            {
                ParseOption(token);
            }
            else
            {
                request.Url ??= token;
                position++;
            }
        }

        return Build();
    }

    private void SkipExecutableName()
    {
        if (tokens.Count > 0 && string.Equals(tokens[0], "curl", StringComparison.OrdinalIgnoreCase))
        {
            position = 1;
        }
    }

    private static bool IsOption(string token)
    {
        return token.Length > 1 && token[0] == '-';
    }

    private void ParseOption(string token)
    {
        string name;
        string? inlineValue = null;

        if (token.StartsWith("--", StringComparison.Ordinal))
        {
            int separator = token.IndexOf("=", StringComparison.Ordinal);
            if (separator >= 0)
            {
                name = token.Substring(0, separator);
                inlineValue = token.Substring(separator + 1);
            }
            else
            {
                name = token;
            }
        }
        else
        {
            name = token.Substring(0, 2);
            if (token.Length > 2)
            {
                inlineValue = token.Substring(2);
            }
        }

        position++;
        DispatchOption(name, inlineValue);
    }

    private void DispatchOption(string name, string? inlineValue)
    {
        switch (name)
        {
            case "-X":
            case "--request":
            {
                request.Method = ConsumeValue(name, inlineValue);
                break;
            }

            case "-H":
            case "--header":
            {
                AddHeader(ConsumeValue(name, inlineValue));
                break;
            }

            case "-d":
            case "--data":
            case "--data-raw":
            case "--data-ascii":
            case "--data-binary":
            {
                dataParts.Add(ConsumeValue(name, inlineValue));
                break;
            }

            case "--data-urlencode":
            {
                dataParts.Add(EncodeDataUrlEncodeValue(ConsumeValue(name, inlineValue)));
                break;
            }

            case "--url":
            {
                request.Url = ConsumeValue(name, inlineValue);
                break;
            }

            case "-A":
            case "--user-agent":
            {
                AddHeaderValue("User-Agent", ConsumeValue(name, inlineValue));
                break;
            }

            case "-e":
            case "--referer":
            {
                AddHeaderValue("Referer", ConsumeValue(name, inlineValue));
                break;
            }

            case "-b":
            case "--cookie":
            {
                AddHeaderValue("Cookie", ConsumeValue(name, inlineValue));
                break;
            }

            default:
            {
                SkipUnknownOption(name, inlineValue);
                break;
            }
        }
    }

    private void SkipUnknownOption(string name, string? inlineValue)
    {
        // Unknown boolean flags (such as --compressed) are ignored. Options known to take a value are
        // consumed so their value is not mistaken for the URL (for example "-u user:pass").
        if (inlineValue is null && ValueTakingOptions.Contains(name))
        {
            ConsumeValue(name, inlineValue);
        }
    }

    private string ConsumeValue(string optionName, string? inlineValue)
    {
        if (inlineValue is not null)
        {
            return inlineValue;
        }

        if (position >= tokens.Count)
        {
            throw new ArgumentException($"The cURL option '{optionName}' is missing a value.");
        }

        string value = tokens[position];
        position++;
        return value;
    }

    /// <summary>
    /// Applies the encoding <c>--data-urlencode</c> performs before curl sends the request, supporting the
    /// <c>content</c>, <c>=content</c> and <c>name=content</c> forms. The <c>@filename</c> and
    /// <c>name@filename</c> forms read from a file and are not supported.
    /// </summary>
    private static string EncodeDataUrlEncodeValue(string raw)
    {
        int separator = raw.IndexOf("=", StringComparison.Ordinal);
        if (separator < 0)
        {
            if (HasFileReference(raw))
            {
                throw new ArgumentException(
                    "The cURL option '--data-urlencode' with a file reference (@filename or name@filename) is not supported.");
            }

            return WebUtility.UrlEncode(raw);
        }

        string name = raw.Substring(0, separator);
        string content = raw.Substring(separator + 1);
        string encodedContent = WebUtility.UrlEncode(content);

        return name.Length == 0 ? encodedContent : $"{name}={encodedContent}";
    }

    private static bool HasFileReference(string raw)
    {
#if NET8_0_OR_GREATER
        return raw.Contains('@', StringComparison.Ordinal);
#else
        return raw.Contains("@", StringComparison.Ordinal);
#endif
    }

    private void AddHeader(string header)
    {
        int separator = header.IndexOf(":", StringComparison.Ordinal);
        if (separator < 0)
        {
            // curl's "Name;" syntax explicitly sends the header with an empty value.
            if (header.EndsWith(";", StringComparison.Ordinal) && header.Length > 1)
            {
                AddHeaderValue(header.Substring(0, header.Length - 1).Trim(), string.Empty);
                return;
            }

            throw new ArgumentException($"The header '{header}' is not in the expected 'Name: Value' format.");
        }

        string name = header.Substring(0, separator).Trim();
        string value = header.Substring(separator + 1).Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException($"The header '{header}' does not specify a name.");
        }

        if (value.Length == 0)
        {
            // curl's "Name:" syntax suppresses that header rather than sending it with an empty value.
            request.Headers.Add(new KeyValuePair<string, string?>(name, null));
            return;
        }

        AddHeaderValue(name, value);
    }

    private void AddHeaderValue(string name, string value)
    {
        request.Headers.Add(new KeyValuePair<string, string?>(name, value));
    }

    private CurlRequest Build()
    {
        if (dataParts.Count > 0)
        {
            request.Body = string.Join("&", dataParts);
        }

        if (string.IsNullOrEmpty(request.Url))
        {
            throw new ArgumentException("The cURL command does not contain a URL.");
        }

        return request;
    }
}
