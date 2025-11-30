namespace Mockly;

public class Matcher(Func<RequestInfo, Task<bool>> predicate, string? displayText)
{
    public override string ToString() => displayText ?? "Custom matcher";

    public Task<bool> IsMatch(RequestInfo request)
    {
        return predicate(request);
    }
}
