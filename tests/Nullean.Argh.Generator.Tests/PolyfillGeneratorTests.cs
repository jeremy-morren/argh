using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// The generator emits <c>ArghPolyfills.g.cs</c> with internal <c>ModuleInitializerAttribute</c> and
/// <c>NotNullWhenAttribute</c> definitions only when the compilation can't use existing ones. These tests compile
/// against the netstandard2.0 reference assembly, where neither attribute exists.
/// </summary>
public class PolyfillGeneratorTests
{
	private const string PolyfillFile = "ArghPolyfills.g.cs";

	private const string OwnPolyfills = """
		namespace System.Runtime.CompilerServices
		{
			[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
			internal sealed class ModuleInitializerAttribute : System.Attribute { }
		}

		namespace System.Diagnostics.CodeAnalysis
		{
			[System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = false)]
			internal sealed class NotNullWhenAttribute : System.Attribute
			{
				public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
				public bool ReturnValue { get; }
			}
		}
		""";

	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	private static ImmutableArray<MetadataReference> NetStandardReferences() =>
		[MetadataReference.CreateFromFile(Path.Combine(AppContext.BaseDirectory, "refs", "netstandard2.0", "netstandard.dll"))];

	private static ImmutableArray<MetadataReference> ModernReferences() =>
		((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
		.Split(Path.PathSeparator)
		.Select(static p => (MetadataReference)MetadataReference.CreateFromFile(p))
		.ToImmutableArray();

	private static CSharpCompilation Compile(string assemblyName, string source, ImmutableArray<MetadataReference> references) =>
		CSharpCompilation.Create(
			assemblyName,
			[CSharpSyntaxTree.ParseText(source, ParseOptions)],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

	private static MetadataReference ToImage(CSharpCompilation compilation)
	{
		using var stream = new MemoryStream();
		var emit = compilation.Emit(stream);
		Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	/// <summary>Runs the generator and returns the polyfill source, or null when none was emitted.</summary>
	private static string? GeneratedPolyfills(CSharpCompilation compilation)
	{
		GeneratorDriver driver = CSharpGeneratorDriver.Create([new CliParserGenerator().AsSourceGenerator()], parseOptions: ParseOptions);
		driver = driver.RunGenerators(compilation);
		var result = Assert.Single(driver.GetRunResult().Results);
		Assert.Null(result.Exception);
		return result.GeneratedSources.SingleOrDefault(s => s.HintName == PolyfillFile).SourceText?.ToString();
	}

	/// <summary>Compiler diagnostics that a duplicate or clashing attribute definition would cause.</summary>
	private static void AssertNoDuplicateDefinitions(CSharpCompilation compilation, string? polyfills)
	{
		var withOutput = polyfills is null ? compilation : compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(polyfills, ParseOptions));
		var clashes = withOutput.GetDiagnostics().Where(d => d.Id is "CS0101" or "CS0433" or "CS0436").ToList();
		Assert.True(clashes.Count == 0, string.Join(Environment.NewLine, clashes));
	}

	[Fact]
	public void Netstandard2_0_gets_both_polyfills_and_they_compile()
	{
		var compilation = Compile("App", "namespace App { public class Marker { } }", NetStandardReferences());

		var polyfills = GeneratedPolyfills(compilation);

		Assert.NotNull(polyfills);
		Assert.Contains("internal sealed class ModuleInitializerAttribute", polyfills);
		Assert.Contains("internal sealed class NotNullWhenAttribute", polyfills);
		var compiled = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(polyfills!, ParseOptions));
		Assert.Empty(compiled.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning));
	}

	[Fact]
	public void Modern_framework_gets_no_polyfills()
	{
		var compilation = Compile("App", "namespace App { public class Marker { } }", ModernReferences());

		Assert.Null(GeneratedPolyfills(compilation));
	}

	[Fact]
	public void Definitions_in_the_project_itself_are_used()
	{
		var compilation = Compile("App", OwnPolyfills, NetStandardReferences());

		var polyfills = GeneratedPolyfills(compilation);

		Assert.Null(polyfills);
		AssertNoDuplicateDefinitions(compilation, polyfills);
	}

	[Fact]
	public void Internal_definitions_in_a_reference_without_access_are_ignored()
	{
		var library = ToImage(Compile("Lib", OwnPolyfills, NetStandardReferences()));
		var compilation = Compile("App", "namespace App { public class Marker { } }", NetStandardReferences().Add(library));

		var polyfills = GeneratedPolyfills(compilation);

		Assert.NotNull(polyfills);
		Assert.Contains("ModuleInitializerAttribute", polyfills);
		Assert.Contains("NotNullWhenAttribute", polyfills);
		AssertNoDuplicateDefinitions(compilation, polyfills);
	}

	[Fact]
	public void Internal_definitions_visible_through_InternalsVisibleTo_are_used()
	{
		var librarySource = "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"App\")]\n" + OwnPolyfills;
		var library = ToImage(Compile("Lib", librarySource, NetStandardReferences()));
		var compilation = Compile("App", "namespace App { public class Marker { } }", NetStandardReferences().Add(library));

		var polyfills = GeneratedPolyfills(compilation);

		Assert.Null(polyfills);
		AssertNoDuplicateDefinitions(compilation, polyfills);
	}

	[Fact]
	public void Public_definitions_in_two_references_count_as_available()
	{
		// GetTypeByMetadataName returns null when two assemblies define the type; detection must not take that as missing.
		const string publicModuleInitializer = """
			namespace System.Runtime.CompilerServices
			{
				[System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
				public sealed class ModuleInitializerAttribute : System.Attribute { }
			}
			""";
		var first = ToImage(Compile("First", publicModuleInitializer, NetStandardReferences()));
		var second = ToImage(Compile("Second", publicModuleInitializer, NetStandardReferences()));
		var compilation = Compile("App", "namespace App { public class Marker { } }", NetStandardReferences().Add(first).Add(second));

		var polyfills = GeneratedPolyfills(compilation);

		Assert.NotNull(polyfills);
		Assert.DoesNotContain("ModuleInitializerAttribute", polyfills);
		Assert.Contains("NotNullWhenAttribute", polyfills);
	}
}
