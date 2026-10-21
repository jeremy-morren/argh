using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// <c>bool</c> and <c>bool?</c> in every position compile, read their environment variable (explicit or from the prefix)
/// and need no warning for an <c>[Env]</c> on a plain <c>bool</c>. <c>UseEnvironmentPrefix</c> applies to every flag.
/// </summary>
public class BoolGeneratorTests
{
	private const string Source = Usings + """
		namespace App
		{
			public class GlobalOpts
			{
				public bool Quiet { get; set; }

				public bool? Color { get; set; }
			}

			public static class Commands
			{
				public static void Run(GlobalOpts g, bool force = false, bool? dryRun = null, [Env("APP_LOUD")] bool loud = false, string? region = null) { }

				[NoOptionsInjection]
				public static void Args([Argument] bool first, [Argument, Env("APP_SECOND")] bool? second = null) { }

				[NoOptionsInjection]
				public static void Bits(bool[] bits) { }
			}

			public static class Program
			{
				public static void Configure(IArghRootBuilder app)
				{
					app.UseEnvironmentPrefix("APP_");
					app.UseGlobalOptions<GlobalOpts>();
					app.Map("run", Commands.Run);
					app.Map("args", Commands.Args);
					app.Map("bits", Commands.Bits);
				}
			}
		}
		""";

	[Fact]
	public void Bools_in_every_position_compile_without_warnings()
	{
		Generate(Source, out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
	}

	[Theory]
	[InlineData("APP_FORCE")]
	[InlineData("APP_DRY_RUN")]
	[InlineData("APP_LOUD")]
	[InlineData("APP_QUIET")]
	[InlineData("APP_COLOR")]
	public void Bool_flags_read_their_environment_variable(string variable)
	{
		var generated = Generate(Source, out _, out _);
		Assert.Contains($"ArghBoolBinding.FromEnvironment(\"{variable}\", true)", generated);
	}

	[Fact]
	public void The_environment_prefix_applies_to_every_flag() =>
		Assert.Contains("GetEnvironmentVariable(\"APP_REGION\")", Generate(Source, out _, out _));

	[Fact]
	public void A_bool_argument_reads_its_environment_variable_as_text() =>
		Assert.Contains("ArghBoolBinding.EnvironmentText(\"APP_SECOND\", true)", Generate(Source, out _, out _));
}
