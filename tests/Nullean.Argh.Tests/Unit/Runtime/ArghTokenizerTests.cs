using FluentAssertions;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary><see cref="ArghTokenizer"/> reads options and arguments as clap does.</summary>
public class ArghTokenizerTests
{
	private static readonly ArghFlagSet Flags = new(
		new ArghFlag("verbose", ArghFlagKind.Switch, 'v'),
		new ArghFlag("color", ArghFlagKind.NegatableSwitch, aliases: ["colour"]),
		new ArghFlag("name", ArghFlagKind.Value, 'n', ["label"]),
		new ArghFlag("count", ArghFlagKind.Value, 'c'),
		new ArghFlag("tag", ArghFlagKind.Collection, 't'),
		new ArghFlag("last", ArghFlagKind.Value, repeated: RepeatedScalarBehaviour.TakeLast));

	private static ArghTokens Read(params string[] args) => ArghTokenizer.ReadCommand(args, Flags);

	[Fact]
	public void Reads_long_and_short_flags_values_and_positionals()
	{
		var tokens = Read("a", "--verbose", "--name", "x", "-c", "2", "b", "--tag=t1", "-t", "t2");
		tokens.Error.Should().BeNull();
		tokens.Flags.Should().BeEquivalentTo(new Dictionary<string, string?> { ["verbose"] = null, ["name"] = "x", ["count"] = "2" });
		tokens.MultiFlags["tag"].Should().Equal("t1", "t2");
		tokens.Positionals.Should().Equal("a", "b");
	}

	[Theory]
	[InlineData(new[] { "-vn", "x" }, true, "x")]
	[InlineData(new[] { "-vnx" }, true, "x")]
	[InlineData(new[] { "-vn=x" }, true, "x")]
	[InlineData(new[] { "-nx" }, false, "x")]
	[InlineData(new[] { "-n=x" }, false, "x")]
	[InlineData(new[] { "-nvx" }, false, "vx")]
	[InlineData(new[] { "-n=" }, false, "")]
	public void Short_flags_combine(string[] args, bool verbose, string name)
	{
		var tokens = Read(args);
		tokens.Error.Should().BeNull();
		tokens.Flags.ContainsKey("verbose").Should().Be(verbose);
		tokens.Flags["name"].Should().Be(name);
	}

	[Fact]
	public void Switch_in_a_group_takes_a_value_after_an_equals_sign() =>
		Read("-v=false").Flags["verbose"].Should().Be("false");

	[Theory]
	[InlineData("--no-color", "color", "false")]
	[InlineData("--no-colour", "color", "false")]
	[InlineData("--colour", "color", null)]
	[InlineData("--label=x", "name", "x")]
	public void Long_names_aliases_and_negations_store_under_the_flag_name(string arg, string flag, string? value) =>
		Read(arg).Flags.Should().BeEquivalentTo(new Dictionary<string, string?> { [flag] = value });

	[Theory]
	[InlineData("--NAME=x", ArghTokenErrorKind.UnknownOption, "NAME")]
	[InlineData("--No-Color", ArghTokenErrorKind.UnknownOption, "No-Color")]
	[InlineData("-V", ArghTokenErrorKind.UnknownShortOption, "-V")]
	public void Flag_names_match_case(string arg, ArghTokenErrorKind kind, string name)
	{
		var error = Read(arg).Error!;
		error.Kind.Should().Be(kind);
		error.Name.Should().Be(name);
	}

	[Fact]
	public void Negation_takes_no_value()
	{
		var error = Read("--no-color=true").Error!;
		error.Kind.Should().Be(ArghTokenErrorKind.UnexpectedValue);
		error.Message.Should().Be("Error: --no-color doesn't take a value.");
		error.FlagName.Should().Be("color");
	}

	[Theory]
	[InlineData("--name", "--count", "1")]
	[InlineData("--name", "-c", "1")]
	[InlineData("--name", "-vc", "1")]
	[InlineData("--name", "--help")]
	[InlineData("--name", "-h")]
	[InlineData("--name", "--version")]
	[InlineData("--name", "--")]
	[InlineData("--name", "--no-color")]
	[InlineData("-n", "--verbose")]
	[InlineData("-vn", "--verbose")]
	[InlineData("--name")]
	public void Value_is_missing_when_an_option_or_nothing_follows(params string[] args)
	{
		var tokens = Read(args);
		tokens.Error!.Kind.Should().Be(ArghTokenErrorKind.MissingValue);
		tokens.Error.Message.Should().Be("Error: a value is required for --name but none was supplied.");
		tokens.Error.FlagName.Should().Be("name");
		tokens.HelpRequested.Should().BeFalse();
	}

	[Theory]
	[InlineData("--bogus", ArghTokenErrorKind.UnknownOption, "bogus")]
	[InlineData("-x", ArghTokenErrorKind.UnknownShortOption, "-x")]
	[InlineData("-xv", ArghTokenErrorKind.UnknownShortOption, "-x")]
	public void Unknown_option_after_a_flag_is_reported_as_unknown(string option, ArghTokenErrorKind kind, string name)
	{
		var error = Read("--name", option).Error!;
		error.Kind.Should().Be(kind);
		error.Name.Should().Be(name);
		error.FlagName.Should().BeNull();
	}

	[Theory]
	[InlineData("-5")]
	[InlineData("-1.5")]
	[InlineData("-2e-3")]
	[InlineData("-0,5")]
	[InlineData("-.5")]
	[InlineData("-")]
	[InlineData("")]
	public void Negative_numbers_a_dash_and_plain_text_are_values(string value) =>
		Read("--count", value).Flags["count"].Should().Be(value);

	[Fact]
	public void Negative_number_is_an_option_when_a_short_flag_has_its_digit()
	{
		var flags = new ArghFlagSet(new ArghFlag("count", ArghFlagKind.Value), new ArghFlag("one", ArghFlagKind.Switch, '1'));
		ArghTokenizer.ReadCommand(["--count", "-1"], flags).Error!.Kind.Should().Be(ArghTokenErrorKind.MissingValue);
		ArghTokenizer.ReadCommand(["--count", "-5"], flags).Flags["count"].Should().Be("-5");
	}

	[Fact]
	public void Negative_number_is_a_positional_argument()
	{
		var tokens = Read("-5", "-0.5");
		tokens.Error.Should().BeNull();
		tokens.Positionals.Should().Equal("-5", "-0.5");
	}

	[Theory]
	[InlineData("-5x", false)]
	[InlineData("-x5", true)]
	[InlineData("-e5", true)]
	public void Negative_number_detection(string token, bool isOption) =>
		ArghTokenizer.IsNegativeNumber(token).Should().Be(!isOption);

	[Fact]
	public void Double_dash_makes_the_rest_positional()
	{
		var tokens = Read("--verbose", "--", "--name", "-v", "--");
		tokens.Flags.Keys.Should().Equal("verbose");
		tokens.Positionals.Should().Equal("--name", "-v", "--");
	}

	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("-vh")]
	public void Help_stops_reading(string help)
	{
		var tokens = Read("--verbose", help, "--bogus");
		tokens.HelpRequested.Should().BeTrue();
		tokens.Error.Should().BeNull();
	}

	[Fact]
	public void Error_before_help_wins() =>
		Read("--bogus", "--help").HelpRequested.Should().BeFalse();

	[Fact]
	public void Without_help_help_is_an_unknown_option() =>
		ArghTokenizer.ReadCommand(["--help"], Flags, help: false).Error!.Kind.Should().Be(ArghTokenErrorKind.UnknownOption);

	[Fact]
	public void Lenient_skips_unknown_options_and_their_values()
	{
		var tokens = ArghTokenizer.ReadCommand(["--bogus", "x", "-z", "y", "--name", "n", "--other=1"], Flags, lenient: true);
		tokens.Error.Should().BeNull();
		tokens.Flags.Should().BeEquivalentTo(new Dictionary<string, string?> { ["name"] = "n" });
		tokens.Positionals.Should().BeEmpty();
	}

	[Theory]
	[InlineData(new[] { "--name", "a", "--name", "b" }, "name")]
	[InlineData(new[] { "-n", "a", "--label", "a" }, "name")]
	[InlineData(new[] { "-vv" }, "verbose")]
	[InlineData(new[] { "--color", "--no-color" }, "color")]
	public void A_flag_given_twice_is_rejected_by_default(string[] args, string flag)
	{
		var error = Read(args).Error!;
		error.Kind.Should().Be(ArghTokenErrorKind.Repeated);
		error.Message.Should().Be($"Error: the argument --{flag} cannot be used multiple times.");
		error.FlagName.Should().Be(flag);
	}

	[Fact]
	public void A_flag_given_before_is_rejected_again()
	{
		var given = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "a" };
		ArghTokenizer.ReadCommand(["--name", "b"], Flags, given).Error!.Kind.Should().Be(ArghTokenErrorKind.Repeated);
	}

	[Fact]
	public void Take_last_keeps_the_last_value()
	{
		using var _ = ArghRuntime.PushParserOptions(new ParserOptions { RepeatedScalarBehaviour = RepeatedScalarBehaviour.TakeLast });
		var tokens = Read("--name", "a", "-nb", "-vv", "--color", "--no-color");
		tokens.Error.Should().BeNull();
		tokens.Flags["name"].Should().Be("b");
		tokens.Flags["color"].Should().Be("false");
	}

	[Theory]
	[InlineData(new[] { "--name", "a", "-n", "a" }, true)]
	[InlineData(new[] { "-v", "--verbose=yes" }, true)]
	[InlineData(new[] { "--color", "--no-color" }, false)]
	[InlineData(new[] { "--name", "a", "--name", "A" }, false)]
	public void Allow_identical_accepts_only_the_same_value(string[] args, bool accepted)
	{
		using var _ = ArghRuntime.PushParserOptions(new ParserOptions { RepeatedScalarBehaviour = RepeatedScalarBehaviour.AllowIdentical });
		var error = Read(args).Error;
		if (accepted)
			error.Should().BeNull();
		else
			error!.Message.Should().Contain("cannot be used multiple times with different values");
	}

	[Fact]
	public void A_flag_rule_wins_over_the_parser_options()
	{
		Read("--last", "a", "--last", "b").Flags["last"].Should().Be("b");
		var strict = new ArghFlagSet(new ArghFlag("name", ArghFlagKind.Value, repeated: RepeatedScalarBehaviour.Reject));
		using var _ = ArghRuntime.PushParserOptions(new ParserOptions { RepeatedScalarBehaviour = RepeatedScalarBehaviour.TakeLast });
		ArghTokenizer.ReadCommand(["--name", "a", "--name", "b"], strict).Error!.Kind.Should().Be(ArghTokenErrorKind.Repeated);
	}

	[Fact]
	public void Options_stop_at_the_first_argument_that_isnt_one()
	{
		var given = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var tokens = ArghTokenizer.ReadOptions(["cmd", "-vn", "x", "list", "--count", "1"], 1, Flags, given: given);
		tokens.Error.Should().BeNull();
		tokens.Next.Should().Be(3);
		given.Should().BeEquivalentTo(new Dictionary<string, string?> { ["verbose"] = null, ["name"] = "x" });
	}

	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("--version")]
	[InlineData("--")]
	[InlineData("-")]
	[InlineData("-5")]
	[InlineData("-vh")]
	public void Options_leave_help_version_dashes_and_numbers_to_the_dispatcher(string arg) =>
		ArghTokenizer.ReadOptions(["--verbose", arg], 0, Flags).Next.Should().Be(1);

	[Fact]
	public void Options_stop_at_a_flag_of_the_command_that_reads_the_rest()
	{
		var defer = new ArghFlagSet(new ArghFlag("deep", ArghFlagKind.Switch, 'd'));
		ArghTokenizer.ReadOptions(["-v", "--deep"], 0, Flags, defer).Next.Should().Be(1);
		ArghTokenizer.ReadOptions(["-v", "-vd"], 0, Flags, defer).Next.Should().Be(1);
		ArghTokenizer.ReadOptions(["--name", "--deep"], 0, Flags, defer).Error!.Kind.Should().Be(ArghTokenErrorKind.MissingValue);
		ArghTokenizer.ReadOptions(["--bogus"], 0, Flags, defer).Error!.Kind.Should().Be(ArghTokenErrorKind.UnknownOption);
	}

	[Fact]
	public void Options_for_completion_stop_at_a_flag_still_missing_its_value_and_ignore_repeats()
	{
		ArghTokenizer.ReadOptions(["-v", "--name"], 0, Flags, complete: true).Next.Should().Be(1);
		ArghTokenizer.ReadOptions(["--name", "a", "--name", "b", "x"], 0, Flags, complete: true).Next.Should().Be(4);
		ArghTokenizer.ReadOptions(["-v", "--name", "a", "x"], 0, Flags, end: 2, complete: true).Next.Should().Be(1);
	}
}
