using Microsoft.OpenApi;

namespace Mockly;

internal static class MediaTypes
{
    public static bool IsJson(string mediaType)
    {
        string type = StripParameters(mediaType);

        return type.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            type.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the media type of <paramref name="content"/> that describes <paramref name="contentType"/>, preferring an
    /// exact match over a range such as <c>application/*</c> or <c>*/*</c>.
    /// </summary>
    public static IOpenApiMediaType? FindBestMatch(IDictionary<string, IOpenApiMediaType> content, string contentType)
    {
        string type = StripParameters(contentType);
        string range = type.Split('/')[0] + "/*";

        return Find(content, type) ?? Find(content, range) ?? Find(content, "*/*");
    }

    public static string StripParameters(string mediaType)
    {
        return mediaType.Split(';')[0].Trim();
    }

    private static IOpenApiMediaType? Find(IDictionary<string, IOpenApiMediaType> content, string mediaType)
    {
        return content
            .Where(pair => StripParameters(pair.Key).Equals(mediaType, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }
}
