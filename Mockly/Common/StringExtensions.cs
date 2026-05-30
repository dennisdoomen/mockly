using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

#pragma warning disable CA1307
#pragma warning disable CA1309

namespace Mockly.Common;

internal static class StringExtensions
{
    /// <summary>
    /// Parses an <c>application/x-www-form-urlencoded</c> string, such as a URI query string or a form body, into its
    /// individual name/value pairs, decoding both names and values.
    /// </summary>
    /// <param name="input">The url-encoded string to parse. A leading <c>?</c> is ignored.</param>
    /// <returns>The decoded name/value pairs in the order they appear in the input.</returns>
    public static IEnumerable<KeyValuePair<string, string>> ParseUrlEncoded(this string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            yield break;
        }

        string data = input!.StartsWith("?", StringComparison.Ordinal) ? input.Substring(1) : input;

        foreach (string pair in data.Split('&'))
        {
            if (pair.Length == 0)
            {
                continue;
            }

            int separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                yield return new KeyValuePair<string, string>(WebUtility.UrlDecode(pair), string.Empty);
            }
            else
            {
                string name = WebUtility.UrlDecode(pair.Substring(0, separatorIndex));
                string value = WebUtility.UrlDecode(pair.Substring(separatorIndex + 1));
                yield return new KeyValuePair<string, string>(name, value);
            }
        }
    }

    /// <summary>
    /// Determines whether the specified text matches the given wildcard pattern in its entirety.
    /// </summary>
    /// <param name="text">The text to be evaluated.</param>
    /// <param name="wildcardPattern">The wildcard pattern to match, allowing '?' for single character matches and '*' for multi-character matches.</param>
    /// <remarks>
    /// Unlike <see cref="MatchesWildcard"/>, the pattern is anchored, so the entire text must match the pattern rather
    /// than merely containing a match.
    /// </remarks>
    public static bool MatchesWildcardExactly(this string text, string wildcardPattern)
    {
        if (text.Equals(wildcardPattern))
        {
            return true;
        }

        string regexPattern = "^" + Regex.Escape(wildcardPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified text matches the given wildcard pattern.
    /// </summary>
    /// <param name="text">The text to be evaluated.</param>
    /// <param name="wildcardPattern">The wildcard pattern to match, allowing '?' for single character matches and '*' for multi-character matches.</param>
    public static bool MatchesWildcard(this string text, string wildcardPattern)
    {
        if (text.Equals(wildcardPattern))
        {
            return true;
        }

        string regexPattern = Regex.Escape(wildcardPattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");

        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Calculates how many characters two strings overlap while preserving order.
    /// </summary>
    /// <param name="text">The first string.</param>
    /// <param name="other">The second string.</param>
    /// <returns>
    /// The length of the shorter string when all of its characters can be found in
    /// the longer string in the same order; otherwise 0. For example, "abcdef" and
    /// "abef" return 4 ("abef" is a subsequence of "abcdef"), "abcdef" and "acf"
    /// return 3 ("acf" is a subsequence of "abcdef"), while "fedcba" and "abef"
    /// return 0.
    /// </returns>
    public static int CountOrderedOverlap(this string text, string other)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(other))
        {
            return 0;
        }

        // Determine which string is shorter to keep the logic symmetric with respect
        // to the input order while still using the extension method syntax.
        string longer;
        string shorter;

        if (text.Length >= other.Length)
        {
            longer = text;
            shorter = other;
        }
        else
        {
            longer = other;
            shorter = text;
        }

        int j = 0;

        // Walk the longer string once, advancing through the shorter string
        // whenever characters match. If we can consume all characters of the
        // shorter string in order, then it is a subsequence of the longer one.
        for (int i = 0; i < longer.Length && j < shorter.Length; i++)
        {
            if (longer[i] == shorter[j])
            {
                j++;
            }
        }

        return j;
    }
}

#pragma warning restore CA1307
#pragma warning restore CA1309
