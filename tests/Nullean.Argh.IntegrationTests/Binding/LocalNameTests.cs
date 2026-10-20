using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// Generated locals can't collide: parameters named like the generator's own locals, parameters whose kebab-case
/// names match (<c>fooBar</c>, <c>foo_bar</c>), and two parameters that need the same parse temporaries.
/// </summary>
public class LocalNameTests
{
	[Fact]
	public void Parameters_named_like_generated_locals_bind_to_the_right_values()
	{
		var result = CliHostRunner.Run("local-names", "--start", "1,2", "--end", "3,4",
			"--args", "A", "--flags", "F", "--positionals", "P", "--i", "I", "--a", "X", "--eq", "E", "--ct", "C",
			"--foo-bar", "FB", "--foo_bar", "F_B",
			"--wait", "5m", "--delay", "10s", "--home", "https://h.example", "--mirror", "https://m.example");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("1,2|3,4|A|F|P|I|X|E|C|FB|F_B|300|10|h.example|m.example");
	}

	[Fact]
	public void Two_custom_parsed_dto_members_bind_separately()
	{
		var result = CliHostRunner.Run("local-names-dto", "--from", "1,2", "--to", "5,6");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be("1,2|5,6");
	}
}
