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
}
