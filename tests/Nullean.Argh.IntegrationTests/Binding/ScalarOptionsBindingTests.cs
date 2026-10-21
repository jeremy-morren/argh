using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// TimeSpan, DateTime, DateTimeOffset, DateOnly, TimeOnly, Uri, FileInfo and DirectoryInfo members,
/// plain and nullable, on an [AsParameters] DTO (<c>as-params-scalars</c>) and on namespace options (<c>scalars-ns</c>).
/// </summary>
public class ScalarOptionsBindingTests
{
	private static readonly string[] PlainArgs =
	[
		"--span", "5m", "--stamp", "2024-06-15T14:30:00Z", "--offset", "2024-06-15T14:30:00+10:00",
		"--day", "2024-06-15", "--clock", "14:30", "--link", "https://example.com/a", "--doc", "notes.txt", "--folder", "out"
	];

	private static readonly string[] NullableArgs =
	[
		"--span-opt", "01:30:00", "--stamp-opt", "2024-01-02T03:04:05Z", "--offset-opt", "2024-01-02T03:04:05-05:00",
		"--day-opt", "2024-07-01", "--clock-opt", "17:45:05", "--link-opt", "https://example.org/b", "--doc-opt", "extra.json",
		"--folder-opt", "cache"
	];

	private static readonly string[] PlainOutput =
	[
		"span=300", "stamp=2024-06-15T14:30:00.0000000Z", "offset=2024-06-15T14:30:00.0000000+10:00", "day=2024-06-15",
		"clock=14:30:00", "link=https://example.com/a", "doc=notes.txt", "folder=out"
	];

	private static readonly string[] NullableOutput =
	[
		"span-opt=5400", "stamp-opt=2024-01-02T03:04:05.0000000Z", "offset-opt=2024-01-02T03:04:05.0000000-05:00",
		"day-opt=2024-07-01", "clock-opt=17:45:05", "link-opt=https://example.org/b", "doc-opt=extra.json", "folder-opt=cache"
	];

	private static readonly string[] NullableOmittedOutput =
	[
		"span-opt=none", "stamp-opt=none", "offset-opt=none", "day-opt=none", "clock-opt=none", "link-opt=none",
		"doc-opt=none", "folder-opt=none"
	];

	[Fact]
	public void AsParams_plain_scalars_bind_and_omitted_nullables_are_null()
	{
		var result = CliHostRunner.Run(["as-params-scalars", .. PlainArgs]);
		result.ExitCode.Should().Be(0);
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOmittedOutput]);
	}

	[Fact]
	public void AsParams_nullable_scalars_bind_when_given()
	{
		var result = CliHostRunner.Run(["as-params-scalars", .. PlainArgs, .. NullableArgs]);
		result.ExitCode.Should().Be(0);
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOutput]);
	}

	[Theory]
	[MemberData(nameof(InvalidValues))]
	public void AsParams_invalid_scalar_exits_2(string flag, string value, string typeName)
	{
		var result = CliHostRunner.Run(["as-params-scalars", .. PlainArgs, flag, value]);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain($"Error: invalid {typeName} for {flag}: '{value}'.");
	}

	[Fact]
	public void NamespaceOptions_plain_scalars_bind_and_omitted_nullables_are_null()
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs, "show"]);
		result.ExitCode.Should().Be(0);
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOmittedOutput]);
	}

	[Fact]
	public void NamespaceOptions_nullable_scalars_bind_before_the_command()
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs, .. NullableArgs, "show"]);
		result.ExitCode.Should().Be(0);
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOutput]);
	}

	[Fact]
	public void NamespaceOptions_nullable_scalars_bind_after_the_command()
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs, "show", .. NullableArgs]);
		result.ExitCode.Should().Be(0);
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOutput]);
	}

	[Theory]
	[MemberData(nameof(InvalidValues))]
	public void NamespaceOptions_invalid_scalar_before_the_command_exits_2(string flag, string value, string typeName)
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs, flag, value, "show"]);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain($"Error: invalid {typeName} for {flag}: '{value}'.");
	}

	[Theory]
	[MemberData(nameof(InvalidValues))]
	public void NamespaceOptions_invalid_scalar_after_the_command_exits_2(string flag, string value, string typeName)
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs, "show", flag, value]);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().Contain($"Error: invalid {typeName} for {flag}: '{value}'.");
		CliHostRunner.StdoutText(result).Should().BeEmpty();
	}

	public static TheoryData<string, string, string> InvalidValues => new()
	{
		{ "--span", "nope", "TimeSpan" },
		{ "--span-opt", "nope", "TimeSpan" },
		{ "--stamp", "nope", "DateTime" },
		{ "--stamp-opt", "nope", "DateTime" },
		{ "--offset", "nope", "DateTimeOffset" },
		{ "--offset-opt", "nope", "DateTimeOffset" },
		{ "--day", "nope", "DateOnly" },
		{ "--day-opt", "nope", "DateOnly" },
		{ "--clock", "25:00", "TimeOnly" },
		{ "--clock-opt", "25:00", "TimeOnly" },
		{ "--link", "http://[", "URI" },
		{ "--link-opt", "http://[", "URI" },
	};

	private static string[] Lines(ProcNet.ProcessCaptureResult result) =>
		CliHostRunner.StdoutText(result).Split('\n', StringSplitOptions.RemoveEmptyEntries);

	[Fact]
	public void NamespaceOptions_required_scalars_bind_after_the_command()
	{
		var result = CliHostRunner.Run(["scalars-ns", "show", .. PlainArgs]);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOmittedOutput]);
	}

	[Fact]
	public void NamespaceOptions_required_scalars_bind_on_both_sides_of_the_command()
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs[..8], "show", .. PlainArgs[8..]]);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		Lines(result).Should().BeEquivalentTo([.. PlainOutput, .. NullableOmittedOutput]);
	}

	[Fact]
	public void NamespaceOptions_missing_required_member_is_reported_after_the_whole_line_is_read()
	{
		var result = CliHostRunner.Run(["scalars-ns", .. PlainArgs[2..], "show"]);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result))
			.Should().StartWith("Error: missing required flag --span.")
			.And.Contain("Run 'Nullean.Argh.Tests.CliHost scalars-ns show --help' for usage.");
	}

	[Fact]
	public void NamespaceOptions_with_required_members_show_namespace_help_without_a_command()
	{
		var result = CliHostRunner.Run("scalars-ns");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Should().Contain("show");
	}
}
