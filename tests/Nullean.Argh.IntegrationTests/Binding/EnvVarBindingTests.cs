using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

public class EnvVarBindingTests
{
	[Fact]
	public void Env_attribute_flag_takes_value_from_cli_when_provided()
	{
		var result = CliHostRunner.Run("env-cmd", "--token", "cli-token");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-token:cli-token");
	}

	[Fact]
	public void Env_attribute_flag_falls_back_to_env_var_when_cli_flag_absent()
	{
		var env = new Dictionary<string, string> { ["MY_APP_TOKEN"] = "env-token-value" };
		var result = CliHostRunner.Run(env, "env-cmd");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-token:env-token-value");
	}

	[Fact]
	public void Env_attribute_flag_is_null_when_neither_cli_nor_env_provided()
	{
		var result = CliHostRunner.Run("env-cmd");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-token:");
	}

	[Fact]
	public void Env_attribute_cli_flag_takes_precedence_over_env_var()
	{
		var env = new Dictionary<string, string> { ["MY_APP_TOKEN"] = "env-value" };
		var result = CliHostRunner.Run(env, "env-cmd", "--token", "cli-wins");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-token:cli-wins");
	}

	[Fact]
	public void Env_attribute_required_flag_satisfied_by_env_var()
	{
		var env = new Dictionary<string, string> { ["MY_APP_SECRET"] = "super-secret" };
		var result = CliHostRunner.Run(env, "env-required");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-secret:super-secret");
	}

	[Fact]
	public void Env_attribute_required_flag_fails_when_neither_cli_nor_env_provided()
	{
		var result = CliHostRunner.Run("env-required");
		result.ExitCode.Should().NotBe(0);
		CliHostRunner.StderrText(result).Should().Contain("--secret");
	}

	[Fact]
	public void Env_attribute_bool_flag_set_by_env_var_true()
	{
		var env = new Dictionary<string, string> { ["MY_APP_VERBOSE"] = "true" };
		var result = CliHostRunner.Run(env, "env-bool");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-verbose:True");
	}

	[Fact]
	public void Env_attribute_bool_flag_env_var_0_means_false()
	{
		var env = new Dictionary<string, string> { ["MY_APP_VERBOSE"] = "0" };
		var result = CliHostRunner.Run(env, "env-bool");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-verbose:False");
	}

	[Fact]
	public void Env_attribute_bool_flag_cli_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["MY_APP_VERBOSE"] = "false" };
		var result = CliHostRunner.Run(env, "env-bool", "--verbose");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-verbose:True");
	}

	[Fact]
	public void Env_attribute_shown_in_help_output()
	{
		var result = CliHostRunner.Run("env-cmd", "--help");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Should().Contain("[env: MY_APP_TOKEN]");
	}

	[Fact]
	public void Env_on_global_option_falls_back_to_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_API_URL"] = "https://api.example.com" };
		var result = CliHostRunner.Run(env, "env-global");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-global-api-url:https://api.example.com");
	}

	[Fact]
	public void Env_on_global_option_cli_flag_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_API_URL"] = "https://from-env.example.com" };
		var result = CliHostRunner.Run(env, "env-global", "--api-url", "https://from-cli.example.com");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-global-api-url:https://from-cli.example.com");
	}

	[Fact]
	public void Env_on_global_option_cli_flag_before_the_command_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_API_URL"] = "fromenv" };
		var result = CliHostRunner.Run(env, "--api-url", "cli", "env-global");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-global-api-url:cli");
	}

	[Fact]
	public void Env_on_global_option_cli_flag_between_namespace_and_command_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["ARGH_TEST_QUOTA"] = "9" };
		var result = CliHostRunner.Run(env, "numeric-ns", "--quota", "3", "show");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		// NumericNsShow prints the members joined by |; quota is the 14th.
		CliHostRunner.StdoutText(result).Trim().Split('|')[13].Should().Be("3");
	}

	[Fact]
	public void Env_on_global_option_null_when_neither_cli_nor_env()
	{
		var result = CliHostRunner.Run("env-global");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-global-api-url:");
	}

	[Fact]
	public void Env_on_namespace_option_falls_back_to_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_NS_KEY"] = "from-env-key" };
		var result = CliHostRunner.Run(env, "env-ns", "key");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-ns-key:from-env-key");
	}

	[Fact]
	public void Env_on_namespace_option_cli_flag_before_the_command_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_NS_KEY"] = "nsenv" };
		var result = CliHostRunner.Run(env, "env-ns", "--key", "cli", "key");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-ns-key:cli");
	}

	[Fact]
	public void Env_on_namespace_option_cli_flag_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["TEST_NS_KEY"] = "from-env" };
		var result = CliHostRunner.Run(env, "env-ns", "key", "--key", "from-cli");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-ns-key:from-cli");
	}

	[Fact]
	public void Env_on_positional_arguments_falls_back_to_env_vars()
	{
		var env = new Dictionary<string, string> { ["ARGH_TEST_POS_NAME"] = "from-env", ["ARGH_TEST_POS_COUNT"] = "3" };
		var result = CliHostRunner.Run(env, "env-positional");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-positional:from-env:3");
	}

	[Fact]
	public void Env_on_positional_argument_cli_value_wins_over_env_var()
	{
		var env = new Dictionary<string, string> { ["ARGH_TEST_POS_NAME"] = "from-env", ["ARGH_TEST_POS_COUNT"] = "3" };
		var result = CliHostRunner.Run(env, "env-positional", "from-cli");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-positional:from-cli:3");
	}

	[Fact]
	public void Env_on_optional_positional_argument_keeps_its_default_when_unset()
	{
		var result = CliHostRunner.Run("env-positional", "x");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("env-positional:x:1");
	}

	[Fact]
	public void Env_on_required_positional_argument_fails_when_neither_cli_nor_env_provided()
	{
		var result = CliHostRunner.Run("env-positional");
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain("missing required argument <name>");
	}

	[Fact]
	public void Env_on_positional_argument_rejects_an_invalid_value()
	{
		var env = new Dictionary<string, string> { ["ARGH_TEST_POS_COUNT"] = "many" };
		var result = CliHostRunner.Run(env, "env-positional", "x");
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain("'many'");
	}
}
