using FluentAssertions;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary><see cref="ArghApp.IsIntrinsicCommand"/> sees <c>--help</c> only where the parser would read it as help.</summary>
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
	public void Help_or_version_of_its_own_is_intrinsic(params string[] args) =>
		ArghApp.IsIntrinsicCommand(args).Should().BeTrue();

	[Theory]
	[InlineData("hello", "--name", "--help")]
	[InlineData("hello", "-n", "-h")]
	[InlineData("hello", "--", "--help")]
	[InlineData("hello", "--name", "x")]
	public void Help_that_may_be_a_value_or_an_operand_is_not_intrinsic(params string[] args) =>
		ArghApp.IsIntrinsicCommand(args).Should().BeFalse();
}
