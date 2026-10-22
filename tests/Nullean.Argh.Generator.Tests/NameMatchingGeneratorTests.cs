using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Generated code matches command, namespace and option names with the comparers in <c>ParserOptions</c> rather than
/// fixing their case at build time, and AGH0041 warns about names that only the default command comparer makes the same.
/// </summary>
public class NameMatchingGeneratorTests
{
	private static string Source(string configure) => Usings + $$"""
		namespace App
		{
			public static class Commands
			{
				public static void Run(string name = "") { }

				public static void Other(bool verbose = false) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					{{configure}}
				}
			}
		}
		""";

	[Fact]
	public void Names_are_matched_through_the_configured_comparers()
	{
		var generated = Generate(Source("""
			app.Map("deploy", Commands.Run);
			app.MapNamespace("storage", "Storage commands", g => g.Map("list", Commands.Other));
			"""), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains("ArghNames.Check(s_nameScopes);", generated);
		Assert.Contains("__names_Root = ArghNames.Register(s_nameScopes, new ArghCommandSet(", generated);
		Assert.Contains("switch (__names_Root.Find(tok))", generated);
		Assert.Contains("switch (__names_storage.Find(args[idx[0]]))", generated);
		Assert.Contains("__names_storage.Suggest(__tok)", generated);
		Assert.Contains("ArghNames.IsHelp(args[idx[0]])", generated);
		Assert.Contains("ArghNames.IsVersion(args[idx[0]])", generated);
		Assert.Contains("ArghNames.Register(s_nameScopes, new global::Nullean.Argh.Runtime.ArghFlagSet(", generated);
		Assert.Contains("CompletionWriter.WriteCommands(", generated);
		Assert.Contains("CompletionWriter.WriteFlags(", generated);
		Assert.DoesNotContain("OrdinalIgnoreCase", generated);
		Assert.DoesNotContain("ToLowerInvariant", generated);
		Assert.DoesNotContain("== \"--help\"", generated);
	}

	[Theory]
	[InlineData("app.Map(\"deploy\", Commands.Run); app.Map(\"Deploy\", Commands.Other);", "deploy", "Deploy")]
	[InlineData("app.Map(\"deploy\", Commands.Run); app.MapNamespace(\"DEPLOY\", \"Deploy commands\", g => g.Map(\"run\", Commands.Other));", "deploy", "DEPLOY")]
	[InlineData("app.MapNamespace(\"storage\", \"Storage\", g => { g.Map(\"list\", Commands.Run); g.Map(\"List\", Commands.Other); });", "list", "List")]
	public void Names_differing_only_in_case_report_AGH0041(string configure, string first, string second)
	{
		var generated = Generate(Source(configure), out var output, out var diagnostics);

		var diagnostic = Assert.Single(diagnostics);
		Assert.Equal("AGH0041", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
		Assert.Equal(
			$"'{first}' and '{second}' differ only in case, so they collide under the default ParserOptions.CommandStringComparer (OrdinalIgnoreCase) and the app fails at startup; rename one, or set CommandStringComparer to StringComparer.Ordinal",
			diagnostic.GetMessage());
		AssertCompiles(output);
		Assert.Contains($"case \"{first}\":", generated);
		Assert.Contains($"case \"{second}\":", generated);
	}

	[Fact]
	public void The_same_name_in_different_scopes_reports_nothing()
	{
		Generate(Source("""
			app.Map("list", Commands.Run);
			app.MapNamespace("storage", "Storage", g => g.Map("List", Commands.Other));
			"""), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
	}

	[Fact]
	public void The_same_name_twice_is_still_AGH0027()
	{
		Generate(Source("app.Map(\"deploy\", Commands.Run); app.Map(\"deploy\", Commands.Other);"), out _, out var diagnostics, requireOutput: false);

		Assert.Equal("AGH0027", Assert.Single(diagnostics).Id);
	}
}
