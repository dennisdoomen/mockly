using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Mockly.FluentAssertions;

/// <summary>
/// Provides FluentAssertions assertion methods for <see cref="HttpMock"/>.
/// </summary>
public class HttpMockAssertions(HttpMock subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<HttpMock, HttpMockAssertions>(subject, assertionChain)
{
    private readonly AssertionChain assertionChain = assertionChain;

    /// <inheritdoc/>
    protected override string Identifier => "http mock";

    /// <summary>
    /// Asserts that all configured HTTP mocks have been called the expected number of times.
    /// </summary>
    /// <remarks>
    /// Each mock without an invocation limit must be called at least once.
    /// Each mock configured with <c>Times(n)</c>, <c>Once()</c>, or <c>Twice()</c> must be called at least that many times.
    /// When the assertion fails, the error message lists all mocks that were not called (enough).
    /// </remarks>
    /// <param name="because">A formatted phrase explaining why the assertion should be satisfied.</param>
    /// <param name="becauseArgs">Zero or more objects to format using the placeholders in <paramref name="because"/>.</param>
    public AndConstraint<HttpMockAssertions> HaveAllRequestsCalled(string because = "", params object[] becauseArgs)
    {
        IReadOnlyList<RequestMock> uninvokedMocks = Subject.GetUninvokedMocks().ToList();

        assertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(uninvokedMocks.Count == 0)
            .FailWith(BuildFailureMessage(uninvokedMocks));

        return new AndConstraint<HttpMockAssertions>(this);
    }

    private static string BuildFailureMessage(IReadOnlyList<RequestMock> uninvokedMocks)
    {
        if (uninvokedMocks.Count == 0)
        {
            return string.Empty;
        }

        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine(
            "Expected all HTTP mocks to have been called{reason}, but the following mock(s) were not called (enough):");

        foreach (RequestMock mock in uninvokedMocks)
        {
            messageBuilder.Append("  - ");
            messageBuilder.Append(mock.ToString());

            if (mock.MaxInvocations is not null)
            {
                messageBuilder.Append(
                    $" (expected {mock.MaxInvocations} call(s), but was called {mock.InvocationCount} time(s))");
            }
            else
            {
                messageBuilder.Append(" (was never called)");
            }

            messageBuilder.AppendLine();
        }

        return messageBuilder.ToString();
    }
}
