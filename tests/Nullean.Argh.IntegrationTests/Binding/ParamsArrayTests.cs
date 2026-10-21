using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>A <c>params</c> array flag is optional: left out, the handler gets an empty array.</summary>
public class ParamsArrayTests
{
	[Fact]
	public void Params_array_flag_is_optional()
	{
		var result = CliHostRunner.Run("params-items");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("items:0:");
	}

	[Fact]
	public void Params_array_flag_takes_repeated_values()
	{
		var result = CliHostRunner.Run("params-items", "--items", "a", "--items", "b");
		result.ExitCode.Should().Be(0, CliHostRunner.StderrText(result));
		CliHostRunner.StdoutText(result).Trim().Should().Be("items:2:a,b");
	}

	[Fact]
	public void Params_array_flag_is_not_shown_as_required()
	{
		var result = CliHostRunner.Run("params-items", "--help");
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Split('\n').Single(l => l.Contains("--items")).Should().NotContain("[required]");
	}
}
