using Xunit;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// SQL-A5: the three pathological inputs that guard the request boundary must fail (or succeed) inside a
/// child process with a hard budget, never hang or crash the test host.
/// </summary>
public sealed class ResourceIsolationTests
{
    [Theory]
    [InlineData("cqm-nested-not", "result=success")]
    [InlineData("cqm-big-select", "result=failure;code=cqm.limitExceeded")]
    [InlineData("overlay-flow", "result=failure;code=overlay.yamlSyntaxInvalid")]
    public void ResourceProbeStaysStructuredUnderMemoryAndTimeLimits(string scenario, string expected)
    {
        var result = ProcessIsolated.Run(scenario, maxSeconds: 20, maxMemoryMb: 256);

        Assert.False(result.TimedOut, $"{scenario} exceeded the child-process timeout. stderr: {result.StandardError}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expected, result.LastOutputLine, StringComparison.Ordinal);
    }
}
