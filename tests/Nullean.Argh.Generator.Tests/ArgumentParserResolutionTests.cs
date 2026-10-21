using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Which <c>IArgumentParser&lt;T&gt;</c> a custom parser is called through. A <c>T?</c> (<c>Nullable&lt;T&gt;</c>) value uses the
/// parser's <c>IArgumentParser&lt;T?&gt;</c> when it implements one, and otherwise its <c>IArgumentParser&lt;T&gt;</c>.
/// </summary>
public class ArgumentParserResolutionTests
{
	private const string Source = """
		using Nullean.Argh;
		using Nullean.Argh.Builder;
		using Nullean.Argh.Parsing;

		namespace App
		{
			public readonly record struct Point(int X, int Y);

			public sealed class PointParser : IArgumentParser<Point>
			{
				public bool TryParse(string raw, ParserOptions options, out Point value) { value = new Point(1, 1); return true; }
			}

			public sealed class EitherParser : IArgumentParser<Point>, IArgumentParser<Point?>
			{
				bool IArgumentParser<Point>.TryParse(string raw, ParserOptions options, out Point value) { value = default; return false; }
				bool IArgumentParser<Point?>.TryParse(string raw, ParserOptions options, out Point? value) { value = null; return true; }
			}

			public sealed class GlobalOpts
			{
				[ArgumentParser(typeof(PointParser))]
				public Point? Origin { get; set; }

				[ArgumentParser(typeof(EitherParser))]
				public Point? Fallback { get; set; }
			}

			public static class Commands
			{
				public static void Lifted([ArgumentParser(typeof(PointParser))] Point? at = null) { }
				public static void Exact([ArgumentParser(typeof(EitherParser))] Point? at = null) { }
				public static void Plain([ArgumentParser(typeof(EitherParser))] Point at) { }
			}

			public static class Registration
			{
				public static void Configure(IArghBuilder app)
				{
					app.UseGlobalOptions<GlobalOpts>();
					app.Map("lifted", Commands.Lifted);
					app.Map("exact", Commands.Exact);
					app.Map("plain", Commands.Plain);
				}
			}
		}
		""";

	[Fact]
	public void Nullable_values_use_the_exact_parser_interface_and_fall_back_to_the_underlying_one()
	{
		var (output, generated) = RunGenerator(Source);

		// EitherParser implements TryParse explicitly, so this compiles only when each call goes through an interface.
		// ArghTypeBindingExtensions.g.cs uses C# 14 extension blocks, which the Roslyn version these tests reference can't parse.
		var errors = output.GetDiagnostics()
			.Where(d => d.Location.SourceTree?.FilePath.EndsWith("ArghTypeBindingExtensions.g.cs", StringComparison.Ordinal) != true)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToList();
		Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));

		// PointParser for a Point? value: through IArgumentParser<Point>, converted to Point?.
		Assert.Contains("global::Nullean.Argh.Parsing.IArgumentParser<global::App.Point> __", generated);
		Assert.Contains("(global::App.Point?)", generated);
		// EitherParser for a Point? value: through IArgumentParser<Point?>.
		Assert.Contains("global::Nullean.Argh.Parsing.IArgumentParser<global::App.Point?> __", generated);
	}

	private static (Compilation Output, string Generated) RunGenerator(string source)
	{
		var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), new CSharpParseOptions(LanguageVersion.Preview), path: "Program.cs");
		var compilation = CSharpCompilation.Create(
			"App",
			[tree],
			BuildReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			[new CliParserGenerator().AsSourceGenerator()],
			parseOptions: (CSharpParseOptions)tree.Options);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
		var result = Assert.Single(driver.GetRunResult().Results);
		Assert.Null(result.Exception);
		return (output, string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString())));
	}

	private static ImmutableArray<MetadataReference> BuildReferences()
	{
		var builder = ImmutableArray.CreateBuilder<MetadataReference>();
		foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
			builder.Add(MetadataReference.CreateFromFile(path));
		builder.Add(MetadataReference.CreateFromFile(typeof(Nullean.Argh.Builder.IArghBuilder).Assembly.Location));
		builder.Add(MetadataReference.CreateFromFile(typeof(Nullean.Argh.Runtime.ArghRuntime).Assembly.Location));
		return builder.ToImmutable();
	}
}
