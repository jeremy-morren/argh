using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Nullean.Argh.Generator.Tests;

/// <summary>
/// Obsolete and experimental APIs that the generated code references are reported at the registration call
/// (AGH0036, AGH0037, or the API's own diagnostic ID), and the generated code itself compiles without warnings for them.
/// </summary>
public class SymbolUsageDiagnosticsTests
{
	private const string AppSource = """
		using System;
		using System.Diagnostics.CodeAnalysis;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		namespace App
		{
			public enum Mode
			{
				Fast,
				[Obsolete("Use Fast.")] Slow,
				[Experimental("APPEXP001", UrlFormat = "https://example.test/{0}")] Turbo
			}

			public sealed class RunArgs
			{
				public string? Name { get; init; }

				[Obsolete("Use --name.")]
				public string? Legacy { get; init; }
			}

			public static class Commands
			{
				public static void Run(Mode mode = Mode.Fast) { }

				public static void Dto([AsParameters] RunArgs args) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.Map("run", Commands.Run);
					app.Map("dto", Commands.Dto);
				}

				[Obsolete("Legacy setup.")]
				public static void ConfigureLegacy(IArghBuilder app) => app.Map("legacy-dto", Commands.Dto);
			}
		}
		""";

	[Fact]
	public void Obsolete_members_used_by_generated_code_are_reported_at_the_registration()
	{
		var (diagnostics, _, tree) = RunGenerator();

		var obsolete = diagnostics.Where(d => d.Id == "AGH0036").OrderBy(d => d.Location.SourceSpan.Start).ToList();
		Assert.Equal(2, obsolete.Count);

		AssertAtRegistration(obsolete[0], tree, "app.Map(\"run\"");
		Assert.Equal(DiagnosticSeverity.Warning, obsolete[0].Severity);
		Assert.Equal("'Mode.Slow' is obsolete: 'Use Fast.' Argh's generated code uses it for this registration.", obsolete[0].GetMessage());

		AssertAtRegistration(obsolete[1], tree, "app.Map(\"dto\"");
		Assert.Equal("'RunArgs.Legacy' is obsolete: 'Use --name.' Argh's generated code uses it for this registration.", obsolete[1].GetMessage());
	}

	[Fact]
	public void Registration_inside_an_obsolete_member_reports_no_obsolete_usage()
	{
		var (diagnostics, _, tree) = RunGenerator();

		var legacyLine = LineOf(tree, "app.Map(\"legacy-dto\"");
		Assert.DoesNotContain(diagnostics, d => d.Id == "AGH0036" && d.Location.GetLineSpan().StartLinePosition.Line == legacyLine);
	}

	[Fact]
	public void Experimental_members_used_by_generated_code_are_reported_under_their_own_id()
	{
		var (diagnostics, _, tree) = RunGenerator();

		var experimental = Assert.Single(diagnostics, d => d.Id == "APPEXP001");
		AssertAtRegistration(experimental, tree, "app.Map(\"run\"");
		Assert.Equal(DiagnosticSeverity.Error, experimental.Severity);
		Assert.Equal("https://example.test/APPEXP001", experimental.Descriptor.HelpLinkUri);
		Assert.StartsWith("'Mode.Turbo' is for evaluation purposes only", experimental.GetMessage());
	}

	[Fact]
	public void Generated_code_compiles_without_obsolete_or_experimental_diagnostics()
	{
		var (_, output, _) = RunGenerator();

		// ArghTypeBindingExtensions.g.cs uses C# 14 extension blocks, which the Roslyn version these tests reference
		// (the generator's minimum) can't parse. The solution build compiles it with the SDK's compiler.
		var problems = output.GetDiagnostics()
			.Where(d => d.Location.SourceTree?.FilePath.EndsWith("ArghTypeBindingExtensions.g.cs", StringComparison.Ordinal) != true)
			.Where(d => d.Severity == DiagnosticSeverity.Error || d.Id is "CS0612" or "CS0618" or "APPEXP001")
			.ToList();
		Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Select(p => p.ToString())));
	}

	private const string ErrorAndCustomIdSource = """
		using System;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		namespace App
		{
			public sealed class Args
			{
				public string? Name { get; init; }

				[Obsolete("Removed.", error: true)]
				public string? Gone { get; init; }

				[Obsolete("Use --name.", DiagnosticId = "APPOBS001", UrlFormat = "https://example.test/{0}")]
				public string? Old { get; init; }

				[Obsolete("Removed too.", true, DiagnosticId = "APPOBS002")]
				public string? Older { get; init; }
			}

			public static class Commands
			{
				public static void Run([AsParameters] Args args) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.Map("run", Commands.Run);
				}
			}
		}
		""";

	[Fact]
	public void Obsolete_error_is_reported_as_an_AGH0037_error()
	{
		var (diagnostics, _, tree) = RunGenerator(ErrorAndCustomIdSource);

		var error = Assert.Single(diagnostics, d => d.Id == "AGH0037");
		AssertAtRegistration(error, tree, "app.Map(\"run\"");
		Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		Assert.StartsWith("'Args.Gone' is obsolete: 'Removed.'", error.GetMessage());
	}

	[Fact]
	public void Obsolete_with_a_DiagnosticId_is_reported_under_that_id_and_severity()
	{
		var (diagnostics, _, tree) = RunGenerator(ErrorAndCustomIdSource);

		var warning = Assert.Single(diagnostics, d => d.Id == "APPOBS001");
		AssertAtRegistration(warning, tree, "app.Map(\"run\"");
		Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
		Assert.Equal("https://example.test/APPOBS001", warning.Descriptor.HelpLinkUri);
		Assert.StartsWith("'Args.Old' is obsolete: 'Use --name.'", warning.GetMessage());

		var error = Assert.Single(diagnostics, d => d.Id == "APPOBS002");
		Assert.Equal(DiagnosticSeverity.Error, error.Severity);
		Assert.DoesNotContain(diagnostics, d => d.Id == "AGH0036" || (d.Id == "AGH0037" && !d.GetMessage().Contains("Args.Gone")));
	}

	private const string ExperimentalLibrarySource = """
		[assembly: System.Diagnostics.CodeAnalysis.Experimental("LIBEXP001")]

		namespace Lib
		{
			public enum LibMode { A, B }
		}
		""";

	private const string ExperimentalTypeSource = """
		using System;
		using System.Diagnostics.CodeAnalysis;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		namespace App
		{
			[Experimental("APPEXP002")]
			public sealed class PreviewArgs
			{
				public string? Name { get; init; }
			}

			public static class Commands
			{
		#pragma warning disable APPEXP002, LIBEXP001
				public static void Preview([AsParameters] PreviewArgs args) { }

				public static void FromLib(Lib.LibMode mode) { }
		#pragma warning restore APPEXP002, LIBEXP001
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.Map("preview", Commands.Preview);
					app.Map("from-lib", Commands.FromLib);
				}
			}
		}
		""";

	[Fact]
	public void Experimental_types_and_assemblies_are_reported_under_their_own_id()
	{
		var library = CompileLibrary("Lib", ExperimentalLibrarySource);
		var (diagnostics, _, tree) = RunGenerator(ExperimentalTypeSource, library);

		var type = Assert.Single(diagnostics, d => d.Id == "APPEXP002");
		AssertAtRegistration(type, tree, "app.Map(\"preview\"");
		Assert.Equal(DiagnosticSeverity.Error, type.Severity);

		// [assembly: Experimental] makes every type in the assembly experimental, as it does for the compiler.
		var assembly = Assert.Single(diagnostics, d => d.Id == "LIBEXP001");
		AssertAtRegistration(assembly, tree, "app.Map(\"from-lib\"");
		Assert.Equal(DiagnosticSeverity.Error, assembly.Severity);
	}

	private const string NamedByCallerSource = """
		using System;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		namespace App
		{
			[Obsolete("Old group.")]
			public sealed class OldCommands
			{
				public static void Go() { }
			}

			public static class Commands
			{
				[Obsolete("Old handler.")]
				public static void OldCmd() { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
		#pragma warning disable CS0612, CS0618
					app.Map<OldCommands>();
					app.Map("old", Commands.OldCmd);
		#pragma warning restore CS0612, CS0618
				}
			}
		}
		""";

	[Fact]
	public void Symbols_the_call_names_itself_are_left_to_the_compiler()
	{
		var (diagnostics, _, _) = RunGenerator(NamedByCallerSource);

		// The compiler already reports Map<OldCommands>() and the Commands.OldCmd method group at the call.
		Assert.DoesNotContain(diagnostics, d => d.Id is "AGH0036" or "AGH0037");
	}

	private const string RegistrationKindsSource = """
		using System;
		using System.Threading.Tasks;
		using Nullean.Argh;
		using Nullean.Argh.Builder;
		using Nullean.Argh.Middleware;
		using Nullean.Argh.Parsing;

		namespace App
		{
			public enum Mode { Fast, [Obsolete("Use Fast.")] Slow }

			public sealed class GlobalOpts
			{
				public bool Verbose { get; set; }

				[Obsolete("Use --verbose.")]
				public bool Loud { get; set; }
			}

			public sealed class NsCommands
			{
				public static void List(Mode mode = Mode.Fast) { }
			}

			[Obsolete("Use a newer parser.")]
			public sealed class OldParser : IArgumentParser<int>
			{
				public bool TryParse(string raw, ParserOptions options, out int value) => int.TryParse(raw, System.Globalization.NumberStyles.Integer, options.Culture, out value);
			}

			public sealed class GlobalMiddleware : ICommandMiddleware
			{
				[Obsolete("Use the factory.")]
				public GlobalMiddleware() { }

				public ValueTask InvokeAsync(CommandContext context, CommandMiddlewareDelegate next) => next(context);
			}

			public sealed class CommandMiddleware : ICommandMiddleware
			{
				[Obsolete("Old constructor.")]
				public CommandMiddleware() { }

				public ValueTask InvokeAsync(CommandContext context, CommandMiddlewareDelegate next) => next(context);
			}

			public static class Commands
			{
		#pragma warning disable CS0618
				public static void Parse([ArgumentParser(typeof(OldParser))] int n) { }
		#pragma warning restore CS0618

				[Middleware<CommandMiddleware>]
				public static void WithMiddleware() { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
					app.UseGlobalOptions<GlobalOpts>();
					app.UseMiddleware<GlobalMiddleware>();
					app.MapNamespace<NsCommands>("ns");
					app.Map("parse", Commands.Parse);
					app.Map("with-middleware", Commands.WithMiddleware);
				}
			}
		}
		""";

	[Theory]
	[InlineData("app.UseGlobalOptions<GlobalOpts>", "UseGlobalOptions<GlobalOpts>", "GlobalOpts.Loud")]
	[InlineData("app.UseMiddleware<GlobalMiddleware>", "UseMiddleware<GlobalMiddleware>", "GlobalMiddleware.GlobalMiddleware()")]
	[InlineData("app.MapNamespace<NsCommands>", "MapNamespace<NsCommands>", "Mode.Slow")]
	[InlineData("app.Map(\"parse\"", "Map", "OldParser")]
	[InlineData("app.Map(\"with-middleware\"", "Map", "CommandMiddleware.CommandMiddleware()")]
	public void Each_registration_kind_reports_what_its_generated_code_uses(string registration, string method, string symbol)
	{
		var (diagnostics, _, tree) = RunGenerator(RegistrationKindsSource);

		var line = LineOf(tree, registration);
		var usage = Assert.Single(diagnostics, d => d.Id == "AGH0036" && d.Location.GetLineSpan().StartLinePosition.Line == line);
		AssertAtRegistration(usage, tree, registration, method);
		Assert.StartsWith($"'{symbol}' is obsolete", usage.GetMessage());
	}

	private const string PragmaSource = """
		using System;
		using Nullean.Argh;
		using Nullean.Argh.Builder;

		namespace App
		{
			public enum Mode { Fast, [Obsolete("Use Fast.")] Slow }

			public static class Commands
			{
				public static void Run(Mode mode = Mode.Fast) { }
			}

			public static class Program
			{
				public static void Configure(IArghBuilder app)
				{
		#pragma warning disable AGH0036
					app.Map("quiet", Commands.Run);
		#pragma warning restore AGH0036
					app.Map("loud", Commands.Run);
				}
			}
		}
		""";

	[Fact]
	public async Task Pragma_at_the_registration_suppresses_the_diagnostic()
	{
		var (diagnostics, output, tree) = RunGenerator(PragmaSource);
		var usages = diagnostics.Where(d => d.Id == "AGH0036").ToImmutableArray();
		Assert.Equal(2, usages.Length);

		// Generator diagnostics aren't filtered here, so relay them through an analyzer: the analyzer driver applies
		// #pragma the same way the compiler does for generator diagnostics, and reports what it suppressed.
		var withAnalyzers = output.WithAnalyzers(
			ImmutableArray.Create<DiagnosticAnalyzer>(new RelayAnalyzer(usages)),
			new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), null, concurrentAnalysis: false,
				logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true));
		var relayed = (await withAnalyzers.GetAnalyzerDiagnosticsAsync()).Where(d => d.Id == "AGH0036").ToList();

		var quiet = Assert.Single(relayed, d => d.Location.GetLineSpan().StartLinePosition.Line == LineOf(tree, "app.Map(\"quiet\""));
		Assert.True(quiet.IsSuppressed);
		var loud = Assert.Single(relayed, d => d.Location.GetLineSpan().StartLinePosition.Line == LineOf(tree, "app.Map(\"loud\""));
		Assert.False(loud.IsSuppressed);
	}

	/// <summary>Reports the given diagnostics from a compilation action, so the analyzer driver filters them.</summary>
	// Created directly by the test, never loaded by a compiler, so it needs no [DiagnosticAnalyzer] registration.
#pragma warning disable RS1001
	private sealed class RelayAnalyzer(ImmutableArray<Diagnostic> diagnostics) : DiagnosticAnalyzer
#pragma warning restore RS1001
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			diagnostics.Select(d => d.Descriptor).Distinct().ToImmutableArray();

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationAction(c =>
			{
				foreach (var d in diagnostics)
					c.ReportDiagnostic(d);
			});
		}
	}

	private static void AssertAtRegistration(Diagnostic diagnostic, SyntaxTree tree, string registration, string method = "Map")
	{
		// The location must be in the user's syntax tree, so #pragma and [SuppressMessage] apply.
		Assert.Same(tree, diagnostic.Location.SourceTree);
		Assert.Equal(LineOf(tree, registration), diagnostic.Location.GetLineSpan().StartLinePosition.Line);
		Assert.Equal(method, tree.GetText().ToString(diagnostic.Location.SourceSpan));
	}

	private static int LineOf(SyntaxTree tree, string text) =>
		tree.GetText().Lines.GetLineFromPosition(tree.GetText().ToString().IndexOf(text, StringComparison.Ordinal)).LineNumber;

	/// <summary>Compiles <paramref name="source"/> into an in-memory assembly the app can reference.</summary>
	private static MetadataReference CompileLibrary(string assemblyName, string source)
	{
		var library = CSharpCompilation.Create(
			assemblyName,
			new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)) },
			BuildReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		using var stream = new MemoryStream();
		var emitted = library.Emit(stream);
		Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
		return MetadataReference.CreateFromImage(stream.ToArray());
	}

	private static (ImmutableArray<Diagnostic> Diagnostics, Compilation Output, SyntaxTree Tree) RunGenerator(
		string source = AppSource, params MetadataReference[] extraReferences)
	{
		var tree = CSharpSyntaxTree.ParseText(
			SourceText.From(source, Encoding.UTF8),
			new CSharpParseOptions(LanguageVersion.Preview),
			path: "Program.cs");
		var compilation = CSharpCompilation.Create(
			"App",
			new[] { tree },
			BuildReferences().AddRange(extraReferences),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

		var userErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
		Assert.True(userErrors.Count == 0, string.Join(Environment.NewLine, userErrors.Select(e => e.ToString())));

		GeneratorDriver driver = CSharpGeneratorDriver.Create(
			new[] { new CliParserGenerator().AsSourceGenerator() },
			parseOptions: (CSharpParseOptions)tree.Options);
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
		Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
		return (diagnostics, output, tree);
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
