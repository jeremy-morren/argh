using System.Runtime.InteropServices;
using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// <c>FileInfo</c>, <c>DirectoryInfo</c> and <c>Uri</c> values: an empty value, and whatever the type's constructor
/// rejects, is a parse error (exit 2) in every binding position. A <c>Uri</c> is absolute unless <c>[UriOptions]</c> says otherwise.
/// </summary>
public class PathUriBindingTests
{
	private static readonly Dictionary<string, string> NoColor = new(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };

	private static string Stderr(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StderrText(result));

	private static string Stdout(ProcNet.ProcessCaptureResult result) => ConsoleOutput.Normalize(CliHostRunner.StdoutText(result));

	private static ProcNet.ProcessCaptureResult Run(params string[] args) => CliHostRunner.Run(NoColor, args);

	private static ProcNet.ProcessCaptureResult RunWithEnv(Dictionary<string, string> environment, params string[] args) =>
		CliHostRunner.Run(new Dictionary<string, string>(environment) { ["NO_COLOR"] = "1" }, args);

	private static void ShouldFail(ProcNet.ProcessCaptureResult result, string error, string? row = null, string? runHint = null)
	{
		result.ExitCode.Should().Be(2);
		var err = Stderr(result);
		err.Should().StartWith(error + "\n");
		if (row is not null)
			err.Should().Contain(row);
		if (runHint is not null)
			err.Should().Contain($"Run '{CliHostPaths.CliHostAssemblyName} {runHint} --help' for usage.");
	}

	[Theory]
	[InlineData("--file=", "file-cmd")]
	[InlineData("--dir=", "dir-cmd")]
	[InlineData("--file=", "validate-existing-file")]
	public void Empty_required_path_is_a_parse_error(string flag, string command) =>
		ShouldFail(Run(command, flag), $"Error: invalid path for {flag.TrimEnd('=')}: ''. The path is empty.",
			$"{flag.TrimEnd('=')} <", command);

	[Fact]
	public void Empty_path_is_a_parse_error_in_every_position()
	{
		ShouldFail(Run("path-inputs", ""), "Error: invalid path for <source>: ''. The path is empty.");
		ShouldFail(Run("path-inputs", "a.txt", "--target", ""), "Error: invalid path for --target: ''. The path is empty.",
			"--target <dir>", "path-inputs");
		ShouldFail(Run("path-inputs", "a.txt", "--files", "x", "--files", ""), "Error: invalid path for --files: ''. The path is empty.");
		ShouldFail(Run("path-uri-ns", "show", "--doc="), "Error: invalid path for --doc: ''. The path is empty.");
	}

	[Fact]
	public void Empty_path_from_an_environment_variable_names_the_variable()
	{
		var result = RunWithEnv(new() { ["ARGH_TEST_DOC"] = "" }, "path-inputs", "a.txt");
		ShouldFail(result, "Error: invalid path for --doc: ''. The path is empty.");
		Stderr(result).Should().Contain("The value comes from the environment variable ARGH_TEST_DOC.");
	}

	[Fact]
	public void Empty_environment_variable_is_unset_by_default() =>
		Stdout(RunWithEnv(new() { ["ARGH_TEST_NS_DOC"] = "" }, "path-uri-ns", "show")).Should().Contain("path-uri-ns:doc:[none] base:[none]");

	[Fact]
	public void Paths_bind_in_every_position()
	{
		var result = RunWithEnv(new() { ["ARGH_TEST_DOC"] = "d.txt" }, "path-inputs", "a.txt", "--target", "t", "--files", "x", "--files", "y",
			"--dirs", "a,b");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("paths:[a.txt] target:[t] doc:[d.txt] files:[x;y] dirs:[a;b]");
	}

	// Whitespace isn't rejected up front: it can name a file, so the constructor decides. Windows rejects it, Linux and macOS don't.
	[Fact]
	public void Whitespace_path_is_up_to_the_platform()
	{
		var result = Run("file-cmd", "--file", " ");
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			ShouldFail(result, "Error: invalid path for --file: ' '. The path is empty.", "--file <file>", "file-cmd");
		else
		{
			result.ExitCode.Should().Be(0, Stderr(result));
			Stdout(result).Should().Contain("file: \n");
		}
	}

	[Theory]
	[InlineData("uri-cmd", "--uri", "", "Error: invalid URI for --uri: ''. The URI is empty.")]
	[InlineData("uri-cmd", "--uri", " ", "Error: invalid URI for --uri: ' '. Expected an absolute URI.")]
	[InlineData("uri-cmd", "--uri", "not a uri", "Error: invalid URI for --uri: 'not a uri'. Expected an absolute URI.")]
	[InlineData("uri-cmd", "--uri", "::", "Error: invalid URI for --uri: '::'. Expected an absolute URI.")]
	[InlineData("uri-cmd", "--uri", "http://", "Error: invalid URI for --uri: 'http://'. Invalid URI: The hostname could not be parsed.")]
	[InlineData("validate-uri-scheme-opt", "--endpoint", "", "Error: invalid URI for --endpoint: ''. The URI is empty.")]
	[InlineData("optional-uri-as-params", "--endpoint", "", "Error: invalid URI for --endpoint: ''. The URI is empty.")]
	[InlineData("uri-inputs", "--path", "", "Error: invalid URI for --path: ''. The URI is empty.")]
	[InlineData("uri-inputs", "--path", "https://x", "Error: invalid URI for --path: 'https://x'. Expected a relative URI.")]
	[InlineData("uri-inputs", "--links", "", "Error: invalid URI for --links: ''. The URI is empty.")]
	[InlineData("uri-dto", "--site", "s", "Error: invalid URI for --site: 's'. Expected an absolute URI.")]
	[InlineData("uri-dto", "--path", "https://x", "Error: invalid URI for --path: 'https://x'. Expected a relative URI.")]
	public void Invalid_uri_is_a_parse_error(string command, string flag, string value, string error)
	{
		string[] args = command switch
		{
			"uri-inputs" => [command, "https://e", flag, value],
			"uri-dto" when flag != "--site" => [command, "--site", "https://s", flag, value],
			_ => [command, flag, value],
		};
		ShouldFail(Run(args), error, flag + " <", command);
	}

	[Fact]
	public void Invalid_uri_positional_is_a_parse_error()
	{
		ShouldFail(Run("uri-inputs", ""), "Error: invalid URI for <endpoint>: ''. The URI is empty.");
		ShouldFail(Run("uri-inputs", "rel"), "Error: invalid URI for <endpoint>: 'rel'. Expected an absolute URI.");
		ShouldFail(Run("uri-variadic", "a", "https://x"), "Error: invalid URI for <paths>: 'https://x'. Expected a relative URI.");
	}

	[Fact]
	public void Invalid_uri_from_an_environment_variable_names_the_variable()
	{
		var result = RunWithEnv(new() { ["ARGH_TEST_HOME"] = "nope" }, "uri-inputs", "https://e");
		ShouldFail(result, "Error: invalid URI for --home: 'nope'. Expected an absolute URI.");
		Stderr(result).Should().Contain("The value comes from the environment variable ARGH_TEST_HOME.");

		result = RunWithEnv(new() { ["ARGH_TEST_NS_BASE"] = "https://x" }, "path-uri-ns", "show");
		ShouldFail(result, "Error: invalid URI for --base: 'https://x'. Expected a relative URI.");
		Stderr(result).Should().Contain("The value comes from the environment variable ARGH_TEST_NS_BASE.");
	}

	[Fact]
	public void Uris_bind_with_their_kind_in_every_position()
	{
		var result = RunWithEnv(new() { ["ARGH_TEST_HOME"] = "https://h" }, "uri-inputs", "https://e", "--mirror", "https://m",
			"--path", "a/b", "--link", "c", "--links", "https://z", "--links", "d/e");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("uris:[https://e/] mirror:[https://m/] path:[a/b] link:[c] home:[https://h/] links:[https://z/;d/e]");

		result = Run("uri-dto", "--site", "https://s", "--path", "p", "--link", "https://l");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("uri-dto:[https://s/] path:[p] link:[https://l/]");

		result = Run("uri-variadic", "a", "b/c");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("uri-variadic:a;b/c");

		result = Run("path-uri-ns", "--base", "a/b", "--doc", "d.txt", "show");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("path-uri-ns:doc:[d.txt] base:[a/b]");
	}

	[Fact]
	public void Absent_optional_values_stay_null()
	{
		var result = Run("uri-inputs", "https://e");
		result.ExitCode.Should().Be(0, Stderr(result));
		Stdout(result).Should().Contain("uris:[https://e/] mirror:[none] path:[none] link:[none] home:[none] links:[none]");
	}

	[Fact]
	public void Help_shows_a_uri_kind_that_isnt_absolute()
	{
		var help = Stdout(Run("uri-inputs", "--help"));
		help.Should().Contain("--mirror <uri>       An optional absolute URI.\n");
		help.Should().Contain("--path <uri>         A relative URI. [relative]\n");
		help.Should().Contain("--link <uri>         A relative or absolute URI. [relative or absolute]\n");
		help.Should().Contain("--links <values>     [repeatable] Relative or absolute URIs. [relative or absolute]\n");
	}
}
