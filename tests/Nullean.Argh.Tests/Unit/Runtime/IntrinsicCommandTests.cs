using FluentAssertions;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary>
/// <see cref="ArghApp.IsIntrinsicCommand"/> sees <c>--help</c>, <c>-h</c> and <c>--version</c> as arguments of their own,
/// before any <c>--</c>. One can't be the value of the flag before it: that's an error the parser reports.
/// </summary>
[Collection("Console")]
public class IntrinsicCommandTests
{
	[Theory]
	[InlineData("--help")]
	[InlineData("-h")]
	[InlineData("--version")]
	[InlineData("hello", "--help")]
	[InlineData("hello", "--name", "x", "-h")]
	[InlineData("hello", "--name=x", "--help")]
	[InlineData("hello", "--name", "--help")]
	[InlineData("hello", "-n", "-h")]
	public void Help_or_version_of_its_own_is_intrinsic(params string[] args) =>
		ArghApp.IsIntrinsicCommand(args).Should().BeTrue();

	[Theory]
	[InlineData("hello", "--", "--help")]
	[InlineData("hello", "--name", "x")]
	[InlineData("hello", "--name=--help")]
	public void Help_that_is_an_operand_or_a_value_is_not_intrinsic(params string[] args) =>
		ArghApp.IsIntrinsicCommand(args).Should().BeFalse();
}
