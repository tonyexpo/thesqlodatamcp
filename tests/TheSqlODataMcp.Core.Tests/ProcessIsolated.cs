using System.Diagnostics;
using System.Globalization;

namespace TheSqlODataMcp.Core.Tests;

/// <summary>
/// SQL-A5: runs a resource probe in a child process with a hard heap budget and timeout, so a regression
/// to an exponential or recursive path fails the parent test instead of hanging or killing it.
/// </summary>
internal static class ProcessIsolated
{
    public static ProcessIsolatedResult Run(string scenario, int maxSeconds, int maxMemoryMb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMemoryMb);

        var probe = Path.Combine(AppContext.BaseDirectory, "probe", "TheSqlODataMcp.Probe.dll");
        if (!File.Exists(probe))
        {
            throw new FileNotFoundException("The isolated resource probe was not copied to the test output.", probe);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(probe);
        startInfo.ArgumentList.Add(scenario);
        // The documented format is hexadecimal bytes. A regressed allocation path hits the limit instead
        // of consuming the host's memory.
        startInfo.Environment["DOTNET_GCHeapHardLimit"] = (maxMemoryMb * 1024L * 1024L).ToString("X", CultureInfo.InvariantCulture);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The isolated resource probe could not be started.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(maxSeconds * 1000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return new ProcessIsolatedResult(
                null,
                TimedOut: true,
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult());
        }

        return new ProcessIsolatedResult(
            process.ExitCode,
            TimedOut: false,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    public static string LastLine(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .LastOrDefault() ?? string.Empty;
}

internal sealed record ProcessIsolatedResult(int? ExitCode, bool TimedOut, string StandardOutput, string StandardError)
{
    public string LastOutputLine => ProcessIsolated.LastLine(StandardOutput);
}
