using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Nullean.Argh.Generator.Tests;

/// <summary>Runs the generator over one source file for the numeric literal and <c>[Range]</c> tests.</summary>
internal static class NumericGeneratorHarness
{
	public const string Usings = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using System.Numerics;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		""";

	/// <summary>
	/// The generated <c>ArghGenerated.g.cs</c>, or an empty string when <paramref name="requireOutput"/> is false (a run
	/// that stops on an error diagnostic generates nothing).
	/// </summary>
	public static string Generate(string source, out Compilation output, out ImmutableArray<Diagnostic> diagnostics, bool requireOutput = true)
	{
		var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET7_0_OR_GREATER", "NET8_0_OR_GREATER"]);
		var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), parseOptions, path: "Program.cs");
		var compilation = CSharpCompilation.Create(
			"App",
			new[] { tree },
			BuildReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		var userErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		Assert.True(userErrors.Count == 0, string.Join(Environment.NewLine, userErrors.Select(e => e.ToString())));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new CliParserGenerator().AsSourceGenerator() }, parseOptions: parseOptions);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out _);
		var result = Assert.Single(driver.GetRunResult().Results);
		Assert.Null(result.Exception);
		diagnostics = result.Diagnostics;
		if (!requireOutput)
			return "";
		return Assert.Single(result.GeneratedSources, s => s.HintName == "ArghGenerated.g.cs").SourceText.ToString();
	}

	/// <summary>Asserts the compilation with the generated code has no errors.</summary>
	/// <remarks>ArghTypeBindingExtensions.g.cs uses C# 14 extension blocks, which the Roslyn version these tests reference can't parse.</remarks>
	public static void AssertCompiles(Compilation output)
	{
		var errors = output.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.Where(d => d.Location.SourceTree?.FilePath.EndsWith("ArghTypeBindingExtensions.g.cs", StringComparison.Ordinal) != true)
			.ToList();
		Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
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
