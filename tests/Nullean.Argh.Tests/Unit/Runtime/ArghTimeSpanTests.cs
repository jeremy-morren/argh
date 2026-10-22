using System.Globalization;
using FluentAssertions;
using Nullean.Argh;
using Xunit;

namespace Nullean.Argh.Tests.Unit.Runtime;

/// <summary>A CLI <see cref="TimeSpan"/> is a <see cref="HumanTime"/> duration, or <see cref="TimeSpan"/>'s own format when it has a <c>:</c>.</summary>
public class ArghTimeSpanTests
{
	[Theory]
	[InlineData("1s", 1)]
	[InlineData("5m", 5 * 60)]
	[InlineData("90m", 90 * 60)]
	[InlineData("2h", 2 * 3600)]
	[InlineData("3d", 3 * 86400)]
	[InlineData("1.5h", 5400)]
	[InlineData("1h30m", 5400)]
	[InlineData("-5m", -300)]
	[InlineData("+5s", 5)]
	[InlineData("10ms", 0.01)]
	[InlineData("1w", 7 * 86400)]
	[InlineData("0.5s", 0.5)]
	[InlineData("0", 0)]
	public void Parses_a_human_duration(string raw, double expectedTotalSeconds)
	{
		ArghTimeSpan.TryParse(raw, out var ts).Should().BeTrue();
		ts.TotalSeconds.Should().Be(expectedTotalSeconds);
	}

	[Theory]
	[InlineData("00:30", 30 * 60)]
	[InlineData("1:2", 3600 + 2 * 60)]
	[InlineData("01:30:00", 5400)]
	[InlineData("1.00:00:00", 86400)]
	[InlineData("1.02:03:04", 86400 + 2 * 3600 + 3 * 60 + 4)]
	[InlineData("-1:00", -3600)]
	[InlineData("00:00:00.5", 0.5)]
	public void Parses_the_TimeSpan_format_when_the_value_has_a_colon(string raw, double expectedTotalSeconds)
	{
		ArghTimeSpan.TryParse(raw, out var ts).Should().BeTrue();
		ts.TotalSeconds.Should().Be(expectedTotalSeconds);
	}

	[Theory]
	[InlineData("10", "Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("-10", "Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("25:00", "Expected [-][d.]hh:mm[:ss[.fffffff]], or a duration such as 1h30m.")]
	[InlineData("1h:30m", "Expected [-][d.]hh:mm[:ss[.fffffff]], or a duration such as 1h30m.")]
	[InlineData("nope", "Expected a number at position 1, but found 'n'.")]
	public void Rejects_a_value_and_says_why(string raw, string reason)
	{
		ArghTimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out _, out var error).Should().BeFalse();
		error.Should().Be(reason);
	}

	[Fact]
	public void Uses_the_culture_for_both_forms()
	{
		var german = CultureInfo.GetCultureInfo("de-DE");
		ArghTimeSpan.TryParse("1,5h", german, out var human).Should().BeTrue();
		human.Should().Be(TimeSpan.FromMinutes(90));
		ArghTimeSpan.TryParse("0:00:01,5", german, out var clock).Should().BeTrue();
		clock.Should().Be(TimeSpan.FromSeconds(1.5));
	}
}
