using System.Diagnostics;
using Xunit;

namespace Jiifureit.JsonCliFormatter.Tests;

/// <summary>
/// End-to-end tests that exercise the compiled `jsonfmt` executable directly, covering
/// argument parsing, input-source selection, output destination, and error/exit-code
/// behavior that the in-process <see cref="JsonFormatter"/> unit tests do not reach.
/// </summary>
public class CliIntegrationTests
{
    private static readonly string ExecutablePath = Path.Combine(
        AppContext.BaseDirectory,
        OperatingSystem.IsWindows() ? "jsonfmt.exe" : "jsonfmt");

    private static string WithPlatformLineEndings(string value) =>
        value.ReplaceLineEndings(Environment.NewLine);

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string[] args, string? stdin = null)
    {
        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start jsonfmt process.");

        if (stdin is not null)
        {
            process.StandardInput.Write(stdin);
        }
        process.StandardInput.Close();

        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdOut, stdErr);
    }

    [Fact]
    public void Cli_FilePathPositionalArgument_ReadsAndFormatsFileContents()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"a":1,"b":2}""");

            var (exitCode, stdOut, stdErr) = RunCli([tempFile]);

            Assert.Equal(0, exitCode);
            Assert.Equal("", stdErr);
            Assert.Equal(WithPlatformLineEndings("{\n  \"a\": 1,\n  \"b\": 2\n}") + Environment.NewLine, stdOut);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Cli_InputOption_ReadsAndFormatsFileContents()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"a":1}""");

            var (exitCode, stdOut, stdErr) = RunCli(["-i", tempFile]);

            Assert.Equal(0, exitCode);
            Assert.Equal("", stdErr);
            Assert.Equal(WithPlatformLineEndings("{\n  \"a\": 1\n}") + Environment.NewLine, stdOut);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Cli_PositionalArgumentThatIsNotAnExistingFile_IsParsedAsJsonLiteral()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}"""]);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stdErr);
        Assert.Equal(WithPlatformLineEndings("{\n  \"a\": 1\n}") + Environment.NewLine, stdOut);
    }

    [Fact]
    public void Cli_NoPositionalArgumentOrInputOption_ReadsFromStandardInput()
    {
        var (exitCode, stdOut, stdErr) = RunCli([], stdin: """{"a":1}""");

        Assert.Equal(0, exitCode);
        Assert.Equal("", stdErr);
        Assert.Equal(WithPlatformLineEndings("{\n  \"a\": 1\n}") + Environment.NewLine, stdOut);
    }

    [Fact]
    public void Cli_NoOutputOption_WritesFormattedResultToStandardOutput()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}"""]);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stdErr);
        Assert.Equal(WithPlatformLineEndings("{\n  \"a\": 1\n}") + Environment.NewLine, stdOut);
    }

    [Fact]
    public void Cli_OutputOption_WritesFormattedResultToFileInsteadOfStandardOutput()
    {
        var outputFile = Path.GetTempFileName();
        try
        {
            var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}""", "-o", outputFile]);

            Assert.Equal(0, exitCode);
            Assert.Equal("", stdErr);
            Assert.Equal("", stdOut);
            Assert.Equal(
                WithPlatformLineEndings("{\n  \"a\": 1\n}") + Environment.NewLine,
                File.ReadAllText(outputFile));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    [Fact]
    public void Cli_CustomIndentOption_AppliesRequestedIndentWidth()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}""", "--indent", "4"]);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stdErr);
        Assert.Equal(WithPlatformLineEndings("{\n    \"a\": 1\n}") + Environment.NewLine, stdOut);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Cli_InvalidIndentOption_WritesErrorToStandardErrorAndExitsNonZero(string invalidIndent)
    {
        var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}""", "--indent", invalidIndent]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("--indent", stdErr);
    }

    [Fact]
    public void Cli_InvalidJsonInput_WritesErrorToStandardErrorAndExitsNonZero()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["{not valid json"]);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("Error", stdErr);
    }

    [Theory]
    [InlineData("-i")]
    [InlineData("--input")]
    public void Cli_InputOptionWithoutValue_WritesErrorAndExitsWithCode1(string option)
    {
        var (exitCode, stdOut, stdErr) = RunCli([option]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("Error", stdErr);
        Assert.Contains("requires a value", stdErr);
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--output")]
    public void Cli_OutputOptionWithoutValue_WritesErrorAndExitsWithCode1(string option)
    {
        var (exitCode, stdOut, stdErr) = RunCli([option]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("Error", stdErr);
        Assert.Contains("requires a value", stdErr);
    }

    [Fact]
    public void Cli_IndentOptionWithoutValue_WritesErrorAndExitsWithCode1()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["--indent"]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("Error", stdErr);
        Assert.Contains("requires a value", stdErr);
    }

    [Fact]
    public void Cli_InputOptionAtEndOfArgs_WritesErrorAndExitsWithCode1()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["""{"a":1}""", "-i"]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("Error", stdErr);
        Assert.Contains("requires a value", stdErr);
    }

    [Fact]
    public void Cli_InputOptionFollowedByAnotherOption_ReportsMissingValue()
    {
        var (exitCode, stdOut, stdErr) = RunCli(["--input", "--output", "formatted.json"]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", stdOut);
        Assert.Contains("--input requires a value", stdErr);
    }
}
