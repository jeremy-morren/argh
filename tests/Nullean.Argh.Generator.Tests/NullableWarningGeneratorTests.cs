using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Validated values that may be unset but whose declared type isn't nullable (a <c>string</c> with a default) compile
/// without nullable warnings in the generated code, in a project with nullable reference types enabled.
/// </summary>
public class NullableWarningGeneratorTests
{
	private const string Source = Usings + """
		using System.IO;

		namespace App
		{
			public class GlobalOpts
			{
				[AllowedValues("eu", "us")]
				public string Zone { get; set; } = "eu";
			}

			public sealed class NsOpts : GlobalOpts
			{
				[AllowedValues("fast", "slow")]
				public string Mode { get; set; } = "fast";

				[StringLength(10)]
				public string Name { get; set; } = "x";

				[RegularExpression("^[a-z]+$")]
				public string Code { get; set; } = "abc";

				[DeniedValues("none")]
				public string Tag { get; set; } = "t";

				[EmailAddress]
				public string Mail { get; set; } = "a@b.c";

				[Url]
				public string Site { get; set; } = "https://example.com";

				[UriScheme("https")]
				public Uri Endpoint { get; set; } = new("https://example.com");

				[Existing, RejectSymbolicLinks, FileExtensions(Extensions = "txt")]
				public FileInfo Doc { get; set; } = new("a.txt");

				[NonExisting]
				public DirectoryInfo Output { get; set; } = new("out");

				[StringLength(10)]
				public string? Note { get; set; }

				public string Label { get; set; } = "l";
			}

			public static class Commands
			{
				public static void Show(NsOpts o) { }

				public static void Run(
					GlobalOpts g,
					[AllowedValues("fast", "slow")] string mode = "fast",
					[StringLength(10)] string name = "x",
					[RegularExpression("^[a-z]+$")] string code = "abc",
					[DeniedValues("none")] string tag = "t",
					[EmailAddress] string mail = "a@b.c",
					[Url] string site = "https://example.com",
					[StringLength(10)] string? note = null) { }

				public static void Required(GlobalOpts g, [AllowedValues("fast", "slow")] string mode, [UriScheme("https")] Uri endpoint, [Existing] FileInfo doc) { }
			}

			public static class Program
			{
				public static void Configure(IArghRootBuilder app)
				{
					app.UseGlobalOptions<GlobalOpts>();
					app.MapNamespace("ns", "Validated namespace options.", g =>
					{
						g.UseNamespaceOptions<NsOpts>();
						g.Map("show", Commands.Show);
					});
					app.Map("run", Commands.Run);
					app.Map("required", Commands.Required);
				}
			}
		}
		""";

	[Fact]
	public void Validated_non_nullable_values_with_defaults_compile_without_nullable_warnings()
	{
		Generate(Source, out var output, out var diagnostics);
		Assert.DoesNotContain(diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
		AssertCompiles(output);
		var nullableWarnings = output.GetDiagnostics()
			.Where(static d => d.Id.StartsWith("CS86", StringComparison.Ordinal))
			.Where(static d => d.Location.SourceTree?.FilePath.EndsWith("ArghGenerated.g.cs", StringComparison.Ordinal) == true)
			.ToList();
		Assert.True(nullableWarnings.Count == 0, string.Join(Environment.NewLine, nullableWarnings.Select(static d => d.ToString())));
	}
}
