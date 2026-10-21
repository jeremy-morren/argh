using FluentAssertions;
using Nullean.Argh.Tests.Fixtures;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Binding;

public class TryParseArghNegativeTests
{
	[Fact]
	public void TryParseArghExact_unknown_flag_returns_false_for_deploy_record()
	{
		var ok = DeployCliArgs.TryParseArghExact(["--not-a-real-flag", "x"], out _);
		ok.Should().BeFalse();
	}

	[Fact]
	public void TryParseArghExact_extra_positional_returns_false()
	{
		DeployCliArgs.TryParseArghExact(["--app-env", "prod", "--app-port", "80", "extra"], out _).Should().BeFalse();
		DeployCliArgs.TryParseArghExact(["--app-env", "prod", "--app-port", "80", "--", "extra"], out _).Should().BeFalse();
	}

	[Fact]
	public void TryParseArghExact_accepts_a_trailing_double_dash()
	{
		DeployCliArgs.TryParseArghExact(["--app-env", "prod", "--app-port", "80", "--"], out var d).Should().BeTrue();
		d!.Port.Should().Be(80);
	}

	[Fact]
	public void TryParseArgh_ignores_what_follows_double_dash()
	{
		DeployCliArgs.TryParseArgh(["--app-env", "prod", "--app-port", "80", "--", "--app-port", "1"], out var d).Should().BeTrue();
		d!.Port.Should().Be(80);
	}

	[Fact]
	public void TryParseArgh_unknown_flag_at_end_does_not_fail_and_extracts_known_flags()
	{
		var ok = DeployCliArgs.TryParseArgh(["--app-env", "prod", "--app-port", "8080", "--assume-cloned"], out var d);
		ok.Should().BeTrue();
		d!.Env.Should().Be("prod");
		d.Port.Should().Be(8080);
	}

	[Fact]
	public void TryParseArgh_unknown_flag_with_value_in_middle_does_not_fail()
	{
		var ok = DeployCliArgs.TryParseArgh(["--unknown-flag", "somevalue", "--app-env", "staging", "--app-port", "443"], out var d);
		ok.Should().BeTrue();
		d!.Env.Should().Be("staging");
		d.Port.Should().Be(443);
	}

	[Fact]
	public void TryParseArgh_mixed_unknown_and_known_flags_extracts_correctly()
	{
		var ok = DeployCliArgs.TryParseArgh(
			["assembler", "clone", "--app-env", "prod", "--app-port", "80", "--assume-cloned"],
			out var d);
		ok.Should().BeTrue();
		d!.Env.Should().Be("prod");
		d.Port.Should().Be(80);
	}
}
