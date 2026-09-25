namespace Mockly;

internal class Matcher(Func<RequestInfo, Task<bool>> predicate, string? displayText,
    Func<RequestInfo, Task<string?>>? describeMismatch = null)
{
    public override string ToString() => displayText ?? "Custom matcher";

    public Task<bool> IsMatch(RequestInfo request)
    {
        return predicate(request);
    }

    /// <summary>
    /// Describes why this matcher did not match the given request, for diagnostic reporting only.
    /// Returns <c>null</c> when no specific reason is available, in which case a generic message should be used.
    /// </summary>
    public Task<string?> DescribeMismatch(RequestInfo request)
    {
        return describeMismatch is not null ? describeMismatch(request) : Task.FromResult<string?>(null);
    }
}
