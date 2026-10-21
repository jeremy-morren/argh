using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Types that implement <c>IParsable&lt;T&gt;</c> are parsed through a constrained generic helper, after
/// <c>[ArgumentParser]</c> and the built-in kinds.
/// </summary>
public class ParsableGeneratorTests
{
	private const string AppSource = """
		using System;
		using System.Diagnostics.CodeAnalysis;
		using System.Globalization;
		using Nullean.Argh;
		using Nullean.Argh.Builder;
		using Nullean.Argh.Parsing;

		namespace App
		{
			public readonly record struct OrderId(int Number) : IParsable<OrderId>
			{
				public static OrderId Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));
				public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out OrderId result)
				{
					var ok = int.TryParse(s, NumberStyles.Integer, provider, out var n);
					result = new OrderId(n);
					return ok;
				}
			}

			public sealed class Hidden : IParsable<Hidden>
			{
				static Hidden IParsable<Hidden>.Parse(string s, IFormatProvider? provider) => new();
				static bool IParsable<Hidden>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Hidden result)
				{
					result = new Hidden();
					return true;
				}
			}

			public sealed class OrderIdParser : IArgumentParser<OrderId>
			{
				public bool TryParse(string raw, Nullean.Argh.Parsing.ParserOptions options, out OrderId value)
				{
					value = new OrderId(1);
					return true;
				}
			}

			public static class Commands
			{
				public static void Run(OrderId plain, Hidden hidden, [ArgumentParser(typeof(OrderIdParser))] OrderId viaParser, ushort port, int count = 1) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app) => app.Map("run", Commands.Run);
			}
		}
		""";

	[Fact]
	public void An_ArgumentParser_wins_over_IParsable_and_built_in_kinds_keep_their_own_parsing()
	{
		var generated = Generate(out _);

		Assert.Contains("TryParseParsable<global::App.OrderId>(", generated);
		Assert.Contains("TryParseParsable<global::App.Hidden>(", generated);
		Assert.Contains("new global::App.OrderIdParser()", generated);
		// Exactly the two parameters without a parser go through IParsable; int keeps its NumberStyles parse.
		Assert.Equal(2, CountOccurrences(generated, "if (!TryParseParsable<"));
		Assert.DoesNotContain("TryParseParsable<int>", generated);
		Assert.DoesNotContain("TryParseNumberBase<int>", generated);
		// ushort has no built-in handling and implements INumberBase, so NumberStyles can reach it.
		Assert.Contains("TryParseNumberBase<ushort>(", generated);
	}

	[Fact]
	public void Explicit_implementations_compile_through_the_constrained_helper()
	{
		Generate(out var output);

		// ArghTypeBindingExtensions.g.cs uses C# 14 extension blocks, which the Roslyn version these tests reference can't parse.
		var errors = output.GetDiagnostics()
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.Where(d => d.Location.SourceTree?.FilePath.EndsWith("ArghTypeBindingExtensions.g.cs", StringComparison.Ordinal) != true)
			.ToList();
		Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
	}

	[Fact]
	public void The_helper_exists_only_where_IParsable_does()
	{
		var generated = Generate(out _);

		var helper = generated.IndexOf("private static bool TryParseParsable<T>", StringComparison.Ordinal);
		Assert.True(helper > 0);
		Assert.Equal("#if NET7_0_OR_GREATER", LastLineBefore(generated, helper));
	}

	private static string LastLineBefore(string text, int index)
	{
		var lines = text.Substring(0, index).TrimEnd().Split('\n');
		return lines[^1].Trim();
	}

	private static int CountOccurrences(string text, string value)
	{
		var count = 0;
		for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
			count++;
		return count;
	}

	private static string Generate(out Compilation output)
	{
		// The SDK defines NET7_0_OR_GREATER for .NET 7+ targets; the helper is behind it.
		var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET7_0_OR_GREATER", "NET8_0_OR_GREATER"]);
		var tree = CSharpSyntaxTree.ParseText(SourceText.From(AppSource, Encoding.UTF8), parseOptions, path: "Program.cs");
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
		return Assert.Single(result.GeneratedSources, s => s.HintName == "ArghGenerated.g.cs").SourceText.ToString();
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
