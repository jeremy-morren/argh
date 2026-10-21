using System.Text.Json;
using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Defaults and value lists the generator writes back as C# literals: <c>decimal</c>, <c>float</c>, non-finite, <c>char</c>,
/// narrow integer and enum defaults compile and bind, and floating-point value lists compare with <c>.Equals</c>.
/// </summary>
public class NumericLiteralTests
{
	private static Dictionary<string, string> Env(string? culture = null)
	{
		var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["NO_COLOR"] = "1" };
		if (culture is not null)
			env["ARGH_TEST_CULTURE"] = culture;
		return env;
	}

	private static string[] OutputLines(ProcNet.ProcessCaptureResult result) =>
		CliHostRunner.StdoutText(result).Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Where(l => !l.StartsWith("[tests:", StringComparison.Ordinal)).ToArray();

	private static string HelpLine(ProcNet.ProcessCaptureResult result, string flag) =>
		ConsoleOutput.Normalize(CliHostRunner.StdoutText(result)).Split('\n')
			.Single(l => l.TrimStart().StartsWith(flag + " ", StringComparison.Ordinal)).TrimEnd();

	[Fact]
	public void Method_parameter_and_dto_defaults_bind_to_their_declared_values()
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		OutputLines(result).Should().Equal(
			"1.25|0.5|Infinity||1.5|1.5|2.5|NaN|-Infinity|,|-5|18446744073709551615|High||",
			"3|Mid|none|7|-5|5|8080|200|-5|79228162514264337593543950335|-0|2.5|null|Mid|3|Warning|'");
	}

	[Fact]
	public void Defaults_bind_the_same_under_another_culture()
	{
		var result = CliHostRunner.Run(Env("de-DE"), "numeric-defaults");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		OutputLines(result)[0].Should().Be("1.25|0.5|Infinity||1.5|1.5|2.5|NaN|-Infinity|,|-5|18446744073709551615|High||");
	}

	[Fact]
	public void Options_member_defaults_bind_to_their_declared_values()
	{
		var result = CliHostRunner.Run(Env(), "numeric-ns", "show");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		OutputLines(result).Should().Equal("0.25|1.5|NaN|18446744073709551615|-7|High|none|none|none|none|none|none|none|none|none|none");
	}

	[Theory]
	[InlineData("--money", "[default: 1.25]")]
	[InlineData("--factor", "[default: 0.5]")]
	[InlineData("--limit", "[default: Infinity]")]
	[InlineData("--retries", "[default: 3]")]
	[InlineData("--grade", "[default: mid]")]
	[InlineData("--tick", "[allowed: 0.1|0.2]")]
	[InlineData("--rate", "[default: 1.5]")]
	[InlineData("--scale", "[default: 1.5]")]
	[InlineData("--gain", "[default: 2.5]")]
	[InlineData("--missing", "[default: NaN]")]
	[InlineData("--floor", "[default: -Infinity]")]
	[InlineData("--separator", "[default: ,]")]
	[InlineData("--offset", "[default: -5]")]
	[InlineData("--big", "[default: 18446744073709551615]")]
	[InlineData("--tier", "[default: high]")]
	[InlineData("--pick", "[allowed: NaN|1.5]")]
	[InlineData("--avoid", "[denied: 1]")]
	[InlineData("--count", "[default: 7]")]
	[InlineData("--total", "[default: -5]")]
	[InlineData("--mask", "[default: 5]")]
	[InlineData("--width", "[default: 8080]")]
	[InlineData("--shade", "[default: 200]")]
	[InlineData("--delta", "[default: -5]")]
	[InlineData("--most", "[default: 79228162514264337593543950335]")]
	[InlineData("--neg-zero", "[default: -0]")]
	[InlineData("--maybe-rate", "[default: 2.5]")]
	[InlineData("--set-tier", "[default: mid]")]
	[InlineData("--access", "[default: 3]")]
	[InlineData("--cross-level", "[default: warning]")]
	[InlineData("--quote", "[default: ']")]
	public void Help_shows_each_default_as_users_type_it(string flag, string expected)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", "--help");
		result.ExitCode.Should().Be(0);
		HelpLine(result, flag).Should().EndWith(expected);
	}

	[Theory]
	[InlineData("--money", "[default: 1,25]")]
	[InlineData("--factor", "[default: 0,5]")]
	[InlineData("--rate", "[default: 1,5]")]
	[InlineData("--scale", "[default: 1,5]")]
	[InlineData("--gain", "[default: 2,5]")]
	[InlineData("--maybe-rate", "[default: 2,5]")]
	[InlineData("--tick", "[allowed: 0,1|0,2]")]
	[InlineData("--pick", "[allowed: NaN|1,5]")]
	[InlineData("--total", "[default: -5]")]
	[InlineData("--most", "[default: 79228162514264337593543950335]")]
	public void Help_shows_defaults_in_the_parse_culture(string flag, string expected)
	{
		var result = CliHostRunner.Run(Env("de-DE"), "numeric-defaults", "--help");
		result.ExitCode.Should().Be(0);
		HelpLine(result, flag).Should().EndWith(expected);
	}

	[Theory]
	[InlineData(null, "--fee", "[default: 0.25]")]
	[InlineData(null, "--gain", "[default: 1.5]")]
	[InlineData(null, "--missing", "[default: NaN]")]
	[InlineData(null, "--big", "[default: 18446744073709551615]")]
	[InlineData(null, "--offset", "[default: -7]")]
	[InlineData(null, "--tier", "[default: high]")]
	[InlineData(null, "--ceiling", "[max: 100]")]
	[InlineData(null, "--step", "[allowed: 0.1|0.5]")]
	[InlineData("de-DE", "--fee", "[default: 0,25]")]
	[InlineData("de-DE", "--gain", "[default: 1,5]")]
	[InlineData("de-DE", "--step", "[allowed: 0,1|0,5]")]
	public void Help_shows_options_member_defaults_and_limits(string? culture, string flag, string expected)
	{
		var result = CliHostRunner.Run(Env(culture), "numeric-ns", "show", "--help");
		result.ExitCode.Should().Be(0);
		HelpLine(result, flag).Should().EndWith(expected);
	}

	[Theory]
	[InlineData("--maybe-tier")]
	[InlineData("--pick")]
	[InlineData("--avoid")]
	public void Help_shows_no_default_for_a_null_default(string flag)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", "--help");
		HelpLine(result, flag).Should().NotContain("[default:");
	}

	[Fact]
	public void Nullable_enum_without_a_value_binds_null()
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults");
		OutputLines(result)[1].Split('|')[12].Should().Be("null");
	}

	[Theory]
	[InlineData("--maybe-tier", "high", 12, "High")]
	[InlineData("--set-tier", "low", 13, "Low")]
	[InlineData("--access", "execute", 14, "4")]
	[InlineData("--cross-level", "error", 15, "Error")]
	[InlineData("--quote", "x", 16, "x")]
	[InlineData("--most", "1.5", 9, "1.5")]
	[InlineData("--retries", "9", 0, "9")]
	public void Given_values_replace_the_defaults(string flag, string value, int index, string expected)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", flag, value);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		OutputLines(result)[1].Split('|')[index].Should().Be(expected);
	}

	[Theory]
	[InlineData("NaN")]
	[InlineData("1.5")]
	public void Allowed_values_match_NaN_and_finite_doubles(string value)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", "--pick", value);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
	}

	[Theory]
	[InlineData(null, "2", "Error: --pick: value must be one of: NaN, 1.5.")]
	[InlineData("de-DE", "2", "Error: --pick: value must be one of: NaN, 1,5.")]
	public void Value_outside_the_allowed_doubles_is_rejected(string? culture, string value, string error)
	{
		var result = CliHostRunner.Run(Env(culture), "numeric-defaults", "--pick", value);
		result.ExitCode.Should().Be(2);
		ConsoleOutput.Normalize(CliHostRunner.StderrText(result)).Should().Contain(error);
	}

	[Theory]
	[InlineData("1", 2)]
	[InlineData("1.0001", 0)]
	public void Int_constant_denied_on_a_double_matches_the_same_double(string value, int exitCode)
	{
		var result = CliHostRunner.Run(Env(), "numeric-defaults", "--avoid", value);
		result.ExitCode.Should().Be(exitCode, CliHostRunner.StderrText(result));
	}

	[Theory]
	[InlineData("--ratio", "0.1", 0)]
	[InlineData("--ratio", "0.25", 0)]
	[InlineData("--ratio", "0.2", 2)]
	[InlineData("--amount", "0.1", 0)]
	[InlineData("--amount", "0.30", 0)]
	[InlineData("--amount", "0.30000000000000004", 2)]
	[InlineData("--size", "-1", 0)]
	[InlineData("--size", "0", 2)]
	[InlineData("--code", "0", 2)]
	[InlineData("--code", "255", 2)]
	[InlineData("--code", "7", 0)]
	[InlineData("--id", "0", 2)]
	[InlineData("--id", "5000000000", 0)]
	[InlineData("--ceiling", "Infinity", 2)]
	[InlineData("--ceiling", "1e308", 0)]
	[InlineData("--tick", "0.2", 0)]
	[InlineData("--tick", "0.25", 2)]
	public void Allowed_and_denied_values_compare_in_the_parameter_type(string flag, string value, int exitCode)
	{
		var command = flag == "--tick" ? "numeric-defaults" : "numeric-choices";
		var result = CliHostRunner.Run(Env(), command, flag, value);
		result.ExitCode.Should().Be(exitCode, CliHostRunner.StderrText(result));
	}

	// short? and byte? bind through INumberBase; an unset value must pass [AllowedValues] rather than fail as "not one of".
	[Theory]
	[InlineData("numeric-choices")]
	[InlineData("numeric-ranges")]
	public void Unset_nullable_values_pass_every_check(string command)
	{
		var result = CliHostRunner.Run(Env(), command);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
	}

	[Theory]
	[InlineData(null, "--ratio", "[allowed: 0.1|0.25]")]
	[InlineData(null, "--amount", "[allowed: 0.1|0.3]")]
	[InlineData(null, "--size", "[allowed: -1|1]")]
	[InlineData(null, "--code", "[denied: 0|255]")]
	[InlineData(null, "--id", "[denied: 0]")]
	[InlineData(null, "--ceiling", "[denied: Infinity]")]
	[InlineData("de-DE", "--ratio", "[allowed: 0,1|0,25]")]
	[InlineData("de-DE", "--amount", "[allowed: 0,1|0,3]")]
	public void Help_shows_value_lists_in_the_parse_culture(string? culture, string flag, string expected)
	{
		var result = CliHostRunner.Run(Env(culture), "numeric-choices", "--help");
		result.ExitCode.Should().Be(0);
		HelpLine(result, flag).Should().EndWith(expected);
	}

	[Theory]
	[InlineData("numeric-defaults", "rate", "1.5")]
	[InlineData("numeric-defaults", "scale", "1.5")]
	[InlineData("numeric-defaults", "missing", "NaN")]
	[InlineData("numeric-defaults", "floor", "-Infinity")]
	[InlineData("numeric-defaults", "most", "79228162514264337593543950335")]
	[InlineData("numeric-defaults", "offset", "-5")]
	[InlineData("numeric-defaults", "big", "18446744073709551615")]
	[InlineData("numeric-defaults", "tier", "high")]
	[InlineData("numeric-defaults", "quote", "'")]
	[InlineData("numeric-defaults", "access", "3")]
	[InlineData("numeric-defaults", "maybe-tier", null)]
	[InlineData("numeric-defaults", "pick", null)]
	[InlineData("numeric-ranges", "at-least", null)]
	public void Schema_default_values_stay_invariant(string command, string parameter, string? expected)
	{
		var result = CliHostRunner.Run(Env("de-DE"), "__schema");
		result.ExitCode.Should().Be(0);
		using var doc = JsonDocument.Parse(CliHostRunner.StdoutText(result));
		var p = SchemaParameter(doc.RootElement, command, parameter);
		var actual = p.TryGetProperty("defaultValue", out var d) ? d.GetString() : null;
		actual.Should().Be(expected);
	}

	internal static JsonElement SchemaParameter(JsonElement root, string command, string parameter)
	{
		static JsonElement? Find(JsonElement node, string name)
		{
			if (node.TryGetProperty("name", out var n) && n.GetString() == name && node.TryGetProperty("parameters", out _))
				return node;
			if (node.TryGetProperty("commands", out var commands))
				foreach (var c in commands.EnumerateArray())
					if (Find(c, name) is { } found)
						return found;
			return null;
		}

		return Find(root, command)!.Value.GetProperty("parameters").EnumerateArray()
			.Single(p => p.GetProperty("name").GetString() == parameter);
	}
}
