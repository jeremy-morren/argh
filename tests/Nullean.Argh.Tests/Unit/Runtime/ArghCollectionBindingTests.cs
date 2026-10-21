using FluentAssertions;
using Nullean.Argh.Runtime;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

public class ArghCollectionBindingTests
{
	[Theory]
	[InlineData("", new[] { "" })]
	[InlineData(",,", new[] { "", "", "" })]
	[InlineData("1,,3", new[] { "1", "", "3" })]
	[InlineData(",a,", new[] { "", "a", "" })]
	[InlineData("a", new[] { "a" })]
	public void Splitting_keeps_empty_parts(string value, string[] expected) =>
		ArghCollectionBinding.Elements([value], null, ",").Should().Equal(expected);

	[Fact]
	public void A_multi_character_separator_splits_on_the_whole_separator() =>
		ArghCollectionBinding.Elements(["a::b:c"], null, "::").Should().Equal("a", "b:c");

	[Fact]
	public void Each_value_is_split_in_order() =>
		ArghCollectionBinding.Elements(["1,2", "", "3"], null, ",").Should().Equal("1", "2", "", "3");

	[Fact]
	public void Without_a_separator_each_value_is_one_element() =>
		ArghCollectionBinding.Elements(["a,b", ""], null, null).Should().Equal("a,b", "");

	[Fact]
	public void The_environment_value_is_used_only_when_no_values_were_given()
	{
		ArghCollectionBinding.Elements(null, "4,5", ",").Should().Equal("4", "5");
		ArghCollectionBinding.Elements(["6"], "4,5", ",").Should().Equal("6");
		ArghCollectionBinding.Elements(null, null, ",").Should().BeEmpty();
	}
}
