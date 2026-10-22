using FluentAssertions;
using Nullean.Argh.Matching;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary>
/// Command and namespace names match with <see cref="ParserOptions.CommandStringComparer"/> (ignoring case by default),
/// long option names with <see cref="ParserOptions.ArgumentStringComparer"/> (matching case by default), and short options
/// always match case. Names a comparer makes the same fail fast.
/// </summary>
public class ArghNamesTests
{
	private static readonly ArghFlagSet Flags = new(
		new ArghFlag("name", ArghFlagKind.Value, 'n', ["label"]),
		new ArghFlag("color", ArghFlagKind.NegatableSwitch),
		new ArghFlag("verbose", ArghFlagKind.Switch, 'v'),
		new ArghFlag("version-check", ArghFlagKind.Switch, 'V'));

	private static IDisposable Use(StringComparer? commands = null, StringComparer? arguments = null) =>
		ArghRuntime.PushParserOptions(new ParserOptions { CommandStringComparer = commands!, ArgumentStringComparer = arguments! });

	[Fact]
	public void Defaults_ignore_case_for_commands_and_match_case_for_options()
	{
		var options = new ParserOptions();
		options.CommandStringComparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
		options.ArgumentStringComparer.Should().BeSameAs(StringComparer.Ordinal);
		options.CommandStringComparer = null!;
		options.ArgumentStringComparer = null!;
		options.CommandStringComparer.Should().BeSameAs(StringComparer.OrdinalIgnoreCase);
		options.ArgumentStringComparer.Should().BeSameAs(StringComparer.Ordinal);
	}

	[Fact]
	public void Command_set_finds_the_registered_name_with_the_command_comparer()
	{
		var set = new ArghCommandSet("deploy", "storage");
		using (Use())
		{
			set.Find("DEPLOY").Should().Be("deploy");
			set.Find("Storage").Should().Be("storage");
			set.Find("other").Should().BeNull();
		}

		using (Use(commands: StringComparer.Ordinal))
		{
			set.Find("deploy").Should().Be("deploy");
			set.Find("DEPLOY").Should().BeNull();
		}
	}

	[Fact]
	public void Suggestions_compare_characters_with_the_command_comparer()
	{
		var set = new ArghCommandSet("hello", "storage");
		using (Use())
			set.Suggest("HELO").Should().Equal("hello");
		using (Use(commands: StringComparer.Ordinal))
			set.Suggest("HELO").Should().BeEmpty();
	}

	[Fact]
	public void Commands_a_comparer_makes_the_same_fail_fast()
	{
		var set = new ArghCommandSet("Deploy", "deploy");
		using (Use())
		{
			var check = () => ArghNames.Check([set]);
			check.Should().Throw<InvalidOperationException>().WithMessage(
				"ParserOptions.CommandStringComparer treats the commands or namespaces 'Deploy' and 'deploy' as the same name. Rename one, or use a comparer that tells them apart, such as StringComparer.Ordinal.");
			var find = () => set.Find("deploy");
			find.Should().Throw<InvalidOperationException>();
		}

		using (Use(commands: StringComparer.Ordinal))
		{
			ArghNames.Check([set]);
			set.Find("deploy").Should().Be("deploy");
			set.Find("Deploy").Should().Be("Deploy");
		}
	}

	[Fact]
	public void Flags_a_comparer_makes_the_same_fail_fast()
	{
		var flags = new ArghFlagSet(new ArghFlag("name", ArghFlagKind.Value), new ArghFlag("Name", ArghFlagKind.Value));
		using (Use(arguments: StringComparer.OrdinalIgnoreCase))
		{
			var check = () => ArghNames.Check([flags]);
			check.Should().Throw<InvalidOperationException>().WithMessage(
				"ParserOptions.ArgumentStringComparer treats the options --name and --Name as the same name, but they are different flags. Rename one, or use a comparer that tells them apart, such as StringComparer.Ordinal.");
		}

		using (Use())
		{
			ArghNames.Check([flags]);
			var tokens = ArghTokenizer.ReadCommand(["--name", "a", "--Name", "b"], flags);
			tokens.Error.Should().BeNull();
			tokens.Flags.Should().BeEquivalentTo(new Dictionary<string, string?> { ["name"] = "a", ["Name"] = "b" });
		}
	}

	[Fact]
	public void Spellings_of_one_flag_never_collide()
	{
		var flags = new ArghFlagSet(new ArghFlag("name", ArghFlagKind.Value, aliases: ["Name"]), new ArghFlag("name", ArghFlagKind.Value));
		using (Use(arguments: StringComparer.OrdinalIgnoreCase))
		{
			ArghNames.Check([flags]);
			ArghTokenizer.ReadCommand(["--NAME", "a"], flags).Flags["name"].Should().Be("a");
		}
	}

	[Fact]
	public void Long_options_match_case_by_default()
	{
		using (Use())
		{
			ArghTokenizer.ReadCommand(["--NAME", "x"], Flags).Error!.Message.Should().Be("Error: unknown option '--NAME'.");
			ArghTokenizer.ReadCommand(["--HELP"], Flags).Error!.Message.Should().Be("Error: unknown option '--HELP'.");
		}
	}

	[Fact]
	public void Long_options_and_help_match_with_the_argument_comparer()
	{
		using (Use(arguments: StringComparer.OrdinalIgnoreCase))
		{
			var tokens = ArghTokenizer.ReadCommand(["--NAME", "x", "--No-Color"], Flags);
			tokens.Error.Should().BeNull();
			tokens.Flags.Should().BeEquivalentTo(new Dictionary<string, string?> { ["name"] = "x", ["color"] = "false" });
			ArghTokenizer.ReadCommand(["--LABEL=y"], Flags).Flags["name"].Should().Be("y");
			ArghTokenizer.ReadCommand(["--NAME", "x", "--Label", "y"], Flags).Error!.Message
				.Should().Be("Error: the argument --name cannot be used multiple times.");
		}
	}

	[Fact]
	public void Help_and_version_match_with_the_argument_comparer()
	{
		using (Use(arguments: StringComparer.OrdinalIgnoreCase))
		{
			ArghTokenizer.ReadCommand(["--Help"], Flags).HelpRequested.Should().BeTrue();
			ArghNames.IsHelp("--HELP").Should().BeTrue();
			ArghNames.IsVersion("--Version").Should().BeTrue();
			ArghTokenizer.ReadOptions(["--VERSION"], 0, Flags).Next.Should().Be(0);
		}

		using (Use())
		{
			ArghNames.IsHelp("--HELP").Should().BeFalse();
			ArghNames.IsVersion("--Version").Should().BeFalse();
			ArghNames.IsHelp("-h").Should().BeTrue();
		}
	}

	[Fact]
	public void Short_options_always_match_case()
	{
		using (Use(arguments: StringComparer.OrdinalIgnoreCase))
		{
			ArghTokenizer.ReadCommand(["-N", "x"], Flags).Error!.Message.Should().Be("Error: unknown short option '-N'.");
			ArghTokenizer.ReadCommand(["-H"], Flags).Error!.Message.Should().Be("Error: unknown short option '-H'.");
			var tokens = ArghTokenizer.ReadCommand(["-vV"], Flags);
			tokens.Error.Should().BeNull();
			tokens.Flags.Keys.Should().BeEquivalentTo("verbose", "version-check");
			ArghNames.IsFlag("-V", "-v").Should().BeFalse();
			ArghNames.IsFlag("--Verbose", "--verbose").Should().BeTrue();
		}
	}

	[Fact]
	public void Fuzzy_match_compares_characters_with_the_comparer()
	{
		FuzzyMatch.FindClosest("NAEM", ["name", "count"], 2).Should().BeEmpty();
		FuzzyMatch.FindClosest("NAEM", ["name", "count"], 2, StringComparer.OrdinalIgnoreCase).Should().Equal("name");
		FuzzyMatch.LevenshteinDistance("Hello", "hELLO", StringComparer.OrdinalIgnoreCase).Should().Be(0);
		FuzzyMatch.LevenshteinDistance("Hello", "hELLO", StringComparer.InvariantCultureIgnoreCase).Should().Be(0);
		FuzzyMatch.LevenshteinDistance("Hello", "hELLO").Should().Be(5);
	}
}
