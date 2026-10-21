using Microsoft.CodeAnalysis;
using Xunit;
using static Nullean.Argh.Generator.Tests.NumericGeneratorHarness;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// A collection's elements are parsed and validated by the code for a single value of the element type, so every element
/// type and every value constraint that works on a single value works on a collection of it.
/// </summary>
public class CollectionElementGeneratorTests
{
	// A params array can't be a record member, so only the method parameter has one.
	private static string Source(string parameter) => Usings + $$"""
		using System.Collections.Generic;
		using System.IO;
		using Nullean.Argh.Parsing;

		namespace App
		{
			public enum Level { Low, High }

			public readonly record struct Point(int X, int Y);

			public sealed class PointParser : IArgumentParser<Point>
			{
				public bool TryParse(string raw, ParserOptions options, out Point value) { value = new Point(1, 1); return true; }
			}

			public sealed class PointsParser : IArgumentParser<Point[]>
			{
				public bool TryParse(string raw, ParserOptions options, out Point[] value) { value = [new Point(1, 1)]; return true; }
			}

			public sealed record Args({{parameter.Replace("params ", "")}});

			public static class Commands
			{
				public static void Run({{parameter}}) { }
				public static void Dto([AsParameters] Args args) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.Map("run", Commands.Run);
					app.Map("dto", Commands.Dto);
				}
			}
		}
		""";

	private static string CompilesCleanly(string parameter)
	{
		var generated = Generate(Source(parameter), out var output, out var diagnostics);
		Assert.Empty(diagnostics);
		AssertCompiles(output);
		var nullableWarnings = output.GetDiagnostics()
			.Where(static d => d.Id.StartsWith("CS86", StringComparison.Ordinal))
			.Where(static d => d.Location.SourceTree?.FilePath.EndsWith("ArghGenerated.g.cs", StringComparison.Ordinal) == true)
			.ToList();
		Assert.True(nullableWarnings.Count == 0, string.Join(Environment.NewLine, nullableWarnings.Select(static d => d.ToString())));
		return generated;
	}

	[Theory]
	[InlineData("int?[] Values")]
	[InlineData("List<decimal?> Values")]
	[InlineData("bool?[] Values")]
	[InlineData("Level?[] Values")]
	[InlineData("IReadOnlySet<Guid?> Values")]
	[InlineData("IReadOnlyList<TimeOnly?> Values")]
	[InlineData("string?[] Values")]
	[InlineData("List<Uri?> Values")]
	[InlineData("FileInfo?[] Values")]
	[InlineData("[Argument] params int?[] Values")]
	public void A_collection_of_nullable_elements_compiles(string parameter) => CompilesCleanly(parameter);

	[Theory]
	[InlineData("[ArgumentParser(typeof(PointParser))] Point[] Values")]
	[InlineData("[ArgumentParser(typeof(PointParser))] List<Point?> Values")]
	[InlineData("[ArgumentParser(typeof(PointParser)), CollectionSyntax(Separator = \";\")] IReadOnlySet<Point> Values")]
	[InlineData("[Argument, ArgumentParser(typeof(PointParser))] params Point[] Values")]
	public void A_parser_for_the_element_type_parses_each_element(string parameter)
	{
		var generated = CompilesCleanly(parameter);
		Assert.Contains("global::Nullean.Argh.Parsing.IArgumentParser<global::App.Point>", generated);
		Assert.DoesNotContain("IArgumentParser<global::App.Point[]>", generated);
	}

	[Fact]
	public void A_parser_for_the_collection_type_parses_the_whole_value()
	{
		var generated = CompilesCleanly("[ArgumentParser(typeof(PointsParser))] Point[] Values");
		Assert.Contains("global::Nullean.Argh.Parsing.IArgumentParser<global::App.Point[]>", generated);
		Assert.DoesNotContain("ArghCollectionBinding.Elements(", generated);
	}

	[Theory]
	[InlineData("[Range(1, 10)] int[] Values")]
	[InlineData("[Range(typeof(decimal), \"0.5\", \"2.5\")] List<decimal?> Values")]
	[InlineData("[AllowedValues(1, 2)] int[] Values")]
	[InlineData("[DeniedValues(\"x\")] List<string> Values")]
	[InlineData("[RegularExpression(\"^a\")] string[] Values")]
	[InlineData("[EmailAddress] List<string> Values")]
	[InlineData("[Url] string[] Values")]
	[InlineData("[Url] Uri[] Values")]
	[InlineData("[UriScheme(\"https\")] List<Uri> Values")]
	[InlineData("[TimeSpanRange(\"00:00:01\", \"01:00:00\")] TimeSpan[] Values")]
	[InlineData("[Range(1, 10), MinLength(1), MaxLength(3)] int[]? Values = null")]
	[InlineData("[Argument, Range(1, 10)] params int[] Values")]
	public void A_value_constraint_on_a_collection_checks_each_element(string parameter)
	{
		var generated = CompilesCleanly(parameter.Replace("Values = null", "Values"));
		Assert.Contains("var __vitem_", generated);
	}

	[Fact]
	public void A_count_constraint_checks_the_collection_and_not_its_elements()
	{
		var generated = CompilesCleanly("[MinLength(2)] List<string> Values");
		Assert.Contains(".Count < 2", generated);
		Assert.DoesNotContain("var __vitem_", generated);
	}

	[Fact]
	public void Elements_are_split_with_empty_parts_kept_and_read_from_the_environment()
	{
		var generated = CompilesCleanly("[Env(\"APP_IDS\"), CollectionSyntax(Separator = \",\")] List<int> Values");
		Assert.Contains("global::Nullean.Argh.Runtime.ArghCollectionBinding.Elements(", generated);
		Assert.Contains("GetEnvironmentVariable(\"APP_IDS\")", generated);
		Assert.DoesNotContain("IsNullOrEmpty(__part)", generated);
	}
}
