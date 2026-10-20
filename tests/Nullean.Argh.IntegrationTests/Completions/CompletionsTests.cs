using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Completions;

public class CompletionsTests
{
	[Fact]
	public void Zsh_nonempty()
	{
		var result = CliHostRunner.Run("__completion", "zsh");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Fish_nonempty()
	{
		var result = CliHostRunner.Run("__completion", "fish");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Zsh_contains_complete_keyword()
	{
		var result = CliHostRunner.Run("__completion", "zsh");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().Contain("complete");
	}

	[Fact]
	public void Fish_contains_complete_keyword()
	{
		var result = CliHostRunner.Run("__completion", "fish");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().Contain("complete");
	}

	[Theory]
	[InlineData("powershell")]
	[InlineData("pwsh")]
	public void PowerShell_registers_native_completer(string shell)
	{
		var result = CliHostRunner.Run("__completion", shell);
		result.ExitCode.Should().Be(0);
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("Register-ArgumentCompleter -Native");
		stdout.Should().Contain("__complete powershell");
		stdout.Should().NotContain("{0}");
	}

	[Fact]
	public void Complete_bash_root_emits_command_names()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--");
		result.ExitCode.Should().Be(0);
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("hello");
		stdout.Should().Contain("storage");
	}

	[Fact]
	public void Complete_bash_after_storage_namespace_emits_subcommands()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "storage", "");
		result.ExitCode.Should().Be(0);
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("blob");
		stdout.Should().Contain("list");
	}

	[Theory]
	[InlineData("powershell")]
	[InlineData("pwsh")]
	public void Complete_powershell_after_storage_namespace_emits_subcommands(string shell)
	{
		var result = CliHostRunner.Run("__complete", shell, "--", "storage", "");
		result.ExitCode.Should().Be(0);
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("blob");
		stdout.Should().Contain("list");
	}

	[Fact]
	public void Complete_powershell_filters_by_partial_word()
	{
		var result = CliHostRunner.Run("__complete", "powershell", "--", "sto");
		result.ExitCode.Should().Be(0);
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("storage");
		stdout.Should().NotContain("hello");
	}

	[Fact]
	public void Complete_filters_by_partial_word_ignoring_case()
	{
		var result = CliHostRunner.Run("__complete", "powershell", "--", "STO");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().Be("storage\n");
	}

	[Fact]
	public void Complete_enum_flag_value_uses_cli_names()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "enum-value-cmd", "--palette", "");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().Be("fire-red\ngreen\nocean-blue\n");
	}

	[Fact]
	public void Complete_enum_flag_value_filters_by_partial()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "enum-value-cmd", "--palette", "f");
		CliHostRunner.StdoutText(result).Should().Be("fire-red\n");
	}

	[Fact]
	public void Complete_enum_flag_value_after_short_alias()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "enum-cmd", "-c", "");
		CliHostRunner.StdoutText(result).Should().Be("blue\nred\n");
	}

	[Fact]
	public void Complete_inline_flag_value_keeps_flag_prefix()
	{
		var result = CliHostRunner.Run("__complete", "zsh", "--", "enum-value-cmd", "--palette=o");
		CliHostRunner.StdoutText(result).Should().Be("--palette=ocean-blue\n");
	}

	[Fact]
	public void Complete_flag_value_after_bash_equals_word_break()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "enum-value-cmd", "--palette", "=", "g");
		CliHostRunner.StdoutText(result).Should().Be("green\n");
	}

	[Theory]
	[InlineData("--severity")]
	[InlineData("hello", "--severity")]
	[InlineData("storage", "--severity")]
	[InlineData("storage", "blob", "upload", "--severity")]
	public void Complete_global_enum_flag_value_in_any_scope(params string[] words)
	{
		var result = CliHostRunner.Run(["__complete", "bash", "--", .. words, ""]);
		CliHostRunner.StdoutText(result).Should().Be("information\ntrace\nwarning\n");
	}

	[Fact]
	public void Complete_enum_collection_flag_value()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "color-set", "--colors", "red", "--colors", "");
		CliHostRunner.StdoutText(result).Should().Be("blue\nred\n");
	}

	[Fact]
	public void Complete_allowed_values_flag_value()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "validate-allowed", "--env", "");
		CliHostRunner.StdoutText(result).Should().Be("dev\nprod\nstaging\n");
	}

	[Fact]
	public void Complete_allowed_values_with_quotes_are_written_as_typed()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "schema-quoted-values", "--quote", "");
		CliHostRunner.StdoutText(result).Should().Be("C:\\temp\\\nit's\nplain\nsay \"hi\"\n");
	}

	[Fact]
	public void Complete_open_ended_flag_value_emits_nothing()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "hello", "--name", "");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().BeEmpty();
	}

	[Fact]
	public void Complete_flag_after_enum_flag_lists_flags()
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", "hello", "--severity", "--n");
		CliHostRunner.StdoutText(result).Should().Be("--name\n");
	}

	[Theory]
	[InlineData("hello")]
	[InlineData("storage")]
	public void Complete_command_and_namespace_flags_include_help(string word)
	{
		var result = CliHostRunner.Run("__complete", "bash", "--", word, "-");
		var stdout = CliHostRunner.StdoutText(result);
		stdout.Should().Contain("--help\n");
		stdout.Should().Contain("-h\n");
	}

	[Fact]
	public void Complete_unknown_shell_exits_nonzero()
	{
		var result = CliHostRunner.Run("__complete", "tcsh", "--");
		result.ExitCode.Should().Be(2);
	}
}
