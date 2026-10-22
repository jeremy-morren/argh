using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// <c>[RepeatedScalar]</c> sets a flag's repeat rule in the flags the generated code hands to <c>ArghTokenizer</c>, and
/// AGH0039 reports one where it has no effect.
/// </summary>
public class RepeatedScalarGeneratorTests
{
	private static string Source(string parameters, string dto = "") => Usings + $$"""
		using System.Collections.Generic;

		namespace App
		{
			{{dto}}

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
	[InlineData("[RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string name", "TakeLast")]
	[InlineData("[RepeatedScalar(RepeatedScalarBehaviour.AllowIdentical)] bool verbose = false", "AllowIdentical")]
	[InlineData("[RepeatedScalar(RepeatedScalarBehaviour.Reject)] int? count = null", "Reject")]
	public void Flag_rule_is_passed_to_the_tokenizer(string parameter, string behaviour)
	{
		var generated = Generate(Source(parameter), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains($"repeated: global::Nullean.Argh.RepeatedScalarBehaviour.{behaviour})", generated);
	}

	[Theory]
	[InlineData("[property: RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string Level")]
	[InlineData("[RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string Level")]
	public void Flag_rule_on_a_dto_member_is_passed_to_the_tokenizer(string member)
	{
		var generated = Generate(Source("[AsParameters] Args args", $"public sealed record Args({member});"), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains("repeated: global::Nullean.Argh.RepeatedScalarBehaviour.TakeLast)", generated);
	}

	[Fact]
	public void Flag_without_a_rule_follows_the_parser_options()
	{
		var generated = Generate(Source("string name"), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.DoesNotContain("repeated:", generated);
	}

	[Theory]
	[InlineData("[RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] List<string> tags", "tags", "a collection keeps every value it's given.")]
	[InlineData("[Argument, RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] string file", "file", "a positional argument isn't a flag.")]
	[InlineData("[Argument, RepeatedScalar(RepeatedScalarBehaviour.TakeLast)] params string[] files", "files", "a collection keeps every value it's given.")]
	public void Rule_that_has_no_effect_reports_AGH0039(string parameter, string name, string reason)
	{
		var generated = Generate(Source(parameter), out var output, out var diagnostics);

		var diagnostic = Assert.Single(diagnostics);
		Assert.Equal("AGH0039", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
		Assert.Equal($"[RepeatedScalar] on '{name}' has no effect: {reason}", diagnostic.GetMessage());
		AssertCompiles(output);
		Assert.DoesNotContain("repeated:", generated);
	}
}
