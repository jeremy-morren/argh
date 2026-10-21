using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Types that implement <c>IParsable&lt;T&gt;</c> bind without an <c>[ArgumentParser]</c>. <c>parsables</c> prints
/// order, code, token, id, small, letter, amount, maybe, more and fromEnv, separated by <c>|</c>.
/// </summary>
public class ParsableBindingTests
{
	private const string Id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";
	private static readonly string[] Required = ["--order", "ORD-42", "--code", "abc", "--token", "t1", "--id", Id];

	private static string Run(IReadOnlyDictionary<string, string>? environment, params string[] args)
	{
		var result = CliHostRunner.Run(environment, args);
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		return CliHostRunner.StdoutText(result).Trim();
	}

	[Fact]
	public void Implicit_explicit_and_span_parsable_types_bind_and_optional_ones_keep_their_defaults() =>
		Run(null, ["parsables", .. Required])
			.Should().Be($"ORD-42|ABC|tok:t1|{Id}|7|x|none|none|none|none");

	[Fact]
	public void Optional_nullable_repeatable_and_environment_values_bind()
	{
		var env = new Dictionary<string, string> { ["ARGH_TEST_ORDER"] = "ORD-9" };
		Run(env, ["parsables", .. Required, "--small", "3", "--letter", "q", "--amount", "1.5", "--maybe", "ORD-1", "--more", "ORD-2", "--more", "ORD-3"])
			.Should().Be($"ORD-42|ABC|tok:t1|{Id}|3|q|1.5|ORD-1|ORD-2,ORD-3|ORD-9");
	}

	[Fact]
	public void The_configured_culture_reaches_TryParse()
	{
		var german = new Dictionary<string, string> { ["ARGH_TEST_CULTURE"] = "de-DE" };
		Run(german, ["parsables", .. Required, "--amount", "1,5"]).Split('|')[6].Should().Be("1.5");
	}

	[Theory]
	[InlineData("--order", "nope", "OrderId")]
	[InlineData("--code", "toolong", "ExplicitCode")]
	[InlineData("--id", "not-a-guid", "Guid")]
	[InlineData("--small", "70000", "short")]
	public void An_unparseable_value_exits_2_naming_the_type(string flag, string value, string typeName)
	{
		var args = Required.ToList();
		var i = args.IndexOf(flag);
		if (i >= 0)
			args[i + 1] = value;
		else
			args.AddRange([flag, value]);
		var result = CliHostRunner.Run(["parsables", .. args]);
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain($"Error: invalid {typeName} for {flag}: '{value}'.");
	}

	[Fact]
	public void AsParameters_members_and_separator_collections_bind() =>
		Run(null, "parsable-dto", "--order", "ORD-5", "--id", Id, "--batch", "ORD-1,ORD-2")
			.Should().Be($"ORD-5|{Id}|ORD-1,ORD-2");

	[Fact]
	public void An_ArgumentParser_wins_over_the_types_own_IParsable() =>
		Run(null, "parsable-with-parser", "--order", "5").Should().Be("ORD-1005");

	[Theory]
	[InlineData("ORD-7|none", "parsable-ns", "--order", "ORD-7", "show")]
	[InlineData($"ORD-8|{Id}", "parsable-ns", "show", "--order", "ORD-8", "--id", Id)]
	public void Namespace_options_bind_before_and_after_the_command(string expected, params string[] args) =>
		Run(null, args).Should().Be(expected);

	[Fact]
	public void Help_shows_the_type_name_as_the_placeholder()
	{
		var help = ConsoleOutput.Normalize(CliHostRunner.StdoutText(CliHostRunner.Run("parsables", "--help")));
		help.Should().Contain("--order <orderId>").And.Contain("--code <explicitCode>").And.Contain("--token <spanToken>")
			.And.Contain("--id <guid>").And.Contain("--small <short>").And.Contain("--letter <char>");
	}

	[Theory]
	[InlineData("--b=255", "b=255")]
	[InlineData("--sb=-128", "sb=-128")]
	[InlineData("--us=65535", "us=65535")]
	[InlineData("--ui=4294967295", "ui=4294967295")]
	[InlineData("--ul=18446744073709551615", "ul=18446744073709551615")]
	[InlineData("--h=1.5", "h=1.5")]
	[InlineData("--i128=170141183460469231731687303715884105727", "i128=170141183460469231731687303715884105727")]
	[InlineData("--big=123456789012345678901234567890", "big=123456789012345678901234567890")]
	public void Numeric_types_without_built_in_handling_bind_as_nullable_values(string arg, string expected) =>
		Run(null, "numeric-parsables", arg).Should().Be(expected);

	[Fact]
	public void Nullable_parsable_values_stay_null_when_absent() =>
		Run(null, "numeric-parsables").Should().Be("none");

	[Theory]
	[InlineData("--b=256", "Error: invalid byte for --b: '256'.")]
	[InlineData("--us=-1", "Error: invalid ushort for --us: '-1'.")]
	public void An_out_of_range_number_exits_2(string arg, string message)
	{
		var result = CliHostRunner.Run("numeric-parsables", arg);
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain(message);
	}

	[Fact]
	public void NumberStyles_from_the_parser_options_reach_number_types()
	{
		var thousands = new Dictionary<string, string> { ["ARGH_TEST_NUMBER_STYLES"] = "Integer, AllowThousands" };
		Run(thousands, ["parsables", .. Required, "--small", "1,000"]).Split('|')[4].Should().Be("1000");
		Run(thousands, "numeric-parsables", "--us=1,000").Should().Be("us=1000");

		// The default Float styles reject the group separator.
		var result = CliHostRunner.Run(["parsables", .. Required, "--small", "1,000"]);
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain("Error: invalid short for --small: '1,000'.");
	}

	[Theory]
	[InlineData("--us=1e3", "us=1000")]
	[InlineData("--b=1.0", "b=1")]
	[InlineData("--ui= 7 ", "ui=7")]
	[InlineData("--big=1e30", "big=1000000000000000000000000000000")]
	[InlineData("--h=1.5e2", "h=150")]
	public void Number_types_take_the_default_float_styles(string arg, string expected) =>
		Run(null, "numeric-parsables", arg).Should().Be(expected);

	[Fact]
	public void An_integer_type_rejects_a_fraction()
	{
		var result = CliHostRunner.Run("numeric-parsables", "--us=1.5");
		result.ExitCode.Should().Be(2);
		CliHostRunner.StderrText(result).Should().Contain("Error: invalid ushort for --us: '1.5'.");
	}

	[Fact]
	public void Schema_types_follow_the_json_type_of_the_value()
	{
		using var doc = JsonDocument.Parse(CliHostRunner.StdoutText(CliHostRunner.Run("__schema")));
		var parameters = doc.RootElement.GetProperty("commands").EnumerateArray()
			.Single(c => c.GetProperty("name").GetString() == "parsables")
			.GetProperty("parameters").EnumerateArray()
			.ToDictionary(p => p.GetProperty("name").GetString()!, p => p);
		parameters["order"].GetProperty("type").GetString().Should().Be("string");
		parameters["id"].GetProperty("type").GetString().Should().Be("string");
		parameters["small"].GetProperty("type").GetString().Should().Be("integer");
		parameters["more"].GetProperty("type").GetString().Should().Be("array");
		parameters["more"].GetProperty("elementType").GetString().Should().Be("string");
	}
}
