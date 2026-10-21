using FluentAssertions;
using Nullean.Argh;
using Nullean.Argh.Runtime;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

public class ArghBoolTests
{
	[Theory]
	[InlineData("y")]
	[InlineData("Y")]
	[InlineData("yes")]
	[InlineData("YES")]
	[InlineData("Yes")]
	[InlineData("t")]
	[InlineData("T")]
	[InlineData("true")]
	[InlineData("TRUE")]
	[InlineData("True")]
	[InlineData("on")]
	[InlineData("ON")]
	[InlineData("oN")]
	[InlineData("1")]
	public void Boolish_reads_each_true_spelling_ignoring_case(string raw)
	{
		ArghBool.TryParseBoolish(raw, out var value).Should().BeTrue();
		value.Should().BeTrue();
	}

	[Theory]
	[InlineData("n")]
	[InlineData("N")]
	[InlineData("no")]
	[InlineData("NO")]
	[InlineData("No")]
	[InlineData("f")]
	[InlineData("F")]
	[InlineData("false")]
	[InlineData("FALSE")]
	[InlineData("False")]
	[InlineData("off")]
	[InlineData("OFF")]
	[InlineData("oFF")]
	[InlineData("0")]
	public void Boolish_reads_each_false_spelling_ignoring_case(string raw)
	{
		ArghBool.TryParseBoolish(raw, out var value).Should().BeTrue();
		value.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData(" true")]
	[InlineData("true ")]
	[InlineData("2")]
	[InlineData("-1")]
	[InlineData("01")]
	[InlineData("ye")]
	[InlineData("yeah")]
	[InlineData("tru")]
	[InlineData("nope")]
	[InlineData("o")]
	[InlineData("offf")]
	[InlineData("random")]
	// U+017F LATIN SMALL LETTER LONG S upper-cases to 'S' but isn't 's': clap doesn't read "yeſ" as "yes".
	[InlineData("yeſ")]
	public void Boolish_rejects_anything_else(string? raw)
	{
		ArghBool.TryParseBoolish(raw, out var value).Should().BeFalse();
		value.Should().BeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("n")]
	[InlineData("No")]
	[InlineData("f")]
	[InlineData("FALSE")]
	[InlineData("Off")]
	[InlineData("0")]
	public void Falsey_is_false_for_empty_and_each_false_spelling(string? raw) =>
		ArghBool.ParseFalsey(raw).Should().BeFalse();

	[Theory]
	[InlineData("true")]
	[InlineData("YES")]
	[InlineData("1")]
	[InlineData("2")]
	[InlineData("100")]
	[InlineData("random")]
	[InlineData(" ")]
	[InlineData(" no")]
	public void Falsey_is_true_for_anything_else(string raw) =>
		ArghBool.ParseFalsey(raw).Should().BeTrue();

	[Theory]
	[InlineData("true", true)]
	[InlineData("Off", false)]
	public void TryGetFlag_reads_the_value(string raw, bool expected)
	{
		var flags = new Dictionary<string, string?> { ["verbose"] = raw };
		ArghBoolBinding.TryGetFlag(flags, "verbose", false, out var value, out var error).Should().BeTrue();
		value.Should().Be(expected);
		error.Should().BeNull();
	}

	[Fact]
	public void TryGetFlag_is_null_when_the_flag_is_absent()
	{
		ArghBoolBinding.TryGetFlag(new Dictionary<string, string?>(), "verbose", true, out var value, out _).Should().BeTrue();
		value.Should().BeNull();
	}

	[Fact]
	public void TryGetFlag_reads_a_bare_switch_as_true()
	{
		var flags = new Dictionary<string, string?> { ["verbose"] = null };
		ArghBoolBinding.TryGetFlag(flags, "verbose", false, out var value, out var error).Should().BeTrue();
		value.Should().BeTrue();
		error.Should().BeNull();
	}

	[Fact]
	public void TryGetFlag_reads_the_no_form_as_false_which_wins()
	{
		var flags = new Dictionary<string, string?> { ["dry-run"] = "true", ["no-dry-run"] = null };
		ArghBoolBinding.TryGetFlag(flags, "dry-run", true, out var value, out _).Should().BeTrue();
		value.Should().BeFalse();
	}

	[Theory]
	[InlineData("true")]
	[InlineData("false")]
	[InlineData("")]
	[InlineData("bogus")]
	public void TryGetFlag_rejects_a_value_on_the_no_form(string raw)
	{
		var flags = new Dictionary<string, string?> { ["no-dry-run"] = raw };
		ArghBoolBinding.TryGetFlag(flags, "dry-run", true, out var value, out var error).Should().BeFalse();
		value.Should().BeNull();
		error.Should().Be("Error: --no-dry-run doesn't take a value.");
	}

	[Fact]
	public void TryGetFlag_ignores_the_no_form_of_a_flag_that_isnt_negatable()
	{
		var flags = new Dictionary<string, string?> { ["no-verbose"] = "true" };
		ArghBoolBinding.TryGetFlag(flags, "verbose", false, out var value, out _).Should().BeTrue();
		value.Should().BeNull();
	}

	[Theory]
	[InlineData("bogus")]
	[InlineData("")]
	public void TryGetFlag_rejects_a_value_that_isnt_a_boolean(string raw)
	{
		var flags = new Dictionary<string, string?> { ["verbose"] = raw };
		ArghBoolBinding.TryGetFlag(flags, "verbose", true, out _, out var error).Should().BeFalse();
		error.Should().Be($"Error: invalid bool for --verbose: '{raw}'. Expected true/false, yes/no, y/n, t/f, on/off or 1/0.");
	}

	[Fact]
	public void FromEnvironment_reads_the_variable_as_falsey()
	{
		const string name = "ARGH_UNIT_TEST_BOOL_ENV";
		try
		{
			Environment.SetEnvironmentVariable(name, null);
			ArghBoolBinding.FromEnvironment(name, treatEmptyAsUnset: true).Should().BeNull();
			ArghBoolBinding.EnvironmentText(name, treatEmptyAsUnset: true).Should().BeNull();

			Environment.SetEnvironmentVariable(name, "Off");
			ArghBoolBinding.FromEnvironment(name, treatEmptyAsUnset: true).Should().BeFalse();
			ArghBoolBinding.EnvironmentText(name, treatEmptyAsUnset: true).Should().Be("false");

			Environment.SetEnvironmentVariable(name, "anything");
			ArghBoolBinding.FromEnvironment(name, treatEmptyAsUnset: false).Should().BeTrue();
			ArghBoolBinding.EnvironmentText(name, treatEmptyAsUnset: false).Should().Be("true");
		}
		finally
		{
			Environment.SetEnvironmentVariable(name, null);
		}
	}
}
