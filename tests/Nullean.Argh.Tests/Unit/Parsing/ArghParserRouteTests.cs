using FluentAssertions;
using Nullean.Argh.Parsing;
using Nullean.Argh.Runtime;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Parsing;

[Collection("Console")]
public class ArghParserRouteTests
{
	[Fact]
	public void Route_resolves_flat_command_and_remaining_arguments()
	{
		var r = ArghParser.Route(["hello", "--name", "x"]);
		r.Should().NotBeNull();
		r.Value.CommandPath.Should().Be("hello");
		r.Value.RemainingArgs.Should().Equal("--name", "x");
	}

	[Fact]
	public void Route_resolves_group_command_and_remaining_arguments()
	{
		var r = ArghParser.Route(["storage", "list", "--verbose"]);
		r.Should().NotBeNull();
		r.Value.CommandPath.Should().Be("storage/list");
		r.Value.RemainingArgs.Should().Equal("--verbose");
	}

	[Fact]
	public void Route_resolves_nested_group_command_path()
	{
		var r = ArghParser.Route(["storage", "blob", "upload"]);
		r.Should().NotBeNull();
		r.Value.CommandPath.Should().Be("storage/blob/upload");
		r.Value.RemainingArgs.Should().BeEmpty();
	}

	[Theory]
	[InlineData("a", "b")]
	[InlineData("--", "version")]
	[InlineData("-a")]
	public void Route_resolves_unmatched_arguments_to_a_default_command_with_positionals(params string[] args)
	{
		var r = ArghParser.Route(["ls", .. args]);
		r.Should().NotBeNull();
		r.Value.CommandPath.Should().NotBe("ls/version");
		r.Value.RemainingArgs.Should().Equal(args);
	}

	[Fact]
	public void Route_resolves_a_command_next_to_a_default_command_with_positionals()
	{
		var r = ArghParser.Route(["ls", "version"]);
		r.Should().NotBeNull();
		r.Value.CommandPath.Should().Be("ls/version");
	}

	[Fact]
	public void Route_returns_null_for_root_help() => ArghParser.Route(["--help"]).Should().BeNull();

	[Fact]
	public void Route_matches_command_and_namespace_names_with_the_command_comparer()
	{
		ArghParser.Route(["HELLO", "--name", "x"])!.Value.CommandPath.Should().Be("hello");
		ArghParser.Route(["Storage", "LIST"])!.Value.CommandPath.Should().Be("storage/list");
		using (ArghRuntime.PushParserOptions(new ParserOptions { CommandStringComparer = StringComparer.Ordinal }))
		{
			ArghParser.Route(["HELLO", "--name", "x"]).Should().BeNull();
			ArghParser.Route(["Storage", "list"]).Should().BeNull();
			ArghParser.Route(["storage", "list"])!.Value.CommandPath.Should().Be("storage/list");
		}
	}
}
