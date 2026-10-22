using System.Globalization;
using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// <c>[TimeSpanRange]</c> bounds are parsed at build time as a <c>TimeSpan</c> value is parsed at runtime, so the check
/// compares ticks, <c>__schema</c> gets ISO 8601 durations, and a bound that doesn't parse is AGH0040.
/// </summary>
public class TimeSpanRangeGeneratorTests
{
	private static string Source(string parameters, string types = "") => Usings + $$"""
		namespace App
		{
			{{types}}

			public static class Commands
			{
				public static void Run({{parameters}}) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
			}
		}
		""";

	[Theory]
	[InlineData("\"5m\"", "\"1h30m\"", 3_000_000_000L, 54_000_000_000L, "PT5M", "PT1H30M")]
	[InlineData("\"00:00:01\"", "\"1.02:03:04\"", 10_000_000L, 937_840_000_000L, "PT1S", "P1DT2H3M4S")]
	[InlineData("\"-1.5h\"", "\"0\"", -54_000_000_000L, 0L, "-PT1H30M", "PT0S")]
	[InlineData("\"100ms\"", "\"2w\"", 1_000_000L, 12_096_000_000_000L, "PT0.1S", "P14D")]
	public void Bounds_are_parsed_at_build_time(string min, string max, long minTicks, long maxTicks, string isoMin, string isoMax)
	{
		var generated = Generate(Source($"[TimeSpanRange({min}, {max})] TimeSpan? window = null"), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains($".Ticks < {minTicks}L || ", generated);
		Assert.Contains($".Ticks > {maxTicks}L", generated);
		Assert.Contains($"value must be between {min.Trim('"')} and {max.Trim('"')}.", generated);
		Assert.Contains($"new CliConstraintSchema(\"timeSpanRange\", Min: \"{isoMin}\", Max: \"{isoMax}\")", generated);
		Assert.DoesNotContain("ArghTimeSpan.TryParse(\"", generated);
	}

	[Theory]
	[InlineData("[TimeSpanRange(\"10\", \"1h\")] TimeSpan window", "\"10\"", "window", "\"10\" isn't a duration. Expected a unit after 10, such as 10s or 10m.")]
	[InlineData("[TimeSpanRange(\"1m\", \"5x\")] TimeSpan window", "\"5x\"", "window", "\"5x\" isn't a duration. Unknown unit 'x'.")]
	[InlineData("[TimeSpanRange(\"1,5m\", \"1h\")] TimeSpan window", "\"1,5m\"", "window", "\"1,5m\" isn't a duration. Unexpected ','")]
	[InlineData("[TimeSpanRange(\"25:00\", \"23:00\")] TimeSpan window", "\"25:00\"", "window", "\"25:00\" isn't a duration.")]
	[InlineData("[TimeSpanRange(\"2h\", \"1h\")] TimeSpan? window = null", "\"2h\"", "window", "the minimum \"2h\" is greater than the maximum \"1h\".")]
	[InlineData("[TimeSpanRange(\"\", \"1h\")] TimeSpan[] windows", "\"\"", "windows", "\"\" isn't a duration. The duration is empty.")]
	public void An_unusable_bound_reports_AGH0040_at_the_argument(string parameter, string offending, string name, string reason)
	{
		var source = Source(parameter);
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0040(Assert.Single(diagnostics), source, offending, name, reason);
	}

	[Fact]
	public void An_unusable_bound_on_a_dto_member_reports_AGH0040()
	{
		var source = Source("[AsParameters] Args args",
			"public sealed record Args([TimeSpanRange(\"1m\", \"soon\")] TimeSpan? Wait);");
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0040(Assert.Single(diagnostics), source, "\"soon\"", "Wait", "\"soon\" isn't a duration.");
	}

	[Fact]
	public void An_unusable_bound_on_a_global_options_member_reports_AGH0040()
	{
		var source = Usings + """
			namespace App
			{
				public sealed class Opts { [TimeSpanRange("later", "1h")] public TimeSpan? Wait { get; set; } }

				public static class Commands
				{
					public static void Run(Opts opts) { }
				}

				public static class Program
				{
					public static void Configure(IArghBuilder app)
					{
						app.UseGlobalOptions<Opts>();
						app.Map("run", Commands.Run);
					}
				}
			}
			""";
		Generate(source, out _, out var diagnostics, requireOutput: false);

		AssertAgh0040(Assert.Single(diagnostics), source, "\"later\"", "Wait", "\"later\" isn't a duration.");
	}

	private static void AssertAgh0040(Diagnostic diagnostic, string source, string offending, string parameter, string reason)
	{
		Assert.Equal("AGH0040", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
		Assert.Equal("Program.cs", diagnostic.Location.GetLineSpan().Path);
		Assert.Equal(offending, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
		var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
		Assert.StartsWith($"[TimeSpanRange] on '{parameter}' can't be checked: ", message);
		Assert.Contains(reason, message);
	}
}
