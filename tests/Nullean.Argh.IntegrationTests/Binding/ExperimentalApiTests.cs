using FluentAssertions;
using Nullean.Argh.IntegrationTests.Infrastructure;
using Xunit;

namespace Nullean.Argh.IntegrationTests.Binding;

/// <summary>
/// The generated code references <c>FixtureStage.Preview</c>, which is <c>[Experimental]</c>. The CLI host only
/// builds because generated code suppresses the experimental diagnostic and Argh reports it at the registration,
/// where <c>CliRegistrationModule</c> suppresses it.
/// </summary>
public class ExperimentalApiTests
{
	[Theory]
	[InlineData("stable", "stage:Stable")]
	[InlineData("preview", "stage:Preview")]
	public void Experimental_enum_member_binds(string value, string expected)
	{
		var result = CliHostRunner.Run("experimental-api", "--stage", value);
		result.ExitCode.Should().Be(0);
		CliHostRunner.StdoutText(result).Trim().Should().Be(expected);
	}
}
