using System.Text;

namespace Mockly;

/// <summary>
/// Represents the relevant parts of a parsed <c>curl</c> command.
/// </summary>
internal sealed class CurlRequest
{
    public string? Method { get; set; }

    public string? Url { get; set; }

    public IList<KeyValuePair<string, string>> Headers { get; } = new List<KeyValuePair<string, string>>();

    public string? Body { get; set; }
}

/// <summary>
/// Splits a <c>curl</c> command line into individual tokens, honoring single quotes, double quotes
/// and shell line-continuation characters (<c>\</c>, <c>^</c> and <c>`</c>).
/// </summary>
internal sealed class CurlTokenizer
{
    private readonly string text;
    private readonly List<string> tokens = new();
    private readonly StringBuilder current = new();
    private bool tokenStarted;
    private int position;

    public CurlTokenizer(string text)
    {
        this.text = text;
    }

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
        return new CurlCommandParser(parsedTokens).ParseTokens(curlCommand);
    }

    private CurlRequest ParseTokens(string curlCommand)
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

        return Build(curlCommand);
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

    private static int IndexOfChar(string text, char value)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private void ParseOption(string token)
    {
        string name;
        string? inlineValue = null;

        if (token.StartsWith("--", StringComparison.Ordinal))
        {
            int separator = IndexOfChar(token, '=');
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
            case "--data-urlencode":
            {
                dataParts.Add(ConsumeValue(name, inlineValue));
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

    private void AddHeader(string header)
    {
        int separator = IndexOfChar(header, ':');
        if (separator < 0)
        {
            throw new ArgumentException($"The header '{header}' is not in the expected 'Name: Value' format.");
        }

        string name = header.Substring(0, separator).Trim();
        string value = header.Substring(separator + 1).Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException($"The header '{header}' does not specify a name.");
        }

        AddHeaderValue(name, value);
    }

    private void AddHeaderValue(string name, string value)
    {
        request.Headers.Add(new KeyValuePair<string, string>(name, value));
    }

    private CurlRequest Build(string curlCommand)
    {
        if (dataParts.Count > 0)
        {
            request.Body = string.Join("&", dataParts);
        }

        if (string.IsNullOrEmpty(request.Url))
        {
            throw new ArgumentException("The cURL command does not contain a URL.", nameof(curlCommand));
        }

        return request;
    }
}
