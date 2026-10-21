using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using TrackingNames = Nullean.Argh.CliParserGenerator.TrackingNames;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// The incremental pipeline must reuse its work when an edit doesn't affect it, and its step outputs must not hold
/// compiler objects, which would defeat caching and keep whole compilations alive.
/// </summary>
public class IncrementalCachingTests
{
	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	private const string AppSource = """
		using Nullean.Argh.Builder;

		namespace App
		{
			public static class Handlers
			{
				/// <summary>Says hello.</summary>
				/// <param name="name">Who to greet.</param>
				public static void Hello(string name) => System.Console.WriteLine(name);
			}

			public static class Cli
			{
				public static void Configure(IArghBuilder app) => app.Map("hello", Handlers.Hello);
			}
		}
		""";

	private static readonly string[] TrackedSteps =
	[
		TrackingNames.AssemblyInfo,
		TrackingNames.ParseOptions,
		TrackingNames.Capabilities,
		TrackingNames.ArtifactsPath,
		TrackingNames.AnalyzedInvocation,
		TrackingNames.AnalyzedInvocations,
		TrackingNames.Polyfills,
	];

	/// <summary>Steps whose output must stay cached across an edit that doesn't affect them.</summary>
	private static readonly string[] CachedSteps =
	[
		TrackingNames.AssemblyInfo,
		TrackingNames.ParseOptions,
		TrackingNames.Capabilities,
		TrackingNames.ArtifactsPath,
		TrackingNames.Polyfills,
	];

	private static readonly Type[] ForbiddenTypes =
		[typeof(Compilation), typeof(SemanticModel), typeof(ISymbol), typeof(SyntaxNode), typeof(Location)];

	private static CSharpCompilation CreateCompilation()
	{
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Select(static p => (MetadataReference)MetadataReference.CreateFromFile(p))
			.Append(MetadataReference.CreateFromFile(typeof(Nullean.Argh.Builder.IArghBuilder).Assembly.Location));
		return CSharpCompilation.Create(
			"App",
			[CSharpSyntaxTree.ParseText(AppSource, ParseOptions)],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
	}

	private static GeneratorDriver CreateDriver() =>
		CSharpGeneratorDriver.Create(
			[new CliParserGenerator().AsSourceGenerator()],
			parseOptions: ParseOptions,
			driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

	/// <summary>Runs the generator, then again after adding a file that has nothing to do with the CLI.</summary>
	private static GeneratorRunResult RunTwiceWithUnrelatedEdit()
	{
		var compilation = CreateCompilation();
		var driver = CreateDriver().RunGenerators(compilation);
		var first = Assert.Single(driver.GetRunResult().Results);
		Assert.Null(first.Exception);
		Assert.Contains(first.GeneratedSources, s => s.HintName == "ArghGenerated.g.cs");

		var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
			"namespace App { // an unrelated edit\n internal sealed class Unrelated { } }", ParseOptions));
		driver = driver.RunGenerators(edited);
		return Assert.Single(driver.GetRunResult().Results);
	}

	private static void AssertReused(GeneratorRunResult result, string stepName)
	{
		Assert.True(result.TrackedSteps.TryGetValue(stepName, out var steps), $"step '{stepName}' was not tracked");
		foreach (var output in steps.SelectMany(static s => s.Outputs))
		{
			Assert.True(output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
				$"step '{stepName}' re-ran with reason {output.Reason}");
		}
	}

	[Fact]
	public void An_unrelated_edit_reuses_the_input_steps_and_the_polyfill_output()
	{
		var result = RunTwiceWithUnrelatedEdit();

		foreach (var name in CachedSteps)
			AssertReused(result, name);

		// The polyfill file comes from its own source output, fed only by the polyfill step.
		var polyfillOutput = result.TrackedOutputSteps.SelectMany(static kv => kv.Value)
			.Single(static s => s.Inputs.Any(static i => i.Source.Name == TrackingNames.Polyfills));
		Assert.All(polyfillOutput.Outputs, static o => Assert.Equal(IncrementalStepRunReason.Cached, o.Reason));
	}

	[Fact(Skip = "AIMapCommand.Commands, and the CommandModel/ParameterModel/OptionsTypeModel records inside it, hold "
	             + "ImmutableArray<T>, which compares by reference. Each edit re-creates equal records that compare unequal, "
	             + "so the collected invocations and the main source output re-run. Needs an equatable array type in the models.")]
	public void An_unrelated_edit_reuses_the_analyzed_invocations_and_the_main_output()
	{
		var result = RunTwiceWithUnrelatedEdit();

		AssertReused(result, TrackingNames.AnalyzedInvocation);
		AssertReused(result, TrackingNames.AnalyzedInvocations);
		Assert.All(result.TrackedOutputSteps.SelectMany(static kv => kv.Value).SelectMany(static s => s.Outputs),
			static o => Assert.Equal(IncrementalStepRunReason.Cached, o.Reason));
	}

	[Fact]
	public void Tracked_step_outputs_hold_no_compilations_symbols_syntax_or_locations()
	{
		var driver = CreateDriver().RunGenerators(CreateCompilation());
		var result = Assert.Single(driver.GetRunResult().Results);

		foreach (var name in TrackedSteps)
		{
			Assert.True(result.TrackedSteps.TryGetValue(name, out var steps), $"step '{name}' was not tracked");
			foreach (var output in steps.SelectMany(static s => s.Outputs))
				AssertHoldsNoCompilerObjects(output.Value, name);
		}
	}

	/// <summary>Walks the object graph of a step output, as in the Roslyn incremental-generator cookbook.</summary>
	private static void AssertHoldsNoCompilerObjects(object? value, string stepName)
	{
		var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
		Visit(value, stepName);

		void Visit(object? node, string path)
		{
			if (node is null || !visited.Add(node))
				return;

			var type = node.GetType();
			foreach (var forbidden in ForbiddenTypes)
				Assert.False(forbidden.IsAssignableFrom(type), $"step '{stepName}' holds a {type.Name} at {path}");

			if (type.IsPrimitive || type.IsEnum || node is string or decimal or Type)
				return;

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
			    && (bool)type.GetProperty(nameof(ImmutableArray<int>.IsDefault))!.GetValue(node)!)
				return;

			if (node is IEnumerable items)
			{
				var i = 0;
				foreach (var item in items)
					Visit(item, $"{path}[{i++}]");
				return;
			}

			for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
			{
				foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					Visit(field.GetValue(node), $"{path}.{field.Name}");
			}
		}
	}
}
