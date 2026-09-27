using System.Text.Json;

namespace Mockly.Common;

internal static class JsonElementExtensions
{
    /// <summary>
    /// Compares two <see cref="JsonElement"/> objects for deep equality.
    /// </summary>
    /// <param name="expected">The expected <see cref="JsonElement"/> to compare.</param>
    /// <param name="actual">The actual <see cref="JsonElement"/> to compare against.</param>
    /// <returns><c>true</c> if both <see cref="JsonElement"/> objects are deeply equal; otherwise, <c>false</c>.</returns>
    public static bool JsonEquals(this JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                // Compare property count first
                var expectedProperties = expected.EnumerateObject().ToList();
                var actualProperties = actual.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

                if (expectedProperties.Count != actualProperties.Count)
                {
                    return false;
                }

                foreach (var expectedProperty in expectedProperties)
                {
                    if (!actualProperties.TryGetValue(expectedProperty.Name, out var actualValue))
                    {
                        return false;
                    }

                    if (!expectedProperty.Value.JsonEquals(actualValue))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.Array:
            {
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();

                if (expectedItems.Count != actualItems.Count)
                {
                    return false;
                }

                for (var i = 0; i < expectedItems.Count; i++)
                {
                    if (!expectedItems[i].JsonEquals(actualItems[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.String:
            {
                return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal);
            }

            case JsonValueKind.Number:
            {
                return expected.GetDecimal() == actual.GetDecimal();
            }

            case JsonValueKind.True:
            case JsonValueKind.False:
            {
                return expected.GetBoolean() == actual.GetBoolean();
            }

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            {
                return true;
            }

            default:
            {
                return string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Compares two <see cref="JsonElement"/> objects and returns a human-readable description of every property
    /// or value that differs, for diagnostic reporting only.
    /// </summary>
    /// <param name="expected">The expected <see cref="JsonElement"/> to compare.</param>
    /// <param name="actual">The actual <see cref="JsonElement"/> to compare against.</param>
    /// <returns>A list of mismatch descriptions. Empty when the two elements are equivalent.</returns>
    public static IReadOnlyList<string> Diff(this JsonElement expected, JsonElement actual)
    {
        var differences = new List<string>();
        CollectDifferences(expected, actual, string.Empty, differences);
        return differences;
    }

    private static void CollectDifferences(JsonElement expected, JsonElement actual, string path, List<string> differences)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            differences.Add($"expected {Describe(path)} to be {Format(expected)} but found {Format(actual)}");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var actualProperties = actual.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);

                foreach (var expectedProperty in expected.EnumerateObject())
                {
                    string childPath = path.Length == 0 ? expectedProperty.Name : $"{path}.{expectedProperty.Name}";

                    if (!actualProperties.TryGetValue(expectedProperty.Name, out var actualValue))
                    {
                        differences.Add($"expected {Describe(childPath)} to be {Format(expectedProperty.Value)} but it was missing");
                    }
                    else
                    {
                        CollectDifferences(expectedProperty.Value, actualValue, childPath, differences);
                    }
                }

                break;
            }

            case JsonValueKind.Array:
            {
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();

                if (expectedItems.Count != actualItems.Count)
                {
                    differences.Add(
                        $"expected {Describe(path)} to have {expectedItems.Count} item(s) but found {actualItems.Count}");
                }
                else
                {
                    for (var i = 0; i < expectedItems.Count; i++)
                    {
                        CollectDifferences(expectedItems[i], actualItems[i], $"{path}[{i}]", differences);
                    }
                }

                break;
            }

            default:
            {
                if (!expected.JsonEquals(actual))
                {
                    differences.Add($"expected {Describe(path)} to be {Format(expected)} but found {Format(actual)}");
                }

                break;
            }
        }
    }

    private static string Describe(string path)
    {
        return path.Length == 0 ? "the body" : $"property \"{path}\"";
    }

    private static string Format(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Null or JsonValueKind.Undefined => "null",
            _ => element.GetRawText()
        };
    }
}
