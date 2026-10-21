using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// <c>[UriOptions]</c> sets the <c>UriKind</c> of a <c>Uri</c>, and AGH0024 reports a <c>[UriOptions]</c> or
/// <c>[UriScheme]</c> that has no effect or that no value can pass.
/// </summary>
public class UriAttributeGeneratorTests
{
	private static string Source(string parameters, string dto = "") => Usings + $$"""
		using System.Collections.Generic;
		using System.IO;

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
	[InlineData("Uri link", "global::System.UriKind.Absolute")]
	[InlineData("[UriOptions(UriKind.Absolute)] Uri link", "global::System.UriKind.Absolute")]
	[InlineData("[UriOptions(UriKind.Relative)] Uri link", "global::System.UriKind.Relative")]
	[InlineData("[UriOptions(UriKind.RelativeOrAbsolute)] Uri? link = null", "global::System.UriKind.RelativeOrAbsolute")]
	[InlineData("[UriOptions(UriKind.Relative)] List<Uri> link", "global::System.UriKind.Relative")]
	[InlineData("[Argument, UriOptions(UriKind.Relative)] params Uri[] link", "global::System.UriKind.Relative")]
	public void Uri_is_parsed_with_its_kind(string parameter, string kind)
	{
		var generated = Generate(Source(parameter), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains($"global::Nullean.Argh.Runtime.ArghPathBinding.TryUri(", generated);
		Assert.Contains($", {kind}, out var ", generated);
	}

	[Fact]
	public void Uri_kind_on_a_dto_member_is_applied()
	{
		var generated = Generate(Source("[AsParameters] Args args",
			"public sealed record Args([UriOptions(UriKind.Relative)] Uri Path);"), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
		Assert.Contains(", global::System.UriKind.Relative, out var ", generated);
	}

	[Theory]
	[InlineData("[UriOptions(UriKind.Relative)] string link", "[UriOptions] only applies to Uri, Uri?, or a collection of Uri parameters and properties; it is ignored.")]
	[InlineData("[UriOptions(UriKind.Relative)] FileInfo link", "[UriOptions] only applies to Uri, Uri?, or a collection of Uri parameters and properties; it is ignored.")]
	[InlineData("[UriScheme(\"https\")] string link", "[UriScheme] only applies to Uri, Uri?, or a collection of Uri parameters and properties; it is ignored.")]
	[InlineData("[UriScheme(\"https\")] List<string> link", "[UriScheme] only applies to Uri, Uri?, or a collection of Uri parameters and properties; it is ignored.")]
	[InlineData("[UriOptions(UriKind.Relative), UriScheme(\"https\")] Uri link", "[UriOptions(UriKind.Relative)] with [UriScheme] or [Url] rejects every value: a relative URI has no scheme.")]
	[InlineData("[UriOptions(UriKind.Relative), Url] Uri? link = null", "[UriOptions(UriKind.Relative)] with [UriScheme] or [Url] rejects every value: a relative URI has no scheme.")]
	public void Ineffective_uri_attribute_reports_AGH0024(string parameter, string message)
	{
		Generate(Source(parameter), out var output, out var diagnostics);

		var diagnostic = Assert.Single(diagnostics);
		Assert.Equal("AGH0024", diagnostic.Id);
		Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
		Assert.Equal($"'link': {message}", diagnostic.GetMessage());
		AssertCompiles(output);
	}

	[Theory]
	[InlineData("[UriOptions(UriKind.RelativeOrAbsolute), UriScheme(\"https\")] Uri link")]
	[InlineData("[UriOptions(UriKind.Relative)] Uri[] link")]
	[InlineData("[UriScheme(\"https\")] List<Uri> link")]
	[InlineData("[Url] Uri[] link")]
	public void Uri_attributes_that_apply_report_nothing(string parameter)
	{
		Generate(Source(parameter), out var output, out var diagnostics);

		Assert.Empty(diagnostics);
		AssertCompiles(output);
	}
}
