namespace Mockly;

/// <summary>
/// Represents a builder that can construct an instance of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of object to build.</typeparam>
/// <remarks>
/// This interface is useful for integrating test data builders with Mockly's response configuration methods.
/// By implementing this interface, you can pass builders directly to methods like <see cref="RequestMockBuilder.RespondsWithJsonContent{T}(IResponseBuilder{T})"/>.
/// </remarks>
public interface IResponseBuilder<out T>
{
    /// <summary>
    /// Builds and returns an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>The constructed instance.</returns>
    T Build();
}
